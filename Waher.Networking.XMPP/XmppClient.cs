﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Threading;
using Waher.Networking.Sniffers;
using Waher.Networking.XMPP.Authentication;
using Waher.Networking.XMPP.AuthenticationErrors;
using Waher.Networking.XMPP.StanzaErrors;
using Waher.Networking.XMPP.StreamErrors;
using Waher.Networking.XMPP.DataForms;

namespace Waher.Networking.XMPP
{
	/// <summary>
	/// Connection error event handler delegate.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="Exception">Information about error received.</param>
	public delegate void XmppExceptionEventHandler(XmppClient Sender, Exception Exception);

	/// <summary>
	/// Event handler delegate for state change events.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="NewState">New state reported.</param>
	public delegate void StateChangedEventHandler(XmppClient Sender, XmppState NewState);

	/// <summary>
	/// Delegate for IQ result callback methods.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="e">Event arguments.</param>
	public delegate void IqResultEventHandler(XmppClient Sender, IqResultEventArgs e);

	/// <summary>
	/// Delegate for IQ get and set handlers.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="e">Event arguments.</param>
	public delegate void IqEventHandler(XmppClient Sender, IqEventArgs e);

	/// <summary>
	/// Delegate for Presence events.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="e">Event arguments.</param>
	public delegate void PresenceEventHandler(XmppClient Sender, PresenceEventArgs e);

	/// <summary>
	/// Delegate for Message events.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="e">Event arguments.</param>
	public delegate void MessageEventHandler(XmppClient Sender, MessageEventArgs e);

	/// <summary>
	/// Delegate for Roster Item events.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="Item">Roster Item</param>
	public delegate void RosterItemEventHandler(XmppClient Sender, RosterItem Item);

	/// <summary>
	/// Delegate for Dynamic Data Form events.
	/// </summary>
	/// <param name="Sender">Sender of event.</param>
	/// <param name="e">Event arguments.</param>
	public delegate void DynamicDataFormEventHandler(XmppClient Sender, DynamicDataFormEventArgs e);

	/// <summary>
	/// Manages an XMPP client connection. Implements XMPP, as defined in
	/// https://tools.ietf.org/html/rfc6120
	/// https://tools.ietf.org/html/rfc6121
	/// https://tools.ietf.org/html/rfc6122
	/// 
	/// Extensions supported directly by client object:
	/// 
	/// XEP-0077: In-band registration: http://xmpp.org/extensions/xep-0077.html
	/// </summary>
	public class XmppClient : Sniffable, IDisposable
	{
		/// <summary>
		/// urn:ietf:params:xml:ns:xmpp-streams
		/// </summary>
		public const string NamespaceXmppStreams = "urn:ietf:params:xml:ns:xmpp-streams";

		/// <summary>
		/// urn:ietf:params:xml:ns:xmpp-stanzas
		/// </summary>
		public const string NamespaceXmppStanzas = "urn:ietf:params:xml:ns:xmpp-stanzas";

		/// <summary>
		/// urn:ietf:params:xml:ns:xmpp-sasl
		/// </summary>
		public const string NamespaceXmppSasl = "urn:ietf:params:xml:ns:xmpp-sasl";

		/// <summary>
		/// jabber:iq:register
		/// </summary>
		public const string NamespaceRegister = "jabber:iq:register";

		/// <summary>
		/// jabber:x:data
		/// </summary>
		public const string NamespaceData = "jabber:x:data";

		/// <summary>
		/// jabber:iq:roster
		/// </summary>
		public const string NamespaceRoster = "jabber:iq:roster";

		/// <summary>
		/// urn:xmpp:xdata:dynamic
		/// </summary>
		public const string NamespaceDynamicForms = "urn:xmpp:xdata:dynamic";

		private const int BufferSize = 16384;
		private const int KeepAliveTimeSeconds = 30;

		private X509CertificateCollection clientCertificates = new X509CertificateCollection();
		private LinkedList<KeyValuePair<byte[], EventHandler>> outputQueue = new LinkedList<KeyValuePair<byte[], EventHandler>>();
		private Dictionary<string, bool> authenticationMechanisms = new Dictionary<string, bool>();
		private Dictionary<string, bool> compressionMethods = new Dictionary<string, bool>();
		private Dictionary<uint, KeyValuePair<IqResultEventHandler, object>> callbackMethods = new Dictionary<uint, KeyValuePair<IqResultEventHandler, object>>();
		private Dictionary<string, IqEventHandler> iqGetHandlers = new Dictionary<string, IqEventHandler>();
		private Dictionary<string, IqEventHandler> iqSetHandlers = new Dictionary<string, IqEventHandler>();
		private Dictionary<string, MessageEventHandler> messageHandlers = new Dictionary<string, MessageEventHandler>();
		private Dictionary<string, bool> clientFeatures = new Dictionary<string, bool>();
		private Dictionary<string, RosterItem> roster = new Dictionary<string, RosterItem>();
		private byte[] buffer = new byte[BufferSize];
		private AuthenticationMethod authenticationMethod = null;
		private TcpClient client = null;
		private Stream stream = null;
		private Timer secondTimer = null;
		private DateTime nextPing = DateTime.MinValue;
		private UTF8Encoding encoding = new UTF8Encoding(false, false);
		private StringBuilder fragment = new StringBuilder();
		private XmppState state;
		private object synchObject = new object();
		private Availability currentAvailability = Availability.Online;
		private string customPresenceXml = string.Empty;
		private KeyValuePair<string, string>[] customPresenceStatus = new KeyValuePair<string, string>[0];
		private string host;
		private string language;
		private string domain;
		private string baseJid;
		private string fullJid;
		private string resource = string.Empty;
		private string userName;
		private string password;
		private string streamId;
		private string streamHeader;
		private string streamFooter;
		private string formSignatureKey;
		private string formSignatureSecret;
		private double version;
		private int port;
		private int keepAliveSeconds;
		private int inputState = 0;
		private int inputDepth = 0;
		private uint seqnr = 0;
		private bool trustServer = false;
		private bool isWriting = false;
		private bool canRegister = false;
		private bool hasRegistered = false;
		private bool hasRoster = false;
		private bool setPresence = false;
		private bool requestRosterOnStartup = true;
		private bool allowedToRegistered = false;

		/// <summary>
		/// Manages an XMPP client connection. Implements XMPP, as defined in
		/// https://tools.ietf.org/html/rfc6120
		/// https://tools.ietf.org/html/rfc6121
		/// https://tools.ietf.org/html/rfc6122
		/// 
		/// Extensions supported directly by client object:
		/// 
		/// XEP-0077: In-band registration: http://xmpp.org/extensions/xep-0077.html
		/// </summary>
		/// <param name="Host">Host name or IP address of XMPP server.</param>
		/// <param name="Port">Port to connect to.</param>
		/// <param name="Tls">If TLS is used to encrypt communication.</param>
		/// <param name="UserName">User Name</param>
		/// <param name="Password">Password</param>
		/// <param name="Language">Language Code, according to RFC 5646.</param>
		/// <param name="ClientCertificates">Any client certificates.</param>
		public XmppClient(string Host, int Port, string UserName, string Password, string Language, params X509Certificate[] ClientCertificates)
		{
			this.host = Host;
			this.port = Port;
			this.userName = UserName;
			this.password = Password;
			this.language = Language;
			this.state = XmppState.Connecting;
			this.clientCertificates.AddRange(ClientCertificates);

			this.RegisterDefaultHandlers();

			this.client = new TcpClient();
			this.client.BeginConnect(Host, Port, this.ConnectCallback, null);
		}

		private void RegisterDefaultHandlers()
		{
			this.RegisterIqSetHandler("query", NamespaceRoster, this.RosterPush, true);
			this.RegisterMessageHandler("updated", NamespaceDynamicForms, this.DynamicFormUpdated, true);

			this.clientFeatures["urn:xmpp:xdata:signature:oauth1"] = true;
			this.clientFeatures["http://jabber.org/protocols/xdata-validate"] = true;
			this.clientFeatures[NamespaceData] = true;
		}

		private void ConnectCallback(IAsyncResult ar)
		{
			try
			{
				this.client.EndConnect(ar);
			}
			catch (Exception ex)
			{
				this.ConnectionError(ex);
				return;
			}

			this.stream = new NetworkStream(this.client.Client, false);

			this.State = XmppState.StreamNegotiation;
			this.baseJid = this.userName + "@" + this.host;
			this.BeginWrite("<?xml version='1.0'?><stream:stream from='" + XmlEncode(this.baseJid) + "' to='" + XmlEncode(this.host) +
				"' version='1.0' xml:lang='" + XmlEncode(this.language) + "' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", null);

			this.ResetState(false);
			this.BeginRead();
		}

		private void ResetState(bool Authenticated)
		{
			this.inputState = 0;
			this.inputDepth = 0;
			this.canRegister = false;
			this.setPresence = false;

			if (!Authenticated)
			{
				this.authenticationMethod = null;
				this.authenticationMechanisms.Clear();
			}

			this.compressionMethods.Clear();
			this.callbackMethods.Clear();
		}

		private void ConnectionError(Exception ex)
		{
			XmppExceptionEventHandler h = this.OnConnectionError;
			if (h != null)
			{
				try
				{
					h(this, ex);
				}
				catch (Exception ex2)
				{
					Exception(ex2);
				}
			}

			this.Error(ex);

			this.inputState = -1;
			if (this.stream != null)
			{
				this.stream.Close();
				this.stream = null;

				this.client.Close();
				this.client = null;
			}

			this.State = XmppState.Error;
		}

