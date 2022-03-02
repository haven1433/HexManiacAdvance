using HavenSoft.HexManiac.Core.Models;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// Each command expects an IFileSystem as its Command Parameter.
   /// </summary>
   public interface ITabContent : INotifyPropertyChanged {
      string Name { get; }
      bool IsMetadataOnlyChange { get; }
      ICommand Save { get; }   // parameter: IFileSystem
      ICommand SaveAs { get; } // parameter: IFileSystem
      ICommand ExportBackup { get; } // parameter: IFileSystem
      ICommand Undo { get; }
      ICommand Redo { get; }
      ICommand Copy { get; }   // parameter: IFileSystem
      ICommand DeepCopy { get; }//parameter: IFileSystem
      ICommand Clear { get; }
      ICommand SelectAll { get; }
      ICommand Goto { get; }   // parameter: target destination as string (example, a hex address)
      ICommand ResetAlignment { get; }
      ICommand Back { get; }
      ICommand Forward { get; }
      ICommand Close { get; }  // parameter: IFileSystem
      ICommand Diff { get; }   // parameter: the tab to diff with. If null, then diff with self since last save
      ICommand DiffLeft { get; }  // send a request to the editor to diff this tab with the tab on the left
      ICommand DiffRight { get; } // send a request to the editor to diff this tab with the tab on the right

      event EventHandler<string> OnError;
      event EventHandler<string> OnMessage;
      event EventHandler ClearMessage;
      event EventHandler Closed;
      event EventHandler<ITabContent> RequestTabChange;
      event EventHandler<Func<Task>> RequestDelayedWork;
      event EventHandler RequestMenuClose;
      event EventHandler<Direction> RequestDiff;
      event EventHandler<CanDiffEventArgs> RequestCanDiff;

      bool CanDuplicate { get; }
      void Duplicate();

      void Refresh();
      bool TryImport(LoadedFile file, IFileSystem fileSystem);
   }

   public interface IRaiseMessageTab : ITabContent {
      void RaiseMessage(string message);
   }

   public class CanDiffEventArgs : EventArgs {
      public Direction Direction { get; }
      public bool Result { get; set; }
      public CanDiffEventArgs(Direction d) => Direction = d;
   }
}
