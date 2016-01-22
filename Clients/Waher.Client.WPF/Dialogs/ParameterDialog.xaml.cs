﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Waher.Content;
using Waher.Script;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.DataForms;
using Waher.Networking.XMPP.DataForms.FieldTypes;
using Waher.Networking.XMPP.DataForms.Layout;

namespace Waher.Client.WPF.Dialogs
{
	/// <summary>
	/// Interaction logic for ParameterDialog.xaml
	/// </summary>
	public partial class ParameterDialog : Window
	{
		private DataForm form;

		/// <summary>
		/// Interaction logic for ParameterDialog.xaml
		/// </summary>
		public ParameterDialog(DataForm Form)
		{
			InitializeComponent();
			this.form = Form;

			this.Title = Form.Title;

			Panel Container = this.DialogPanel;
			TabControl TabControl = null;
			TabItem TabItem;
			StackPanel StackPanel;
			ScrollViewer ScrollViewer;

			if (Form.HasPages)
			{
				TabControl = new TabControl();
				this.DialogPanel.Children.Add(TabControl);
				DockPanel.SetDock(TabControl, Dock.Top);
			}
			else
			{
				ScrollViewer = new ScrollViewer();
				ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
				this.DialogPanel.Children.Add(ScrollViewer);
				DockPanel.SetDock(ScrollViewer, Dock.Top);

				StackPanel = new StackPanel();
				StackPanel.Margin = new Thickness(10, 10, 10, 10);
				ScrollViewer.Content = StackPanel;
				Container = StackPanel;
			}

			foreach (Networking.XMPP.DataForms.Layout.Page Page in Form.Pages)
			{
				if (TabControl != null)
				{
					TabItem = new TabItem();
					TabItem.Header = Page.Label;
					TabControl.Items.Add(TabItem);

					ScrollViewer = new ScrollViewer();
					ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
					TabItem.Content = ScrollViewer;

					StackPanel = new StackPanel();
					StackPanel.Margin = new Thickness(10, 10, 10, 10);
					ScrollViewer.Content = StackPanel;
					Container = StackPanel;
				}
				else
					TabItem = null;

				foreach (LayoutElement Element in Page.Elements)
					this.Layout(Container, Element, Form);

				if (TabControl != null && TabControl.Items.Count == 1)
					TabItem.Focus();
			}

			this.CheckOkButtonEnabled();
		}

		private void Layout(Panel Container, LayoutElement Element, DataForm Form)
		{
			if (Element is FieldReference)
				this.Layout(Container, (FieldReference)Element, Form);
			else if (Element is Networking.XMPP.DataForms.Layout.TextElement)
				this.Layout(Container, (Networking.XMPP.DataForms.Layout.TextElement)Element, Form);
			else if (Element is Networking.XMPP.DataForms.Layout.Section)
				this.Layout(Container, (Networking.XMPP.DataForms.Layout.Section)Element, Form);
			else if (Element is ReportedReference)
				this.Layout(Container, (ReportedReference)Element, Form);
		}

		private void Layout(Panel Container, Networking.XMPP.DataForms.Layout.Section Section, DataForm Form)
		{
			GroupBox GroupBox = new GroupBox();
			Container.Children.Add(GroupBox);
			GroupBox.Header = Section.Label;
			GroupBox.Margin = new Thickness(5, 5, 5, 5);

			StackPanel StackPanel = new StackPanel();
			GroupBox.Content = StackPanel;
			StackPanel.Margin = new Thickness(5, 5, 5, 5);

			foreach (LayoutElement Element in Section.Elements)
				this.Layout(StackPanel, Element, Form);
		}

		private void Layout(Panel Container, Networking.XMPP.DataForms.Layout.TextElement TextElement, DataForm Form)
		{
			TextBlock TextBlock = new TextBlock();
			TextBlock.TextWrapping = TextWrapping.Wrap;
			TextBlock.Margin = new Thickness(0, 0, 0, 5);

			TextBlock.Text = TextElement.Text;
			Container.Children.Add(TextBlock);
		}

