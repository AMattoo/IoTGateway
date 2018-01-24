﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Input;
using System.Xml;
using Waher.Content;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Concentrator;
using Waher.Networking.XMPP.Control;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.Sensor;
using Waher.Things;
using Waher.Things.DisplayableParameters;
using Waher.Things.SensorData;
using Waher.Client.WPF.Dialogs;

namespace Waher.Client.WPF.Model
{
	/// <summary>
	/// Represents a node in a concentrator.
	/// </summary>
	public class Node : TreeNode
	{
		private NodeInformation nodeInfo;
		private DisplayableParameters parameters;

		public Node(TreeNode Parent, NodeInformation NodeInfo)
			: base(Parent)
		{
			this.nodeInfo = NodeInfo;

			if (this.nodeInfo.ParameterList == null)
				this.parameters = null;
			else
				this.parameters = new DisplayableParameters(this.nodeInfo.ParameterList);

			if (nodeInfo.HasChildren)
			{
				this.children = new SortedDictionary<string, TreeNode>()
				{
					{ string.Empty, new Loading(this) }
				};
			}
		}

		public string NodeId => this.nodeInfo.NodeId;
		public string SourceId => this.nodeInfo.SourceId;
		public string Partition => this.nodeInfo.Partition;

		public override string Key => this.nodeInfo.NodeId;
		public override string Header => this.nodeInfo.LocalId;
		public override string ToolTip => "Node";
		public override string TypeName => this.nodeInfo.NodeType;
		public override bool CanRecycle => false;
		public override DisplayableParameters DisplayableParameters => this.parameters;

		public override ImageSource ImageResource
		{
			get
			{
				if (this.nodeInfo.HasChildren)
				{
					if (this.IsExpanded)
						return XmppAccountNode.folderOpen;
					else
						return XmppAccountNode.folderClosed;
				}
				else
					return XmppAccountNode.box;
			}
		}

		public override void Write(XmlWriter Output)
		{
			// Don't output.
		}

		public XmppConcentrator Concentrator
		{
			get
			{
				TreeNode Loop = this.Parent;

				while (Loop != null)
				{
					if (Loop is XmppConcentrator Concentrator)
						return Concentrator;

					Loop = Loop.Parent;
				}

				return null;
			}
		}

		private bool loadingChildren = false;

		public ConcentratorClient ConcentratorClient
		{
			get
			{
				XmppConcentrator Concentrator = this.Concentrator;
				if (Concentrator == null)
					return null;

				XmppAccountNode AccountNode = Concentrator.XmppAccountNode;
				if (AccountNode == null)
					return null;

				return AccountNode.ConcentratorClient;
			}
		}

		protected override void LoadChildren()
		{
			if (!this.loadingChildren && this.children != null && this.children.Count == 1 && this.children.ContainsKey(string.Empty))
			{
				string FullJid = this.Concentrator?.FullJid;
				ConcentratorClient ConcentratorClient = this.ConcentratorClient;

				if (ConcentratorClient != null && !string.IsNullOrEmpty(FullJid))
				{
					if (this.nodeInfo.HasChildren)
					{
						Mouse.OverrideCursor = Cursors.Wait;

						this.loadingChildren = true;
						ConcentratorClient.GetChildNodes(FullJid, this.nodeInfo, true, true, "en", string.Empty, string.Empty, string.Empty, (sender, e) =>
						{
							this.loadingChildren = false;
							MainWindow.MouseDefault();

							if (e.Ok)
							{
								SortedDictionary<string, TreeNode> Children = new SortedDictionary<string, TreeNode>();

								foreach (NodeInformation Ref in e.NodesInformation)
									Children[Ref.NodeId] = new Node(this, Ref);

								this.children = Children;

								this.OnUpdated();
								this.Concentrator?.NodesAdded(Children.Values, this);
							}
						}, null);
					}
					else
					{
						if (this.children != null)
							this.Concentrator?.NodesRemoved(this.children.Values, this);

						this.children = null;

						this.OnUpdated();
					}
				}
			}

			base.LoadChildren();
		}

		protected override void UnloadChildren()
		{
			base.UnloadChildren();

			if (this.nodeInfo.HasChildren && (this.children == null || this.children.Count != 1 || !this.children.ContainsKey(string.Empty)))
			{
				if (this.children != null)
					this.Concentrator?.NodesRemoved(this.children.Values, this);

				this.children = new SortedDictionary<string, TreeNode>()
				{
					{ string.Empty, new Loading(this) }
				};

				this.OnUpdated();
			}
		}

		public override bool CanReadSensorData => this.nodeInfo.IsReadable && this.IsOnline;
		public override bool CanSubscribeToSensorData => this.nodeInfo.IsReadable && this.Concentrator.SupportsEvents && this.IsOnline;

