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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls; 

public partial class TableGroupPanel : FrameworkElement {
   private readonly SpriteCache spriteCache = new();

   private IArrayElementViewModel focusElement;

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
      }
      var newSource = (ObservableCollection<IArrayElementViewModel>)e.NewValue;
      if (newSource != null) {
         newSource.CollectionChanged += CollectionChanged;
         foreach (var element in newSource) element.PropertyChanged += CollectionPropertyChanged;
      }
      InvalidateVisual();
   }

   private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      if (e.Action.IsAny(NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Remove)) {
         foreach (IArrayElementViewModel item in e.OldItems) item.PropertyChanged -= CollectionPropertyChanged;
      }
      if (e.Action.IsAny(NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace)) {
         foreach (IArrayElementViewModel item in e.NewItems) item.PropertyChanged += CollectionPropertyChanged;
      }
      if (e.Action.IsAny(NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Move)) {
         foreach (var element in Source) {
            element.PropertyChanged -= CollectionPropertyChanged;
            element.PropertyChanged += CollectionPropertyChanged;
         }
      }
      InvalidateVisual();
   }

   private void CollectionPropertyChanged(object? sender, PropertyChangedEventArgs e) {
      if (sender is SpriteElementViewModel sprite) spriteCache.NeedsRedraw(sprite);
      InvalidateVisual();
   }

   #endregion

   public TableGroupPanel() {
      RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
      ClipToBounds = true;
      Focusable = true;
   }

   protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
      base.OnLostKeyboardFocus(e);
      if ((focusElement != null)) {
         focusElement = null;
         InvalidateVisual();
      }
   }

   protected override void OnRender(DrawingContext dc) {
      base.OnRender(dc);
      if (Source == null) return;
      var renderer = new TableGroupPainter(dc, spriteCache, (int)ActualWidth, focusElement);
      foreach (var member in Source) {
         renderer.Render(member);
      }
   }

   protected override Size MeasureOverride(Size availableSize) {
      // how big do I want to be?
      var painter = new TableGroupPainter(null, spriteCache, (int)availableSize.Width, focusElement);
      foreach (var member in Source) painter.Render(member);
      return new Size(Math.Max(availableSize.Width, 100), painter.Y);
   }

   protected override Size ArrangeOverride(Size finalSize) {
      // how big do I actually get to be
      var size = base.ArrangeOverride(finalSize);
      var painter = new TableGroupPainter(null, spriteCache, (int)size.Width, focusElement);
      foreach (var member in Source) painter.Render(member);
      return new Size(Math.Max(size.Width, 100), painter.Y);
   }

   protected override void OnMouseDown(MouseButtonEventArgs e) {
      base.OnMouseDown(e);
      var pos = e.GetPosition(this);
      var painter = new TableGroupPainter(null, spriteCache, (int)ActualWidth, focusElement);
      focusElement = null;
      foreach (var member in Source) {
         painter.Render(member);
         if (painter.Y < pos.Y) continue;
         focusElement = member;
         pos.X -= painter.X;
         pos.Y -= painter.Y;
         new TableGroupMouseEditor(focusElement, painter.AvailableWidth, pos, e).Edit();
         Focus();
         InvalidateVisual();
         e.Handled = true;
         break;
      }
   }

   protected override void OnTextInput(TextCompositionEventArgs e) {
      base.OnTextInput(e);
      if (focusElement == null) return;
      new TableGroupTextEditor(focusElement, (int)ActualWidth, e).Edit();
      e.Handled = true;
   }

   protected override void OnKeyDown(KeyEventArgs e) {
      base.OnKeyDown(e);
      if (focusElement == null) return;
      if (e.Key == Key.Tab) {
         var index = Source.IndexOf(focusElement);
         if (index != -1) {
            index += 1;
            if (Keyboard.Modifiers == ModifierKeys.Shift) index -= 2;
            if (index < 0) index += Source.Count;
            if (index >= Source.Count) index -= Source.Count;
            focusElement = Source[index];
            e.Handled = true;
            InvalidateVisual();
            return;
         }
      }
      var editor = new TableGroupKeyEditor(this, (int)ActualWidth, e);
      editor.Edit();
   }

   private static SolidColorBrush Brush(string name) {
      return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
   }

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

   public void NeedsRedraw(SpriteElementViewModel sprite) {
      var cacheIndex = pvmCache.IndexOf(sprite);
      if (cacheIndex >= 0) cacheNeedsRedraw |= 1L << cacheIndex;
   }
}
