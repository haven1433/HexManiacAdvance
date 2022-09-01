using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using HavenSoft.HexManiac.WPF.Implementations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;
using ScreenPoint = System.Windows.Point;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class HexContent : FrameworkElement {
      public static readonly Pen BorderPen = new Pen(Brush(nameof(Theme.Stream2)), 1);

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
            oldViewPort.Headers.CollectionChanged -= OnViewPortContentChanged;
         }

         if (e.NewValue is IViewPort newViewPort) {
            newViewPort.CollectionChanged += OnViewPortContentChanged;
            newViewPort.PropertyChanged += OnViewPortPropertyChanged;
            newViewPort.RequestMenuClose += OnViewPortRequestMenuClose;
            newViewPort.Headers.CollectionChanged += OnViewPortContentChanged;
            UpdateViewPortSize();
         }

         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      private void OnViewPortContentChanged(object sender, NotifyCollectionChangedEventArgs e) {
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      private void OnViewPortPropertyChanged(object sender, PropertyChangedEventArgs e) {
         var propertyChangesThatRequireRedraw = new[] {
            nameof(Core.ViewModels.ViewPort.SelectionStart),
            nameof(Core.ViewModels.ViewPort.SelectionEnd),
            nameof(Core.ViewModels.ViewPort.ScrollValue),
            nameof(Core.ViewModels.ViewPort.UpdateInProgress),
         };

         if (propertyChangesThatRequireRedraw.Contains(e.PropertyName)) {
            InvalidateVisual();
            CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
         }

         var propertyChangesThatRequireResize = new[] {
            nameof(IViewPort.StretchData),
            nameof(IViewPort.AllowMultipleElementsPerLine),
            nameof(Core.ViewModels.ViewPort.PreferredWidth),
         };

         if (propertyChangesThatRequireResize.Contains(e.PropertyName)) {
            UpdateViewPortSize();
            InvalidateVisual();
            CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
         }

         if (e.PropertyName == nameof(ViewPort.UpdateInProgress) && !ViewPort.UpdateInProgress) {
            CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
         } else if (e.PropertyName == nameof(IEditableViewPort.SelectionEnd)) {
            MakeSelectionEndOnScreen();
         }
      }

      private void OnViewPortRequestMenuClose(object sender, EventArgs e) {
         if (ContextMenu != null) ContextMenu.IsOpen = false;
         if (recentMenu == null) return;
         recentMenu.IsOpen = false;
      }

      #endregion

      #region CellWidth / Cell Height

      public static readonly DependencyProperty CellWidthProperty = DependencyProperty.Register(nameof(CellWidth), typeof(double), typeof(HexContent), new PropertyMetadata(0.0));

      public double CellWidth {
         get => (double)GetValue(CellWidthProperty);
         set => SetValue(CellWidthProperty, value);
      }

      public static readonly DependencyProperty CellHeightProperty = DependencyProperty.Register(nameof(CellHeight), typeof(double), typeof(HexContent), new PropertyMetadata(0.0));

      public double CellHeight {
         get => (double)GetValue(CellHeightProperty);
         set => SetValue(CellHeightProperty, value);
      }

      #endregion

      public event EventHandler CursorNeedsUpdate;

      #region FontSize

      public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(nameof(FontSize), typeof(int), typeof(HexContent), new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, FontSizeChanged));

      public int FontSize {
         get => (int)GetValue(FontSizeProperty);
         set => SetValue(FontSizeProperty, value);
      }

      private static void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnFontSizeChanged();
      }

      private void OnFontSizeChanged() {
         UpdateViewPortSize();
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      #region ShowGrid

      public bool ShowGrid {
         get { return (bool)GetValue(ShowGridProperty); }
         set { SetValue(ShowGridProperty, value); }
      }

      public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(HexContent), new FrameworkPropertyMetadata(false, RequestInvalidateVisual));

      private static void RequestInvalidateVisual(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnRequestInvalidateVisual();
      }

      private void OnRequestInvalidateVisual() {
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      #region ShowHorizontalScroll

      public bool ShowHorizontalScroll {
         get { return (bool)GetValue(ShowHorizontalScrollProperty); }
         set { SetValue(ShowHorizontalScrollProperty, value); }
      }

      public static readonly DependencyProperty ShowHorizontalScrollProperty = DependencyProperty.Register("ShowHorizontalScroll", typeof(bool), typeof(HexContent), new FrameworkPropertyMetadata(false, RequestInvalidateVisual));

      #endregion

      #region HorizontalScrollValue

      public double HorizontalScrollValue {
         get { return (double)GetValue(HorizontalScrollValueProperty); }
         set { SetValue(HorizontalScrollValueProperty, value); }
      }

      public static readonly DependencyProperty HorizontalScrollValueProperty = DependencyProperty.Register("HorizontalScrollValue", typeof(double), typeof(HexContent), new FrameworkPropertyMetadata(0.0, RequestInvalidateVisual));

      DoubleAnimation lastAnimation;
      double targetValue;
      private void MakeSelectionEndOnScreen() {
         if (!(ViewPort is IEditableViewPort viewPort)) return;
         var x = viewPort.SelectionEnd.X;
         var newDesiredValue = HorizontalScrollValue;
         if (lastAnimation != null) newDesiredValue = targetValue;
         if (HorizontalScrollValue > x * CellWidth) {
            // can't see left edge of cell
            newDesiredValue = x * CellWidth;
         }
         if (newDesiredValue + ActualWidth < (x + 1) * CellWidth) {
            // can't see right edge of cell
            newDesiredValue = (x + 1) * CellWidth - ActualWidth;
         }

         if (newDesiredValue != HorizontalScrollValue || lastAnimation != null) {
            var duration = new Duration(TimeSpan.FromMilliseconds(200));
            var animation = new DoubleAnimation(newDesiredValue, duration) { DecelerationRatio = 1, FillBehavior = FillBehavior.Stop };
            lastAnimation = animation;
            targetValue = newDesiredValue;
            animation.Completed += (sender, e) => {
               if (animation == lastAnimation) {
                  HorizontalScrollValue = newDesiredValue;
                  lastAnimation = null;
               }
            };
            BeginAnimation(HorizontalScrollValueProperty, animation);
         }
      }

      #endregion

      #region HorizontalScrollMaximum

      public double HorizontalScrollMaximum {
         get { return (double)GetValue(HorizontalScrollMaximumProperty); }
         set { SetValue(HorizontalScrollMaximumProperty, value); }
      }

      public IFileSystem FileSystem => (IFileSystem)Application.Current.MainWindow.Resources["FileSystem"];

      public static readonly DependencyProperty HorizontalScrollMaximumProperty = DependencyProperty.Register("HorizontalScrollMaximum", typeof(double), typeof(HexContent), new PropertyMetadata(0.0));

      #endregion

      #region SearchByte

      public static readonly DependencyProperty SearchByteProperty = DependencyProperty.Register(nameof(SearchBytes), typeof(byte[]), typeof(HexContent), new FrameworkPropertyMetadata(null, SearchByteChanged));

      public byte[] SearchBytes {
         get => (byte[])GetValue(SearchByteProperty);
         set => SetValue(SearchByteProperty, value);
      }

      private static void SearchByteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnRequestInvalidateVisual();
      }

      #endregion

      #region DesiredHorizontalViewportSize

      public static readonly DependencyProperty DesiredHorizontalViewportSizeProperty = DependencyProperty.Register(nameof(DesiredHorizontalViewportSize), typeof(double), typeof(HexContent), new FrameworkPropertyMetadata(0.0, DesiredHorizontalViewportSizeChanged));

      public double DesiredHorizontalViewportSize {
         get => (double)GetValue(DesiredHorizontalViewportSizeProperty);
         set => SetValue(DesiredHorizontalViewportSizeProperty, value);
      }

      private static void DesiredHorizontalViewportSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnDesiredHorizontalViewportSizeChanged(e);
      }

      protected virtual void OnDesiredHorizontalViewportSizeChanged(DependencyPropertyChangedEventArgs e) { }

      #endregion

      public ScreenPoint CursorLocation {
         get {
            if (!(ViewPort is ViewPort viewPort)) return new ScreenPoint(-1, -1);
            var selection = viewPort.SelectionStart;
            return new ScreenPoint(selection.X * CellWidth - HorizontalScrollValue, selection.Y * CellHeight);
         }
      }

      public HexContent() {
         ClipToBounds = true;
         Focusable = true;
         base.ToolTip = new ToolTip();
         ToolTipService.SetIsEnabled(this, false);

         void AddKeyCommand(string commandPath, object arg, Key key, ModifierKeys modifiers = ModifierKeys.None) {
            var keyBinding = new KeyBinding { CommandParameter = arg, Key = key, Modifiers = modifiers };
            BindingOperations.SetBinding(keyBinding, InputBinding.CommandProperty, new Binding(commandPath));
            InputBindings.Add(keyBinding);
         }

         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.PageUp, Key.PageUp);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.PageDown, Key.PageDown);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.Home, Key.Home);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionStart), Direction.End, Key.End);

         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.PageUp, Key.PageUp, ModifierKeys.Shift);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.PageDown, Key.PageDown, ModifierKeys.Shift);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.Home, Key.Home, ModifierKeys.Shift);
         AddKeyCommand(nameof(Core.ViewModels.ViewPort.MoveSelectionEnd), Direction.End, Key.End, ModifierKeys.Shift);

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
                  Execute = arg => (ViewPort as IEditableViewPort)?.Edit(consoleKey)
               }
            });
         }

         AddConsoleKeyCommand(Key.Back, ConsoleKey.Backspace);
         AddConsoleKeyCommand(Key.Escape, ConsoleKey.Escape);
         AddConsoleKeyCommand(Key.Enter, ConsoleKey.Enter);
         AddConsoleKeyCommand(Key.Tab, ConsoleKey.Tab);

         DataContextChanged += (sender, e) => ClearTooltip();
      }

      protected override void OnMouseDown(MouseButtonEventArgs e) {
         base.OnMouseDown(e);
         if (ViewPort == null) return;
         if (e.ChangedButton == MouseButton.XButton1 && ViewPort.Back.CanExecute(null)) {
            ViewPort.Back.Execute();
            return;
         }
         if (e.ChangedButton == MouseButton.XButton2 && ViewPort.Forward.CanExecute(null)) {
            ViewPort.Forward.Execute();
            return;
         }
         downPoint = ControlCoordinatesToModelCoordinates(e);
         if (e.ChangedButton == MouseButton.Middle) {
            CaptureMouse();
            return;
         }
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
            var point = e.GetPosition(this);
            if (point.X < 0) {
               downPoint = new ModelPoint(0, downPoint.Y);
               editableViewPort.SelectionStart = downPoint;
               editableViewPort.SelectionEnd = new ModelPoint(editableViewPort.Width - 1, downPoint.Y);
            } else if (Keyboard.Modifiers == ModifierKeys.Shift) {
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
            UpdateTooltip(newMouseOverPoint);
         }
         if (!IsMouseCaptured) return;

         var point = e.GetPosition(this);
         var modelPoint = ControlCoordinatesToModelCoordinates(e);

         if (e.MiddleButton == MouseButtonState.Pressed) {
            ViewPort.ScrollValue -= modelPoint.Y - downPoint.Y;
            downPoint = modelPoint;
            return;
         }

         if (!(ViewPort is ViewPort viewPort)) return;

         if (point.X < 0) {
            viewPort.SelectionEnd = new ModelPoint(viewPort.Width - 1, modelPoint.Y);
         } else {
            viewPort.SelectionEnd = modelPoint;
         }
      }

      private void UpdateTooltip(ModelPoint newMouseOverPoint) {
         IDataFormat format;
         try {
            format = ViewPort[newMouseOverPoint.X, newMouseOverPoint.Y].Format;
         } catch (InvalidOperationException) {
            // if the user moves the mouse during reload, there's a chance that this tries to query the model runs while they're reloading.
            // if that happens, just skip the tooltip update
            ClearTooltip();
            return;
         }

         bool needClearToolTip = true;
         if (ViewPort is ViewPort viewPort1) {
            var source = viewPort1.ConvertViewPointToAddress(newMouseOverPoint);
            if (format is IDataFormatInstance dfi) source = dfi.Source;
            if (source == previousSource && ToolTipService.GetIsEnabled(this)) {
               // already set
               needClearToolTip = false;
            } else if (MakeNewToolTip(format)) {
               previousSource = source;
               needClearToolTip = false;
            }
         }
         if (needClearToolTip) {
            ClearTooltip();
         }
      }

      private void ClearTooltip() {
         ToolTipService.SetIsEnabled(this, false);
         ToolTip.IsOpen = false;
         InvalidateVisual();
      }

      protected override void OnMouseUp(MouseButtonEventArgs e) {
         base.OnMouseUp(e);
         if (e.ChangedButton == MouseButton.Right && e.LeftButton == MouseButtonState.Released && !IsMouseCaptured) {
            var p = ControlCoordinatesToModelCoordinates(e);
            var children = new List<FrameworkElement>();

            if (ViewPort is ViewPort editableViewPort) {
               if (!editableViewPort.IsSelected(p)) editableViewPort.SelectionStart = p;
            }
            var items = ViewPort.GetContextMenuItems(p, FileSystem);
            children.AddRange(BuildContextMenuUI(items));

            ShowMenu(children);
            return;
         }
         if (!IsMouseCaptured) return;
         ReleaseMouseCapture();
      }

      private int previousSource;
      private new ToolTip ToolTip => (ToolTip)base.ToolTip;
      private void ShowToolTip() => ToolTip.IsOpen = true;
      protected override void OnMouseLeave(MouseEventArgs e) {
         ToolTip.IsOpen = false;
      }
      private bool MakeNewToolTip(IDataFormat instance) {
         var visitor = new ToolTipContentVisitor(ViewPort.Model);
         instance.Visit(visitor, default);
         ToolTip.IsOpen = false;
         if (visitor.Content.Count == 0) return false;
         base.ToolTip = new HexContentToolTip { DataContext = visitor.Content }; // have to make a new one to prevent a glitch of text changing as the old one fades to closed.
         ToolTipService.SetIsEnabled(this, true);
         ShowToolTip();
         InvalidateVisual();
         return true;
      }

      private IEnumerable<MenuItem> BuildContextMenuUI(IReadOnlyList<IContextItem> items) {
         foreach (var item in items) {
            if (item is ContextItemGroup group) {
               var menuItem = new MenuItem { Header = group.Text };
               foreach (var subItem in BuildContextMenuUI(group)) {
                  menuItem.Items.Add(subItem);
               }
               yield return menuItem;
            } else if (item is CompositeContextItem composite) {
               foreach(var subItem in BuildContextMenuUI(composite)) {
                  yield return subItem;
               }
            } else {
               yield return new MenuItem {
                  Header = item.Text,
                  InputGestureText = item.ShortcutText,
                  CommandParameter = item.Parameter ?? FileSystem,
                  Command = item.Command
               };
            }
         }
      }

      protected override void OnMouseWheel(MouseWheelEventArgs e) {
         base.OnMouseWheel(e);
         if (Keyboard.Modifiers == ModifierKeys.Control) {
            FontSize = Math.Min(Math.Max(8, FontSize + Math.Sign(e.Delta)), 24);
         } else if (Keyboard.Modifiers == ModifierKeys.Shift) {
            ViewPort.ScrollValue -= Math.Sign(e.Delta) * 5;
         } else {
            ViewPort.ScrollValue -= Math.Sign(e.Delta);
         }
      }

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);
         if (ViewPort == null) return;
         var visitor = new FormatDrawer(drawingContext, ViewPort, ViewPort.Width, ViewPort.Height, CellWidth, CellHeight, FontSize, SearchBytes);

         // clear
         drawingContext.DrawRectangle(Brush(nameof(Theme.Background)), null, new Rect(0, 0, ActualWidth, ActualHeight));

         {
            if (ShowHorizontalScroll) drawingContext.PushTransform(new TranslateTransform(-HorizontalScrollValue, 0));
            RenderBackground(drawingContext);
            RenderGrid(drawingContext);
            RenderSelection(drawingContext);
            {
               drawingContext.PushClip(new RectangleGeometry(new Rect(new Size(ViewPort.Width * CellWidth, ViewPort.Height * CellHeight))));
               RenderData(visitor);
               drawingContext.Pop();
            }
            if (ShowHorizontalScroll) drawingContext.Pop();
         }
      }

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }

      private void RenderBackground(DrawingContext context) {
         for (int y = 0; y < ViewPort.Height; y++) {
            var format = ViewPort[0, y].Format;
            if (format is Anchor anchor) format = anchor.OriginalFormat;
            if (!(format is SpriteDecorator sprite)) continue;
            if (sprite.CellWidth != ViewPort.Width) continue;
            var source = PixelImage.WriteOnce(sprite.Pixels);
            var (w, h) = (CellWidth, CellHeight);
            var rect = new Rect(0, y * h, w * sprite.CellWidth, h * sprite.CellHeight);
            if (sprite.CellWidth != sprite.Pixels.PixelWidth / 8 || sprite.CellHeight != sprite.Pixels.PixelHeight / 8) {
               // the sprite is to be displayed within the cell area, but need not be displayed with exactly one tile per cell.
               var availableRows = Math.Min(ViewPort.Height - y, sprite.CellHeight);
               var scale = Math.Max(sprite.Pixels.PixelWidth / (sprite.CellWidth * CellWidth), sprite.Pixels.PixelHeight / (availableRows * CellHeight));
               scale = 1 / scale;
               rect.Width = sprite.Pixels.PixelWidth * scale;
               rect.Height = sprite.Pixels.PixelHeight * scale;
               var availableWidth = ViewPort.Width * CellWidth;
               var availableHeight = availableRows * CellHeight;
               rect.X += (availableWidth - rect.Width) / 2;
               rect.Y += (availableHeight - rect.Height) / 2;
            }
            context.PushOpacity(.4);
            context.DrawImage(source, rect);
            context.Pop();
         }
      }

      private void RenderGrid(DrawingContext drawingContext) {
         if (!ShowGrid) return;

         var gridPen = new Pen(Brush(nameof(Theme.Backlight)), 1);

         for (int x = 1; x <= ViewPort.Width; x++) {
            drawingContext.DrawLine(gridPen, new ScreenPoint(CellWidth * x, 0), new ScreenPoint(CellWidth * x, CellHeight * ViewPort.Height));
         }

         for (int y = 1; y <= ViewPort.Height; y++) {
            drawingContext.DrawLine(gridPen, new ScreenPoint(0, CellHeight * y), new ScreenPoint(CellWidth * ViewPort.Width, CellHeight * y));
         }
      }

      private void RenderSelection(DrawingContext drawingContext) {
         var cellRect = new Rect(0, 0, CellWidth, CellHeight);
         ScreenPoint
            topLeft = new ScreenPoint(0, 0),
            topRight = new ScreenPoint(CellWidth, 0),
            bottomLeft = new ScreenPoint(0, CellHeight),
            bottomRight = new ScreenPoint(CellWidth, CellHeight);

         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               var element = ViewPort[x, y];
               if (element.Edited && !ViewPort.IsSelected(new ModelPoint(x, y))) {
                  drawingContext.DrawRectangle(Brush(nameof(Theme.EditBackground)), null, new Rect(x * CellWidth, y * CellHeight, CellWidth, CellHeight));
               }

               if (!ViewPort.IsSelected(new ModelPoint(x, y))) continue;

               drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));

               drawingContext.DrawRectangle(Brush(nameof(Theme.Backlight)), null, cellRect);
               if (element.Edited) {
                  drawingContext.DrawRectangle(Brush(nameof(Theme.EditBackground)), null, cellRect);
               }
               if (!ViewPort.IsSelected(new ModelPoint(x, y - 1))) drawingContext.DrawLine(BorderPen, topLeft, topRight);
               if (!ViewPort.IsSelected(new ModelPoint(x, y + 1))) drawingContext.DrawLine(BorderPen, bottomLeft, bottomRight);
               if (!ViewPort.IsSelected(new ModelPoint(x - 1, y))) drawingContext.DrawLine(BorderPen, topLeft, bottomLeft);
               if (!ViewPort.IsSelected(new ModelPoint(x + 1, y))) drawingContext.DrawLine(BorderPen, topRight, bottomRight);

               drawingContext.Pop();
            }
         }
      }

      private void RenderData(FormatDrawer visitor) {
         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               visitor.MouseIsOverCurrentFormat = mouseOverPoint.Equals(new ModelPoint(x, y));
               var element = ViewPort[x, y];

               visitor.Position = new ModelPoint(x, y);
               element.Format.Visit(visitor, element.Value);

               if (element.Format is UnderEdit underEdit && underEdit.AutocompleteOptions != null) {
                  ShowAutocompletePopup(x, y, underEdit.AutocompleteOptions);
               }
            }
         }
      }

      private void ShowAutocompletePopup(int x, int y, IReadOnlyList<AutoCompleteSelectionItem> autocompleteOptions) {
         // close any currently open menu
         if (autocompleteOptions.Count == 0) {
            if (recentMenu != null && recentMenu.IsOpen) recentMenu.IsOpen = false;
            if (ContextMenu != null) ContextMenu.IsOpen = false;
            return;
         }

         var children = new List<FrameworkElement>();
         foreach (var option in autocompleteOptions) {
            var button = new Button { Content = option.CompletionText };
            button.Click += (sender, e) => {
               var text = ((Button)sender).Content.ToString();
               ((ViewPort)ViewPort).Autocomplete(text);
               recentMenu.IsOpen = false;
               if (ContextMenu != null) ContextMenu.IsOpen = false;
               Focus();
            };
            if (option.IsSelected) button.BorderBrush = Brush(nameof(Theme.Accent));
            children.Add(button);
         }

         // reuse existing popup if possible (to prevent flickering)
         if (recentMenu == null) recentMenu = new Popup();
         recentMenu.Child = FillPopup(children);
         recentMenu.StaysOpen = false;
         recentMenu.Placement = PlacementMode.Relative;
         recentMenu.PlacementTarget = this;
         recentMenu.VerticalOffset = (y + 1) * CellHeight;
         recentMenu.HorizontalOffset = x * CellWidth - HorizontalScrollValue;
         recentMenu.IsOpen = true;
      }

      protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e) {
         base.OnIsKeyboardFocusedChanged(e);
         if (ViewPort is IEditableViewPort editableViewPort) editableViewPort.IsFocused = IsKeyboardFocused;
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         UpdateViewPortSize();
         InvalidateVisual();
         CursorNeedsUpdate?.Invoke(this, EventArgs.Empty);
      }

      protected override void OnTextInput(TextCompositionEventArgs e) {
         if (ViewPort is ViewPort editableViewPort) {
            editableViewPort.Edit(e.Text);
            e.Handled = true;
         }
      }

      protected override void OnPreviewKeyDown(KeyEventArgs e) {
         ClearTooltip();
         base.OnPreviewKeyDown(e);
      }

      protected override void OnKeyDown(KeyEventArgs e) {
         base.OnKeyDown(e);
         if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) {
            if (ViewPort is IEditableViewPort viewPort) {
               var point = viewPort.SelectionStart;
               viewPort.FollowLink(point.X, point.Y);
               e.Handled = true;
            }
         }
      }

      private void ShowMenu(IList<FrameworkElement> children) {
         if (children.Count == 0) return;

         ContextMenu = new ContextMenu();
         foreach (var item in children) ContextMenu.Items.Add(item);
      }

      private static FrameworkElement FillPopup(IList<FrameworkElement> children) {
         var panel = new StackPanel { Background = Brush(nameof(Theme.Background)), MinWidth = 150 };
         foreach (var child in children) panel.Children.Add(child);
         var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, MaxHeight = 200 };
         return new Border {
            BorderBrush = Brush(nameof(Theme.Accent)),
            BorderThickness = new Thickness(1),
            Child = scroll,
         };
      }

      private void UpdateViewPortSize() {
         if (ViewPort == null) return;

         // calculate the initial 3x2 cell size from the fontsize
         var sampleElement = new FormattedText("000", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), FontSize, Brushes.Transparent, 1);
         CellHeight = Math.Ceiling(Math.Max(sampleElement.Height, sampleElement.Width * 2 / 3));
         CellWidth = Math.Ceiling(CellHeight * 3 / 2);

         // let the ViewPort decide its width based on the available space for cells per line
         ViewPort.Width = (int)(ActualWidth / CellWidth);
         ViewPort.Height = (int)(ActualHeight / CellHeight);

         // add extra width to the cells as able
         var extraWidth = Math.Min(ActualWidth - ViewPort.Width * CellWidth, ViewPort.Width * CellWidth * 2);
         if (extraWidth > 0 && ViewPort.StretchData) CellWidth += (int)(extraWidth / ViewPort.Width);

         // add horizontal scrolling if needed
         var requiredSize = ViewPort.Width * CellWidth;
         if (requiredSize > ActualWidth) {
            ShowHorizontalScroll = true;
            HorizontalScrollMaximum = requiredSize - ActualWidth;
            HorizontalScrollValue = Math.Min(HorizontalScrollValue, HorizontalScrollMaximum);
            var desiredThumbWidth = ActualWidth / requiredSize;
            DesiredHorizontalViewportSize = HorizontalScrollMaximum * desiredThumbWidth / (1 - desiredThumbWidth);
         } else {
            ShowHorizontalScroll = false;
            HorizontalScrollValue = 0;
         }
      }

      private ModelPoint ControlCoordinatesToModelCoordinates(MouseEventArgs e) {
         var point = e.GetPosition(this);
         point = new ScreenPoint(Math.Max(0, point.X + HorizontalScrollValue), point.Y); // out of bounds to the left clamps to 0 (useful for row headers)
         return new ModelPoint(Math.Min((int)(point.X / CellWidth), ViewPort.Width - 1), (int)(point.Y / CellHeight)); // out of bounds right clamps to Width - 1 (prevents weird multiline scrolling.)
      }
   }

   public static class FrameworkElementExtensions {
      public static T SetEvent<T>(this T item, RoutedEvent routedEvent, RoutedEventHandler handler) where T : FrameworkElement {
         item.AddHandler(routedEvent, handler);
         return item;
      }
   }
}
