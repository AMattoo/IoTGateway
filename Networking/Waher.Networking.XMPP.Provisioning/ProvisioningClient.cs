﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
#if WINDOWS_UWP
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage.Streams;
#else
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#endif
using Waher.Content;
using Waher.Content.Xml;
using Waher.Events;
using Waher.Networking.XMPP.StanzaErrors;
using Waher.Things;
using Waher.Things.SensorData;

namespace Waher.Networking.XMPP.Provisioning
{
	/// <summary>
	/// Delegate for Token callback methods.
	/// </summary>
	/// <param name="Sender">Sender</param>
	/// <param name="e">Event arguments.</param>
	public delegate void TokenCallback(object Sender, TokenEventArgs e);

	/// <summary>
	/// Delegate for IsFriend callback methods.
	/// </summary>
	/// <param name="Sender">Sender</param>
	/// <param name="e">Event arguments.</param>
	public delegate void IsFriendCallback(object Sender, IsFriendEventArgs e);

	/// <summary>
	/// Delegate for CanRead callback methods.
	/// </summary>
	/// <param name="Sender">Sender</param>
	/// <param name="e">Event arguments.</param>
	public delegate void CanReadCallback(object Sender, CanReadEventArgs e);

	/// <summary>
	/// Delegate for CanControl callback methods.
	/// </summary>
	/// <param name="Sender">Sender</param>
	/// <param name="e">Event arguments.</param>
	public delegate void CanControlCallback(object Sender, CanControlEventArgs e);

	/// <summary>
	/// Implements an XMPP provisioning client interface.
	/// 
	/// The interface is defined in XEP-0324:
	/// http://xmpp.org/extensions/xep-0324.html
	/// </summary>
	public class ProvisioningClient : IDisposable
	{
		private Dictionary<string, CertificateUse> certificates = new Dictionary<string, CertificateUse>();
		private XmppClient client;
		private string provisioningServerAddress;

		/// <summary>
		/// urn:xmpp:iot:provisioning
		/// </summary>
		public const string NamespaceProvisioning = "urn:xmpp:iot:provisioning";

		/// <summary>
		/// Implements an XMPP provisioning client interface.
		/// 
		/// The interface is defined in XEP-0324:
		/// http://xmpp.org/extensions/xep-0324.html
		/// </summary>
		/// <param name="Client">XMPP Client</param>
		/// <param name="ProvisioningServerAddress">Provisioning Server XMPP Address.</param>
		public ProvisioningClient(XmppClient Client, string ProvisioningServerAddress)
		{
			this.client = Client;
			this.provisioningServerAddress = ProvisioningServerAddress;

			this.client.RegisterIqGetHandler("tokenChallenge", NamespaceProvisioning, this.TokenChallengeHandler, true);

			this.client.RegisterMessageHandler("unfriend", NamespaceProvisioning, this.UnfriendHandler, false);
			this.client.RegisterMessageHandler("friend", NamespaceProvisioning, this.FriendHandler, false);

			this.client.OnPresenceSubscribe += Client_OnPresenceSubscribe;
			this.client.OnPresenceUnsubscribe += Client_OnPresenceUnsubscribe;
		}

		private void Client_OnPresenceUnsubscribe(object Sender, PresenceEventArgs e)
		{
			e.Accept();
		}

		private void Client_OnPresenceSubscribe(object Sender, PresenceEventArgs e)
		{
			if (e.From == this.provisioningServerAddress)
				e.Accept();
			else
				this.IsFriend(XmppClient.GetBareJID(e.From), this.CheckIfFriendCallback, e);
		}

		private void CheckIfFriendCallback(object Sender, IsFriendEventArgs e2)
		{
			PresenceEventArgs e = (PresenceEventArgs)e2.State;

			if (e2.Ok && e2.Friend)
			{
				e.Accept();

				RosterItem Item = this.client.GetRosterItem(e.FromBareJID);
				if (Item == null || Item.State == SubscriptionState.None || Item.State == SubscriptionState.From)
					this.client.RequestPresenceSubscription(e.FromBareJID);
			}
			else
				e.Decline();
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.client.UnregisterIqGetHandler("tokenChallenge", NamespaceProvisioning, this.TokenChallengeHandler, true);

			this.client.UnregisterMessageHandler("unfriend", NamespaceProvisioning, this.UnfriendHandler, false);
			this.client.UnregisterMessageHandler("friend", NamespaceProvisioning, this.FriendHandler, false);

			this.client.OnPresenceSubscribe -= Client_OnPresenceSubscribe;
			this.client.OnPresenceUnsubscribe -= Client_OnPresenceUnsubscribe;

		}

