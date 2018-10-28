using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.Gen3Hex.View {
   public class HexContent : FrameworkElement {
      public const int CellWidth = 30, CellHeight = 20;

      public static readonly Rect CellRect = new Rect(0, 0, CellWidth, CellHeight);

      #region ViewPort

      public ViewPort ViewPort {
         get { return (ViewPort)GetValue(ViewPortProperty); }
         set { SetValue(ViewPortProperty, value); }
      }

      public static readonly DependencyProperty ViewPortProperty = DependencyProperty.Register("ViewPort", typeof(ViewPort), typeof(HexContent), new FrameworkPropertyMetadata(null, ViewPortChanged));

      private static void ViewPortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnViewPortChanged(e);
      }

      private void OnViewPortChanged(DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is ViewPort oldViewPort) {
            oldViewPort.CollectionChanged -= OnViewPortContentChanged;
            oldViewPort.PropertyChanged -= OnViewPortPropertyChanged;
         }

         if (e.NewValue is ViewPort newViewPort) {
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
            nameof(ViewPort.SelectionStart),
            nameof(ViewPort.SelectionEnd),
         };

         if (propertyChangesThatRequireRedraw.Contains(e.PropertyName)) {
            InvalidateVisual();
         }
      }

      #endregion

      public HexContent() {
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

         AddKeyCommand(nameof(ViewPort.MoveSelectionStart), Direction.Up, Key.Up);
         AddKeyCommand(nameof(ViewPort.MoveSelectionStart), Direction.Down, Key.Down);
         AddKeyCommand(nameof(ViewPort.MoveSelectionStart), Direction.Left, Key.Left);
         AddKeyCommand(nameof(ViewPort.MoveSelectionStart), Direction.Right, Key.Right);

         AddKeyCommand(nameof(ViewPort.MoveSelectionEnd), Direction.Up, Key.Up, ModifierKeys.Shift);
         AddKeyCommand(nameof(ViewPort.MoveSelectionEnd), Direction.Down, Key.Down, ModifierKeys.Shift);
         AddKeyCommand(nameof(ViewPort.MoveSelectionEnd), Direction.Left, Key.Left, ModifierKeys.Shift);
         AddKeyCommand(nameof(ViewPort.MoveSelectionEnd), Direction.Right, Key.Right, ModifierKeys.Shift);

         AddKeyCommand(nameof(ViewPort.Scroll), Direction.Up, Key.Up, ModifierKeys.Control);
         AddKeyCommand(nameof(ViewPort.Scroll), Direction.Down, Key.Down, ModifierKeys.Control);
         AddKeyCommand(nameof(ViewPort.Scroll), Direction.Left, Key.Left, ModifierKeys.Control);
         AddKeyCommand(nameof(ViewPort.Scroll), Direction.Right, Key.Right, ModifierKeys.Control);

         AddKeyCommand(nameof(ViewPort.Undo), null, Key.Z, ModifierKeys.Control);
         AddKeyCommand(nameof(ViewPort.Redo), null, Key.Y, ModifierKeys.Control);
      }

      protected override void OnMouseDown(MouseButtonEventArgs e) {
         base.OnMouseDown(e);
         if (e.LeftButton != MouseButtonState.Pressed) return;
         Focus();

         ViewPort.SelectionStart = ControlCoordinatesToModelCoordinates(e);
         CaptureMouse();
      }

      protected override void OnMouseMove(MouseEventArgs e) {
         base.OnMouseMove(e);
         if (!IsMouseCaptured) return;

         ViewPort.SelectionEnd = ControlCoordinatesToModelCoordinates(e);
      }

      protected override void OnMouseUp(MouseButtonEventArgs e) {
         base.OnMouseUp(e);
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

         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               var element = ViewPort[x, y];
               drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));
               if (ViewPort.IsSelected(new Model.Point(x, y))) {
                  drawingContext.DrawRectangle(Solarized.Theme.Backlight, null, CellRect);
               }
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
         ViewPort.Edit(e.Text);
         e.Handled = true;
      }

      private void UpdateViewPortSize() {
         ViewPort.Width = (int)ActualWidth / CellWidth;
         ViewPort.Height = (int)ActualHeight / CellHeight;
      }

      private Model.Point ControlCoordinatesToModelCoordinates(MouseEventArgs e) {
         var point = e.GetPosition(this);
         return new Model.Point((int)point.X / CellWidth, (int)point.Y / CellHeight);
      }
   }
}