		private void Layout(Panel Container, FieldReference FieldReference, DataForm Form)
		{
			Field Field = Form[FieldReference.Var];
			if (Field == null)
				return;

			Field.Validate(Field.ValueStrings);

			if (Field is TextSingleField)
				this.Layout(Container, (TextSingleField)Field, Form);
			else if (Field is TextMultiField)
				this.Layout(Container, (TextMultiField)Field, Form);
			else if (Field is TextPrivateField)
				this.Layout(Container, (TextPrivateField)Field, Form);
			else if (Field is BooleanField)
				this.Layout(Container, (BooleanField)Field, Form);
			else if (Field is ListSingleField)
				this.Layout(Container, (ListSingleField)Field, Form);
			else if (Field is ListMultiField)
				this.Layout(Container, (ListMultiField)Field, Form);
			else if (Field is FixedField)
				this.Layout(Container, (FixedField)Field, Form);
			else if (Field is HiddenField)
				this.Layout(Container, (HiddenField)Field, Form);
			else if (Field is JidMultiField)
				this.Layout(Container, (JidMultiField)Field, Form);
			else if (Field is JidSingleField)
				this.Layout(Container, (JidSingleField)Field, Form);
			else if (Field is MediaField)
				this.Layout(Container, (MediaField)Field, Form);
		}

		private void Layout(Panel Container, BooleanField Field, DataForm Form)
		{
			TextBlock TextBlock = new TextBlock();
			TextBlock.TextWrapping = TextWrapping.Wrap;
			TextBlock.Text = Field.Label;

			if (Field.Required)
			{
				Run Run = new Run("*");
				TextBlock.Inlines.Add(Run);
				Run.Foreground = new SolidColorBrush(Colors.Red);
			}

			CheckBox CheckBox;
			bool IsChecked;

			CheckBox = new CheckBox();
			CheckBox.Name = "Form_" + Field.Var;
			CheckBox.Content = TextBlock;
			CheckBox.Margin = new Thickness(0, 3, 0, 3);
			CheckBox.IsEnabled = !Field.ReadOnly;
			CheckBox.ToolTip = Field.Description;

			if (!CommonTypes.TryParse(Field.ValueString, out IsChecked))
				CheckBox.IsChecked = null;
			else
				CheckBox.IsChecked = IsChecked;

			if (Field.HasError)
				CheckBox.Background = new SolidColorBrush(Colors.PeachPuff);
			else if (Field.NotSame)
				CheckBox.Background = new SolidColorBrush(Colors.LightGray);

			CheckBox.Click += new RoutedEventHandler(CheckBox_Click);

			Container.Children.Add(CheckBox);
		}

		private void CheckBox_Click(object sender, RoutedEventArgs e)
		{
			CheckBox CheckBox = sender as CheckBox;
			if (CheckBox == null)
				return;

			string Var = CheckBox.Name.Substring(5);
			Field Field = this.form[Var];
			if (Field == null)
				return;

			if (CheckBox.IsChecked.HasValue)
			{
				CheckBox.Background = null;
				Field.SetValue(CommonTypes.Encode(CheckBox.IsChecked.Value));
				this.CheckOkButtonEnabled();
			}
			else
			{
				CheckBox.Background = new SolidColorBrush(Colors.LightGray);
				Field.SetValue(string.Empty);
			}
		}

		private void CheckOkButtonEnabled()
		{
			foreach (Field Field in this.form.Fields)
			{
				if (!Field.ReadOnly && Field.HasError)
				{
					this.OkButton.IsEnabled = false;
					return;
				}

				if (Field.Required && string.IsNullOrEmpty(Field.ValueString))
				{
					this.OkButton.IsEnabled = false;
					return;
				}
			}

			this.OkButton.IsEnabled = true;
		}

		private void Layout(Panel Container, FixedField Field, DataForm Form)
		{
			TextBlock TextBlock = new TextBlock();
			TextBlock.TextWrapping = TextWrapping.Wrap;
			TextBlock.Margin = new Thickness(0, 5, 0, 5);

			TextBlock.Text = Field.ValueString;
			Container.Children.Add(TextBlock);
		}

		private void Layout(Panel Container, HiddenField Field, DataForm Form)
		{
			// Do nothing
		}

