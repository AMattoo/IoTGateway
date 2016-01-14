﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Waher.Content;
using Waher.Things;
using Waher.Things.SensorData;

namespace Waher.Networking.XMPP.Sensor
{
	/// <summary>
	/// Implements an XMPP sensor client interface.
	/// 
	/// The interface is defined in XEP-0323:
	/// http://xmpp.org/extensions/xep-0323.html
	/// </summary>
	public class SensorClient : IDisposable
	{
		/// <summary>
		/// urn:xmpp:iot:sensordata
		/// </summary>
		public const string NamespaceSensorData = "urn:xmpp:iot:sensordata";

		private Dictionary<int, SensorDataClientRequest> requests = new Dictionary<int, SensorDataClientRequest>();
		private XmppClient client;
		private int seqNr = 0;
		private object synchObj = new object();

		/// <summary>
		/// Implements an XMPP sensor client interface.
		/// 
		/// The interface is defined in XEP-0323:
		/// http://xmpp.org/extensions/xep-0323.html
		/// </summary>
		/// <param name="Client">XMPP Client</param>
		public SensorClient(XmppClient Client)
		{
			this.client = Client;

			this.client.RegisterMessageHandler("started", NamespaceSensorData, this.StartedHandler, false);
			this.client.RegisterMessageHandler("done", NamespaceSensorData, this.DoneHandler, false);
			this.client.RegisterMessageHandler("failure", NamespaceSensorData, this.FailureHandler, false);
			this.client.RegisterMessageHandler("fields", NamespaceSensorData, this.FieldsHandler, false);
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.client.UnregisterMessageHandler("started", NamespaceSensorData, this.StartedHandler, false);
			this.client.UnregisterMessageHandler("done", NamespaceSensorData, this.DoneHandler, false);
			this.client.UnregisterMessageHandler("failure", NamespaceSensorData, this.FailureHandler, false);
			this.client.UnregisterMessageHandler("fields", NamespaceSensorData, this.FieldsHandler, false);
		}

