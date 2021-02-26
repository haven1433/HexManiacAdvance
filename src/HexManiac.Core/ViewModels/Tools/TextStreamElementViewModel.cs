using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TextStreamElementViewModel : StreamElementViewModel {
      string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               using (ModelCacheScope.CreateScope(Model)) {
                  var destination = Model.ReadPointer(Start);
                  var run = (IStreamRun)Model.GetNextRun(destination);
                  var newRun = run.DeserializeRun(content, ViewPort.CurrentChange);
                  Model.ObserveRunWritten(ViewPort.CurrentChange, newRun);
                  if (run.Start != newRun.Start) {
                     RaiseDataMoved(run.Start, newRun.Start);
                  }
                  using (PreventSelfCopy()) {
                     RaiseDataChanged();
                  }
                  NotifyPropertyChanged(nameof(Visualizations));
               }
            }
         }
      }

      public IReadOnlyList<IPixelViewModel> Visualizations {
         get {
            var destination = Model.ReadPointer(Start);
            if (Model.GetNextRun(destination) is IStreamRun run) {
               return run.Visualizations;
            }
            return new List<IPixelViewModel>();
         }
      }

      public TextStreamElementViewModel(ViewPort viewPort, int start, string format) : base(viewPort, format ?? PCSRun.SharedFormatString, start) {
         var destination = viewPort.Model.ReadPointer(Start);
         if (viewPort.Model.GetNextRun(destination) is IStreamRun run) {
            content = run.SerializeRun() ?? string.Empty;
         } else {
            content = string.Empty;
         }
      }

      protected override bool TryCopy(StreamElementViewModel other) {
         if (!(other is TextStreamElementViewModel stream)) return false;
         Start = other.Start;
         TryUpdate(ref content, stream.content, nameof(Content));
         NotifyPropertyChanged(nameof(Visualizations));
         return true;
      }
   }
}
