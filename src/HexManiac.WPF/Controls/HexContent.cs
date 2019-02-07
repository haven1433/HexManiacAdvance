using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.WPF.Implementations;
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
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;
using ScreenPoint = System.Windows.Point;

namespace HavenSoft.HexManiac.WPF.Controls {

   public class HexContent : FrameworkElement {
      public const double CellWidth = 30, CellHeight = 20;

      public static readonly Rect CellRect = new Rect(0, 0, CellWidth, CellHeight);

      public static readonly ScreenPoint
         TopLeft = new ScreenPoint(0, 0),
         TopRight = new ScreenPoint(CellWidth, 0),
         BottomLeft = new ScreenPoint(0, CellHeight),
         BottomRight = new ScreenPoint(CellWidth, CellHeight);

      public static readonly Pen BorderPen = new Pen(Solarized.Brushes.Green, 1);

      private Popup recentMenu;
      private ModelPoint downPoint;
      private ModelPoint mouseOverPoint;

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
            oldViewPort.RequestMenuClose -= OnViewPortRequestMenuClose;
         }

         if (e.NewValue is IViewPort newViewPort) {
            newViewPort.CollectionChanged += OnViewPortContentChanged;
            newViewPort.PropertyChanged += OnViewPortPropertyChanged;
            newViewPort.RequestMenuClose += OnViewPortRequestMenuClose;
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

      private void OnViewPortRequestMenuClose(object sender, EventArgs e) {
         if (recentMenu == null) return;
         recentMenu.IsOpen = false;
      }

      #endregion

      #region ShowGrid

      public bool ShowGrid {
         get { return (bool)GetValue(ShowGridProperty); }
         set { SetValue(ShowGridProperty, value); }
      }

      public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(HexContent), new FrameworkPropertyMetadata(false, ShowGridChanged));

      private static void ShowGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnShowGridChanged(e);
      }

      private void OnShowGridChanged(DependencyPropertyChangedEventArgs e) => InvalidateVisual();

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
         AddConsoleKeyCommand(Key.Enter, ConsoleKey.Enter);
         AddConsoleKeyCommand(Key.Tab, ConsoleKey.Tab);
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
            ViewPort.ExpandSelection(downPoint.X, downPoint.Y);
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
         var newMouseOverPoint = ControlCoordinatesToModelCoordinates(e);
         if (!newMouseOverPoint.Equals(mouseOverPoint)) {
            mouseOverPoint = newMouseOverPoint;
            InvalidateVisual();
         }
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
               if (!(format is None || format is Undefined)) children.AddRange(GetClearFormattingChildren(p));
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
         var visitor = new FormatDrawer(drawingContext, ViewPort.Width, ViewPort.Height);

         RenderGrid(drawingContext);
         RenderSelection(drawingContext);
         RenderData(drawingContext, visitor);
      }

      private void RenderGrid(DrawingContext drawingContext) {
         drawingContext.DrawRectangle(Solarized.Theme.Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
         if (!ShowGrid) return;

         var gridPen = new Pen(Solarized.Theme.Backlight, 1);

         for (int x = 1; x <= ViewPort.Width; x++) {
            drawingContext.DrawLine(gridPen, new ScreenPoint(CellWidth * x, 0), new ScreenPoint(CellWidth * x, CellHeight * ViewPort.Height));
         }

         for (int y = 1; y <= ViewPort.Height; y++) {
            drawingContext.DrawLine(gridPen, new ScreenPoint(0, CellHeight * y), new ScreenPoint(CellWidth * ViewPort.Width, CellHeight * y));
         }
      }

      private void RenderSelection(DrawingContext drawingContext) {
         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               if (!ViewPort.IsSelected(new ModelPoint(x, y))) continue;
               var element = ViewPort[x, y];
               drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));

               drawingContext.DrawRectangle(Solarized.Theme.Backlight, null, CellRect);
               if (!ViewPort.IsSelected(new ModelPoint(x, y - 1))) drawingContext.DrawLine(BorderPen, TopLeft, TopRight);
               if (!ViewPort.IsSelected(new ModelPoint(x, y + 1))) drawingContext.DrawLine(BorderPen, BottomLeft, BottomRight);
               if (!ViewPort.IsSelected(new ModelPoint(x - 1, y))) drawingContext.DrawLine(BorderPen, TopLeft, BottomLeft);
               if (!ViewPort.IsSelected(new ModelPoint(x + 1, y))) drawingContext.DrawLine(BorderPen, TopRight, BottomRight);

               drawingContext.Pop();
            }
         }
      }

      private void RenderData(DrawingContext drawingContext, FormatDrawer visitor) {
         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               visitor.MouseIsOverCurrentFormat = mouseOverPoint.Equals(new ModelPoint(x, y));
               var element = ViewPort[x, y];
               drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));

               visitor.Position = new ModelPoint(x, y);
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

      private IEnumerable<FrameworkElement> GetAnchorChildren(ModelPoint p) {
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

      private IEnumerable<FrameworkElement> GetStringChildren(ModelPoint p) {
         yield return CreateFollowLinkButton("Open In String Tool", p);
      }

      private IEnumerable<FrameworkElement> GetPointerChildren(ModelPoint p) {
         yield return CreateFollowLinkButton("Follow Pointer", p);
      }

      private IEnumerable<FrameworkElement> GetSearchChildren(ModelPoint p) {
         yield return CreateFollowLinkButton("Open in main tab", p);
      }

      private IEnumerable<FrameworkElement> GetClearFormattingChildren(ModelPoint p) {
         yield return new Button {
            Content = new TextBlock { Text = "Clear Format" },
         }.SetEvent(ButtonBase.ClickEvent, (sender, e) => {
            ((ViewPort)ViewPort).ClearFormat(p);
            recentMenu.IsOpen = false;
         });
      }

      private Button CreateFollowLinkButton(string message, ModelPoint p) {
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

      private ModelPoint ControlCoordinatesToModelCoordinates(MouseEventArgs e) {
         var point = e.GetPosition(this);
         point = new System.Windows.Point(Math.Max(0, point.X), Math.Max(0, point.Y)); // out of bounds to the left/top clamps to 0 (useful for headers)
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
