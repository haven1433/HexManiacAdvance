using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TabView {

      #region ZoomLevel

      public static readonly DependencyProperty ZoomLevelProperty = DependencyProperty.Register(nameof(ZoomLevel), typeof(int), typeof(TabView), new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

      public int ZoomLevel {
         get => (int)GetValue(ZoomLevelProperty);
         set => SetValue(ZoomLevelProperty, value);
      }

      #endregion

      public IFileSystem FileSystem => (IFileSystem)Application.Current.MainWindow.Resources["FileSystem"];
      public TabView() {
         InitializeComponent();
         CodeModeSelector.ItemsSource = Enum.GetValues(typeof(CodeMode)).Cast<CodeMode>();
      }

      #region Manual Selection Code

      private void HideManualSelection(object sender, EventArgs e) => ManualHighlight.Visibility = Visibility.Collapsed;

      private void ShowManualSelection(object sender, EventArgs e) {
         ManualHighlight.Visibility = Visibility.Visible;
         UpdateManualSelectionFromScroll(StringToolTextBox, EventArgs.Empty);
      }

      private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is ViewPort viewPortOld) {
            viewPortOld.Tools.StringTool.PropertyChanged -= HandleStringToolPropertyChanged;
         }
         if (e.NewValue is ViewPort viewPortNew) {
            viewPortNew.Tools.StringTool.PropertyChanged += HandleStringToolPropertyChanged;
         }
      }

      private void HandleStringToolPropertyChanged(object sender, PropertyChangedEventArgs e) {
         var tool = (PCSTool)sender;

         StringToolTextBox.SelectionChanged -= StringToolContentSelectionChanged;
         if (e.PropertyName is nameof(PCSTool.ContentIndex)) {
            StringToolTextBox.SelectionStart = tool.ContentIndex;
            UpdateManualSelection(tool);
         }
         if (e.PropertyName is nameof(PCSTool.ContentSelectionLength)) {
            StringToolTextBox.SelectionLength = tool.ContentSelectionLength;
            UpdateManualSelection(tool);
         }
         StringToolTextBox.SelectionChanged += StringToolContentSelectionChanged;
      }

      private void UpdateManualSelection(PCSTool tool) {
         var linesBeforeSelection = StringToolTextBox.Text.Substring(0, StringToolTextBox.SelectionStart).Split(Environment.NewLine).Length - 1;
         var totalLines = StringToolTextBox.Text.Split(Environment.NewLine).Length;
         var highestScroll = StringToolTextBox.ExtentHeight - StringToolTextBox.ViewportHeight;
         var verticalOffset = linesBeforeSelection * highestScroll / totalLines;
         StringToolTextBox.ScrollToVerticalOffset(verticalOffset);
      }

      private void UpdateManualSelectionFromScroll(object sender, EventArgs e) {
         if (ManualHighlight.Visibility != Visibility.Visible) return;

         var tools = StringToolTextBox.DataContext as ToolTray;
         if (tools == null || tools.StringTool == null) return;
         var tool = tools.StringTool;

         var linesBeforeSelection = StringToolTextBox.Text.Substring(0, StringToolTextBox.SelectionStart).Split(Environment.NewLine).Length - 1;
         var totalLines = StringToolTextBox.Text.Split(Environment.NewLine).Length;
         var verticalOffset = StringToolTextBox.VerticalOffset;
         var lineHeight = StringToolTextBox.ExtentHeight / totalLines;
         var verticalStart = lineHeight * linesBeforeSelection - verticalOffset + 2;
         if (verticalStart < 0 || verticalStart > StringToolTextBox.ViewportHeight) return;

         var selectionStart = StringToolTextBox.Text.Substring(0, StringToolTextBox.SelectionStart).Split(Environment.NewLine).Last().Length;
         const int fontWidth = 7; // FormattedText.Width gives more like 6.5, but 7 actually looks better.
         var horizontalStart = selectionStart * fontWidth + 2;
         var width = tool.ContentSelectionLength * fontWidth;
         ManualHighlight.Margin = new Thickness(horizontalStart, verticalStart, StringToolTextBox.ActualWidth - horizontalStart - width, 0);
      }

      #endregion

      private void StringToolContentSelectionChanged(object sender, RoutedEventArgs e) {
         var textbox = (TextBox)sender;
         var tools = textbox.DataContext as ToolTray;
         if (tools == null || tools.StringTool == null) return;
         var tool = tools.StringTool;

         tool.PropertyChanged -= HandleStringToolPropertyChanged;
         tool.ContentIndex = textbox.SelectionStart;
         tool.ContentSelectionLength = textbox.SelectionLength;
         tool.PropertyChanged += HandleStringToolPropertyChanged;
      }

      private void HeaderMouseDown(object sender, MouseButtonEventArgs e) {
         HexContent.RaiseEvent(e);
      }

      private readonly Popup contextMenu = new Popup();
      private void AddressShowMenu(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var viewModel = element.DataContext as ViewPort;
         if (viewModel == null) return;
         contextMenu.Child = new Button {
            Content = "Copy Address"
         }.SetEvent(Button.ClickEvent, (sender2, e2) => {
            viewModel.CopyAddress.Execute(FileSystem);
            contextMenu.IsOpen = false;
         });
         contextMenu.PlacementTarget = element;
         contextMenu.StaysOpen = false;
         contextMenu.IsOpen = true;
      }
   }
}
