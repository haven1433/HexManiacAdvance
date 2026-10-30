using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Windows {
   public partial class RequestTextDialog {
      public string Result { get; set; }
      public RequestTextDialog() => InitializeComponent();

      protected override void OnActivated(EventArgs e) {
         base.OnActivated(e);
         Keyboard.Focus(TextBox);
      }

      private void AcceptText(object sender, ExecutedRoutedEventArgs e) {
         Result = TextBox.Text;
         DialogResult = true;
      }
   }
}
