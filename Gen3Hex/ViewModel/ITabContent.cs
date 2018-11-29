using System;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// Each command expects an IFileSystem as its Command Parameter.
   /// </summary>
   public interface ITabContent {
      string Name { get; }
      ICommand Save { get; }   // parameter: IFileSystem
      ICommand SaveAs { get; } // parameter: IFileSystem
      ICommand Undo { get; }
      ICommand Redo { get; }
      ICommand Copy { get; }   // parameter: IFileSystem
      ICommand Clear { get; }
      ICommand Goto { get; }   // parameter: target destination as string (example, a hex address)
      ICommand Back { get; }
      ICommand Forward { get; }
      ICommand Close { get; }  // parameter: IFileSystem

      event EventHandler<string> OnError;
      event EventHandler Closed;
      event EventHandler<ITabContent> RequestTabChange;
   }
}
