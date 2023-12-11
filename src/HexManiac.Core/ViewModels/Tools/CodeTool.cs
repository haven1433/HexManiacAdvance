using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum CodeMode { Thumb, Script, BattleScript, AnimationScript, TrainerAiScript, Raw }

   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      private CodeMode mode;
      private readonly Singletons singletons;
      private readonly ThumbParser thumb;
      private readonly ScriptParser script, battleScript, animationScript, battleAIScript;
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IRaiseMessageTab messageTab;

      public event EventHandler<ErrorInfo> ModelDataChanged;
      public event EventHandler AttentionNewContent;

      public bool IsReadOnly => Mode == CodeMode.Raw;
      public bool UseSingleContent => !UseMultiContent;
      public bool UseMultiContent => Mode.IsAny(CodeMode.Script, CodeMode.BattleScript, CodeMode.AnimationScript, CodeMode.TrainerAiScript);

      public IDataInvestigator Investigator { get; set; }

      private bool isSelected;
      public bool IsSelected { get => isSelected; set => Set(ref isSelected, value, old => UpdateContent()); }

      private bool insertAutoActive = true;
      public bool InsertAutoActive { get => insertAutoActive; set => Set(ref insertAutoActive, value); }

      private bool showErrorText;
      public bool ShowErrorText { get => showErrorText; private set => TryUpdate(ref showErrorText, value); }

      private string errorText;
      public string ErrorText { get => errorText; private set => TryUpdate(ref errorText, value); }

      private int fontSize = 12;
      public int FontSize { get => fontSize; set => TryUpdate(ref fontSize, value); }

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

      public TextEditorViewModel Editor { get; } = new();

      public string Content {
         get => Editor.Content;
         set {
            if (ignoreContentUpdates) return;
            if (Editor.Content != value) Editor.Content = value;
            else CompileChanges();
         }
      }

      public ObservableCollection<CodeBody> Contents { get; } = new ObservableCollection<CodeBody>();

      public ThumbParser Parser => thumb;

      public ScriptParser ScriptParser => script;

      public ScriptParser BattleScriptParser => battleScript;

      public ScriptParser AnimationScriptParser => animationScript;

      public ScriptParser BattleAIScriptParser => battleAIScript;

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

      public CodeTool(Singletons singletons, ViewPort viewPort, Selection selection, ChangeHistory<ModelDelta> history, IRaiseMessageTab messageTab) {
         this.singletons = singletons;
         var gameHash = viewPort.Model.GetShortGameCode();
         thumb = new ThumbParser(singletons);
         script = new ScriptParser(gameHash, singletons.ScriptLines, 0x02);
         battleScript = new ScriptParser(gameHash, singletons.BattleScriptLines, 0x3D);
         animationScript = new ScriptParser(gameHash, singletons.AnimationScriptLines, 0x08);
         battleAIScript = new ScriptParser(gameHash, singletons.BattleAIScriptLines, 0x5A);
         this.viewPort = viewPort;
         this.model = viewPort.Model;
         this.selection = selection;
         this.history = history;
         this.messageTab = messageTab;
         selection.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(selection.SelectionEnd)) {
               UpdateContent();
            }
         };

         SetupThumbKeywords(singletons);
         Editor.Bind(nameof(Editor.Content), (sender, e) => {
            if (ignoreContentUpdates) return;
            NotifyPropertyChanged(nameof(Content));
            CompileChanges();
         });
      }

      private void SetupThumbKeywords(Singletons singletons) {
         Editor.LineCommentHeader = "@";
         Editor.MultiLineCommentHeader = "/*";
         Editor.MultiLineCommentFooter = "*/";

         Editor.Keywords.Clear();
         var set = new HashSet<string>();
         foreach (var template in singletons.ThumbInstructionTemplates) {
            if (template is Instruction instr) {
               set.Add(instr.Operator);
            }
         }
         set.AddRange(new[] { ".word", ".byte", ".hword", ".align" });
         set.AddRange("beq bne bhs blo bcs bcc bmi bpl bvs bvc bhi bls bge blt bgt ble bal bnv".Split(' '));
         Editor.Keywords.AddRange(set);

         Editor.Constants.Clear();
         for (int i = 0; i <= 15; i++) Editor.Constants.Add($"r{i}");
         Editor.Constants.AddRange(new[] { "lr", "sp", "pc" });
      }

      public void DataForCurrentRunChanged() => UpdateContent();

      public void UpdateContent() {
         if (ignoreContentUpdates || !isSelected) return;
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));

         if (start > end) (start, end) = (end, start);
         int length = end - start + 1;

         using (Scope(ref ignoreContentUpdates, true, old => ignoreContentUpdates = old)) {
            if (length > 0x1000) {
               Editor.Content = "Too many bytes selected.";
               NotifyPropertyChanged(nameof(Content));
            } else if (mode == CodeMode.Raw) {
               Editor.Content = RawParse(model, start, end - start + 1);
               NotifyPropertyChanged(nameof(Content));
            } else if (length < 2 && mode == CodeMode.Thumb) {
               Editor.Content = string.Empty;
               NotifyPropertyChanged(nameof(Content));
               UpdateContents(-1, null);
               CanRepointThumb = CalculateCanRepointThumb();
            } else if (mode == CodeMode.Script) {
               UpdateContents(start, script);
            } else if (mode == CodeMode.BattleScript) {
               UpdateContents(start, battleScript);
            } else if (mode == CodeMode.AnimationScript) {
               UpdateContents(start, animationScript);
            } else if (mode == CodeMode.TrainerAiScript) {
               UpdateContents(start, battleAIScript);
            } else if (mode == CodeMode.Thumb) {
               Editor.Content = thumb.Parse(model, start, end - start + 1);
               NotifyPropertyChanged(nameof(Content));
               CanRepointThumb = CalculateCanRepointThumb();
            } else {
               throw new NotImplementedException();
            }
         }
      }

      public void ClearConstantCache() {
         script.ClearConstantCache();
         battleScript.ClearConstantCache();
         animationScript.ClearConstantCache();
         battleAIScript.ClearConstantCache();
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
         int existingSectionCount = 0;
         for (int i = 0; i < scripts.Count; i++) {
            var scriptStart = scripts[i];
            if (scriptStart == currentScriptStart && Contents.Count > i && Contents[i].Address == scriptStart) {
               model.CurrentCacheScope.GetScriptInfo(parser, scriptStart, null, ref existingSectionCount); // mostly to update existingSectionCount
               continue;
            }
            if (currentScriptStart < scriptStart && scriptStart < currentScriptStart + currentScriptLength) {
               // this script is included inside the current under-edit script
               // it doesn't need its own content
               skippedScripts += 1;
               continue;
            }

            var label = scriptStart.ToString("X6");
            var body = Contents.Count > i ? Contents[i] :
               new CodeBody(model, parser, Investigator) { Address = scriptStart, Label = label };

            var info = model.CurrentCacheScope.GetScriptInfo(parser, scriptStart, body, ref existingSectionCount);
            bool needsAnimation = false;

            if (Contents.Count > i) {
               Contents[i].ContentChanged -= ScriptChanged;
               Contents[i].HelpSourceChanged -= UpdateScriptHelpFromLine;
               Contents[i].Content = string.Empty;
               if (Contents[i].Address != scriptStart) parser.AddKeywords(model, Contents[i]);
               Contents[i].Content = info.Content;
               Contents[i].Address = scriptStart;
               Contents[i].CompiledLength = info.Length;
               Contents[i].Label = label;
               Contents[i].HelpSourceChanged += UpdateScriptHelpFromLine;
               Contents[i].ContentChanged += ScriptChanged;
            } else {
               body.CompiledLength = info.Length;
               parser.AddKeywords(model, body);
               body.Content = info.Content;
               body.ContentChanged += ScriptChanged;
               body.HelpSourceChanged += UpdateScriptHelpFromLine;
               body.RequestShowSearchResult += ShowSearchResults;
               Contents.Add(body);
               needsAnimation = currentScriptLength != -1;
            }

            if (needsAnimation) AttentionNewContent.Raise(this);
         }

         while (Contents.Count > scripts.Count - skippedScripts) {
            Contents[Contents.Count - 1].ContentChanged -= ScriptChanged;
            Contents.RemoveAt(Contents.Count - 1);
         }
      }

      private void ScriptChanged(object viewModel, ExtendedPropertyChangedEventArgs<string> e) {
         var parser = mode switch {
            CodeMode.Script => script,
            CodeMode.BattleScript => battleScript,
            CodeMode.AnimationScript => animationScript,
            CodeMode.TrainerAiScript => battleAIScript,
            _ => null,
         };
         var body = (CodeBody)viewModel;
         body.TryCompleteCommandToken();
         if (InsertAutoActive && body.TryInsertAuto()) {
            // update the caret later, or weird stuff happens
            singletons.WorkDispatcher.DispatchWork(() =>
               singletons.WorkDispatcher.BlockOnUIWork(() => body.Editor.CaretIndex += 6)
            );
         }
         if (InsertAutoActive) body.TryInsertAuto();
         var delta = body.Content.Length - e.OldValue.Length;
         var deltaSize = Math.Abs(delta);
         if (body.CaretPosition >= deltaSize && body.CaretPosition < body.Content.Length - deltaSize) {
            var start = body.Content[0..(body.CaretPosition + delta)];
            if (start.EndsWith("<auto>")) InsertAutoActive = true;
            start = body.Content[0..(body.CaretPosition + delta)];
            if (start.EndsWith("<auto")) InsertAutoActive = false;
         }

         var codeContent = body.Content;

         var run = model.GetNextRun(body.Address);
         if (run != null && run.Start != body.Address) run = null;

         int length = parser.FindLength(model, body.Address);
         using (ModelCacheScope.CreateScope(model)) {
            var initialStart = selection.Scroll.ViewPointToDataIndex(selection.SelectionStart);
            var initialEnd = selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd);
            if (initialStart > initialEnd) (initialStart, initialEnd) = (initialEnd, initialStart);

            if (mode == CodeMode.Script) {
               CompileScriptChanges<XSERun>(body, run, ref codeContent, e.OldValue, parser, body == Contents[0]);
            } else if (mode == CodeMode.AnimationScript) {
               CompileScriptChanges<ASERun>(body, run, ref codeContent, e.OldValue, parser, body == Contents[0]);
            } else if (mode == CodeMode.BattleScript) {
               CompileScriptChanges<BSERun>(body, run, ref codeContent, e.OldValue, parser, body == Contents[0]);
            } else if (mode == CodeMode.TrainerAiScript) {
               CompileScriptChanges<TSERun>(body, run, ref codeContent, e.OldValue, parser, body == Contents[0]);
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
            if (initialStart == body.Address) {
               length = end - start + 1;
               body.Address = start; // in case of the code getting repointed
            }
            UpdateContents(start, parser, body.Address, length);
         }
      }

      private void UpdateScriptHelpFromLine(object sender, HelpContext context) {
         var codeBody = (CodeBody)sender;
         string help;
         if (mode == CodeMode.Script) help = ScriptParser.GetHelp(model, codeBody, context);
         else if (mode == CodeMode.BattleScript) help = BattleScriptParser.GetHelp(model, codeBody, context);
         else if (mode == CodeMode.AnimationScript) help = AnimationScriptParser.GetHelp(model, codeBody, context);
         else if (mode == CodeMode.TrainerAiScript) help = BattleAIScriptParser.GetHelp(model, codeBody, context);
         else throw new NotImplementedException();
         codeBody.HelpContent = help;
      }

      private void ShowSearchResults(object sender, ISet<(int, int)> results) {
         viewPort.OpenSearchResultsTab("Script Search Results", results.ToList());
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

      private SERun Construct<SERun>(int start, SortedSpan<int> sources) where SERun : IScriptStartRun {
         if (typeof(SERun) == typeof(XSERun)) return (SERun)(IScriptStartRun)new XSERun(start, sources);
         if (typeof(SERun) == typeof(ASERun)) return (SERun)(IScriptStartRun)new ASERun(start, sources);
         if (typeof(SERun) == typeof(BSERun)) return (SERun)(IScriptStartRun)new BSERun(start, sources);
         if (typeof(SERun) == typeof(TSERun)) return (SERun)(IScriptStartRun)new TSERun(start, sources);
         throw new NotImplementedException();
      }

      bool ignoreContentUpdates;
      private IDisposable CreateRecursionGuard() {
         if (ignoreContentUpdates) return new StubDisposable();
         ignoreContentUpdates = true;
         return new StubDisposable { Dispose = () => ignoreContentUpdates = false };
      }

      private void CompileScriptChanges<SERun>(CodeBody body, IFormattedRun run, ref string codeContent, string previousText, ScriptParser parser, bool updateSelection) where SERun : IScriptStartRun {
         ShowErrorText = false;
         ErrorText = string.Empty;
         var sources = run?.PointerSources ?? null;
         int start = body.Address;
         int length = body.CompiledLength;

         using (CreateRecursionGuard()) {
            var oldScripts = parser.CollectScripts(model, start);
            var originalCodeContent = codeContent;
            int caret = body.CaretPosition;
            body.ClearErrors();
            parser.CompileError += body.WatchForCompileErrors;
            var code = parser.Compile(history.CurrentChange, model, start, ref codeContent, ref caret, body, out var movedData, out int ignoreCharacterCount);
            parser.CompileError -= body.WatchForCompileErrors;
            if (originalCodeContent != codeContent) body.Editor.CaretIndex = caret + codeContent.Length - previousText.Length - ignoreCharacterCount;
            if (code == null) {
               return;
            }

            if (code.Length > length) {
               model.ExpandData(history.CurrentChange, start + code.Length - 1);
               selection.Scroll.DataLength = model.RawData.Length;
               if (run == null) {
                  var availableLength = length;
                  for (int i = start + length; i < start + code.Length; i++) {
                     // if it's freespace, then it's available
                     if (model[i] == 0xFF) {
                        availableLength++;
                        continue;
                     }
                     if (model.GetNextRun(i) is IScriptStartRun scriptRun && scriptRun.Start == i) {
                        // the next byte is a script... maybe it's ok to overwrite it
                        // we can overwrite it if it passes 2 checks
                        // (1) the only pointers to that script are contained within the script we're currently compiling
                        // (2) the script is contained completely within the compiled code (meaning it's actually part of the script as written)
                        if (scriptRun.PointerSources.All(source => start < source && source < start + length)) {
                           var scriptLength = parser.FindLength(model, scriptRun.Start, model.CurrentCacheScope.ScriptDestinations(start));
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
                  if (run is NoInfoRun) run = Construct<SERun>(run.Start, run.PointerSources);
                  run = model.RelocateForExpansion(history.CurrentChange, run, body.CompiledLength, code.Length);
                  if (start != run.Start) {
                     ModelDataMoved?.Invoke(this, (start, run.Start));
                     start = run.Start;
                     int changedCaret = body.CaretPosition;
                     code = parser.Compile(history.CurrentChange, model, start, ref codeContent, ref changedCaret, body, out movedData, out var _); // recompile for the new location. Could update pointers.
                     // assume that changedCaret == body.CaretPosition? But it's probably not important
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
            if (changeStart < code.Length) {
               // use a nodatachange token here: we want to keep bytes, not anchor names
               var change = history.InsertCustomChange(new NoDataChangeDeltaModel());
               model.ClearFormat(change, start + changeStart, code.Length - changeStart);
            }

            var anyChanges = history.CurrentChange.ChangeData(model, start, code);
            if (anyChanges || body.CompiledLength != code.Length) {
               body.CompiledLength = code.Length;
               model.ClearFormatAndData(history.CurrentChange, start + code.Length, length - code.Length);
            }
            var formatted = parser.FormatScript<SERun>(history.CurrentChange, model, start, code.Length);
            if (sources != null) {
               foreach (var source in sources) {
                  // skip the source if it's within one of the added scripts: it may have moved, and we've already added it.
                  if (formatted.Any(kvp => source.InRange(kvp.Key, kvp.Key + kvp.Value))) continue;
                  var existingRun = model.GetNextRun(source);
                  if (existingRun.Start > source || !(existingRun is ITableRun)) {
                     model.ObserveRunWritten(history.CurrentChange, new PointerRun(source));
                  }
               }
            }

            // this change may have orphaned some existing scripts. Don't lose them!
            var newScripts = parser.CollectScripts(model, start);
            var orphans = oldScripts.Except(newScripts).ToList();
            foreach (var orphan in orphans) {
               var orphanRun = model.GetNextRun(orphan);
               if (orphanRun.Start == orphan && orphanRun.PointerSources.IsNullOrEmpty() && string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, orphan))) {
                  parser.FormatScript<SERun>(history.CurrentChange, model, orphan);
                  if (typeof(SERun) == typeof(XSERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.xse{orphan:X6}", new XSERun(orphan));
                  } else if (typeof(SERun) == typeof(BSERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.bse{orphan:X6}", new BSERun(orphan));
                  } else if (typeof(SERun) == typeof(ASERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.ase{orphan:X6}", new ASERun(orphan));
                  } else if (typeof(SERun) == typeof(TSERun)) {
                     model.ObserveAnchorWritten(history.CurrentChange, $"orphans.tse{orphan:X6}", new TSERun(orphan));
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
   }

   public class CodeTextFormatter : ITextPreProcessor {
      public TextFormatting[] Format(string content) {
         var result = new TextFormatting[content.Length];
         bool inText = false, inComment = false;
         for (int i = 0; i < content.Length; i++) {
            if (inComment && content[i] == '\n') inComment = false;
            if (content[i] == '#') inComment = true;
            if (inComment) continue;
            if (content[i] == '{') inText = true;
            else if (content[i] == '}') inText = false;
            else if (inText) result[i] = TextFormatting.Text;
         }
         return result;
      }
   }

   public record HelpContext(string Line, int Index, int ContentBoundaryCount = 0, int ContentBoundaryIndex = -1, bool IsSelection = false);
}
