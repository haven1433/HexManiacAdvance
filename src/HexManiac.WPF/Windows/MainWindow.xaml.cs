using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class MainWindow {
      private readonly List<Action> deferredActions = new List<Action>();
      private ThemeSelector themeWindow;

      public EditorViewModel ViewModel { get; }

      public MainWindow(EditorViewModel viewModel) {
         InitializeComponent();
         ViewModel = viewModel;
         viewModel.RequestDelayedWork += (sender, e) => deferredActions.Add(e);
         DataContext = viewModel;
         viewModel.MoveFocusToFind += (sender, e) => FocusTextBox(FindBox);
         viewModel.GotoViewModel.MoveFocusToGoto += FocusGotoBox;
         viewModel.PropertyChanged += ViewModelPropertyChanged;

         GotoPanel.IsVisibleChanged += AnimateFocusToCorner;
         FindPanel.IsVisibleChanged += AnimateFocusToCorner;
         MessagePanel.IsVisibleChanged += AnimateFocusToCorner;
         ErrorPanel.IsVisibleChanged += AnimateFocusToCorner;
      }

      protected override void OnDrop(DragEventArgs e) {
         base.OnDrop(e);

         if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var fileName in files) {
               var data = File.ReadAllBytes(fileName);
               ViewModel.Open.Execute(new LoadedFile(fileName, data));
            }
         }
      }

      protected override void OnClosing(CancelEventArgs e) {
         base.OnClosing(e);
         ViewModel.CloseAll.Execute();
         if (ViewModel.Count != 0) {
            e.Cancel = true;
         } else {
            themeWindow?.Close();
            ViewModel.WriteAppLevelMetadata();
         }
      }

      private static FrameworkElement GetChild(DependencyObject depObj, string name, object dataContext) {
         for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
            var child = VisualTreeHelper.GetChild(depObj, i);
            var childContext = child.GetValue(DataContextProperty);
            var childName = child.GetValue(NameProperty);
            if (childContext == dataContext && name == childName.ToString()) return (FrameworkElement)child;
            var next = GetChild(child, name, dataContext);
            if (next != null) return next;
         }

         return null;
      }

      #region Tab Mouse Events

      private void TabMouseDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (e.LeftButton != MouseButtonState.Pressed) return;
         if (e.ChangedButton != MouseButton.Left) return;

         element.CaptureMouse();
      }

      /// <summary>
      /// If the mouse has dragged the tab through more than half of the next tab, swap the tabs horizontally.
      /// </summary>
      /// <remarks>
      /// The "more than half through the next tab" metric was chosen to deal with disparity between widths of tabs.
      /// A smaller number would cause tabs to flicker when a narrow tab is dragged past a wide tab.
      /// </remarks>
      private void TabMouseMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;

         var index = ViewModel.SelectedIndex;
         var leftWidth = index > 0 ? GetChild(Tabs, "TabTextBlock", ViewModel[index - 1]).ActualWidth : double.PositiveInfinity;
         var rightWidth = index < ViewModel.Count - 1 ? GetChild(Tabs, "TabTextBlock", ViewModel[index + 1]).ActualWidth : double.PositiveInfinity;
         var offset = e.GetPosition(element).X;

         if (offset < -leftWidth / 2) {
            ViewModel.SwapTabs(index, index - 1);
         } else if (offset > element.ActualWidth + rightWidth / 2) {
            ViewModel.SwapTabs(index, index + 1);
         }
      }

      private void TabMouseUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         if (e.LeftButton != MouseButtonState.Released) return;
         if (e.ChangedButton != MouseButton.Left) return;

         e.Handled = true;
         element.ReleaseMouseCapture();
      }

      #endregion

      private void HeaderMouseDown(object sender, MouseButtonEventArgs e) {
         var selectedElement = (HexContent)GetChild(Tabs, "HexContent", ViewModel[ViewModel.SelectedIndex]);
         selectedElement.RaiseEvent(e);
      }

      private void ToggleTheme(object sender, EventArgs e) {
         ViewModel.Theme.LightVariant = !ViewModel.Theme.LightVariant;
      }

      private void ExitClicked(object sender, EventArgs e) {
         ViewModel.CloseAll.Execute();
         if (ViewModel.Count == 0) Close();
      }

      private void WikiClick(object sender, EventArgs e) => System.Diagnostics.Process.Start("https://github.com/haven1433/HexManiacAdvance/wiki");
      private void ReportIssueClick(object sender, EventArgs e) => System.Diagnostics.Process.Start("https://github.com/haven1433/HexManiacAdvance/issues");
      private void AboutClick(object sender, EventArgs e) => new AboutWindow().ShowDialog();

      private void EditBoxVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) {
         var box = (TextBox)sender;
         if (!box.IsVisible) {
            if (ViewModel.SelectedIndex == -1) return;
            var selectedElement = (HexContent)GetChild(Tabs, "HexContent", ViewModel[ViewModel.SelectedIndex]);
            Keyboard.Focus(selectedElement);
            ViewModel.GotoViewModel.ShowAutoCompleteOptions = false;
         }
      }

      // when the ViewModel changes its GotoControlViewModel subsystem, update the event handler
      private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName != nameof(ViewModel.GotoViewModel)) return;
         var args = (ExtendedPropertyChangedEventArgs)e;
         var old = (GotoControlViewModel)args.OldValue;
         old.MoveFocusToGoto -= FocusGotoBox;
         ViewModel.GotoViewModel.MoveFocusToGoto += FocusGotoBox;
      }

      private void FocusGotoBox(object sender, EventArgs e) => FocusTextBox(GotoBox);

      private void FocusTextBox(TextBox textBox) {
         textBox.SelectAll();
         Keyboard.Focus(textBox);
      }

      private void RunDeferredActions(object sender, MouseButtonEventArgs e) {
         if (deferredActions.Count == 0) return;
         var copy = deferredActions.ToList();
         deferredActions.Clear();
         foreach (var action in copy) action();
      }

      private void StringToolContentSelectionChanged(object sender, RoutedEventArgs e) {
         var textbox = (TextBox)sender;
         var tools = textbox.DataContext as ToolTray;
         if (tools == null || tools.StringTool == null) return;
         tools.StringTool.ContentIndex = textbox.SelectionStart;
         tools.StringTool.ContentSelectionLength = textbox.SelectionLength;
      }

      private void AnimateFocusToCorner(object sender, DependencyPropertyChangedEventArgs e) {
         var element = (FrameworkElement)sender;
         if (element.Visibility != Visibility.Visible) return;
         element.Arrange(new Rect());

         FocusAnimationElement.Visibility = Visibility.Visible;
         var widthAnimation = new DoubleAnimation(ActualWidth, element.ActualWidth, TimeSpan.FromSeconds(.3));
         var heightAnimation = new DoubleAnimation(ActualHeight, element.ActualHeight, TimeSpan.FromSeconds(.3));
         heightAnimation.Completed += (sender1, e1) => FocusAnimationElement.Visibility = Visibility.Collapsed;

         FocusAnimationElement.BeginAnimation(WidthProperty, widthAnimation);
         FocusAnimationElement.BeginAnimation(HeightProperty, heightAnimation);
      }

      private void ShowThemeSelector(object sender, RoutedEventArgs e) {
         if (themeWindow?.IsVisible != true) {
            themeWindow = new ThemeSelector { DataContext = ViewModel.Theme };
            themeWindow.Show();
         }
      }
   }
}