		private void Error(Exception ex)
		{
			XmppExceptionEventHandler h = this.OnError;
			if (h != null)
			{
				try
				{
					h(this, ex);
				}
				catch (Exception ex2)
				{
					Exception(ex2);
				}
			}
		}

		/// <summary>
		/// Event raised when a connection to a broker could not be made.
		/// </summary>
		public event XmppExceptionEventHandler OnConnectionError = null;

		/// <summary>
		/// Event raised when an error was encountered.
		/// </summary>
		public event XmppExceptionEventHandler OnError = null;

		/// <summary>
		/// Host or IP address of XMPP server.
		/// </summary>
		public string Host
		{
			get { return this.host; }
		}

		/// <summary>
		/// Port number to connect to.
		/// </summary>
		public int Port
		{
			get { return this.port; }
		}

		/// <summary>
		/// If server should be trusted, regardless if the operating system could validate its certificate or not.
		/// </summary>
		public bool TrustServer
		{
			get { return this.trustServer; }
			set { this.trustServer = value; }
		}

		/// <summary>
		/// Current state of connection.
		/// </summary>
		public XmppState State
		{
			get { return this.state; }
			internal set
			{
				if (this.state != value)
				{
					this.state = value;

					this.Information("State changed to " + value.ToString());

					StateChangedEventHandler h = this.OnStateChanged;
					if (h != null)
					{
						try
						{
							h(this, value);
						}
						catch (Exception ex)
						{
							Exception(ex);
						}
					}
				}
			}
		}

		/// <summary>
		/// Event raised whenever the internal state of the connection changes.
		/// </summary>
		public event StateChangedEventHandler OnStateChanged = null;

		/// <summary>
		/// Closes the connection and disposes of all resources.
		/// </summary>
		public void Dispose()
		{
			if (this.state == XmppState.Connected || this.state == XmppState.FetchingRoster || this.state == XmppState.SettingPresence)
				this.BeginWrite(this.streamFooter, this.CleanUp);
			else
				this.CleanUp(this, new EventArgs());
		}

		/// <summary>
		/// Closes the connection the hard way. This might disrupt stream processing, but can simulate a lost connection. To close the connection
		/// softly, call the <see cref="Dispose"/> method.
		/// 
		/// Note: After turning the connection hard-offline, you can reconnect to the server calling the <see cref="Reconnect"/> method.
		/// </summary>
		public void HardOffline()
		{
			this.CleanUp(this, new EventArgs());
		}

		private void CleanUp(object Sender, EventArgs e)
		{
			this.State = XmppState.Offline;

			if (this.outputQueue != null)
			{
				lock (this.outputQueue)
				{
					this.outputQueue.Clear();
				}
			}

			if (this.authenticationMechanisms != null)
				this.authenticationMechanisms.Clear();

			if (this.compressionMethods != null)
				this.compressionMethods.Clear();

			if (this.callbackMethods != null)
			{
				lock (this.synchObject)
				{
					this.callbackMethods.Clear();
				}
			}

			if (this.secondTimer != null)
			{
				this.secondTimer.Dispose();
				this.secondTimer = null;
			}

			if (this.stream != null)
			{
				this.stream.Dispose();
				this.stream = null;
			}

			if (this.client != null)
			{
				this.client.Close();
				this.client = null;
			}
		}

		/// <summary>
		/// Reconnects a client after an error or if it's offline. Reconnecting, instead of creating a completely new connection,
		/// saves time. It binds to the same resource provided earlier, and avoids fetching the roster.
		/// </summary>
		public void Reconnect()
		{
			if (this.state == XmppState.Error || this.state == XmppState.Offline)
			{
				this.state = XmppState.Connecting;
				this.client = new TcpClient();
				this.client.BeginConnect(Host, Port, this.ConnectCallback, null);
			}
			else
				throw new XmppException("Client not offline.");
		}

		private void BeginWrite(string Xml, EventHandler Callback)
		{
			TransmitText(Xml);

			byte[] Packet = this.encoding.GetBytes(Xml);

			lock (this.outputQueue)
			{
				if (this.isWriting)
					this.outputQueue.AddLast(new KeyValuePair<byte[], EventHandler>(Packet, Callback));
				else
					this.DoBeginWriteLocked(Packet, Callback);
			}
		}

		private void DoBeginWriteLocked(byte[] Packet, EventHandler Callback)
		{
			this.stream.BeginWrite(Packet, 0, Packet.Length, this.EndWrite, Callback);
			this.isWriting = true;
		}

		private void EndWrite(IAsyncResult ar)
		{
			if (this.stream == null)
				return;

			try
			{
				this.stream.EndWrite(ar);
				this.nextPing = DateTime.Now.AddMilliseconds(this.keepAliveSeconds * 500);

				EventHandler h = (EventHandler)ar.AsyncState;
				if (h != null)
				{
					try
					{
						h(this, new EventArgs());
					}
					catch (Exception ex)
					{
						Exception(ex);
					}
				}

				lock (this.outputQueue)
				{
					LinkedListNode<KeyValuePair<byte[], EventHandler>> Next = this.outputQueue.First;

					if (Next == null)
						this.isWriting = false;
					else
					{
						this.outputQueue.RemoveFirst();
						this.DoBeginWriteLocked(Next.Value.Key, Next.Value.Value);
					}
				}
			}
			catch (Exception ex)
			{
				this.ConnectionError(ex);

				lock (this.outputQueue)
				{
					this.outputQueue.Clear();
					this.isWriting = false;
				}
			}
		}

		private void BeginRead()
		{
			this.stream.BeginRead(this.buffer, 0, BufferSize, this.EndRead, null);
		}

		private void EndRead(IAsyncResult ar)
		{
			string s;
			int NrRead;

			if (this.stream == null)
				return;

			try
			{
				NrRead = this.stream.EndRead(ar);
				if (NrRead > 0)
				{
					s = this.encoding.GetString(this.buffer, 0, NrRead);
					ReceiveText(s);

					if (this.ParseIncoming(s))
						this.stream.BeginRead(this.buffer, 0, BufferSize, this.EndRead, null);
				}
			}
			catch (Exception ex)
			{
				this.ConnectionError(ex);
				return;
			}
		}

		private bool ParseIncoming(string s)
		{
			bool Result = true;

			foreach (char ch in s)
			{
				switch (this.inputState)
				{
					case 0:		// Waiting for <?
						if (ch == '<')
						{
							this.fragment.Append(ch);
							this.inputState++;
						}
						else if (ch > ' ')
						{
							this.inputState = -1;
							if (this.stream != null)
							{
								this.stream.Close();
								this.stream = null;

								this.client.Close();
								this.client = null;
							}
							this.State = XmppState.Error;
							return false;
						}
						break;

					case 1:		// Waiting for ? or >
						this.fragment.Append(ch);
						if (ch == '?')
							this.inputState++;
						else if (ch == '>')
						{
							this.inputState = 5;
							this.inputDepth = 1;
							this.ProcessStream(this.fragment.ToString());
							this.fragment.Clear();
						}
						break;

					case 2:		// Waiting for ?>
						this.fragment.Append(ch);
						if (ch == '>')
							this.inputState++;
						break;

					case 3:		// Waiting for <stream
						this.fragment.Append(ch);
						if (ch == '<')
							this.inputState++;
						else if (ch > ' ')
						{
							this.inputState = -1;
							if (this.stream != null)
							{
								this.stream.Close();
								this.stream = null;

								this.client.Close();
								this.client = null;
							}
							this.State = XmppState.Error;
							return false;
						}
						break;

					case 4:		// Waiting for >
						this.fragment.Append(ch);
						if (ch == '>')
						{
							this.inputState++;
							this.inputDepth = 1;
							this.ProcessStream(this.fragment.ToString());
							this.fragment.Clear();
						}
						break;

					case 5:	// Waiting for <
						if (ch == '<')
						{
							this.fragment.Append(ch);
							this.inputState++;
						}

						else if (this.inputDepth > 1)
							this.fragment.Append(ch);
						else if (ch > ' ')
						{
							this.inputState = -1;
							if (this.stream != null)
							{
								this.stream.Close();
								this.stream = null;

								this.client.Close();
								this.client = null;
							}
							this.State = XmppState.Error;
							return false;
						}
						break;

					case 6:	// Second character in tag
						this.fragment.Append(ch);
						if (ch == '/')
							this.inputState++;
						else
							this.inputState += 2;
						break;

					case 7:	// Waiting for end of closing tag
						this.fragment.Append(ch);
						if (ch == '>')
						{
							this.inputDepth--;
							if (this.inputDepth < 1)
							{
								this.inputState = -1;
								if (this.stream != null)
								{
									this.stream.Close();
									this.stream = null;

									this.client.Close();
									this.client = null;
								}
								this.State = XmppState.Offline;
								return false;
							}
							else
							{
								if (this.inputDepth == 1)
								{
									if (!this.ProcessFragment(this.fragment.ToString()))
										Result = false;

									this.fragment.Clear();
								}

								if (this.inputState != 0)
									this.inputState = 5;
							}
						}
						break;

					case 8:	// Wait for end of start tag
						this.fragment.Append(ch);
						if (ch == '>')
						{
							this.inputDepth++;
							this.inputState = 5;
						}
						else if (ch == '/')
							this.inputState++;
						break;

					case 9:	// Check for end of childless tag.
						this.fragment.Append(ch);
						if (ch == '>')
						{
							if (this.inputDepth == 1)
							{
								if (!this.ProcessFragment(this.fragment.ToString()))
									Result = false;

								this.fragment.Clear();
							}

							if (this.inputState != 0)
								this.inputState = 5;
						}
						else
							this.inputState--;
						break;

					default:
						break;
				}
			}

			return Result;
		}

