using HavenSoft.HexManiac.Core.Models;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public interface IQuickEditItem {
      string Name { get; }
      string Description { get; }
      string WikiLink { get; }

      event EventHandler CanRunChanged;
      bool CanRun(IViewPort viewPort);
      Task<ErrorInfo> Run(IViewPort viewPort);
      void TabChanged();
   }
}
