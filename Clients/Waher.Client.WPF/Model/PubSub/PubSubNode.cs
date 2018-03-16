﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Input;
using System.Xml;
using Waher.Content;
using Waher.Events;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.PubSub;
using Waher.Networking.XMPP.ServiceDiscovery;
using Waher.Things;
using Waher.Things.DisplayableParameters;
using Waher.Things.SensorData;
using Waher.Client.WPF.Dialogs;

namespace Waher.Client.WPF.Model.PubSub
{
	/// <summary>
	/// Represents a node in a Publish/Subscribe service.
	/// </summary>
	public class PubSubNode : TreeNode
	{
		private DisplayableParameters parameters;
		private NodeType nodeType;
		private string jid;
		private string node;
		private string name;

		public PubSubNode(TreeNode Parent, string Jid, string Node, string Name, NodeType NodeType)
			: base(Parent)
		{
			this.jid = Jid;
			this.node = Node;
			this.name = Name;
			this.nodeType = NodeType;

			List<Parameter> Parameters = new List<Parameter>();

			if (!string.IsNullOrEmpty(this.jid))
				Parameters.Add(new StringParameter("JID", "JID", this.jid));

			if (!string.IsNullOrEmpty(this.name))
				Parameters.Add(new StringParameter("Name", "Name", this.name));

			if (this.nodeType == NodeType.collection)
				Parameters.Add(new StringParameter("Type", "Type", "Collection"));
			else
				Parameters.Add(new StringParameter("Type", "Type", "Leaf"));

			this.children = new SortedDictionary<string, TreeNode>()
			{
				{ string.Empty, new Loading(this) }
			};

			this.parameters = new DisplayableParameters(Parameters.ToArray());
		}

		public override string Key => this.node;
		public override string Header => this.node;
		public override string ToolTip => this.name;
		public override bool CanRecycle => false;
		public override DisplayableParameters DisplayableParameters => this.parameters;

		public override string TypeName
		{
			get
			{
				return "Publish/Subscribe Node";
			}
		}

		public override ImageSource ImageResource
		{
			get
			{
				if (this.IsExpanded)
					return XmppAccountNode.folderOpen;
				else
					return XmppAccountNode.folderClosed;
			}
		}

		public override void Write(XmlWriter Output)
		{
			// Don't output.
		}

		public PubSubService Service
		{
			get
			{
				TreeNode Loop = this.Parent;

				while (Loop != null)
				{
					if (Loop is PubSubService PubSubService)
						return PubSubService;

					Loop = Loop.Parent;
				}

				return null;
			}
		}

		private bool loadingChildren = false;

		public PubSubClient PubSubClient
		{
			get
			{
				return this.Service?.PubSubClient;
			}
		}

		public override bool CanAddChildren => false;   // TODO
		public override bool CanDelete => false;    // TODO
		public override bool CanEdit => false;  // TODO

		protected override void LoadChildren()
		{
			if (!this.loadingChildren && this.children != null && this.children.Count == 1 && this.children.ContainsKey(string.Empty))
			{
				Mouse.OverrideCursor = Cursors.Wait;

				this.loadingChildren = true;
				this.Service.Account.Client.SendServiceItemsDiscoveryRequest(this.PubSubClient.ComponentAddress, this.node, (sender, e) =>
				{
					this.loadingChildren = false;
					MainWindow.MouseDefault();

					if (e.Ok)
					{
						SortedDictionary<string, TreeNode> Children = new SortedDictionary<string, TreeNode>();

						this.Service.NodesRemoved(this.children.Values, this);

						if (this.nodeType == NodeType.leaf)
						{
							foreach (Item Item in e.Items)
								Children[Item.Name] = new PubSubItem(this, this.jid, this.node, Item.Name);

							this.children = new SortedDictionary<string, TreeNode>(Children);

							this.OnUpdated();
							this.Service.NodesAdded(this.children.Values, this);
						}
						else
						{
							foreach (Item Item in e.Items)
							{
								this.Service.Account.Client.SendServiceDiscoveryRequest(this.PubSubClient.ComponentAddress, Item.Node, (sender2, e2) =>
								{
									if (e2.Ok)
									{
										Item Item2 = (Item)e2.State;
										string Jid = Item2.JID;
										string Node = Item2.Node;
										string Name = Item2.Name;
										NodeType NodeType = NodeType.leaf;
										TreeNode NewNode;

										foreach (Identity Identity in e2.Identities)
										{
											if (Identity.Category == "pubsub")
											{
												if (!Enum.TryParse<NodeType>(Identity.Type, out NodeType))
													NodeType = NodeType.leaf;

												if (!string.IsNullOrEmpty(Identity.Name))
													Name = Identity.Name;
											}
										}

										lock (Children)
										{
											NewNode = new PubSubNode(this, Jid, Node, Name, NodeType);
											Children[Item2.Node] = NewNode;
											this.children = new SortedDictionary<string, TreeNode>(Children);
										}

										this.OnUpdated();
										this.Service.NodesAdded(new TreeNode[] { NewNode }, this);
									}
								}, Item);
							}
						}
					}
				}, null);
			}

			base.LoadChildren();
		}

		protected override void UnloadChildren()
		{
			base.UnloadChildren();

			if (this.children == null || this.children.Count != 1 || !this.children.ContainsKey(string.Empty))
			{
				if (this.children != null)
					this.Service?.NodesRemoved(this.children.Values, this);

				this.children = new SortedDictionary<string, TreeNode>()
				{
					{ string.Empty, new Loading(this) }
				};

				this.OnUpdated();
			}
		}

	}
}
