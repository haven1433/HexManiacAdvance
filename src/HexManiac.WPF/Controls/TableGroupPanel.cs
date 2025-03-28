using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Windows.Input;
using System.Linq;
using HavenSoft.HexManiac.WPF.Resources;
using System.Windows.Threading;
using System.Windows.Controls;

namespace HavenSoft.HexManiac.WPF.Controls;

// TODO jump button for enums
// TODO jump button for bit arrays
// TODO jump button behavior
// TODO text selection for textboxes
// TODO text editing for textboxes
// TODO application commands (cut/copy/paste/selectall) for textboxes
// TODO keyboard shortcuts cut/copy/paste/selectall for textboxes
// TODO home/end/left/right/increment/decrement for textboxes

// TODO text editing for enums
// TODO showing filtered dropdown for enums

// additional controls:
// tuples
// calculated fields


public partial class TableGroupPanel : FrameworkElement {
   private readonly SpriteCache spriteCache = new();
   private readonly DispatcherTimer timer;
   private bool isCursorShowing;

   private IArrayElementViewModel keyboardFocusElement, mouseHoverElement;

   private readonly Dictionary<IArrayElementViewModel, IGroupControl> controls = new();

   #region Source

   public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(ObservableCollection<IArrayElementViewModel>), typeof(TableGroupPanel), new FrameworkPropertyMetadata(null, SourceChanged));

   public ObservableCollection<IArrayElementViewModel> Source {
      get => (ObservableCollection<IArrayElementViewModel>)GetValue(SourceProperty);
      set => SetValue(SourceProperty, value);
   }

   private static void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      var self = (TableGroupPanel)d;
      self.OnSourceChanged(e);
   }

   protected virtual void OnSourceChanged(DependencyPropertyChangedEventArgs e) {
      var oldSource = (ObservableCollection<IArrayElementViewModel>)e.OldValue;
      if (oldSource != null) {
         oldSource.CollectionChanged -= CollectionChanged;
         foreach (var element in oldSource) element.PropertyChanged -= CollectionPropertyChanged;
         controls.Clear();
      }
      var newSource = (ObservableCollection<IArrayElementViewModel>)e.NewValue;
      if (newSource != null) {
         newSource.CollectionChanged += CollectionChanged;
         foreach (var element in newSource) {
            element.PropertyChanged += CollectionPropertyChanged;
            controls[element] = BuildControl(element);
         }
      }
      InvalidateVisual();
   }

   private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      if (e.Action.IsAny(NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Remove)) {
         foreach (IArrayElementViewModel item in e.OldItems) {
            item.PropertyChanged -= CollectionPropertyChanged;
            controls.Remove(item);
         }
      }
      if (e.Action.IsAny(NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace)) {
         foreach (IArrayElementViewModel item in e.NewItems) {
            item.PropertyChanged += CollectionPropertyChanged;
            controls[item] = BuildControl(item);
         }
      }
      if (e.Action.IsAny(NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Move)) {
         controls.Clear();
         foreach (var element in Source) {
            element.PropertyChanged -= CollectionPropertyChanged;
            element.PropertyChanged += CollectionPropertyChanged;
            controls[element] = BuildControl(element);
         }
      }
      InvalidateVisual();
   }

   private void CollectionPropertyChanged(object? sender, PropertyChangedEventArgs e) {
      if (sender is SpriteElementViewModel sprite) spriteCache.NeedsRedraw(sprite);
      InvalidateVisual();
   }

   #endregion

   private Point cursorPosition = new(double.NaN, double.NaN);
   public Point CursorPosition {
      get => cursorPosition;
      set {
         cursorPosition = value;
         if (double.IsNaN(cursorPosition.X)) {
            timer.Stop();
            InvalidateVisual();
         } else {
            isCursorShowing = true;
            timer.Start();
         }
      }
   }

   public int FontSize { get; private set; } = 16; // TODO dependency property?

   public TableGroupPanel() {
      RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
      VerticalAlignment = VerticalAlignment.Top;
      ClipToBounds = true;
      Focusable = true;
      timer = new DispatcherTimer(TimeSpan.FromSeconds(.6), DispatcherPriority.ApplicationIdle, BlinkCursor, Dispatcher);
   }

   protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
      base.OnLostKeyboardFocus(e);
      if (keyboardFocusElement != null) {
         keyboardFocusElement = null;
         InvalidateVisual();
      }
      CursorPosition = new(double.NaN, double.NaN);
   }

   protected override void OnRender(DrawingContext dc) {
      base.OnRender(dc);
      dc.DrawRectangle(Brush(nameof(Theme.Background)), null, new(0, 0, ActualWidth, ActualHeight));
      if (Source == null) return;
      var offset = 0;
      var width = (int)ActualWidth;
      var context = new RenderContext(dc);
      foreach (var element in Source) {
         if (!element.Visible && element is not SplitterArrayElementViewModel) continue;
         controls[element].Render(context);
         offset += controls[element].Height;
      }

      if (isCursorShowing && !double.IsNaN(CursorPosition.X)) {
         dc.DrawLine(new Pen(Brush(nameof(Theme.Secondary)), 1), CursorPosition, new(cursorPosition.X, cursorPosition.Y + FontSize));
      }

   }

   protected override Size MeasureOverride(Size availableSize) {
      // how big I want to be
      int width = (int)availableSize.Width, height = 0;
      foreach (var element in Source) {
         if (!element.Visible && element is not SplitterArrayElementViewModel) continue;
         height += controls[element].UpdateHeight(width, height, FontSize);
      }
      return new Size(Math.Max(width, 100), height);
   }

   protected override Size ArrangeOverride(Size finalSize) {
      // how big do I actually get to be
      int width = (int)base.ArrangeOverride(finalSize).Width, height = 0;
      foreach (var element in Source) {
         if (!element.Visible && element is not SplitterArrayElementViewModel) continue;
         height += controls[element].UpdateHeight(width, height, FontSize);
      }
      return new Size(Math.Max(width, 100), height);
   }

   #region Mouse

   private IArrayElementViewModel GetElementUnderCursor(int y) {
      foreach (var member in Source) {
         if (member is SplitterArrayElementViewModel || member.Visible) y -= controls[member].Height;
         if (y < 0) return member;
      }
      return null;
   }

   protected override void OnMouseDown(MouseButtonEventArgs e) {
      base.OnMouseDown(e);
      var pos = e.GetPosition(this);
      mouseHoverElement = GetElementUnderCursor((int)pos.Y);
      keyboardFocusElement = mouseHoverElement;
      if (mouseHoverElement == null) return;
      Focus();
      controls[mouseHoverElement].MouseDown(this, e);
      e.Handled = true;
      CaptureMouse();
      InvalidateVisual();
   }

   protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      if (!IsMouseCaptured) {
         var pos = e.GetPosition(this);
         var previousHoverElement = mouseHoverElement;
         mouseHoverElement = GetElementUnderCursor((int)pos.Y);
         if (previousHoverElement != mouseHoverElement) {
            if (previousHoverElement != null) controls[previousHoverElement].MouseExit(this, e);
            if (mouseHoverElement != null) controls[mouseHoverElement].MouseEnter(this, e);
         }
         if (mouseHoverElement != null) controls[mouseHoverElement].MouseMove(this, e);
      } else {
         if (mouseHoverElement != null) controls[mouseHoverElement].MouseMove(this, e);
      }
   }

   protected override void OnMouseUp(MouseButtonEventArgs e) {
      base.OnMouseUp(e);
      if (!IsMouseCaptured) return;
      if (mouseHoverElement != null) controls[mouseHoverElement].MouseUp(this, e);
      ReleaseMouseCapture();
   }

   protected override void OnMouseLeave(MouseEventArgs e) {
      base.OnMouseLeave(e);
      if (mouseHoverElement != null) {
         controls[mouseHoverElement].MouseExit(this, e);
         mouseHoverElement = null;
      }
   }

   #endregion

   protected override void OnTextInput(TextCompositionEventArgs e) {
      base.OnTextInput(e);
      if (keyboardFocusElement == null) return;
      controls[keyboardFocusElement].TextInput(this, e);
      e.Handled = true;
   }

   protected override void OnKeyDown(KeyEventArgs e) {
      base.OnKeyDown(e);
      if (keyboardFocusElement == null) return;
      if (e.Key == Key.Tab) {
         var index = Source.IndexOf(keyboardFocusElement);
         if (index != -1) {
            index += 1;
            if (Keyboard.Modifiers == ModifierKeys.Shift) index -= 2;
            if (index < 0) index += Source.Count;
            if (index >= Source.Count) index -= Source.Count;
            keyboardFocusElement = Source[index];
            e.Handled = true;
            InvalidateVisual();
            // TODO some sort of "gained focus" notification for the GroupControl?
            return;
         }
      }
      controls[keyboardFocusElement].KeyInput(this, e);
   }

   private static SolidColorBrush Brush(string name) {
      return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
   }

   private IGroupControl BuildControl(IArrayElementViewModel element) {
      IGroupControl control = element switch {
         SplitterArrayElementViewModel splitter => new GroupSplitterControl(splitter),
         FieldArrayElementViewModel field => new GroupTextControl(field),
         ComboBoxArrayElementViewModel combo => new GroupEnumControl(combo),
         BitListArrayElementViewModel bits => new GroupBitArrayControl(bits),
         SpriteElementViewModel sprite => new GroupImageControl(sprite, spriteCache),
         SpriteIndicatorElementViewModel spriteIndicator => new GroupSpriteIndicatorControl(spriteIndicator, spriteCache),
         PaletteElementViewModel palette => new GroupPaletteControl(palette),
         OffsetRenderViewModel offsetRender => new GroupOffsetRenderControl(offsetRender, spriteCache),
         TextStreamElementViewModel textStream => new GroupTextStreamControl(textStream),
         ButtonArrayElementViewModel button => new GroupButtonControl(button),
         PythonButtonElementViewModel pButton => new GroupPythonButtonControl(pButton),
         _ => new GroupDefaultControl(element)
      };

      return control;
   }

   private void BlinkCursor(object sender, EventArgs e) {
      isCursorShowing = !isCursorShowing;
      InvalidateVisual();
   }

   /*
   #region Helpers

   private record TableGroupMouseEditor(IArrayElementViewModel Element, int AvailableWidth, Point Position, MouseButtonEventArgs Args) {
      public void Edit() {
         if (!Element.Visible) return;

         else if (Element is SplitterArrayElementViewModel splitter) Edit(splitter);
         else if (Element is TextStreamElementViewModel stream) Edit(stream);
         else if (Element is FieldArrayElementViewModel field) Edit(field);
         else if (Element is ComboBoxArrayElementViewModel combo) Edit(combo);
         else if (Element is PaletteElementViewModel pal) Edit(pal);
         else if (Element is SpriteElementViewModel sprite) Edit(sprite);
         else if (Element is SpriteIndicatorElementViewModel indicator) Edit(indicator);
         else if (Element is TupleArrayElementViewModel tuples) Edit(tuples);
         else if (Element is PythonButtonElementViewModel python) Edit(python);
         else if (Element is ButtonArrayElementViewModel button) Edit(button);
         else if (Element is BitListArrayElementViewModel bits) Edit(bits);
         else if (Element is CalculatedElementViewModel calc) Edit(calc);
         else {
            // ???
         }
      }

      private void Edit(CalculatedElementViewModel calc) { }

      private void Edit(BitListArrayElementViewModel bits) { }

      private void Edit(ButtonArrayElementViewModel button) { }

      private void Edit(PythonButtonElementViewModel python) { }

      private void Edit(ComboBoxArrayElementViewModel combo) {
         // TODO
      }

      private void Edit(TupleArrayElementViewModel tuples) {
         throw new NotImplementedException();
      }

      private void Edit(SpriteIndicatorElementViewModel indicator) { }

      private void Edit(SpriteElementViewModel sprite) {
         throw new NotImplementedException();
      }

      private void Edit(PaletteElementViewModel pal) {
         // TODO support right-click
      }

      private void Edit(FieldArrayElementViewModel field) {
         // TODO support right-click
      }

      private void Edit(TextStreamElementViewModel stream) {
         // TODO
      }

      private void Edit(SplitterArrayElementViewModel splitter) { }
   }

   private record TableGroupKeyEditor(TableGroupPanel Panel, int AvailableWidth, KeyEventArgs Args) {
      public void Edit() {
         var element = Panel.focusElement;
         if (!element.Visible) return;

         else if (element is SplitterArrayElementViewModel splitter) Edit(splitter);
         else if (element is TextStreamElementViewModel stream) Edit(stream);
         else if (element is FieldArrayElementViewModel field) Edit(field);
         else if (element is ComboBoxArrayElementViewModel combo) Edit(combo);
         else if (element is PaletteElementViewModel pal) Edit(pal);
         else if (element is SpriteElementViewModel sprite) Edit(sprite);
         else if (element is SpriteIndicatorElementViewModel indicator) Edit(indicator);
         else if (element is TupleArrayElementViewModel tuples) Edit(tuples);
         else if (element is PythonButtonElementViewModel python) Edit(python);
         else if (element is ButtonArrayElementViewModel button) Edit(button);
         else if (element is BitListArrayElementViewModel bits) Edit(bits);
         else if (element is CalculatedElementViewModel calc) Edit(calc);
         else {
            // ???
         }
      }

      private void Edit(CalculatedElementViewModel calc) { }

      private void Edit(BitListArrayElementViewModel bits) { }

      private void Edit(ButtonArrayElementViewModel button) { }

      private void Edit(PythonButtonElementViewModel python) { }

      private void Edit(ComboBoxArrayElementViewModel combo) {
         // TODO
      }

      private void Edit(TupleArrayElementViewModel tuples) {
         throw new NotImplementedException();
      }

      private void Edit(SpriteIndicatorElementViewModel indicator) { }

      private void Edit(SpriteElementViewModel sprite) {
         throw new NotImplementedException();
      }

      private void Edit(PaletteElementViewModel pal) {
         // TODO support copy/paste
      }

      private void Edit(FieldArrayElementViewModel field) {
         if (Args.Key == Key.Back) {
            field.Content = field.Content.Substring(0, field.Content.Length - 1);
            Args.Handled = true;
         } else if (Args.Key == Key.Escape) {
            field.ResetContent();
            Args.Handled = true;
            Panel.focusElement = null;
            Panel.InvalidateVisual();
         } else if (Args.Key == Key.Enter) {
            field.Accept();
            Args.Handled = true;
            Panel.focusElement = null;
            Panel.InvalidateVisual();
         }
      }

      private void Edit(TextStreamElementViewModel stream) {
         // TODO
      }

      private void Edit(SplitterArrayElementViewModel splitter) { }
   }

   private record TableGroupTextEditor(IArrayElementViewModel Element, int AvailableWidth, TextCompositionEventArgs Args) {
      public void Edit() {
         if (!Element.Visible) return;

         else if (Element is SplitterArrayElementViewModel splitter) Edit(splitter);
         else if (Element is TextStreamElementViewModel stream) Edit(stream);
         else if (Element is FieldArrayElementViewModel field) Edit(field);
         else if (Element is ComboBoxArrayElementViewModel combo) Edit(combo);
         else if (Element is PaletteElementViewModel pal) Edit(pal);
         else if (Element is SpriteElementViewModel sprite) Edit(sprite);
         else if (Element is SpriteIndicatorElementViewModel indicator) Edit(indicator);
         else if (Element is TupleArrayElementViewModel tuples) Edit(tuples);
         else if (Element is PythonButtonElementViewModel python) Edit(python);
         else if (Element is ButtonArrayElementViewModel button) Edit(button);
         else if (Element is BitListArrayElementViewModel bits) Edit(bits);
         else if (Element is CalculatedElementViewModel calc) Edit(calc);
         else {
            // ???
         }
      }

      private void Edit(CalculatedElementViewModel calc) { }

      private void Edit(BitListArrayElementViewModel bits) { }

      private void Edit(ButtonArrayElementViewModel button) { }

      private void Edit(PythonButtonElementViewModel python) { }

      private void Edit(ComboBoxArrayElementViewModel combo) {
         // TODO
      }

      private void Edit(TupleArrayElementViewModel tuples) {
         throw new NotImplementedException();
      }

      private void Edit(SpriteIndicatorElementViewModel indicator) { }

      private void Edit(SpriteElementViewModel sprite) {
         throw new NotImplementedException();
      }

      private void Edit(PaletteElementViewModel pal) {
         // TODO support copy/paste
      }

      private void Edit(FieldArrayElementViewModel field) {
         field.Content += Args.Text;
      }

      private void Edit(TextStreamElementViewModel stream) {
         // TODO
      }

      private void Edit(SplitterArrayElementViewModel splitter) { }
   }

   private record TableGroupPainter(DrawingContext Context, SpriteCache SpriteCache, int AvailableWidth, IArrayElementViewModel FocusElement) {
      private const int DefaultFontSize = 16;
      private const int DefaultTextPadding = 4;

      public int X { get; private set; }
      public int Y { get; private set; }

      private Pen borderPen = new Pen(Brush(nameof(Theme.Primary)), 1);
      private Pen accentPen = new Pen(Brush(nameof(Theme.Accent)), 1);

      #region Render

      public void Render(IArrayElementViewModel member) {
         if (!member.Visible) return; // Don't Render

         else if (member is SplitterArrayElementViewModel splitter) Render(splitter);
         else if (member is TextStreamElementViewModel stream) Render(stream);
         else if (member is FieldArrayElementViewModel field) Render(field);
         else if (member is ComboBoxArrayElementViewModel combo) Render(combo);
         else if (member is PaletteElementViewModel pal) Render(pal);
         else if (member is SpriteElementViewModel sprite) Render(sprite);
         else if (member is SpriteIndicatorElementViewModel indicator) Render(indicator);
         else if (member is TupleArrayElementViewModel tuples) Render(tuples);
         else if (member is PythonButtonElementViewModel python) Render(python);
         else if (member is ButtonArrayElementViewModel button) Render(button);
         else if (member is BitListArrayElementViewModel bits) Render(bits);
         else if (member is CalculatedElementViewModel calc) Render(calc);
         else {
            Context?.DrawEllipse(Brush(nameof(Theme.Primary)), null, new(30, 30 + Y), 30, 30);
            Y += 60;
         }

         RoundHeight();
      }

      public void Render(SplitterArrayElementViewModel splitter) {
         Context?.DrawText(NormalText(splitter.SectionName), new(X, Y));
         Y += DefaultFontSize + DefaultTextPadding;
      }

      public void Render(FieldArrayElementViewModel field) {
         Context?.DrawText(NormalText(field.Name), new(X, Y));
         var pen = field == FocusElement ? accentPen : null;
         Context?.DrawRectangle(Brush(nameof(Theme.Backlight)), pen, new Rect(X + AvailableWidth / 2, Y, AvailableWidth / 2, DefaultFontSize + DefaultTextPadding - 1));
         var contentText = NormalText(field.Content);
         Context?.DrawText(contentText, new(AvailableWidth - contentText.Width - 4, Y));
         Y += DefaultFontSize + DefaultTextPadding;
      }

      public void Render(TextStreamElementViewModel stream) {
         var text = NormalText(stream.Content);
         text.MaxTextWidth = AvailableWidth;

         Context?.DrawRectangle(Brush(nameof(Theme.Backlight)), null, new Rect(X, Y, AvailableWidth, text.Height));
         Context?.DrawText(text, new(X, Y));
         Y += (int)Math.Ceiling(text.Height);
      }

      public void Render(ComboBoxArrayElementViewModel combo) {
         Context?.DrawText(NormalText(combo.Name), new(X, Y));
         Context?.DrawRectangle(Brush(nameof(Theme.Backlight)), null, new Rect(X + AvailableWidth / 2, Y, AvailableWidth / 2, DefaultFontSize + DefaultTextPadding - 1));
         var contentText = NormalText(combo.FilteringComboOptions.DisplayText);
         Context?.DrawText(contentText, new(AvailableWidth - contentText.Width, Y));
         Y += DefaultFontSize + DefaultTextPadding;
      }

      public void Render(SpriteIndicatorElementViewModel indicator) {
         var sprite = indicator.Image;
         Context?.DrawImage(SpriteCache.WriteUpdate(sprite), new Rect(X, Y, sprite.PixelWidth * sprite.SpriteScale, sprite.PixelHeight * sprite.SpriteScale));
         var height = (int)Math.Ceiling(sprite.PixelHeight * sprite.SpriteScale);
         var mod = height % (DefaultFontSize + DefaultTextPadding);
         if (mod != 0) height += DefaultFontSize + DefaultTextPadding - mod;
         Y += height;
      }

      public void Render(SpriteElementViewModel sprite) {
         if (Context != null) {
            Context.DrawImage(SpriteCache.WriteUpdate(sprite), new Rect(X, Y, sprite.PixelWidth * sprite.SpriteScale, sprite.PixelHeight * sprite.SpriteScale));
         }

         Y += (int)Math.Ceiling(sprite.PixelHeight * sprite.SpriteScale);
      }

      public void Render(PaletteElementViewModel pal) {
         var stride = 4;
         if (pal.Colors.Elements.Count > 16) stride = 8;
         if (Context != null) {
            for (int i = 0; i < pal.Colors.Elements.Count; i++) {
               var wpfColor = TileImage.Convert16BitColor(pal.Colors.Elements[i].Color);
               var brush = new SolidColorBrush(wpfColor);
               var rect = new Rect(X + (i % stride) * 16, Y + i / stride * 16, 16, 16);
               Context?.DrawRectangle(brush, borderPen, rect);
            }
         }
         Y += pal.Colors.Elements.Count / stride * 16;
      }

      public void Render(PythonButtonElementViewModel python) {
         var text = NormalText(python.Name);
         Context?.DrawRectangle(null, borderPen, new Rect(X, Y, text.Width + 4, text.Height + 4));
         Context?.DrawText(text, new(X + 2, Y + 2));
         Y += (int)Math.Ceiling(text.Height) + DefaultTextPadding;
      }

      public void Render(ButtonArrayElementViewModel button) {
         var text = NormalText(button.Text);
         Context?.DrawRectangle(null, borderPen, new Rect(X, Y, text.Width + 4, text.Height + 4));
         Context?.DrawText(text, new(X + 2, Y + 2));
         Y += (int)Math.Ceiling(text.Height) + DefaultTextPadding;
      }

      public void Render(CalculatedElementViewModel calc) {
         Context?.DrawText(NormalText(calc.Name), new(X, Y));
         var contentText = NormalText(calc.CalculatedValue);
         Context?.DrawText(contentText, new(AvailableWidth - contentText.Width, Y));
         Y += DefaultFontSize + DefaultTextPadding;
      }

      public void Render(BitListArrayElementViewModel list) {
         Context?.DrawText(NormalText(list.Name), new(X, Y));
         Y += DefaultFontSize + DefaultTextPadding;
         var halfWidth = AvailableWidth / 2;
         foreach (var element in list) {
            Context?.DrawText(NormalText(element.BitLabel), new(X, Y));
            DrawCheckbox(element.IsChecked, X + AvailableWidth / 4, Y);
            X += halfWidth;
            if (X > halfWidth) {
               X = 0;
               Y += DefaultFontSize + DefaultTextPadding;
            }
         }
         if (X != 0) {
            X = 0;
            Y += DefaultFontSize + DefaultTextPadding;
         }
      }

      public void Render(TupleArrayElementViewModel tuples) {
         Context?.DrawText(NormalText(tuples.Name), new(X, Y));
         Y += DefaultFontSize + DefaultTextPadding;
         var halfWidth = AvailableWidth / 2;
         foreach (var tuple in tuples.Children) {
            if (tuple is CheckBoxTupleElementViewModel cbt) Render(cbt);
            if (tuple is EnumTupleElementViewModel et) Render(et);
            if (tuple is NumericTupleElementViewModel nt) Render(nt);
            X += halfWidth;
            if (X > halfWidth) {
               X = 0;
               Y += DefaultFontSize + DefaultTextPadding;
            }
         }
         if (X != 0) {
            X = 0;
            Y += DefaultFontSize + DefaultTextPadding;
         }
      }

      public void Render(CheckBoxTupleElementViewModel cbt) {
         Context?.DrawText(NormalText(cbt.Name), new(X, Y));
         DrawCheckbox(cbt.IsChecked, X + AvailableWidth / 4, Y);
      }

      public void Render(EnumTupleElementViewModel et) {
         Context?.DrawText(NormalText(et.Name), new(X, Y));
         Context?.DrawRectangle(Brush(nameof(Theme.Backlight)), null, new Rect(X + AvailableWidth / 4, Y, AvailableWidth / 4, DefaultFontSize + DefaultTextPadding - 1));
         var contentText = NormalText(et.FilteringComboOptions.DisplayText);
         Context?.DrawText(contentText, new(X + AvailableWidth / 2 - contentText.Width, Y));
      }

      public void Render(NumericTupleElementViewModel nt) {
         Context?.DrawText(NormalText(nt.Name), new(X, Y));
         Context?.DrawRectangle(Brush(nameof(Theme.Backlight)), null, new Rect(X + AvailableWidth / 4, Y, AvailableWidth / 4, DefaultFontSize + DefaultTextPadding - 1));
         var contentText = NormalText(nt.Content.ToString());
         Context?.DrawText(contentText, new(X + AvailableWidth / 2 - contentText.Width - 4, Y));
      }

      #endregion

      public static FormattedText NormalText(string text) => new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), DefaultFontSize, Brush(nameof(Theme.Primary)), 1);

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }

      private void RoundHeight() {
         var mod = Y % (DefaultFontSize + DefaultTextPadding);
         if (mod != 0) Y += DefaultFontSize + DefaultTextPadding - mod;
      }

      private void DrawCheckbox(bool isChecked, int x, int y) {
         Context?.DrawRectangle(null, borderPen, new Rect(x, y, 16, 16));
         if (isChecked) { // draw X
            Context?.DrawLine(accentPen, new(x + 4, y + 4), new(x + 11, y + 11));
            Context?.DrawLine(accentPen, new(x + 4, y + 11), new(x + 11, y + 4));
         }
      }
   }

   #endregion
   //*/
}