		/// <summary>
		/// Encodes a string for use in XML.
		/// </summary>
		/// <param name="s">String</param>
		/// <returns>XML-encoded string.</returns>
		public static string XmlEncode(string s)
		{
			if (s.IndexOfAny(specialCharacters) < 0)
				return s;

			return s.
				Replace("&", "&amp;").
				Replace("<", "&lt;").
				Replace(">", "&gt;").
				Replace("\"", "&quot;").
				Replace("'", "&apos;");
		}

		/// <summary>
		/// Decodes a string used in XML.
		/// </summary>
		/// <param name="s">String</param>
		/// <returns>XML-decoded string.</returns>
		public static string XmlDecode(string s)
		{
			if (s.IndexOf('&') < 0)
				return s;

			return s.
				Replace("&apos;", "'").
				Replace("&qout;", "\"").
				Replace("&lt;", "<").
				Replace("&gt;", ">").
				Replace("&amp;", "&");
		}

		/// <summary>
		/// Gets the value of an XML attribute.
		/// </summary>
		/// <param name="E">XML Element</param>
		/// <param name="Name">Name of attribute</param>
		/// <returns>Value of attribute, if found, or the empty string, if not found.</returns>
		public static string XmlAttribute(XmlElement E, string Name)
		{
			if (E.HasAttribute(Name))
				return E.GetAttribute(Name);
			else
				return string.Empty;
		}

		/// <summary>
		/// Gets the value of an XML attribute.
		/// </summary>
		/// <param name="E">XML Element</param>
		/// <param name="Name">Name of attribute</param>
		/// <param name="DefaultValue">Default value.</param>
		/// <returns>Value of attribute, if found, or the default value, if not found.</returns>
		public static string XmlAttribute(XmlElement E, string Name, string DefaultValue)
		{
			if (E.HasAttribute(Name))
				return E.GetAttribute(Name);
			else
				return DefaultValue;
		}

		/// <summary>
		/// Gets the value of an XML attribute.
		/// </summary>
		/// <param name="E">XML Element</param>
		/// <param name="Name">Name of attribute</param>
		/// <param name="DefaultValue">Default value.</param>
		/// <returns>Value of attribute, if found, or the default value, if not found.</returns>
		public static int XmlAttribute(XmlElement E, string Name, int DefaultValue)
		{
			int Result;

			if (E.HasAttribute(Name))
			{
				if (int.TryParse(E.GetAttribute(Name), out Result))
					return Result;
				else
					return DefaultValue;
			}
			else
				return DefaultValue;
		}

		/// <summary>
		/// Gets the value of an XML attribute.
		/// </summary>
		/// <param name="E">XML Element</param>
		/// <param name="Name">Name of attribute</param>
		/// <param name="DefaultValue">Default value.</param>
		/// <returns>Value of attribute, if found, or the default value, if not found.</returns>
		public static double XmlAttribute(XmlElement E, string Name, double DefaultValue)
		{
			double Result;

			if (E.HasAttribute(Name))
			{
				if (double.TryParse(E.GetAttribute(Name).Replace(".", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator), out Result))
					return Result;
				else
					return DefaultValue;
			}
			else
				return DefaultValue;
		}

		private static readonly char[] specialCharacters = new char[] { '<', '>', '&', '"', '\'' };

		private void ProcessStream(string Xml)
		{
			try
			{
				int i = Xml.IndexOf("?>");
				if (i >= 0)
					Xml = Xml.Substring(i + 2).TrimStart();

				this.streamHeader = Xml;

				i = Xml.IndexOf(":stream");
				if (i < 0)
					this.streamFooter = "</stream>";
				else
					this.streamFooter = "</" + Xml.Substring(1, i - 1) + ":stream>";

				XmlDocument Doc = new XmlDocument();
				Doc.LoadXml(Xml + this.streamFooter);

				if (Doc.DocumentElement.LocalName != "stream")
					throw new XmppException("Invalid stream.", Doc.DocumentElement);

				XmlElement Stream = Doc.DocumentElement;

				this.version = XmlAttribute(Stream, "version", 0.0);
				this.streamId = XmlAttribute(Stream, "id");
				this.domain = XmlAttribute(Stream, "from");
				this.baseJid = this.userName + "@" + this.domain;

				if (this.version < 1.0)
					throw new XmppException("Version not supported.", Stream);
			}
			catch (Exception ex)
			{
				this.ConnectionError(ex);
			}
		}

		private bool ProcessFragment(string Xml)
		{
			XmlDocument Doc;
			XmlElement E;

			try
			{
				Doc = new XmlDocument();
				Doc.LoadXml(this.streamHeader + Xml + this.streamFooter);

				foreach (XmlNode N in Doc.DocumentElement.ChildNodes)
				{
					E = N as XmlElement;
					if (E == null)
						continue;

					switch (E.LocalName)
					{
						case "iq":
							string Type = XmlAttribute(E, "type");
							string Id = XmlAttribute(E, "id");
							string To = XmlAttribute(E, "to");
							string From = XmlAttribute(E, "from");
							switch (Type)
							{
								case "get":
									this.ProcessIq(this.iqGetHandlers, new IqEventArgs(this, E, Id, To, From));
									break;

								case "set":
									this.ProcessIq(this.iqSetHandlers, new IqEventArgs(this, E, Id, To, From));
									break;

								case "result":
								case "error":
									uint SeqNr;
									IqResultEventHandler Callback;
									object State;
									KeyValuePair<IqResultEventHandler, object> Rec;
									bool Ok = (Type == "result");

									if (uint.TryParse(Id, out SeqNr))
									{
										lock (this.synchObject)
										{
											if (this.callbackMethods.TryGetValue(SeqNr, out Rec))
											{
												Callback = Rec.Key;
												State = Rec.Value;

												this.callbackMethods.Remove(SeqNr);
											}
											else
											{
												Callback = null;
												State = null;
											}
										}

										if (Callback != null)
										{
											try
											{
												Callback(this, new IqResultEventArgs(E, Id, To, From, Ok, State));
											}
											catch (Exception ex)
											{
												Exception(ex);
											}
										}
									}
									break;
							}
							break;

						case "message":
							this.ProcessMessage(new MessageEventArgs(this, E));
							break;

						case "presence":
							this.ProcessPresence(new PresenceEventArgs(this, E));
							break;

						case "features":
							if (E.FirstChild == null)
								this.AdvanceUntilConnected();
							else
							{
								foreach (XmlNode N2 in E.ChildNodes)
								{
									switch (N2.LocalName)
									{
										case "starttls":
											this.BeginWrite("<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls'/>", null);
											return true;

										case "mechanisms":
											foreach (XmlNode N3 in N2.ChildNodes)
											{
												if (N3.LocalName == "mechanism")
													this.authenticationMechanisms[N3.InnerText.Trim().ToUpper()] = true;
											}
											break;

										case "compression":
											foreach (XmlNode N3 in N2.ChildNodes)
											{
												if (N3.LocalName == "method")
													this.compressionMethods[N3.InnerText.Trim().ToUpper()] = true;
											}
											break;

										case "auth":
											this.StartAuthentication();
											break;

										case "register":
											this.canRegister = true;
											break;

										case "bind":
											this.State = XmppState.Binding;
											if (string.IsNullOrEmpty(this.resource))
												this.IqSet(this.domain, "<bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/>", this.BindResult, null);
											else
											{
												this.IqSet(this.domain, "<bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>" +
													XmlEncode(this.resource) + "</resource></bind>", this.BindResult, null);
											}
											break;

										case "session":
											break;

										default:
											break;
									}
								}
							}
							break;

						case "proceed":
							this.State = XmppState.StartingEncryption;

							SslStream SslStream = new SslStream(this.stream, true, this.ValidateCertificate);
							this.stream = SslStream;

							SslStream.BeginAuthenticateAsClient(this.host, this.clientCertificates, SslProtocols.Tls, true, this.EndAuthenticateAsClient, null);
							return false;

						case "failure":
							if (this.authenticationMethod != null)
							{
								if (this.canRegister && !this.hasRegistered && this.allowedToRegistered)
								{
									this.hasRegistered = true;
									this.IqGet(this.domain, "<query xmlns='" + NamespaceRegister + "'/>", this.RegistrationFormReceived, null);
									break;
								}
								else if (E.FirstChild == null)
									throw new XmppException("Unable to authenticate user.", E);
								else
									throw GetSaslExceptionObject(E);
							}
							else
							{
								if (E.FirstChild == null)
									throw new XmppException("Unable to start TLS negotiation.", E);
								else
									throw GetStreamExceptionObject(E);
							}

						case "challenge":
							if (this.authenticationMethod == null)
								throw new XmppException("No authentication method selected.", E);
							else
							{
								string Response = this.authenticationMethod.Challenge(E.InnerText, this);
								this.BeginWrite("<response xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>" + Response + "</response>", null);
							}
							break;

						case "error":
							throw GetStreamExceptionObject(E);

						case "success":
							if (this.authenticationMethod == null)
								throw new XmppException("No authentication method selected.", E);
							else
							{
								if (this.authenticationMethod.CheckSuccess(E.InnerText, this))
								{
									this.ResetState(true);
									this.BeginWrite("<?xml version='1.0'?><stream:stream from='" + XmlEncode(this.baseJid) + "' to='" + XmlEncode(this.host) +
										"' version='1.0' xml:lang='" + XmlEncode(this.language) + "' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", null);
								}
								else
									throw new XmppException("Server authentication rejected by client.", E);
							}
							break;

						default:
							break;
					}
				}
			}
			catch (Exception ex)
			{
				this.ConnectionError(ex);
				return false;
			}

			return true;
		}

