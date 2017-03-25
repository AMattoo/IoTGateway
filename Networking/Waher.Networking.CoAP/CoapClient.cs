﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Waher.Events;
using Waher.Networking.Sniffers;
using Waher.Runtime.Timing;

namespace Waher.Networking.CoAP
{
	/// <summary>
	/// CoAP client. CoAP is defined in RFC7252:
	/// https://tools.ietf.org/html/rfc7252
	/// </summary>
	public class CoapClient : Sniffable, IDisposable
	{
		/// <summary>
		/// Default CoAP port = 5683
		/// </summary>
		public const int DefaultCoapPort = 5683;

		/// <summary>
		/// DEfault CoAP over DTLS port = 5684
		/// </summary>
		public const int DefaultCoapsPort = 5684;

		internal const int ACK_TIMEOUT = 2;   // seconds
		internal const double ACK_RANDOM_FACTOR = 1.5;
		internal const int MAX_RETRANSMIT = 4;
		internal const int NSTART = 1;
		internal const int DEFAULT_LEISURE = 5;   // seconds
		internal const int PROBING_RATE = 1;  // byte/second

		private static readonly CoapOptionComparer optionComparer = new CoapOptionComparer();

		private LinkedList<Message> outputQueue = new LinkedList<Message>();
		private Dictionary<ushort, Message> outgoingMessages = new Dictionary<ushort, Message>();
		private Random gen = new Random();
		private Scheduler scheduler;
		private LinkedList<KeyValuePair<UdpClient, IPEndPoint>> coapMulticast = new LinkedList<KeyValuePair<UdpClient, IPEndPoint>>();
		private LinkedList<UdpClient> coapSinglecast = new LinkedList<UdpClient>();
		private ushort msgId = 0;
		private ulong token = 0;
		private bool isWriting = false;
		private bool disposed = false;

		/// <summary>
		/// CoAP client. CoAP is defined in RFC7252:
		/// https://tools.ietf.org/html/rfc7252
		/// </summary>
		/// <param name="Sniffers">Optional set of sniffers to use.</param>
		public CoapClient(params ISniffer[] Sniffers)
			: base(Sniffers)
		{
			UdpClient Outgoing;
			UdpClient Incoming;

			foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (Interface.OperationalStatus != OperationalStatus.Up)
					continue;

				IPInterfaceProperties Properties = Interface.GetIPProperties();
				IPAddress MulticastAddress;

				foreach (UnicastIPAddressInformation UnicastAddress in Properties.UnicastAddresses)
				{
					if (UnicastAddress.Address.AddressFamily == AddressFamily.InterNetwork && Socket.OSSupportsIPv4)
					{
						try
						{
							Outgoing = new UdpClient(AddressFamily.InterNetwork);
							MulticastAddress = IPAddress.Parse("224.0.1.187");
							Outgoing.DontFragment = true;
							Outgoing.MulticastLoopback = false;
						}
						catch (Exception)
						{
							continue;
						}
					}
					else if (UnicastAddress.Address.AddressFamily == AddressFamily.InterNetworkV6 && Socket.OSSupportsIPv6)
					{
						try
						{
							Outgoing = new UdpClient(AddressFamily.InterNetworkV6);
							Outgoing.MulticastLoopback = false;
							MulticastAddress = IPAddress.Parse("[FF02::FD]");
						}
						catch (Exception)
						{
							continue;
						}
					}
					else
						continue;

					Outgoing.EnableBroadcast = true;
					Outgoing.MulticastLoopback = false;
					Outgoing.Ttl = 30;
					Outgoing.Client.Bind(new IPEndPoint(UnicastAddress.Address, 0));
					Outgoing.JoinMulticastGroup(MulticastAddress);

					IPEndPoint EP = new IPEndPoint(MulticastAddress, DefaultCoapPort);
					this.coapMulticast.AddLast(new KeyValuePair<UdpClient, IPEndPoint>(Outgoing, EP));

					Outgoing.BeginReceive(this.EndReceiveOutgoing, Outgoing);

					try
					{
						Incoming = new UdpClient(Outgoing.Client.AddressFamily);
						Incoming.ExclusiveAddressUse = false;
						Incoming.Client.Bind(new IPEndPoint(UnicastAddress.Address, DefaultCoapPort));

						Incoming.BeginReceive(this.EndReceiveIncoming, Incoming);

						this.coapSinglecast.AddLast(Incoming);
					}
					catch (Exception)
					{
						Incoming = null;
					}

					try
					{
						Incoming = new UdpClient(DefaultCoapPort, Outgoing.Client.AddressFamily);
						Incoming.MulticastLoopback = false;
						Incoming.JoinMulticastGroup(MulticastAddress);

						Incoming.BeginReceive(this.EndReceiveIncoming, Incoming);

						this.coapSinglecast.AddLast(Incoming);
					}
					catch (Exception)
					{
						Incoming = null;
					}
				}
			}

