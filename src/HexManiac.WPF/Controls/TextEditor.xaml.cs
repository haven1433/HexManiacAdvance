using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows.Controls;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TextEditor {
      public TextEditorViewModel ViewModel => (TextEditorViewModel)DataContext;
      public TextEditor() {
         InitializeComponent();
      }

      private void TextScrollChanged(object sender, ScrollChangedEventArgs e) {
         foreach (var layer in new[] { BasicLayer, AccentLayer, ConstantsLayer, CommentLayer }) {
            var transform = (TranslateTransform)layer.RenderTransform;
            transform.Y = -TransparentLayer.VerticalOffset;
         }
      }
   }
}