		private void Layout(Panel Container, JidMultiField Field, DataForm Form)
		{
			TextBox TextBox = this.LayoutTextBox(Container, Field);
			TextBox.TextChanged += new TextChangedEventHandler(TextBox_TextChanged);
			TextBox.AcceptsReturn = true;
			TextBox.AcceptsTab = true;
			TextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		}

		private void Layout(Panel Container, JidSingleField Field, DataForm Form)
		{
			TextBox TextBox = this.LayoutTextBox(Container, Field);
			TextBox.TextChanged += new TextChangedEventHandler(TextBox_TextChanged);
		}

		private void Layout(Panel Container, ListMultiField Field, DataForm Form)
		{
			TextBlock TextBlock = new TextBlock();
			TextBlock.TextWrapping = TextWrapping.Wrap;
			TextBlock.Text = Field.Label;

			if (Field.Required)
			{
				Run Run = new Run("*");
				TextBlock.Inlines.Add(Run);
				Run.Foreground = new SolidColorBrush(Colors.Red);
			}

			GroupBox GroupBox = new GroupBox();
			Container.Children.Add(GroupBox);
			GroupBox.Name = "Form_" + Field.Var;
			GroupBox.Header = TextBlock;
			GroupBox.ToolTip = Field.Description;
			GroupBox.Margin = new Thickness(5, 5, 5, 5);

			StackPanel StackPanel = new StackPanel();
			GroupBox.Content = StackPanel;
			StackPanel.Margin = new Thickness(5, 5, 5, 5);

			string[] Values = Field.ValueStrings;
			CheckBox CheckBox;

			foreach (KeyValuePair<string, string> Option in Field.Options)
			{
				CheckBox = new CheckBox();
				CheckBox.Content = Option.Key;
				CheckBox.Tag = Option.Value;
				CheckBox.Margin = new Thickness(0, 3, 0, 3);
				CheckBox.IsEnabled = !Field.ReadOnly;

				CheckBox.IsChecked = Array.IndexOf<string>(Values, Option.Value) >= 0;

				if (Field.HasError)
					CheckBox.Background = new SolidColorBrush(Colors.PeachPuff);
				else if (Field.NotSame)
					CheckBox.Background = new SolidColorBrush(Colors.LightGray);

				CheckBox.Click += new RoutedEventHandler(MultiListCheckBox_Click);

				StackPanel.Children.Add(CheckBox);
			}

			GroupBox.Tag = this.LayoutErrorLabel(StackPanel, Field);
		}

		private void MultiListCheckBox_Click(object sender, RoutedEventArgs e)
		{
			CheckBox CheckBox = sender as CheckBox;
			if (CheckBox == null)
				return;

			StackPanel StackPanel = CheckBox.Parent as StackPanel;
			if (StackPanel == null)
				return;

			GroupBox GroupBox = StackPanel.Parent as GroupBox;
			if (GroupBox == null)
				return;

			string Var = GroupBox.Name.Substring(5);
			Field Field = this.form[Var];
			if (Field == null)
				return;

			List<string> Values = new List<string>();

			foreach (UIElement Element in StackPanel.Children)
			{
				CheckBox = Element as CheckBox;
				if (CheckBox == null)
					continue;

				if (CheckBox.IsChecked.HasValue && CheckBox.IsChecked.Value)
					Values.Add((string)CheckBox.Tag);
			}

			Field.SetValue(Values.ToArray());

			TextBlock ErrorLabel = (TextBlock)GroupBox.Tag;
			Brush Background;

			if (Field.HasError)
			{
				Background = new SolidColorBrush(Colors.PeachPuff);
				this.OkButton.IsEnabled = false;
				ErrorLabel.Text = Field.Error;
				ErrorLabel.Visibility = Visibility.Visible;
			}
			else
			{
				Background = null;
				this.CheckOkButtonEnabled();
				ErrorLabel.Visibility = Visibility.Collapsed;
			}

			foreach (UIElement Element in StackPanel.Children)
			{
				CheckBox = Element as CheckBox;
				if (CheckBox == null)
					continue;

				CheckBox.Background = Background;
			}
		}

