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

      public TextStreamElementViewModel(ViewPort viewPort, IDataModel model, int start) : base(viewPort, start) {
         var destination = model.ReadPointer(Start);
         var run = model.GetNextRun(destination) as IStreamRun;

         if (run != null) {
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