		/// <summary>
		/// XMPP Client
		/// </summary>
		public XmppClient Client
		{
			get { return this.client; }
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <returns>Request object maintaining the current status of the request.</returns>
		public SensorDataClientRequest RequestReadout(string Destination, FieldType Types)
		{
			return this.RequestReadout(Destination, null, Types, null, DateTime.MinValue, DateTime.MaxValue, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, string[] Fields, FieldType Types)
		{
			return this.RequestReadout(Destination, null, Types, Fields, DateTime.MinValue, DateTime.MaxValue, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, FieldType Types, string[] Fields, DateTime From)
		{
			return this.RequestReadout(Destination, null, Types, Fields, From, DateTime.MaxValue, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <param name="To">To what time readout is to be made. Use <see cref="DateTime.MaxValue"/> to specify no upper limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, FieldType Types, string[] Fields, DateTime From, DateTime To)
		{
			return this.RequestReadout(Destination, null, Types, Fields, From, To, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <param name="To">To what time readout is to be made. Use <see cref="DateTime.MaxValue"/> to specify no upper limit.</param>
		/// <param name="When">When the readout is to be made. Use <see cref="DateTime.MinValue"/> to start the readout immediately.</param>
		public SensorDataClientRequest RequestReadout(string Destination, FieldType Types, string[] Fields, DateTime From, DateTime To, DateTime When)
		{
			return this.RequestReadout(Destination, null, Types, Fields, From, To, When, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor to read.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <param name="To">To what time readout is to be made. Use <see cref="DateTime.MaxValue"/> to specify no upper limit.</param>
		/// <param name="When">When the readout is to be made. Use <see cref="DateTime.MinValue"/> to start the readout immediately.</param>
		/// <param name="ServiceToken">Optional service token, as defined in XEP-0324.</param>
		/// <param name="DeviceToken">Optional device token, as defined in XEP-0324.</param>
		/// <param name="UserToken">Optional user token, as defined in XEP-0324.</param>
		public SensorDataClientRequest RequestReadout(string Destination, FieldType Types, string[] Fields, DateTime From, DateTime To, DateTime When,
			string ServiceToken, string DeviceToken, string UserToken)
		{
			return this.RequestReadout(Destination, null, Types, Fields, From, To, When, ServiceToken, DeviceToken, UserToken);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor or concentrator containing the thing(s) to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, ThingReference[] Nodes, FieldType Types)
		{
			return this.RequestReadout(Destination, Nodes, Types, null, DateTime.MinValue, DateTime.MaxValue, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor or concentrator containing the thing(s) to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, ThingReference[] Nodes, string[] Fields, FieldType Types)
		{
			return this.RequestReadout(Destination, Nodes, Types, Fields, DateTime.MinValue, DateTime.MaxValue, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor or concentrator containing the thing(s) to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, ThingReference[] Nodes, FieldType Types, string[] Fields, DateTime From)
		{
			return this.RequestReadout(Destination, Nodes, Types, Fields, From, DateTime.MaxValue, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor or concentrator containing the thing(s) to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <param name="To">To what time readout is to be made. Use <see cref="DateTime.MaxValue"/> to specify no upper limit.</param>
		public SensorDataClientRequest RequestReadout(string Destination, ThingReference[] Nodes, FieldType Types, string[] Fields, DateTime From, DateTime To)
		{
			return this.RequestReadout(Destination, Nodes, Types, Fields, From, To, DateTime.MinValue, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor or concentrator containing the thing(s) to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <param name="To">To what time readout is to be made. Use <see cref="DateTime.MaxValue"/> to specify no upper limit.</param>
		/// <param name="When">When the readout is to be made. Use <see cref="DateTime.MinValue"/> to start the readout immediately.</param>
		public SensorDataClientRequest RequestReadout(string Destination, ThingReference[] Nodes, FieldType Types, string[] Fields, DateTime From, DateTime To, DateTime When)
		{
			return this.RequestReadout(Destination, Nodes, Types, Fields, From, To, When, string.Empty, string.Empty, string.Empty);
		}

		/// <summary>
		/// Requests a sensor data readout.
		/// </summary>
		/// <param name="Destination">JID of sensor or concentrator containing the thing(s) to read.</param>
		/// <param name="Nodes">Array of nodes to read. Can be null or empty, if reading a sensor that is not a concentrator.</param>
		/// <param name="Types">Field Types to read.</param>
		/// <param name="Fields">Fields to read.</param>
		/// <param name="From">From what time readout is to be made. Use <see cref="DateTime.MinValue"/> to specify no lower limit.</param>
		/// <param name="To">To what time readout is to be made. Use <see cref="DateTime.MaxValue"/> to specify no upper limit.</param>
		/// <param name="When">When the readout is to be made. Use <see cref="DateTime.MinValue"/> to start the readout immediately.</param>
		/// <param name="ServiceToken">Optional service token, as defined in XEP-0324.</param>
		/// <param name="DeviceToken">Optional device token, as defined in XEP-0324.</param>
		/// <param name="UserToken">Optional user token, as defined in XEP-0324.</param>
		public SensorDataClientRequest RequestReadout(string Destination, ThingReference[] Nodes, FieldType Types, string[] Fields, DateTime From, DateTime To, DateTime When,
			string ServiceToken, string DeviceToken, string UserToken)
		{
			StringBuilder Xml = new StringBuilder();
			int SeqNr;

			lock (this.synchObj)
			{
				SeqNr = this.seqNr++;
			}

			Xml.Append("<req xmlns='");
			Xml.Append(NamespaceSensorData);
			Xml.Append("' seqnr='");
			Xml.Append(SeqNr.ToString());

			if ((Types & FieldType.All) == FieldType.All)
				Xml.Append("' all='true");
			else
			{
				if ((Types & FieldType.Historical) == FieldType.Historical)
				{
					Xml.Append("' historical='true");
					Types &= ~FieldType.Historical;
				}

				if (Types.HasFlag(FieldType.Momentary))
					Xml.Append("' momentary='true");

				if (Types.HasFlag(FieldType.Peak))
					Xml.Append("' peak='true");

				if (Types.HasFlag(FieldType.Status))
					Xml.Append("' status='true");

				if (Types.HasFlag(FieldType.Computed))
					Xml.Append("' computed='true");

				if (Types.HasFlag(FieldType.Identity))
					Xml.Append("' identity='true");

				if (Types.HasFlag(FieldType.HistoricalSecond))
					Xml.Append("' historicalSecond='true");

				if (Types.HasFlag(FieldType.HistoricalMinute))
					Xml.Append("' historicalMinute='true");

				if (Types.HasFlag(FieldType.HistoricalHour))
					Xml.Append("' historicalHour='true");

				if (Types.HasFlag(FieldType.HistoricalDay))
					Xml.Append("' historicalDay='true");

				if (Types.HasFlag(FieldType.HistoricalWeek))
					Xml.Append("' historicalWeek='true");

				if (Types.HasFlag(FieldType.HistoricalMonth))
					Xml.Append("' historicalMonth='true");

				if (Types.HasFlag(FieldType.HistoricalQuarter))
					Xml.Append("' historicalQuarter='true");

				if (Types.HasFlag(FieldType.HistoricalYear))
					Xml.Append("' historicalYear='true");

				if (Types.HasFlag(FieldType.HistoricalOther))
					Xml.Append("' historicalOther='true");
			}

			if (From != DateTime.MinValue)
			{
				Xml.Append("' from='");
				Xml.Append(XML.Encode(From));
			}

			if (To != DateTime.MaxValue)
			{
				Xml.Append("' to='");
				Xml.Append(XML.Encode(To));
			}

			if (When != DateTime.MinValue)
			{
				Xml.Append("' when='");
				Xml.Append(XML.Encode(When));
			}

			if (!string.IsNullOrEmpty(ServiceToken))
			{
				Xml.Append("' serviceToken='");
				Xml.Append(ServiceToken);
			}

			if (!string.IsNullOrEmpty(DeviceToken))
			{
				Xml.Append("' deviceToken='");
				Xml.Append(DeviceToken);
			}

			if (!string.IsNullOrEmpty(UserToken))
			{
				Xml.Append("' userToken='");
				Xml.Append(UserToken);
			}

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

			if (Fields != null)
			{
				foreach (string Field in Fields)
				{
					Xml.Append("<field name='");
					Xml.Append(XML.Encode(Field));
					Xml.Append("'/>");
				}
			}

			Xml.Append("</req>");

			SensorDataClientRequest Request = new SensorDataClientRequest(SeqNr, this, Destination, Destination, Nodes, Types, Fields, From, To, When,
				ServiceToken, DeviceToken, UserToken);

			lock (this.requests)
			{
				this.requests[SeqNr] = Request;
			}

			this.client.SendIqGet(Destination, Xml.ToString(), this.RequestResponse, Request);

			return Request;
		}

		private void RequestResponse(object Sender, IqResultEventArgs e)
		{
			SensorDataClientRequest Request = (SensorDataClientRequest)e.State;

			if (e.Ok)
			{
				foreach (XmlNode N in e.Response.ChildNodes)
				{
					switch (N.LocalName)
					{
						case "accepted":
							XmlElement E = (XmlElement)N;
							int SeqNr = XML.Attribute(E, "seqnr", 0);
							bool Queued = XML.Attribute(E, "queued", false);

							if (SeqNr == Request.SeqNr)
								Request.Accept(Queued);
							else
								Request.Fail("Sequence number mismatch.");

							return;

						case "started":
							E = (XmlElement)N;
							SeqNr = XML.Attribute(E, "seqnr", 0);

							if (SeqNr == Request.SeqNr)
							{
								Request.Accept(false);
								Request.Started();
							}
							else
								Request.Fail("Sequence number mismatch.");

							return;

						case "failure":
							E = (XmlElement)N;
							SeqNr = XML.Attribute(E, "seqnr", 0);

							if (SeqNr == Request.SeqNr)
								this.ProcessFailure(E, Request);
							else
								Request.Fail("Sequence number mismatch.");

							return;

						case "fields":
							E = (XmlElement)N;
							SeqNr = XML.Attribute(E, "seqnr", 0);

							if (SeqNr == Request.SeqNr)
								this.ProcessFields(E, Request);
							else
								Request.Fail("Sequence number mismatch.");

							return;
					}
				}

				Request.Fail("Invalid response to request.");
			}
			else
				Request.Fail(e.ErrorText);
		}

		private void StartedHandler(object Sender, MessageEventArgs e)
		{
			SensorDataClientRequest Request;
			int SeqNr = XML.Attribute(e.Content, "seqnr", 0);

			lock (this.requests)
			{
				if (!this.requests.TryGetValue(SeqNr, out Request))
					return;
			}

			Request.Started();
		}

		private void DoneHandler(object Sender, MessageEventArgs e)
		{
			SensorDataClientRequest Request;
			int SeqNr = XML.Attribute(e.Content, "seqnr", 0);

			lock (this.requests)
			{
				if (!this.requests.TryGetValue(SeqNr, out Request))
					return;
			}

			Request.Done();
		}

		private void FailureHandler(object Sender, MessageEventArgs e)
		{
			SensorDataClientRequest Request;
			int SeqNr = XML.Attribute(e.Content, "seqnr", 0);
			bool Done = XML.Attribute(e.Content, "done", false);

			lock (this.requests)
			{
				if (!this.requests.TryGetValue(SeqNr, out Request))
					return;
			}

			this.ProcessFailure(e.Content, Request);
		}

		private void ProcessFailure(XmlElement Content, SensorDataClientRequest Request)
		{
			List<ThingError> Errors = new List<ThingError>();
			XmlElement E;
			DateTime Timestamp;
			string NodeId;
			string SourceId;
			string CacheType;
			string ErrorMessage;

			this.AssertReceiving(Request);

			foreach (XmlNode N in Content.ChildNodes)
			{
				if (N.LocalName == "error")
				{
					E = (XmlElement)N;
					NodeId = XML.Attribute(E, "nodeId");
					SourceId = XML.Attribute(E, "sourceId");
					CacheType = XML.Attribute(E, "cacheType");
					Timestamp = XML.Attribute(E, "timestamp", DateTime.MinValue);
					ErrorMessage = E.InnerText;

					Errors.Add(new ThingError(NodeId, SourceId, CacheType, Timestamp, ErrorMessage));
				}
			}

			Request.LogErrors(Errors);
		}

		private void AssertReceiving(SensorDataClientRequest Request)
		{
			if (Request.State == SensorDataReadoutState.Requested)
				Request.State = SensorDataReadoutState.Accepted;

			if (Request.State == SensorDataReadoutState.Accepted)
				Request.State = SensorDataReadoutState.Started;

			if (Request.State == SensorDataReadoutState.Started)
				Request.State = SensorDataReadoutState.Receiving;
		}

		private void FieldsHandler(object Sender, MessageEventArgs e)
		{
			SensorDataClientRequest Request;
			int SeqNr = XML.Attribute(e.Content, "seqnr", 0);

			lock (this.requests)
			{
				if (!this.requests.TryGetValue(SeqNr, out Request))
					return;
			}

			this.ProcessFields(e.Content, Request);
		}

		private void ProcessFields(XmlElement Content, SensorDataClientRequest Request)
		{
			List<Field> Fields;
			bool Done;

			this.AssertReceiving(Request);

			Fields = ParseFields(Content, out Done);

			Request.LogFields(Fields);

			if (Done)
				Request.Done();
		}

		/// <summary>
		/// Parses sensor data field definitions.
		/// </summary>
		/// <param name="Content">Fields element containing sensor data as defined in XEP-0323.</param>
		/// <param name="Done">If sensor data readout is done.</param>
		/// <returns>Parsed fields.</returns>
		public static List<Field> ParseFields(XmlElement Content, out bool Done)
		{
			List<Field> Fields;
			LocalizationStep[] LocalizationSteps;
			XmlElement E;
			DateTime Timestamp;
			DateTime DT;
			Duration D;
			TimeSpan TS;
			FieldType FieldTypes;
			FieldQoS FieldQoS;
			ThingReference Thing;
			string NodeId;
			string SourceId;
			string CacheType;
			string FieldName;
			string Module;
			string StringIds;
			string ValueString;
			string ValueType;
			string Unit;
			long l;
			int i;
			double d;
			byte NrDec;
			bool Writable;
			bool b;

			Done = XML.Attribute(Content, "done", false);

			Fields = new List<Field>();

			foreach (XmlNode N in Content.ChildNodes)
			{
				if (N.LocalName == "node")
				{
					E = (XmlElement)N;
					NodeId = XML.Attribute(E, "nodeId");
					SourceId = XML.Attribute(E, "sourceId");
					CacheType = XML.Attribute(E, "cacheType");
					Thing = new ThingReference(NodeId, SourceId, CacheType);

					foreach (XmlNode N2 in N.ChildNodes)
					{
						if (N2.LocalName == "timestamp")
						{
							E = (XmlElement)N2;
							Timestamp = XML.Attribute(E, "value", DateTime.MinValue);

							foreach (XmlNode N3 in N2.ChildNodes)
							{
								E = N3 as XmlElement;
								if (E == null)
									continue;

								FieldName = string.Empty;
								FieldTypes = (FieldType)0;
								FieldQoS = (FieldQoS)0;
								Module = string.Empty;
								StringIds = string.Empty;
								Writable = false;
								ValueString = string.Empty;
								ValueType = string.Empty;
								Unit = string.Empty;

								foreach (XmlAttribute Attr in E.Attributes)
								{
									switch (Attr.Name)
									{
										case "name":
											FieldName = Attr.Value;
											break;

										case "module":
											Module = Attr.Value;
											break;

										case "stringIds":
											StringIds = Attr.Value;
											break;

										case "writable":
											if (!CommonTypes.TryParse(Attr.Value, out Writable))
												Writable = false;
											break;

										case "momentary":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.Momentary;
											break;

										case "peak":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.Peak;
											break;

										case "status":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.Status;
											break;

										case "computed":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.Computed;
											break;

										case "identity":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.Identity;
											break;

										case "historicalSecond":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalSecond;
											break;

										case "historicalMinute":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalMinute;
											break;

										case "historicalHour":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalMonth;
											break;

										case "historicalDay":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalDay;
											break;

										case "historicalWeek":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalWeek;
											break;

										case "historicalMonth":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalMonth;
											break;

										case "historicalQuarter":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalQuarter;
											break;

										case "historicalYear":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalYear;
											break;

										case "historicalOther":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldTypes |= FieldType.HistoricalOther;
											break;

										case "missing":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.Missing;
											break;

										case "inProgress":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.InProgress;
											break;

										case "automaticEstimate":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.AutomaticEstimate;
											break;

										case "manualEstimate":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.ManualEstimate;
											break;

										case "manualReadout":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.ManualReadout;
											break;

										case "automaticReadout":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.AutomaticReadout;
											break;

										case "timeOffset":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.TimeOffset;
											break;

										case "warning":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.Warning;
											break;

										case "error":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.Error;
											break;

										case "signed":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.Signed;
											break;

										case "invoiced":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.Invoiced;
											break;

										case "endOfSeries":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.EndOfSeries;
											break;

										case "powerFailure":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.PowerFailure;
											break;

										case "invoiceConfirmed":
											if (CommonTypes.TryParse(Attr.Value, out b) && b)
												FieldQoS |= FieldQoS.InvoiceConfirmed;
											break;

										case "value":
											ValueString = Attr.Value;
											break;

										case "unit":
											Unit = Attr.Value;
											break;

										case "dataType":
											ValueType = Attr.Value;
											break;
									}
								}

								if (string.IsNullOrEmpty(StringIds))
									LocalizationSteps = null;
								else
									LocalizationSteps = ParseStringIds(StringIds);

								switch (N3.LocalName)
								{
									case "boolean":
										if (CommonTypes.TryParse(ValueString, out b))
											Fields.Add(new BooleanField(Thing, Timestamp, FieldName, b, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "date":
										if (CommonTypes.TryParse(ValueString, out DT))
											Fields.Add(new DateField(Thing, Timestamp, FieldName, DT, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "dateTime":
										if (CommonTypes.TryParse(ValueString, out DT))
											Fields.Add(new DateTimeField(Thing, Timestamp, FieldName, DT, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "duration":
										if (Duration.TryParse(ValueString, out D))
											Fields.Add(new DurationField(Thing, Timestamp, FieldName, D, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "enum":
										Fields.Add(new EnumField(Thing, Timestamp, FieldName, ValueString, ValueType, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "int":
										if (int.TryParse(ValueString, out i))
											Fields.Add(new Int32Field(Thing, Timestamp, FieldName, i, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "long":
										if (long.TryParse(ValueString, out l))
											Fields.Add(new Int64Field(Thing, Timestamp, FieldName, l, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "numeric":
										if (CommonTypes.TryParse(ValueString, out d, out NrDec))
											Fields.Add(new QuantityField(Thing, Timestamp, FieldName, d, NrDec, Unit, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "string":
										Fields.Add(new StringField(Thing, Timestamp, FieldName, ValueString, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;

									case "time":
										if (TimeSpan.TryParse(ValueString, out TS))
											Fields.Add(new TimeField(Thing, Timestamp, FieldName, TS, FieldTypes, FieldQoS, Writable, Module, LocalizationSteps));
										break;
								}
							}
						}
					}
				}
			}

			return Fields;
		}

		private static LocalizationStep[] ParseStringIds(string StringIds)
		{
			int StringId;

			if (string.IsNullOrEmpty(StringIds))
				return null;

			if (int.TryParse(StringIds, out StringId))
				return new LocalizationStep[1] { new LocalizationStep(StringId) };

			string[] Steps = StringIds.Split(',');
			string[] Parts;
			string Module;
			string Seed;
			int i, d, c = Steps.Length;
			LocalizationStep[] Result = new LocalizationStep[c];

			for (i = 0; i < c; i++)
			{
				Parts = Steps[i].Split('|');
				d = Parts.Length;

				if (!int.TryParse(Parts[0], out StringId))
					continue;

				if (d > 1)
				{
					Module = Parts[1];

					if (d > 2)
						Seed = Parts[2];
					else
						Seed = string.Empty;
				}
				else
				{
					Module = string.Empty;
					Seed = string.Empty;
				}

				Result[i] = new LocalizationStep(StringId, Module, Seed);
			}

			return Result;
		}

	}
}
