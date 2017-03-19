﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Waher.Content;
using Waher.Events;
using Waher.Networking.HTTP;
using Waher.Networking.XMPP.InBandBytestreams;
using Waher.Networking.XMPP.P2P;

namespace Waher.Networking.XMPP.HTTPX
{
	/// <summary>
	/// Implements a Proxy resource that allows Web clients to fetch HTTP-based resources over HTTPX.
	/// </summary>
	public class HttpxProxy : HttpAsynchronousResource, IDisposable, IHttpGetMethod, IHttpGetRangesMethod, IHttpOptionsMethod,
		IHttpPostMethod, IHttpPostRangesMethod, IHttpPutMethod, IHttpPutRangesMethod, IHttpTraceMethod
	{
		private XmppClient defaultXmppClient;
		private HttpxClient httpxClient;
		private XmppServerlessMessaging serverlessMessaging;
		private IHttpxCache httpxCache;
		private IbbClient ibbClient = null;

		/// <summary>
		/// Implements a Proxy resource that allows Web clients to fetch HTTP-based resources over HTTPX.
		/// </summary>
		/// <param name="ResourceName">Resource name of proxy resource.</param>
		/// <param name="DefaultXmppClient">Default XMPP client.</param>
		/// <param name="MaxChunkSize">Max Chunk Size to use.</param>
		public HttpxProxy(string ResourceName, XmppClient DefaultXmppClient, int MaxChunkSize)
			: this(ResourceName, DefaultXmppClient, MaxChunkSize, null, null)
		{
		}

		/// <summary>
		/// Implements a Proxy resource that allows Web clients to fetch HTTP-based resources over HTTPX.
		/// </summary>
		/// <param name="ResourceName">Resource name of proxy resource.</param>
		/// <param name="DefaultXmppClient">Default XMPP client.</param>
		/// <param name="MaxChunkSize">Max Chunk Size to use.</param>
		/// <param name="ServerlessMessaging">Serverless messaging manager.</param>
		public HttpxProxy(string ResourceName, XmppClient DefaultXmppClient, int MaxChunkSize, XmppServerlessMessaging ServerlessMessaging)
			: this(ResourceName, DefaultXmppClient, MaxChunkSize, ServerlessMessaging, null)
		{
		}

		/// <summary>
		/// Implements a Proxy resource that allows Web clients to fetch HTTP-based resources over HTTPX.
		/// </summary>
		/// <param name="ResourceName">Resource name of proxy resource.</param>
		/// <param name="DefaultXmppClient">Default XMPP client.</param>
		/// <param name="MaxChunkSize">Max Chunk Size to use.</param>
		/// <param name="ServerlessMessaging">Serverless messaging manager.</param>
		public HttpxProxy(string ResourceName, XmppClient DefaultXmppClient, int MaxChunkSize, XmppServerlessMessaging ServerlessMessaging,
			IHttpxCache HttpxCache) : base(ResourceName)
		{
			this.defaultXmppClient = DefaultXmppClient;
			this.httpxClient = new HttpxClient(this.defaultXmppClient, MaxChunkSize);
			this.serverlessMessaging = ServerlessMessaging;
			this.httpxCache = HttpxCache;
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			if (this.httpxClient != null)
			{
				this.httpxClient.Dispose();
				this.httpxClient = null;
			}
		}

		/// <summary>
		/// Serverless messaging manager.
		/// </summary>
		public XmppServerlessMessaging ServerlessMessaging
		{
			get { return this.serverlessMessaging; }
			set
			{
				if (this.serverlessMessaging != null && this.serverlessMessaging != value)
					throw new Exception("Property already set.");

				this.serverlessMessaging = value;
			}
		}

		/// <summary>
		/// Reference to the HTTPX Cache manager.
		/// </summary>
		public IHttpxCache HttpxCache
		{
			get { return this.httpxCache; }
			set
			{
				if (this.httpxCache != null && this.httpxCache != value)
					throw new Exception("Property already set.");

				this.httpxCache = value;
			}
		}

		/// <summary>
		/// Default XMPP client.
		/// </summary>
		public XmppClient DefaultXmppClient
		{
			get { return this.defaultXmppClient; }
		}

		/// <summary>
		/// Default HTTPX client.
		/// </summary>
		public HttpxClient DefaultHttpxClient
		{
			get { return this.httpxClient; }
		}

		/// <summary>
		/// In-band bytestream client, if supported.
		/// </summary>
		public IbbClient IbbClient
		{
			get { return this.ibbClient; }
			set
			{
				this.ibbClient = value;

				if (this.httpxClient != null)
					this.httpxClient.IbbClient = value;
			}
		}

		public override bool HandlesSubPaths
		{
			get
			{
				return true;
			}
		}

		public override bool UserSessions
		{
			get
			{
				return false;
			}
		}

		private async void Request(string Method, HttpRequest Request, HttpResponse Response)
		{
			try
			{
				string Url = Request.SubPath;
				if (Url.StartsWith("/"))
					Url = Url.Substring(1);

				if (!Url.StartsWith("httpx://", StringComparison.OrdinalIgnoreCase))
					throw new BadRequestException();

				int i = Url.IndexOf('/', 8);
				if (i < 0)
					throw new BadRequestException();

				string BareJID = Url.Substring(8, i - 8);
				string LocalUrl = Url.Substring(i);

				IHttpxCachedResource CachedResource;

				if (Method == "GET" && this.httpxCache != null)
				{
					if ((CachedResource = await this.httpxCache.TryGetCachedResource(BareJID, LocalUrl)) != null)
					{
						if (Request.Header.IfNoneMatch != null)
						{
							if (CachedResource.ETag != null && Request.Header.IfNoneMatch.Value == CachedResource.ETag)
								throw new NotModifiedException();
						}
						else if (Request.Header.IfModifiedSince != null)
						{
							DateTimeOffset? Limit;

							if ((Limit = Request.Header.IfModifiedSince.Timestamp).HasValue &&
								HttpFolderResource.LessOrEqual(CachedResource.LastModified.UtcDateTime, Limit.Value.ToUniversalTime()))
							{
								throw new NotModifiedException();
							}
						}

						HttpFolderResource.SendResponse(CachedResource.FileName, CachedResource.ContentType, CachedResource.ETag,
							CachedResource.LastModified.UtcDateTime, Response);

						return;
					}
				}

				RosterItem Item = this.defaultXmppClient.GetRosterItem(BareJID);
				if (Item == null)
				{
					if (!XmppClient.BareJidRegEx.IsMatch(BareJID))
						throw new BadRequestException();

					// TODO: Request presence subscription, if user authenticated and request valid.

					throw new ConflictException();  // TODO: Provide body describing error.
				}
				else if (Item.HasLastPresence)
				{
					if (this.serverlessMessaging != null)
					{
						this.serverlessMessaging.GetPeerConnection(BareJID, (sender, e) =>
						{
							try
							{
								if (e.Client == null)
									this.SendRequest(this.httpxClient, Item.LastPresenceFullJid, Method, BareJID, LocalUrl, Request, Response);
								else
								{
									HttpxClient HttpxClient;
									object Obj;

									if (e.Client.SupportsFeature(HttpxClient.Namespace) &&
										e.Client.TryGetTag("HttpxClient", out Obj) &&
										(HttpxClient = Obj as HttpxClient) != null)
									{
										this.SendRequest(HttpxClient, BareJID, Method, BareJID, LocalUrl, Request, Response);
									}
									else
										this.SendRequest(this.httpxClient, Item.LastPresenceFullJid, Method, BareJID, LocalUrl, Request, Response);
								}
							}
							catch (Exception ex)
							{
								Response.SendResponse(ex);
							}
						}, null);
					}
					else
						this.SendRequest(this.httpxClient, Item.LastPresenceFullJid, Method, BareJID, LocalUrl, Request, Response);
				}
				else
					throw new ServiceUnavailableException();
			}
			catch (Exception ex)
			{
				Response.SendResponse(ex);
			}
		}

		private void SendRequest(HttpxClient HttpxClient, string To, string Method, string BareJID, string LocalUrl,
			HttpRequest Request, HttpResponse Response)
		{
			LinkedList<HttpField> Headers = new LinkedList<HttpField>();

			foreach (HttpField Header in Request.Header)
			{
				switch (Header.Key.ToLower())
				{
					case "host":
						Headers.AddLast(new HttpField("Host", BareJID));
						break;

					case "cookie":
					case "set-cookie":
						// Do not forward cookies.
						break;

					default:
						Headers.AddLast(Header);
						break;
				}
			}

			ReadoutState State = new ReadoutState(Response, BareJID, LocalUrl);
			State.Cacheable = (Method == "GET" && this.httpxCache != null);

			string s = LocalUrl;
			int i = s.IndexOf('.');
			if (i > 0)
			{
				s = s.Substring(i + 1);
				i = s.IndexOfAny(new char[] { '?', '#' });
				if (i > 0)
					s = s.Substring(0, i);

				if (this.httpxCache.CanCache(BareJID, LocalUrl, InternetContent.GetContentType(s)))
				{
					LinkedListNode<HttpField> Loop = Headers.First;
					LinkedListNode<HttpField> Next;

					while (Loop != null)
					{
						Next = Loop.Next;

						switch (Loop.Value.Key.ToLower())
						{
							case "if-match":
							case "if-modified-since":
							case "if-none-match":
							case "if-range":
							case "if-unmodified-since":
								Headers.Remove(Loop);
								break;
						}

						Loop = Next;
					}
				}
			}

			HttpxClient.Request(To, Method, LocalUrl, Request.Header.HttpVersion, Headers, Request.HasData ? Request.DataStream : null,
				(sender, e) =>
				{
					ReadoutState State2 = (ReadoutState)e.State;

					State2.Response.StatusCode = e.StatusCode;
					State2.Response.StatusMessage = e.StatusMessage;

					if (e.HttpResponse != null)
					{
						foreach (KeyValuePair<string, string> Field in e.HttpResponse.GetHeaders())
						{
							switch (Field.Key.ToLower())
							{
								case "cookie":
								case "set-cookie":
								// Do not forward cookies.
								break;

								case "content-type":
									State2.ContentType = Field.Value;
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;

								case "etag":
									State2.ETag = Field.Value;
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;

								case "last-modified":
									DateTimeOffset TP;
									if (CommonTypes.TryParseRfc822(Field.Value, out TP))
										State2.LastModified = TP;
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;

								case "expires":
									if (CommonTypes.TryParseRfc822(Field.Value, out TP))
										State2.Expires = TP;
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;

								case "cache-control":
									State2.CacheControl = Field.Value;
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;

								case "pragma":
									State2.Pragma = Field.Value;
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;

								default:
									State2.Response.SetHeader(Field.Key, Field.Value);
									break;
							}
						}
					}

					if (!e.HasData)
						State2.Response.SendResponse();
					else
					{
						if (e.StatusCode == 200 && State2.Cacheable && State2.CanCache &&
							this.httpxCache.CanCache(State2.BareJid, State2.LocalResource, State2.ContentType))
						{
							State2.TempOutput = new TemporaryFile();
						}

						if (e.Data != null)
							this.BinaryDataReceived(State2, true, e.Data);
					}
				}, (sender, e) =>
				{
					ReadoutState State2 = (ReadoutState)e.State;

					this.BinaryDataReceived(State2, e.Last, e.Data);

				}, State);
		}

		private void BinaryDataReceived(ReadoutState State2, bool Last, byte[] Data)
		{
			State2.Response.Write(Data);

			if (State2.TempOutput != null)
				State2.TempOutput.Write(Data, 0, Data.Length);

			if (Last)
			{
				State2.Response.SendResponse();

				Task.Run(async () =>
				{
					try
					{
						if (State2.TempOutput != null)
						{
							State2.TempOutput.Position = 0;

							await this.httpxCache.AddToCache(State2.BareJid, State2.LocalResource, State2.ContentType, State2.ETag,
								State2.LastModified.Value, State2.Expires, State2.TempOutput);
						}
					}
					catch (Exception ex)
					{
						Log.Critical(ex);
					}
					finally
					{
						try
						{
							State2.Dispose();
						}
						catch (Exception ex2)
						{
							Log.Critical(ex2);
						}
					}
				});
			}
		}

		private class ReadoutState : IDisposable
		{
			public bool Cacheable = false;
			public HttpResponse Response;
			public string ETag = null;
			public string BareJid = null;
			public string LocalResource = null;
			public string ContentType = null;
			public string CacheControl = null;
			public string Pragma = null;
			public DateTimeOffset? Expires = null;
			public DateTimeOffset? LastModified = null;
			public TemporaryFile TempOutput = null;

			public ReadoutState(HttpResponse Response, string BareJid, string LocalResource)
			{
				this.Response = Response;
				this.BareJid = BareJid;
				this.LocalResource = LocalResource;
			}

			public bool CanCache
			{
				get
				{
					if (this.ETag == null || !this.LastModified.HasValue)
						return false;

					if (this.CacheControl != null)
					{
						if ((this.CacheControl.Contains("no-cache") || this.CacheControl.Contains("no-store")))
							return false;

						if (!this.Expires.HasValue)
						{
							string s = this.CacheControl;
							int i = s.IndexOf("max-age");
							int c = s.Length;
							char ch;

							while (i < c && ((ch = s[i]) <= ' ' || ch == '=' || ch == 160))
								i++;

							int j = i;

							while (j < c && (ch = s[j]) >= '0' && ch <= '9')
								j++;

							if (j > i && int.TryParse(s.Substring(i, j - i), out j))
								this.Expires = DateTimeOffset.UtcNow.AddSeconds(j);
						}
					}

					if (this.Pragma != null && this.Pragma.Contains("no-cache"))
						return false;

					return true;
				}
			}

			public void Dispose()
			{
				if (this.TempOutput != null)
				{
					this.TempOutput.Dispose();
					this.TempOutput = null;
				}
			}
		}

		public void GET(HttpRequest Request, HttpResponse Response)
		{
			this.Request("GET", Request, Response);
		}

		public void GET(HttpRequest Request, HttpResponse Response, ByteRangeInterval FirstInterval)
		{
			this.Request("GET", Request, Response);
		}

		public void OPTIONS(HttpRequest Request, HttpResponse Response)
		{
			this.Request("OPTIONS", Request, Response);
		}

		public void POST(HttpRequest Request, HttpResponse Response)
		{
			this.Request("POST", Request, Response);
		}

		public void POST(HttpRequest Request, HttpResponse Response, ContentByteRangeInterval Interval)
		{
			this.Request("POST", Request, Response);
		}

		public void PUT(HttpRequest Request, HttpResponse Response)
		{
			this.Request("PUT", Request, Response);
		}

		public void PUT(HttpRequest Request, HttpResponse Response, ContentByteRangeInterval Interval)
		{
			this.Request("PUT", Request, Response);
		}

		public void TRACE(HttpRequest Request, HttpResponse Response)
		{
			this.Request("TRACE", Request, Response);
		}
	}
}
