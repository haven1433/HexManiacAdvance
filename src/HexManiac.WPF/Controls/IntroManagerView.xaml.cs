using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   /// <summary>The view of the Intro Manager tab. All the logic is in the view model: this only makes Enter and Escape work in the number boxes.</summary>
   public partial class IntroManagerView {
      public IntroManagerView() {
         InitializeComponent();
      }

      /// <summary>Enter keeps the number that was typed; Escape puts the number of the ROM back.</summary>
      private void NumberKeyDown(object sender, KeyEventArgs e) {
         if (sender is not TextBox box) return;
         if (e.Key == Key.Enter) {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Keyboard.ClearFocus();
            e.Handled = true;
         } else if (e.Key == Key.Escape) {
            if (box.DataContext is IntroSettingEditor editor) box.Text = editor.NumberText;
            else if (box.DataContext is IntroSpawnEditor spawn) box.Text = box.Tag as string == "Y" ? spawn.YText : spawn.XText;
            Keyboard.ClearFocus();
            e.Handled = true;
         }
      }
   }
}
