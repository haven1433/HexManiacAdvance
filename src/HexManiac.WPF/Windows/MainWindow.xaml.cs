using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.WPF.Controls;
using HavenSoft.HexManiac.WPF.Implementations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class MainWindow {
      private readonly List<Action> deferredActions = new();
      private ThemeSelector themeWindow;

      public EditorViewModel ViewModel { get; }
      public WindowsFileSystem FileSystem => (WindowsFileSystem)Resources["FileSystem"];

      public MainWindow(EditorViewModel viewModel) {
         InitializeComponent();
         ViewModel = viewModel;
         Title += $" ({viewModel.Singletons.MetadataInfo.VersionNumber})";
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
         Trace.Listeners.Clear();
         Trace.Listeners.Add(new CustomTraceListener(FileSystem, ViewModel.Singletons.MetadataInfo.VersionNumber));
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
         text.AppendLine(DateTime.Now.ToString());
         text.AppendLine("General Information:");
         AppendGeneralAppInfo(text);
         text.AppendLine("Exception Information:");
         AppendException(text, e.Exception);
         text.AppendLine("-------------------------------------------");
         text.AppendLine(Environment.NewLine);
         File.AppendAllText("crash.log", text.ToString());
         var shortError = Environment.NewLine.Join(text.ToString().SplitLines().Take(20));
         shortError = Environment.NewLine.Join(new[] {
            $"~I got a crash! ({ViewModel.Singletons.MetadataInfo.VersionNumber})",
            "```",
            shortError + "...",
            "```",
            "Let me tell you what I was doing right before I got the crash:",
         });
         var exceptionInfo = ExtractExceptionInfo(e.Exception);
         FileSystem.ShowCustomMessageBox(
            "An unhandled error occured. Please report it on Discord or open an issue on GitHub." + Environment.NewLine +
            Title + " might be in a bad state. You should close as soon as possible." + Environment.NewLine +
            "Here's a summary of the issue:" + Environment.NewLine +
            Environment.NewLine +
            exceptionInfo + Environment.NewLine +
            "The error has been logged to crash.log" + Environment.NewLine +
            "You may want to:",
            showYesNoCancel: false,
            new ProcessModel("Show crash.log in Explorer", "."),
            new ProcessModel("Report this via Discord", "https://discord.gg/Re6E6ePpFc"),
            new ProcessModel(
               "Report this via GitHub",
               "https://github.com/haven1433/HexManiacAdvance/issues/new?body=" + HttpUtility.UrlEncode(
                  "*(Please replace this section with notes about what you were doing or how to reproduce)*" + Environment.NewLine +
                  Environment.NewLine + Environment.NewLine + Environment.NewLine + Environment.NewLine +
                  "Notes from crash.log: " + Environment.NewLine + Environment.NewLine +
                  "    " + text.ToString().Replace(Environment.NewLine, Environment.NewLine + "    ")
               )
            ),
            new ProcessModel("Copy a crash message to the clipboard", shortError)
         );
         e.Handled = true;
      }

      private static string ExtractExceptionInfo(Exception ex) {
         var exceptionTypeShortName = ex.GetType().ToString().Split('.').Last().Split("Exception").First();
         var info = exceptionTypeShortName + ":" + Environment.NewLine + $"{ex.Message}" + Environment.NewLine;
         if (ex is AggregateException ag) {
            foreach (var e in ag.InnerExceptions) {
               info += ExtractExceptionInfo(e);
            }
         }
         return info;
      }

      private static void AppendException(StringBuilder text, Exception ex, int indent = 0) {
         var lineStart = new string(' ', indent);

         text.AppendLine(lineStart + ex.GetType().ToString());
         text.AppendLine(lineStart + ex.Message);
         if (ex is ArgumentOutOfRangeException aoore) text.AppendLine(lineStart + aoore.ActualValue?.ToString() ?? "<null>");
         if (ex is AggregateException ae) {
            foreach (var e in ae.InnerExceptions) AppendException(text, e, indent + 2);
         }

         text.AppendLine(ex.StackTrace + Environment.NewLine);
      }

      private void AppendGeneralAppInfo(StringBuilder text) {
         var editor = DataContext as EditorViewModel;
         if (editor == null) {
            text.AppendLine("No Editor ViewModel found.");
            return;
         }
         text.AppendLine("Current tab count: " + editor.Count);
         text.AppendLine("Current selected tab: " + editor.SelectedIndex);
         foreach (var tab in editor) {
            if (tab is IEditableViewPort viewPort) {
               text.AppendLine("Tab is ViewPort for " + Path.GetFileName(viewPort.FileName));
               text.AppendLine("Game Code: " + viewPort.Model.GetGameCode());
               text.AppendLine("Data Length: 0x" + viewPort.Model.Count.ToAddress());
               text.AppendLine("Pokemon Count: " + (viewPort.Model.GetTable(HardcodeTablesModel.PokemonNameTable)?.ElementCount ?? 0));
               text.AppendLine("---");
            } else {
               text.AppendLine($"Tab is {tab.GetType()}");
               text.AppendLine(tab.Name);
               text.AppendLine("---");
            }
         }
      }

      private void FillQuickEditMenu() {
         foreach (var edit in ViewModel.QuickEditsPokedex) {
            var command = CreateQuickEditCommand(edit);
            QuickEditsPokedex.Items.Add(new MenuItem {
               Header = edit.Name,
               Command = command,
            });
         }

         foreach (var edit in ViewModel.QuickEditsExpansion) {
            var command = CreateQuickEditCommand(edit);
            QuickEditsExpansion.Items.Add(new MenuItem {
               Header = edit.Name,
               Command = command,
            });
         }

         foreach (var edit in ViewModel.QuickEditsMisc) {
            var command = CreateQuickEditCommand(edit);
            QuickEditsMisc.Items.Add(new MenuItem {
               Header = edit.Name,
               Command = command
            });
         }
         ((RomOverview)ViewModel.QuickEditsMisc[0]).EditSelected += (sender, e) => DeveloperRenderRomOverview();
      }

      private ICommand CreateQuickEditCommand(IQuickEditItem edit) {
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

         return command;
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         NativeProcess.Start(e.Uri.AbsoluteUri);
         e.Handled = true;
      }

      protected override void OnDragEnter(DragEventArgs e) {
         base.OnDragEnter(e);
         if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (ViewModel.SelectedTab is not IEditableViewPort) return;
            foreach (var fileName in files) {
               if (fileName.ToLower().EndsWith(".ips")) {
                  ViewModel.OverlayText = "Apply IPS Patch";
               } else if (fileName.ToLower().EndsWith(".ups")) {
                  ViewModel.OverlayText = "Apply UPS Patch";
               } else if (fileName.ToLower().EndsWith(".hma")) {
                  var lines = File.ReadLines(fileName)
                     .Until(string.IsNullOrEmpty)
                     .Select(line => line.Substring(1));
                  ViewModel.OverlayText = Environment.NewLine.Join(lines);
               } else {
                  continue;
               }
               ViewModel.ShowOverlayText = true;
               BlurTabs();
            }
         }
      }

      protected override void OnDrop(DragEventArgs e) {
         base.OnDrop(e);

         if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var fileName in files) {
               var loadedFile = FileSystem.LoadFile(fileName);
               ViewModel.Open.Execute(loadedFile);
            }
         }

         ViewModel.ShowOverlayText = false;
         UnblurTabs();
      }

      protected override void OnDragLeave(DragEventArgs e) {
         base.OnDragLeave(e);
         ViewModel.ShowOverlayText = false;
         UnblurTabs();
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

      private void UpdateClick(object sender, EventArgs e) => NativeProcess.Start(App.ReleaseUrl);
      private void WikiClick(object sender, EventArgs e) => NativeProcess.Start("https://github.com/haven1433/HexManiacAdvance/wiki");
      private void TutorialsClick(object sender, RoutedEventArgs e) => NativeProcess.Start("https://github.com/haven1433/HexManiacAdvance/wiki/Tutorials");
      private void ReportIssueClick(object sender, EventArgs e) => NativeProcess.Start("https://github.com/haven1433/HexManiacAdvance/issues");
      private void DiscordClick(object sender, EventArgs e) => NativeProcess.Start("https://discord.gg/x9eQuBg");
      private void AboutClick(object sender, EventArgs e) => new AboutWindow(ViewModel.Singletons.MetadataInfo).ShowDialog();

      private void EditBoxVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) {
         var box = sender as TextBox ?? ((AngleTextBox)sender).GetTextBox();
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
         if (!GotoBox.GetTextBox().IsKeyboardFocused) FocusTextBox(GotoBox.GetTextBox());
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
            if (element == GotoPanel) UnblurTabs();
            return;
         } else if (element == GotoPanel) {
            BlurTabs();
         }
         element.Arrange(new Rect());

         if (element == GotoPanel) {
            NavigationCommands.NavigateJournal.Execute(GotoBox, this);
         } else {
            NavigationCommands.NavigateJournal.Execute(element, this);
         }
      }

      private void BlurTabs() {
         if (Tabs.Effect != null) return;
         var effect = new BlurEffect { Radius = 0 };
         Tabs.Effect = effect;
         effect.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(5, fastTime));
         GotoBackground.BeginAnimation(OpacityProperty, new DoubleAnimation(0, .7, fastTime));
      }

      private void UnblurTabs() {
         if (ViewModel.GotoViewModel.ControlVisible) return;
         Tabs.Effect = null;
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
         if (element == GotoBox && !ViewModel.GotoViewModel.ShowAll) return;
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

      private void DeveloperThrowAggregateException(object sender, RoutedEventArgs e) {
         var task = Task.Factory.StartNew(() => throw new NotImplementedException());
         task.Wait();
      }

      private void DeveloperWriteDebug(object sender, RoutedEventArgs e) => Debug.WriteLine("Debug");

      private void DeveloperWriteTrace(object sender, RoutedEventArgs e) => Trace.WriteLine("Trace");

      private void DeveloperRenderRomOverview() {
         var tab = (ViewPort)ViewModel.SelectedTab;
         var model = tab.Model;
         int BlockSize = 64, BlockWidth = 16, BlockHeight = 16, BytesPerPixel = 16;
         if (model.Count == 0x2000000) {
            BlockWidth = 32;
         }
         var imageWidth = BlockSize * BlockWidth + (BlockWidth - 1);
         var imageHeight = BlockSize * BlockHeight + (BlockHeight - 1);
         var imageData = new int[imageWidth * imageHeight];
         var backlight = Color(nameof(Theme.Backlight));
         var accent = Color(nameof(Theme.Accent));
         var secondary = Color(nameof(Theme.Secondary));
         var data1 = Color(nameof(Theme.Data1));
         var data2 = Color(nameof(Theme.Data2));
         var text2 = Color(nameof(Theme.Text2));

         Parallel.For(0, BlockWidth * BlockHeight, i => {
            var blockXStart = (BlockSize + 1) * (i % BlockWidth);
            var blockYStart = (BlockSize + 1) * (i / BlockWidth);
            var blockStart = blockYStart * imageWidth + blockXStart;
            for (int j = 0; j < BlockSize * BlockSize * BytesPerPixel; j += BytesPerPixel) {
               var blockOffsetX = (j / BytesPerPixel) % BlockSize;
               var blockOffsetY = (j / BytesPerPixel) / BlockSize;
               var blockOffset = blockOffsetY * imageWidth + blockOffsetX;
               var pixelIndex = blockStart + blockOffset;
               var address = (i * BlockSize * BlockSize * BytesPerPixel) + j;
               if (address < 0 || address >= model.Count) break;
               var run = model.GetNextRun(address);
               if (model[address] == 0xFF) {
                  imageData[pixelIndex] = backlight;
               } else if (run.Start > address || run is NoInfoRun) {
                  imageData[pixelIndex] = secondary;
               } else if (run is PointerRun) {
                  imageData[pixelIndex] = accent;
               } else if (run is ITableRun tableRun0 && tableRun0.ElementContent[tableRun0.ConvertByteOffsetToArrayOffset(address).SegmentIndex].Type == ElementContentType.Pointer) {
                  imageData[pixelIndex] = accent;
               } else if (run is ISpriteRun || run is IPaletteRun) {
                  imageData[pixelIndex] = data2;
               } else if (run is PCSRun || run is AsciiRun) {
                  imageData[pixelIndex] = text2;
               } else {
                  imageData[pixelIndex] = data1;
               }
            }
         });

         var source = BitmapSource.Create(imageWidth, imageHeight, 96, 96, PixelFormats.Bgra32, null, imageData, imageWidth * 4);
         var window = new Window {
            Title = tab.Name,
            Background = (Brush)Application.Current.Resources.MergedDictionaries[0][nameof(Theme.Background)],
            Content = new Image { Source = source, Width = imageWidth * .9 },
            SizeToContent = SizeToContent.WidthAndHeight,
         };
         window.Show();
      }

      private void DeveloperReloadMetadata(object sender, EventArgs e) {
         var tab = (ViewPort)ViewModel.SelectedTab;
         tab.ConsiderReload(FileSystem);
      }

      private void DeveloperOpenMapEditor(object sender, EventArgs e) {
         if (!ViewModel.ShowDeveloperMenu) return;
         if (ViewModel.SelectedTab is ViewPort viewPort) {
            var newTab = new MapEditorViewModel(viewPort.Model, viewPort.ChangeHistory, ViewModel.Singletons);
            ViewModel.Add(newTab);
         }
      }

      private static int Color(string name) {
         var color = ((SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name]).Color;
         return (color.A << 24) + (color.R << 16) + (color.G << 8) + color.B;
      }

      #endregion

      private void AcknowledgeAccentItem(object sender, MouseEventArgs e) {
         var property = (string)((FrameworkElement)sender).Tag;
         ViewModel.GetType().GetProperty(property).SetValue(ViewModel, true);
      }

      private void FillGotoBox(object sender, MouseButtonEventArgs e) {
         var source = (TextBlock)sender;
         var textbox = GotoBox.GetTextBox();
         textbox.Text = source.Text;
         textbox.SelectAll();
         textbox.Focus();
         e.Handled = true;
      }

      private void SetPythonPanelWidth(object sender, RoutedEventArgs e) {
         TabContainer.ColumnDefinitions[2].Width = new GridLength(300);
      }
   }

   public class CustomTraceListener : TraceListener {
      private readonly WindowsFileSystem fileSystem;
      private readonly TraceListener core = new DefaultTraceListener();

      private bool ignoreAssertions;
      private readonly string versionNumber;

      public CustomTraceListener(WindowsFileSystem fs, string version) {
         fileSystem = fs;
         versionNumber = $" Version ({version})";
      }

      public override void Fail(string message, string detailMessage) {
         if (ignoreAssertions) return;
         if (Debugger.IsAttached) {
            core.Fail(message, detailMessage);
            return;
         }

         int result = 0;

         Application.Current.Dispatcher.Invoke(() => {
            result = fileSystem.ShowOptions(
               "Debug Assert!" + versionNumber,
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
         while (result == 1) {
            if (Debugger.IsAttached) {
               Debugger.Break();
               break;
            } else {
               Application.Current.Dispatcher.Invoke(() => {
                  result = fileSystem.ShowOptions(
                     "Attach a Debugger",
                     "Attach a debugger and click 'Debug' to get more information about the following assertion:" + Environment.NewLine +
                     message + Environment.NewLine +
                     detailMessage + Environment.NewLine +
                     "Stack Trace:" + Environment.NewLine +
                     Environment.StackTrace,
                     null,
                        new[] {
                           new VisualOption {
                              Index = 1,
                              Option = "Debug",
                              ShortDescription = "Show in Debugger",
                              Description = "Break in a connected debugger"
                           },
                        }
                     );
               });
            }
         }
      }

      public override void Write(string message) => core.Write(message);

      public override void WriteLine(string message) => core.WriteLine(message);
   }
}
