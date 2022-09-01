using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum CodeMode { Thumb, Script, BattleScript, AnimationScript, Raw }

   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      private string content;
      private CodeMode mode;
      private readonly ThumbParser thumb;
      private readonly ScriptParser script, battleScript, animationScript;
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IRaiseMessageTab messageTab;

      public event EventHandler<ErrorInfo> ModelDataChanged;

      public bool IsReadOnly => Mode == CodeMode.Raw;
      public bool UseSingleContent => !UseMultiContent;
      public bool UseMultiContent => Mode.IsAny(CodeMode.Script, CodeMode.BattleScript, CodeMode.AnimationScript);

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

      public ScriptParser BattleScriptParser => battleScript;

      public ScriptParser AnimationScriptParser => animationScript;

      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      private StubCommand isEventScript;
      public ICommand IsEventScript => StubCommand(ref isEventScript, ExecuteIsEventScript);
      private void ExecuteIsEventScript() {
         var searchPoint = selection.Scroll.ViewPointToDataIndex(selection.SelectionStart);
         ScriptParser.FormatScript<XSERun>(history.CurrentChange, model, searchPoint);
      }

      // properties that exist solely so the UI can remember things when the tab switches
      public double SingleBoxVerticalOffset { get; set; }
      public double MultiBoxVerticalOffset { get; set; }

      public CodeTool(Singletons singletons, IDataModel model, Selection selection, ChangeHistory<ModelDelta> history, IRaiseMessageTab messageTab) {
         thumb = new ThumbParser(singletons);
         script = new ScriptParser(singletons.ScriptLines, 0x02);
         battleScript = new ScriptParser(singletons.BattleScriptLines, 0x3D);
         animationScript = new ScriptParser(singletons.AnimationScriptLines, 0x08);
         script.CompileError += ObserveCompileError;
         battleScript.CompileError += ObserveCompileError;
         animationScript.CompileError += ObserveCompileError;
         this.model = model;
         this.selection = selection;
         this.history = history;
         this.messageTab = messageTab;
         selection.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(selection.SelectionEnd)) {
               UpdateContent();
            }
         };
      }

      public void DataForCurrentRunChanged() { }

      public void UpdateContent() {
         if (ignoreContentUpdates) return;
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));

         if (start > end) (start, end) = (end, start);
         int length = end - start + 1;

         using (ModelCacheScope.CreateScope(model)) {
            if (length > 0x1000) {
               Set(ref content, "Too many bytes selected.", nameof(Content));
            } else if (mode == CodeMode.Raw) {
               Content = RawParse(model, start, end - start + 1);
            } else if (length < 2 && mode == CodeMode.Thumb) {
               TryUpdate(ref content, string.Empty, nameof(Content));
               UpdateContents(-1, null);
               CanRepointThumb = CalculateCanRepointThumb();
            } else if (mode == CodeMode.Script) {
               UpdateContents(start, script);
            } else if (mode == CodeMode.BattleScript) {
               UpdateContents(start, battleScript);
            } else if (mode == CodeMode.AnimationScript) {
               UpdateContents(start, animationScript);
            } else if (mode == CodeMode.Thumb) {
               TryUpdate(ref content, thumb.Parse(model, start, end - start + 1), nameof(Content));
               CanRepointThumb = CalculateCanRepointThumb();
            } else {
               throw new NotImplementedException();
            }
         }
      }

      #region RepointThumb

      private bool CalculateCanRepointThumb() {
         int left = selection.Scroll.ViewPointToDataIndex(selection.SelectionStart);
         int right = selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd);
         if (left > right) (left, right) = (right, left);
         var length = right - left + 1;
         return Parser.CanRepoint(model, left, length) != -1;
      }

      private bool canRepointThumb;
      public bool CanRepointThumb { get => canRepointThumb; private set => Set(ref canRepointThumb, value); }

      public void RepointThumb() {
         int left = selection.Scroll.ViewPointToDataIndex(selection.SelectionStart);
         int right = selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd);
         if (left > right) (left, right) = (right, left);
         int register = Parser.CanRepoint(model, left, right - left + 1);
         if (register != -1) {
            var newAddress = Parser.Repoint(history.CurrentChange, model, left, register);
            messageTab.RaiseMessage($"Thumb code repointed to {newAddress:X6}");
            selection.Goto.Execute(newAddress);
            selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(newAddress + 0x13);
         }
      }

      #endregion

      /// <summary>
      /// Update all the content objects.
      /// If one of the content objects is the one being changed, don't update that one.
      /// </summary>
      /// <param name="start"></param>
      /// <param name="currentScriptStart"></param>
      private void UpdateContents(int start, ScriptParser parser, int currentScriptStart = -1, int currentScriptLength = -1) {
         if (currentScriptStart == -1) {
            ShowErrorText = false;
            ErrorText = string.Empty;
         }

         var scripts = parser?.CollectScripts(model, start) ?? new List<int>();
         int skippedScripts = 0;
         for (int i = 0; i < scripts.Count; i++) {
            var scriptStart = scripts[i];
            if (scriptStart == currentScriptStart && Contents.Count > i && Contents[i].Address == scriptStart) continue;
            if (scriptStart > currentScriptStart && scriptStart < currentScriptStart + currentScriptLength) {
               // this script is included inside the current under-edit script
               // it doesn't need its own content
               skippedScripts += 1;
               continue;
            }
            var scriptLength = parser.FindLength(model, scriptStart);
            var label = scriptStart.ToString("X6");
            var content = parser.Parse(model, scriptStart, scriptLength);
            var body = new CodeBody { Address = scriptStart, Label = label, Content = content, CompiledLength = scriptLength };

            if (Contents.Count > i) {
               Contents[i].ContentChanged -= ScriptChanged;
               Contents[i].HelpSourceChanged -= UpdateScriptHelpFromLine;
               Contents[i].Content = body.Content;
               Contents[i].Address = body.Address;
               Contents[i].CompiledLength = body.CompiledLength;
               Contents[i].Label = body.Label;
               Contents[i].HelpSourceChanged += UpdateScriptHelpFromLine;
               Contents[i].ContentChanged += ScriptChanged;
            } else {
               body.ContentChanged += ScriptChanged;
               body.HelpSourceChanged += UpdateScriptHelpFromLine;
               Contents.Add(body);
            }
         }

         while (Contents.Count > scripts.Count - skippedScripts) {
            Contents[Contents.Count - 1].ContentChanged -= ScriptChanged;
            Contents.RemoveAt(Contents.Count - 1);
         }
      }

      private void ScriptChanged(object viewModel, EventArgs e) {
         var parser = mode == CodeMode.Script ? script : mode == CodeMode.BattleScript ? battleScript : animationScript;
         var body = (CodeBody)viewModel;
         var codeContent = body.Content;

         var run = model.GetNextRun(body.Address);
         if (run != null && run.Start != body.Address) run = null;

         int length = parser.FindLength(model, body.Address);
         using (ModelCacheScope.CreateScope(model)) {
            if (mode == CodeMode.Script) {
               CompileScriptChanges<XSERun>(body, run, ref codeContent, parser, body == Contents[0]);
            } else if (mode == CodeMode.AnimationScript) {
               CompileScriptChanges<ASERun>(body, run, ref codeContent, parser, body == Contents[0]);
            } else {
               CompileScriptChanges<BSERun>(body, run, ref codeContent, parser, body == Contents[0]);
            }

            body.ContentChanged -= ScriptChanged;
            body.HelpSourceChanged -= UpdateScriptHelpFromLine;
            body.Content = codeContent;
            body.HelpSourceChanged += UpdateScriptHelpFromLine;
            body.ContentChanged += ScriptChanged;

            // reload
            var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
            var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
            if (start > end) (start, end) = (end, start);
            UpdateContents(start, parser, body.Address, end - start + 1);
         }
      }

      private void UpdateScriptHelpFromLine(object sender, string line) {
         var codeBody = (CodeBody)sender;
         string help = null;
         if (mode == CodeMode.Script) help = ScriptParser.GetHelp(line);
         else if (mode == CodeMode.BattleScript) BattleScriptParser.GetHelp(line);
         else if (mode == CodeMode.AnimationScript) AnimationScriptParser.GetHelp(line);
         else throw new NotImplementedException();
         codeBody.HelpContent = help;
      }

      private void CompileChanges() {
         using (ModelCacheScope.CreateScope(model)) {
            if (mode == CodeMode.Thumb) CompileThumbChanges();
         }
      }

      private void CompileThumbChanges() {
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
         if (start > end) (start, end) = (end, start);
         int length = end - start + 1;
         int originalLength = length;
         var code = thumb.Compile(model, start, out var newRuns, Content.Split(Environment.NewLine));

         // if more length is needed and the next available bytes are free, allow it.
         while (code.Count > length && model.Count > start + length + 1 && model[start + length] == 0xFF && model[start + length + 1] == 0xFF) length += 2;

         if (code.Count > length) return;

         model.ClearFormat(history.CurrentChange, start + 1, length - 1);
         for (int i = 0; i < code.Count; i++) history.CurrentChange.ChangeData(model, start + i, code[i]);
         for (int i = code.Count; i < length; i++) history.CurrentChange.ChangeData(model, start + i, 0xFF);
         foreach (var run in newRuns) model.ObserveRunWritten(history.CurrentChange, run);

         ModelDataChanged?.Invoke(this, ErrorInfo.NoError);

         if (length > originalLength) {
            using (CreateRecursionGuard()) {
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(start);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(start + length - 1);
            }
         }
      }

      private TSERun Construct<TSERun>(int start, SortedSpan<int> sources) where TSERun : IScriptStartRun {
         if (typeof(TSERun) == typeof(XSERun)) return (TSERun)(IScriptStartRun)new XSERun(start, sources);
         if (typeof(TSERun) == typeof(ASERun)) return (TSERun)(IScriptStartRun)new ASERun(start, sources);
         if (typeof(TSERun) == typeof(BSERun)) return (TSERun)(IScriptStartRun)new BSERun(start, sources);
         throw new NotImplementedException();
      }

      bool ignoreContentUpdates;
      private IDisposable CreateRecursionGuard() {
         if (ignoreContentUpdates) return new StubDisposable();
         ignoreContentUpdates = true;
         return new StubDisposable { Dispose = () => ignoreContentUpdates = false };
      }

      private void CompileScriptChanges<TSERun>(CodeBody body, IFormattedRun run, ref string codeContent, ScriptParser parser, bool updateSelection) where TSERun : IScriptStartRun {
         ShowErrorText = false;
         ErrorText = string.Empty;
         var sources = run?.PointerSources ?? null;
         int start = body.Address;
         int length = body.CompiledLength;

         using (CreateRecursionGuard()) {
            var oldScripts = parser.CollectScripts(model, start);
            var originalCodeContent = codeContent;
            var code = parser.Compile(history.CurrentChange, model, start, ref codeContent, out var movedData);
            if (code == null) {
               return;
            }

            if (code.Length > length) {
               if (run == null) {
                  var availableLength = length;
                  for (int i = start + length; i < start + code.Length; i++) {
                     // if it's freespace, then it's available
                     if (model[i] == 0xFF) {
                        availableLength++;
                        continue;
                     }
                     if (model.GetNextRun(i) is IScriptStartRun scriptRun) {
                        // the next byte is a script... maybe it's ok to overwrite it
                        // we can overwrite it if it passes 2 checks
                        // (1) the only pointers to that script are contained within the script we're currently compiling
                        // (2) the script is contained completely within the compiled code (meaning it's actually part of the script as written)
                        if (scriptRun.PointerSources.All(source => start < source && source < start + length)) {
                           var scriptLength = parser.FindLength(model, scriptRun.Start);
                           if (i + scriptLength <= start + code.Length) {
                              i += scriptLength - 1;
                              availableLength += scriptLength;
                              continue;
                           }
                        }
                     }
                     ErrorText = $"Script is {code.Length} bytes long, but only {availableLength} bytes are available.";
                     ShowErrorText = true;
                     return;
                  }
               } else {
                  if (run is NoInfoRun) run = Construct<TSERun>(run.Start, run.PointerSources);
                  run = model.RelocateForExpansion(history.CurrentChange, run, code.Length);
                  if (start != run.Start) {
                     ModelDataMoved?.Invoke(this, (start, run.Start));
                     start = run.Start;
                     code = parser.Compile(history.CurrentChange, model, start, ref codeContent, out movedData); // recompile for the new location. Could update pointers.
                     sources = run.PointerSources;
                  }
               }
            }

            // pre-clear the format before we change the data
            // waiting beyond this point won't clear anchors correctly, since pointer values will be changed
            var changeStart = code.Length;
            for (int i = 0; i < code.Length; i++) {
               if (model[start + i] == code[i]) continue;
               changeStart = Math.Max(1, i); // changeStart should never be zero: we don't want to clear the script anchor
               break;
            }
            if (changeStart < code.Length) model.ClearFormat(history.CurrentChange, start + changeStart, code.Length - changeStart);

            history.CurrentChange.ChangeData(model, start, code);
            body.CompiledLength = code.Length;
            model.ClearFormatAndData(history.CurrentChange, start + code.Length, length - code.Length);
            parser.FormatScript<TSERun>(history.CurrentChange, model, start);
            if (sources != null) {
               foreach (var source in sources) {
                  var existingRun = model.GetNextRun(source);
                  if (existingRun.Start > source || !(existingRun is ITableRun)) {
                     model.ObserveRunWritten(history.CurrentChange, new PointerRun(source));
                  }
               }
            }

            // this change may have orphaned some existing scripts. Don't lose them!
            var newScripts = parser.CollectScripts(model, start);
            foreach (var orphan in oldScripts.Except(newScripts)) {
               var orphanRun = model.GetNextRun(orphan);
               if (orphanRun.Start == orphan && string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, orphan))) {
                  parser.FormatScript<TSERun>(history.CurrentChange, model, orphan);
                  if (typeof(TSERun) == typeof(XSERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.xse{orphan:X6}", new XSERun(orphan));
                  } else if (typeof(TSERun) == typeof(BSERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.bse{orphan:X6}", new BSERun(orphan));
                  } else if (typeof(TSERun) == typeof(ASERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.ase{orphan:X6}", new ASERun(orphan));
                  } else {
                     throw new NotImplementedException();
                  }
               }
            }

            if (updateSelection) {
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(start);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(start + code.Length - 1);
            }

            foreach (var movedResource in movedData) ModelDataMoved?.Invoke(this, movedResource);
         }

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

      public event EventHandler<string> HelpSourceChanged;

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

      private int compiledLength;
      public int CompiledLength {
         get => compiledLength;
         set => Set(ref compiledLength, value);
      }

      private int caretPosition;
      public int CaretPosition {
         get => caretPosition;
         set {
            if (!TryUpdate(ref caretPosition, value)) return;
            var lines = content.Split('\r', '\n').ToList();
            var contentBoundaryCount = 0;
            while (caretPosition > lines[0].Length) {
               if (lines[0].Trim() == "{") contentBoundaryCount += 1;
               if (lines[0].Trim() == "}") contentBoundaryCount -= 1;
               caretPosition -= lines[0].Length + 1;
               lines.RemoveAt(0);
            }

            // only show help if we're not within content curlies.
            if (contentBoundaryCount != 0) HelpContent = string.Empty;
            else HelpSourceChanged?.Invoke(this, lines[0]);
         }
      }

      private string content;
      public string Content {
         get => content;
         set {
            if (!TryUpdate(ref content, value)) return;
            ContentChanged?.Invoke(this, EventArgs.Empty);
         }
      }

      private string helpContent;
      public string HelpContent { get => helpContent; set => TryUpdate(ref helpContent, value); }
   }
}
