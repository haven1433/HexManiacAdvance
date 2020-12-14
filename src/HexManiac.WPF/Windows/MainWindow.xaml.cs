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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Navigation;
using System.Windows.Threading;

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

         Application.Current.DispatcherUnhandledException += HandleException;
         Loaded += SetupDebugListener;

         FillQuickEditMenu();
      }

      private void SetupDebugListener(object sender, RoutedEventArgs e) {
         Debug.Listeners.Clear();
         Debug.Listeners.Add(new CustomTraceListener(FileSystem));
         Loaded -= SetupDebugListener;
      }

      private void HandleException(object sender, DispatcherUnhandledExceptionEventArgs e) {
         var text = new StringBuilder();
         text.AppendLine("Version Number:" + ViewModel.Singletons.MetadataInfo.VersionNumber);
#if DEBUG
         text.AppendLine("Debug Version");
#else
         text.AppendLine("Release Version");
#endif
         text.AppendLine(e.Exception.GetType().ToString());
         text.AppendLine(e.Exception.Message);
         text.AppendLine(e.Exception.StackTrace);
         text.AppendLine("-------------------------------------------");
         text.AppendLine(Environment.NewLine);
         File.AppendAllText("crash.log", text.ToString());
         var exceptionTypeShortName = e.Exception.GetType().ToString().Split('.').Last().Split("Exception").First();
         FileSystem.ShowCustomMessageBox(
            "An unhandled error occured. Please report it on Discord or open an issue on GitHub." + Environment.NewLine +
            Title + " might be in a bad state. You should close as soon as possible." + Environment.NewLine +
            "Here's a summary of the issue:" + Environment.NewLine +
            Environment.NewLine +
            exceptionTypeShortName + ":" + Environment.NewLine +
            $"{e.Exception.Message}" + Environment.NewLine +
            Environment.NewLine +
            "The error has been logged to crash.log", showYesNoCancel: false, processButtonText: "Show crash.log in Explorer", processContent: ".");
         e.Handled = true;
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

      protected override void OnActivated(EventArgs e) {
         base.OnActivated(e);
         if (ViewModel.GotoViewModel.ControlVisible) FocusGotoBox();
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

      private void UpdateClick(object sender, EventArgs e) => Process.Start(App.ReleaseUrl);
      private void WikiClick(object sender, EventArgs e) => Process.Start("https://github.com/haven1433/HexManiacAdvance/wiki");
      private void TutorialsClick(object sender, RoutedEventArgs e) => Process.Start("https://github.com/haven1433/HexManiacAdvance/wiki/Tutorials");
      private void ReportIssueClick(object sender, EventArgs e) => Process.Start("https://github.com/haven1433/HexManiacAdvance/issues");
      private void DiscordClick(object sender, EventArgs e) => Process.Start("https://discord.gg/x9eQuBg");
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

      private void FocusGotoBox(object sender = default, EventArgs e = default) {
         FocusTextBox(GotoBox);
      }

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
         var element = (FrameworkElement)sender;
         if (element.Visibility != Visibility.Visible) {
            if (element == GotoPanel) Tabs.Effect = null;
            return;
         } else if (element == GotoPanel) {
            var effect = new BlurEffect { Radius = 0 };
            Tabs.Effect = effect;
            effect.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(5, fastTime));
            GotoBackground.BeginAnimation(FrameworkElement.OpacityProperty, new DoubleAnimation(0, .7, fastTime));
         }
         element.Arrange(new Rect());

         if (element == GotoPanel) {
            NavigationCommands.NavigateJournal.Execute(GotoBox, this);
         } else {
            NavigationCommands.NavigateJournal.Execute(element, this);
         }
      }

      private void ShowThemeSelector(object sender, RoutedEventArgs e) {
         if (themeWindow?.IsVisible != true) {
            themeWindow = new ThemeSelector { DataContext = ViewModel.Theme };
            themeWindow.Show();
         }
      }

      private void ExecuteAnimation(object sender, ExecutedRoutedEventArgs e) {
         if (!IsActive) return;
         var element = (FrameworkElement)e.Parameter;
         Dispatcher.BeginInvoke((Action<FrameworkElement>)DispatchAnimation, DispatcherPriority.ApplicationIdle, element);
      }

      private static readonly Duration fastTime = TimeSpan.FromSeconds(.3);
      private void DispatchAnimation(FrameworkElement element) {
         var point = element.TranslatePoint(new System.Windows.Point(), ContentPanel);

         FocusAnimationElement.Visibility = Visibility.Visible;
         FocusAnimationElement.RenderTransform = new TranslateTransform();

         var xAnimation = new DoubleAnimation(-ContentPanel.ActualWidth + point.X + element.ActualWidth, fastTime);
         var yAnimation = new DoubleAnimation(point.Y, fastTime);
         var widthAnimation = new DoubleAnimation(ContentPanel.ActualWidth, element.ActualWidth, fastTime);
         var heightAnimation = new DoubleAnimation(ContentPanel.ActualHeight, element.ActualHeight, fastTime);
         heightAnimation.Completed += (sender1, e1) => {
            FocusAnimationElement.Visibility = Visibility.Collapsed;
            FocusAnimationElement.RenderTransform = null;
         };

         FocusAnimationElement.RenderTransform.BeginAnimation(TranslateTransform.XProperty, xAnimation);
         FocusAnimationElement.RenderTransform.BeginAnimation(TranslateTransform.YProperty, yAnimation);
         FocusAnimationElement.BeginAnimation(WidthProperty, widthAnimation);
         FocusAnimationElement.BeginAnimation(HeightProperty, heightAnimation);
      }

      private void ShowElementPopup(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         ((ToolTip)element.ToolTip).IsOpen = true;
      }

      private void HideElementPopup(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         ((ToolTip)element.ToolTip).IsOpen = false;
      }

      #region Developer Utilities

      private void DeveloperRaiseAssert(object sender, RoutedEventArgs e) {
         Debug.Assert(false, "Intentional Assert");
      }

      private void DeveloperThrowArgumentOutOfRangeException(object sender, RoutedEventArgs e) {
         var list = new List<int>();
         var number = list[13];
      }

      private void DeveloperWriteDebug(object sender, RoutedEventArgs e) => Debug.WriteLine("Debug");

      private void DeveloperWriteTrace(object sender, RoutedEventArgs e) => Trace.WriteLine("Trace");

      #endregion
   }

   public class CustomTraceListener : TraceListener {
      private readonly WindowsFileSystem fileSystem;
      private readonly TraceListener core = new DefaultTraceListener();

      private bool ignoreAssertions;

      public CustomTraceListener(WindowsFileSystem fs) => fileSystem = fs;

      public override void Fail(string message, string detailMessage) {
         if (ignoreAssertions) return;
         if (Debugger.IsAttached) {
            core.Fail(message, detailMessage);
            return;
         }

         int result = 0;

         Application.Current.Dispatcher.Invoke(() => {
            result = fileSystem.ShowOptions(
               "Debug Assert!",
               message + Environment.NewLine + Environment.NewLine + detailMessage,
               null,
               new[] {
                  new VisualOption {
                     Index = 0,
                     Option = "Ignore",
                     ShortDescription = "Don't Show Assertions",
                     Description = "Ignore assertions until the next time the application is opened."
                  },
                  new VisualOption {
                     Index = 1,
                     Option = "Debug",
                     ShortDescription = "Show Full Message",
                     Description = "Bring up the full dialog with debugging options."
                  },
                  new VisualOption {
                     Index = 2,
                     Option = "Continue",
                     ShortDescription = "Ignore This One",
                     Description = "Ignore this assertion, but show this dialog again if there's another."
                  },
               });
         });

         // user hit "Ignore Additional Assertions"
         ignoreAssertions = result == 0;
         if (result == 1) core.Fail(message, detailMessage);
      }

      public override void Write(string message) => core.Write(message);

      public override void WriteLine(string message) => core.WriteLine(message);
   }
}
