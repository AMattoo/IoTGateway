﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Waher.Events;
using Waher.Events.Console;
using Waher.Events.Files;
using Waher.IoTGateway.Svc.ServiceManagement;
using Waher.IoTGateway.Svc.ServiceManagement.Enumerations;
using Waher.IoTGateway.Svc.ServiceManagement.Structures;

namespace Waher.IoTGateway.Svc
{
	/// <summary>
	/// IoT Gateway Windows Service Application.
	/// 
	/// Command line switches:
	/// 
	/// -?                   Brings this help.
	/// -install             Installs service in operating system
	/// -uninstall           Uninstalls service from operating system.
	/// -displayname Name    Sets the display name of the service. Default is "IoT Gateway Service".
	/// -description Desc    Sets the textual description of the service. Default is "Windows Service hosting the Waher IoT Gateway.".
	/// -start Mode          Sets the default starting mode of the service. Default is Disabled. Available options are StartOnBoot, StartOnSystemStart, AutoStart, StartOnDemand and Disabled
	/// -immediate           If the service should be started immediately.
	/// -console             Run the service as a console application.
	/// -localsystem         Installed service will run using the Local System account.
	/// -localservice        Installed service will run using the Local Service account (default).
	/// -networkservice      Installed service will run using the Network Service account.
	/// </summary>
	public class Program
	{
		public static int Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				Log.Error("Unhandled exception caught.", new KeyValuePair<string, object>("IsTerminating", e.IsTerminating));

				if (e.ExceptionObject is Exception ex)
					Log.Critical(ex);
				else if (e.ExceptionObject != null)
					Log.Critical(e.ExceptionObject.ToString());
				else
					Log.Critical("Unexpected null exception thrown.");
			};

			AppDomain.CurrentDomain.DomainUnload += (sender, e) =>
			{
				Log.Informational("Unloading domain.");
			};

			AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
			{
				Log.Informational("Exiting process.");
				Log.Terminate();
			};