		/// <summary>
		/// XMPP Client
		/// </summary>
		public XmppClient Client
		{
			get { return this.client; }
		}

		/// <summary>
		/// Provisioning server XMPP address.
		/// </summary>
		public string ProvisioningServerAddress
		{
			get { return this.provisioningServerAddress; }
		}

		/// <summary>
		/// Gets a token for a certicate. This token can be used to identify services, devices or users. The provisioning server will 
		/// challenge the request, and may choose to challenge it further when it is used, to make sure the sender is the correct holder
		/// of the private certificate.
		/// </summary>
		/// <param name="Certificate">Private certificate. Only the public part will be sent to the provisioning server. But the private
		/// part is required in order to be able to respond to challenges sent by the provisioning server.</param>
		/// <param name="Callback">Callback method called, when token is available.</param>
		/// <param name="State">State object that will be passed on to the callback method.</param>
#if WINDOWS_UWP
		public void GetToken(Certificate Certificate, TokenCallback Callback, object State)
#else
		public void GetToken(X509Certificate2 Certificate, TokenCallback Callback, object State)
#endif
		{
			if (!Certificate.HasPrivateKey)
				throw new ArgumentException("Certificate must have private key.", nameof(Certificate));

#if WINDOWS_UWP
			IBuffer Buffer = Certificate.GetCertificateBlob();
			byte[] Bin;

			CryptographicBuffer.CopyToByteArray(Buffer, out Bin);
			string Base64 = System.Convert.ToBase64String(Bin);
#else
			byte[] Bin = Certificate.Export(X509ContentType.Cert);
			string Base64 = System.Convert.ToBase64String(Bin);
#endif
			this.client.SendIqGet(this.provisioningServerAddress, "<getToken xmlns='urn:xmpp:iot:provisioning'>" + Base64 + "</getToken>",
				this.GetTokenResponse, new object[] { Certificate, Callback, State });
		}

		private void GetTokenResponse(object Sender, IqResultEventArgs e)
		{
			object[] P = (object[])e.State;
#if WINDOWS_UWP
			Certificate Certificate = (Certificate)P[0];
#else
			X509Certificate2 Certificate = (X509Certificate2)P[0];
#endif
			XmlElement E = e.FirstElement;

			if (e.Ok && E != null && E.LocalName == "getTokenChallenge" && E.NamespaceURI == NamespaceProvisioning)
			{
				int SeqNr = XML.Attribute(E, "seqnr", 0);
				string Challenge = E.InnerText;
				byte[] Bin = System.Convert.FromBase64String(Challenge);

#if WINDOWS_UWP
				CryptographicKey Key = PersistedKeyProvider.OpenPublicKeyFromCertificate(Certificate, 
					Certificate.SignatureHashAlgorithmName, CryptographicPadding.RsaPkcs1V15);
				IBuffer Buffer = CryptographicBuffer.CreateFromByteArray(Bin);
				Buffer = CryptographicEngine.Decrypt(Key, Buffer, null);
				CryptographicBuffer.CopyToByteArray(Buffer, out Bin);
				string Response = System.Convert.ToBase64String(Bin);
#else
				Bin = Certificate.GetRSAPrivateKey().Decrypt(Bin, RSAEncryptionPadding.Pkcs1);
				string Response = System.Convert.ToBase64String(Bin);
#endif

				this.client.SendIqGet(this.provisioningServerAddress, "<getTokenChallengeResponse xmlns='urn:xmpp:iot:provisioning' seqnr='" +
					SeqNr.ToString() + "'>" + Response + "</getTokenChallengeResponse>",
					this.GetTokenChallengeResponse, P);
			}
		}

