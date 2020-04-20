using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.IO;
using System.Text;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum CodeMode { Thumb, Script, Raw }

   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      private string content;
      private CodeMode mode;
      private readonly ThumbParser thumb;
      private readonly ScriptParser script;
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;

      public event EventHandler<ErrorInfo> ModelDataChanged;

      public bool IsReadOnly => Mode != CodeMode.Script;

      public CodeMode Mode {
         get => mode;
         set {
            if (!TryUpdateEnum(ref mode, value)) return;
            UpdateContent();
            NotifyPropertyChanged(nameof(IsReadOnly));
         }
      }

      bool ignoreContentUpdates;
      public string Content {
         get => content;
         set {
            if (ignoreContentUpdates) return;
            TryUpdate(ref content, value);
            CompileChanges();
         }
      }

      public ThumbParser Parser => thumb;

      public ScriptParser ScriptParser => script;

      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      public CodeTool(Singletons singletons, IDataModel model, Selection selection, ChangeHistory<ModelDelta> history) {
         thumb = new ThumbParser(singletons);
         script = new ScriptParser(singletons.ScriptLines);
         this.model = model;
         this.selection = selection;
         this.history = history;
         selection.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(selection.SelectionEnd)) {
               UpdateContent();
            }
         };
      }

      public void UpdateContent() {
         if (ignoreContentUpdates) return;
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));

         if (start > end) (start, end) = (end, start);
         int length = end - start + 1;

         using (ModelCacheScope.CreateScope(model)) {
            if (length > 0x1000) {
               Content = "Too many bytes selected.";
            } else if (mode == CodeMode.Raw) {
               Content = RawParse(model, start, end - start + 1);
            } else if (length < 2) {
               TryUpdate(ref content, string.Empty, nameof(Content));
            } else if (mode == CodeMode.Script) {
               TryUpdate(ref content, script.Parse(model, start, end - start + 1), nameof(Content));
            } else if (mode == CodeMode.Thumb) {
               TryUpdate(ref content, thumb.Parse(model, start, end - start + 1), nameof(Content));
            } else {
               throw new NotImplementedException();
            }
         }
      }

      private void CompileChanges() {
         using (ModelCacheScope.CreateScope(model)) {
            if (mode == CodeMode.Thumb) CompileThumbChanges();
            if (mode == CodeMode.Script) CompileScriptChanges();
         }
      }

      private void CompileThumbChanges() {
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
         if (start > end) (start, end) = (end, start);
         int length = end - start + 1;
         var code = thumb.Compile(model, start, Content.Split(Environment.NewLine));

         if (code.Count != length) return;

         for (int i = 0; i < code.Count; i++) {
            history.CurrentChange.ChangeData(model, start + i, code[i]);
         }

         ModelDataChanged?.Invoke(this, ErrorInfo.NoError);
      }

      private void CompileScriptChanges() {
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
         if (start > end) (start, end) = (end, start);

         var run = model.GetNextRun(start) as XSERun;
         if (run == null || run.Start != start) return;

         ignoreContentUpdates = true;
         {
            int length = end - start + 1;
            var code = script.Compile(model, Content);

            if (code.Length > length) {
               run = (XSERun)model.RelocateForExpansion(history.CurrentChange, run, code.Length);
               ModelDataMoved?.Invoke(this, (start, run.Start));
            }

            model.ClearFormat(history.CurrentChange, start, length);
            for (int i = 0; i < code.Length; i++) history.CurrentChange.ChangeData(model, run.Start + i, code[i]);
            for (int i = code.Length; i < length; i++) history.CurrentChange.ChangeData(model, run.Start + i, 0xFF);
            ScriptParser.FormatScript(history.CurrentChange, model, start);

            selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(run.Start);
            selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(run.Start + code.Length - 1);
         }
         ignoreContentUpdates = false;

         ModelDataChanged?.Invoke(this, ErrorInfo.NoError);
      }

      private string RawParse(IDataModel model, int start, int length) {
         var builder = new StringBuilder();
         while (length > 0) {
            builder.Append(model[start].ToHexString());
            builder.Append(" ");
            length--;
            start++;
            if (start % 16 == 0) builder.AppendLine();
         }
         return builder.ToString();
      }
   }
}
