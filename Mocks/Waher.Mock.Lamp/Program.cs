﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Waher.Events;
using Waher.Events.Console;
using Waher.Events.XMPP;
using Waher.Things;
using Waher.Things.SensorData;
using Waher.Networking;
using Waher.Networking.Sniffers;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Chat;
using Waher.Networking.XMPP.Control;
using Waher.Networking.XMPP.Control.ParameterTypes;
using Waher.Networking.XMPP.Interoperability;
using Waher.Networking.XMPP.Sensor;

namespace Waher.Mock.Lamp
{
	/// <summary>
	/// Mock lamp switch using XMPP.
	/// </summary>
	public class Program
	{
		private const string FormSignatureKey = "";		// Form signature key, if form signatures (XEP-0348) is to be used during registration.
		private const string FormSignatureSecret = "";	// Form signature secret, if form signatures (XEP-0348) is to be used during registration.

		private static SimpleXmppConfiguration xmppConfiguration;

		static void Main(string[] args)
		{
			try
			{
				Console.ForegroundColor = ConsoleColor.White;

				Console.Out.WriteLine("Welcome to the Mock Temperature sensor application.");
				Console.Out.WriteLine(new string('-', 79));
				Console.Out.WriteLine("This application will simulate an outside temperature sensor.");
				Console.Out.WriteLine("Values will be published over XMPP using the interface defined in XEP-0323.");

				Log.Register(new ConsoleEventSink());

				xmppConfiguration = SimpleXmppConfiguration.GetConfigUsingSimpleConsoleDialog("xmpp.config",
					Guid.NewGuid().ToString().Replace("-", string.Empty),	// Default user name.
					Guid.NewGuid().ToString().Replace("-", string.Empty),	// Default password.
					FormSignatureKey, FormSignatureSecret);

				using (XmppClient Client = xmppConfiguration.GetClient("en"))
				{
					if (xmppConfiguration.TrustServer)
						Client.TrustServer = true;

					Client.AllowRegistration(FormSignatureKey, FormSignatureSecret);

					if (xmppConfiguration.Sniffer)
						Client.Add(new ConsoleOutSniffer(BinaryPresentationMethod.ByteCount));

					if (!string.IsNullOrEmpty(xmppConfiguration.Events))
						Log.Register(new XmppEventSink("XMPP Event Sink", Client, xmppConfiguration.Events, false));

					Timer ConnectionTimer = new Timer((P) =>
					{
						if (Client.State == XmppState.Offline || Client.State == XmppState.Error || Client.State == XmppState.Authenticating)
						{
							try
							{
								Client.Reconnect();
							}
							catch (Exception ex)
							{
								Log.Critical(ex);
							}
						}
					}, null, 60000, 60000);

					bool Connected = false;
					bool ImmediateReconnect;

					Client.OnStateChanged += (sender, NewState) =>
					{
						switch (NewState)
						{
							case XmppState.Connected:
								Connected = true;

								break;

							case XmppState.Offline:
								ImmediateReconnect = Connected;
								Connected = false;

								if (ImmediateReconnect)
									Client.Reconnect();
								break;
						}
					};

					Client.OnPresenceSubscribe += (sender, e) =>
					{
						e.Accept();		// TODO: Provisioning
						Client.SetPresence(Availability.Chat);
					};

					Client.OnPresenceUnsubscribe += (sender, e) =>
					{
						e.Accept();
					};

					bool SwitchOn = false;

					SensorServer SensorServer = new SensorServer(Client);
					SensorServer.OnExecuteReadoutRequest += (Sender, Request) =>
					{
						DateTime Now = DateTime.Now;

						Log.Informational("Readout requested", string.Empty, Request.Actor);

						Request.ReportFields(true, new BooleanField(ThingReference.Empty, Now, "Lamp", SwitchOn, FieldType.Momentary, FieldQoS.AutomaticReadout));
					};

					ControlServer ControlServer = new ControlServer(Client,
						new BooleanControlParameter("Lamp", "Control", "Lamp switch on.", "If checked, lamp is turned on.",
							(Node) => SwitchOn,
							(Node, Value) =>
							{
								SwitchOn = Value;
								Log.Informational(Environment.NewLine + Environment.NewLine + "Lamp turned " + (SwitchOn ? "ON" : "OFF") + Environment.NewLine + Environment.NewLine);
							}));

					ChatServer ChatServer = new ChatServer(Client, SensorServer);

					InteroperabilityServer InteroperabilityServer = new InteroperabilityServer(Client);
					InteroperabilityServer.OnGetInterfaces += (sender, e) =>
					{
						e.Add("XMPP.IoT.Actuator.Lamp");
					};

					while (true)
						Thread.Sleep(1000);
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Out.WriteLine(ex.Message);
			}
		}
	}
}
