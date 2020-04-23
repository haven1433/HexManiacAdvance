using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum CodeMode { Thumb, Script, Raw }

   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      private bool useMultiScriptContent = true; // feature toggle

      private string content;
      private CodeMode mode;
      private readonly ThumbParser thumb;
      private readonly ScriptParser script;
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;

      public event EventHandler<ErrorInfo> ModelDataChanged;

      public bool IsReadOnly => Mode != CodeMode.Script;
      public bool UseSingleContent => !UseMultiContent;
      public bool UseMultiContent => Mode == CodeMode.Script && useMultiScriptContent;

      private bool showErrorText;
      public bool ShowErrorText { get => showErrorText; private set => TryUpdate(ref showErrorText, value); }

      private string errorText;
      public string ErrorText { get => errorText; private set => TryUpdate(ref errorText, value); }

      public CodeMode Mode {
         get => mode;
         set {
            if (!TryUpdateEnum(ref mode, value)) return;
            UpdateContent();
            NotifyPropertyChanged(nameof(IsReadOnly));
            NotifyPropertyChanged(nameof(UseSingleContent));
            NotifyPropertyChanged(nameof(UseMultiContent));
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

      public ObservableCollection<CodeBody> Contents { get; } = new ObservableCollection<CodeBody>();

      public ThumbParser Parser => thumb;

      public ScriptParser ScriptParser => script;

      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      public CodeTool(Singletons singletons, IDataModel model, Selection selection, ChangeHistory<ModelDelta> history) {
         thumb = new ThumbParser(singletons);
         script = new ScriptParser(singletons.ScriptLines);
         script.CompileError += ObserveCompileError;
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
            } else if (length < 2 && mode != CodeMode.Script) {
               TryUpdate(ref content, string.Empty, nameof(Content));
               UpdateContents(-1);
            } else if (mode == CodeMode.Script) {
               if (useMultiScriptContent) {
                  UpdateContents(start);
               } else {
                  TryUpdate(ref content, script.Parse(model, start, end - start + 1), nameof(Content));
               }
            } else if (mode == CodeMode.Thumb) {
               TryUpdate(ref content, thumb.Parse(model, start, end - start + 1), nameof(Content));
            } else {
               throw new NotImplementedException();
            }
         }
      }

      /// <summary>
      /// Update all the content objects.
      /// If one of the content objects is the one being changed, don't update that one.
      /// </summary>
      /// <param name="start"></param>
      /// <param name="currentScriptStart"></param>
      private void UpdateContents(int start, int currentScriptStart = -1) {
         var scripts = script.CollectScripts(model, start);
         for (int i = 0; i < scripts.Count; i++) {
            var scriptStart = scripts[i];
            if (scriptStart == currentScriptStart && Contents.Count > i && Contents[i].Address == scriptStart) continue;
            var scriptLength = script.FindLength(model, scriptStart);
            var label = scriptStart.ToString("X6");
            var content = script.Parse(model, scriptStart, scriptLength);
            var body = new CodeBody { Address = scriptStart, Label = label, Content = content };

            if (Contents.Count > i) {
               Contents[i].ContentChanged -= ScriptChanged;
               Contents[i].Content = body.Content;
               Contents[i].Address = body.Address;
               Contents[i].Label = body.Label;
               Contents[i].ContentChanged += ScriptChanged;
            } else {
               body.ContentChanged += ScriptChanged;
               Contents.Add(body);
            }
         }

         while (Contents.Count > scripts.Count) {
            Contents[Contents.Count - 1].ContentChanged -= ScriptChanged;
            Contents.RemoveAt(Contents.Count - 1);
         }
      }

      private void ScriptChanged(object viewModel, EventArgs e) {
         var body = (CodeBody)viewModel;
         var codeContent = body.Content;

         var run = model.GetNextRun(body.Address) as XSERun;
         if (run == null || run.Start != body.Address) Debug.Fail("How did this happen?");

         int length = script.FindLength(model, run.Start);
         using (ModelCacheScope.CreateScope(model)) {
            CompileScriptChanges(run, length, ref codeContent, body == Contents[0]);

            body.ContentChanged -= ScriptChanged;
            body.Content = codeContent;
            body.ContentChanged += ScriptChanged;

            // reload
            var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
            var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
            if (start > end) (start, end) = (end, start);
            UpdateContents(start, body.Address);
         }
      }

      private void CompileChanges() {
         using (ModelCacheScope.CreateScope(model)) {
            if (mode == CodeMode.Thumb) CompileThumbChanges();
            if (mode == CodeMode.Script && !useMultiScriptContent) CompileScriptChanges();
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
         int length = end - start + 1;

         string codeContent = Content;
         CompileScriptChanges(run, length, ref codeContent, true);
         TryUpdate(ref content, codeContent, nameof(Content));
      }

      private void CompileScriptChanges(XSERun run, int length, ref string codeContent, bool updateSelection) {
         ShowErrorText = false;
         ErrorText = string.Empty;
         int start = run.Start;

         ignoreContentUpdates = true;
         {
            var oldScripts = script.CollectScripts(model, run.Start);
            var code = script.Compile(history.CurrentChange, model, ref codeContent, out var movedData);
            if (code == null) {
               ignoreContentUpdates = false;
               return;
            }

            if (code.Length > length) {
               run = (XSERun)model.RelocateForExpansion(history.CurrentChange, run, code.Length);
               ModelDataMoved?.Invoke(this, (start, run.Start));
            }

            model.ClearAnchor(history.CurrentChange, start, length);
            for (int i = 0; i < code.Length; i++) history.CurrentChange.ChangeData(model, run.Start + i, code[i]);
            for (int i = code.Length; i < length; i++) history.CurrentChange.ChangeData(model, run.Start + i, 0xFF);
            script.FormatScript(history.CurrentChange, model, run.Start, run.PointerSources);
            foreach (var source in run.PointerSources) model.ObserveRunWritten(history.CurrentChange, new PointerRun(source));

            // this change may have orphaned some existing scripts. Don't lose them!
            var newScripts = script.CollectScripts(model, run.Start);
            foreach (var orphan in oldScripts.Except(newScripts)) {
               var orphanRun = model.GetNextRun(orphan);
               if (orphanRun.Start == orphan && string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, orphan))) {
                  script.FormatScript(history.CurrentChange, model, orphan);
                  model.ObserveAnchorWritten(history.CurrentChange, $"xse{orphan:X6}", new XSERun(orphan));
               }
            }

            if (updateSelection) {
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(run.Start);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(run.Start + code.Length - 1);
            }

            foreach (var movedResource in movedData) ModelDataMoved?.Invoke(this, movedResource);
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

      private void ObserveCompileError(object sender, string error) {
         ShowErrorText = true;
         ErrorText += error + Environment.NewLine;
      }
   }

   public class CodeBody : ViewModelCore {
      public event EventHandler ContentChanged;

      private string label;
      public string Label {
         get => label;
         set => TryUpdate(ref label, value);
      }

      private int address;
      public int Address {
         get => address;
         set => TryUpdate(ref address, value);
      }

      private string content;
      public string Content {
         get => content;
         set {
            if (!TryUpdate(ref content, value)) return;
            ContentChanged?.Invoke(this, EventArgs.Empty);
         }
      }
   }
}