		private void GetTokenChallengeResponse(object Sender, IqResultEventArgs e)
		{
			object[] P = (object[])e.State;
#if WINDOWS_UWP
			Certificate Certificate = (Certificate)P[0];
#else
			X509Certificate2 Certificate = (X509Certificate2)P[0];
#endif
			TokenCallback Callback = (TokenCallback)P[1];
			object State = P[2];
			XmlElement E = e.FirstElement;
			string Token;

			if (e.Ok && E != null && E.LocalName == "getTokenResponse" && E.NamespaceURI == NamespaceProvisioning)
			{
				Token = XML.Attribute(E, "token");

				lock (this.certificates)
				{
					this.certificates[Token] = new CertificateUse(Token, Certificate);
				}
			}
			else
				Token = null;

			TokenEventArgs e2 = new TokenEventArgs(e, State, Token);
			try
			{
				Callback(this, e2);
			}
			catch (Exception ex)
			{
				Log.Critical(ex);
			}
		}

		/// <summary>
		/// Tells the client a token has been used, for instance in a sensor data request or control operation. Tokens must be
		/// refreshed when they are used, to make sure the client only responds to challenges of recently used certificates.
		/// </summary>
		/// <param name="Token">Token</param>
		public void TokenUsed(string Token)
		{
			lock (this.certificates)
			{
				if (this.certificates.TryGetValue(Token, out CertificateUse Use))
					Use.LastUse = DateTime.Now;
			}
		}

		/// <summary>
		/// Tells the client a token has been used, for instance in a sensor data request or control operation. Tokens must be
		/// refreshed when they are used, to make sure the client only responds to challenges of recently used certificates.
		/// </summary>
		/// <param name="Token">Token</param>
		/// <param name="RemoteJid">Remote JID of entity sending the token.</param>
		public void TokenUsed(string Token, string RemoteJid)
		{
			lock (this.certificates)
			{
				if (this.certificates.TryGetValue(Token, out CertificateUse Use))
				{
					Use.LastUse = DateTime.Now;
					Use.RemoteCertificateJid = RemoteJid;
				}
				else
					this.certificates[Token] = new CertificateUse(Token, RemoteJid);
			}
		}

		private void TokenChallengeHandler(object Sender, IqEventArgs e)
		{
			XmlElement E = e.Query;
			string Token = XML.Attribute(E, "token");
			string Challenge = E.InnerText;
			CertificateUse Use;

			lock (this.certificates)
			{
				if (!this.certificates.TryGetValue(Token, out Use) || (DateTime.Now - Use.LastUse).TotalMinutes > 1)
					throw new ForbiddenException("Token not recognized.", e.IQ);
			}

			if (Use.LocalCertificate != null)
			{
				byte[] Bin = System.Convert.FromBase64String(Challenge);

#if WINDOWS_UWP
				CryptographicKey Key = PersistedKeyProvider.OpenPublicKeyFromCertificate(Use.LocalCertificate,
					Use.LocalCertificate.SignatureHashAlgorithmName, CryptographicPadding.RsaPkcs1V15);
				IBuffer Buffer = CryptographicBuffer.CreateFromByteArray(Bin);
				Buffer = CryptographicEngine.Decrypt(Key, Buffer, null);
				CryptographicBuffer.CopyToByteArray(Buffer, out Bin);
				string Response = System.Convert.ToBase64String(Bin);
#else
				Bin = Use.LocalCertificate.GetRSAPrivateKey().Decrypt(Bin, RSAEncryptionPadding.Pkcs1);
				string Response = System.Convert.ToBase64String(Bin);
#endif

				e.IqResult("<tokenChallengeResponse xmlns='" + NamespaceProvisioning + "'>" + Response + "</tokenChallengeResponse>");
			}
			else
				this.client.SendIqGet(Use.RemoteCertificateJid, e.Query.OuterXml, this.ForwardedTokenChallengeResponse, e);
		}

		private void ForwardedTokenChallengeResponse(object Sender, IqResultEventArgs e2)
		{
			IqEventArgs e = (IqEventArgs)e2.State;

			if (e2.Ok)
				e.IqResult(e2.FirstElement.OuterXml);
			else
				e.IqError(e2.ErrorElement.OuterXml);
		}

		/// <summary>
		/// Asks the provisioning server if a JID is a friend or not.
		/// </summary>
		/// <param name="JID">JID</param>
		/// <param name="Callback">Method to call when response is received.</param>
		/// <param name="State">State object to pass to callback method.</param>
		public void IsFriend(string JID, IsFriendCallback Callback, object State)
		{
			this.client.SendIqGet(this.provisioningServerAddress, "<isFriend xmlns='" + NamespaceProvisioning + "' jid='" +
				XML.Encode(JID) + "'/>", this.IsFriendCallback, new object[] { Callback, State });
		}

