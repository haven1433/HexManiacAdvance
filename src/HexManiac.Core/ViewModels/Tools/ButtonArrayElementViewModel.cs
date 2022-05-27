using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ButtonArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      public string Text { get; private set; }
      public string ToolTipText { get; private set; }
      public ICommand Command { get; private set; }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public ButtonArrayElementViewModel(string text, Action action) {
         Text = text;
         ToolTipText = text;
         Command = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => action(),
         };
      }

      public ButtonArrayElementViewModel(string text, string toolTip, Action action) {
         Text = text;
         ToolTipText = toolTip;
         Command = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => action(),
         };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is ButtonArrayElementViewModel button)) return false;
         if (Text != button.Text) return false;
         if (ToolTipText != button.ToolTipText) return false;
         Command = button.Command;
         Visible = other.Visible;
         NotifyPropertyChanged(nameof(Command));
         return true;
      }
   }
}
