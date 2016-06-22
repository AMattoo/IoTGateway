﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using Waher.Content;
using Waher.Events;
using Waher.Networking.HTTP;
using Waher.Runtime.Cache;

namespace Waher.Networking.XMPP.HTTPX
{
	/// <summary>
	/// HTTPX client.
	/// </summary>
	public class HttpxClient : IDisposable
	{
		public const string Namespace = "urn:xmpp:http";
		private const int MaxChunkSize = 4096;

		private Cache<string, CacheRecord> chunkedResponses;
		private XmppClient client;

		/// <summary>
		/// HTTPX client.
		/// </summary>
		/// <param name="Client">XMPP Client.</param>
		public HttpxClient(XmppClient Client)
		{
			this.client = Client;

			this.client.RegisterMessageHandler("chunk", Namespace, this.ChunkReceived, true);

			this.chunkedResponses = new Cache<string, CacheRecord>(int.MaxValue, TimeSpan.MaxValue, new TimeSpan(0, 1, 0));
			this.chunkedResponses.Removed += CacheItem_Removed;
		}

		public void Dispose()
		{
			this.client.UnregisterMessageHandler("chunk", Namespace, this.ChunkReceived, true);

			this.chunkedResponses.Clear();
			this.chunkedResponses.Dispose();
		}

		/// <summary>
		/// Performs a HTTP GET request.
		/// </summary>
		/// <param name="To">Full JID of entity to query.</param>
		/// <param name="Resource">Local HTTP resource to query.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		/// <param name="Headers">HTTP headers of the request.</param>
		public void GET(string To, string Resource, HttpxResponseEventHandler Callback,
			HttpxResponseDataEventHandler DataCallback, object State, params KeyValuePair<string, string>[] Headers)
		{
			this.Request(To, "GET", Resource, Callback, DataCallback, State, Headers);
		}

		/// <summary>
		/// Performs a HTTP GET request.
		/// </summary>
		/// <param name="To">Full JID of entity to query.</param>
		/// <param name="Request">HTTP Request.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		public void GET(string To, HttpRequest Request, HttpxResponseEventHandler Callback, 
			HttpxResponseDataEventHandler DataCallback, object State)
		{
			this.Request(To, "GET", Request, Callback, DataCallback, State);
		}

		// TODO: Add more HTTP methods.

		/// <summary>
		/// Performs a HTTP request.
		/// </summary>
		/// <param name="To">Full JID of entity to query.</param>
		/// <param name="Method">HTTP Method.</param>
		/// <param name="Resource">Local HTTP resource to query.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		/// <param name="Headers">HTTP headers of the request.</param>
		public void Request(string To, string Method, string Resource, HttpxResponseEventHandler Callback,
			HttpxResponseDataEventHandler DataCallback, object State, params KeyValuePair<string, string>[] Headers)
		{
			HttpRequestHeader Header = new HttpRequestHeader(Method, Resource, "1.1", Headers);
			HttpRequest Request = new HttpRequest(Header, null, null);
			this.Request(To, Method, Request, Callback, DataCallback, State);
		}

