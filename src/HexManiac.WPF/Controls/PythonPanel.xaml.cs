using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class PythonPanel {
      public PythonPanel() => InitializeComponent();

      private void PythonTextKeyDown(object sender, KeyEventArgs e) {
         if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) {
            e.Handled = true;
            if (DataContext is not PythonTool tool) return;
            tool.RunPython();
         }
      }
   }
}