public class SpriteCache {
   // sprites are expensive, but need to be updated often
   // Re-use the same ImageSource (WriteableBitmap) when possible to save resources.

   private readonly List<WriteableBitmap> wbCache = new();
   private readonly List<IPixelViewModel> pvmCache = new();
   private long cacheNeedsRedraw = 0;

   public WriteableBitmap WriteUpdate(IPixelViewModel viewModel) {
      var cacheIndex = pvmCache.IndexOf(viewModel);
      if (cacheIndex >= 0 && (cacheNeedsRedraw & (1L << cacheIndex)) == 0) return wbCache[cacheIndex];

      var pixels = viewModel.PixelData;
      if (pixels == null) return null;
      var expectedLength = viewModel.PixelWidth * viewModel.PixelHeight;
      if (pixels.Length < expectedLength || pixels.Length == 0) return null;
      int stride = viewModel.PixelWidth * 2;
      var rect = new Int32Rect(0, 0, viewModel.PixelWidth, viewModel.PixelHeight);
      var format = PixelFormats.Bgr555;

      if (cacheIndex < 0) {
         // image not found, need to make one
         var source = new WriteableBitmap(viewModel.PixelWidth, viewModel.PixelHeight, 96, 96, format, null);
         source.WritePixels(rect, pixels, stride, 0);
         wbCache.Add(source);
         pvmCache.Add(viewModel);
         if (wbCache.Count > 64) { wbCache.RemoveAt(0); pvmCache.RemoveAt(0); cacheNeedsRedraw >>= 1; }
         return source;
      }
      else if (wbCache[cacheIndex].PixelWidth != viewModel.PixelWidth || wbCache[cacheIndex].PixelHeight != viewModel.PixelHeight) {
         // size is wrong, throw out the cache and replace it
         var source = new WriteableBitmap(viewModel.PixelWidth, viewModel.PixelHeight, 96, 96, format, null);
         source.WritePixels(rect, pixels, stride, 0);
         wbCache[cacheIndex] = source;
         return source;
      }
      else {
         // size is write, just redraw over the same WriteableBitmap to save resources.
         wbCache[cacheIndex].WritePixels(rect, pixels, stride, 0);
         cacheNeedsRedraw &= ~(1L << cacheIndex);
         return wbCache[cacheIndex];
      }
   }