		private void ProcessMessage(MessageEventArgs e)
		{
			MessageEventHandler h = null;
			string Key;

			lock (this.synchObject)
			{
				foreach (XmlElement E in e.Message.ChildNodes)
				{
					Key = E.LocalName + " " + E.NamespaceURI;
					if (this.messageHandlers.TryGetValue(Key, out h))
					{
						e.Content = E;
						break;
					}
					else
						h = null;
				}
			}

			if (h != null)
				this.Information(h.Method.Name);
			else
			{
				switch (e.Type)
				{
					case MessageType.Chat:
						this.Information("OnChatMessage()");
						h = this.OnChatMessage;
						break;

					case MessageType.Error:
						this.Information("OnErrorMessage()");
						h = this.OnErrorMessage;
						break;

					case MessageType.GroupChat:
						this.Information("OnGroupChatMessage()");
						h = this.OnGroupChatMessage;
						break;

					case MessageType.Headline:
						this.Information("OnHeadlineMessage()");
						h = this.OnHeadlineMessage;
						break;

					case MessageType.Normal:
					default:
						this.Information("OnNormalMessage()");
						h = this.OnNormalMessage;
						break;
				}
			}

			if (h != null)
			{
				try
				{
					h(this, e);
				}
				catch (Exception ex)
				{
					this.Exception(ex);
				}
			}
		}

		private void ProcessPresence(PresenceEventArgs e)
		{
			PresenceEventHandler h;
			RosterItem Item;

			switch (e.Type)
			{
				case PresenceType.Available:
					this.Information("OnPresence()");
					h = this.OnPresence;

					lock (this.roster)
					{
						if (this.roster.TryGetValue(e.FromBaseJID, out Item))
							Item.LastPresence = e;
					}
					break;

				case PresenceType.Unavailable:
					this.Information("OnPresence()");
					h = this.OnPresence;

					lock (this.roster)
					{
						if (this.roster.TryGetValue(e.FromBaseJID, out Item))
						{
							if (Item.LastPresenceFullJid == e.From)
								Item.LastPresence = null;
						}
					}
					break;

				case PresenceType.Error:
				case PresenceType.Probe:
				default:
					this.Information("OnPresence()");
					h = this.OnPresence;
					break;

				case PresenceType.Subscribe:
					this.Information("OnPresenceSubscribe()");
					h = this.OnPresenceSubscribe;
					break;

				case PresenceType.Subscribed:
					this.Information("OnPresenceSubscribed()");
					h = this.OnPresenceSubscribed;
					break;

				case PresenceType.Unsubscribe:
					this.Information("OnPresenceUnsubscribe()");
					h = this.OnPresenceUnsubscribe;
					break;

				case PresenceType.Unsubscribed:
					this.Information("OnPresenceUnsubscribed()");
					h = this.OnPresenceUnsubscribed;
					break;
			}

			if (h != null)
			{
				try
				{
					h(this, e);
				}
				catch (Exception ex)
				{
					this.Exception(ex);
				}
			}
		}

		private void ProcessIq(Dictionary<string, IqEventHandler> Handlers, IqEventArgs e)
		{
			IqEventHandler h = null;
			string Key;

			lock (this.synchObject)
			{
				foreach (XmlElement E in e.IQ.ChildNodes)
				{
					Key = E.LocalName + " " + E.NamespaceURI;
					if (Handlers.TryGetValue(Key, out h))
					{
						e.Query = E;
						break;
					}
					else
						h = null;
				}
			}

			if (h == null)
				this.IqError(e.Id, e.From, "<error type='cancel'><feature-not-implemented xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error>");
			else
			{
				try
				{
					h(this, e);
				}
				catch (Exception ex)
				{
					this.Exception(ex);
				}
			}
		}

		/// <summary>
		/// Registers an IQ-Get handler.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		/// <param name="Handler">Handler to process request.</param>
		/// <param name="PublishNamespaceAsClientFeature">If the namespace should be published as a client feature.</param>
		public void RegisterIqGetHandler(string LocalName, string Namespace, IqEventHandler Handler, bool PublishNamespaceAsClientFeature)
		{
			this.RegisterIqHandler(this.iqGetHandlers, LocalName, Namespace, Handler, PublishNamespaceAsClientFeature);
		}

		/// <summary>
		/// Registers an IQ-Set handler.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		/// <param name="Handler">Handler to process request.</param>
		/// <param name="PublishNamespaceAsClientFeature">If the namespace should be published as a client feature.</param>
		public void RegisterIqSetHandler(string LocalName, string Namespace, IqEventHandler Handler, bool PublishNamespaceAsClientFeature)
		{
			this.RegisterIqHandler(this.iqSetHandlers, LocalName, Namespace, Handler, PublishNamespaceAsClientFeature);
		}

		private void RegisterIqHandler(Dictionary<string, IqEventHandler> Handlers, string LocalName, string Namespace, IqEventHandler Handler,
			bool PublishNamespaceAsClientFeature)
		{
			string Key = LocalName + " " + Namespace;

			lock (this.synchObject)
			{
				if (Handlers.ContainsKey(Key))
					throw new ArgumentException("Handler already registered.", "LocalName");

				Handlers[Key] = Handler;

				if (PublishNamespaceAsClientFeature)
					this.clientFeatures[Namespace] = true;
			}
		}

		/// <summary>
		/// Registers a Message handler.
		/// </summary>
		/// <param name="LocalName">Local Name</param>
		/// <param name="Namespace">Namespace</param>
		/// <param name="Handler">Handler to process message.</param>
		/// <param name="PublishNamespaceAsClientFeature">If the namespace should be published as a client feature.</param>
		public void RegisterMessageHandler(string LocalName, string Namespace, MessageEventHandler Handler, bool PublishNamespaceAsClientFeature)
		{
			string Key = LocalName + " " + Namespace;

			lock (this.synchObject)
			{
				if (this.messageHandlers.ContainsKey(Key))
					throw new ArgumentException("Handler already registered.", "LocalName");

				this.messageHandlers[Key] = Handler;

				if (PublishNamespaceAsClientFeature)
					this.clientFeatures[Namespace] = true;
			}
		}

		/// <summary>
		/// Event raised when a presence message has been received from a resource.
		/// </summary>
		public event PresenceEventHandler OnPresence = null;

		/// <summary>
		/// Event raised when a resource is requesting to be informed of the current client's presence
		/// </summary>
		public event PresenceEventHandler OnPresenceSubscribe = null;

		/// <summary>
		/// Event raised when your presence subscription has been accepted.
		/// </summary>
		public event PresenceEventHandler OnPresenceSubscribed = null;

		/// <summary>
		/// Event raised when a resource is requesting to be removed from the current client's presence
		/// </summary>
		public event PresenceEventHandler OnPresenceUnsubscribe = null;

		/// <summary>
		/// Event raised when your presence unsubscription has been accepted.
		/// </summary>
		public event PresenceEventHandler OnPresenceUnsubscribed = null;

		/// <summary>
		/// Raised when a chat message has been received, that is not handled by a specific message handler.
		/// </summary>
		public event MessageEventHandler OnChatMessage = null;

		/// <summary>
		/// Raised when an error message has been received, that is not handled by a specific message handler.
		/// </summary>
		public event MessageEventHandler OnErrorMessage = null;

		/// <summary>
		/// Raised when a group chat message has been received, that is not handled by a specific message handler.
		/// </summary>
		public event MessageEventHandler OnGroupChatMessage = null;

		/// <summary>
		/// Raised when a headline message has been received, that is not handled by a specific message handler.
		/// </summary>
		public event MessageEventHandler OnHeadlineMessage = null;

		/// <summary>
		/// Raised when a normal message has been received, that is not handled by a specific message handler.
		/// </summary>
		public event MessageEventHandler OnNormalMessage = null;

		private void StartAuthentication()
		{
			if (this.authenticationMethod == null)
			{
				if (this.authenticationMechanisms.ContainsKey("SCRAM-SHA-1"))
				{
					string Nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray(), Base64FormattingOptions.None);
					string s = "n,,n=" + this.userName + ",r=" + Nonce;
					byte[] Data = System.Text.Encoding.UTF8.GetBytes(s);

					this.State = XmppState.Authenticating;
					this.authenticationMethod = new ScramSha1(Nonce);
					this.BeginWrite("<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='SCRAM-SHA-1'>" +
						Convert.ToBase64String(Data) + "</auth>", null);
				}
				else if (this.authenticationMechanisms.ContainsKey("DIGEST-MD5"))
				{
					this.State = XmppState.Authenticating;
					this.authenticationMethod = new DigestMd5();
					this.BeginWrite("<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='DIGEST-MD5'/>", null);
				}
				else if (this.authenticationMechanisms.ContainsKey("CRAM-MD5"))
				{
					this.State = XmppState.Authenticating;
					this.authenticationMethod = new CramMd5();
					this.BeginWrite("<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='CRAM-MD5'/>", null);
				}
				else if (this.authenticationMechanisms.ContainsKey("PLAIN"))
					throw new XmppException("PLAIN authentication method not allowed.");
				else if (this.authenticationMechanisms.ContainsKey("ANONYMOUS"))
					throw new XmppException("ANONYMOUS authentication method not allowed.");
				else
					throw new XmppException("No allowed authentication method supported.");
			}
		}

