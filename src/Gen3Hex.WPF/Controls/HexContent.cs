using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using HavenSoft.Gen3Hex.WPF.Implementations;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.Gen3Hex.WPF.Controls {
   public class HexContent : FrameworkElement {
      public const double CellWidth = 30, CellHeight = 20;

      public static readonly Rect CellRect = new Rect(0, 0, CellWidth, CellHeight);

      private Core.Models.Point downPoint;

      #region ViewPort

      public IViewPort ViewPort {
         get { return (IViewPort)GetValue(ViewPortProperty); }
         set { SetValue(ViewPortProperty, value); }
      }

      public static readonly DependencyProperty ViewPortProperty = DependencyProperty.Register(nameof(ViewPort), typeof(IViewPort), typeof(HexContent), new FrameworkPropertyMetadata(null, ViewPortChanged));

      private static void ViewPortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnViewPortChanged(e);
      }

      private void OnViewPortChanged(DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is IViewPort oldViewPort) {
            oldViewPort.CollectionChanged -= OnViewPortContentChanged;
            oldViewPort.PropertyChanged -= OnViewPortPropertyChanged;
         }

         if (e.NewValue is IViewPort newViewPort) {
            newViewPort.CollectionChanged += OnViewPortContentChanged;
            newViewPort.PropertyChanged += OnViewPortPropertyChanged;
            UpdateViewPortSize();
         }

         InvalidateVisual();
      }

      private void OnViewPortContentChanged(object sender, NotifyCollectionChangedEventArgs e) {
         InvalidateVisual();
      }

      private void OnViewPortPropertyChanged(object sender, PropertyChangedEventArgs e) {
         var propertyChangesThatRequireRedraw = new[] {
            nameof(Core.ViewModels.ViewPort.SelectionStart),
            nameof(Core.ViewModels.ViewPort.SelectionEnd),
         };

         if (propertyChangesThatRequireRedraw.Contains(e.PropertyName)) {
            InvalidateVisual();
         }
      }

      #endregion

      public HexContent() {
         ClipToBounds = true;
         Focusable = true;

         void AddKeyCommand(string commandPath, object arg, Key key, ModifierKeys modifiers = ModifierKeys.None) {
            var keyBinding = new KeyBinding { CommandParameter = arg, Key = key, Modifiers = modifiers };
            BindingOperations.SetBinding(keyBinding, InputBinding.CommandProperty, new Binding(commandPath));
            InputBindings.Add(keyBinding);
         }

         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Up, Key.Up);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Down, Key.Down);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Left, Key.Left);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Right, Key.Right);

         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Up, Key.Up, ModifierKeys.Shift);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Down, Key.Down, ModifierKeys.Shift);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Left, Key.Left, ModifierKeys.Shift);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Right, Key.Right, ModifierKeys.Shift);

         AddKeyCommand(nameof(IViewPort.Scroll), Direction.Up, Key.Up, ModifierKeys.Control);
         AddKeyCommand(nameof(IViewPort.Scroll), Direction.Down, Key.Down, ModifierKeys.Control);
         AddKeyCommand(nameof(IViewPort.Scroll), Direction.Left, Key.Left, ModifierKeys.Control);
         AddKeyCommand(nameof(IViewPort.Scroll), Direction.Right, Key.Right, ModifierKeys.Control);

         AddKeyCommand(nameof(IViewPort.Undo), null, Key.Z, ModifierKeys.Control);
         AddKeyCommand(nameof(IViewPort.Redo), null, Key.Y, ModifierKeys.Control);

         void AddConsoleKeyCommand(Key key, ConsoleKey consoleKey) {
            InputBindings.Add(new KeyBinding {
               Key = key,
               Command = new StubCommand {
                  CanExecute = ICommandExtensions.CanAlwaysExecute,
                  Execute = arg => (ViewPort as ViewPort)?.Edit(consoleKey)
               }
            });
         }

         AddConsoleKeyCommand(Key.Back, ConsoleKey.Backspace);
         AddConsoleKeyCommand(Key.Escape, ConsoleKey.Escape);
      }

      protected override void OnMouseDown(MouseButtonEventArgs e) {
         base.OnMouseDown(e);
         if (e.ChangedButton == MouseButton.XButton1 && ViewPort.Back.CanExecute(null)) {
            ViewPort.Back.Execute();
            return;
         }
         if (e.ChangedButton == MouseButton.XButton2 && ViewPort.Forward.CanExecute(null)) {
            ViewPort.Forward.Execute();
            return;
         }
         downPoint = ControlCoordinatesToModelCoordinates(e);
         if (e.ChangedButton != MouseButton.Left) return;
         Focus();
         if (Keyboard.Modifiers == ModifierKeys.Control) {
            ViewPort.FollowLink(downPoint.X, downPoint.Y);
            return;
         }
         if (e.ClickCount == 2) {
            ViewPort.ExpandSelection();
            return;
         }

         if (ViewPort is ViewPort editableViewPort) {
            if (Keyboard.Modifiers == ModifierKeys.Shift) {
               editableViewPort.SelectionEnd = downPoint;
            } else {
               editableViewPort.SelectionStart = downPoint;
            }
            CaptureMouse();
         }
      }

      protected override void OnMouseMove(MouseEventArgs e) {
         base.OnMouseMove(e);
         if (!IsMouseCaptured) return;

         ((ViewPort)ViewPort).SelectionEnd = ControlCoordinatesToModelCoordinates(e);
      }

      protected override void OnMouseUp(MouseButtonEventArgs e) {
         base.OnMouseUp(e);
         if (e.ChangedButton == MouseButton.Right && e.LeftButton == MouseButtonState.Released && !IsMouseCaptured) {
            var p = ControlCoordinatesToModelCoordinates(e);
            var children = new List<FrameworkElement>();
            var format = ViewPort[p.X, p.Y].Format;

            if (ViewPort is ViewPort editableViewPort) {
               if (format is Anchor && p.Equals(downPoint)) {
                  children.AddRange(GetAnchorChildren(p));
                  format = ((Anchor)format).OriginalFormat;
               }
               if (format is PCS pcs) children.AddRange(GetStringChildren(p));
               if (format is Pointer pointer) children.AddRange(GetPointerChildren(p));
            } else {
               children.AddRange(GetSearchChildren(p));
            }

            ShowMenu(children);
            return;
         }
         if (!IsMouseCaptured) return;
         ReleaseMouseCapture();
      }

      protected override void OnMouseWheel(MouseWheelEventArgs e) {
         base.OnMouseWheel(e);
         ViewPort.ScrollValue -= Math.Sign(e.Delta);
      }

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);
         if (ViewPort == null) return;
         drawingContext.DrawRectangle(Solarized.Theme.Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

         var visitor = new FormatDrawer(drawingContext);

         // first pass: draw selection
         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               if (ViewPort.IsSelected(new Core.Models.Point(x, y))) {
                  var element = ViewPort[x, y];
                  drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));
                  drawingContext.DrawRectangle(Solarized.Theme.Backlight, null, CellRect);
                  drawingContext.Pop();
               }
            }
         }

         // second pass: draw data
         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               var element = ViewPort[x, y];
               drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));
               element.Format.Visit(visitor, element.Value);
               drawingContext.Pop();
            }
         }
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         if (ViewPort != null) UpdateViewPortSize();
      }

      protected override void OnTextInput(TextCompositionEventArgs e) {
         if (ViewPort is ViewPort editableViewPort) {
            editableViewPort.Edit(e.Text);
            e.Handled = true;
         }
      }

      Popup recentMenu;

      private IEnumerable<FrameworkElement> GetAnchorChildren(Core.Models.Point p) {
         var anchor = (Anchor)ViewPort[p.X, p.Y].Format;

         if (!string.IsNullOrEmpty(anchor.Name)) {
            yield return new TextBlock {
               HorizontalAlignment = HorizontalAlignment.Center,
               Text = anchor.Name,
               Margin = new Thickness(0, 0, 0, 10),
            };
         };

         if (anchor.Sources.Count == 0) {
            yield return new TextBlock {
               HorizontalAlignment = HorizontalAlignment.Center,
               Foreground = Solarized.Theme.Secondary,
               FontStyle = FontStyles.Italic,
               Text = "(Nothing points to this.)",
               Margin = new Thickness(0, 0, 0, 5),
            };
         }

         if (anchor.Sources.Count > 1) {
            yield return new Button {
               Content = "Show All Sources in new tab"
            }.SetEvent(ButtonBase.ClickEvent, (sender, e) => {
               ViewPort.FindAllSources(p.X, p.Y);
               recentMenu.IsOpen = false;
            });
         }

         if (anchor.Sources.Count < 5) {
            for (int i = 0; i < anchor.Sources.Count; i++) {
               var source = anchor.Sources[i].ToString("X6");
               yield return new Button {
                  Content = source,
               }.SetEvent(ButtonBase.ClickEvent, (sender, e) => {
                  ViewPort.Goto.Execute(source);
                  recentMenu.IsOpen = false;
               });
            }
         } else {
            yield return new ListBox {
               MaxHeight = 120,
               ItemsSource = anchor.Sources.Select(source => source.ToString("X6")).ToList(),
            }.SetEvent(Selector.SelectionChangedEvent, (sender, e) => {
               var source = anchor.Sources[((ListBox)sender).SelectedIndex].ToString("X6");
               ViewPort.Goto.Execute(source);
               recentMenu.IsOpen = false;
            });
         }
      }

      private IEnumerable<FrameworkElement> GetStringChildren(Core.Models.Point p) {
         yield return CreateFollowLinkButton("Open In String Tool", p);
      }

      private IEnumerable<FrameworkElement> GetPointerChildren(Core.Models.Point p) {
         yield return CreateFollowLinkButton("Follow Pointer", p);
      }

      private IEnumerable<FrameworkElement> GetSearchChildren(Core.Models.Point p) {
         yield return CreateFollowLinkButton("Open in main tab", p);
      }

      private Button CreateFollowLinkButton(string message, Core.Models.Point p) {
         return new Button {
            Content = new StackPanel {
               Orientation = Orientation.Horizontal,
               Children = {
                  new TextBlock { Text = message },
                  new TextBlock { Foreground = Solarized.Theme.Secondary, FontStyle = FontStyles.Italic, Margin = new Thickness(20, 0, 0, 0), Text = "Ctrl+Click" }
               }
            },
         }.SetEvent(ButtonBase.ClickEvent, (sender, e) => {
            ViewPort.FollowLink(p.X, p.Y);
            recentMenu.IsOpen = false;
         });
      }

      private void ShowMenu(IList<FrameworkElement> children) {
         if (children.Count == 0) return;

         var panel = new StackPanel { Background = Solarized.Theme.Background, MinWidth = 150 };
         recentMenu = new Popup {
            Placement = PlacementMode.Mouse,
            Child = new Border {
               BorderBrush = Solarized.Brushes.Blue,
               BorderThickness = new Thickness(1),
               Child = panel,
            },
            StaysOpen = false,
         };

         foreach (var child in children) panel.Children.Add(child);

         recentMenu.IsOpen = true;
      }

      private void UpdateViewPortSize() {
         ViewPort.Width = (int)(ActualWidth / CellWidth);
         ViewPort.Height = (int)(ActualHeight / CellHeight);
      }

      private Core.Models.Point ControlCoordinatesToModelCoordinates(MouseEventArgs e) {
         var point = e.GetPosition(this);
         return new Core.Models.Point((int)(point.X / CellWidth), (int)(point.Y / CellHeight));
      }
   }

   public static class FrameworkElementExtensions {
      public static T SetEvent<T>(this T item, RoutedEvent routedEvent, RoutedEventHandler handler) where T : FrameworkElement {
         item.AddHandler(routedEvent, handler);
         return item;
      }
   }
}