			this.scheduler = new Scheduler(ThreadPriority.BelowNormal, "CoAP tasks");
		}

		/// <summary>
		/// <see cref="IDisposable.Dispose"/>
		/// </summary>
		public void Dispose()
		{
			this.disposed = true;

			if (this.scheduler != null)
			{
				this.scheduler.Dispose();
				this.scheduler = null;
			}

			foreach (KeyValuePair<UdpClient, IPEndPoint> P in this.coapMulticast)
			{
				try
				{
					P.Key.Close();
				}
				catch (Exception)
				{
					// Ignore
				}
			}

			this.coapMulticast.Clear();

			foreach (UdpClient Client in this.coapSinglecast)
			{
				try
				{
					Client.Close();
				}
				catch (Exception)
				{
					// Ignore
				}
			}

			this.coapSinglecast.Clear();

			foreach (ISniffer Sniffer in this.Sniffers)
			{
				IDisposable Disposable = Sniffer as IDisposable;
				if (Disposable != null)
				{
					try
					{
						Disposable.Dispose();
					}
					catch (Exception ex)
					{
						Log.Critical(ex);
					}
				}
			}
		}

		private void EndReceiveOutgoing(IAsyncResult ar)
		{
			if (this.disposed)
				return;

			try
			{
				UdpClient UdpClient = (UdpClient)ar.AsyncState;
				IPEndPoint RemoteIP = null;
				byte[] Packet = UdpClient.EndReceive(ar, ref RemoteIP);

				this.ReceiveBinary(Packet);

				UdpClient.BeginReceive(this.EndReceiveOutgoing, UdpClient);
			}
			catch (Exception ex)
			{
				this.Error(ex.Message);
			}
		}

		private void EndReceiveIncoming(IAsyncResult ar)
		{
			if (this.disposed)
				return;

			try
			{
				UdpClient UdpClient = (UdpClient)ar.AsyncState;
				IPEndPoint RemoteIP = null;
				byte[] Packet = UdpClient.EndReceive(ar, ref RemoteIP);

				this.ReceiveBinary(Packet);

				UdpClient.BeginReceive(this.EndReceiveIncoming, UdpClient);
			}
			catch (Exception ex)
			{
				this.Error(ex.Message);
			}
		}

		private byte[] Encode(CoapMessageType Type, CoapCode Code, ulong Token, ushort MessageID, byte[] Payload, params CoapOption[] Options)
		{
			MemoryStream ms = new MemoryStream(128);
			ulong Temp;
			byte b, b2;

			b = 1 << 6;	// Version
			b |= (byte)((byte)Type << 4);

			Temp = Token;
			b2 = 0;

			while (Temp > 0)
			{
				Temp >>= 8;
				b2++;
			}

			b |= b2; 
			ms.WriteByte(b);
			ms.WriteByte((byte)Code);
			ms.WriteByte((byte)(MessageID >> 8));
			ms.WriteByte((byte)MessageID);

			while (b2 > 0)
			{
				ms.WriteByte((byte)Token);
				Token >>= 8;
				b2--;
			}

			if (Options != null && Options.Length > 0)
			{
				byte[] Value;
				int LastNumber = 0;
				int Delta;
				int Length;

				Array.Sort(Options, optionComparer);

				foreach (CoapOption Option in Options)
				{
					Delta = Option.OptionNumber - LastNumber;
					LastNumber += Delta;

					Value = Option.GetValue();
					Length = Value == null ? 0 : Value.Length;

					if (Delta < 13)
						b = (byte)(Delta << 4);
					else if (Delta < 269)
						b = 13 << 4;
					else
						b = 14 << 4;

					if (Length < 13)
						b |= (byte)Length;
					else if (Length < 269)
						b |= 13;
					else
						b |= 14;

					ms.WriteByte(b);

					if (Delta >= 13)
					{
						if (Delta < 269)
							ms.WriteByte((byte)(Delta - 13));
						else
						{
							Delta -= 269;
							ms.WriteByte((byte)(Delta >> 8));
							ms.WriteByte((byte)Delta);
						}
					}

					if (Length >= 13)
					{
						if (Length < 269)
							ms.WriteByte((byte)(Length - 13));
						else
						{
							Length -= 269;
							ms.WriteByte((byte)(Length >> 8));
							ms.WriteByte((byte)Length);
						}
					}

					if (Value != null)
						ms.Write(Value, 0, Value.Length);
				}
			}

			if (Payload != null && Payload.Length > 0)
			{
				ms.WriteByte(0xff);
				ms.Write(Payload, 0, Payload.Length);
			}

			byte[] Result = ms.ToArray();

			ms.Dispose();

			return Result;
		}