   public void NeedsRedraw(IPixelViewModel sprite) {
      var cacheIndex = pvmCache.IndexOf(sprite);
      if (cacheIndex >= 0) cacheNeedsRedraw |= 1L << cacheIndex;
   }
}

public record RenderContext(DrawingContext Api) {
   public static Typeface Consolas { get; } = new Typeface("Consolas");

   public int DefaultTextPadding => 4;
   public int CurrentFontSize { get; set; } = 16;
   public Pen AccentPen { get; } = new Pen(Brush(nameof(Theme.Accent)), 1);

   public static SolidColorBrush Brush(string name) {
      if (string.IsNullOrEmpty(name)) return null;
      return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
   }

   public void DrawRectangle(string fill, string stroke, int x, int y, int width, int height) {
      var borderPen = string.IsNullOrEmpty(stroke) ? null : new Pen(Brush(stroke), 1);
      Api.DrawRectangle(Brush(fill), borderPen, new Rect(x, y, width, height));
   }

   public void DrawIcon(Rect placement, string icon, string fill, string border = null, double borderThickness = 1) {
      var pen = border != null ? new Pen(Brush(border), borderThickness) : null;
      var geometry = IconExtension.GetIcon(icon);
      var widthRatio = placement.Width / geometry.Bounds.Width;
      var heightRatio = placement.Height / geometry.Bounds.Height;
      Api.PushTransform(new TranslateTransform(placement.X - geometry.Bounds.Left * widthRatio, placement.Y - geometry.Bounds.Top * heightRatio));
      Api.PushTransform(new ScaleTransform(widthRatio, heightRatio));
      Api.DrawGeometry(Brush(fill), pen, geometry);
      Api.Pop();
      Api.Pop();
   }

