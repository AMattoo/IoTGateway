﻿using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using Waher.Events;
using Waher.Events.Statistics;
using Waher.Networking.HTTP.HeaderFields;
using Waher.Networking.Sniffers;
using Waher.Runtime.Cache;
using Waher.Script;

namespace Waher.Networking.HTTP
{
	/// <summary>
	/// Implements an HTTP server.
	/// </summary>
	public class HttpServer : Sniffable, IDisposable
	{
		/// <summary>
		/// Default HTTP Port (80).
		/// </summary>
		public const int DefaultHttpPort = 80;

		/// <summary>
		/// Default HTTPS port (443).
		/// </summary>
		public const int DefaultHttpsPort = 443;

		/// <summary>
		/// Default Connection backlog (10).
		/// </summary>
		public const int DefaultConnectionBacklog = 10;

		/// <summary>
		/// Default buffer size (16384).
		/// </summary>
		public const int DefaultBufferSize = 16384;

		private static readonly Variables globalVariables = new Variables();

		private LinkedList<TcpListener> listeners = new LinkedList<TcpListener>();
		private Dictionary<string, HttpResource> resources = new Dictionary<string, HttpResource>(StringComparer.InvariantCultureIgnoreCase);
		private X509Certificate serverCertificate;
		private TimeSpan sessionTimeout = new TimeSpan(0, 20, 0);
		private TimeSpan requestTimeout = new TimeSpan(0, 2, 0);
		private Cache<HttpRequest, RequestInfo> currentRequests;
		private Cache<string, Variables> sessions;
		private string resourceOverride = null;
		private object statSynch = new object();
		private Dictionary<string, Statistic> callsPerMethod = new Dictionary<string, Statistic>();
		private Dictionary<string, Statistic> callsPerUserAgent = new Dictionary<string, Statistic>();
		private Dictionary<string, Statistic> callsPerFrom = new Dictionary<string, Statistic>();
		private Dictionary<string, Statistic> callsPerResource = new Dictionary<string, Statistic>();
		private DateTime lastStat = DateTime.MinValue;
		private long nrBytesRx = 0;
		private long nrBytesTx = 0;
		private long nrCalls = 0;
		private bool closed = false;

		#region Constructors

		/// <summary>
		/// Implements an HTTPS server.
		/// </summary>
		public HttpServer()
			: this(new int[] { DefaultHttpPort }, null, null)
		{
		}

		/// <summary>
		/// Implements an HTTPS server.
		/// </summary>
		/// <param name="HttpPort">HTTP Port</param>
		public HttpServer(int HttpPort)
			: this(new int[] { HttpPort }, null, null)
		{
		}

		/// <summary>
		/// Implements an HTTPS server.
		/// </summary>
		/// <param name="ServerCertificate">Server certificate identifying the domain of the server.</param>
		public HttpServer(X509Certificate ServerCertificate)
			: this(new int[] { DefaultHttpPort }, new int[] { DefaultHttpsPort }, ServerCertificate)
		{
		}

		/// <summary>
		/// Implements an HTTPS server.
		/// </summary>
		/// <param name="HttpPort">HTTP Port</param>
		/// <param name="HttpsPort">HTTPS Port</param>
		/// <param name="ServerCertificate">Server certificate identifying the domain of the server.</param>
		public HttpServer(int HttpPort, int HttpsPort, X509Certificate ServerCertificate)
			: this(new int[] { HttpPort }, new int[] { HttpsPort }, ServerCertificate)
		{
		}

