using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using Microsoft.Scripting.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// TODO consolidate script length logic:
// ScriptExtensions.GetScriptSegmentLength
// ScriptParser.CollectScripts
// ScriptParser.FindLength

namespace HavenSoft.HexManiac.Core.Models.Code {
   public record ScriptErrorInfo(string Message, TextSegment Segment);

   public class ScriptParser {
      public const int MaxRepeates = 20;
      private readonly IReadOnlyList<IScriptLine> engine;
      private readonly byte endToken;
      private int gameHash;

      public bool RequireCompleteAddresses { get; set; } = true;

      public event EventHandler<ScriptErrorInfo> CompileError;

      public ScriptParser(int gameHash, IReadOnlyList<IScriptLine> engine, byte endToken) {
         (this.engine, this.endToken) = (engine, endToken);
         this.gameHash = gameHash;
      }

      public void RefreshGameHash(IDataModel model) => gameHash = model.GetShortGameCode();

      public int GetScriptSegmentLength(IDataModel model, int address) => engine.GetScriptSegmentLength(gameHash, model, address, new Dictionary<int, int>());

      public string Parse(IDataModel data, int start, int length) {
         var builder = new StringBuilder();
         foreach (var line in Decompile(data, start, length)) builder.AppendLine(line);
         return builder.ToString();
      }

      public List<int> CollectScripts(IDataModel model, int address) {
         // do some basic validation to make sure this is actually a reasonable thing to decode
         var currentRun = model.GetNextRun(address);
         var scripts = new List<int>();
         if (address < 0 || address >= model.Count) return scripts;
         if (currentRun.Start < address) return scripts;
         if (currentRun.Start == address && !(currentRun is IScriptStartRun) && !(currentRun is NoInfoRun)) return scripts;

         scripts.Add(address);
         var lengths = new List<int>();

         for (int i = 0; i < scripts.Count; i++) {
            while (i < scripts.Count && i.Range().Any(j => scripts[j] <= scripts[i] && scripts[i] < scripts[j] + lengths[j])) scripts.RemoveAt(i);
            if (i == scripts.Count) break;
            address = scripts[i];
            int length = 0;
            var destinations = new Dictionary<int, int>();
            int lastCommand = -1, repeateLength = 0;
            while (true) {
               var line = engine.GetMatchingLine(gameHash, model, address + length);
               if (line == null) break;
               // normally we would just use this,
               // but we want to track all the pointers that go to scripts separately.
               // so just use this to get the child-sizes, so we can adjust the length after
               line.CompiledByteLength(model, address + length, destinations);
               length += line.LineCode.Count;
               foreach (var arg in line.Args) {
                  if (arg.Type == ArgType.Pointer) {
                     var destination = model.ReadPointer(address + length);
                     if (destination >= 0 && destination < model.Count &&
                        arg.PointerType == ExpectedPointerType.Script &&
                        !scripts.Contains(destination) &&
                        i.Range().All(j => destination < scripts[j] || scripts[j] + lengths[j] <= destination)
                     ) {
                        scripts.Add(destination);
                     }
                  }
                  length += arg.Length(model, address + length);
               }
               if (line.IsEndingCommand) break;

               if (line.LineCode[0] != lastCommand) (lastCommand, repeateLength) = (line.LineCode[0], 1);
               else repeateLength += 1;
               if (line.Args.Count > 0) repeateLength = 0;
               if (repeateLength > MaxRepeates) break; // same command lots of times in a row is fishy
            }

            // append child scripts that come directly after this script
            while (true) {
               // child script starts directly after this script and has only one source
               if (destinations.TryGetValue(address + length, out int childLength) && childLength > 0) {
                  var anchor = model.GetNextAnchor(address + length);
                  if (anchor.Start == address + length && anchor.PointerSources.Count == 1) {
                     length += childLength;
                     continue;
                  }
               }
               // child script has a 1-byte margin (probably an end after a goto) and has only one source
               if (destinations.TryGetValue(address + length + 1, out childLength)) {
                  // there was a skip... should we ignore it?
                  // If something points to that position, we can't keep going.
                  var anchor = model.GetNextAnchor(address + length);
                  if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

                  anchor = model.GetNextAnchor(address + length + 1);
                  if (anchor.Start == address + length + 1 && anchor.PointerSources.Count == 1) {
                     length += childLength + 1;
                     continue;
                  }
               }
               // child script has a 2-byte margin (probably aligned mart data) and has only one source
               if (destinations.TryGetValue(address + length + 2, out childLength)) {
                  // there was a skip... should we ignore it?
                  // If something points to that position, we can't keep going.
                  var anchor = model.GetNextAnchor(address + length);
                  if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

                  anchor = model.GetNextAnchor(address + length + 2);
                  if (anchor.Start == address + length + 2 && anchor.PointerSources.Count == 1) {
                     length += childLength + 2;
                     continue;
                  }
               }
               // child script has a 3-byte margin (probably aligned mart data) and has only one source
               if (destinations.TryGetValue(address + length + 3, out childLength)) {
                  // there was a skip... should we ignore it?
                  // If something points to that position, we can't keep going.
                  var anchor = model.GetNextAnchor(address + length);
                  if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

                  anchor = model.GetNextAnchor(address + length + 3);
                  if (anchor.Start == address + length + 3 && anchor.PointerSources.Count == 1) {
                     length += childLength + 3;
                     continue;
                  }
               }
               break;
            }

            lengths.Add(length);
            for (int j = lengths.Count - 1; j >= 0; j--) {
               if (scripts[j] <= address || scripts[j] >= address + length) continue;
               lengths.RemoveAt(j);
               scripts.RemoveAt(j);
               i--;
            }

            // look for other scripts from destinations that we should care about but don't yet
            foreach (var d in destinations.Keys) {
               if (scripts.Contains(d)) continue; // destination is already in queue
               if (model.GetNextRun(d) is not IScriptStartRun scriptStart || scriptStart.Start != d) continue; // destination isn't formatted as a script
               if (lengths.Count().Range().Any(j => scripts[j] <= d && d < scripts[j] + lengths[j])) continue; // destination is included within exsting script
               scripts.Add(d);
            }
         }

         return scripts;
      }

      public int FindLength(IDataModel model, int address, Dictionary<int, int> destinations = null) {
         if (destinations == null) destinations = model.CurrentCacheScope.ScriptDestinations(address);
         int length = 0;
         int consecutiveNops = 0;
         int lastCommand = -1, repeateLength = 1;
         while (true) {
            var line = engine.GetMatchingLine(gameHash, model, address + length);
            if (line == null) break;
            consecutiveNops = line.LineCommand.StartsWith("nop") ? consecutiveNops + 1 : 0;
            if (consecutiveNops > 16) return 0;
            length += line.CompiledByteLength(model, address + length, destinations);
            if (line.IsEndingCommand) break;

            if (line.LineCode[0] != lastCommand) (lastCommand, repeateLength) = (line.LineCode[0], 1);
            else repeateLength += 1;
            if (line.Args.Count > 0) repeateLength = 0;
            if (repeateLength > ScriptParser.MaxRepeates) break; // same command lots of times in a row is fishy
         }

         // Include in the length any content that comes directly (or +1) after the script.
         // This content should be considered part of the script.
         // Only do this if the content is only referenced once, otherwise it may not be safe to include during repoints.
         while (true) {
            if (destinations.TryGetValue(address + length, out int additionalLength) && additionalLength > 0) {
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count == 1) {
                  length += additionalLength;
                  continue;
               }
            }
            if (destinations.TryGetValue(address + length + 1, out additionalLength)) {
               // there was a skip... should we ignore it?
               // If something points to that position, we can't keep going.
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

               anchor = model.GetNextAnchor(address + length + 1);
               if (anchor.Start == address + length + 1 && anchor.PointerSources.Count == 1) {
                  length += additionalLength + 1;
                  continue;
               }
            }
            if (destinations.TryGetValue(address + length + 2, out additionalLength)) {
               // there was a skip... should we ignore it?
               // If something points to that position, we can't keep going.
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

               anchor = model.GetNextAnchor(address + length + 2);
               if (anchor.Start == address + length + 2 && anchor.PointerSources.Count == 1) {
                  length += additionalLength + 2;
                  continue;
               }
            }
            if (destinations.TryGetValue(address + length + 3, out additionalLength)) {
               // there was a skip... should we ignore it?
               // If something points to that position, we can't keep going.
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

               anchor = model.GetNextAnchor(address + length + 3);
               if (anchor.Start == address + length + 3 && anchor.PointerSources.Count == 1) {
                  length += additionalLength + 3;
                  continue;
               }
            }

            break;
         }

         return length;
      }

      public MacroScriptLine GetMacro(IDataModel model, int address) => engine.GetMatchingMacro(gameHash, model, address);
      public ScriptLine GetLine(IDataModel model, int address) => engine.GetMatchingLine(gameHash, model, address);

      public IEnumerable<IScriptLine> DependsOn(string basename) {
         foreach (var line in engine) {
            foreach (var arg in line.Args) {
               if (arg.EnumTableName == basename) {
                  yield return line;
                  break;
               }
            }
         }
      }