   public void DrawText(Point origin, double size, string text, string foreground)
      => Api.DrawText(FormattedText(text, size, foreground), origin);

   public void DrawText(Point origin, double preferredSize, double maxWidth, string text, string foreground) {
      var formattedText = FormattedText(text, preferredSize, foreground);
      if (formattedText.Width > maxWidth) formattedText = FormattedText(text, preferredSize * maxWidth / formattedText.Width, foreground);
      Api.DrawText(formattedText, origin);
   }

   public void DrawCheckbox(int x, int y, int size, bool isChecked, bool isHover) {
      var border = isHover ? nameof(Theme.Accent) : nameof(Theme.Secondary);
      var fill = nameof(Theme.Backlight);
      Api.DrawRectangle(Brush(fill), new Pen(Brush(border), 1), new(x, y, size, size));
      if (isChecked) {
         DrawIcon(new(x + 1, y - 2, size - 2, size + 2), nameof(Icons.Check), nameof(Theme.Accent));
      }
   }

   public void DrawTextButton(Rect rect, double fontSize, string text, bool isHover, bool isEnabled = true) {
      var border = isHover ? nameof(Theme.Primary) : nameof(Theme.Secondary);
      var fill = nameof(Theme.Backlight);
      var textFill = nameof(Theme.Primary);
      if (!isEnabled) { fill = nameof(Theme.Background); border = nameof(Theme.Secondary); textFill = nameof(Theme.Secondary); }
      Api.DrawRectangle(Brush(fill), new Pen(Brush(border), 1), rect);
      var formattedText = FormattedText(text, fontSize, textFill);
      if (formattedText.Width > rect.Width - 2) {
         fontSize *= (rect.Width - 2) / formattedText.Width;
         formattedText = FormattedText(text, fontSize, nameof(Theme.Primary));
      }
      Api.DrawText(formattedText, new(rect.X + rect.Width / 2 - formattedText.Width / 2, rect.Y));
   }

