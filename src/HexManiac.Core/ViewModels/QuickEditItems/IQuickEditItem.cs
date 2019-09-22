using HavenSoft.HexManiac.Core.Models;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public interface IQuickEditItem {
      string Name { get; }
      string Description { get; }

      event EventHandler CanRunChanged;
      bool CanRun(IViewPort viewPort);
      ErrorInfo Run(IViewPort viewPort);
      void TabChanged();
   }
}
