using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;

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
               }
            }
         }
      }

      public TextStreamElementViewModel(ViewPort viewPort, IDataModel model, int start, string format) : base(viewPort, format ?? PCSRun.SharedFormatString, start) {
         var destination = model.ReadPointer(Start);
         if (model.GetNextRun(destination) is IStreamRun run) {
            content = run.SerializeRun() ?? string.Empty;
         } else {
            content = string.Empty;
         }
      }

      protected override bool TryCopy(StreamElementViewModel other) {
         if (!(other is TextStreamElementViewModel stream)) return false;
         TryUpdate(ref content, stream.content, nameof(Content));
         return true;
      }
   }
}