      private HashSet<string> constantCache, keywordCache;
      public void AddKeywords(IDataModel model, CodeBody body) {
         var editor = body.Editor;
         editor.LineCommentHeader = "#";
         editor.MultiLineCommentHeader = "/*";
         editor.MultiLineCommentFooter = "*/";
         if (constantCache == null || keywordCache == null) {
            constantCache = new HashSet<string>();
            keywordCache = new HashSet<string>();
            foreach (var line in engine) {
               keywordCache.Add(line.LineCommand);
               foreach (var arg in line.Args) {
                  if (string.IsNullOrEmpty(arg.EnumTableName)) continue;
                  foreach (var option in model.GetOptions(arg.EnumTableName)) {
                     if (option == null) continue;
                     if ("<>!=?".Any(option.Contains)) continue;
                     constantCache.Add(option);
                  }
               }
            }
         }

         editor.Constants.Clear();
         editor.Constants.AddRange(constantCache);
         editor.Keywords.Clear();
         editor.Keywords.AddRange(keywordCache);
         editor.Keywords.Add("auto"); // for the auto-pointer feature
      }

      public void ClearConstantCache() {
         constantCache = null;
      }

      // TODO refactor to rely on CollectScripts rather than duplicate code
      // returns a list of scripts that were formatted, and their length
      public IDictionary<int, int> FormatScript<SERun>(ModelDelta token, IDataModel model, int address) where SERun : IScriptStartRun {
         Func<int, SortedSpan<int>, IScriptStartRun> constructor = (a, s) => new XSERun(a, s);
         if (typeof(SERun) == typeof(BSERun)) constructor = (a, s) => new BSERun(a, s);
         if (typeof(SERun) == typeof(ASERun)) constructor = (a, s) => new ASERun(a, s);
         if (typeof(SERun) == typeof(TSERun)) constructor = (a, s) => new TSERun(a, s);

         var processed = new Dictionary<int, int>();
         var toProcess = new List<int> { address };
         while (toProcess.Count > 0) {
            address = toProcess.Last();
            toProcess.RemoveAt(toProcess.Count - 1);
            var existingRun = model.GetNextRun(address);
            // We need to check for an anchor here _before_ checking if this is a duplicate address and therefore skippable.
            // Because self-referential scripts won't have their anchor picked up on the initial run.
            if (!(existingRun is SERun) && existingRun.Start == address) {
               var anchorName = model.GetAnchorFromAddress(-1, address);
               model.ObserveAnchorWritten(token, anchorName, constructor(address, existingRun.PointerSources));
            }
            if (processed.ContainsKey(address)) continue;
            int length = 0;
            while (true) {
               var line = engine.GetMatchingLine(gameHash, model, address + length);
               if (line == null) break;
               // there may've previously been a pointer here: the code has changed!
               if (model.GetNextRun(address + length) is PointerRun pRun && pRun.Start == address + length) model.ClearFormat(token, address + length, line.LineCode.Count);
               length += line.LineCode.Count;
               foreach (var arg in line.Args) {
                  if (arg.Type != ArgType.Pointer) {
                     // there may've previously been a pointer here: the code has changed!
                     // when clearing args, we actually _do_ want to clear any anchors that point to them.
                     var start = address + length;
                     var argLength = arg.Length(model, start);
                     if (model.GetNextRun(start) is ITableRun tableRun && tableRun.Start == start && tableRun.Length == argLength) {
                        // no need to clear
                     } else {
                        model.ClearFormat(token, start, argLength);
                     }
                  } else {
                     var destination = model.ReadPointer(address + length);
                     if (destination.InRange(0, model.Count) && !destination.InRange(address + length, address + length + 4)) {
                        var destinationRun = model.GetNextRun(destination);
                        if (destinationRun.Start < destination) {
                           // we're trying to point into already formatted data
                           // don't add the pointer source or destination
                        } else {
                           var pointerSource = model.GetNextRun(address + length);
                           if (pointerSource is PointerRun pointerRun && pointerRun.Start == address + length) {
                              // no need to clear/update
                           } else {
                              model.ClearFormat(token, address + length, 4);
                              model.ObserveRunWritten(token, new PointerRun(address + length));
                           }
                           if (arg.PointerType == ExpectedPointerType.Script) toProcess.Add(destination);
                           if (arg.PointerType == ExpectedPointerType.Text) {
                              WriteTextStream(model, token, destination, address + length);
                           } else if (arg.PointerType == ExpectedPointerType.Movement) {
                              WriteMovementStream(model, token, destination, address + length);
                           } else if (arg.PointerType == ExpectedPointerType.Mart) {
                              WriteMartStream(model, token, destination, address + length);
                           } else if (arg.PointerType == ExpectedPointerType.Decor) {
                              WriteDecorStream(model, token, destination, address + length);
                           } else if (arg.PointerType == ExpectedPointerType.SpriteTemplate) {
                              WriteSpriteTemplateStream(model, token, destination, address + length);
                           }
                        }
                     }
                  }
                  length += arg.Length(model, address + length);
               }
               if (line.IsEndingCommand) break;
            }
            processed.Add(address, length);
         }

         return processed;
      }

      private void WriteTextStream(IDataModel model, ModelDelta token, int destination, int source) {
         if (destination < 0 || destination > model.Count) return;
         var destinationLength = PCSString.ReadString(model, destination, true);
         if (destinationLength > 0) {
            // if there's an anchor that starts exactly here, we don't want to clear it: just update it
            // if the run starts somewhere else, we better to a clear to prevent conflicting formats
            var existingTextRun = model.GetNextRun(destination);
            if (existingTextRun.Start != destination) {
               model.ClearFormat(token, destination, destinationLength);
               model.ObserveRunWritten(token, new PCSRun(model, destination, destinationLength, SortedSpan.One(source)));
            } else if (existingTextRun is PCSRun && destinationLength == existingTextRun.Length) {
               // length and format is correct, nothing to do
            } else {
               model.ClearAnchor(token, destination + existingTextRun.Length, destinationLength - existingTextRun.Length); // assuming that the old run ends before the new run, clear the difference
               model.ObserveRunWritten(token, new PCSRun(model, destination, destinationLength, SortedSpan.One(source)));
               model.ClearAnchor(token, destination + destinationLength, existingTextRun.Length - destinationLength); // assuming that the new run ends before the old run, clear the difference
            }
         }
      }

      private void WriteMovementStream(IDataModel model, ModelDelta token, int start, int source) {
         var format = "[move.movementtypes]!FE";
         WriteStream(model, token, start, source, format);
      }

      public void WriteMartStream(IDataModel model, ModelDelta token, int start, int source) {
         var format = $"[item:{HardcodeTablesModel.ItemsTableName}]!0000";
         WriteStream(model, token, start, source, format);
      }

      public void WriteDecorStream(IDataModel model, ModelDelta token, int start, int source) {
         var format = $"[item:{HardcodeTablesModel.DecorationsTableName}]!0000";
         WriteStream(model, token, start, source, format);
      }

      private void WriteSpriteTemplateStream(IDataModel model, ModelDelta token, int start, int source) {
         var format = "[tileTag: paletteTag: oam<> anims<> images<> affineAnims<> callback<>]1";
         WriteStream(model, token, start, source, format);
      }

      private void WriteStream(IDataModel model, ModelDelta token, int start, int source, string format) {
         var sources = source >= 0 ? SortedSpan.One(source) : SortedSpan<int>.None;
         TableStreamRun.TryParseTableStream(model, start, sources, string.Empty, format, null, out var tsRun);
         if (tsRun != null) {
            var endTokenLength = tsRun.Length - tsRun.ElementLength * tsRun.ElementCount;
            if (endTokenLength > 0 && model.IsFreespace(start, endTokenLength) && token is not NoDataChangeDeltaModel) {
               // freespace: write the end token
               model.ClearFormat(token, tsRun.Start, endTokenLength);
               tsRun = tsRun.DeserializeRunFromZero(string.Empty, token, out var _, out var _);
               model.ObserveRunWritten(token, tsRun);
            } else if (model.GetNextRun(tsRun.Start) is ITableRun existingRun && existingRun.Start == tsRun.Start && tsRun.DataFormatMatches(existingRun)) {
               // no need to update the format, the format already matches what we want
            } else {
               model.ClearFormat(token, tsRun.Start, tsRun.Length);
               if (tsRun.ElementCount == 0) {
                  // write the end token
                  tsRun.DeserializeRun(string.Empty, token, out var _, out var _);
               }
               model.ObserveRunWritten(token, tsRun);
            }
         }
      }

      private record StreamInfo(ExpectedPointerType PointerType, int Source, int Destination);

      public static int InsertMissingClosers(ref string script, int caret) {
         int caretMove = 0;
         bool isStartOfLine = true;
         var text = script.ToList();
         for (int i = 0; i < text.Count; i++) {
            if (text[i] == '\n' || text[i] == '\r') isStartOfLine = true;
            else if (text[i] != ' ' && text[i] != '{') isStartOfLine = false;
            if (text[i] != '{') continue;

            if (!isStartOfLine) {
               text.Insert(i, '\r');
               text.Insert(i + 1, '\n');
               if (i <= caret) { caret += 2; }
               i += 2;
            }

            isStartOfLine = true;
            var close = -1;
            for (int j = i + 1; j < text.Count && text[j] != '{'; j++) {
               if (text[j] == '\n' || text[j] == '\r') isStartOfLine = true;
               else if (text[j] != ' ' && text[j] != '}') isStartOfLine = false;
               if (text[j] != '}') continue;
               if (!isStartOfLine) {
                  text.Insert(j, '\r');
                  text.Insert(j + 1, '\n');
                  if (j < caret) { caret += 2; caretMove += 2; }
                  j += 2;
               }
               close = j;
               break;
            }
            if (close == -1) {
               if (i == caret) {
                  text.Insert(i + 1, '\r');
                  text.Insert(i + 2, '\n');
               }
               text.Insert(i + 3, '}');
               close = i + 3;
               if (i == caret) caretMove -= 3;
               // check for excess blank lines after the lines we just inserted
               if (close < text.Count - 4 && text[close + 1] == '\r' && text[close + 2] == '\n' && text[close + 3] == '\r' && text[close + 4] == '\n') {
                  text.RemoveAt(close + 1);
                  text.RemoveAt(close + 1);
               }
            }
            if (i + 1 < text.Count && text[i + 1] != '\r') {
               text.Insert(i + 1, '\r');
               text.Insert(i + 2, '\n');
               if (i < caret) { caret += 2; caretMove += 2; }
               close += 2;
            }
            if (close + 1 < text.Count && text[close + 1] != '\r') {
               text.Insert(close + 1, '\r');
               text.Insert(close + 2, '\n');
               if (close < caret) { caret += 2; }
               isStartOfLine = true;
               close += 2;
            }

            // special case: no blank line between open and close
            if (close == i + 3) {
               text.Insert(i + 1, '\r');
               text.Insert(i + 2, '\n');
               if (i < caret) { caret += 2; caretMove -= 2; }
               close += 2;
            }

            i = close;
         }

         script = new string(text.ToArray());
         return caretMove;
      }

