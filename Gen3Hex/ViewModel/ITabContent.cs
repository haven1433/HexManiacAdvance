using System;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// Each command expects an IFileSystem as its Command Parameter.
   /// </summary>
   public interface ITabContent {
      string Name { get; }
      ICommand Save { get; }
      ICommand SaveAs { get; }
      ICommand Undo { get; }
      ICommand Redo { get; }
      ICommand Copy { get; }
      ICommand Clear { get; }
      ICommand Goto { get; }
      ICommand Back { get; }
      ICommand Forward { get; }
      ICommand Close { get; }

      event EventHandler<string> OnError;
      event EventHandler Closed;
   }
}
