﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Waher.Events;
using Waher.Networking;
using Waher.Networking.XMPP;
using Waher.Client.WPF.Dialogs;

namespace Waher.Client.WPF.Model
{
	/// <summary>
	/// Class representing a normal XMPP account.
	/// </summary>
	public class XmppAccountNode : TreeNode
	{
		private Connections connections;
		private XmppClient client;
		private Timer connectionTimer;
		private Exception lastError = null;
		private string host;
		private int port;
		private string account;
		private string password;
		private string passwordHash;
		private string passwordHashMethod;
		private bool trustCertificate;
		private bool connected = false;

		/// <summary>
		/// Class representing a normal XMPP account.
		/// </summary>
		/// <param name="Connections">Connections object.</param>
		/// <param name="Parent">Parent node.</param>
		/// <param name="Host">Host name.</param>
		/// <param name="Port">Port number.</param>
		/// <param name="Account">Account name.</param>
		/// <param name="PasswordHash">Password hash.</param>
		/// <param name="PasswordHashMethod">Password hash method.</param>
		/// <param name="TrustCertificate">If the server certificate should be trusted.</param>
		public XmppAccountNode(Connections Connections, TreeNode Parent, string Host, int Port, string Account,
			string PasswordHash, string PasswordHashMethod, bool TrustCertificate)
			: base(Parent)
		{
			this.connections = Connections;
			this.host = Host;
			this.port = Port;
			this.account = Account;
			this.password = string.Empty;
			this.passwordHash = PasswordHash;
			this.passwordHashMethod = PasswordHashMethod;
			this.trustCertificate = TrustCertificate;

			this.Init();
		}

		public XmppAccountNode(XmlElement E, Connections Connections, TreeNode Parent)
			: base(Parent)
		{
			this.connections = Connections;
			this.host = XML.Attribute(E, "host");
			this.port = XML.Attribute(E, "port", 5222);
			this.account = XML.Attribute(E, "account");
			this.password = XML.Attribute(E, "password");
			this.passwordHash = XML.Attribute(E, "passwordHash");
			this.passwordHashMethod = XML.Attribute(E, "passwordHashMethod");
			this.trustCertificate = XML.Attribute(E, "trustCertificate", false);

			this.Init();
		}

		private void Init()
		{
			if (!string.IsNullOrEmpty(this.passwordHash))
				this.client = new XmppClient(this.host, this.port, this.account, this.passwordHash, this.passwordHashMethod, "en");
			else
				this.client = new XmppClient(this.host, this.port, this.account, this.password, "en");

			this.client.TrustServer = this.trustCertificate;
			this.client.OnStateChanged += new StateChangedEventHandler(client_OnStateChanged);
			this.client.OnError += new XmppExceptionEventHandler(client_OnError);
			this.client.OnPresence += new PresenceEventHandler(client_OnPresence);
			this.client.OnPresenceSubscribe += new PresenceEventHandler(client_OnPresenceSubscribe);
			this.client.OnPresenceUnsubscribe += new PresenceEventHandler(client_OnPresenceUnsubscribe);
			this.client.OnRosterItemAdded += new RosterItemEventHandler(client_OnRosterItemUpdated);
			this.client.OnRosterItemRemoved += new RosterItemEventHandler(client_OnRosterItemRemoved);
			this.client.OnRosterItemUpdated += new RosterItemEventHandler(client_OnRosterItemUpdated);
			this.connectionTimer = new Timer(this.CheckConnection, null, 60000, 60000);

			this.client.SetPresence(Availability.Chat);
		}

		private void client_OnError(XmppClient Sender, Exception Exception)
		{
			this.lastError = Exception;
		}

		private void client_OnStateChanged(XmppClient Sender, XmppState NewState)
		{
			switch (NewState)
			{
				case XmppState.Connected:
					this.connected = true;
					this.lastError = null;

					if (string.IsNullOrEmpty(this.passwordHash))
					{
						this.passwordHash = this.client.PasswordHash;
						this.passwordHashMethod = this.client.PasswordHashMethod;
					}

					this.CheckRoster();
					break;

				case XmppState.Offline:
					bool ImmediateReconnect = this.connected;
					this.connected = false;

					if (ImmediateReconnect)
						this.client.Reconnect();
					break;
			}

			this.OnUpdated();
		}