      public byte[] Compile(ModelDelta token, IDataModel model, int start, ref string script, out IReadOnlyList<(int originalLocation, int newLocation)> movedData, out int ignoreCharacterCount) {
         int ignoreCaret = 0;
         return Compile(token, model, start, ref script, ref ignoreCaret, out movedData, out ignoreCharacterCount);
      }

      /// <summary>
      /// Potentially edits the script text and returns a set of data repoints.
      /// The data is moved, but the script itself has not written by this method.
      /// </summary>
      /// <param name="movedData">Related data runs that moved during compilation.</param>
      /// <param name="ignoreCharacterCount">Number of new characters added that should be ignored by the caret.</param>
      /// <returns></returns>
      public byte[] Compile(ModelDelta token, IDataModel model, int start, ref string script, ref int caret, out IReadOnlyList<(int originalLocation, int newLocation)> movedData, out int ignoreCharacterCount) {
         ignoreCharacterCount = 0;
         movedData = new List<(int, int)>();
         var deferredContent = new List<DeferredStreamToken>();
         int adjustCaret = InsertMissingClosers(ref script, caret);
         caret += adjustCaret;
         var lines = script.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Select(line => line.Split('#').First())
            .ToArray();
         var result = new List<byte>();
         var streamInfo = new List<StreamInfo>();

         var labels = ExtractLocalLabels(model, start, lines);

         bool lastCommandIsEndCommand = false;

         for (var i = 0; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var line = lines[i].Trim();
            if (line.EndsWith(":")) continue; // label, not code. Don't parse.
            if (line == "{") {
               var streamStart = i + 1;
               var indentCount = 1;
               i += 1;
               while (indentCount > 0 && lines.Length > i) {
                  line = lines[i].Trim();
                  if (line == "{") indentCount += 1;
                  if (line == "}") indentCount -= 1;
                  i += 1;
               }
               i -= 1;
               var streamEnd = i;
               var streamLines = lines.Skip(streamStart).Take(streamEnd - streamStart).ToList();
               var stream = Environment.NewLine.Join(streamLines);

               // Let the stream run handle updating itself based on the stream content.
               if (streamInfo.Count > 0) {
                  var info = streamInfo[0];
                  if (info.Destination == DeferredStreamToken.AutoSentinel + Pointer.NULL) {
                     var deferred = deferredContent[deferredContent.Count - streamInfo.Count];
                     deferred.UpdateContent(model, info.PointerType, stream);
                  } else if (model.GetNextRun(info.Destination) is IStreamRun streamRun && streamRun.Start == info.Destination) {
                     var newStreamRun = streamRun.DeserializeRun(stream, token, out var _, out var _); // we don't notify parents/children based on script-stream changes: we know they never have parents/children.
                     // alter script content and compiled byte location based on stream move
                     if (newStreamRun.Start != info.Destination) {
                        script = script.Replace(info.Destination.ToAddress(), newStreamRun.Start.ToAddress());
                        result[info.Source - start + 0] = (byte)(newStreamRun.Start >> 0);
                        result[info.Source - start + 1] = (byte)(newStreamRun.Start >> 8);
                        result[info.Source - start + 2] = (byte)(newStreamRun.Start >> 16);
                        result[info.Source - start + 3] = (byte)((newStreamRun.Start >> 24) + 0x08);
                        ((List<(int, int)>)movedData).Add((info.Destination, newStreamRun.Start));
                     }
                     if (newStreamRun != streamRun) model.ObserveRunWritten(token, newStreamRun);
                  }
                  streamInfo.RemoveAt(0);
               }
               continue;
            }
            streamInfo.Clear();

            var command = FirstMatch(line);
            if (command != null) {
               var currentSize = result.Count;

               if (line.Contains("<??????>")) {
                  int newAddress = -1;
                  if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.Movement)) {
                     if (model.FreeSpaceStart == start) model.FreeSpaceStart += model.FreeSpaceBuffer;
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, 0xFE);
                     WriteMovementStream(model, token, newAddress, -1);
                  } else if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.Text)) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     if (newAddress == -1) {
                        var endLength = model.Count;
                        model.ExpandData(token, endLength + 0x10);
                        newAddress = endLength + 0x10;
                     }
                     token.ChangeData(model, newAddress, 0xFF);
                     model.ObserveRunWritten(token, new PCSRun(model, newAddress, 1));
                  } else if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.Mart)) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, 0x00);
                     token.ChangeData(model, newAddress + 1, 0x00);
                     WriteMartStream(model, token, newAddress, -1);
                  } else if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.Decor)) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, 0x00);
                     token.ChangeData(model, newAddress + 1, 0x00);
                     WriteDecorStream(model, token, newAddress, -1);
                  } else if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.SpriteTemplate)) {
                     newAddress = model.FindFreeSpace(0, 0x18);
                     for (int j = 0; j < 0x18; j++) token.ChangeData(model, newAddress + j, 0x00);
                     WriteSpriteTemplateStream(model, token, newAddress, -1);
                  } else if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.Script)) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, endToken);
                     // TODO write a new script template... an anchor with a format based on the current script
                  }
                  if (newAddress != -1) {
                     var originalLine = line;
                     line = line.ReplaceOne("<??????>", $"<{newAddress:X6}>");
                     if (command.Args.Any(arg => arg.PointerType == ExpectedPointerType.Script)) {
                        script = script.ReplaceOne("<??????>", $"<{newAddress:X6}>");
                     } else if (script.IndexOf(originalLine) != script.IndexOf($"{originalLine}{Environment.NewLine}{{{Environment.NewLine}")) {
                        script = script.ReplaceOne(originalLine, $"{line}{Environment.NewLine}{{{Environment.NewLine}}}");
                        ignoreCharacterCount += Environment.NewLine.Length * 2 + 2;
                     } else {
                        script = script.ReplaceOne("<??????>", $"<{newAddress:X6}>");
                     }
                  }
               }

               var error = command.Compile(model, start + currentSize, line, labels, out var code);
               if (error == null) {
                  result.AddRange(code);
               } else {
                  var segment = new TextSegment(i, 0, lines[i].Length, SegmentType.Error);
                  CompileError?.Invoke(this, new(i + ": " + error, segment));
                  return null;
               }
               var pointerOffset = command.LineCode.Count;
               foreach (var arg in command.Args) {
                  if (arg.Type == ArgType.Pointer && arg.PointerType != ExpectedPointerType.Unknown) {
                     var destination = result.ReadMultiByteValue(currentSize + pointerOffset, 4) + Pointer.NULL;
                     if (destination == DeferredStreamToken.AutoSentinel + Pointer.NULL) {
                        streamInfo.Add(new(arg.PointerType, start + currentSize + pointerOffset, destination));
                        (string format, byte[] defaultContent, int alignment) = arg.PointerType switch {
                           ExpectedPointerType.Text => ("\"\"", new byte[] { 0xFF }, 1),
                           ExpectedPointerType.Mart => ($"[item:{HardcodeTablesModel.ItemsTableName}]!0000", new byte[] { 0, 0 }, 4),
                           ExpectedPointerType.Decor => ($"[item:{HardcodeTablesModel.DecorationsTableName}]!0000", new byte[] { 0, 0 }, 1),
                           ExpectedPointerType.Movement => ($"[move.movementtypes]!FE", new byte[] { 0xFE }, 1),
                           _ => ("^", new byte[0], 1)
                        };
                        deferredContent.Add(new(currentSize + pointerOffset, format, defaultContent, alignment));
                     } else if (destination >= 0 && arg.PointerType != ExpectedPointerType.Script) {
                        streamInfo.Add(new(arg.PointerType, start + currentSize + pointerOffset, destination));
                        if (arg.PointerType == ExpectedPointerType.Text) {
                           WriteTextStream(model, token, destination, start + currentSize + pointerOffset);
                        } else if (arg.PointerType == ExpectedPointerType.Movement) {
                           WriteMovementStream(model, token, destination, start + currentSize + pointerOffset);
                        }
                     }
                  }
                  pointerOffset += arg.Length(model, currentSize + pointerOffset);
               }

               lastCommandIsEndCommand = command.IsEndingCommand;
            }
         }

         if (!lastCommandIsEndCommand) result.Add(endToken);

         // any labels that were used but not included, stick them on the end of the script
         labels.ResolveUnresolvedLabels(start, result, endToken);

         // done with script lines, now write deferred data
         foreach (var deferred in deferredContent) {
             deferred.WriteData(result, start);
         }

         return result.ToArray();
      }

      public IScriptLine FirstMatch(string line) {
         foreach (var command in engine) {
            if (!command.MatchesGame(gameHash)) continue;
            if (command.CanCompile(line)) return command;
         }
         return null;
      }

      private LabelLibrary ExtractLocalLabels(IDataModel model, int start, string[] lines) {
         var labels = new Dictionary<string, int>();
         var length = 0;
         foreach (var fullLine in lines) {
            var line = fullLine.Trim();
            if (line.EndsWith(":")) {
               labels[line.Substring(0, line.Length - 1)] = start + length;
               continue;
            }
            foreach (var command in engine) {
               if (!command.CanCompile(line)) continue;
               length += command.CompiledByteLength(model, line);
               break;
            }
         }
         return new LabelLibrary(model, labels) { RequireCompleteAddresses = RequireCompleteAddresses };
      }

      private List<string> ReadOptions(IDataModel model, string tableName, string token) {
         if (!string.IsNullOrEmpty(tableName)) {
            var isList = model.TryGetList(tableName, out var list);
            var allOptions = model.GetOptions(tableName);
            var options = new List<string>();
            for (int i = 0; i < allOptions.Count; i++) {
               if (token.Length == 0 || allOptions[i].MatchesPartial(token)) {
                  if (!isList || list.Comments == null || !list.Comments.TryGetValue(i, out var comment)) comment = string.Empty;
                  else comment = " # " + comment;
                  options.Add(allOptions[i] + comment);
               }
            }
            if (token.Length > 0) {
               options.Sort((a, b) => a.SkipCount(token) - b.SkipCount(token));
               options.Sort((a, b) => a.IndexOf(token[0], StringComparison.CurrentCultureIgnoreCase) - b.IndexOf(token[0], StringComparison.CurrentCultureIgnoreCase));
            }

            if (options.Count > 10) {
               while (options.Count > 9) options.RemoveAt(options.Count - 1);
               options.Add("...");
            }
            return options;
         }
         return null;
      }

      public string GetHelp(IDataModel model, HelpContext context) {
         var currentLine = context.Line;
         if (string.IsNullOrWhiteSpace(currentLine)) return null;
         var tokens = ScriptLine.Tokenize(currentLine.Trim());
         var candidates = engine.Where(line => line.LineCommand.Contains(tokens[0], StringComparison.CurrentCultureIgnoreCase)).ToList();

         var isAfterToken = context.Index > 0 &&
            (context.Line.Length == context.Index || context.Line[context.Index] == ' ') &&
            (char.IsLetterOrDigit(context.Line[context.Index - 1]) || context.Line[context.Index - 1].IsAny("_~'\"-.".ToCharArray()));
         if (isAfterToken) {
            tokens = ScriptLine.Tokenize(currentLine.Substring(0, context.Index).Trim());
            // try to auto-complete whatever token is left of the cursor

            // need autocomplete for command?
            if (tokens.Length == 1) {
               candidates = candidates.Where(line => line.LineCommand.Contains(tokens[0], StringComparison.CurrentCultureIgnoreCase) && line.MatchesGame(gameHash)).ToList();
               if (!context.IsSelection) {
                  foreach (var line in candidates) {
                     if (line.LineCommand == tokens[0] && line.CountShowArgs() == 0) return null; // perfect match with no args
                  }
               }
               return Environment.NewLine.Join(candidates.Take(10).Select(line => line.Usage));
            }

            // filter down to just perfect matches. There could be several (trainerbattle)
            candidates = candidates.Where(line => line.LineCommand.Equals(tokens[0], StringComparison.CurrentCultureIgnoreCase)).ToList();
            var checkToken = 1;
            while (candidates.Count > 1 && checkToken < tokens.Length) {
               if (!tokens[checkToken].TryParseHex(out var codeValue)) break;
               candidates = candidates.Where(line => line.LineCode.Count <= checkToken || line.LineCode[checkToken] == codeValue).ToList();
               checkToken++;
            }
            var syntax = candidates.FirstOrDefault();
            if (syntax != null) {
               var args = syntax.Args.Where(arg => arg is ScriptArg).ToList();
               var skipCount = syntax.LineCode.Count;
               if (skipCount == 0) skipCount = 1; // macros
               if (args.Count + skipCount >= tokens.Length && tokens.Length >= skipCount + 1) {
                  var arg = args[tokens.Length - 1 - skipCount];
                  var token = tokens[tokens.Length - 1];
                  var options = ReadOptions(model, arg.EnumTableName, token);
                  if (options != null) {
                     if (args.Count == tokens.Length - skipCount && options.Any(option => option == token)) return null; // perfect match on last token
                     return Environment.NewLine.Join(options);
                  }
               }
            }
         }

         if (candidates.Count > 10) return null;
         if (candidates.Count == 0) return null;

         string partialDocumentation = string.Empty;
         if (candidates.Count == 1 && !candidates[0].Documentation.All(string.IsNullOrWhiteSpace)) {
            if (candidates[0].CountShowArgs() <= tokens.Length - 1) return null;
            partialDocumentation = candidates[0].Usage + Environment.NewLine + string.Join(Environment.NewLine, candidates[0].Documentation);
         }

         if (context.Index > 0 && context.Line[context.Index - 1] == ' ' && tokens.Length > 0) {
            // I'm directly after a space, I'm at the start of a new token, there is no existing documentation
            var skipCount = candidates[0].LineCode.Count;
            if (skipCount == 0) skipCount = 1;
            var args = candidates[0].Args.Where(arg => arg is ScriptArg).ToList();
            if ((tokens.Length - skipCount).InRange(0, args.Count)) {
               var arg = args[tokens.Length - skipCount];
               var options = ReadOptions(model, arg.EnumTableName, string.Empty);
               if (options != null) {
                  if (partialDocumentation.Length > 0) partialDocumentation += Environment.NewLine;
                  partialDocumentation += Environment.NewLine.Join(options);
               }
            }
         }

         if (partialDocumentation.Length > 0) return partialDocumentation;

         var perfectMatch = candidates.FirstOrDefault(candidate => (currentLine + " ").StartsWith(candidate.LineCommand + " "));
         if (perfectMatch != null) {
            if (perfectMatch.CountShowArgs() == tokens.Length - 1) return null;
            return perfectMatch.Usage + Environment.NewLine + string.Join(Environment.NewLine, perfectMatch.Documentation);
         }
         var bestMatch = candidates.FirstOrDefault(candidate => tokens[0].Contains(candidate.LineCommand));
         if (bestMatch != null) {
            if (bestMatch.CountShowArgs() == tokens.Length - 1) return null;
            return bestMatch.Usage + Environment.NewLine + Environment.NewLine.Join(bestMatch.Documentation);
         }
         return string.Join(Environment.NewLine, candidates.Select(line => line.Usage));
      }

      public static int GetArgLength(IDataModel model, IScriptArg arg, int start, IDictionary<int, int> destinationLengths) {
         if (arg.Type == ArgType.Pointer && arg.PointerType != ExpectedPointerType.Unknown) {
            var destination = model.ReadPointer(start);
            if (destination >= 0 && destination < model.Count) {
               var run = model.GetNextRun(destination);
               if (run is IScriptStartRun scriptStart && scriptStart.Start == destination && scriptStart.Start > start) {
                  return model.GetScriptLength(scriptStart, destinationLengths);
               } else if (run.Start == destination) {
                  // we only want to add this run's length as part of the script if:
                  // (1) the run has no name
                  // (2) the run has only one source (the script)
                  if (run is NoInfoRun || run.PointerSources == null) return -1;
                  if (run is IScriptStartRun) return -1; // this script has a length, but don't track it (prevent recursion loop)
                  if (run.PointerSources.Count == 1 && string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, destination))) {
                     return run.Length;
                  }
               } else if (arg.PointerType == ExpectedPointerType.Text) {
                  // we didn't find a matching run, but this data claims to be simple text
                  var textLength = PCSString.ReadString(model, destination, true);
                  return textLength;
               }
            }
         }
         return -1;
      }

      private string[] Decompile(IDataModel data, int index, int length) {
         var results = new List<string>();
         var nextAnchor = data.GetNextAnchor(index);
         var destinations = new Dictionary<int, int>();

         ISet<int> linesWithLabelsToUpdate = new HashSet<int>();
         var labels = new DecompileLabelLibrary(data, index, length);

         while (length > 0) {
            if (index == nextAnchor.Start) {
               if (nextAnchor is IScriptStartRun) {
                  if (results.Count > 0) results.Add(string.Empty);
                  results.Add($"{labels.AddressToLabel(nextAnchor.Start, true)}: # {nextAnchor.Start:X6}");
                  linesWithLabelsToUpdate.Add(results.Count - 1);
               } else if (nextAnchor is IStreamRun) {
                  if (destinations.ContainsKey(index)) {
                     index += nextAnchor.Length;
                     length -= nextAnchor.Length;
                  }
               }
               nextAnchor = data.GetNextAnchor(nextAnchor.Start + nextAnchor.Length);
               continue;
            } else if (index > nextAnchor.Start) {
               nextAnchor = data.GetNextAnchor(nextAnchor.Start + nextAnchor.Length);
               continue;
            }

            var line = engine.FirstOrDefault(option => option.Matches(gameHash, data, index));
            if (line == null) {
               results.Add($".raw {data[index]:X2}");
               index += 1;
               length -= 1;
            } else {
               results.Add("  " + line.Decompile(data, index, labels));
               if (line.Args.Any(arg => arg.Type == ArgType.Pointer && arg.PointerType == ExpectedPointerType.Script)) {
                  linesWithLabelsToUpdate.Add(results.Count - 1);
               }
               var compiledByteLength = line.CompiledByteLength(data, index, destinations);
               index += compiledByteLength;
               length -= compiledByteLength;

               // if we point to shortly after, keep going
               if (destinations.ContainsKey(index) && nextAnchor is IScriptStartRun && nextAnchor.Start == index) continue;
               if (destinations.ContainsKey(index + 1) && nextAnchor is IScriptStartRun && nextAnchor.Start == index + 1) continue;
               if (destinations.ContainsKey(index) && nextAnchor is IStreamRun && nextAnchor.Start == index) continue;
               if (destinations.ContainsKey(index + 1) && nextAnchor is IStreamRun && nextAnchor.Start == index + 1) continue;
               if (destinations.ContainsKey(index + 2) && nextAnchor is IStreamRun && nextAnchor.Start == index + 2) continue;
               if (destinations.ContainsKey(index + 3) && nextAnchor is IStreamRun && nextAnchor.Start == index + 3) continue;

               // if we're at an end command, don't keep going
               if (line.IsEndingCommand) break;
            }
         }

         // post processing: if a line has a stream pointer to within the script,
         // change that pointer to be an -auto- pointer
         foreach (var label in labels.AutoLabels.ToList()) {
            var autoIndex = results.Count.Range().FirstOrDefault(i => results[i].Contains($"<{label:X6}>"));
            var autoRun = data.GetNextRun(label);
            var runIsStream = autoRun is IStreamRun;

            if (runIsStream) {
               results[autoIndex] = results[autoIndex].Replace($"<{label:X6}>", "<auto>");
            } else {
               continue;
            }
         }

         // post processing: change the section labels to be in address order
         var sections = labels.FinalizeLabels();
         foreach (int i in linesWithLabelsToUpdate) {
            results[i] = labels.FinalizeLine(sections, results[i]);
         }

         return results.ToArray();
      }
   }

   public interface IScriptLine {
      IReadOnlyList<IScriptArg> Args { get; }
      IReadOnlyList<byte> LineCode { get; }
      string LineCommand { get; }
      IReadOnlyList<string> Documentation { get; }
      string Usage { get; }

      bool IsEndingCommand { get; }

      bool MatchesGame(int gameCodeHash);
      int CompiledByteLength(IDataModel model, int start, IDictionary<int, int> destinationLengths); // compile from the bytes in the model, at that start location
      int CompiledByteLength(IDataModel model, string line); // compile from the line of code passed in
      bool Matches(int gameCodeHash, IReadOnlyList<byte> data, int index);
      string Decompile(IDataModel data, int start, DecompileLabelLibrary labels);

      /// <summary>
      /// Returns true if the command looks correct, even if the arguments are incomplete.
      /// </summary>
      bool CanCompile(string line);

      /// <summary>
      /// Returns an error if the line cannot be compiled, or a set of tokens if it can be compiled.
      /// </summary>
      string ErrorCheck(string scriptLine, out string[] tokens);

      string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result);

      void AddDocumentation(string content);

      public int CountShowArgs() {
         return Args.Sum(arg => {
            if (arg is ScriptArg) return 1;
            return 0;
            // something with array args?
         });
      }
   }

   public class MacroScriptLine : IScriptLine {
      private static readonly IReadOnlyList<byte> emptyByteList = new byte[0];
      private readonly List<string> documentation = new List<string>();

      private bool hasShortForm;
      private readonly Dictionary<int, int> shortIndexFromLongIndex = new();
      private readonly IReadOnlyList<int> matchingGames;

      public IReadOnlyList<IScriptArg> Args { get; }
      public IReadOnlyList<IScriptArg> ShortFormArgs {
         get {
            if (shortIndexFromLongIndex.Count == 0) {
               return Args.Where(arg => arg is not SilentMatchArg).ToList();
            }
            var args = new IScriptArg[shortIndexFromLongIndex.Count()];
            foreach (var pair in shortIndexFromLongIndex) {
               args[pair.Value] = Args[pair.Key];
            }
            return args;
         }
      }
      public IReadOnlyList<byte> LineCode => emptyByteList;
      public IReadOnlyList<string> Documentation => documentation;
      public string LineCommand { get; }
      public bool IsEndingCommand => false;
      public bool IsValid { get; } = true;
      public string Usage { get; private set; }

      public static bool IsMacroLine(string engineLine) {
         engineLine = engineLine.Trim();
         var tokens = engineLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
         if (tokens.Length == 0) return false;
         var token = tokens[0];
         if (token.StartsWith("[") && tokens.Length > 1) token = tokens[1];
         if (token.StartsWith("#")) return false;
         if (token.Length == 2 && token.TryParseHex(out _)) return false;
         return true;
      }

      public MacroScriptLine(string engineLine) {
         var docSplit = engineLine.Split(new[] { '#' }, 2);
         if (docSplit.Length > 1) documentation.Add('#' + docSplit[1]);
         engineLine = docSplit[0].Trim();
         matchingGames = ScriptLine.ExtractMatchingGames(ref engineLine);
         ExtractShortformInfo(ref engineLine);
         if (!hasShortForm) {
            Usage = " ".Join(engineLine.Split(' ').Where(token => token.Length != 2 || !token.TryParseHex(out _)));
         }

         var tokens = engineLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         var args = new List<IScriptArg>();
         LineCommand = tokens[0];

         for (int i = 1; i < tokens.Length; i++) {
            var token = tokens[i];
            if (token.Length == 2 && token.TryParseHex(out int number)) {
               args.Add(new SilentMatchArg((byte)number));
            } else if (ScriptArg.IsValidToken(token)) {
               args.Add(new ScriptArg(token));
            } else {
               IsValid = false;
            }
         }

         Args = args;
      }

      private void ExtractShortformInfo(ref string engineLine) {
         if (!engineLine.Contains("->")) return;
         var parts = engineLine.Split("->");
         if (parts.Length != 2) return;
         engineLine = parts[1];
         var shortTokens = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
         var longTokens = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
         if (shortTokens[0] != longTokens[0]) return;
         shortTokens = shortTokens.Skip(1).ToArray();
         longTokens = longTokens.Skip(1).ToArray();

         // for each entry in long, it shows up somewhere in short
         // entries in long can appear multiple times
         for (int i = 0; i < longTokens.Length; i++) {
            var index = shortTokens.IndexOf(longTokens[i]);
            if (index == -1) continue;
            shortIndexFromLongIndex.Add(i, index);
         }

         hasShortForm = true;
         Usage = parts[0];
      }

      public bool MatchesGame(int game) => matchingGames?.Contains(game) ?? true;

      public int CompiledByteLength(IDataModel model, int start, IDictionary<int, int> destinationLengths) {
         var length = LineCode.Count;
         foreach (var arg in Args) {
            if (destinationLengths != null) {
               var argLength = ScriptParser.GetArgLength(model, arg, start + length, destinationLengths);
               if (argLength > 0) destinationLengths[model.ReadPointer(start + length)] = argLength;
            }
            length += arg.Length(default, -1);
         }
         return length;
      }

      public int CompiledByteLength(IDataModel model, string line) {
         if (!CanCompile(line)) return 0;
         var length = LineCode.Count;
         foreach (var arg in Args) {
            length += arg.Length(default, -1);
         }
         return length;
      }

      public bool Matches(int gameCodeHash, IReadOnlyList<byte> data, int index) {
         if (Args.Count == 0) return false;
         if (!MatchesGame(gameCodeHash)) return false;
         for (int i = 0; i < Args.Count; i++) {
            var arg = Args[i];
            if (arg is SilentMatchArg smarg) {
               if (data[index] != smarg.ExpectedValue) return false;
            } else if (arg is ScriptArg sarg) {
               // don't validate, this part is variable
            } else {
               throw new NotImplementedException();
            }
            index += arg.Length(default, -1);
         }
         return true;
      }

      public string Decompile(IDataModel data, int start, DecompileLabelLibrary labels) {
         var builder = new StringBuilder(LineCommand);
         var streamContent = new List<string>();
         var args = new List<string>();
         foreach (var arg in Args) {
            if (arg is ScriptArg sarg) {
               var tempBuilder = new StringBuilder();
               sarg.Build(false, data, start, tempBuilder, streamContent, labels);
               args.Add(tempBuilder.ToString());
            }
            start += arg.Length(data, start);
         }
         if (args.Count > 0) {
            builder.Append(" ");
            builder.Append(" ".Join(ConvertLongFormToShortForm(args.ToArray())));
         }
         foreach (var content in streamContent) {
            builder.AppendLine();
            builder.AppendLine("{");
            builder.AppendLine(content);
            builder.Append("}");
         }
         return builder.ToString();
      }

      public bool CanCompile(string line) {
         var tokens = ScriptLine.Tokenize(line);
         if (tokens.Length == 0) return false;
         if (tokens[0] != LineCommand) return false;
         return true;
      }

      public string ErrorCheck(string scriptLine, out string[] tokens) {
         tokens = ScriptLine.Tokenize(scriptLine);
         if (tokens[0] != LineCommand) throw new ArgumentException($"Command {LineCommand} was expected, but received {tokens[0]} instead.");
         var args = tokens.Skip(1).ToArray();
         var shortArgs = args;
         args = ConvertShortFormToLongForm(args);
         var commandText = LineCommand;
         var specifiedArgs = Args.Where(arg => arg is ScriptArg).Count();
         if (specifiedArgs != args.Length) {
            return $"Command {commandText} expects {specifiedArgs} arguments, but received {shortArgs.Length} instead.";
         }
         return null;
      }

      public string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result) {
         result = null;
         var error = ErrorCheck(scriptLine, out var tokens);
         if (error != null) return error;
         var args = tokens.Skip(1).ToArray();
         args = ConvertShortFormToLongForm(args);
         var results = new List<byte>();
         var specifiedArgIndex = 0;
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg scriptArg) {
               var token = args[specifiedArgIndex];
               var message = scriptArg.Build(model, start + results.Count, token, results, labels);
               if (message != null) return message;
               specifiedArgIndex += 1;
            } else if (Args[i] is SilentMatchArg silentArg) {
               results.Add(silentArg.ExpectedValue);
            }
         }
         result = results.ToArray();
         return null;
      }

      public void AddDocumentation(string doc) => documentation.Add(doc);

      private string[] ConvertShortFormToLongForm(string[] args) {
         if (!hasShortForm) return args;
         // build long-form args from this short form
         var longForm = new List<string>();
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is SilentMatchArg) continue;
            var shortIndex = shortIndexFromLongIndex[i];
            if (shortIndex < args.Length) longForm.Add(args[shortIndex]);
         }
         return longForm.ToArray();
      }

      private string[] ConvertLongFormToShortForm(string[] args) {
         if (!hasShortForm) return args;
         var shortForm = new Dictionary<int, string>();
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is SilentMatchArg) continue;
            var shortIndex = shortIndexFromLongIndex[i];
            shortForm[shortIndex] = args[shortForm.Count];
         }
         return shortForm.Count.Range(i => shortForm[i]).ToArray();
      }
   }

   public enum ExpectedPointerType {
      Unknown,
      Script,
      Text,
      Movement,
      Mart,
      Decor,
      SpriteTemplate,
   }

   public abstract class ScriptLine : IScriptLine {
      private readonly List<string> documentation = new List<string>();
      private readonly IReadOnlyList<int> matchingGames;

      public const string Hex = "0123456789ABCDEF";
      public IReadOnlyList<IScriptArg> Args { get; }
      public IReadOnlyList<byte> LineCode { get; }
      public string LineCommand { get; }
      public IReadOnlyList<string> Documentation => documentation;
      public string Usage { get; }

      public virtual bool IsEndingCommand { get; }

      /// <param name="destinationLengths">If this line contains pointers, calculate the pointer data's lengths and include here.</param>
      public int CompiledByteLength(IDataModel model, int start, IDictionary<int, int> destinationLengths) {
         var length = LineCode.Count;
         foreach (var arg in Args) {
            if (arg.Type == ArgType.Pointer) {
               var destination = model.ReadPointer(start + length);
               if (destinationLengths != null && !destinationLengths.ContainsKey(destination)) {
                  var argLength = ScriptParser.GetArgLength(model, arg, start + length, destinationLengths);
                  if (argLength > 0) destinationLengths[destination] = argLength;
               }
            }
            length += arg.Length(model, start + length);
         }
         return length;
      }
      public int CompiledByteLength(IDataModel model, string line) {
         var length = LineCode.Count;
         var tokens = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         for (var i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg sarg) length += sarg.Length(default, -1);
            if (Args[i] is ArrayArg aarg) length += aarg.ConvertMany(model, tokens.Skip(i)).Count() * aarg.TokenLength + 1;
         }
         return length;
      }

      public ScriptLine(string engineLine) {
         var docSplit = engineLine.Split(new[] { '#' }, 2);
         if (docSplit.Length > 1) documentation.Add('#' + docSplit[1]);
         engineLine = docSplit[0].Trim();
         matchingGames = ExtractMatchingGames(ref engineLine);
         Usage = engineLine.Split(new[] { ' ' }, 2).Last();

         var tokens = engineLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         var lineCode = new List<byte>();
         var args = new List<IScriptArg>();

         foreach (var token in tokens) {
            if (token.Length == 2 && token.All(ViewPort.AllHexCharacters.Contains)) {
               lineCode.Add(byte.Parse(token, NumberStyles.HexNumber));
            } else if (token.StartsWith("[") && token.EndsWith("]")) {
               var content = token.Substring(1, token.Length - 2);
               args.Add(new ArrayArg(content));
            } else if (ScriptArg.IsValidToken(token)) {
               args.Add(new ScriptArg(token));
            } else {
               LineCommand = token;
            }
         }

         LineCode = lineCode;
         Args = args;
      }

      public static IReadOnlyList<int> ExtractMatchingGames(ref string line) {
         if (!line.StartsWith("[")) return null;
         var gamesEnd = line.IndexOf("]");
         if (gamesEnd == -1) return null;
         var games = line.Substring(1, gamesEnd - 1);
         line = line.Substring(gamesEnd + 1).TrimStart();
         return games.Split("_").Select(ConvertAscii).ToList();
      }

      public static IReadOnlyList<string> GetMatchingGames(IScriptLine line) {
         var names = new[] { "AXVE", "AXPE", "BPRE", "BPGE", "BPEE" };
         return names.Where(name => line.MatchesGame(ConvertAscii(name))).ToList();
      }

      public static int ConvertAscii(string letters) {
         return letters.Reverse().Aggregate(0, (current, letter) => (current << 8) | (byte)letter);
      }

      public bool MatchesGame(int game) => matchingGames?.Contains(game) ?? true;

      public void AddDocumentation(string doc) => documentation.Add(doc);

      public bool PartialMatchLine(string line) => LineCommand.MatchesPartial(line.Split(' ')[0]);

      public bool Matches(int gameCodeHash, IReadOnlyList<byte> data, int index) {
         if (index + LineCode.Count >= data.Count) return false;
         if (MatchesGame(gameCodeHash)) {
            var result = true;
            for (int i = 0; result && i < LineCode.Count; i++) result = data[index + i] == LineCode[i]; // avoid making lambda for performance
            return result;
         }
         return false;
      }

      public bool CanCompile(string line) {
         if (!(line + " ").StartsWith(LineCommand + " ", StringComparison.CurrentCultureIgnoreCase)) return false;
         if (LineCode.Count == 1) return true;
         var tokens = Tokenize(line).ToList();
         if (tokens.Count < LineCode.Count) return false;
         tokens.RemoveAt(0);
         for (int i = 1; i < LineCode.Count; i++) {
            if (!byte.TryParse(tokens[0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var value)) return false;
            if (value != LineCode[i]) return false;
            tokens.RemoveAt(0);
         }
         return true;
      }

      public string ErrorCheck(string scriptLine, out string[] tokens) {
         tokens = Tokenize(scriptLine);
         if (!tokens[0].Equals(LineCommand, StringComparison.CurrentCultureIgnoreCase)) throw new ArgumentException($"Command {LineCommand} was expected, but received {tokens[0]} instead.");
         var commandText = LineCommand;
         for (int i = 1; i < LineCode.Count; i++) commandText += " " + LineCode[i].ToString("X2");
         var fillerCount = Args.Count(arg => arg.Name == "filler");
         for (int i = 0; i < fillerCount; i++) {
            if (tokens.Length < Args.Count + LineCode.Count) tokens = tokens.Append("0").ToArray();
         }
         if (Args.Count > 0 && Args.Last() is ArrayArg) {
            if (Args.Count > tokens.Length) {
               return $"Command {commandText} expects {Args.Count} arguments, but received {tokens.Length - LineCode.Count} instead.";
            }
         } else if (Args.Count != tokens.Length - LineCode.Count) {
            return $"Command {commandText} expects {Args.Count} arguments, but received {tokens.Length - LineCode.Count} instead.";
         }
         return null;
      }

      public string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result) {
         result = null;
         var error = ErrorCheck(scriptLine, out var tokens);
         if (error != null) return error;
         var results = new List<byte>(LineCode);
         start += LineCode.Count;
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg scriptArg) {
               var token = tokens[i + LineCode.Count];
               var message = scriptArg.Build(model, start, token, results, labels);
               if (message != null) return message;
               start += scriptArg.Length(model, start);
            } else if (Args[i] is ArrayArg arrayArg) {
               var values = arrayArg.ConvertMany(model, tokens.Skip(i + 1)).ToList();
               results.Add((byte)values.Count);
               start += 1;
               foreach (var value in values) {
                  if (Args[i].Type == ArgType.Byte) {
                     results.Add((byte)value);
                     start += 1;
                  } else if (Args[i].Type == ArgType.Short) {
                     results.Add((byte)value);
                     results.Add((byte)(value >> 8));
                     start += 2;
                  } else if (Args[i].Type == ArgType.Word) {
                     results.Add((byte)value);
                     results.Add((byte)(value >> 0x8));
                     results.Add((byte)(value >> 0x10));
                     results.Add((byte)(value >> 0x18));
                     start += 4;
                  } else {
                     throw new NotImplementedException();
                  }
               }
            }
         }
         result = results.ToArray();
         return null;
      }

      public string Decompile(IDataModel data, int start, DecompileLabelLibrary labels) {
         for (int i = 0; i < LineCode.Count; i++) {
            if (LineCode[i] != data[start + i]) throw new ArgumentException($"Data at {start:X6} does not match the {LineCommand} command.");
         }
         var allFillerIsZero = IsAllFillerZero(data, start);
         start += LineCode.Count;
         var builder = new StringBuilder(LineCommand);
         for (int i = 1; i < LineCode.Count; i++) {
            builder.Append(" " + LineCode[i].ToHexString());
         }

         var streamContent = new List<string>();
         foreach (var arg in Args) {
            builder.Append(" ");
            if (arg is ScriptArg scriptArg) {
               if (scriptArg.Build(allFillerIsZero, data, start, builder, streamContent, labels)) continue;
            } else if (arg is ArrayArg arrayArg) {
               builder.Append(arrayArg.ConvertMany(data, start));
            } else {
               throw new NotImplementedException();
            }
            start += arg.Length(data, start);
         }
         foreach (var content in streamContent) {
            builder.AppendLine();
            builder.AppendLine("{");
            builder.AppendLine(content);
            builder.Append("}");
         }
         return builder.ToString();
      }

      private bool IsAllFillerZero(IDataModel data, int start) {
         start += LineCode.Count;
         foreach (var arg in Args) {
            if (arg.Name == "filler") {
               var value = data.ReadMultiByteValue(start, arg.Length(data, start));
               if (value != 0) return false;
            }
            start += arg.Length(data, start);
         }
         return true;
      }

      public static string ReadString(IDataModel data, int start) {
         var length = PCSString.ReadString(data, start, true);
         return data.TextConverter.Convert(data, start, length);
      }

      public static string[] Tokenize(string scriptLine) {
         var result = new List<string>();
         var quoteCut = scriptLine.Split('"');

         for (int i = 0; i < quoteCut.Length; i++) {
            if (i % 2 == 0 && quoteCut[i].Length == 0) continue;

            if (i % 2 == 1) result.Add($"\"{quoteCut[i]}\"");
            else result.AddRange(quoteCut[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
         }

         return result.ToArray();
      }

      public override string ToString() {
         return string.Join(" ", LineCode.Select(code => code.ToHexString()).Concat(Args.Select(arg => arg.Name)).ToArray());
      }
   }

   public class XSEScriptLine : ScriptLine {
      public XSEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x02, 0x03, 0x05, 0x08, 0x0A, 0x0C, 0x0D);
   }

   public class BSEScriptLine : ScriptLine {
      public BSEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x28, 0x3c, 0x3d, 0x3e, 0x3f);
   }

   public class ASEScriptLine : ScriptLine {
      public ASEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x08, 0x0F, 0x11, 0x13);
   }

   public class TSEScriptLine : ScriptLine {
      public TSEScriptLine(string engineLine) : base(engineLine) { }
      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x45, 0x47, 0x59, 0x5A);
   }

   public interface IScriptArg {
      ArgType Type { get; }
      ExpectedPointerType PointerType { get; }
      string Name { get; }
      string EnumTableName { get; }

      int Length(IDataModel model, int start);
   }

   public class ScriptArg : IScriptArg {
      private int length;

      public ArgType Type { get; }
      public ExpectedPointerType PointerType { get; }
      public string Name { get; }
      public string EnumTableName { get; }
      public int EnumOffset { get; }

      public int Length(IDataModel model, int start) => length;

      public ScriptArg(string token) {
         (Type, PointerType, Name, EnumTableName, length) = Construct(token);
         if (EnumTableName == null) return;
         if (EnumTableName.Contains("+")) {
            var parts = EnumTableName.Split(new[] { '+' }, 2);
            EnumTableName = parts[0];
            if (parts[1].TryParseInt(out var result)) EnumOffset = result;
         } else if (EnumTableName.Contains("-")) {
            var parts = EnumTableName.Split(new[] { '-' }, 2);
            EnumTableName = parts[0];
            if (parts[1].TryParseInt(out var result)) EnumOffset = -result;
         }
      }

      public static (ArgType type, ExpectedPointerType pointerType, string name, string enumTableName, int length) Construct(string token) {
         if (token.Contains("<>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Unknown, name, default, length);
         } else if (token.Contains("<\"\">")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<\"\">" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Text, name, default, length);
         } else if (token.Contains("<`mart`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`mart`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Mart, name, default, length);
         } else if (token.Contains("<`decor`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`decor`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Decor, name, default, length);
         } else if (token.Contains("<`move`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`move`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Movement, name, default, length);
         } else if (token.Contains("<`oam`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`oam`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.SpriteTemplate, name, default, length);

         } else if (token.Contains("<`xse`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`xse`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);
         } else if (token.Contains("<`bse`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`bse`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);
         } else if (token.Contains("<`ase`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`ase`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);
         } else if (token.Contains("<`tse`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`tse`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);

         } else if (token.Contains("::")) {
            var (type, length) = (ArgType.Word, 4);
            var name = token.Split(new[] { "::" }, StringSplitOptions.None).First();
            var enumTableName = token.Split("::").Last();
            return (type, default, name, enumTableName, length);
         } else if (token.Contains(':')) {
            var (type, length) = (ArgType.Short, 2);
            var name = token.Split(':').First();
            var enumTableName = token.Split(':').Last();
            return (type, default, name, enumTableName, length);
         } else if (token.Contains('.')) {
            var (type, length) = (ArgType.Byte, 1);
            var parts = token.Split(new[] { '.' }, 2);
            var name = parts[0];
            var enumTableName = parts[1];
            return (type, default, name, enumTableName, length);
         } else {
            // didn't find a token :(
            // I guess it's a byte?
            var (type, length) = (ArgType.Byte, 1);
            var name = token;
            return (type, default, name, default, length);
         }
      }

      public static bool IsValidToken(string token) {
         return "<> <`xse`> <`bse`> <`ase`> <`tse`> <\"\"> <`mart`> <`decor`> <`move`> <`oam`> : .".Split(' ').Any(token.Contains);
      }

      public string Convert(IDataModel model, int value, int bytes) {
         var preferHex = EnumTableName?.EndsWith("|h") ?? false;
         var preferSign = EnumTableName?.EndsWith("|z") ?? false;
         var enumName = EnumTableName?.Split('|')[0];
         var table = string.IsNullOrEmpty(enumName) ? null : model.GetOptions(enumName);
         if (table == null || value - EnumOffset < 0 || table.Count <= value - EnumOffset || string.IsNullOrEmpty(table[value])) {
            if (preferHex || value == int.MinValue || Math.Abs(value) >= 0x4000) {
               return "0x" + ((uint)value).ToString($"X{length * 2}");
            } else {
               if (bytes == 1 && preferSign) value = (sbyte)value;
               if (bytes == 2 && preferSign) value = (short)value;
               return value.ToString();
            }
         }
         return table[value - EnumOffset];
      }

      public int Convert(IDataModel model, string value) {
         int result;
         if (!string.IsNullOrEmpty(EnumTableName)) {
            if (ArrayRunEnumSegment.TryParse(EnumTableName, model, value, out result)) return result + EnumOffset;
         }
         if (value.StartsWith("0x") && value.Substring(2).TryParseHex(out result)) return result;
         if (value.StartsWith("0X") && value.Substring(2).TryParseHex(out result)) return result;
         if (value.StartsWith("$") && value.Substring(1).TryParseHex(out result)) return result;
         if (int.TryParse(value, out result)) return result;
         return 0;
      }

      /// <summary>
      /// Build from compiled bytes to text.
      /// </summary>
      public bool Build(bool allFillerIsZero, IDataModel data, int start, StringBuilder builder, List<string> streamContent, DecompileLabelLibrary labels) {
         if (allFillerIsZero && Name == "filler") return true;
         if (Type == ArgType.Byte) builder.Append(Convert(data, data[start], 1));
         if (Type == ArgType.Short) builder.Append(Convert(data, data.ReadMultiByteValue(start, 2), 2));
         if (Type == ArgType.Word) builder.Append(Convert(data, data.ReadMultiByteValue(start, 4), 4));
         if (Type == ArgType.Pointer) {
            var address = data.ReadMultiByteValue(start, 4);
            if (address < 0x8000000) {
               builder.Append(labels.AddressToLabel(address, Type == ArgType.Pointer && PointerType == ExpectedPointerType.Script));
            } else {
               address -= 0x8000000;
               builder.Append($"<{labels.AddressToLabel(address, Type == ArgType.Pointer && PointerType == ExpectedPointerType.Script)}>");
               if (PointerType != ExpectedPointerType.Unknown) {
                  if (data.GetNextRun(address) is IStreamRun stream && stream.Start == address) {
                     streamContent.Add(stream.SerializeRun());
                  }
               }
            }
         }
         return false;
      }

      /// <summary>
      /// Build from text to compiled bytes.
      /// </summary>
      public string Build(IDataModel model, int address, string token, IList<byte> results, LabelLibrary labels) {
         if (Type == ArgType.Byte) {
            results.Add((byte)Convert(model, token));
         } else if (Type == ArgType.Short) {
            var value = Convert(model, token);
            results.Add((byte)value);
            results.Add((byte)(value >> 8));
         } else if (Type == ArgType.Word) {
            var value = Convert(model, token);
            results.Add((byte)value);
            results.Add((byte)(value >> 0x8));
            results.Add((byte)(value >> 0x10));
            results.Add((byte)(value >> 0x18));
         } else if (Type == ArgType.Pointer) {
            int value;
            if (token.StartsWith("<")) {
               if (!token.EndsWith(">")) return "Unmatched <>";
               token = token.Substring(1, token.Length - 2);
            }
            if (token.StartsWith("0x")) {
               token = token.Substring(2);
            }
            if (token == "auto") {
               if (PointerType == ExpectedPointerType.Script || PointerType == ExpectedPointerType.Unknown) {
                  return "<auto> only supported for text/data.";
               }
               value = Pointer.NULL + DeferredStreamToken.AutoSentinel;
            } else if (labels.TryResolveLabel(token, out value)) {
               // resolved to an address
            } else if (token.TryParseHex(out value)) {
               // pointer *is* an address: nothing else to do
               if (value > -Pointer.NULL) value += Pointer.NULL;
               //       public bool RequireCompleteAddresses { get; set; } = true;
               if (labels.RequireCompleteAddresses && (token.Length < 6 || token.Length > 7)) {
                  return "Script addresses must be 6 or 7 characters long.";
               }
            } else if (PointerType != ExpectedPointerType.Script) {
               return $"'{token}' is not a valid pointer.";
            } else {
               labels.AddUnresolvedLabel(token, address);
               value = Pointer.NULL;
            }
            value -= Pointer.NULL;
            results.Add((byte)value);
            results.Add((byte)(value >> 0x8));
            results.Add((byte)(value >> 0x10));
            results.Add((byte)(value >> 0x18));
         } else {
            throw new NotImplementedException();
         }
         return null;
      }
   }

   public class SilentMatchArg : IScriptArg {
      public ArgType Type => ArgType.Byte;
      public ExpectedPointerType PointerType => ExpectedPointerType.Unknown;
      public string Name => null;
      public string EnumTableName => null;
      public int EnumOffset => 0;

      public int Length(IDataModel model, int start) => 1;

      public byte ExpectedValue { get; }
      public SilentMatchArg(byte value) => ExpectedValue = value;
   }

   public class ArrayArg : IScriptArg {
      public ArgType Type { get; }
      public string Name { get; }
      public string EnumTableName { get; }
      public int TokenLength { get; }
      public ExpectedPointerType PointerType => ExpectedPointerType.Unknown;

      public int Length(IDataModel model, int start) {
         return model[start] * TokenLength + 1;
      }

      public ArrayArg(string token) {
         (Type, _, Name, EnumTableName, TokenLength) = ScriptArg.Construct(token);
      }

      public string ConvertMany(IDataModel model, int start) {
         var result = new StringBuilder();
         var count = model[start];
         start++;
         for (int i = 0; i < count; i++) {
            var value = model.ReadMultiByteValue(start, TokenLength);
            start += TokenLength;
            var tokenText = "0x" + value.ToString($"X{TokenLength * 2}");
            if (!string.IsNullOrEmpty(EnumTableName)) {
               var table = model.GetOptions(EnumTableName);
               if ((table?.Count ?? 0) > value) {
                  tokenText = table[value];
               }
            }
            result.Append(tokenText);
            if (i < count - 1) result.Append(' ');
         }
         return result.ToString();
      }

      public IEnumerable<int> ConvertMany(IDataModel model, IEnumerable<string> info) {
         foreach (var token in info) {
            if (string.IsNullOrEmpty(EnumTableName)) {
               if (token.StartsWith("0x") && token.Substring(2).TryParseHex(out var result)) yield return result;
               else if (token.StartsWith("0X") && token.Substring(2).TryParseHex(out result)) yield return result;
               else if (token.StartsWith("$") && token.Substring(1).TryParseHex(out result)) yield return result;
               else if (int.TryParse(token, out result)) yield return result;
               else yield return 0;
            } else if (ArrayRunEnumSegment.TryParse(EnumTableName, model, token, out var enumValue)) {
               yield return enumValue;
            } else {
               yield return 0;
            }
         }
      }
   }

   public enum ArgType {
      Byte,
      Short,
      Word,
      Pointer,
   }

   public static class ScriptExtensions {
      public static MacroScriptLine GetMatchingMacro(this IReadOnlyList<IScriptLine> self, int gameHash, IReadOnlyList<byte>data, int start) {
         return (MacroScriptLine)self.FirstOrDefault(option => option is MacroScriptLine && option.Matches(gameHash, data, start));
      }

      /// <summary>
      /// Does not consider macros. Only returns individual lines.
      /// </summary>
      public static ScriptLine GetMatchingLine(this IReadOnlyList<IScriptLine> self, int gameHash, IReadOnlyList<byte> data, int start) {
         return (ScriptLine)self.FirstOrDefault(option => option is ScriptLine && option.Matches(gameHash, data, start));
      }

      public static int GetScriptSegmentLength(this IReadOnlyList<IScriptLine> self, int gameHash, IDataModel model, int address, IDictionary<int, int> destinationLengths) {
         int length = 0;
         int lastCommand = -1, repeateLength = 1;
         while (true) {
            var line = self.GetMatchingLine(gameHash, model, address + length);
            if (line == null) break;
            length += line.CompiledByteLength(model, address + length, destinationLengths);
            if (line.IsEndingCommand) break;

            if (line.LineCode[0] != lastCommand) (lastCommand, repeateLength) = (line.LineCode[0], 1);
            else repeateLength += 1;
            if (line.Args.Count > 0) repeateLength = 0;
            if (repeateLength > ScriptParser.MaxRepeates) break; // same command lots of times in a row is fishy
         }

         // concatenate destinations directly after the current script
         // (only if the destination is only used once)
         while (true) {
            if (destinationLengths.TryGetValue(address + length, out int argLength) && argLength > 0) {
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count == 1) {
                  length += argLength;
                  continue;
               }
            }
            if (destinationLengths.TryGetValue(address + length + 1, out argLength)) {
               // there was a skip... should we ignore it?
               // If something points to that position, we can't keep going.
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

               anchor = model.GetNextAnchor(address + length + 1);
               if (anchor.Start == address + length + 1 && anchor.PointerSources.Count == 1) {
                  length += argLength + 1;
                  continue;
               }
            }
            if (destinationLengths.TryGetValue(address + length + 2, out argLength)) {
               // there was a skip... should we ignore it?
               // If something points to that position, we can't keep going.
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

               anchor = model.GetNextAnchor(address + length + 2);
               if (anchor.Start == address + length + 2 && anchor.PointerSources.Count == 1) {
                  length += argLength + 2;
                  continue;
               }
            }
            if (destinationLengths.TryGetValue(address + length + 3, out argLength)) {
               // there was a skip... should we ignore it?
               // If something points to that position, we can't keep going.
               var anchor = model.GetNextAnchor(address + length);
               if (anchor.Start == address + length && anchor.PointerSources.Count > 0) break;

               anchor = model.GetNextAnchor(address + length + 3);
               if (anchor.Start == address + length + 3 && anchor.PointerSources.Count == 1) {
                  length += argLength + 3;
                  continue;
               }
            }
            break;
         }

         return length;
      }
   }

   public class DeferredStreamToken {
      public const int AutoSentinel = -0xAAAA;

      private readonly int pointerOffset;
      private readonly string format;
      private byte[] content;
      private int alignment;

      public int ContentLength => content.Length;

      public DeferredStreamToken(int pointerOffset, string format, byte[] defaultContent, int alignment) {
         this.pointerOffset = pointerOffset;
         this.format = format;
         this.content = defaultContent;
         this.alignment = alignment;
      }

      // need the model not for insertion, but for text encoding
      public void UpdateContent(IDataModel model, ExpectedPointerType type, string text) {
         if (type == ExpectedPointerType.Text) {
            content = model.TextConverter.Convert(text, out _).ToArray();
         } else if (type == ExpectedPointerType.Mart) {
            //   [item:{HardcodeTablesModel.ItemsTableName}]!0000
            var data = new List<byte>();
            foreach (var line in text.SplitLines()) {
               if (string.IsNullOrWhiteSpace(line)) continue;
               if (!ArrayRunEnumSegment.TryParse(HardcodeTablesModel.ItemsTableName, model, line, out int value)) continue;
               data.AddShort(value);
            }
            data.AddShort(0);
            content = data.ToArray();
         } else if (type == ExpectedPointerType.Mart) {
            //   [item:{HardcodeTablesModel.DecorationsTableName}]!0000
            var data = new List<byte>();
            foreach (var line in text.SplitLines()) {
               if (string.IsNullOrWhiteSpace(line)) continue;
               if (!ArrayRunEnumSegment.TryParse(HardcodeTablesModel.DecorationsTableName, model, line, out int value)) continue;
               data.AddShort(value);
            }
            data.AddShort(0);
            content = data.ToArray();
         } else if (type == ExpectedPointerType.Movement) {
            //   $"[move.movementtypes]!FE",
            var data = new List<byte>();
            foreach (var line in text.SplitLines()) {
               if (string.IsNullOrWhiteSpace(line)) continue;
               if (!ArrayRunEnumSegment.TryParse("movementtypes", model, line, out int value)) continue;
               data.Add((byte)value);
            }
            data.Add(0xFE);
            content = data.ToArray();
         } else {
            throw new NotImplementedException();
         }
      }

      public void WriteData(IDataModel model, ModelDelta token, int scriptStart, int contentOffset) {
         model.ClearFormat(token, scriptStart + pointerOffset, 4);
         model.WritePointer(token, scriptStart + pointerOffset, scriptStart + contentOffset);
         model.ObserveRunWritten(token, new PointerRun(scriptStart + pointerOffset));
         token.ChangeData(model, scriptStart + contentOffset, content);
         var strategy = new FormatRunFactory(default).GetStrategy(format);
         strategy.TryAddFormatAtDestination(model, token, scriptStart + pointerOffset, scriptStart + contentOffset, default, default, default);
      }

      public void WriteData(IList<byte> data, int scriptStart) {
         while ((scriptStart + data.Count) % alignment != 0) data.Add(0);
         var address = scriptStart + data.Count - Pointer.NULL;
         data[pointerOffset + 0] = (byte)(address >> 0);
         data[pointerOffset + 1] = (byte)(address >> 8);
         data[pointerOffset + 2] = (byte)(address >> 16);
         data[pointerOffset + 3] = (byte)(address >> 24);
         data.AddRange(content);
      }
   }
}