			try
			{
				string ServiceName = "IoT Gateway Service";
				string DisplayName = ServiceName;
				string Description = "Windows Service hosting the Waher IoT Gateway.";
				string Arg;
				ServiceStartType StartType = ServiceStartType.Disabled;
				Win32ServiceCredentials Credentials = Win32ServiceCredentials.LocalService;
				bool Install = false;
				bool Uninstall = false;
				bool Immediate = false;
				bool AsConsole = false;
				bool Error = false;
				bool Help = false;
				int i, c = args.Length;

				Log.RegisterExceptionToUnnest(typeof(System.Runtime.InteropServices.ExternalException));
				Log.RegisterExceptionToUnnest(typeof(System.Security.Authentication.AuthenticationException));

				Log.Register(new Waher.Events.WindowsEventLog.WindowsEventLog("IoTGateway"));
				Log.Informational("Program started.");

				for (i = 0; i < c; i++)
				{
					Arg = args[i];

					switch (Arg.ToLower())
					{
						case "-?":
							Help = true;
							break;

						case "-install":
							Install = true;
							break;

						case "-uninstall":
							Uninstall = true;
							break;

						case "-immediate":
							Immediate = true;
							break;

						case "-console":
							AsConsole = true;
							break;

						case "-displayname":
							i++;
							if (i >= c)
							{
								Error = true;
								break;
							}

							DisplayName = args[i];
							break;

						case "-description":
							i++;
							if (i >= c)
							{
								Error = true;
								break;
							}

							Description = args[i];
							break;

						case "-start":
							i++;
							if (i >= c)
							{
								Error = true;
								break;
							}

							if (!Enum.TryParse<ServiceStartType>(args[i], out StartType))
							{
								Error = true;
								break;
							}
							break;

						case "-localsystem":
							Credentials = Win32ServiceCredentials.LocalSystem;
							break;

						case "-localservice":
							Credentials = Win32ServiceCredentials.LocalService;
							break;

						case "-networkservice":
							Credentials = Win32ServiceCredentials.NetworkService;
							break;

						default:
							Error = true;
							break;
					}
				}

				if (Error || Help)
				{
					Log.Informational("Displaying help.");

					Console.Out.WriteLine("IoT Gateway Windows Service Application.");
					Console.Out.WriteLine();
					Console.Out.WriteLine("Command line switches:");
					Console.Out.WriteLine();
					Console.Out.WriteLine("-?                   Brings this help.");
					Console.Out.WriteLine("-install             Installs service in operating system");
					Console.Out.WriteLine("-uninstall           Uninstalls service from operating system.");
					Console.Out.WriteLine("-displayname Name    Sets the display name of the service. Default is \"IoT ");
					Console.Out.WriteLine("                     Gateway Service\".");
					Console.Out.WriteLine("-description Desc    Sets the textual description of the service. Default is ");
					Console.Out.WriteLine("                     \"Windows Service hosting the Waher IoT Gateway.\".");
					Console.Out.WriteLine("-start Mode          Sets the default starting mode of the service. Default is ");
					Console.Out.WriteLine("                     Disabled. Available options are StartOnBoot, ");
					Console.Out.WriteLine("                     StartOnSystemStart, AutoStart, StartOnDemand and Disabled.");
					Console.Out.WriteLine("-immediate           If the service should be started immediately.");
					Console.Out.WriteLine("-console             Run the service as a console application.");
					Console.Out.WriteLine("-localsystem         Installed service will run using the Local System account.");
					Console.Out.WriteLine("-localservice        Installed service will run using the Local Service account");
					Console.Out.WriteLine("                     (default).");
					Console.Out.WriteLine("-networkservice      Installed service will run using the Network Service");
					Console.Out.WriteLine("                     account.");

					return -1;
				}

				if (Install && Uninstall)
				{
					Log.Error("Conflicting arguments.");
					Console.Out.Write("Conflicting arguments.");
					return -1;
				}

				if (Install)
				{
					Log.Informational("Installing service.");
					InstallService(ServiceName, DisplayName, Description, StartType, Immediate, Credentials);
				}
				else if (Uninstall)
				{
					Log.Informational("Uninstalling service.");
					UninstallService(ServiceName);
				}
				else if (AsConsole)
				{
					Log.Informational("Running as console application.");
					RunAsConsole();
				}
				else
				{
					Log.Informational("Running as service application.");
					RunAsService(ServiceName);
				}

				return 0;
			}
			catch (Exception ex)
			{
				Log.Critical(ex);
				Console.Out.WriteLine(ex.Message);
				return -1;
			}
		}

		private static void RunAsService(string ServiceName)
		{
			try
			{
				Log.Informational("Starting service.");

				ServiceHost host = new ServiceHost(ServiceName);
				host.Run();
			}
			catch (Exception ex)
			{
				Log.Critical(ex);
				throw;
			}
			finally
			{
				Log.Informational("Service terminated.");
			}
		}

		private static void RunAsConsole()
		{
			try
			{
				Console.ForegroundColor = ConsoleColor.White;

				Console.Out.WriteLine("Welcome to the Internet of Things Gateway server application.");
				Console.Out.WriteLine(new string('-', 79));
				Console.Out.WriteLine("This server application will help you manage IoT devices and");
				Console.Out.WriteLine("create dynamic content that you can publish on the Internet.");
				Console.Out.WriteLine("It also provides programming interfaces (API) which allow you");
				Console.Out.WriteLine("to dynamically and securely interact with the devices and the");
				Console.Out.WriteLine("content you publish.");

				Log.Register(new ConsoleEventSink(false));

				if (!Gateway.Start(true))
				{
					System.Console.Out.WriteLine();
					System.Console.Out.WriteLine("Gateway being started in another process.");
					return;
				}

				ManualResetEvent Done = new ManualResetEvent(false);
				Console.CancelKeyPress += (sender, e) => Done.Set();

				try
				{
					Win32.SetConsoleCtrlHandler((ControlType) =>
					{
						switch (ControlType)
						{
							case CtrlTypes.CTRL_BREAK_EVENT:
							case CtrlTypes.CTRL_CLOSE_EVENT:
							case CtrlTypes.CTRL_C_EVENT:
							case CtrlTypes.CTRL_SHUTDOWN_EVENT:
								Done.Set();
								break;

							case CtrlTypes.CTRL_LOGOFF_EVENT:
								break;
						}

						return true;
					}, true);
				}
				catch (Exception)
				{
					Log.Error("Unable to register CTRL-C control handler.");
				}

				while (!Done.WaitOne(1000))
					;
			}
			catch (Exception ex)
			{
				Log.Critical(ex);
			}
			finally
			{
				Gateway.Stop();
			}
		}

		private static void InstallService(string ServiceName, string DisplayName, string Description, ServiceStartType StartType, bool Immediate,
			Win32ServiceCredentials Credentials)
		{
			ServiceHost host = new ServiceHost(ServiceName);
			int i;

			switch (i = host.Install(DisplayName, Description, StartType, Immediate, Credentials))
			{
				case 0:
					Console.Out.WriteLine("Service successfully installed. Service start is pending.");
					break;

				case 1:
					Console.Out.WriteLine("Service successfully installed and started.");
					break;

				case 2:
					Console.Out.WriteLine("Service registration successfully updated. Service start is pending.");
					break;

				case 3:
					Console.Out.WriteLine("Service registration successfully updated. Service started.");
					break;

				default:
					throw new Exception("Unexpected installation result: " + i.ToString());
			}
		}

		private static void UninstallService(string ServiceName)
		{
			ServiceHost host = new ServiceHost(ServiceName);

			if (host.Uninstall())
				Console.Out.WriteLine("Service successfully uninstalled.");
			else
				Console.Out.WriteLine("Service not found. Uninstall not required.");
		}

	}
}