   /// <summary>
   /// Draws a squarish jump button based on the fontSize.
   /// </summary>
   public void DrawJumpButton(Point start, bool enabled, bool hover) {
      int width = CurrentFontSize, height = CurrentFontSize;
      Api.PushTransform(new TranslateTransform(start.X, start.Y));
      var background = enabled ? nameof(Theme.Backlight) : nameof(Theme.Background);
      var border = (hover && enabled) ? nameof(Theme.Primary) : nameof(Theme.Secondary);

      var content = $"M0,0 L {width-4},0 {width},{height/2} {width-4},{height} 0,{height} 4,{height/2} Z";
      Api.DrawGeometry(Brush(background), new Pen(Brush(border), 1), Geometry.Parse(content));

      content = $"M4,3 L {width-6},3 {width-4},{height/2} {width-6},{height-3} 4,{height-3} 6,{height/2} Z";
      Api.DrawGeometry(Brush(border), null, Geometry.Parse(content));

      Api.Pop();
   }

   public static FormattedText FormattedText(string text, double size, string foreground)
      => new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Consolas, size, Brush(foreground), 96);

   public static double GetDesiredFontSize(string text, double defaultSize, double maxWidth) {
      var formattedText = FormattedText(text, defaultSize, null);
      if (formattedText.Width <= maxWidth) return defaultSize;
      return defaultSize * maxWidth / formattedText.Width;
   }
}

