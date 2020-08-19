using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Controls;
using HavenSoft.HexManiac.WPF.Implementations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class MainWindow {
      private readonly List<Action> deferredActions = new List<Action>();
      private ThemeSelector themeWindow;

      public EditorViewModel ViewModel { get; }
      public WindowsFileSystem FileSystem => (WindowsFileSystem)Resources["FileSystem"];

      public MainWindow(EditorViewModel viewModel) {
         InitializeComponent();
         ViewModel = viewModel;
         viewModel.RequestDelayedWork += (sender, e) => deferredActions.Add(e);
         DataContext = viewModel;
         viewModel.MoveFocusToFind += (sender, e) => FocusTextBox(FindBox);
         viewModel.MoveFocusToHexConverter += (sender, e) => FocusTextBox(HexBox);
         viewModel.GotoViewModel.MoveFocusToGoto += FocusGotoBox;
         viewModel.PropertyChanged += ViewModelPropertyChanged;

         GotoPanel.IsVisibleChanged += AnimateFocusToCorner;
         FindPanel.IsVisibleChanged += AnimateFocusToCorner;
         HexConverter.IsVisibleChanged += AnimateFocusToCorner;
         HexBox.GotFocus += (sender, e) => HexBox.SelectAll();
         DecBox.GotFocus += (sender, e) => DecBox.SelectAll();
         MessagePanel.IsVisibleChanged += AnimateFocusToCorner;
         ErrorPanel.IsVisibleChanged += AnimateFocusToCorner;

         viewModel.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(viewModel.InformationMessage) &&
               MessagePanel.IsVisible &&
               !string.IsNullOrEmpty(viewModel.InformationMessage)
            ) {
               AnimateFocusToCorner(MessagePanel, default);
            }
         };

         Application.Current.DispatcherUnhandledException += (sender, e) => {
            File.AppendAllText("crash.log", e.Exception.Message + Environment.NewLine + e.Exception.StackTrace);
            FileSystem.ShowCustomMessageBox("An unhandled error occured. Please report it on Discord or open an issue on GitHub." + Environment.NewLine +
               "HexManiac might be in a bad state. You should close as soon as possible." + Environment.NewLine +
               "The error has been logged to crash.log", showYesNoCancel: false);
            e.Handled = true;
         };

         FillQuickEditMenu();
      }

      private void FillQuickEditMenu() {
         foreach (var edit in ViewModel.QuickEdits) {
            var command = new StubCommand {
               CanExecute = arg => ViewModel.SelectedIndex >= 0 && edit.CanRun(ViewModel[ViewModel.SelectedIndex] as IViewPort),
               Execute = arg => {
                  Window window = default;
                  window = new Window {
                     Title = edit.Name,
                     Background = (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][nameof(Theme.Background)],
                     SizeToContent = SizeToContent.WidthAndHeight,
                     WindowStyle = WindowStyle.ToolWindow,
                     Content = new StackPanel {
                        Width = 300,
                        Children = {
                              new TextBlock {
                                 Margin = new Thickness(5),
                                 FontSize = 14,
                                 Text = edit.Description,
                                 TextWrapping = TextWrapping.Wrap,
                              },
                              new TextBlock(
                                 string.IsNullOrEmpty(edit.WikiLink) ? (Inline)new Run() :
                                 new Hyperlink(new Run("Click here to learn more.")) {
                                    Foreground = (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][nameof(Theme.Accent)],
                                    NavigateUri = new Uri(edit.WikiLink),
                                 }.Fluent(link => link.RequestNavigate += Navigate)
                              ) {
                                 HorizontalAlignment = HorizontalAlignment.Center,
                              },
                              new StackPanel {
                                 Orientation = Orientation.Horizontal,
                                 VerticalAlignment = VerticalAlignment.Bottom,
                                 HorizontalAlignment = HorizontalAlignment.Right,
                                 Children = {
                                    new Button {
                                       Content = "Run",
                                       Margin = new Thickness(5),
                                       Command = new StubCommand {
                                          CanExecute = arg1 => true,
                                          Execute = arg1 => {
                                             ViewModel.RunQuickEdit(edit);
                                             window.Close();
                                          }
                                       },
                                    },
                                    new Button {
                                       Content = "Cancel",
                                       IsCancel = true,
                                       Margin = new Thickness(5),
                                       Command = new StubCommand {
                                          CanExecute = arg1 => true,
                                          Execute = arg1 => window.Close(),
                                       },
                                    },
                                 },
                              },
                           },
                     },
                  };
                  window.ShowDialog();
               },
            };

            edit.CanRunChanged += (sender, e) => command.CanExecuteChanged.Invoke(command, EventArgs.Empty);

            QuickEdits.Items.Add(new MenuItem {
               Header = edit.Name,
               Command = command,
            });
         }
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
         e.Handled = true;
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

      public static FrameworkElement GetChild(DependencyObject depObj, string name, object dataContext) {
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

      private void ExitClicked(object sender, EventArgs e) {
         ViewModel.CloseAll.Execute();
         if (ViewModel.Count == 0) Close();
      }

      private void WikiClick(object sender, EventArgs e) => Process.Start("https://github.com/haven1433/HexManiacAdvance/wiki");
      private void TutorialsClick(object sender, RoutedEventArgs e) => Process.Start("https://github.com/haven1433/HexManiacAdvance/wiki/Tutorials");
      private void ReportIssueClick(object sender, EventArgs e) => Process.Start("https://github.com/haven1433/HexManiacAdvance/issues");
      private void AboutClick(object sender, EventArgs e) => new AboutWindow(ViewModel.Singletons.MetadataInfo).ShowDialog();

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
         var args = (ExtendedPropertyChangedEventArgs<GotoControlViewModel>)e;
         args.OldValue.MoveFocusToGoto -= FocusGotoBox;
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

      private void AnimateFocusToCorner(object sender, DependencyPropertyChangedEventArgs e) {
         var duration = TimeSpan.FromSeconds(.3);
         var element = (FrameworkElement)sender;
         if (element.Visibility != Visibility.Visible) return;
         element.Arrange(new Rect());

         FocusAnimationElement.Visibility = Visibility.Visible;
         var widthAnimation = new DoubleAnimation(ContentPanel.ActualWidth, element.ActualWidth, duration);
         var heightAnimation = new DoubleAnimation(ContentPanel.ActualHeight, element.ActualHeight, duration);
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

      private void ExecuteAnimation(object sender, ExecutedRoutedEventArgs e) {
         var duration = TimeSpan.FromSeconds(.3);
         var element = (FrameworkElement)e.Parameter;

         var point = element.TranslatePoint(new System.Windows.Point(), ContentPanel);

         FocusAnimationElement.Visibility = Visibility.Visible;
         FocusAnimationElement.RenderTransform = new TranslateTransform();

         var xAnimation = new DoubleAnimation(-ContentPanel.ActualWidth + point.X + element.ActualWidth, duration);
         var yAnimation = new DoubleAnimation(point.Y, duration);
         var widthAnimation = new DoubleAnimation(ContentPanel.ActualWidth, element.ActualWidth, duration);
         var heightAnimation = new DoubleAnimation(ContentPanel.ActualHeight, element.ActualHeight, duration);
         heightAnimation.Completed += (sender1, e1) => {
            FocusAnimationElement.Visibility = Visibility.Collapsed;
            FocusAnimationElement.RenderTransform = null;
         };

         FocusAnimationElement.RenderTransform.BeginAnimation(TranslateTransform.XProperty, xAnimation);
         FocusAnimationElement.RenderTransform.BeginAnimation(TranslateTransform.YProperty, yAnimation);
         FocusAnimationElement.BeginAnimation(WidthProperty, widthAnimation);
         FocusAnimationElement.BeginAnimation(HeightProperty, heightAnimation);
      }
   }
}
