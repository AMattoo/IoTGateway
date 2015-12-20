﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Waher.Networking.XMPP.ServiceDiscovery
{
	/// <summary>
	/// Delegate for service discovery events or callback methods.
	/// </summary>
	/// <param name="Client"></param>
	/// <param name="e"></param>
	public delegate void ServiceDiscoveryEventHandler(XmppClient Client, ServiceDiscoveryEventArgs e);

	/// <summary>
	/// Event arguments for service discovery responses.
	/// </summary>
	public class ServiceDiscoveryEventArgs : IqResultEventArgs
	{
		private Dictionary<string, bool> features;
		private Identity[] identities;
		private object state;

		internal ServiceDiscoveryEventArgs(IqResultEventArgs e, Dictionary<string, bool> Features, Identity[] Identities, object State)
			: base(e)
		{
			this.features = Features;
			this.identities = Identities;
			this.state = State;
		}

		/// <summary>
		/// Features
		/// </summary>
		public Dictionary<string, bool> Features { get { return this.features; } }

		/// <summary>
		/// Identities
		/// </summary>
		public Identity[] Identities { get { return this.identities; } }

		/// <summary>
		/// State object passed to the original request.
		/// </summary>
		public object State { get { return this.state; } }
	}
}