		internal static XmppException GetStreamExceptionObject(XmlElement E)
		{
			string Msg = string.Empty;

			foreach (XmlNode N2 in E.ChildNodes)
			{
				if (N2.LocalName == "text" && N2.NamespaceURI == NamespaceXmppStreams)
					Msg = N2.InnerText.Trim();
			}

			foreach (XmlNode N2 in E.ChildNodes)
			{
				if (N2.NamespaceURI == NamespaceXmppStreams)
				{
					switch (N2.LocalName)
					{
						// Stream Exceptions:
						case "bad-format": return new BadFormatException(Msg, E);
						case "bad-namespace-prefix": return new BadNamespacePrefixException(Msg, E);
						case "conflict": return new StreamErrors.ConflictException(Msg, E);
						case "connection-timeout": return new ConnectionTimeoutException(Msg, E);
						case "host-gone": return new HostGoneException(Msg, E);
						case "host-unknown": return new HostUnknownException(Msg, E);
						case "improper-addressing": return new ImproperAddressingException(Msg, E);
						case "internal-server-error": return new StreamErrors.InternalServerErrorException(Msg, E);
						case "invalid-from": return new InvalidFromException(Msg, E);
						case "invalid-namespace": return new InvalidNamespaceException(Msg, E);
						case "invalid-xml": return new InvalidXmlException(Msg, E);
						case "not-authorized": return new StreamErrors.NotAuthorizedException(Msg, E);
						case "not-well-formed": return new NotWellFormedException(Msg, E);
						case "policy-violation": return new StreamErrors.PolicyViolationException(Msg, E);
						case "remote-connection-failed": return new RemoteConnectionFailedException(Msg, E);
						case "reset": return new ResetException(Msg, E);
						case "resource-constraint": return new StreamErrors.ResourceConstraintException(Msg, E);
						case "restricted-xml": return new RestrictedXmlException(Msg, E);
						case "see-other-host": return new SeeOtherHostException(Msg, E);
						case "system-shutdown": return new SystemShutdownException(Msg, E);
						case "undefined-condition": return new StreamErrors.UndefinedConditionException(Msg, E);
						case "unsupported-encoding": return new UnsupportedEncodingException(Msg, E);
						case "unsupported-feature": return new UnsupportedFeatureException(Msg, E);
						case "unsupported-stanza-type": return new UnsupportedStanzaTypeException(Msg, E);
						case "unsupported-version": return new UnsupportedVersionException(Msg, E);
						default: return new XmppException(string.IsNullOrEmpty(Msg) ? "Unrecognized stream error returned." : Msg, E);
					}
				}
			}

			return new XmppException(string.IsNullOrEmpty(Msg) ? "Unspecified error returned." : Msg, E);
		}

		internal static XmppException GetStanzaExceptionObject(XmlElement E)
		{
			string Msg = string.Empty;

			foreach (XmlNode N2 in E.ChildNodes)
			{
				if (N2.LocalName == "text" && N2.NamespaceURI == NamespaceXmppStanzas)
					Msg = N2.InnerText.Trim();
			}

			foreach (XmlNode N2 in E.ChildNodes)
			{
				if (N2.NamespaceURI == NamespaceXmppStanzas)
				{
					switch (N2.LocalName)
					{
						case "bad-request": return new BadRequestException(Msg, E);
						case "conflict": return new StanzaErrors.ConflictException(Msg, E);
						case "feature-not-implemented": return new FeatureNotImplementedException(Msg, E);
						case "forbidden": return new ForbiddenException(Msg, E);
						case "gone": return new GoneException(Msg, E);
						case "internal-server-error": return new StanzaErrors.InternalServerErrorException(Msg, E);
						case "item-not-found": return new ItemNotFoundException(Msg, E);
						case "jid-malformed": return new JidMalformedException(Msg, E);
						case "not-acceptable": return new NotAcceptableException(Msg, E);
						case "not-allowed": return new NotAllowedException(Msg, E);
						case "not-authorized": return new StanzaErrors.NotAuthorizedException(Msg, E);
						case "policy-violation": return new StanzaErrors.PolicyViolationException(Msg, E);
						case "recipient-unavailable": return new RecipientUnavailableException(Msg, E);
						case "redirect": return new RedirectException(Msg, E);
						case "registration-required": return new RegistrationRequiredException(Msg, E);
						case "remote-server-not-found": return new RemoteServerNotFoundException(Msg, E);
						case "remote-server-timeout": return new RemoteServerTimeoutException(Msg, E);
						case "resource-constraint": return new StanzaErrors.ResourceConstraintException(Msg, E);
						case "service-unavailable": return new ServiceUnavailableException(Msg, E);
						case "subscription-required": return new SubscriptionRequiredException(Msg, E);
						case "undefined-condition": return new StanzaErrors.UndefinedConditionException(Msg, E);
						case "unexpected-request": return new UnexpectedRequestException(Msg, E);
						default: return new XmppException(string.IsNullOrEmpty(Msg) ? "Unrecognized stanza error returned." : string.Empty, E);
					}
				}
			}

			return new XmppException(string.IsNullOrEmpty(Msg) ? "Unspecified error returned." : string.Empty, E);
		}

		internal static XmppException GetSaslExceptionObject(XmlElement E)
		{
			string Msg = string.Empty;

			foreach (XmlNode N2 in E.ChildNodes)
			{
				if (N2.LocalName == "text" && N2.NamespaceURI == NamespaceXmppStreams)
					Msg = N2.InnerText.Trim();
			}

			foreach (XmlNode N2 in E.ChildNodes)
			{
				if (N2.NamespaceURI == NamespaceXmppSasl)
				{
					switch (N2.LocalName)
					{
						case "account-disabled": return new AccountDisabledException(Msg, E);
						case "credentials-expired": return new CredentialsExpiredException(Msg, E);
						case "encryption-required": return new EncryptionRequiredException(Msg, E);
						case "incorrect-encoding": return new IncorrectEncodingException(Msg, E);
						case "invalid-authzid": return new InvalidAuthzidException(Msg, E);
						case "invalid-mechanism": return new InvalidMechanismException(Msg, E);
						case "malformed-request": return new MalformedRequestException(Msg, E);
						case "mechanism-too-weak": return new MechanismTooWeakException(Msg, E);
						case "not-authorized": return new AuthenticationErrors.NotAuthorizedException(Msg, E);
						case "temporary-auth-failure": return new TemporaryAuthFailureException(Msg, E);
						default: return new XmppException(string.IsNullOrEmpty(Msg) ? "Unrecognized SASL error returned." : Msg, E);
					}
				}
			}

			return new XmppException(string.IsNullOrEmpty(Msg) ? "Unspecified error returned." : Msg, E);
		}