		private void Transmit(IPEndPoint Destination, CoapMessageType MessageType, CoapCode Code, byte[] Payload,
			CoapResponseEventHandler Callback, object State, params CoapOption[] Options)
		{
			Message Message;
			ushort MessageID;

			lock (outgoingMessages)
			{
				do
				{
					MessageID = this.msgId++;
				}
				while (this.outgoingMessages.ContainsKey(MessageID));

				Message = new Message()
				{
					client = this,
					messageType = MessageType,
					acknowledged = MessageType == CoapMessageType.CON,
					destination = Destination,
					messageID = MessageID,
					token = this.token++,
					callback = Callback,
					state = State
				};

				if (Message.acknowledged || Message.callback != null)
					this.outgoingMessages[MessageID] = Message;
			}

			if (Message.acknowledged)
			{
				lock (this.gen)
				{
					Message.timeoutMilliseconds = (int)Math.Round(1000 * (ACK_TIMEOUT + (ACK_RANDOM_FACTOR - 1) * gen.NextDouble()));
				}
			}
			else if (Message.callback != null)
			{
				Message.timeoutMilliseconds = 1000 * ACK_TIMEOUT;
				Message.retryCount = MAX_RETRANSMIT;
			}

			Message.encoded = this.Encode(Message.messageType, Code, Message.token, MessageID, Payload, Options);

			this.SendMessage(Message);
		}

		private void SendMessage(Message Message)
		{
			lock (this.outputQueue)
			{
				if (this.isWriting)
				{
					this.outputQueue.AddLast(Message);
					return;
				}
				else
					this.isWriting = true;
			}

			this.TransmitBinary(Message.encoded);

			foreach (UdpClient Client in this.coapSinglecast)
			{
				Client.BeginSend(Message.encoded, Message.encoded.Length, Message.destination, this.MessageSent,
					new KeyValuePair<Message, UdpClient>(Message, Client));
			}

			if (Message.acknowledged || Message.callback != null)
				this.scheduler.Add(DateTime.Now.AddMilliseconds(Message.timeoutMilliseconds), this.CheckRetry, Message);
		}

		private void MessageSent(IAsyncResult ar)
		{
			if (this.disposed)
				return;

			try
			{
				KeyValuePair<Message, UdpClient> P = (KeyValuePair<Message, UdpClient>)ar.AsyncState;
				Message Message;

				P.Value.EndSend(ar);

				lock (this.outputQueue)
				{
					if (this.outputQueue.First == null)
					{
						this.isWriting = false;
						Message = null;
					}
					else
					{
						Message = this.outputQueue.First.Value;
						this.outputQueue.RemoveFirst();
					}
				}

				if (Message != null)
				{
					P.Value.BeginSend(Message.encoded, Message.encoded.Length, Message.destination, this.MessageSent, Message);

					if (Message.acknowledged || Message.callback != null)
						this.scheduler.Add(DateTime.Now.AddMilliseconds(Message.timeoutMilliseconds), this.CheckRetry, Message);
				}
			}
			catch (Exception ex)
			{
				this.Error(ex.Message);
			}
		}

		private class Message
		{
			public CoapResponseEventHandler callback;
			public CoapClient client;
			public object state;
			public IPEndPoint destination;
			public CoapMessageType messageType;
			public ushort messageID;
			public ulong token;
			public byte[] encoded;
			public int timeoutMilliseconds;
			public int retryCount = 0;
			public bool acknowledged;
			public bool responseReceived = false;

