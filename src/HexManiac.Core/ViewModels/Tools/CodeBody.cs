using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class CodeBody : ViewModelCore {
      public const int MaxEventTextWidth = 214;

      private readonly IDataModel model;
      private readonly ScriptParser parser;
      private readonly IDataInvestigator investigator;
      private readonly int gameHash;

      public event EventHandler<ExtendedPropertyChangedEventArgs<string>> ContentChanged;

      public event EventHandler<ISet<(int, int)>> RequestShowSearchResult;

      public event EventHandler<HelpContext> HelpSourceChanged;

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

      private bool hasError;
      public bool HasError { get => hasError; set => Set(ref hasError, value); }

      private string errorText;
      public string ErrorText { get => errorText; set => Set(ref errorText, value); }

      #region Insertion Utilities

      public bool CanInsertFlag {
         get {
            if (investigator == null || model.SpartanMode) return false;
            var context = SplitCurrentLine();
            if (context.ContentBoundaryCount != 0) return false;

            // only available if the previous character is a space
            if (context.Index == 0 || context.Line[context.Index - 1] != ' ') return false;

            // only available if the next parameter is a flag
            var line = parser.FirstMatch(context.Line.Trim());
            if (line == null) return false;
            var tokens = ScriptLine.Tokenize(context.Line.Substring(0, context.Index));
            if (tokens.Length == 0) return false;
            var args = (line is MacroScriptLine macro) ? macro.ShortFormArgs : line.Args;
            if (args.Count < tokens.Length) return false;
            var currentArg = args[tokens.Length - 1];
            return currentArg.Name == "flag" && currentArg.Type == ArgType.Short && currentArg.EnumTableName == "|h";
         }
      }

      public bool CanInsertVar {
         get {
            if (investigator == null || model.SpartanMode) return false;
            var context = SplitCurrentLine();
            if (context.ContentBoundaryCount != 0) return false;

            // only available if the previous character is a space
            if (context.Index == 0 || context.Line[context.Index - 1] != ' ') return false;

            // only available if the next parameter is a variable
            var line = parser.FirstMatch(context.Line.Trim());
            if (line == null) return false;
            var tokens = ScriptLine.Tokenize(context.Line.Substring(0, context.Index));
            if (tokens.Length == 0) return false;
            var args = (line is MacroScriptLine macro) ? macro.ShortFormArgs : line.Args;
            if (args.Count < tokens.Length) return false;
            var currentArg = args[tokens.Length - 1];
            return currentArg.Name == "variable" && currentArg.Type == ArgType.Short && currentArg.EnumTableName == "scriptvariablealiases";
         }
      }

      public void InsertFlag() {
         var content = new StringBuilder(Content.Substring(0, CaretPosition));
         var afterContent = Content.Substring(CaretPosition);
         var flag = investigator.FindNextUnusedFlag();

         var newContent = $"0x{flag:X4}";
         var context = SplitCurrentLine();
         var lineContent = context.Line.Substring(0, context.Index) + newContent;
         var line = parser.FirstMatch(lineContent.Trim());
         if (line != null) {
            if (line.ErrorCheck(lineContent, out var _) != null) newContent += " ";
            content.Append(newContent);

            content.Append(afterContent);
            var caret = Editor.CaretIndex;
            Content = content.ToString();
            Editor.CaretIndex = caret + newContent.Length;
            NotifyPropertyChanged(nameof(CanInsertFlag));
         }
         Editor.FocusKeyboard();
      }

      public void InsertVar() {
         var content = new StringBuilder(Content.Substring(0, CaretPosition));
         var afterContent = Content.Substring(CaretPosition);
         var variable = investigator.FindNextUnusedVariable();

         var newContent = $"0x{variable:X4}";
         var context = SplitCurrentLine();
         var lineContent = context.Line.Substring(0, context.Index) + newContent;
         var line = parser.FirstMatch(lineContent.Trim());
         if (line != null) {
            if (line.ErrorCheck(lineContent, out var _) != null) newContent += " ";
            content.Append(newContent);

            content.Append(afterContent);
            var caret = Editor.CaretIndex;
            Content = content.ToString();
            Editor.CaretIndex = caret + newContent.Length;
            NotifyPropertyChanged(nameof(CanInsertVar));
         }
         Editor.FocusKeyboard();
      }

      public void InsertFlagOrVar() {
         if (CanInsertFlag) {
            InsertFlag();
         } else if (CanInsertVar) {
            InsertVar();
         }
      }

      /// <summary>
      /// true if the user right-clicked on a variable or flag
      /// </summary>
      public bool CanFindUses {
         get {
            if (CaretPosition < 0 || model.SpartanMode) return false;
            var context = SplitCurrentLine();
            if (context.ContentBoundaryCount != 0) return false;
            int left = context.Index, right = context.Index;
            while (left.InRange(1, context.Line.Length) && context.Line[left] != ' ') left--;
            while (right < context.Line.Length && context.Line[right] != ' ') right++;
            var token = context.Line.Substring(left, right - left).Trim();
            if (!token.TryParseInt(out int value)) return false;
            var line = parser.FirstMatch(context.Line.Trim());
            if (line == null) return false;
            if (line.Args.All(arg => arg.Name != "variable" && arg.Name != "flag")) return false;
            return true;
         }
      }

      public void FindUses() {
         var context = SplitCurrentLine();
         if (context.ContentBoundaryCount != 0) return;
         int left = context.Index, right = context.Index;
         while (right < context.Line.Length && context.Line[right] != ' ') right++;
         while (left.InRange(0, context.Line.Length) && context.Line[left] != ' ') left--;
         var token = context.Line.Substring(left, right - left).Trim();
         if (!token.TryParseInt(out int value)) return;
         var line = parser.FirstMatch(context.Line.Trim());
         if (line == null) return;
         if (value < 0x4000) {
            var flags = Flags.FindFlagUsages(model, parser, value);
            RequestShowSearchResult.Raise(this, flags);
         } else {
            var variables = Flags.FindVarUsages(model, parser, value);
            RequestShowSearchResult.Raise(this, variables);
         }
      }

      public bool CanGotoAddress {
         get {
            if (CaretPosition < 0 || model.SpartanMode) return false;
            var context = SplitCurrentLine();
            if (context.ContentBoundaryCount != 0) return false;
            int left = context.Index, right = context.Index;
            while (left.InRange(1, context.Line.Length) && context.Line[left] != ' ') left--;
            while (right < context.Line.Length && context.Line[right] != ' ') right++;
            var token = context.Line.Substring(left, right - left).Trim();
            if (token.StartsWith("<")) token = token[1..];
            if (token.EndsWith(">")) token = token[..^1];
            return token.TryParseHex(out var _) || model.GetAddressFromAnchor(new(), -1, token) != Pointer.NULL;
         }
      }

      public void GotoAddress() {
         if (CaretPosition < 0) return;
         var context = SplitCurrentLine();
         if (context.ContentBoundaryCount != 0) return;
         int left = context.Index, right = context.Index;
         while (left.InRange(1, context.Line.Length) && context.Line[left] != ' ') left--;
         while (right < context.Line.Length && context.Line[right] != ' ') right++;
         var token = context.Line.Substring(left, right - left).Trim();
         if (token.StartsWith("<")) token = token[1..];
         if (token.EndsWith(">")) token = token[..^1];
         if (token.TryParseHex(out var result)) {
            RequestShowSearchResult.Raise(this, new HashSet<(int, int)> { (result, result) });
            return;
         }
         var address = model.GetAddressFromAnchor(new(), -1, token);
         if (address == Pointer.NULL) return;
         RequestShowSearchResult.Raise(this, new HashSet<(int, int)> { (address, address) });
      }

      private bool TryGetSourceInfo(out string table, out string parsedToken) {
         table = null;
         parsedToken = null;
         if (model.SpartanMode) return false;
         var context = SplitCurrentLine();
         var tokens = ScriptLine.Tokenize(context.Line);
         var token = 0;
         var caret = 0;
         while (caret < context.Index) {
            if (context.Line[caret] == ' ') {
               caret++;
               continue;
            }
            caret += tokens[token].Length;
            if (caret >= context.Index) break;
            token++;
         }
         if (token == 0) return false;
         var line = parser.FirstMatch(context.Line.Trim());
         if (line == null) return false;
         if (token >= tokens.Length) return false;
         parsedToken = tokens[token];
         var args = line.Args;
         if (line is MacroScriptLine macro) {
            args = macro.ShortFormArgs;
            token -= 1;
         } else if (line is ScriptLine script) {
            token -= script.LineCode.Count;
         }
         if (args.Count <= token || token < 0) return false;
         table = args[token].EnumTableName;
         if (table == null) return false;
         var tableRun = model.GetTable(table);
         if (tableRun == null) return false;
         return true;
      }

      public bool CanGotoSource => TryGetSourceInfo(out var _, out var _);

      public void GotoSource() {
         if (!TryGetSourceInfo(out var tableName, out var token)) return;
         if (!ArrayRunEnumSegment.TryParse(tableName, model, token, out var index)) return;
         var run = model.GetTable(tableName);
         var destination = run.Start + run.ElementLength * index;
         RequestShowSearchResult.Raise(this, new HashSet<(int, int)> { (destination, destination) });
      }

      // TODO 'FindUses' for Enum values? Just duplicate the 'Show Uses' logic from the table tool?

      private StubCommand findUsesCommand, gotoSourceCommand, gotoAddressCommand;
      public ICommand FindUsesCommand => StubCommand(ref findUsesCommand, FindUses, () => CanFindUses);
      public ICommand GotoSourceCommand => StubCommand(ref gotoSourceCommand, GotoSource, () => CanGotoSource);
      public ICommand GotoAddressCommand => StubCommand(ref gotoAddressCommand, GotoAddress, () => CanGotoAddress);

      #endregion

      #region <auto> complete

      public bool TryInsertAuto() {
         if (model.SpartanMode) return false;
         var context = SplitCurrentLine();
         if (context.ContentBoundaryCount != 0) return false;
         var tokens = ScriptLine.Tokenize(context.Line);
         if (context.Line.Length > context.Index + 1) return false;
         if (!context.Line.EndsWith(" ")) return false;
         var line = parser.FirstMatch(context.Line.Trim());
         if (line == null) return false;
         var args = line.Args;
         int token = tokens.Length;
         if (line is MacroScriptLine macro) {
            args = macro.ShortFormArgs;
            token -= 1;
         } else if (line is ScriptLine script) {
            token -= script.LineCode.Count;
         }
         if (token < 0 || token >= args.Count) return false;
         if (args[token].Type == ArgType.Pointer && args[token].PointerType.IsAny(ExpectedPointerType.Mart, ExpectedPointerType.SpriteTemplate, ExpectedPointerType.Text, ExpectedPointerType.Movement, ExpectedPointerType.Decor)) {
            var before = Content.Substring(0, CaretPosition + 1);
            var after = Content.Substring(CaretPosition + 1);
            using (Scope(ref ignoreEditorContentUpdates, true, old => ignoreEditorContentUpdates = old)) {
               Editor.Content = before + "<auto>" + after;
               return true;
            }
         }
         return false;
      }

      #endregion

      #region Command completion

      public bool TryCompleteCommandToken() {
         if (model.SpartanMode) return false;
         var context = SplitCurrentLine();
         if (context.ContentBoundaryCount != 0) return false;
         var tokens = ScriptLine.Tokenize(context.Line);
         if (tokens.Length != 1) return false;
         if (context.Line.Length > context.Index + 1) return false;
         if (!context.Line.EndsWith(" ")) return false;
         var line = parser.FirstMatch(context.Line.Trim());
         if (line != null) return false;

         var candidates = parser.PartialMatches(tokens[0]);
         if (candidates.Count == 0) return false;
         candidates = ScriptParser.SortOptions(candidates, tokens[0], c => c.LineCommand).ToList();

         var before = Content[..(CaretPosition - tokens[0].Length)];
         var after = Content[(CaretPosition)..];
         using (Scope(ref ignoreEditorContentUpdates, true, old => ignoreEditorContentUpdates = old)) {
            Editor.Content = before + candidates[0].LineCommand + after;
            Editor.CaretIndex += candidates[0].LineCommand.Length - tokens[0].Length + 1;
            return true;
         }
      }

      #endregion

      public int CaretPosition {
         get => Editor.CaretIndex;
         set {
            if (Editor.CaretIndex == value) return;
            Editor.CaretIndex = value;
            var context = SplitCurrentLine();

            HelpSourceChanged.Raise(this, context);

            NotifyPropertiesChanged(nameof(CanInsertFlag), nameof(CanInsertVar),
               nameof(CanFindUses), nameof(CanGotoSource), nameof(CanGotoAddress));
            findUsesCommand.RaiseCanExecuteChanged();
            gotoSourceCommand.RaiseCanExecuteChanged();
         }
      }

      private string selectedText;
      public string SelectedText {
         get => selectedText;
         set => Set(ref selectedText, value, old => {
            if (string.IsNullOrEmpty(selectedText) || selectedText.Contains(Environment.NewLine)) return;
            var context = SplitCurrentLine();
            HelpSourceChanged.Raise(this, context with { Index = context.Index + selectedText.TrimEnd().Length, IsSelection = true });
         });
      }

      public HelpContext SplitCurrentLine() {
         int value = Math.Min(CaretPosition, Content.Length);
         var lines = Content.Split('\r', '\n').ToList();
         var contentBoundaryCount = 0;
         var contentBoundaryIndex = 0;
         int i = 0;
         while (value > lines[i].Length) {
            if (lines[i].Trim() == "{") { contentBoundaryCount += 1; contentBoundaryIndex += 1; }
            if (lines[i].Trim() == "}") contentBoundaryCount -= 1;
            value -= lines[i].Length + 1;
            i++;
         }
         return new(lines[i], value, contentBoundaryCount, contentBoundaryIndex - 1);
      }

      private HelpContext SplitLine(int lineIndex, int characterIndex) {
         var lines = Content.Split('\n').ToList();
         if (!lineIndex.InRange(0, lines.Count)) return null;
         var contentBoundaryCount = 0;
         var contentBoundaryIndex = 0;
         for (int i = 0; i < lineIndex; i++) {
            if (lines[i].Trim() == "{") { contentBoundaryCount += 1; contentBoundaryIndex += 1; }
            if (lines[i].Trim() == "}") contentBoundaryCount -= 1;
         }
         var cleanLine = lines[lineIndex].Split('\r', StringSplitOptions.RemoveEmptyEntries);
         if (cleanLine.Length == 0) return null;
         return new(cleanLine[0], characterIndex, contentBoundaryCount, contentBoundaryIndex - 1);
      }

      public TextEditorViewModel Editor { get; } = new() { PreFormatter = new CodeTextFormatter() };

      public IReadOnlyList<ExpectedPointerType> StreamTypes { get; set; }

      private bool ignoreEditorContentUpdates;
      public string Content {
         get => Editor.Content;
         set {
            if (Editor.Content != value) {
               using (Scope(ref ignoreEditorContentUpdates, true, old => ignoreEditorContentUpdates = old)) {
                  ClearErrors();
                  var previousValue = Editor.Content;
                  Editor.Content = value;
                  NotifyPropertyChanged();
                  ContentChanged.Raise(this, new(previousValue, nameof(Content)));
                  EvaluateTextLength();
               }
            }
         }
      }

      private string helpContent;
      public string HelpContent { get => helpContent; set => TryUpdate(ref helpContent, value); }

      public CodeBody(IDataModel model, ScriptParser parser, IDataInvestigator investigator) {
         this.model = model;
         this.parser = parser;
         this.investigator = investigator;
         this.gameHash = model.GetShortGameCode();
         Editor.Bind(nameof(Editor.Content), (sender, e) => {
            if (ignoreEditorContentUpdates) return;
            NotifyPropertyChanged(nameof(Content));
            ContentChanged.Raise(this, (ExtendedPropertyChangedEventArgs<string>)e);
            EvaluateTextLength();
         });
         Editor.Bind(nameof(Editor.CaretIndex), (sender, e) => {
            NotifyPropertyChanged(nameof(CaretPosition));
         });
      }

      public void SaveCaret(int lengthDelta) => Editor.CaretIndex += lengthDelta;

      public void ClearErrors() {
         HasError = false;
         ErrorText = string.Empty;
         Editor.ErrorLocations.Clear();
      }

      public void EvaluateTextLength() {
         if (model.SpartanMode) return;
         foreach (var streamLine in LookForStreamLines()) {
            if (streamLine.Type != ExpectedPointerType.Text) continue; // 35*6
            foreach (var error in model.TextConverter.GetOverflow(streamLine.Text, MaxEventTextWidth)) {
               Editor.ErrorLocations.Add(error with { Line = error.Line + streamLine.LineNumber });
            }
         }
      }

      public void WatchForCompileErrors(object? sender, ScriptErrorInfo e) {
         HasError = true;
         ErrorText = e.Message;
         Editor.ErrorLocations.Add(e.Segment);
      }

      public IList<ScriptLineFormatInfo> LookForStreamLines() {
         var results = new List<ScriptLineFormatInfo>();
         var queue = new Queue<ExpectedPointerType>();
         var lines = Content.SplitLines();
         for (int i = 0; i < lines.Length; i++) {
            if (lines[i].Trim() == "{") {
               var type = queue.Count == 0 ? ExpectedPointerType.Unknown : queue.Dequeue();
               i += 1;
               while (lines[i].Trim() != "}") {
                  results.Add(new(i, type, lines[i]));
                  i += 1;
                  if (lines.Length <= i) break;
               }
               continue;
            }
            var command = parser.FirstMatch(lines[i]);
            if (command == null) continue;
            queue.Clear();
            foreach (var arg in command.Args) {
               if (arg.Type == ArgType.Pointer && !arg.PointerType.IsAny(ExpectedPointerType.Script, ExpectedPointerType.Unknown)) queue.Enqueue(arg.PointerType);
            }
         }
         return results;
      }

      public IReadOnlyList<AutocompleteItem> GetTokenComplete(string line, int lineIndex, int characterIndex) {
         if (characterIndex < line.Length && line[characterIndex] != ' ') return null;
         var results = new List<AutocompleteItem>();
         var context = SplitLine(lineIndex, characterIndex);
         if (context == null) return null;
         var before = line.Substring(0, characterIndex);
         var after = line.Substring(characterIndex);
         var tokens = ScriptLine.Tokenize(before.Trim());

         if (tokens.Length == 0) return null;

         // if they're 'working on' a new token, add a blank token to the end
         if (before.EndsWith(" ")) tokens = tokens.Concat(new[] { string.Empty }).ToArray();

         if (context.ContentBoundaryCount > 0) {
            // stream content
            if (!context.ContentBoundaryIndex.InRange(0, StreamTypes.Count)) return null;
            var expectedType = StreamTypes[context.ContentBoundaryIndex];
            if (expectedType == ExpectedPointerType.Mart) {
               var options = parser.ReadOptions(model, HardcodeTablesModel.ItemsTableName, context.Line);
               results.AddRange(options.Select(op => new AutocompleteItem(op, op)));
            } else if (expectedType == ExpectedPointerType.Movement) {
               var options = parser.ReadOptions(model, "movementtypes", context.Line);
               results.AddRange(options.Select(op => new AutocompleteItem(op, op)));
            } else {
               return null;
            }
         } else if (tokens.Length == 1) {
            // script command
            var candidates = parser.PartialMatches(tokens[0]).Where(line => line.MatchesGame(gameHash)).ToList();
            candidates = candidates.Where(line => line.LineCommand.MatchesPartial(tokens[0])).ToList();
            if (!context.IsSelection) {
               foreach (var line1 in candidates) {
                  if (line1.LineCommand == tokens[0] && line1.CountShowArgs() == 0) return null; // perfect match with no args
               }
            }
            candidates = ScriptParser.SortOptions(candidates, tokens[0], c => c.LineCommand).ToList();
            var length = before.Length - tokens[0].Length;
            if (length >= 0) {
               before = before.Substring(0, length);
               results.AddRange(candidates.Select(op => {
                  var afterText = after;
                  if (string.IsNullOrEmpty(afterText) && op.Args.Count + op.LineCode.Count > 1) afterText = " "; // insert whitespace after
                  return new AutocompleteItem(op.Usage, before + op.LineCommand + afterText);
               }));
            }
         } else {
            // script args
            var candidates = parser.PartialMatches(tokens[0]).Where(line => line.MatchesGame(gameHash)).ToList();
            candidates = candidates.Where(line => line.LineCommand.Equals(tokens[0], StringComparison.CurrentCultureIgnoreCase)).ToList();
            var checkToken = 1;
            while (candidates.Count > 1 && checkToken < tokens.Length) {
               if (!tokens[checkToken].TryParseHex(out var codeValue)) break;
               candidates = candidates.Where(line => line.LineCode.Count <= checkToken || line.LineCode[checkToken] == codeValue).ToList();
               checkToken++;
            }
            if (candidates.FirstOrDefault() is IScriptLine syntax) {
               IReadOnlyList<IScriptArg> args = syntax.Args.Where(arg => arg is ScriptArg).ToList();
               if (syntax is MacroScriptLine macro) args = macro.ShortFormArgs;
               var skipCount = syntax.LineCode.Count;
               if (skipCount == 0) skipCount = 1; // macros
               if (args.Count + skipCount >= tokens.Length && tokens.Length >= skipCount + 1) {
                  var arg = args[tokens.Length - 1 - skipCount];
                  var token = tokens[tokens.Length - 1];
                  var options = parser.ReadOptions(model, arg.EnumTableName, token);
                  if (options == null) return null;
                  if (args.Count == tokens.Length - skipCount && options.Any(option => option == token)) return null; // perfect match on last token
                  before = before.Substring(0, before.Length - token.Length);
                  if (string.IsNullOrEmpty(after) && tokens.Length - skipCount < args.Count) after = " "; // insert whitespace after
                  results.AddRange(options.Select(op => new AutocompleteItem(op, before + op.Split('#').First().Trim() + after)));
               }
            }
         }
         return results;
      }
   }

   public record ScriptLineFormatInfo(int LineNumber, ExpectedPointerType Type, string Text);
}