		public override SensorDataClientRequest StartSensorDataMomentaryReadout()
		{
			XmppConcentrator Concentrator = this.Concentrator;
			XmppAccountNode XmppAccountNode = Concentrator.XmppAccountNode;
			SensorClient SensorClient;

			if (XmppAccountNode != null && (SensorClient = XmppAccountNode.SensorClient) != null)
			{
				return SensorClient.RequestReadout(Concentrator.RosterItem.LastPresenceFullJid,
					new ThingReference[] { new ThingReference(this.nodeInfo.NodeId, this.nodeInfo.SourceId, this.nodeInfo.Partition) }, FieldType.Momentary);
			}
			else
				return null;
		}

		public override SensorDataClientRequest StartSensorDataFullReadout()
		{
			XmppConcentrator Concentrator = this.Concentrator;
			XmppAccountNode XmppAccountNode = Concentrator.XmppAccountNode;
			SensorClient SensorClient;

			if (XmppAccountNode != null && (SensorClient = XmppAccountNode.SensorClient) != null)
			{
				return SensorClient.RequestReadout(Concentrator.RosterItem.LastPresenceFullJid,
					new ThingReference[] { new ThingReference(this.nodeInfo.NodeId, this.nodeInfo.SourceId, this.nodeInfo.Partition) }, FieldType.All);
			}
			else
				throw new NotSupportedException();
		}

		public override SensorDataSubscriptionRequest SubscribeSensorDataMomentaryReadout(FieldSubscriptionRule[] Rules)
		{
			XmppConcentrator Concentrator = this.Concentrator;
			XmppAccountNode XmppAccountNode = Concentrator.XmppAccountNode;
			SensorClient SensorClient;

			if (XmppAccountNode != null && (SensorClient = XmppAccountNode.SensorClient) != null)
			{
				return SensorClient.Subscribe(Concentrator.RosterItem.LastPresenceFullJid,
					new ThingReference[] { new ThingReference(this.nodeInfo.NodeId, this.nodeInfo.SourceId, this.nodeInfo.Partition) },
					FieldType.Momentary, Rules, new Duration(false, 0, 0, 0, 0, 0, 1), new Duration(false, 0, 0, 0, 0, 1, 0), false);
			}
			else
				return null;
		}

		public override bool CanConfigure => this.nodeInfo.IsControllable && this.IsOnline;

		public override void GetConfigurationForm(DataFormResultEventHandler Callback, object State)
		{
			XmppConcentrator Concentrator = this.Concentrator;
			XmppAccountNode XmppAccountNode = Concentrator.XmppAccountNode;
			ControlClient ControlClient;

			if (XmppAccountNode != null && (ControlClient = XmppAccountNode.ControlClient) != null)
			{
				ControlClient.GetForm(Concentrator.RosterItem.LastPresenceFullJid, "en", Callback, State,
					new ThingReference(this.nodeInfo.NodeId, this.nodeInfo.SourceId, this.nodeInfo.Partition));
			}
			else
				throw new NotSupportedException();
		}

		public bool IsOnline
		{
			get
			{
				XmppConcentrator Concentrator = this.Concentrator;
				if (Concentrator == null)
					return false;

				XmppAccountNode XmppAccountNode = Concentrator.XmppAccountNode;
				if (XmppAccountNode == null)
					return false;

				XmppClient Client = XmppAccountNode.Client;
				return Client != null && Client.State == XmppState.Connected;
			}
		}

		public override bool CanAddChildren => this.IsOnline;

		public override void Add()
		{
			string FullJid = this.Concentrator?.FullJid;
			ConcentratorClient ConcentratorClient = this.ConcentratorClient;

			if (ConcentratorClient != null && !string.IsNullOrEmpty(FullJid))
			{
				Mouse.OverrideCursor = Cursors.Wait;

				ConcentratorClient.GetAddableNodeTypes(FullJid, this.nodeInfo, "en", string.Empty, string.Empty, string.Empty, (sender, e) =>
				{
					MainWindow.MouseDefault();

					if (e.Ok)
					{
						switch (e.Result.Length)
						{
							case 0:
								MainWindow.ErrorBox("No nodes can be added to this type of node.");
								break;

							case 1:
								this.Add(e.Result[0].Unlocalized);
								break;

							default:
								MainWindow.currentInstance.Dispatcher.BeginInvoke(new ThreadStart(() =>
								{
								SelectItemDialog Form = new SelectItemDialog("Add node", "Select type of node to add:",
									"Add node of selected type.", "Type", "Class", e.Result)
									{
										Owner = MainWindow.currentInstance
									};

									bool? Result = Form.ShowDialog();

									if (Result.HasValue && Result.Value)
									{
										LocalizedString? Item = Form.SelectedItem;
										if (Item.HasValue)
											this.Add(Item.Value.Unlocalized);
									}
								}));
								break;
						}
					}
				}, null);
			}
		}

		private void Add(string Type)
		{

		}

	}
}