			internal void NoResponse()
			{
				if (this.callback != null)
				{
					try
					{
						this.callback(this.client, new CoapResponseEventArgs(false, this.state));
					}
					catch (Exception ex)
					{
						Log.Critical(ex);
					}
				}
			}
		}

		private void CheckRetry(object State)
		{
			Message Message = (Message)State;
			Message Message2;
			bool Fail = false;

			if (Message.responseReceived)
				return;

			lock (this.outgoingMessages)
			{
				if (!this.outgoingMessages.TryGetValue(Message.messageID, out Message2) || Message != Message2)
					return;

				Message.retryCount++;
				if (Message.retryCount >= MAX_RETRANSMIT)
				{
					this.outgoingMessages.Remove(Message.messageID);
					Fail = true;
				}
			}

			if (Fail)
			{
				Message.NoResponse();
				return;
			}

			Message.timeoutMilliseconds *= 2;
			this.SendMessage(Message);
		}

		private void Request(IPEndPoint Destination, bool Acknowledged, CoapCode Code, byte[] Payload,
			CoapResponseEventHandler Callback, object State, params CoapOption[] Options)
		{
			this.Transmit(Destination, Acknowledged ? CoapMessageType.CON : CoapMessageType.NON, Code, Payload, Callback, State, Options);
		}

		private void Request(IPEndPoint Destination, bool Acknowledged, CoapCode Code, byte[] Payload, params CoapOption[] Options)
		{
			this.Transmit(Destination, Acknowledged ? CoapMessageType.CON : CoapMessageType.NON, Code, Payload, null, null, Options);
		}

		private void Respond(IPEndPoint Destination, bool Acknowledged, CoapCode Code, byte[] Payload, params CoapOption[] Options)
		{
			this.Transmit(Destination, Acknowledged ? CoapMessageType.CON : CoapMessageType.NON, Code, Payload, null, null, Options);
		}

		private void ACK(IPEndPoint Destination)
		{
			this.Transmit(Destination, CoapMessageType.ACK, CoapCode.EmptyMessage, null, null, null);
		}

		private void Reset(IPEndPoint Destination)
		{
			this.Transmit(Destination, CoapMessageType.RST, CoapCode.EmptyMessage, null, null, null);
		}

		/// <summary>
		/// Performs a GET operation.
		/// </summary>
		/// <param name="Destination">Request resource from this locaton.</param>
		/// <param name="Acknowledged">If acknowledged message service is to be used.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		/// <param name="Options">CoAP options to include in the request.</param>
		public void GET(IPEndPoint Destination, bool Acknowledged, CoapResponseEventHandler Callback, object State, params CoapOption[] Options)
		{
			this.Request(Destination, Acknowledged, CoapCode.GET, null, Callback, State, Options);
		}

		/// <summary>
		/// Performs a GET operation.
		/// </summary>
		/// <param name="Destination">Request resource from this locaton.</param>
		/// <param name="Acknowledged">If acknowledged message service is to be used.</param>
		/// <param name="Callback">Callback method to call when response is returned.</param>
		/// <param name="State">State object to pass on to callback method.</param>
		/// <param name="Options">CoAP options to include in the request.</param>
		public async Task GET(string Destination, int Port, bool Acknowledged, CoapResponseEventHandler Callback, object State,
			params CoapOption[] Options)
		{
			IPAddress[] Addresses = await Dns.GetHostAddressesAsync(Destination);
			int c = Addresses.Length;

			if (c == 0)
			{
				if (Callback != null)
				{
					try
					{
						Callback(this, new CoapResponseEventArgs(false, State));
					}
					catch (Exception ex)
					{
						Log.Critical(ex);
					}
				}

				return;
			}

			IPAddress Addr;

			if (c == 1)
				Addr = Addresses[0];
			else
			{
				lock (this.gen)
				{
					Addr = Addresses[this.gen.Next(c)];
				}
			}

			this.GET(new IPEndPoint(Addr, Port), Acknowledged, Callback, State, Options);
		}

		private void PUT()
		{
		}

		private void POST()
		{
		}

		private void DELETE()
		{
		}

	}
}
