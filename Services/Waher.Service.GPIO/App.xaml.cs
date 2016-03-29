﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;
using Waher.Events;
using Waher.Events.XMPP;
using Waher.Mock;
using Waher.Things;
using Waher.Things.SensorData;
using Waher.Networking;
using Waher.Networking.Sniffers;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Chat;
using Waher.Networking.XMPP.Control;
using Waher.Networking.XMPP.Control.ParameterTypes;
using Waher.Networking.XMPP.Sensor;

namespace Waher.Service.GPIO
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
				Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
				Microsoft.ApplicationInsights.WindowsCollectors.Session);
			this.InitializeComponent();
			this.Suspending += OnSuspending;
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used such as when the application is launched to open a specific file.
		/// </summary>
		/// <param name="e">Details about the launch request and process.</param>
		protected override void OnLaunched(LaunchActivatedEventArgs e)
		{

#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached)
			{
				this.DebugSettings.EnableFrameRateCounter = true;
			}
#endif

			Frame rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null)
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame();

				rootFrame.NavigationFailed += OnNavigationFailed;

				if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
				{
					//TODO: Load state from previously suspended application
				}

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			if (rootFrame.Content == null)
			{
				// When the navigation stack isn't restored navigate to the first page,
				// configuring the new page by passing required information as a navigation
				// parameter
				rootFrame.Navigate(typeof(MainPage), e.Arguments);
			}
			// Ensure the current window is active
			Window.Current.Activate();

			this.StartActuator();
		}

		private const string FormSignatureKey = "";     // Form signature key, if form signatures (XEP-0348) is to be used during registration.
		private const string FormSignatureSecret = "";  // Form signature secret, if form signatures (XEP-0348) is to be used during registration.
		private const int MaxRecordsPerPeriod = 500;

		private XmppClient xmppClient = null;
		private Timer sampleTimer = null;
		private SensorServer sensorServer = null;
		private ControlServer controlServer = null;
		private ChatServer chatServer = null;
		private GpioController gpio = null;
		private UsbSerial arduinoUsb = null;
		private RemoteDevice arduino = null;
		private SortedDictionary<int, KeyValuePair<GpioPin, KeyValuePair<TextBlock, TextBlock>>> gpioPins = new SortedDictionary<int, KeyValuePair<GpioPin, KeyValuePair<TextBlock, TextBlock>>>();
		private SortedDictionary<string, KeyValuePair<TextBlock, TextBlock>> arduinoPins = new SortedDictionary<string, KeyValuePair<TextBlock, TextBlock>>();

		private async void StartActuator()
		{
			try
			{
				Log.Informational("Starting application.");

				SimpleXmppConfiguration xmppConfiguration = SimpleXmppConfiguration.GetConfigUsingSimpleConsoleDialog("xmpp.config",
					Guid.NewGuid().ToString().Replace("-", string.Empty),   // Default user name.
					Guid.NewGuid().ToString().Replace("-", string.Empty),   // Default password.
					FormSignatureKey, FormSignatureSecret, typeof(App).GetTypeInfo().Assembly);

				Log.Informational("Connecting to XMPP server.");

				xmppClient = xmppConfiguration.GetClient("en", typeof(App).GetTypeInfo().Assembly);
				xmppClient.AllowRegistration(FormSignatureKey, FormSignatureSecret);

				if (xmppConfiguration.Sniffer && MainPage.Sniffer != null)
					xmppClient.Add(MainPage.Sniffer);

				if (!string.IsNullOrEmpty(xmppConfiguration.Events))
					Log.Register(new XmppEventSink("XMPP Event Sink", xmppClient, xmppConfiguration.Events, false));

				Timer ConnectionTimer = new Timer((P) =>
				{
					if (xmppClient.State == XmppState.Offline || xmppClient.State == XmppState.Error || xmppClient.State == XmppState.Authenticating)
					{
						try
						{
							Log.Informational("Reconnecting.");
							xmppClient.Reconnect();
						}
						catch (Exception ex)
						{
							Log.Critical(ex);
						}
					}
				}, null, 60000, 60000);

				bool Connected = false;
				bool ImmediateReconnect;

				xmppClient.OnStateChanged += (sender, NewState) =>
				{
					Log.Informational(NewState.ToString());

					switch (NewState)
					{
						case XmppState.Connected:
							Connected = true;

							break;

						case XmppState.Offline:
							ImmediateReconnect = Connected;
							Connected = false;

							if (ImmediateReconnect)
								xmppClient.Reconnect();
							break;
					}
				};

				xmppClient.OnPresenceSubscribe += (sender, e) =>
				{
					Log.Informational("Subscription request received from " + e.From + ".");

					e.Accept();     // TODO: Provisioning
					xmppClient.SetPresence(Availability.Chat);
				};

				xmppClient.OnPresenceUnsubscribe += (sender, e) =>
				{
					Log.Informational("Unsubscription request received from " + e.From + ".");
					e.Accept();
				};

				gpio = GpioController.GetDefault();
				if (gpio != null)
				{
					GpioPin Pin;
					GpioOpenStatus Status;
					int c = gpio.PinCount;
					int i;

					for (i = 0; i < c; i++)
					{
						if (gpio.TryOpenPin(i, GpioSharingMode.Exclusive, out Pin, out Status) && Status == GpioOpenStatus.PinOpened)
						{
							gpioPins[i] = new KeyValuePair<GpioPin, KeyValuePair<TextBlock, TextBlock>>(Pin,
								MainPage.Instance.AddPin("GPIO" + i.ToString(), Pin.GetDriveMode(), Pin.Read().ToString()));
						}
					}
				}

				DeviceInformationCollection Devices = await Microsoft.Maker.Serial.UsbSerial.listAvailableDevicesAsync();
				foreach (DeviceInformation DeviceInfo in Devices)
				{
					if (DeviceInfo.IsEnabled && DeviceInfo.Name.StartsWith("Arduino"))
					{
						Log.Informational("Connecting to " + DeviceInfo.Name);
						arduinoUsb = new UsbSerial(DeviceInfo);
						arduinoUsb.ConnectionEstablished += () =>
						{
							Log.Informational("USB connection established.");
						};

						arduino = new RemoteDevice(arduinoUsb);
						arduino.DeviceReady += () =>
						{
							Log.Informational("Device ready.");

							Dictionary<string, KeyValuePair<Enum, string>> Values = new Dictionary<string, KeyValuePair<Enum, string>>();
							PinMode Mode;
							PinState State;
							string s;
							ushort Value;
							byte i;

							for (i = 0; i < arduino.DeviceHardwareProfile.AnalogPinCount; i++)
							{
								s = "A" + i.ToString();
								if (arduino.DeviceHardwareProfile.isAnalogSupported((uint)(arduino.DeviceHardwareProfile.AnalogOffset + i)))
									arduino.pinMode(s, PinMode.ANALOG);

								Mode = arduino.getPinMode(s);
								Value = arduino.analogRead(s);

								Values[s] = new KeyValuePair<Enum, string>(Mode, Value.ToString());
							}

							for (i = 0; i < arduino.DeviceHardwareProfile.AnalogOffset; i++)
							{
								if (i < 13)
								{
									if (arduino.DeviceHardwareProfile.isDigitalInputSupported(i))
										arduino.pinMode(i, PinMode.INPUT);
								}
								else if (i == 13)
								{
									if (arduino.DeviceHardwareProfile.isDigitalOutputSupported(13))
									{
										arduino.pinMode(13, PinMode.OUTPUT);    // Onboard LED.
										arduino.digitalWrite(13, PinState.HIGH);
									}
								}
								
								s = "D" + i.ToString();
								Mode = arduino.getPinMode(i);
								State = arduino.digitalRead(i);

								Values[s] = new KeyValuePair<Enum, string>(Mode, State.ToString());
							}

							MainPage.Instance.UpdateValues(Values, arduinoPins);
						};

						arduino.AnalogPinUpdated += (pin, value) =>
						{
							KeyValuePair<TextBlock, TextBlock> P;

							lock (this.arduinoPins)
							{
								if (!this.arduinoPins.TryGetValue(pin, out P))
								{
									Log.Error("Analog pin not found: " + pin);
									return;
								}
							}

							MainPage.Instance.UpdateValue(P.Value, value.ToString());
						};

						arduino.DigitalPinUpdated += (pin, value) =>
						{
							KeyValuePair<TextBlock, TextBlock> P;

							lock (this.arduinoPins)
							{
								if (!this.arduinoPins.TryGetValue("D" + pin.ToString(), out P))
								{
									Log.Error("Analog pin not found: " + pin);
									return;
								}
							}

							MainPage.Instance.UpdateValue(P.Value, value.ToString());
						};

						arduinoUsb.ConnectionFailed += message =>
						{
							Log.Error("USB connection failed: " + message);
						};

						arduinoUsb.ConnectionLost += message =>
						{
							Log.Error("USB connection lost: " + message);
						};

						arduinoUsb.begin(57000, SerialConfig.SERIAL_8N1);
						break;
					}
				}

				sensorServer = new SensorServer(xmppClient);
				sensorServer.OnExecuteReadoutRequest += (Sender, Request) =>
				{
					DateTime Now = DateTime.Now;
					LinkedList<Field> Fields = new LinkedList<Field>();
					DateTime TP = DateTime.Now;
					string s;
					bool ReadMomentary = Request.IsIncluded(FieldType.Momentary);
					bool ReadStatus = Request.IsIncluded(FieldType.Status);

					Log.Informational("Readout requested", string.Empty, Request.Actor);

					foreach (KeyValuePair<GpioPin, KeyValuePair<TextBlock, TextBlock>> Pin in gpioPins.Values)
					{
						if (ReadMomentary && Request.IsIncluded(s = "GPIO" + Pin.Key.PinNumber.ToString()))
						{
							Fields.AddLast(new EnumField(ThingReference.Empty, TP, s,
								Pin.Key.Read(), FieldType.Momentary, FieldQoS.AutomaticReadout));
						}

						if (ReadStatus && Request.IsIncluded(s = "GPIO" + Pin.Key.PinNumber.ToString() + ", Mode"))
						{
							Fields.AddLast(new EnumField(ThingReference.Empty, TP, s,
								Pin.Key.GetDriveMode(), FieldType.Status, FieldQoS.AutomaticReadout));
						}
					}

					if (arduinoPins != null)
					{
						foreach (KeyValuePair<string, KeyValuePair<TextBlock, TextBlock>> Pin in arduinoPins)
						{
							byte i;

							if (ReadMomentary && Request.IsIncluded(s = Pin.Key))
							{
								if (s.StartsWith("D") && byte.TryParse(s.Substring(1), out i))
								{
									Fields.AddLast(new EnumField(ThingReference.Empty, TP, s,
										arduino.digitalRead(i), FieldType.Momentary, FieldQoS.AutomaticReadout));
								}
								else
								{
									ushort Raw = arduino.analogRead(s);
									double Percent = Raw / 10.24;

									Fields.AddLast(new Int32Field(ThingReference.Empty, TP, s + ", Raw",
										Raw, FieldType.Momentary, FieldQoS.AutomaticReadout));

									Fields.AddLast(new QuantityField(ThingReference.Empty, TP, s,
										Percent, 2, "%", FieldType.Momentary, FieldQoS.AutomaticReadout));
								}
							}

							if (ReadStatus && Request.IsIncluded(s = Pin.Key + ", Mode"))
							{
								if (s.StartsWith("D") && byte.TryParse(s.Substring(1), out i))
								{
									Fields.AddLast(new EnumField(ThingReference.Empty, TP, s,
										arduino.getPinMode(i), FieldType.Status, FieldQoS.AutomaticReadout));
								}
								else
								{
									Fields.AddLast(new EnumField(ThingReference.Empty, TP, s,
										arduino.getPinMode(s), FieldType.Status, FieldQoS.AutomaticReadout));
								}
							}
						}
					}

					Request.ReportFields(true, Fields);
				};


				List<ControlParameter> Parameters = new List<ControlParameter>();

				foreach (KeyValuePair<GpioPin, KeyValuePair<TextBlock, TextBlock>> Pin in gpioPins.Values)
				{
					string s = Pin.Key.PinNumber.ToString();

					Parameters.Add(new BooleanControlParameter("GPIO" + s, "Output", "GPIO " + s + ".", "If the GPIO output should be high (checked) or low (unchecked).",
						(Node) => Pin.Key.Read() == GpioPinValue.High,
						(Node, Value) =>
						{
							Pin.Key.Write(Value ? GpioPinValue.High : GpioPinValue.Low);
							Log.Informational("GPIO " + s + " turned " + (Value ? "HIGH" : "LOW"));
						}));

					List<string> Options = new List<string>();

					foreach (GpioPinDriveMode Option in Enum.GetValues(typeof(GpioPinDriveMode)))
					{
						if (Pin.Key.IsDriveModeSupported(Option))
							Options.Add(Option.ToString());
					}

					if (Options.Count > 0)
					{
						Parameters.Add(new StringControlParameter("GPIO" + s + "Mode", "Drive Mode", "GPIO " + s + " Drive Mode:",
							"The drive mode of the underlying hardware for the corresponding GPIO pin.", Options.ToArray(),
							(Node) => Pin.Key.GetDriveMode().ToString(),
							(Node, Value) =>
							{
								GpioPinDriveMode Mode = (GpioPinDriveMode)Enum.Parse(typeof(GpioPinDriveMode), Value);

								Pin.Key.SetDriveMode(Mode);
								Log.Informational("GPIO " + s + " drive mode set to " + Value);
							}));
					}
				}

				controlServer = new ControlServer(xmppClient, Parameters.ToArray());
				chatServer = new ChatServer(xmppClient, sensorServer, controlServer);
			}
			catch (Exception ex)
			{
				Log.Emergency(ex);

				MessageDialog Dialog = new MessageDialog(ex.Message, "Error");
				await Dialog.ShowAsync();
			}
		}

		private async void UpdateMainWindow(bool LampSwitch)
		{
			MainPage MainPage = MainPage.Instance;

			await MainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				((TextBlock)MainPage.FindName("Lamp")).Text = LampSwitch ? "ON" : "OFF";
			});
		}

		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		/// <param name="sender">The Frame which failed navigation</param>
		/// <param name="e">Details about the navigation failure</param>
		void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();

			if (arduino != null)
			{
				arduino.digitalWrite(13, PinState.LOW);
				arduino.pinMode(13, PinMode.INPUT);    // Onboard LED.

				arduino.Dispose();
				arduino = null;
			}

			if (arduinoUsb != null)
			{
				arduinoUsb.end();
				arduinoUsb.Dispose();
				arduinoUsb = null;
			}

			if (gpioPins != null)
			{
				foreach (KeyValuePair<GpioPin, KeyValuePair<TextBlock, TextBlock>> Pin in gpioPins.Values)
					Pin.Key.Dispose();

				gpioPins = null;
			}

			if (this.sampleTimer != null)
			{
				this.sampleTimer.Dispose();
				this.sampleTimer = null;
			}

			if (this.xmppClient != null)
			{
				this.xmppClient.Dispose();
				this.xmppClient = null;
			}

			if (this.chatServer != null)
			{
				this.chatServer.Dispose();
				this.chatServer = null;
			}

			if (this.controlServer != null)
			{
				this.controlServer.Dispose();
				this.controlServer = null;
			}

			if (this.sensorServer != null)
			{
				this.sensorServer.Dispose();
				this.sensorServer = null;
			}

			Waher.Script.Types.Terminate();
			Waher.Content.Markdown.Model.Multimedia.ImageContent.Terminate();

			deferral.Complete();
		}
	}
}