		public string Host { get { return this.host; } }
		public int Port { get { return this.port; } }
		public string Account { get { return this.account; } }
		public string PasswordHash { get { return this.passwordHash; } }
		public string PasswordHashMethod { get { return this.passwordHashMethod; } }
		public bool TrustCertificate { get { return this.trustCertificate; } }

		public override string Header
		{
			get { return this.account + "@" + this.host; }
		}

		public override void Dispose()
		{
			base.Dispose();

			if (this.connectionTimer != null)
			{
				this.connectionTimer.Dispose();
				this.connectionTimer = null;
			}

			if (this.client != null)
			{
				this.client.Dispose();
				this.client = null;
			}
		}

		private void CheckConnection(object P)
		{
			if (this.client.State == XmppState.Offline || this.client.State == XmppState.Error || this.client.State == XmppState.Authenticating)
			{
				try
				{
					this.client.Reconnect();
				}
				catch (Exception ex)
				{
					Log.Critical(ex);
				}
			}
		}

		public override void Write(XmlWriter Output)
		{
			Output.WriteStartElement("XmppAccount");
			Output.WriteAttributeString("host", this.host);
			Output.WriteAttributeString("port", this.port.ToString());
			Output.WriteAttributeString("account", this.account);

			if (string.IsNullOrEmpty(this.passwordHash))
				Output.WriteAttributeString("password", this.password);
			else
			{
				Output.WriteAttributeString("passwordHash", this.passwordHash);
				Output.WriteAttributeString("passwordHashMethod", this.passwordHashMethod);
			}

			Output.WriteAttributeString("trustCertificate", CommonTypes.Encode(this.trustCertificate));
			Output.WriteEndElement();
		}

		internal static readonly BitmapImage away = new BitmapImage(new Uri("Graphics/Away.png", UriKind.Relative));
		internal static readonly BitmapImage busy = new BitmapImage(new Uri("Graphics/Busy.png", UriKind.Relative));
		internal static readonly BitmapImage chat = new BitmapImage(new Uri("Graphics/Chat.png", UriKind.Relative));
		internal static readonly BitmapImage offline = new BitmapImage(new Uri("Graphics/Offline.png", UriKind.Relative));
		internal static readonly BitmapImage online = new BitmapImage(new Uri("Graphics/Online.png", UriKind.Relative));

		public override ImageSource ImageResource
		{
			get
			{
				if (this.client == null)
					return offline;
				else
				{
					switch (this.client.State)
					{
						case XmppState.Connected:
							return online;

						case XmppState.Error:
							return busy;

						case XmppState.Offline:
						default:
							return offline;
					}
				}
			}
		}

		public override string ToolTip
		{
			get
			{
				switch (this.client.State)
				{
					case XmppState.Offline:
					default:
						return "Offline";

					case XmppState.Connecting:
						return "Connecting to broker.";

					case XmppState.StreamNegotiation:
						return "Performing Stream Negotiation.";

					case XmppState.StartingEncryption:
						return "Switching to encrypted channel.";

					case XmppState.Authenticating:
						return "Performing user authentication.";

					case XmppState.Binding:
						return "Performing session binding.";

					case XmppState.FetchingRoster:
						return "Fetching roster.";

					case XmppState.SettingPresence:
						return "Setting presence.";

					case XmppState.Connected:
						return "Connected.";

					case XmppState.Error:
						if (this.lastError == null)
							return "In an error state.";
						else
							return this.lastError.Message;
				}
			}
		}

		public override bool CanAddChildren
		{
			get
			{
				return this.client != null && this.client.State == XmppState.Connected;
			}
		}

		public override void Add()
		{
			AddContactForm Dialog = new AddContactForm();
			Dialog.Owner = this.connections.Owner;
			bool? Result = Dialog.ShowDialog();

			if (Result.HasValue && Result.Value)
				this.client.RequestPresenceSubscription(Dialog.ContactJID.Text);
		}