		private void Layout(Panel Container, ListSingleField Field, DataForm Form)
		{
			this.LayoutControlLabel(Container, Field);

			ComboBox ComboBox = new ComboBox();
			ComboBox.Name = "Form_" + Field.Var;
			ComboBox.IsEnabled = !Field.ReadOnly;
			ComboBox.ToolTip = Field.Description;
			ComboBox.Margin = new Thickness(0, 0, 0, 5);

			if (Field.HasError)
				ComboBox.Background = new SolidColorBrush(Colors.PeachPuff);
			else if (Field.NotSame)
				ComboBox.Background = new SolidColorBrush(Colors.LightGray);

			ComboBoxItem Item;

			foreach (KeyValuePair<string, string> P in Field.Options)
			{
				Item = new ComboBoxItem();
				Item.Content = P.Key;
				Item.Tag = P.Value;

				ComboBox.Items.Add(Item);
			}

			if (Field.ValidationMethod is Networking.XMPP.DataForms.ValidationMethods.OpenValidation)
			{
				ComboBox.IsEditable = true;
				ComboBox.Text = Field.ValueString;
				ComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
					new System.Windows.Controls.TextChangedEventHandler(ComboBox_TextChanged));
			}
			else
			{
				string s = Field.ValueString;

				ComboBox.IsEditable = false;
				ComboBox.SelectedIndex = Array.FindIndex<KeyValuePair<string, string>>(Field.Options, (P) => P.Value.Equals(s));
				ComboBox.SelectionChanged += new SelectionChangedEventHandler(ComboBox_SelectionChanged);
			}

			Container.Children.Add(ComboBox);
			ComboBox.Tag = this.LayoutErrorLabel(Container, Field);
		}

		private void ComboBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			ComboBox ComboBox = sender as ComboBox;
			if (ComboBox == null)
				return;

			string Var = ComboBox.Name.Substring(5);
			Field Field = this.form[Var];
			if (Field == null)
				return;

			TextBlock ErrorLabel = (TextBlock)ComboBox.Tag;
			string s = ComboBox.Text;
			ComboBoxItem ComboBoxItem = ComboBox.SelectedItem as ComboBoxItem;

			if (ComboBoxItem != null && ((string)ComboBoxItem.Content) == s)
				s = (string)ComboBoxItem.Tag;

			Field.SetValue(s);

			if (Field.HasError)
			{
				ComboBox.Background = new SolidColorBrush(Colors.PeachPuff);
				this.OkButton.IsEnabled = false;
				ErrorLabel.Text = Field.Error;
				ErrorLabel.Visibility = Visibility.Visible;
			}
			else
			{
				ComboBox.Background = null;
				ErrorLabel.Visibility = Visibility.Collapsed;
				this.CheckOkButtonEnabled();
			}
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox ComboBox = sender as ComboBox;
			if (ComboBox == null)
				return;

			string Var = ComboBox.Name.Substring(5);
			Field Field = this.form[Var];
			if (Field == null)
				return;

			TextBlock ErrorLabel = (TextBlock)ComboBox.Tag;
			ComboBoxItem Item = ComboBox.SelectedItem as ComboBoxItem;
			string Value;

			if (Item == null)
				Value = string.Empty;
			else
				Value = (string)Item.Tag;

			Field.SetValue(Value);

