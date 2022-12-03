using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows;
using System.Windows.Controls;
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

      #region TextBox-Like properties

      public event RoutedEventHandler SelectionChanged;

      public double VerticalOffset => TransparentLayer.VerticalOffset;
      //public int SelectionStart => TransparentLayer.SelectionStart;
      //public string SelectedText => TransparentLayer.SelectedText;
      //public string Text => TransparentLayer.Text;
      //public double ExtentHeight => TransparentLayer.ExtentHeight;

      #endregion

      public TextEditorViewModel ViewModel => (TextEditorViewModel)DataContext;
      public TextEditor() {
         InitializeComponent();
         TransparentLayer.SelectionChanged += (sender, e) => SelectionChanged?.Invoke(this, e);
      }

      public void ScrollToVerticalOffset(double offset) => TransparentLayer.ScrollToVerticalOffset(offset);

      private void TextScrollChanged(object sender, ScrollChangedEventArgs e) {
         foreach (var layer in new[] { BasicLayer, AccentLayer, ConstantsLayer, NumericLayer, CommentLayer, TextLayer }) {
            var transform = (TranslateTransform)layer.RenderTransform;
            transform.Y = -TransparentLayer.VerticalOffset;
         }
      }
   }
}