		/// <summary>
		/// Performs a HTTP request.
		/// </summary>
		/// <param name="To">Full JID of entity to query.</param>
		/// <param name="Method">HTTP Method.</param>
		/// <param name="Request">HTTP Request.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to the callback method.</param>
		public void Request(string To, string Method, HttpRequest Request, HttpxResponseEventHandler Callback,
			HttpxResponseDataEventHandler DataCallback, object State)
		{
			// TODO: Local IP & port for quick P2P response (TLS, or POST back, web hook).

			StringBuilder Xml = new StringBuilder();

			Xml.Append("<req xmlns='");
			Xml.Append(Namespace);
			Xml.Append("' method='");
			Xml.Append(Method);
			Xml.Append("' resource='");
			Xml.Append(XML.Encode(Request.Header.Resource));
			Xml.Append("' version='");
			Xml.Append(Request.Header.HttpVersion.ToString("F1").Replace(System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator, "."));
			Xml.Append("' maxChunkSize='");
			Xml.Append(MaxChunkSize.ToString());
			Xml.Append("' sipub='false' ibb='false' jingle='false'>");

			Xml.Append("<headers xmlns='http://jabber.org/protocol/shim'>");
			foreach (HttpField HeaderField in Request.Header)
			{
				Xml.Append("<header name='");
				Xml.Append(XML.Encode(HeaderField.Key));
				Xml.Append("'>");
				Xml.Append(XML.Encode(HeaderField.Value));
				Xml.Append("</header>");
			}
			Xml.Append("</headers>");

			string StreamId = null;

			if (Request.HasData)
			{
				if (Request.DataStream.Length < MaxChunkSize)
				{
					int c = (int)Request.DataStream.Length;
					byte[] Data = new byte[c];

					Request.DataStream.Position = 0;
					Request.DataStream.Read(Data, 0, c);

					Xml.Append("<data><base64>");
					Xml.Append(Convert.ToBase64String(Data, Base64FormattingOptions.None));
					Xml.Append("</base64></data>");
				}
				else
				{
					StreamId = Guid.NewGuid().ToString().Replace("-", string.Empty);

					Xml.Append("<data><chunkedBase64 streamId='");
					Xml.Append(StreamId);
					Xml.Append("'/></data>");
				}
			}

			Xml.Append("</req>");

			this.client.SendIqSet(To, Xml.ToString(), this.ResponseHandler, new object[] { Callback, State });

			if (!string.IsNullOrEmpty(StreamId))
			{
				byte[] Data = new byte[MaxChunkSize];
				long Pos = 0;
				long Len = Request.DataStream.Length;
				int Nr = 0;
				int i;

				Request.DataStream.Position = 0;

				while (Pos < Len)
				{
					if (Pos + MaxChunkSize <= Len)
						i = MaxChunkSize;
					else
						i = (int)(Len - Pos);

					Request.DataStream.Read(Data, 0, i);

					Xml.Clear();
					Xml.Append("<chunk xmlns='");
					Xml.Append(Namespace);
					Xml.Append("' streamId='");
					Xml.Append(StreamId);
					Xml.Append("' nr='");
					Xml.Append(Nr.ToString());
					Xml.Append("'>");
					Xml.Append(Convert.ToBase64String(Data, 0, i, Base64FormattingOptions.None));
					Xml.Append("</chunk>");
					Nr++;

					this.client.SendMessage(MessageType.Normal, To, Xml.ToString(), string.Empty, string.Empty, string.Empty,
						string.Empty, string.Empty);
				}
			}
		}

		private void ResponseHandler(object Sender, IqResultEventArgs e)
		{
			XmlElement E = e.FirstElement;
			HttpResponse Response;
			string StatusMessage;
			double Version;
			int StatusCode;
			object[] P = (object[])e.State;
			HttpxResponseEventHandler Callback = (HttpxResponseEventHandler)P[0];
			object State = P[1];
			bool HasData = false;

			if (e.Ok && E != null && E.LocalName == "resp" && E.NamespaceURI == Namespace)
			{
				Version = XML.Attribute(E, "version", 0.0);
				StatusCode = XML.Attribute(E, "statusCode", 0);
				StatusMessage = XML.Attribute(E, "statusMessage");
				Response = new HttpResponse();

				foreach (XmlNode N in E.ChildNodes)
				{
					switch (N.LocalName)
					{
						case "headers":
							foreach (XmlNode N2 in N.ChildNodes)
							{
								switch (N2.LocalName)
								{
									case "header":
										string Key = XML.Attribute((XmlElement)N2, "name");
										string Value = N2.InnerText;

										Response.SetHeader(Key, Value);
										break;
								}
							}
							break;

						case "data":
							foreach (XmlNode N2 in N.ChildNodes)
							{
								switch (N2.LocalName)
								{
									case "text":
										MemoryStream ms = new MemoryStream();
										Response.SetResponseStream(ms);
										byte[] Data = Response.Encoding.GetBytes(N2.InnerText);
										ms.Write(Data, 0, Data.Length);
										ms.Position = 0;
										HasData = true;
										break;

									case "xml":
										ms = new MemoryStream();
										Response.SetResponseStream(ms);
										Data = Response.Encoding.GetBytes(N2.InnerText);
										ms.Write(Data, 0, Data.Length);
										ms.Position = 0;
										HasData = true;
										break;

									case "base64":
										ms = new MemoryStream();
										Response.SetResponseStream(ms);
										Data = Convert.FromBase64String(N2.InnerText);
										ms.Write(Data, 0, Data.Length);
										ms.Position = 0;
										HasData = true;
										break;

									case "chunkedBase64":
										TemporaryFile file = new TemporaryFile();
										Response.SetResponseStream(file);
										string StreamId = XML.Attribute((XmlElement)N2, "streamId");
										this.chunkedResponses.Add(e.From + " " + StreamId, new CacheRecord(this, 
											new HttpxResponseEventArgs(e, Response, State, Version, StatusCode, StatusMessage, true),
											Response, file, Callback, State));
										return;

									case "sipub":
										// TODO: Implement File Transfer support.
										break;

									case "ibb":
										// TODO: Implement In-band byte streams support.
										break;

									case "jingle":
										// TODO: Implement Jingle support.
										break;
								}
							}
							break;
					}
				}
			}
			else
			{
				Version = 0.0;
				StatusCode = 505;
				StatusMessage = "HTTP Version Not Supported";
				Response = new HttpResponse();
			}

			HttpxResponseEventArgs e2 = new HttpxResponseEventArgs(e, Response, State, Version, StatusCode, StatusMessage, HasData);

			try
			{
				Callback(this, e2);
			}
			catch (Exception ex)
			{
				Log.Critical(ex);
			}
			finally
			{
				Response.Dispose();
			}
		}