		private void IsFriendCallback(object Sender, IqResultEventArgs e)
		{
			object[] P = (object[])e.State;
			IsFriendCallback Callback = (IsFriendCallback)P[0];
			object State = P[1];
			string JID;
			bool Result;
			XmlElement E = e.FirstElement;

			if (e.Ok && E != null && E.LocalName == "isFriendResponse" && E.NamespaceURI == NamespaceProvisioning)
			{
				JID = XML.Attribute(E, "jid");
				Result = XML.Attribute(E, "result", false);
			}
			else
			{
				Result = false;
				JID = null;
			}

			IsFriendEventArgs e2 = new IsFriendEventArgs(e, State, JID, Result);
			try
			{
				Callback(this, e2);
			}
			catch (Exception ex)
			{
				Log.Critical(ex);
			}
		}

		private void UnfriendHandler(object Sender, MessageEventArgs e)
		{
			if (e.From == this.provisioningServerAddress)
			{
				string Jid = XML.Attribute(e.Content, "jid");

				if (!string.IsNullOrEmpty(Jid))
					this.client.RequestPresenceUnsubscription(Jid);
			}
		}

		private void FriendHandler(object Sender, MessageEventArgs e)
		{
			if (e.From == this.provisioningServerAddress)
			{
				string Jid = XML.Attribute(e.Content, "jid");

				if (!string.IsNullOrEmpty(Jid))
				{
					this.IsFriend(Jid, (sender, e2) =>
					{
						if (e2.Ok && e2.Friend)
							this.client.RequestPresenceSubscription(Jid);

					}, null);
				}
			}
		}