			if (Field.HasError)
			{
				ComboBox.Background = new SolidColorBrush(Colors.PeachPuff);
				this.OkButton.IsEnabled = false;
				ErrorLabel.Text = Field.Error;
				ErrorLabel.Visibility = Visibility.Visible;
				return;
			}
			else
			{
				ComboBox.Background = null;
				ErrorLabel.Visibility = Visibility.Collapsed;
				this.CheckOkButtonEnabled();
			}
		}

		private void Layout(Panel Container, MediaField Field, DataForm Form)
		{
			MediaElement MediaElement;
			Uri Uri = null;
			Grade Best = Script.Grade.NotAtAll;
			Grade Grade;
			bool IsImage = false;
			bool IsVideo = false;
			bool IsAudio = false;

			bool TopMarginLaidOut = this.LayoutControlLabel(Container, Field);

			foreach (KeyValuePair<string, Uri> P in Field.Media.URIs)
			{
				if (P.Key.StartsWith("image/"))
				{
					IsImage = true;
					Uri = P.Value;
					break;
				}
				else if (P.Key.StartsWith("video/"))
				{
					switch (P.Key.ToLower())
					{
						case "video/x-ms-asf":
						case "video/x-ms-wvx":
						case "video/x-ms-wm":
						case "video/x-ms-wmx":
							Grade = Grade.Perfect;
							break;

						case "video/mp4":
							Grade = Grade.Excellent;
							break;

						case "video/3gp":
						case "video/3gpp ":
						case "video/3gpp2 ":
						case "video/3gpp-tt":
						case "video/h263":
						case "video/h263-1998":
						case "video/h263-2000":
						case "video/h264":
						case "video/h264-rcdo":
						case "video/h264-svc":
							Grade = Grade.Ok;
							break;

						default:
							Grade = Grade.Barely;
							break;
					}

					if (Grade > Best)
					{
						Best = Grade;
						Uri = P.Value;
						IsVideo = true;
					}
				}
				else if (P.Key.StartsWith("audio/"))
				{
					switch (P.Key.ToLower())
					{
						case "audio/x-ms-wma":
						case "audio/x-ms-wax":
						case "audio/x-ms-wmv":
							Grade = Grade.Perfect;
							break;

						case "audio/mp4":
						case "audio/mpeg":
							Grade = Grade.Excellent;
							break;

						case "audio/amr":
						case "audio/amr-wb":
						case "audio/amr-wb+":
						case "audio/pcma":
						case "audio/pcma-wb":
						case "audio/pcmu":
						case "audio/pcmu-wb":
							Grade = Grade.Ok;
							break;

						default:
							Grade = Grade.Barely;
							break;
					}

					if (Grade > Best)
					{
						Best = Grade;
						Uri = P.Value;
						IsAudio = true;
					}
				}
			}

			if (IsImage)
			{
				BitmapImage BitmapImage = new System.Windows.Media.Imaging.BitmapImage();
				BitmapImage.BeginInit();
				BitmapImage.UriSource = Uri;
				BitmapImage.EndInit();

				Image Image = new Image();
				Image.Source = BitmapImage;
				Image.ToolTip = Field.Description;
				Image.Margin = new Thickness(0, TopMarginLaidOut ? 0 : 5, 0, 5);

				if (Field.Media.Width.HasValue)
					Image.Width = Field.Media.Width.Value;

				if (Field.Media.Height.HasValue)
					Image.Height = Field.Media.Height.Value;

				Container.Children.Add(Image);
			}
			else if (IsVideo || IsAudio)
			{
				MediaElement = new MediaElement();
				MediaElement.Source = Uri;
				MediaElement.LoadedBehavior = MediaState.Manual;
				MediaElement.ToolTip = Field.Description;
				Container.Children.Add(MediaElement);

				if (IsVideo)
				{
					MediaElement.Margin = new Thickness(0, TopMarginLaidOut ? 0 : 5, 0, 5);

					if (Field.Media.Width.HasValue)
						MediaElement.Width = Field.Media.Width.Value;

					if (Field.Media.Height.HasValue)
						MediaElement.Height = Field.Media.Height.Value;
				}

				DockPanel ControlPanel = new DockPanel();
				ControlPanel.Width = 290;
				Container.Children.Add(ControlPanel);

				Button Button = new Button();
				Button.Width = 50;
				Button.Height = 23;
				Button.Margin = new Thickness(0, 0, 5, 0);
				Button.Content = "<<";
				ControlPanel.Children.Add(Button);
				Button.Click += new RoutedEventHandler(Rewind_Click);
				Button.Tag = MediaElement;

				Button = new Button();
				Button.Width = 50;
				Button.Height = 23;
				Button.Margin = new Thickness(5, 0, 5, 0);
				Button.Content = "Play";
				ControlPanel.Children.Add(Button);
				Button.Click += new RoutedEventHandler(Play_Click);
				Button.Tag = MediaElement;

				Button = new Button();
				Button.Width = 50;
				Button.Height = 23;
				Button.Margin = new Thickness(5, 0, 5, 0);
				Button.Content = "Pause";
				ControlPanel.Children.Add(Button);
				Button.Click += new RoutedEventHandler(Pause_Click);
				Button.Tag = MediaElement;

				Button = new Button();
				Button.Width = 50;
				Button.Height = 23;
				Button.Margin = new Thickness(5, 0, 5, 0);
				Button.Content = "Stop";
				ControlPanel.Children.Add(Button);
				Button.Click += new RoutedEventHandler(Stop_Click);
				Button.Tag = MediaElement;

				Button = new Button();
				Button.Width = 50;
				Button.Height = 23;
				Button.Margin = new Thickness(5, 0, 0, 0);
				Button.Content = ">>";
				ControlPanel.Children.Add(Button);
				Button.Click += new RoutedEventHandler(Forward_Click);
				Button.Tag = MediaElement;

				MediaElement.Play();
			}
		}

		private void Rewind_Click(object sender, RoutedEventArgs e)
		{
			Button Button = (Button)sender;
			MediaElement MediaElement = (MediaElement)Button.Tag;

			if (MediaElement.SpeedRatio >= 0)
				MediaElement.SpeedRatio = -1;
			else if (MediaElement.SpeedRatio > -32)
				MediaElement.SpeedRatio *= 2;
		}

		private void Play_Click(object sender, RoutedEventArgs e)
		{
			Button Button = (Button)sender;
			MediaElement MediaElement = (MediaElement)Button.Tag;

			if (MediaElement.Position >= MediaElement.NaturalDuration.TimeSpan)
			{
				MediaElement.Stop();
				MediaElement.Position = TimeSpan.Zero;
			}

			MediaElement.Play();
		}

		private void Pause_Click(object sender, RoutedEventArgs e)
		{
			Button Button = (Button)sender;
			MediaElement MediaElement = (MediaElement)Button.Tag;
			MediaElement.Pause();
		}

		private void Stop_Click(object sender, RoutedEventArgs e)
		{
			Button Button = (Button)sender;
			MediaElement MediaElement = (MediaElement)Button.Tag;
			MediaElement.Stop();
		}

		private void Forward_Click(object sender, RoutedEventArgs e)
		{
			Button Button = (Button)sender;
			MediaElement MediaElement = (MediaElement)Button.Tag;

			if (MediaElement.SpeedRatio <= 0)
				MediaElement.SpeedRatio = 1;
			else if (MediaElement.SpeedRatio < 32)
				MediaElement.SpeedRatio *= 2;
		}

		private void Layout(Panel Container, TextMultiField Field, DataForm Form)
		{
			TextBox TextBox = this.LayoutTextBox(Container, Field);
			TextBox.TextChanged += new TextChangedEventHandler(TextBox_TextChanged);
			TextBox.AcceptsReturn = true;
			TextBox.AcceptsTab = true;
			TextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		}

		private void Layout(Panel Container, TextPrivateField Field, DataForm Form)
		{
			this.LayoutControlLabel(Container, Field);

			PasswordBox PasswordBox = new PasswordBox();
			PasswordBox.Name = "Form_" + Field.Var;
			PasswordBox.Password = Field.ValueString;
			PasswordBox.IsEnabled = !Field.ReadOnly;
			PasswordBox.ToolTip = Field.Description;
			PasswordBox.Margin = new Thickness(0, 0, 0, 5);

			if (Field.HasError)
				PasswordBox.Background = new SolidColorBrush(Colors.PeachPuff);
			else if (Field.NotSame)
				PasswordBox.Background = new SolidColorBrush(Colors.LightGray);

			PasswordBox.PasswordChanged += new RoutedEventHandler(PasswordBox_PasswordChanged);

			Container.Children.Add(PasswordBox);
			PasswordBox.Tag = this.LayoutErrorLabel(Container, Field);
		}

		private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			PasswordBox PasswordBox = sender as PasswordBox;
			if (PasswordBox == null)
				return;

			string Var = PasswordBox.Name.Substring(5);
			Field Field = this.form[Var];
			if (Field == null)
				return;

			Field.SetValue(PasswordBox.Password);

			TextBlock ErrorLabel = (TextBlock)PasswordBox.Tag;

			if (Field.HasError)
			{
				PasswordBox.Background = new SolidColorBrush(Colors.PeachPuff);
				this.OkButton.IsEnabled = false;
				ErrorLabel.Text = Field.Error;
				ErrorLabel.Visibility = Visibility.Visible;
			}
			else
			{
				PasswordBox.Background = null;
				ErrorLabel.Visibility = Visibility.Collapsed;
				this.CheckOkButtonEnabled();
			}
		}

		private void Layout(Panel Container, TextSingleField Field, DataForm Form)
		{
			TextBox TextBox = this.LayoutTextBox(Container, Field);
			TextBox.TextChanged += new TextChangedEventHandler(TextBox_TextChanged);
		}

		private TextBox LayoutTextBox(Panel Container, Field Field)
		{
			this.LayoutControlLabel(Container, Field);

			TextBox TextBox = new TextBox();
			TextBox.Name = "Form_" + Field.Var;
			TextBox.Text = Field.ValueString;
			TextBox.IsEnabled = !Field.ReadOnly;
			TextBox.ToolTip = Field.Description;
			TextBox.Margin = new Thickness(0, 0, 0, 5);

			if (Field.HasError)
				TextBox.Background = new SolidColorBrush(Colors.PeachPuff);
			else if (Field.NotSame)
				TextBox.Background = new SolidColorBrush(Colors.LightGray);

			Container.Children.Add(TextBox);
			TextBox.Tag = this.LayoutErrorLabel(Container, Field);

			return TextBox;
		}

		private TextBlock LayoutErrorLabel(Panel Container, Field Field)
		{
			TextBlock ErrorLabel = new TextBlock();
			ErrorLabel.TextWrapping = TextWrapping.Wrap;
			ErrorLabel.Margin = new Thickness(0, 0, 0, 5);
			ErrorLabel.Text = Field.Error;
			ErrorLabel.Foreground = new SolidColorBrush(Colors.Red);
			ErrorLabel.FontWeight = FontWeights.Bold;
			ErrorLabel.Visibility = Field.HasError ? Visibility.Visible : Visibility.Collapsed;
			Container.Children.Add(ErrorLabel);

			return ErrorLabel;
		}

		private bool LayoutControlLabel(Panel Container, Field Field)
		{
			if (string.IsNullOrEmpty(Field.Label) && !Field.Required)
				return false;
			else
			{
				TextBlock TextBlock = new TextBlock();
				TextBlock.TextWrapping = TextWrapping.Wrap;
				TextBlock.Text = Field.Label;
				TextBlock.Margin = new Thickness(0, 5, 0, 0);
				Container.Children.Add(TextBlock);

				if (Field.Required)
				{
					Run Run = new Run("*");
					TextBlock.Inlines.Add(Run);
					Run.Foreground = new SolidColorBrush(Colors.Red);
				}

				return true;
			}
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			TextBox TextBox = sender as TextBox;
			if (TextBox == null)
				return;

			string Var = TextBox.Name.Substring(5);
			Field Field = this.form[Var];
			if (Field == null)
				return;

			TextBlock ErrorLabel = (TextBlock)TextBox.Tag;

			Field.SetValue(TextBox.Text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'));

			if (Field.HasError)
			{
				TextBox.Background = new SolidColorBrush(Colors.PeachPuff);
				this.OkButton.IsEnabled = false;
				ErrorLabel.Text = Field.Error;
				ErrorLabel.Visibility = Visibility.Visible;
			}
			else
			{
				TextBox.Background = null;
				ErrorLabel.Visibility = Visibility.Collapsed;
				this.CheckOkButtonEnabled();
			}
		}

		private void Layout(Panel Container, ReportedReference ReportedReference, DataForm Form)
		{
			if (Form.Records.Length == 0)
				return;


			// TODO: Include table of results.
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			this.form.Submit();

			this.DialogResult = true;
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			this.form.Cancel();
		}

		// TODO: Color picker.
		// TODO: Dynamic forms & post back

	}
}