/// <summary>
/// Represents a lightweight set of methods for rendering and interacting with a section of a table panel
/// </summary>
public interface IGroupControl {
   int YOffset { get; }
   int Width { get; }
   int Height { get; }
   int UpdateHeight(int availableWidth, int currentHeight, int fontSize);

   void Render(RenderContext context);

   void MouseEnter(TableGroupPanel parent, MouseEventArgs e);
   void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e);
   void MouseMove(TableGroupPanel parent, MouseEventArgs e); // if the mouse is down, MouseMove will continue activating on whatever element got clicked.
   void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e);
   void MouseExit(TableGroupPanel parent, MouseEventArgs e);

   void TextInput(TableGroupPanel parent, TextCompositionEventArgs e);
   void KeyInput(TableGroupPanel parent, KeyEventArgs e);
}

public abstract record GroupFixedHeighteControl() {
   public int YOffset { get; private set; }
   public int Width { get; private set; }
   public int Height { get; protected set; } = 20;
   public virtual int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      Width = availableWidth;
      YOffset = currentHeight;
      Height = fontSize + 4;
      return Height;
   }

   // helpers
   protected static string Primary { get; } = nameof(Theme.Primary);
   protected static string Accent { get; } = nameof(Theme.Accent);
   protected static string Secondary { get; } = nameof(Theme.Secondary);
   protected static string Background { get; } = nameof(Theme.Background);
   protected static string Backlight { get; } = nameof(Theme.Backlight);
}