		private class CacheRecord
		{
			internal HttpxClient client;
			internal HttpxResponseEventArgs e;
			internal SortedDictionary<int, Chunk> chunks = null;
			internal TemporaryFile file;
			internal HttpResponse response;
			internal HttpxResponseEventHandler callback;
			internal object state;
			internal int nextChunk = 0;

			internal CacheRecord(HttpxClient Client, HttpxResponseEventArgs e, HttpResponse Response, TemporaryFile File, 
				HttpxResponseEventHandler Callback, object State)
			{
				this.client = Client;
				this.e = e;
				this.response = Response;
				this.file = File;
				this.callback = Callback;
				this.state = State;
			}

			internal void ChunkReceived(int Nr, bool Last, byte[] Data)
			{
				if (Nr == this.nextChunk)
				{
					this.file.Write(Data, 0, Data.Length);
					this.nextChunk++;

					if (Last)
						this.Done();
					else
					{
						while (this.chunks != null)
						{
							if (this.chunks.Count == 0)
								this.chunks = null;
							else
							{
								foreach (Chunk Chunk in this.chunks.Values)
								{
									if (Chunk.Nr == this.nextChunk)
									{
										this.file.Write(Chunk.Data, 0, Chunk.Data.Length);
										this.nextChunk++;
										this.chunks.Remove(Chunk.Nr);

										if (Chunk.Last)
										{
											this.Done();
											this.chunks.Clear();
										}

										break;
									}
									else
										return;
								}
							}
						}
					}
				}
				else if (Nr > this.nextChunk)
				{
					if (this.chunks == null)
						this.chunks = new SortedDictionary<int, Chunk>();

					this.chunks[Nr] = new Chunk(Nr, Last, Data);
				}
			}

			private void Done()
			{
				try
				{
					if (this.callback != null)
						this.callback(this.client, this.e);
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}
				finally
				{
					this.response.Dispose();
				}
			}

			public void Dispose()
			{
				if (this.response != null)
				{
					this.response.Dispose();
					this.response = null;
				}

				if (this.chunks != null)
				{
					this.chunks.Clear();
					this.chunks = null;
				}
			}
		}

		private class Chunk
		{
			internal byte[] Data;
			internal int Nr;
			internal bool Last;

			internal Chunk(int Nr, bool Last, byte[] Data)
			{
				this.Data = Data;
				this.Nr = Nr;
				this.Last = Last;
			}
		}

		private static void CacheItem_Removed(object Sender, CacheItemEventArgs<string, CacheRecord> e)
		{
			e.Value.Dispose();
		}

		private void ChunkReceived(object Sender, MessageEventArgs e)
		{
			CacheRecord Rec;
			string StreamId = XML.Attribute(e.Content, "streamId");
			if (!this.chunkedResponses.TryGetValue(e.From + " " + StreamId, out Rec))
				return;

			int Nr = XML.Attribute(e.Content, "nr", 0);
			if (Nr < 0)
				return;

			bool Last = XML.Attribute(e.Content, "last", false);
			byte[] Data = Convert.FromBase64String(e.Content.InnerText);

			Rec.ChunkReceived(Nr, Last, Data);
		}
	}
}
