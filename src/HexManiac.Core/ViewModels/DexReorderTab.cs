using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DexReorderTab : ITabContent {
      private IDataModel model;
      private ITableRun dexOrder;
      private ITableRun dexInfo;

      public DexReorderTab(IDataModel model, ITableRun dexOrder, ITableRun dexInfo) {
         this.model = model;
         this.dexOrder = dexOrder;
         this.dexInfo = dexInfo;
      }

      public string Name => "Adjust Dex Order";

      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs { get; } = new StubCommand();
      public ICommand Undo { get; } = new StubCommand();
      public ICommand Redo { get; } = new StubCommand();
      public ICommand Copy { get; } = new StubCommand();
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close { get; } = new StubCommand();

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event PropertyChangedEventHandler PropertyChanged;

      public void Refresh() { }
   }
}