		/// <summary>
		/// Checks if a readout can be performed.
		/// </summary>
		/// <param name="RequestFromBareJid">Readout request came from this bare JID.</param>
		/// <param name="FieldTypes">Field types requested.</param>
		/// <param name="Nodes">Any nodes included in the request.</param>
		/// <param name="FieldNames">And field names included in the request. If null, all field names are requested.</param>
		/// <param name="ServiceTokens">Any service tokens provided.</param>
		/// <param name="DeviceTokens">Any device tokens provided.</param>
		/// <param name="UserTokens">Any user tokens provided.</param>
		/// <param name="Callback">Method to call when result is received.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		public void CanRead(string RequestFromBareJid, FieldType FieldTypes, IEnumerable<ThingReference> Nodes, IEnumerable<string> FieldNames,
			string[] ServiceTokens, string[] DeviceTokens, string[] UserTokens, CanReadCallback Callback, object State)
		{
			StringBuilder Xml = new StringBuilder();

			Xml.Append("<canRead xmlns='");
			Xml.Append(NamespaceProvisioning);
			Xml.Append("' jid='");
			Xml.Append(XML.Encode(RequestFromBareJid));

			this.AppendTokens(Xml, "serviceToken", ServiceTokens);
			this.AppendTokens(Xml, "deviceToken", DeviceTokens);
			this.AppendTokens(Xml, "userTokens", UserTokens);

			if ((FieldTypes & FieldType.All) == FieldType.All)
				Xml.Append("' all='true");
			else
			{
				if ((FieldTypes & FieldType.Historical) == FieldType.Historical)
				{
					Xml.Append("' historical='true");
					FieldTypes &= ~FieldType.Historical;
				}

				if (FieldTypes.HasFlag(FieldType.Momentary))
					Xml.Append("' momentary='true");

				if (FieldTypes.HasFlag(FieldType.Peak))
					Xml.Append("' peak='true");

				if (FieldTypes.HasFlag(FieldType.Status))
					Xml.Append("' status='true");

				if (FieldTypes.HasFlag(FieldType.Computed))
					Xml.Append("' computed='true");

				if (FieldTypes.HasFlag(FieldType.Identity))
					Xml.Append("' identity='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalSecond))
					Xml.Append("' historicalSecond='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalMinute))
					Xml.Append("' historicalMinute='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalHour))
					Xml.Append("' historicalHour='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalDay))
					Xml.Append("' historicalDay='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalWeek))
					Xml.Append("' historicalWeek='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalMonth))
					Xml.Append("' historicalMonth='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalQuarter))
					Xml.Append("' historicalQuarter='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalYear))
					Xml.Append("' historicalYear='true");

				if (FieldTypes.HasFlag(FieldType.HistoricalOther))
					Xml.Append("' historicalOther='true");
			}

			if (Nodes == null && FieldNames == null)
				Xml.Append("'/>");
			else
			{
				Xml.Append("'>");

				if (Nodes != null)
				{
					foreach (ThingReference Node in Nodes)
					{
						Xml.Append("<node nodeId='");
						Xml.Append(XML.Encode(Node.NodeId));

						if (!string.IsNullOrEmpty(Node.SourceId))
						{
							Xml.Append("' sourceId='");
							Xml.Append(XML.Encode(Node.SourceId));
						}

						if (!string.IsNullOrEmpty(Node.CacheType))
						{
							Xml.Append("' cacheType='");
							Xml.Append(XML.Encode(Node.CacheType));
						}

						Xml.Append("'/>");
					}
				}

				if (FieldNames != null)
				{
					foreach (string FieldName in FieldNames)
					{
						Xml.Append("<field name='");
						Xml.Append(XML.Encode(FieldName));
						Xml.Append("'/>");
					}
				}

				Xml.Append("</canRead>");
			}

			this.client.SendIqGet(this.provisioningServerAddress, Xml.ToString(), (sender, e) =>
			{
				XmlElement E = e.FirstElement;
				List<ThingReference> Nodes2 = null;
				List<string> Fields2 = null;
				FieldType FieldTypes2 = (FieldType)0;
				string Jid = string.Empty;
				string NodeId;
				string SourceId;
				string CacheType;
				bool b;
				bool CanRead;

				if (e.Ok && E.LocalName == "canReadResponse" && E.NamespaceURI == NamespaceProvisioning)
				{
					CanRead = XML.Attribute(E, "result", false);

					foreach (XmlAttribute Attr in E.Attributes)
					{
						switch (Attr.Name)
						{
							case "jid":
								Jid = Attr.Value;
								break;

							case "all":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.All;
								break;

							case "historical":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.Historical;
								break;

							case "momentary":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.Momentary;
								break;

							case "peak":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.Peak;
								break;

							case "status":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.Status;
								break;

							case "computed":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.Computed;
								break;

							case "identity":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.Identity;
								break;

							case "historicalSecond":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalSecond;
								break;

							case "historicalMinute":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalMinute;
								break;

							case "historicalHour":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalMonth;
								break;

							case "historicalDay":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalDay;
								break;

							case "historicalWeek":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalWeek;
								break;

							case "historicalMonth":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalMonth;
								break;

							case "historicalQuarter":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalQuarter;
								break;

							case "historicalYear":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalYear;
								break;

							case "historicalOther":
								if (CommonTypes.TryParse(Attr.Value, out b) && b)
									FieldTypes2 |= FieldType.HistoricalOther;
								break;
						}
					}

					foreach (XmlNode N in E.ChildNodes)
					{
						switch (N.LocalName)
						{
							case "node":
								if (Nodes2 == null)
									Nodes2 = new List<ThingReference>();

								E = (XmlElement)N;
								NodeId = XML.Attribute(E, "nodeId");
								SourceId = XML.Attribute(E, "sourceId");
								CacheType = XML.Attribute(E, "cacheType");

								Nodes2.Add(new ThingReference(NodeId, SourceId, CacheType));
								break;

							case "field":
								if (Fields2 == null)
									Fields2 = new List<string>();

								Fields2.Add(XML.Attribute((XmlElement)N, "name"));
								break;
						}
					}

				}
				else
					CanRead = false;

				CanReadEventArgs e2 = new CanReadEventArgs(e, State, Jid, CanRead, FieldTypes2, Nodes2?.ToArray(), Fields2?.ToArray());

				try
				{
					Callback(this, e2);
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}

			}, null);
		}

		private void AppendTokens(StringBuilder Xml, string AttributeName, string[] Tokens)
		{
			if (Tokens != null && Tokens.Length > 0)
			{
				Xml.Append("' ");
				Xml.Append(AttributeName);
				Xml.Append("='");

				bool First = true;

				foreach (string Token in Tokens)
				{
					if (First)
						First = false;
					else
						Xml.Append(' ');

					Xml.Append(Token);
				}
			}
		}

		/// <summary>
		/// Checks if a control operation can be performed.
		/// </summary>
		/// <param name="RequestFromBareJid">Readout request came from this bare JID.</param>
		/// <param name="Nodes">Any nodes included in the request.</param>
		/// <param name="ParameterNames">And parameter names included in the request. If null, all parameter names are requested.</param>
		/// <param name="ServiceTokens">Any service tokens provided.</param>
		/// <param name="DeviceTokens">Any device tokens provided.</param>
		/// <param name="UserTokens">Any user tokens provided.</param>
		/// <param name="Callback">Method to call when result is received.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		public void CanControl(string RequestFromBareJid, IEnumerable<ThingReference> Nodes, IEnumerable<string> ParameterNames,
			string[] ServiceTokens, string[] DeviceTokens, string[] UserTokens, CanControlCallback Callback, object State)
		{
			StringBuilder Xml = new StringBuilder();

			Xml.Append("<canControl xmlns='");
			Xml.Append(NamespaceProvisioning);
			Xml.Append("' jid='");
			Xml.Append(XML.Encode(RequestFromBareJid));

			this.AppendTokens(Xml, "serviceToken", ServiceTokens);
			this.AppendTokens(Xml, "deviceToken", DeviceTokens);
			this.AppendTokens(Xml, "userTokens", UserTokens);

			if (Nodes == null && ParameterNames == null)
				Xml.Append("'/>");
			else
			{
				Xml.Append("'>");

				if (Nodes != null)
				{
					foreach (ThingReference Node in Nodes)
					{
						Xml.Append("<node nodeId='");
						Xml.Append(XML.Encode(Node.NodeId));

						if (!string.IsNullOrEmpty(Node.SourceId))
						{
							Xml.Append("' sourceId='");
							Xml.Append(XML.Encode(Node.SourceId));
						}

						if (!string.IsNullOrEmpty(Node.CacheType))
						{
							Xml.Append("' cacheType='");
							Xml.Append(XML.Encode(Node.CacheType));
						}

						Xml.Append("'/>");
					}
				}

				if (ParameterNames != null)
				{
					foreach (string ParameterName in ParameterNames)
					{
						Xml.Append("<parameter name='");
						Xml.Append(XML.Encode(ParameterName));
						Xml.Append("'/>");
					}
				}

				Xml.Append("</canControl>");
			}

			this.client.SendIqGet(this.provisioningServerAddress, Xml.ToString(), (sender, e) =>
			{
				XmlElement E = e.FirstElement;
				List<ThingReference> Nodes2 = null;
				List<string> ParameterNames2 = null;
				string Jid = string.Empty;
				string NodeId;
				string SourceId;
				string CacheType;
				bool CanControl;

				if (e.Ok && E.LocalName == "canControlResponse" && E.NamespaceURI == NamespaceProvisioning)
				{
					CanControl = XML.Attribute(E, "result", false);

					foreach (XmlAttribute Attr in E.Attributes)
					{
						if (Attr.Name == "jid")
							Jid = Attr.Value;
					}

					foreach (XmlNode N in E.ChildNodes)
					{
						switch (N.LocalName)
						{
							case "node":
								if (Nodes2 == null)
									Nodes2 = new List<ThingReference>();

								E = (XmlElement)N;
								NodeId = XML.Attribute(E, "nodeId");
								SourceId = XML.Attribute(E, "sourceId");
								CacheType = XML.Attribute(E, "cacheType");

								Nodes2.Add(new ThingReference(NodeId, SourceId, CacheType));
								break;

							case "parameter":
								if (ParameterNames2 == null)
									ParameterNames2 = new List<string>();

								ParameterNames2.Add(XML.Attribute((XmlElement)N, "name"));
								break;
						}
					}

				}
				else
					CanControl = false;

				CanControlEventArgs e2 = new CanControlEventArgs(e, State, Jid, CanControl,
					Nodes2?.ToArray(), ParameterNames2?.ToArray());

				try
				{
					Callback(this, e2);
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}

			}, null);
		}

	}
}
