using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Windows {
   public partial class ThemeSelector {
      public ThemeSelector() => InitializeComponent();

      private void ClearKeyboardFocus(object sender, EventArgs e) => Keyboard.ClearFocus();
   }
}