public record GroupDefaultControl(IArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public void Render(RenderContext context) {
      context.DrawText(new(2, YOffset), context.CurrentFontSize, Element.GetType().Name, Primary);
   }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupTextControl(FieldArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   enum ControlSegment { None, Label, TextBox, Button }
   private ControlSegment mouseOver;

   public bool IsFocused { get; private set; }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) {
      var text = RenderContext.FormattedText(Element.Content, parent.FontSize, null);
      var labelAndContentWidth = Width - parent.FontSize - 2;
      var leftEdge = labelAndContentWidth - text.Width - 2;
      var characterWidth = text.Width / Element.Content.Length;

      var p = e.GetPosition(parent);
      var index = (p.X - leftEdge) / characterWidth + .5;
      index = (int)index.LimitToRange(0, Element.Content.Length);
      parent.CursorPosition = new((int)(leftEdge + index * characterWidth), YOffset + 1);
   }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) {
      var p = e.GetPosition(parent);
      parent.Cursor = p.X > Width / 2 ? Cursors.IBeam : Cursors.Arrow;

      if (e.LeftButton == MouseButtonState.Pressed) {
         // TODO mouse drag
      } else {
         // mouse hover
         var newHover = CalculateMouseOver(parent, new(p.X, p.Y - YOffset));
         if ((newHover == ControlSegment.Button) != (mouseOver == ControlSegment.Button)) {
            mouseOver = newHover;
            parent.InvalidateVisual(); // redraw for border
         }
      }

   }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) {
      // TODO end of mouse drag
      // TODO need the ability to actually respond to these application commands
      if (e.ChangedButton == MouseButton.Right) {
         parent.ContextMenu = new ContextMenu {
            Items = {
               new MenuItem { Command = ApplicationCommands.Cut },
               new MenuItem { Command = ApplicationCommands.Copy },
               new MenuItem { Command = ApplicationCommands.Paste },
               new Separator(),
               new MenuItem { Command = ApplicationCommands.SelectAll },
            },
         };
      }
   }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) {
      parent.Cursor = Cursors.Arrow;
      if (mouseOver == ControlSegment.Button) {
         mouseOver = ControlSegment.None;
         parent.InvalidateVisual();
      }
   }
   private ControlSegment CalculateMouseOver(TableGroupPanel parent, Point internalPoint) {
      var labelAndContentWidth = Width - parent.FontSize;
      if (internalPoint.X < labelAndContentWidth / 2) return ControlSegment.Label;
      if (internalPoint.X > labelAndContentWidth) return ControlSegment.Button;
      return ControlSegment.TextBox;
   }

   public void Render(RenderContext context) {
      var topOfText = YOffset;
      var labelAndContentWidth = Width - context.CurrentFontSize - 2;

      // label
      context.DrawText(new(2, topOfText + 2), context.CurrentFontSize - 4, labelAndContentWidth / 2, Element.Name, Primary);

      // box
      var pen = IsFocused ? context.AccentPen : null;
      var background = RenderContext.Brush(Backlight);
      context.Api.DrawRectangle(background, pen, new Rect(labelAndContentWidth / 2, YOffset + 1, labelAndContentWidth / 2, Height - 2));

      // content
      var text = RenderContext.FormattedText(Element.Content, context.CurrentFontSize, Primary);
      var textWidth = text.Width + 2;
      if (textWidth > labelAndContentWidth / 2) textWidth = labelAndContentWidth / 2; // TODO crop the text if this happens
      context.Api.DrawText(text, new(labelAndContentWidth - textWidth, topOfText));

      // goto button
      context.DrawJumpButton(new(Width - context.CurrentFontSize, topOfText + 2), Element.CanAccept(), mouseOver == ControlSegment.Button);
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupSplitterControl(SplitterArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private bool hover;
   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) => Height = base.UpdateHeight(availableWidth, currentHeight, fontSize) * 2;

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { hover = true; parent.InvalidateVisual(); }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { Element.ToggleVisibility.Execute(); parent.InvalidateVisual(); }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { hover = false; parent.InvalidateVisual(); }

   public void Render(RenderContext context) {
      var collapserRect = new Rect(4, YOffset + Height * 11 / 16, Height / 2 - 8, Height / 4 - 4);
      var border = hover ? Accent : null;
      if (Element.Visible) {
         context.DrawIcon(collapserRect, nameof(Icons.Chevron), Primary, border, .2);
      } else {
         context.DrawIcon(collapserRect, nameof(Icons.ChevronUp), Primary, border, .2);
      }
      context.DrawText(new(Height / 2, YOffset + Height / 2), context.CurrentFontSize, Width - Height / 2, Element.SectionName, Primary);
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupImageControl(SpriteElementViewModel Element, SpriteCache Cache) : GroupFixedHeighteControl(), IGroupControl {
   private int scale;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var multiple = (int)Math.Ceiling((double)Element.PixelHeight / unitHeight);
      if (multiple < 5) {
         scale = 2;
         multiple = (int)Math.Ceiling((double)Element.PixelHeight * 2 / unitHeight);
      } else {
         scale = 1;
      }

      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { }

   public void Render(RenderContext context) {
      var image = Cache.WriteUpdate(Element);
      context.Api.DrawImage(image, new Rect(0, YOffset, Element.PixelWidth * scale, Element.PixelHeight * scale));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupEnumControl(ComboBoxArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { }

   public void Render(RenderContext context) {
      // name
      context.DrawText(new(2, YOffset + 2), context.CurrentFontSize - 4, Width / 2, Element.Name, Primary);

      // box
      context.Api.DrawRectangle(RenderContext.Brush(Backlight), new Pen(RenderContext.Brush(Secondary), 1), new(Width / 2, YOffset + 1, Width / 2, Height - 2));
      var textSize = RenderContext.GetDesiredFontSize(Element.FilteringComboOptions.DisplayText, context.CurrentFontSize, Width / 2 - context.CurrentFontSize - 2);
      context.DrawText(new(Width / 2 + 2, YOffset + 1), textSize, Element.FilteringComboOptions.DisplayText, Primary);
      var unit = context.CurrentFontSize / 4;
      context.DrawIcon(new(Width - unit * 4, YOffset + unit + 2, unit * 4 - 2, unit * 2), nameof(Icons.Chevron), Primary);

      // TODO jump button
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupOffsetRenderControl(OffsetRenderViewModel Element, SpriteCache Cache) : GroupFixedHeighteControl(), IGroupControl {
   private double yStart = double.NaN;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var multiple = (int)Math.Ceiling((double)Element.PixelHeight / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) => parent.Cursor = Cursors.Hand;
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) => yStart = e.GetPosition(parent).Y;
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) {
      if (double.IsNaN(yStart)) return;
      var newY = e.GetPosition(parent).Y;
      var delta = (int)(newY - yStart);
      yStart += delta;
      Cache.NeedsRedraw(Element);
      Element.ShiftDelta(0, delta);
   }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) => yStart = double.NaN;
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) => parent.Cursor = Cursors.Arrow;

   public void Render(RenderContext context) {
      var image = Cache.WriteUpdate(Element);
      context.Api.DrawImage(image, new Rect(0, YOffset, Element.PixelWidth, Element.PixelHeight));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupSpriteIndicatorControl(SpriteIndicatorElementViewModel Element, SpriteCache Cache) : GroupFixedHeighteControl(), IGroupControl {
   private double scale = 1;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      scale = Math.Min(1, (double)availableWidth / Element.Image.PixelWidth);
      var multiple = (int)Math.Ceiling(Element.Image.PixelHeight * scale / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { }

   public void Render(RenderContext context) {
      var image = Cache.WriteUpdate(Element.Image);
      context.Api.DrawImage(image, new Rect(0, YOffset, Element.Image.PixelWidth * scale, Element.Image.PixelHeight * scale));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }

}

public record GroupTextStreamControl(TextStreamElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);

      var formattedContent = new FormattedText(Element.Content, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, RenderContext.Consolas, fontSize, RenderContext.Brush(Primary), 96);
      var minHeight = formattedContent.Height + 4;

      var multiple = (int)Math.Ceiling((double)minHeight / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { }

   public void Render(RenderContext context) {
      context.DrawRectangle(Backlight, Secondary, 0, YOffset, Width, Height);
      context.DrawText(new(2, YOffset + 2), context.CurrentFontSize, Element.Content, Primary);
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupPaletteControl(PaletteElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      var unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var blockHeight = Element.Colors.ColorHeight * (fontSize * 2 / 3 + 4);
      var multiple = (int)Math.Ceiling((double)blockHeight / unitHeight);
      return Height = unitHeight * multiple;
   }

   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { }

   public void Render(RenderContext context) {
      var colorWidth = Element.Colors.ColorWidth;
      var unitWidth = context.CurrentFontSize * 2 / 3 + 4;
      for (int y = 0; y < Element.Colors.ColorHeight; y++) {
         for (int x = 0; x < colorWidth; x++) {
            var color = Element.Colors.Elements[y * colorWidth + x];
            var fill = new SolidColorBrush(TileImage.Convert16BitColor(color.Color));
            var border = RenderContext.Brush(Primary);
            context.Api.DrawRectangle(fill, new Pen(border, 1), new Rect(unitWidth * x + 1, unitWidth * y + 1 + YOffset, unitWidth - 2, unitWidth - 2));
         }
      }
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }

   private int GetCell(TableGroupPanel parent, double x, double y) {
      var unitWidth = parent.FontSize * 2 / 3 + 4;
      var cellY = (int)((y - YOffset) / unitWidth).LimitToRange(0, Element.Colors.ColorHeight);
      var cellX = (int)(x / unitWidth).LimitToRange(0,Element.Colors.ColorWidth);
      return cellY * Element.Colors.ColorWidth + cellX;
   }
}

public record GroupBitArrayControl(BitListArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private int unitHeight, unitWidth, childrenPerLine;
   private BitElement hover, mouseClickElement;

   public override int UpdateHeight(int availableWidth, int currentHeight, int fontSize) {
      unitHeight = base.UpdateHeight(availableWidth, currentHeight, fontSize);
      var characterLength = Element.Max(child => child.BitLabel.Length);
      var sampleText = RenderContext.FormattedText(new string('X', characterLength), fontSize - 4, null);
      unitWidth = (int)(sampleText.Width + fontSize + 4);
      childrenPerLine = Math.Max(availableWidth / unitWidth, 1);
      var rows = (Element.Count - 1) / childrenPerLine + 1;
      return Height = unitHeight * (rows + 1);
   }
   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) => hover = null;
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) => mouseClickElement = GetBit(e.GetPosition(parent));
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) {
      var newHover = GetBit(e.GetPosition(parent));
      if (newHover != hover) parent.InvalidateVisual();
      hover = newHover;
   }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) {
      if (e.ChangedButton == MouseButton.Right) {
         parent.ContextMenu = new ContextMenu {
            Items = {
               new MenuItem { Header = "Select All", Command = Element.SelectAll },
               new MenuItem { Header = "Unselect All", Command = Element.UnselectAll },
            },
         };
      } else if (GetBit(e.GetPosition(parent)) == mouseClickElement && mouseClickElement != null) {
         mouseClickElement.IsChecked = !mouseClickElement.IsChecked;
      }
   }
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) => hover = null;

   public void Render(RenderContext context) {
      context.DrawText(new(0, YOffset + 2), context.CurrentFontSize, Element.Name, Primary);

      for (int i = 0; i < Element.Count; i++) {
         var xOffset = (i % childrenPerLine) * unitWidth;
         var yOffset = (i / childrenPerLine + 1) * unitHeight + YOffset;
         context.DrawCheckbox(xOffset + 2, yOffset + context.CurrentFontSize / 4, context.CurrentFontSize * 3 / 4, Element[i].IsChecked, Element[i] == hover);
         xOffset += context.CurrentFontSize + 2;
         context.DrawText(new(xOffset, yOffset + 4), context.CurrentFontSize - 4, Element[i].BitLabel, Primary);
      }
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }

   private BitElement GetBit(Point p) {
      p.Y -= YOffset + unitHeight;
      if (p.Y < 0) return null;
      var rows = (Element.Count - 1) / childrenPerLine + 1;
      var yy = (int)(p.Y / unitHeight).LimitToRange(0, rows - 1);
      var xx = (int)(p.X / unitWidth).LimitToRange(0, childrenPerLine - 1);
      var hover = yy * childrenPerLine + xx;
      if (hover >= Element.Count) {
         return null;
      } else {
         return Element[hover];
      }
   }
}

public record GroupButtonControl(ButtonArrayElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private bool isHover;
   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { isHover = true; parent.InvalidateVisual(); }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) => Element.Command.Execute();
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { isHover = false; parent.InvalidateVisual(); }

   public void Render(RenderContext context) {
      context.DrawTextButton(new Rect(2, YOffset + 2, Width - 4, Height - 4), context.CurrentFontSize - 2, Element.Text, isHover, Element.Command.CanExecute(null));
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}

public record GroupPythonButtonControl(PythonButtonElementViewModel Element) : GroupFixedHeighteControl(), IGroupControl {
   private bool isHover;
   public void MouseEnter(TableGroupPanel parent, MouseEventArgs e) { isHover = true; parent.InvalidateVisual(); }
   public void MouseDown(TableGroupPanel parent, MouseButtonEventArgs e) { }
   public void MouseMove(TableGroupPanel parent, MouseEventArgs e) { }
   public void MouseUp(TableGroupPanel parent, MouseButtonEventArgs e) => Element.Execute();
   public void MouseExit(TableGroupPanel parent, MouseEventArgs e) { isHover = false; parent.InvalidateVisual(); }

   public void Render(RenderContext context) {
      context.DrawTextButton(new Rect(2, YOffset + 2, Width - 4, Height - 4), context.CurrentFontSize - 2, Element.Name, isHover, Element.CanExecute());
   }

   public void KeyInput(TableGroupPanel parent, KeyEventArgs e) { }
   public void TextInput(TableGroupPanel parent, TextCompositionEventArgs e) { }
}