		private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors != SslPolicyErrors.None)
				return this.trustServer;

			return true;
		}

		private void EndAuthenticateAsClient(IAsyncResult ar)
		{
			try
			{
				if (this.stream != null)
				{
					((SslStream)this.stream).EndAuthenticateAsClient(ar);

					this.BeginWrite("<?xml version='1.0'?><stream:stream from='" + XmlEncode(this.baseJid) + "' to='" + XmlEncode(this.host) +
						"' version='1.0' xml:lang='" + XmlEncode(this.language) + "' xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'>", null);

					this.ResetState(false);
					this.BeginRead();
				}
			}
			catch (Exception ex)
			{
				this.ConnectionError(ex);
			}
		}

		internal string UserName
		{
			get { return this.userName; }
		}

		internal string Password
		{
			get { return this.password; }
		}

		/// <summary>
		/// Current Domain.
		/// </summary>
		public string Domain
		{
			get { return this.domain; }
		}

		/// <summary>
		/// Base JID
		/// </summary>
		public string BaseJID
		{
			get { return this.baseJid; }
		}

		/// <summary>
		/// Full JID.
		/// </summary>
		public string FullJID
		{
			get { return this.fullJid; }
		}

		/// <summary>
		/// Resource part of the <see cref="FullJID"/>. Will be available after successfully binding the connection.
		/// </summary>
		public string Resource
		{
			get { return this.resource; }
		}

		/// <summary>
		/// If registration of a new account is allowed.
		/// </summary>
		public void AllowRegistration()
		{
			this.AllowRegistration(string.Empty, string.Empty);
		}

		/// <summary>
		/// If registration of a new account is allowed.
		/// </summary>
		/// <param name="FormSignatureKey">Form signature key, if form signatures (XEP-0348) is to be used during registration.</param>
		/// <param name="FormSignatureSecret">Form signature secret, if form signatures (XEP-0348) is to be used during registration.</param>
		public void AllowRegistration(string FormSignatureKey, string FormSignatureSecret)
		{
			this.allowedToRegistered = true;
			this.formSignatureKey = FormSignatureKey;
			this.formSignatureSecret = FormSignatureSecret;
		}

		/// <summary>
		/// Performs an IQ Get request.
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Xml">XML to embed into the request.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		/// <returns>ID of IQ stanza</returns>
		public uint IqGet(string To, string Xml, IqResultEventHandler Callback, object State)
		{
			return this.Iq(null, To, Xml, "get", Callback, State);
		}

		/// <summary>
		/// Performs an IQ Set request.
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Xml">XML to embed into the request.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		/// <returns>ID of IQ stanza</returns>
		public uint IqSet(string To, string Xml, IqResultEventHandler Callback, object State)
		{
			return this.Iq(null, To, Xml, "set", Callback, State);
		}

		/// <summary>
		/// Returns a response to an IQ Get/Set request.
		/// </summary>
		/// <param name="Id">ID attribute of original IQ request.</param>
		/// <param name="To">Destination address</param>
		/// <param name="Xml">XML to embed into the response.</param>
		public void IqResult(string Id, string To, string Xml)
		{
			this.Iq(Id, To, Xml, "result", null, null);
		}

		/// <summary>
		/// Returns an error response to an IQ Get/Set request.
		/// </summary>
		/// <param name="Id">ID attribute of original IQ request.</param>
		/// <param name="To">Destination address</param>
		/// <param name="Xml">XML to embed into the response.</param>
		public void IqError(string Id, string To, string Xml)
		{
			this.Iq(Id, To, Xml, "error", null, null);
		}

		private uint Iq(string Id, string To, string Xml, string Type, IqResultEventHandler Callback, object State)
		{
			uint SeqNr;

			if (string.IsNullOrEmpty(Id))
			{
				lock (this.synchObject)
				{
					SeqNr = this.seqnr++;
					this.callbackMethods[SeqNr] = new KeyValuePair<IqResultEventHandler, object>(Callback, State);
				}

				Id = SeqNr.ToString();
			}
			else
				SeqNr = 0;

			StringBuilder sb = new StringBuilder();

			sb.Append("<iq type='");
			sb.Append(Type);
			sb.Append("' id='");
			sb.Append(Id);
			sb.Append("' to='");
			sb.Append(XmlEncode(To));
			sb.Append("'>");
			sb.Append(Xml);
			sb.Append("</iq>");

			this.BeginWrite(sb.ToString(), null);

			return SeqNr;
		}

		private void RegistrationFormReceived(XmppClient Sender, IqResultEventArgs e)
		{
			if (e.Ok)
			{
				foreach (XmlNode N in e.Response.ChildNodes)
				{
					if (N.LocalName == "query" && N.NamespaceURI == NamespaceRegister)
					{
						DataForm Form = null;
						string UserName = null;
						string Password = null;

						foreach (XmlNode N2 in N.ChildNodes)
						{
							switch (N2.LocalName)
							{
								case "username":
									UserName = N2.InnerText;
									break;

								case "password":
									Password = N2.InnerText;
									break;

								case "x":
									Form = new DataForm(this, (XmlElement)N2, this.SubmitRegistrationForm, this.CancelRegistrationForm, e.From, e.To);
									Form.State = e;

									Field Field = Form["username"];
									if (Field != null)
										Field.SetValue(this.userName);

									Field = Form["password"];
									if (Field != null)
										Field.SetValue(this.password);
									break;
							}
						}

						if (Form != null)
						{
							this.Information("OnRegistrationForm()");
							DataFormEventHandler h = this.OnRegistrationForm;
							if (h != null)
							{
								try
								{
									h(this, Form);
								}
								catch (Exception ex)
								{
									Exception(ex);
								}
							}
							else
								Form.Submit();
						}
						else
						{
							StringBuilder sb = new StringBuilder();

							sb.Append("<query xmlns='" + NamespaceRegister + "'>");

							if (UserName != null)
							{
								sb.Append("<username>");
								sb.Append(XmlEncode(this.userName));
								sb.Append("</username>");
							}

							if (Password != null)
							{
								sb.Append("<password>");
								sb.Append(XmlEncode(this.userName));
								sb.Append("</password>");
							}

							this.IqSet(e.From, sb.ToString(), this.RegistrationResultReceived, null);
						}
						return;
					}
				}
			}

			this.ConnectionError(e.StanzaError != null ? e.StanzaError : new XmppException("Unable to register new account.", e.Response));
		}

		/// <summary>
		/// Event raised when a registration form is shown during automatic account creation during connection.
		/// </summary>
		public event DataFormEventHandler OnRegistrationForm = null;

		private void SubmitRegistrationForm(object Sender, DataForm RegistrationForm)
		{
			IqResultEventArgs e = (IqResultEventArgs)RegistrationForm.State;
			StringBuilder sb = new StringBuilder();

			if (!string.IsNullOrEmpty(this.formSignatureKey) && !string.IsNullOrEmpty(this.formSignatureSecret))
				RegistrationForm.Sign(this.formSignatureKey, this.formSignatureSecret);

			sb.Append("<query xmlns='" + NamespaceRegister + "'>");
			RegistrationForm.SerializeSubmit(sb);
			sb.Append("</query>");

			this.IqSet(e.From, sb.ToString(), this.RegistrationResultReceived, null);
		}

		private void CancelRegistrationForm(object Sender, DataForm RegistrationForm)
		{
			IqResultEventArgs e = (IqResultEventArgs)RegistrationForm.State;
			StringBuilder sb = new StringBuilder();

			sb.Append("<query xmlns='" + NamespaceRegister + "'>");
			RegistrationForm.SerializeCancel(sb);
			sb.Append("</query>");

			this.IqSet(e.From, sb.ToString(), null, null);
		}

		private void RegistrationResultReceived(XmppClient Sender, IqResultEventArgs e)
		{
			if (e.Ok)
			{
				this.authenticationMethod = null;
				this.StartAuthentication();
			}
			else
				this.ConnectionError(e.StanzaError != null ? e.StanzaError : new XmppException("Unable to register new account.", e.Response));
		}

		private void BindResult(XmppClient Sender, IqResultEventArgs e)
		{
			if (e.Ok)
			{
				foreach (XmlNode N in e.Response.ChildNodes)
				{
					if (N.LocalName == "bind")
					{
						foreach (XmlNode N2 in N.ChildNodes)
						{
							if (N2.LocalName == "jid")
							{
								this.fullJid = N2.InnerText.Trim();

								int i = this.fullJid.IndexOf('/');
								if (i > 0)
								{
									this.resource = this.fullJid.Substring(i + 1);

									i = this.resource.IndexOf(' ');
									if (i > 0)
									{
										this.resource = this.resource.Substring(i + 1).TrimStart();
										this.fullJid = this.baseJid + "/" + this.resource;
									}
								}

								this.AdvanceUntilConnected();
								return;
							}
						}
					}
				}
			}

			this.ConnectionError(e.StanzaError != null ? e.StanzaError : new XmppException("Unable to bind the connection.", e.Response));
		}

		/// <summary>
		/// Changes the password of the current user.
		/// </summary>
		/// <param name="NewPassword">New password.</param>
		public void ChangePassword(string NewPassword)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("<query xmlns='" + NamespaceRegister + "'><username>");
			sb.Append(XmlEncode(this.userName));
			sb.Append("</username><password>");
			sb.Append(XmlEncode(NewPassword));
			sb.Append("</password></query>");

			this.IqSet(this.domain, sb.ToString(), this.ChangePasswordResult, new object[] { NewPassword, true });
		}

		private void ChangePasswordResult(XmppClient Sender, IqResultEventArgs e)
		{
			object[] P = (object[])e.State;
			string NewPassword = (string)P[0];
			bool FirstAttempt = (bool)P[1];

			if (e.Ok)
			{
				this.password = NewPassword;

				this.Information("OnPasswordChanged()");
				EventHandler h = this.OnPasswordChanged;
				if (h != null)
				{
					try
					{
						h(this, new EventArgs());
					}
					catch (Exception ex)
					{
						Exception(ex);
					}
				}
			}
			else
			{
				if (e.ErrorType == ErrorType.Modify)
				{
					foreach (XmlNode N in e.Response.ChildNodes)
					{
						if (N.LocalName == "query" && N.NamespaceURI == NamespaceRegister)
						{
							foreach (XmlNode N2 in N.ChildNodes)
							{
								if (N2.LocalName == "x" && N2.NamespaceURI == NamespaceData)
								{
									DataForm Form = new DataForm(this, (XmlElement)N2, this.SubmitChangePasswordForm, this.CancelChangePasswordForm, e.From, e.To);
									Form.State = e;

									Field Field = Form["username"];
									if (Field != null)
										Field.SetValue(this.userName);

									Field = Form["old_password"];
									if (Field != null)
										Field.SetValue(this.password);

									Field = Form["password"];
									if (Field != null)
										Field.SetValue(NewPassword);

									this.Information("OnChangePasswordForm()");
									DataFormEventHandler h = this.OnChangePasswordForm;
									if (h != null)
									{
										try
										{
											h(this, Form);
										}
										catch (Exception ex)
										{
											Exception(ex);
										}

										return;
									}
									else if (FirstAttempt)
									{
										Form.Submit();
										return;
									}
								}
							}
						}
					}
				}

				this.Error(e.StanzaError);
			}
		}

		private void SubmitChangePasswordForm(object Sender, DataForm RegistrationForm)
		{
			IqResultEventArgs e = (IqResultEventArgs)RegistrationForm.State;
			StringBuilder sb = new StringBuilder();

			if (!string.IsNullOrEmpty(this.formSignatureKey) && !string.IsNullOrEmpty(this.formSignatureSecret))
				RegistrationForm.Sign(this.formSignatureKey, this.formSignatureSecret);

			sb.Append("<query xmlns='" + NamespaceRegister + "'>");
			RegistrationForm.SerializeSubmit(sb);
			sb.Append("</query>");

			this.IqSet(e.From, sb.ToString(), this.ChangePasswordResult, e.State);
		}

		private void CancelChangePasswordForm(object Sender, DataForm RegistrationForm)
		{
			IqResultEventArgs e = (IqResultEventArgs)RegistrationForm.State;
			StringBuilder sb = new StringBuilder();

			sb.Append("<query xmlns='" + NamespaceRegister + "'>");
			RegistrationForm.SerializeCancel(sb);
			sb.Append("</query>");

			this.IqSet(e.From, sb.ToString(), null, null);
		}

		/// <summary>
		/// Event raised when a change password form is shown during password change.
		/// </summary>
		public event DataFormEventHandler OnChangePasswordForm = null;

		/// <summary>
		/// Event raised when password has been changed.
		/// </summary>
		public event EventHandler OnPasswordChanged = null;

		/// <summary>
		/// If the roster should be automatically fetched on startup or not.
		/// </summary>
		public bool RequestRosterOnStartup
		{
			get { return this.requestRosterOnStartup; }
			set { this.requestRosterOnStartup = value; }
		}

		private void AdvanceUntilConnected()
		{
			if (!this.hasRoster && this.requestRosterOnStartup)
			{
				this.State = XmppState.FetchingRoster;
				this.IqGet(this.domain, "<query xmlns='jabber:iq:roster'/>", this.RosterResult, null);
			}
			else if (!this.setPresence)
			{
				this.State = XmppState.SettingPresence;
				this.SetPresence(this.currentAvailability, this.customPresenceXml, this.customPresenceStatus);
			}
			else
				this.State = XmppState.Connected;
		}

		private void RosterResult(XmppClient Client, IqResultEventArgs e)
		{
			RosterItem Item;

			if (e.Ok)
			{
				this.hasRoster = true;

				foreach (XmlNode N in e.Response.ChildNodes)
				{
					if (N.LocalName == "query" && N.NamespaceURI == NamespaceRoster)
					{
						lock (this.roster)
						{
							this.roster.Clear();

							foreach (XmlNode N2 in N.ChildNodes)
							{
								if (N2.LocalName == "item")
								{
									Item = new RosterItem((XmlElement)N2);
									this.roster[Item.BaseJid] = Item;
								}
							}
						}
					}
				}

				this.AdvanceUntilConnected();
			}
			else
				this.ConnectionError(e.StanzaError != null ? e.StanzaError : new XmppException("Unable to fetch roster.", e.Response));
		}

		/// <summary>
		/// Access to the roster in the client.
		/// 
		/// To add or update a roster item, simply set the corresponding property value. To remove a roster item, set it to null.
		/// </summary>
		/// <param name="BaseJID">Base JID of roster item.</param>
		/// <returns>Roster item, if found, or null, if not available.</returns>
		/// <exception cref="ArgumentException">If updating a roster item with an item that doesn't have the same base JID as <paramref name="BaseJID"/>.</exception>
		public RosterItem this[string BaseJID]
		{
			get
			{
				return this.GetRosterItem(BaseJID);
			}

			set
			{
				if (value == null)
					this.RemoveRosterItem(BaseJID, null, null);
				else if (BaseJID != value.BaseJid)
					throw new ArgumentException("Base JIDs don't match.", "BaseJID");
				else
					this.AddRosterItem(value, null, null);
			}
		}

		/// <summary>
		/// Gets a roster item.
		/// </summary>
		/// <param name="BaseJID">Base JID of roster item.</param>
		/// <returns>Roster item, if found, or null, if not available.</returns>
		public RosterItem GetRosterItem(string BaseJID)
		{
			RosterItem RosterItem;

			lock (this.roster)
			{
				if (this.roster.TryGetValue(BaseJID, out RosterItem))
					return RosterItem;
				else
					return null;
			}
		}

		/// <summary>
		/// Adds an item to the roster. If an item with the same Base JID is found in the roster, that item is updated.
		/// </summary>
		/// <param name="Item">Item to add.</param>
		public void AddRosterItem(RosterItem Item)
		{
			this.AddRosterItem(Item, null, null);
		}

		/// <summary>
		/// Adds an item to the roster. If an item with the same Base JID is found in the roster, that item is updated.
		/// </summary>
		/// <param name="Item">Item to add.</param>
		/// <param name="Callback">Callback method to call, when roster has been updated. Can be null.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		public void AddRosterItem(RosterItem Item, IqResultEventHandler Callback, object State)
		{
			RosterItem RosterItem;

			lock (this.roster)
			{
				if (this.roster.TryGetValue(BaseJID, out RosterItem))
				{
					Item.PendingSubscription = RosterItem.PendingSubscription;
					Item.State = RosterItem.State;
				}

				this.roster[BaseJID] = Item;
			}

			StringBuilder sb = new StringBuilder();

			sb.Append("<query xmlns='");
			sb.Append(NamespaceRoster);
			sb.Append("'>");

			Item.Serialize(sb);

			sb.Append("</query>");

			this.IqSet(this.domain, sb.ToString(), Callback, State);
		}

		/// <summary>
		/// Updates an item in the roster.
		/// </summary>
		/// <param name="BaseJID">Base JID of the roster item.</param>
		/// <param name="Name">New name for the item.</param>
		/// <param name="Groups">Set of groups assigned to the item.</param>
		/// <exception cref="ArgumentException">If there is no roste item available with the corresponding base JID.</exception>
		public void UpdateRosterItem(string BaseJID, string Name, params string[] Groups)
		{
			this.UpdateRosterItem(BaseJID, Name, Groups, null, null);
		}

		/// <summary>
		/// Updates an item in the roster.
		/// </summary>
		/// <param name="BaseJID">Base JID of the roster item.</param>
		/// <param name="Name">New name for the item.</param>
		/// <param name="Groups">Set of groups assigned to the item.</param>
		/// <param name="Callback">Callback method to call, when roster has been updated. Can be null.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		/// <exception cref="ArgumentException">If there is no roster item available with the corresponding base JID.</exception>
		public void UpdateRosterItem(string BaseJID, string Name, string[] Groups, IqResultEventHandler Callback, object State)
		{
			RosterItem RosterItem;

			lock (this.roster)
			{
				if (!this.roster.TryGetValue(BaseJID, out RosterItem))
					throw new ArgumentException("A Roster Item with that base JID was not found.", "BaseJID");

				RosterItem.Name = Name;
				RosterItem.Groups = Groups;
			}

			StringBuilder sb = new StringBuilder();

			sb.Append("<query xmlns='");
			sb.Append(NamespaceRoster);
			sb.Append("'>");

			RosterItem.Serialize(sb);

			sb.Append("</query>");

			this.IqSet(this.domain, sb.ToString(), Callback, State);
		}

		/// <summary>
		/// Removes an item from the roster.
		/// </summary>
		/// <param name="BaseJID">Base JID of the roster item.</param>
		/// <exception cref="ArgumentException">If there is no roster item available with the corresponding base JID.</exception>
		public void RemoveRosterItem(string BaseJID)
		{
			this.RemoveRosterItem(BaseJID, null, null);
		}

		/// <summary>
		/// Removes an item from the roster.
		/// </summary>
		/// <param name="BaseJID">Base JID of the roster item.</param>
		/// <param name="Callback">Callback method to call, when roster has been updated. Can be null.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		/// <exception cref="ArgumentException">If there is no roster item available with the corresponding base JID.</exception>
		public void RemoveRosterItem(string BaseJID, IqResultEventHandler Callback, object State)
		{
			lock (this.roster)
			{
				if (!this.roster.Remove(BaseJID))
					throw new ArgumentException("A Roster Item with that base JID was not found.", "BaseJID");
			}

			StringBuilder sb = new StringBuilder();

			sb.Append("<query xmlns='");
			sb.Append(NamespaceRoster);
			sb.Append("'><item jid='");
			sb.Append(XmppClient.XmlEncode(BaseJID));
			sb.Append("' subscription='remove'/></query>");

			this.IqSet(this.domain, sb.ToString(), Callback, State);
		}

		/// <summary>
		/// If the roster has been fetched.
		/// </summary>
		public bool HasRoster
		{
			get { return this.hasRoster; }
		}

		/// <summary>
		/// Sets the presence of the connection.
		/// </summary>
		public void SetPresence()
		{
			this.SetPresence(Availability.Online, string.Empty, null);
		}

		/// <summary>
		/// Sets the presence of the connection.
		/// </summary>
		/// <param name="Availability">Client availability.</param>
		public void SetPresence(Availability Availability)
		{
			this.SetPresence(Availability, string.Empty, null);
		}

		/// <summary>
		/// Sets the presence of the connection.
		/// </summary>
		/// <param name="Availability">Client availability.</param>
		/// <param name="CustomXml">Custom XML.</param>
		public void SetPresence(Availability Availability, string CustomXml)
		{
			this.SetPresence(Availability, CustomXml, null);
		}

		/// <summary>
		/// Sets the presence of the connection.
		/// </summary>
		/// <param name="Availability">Client availability.</param>
		/// <param name="CustomXml">Custom XML.</param>
		/// <param name="Status">Custom Status message, defined as a set of (language,text) pairs.</param>
		public void SetPresence(Availability Availability, string CustomXml, params KeyValuePair<string, string>[] Status)
		{
			this.currentAvailability = Availability;
			this.customPresenceXml = CustomXml;
			this.customPresenceStatus = Status;

			if (this.state == XmppState.Connected || this.state == XmppState.SettingPresence)
			{
				StringBuilder sb = new StringBuilder();

				switch (Availability)
				{
					case XMPP.Availability.Online:
					default:
						sb.Append("<presence>");
						break;

					case XMPP.Availability.Away:
						sb.Append("<presence><show>away</show>");
						break;

					case XMPP.Availability.Chat:
						sb.Append("<presence><show>chat</show>");
						break;

					case XMPP.Availability.DoNotDisturb:
						sb.Append("<presence><show>dnd</show>");
						break;

					case XMPP.Availability.ExtendedAway:
						sb.Append("<presence><show>xa</show>");
						break;

					case XMPP.Availability.Offline:
						sb.Append("<presence type='unavailable'>");
						break;
				}

				if (Status != null)
				{
					foreach (KeyValuePair<string, string> P in Status)
					{
						sb.Append("<status");

						if (!string.IsNullOrEmpty(P.Key))
						{
							sb.Append(" xml:lang='");
							sb.Append(XmlEncode(P.Key));
							sb.Append("'>");
						}
						else
							sb.Append('>');

						sb.Append(XmlEncode(P.Value));
						sb.Append("</status>");
					}
				}

				if (!string.IsNullOrEmpty(CustomXml))
					sb.Append(CustomXml);

				sb.Append("</presence>");

				this.BeginWrite(sb.ToString(), this.PresenceSent);
			}
		}

		private void PresenceSent(object Sender, EventArgs e)
		{
			if (!this.setPresence)
			{
				this.setPresence = true;
				this.AdvanceUntilConnected();
			}
		}

		/// <summary>
		/// Requests subscription of presence information from a contact.
		/// </summary>
		/// <param name="BaseJid">Base JID of contact.</param>
		public void RequestPresenceSubscription(string BaseJid)
		{
			StringBuilder sb = new StringBuilder();
			uint SeqNr;

			lock (this.synchObject)
			{
				SeqNr = this.seqnr++;
			}

			sb.Append("<presence id='");
			sb.Append(SeqNr.ToString());
			sb.Append("' to='");
			sb.Append(XmlEncode(BaseJid));
			sb.Append("' type='subscribe'/>");

			this.BeginWrite(sb.ToString(), null);
		}

		/// <summary>
		/// Requests unssubscription of presence information from a contact.
		/// </summary>
		/// <param name="BaseJid">Base JID of contact.</param>
		public void RequestPresenceUnsubscription(string BaseJid)
		{
			StringBuilder sb = new StringBuilder();
			uint SeqNr;

			lock (this.synchObject)
			{
				SeqNr = this.seqnr++;
			}

			sb.Append("<presence id='");
			sb.Append(SeqNr.ToString());
			sb.Append("' to='");
			sb.Append(XmlEncode(BaseJid));
			sb.Append("' type='unsubscribe'/>");

			this.BeginWrite(sb.ToString(), null);
		}

		internal void PresenceSubscriptionAccepted(string Id, string BaseJid)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("<presence id='");
			sb.Append(XmlEncode(Id));
			sb.Append("' to='");
			sb.Append(XmlEncode(BaseJid));
			sb.Append("' type='subscribed'/>");

			this.BeginWrite(sb.ToString(), null);
		}

		internal void PresenceSubscriptionDeclined(string Id, string BaseJid)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("<presence id='");
			sb.Append(XmlEncode(Id));
			sb.Append("' to='");
			sb.Append(XmlEncode(BaseJid));
			sb.Append("' type='unsubscribed'/>");

			this.BeginWrite(sb.ToString(), null);
		}

		internal void PresenceUnsubscriptionAccepted(string Id, string BaseJid)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("<presence id='");
			sb.Append(XmlEncode(Id));
			sb.Append("' to='");
			sb.Append(XmlEncode(BaseJid));
			sb.Append("' type='unsubscribed'/>");

			this.BeginWrite(sb.ToString(), null);
		}

		internal void PresenceUnsubscriptionDeclined(string Id, string BaseJid)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("<presence id='");
			sb.Append(XmlEncode(Id));
			sb.Append("' to='");
			sb.Append(XmlEncode(BaseJid));
			sb.Append("' type='subscribed'/>");

			this.BeginWrite(sb.ToString(), null);
		}

		private void RosterPush(XmppClient Sender, IqEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.From))
				return;

			RosterItem Prev = null;
			RosterItem Item = null;

			foreach (XmlElement E in e.Query.ChildNodes)
			{
				if (E.LocalName == "item" && E.NamespaceURI == NamespaceRoster)
				{
					Item = new RosterItem(E);
					break;
				}
			}

			if (Item == null)
				this.IqError(e.Id, e.From, "<error type='cancel'><bad-request xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error>");
			else
			{
				RosterItemEventHandler h;

				this.IqResult(e.Id, e.From, string.Empty);

				lock (this.roster)
				{
					if (Item.State == SubscriptionState.Remove)
					{
						this.roster.Remove(Item.BaseJid);
						this.Information("OnRosterItemRemoved()");
						h = this.OnRosterItemRemoved;
					}
					else
					{
						if (this.roster.TryGetValue(Item.BaseJid, out Prev))
						{
							this.Information("OnRosterItemUpdated()");
							h = this.OnRosterItemUpdated;
							if (Prev.HasLastPresence)
								Item.LastPresence = Prev.LastPresence;
						}
						else
						{
							this.Information("OnRosterItemAdded()");
							h = this.OnRosterItemAdded;
							this.roster[Item.BaseJid] = Item;
						}
					}
				}

				if (h != null)
				{
					try
					{
						h(this, Item);
					}
					catch (Exception ex)
					{
						this.Exception(ex);
					}
				}
			}
		}

		/// <summary>
		/// Event raised when an item has been added to the roster.
		/// </summary>
		public event RosterItemEventHandler OnRosterItemAdded = null;

		/// <summary>
		/// Event raised when an item has been updated in the roster.
		/// </summary>
		public event RosterItemEventHandler OnRosterItemUpdated = null;

		/// <summary>
		/// Event raised when an item has been removed from the roster.
		/// </summary>
		public event RosterItemEventHandler OnRosterItemRemoved = null;

		/// <summary>
		/// Sends a simple chat message
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Body">Body text of chat message.</param>
		public void SendChatMessage(string To, string Body)
		{
			this.SendMessage(MessageType.Chat, To, string.Empty, Body, string.Empty, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Sends a simple chat message
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Body">Body text of chat message.</param>
		/// <param name="Subject">Subject</param>
		public void SendChatMessage(string To, string Body, string Subject)
		{
			this.SendMessage(MessageType.Chat, To, string.Empty, Body, Subject, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Sends a simple chat message
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Body">Body text of chat message.</param>
		/// <param name="Subject">Subject</param>
		/// <param name="Language">Language used.</param>
		public void SendChatMessage(string To, string Body, string Subject, string Language)
		{
			this.SendMessage(MessageType.Chat, To, string.Empty, Body, Subject, Language, string.Empty, string.Empty);
		}

		/// <summary>
		/// Sends a simple chat message
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Body">Body text of chat message.</param>
		/// <param name="Subject">Subject</param>
		/// <param name="Language">Language used.</param>
		/// <param name="ThreadId">Thread ID</param>
		public void SendChatMessage(string To, string Body, string Subject, string Language, string ThreadId)
		{
			this.SendMessage(MessageType.Chat, To, string.Empty, Body, Subject, Language, ThreadId, string.Empty);
		}

		/// <summary>
		/// Sends a simple chat message
		/// </summary>
		/// <param name="To">Destination address</param>
		/// <param name="Body">Body text of chat message.</param>
		/// <param name="Subject">Subject</param>
		/// <param name="Language">Language used.</param>
		/// <param name="ThreadId">Thread ID</param>
		/// <param name="ParentThreadId">Parent Thread ID</param>
		public void SendChatMessage(string To, string Body, string Subject, string Language, string ThreadId, string ParentThreadId)
		{
			this.SendMessage(MessageType.Chat, To, string.Empty, Body, Subject, Language, ThreadId, ParentThreadId);
		}

		/// <summary>
		/// Sends a simple chat message
		/// </summary>
		/// <param name="Type">Type of message to send.</param>
		/// <param name="To">Destination address</param>
		/// <param name="CustomXml">Custom XML</param>
		/// <param name="Body">Body text of chat message.</param>
		/// <param name="Subject">Subject</param>
		/// <param name="Language">Language used.</param>
		/// <param name="ThreadId">Thread ID</param>
		/// <param name="ParentThreadId">Parent Thread ID</param>
		public void SendMessage(MessageType Type, string To, string CustomXml, string Body, string Subject, string Language, string ThreadId, string ParentThreadId)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("<message type='chat' to='");
			sb.Append(XmlEncode(To));

			if (!string.IsNullOrEmpty(Language))
			{
				sb.Append("' xml:lang='");
				sb.Append(XmlEncode(Language));
			}

			sb.Append("'>");

			if (!string.IsNullOrEmpty(Subject))
			{
				sb.Append("<subject>");
				sb.Append(XmlEncode(Subject));
				sb.Append("</subject>");
			}

			sb.Append("<body>");
			sb.Append(XmlEncode(Body));
			sb.Append("</body>");

			if (!string.IsNullOrEmpty(ThreadId))
			{
				sb.Append("<thread");

				if (!string.IsNullOrEmpty(ParentThreadId))
				{
					sb.Append(" parent='");
					sb.Append(XmlEncode(ParentThreadId));
					sb.Append("'");
				}

				sb.Append(">");
				sb.Append(XmlEncode(ThreadId));
				sb.Append("</thread>");
			}

			if (!string.IsNullOrEmpty(CustomXml))
				sb.Append(CustomXml);

			sb.Append("</message>");

			this.BeginWrite(sb.ToString(), null);
		}

		private void DynamicFormUpdated(XmppClient Sender, MessageEventArgs e)
		{
			DataForm Form = null;
			string SessionVariable = XmlAttribute(e.Content, "sessionVariable");
			string Language = XmlAttribute(e.Content, "xml:lang");

			foreach (XmlNode N in e.Content.ChildNodes)
			{
				if (N.LocalName == "x")
				{
					Form = new DataForm(this, (XmlElement)N, null, null, e.From, e.To);
					break;
				}
			}

			if (Form != null)
			{
				DynamicDataFormEventHandler h = this.OnDynamicFormUpdated;
				if (h != null)
				{
					try
					{
						h(this, new DynamicDataFormEventArgs(Form, SessionVariable, Language));
					}
					catch (Exception ex)
					{
						this.Exception(ex);
					}
				}
			}
		}

		/// <summary>
		/// Event raised when a dynamic for has been updated. Dynamic forms have to be joined to the previous form 
		/// using the <see cref="DataForm.Join"/> method on the old form. The old form is identified using
		/// <see cref="DynamicDataFormEventArgs.SessionVariable"/>.
		/// </summary>
		public event DynamicDataFormEventHandler OnDynamicFormUpdated = null;

	}
}
