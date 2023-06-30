using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
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

      public TextEditor() {
         InitializeComponent();
         TransparentLayer.SelectionChanged += (sender, e) => {
            SelectionChanged?.Invoke(this, e);
         };
         DataContextChanged += HandleDataContextChanged;
      }

      private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is TextEditorViewModel oldVM) {
            oldVM.RequestCaretMove -= HandleViewModelCaretMove;
            oldVM.RequestKeyboardFocus -= HandleViewModelRequestKeyboardFocus;
         }
         if (e.NewValue is TextEditorViewModel newVM) {
            newVM.RequestCaretMove += HandleViewModelCaretMove;
            newVM.RequestKeyboardFocus += HandleViewModelRequestKeyboardFocus;
         }
      }

      private void HandleViewModelCaretMove(object sender, EventArgs e) {
         var vm = (TextEditorViewModel)sender;
         TransparentLayer.CaretIndex = vm.CaretIndex;
      }

      private void HandleViewModelRequestKeyboardFocus(object sender, EventArgs e) => Keyboard.Focus(TransparentLayer);

      public void ScrollToVerticalOffset(double offset) => TransparentLayer.ScrollToVerticalOffset(offset);

      private void TextScrollChanged(object sender, ScrollChangedEventArgs e) {
         foreach (var layer in new[] { BasicLayer, AccentLayer, ConstantsLayer, NumericLayer, CommentLayer, TextLayer }) {
            var transform = (TranslateTransform)layer.RenderTransform;
            transform.Y = -TransparentLayer.VerticalOffset;
         }
      }
   }
}
