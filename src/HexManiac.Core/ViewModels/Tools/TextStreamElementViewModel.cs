using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TextStreamElementViewModel : StreamElementViewModel {
      public string content;

      public IReadOnlyList<IPixelViewModel> Visualizations {
         get {
            var destination = Model.ReadPointer(Start);
            if (Model.GetNextRun(destination) is IStreamRun run) {
               return run.Visualizations;
            }
            return new List<IPixelViewModel>();
         }
      }
   }
}
