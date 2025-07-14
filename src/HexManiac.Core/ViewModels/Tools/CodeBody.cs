using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class CodeBody : ViewModelCore {
      public const int MaxEventTextWidth = 214;

      public readonly IDataModel model;
      public readonly ScriptParser parser;
      public readonly IDataInvestigator investigator;
      public readonly int gameHash;

      public event EventHandler<ExtendedPropertyChangedEventArgs<string>> ContentChanged;

      public event EventHandler<ISet<(int, int)>> RequestShowSearchResult;

      public event EventHandler<HelpContext> HelpSourceChanged;

      public string label;
      public string Label {
         get => label;
         set => TryUpdate(ref label, value);
      }

      public int address;
      public int Address {
         get => address;
         set => TryUpdate(ref address, value);
      }

      public int compiledLength;
      public int CompiledLength {
         get => compiledLength;
         set => Set(ref compiledLength, value);
      }

      public bool hasError;
      public bool HasError { get => hasError; set => Set(ref hasError, value); }

      public string errorText;
      public string ErrorText { get => errorText; set => Set(ref errorText, value); }

      public ScriptParser Parser => parser;

      public string selectedText;
      public HelpContext SplitLine(int lineIndex, int characterIndex) {
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

      public bool ignoreEditorContentUpdates;
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

      public string helpContent;
      public string HelpContent { get => helpContent; set => TryUpdate(ref helpContent, value); }

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