		/// <summary>
		/// Implements an HTTPS server.
		/// </summary>
		/// <param name="HttpPorts">HTTP Ports</param>
		/// <param name="HttpsPorts">HTTPS Ports</param>
		/// <param name="ServerCertificate">Server certificate identifying the domain of the server.</param>
		public HttpServer(int[] HttpPorts, int[] HttpsPorts, X509Certificate ServerCertificate)
		{
			TcpListener Listener;

			this.serverCertificate = ServerCertificate;
			this.sessions = new Cache<string, Variables>(int.MaxValue, TimeSpan.MaxValue, this.sessionTimeout);
			this.sessions.Removed += Sessions_Removed;
			this.currentRequests = new Cache<HttpRequest, RequestInfo>(int.MaxValue, TimeSpan.MaxValue, this.requestTimeout);
			this.currentRequests.Removed += CurrentRequests_Removed;
			this.lastStat = DateTime.Now;

			foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (Interface.OperationalStatus != OperationalStatus.Up)
					continue;

				IPInterfaceProperties Properties = Interface.GetIPProperties();

				foreach (UnicastIPAddressInformation UnicastAddress in Properties.UnicastAddresses)
				{
					if ((UnicastAddress.Address.AddressFamily == AddressFamily.InterNetwork && Socket.OSSupportsIPv4) ||
						(UnicastAddress.Address.AddressFamily == AddressFamily.InterNetworkV6 && Socket.OSSupportsIPv6))
					{
						if (HttpPorts != null)
						{
							foreach (int HttpPort in HttpPorts)
							{
								try
								{
									Listener = new TcpListener(UnicastAddress.Address, HttpPort);
									Listener.Start(DefaultConnectionBacklog);
									Listener.BeginAcceptTcpClient(this.AcceptTcpClientCallback, new object[] { Listener, false });
									this.listeners.AddLast(Listener);
								}
								catch (Exception ex)
								{
									Log.Critical(ex, UnicastAddress.Address.ToString() + ":" + HttpPort);
								}
							}
						}

						if (HttpsPorts != null)
						{
							foreach (int HttpsPort in HttpsPorts)
							{
								try
								{
									Listener = new TcpListener(UnicastAddress.Address, HttpsPort);
									Listener.Start(DefaultConnectionBacklog);
									Listener.BeginAcceptTcpClient(this.AcceptTcpClientCallback, new object[] { Listener, true });
									this.listeners.AddLast(Listener);
								}
								catch (Exception ex)
								{
									Log.Critical(ex, UnicastAddress.Address.ToString() + ":" + HttpsPort);
								}
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.closed = true;

			if (this.listeners != null)
			{
				LinkedList<TcpListener> Listeners = this.listeners;
				this.listeners = null;

				foreach (TcpListener Listener in Listeners)
					Listener.Stop();
			}

			if (this.sessions != null)
			{
				this.sessions.Dispose();
				this.sessions = null;
			}
		}

		/// <summary>
		/// Ports successfully opened.
		/// </summary>
		public int[] OpenPorts
		{
			get
			{
				SortedDictionary<int, bool> Open = new SortedDictionary<int, bool>();

				if (this.listeners != null)
				{
					IPEndPoint IPEndPoint;

					foreach (TcpListener Listener in this.listeners)
					{
						IPEndPoint = Listener.LocalEndpoint as IPEndPoint;
						if (IPEndPoint != null)
							Open[IPEndPoint.Port] = true;
					}
				}

				int[] Result = new int[Open.Count];
				Open.Keys.CopyTo(Result, 0);

				return Result;
			}
		}

		#endregion

		#region Connections

		private void AcceptTcpClientCallback(IAsyncResult ar)
		{
			object[] P = (object[])ar.AsyncState;
			TcpListener Listener = (TcpListener)P[0];
			bool Https = (bool)P[1];

			try
			{
				if (!this.closed)
				{
					TcpClient Client = Listener.EndAcceptTcpClient(ar);

					this.Information("Connection accepted from " + Client.Client.RemoteEndPoint.ToString() + ".");

					if (Https)
					{
						this.Information("Switching to TLS.");

						NetworkStream NetworkStream = Client.GetStream();
						SslStream SslStream = new SslStream(NetworkStream);
						SslStream.BeginAuthenticateAsServer(this.serverCertificate, false, SslProtocols.Tls, true,
							this.AuthenticateAsServerCallback, new object[] { Client, SslStream, NetworkStream });
					}
					else
					{
						NetworkStream Stream = Client.GetStream();
						HttpClientConnection Connection = new HttpClientConnection(this, Client, Stream, Stream, DefaultBufferSize, false);

						if (this.HasSniffers)
						{
							foreach (ISniffer Sniffer in this.Sniffers)
								Connection.Add(Sniffer);
						}
					}

					Listener.BeginAcceptTcpClient(this.AcceptTcpClientCallback, P);
				}
			}
			catch (SocketException)
			{
				// Ignore
			}
			catch (Exception ex)
			{
				if (this.listeners == null)
					return;

				Log.Critical(ex);
			}
		}

		private void AuthenticateAsServerCallback(IAsyncResult ar)
		{
			object[] P = (object[])ar.AsyncState;
			TcpClient Client = (TcpClient)P[0];
			SslStream SslStream = (SslStream)P[1];
			NetworkStream NetworkStream = (NetworkStream)P[2];

			try
			{
				SslStream.EndAuthenticateAsServer(ar);

				this.Information("TLS established.");

				HttpClientConnection Connection = new HttpClientConnection(this, Client, SslStream, NetworkStream, DefaultBufferSize, true);

				if (this.HasSniffers)
				{
					foreach (ISniffer Sniffer in this.Sniffers)
						Connection.Add(Sniffer);
				}
			}
			catch (SocketException)
			{
				Client.Close();
			}
			catch (Exception ex)
			{
				Client.Close();
				Log.Critical(ex);
			}
		}

		#endregion

		#region Resources

		/// <summary>
		/// By default, this property is null. If not null, or empty, every request made to the web server will
		/// be redirected to this resource.
		/// </summary>
		public string ResourceOverride
		{
			get { return this.resourceOverride; }
			set { this.resourceOverride = value; }
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="Resource">Resource</param>
		/// <exception cref="Exception">If a resource with the same resource name is already registered.</exception>
		public void Register(HttpResource Resource)
		{
			lock (this.resources)
			{
				if (!this.resources.ContainsKey(Resource.ResourceName))
					this.resources[Resource.ResourceName] = Resource;
				else
					throw new Exception("Resource name already registered.");
			}
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(ResourceName, GET, true, false, false, AuthenticationSchemes);
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="Synchronous">If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, bool Synchronous, params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(ResourceName, GET, Synchronous, false, false, AuthenticationSchemes);
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="Synchronous">If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).</param>
		/// <param name="HandlesSubPaths">If sub-paths are handled.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, bool Synchronous, bool HandlesSubPaths,
			params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(ResourceName, GET, Synchronous, HandlesSubPaths, false, AuthenticationSchemes);
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="Synchronous">If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).</param>
		/// <param name="HandlesSubPaths">If sub-paths are handled.</param>
		/// <param name="UserSessions">If the resource uses user sessions.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, bool Synchronous, bool HandlesSubPaths,
			bool UserSessions, params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(new HttpGetDelegateResource(ResourceName, GET, Synchronous, HandlesSubPaths, UserSessions, AuthenticationSchemes));
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="POST">PSOT method handler.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, HttpMethodHandler POST, params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(ResourceName, GET, POST, true, false, false, AuthenticationSchemes);
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="POST">PSOT method handler.</param>
		/// <param name="Synchronous">If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, HttpMethodHandler POST, bool Synchronous,
			params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(ResourceName, GET, POST, Synchronous, false, false, AuthenticationSchemes);
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="POST">PSOT method handler.</param>
		/// <param name="Synchronous">If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).</param>
		/// <param name="HandlesSubPaths">If sub-paths are handled.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, HttpMethodHandler POST, bool Synchronous, bool HandlesSubPaths,
			params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(ResourceName, GET, POST, Synchronous, HandlesSubPaths, false, AuthenticationSchemes);
		}

		/// <summary>
		/// Registers a resource with the server.
		/// </summary>
		/// <param name="ResourceName">Resource Name.</param>
		/// <param name="GET">GET method handler.</param>
		/// <param name="POST">PSOT method handler.</param>
		/// <param name="Synchronous">If the resource is synchronous (i.e. returns a response in the method handler), or if it is asynchronous
		/// (i.e. sends the response from another thread).</param>
		/// <param name="HandlesSubPaths">If sub-paths are handled.</param>
		/// <param name="UserSessions">If the resource uses user sessions.</param>
		/// <param name="AuthenticationSchemes">Any authentication schemes used to authenticate users before access is granted.</param>
		public void Register(string ResourceName, HttpMethodHandler GET, HttpMethodHandler POST, bool Synchronous, bool HandlesSubPaths,
			bool UserSessions, params HttpAuthenticationScheme[] AuthenticationSchemes)
		{
			this.Register(new HttpGetPostDelegateResource(ResourceName, GET, POST, Synchronous, HandlesSubPaths, UserSessions,
				AuthenticationSchemes));
		}

		/// <summary>
		/// Unregisters a resource from the server.
		/// </summary>
		/// <param name="Resource">Resource to unregister.</param>
		/// <returns>If the resource was found and removed.</returns>
		public bool Unregister(HttpResource Resource)
		{
			if (Resource == null)
				return false;

			lock (this.resources)
			{
				if (this.resources.TryGetValue(Resource.ResourceName, out HttpResource Resource2) && Resource2 == Resource)
				{
					this.resources.Remove(Resource.ResourceName);
					return true;
				}
				else
					return false;
			}
		}

		/// <summary>
		/// Tries to get a resource from the server.
		/// </summary>
		/// <param name="ResourceName">Full resource name.</param>
		/// <param name="Resource">Resource matching the full resource name.</param>
		/// <param name="SubPath">Trailing end of full resource name, relative to the best resource that was found.</param>
		/// <returns>If a resource was found matching the full resource name.</returns>
		public bool TryGetResource(string ResourceName, out HttpResource Resource, out string SubPath)
		{
			int i;

			if (!string.IsNullOrEmpty(this.resourceOverride))
				ResourceName = this.resourceOverride;

			SubPath = string.Empty;

			lock (this.resources)
			{
				while (true)
				{
					if (this.resources.TryGetValue(ResourceName, out Resource))
					{
						if (Resource.HandlesSubPaths || string.IsNullOrEmpty(SubPath))
							return true;
					}

					i = ResourceName.LastIndexOf('/');
					if (i < 0)
						break;

					SubPath = ResourceName.Substring(i) + SubPath;
					ResourceName = ResourceName.Substring(0, i);
				}
			}

			Resource = null;

			return false;
		}

		#endregion

		#region Sessions

		/// <summary>
		/// Session timeout. Default is 20 minutes.
		/// </summary>
		public TimeSpan SessionTimeout
		{
			get { return this.sessionTimeout; }

			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentException("The session timeout must be positive.");

				this.sessionTimeout = value;
				this.sessions.MaxTimeUnused = value;
			}
		}

		/// <summary>
		/// Gets the set of session states corresponing to a given session ID. If no such session is known, a new is created.
		/// </summary>
		/// <param name="SessionId">Session ID</param>
		/// <returns>Session states.</returns>
		public Variables GetSession(string SessionId)
		{
			if (this.sessions.TryGetValue(SessionId, out Variables Result))
				return Result;

			Result = new Variables()
			{
				{ "Global", globalVariables }
			};

			this.sessions.Add(SessionId, Result);

			return Result;
		}

		private void Sessions_Removed(object Sender, CacheItemEventArgs<string, Variables> e)
		{
			CacheItemEventHandler<string, Variables> h = this.SessionRemoved;
			if (h != null)
			{
				try
				{
					h(this, e);
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}
			}
		}

		/// <summary>
		/// Event raised when a session has been closed.
		/// </summary>
		public event CacheItemEventHandler<string, Variables> SessionRemoved = null;

		#endregion

		#region Statistics

		/// <summary>
		/// Call this method when data has been received.
		/// </summary>
		/// <param name="NrRead">Number of bytes received.</param>
		internal void DataReceived(int NrRead)
		{
			lock (this.statSynch)
			{
				this.nrBytesRx += NrRead;
			}
		}

		/// <summary>
		/// Call this method when data has been written back to a client.
		/// </summary>
		/// <param name="NrWritten">Number of bytes transmitted.</param>
		internal void DataTransmitted(int NrWritten)
		{
			lock (this.statSynch)
			{
				this.nrBytesTx += NrWritten;
			}
		}

		/// <summary>
		/// Registers an incoming request.
		/// 
		/// Note: Each call to <see cref="RequestReceived"/> must be followed by a call to
		/// <see cref="RequestResponded"/>.
		/// </summary>
		/// <param name="Request">Request object.</param>
		/// <param name="ClientAddress">Address of client, from where the request was received.</param>
		/// <param name="Resource">Matching resource, if found, or null, if not found.</param>
		/// <param name="SubPath">Sub-path of request.</param>
		public void RequestReceived(HttpRequest Request, string ClientAddress, HttpResource Resource, string SubPath)
		{
			if (Request == null)
				return;

			HttpFieldUserAgent UserAgent;
			HttpFieldFrom From;

			lock (this.statSynch)
			{
				this.nrCalls++;

				this.IncLocked(Request.Header.Method, this.callsPerMethod);
				this.IncLocked(Resource.ResourceName, this.callsPerResource);

				if ((UserAgent = Request.Header.UserAgent) != null)
					this.IncLocked(UserAgent.Value, this.callsPerUserAgent);

				if ((From = Request.Header.From) != null)
					this.IncLocked(From.Value, this.callsPerFrom);
				else
				{
					string s = Request.RemoteEndPoint;
					int i = s.LastIndexOf(':');
					if (i > 0)
						s = s.Substring(0, i);

					this.IncLocked(s, this.callsPerFrom);
				}
			}

			RequestInfo Info = new RequestInfo()
			{
				ClientAddress = ClientAddress,
				Resource = Resource,
				SubPath = SubPath,
				ResourceStr = Request.Header.Resource,
				Method = Request.Header.Method
			};

			this.currentRequests.Add(Request, Info);
		}

		private void IncLocked(string Key, Dictionary<string, Statistic> Stat)
		{
			if (!Stat.TryGetValue(Key, out Statistic Rec))
			{
				Rec = new Statistic(1);
				Stat[Key] = Rec;
			}
			else
				Rec.Inc();
		}

		/// <summary>
		/// Gets communication statistics since last call.
		/// </summary>
		/// <returns>Communication statistics.</returns>
		public CommunicationStatistics GetCommunicationStatisticsSinceLast()
		{
			CommunicationStatistics Result;
			DateTime TP = DateTime.Now;

			lock (this.statSynch)
			{
				Result = new CommunicationStatistics()
				{
					CallsPerMethod = this.callsPerMethod,
					CallsPerUserAgent = this.callsPerUserAgent,
					CallsPerFrom = this.callsPerFrom,
					CallsPerResource = this.callsPerResource,
					LastStat = this.lastStat,
					CurrentStat = TP,
					NrBytesRx = this.nrBytesRx,
					NrBytesTx = this.nrBytesTx,
					NrCalls = this.nrCalls
				};

				this.callsPerMethod = new Dictionary<string, Statistic>();
				this.callsPerUserAgent = new Dictionary<string, Statistic>();
				this.callsPerFrom = new Dictionary<string, Statistic>();
				this.callsPerResource = new Dictionary<string, Statistic>();
				this.lastStat = TP;
				this.nrBytesRx = 0;
				this.nrBytesTx = 0;
				this.nrCalls = 0;
			}

			return Result;
		}

		private class RequestInfo
		{
			public DateTime Received = DateTime.Now;
			public HttpResource Resource;
			public string ClientAddress;
			public string SubPath;
			public string Method;
			public string ResourceStr;
			public int? StatusCode = null;
		}

		/// <summary>
		/// Registers an outgoing response to a requesst.
		/// </summary>
		/// <param name="Request">Original request object.</param>
		/// <param name="StatusCode">Status code.</param>
		public void RequestResponded(HttpRequest Request, int StatusCode)
		{
			if (this.currentRequests.TryGetValue(Request, out RequestInfo Info))
			{
				Info.StatusCode = StatusCode;
				this.currentRequests.Remove(Request);
			}
			else if (StatusCode != 0)
			{
				Log.Warning("Late response.", Request.Header.Resource,
					new KeyValuePair<string, object>("Response", StatusCode),
					new KeyValuePair<string, object>("Method", Request.Header.Method));
			}
		}

		/// <summary>
		/// Keeps the request alive, without timing out
		/// </summary>
		/// <param name="Request">Request.</param>
		/// <returns>If request found among current requests.</returns>
		public bool PingRequest(HttpRequest Request)
		{
			return this.currentRequests.TryGetValue(Request, out RequestInfo Info);
		}

		private void CurrentRequests_Removed(object Sender, CacheItemEventArgs<HttpRequest, RequestInfo> e)
		{
			RequestInfo Info = e.Value;

			if (e.Reason != RemovedReason.Manual)
			{
				Log.Warning("HTTP request timed out.", Info.ResourceStr,
					new KeyValuePair<string, object>("From", Info.ClientAddress),
					new KeyValuePair<string, object>("Method", Info.Method));
			}
		}

		/// <summary>
		/// Request timeout. Default is 2 minutes.
		/// </summary>
		public TimeSpan RequestTimeout
		{
			get { return this.requestTimeout; }

			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentException("The request timeout must be positive.");

				this.requestTimeout = value;
				this.currentRequests.MaxTimeUnused = value;
			}
		}

		#endregion

		// TODO: Web Service resources
	}
}