		private void CheckRoster()
		{
			SortedDictionary<string, TreeNode> Contacts = this.children;
			LinkedList<TreeNode> Added = null;

			if (Contacts == null)
				Contacts = new SortedDictionary<string, TreeNode>();

			lock (Contacts)
			{
				XmppContact Contact;

				foreach (RosterItem Item in this.client.Roster)
				{
					if (!Contacts.ContainsKey(Item.BareJid))
					{
						Contact = new XmppContact(this, Item);
						Contacts[Item.BareJid] = Contact;

						if (Added == null)
							Added = new LinkedList<TreeNode>();

						Added.AddLast(Contact);
					}
				}

				if (this.children == null)
					this.children = Contacts;
			}

			if (Added != null)
			{
				foreach (TreeNode Node in Added)
					this.connections.Owner.NodeAdded(this, Node);
			}

			this.OnUpdated();
		}

		private void client_OnRosterItemUpdated(XmppClient Sender, RosterItem Item)
		{
			if (this.children == null)
				this.CheckRoster();
			else
			{
				TreeNode Node;
				XmppContact Contact;
				bool Added = false;

				lock (this.children)
				{
					if (this.children.TryGetValue(Item.BareJid, out Node))
					{
						if ((Contact = Node as XmppContact) != null)
							Contact.RosterItem = Item;
					}
					else
					{
						Contact = new XmppContact(this, Item);
						this.children[Item.BareJid] = Contact;
						Added = true;
					}
				}

				if (Added)
				{
					this.connections.Owner.NodeAdded(this, Contact);
					this.OnUpdated();
				}
				else
					Contact.OnUpdated();
			}
		}

		private void client_OnRosterItemRemoved(XmppClient Sender, RosterItem Item)
		{
			if (this.children == null)
				this.CheckRoster();
			else
			{
				bool Updated;

				lock (this.children)
				{
					Updated = this.children.Remove(Item.BareJid);
				}

				this.OnUpdated();
			}
		}

		private void client_OnPresence(XmppClient Sender, PresenceEventArgs e)
		{
			if (this.children == null)
				this.CheckRoster();
			else
			{
				TreeNode Node;

				lock (this.children)
				{
					if (!this.children.TryGetValue(e.FromBareJID, out Node))
						Node = null;
				}

				Node.OnUpdated();
			}
		}

		private void client_OnPresenceSubscribe(XmppClient Sender, PresenceEventArgs e)
		{
			this.connections.Owner.Dispatcher.BeginInvoke(new ParameterizedThreadStart(this.PresenceSubscribe), e);
		}

		private void PresenceSubscribe(object P)
		{
			PresenceEventArgs e = (PresenceEventArgs)P;

			switch (MessageBox.Show(this.connections.Owner, e.FromBareJID + " has requested to subscribe to your presence (become your friend). Do you accept?",
				this.client.BareJID, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes))
			{
				case MessageBoxResult.Yes:
					e.Accept();

					RosterItem Item = this.client.GetRosterItem(e.FromBareJID);
					if (Item == null || Item.State == SubscriptionState.None || Item.State == SubscriptionState.From)
						this.client.RequestPresenceSubscription(e.FromBareJID);

					this.client.SetPresence(Availability.Chat);
					break;

				case MessageBoxResult.No:
					e.Decline();
					break;

				case MessageBoxResult.Cancel:
				default:
					// Do nothing.
					break;
			}
		}

		private void client_OnPresenceUnsubscribe(XmppClient Sender, PresenceEventArgs e)
		{
			e.Accept();
		}

		public override bool CanRecycle
		{
			get { return true; }
		}

		public override void Recycle()
		{
			if (this.client != null)
			{
				this.client.Dispose();
				this.client = null;
			}

			this.Init();
		}

		public bool IsOnline
		{
			get
			{
				return this.client != null && this.client.State == XmppState.Connected;
			}
		}

		public string BareJID
		{
			get
			{
				if (this.client == null)
					return string.Empty;
				else
					return this.client.BareJID;
			}
		}

		public override string Key
		{
			get
			{
				return this.BareJID;
			}
		}

		public override bool Delete(TreeNode Node)
		{
			if (base.Delete(Node))
			{
				XmppContact Contact = Node as XmppContact;

				if (Contact != null)
				{
					try
					{
						this.client.RemoveRosterItem(BareJID);
					}
					catch (ArgumentException)
					{
						// Already removed.
					}
				}

				return true;
			}
			else
				return false;
		}

	}
}
