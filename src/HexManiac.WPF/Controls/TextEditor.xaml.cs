using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TextEditor {
      #region IsReadOnly

      public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TextEditor), new FrameworkPropertyMetadata(false, IsReadOnlyChanged));

      public bool IsReadOnly {
         get => (bool)GetValue(IsReadOnlyProperty);
         set => SetValue(IsReadOnlyProperty, value);
      }

      private static void IsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (TextEditor)d;
         self.OnIsReadOnlyChanged(e);
      }

      protected virtual void OnIsReadOnlyChanged(DependencyPropertyChangedEventArgs e) {
         TransparentLayer.IsReadOnly = IsReadOnly;
      }

      #endregion

      #region ContextMenuOverride

      public ContextMenu ContextMenuOverride {
         get { return (ContextMenu)GetValue(ContextMenuOverrideProperty); }
         set { SetValue(ContextMenuOverrideProperty, value); }
      }

      public static readonly DependencyProperty ContextMenuOverrideProperty = DependencyProperty.Register(nameof(ContextMenuOverride), typeof(ContextMenu), typeof(TextEditor), new PropertyMetadata(null));

      #endregion

      #region TextBox-Like properties

      public event RoutedEventHandler SelectionChanged;

      public double VerticalOffset => TransparentLayer.VerticalOffset;

      #endregion

      public TextEditorViewModel ViewModel => (TextEditorViewModel)DataContext;

      private IEnumerable<TextBlock> Layers => new[] { BasicLayer, AccentLayer, ConstantsLayer, NumericLayer, CommentLayer, TextLayer };

      public TextEditor() {
         InitializeComponent();
         TransparentLayer.SelectionChanged += (sender, e) => {
            SelectionChanged?.Invoke(this, e);
         };
         DataContextChanged += HandleDataContextChanged;
         // ExtentWidth is not a DependencyProperty, so check for the horizontal scroll bar when the text chanegs
         TransparentLayer.TextChanged += (sender, e) => {
            // measure the width of the text, since ExtentWidth hasn't been updated yet.
            var typeface = new Typeface(TransparentLayer.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var width = new FormattedText(TransparentLayer.Text, CultureInfo.CurrentCulture, FlowDirection, typeface, TransparentLayer.FontSize, Brushes.Transparent, 1).Width;
            foreach (var layer in Layers) layer.Width = width;
            if (width > TransparentLayer.ViewportWidth && TransparentLayer.ViewportHeight > TransparentLayer.ExtentHeight) {
               CornerCover.Width = 16;
               CornerCover.Height = 17;
            } else {
               CornerCover.Width = 0;
               CornerCover.Height = 0;
            }
         };
      }

      private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is TextEditorViewModel oldVM) {
            oldVM.PropertyChanged -= HandleViewModelPropertyChanged;
            oldVM.RequestCaretMove -= HandleViewModelCaretMove;
            oldVM.RequestKeyboardFocus -= HandleViewModelRequestKeyboardFocus;
            oldVM.ErrorLocations.CollectionChanged -= HandleViewModelErrorUpdate;
         }
         if (e.NewValue is TextEditorViewModel newVM) {
            newVM.PropertyChanged += HandleViewModelPropertyChanged;
            newVM.RequestCaretMove += HandleViewModelCaretMove;
            newVM.RequestKeyboardFocus += HandleViewModelRequestKeyboardFocus;
            newVM.ErrorLocations.CollectionChanged += HandleViewModelErrorUpdate;
            UpdateErrorDecorations();
         }
      }

      private void HandleViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(TextEditorViewModel.CommentContent)) {
            UpdateErrorDecorations();
         }
      }

      private void HandleViewModelCaretMove(object sender, EventArgs e) {
         var vm = (TextEditorViewModel)sender;
         if (TransparentLayer.CaretIndex != vm.CaretIndex) TransparentLayer.CaretIndex = vm.CaretIndex;
      }

      private void HandleViewModelRequestKeyboardFocus(object sender, EventArgs e) {
         RequestBringIntoView += SuppressBringIntoView;
         Keyboard.Focus(TransparentLayer);
         RequestBringIntoView -= SuppressBringIntoView;
      }

      private void HandleViewModelErrorUpdate(object sender, EventArgs e) => UpdateErrorDecorations();

      private void SuppressBringIntoView(object sender, RequestBringIntoViewEventArgs e) => e.Handled = true;

      public void ScrollToVerticalOffset(double offset) => TransparentLayer.ScrollToVerticalOffset(offset);

      protected override void OnMouseDoubleClick(MouseButtonEventArgs e) {
         base.OnMouseDoubleClick(e);

         // expand the selection left until the next whitespace
         var start = TransparentLayer.SelectionStart;
         var length = TransparentLayer.SelectionLength;
         while (start > 0 && !char.IsWhiteSpace(ViewModel.Content[start - 1])) { start--; length++; }
         while ((start + length - 1).InRange(0, ViewModel.Content.Length) && !char.IsWhiteSpace(ViewModel.Content[start + length - 1])) length++;
         TransparentLayer.Select(start, length);
      }

      private void TextScrollChanged(object sender, ScrollChangedEventArgs e) {
         foreach (var layer in Layers) {
            var transform = (TranslateTransform)layer.RenderTransform;
            transform.Y = -TransparentLayer.VerticalOffset;
            transform.X = -TransparentLayer.HorizontalOffset;
            layer.Width = TransparentLayer.ExtentWidth;
         }
      }

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }

      private void UpdateErrorDecorations() {
         var text = ViewModel.CommentContent;
         var inlines = CommentLayer.Inlines;
         inlines.Clear();
         int character = 0, line = 0;
         var geometry = Geometry.Parse("M0,0 L1,1 2,0");

         var errorBrush = Brush(nameof(Theme.Error));
         var errorPen = new Pen(new DrawingBrush(new GeometryDrawing(errorBrush, new(), geometry)) {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 3, 2)
         }, 2);
         var errorDecoration = new TextDecoration(TextDecorationLocation.Underline, errorPen, -1, TextDecorationUnit.Pixel, TextDecorationUnit.Pixel);

         var warningBrush = Brush(nameof(Theme.Data1));
         var warningPen = new Pen(new DrawingBrush(new GeometryDrawing(warningBrush, new(), geometry)) {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 3, 2)
         }, 2);
         var warningDecoration = new TextDecoration(TextDecorationLocation.Underline, warningPen, -1, TextDecorationUnit.Pixel, TextDecorationUnit.Pixel);

         var lineEnd = Environment.NewLine.ToCharArray().Last();

         foreach (var error in ViewModel.ErrorLocations) {
            var previousLines = character;
            while (line < error.Line && character < text.Length) {
               character++;
               if (text[character - 1] == lineEnd) line++;
            }

            if (error.Start + error.Length > text.Length - character) break;
            inlines.Add(new Run(text.Substring(previousLines, character - previousLines + error.Start)));
            if (error.Type == SegmentType.Error) {
               inlines.Add(new Run(text.Substring(character + error.Start, error.Length)) { TextDecorations = { errorDecoration } });
            } else if (error.Type == SegmentType.Warning) {
               inlines.Add(new Run(text.Substring(character + error.Start, error.Length)) { TextDecorations = { warningDecoration } });
            } else {
               throw new NotImplementedException();
            }

            character += error.Start + error.Length;
         }

         if (character < text.Length) inlines.Add(new Run(text.Substring(character)));
      }
   }
}
