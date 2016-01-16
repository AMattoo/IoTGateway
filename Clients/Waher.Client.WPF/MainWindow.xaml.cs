﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using Waher.Content;
using Waher.Events;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Sensor;
using Waher.Things;
using Waher.Things.SensorData;
using Waher.Client.WPF.Controls;
using Waher.Client.WPF.Controls.Chat;
using Waher.Client.WPF.Controls.Sniffers;
using Waher.Client.WPF.Dialogs;
using Waher.Client.WPF.Model;

namespace Waher.Client.WPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public const string WindowTitle = "Simple XMPP IoT Client";

		public static RoutedUICommand Add = new RoutedUICommand("Add", "Add", typeof(MainWindow));
		public static RoutedUICommand ConnectTo = new RoutedUICommand("Connect To", "ConnectTo", typeof(MainWindow));
		public static RoutedUICommand Sniff = new RoutedUICommand("Sniff", "Sniff", typeof(MainWindow));
		public static RoutedUICommand CloseTab = new RoutedUICommand("Close Tab", "CloseTab", typeof(MainWindow));
		public static RoutedUICommand Chat = new RoutedUICommand("Chat", "Chat", typeof(MainWindow));
		public static RoutedUICommand ReadMomentary = new RoutedUICommand("Read Momentary", "ReadMomentary", typeof(MainWindow));
		public static RoutedUICommand ReadDetailed = new RoutedUICommand("Read Detailed", "ReadDetailed", typeof(MainWindow));
		internal static MainWindow currentInstance = null;

		public MainWindow()
		{
			if (currentInstance == null)
				currentInstance = this;

			InitializeComponent();

			this.MainView.Load(this);
		}

		private static readonly string registryKey = Registry.CurrentUser + @"\Software\Waher Data AB\Waher.Client.WPF";

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			object Value;

			try
			{
				Value = Registry.GetValue(registryKey, "WindowLeft", (int)this.Left);
				if (Value != null && Value is int)
					this.Left = (int)Value;

				Value = Registry.GetValue(registryKey, "WindowTop", (int)this.Top);
				if (Value != null && Value is int)
					this.Top = (int)Value;

				Value = Registry.GetValue(registryKey, "WindowWidth", (int)this.Width);
				if (Value != null && Value is int)
					this.Width = (int)Value;

				Value = Registry.GetValue(registryKey, "WindowHeight", (int)this.Height);
				if (Value != null && Value is int)
					this.Height = (int)Value;

				Value = Registry.GetValue(registryKey, "ConnectionTreeWidth", (int)this.MainView.ConnectionTree.Width);
				if (Value != null && Value is int)
					this.MainView.ConnectionsGrid.ColumnDefinitions[0].Width = new GridLength((int)Value);

				Value = Registry.GetValue(registryKey, "WindowState", this.WindowState.ToString());
				if (Value != null && Value is string)
					this.WindowState = (WindowState)Enum.Parse(typeof(WindowState), (string)Value);

				Value = Registry.GetValue(registryKey, "FileName", string.Empty);
				if (Value != null && Value is string)
				{
					this.MainView.FileName = (string)Value;
					if (!string.IsNullOrEmpty(this.MainView.FileName))
						this.MainView.Load(this.MainView.FileName);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Unable to load values from registry.", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!this.MainView.CheckSaved())
			{
				e.Cancel = true;
				return;
			}

			Registry.SetValue(registryKey, "WindowLeft", (int)this.Left, RegistryValueKind.DWord);
			Registry.SetValue(registryKey, "WindowTop", (int)this.Top, RegistryValueKind.DWord);
			Registry.SetValue(registryKey, "WindowWidth", (int)this.Width, RegistryValueKind.DWord);
			Registry.SetValue(registryKey, "WindowHeight", (int)this.Height, RegistryValueKind.DWord);
			Registry.SetValue(registryKey, "ConnectionTreeWidth", (int)this.MainView.ConnectionsGrid.ColumnDefinitions[0].Width.Value, RegistryValueKind.DWord);
			Registry.SetValue(registryKey, "WindowState", this.WindowState.ToString(), RegistryValueKind.String);
			Registry.SetValue(registryKey, "FileName", this.MainView.FileName, RegistryValueKind.String);
		}
		private void ConnectTo_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.MainView.ConnectTo_Executed(sender, e);
		}

		public ITabView CurrentTab
		{
			get
			{
				TabItem TabItem = this.Tabs.SelectedItem as TabItem;
				if (TabItem == null)
					return null;
				else
					return TabItem.Content as ITabView;
			}
		}

		private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			ITabView TabView = this.CurrentTab;
			if (TabView != null)
				TabView.SaveButton_Click(sender, e);
		}

		private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			ITabView TabView = this.CurrentTab;
			if (TabView != null)
				TabView.SaveAsButton_Click(sender, e);
		}

		private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			ITabView TabView = this.CurrentTab;
			if (TabView != null)
				TabView.OpenButton_Click(sender, e);
		}

		private void New_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			ITabView TabView = this.CurrentTab;
			if (TabView != null)
				TabView.NewButton_Click(sender, e);
		}

		internal void SelectionChanged()
		{
			TreeNode Node = this.SelectedNode;

			if (Node == null)
			{
				this.AddButton.IsEnabled = false;
				this.DeleteButton.IsEnabled = false;
				this.RefreshButton.IsEnabled = false;
				this.ChatButton.IsEnabled = false;
				this.ReadMomentaryButton.IsEnabled = false;
				this.ReadDetailedButton.IsEnabled = false;
			}
			else
			{
				this.AddButton.IsEnabled = Node.CanAddChildren;
				this.DeleteButton.IsEnabled = true;
				this.RefreshButton.IsEnabled = Node.CanRecycle;
				this.SniffButton.IsEnabled = Node.IsSniffable;
				this.ChatButton.IsEnabled = Node.CanChat;
				this.ReadMomentaryButton.IsEnabled = Node.CanReadSensorData;
				this.ReadDetailedButton.IsEnabled = Node.CanReadSensorData;
			}
		}

		private TreeNode SelectedNode
		{
			get
			{
				if (this.Tabs == null)
					return null;

				if (this.Tabs.SelectedIndex != 0)
					return null;

				if (this.MainView == null || this.MainView.ConnectionTree == null)
					return null;

				return this.MainView.SelectedNode;
			}
		}

		public static MainWindow FindWindow(FrameworkElement Element)
		{
			MainWindow MainWindow = Element as MainWindow;

			while (MainWindow == null && Element != null)
			{
				Element = Element.Parent as FrameworkElement;
				MainWindow = Element as MainWindow;
			}

			return MainWindow;
		}

		private void Add_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null && Node.CanAddChildren);
		}

		private void Add_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null || !Node.CanAddChildren)
				return;

			Node.Add();
		}

		private void Refresh_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null && Node.CanRecycle);
		}

		private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null || !Node.CanRecycle)
				return;

			Node.Recycle();
		}

		private void Delete_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null);
		}

		private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null)
				return;

			if (MessageBox.Show(this, "Are you sure you want to remove " + Node.Header + "?", "Are you sure?", MessageBoxButton.YesNo,
				MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
			{
				this.MainView.NodeRemoved(Node.Parent, Node);
			}
		}

		private void Sniff_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null && Node.IsSniffable);
		}

		private void Sniff_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null || !Node.IsSniffable)
				return;

			SnifferView View;

			foreach (TabItem Tab in this.Tabs.Items)
			{
				View = Tab.Content as SnifferView;
				if (View == null)
					continue;

				if (View.Node == Node)
				{
					Tab.Focus();
					return;
				}
			}

			TabItem TabItem = new TabItem();
			this.Tabs.Items.Add(TabItem);

			View = new SnifferView(Node);

			TabItem.Header = Node.Header;
			TabItem.Content = View;

			View.Sniffer = new TabSniffer(TabItem, View);
			Node.AddSniffer(View.Sniffer);

			this.Tabs.SelectedItem = TabItem;
		}

		private void CloseTab_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = this.Tabs.SelectedIndex > 0;
		}

		private void CloseTab_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			int i = this.Tabs.SelectedIndex;
			if (i > 0)
			{
				TabItem TabItem = this.Tabs.Items[i] as TabItem;
				if (TabItem != null)
				{
					object Content = TabItem.Content;
					if (Content != null && Content is IDisposable)
						((IDisposable)Content).Dispose();
				}

				this.Tabs.Items.RemoveAt(i);
			}
		}

		private void Chat_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null && Node.CanChat);
		}

		private void Chat_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null || !Node.CanChat)
				return;

			ChatView View;

			foreach (TabItem Tab in this.Tabs.Items)
			{
				View = Tab.Content as ChatView;
				if (View == null)
					continue;

				if (View.Node == Node)
				{
					Tab.Focus();
					return;
				}
			}

			TabItem TabItem = new TabItem();
			this.Tabs.Items.Add(TabItem);

			View = new ChatView(Node);

			TabItem.Header = Node.Header;
			TabItem.Content = View;

			this.Tabs.SelectedItem = TabItem;

			Thread T = new Thread(this.FocusChatInput);
			T.Start(View);
		}

		private void FocusChatInput(object P)
		{
			Thread.Sleep(50);
			this.Dispatcher.BeginInvoke(new ParameterizedThreadStart(this.FocusChatInput2), P);
		}

		private void FocusChatInput2(object P)
		{
			ChatView View = (ChatView)P;
			View.Input.Focus();
		}

		public void OnChatMessage(object Sender, MessageEventArgs e)
		{
			this.Dispatcher.BeginInvoke(new ParameterizedThreadStart(this.ChatMessageReceived), e);
		}

		private void ChatMessageReceived(object P)
		{
			MessageEventArgs e = (MessageEventArgs)P;
			ChatView ChatView;

			string Message = e.Body;
			bool IsMarkdown = false;

			foreach (XmlNode N in e.Message.ChildNodes)
			{
				if (N.LocalName == "content" && N.NamespaceURI == "urn:xmpp:content")
				{
					string Type = XML.Attribute((XmlElement)N, "type");
					if (Type == "text/markdown")
					{
						IsMarkdown = true;

						Type = N.InnerText;
						if (!string.IsNullOrEmpty(Type))
							Message = Type;

						break;
					}
				}
			}

			foreach (TabItem TabItem in this.Tabs.Items)
			{
				ChatView = TabItem.Content as ChatView;
				if (ChatView == null)
					continue;

				XmppContact XmppContact = ChatView.Node as XmppContact;
				if (XmppContact == null)
					continue;

				if (XmppContact.BareJID != e.FromBareJID)
					continue;

				XmppAccountNode XmppAccountNode = XmppContact.XmppAccountNode;
				if (XmppAccountNode == null)
					continue;

				if (XmppAccountNode.BareJID != XmppClient.GetBareJID(e.To))
					continue;

				ChatView.ChatMessageReceived(Message, IsMarkdown);
				return;
			}

			foreach (TreeNode Node in this.MainView.ConnectionTree.Items)
			{
				XmppAccountNode XmppAccountNode = Node as XmppAccountNode;
				if (XmppAccountNode == null)
					continue;

				if (XmppAccountNode.BareJID != XmppClient.GetBareJID(e.To))
					continue;

				TreeNode ContactNode;

				if (XmppAccountNode.TryGetChild(e.FromBareJID, out ContactNode))
				{
					TabItem TabItem2 = new TabItem();
					this.Tabs.Items.Add(TabItem2);

					ChatView = new ChatView(ContactNode);

					TabItem2.Header = e.FromBareJID;
					TabItem2.Content = ChatView;

					ChatView.ChatMessageReceived(Message, IsMarkdown);
					return;
				}
			}
		}

		private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			TabItem Item = this.Tabs.SelectedItem as TabItem;
			if (Item != null)
			{
				ChatView View = Item.Content as ChatView;
				if (View != null)
				{
					Thread T = new Thread(this.FocusChatInput);
					T.Start(View);
				}
			}
		}

		private void ReadMomentary_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null && Node.CanReadSensorData);
		}

		private void ReadMomentary_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null || !Node.CanReadSensorData)
				return;

			SensorDataClientRequest Request = Node.StartSensorDataMomentaryReadout();
			if (Request == null)
				return;

			TabItem TabItem = new TabItem();
			this.Tabs.Items.Add(TabItem);

			SensorDataView View = new SensorDataView(Request, Node);

			TabItem.Header = Node.Header;
			TabItem.Content = View;

			this.Tabs.SelectedItem = TabItem;
		}

		private void ReadDetailed_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			e.CanExecute = (Node != null && Node.CanReadSensorData);
		}

		private void ReadDetailed_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TreeNode Node = this.SelectedNode;
			if (Node == null || !Node.CanReadSensorData)
				return;

			SensorDataClientRequest Request = Node.StartSensorDataFullReadout();
			if (Request == null)
				return;

			TabItem TabItem = new TabItem();
			this.Tabs.Items.Add(TabItem);

			SensorDataView View = new SensorDataView(Request, Node);

			TabItem.Header = Node.Header;
			TabItem.Content = View;

			this.Tabs.SelectedItem = TabItem;
		}

	}
}
