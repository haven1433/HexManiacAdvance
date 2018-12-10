using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.WPF.Implementations;
using System;
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
            var keyBinding = new KeyBinding {
               CommandParameter = arg,
               Key = key,
               Modifiers = modifiers,
            };
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

         InputBindings.Add(new KeyBinding {
            Key = Key.Back,
            Command = new StubCommand {
               CanExecute = ICommandExtensions.CanAlwaysExecute,
               Execute = arg => (ViewPort as ViewPort).Edit(ConsoleKey.Backspace)
            }
         });
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
         if (e.ChangedButton != MouseButton.Left) return;
         var p = ControlCoordinatesToModelCoordinates(e);
         Focus();
         if (e.ClickCount == 2) {
            ViewPort.FollowLink(p.X, p.Y);
            return;
         }

         if (ViewPort is ViewPort editableViewPort) {
            if (Keyboard.Modifiers == ModifierKeys.Shift) {
               editableViewPort.SelectionEnd = p;
            } else {
               editableViewPort.SelectionStart = p;
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
            if (ViewPort[p.X, p.Y].Format is Core.ViewModels.DataFormats.Anchor) ShowAnchorMenu(p);
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

      private void ShowAnchorMenu(Core.Models.Point p) {
         var anchor = (Core.ViewModels.DataFormats.Anchor)ViewPort[p.X, p.Y].Format;

         var panel = new StackPanel { Background = Solarized.Theme.Background, MinWidth = 150 };
         var menu = new Popup {
            Placement = PlacementMode.Mouse,
            Child = new Border {
               BorderBrush = Solarized.Brushes.Blue,
               BorderThickness = new Thickness(1),
               Child = panel,
            },
            StaysOpen = false,
         };

         if (!string.IsNullOrEmpty(anchor.Name)) {
            panel.Children.Add(new TextBlock {
               HorizontalAlignment = HorizontalAlignment.Center,
               Text = anchor.Name,
               Margin = new Thickness(0, 0, 0, 10),
            });
         };

         if (anchor.Sources.Count == 0) {
            panel.Children.Add(new TextBlock {
               HorizontalAlignment = HorizontalAlignment.Center,
               Foreground = Solarized.Theme.Secondary,
               FontStyle = FontStyles.Italic,
               Text = "(Nothing points to this.)",
               Margin = new Thickness(0, 0, 0, 5),
            });
         }

         if (anchor.Sources.Count > 1) {
            panel.Children.Add(new Button {
               Content = "Show All Sources in new tab"
            }.SetEvent(ButtonBase.ClickEvent, (sender, e) => {
               ViewPort.FindAllSources(p.X, p.Y);
               menu.IsOpen = false;
            }));
         }

         if (anchor.Sources.Count < 5) {
            for (int i = 0; i < anchor.Sources.Count; i++) {
               var source = anchor.Sources[i].ToString("X6");
               panel.Children.Add(new Button {
                  Content = source,
               }.SetEvent(ButtonBase.ClickEvent, (sender, e) => {
                  ViewPort.Goto.Execute(source);
                  menu.IsOpen = false;
               }));
            }
         } else {
            panel.Children.Add(new ListBox {
               MaxHeight = 120,
               ItemsSource = anchor.Sources.Select(source => source.ToString("X6")).ToList(),
            }.SetEvent(Selector.SelectionChangedEvent, (sender, e) => {
               var source = anchor.Sources[((ListBox)sender).SelectedIndex].ToString("X6");
               ViewPort.Goto.Execute(source);
               menu.IsOpen = false;
            }));
         }

         menu.IsOpen = true;
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
