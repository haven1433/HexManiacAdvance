using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public class ScriptParser {
      private readonly IReadOnlyList<ScriptLine> engine;
      private readonly byte endToken;

      public event EventHandler<string> CompileError;

      public ScriptParser(IReadOnlyList<ScriptLine> engine, byte endToken) => (this.engine, this.endToken) = (engine, endToken);

      public int GetScriptSegmentLength(IDataModel model, int address) => engine.GetScriptSegmentLength(model, address);

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
            address = scripts[i];
            int length = 0;
            while (true) {
               var line = engine.GetMatchingLine(model, address + length);
               if (line == null) break;
               length += line.LineCode.Count;
               foreach (var arg in line.Args) {
                  if (arg.Type == ArgType.Pointer) {
                     var destination = model.ReadPointer(address + length);
                     if (destination >= 0 && destination < model.Count &&
                        line.PointsToNextScript &&
                        !scripts.Contains(destination) &&
                        i.Range().All(j => destination < scripts[j] || scripts[j] + lengths[j] <= destination)
                     ) {
                        scripts.Add(destination);
                     }
                  }
                  length += arg.Length(model, address + length);
               }
               if (line.IsEndingCommand) break;
            }

            scripts.RemoveAll(start => start > address && start < address + length);
            lengths.Add(length);
         }

         return scripts;
      }

      public int FindLength(IDataModel model, int address) {
         int length = 0;
         int consecutiveNops = 0;

         while (true) {
            var line = engine.GetMatchingLine(model, address + length);
            if (line == null) break;
            consecutiveNops = line.LineCommand.StartsWith("nop") ? consecutiveNops + 1 : 0;
            if (consecutiveNops > 16) return 0;
            length += line.CompiledByteLength(model, address + length);
            if (line.IsEndingCommand) break;
         }

         return length;
      }

      // TODO refactor to rely on CollectScripts rather than duplicate code
      public void FormatScript<TSERun>(ModelDelta token, IDataModel model, int address) where TSERun : IScriptStartRun {
         Func<int, SortedSpan<int>, IScriptStartRun> constructor = (a, s) => new XSERun(a, s);
         if (typeof(TSERun) == typeof(BSERun)) constructor = (a, s) => new BSERun(a, s);
         if (typeof(TSERun) == typeof(ASERun)) constructor = (a, s) => new ASERun(a, s);

         var processed = new List<int>();
         var toProcess = new List<int> { address };
         while (toProcess.Count > 0) {
            address = toProcess.Last();
            toProcess.RemoveAt(toProcess.Count - 1);
            if (processed.Contains(address)) continue;
            var existingRun = model.GetNextRun(address);
            if (!(existingRun is TSERun) && existingRun.Start == address) {
               var anchorName = model.GetAnchorFromAddress(-1, address);
               model.ObserveAnchorWritten(token, anchorName, constructor(address, existingRun.PointerSources));
            }
            int length = 0;
            while (true) {
               var line = engine.GetMatchingLine(model, address + length);
               if (line == null) break;
               // there may've previously been a pointer here: the code has changed!
               if (model.GetNextRun(address + length) is PointerRun pRun && pRun.Start == address + length) model.ClearFormat(token, address + length, line.LineCode.Count);
               length += line.LineCode.Count;
               foreach (var arg in line.Args) {
                  if (arg.Type != ArgType.Pointer) {
                     // there may've previously been a pointer here: the code has changed!
                     // when clearing args, we actually _do_ want to clear any anchors that point to them.
                     model.ClearFormat(token, address + length, arg.Length(model, address + length));
                  } else {
                     var destination = model.ReadPointer(address + length);
                     if (destination >= 0 && destination < model.Count) {
                        model.ClearFormat(token, address + length, 4);
                        model.ObserveRunWritten(token, new PointerRun(address + length));
                        if (line.PointsToNextScript) toProcess.Add(destination);
                        if (line.PointsToText) {
                           var destinationLength = PCSString.ReadString(model, destination, true);
                           if (destinationLength > 0) {
                              // if there's an anchor that starts exactly here, we don't want to clear it: just update it
                              // if the run starts somewhere else, we better to a clear to prevent conflicting formats
                              var existingTextRun = model.GetNextRun(destination);
                              if (existingTextRun.Start != destination) {
                                 model.ClearFormat(token, destination, destinationLength);
                                 model.ObserveRunWritten(token, new PCSRun(model, destination, destinationLength));
                              } else {
                                 model.ClearAnchor(token, destination + existingTextRun.Length, destinationLength - existingTextRun.Length); // assuming that the old run ends before the new run, clear the difference
                                 model.ObserveRunWritten(token, new PCSRun(model, destination, destinationLength, SortedSpan.One(address + length)));
                                 model.ClearAnchor(token, destination + destinationLength, existingTextRun.Length - destinationLength); // assuming that the new run ends before the old run, clear the difference
                              }
                           }
                        } else if (line.PointsToMovement) {
                           WriteMovementStream(model, token, destination, address + length);
                        } else if (line.PointsToMart) {
                           WriteMartStream(model, token, destination, address + length);
                        } else if (line.PointsToSpriteTemplate) {
                           WriteSpriteTemplateStream(model, token, destination, address + length);
                        }
                     }
                  }
                  length += arg.Length(model, address + length);
               }
               if (line.IsEndingCommand) break;
            }
            processed.Add(address);
         }
      }

      private void WriteMovementStream(IDataModel model, ModelDelta token, int start, int source) {
         var format = "[move.movementtypes]!FE";
         WriteStream(model, token, start, source, format);
      }

      private void WriteMartStream(IDataModel model, ModelDelta token, int start, int source) {
         var format = $"[item:{HardcodeTablesModel.ItemsTableName}]!0000";
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
            if (model.GetNextRun(tsRun.Start) is ITableRun existingRun && existingRun.Start == tsRun.Start && tsRun.DataFormatMatches(existingRun)) {
               // no need to update the format, the format already matches what we want
            } else {
               model.ClearFormat(token, tsRun.Start, tsRun.Length);
               model.ObserveRunWritten(token, tsRun);
            }
         }
      }

      /// <summary>
      /// Potentially edits the script text and returns a set of data repoints.
      /// The data is moved, but the script itself has not written by this method.
      /// </summary>
      /// <param name="token"></param>
      /// <param name="model"></param>
      /// <param name="start"></param>
      /// <param name="script"></param>
      /// <param name="movedData"></param>
      /// <returns></returns>
      public byte[] Compile(ModelDelta token, IDataModel model, int start, ref string script, out IReadOnlyList<(int originalLocation, int newLocation)> movedData) {
         movedData = new List<(int, int)>();
         var lines = script.Split(new[] { '\n', '\r' }, StringSplitOptions.None)
            .Select(line => line.Split('#').First())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
         var result = new List<byte>();
         int streamLocation = -1, streamPointerLocation = -1;

         var labels = ExtractLocalLabels(model, start, lines);

         for (var i = 0; i < lines.Length; i++) {
            var line = lines[i].Trim();
            if (line.EndsWith(":")) continue; // label, not code. Don't parse.
            if (line == "{") {
               var streamStart = i + 1;
               var indentCount = 1;
               i += 1;
               while (indentCount > 0) {
                  line = lines[i].Trim();
                  if (line == "{") indentCount += 1;
                  if (line == "}") indentCount -= 1;
                  i += 1;
                  if (i == lines.Length) break;
               }
               i -= 1;
               var streamEnd = i;
               var streamLines = lines.Skip(streamStart).Take(streamEnd - streamStart).ToList();
               var stream = streamLines.Aggregate(string.Empty, (a, b) => a + Environment.NewLine + b);

               // Let the stream run handle updating itself based on the stream content.
               if (streamLocation >= 0 && streamPointerLocation >= 0) {
                  if (model.GetNextRun(streamLocation) is IStreamRun streamRun && streamRun.Start == streamLocation) {
                     streamRun = streamRun.DeserializeRun(stream, token, out var _); // we don't notify parents/children based on script-stream changes: we they never have parents/children.
                     // alter script content and compiled byte location based on stream move
                     if (streamRun.Start != streamLocation) {
                        script = script.Replace(streamLocation.ToString("X6"), streamRun.Start.ToString("X6"));
                        result[streamPointerLocation + 0] = (byte)(streamRun.Start >> 0);
                        result[streamPointerLocation + 1] = (byte)(streamRun.Start >> 8);
                        result[streamPointerLocation + 2] = (byte)(streamRun.Start >> 16);
                        result[streamPointerLocation + 3] = (byte)((streamRun.Start >> 24) + 0x08);
                        ((List<(int, int)>)movedData).Add((streamLocation, streamRun.Start));
                     }
                  }
               }
               continue;
            }
            streamLocation = -1; streamPointerLocation = -1;
            foreach (var command in engine) {
               if (!command.CanCompile(line)) continue;
               var currentSize = result.Count;

               if (line.Contains("<??????>")) {
                  int newAddress = -1;
                  if (command.PointsToMovement) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, 0xFE);
                     WriteMovementStream(model, token, newAddress, -1);
                  } else if (command.PointsToText) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     if (newAddress == -1) {
                        var endLength = model.Count;
                        model.ExpandData(token, endLength + 0x10);
                        newAddress = endLength + 0x10;
                     }
                     token.ChangeData(model, newAddress, 0xFF);
                     model.ObserveRunWritten(token, new PCSRun(model, newAddress, 1));
                  } else if (command.PointsToMart) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, 0x00);
                     token.ChangeData(model, newAddress + 1, 0x00);
                     WriteMartStream(model, token, newAddress, -1);
                  } else if (command.PointsToSpriteTemplate) {
                     newAddress = model.FindFreeSpace(0, 0x18);
                     for (int j = 0; j < 0x18; j++) token.ChangeData(model, newAddress + j, 0x00);
                     WriteSpriteTemplateStream(model, token, newAddress, -1);
                  } else if (command.PointsToNextScript) {
                     newAddress = model.FindFreeSpace(0, 0x10);
                     token.ChangeData(model, newAddress, endToken);
                     // TODO write a new script template... an anchor with a format based on the current script
                  }
                  if (newAddress != -1) {
                     line = line.ReplaceOne("<??????>", $"<{newAddress:X6}>");
                     if (command.PointsToNextScript) {
                        script = script.ReplaceOne("<??????>", $"<{newAddress:X6}>");
                     } else {
                        script = script.ReplaceOne("<??????>", $"<{newAddress:X6}>{Environment.NewLine}{{{Environment.NewLine}}}");
                     }
                  }
               }

               var error = command.Compile(model, start, line, labels, out var code);
               if (error == null) {
                  result.AddRange(code);
               } else {
                  CompileError?.Invoke(this, i + ": " + error);
                  return null;
               }
               if (command.PointsToMovement || command.PointsToText || command.PointsToMart || command.PointsToSpriteTemplate) {
                  var pointerOffset = command.Args.Until(arg => arg.Type == ArgType.Pointer).Sum(arg => arg.Length(model, -1)) + command.LineCode.Count;
                  var destination = result.ReadMultiByteValue(currentSize + pointerOffset, 4) - 0x8000000;
                  if (destination >= 0) {
                     streamPointerLocation = currentSize + pointerOffset;
                     streamLocation = destination;
                  }
               }

               break;
            }
         }

         if (result.Count == 0) result.Add(endToken); // end
         return result.ToArray();
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
               if (!(line + " ").StartsWith(command.LineCommand + " ")) continue;
               length += command.CompiledByteLength(model, line);
               break;
            }
         }
         return new LabelLibrary(model, labels);
      }

      public string GetHelp(string currentLine) {
         if (string.IsNullOrWhiteSpace(currentLine)) return null;
         var tokens = ScriptLine.Tokenize(currentLine.Trim());
         var candidates = engine.Where(line => line.LineCommand.Contains(tokens[0])).ToList();
         if (candidates.Count > 10) return null;
         if (candidates.Count == 0) return null;
         if (candidates.Count == 1) {
            if (candidates[0].Args.Count == tokens.Length - 1) return null;
            return candidates[0].Usage + Environment.NewLine + string.Join(Environment.NewLine, candidates[0].Documentation);
         }
         var perfectMatch = candidates.FirstOrDefault(candidate => (currentLine + " ").StartsWith(candidate.LineCommand + " "));
         if (perfectMatch != null) {
            if (perfectMatch.Args.Count == tokens.Length - 1) return null;
            return perfectMatch.Usage + Environment.NewLine + string.Join(Environment.NewLine, perfectMatch.Documentation);
         }
         return string.Join(Environment.NewLine, candidates.Select(line => line.Usage));
      }

      private string[] Decompile(IDataModel data, int index, int length) {
         var results = new List<string>();
         var nextAnchor = data.GetNextAnchor(index);
         while (length > 0) {
            if (index == nextAnchor.Start) {
               results.Add($"{nextAnchor.Start:X6}:");
               nextAnchor = data.GetNextAnchor(nextAnchor.Start + nextAnchor.Length);
            }

            var line = engine.FirstOrDefault(option => option.Matches(data, index));
            if (line == null) {
               results.Add($".raw {data[index]:X2}");
               index += 1;
               length -= 1;
            } else {
               results.Add("  " + line.Decompile(data, index));
               var compiledByteLength = line.CompiledByteLength(data, index);
               index += compiledByteLength;
               length -= compiledByteLength;
               if (line.IsEndingCommand) break;
            }
         }
         return results.ToArray();
      }
   }

   public interface IScriptLine {
      IReadOnlyList<IScriptArg> Args { get; }
      IReadOnlyList<byte> LineCode { get; }
      string LineCommand { get; }

      bool IsEndingCommand { get; }
      bool PointsToNextScript { get; }
      bool PointsToText { get; }
      bool PointsToMovement { get; }
      bool PointsToMart { get; }
      bool PointsToSpriteTemplate { get; }

      int CompiledByteLength(IDataModel model, int start); // compile from the bytes in the model, at that start location
      int CompiledByteLength(IDataModel model, string line); // compile from the line of code passed in
      bool Matches(IReadOnlyList<byte> data, int index);
      string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result);
      string Decompile(IDataModel data, int start);
   }

   public abstract class ScriptLine : IScriptLine {
      private readonly List<string> documentation = new List<string>();

      public const string Hex = "0123456789ABCDEF";
      public IReadOnlyList<IScriptArg> Args { get; }
      public IReadOnlyList<byte> LineCode { get; }
      public string LineCommand { get; }
      public IReadOnlyList<string> Documentation => documentation;
      public string Usage { get; }

      public virtual bool IsEndingCommand { get; }
      public virtual bool PointsToNextScript { get; }
      public virtual bool PointsToText { get; }
      public virtual bool PointsToMovement { get; }
      public virtual bool PointsToMart { get; }
      public virtual bool PointsToSpriteTemplate { get; }

      public int CompiledByteLength(IDataModel model, int start) {
         var length = LineCode.Count;
         foreach (var arg in Args) {
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
            } else if ("<> : .".Split(' ').Any(token.Contains)) {
               args.Add(new ScriptArg(token));
            } else {
               LineCommand = token;
            }
         }

         LineCode = lineCode;
         Args = args;
      }

      public void AddDocumentation(string doc) => documentation.Add(doc);

      public bool PartialMatchLine(string line) => LineCommand.MatchesPartial(line.Split(' ')[0]);

      public bool Matches(IReadOnlyList<byte> data, int index) {
         if (index + LineCode.Count >= data.Count) return false;
         return LineCode.Count.Range().All(i => data[index + i] == LineCode[i]);
      }

      public bool CanCompile(string line) {
         if (!(line + " ").StartsWith(LineCommand + " ")) return false;
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

      public string Compile(IDataModel model, int start, string scriptLine, LabelLibrary labels, out byte[] result) {
         result = null;
         var tokens = Tokenize(scriptLine);
         if (tokens[0] != LineCommand) throw new ArgumentException($"Command {LineCommand} was expected, but received {tokens[0]} instead.");
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
         var results = new List<byte>(LineCode);
         for (int i = 0; i < Args.Count; i++) {
            if (Args[i] is ScriptArg scriptArg) {
               var token = tokens[i + LineCode.Count];
               if (Args[i].Type == ArgType.Byte) {
                  results.Add((byte)scriptArg.Convert(model, token));
               } else if (Args[i].Type == ArgType.Short) {
                  var value = scriptArg.Convert(model, token);
                  results.Add((byte)value);
                  results.Add((byte)(value >> 8));
               } else if (Args[i].Type == ArgType.Word) {
                  var value = scriptArg.Convert(model, token);
                  results.Add((byte)value);
                  results.Add((byte)(value >> 0x8));
                  results.Add((byte)(value >> 0x10));
                  results.Add((byte)(value >> 0x18));
               } else if (Args[i].Type == ArgType.Pointer) {
                  int value;
                  if (token.StartsWith("<")) {
                     if (!token.EndsWith(">")) {
                        return "Unmatched <>";
                     }
                     token = token.Substring(1, token.Length - 2);
                     if (labels.TryResolveLabel(token, out value)) {
                        // resolved to an address
                     } else if (int.TryParse(token, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value)) {
                        // pointer *is* an address: nothing else to do
                     } else {
                        return $"Unable to parse {token} as a hex number.";
                     }
                     value -= Pointer.NULL;
                  } else {
                     if (labels.TryResolveLabel(token, out value)) {
                        // resolved to an address
                     } else if (int.TryParse(token, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value)) {
                        // pointer *is* an address: nothing else to do
                     } else {
                        return $"Unable to parse {token} as a hex number.";
                     }
                  }
                  results.Add((byte)value);
                  results.Add((byte)(value >> 0x8));
                  results.Add((byte)(value >> 0x10));
                  results.Add((byte)(value >> 0x18));
               } else {
                  throw new NotImplementedException();
               }
            } else if (Args[i] is ArrayArg arrayArg) {
               var values = arrayArg.ConvertMany(model, tokens.Skip(i + 1)).ToList();
               results.Add((byte)values.Count);
               foreach (var value in values) {
                  if (Args[i].Type == ArgType.Byte) {
                     results.Add((byte)value);
                  } else if (Args[i].Type == ArgType.Short) {
                     results.Add((byte)value);
                     results.Add((byte)(value >> 8));
                  } else if (Args[i].Type == ArgType.Word) {
                     results.Add((byte)value);
                     results.Add((byte)(value >> 0x8));
                     results.Add((byte)(value >> 0x10));
                     results.Add((byte)(value >> 0x18));
                  } else {
                     throw new NotImplementedException();
                  }
               }
            }
         }
         result = results.ToArray();
         return null;
      }

      public string Decompile(IDataModel data, int start) {
         for (int i = 0; i < LineCode.Count; i++) {
            if (LineCode[i] != data[start + i]) throw new ArgumentException($"Data at {start:X6} does not match the {LineCommand} command.");
         }
         var allFillerIsZero = IsAllFillerZero(data, start);
         start += LineCode.Count;
         var builder = new StringBuilder(LineCommand);
         for (int i = 1; i < LineCode.Count; i++) {
            builder.Append(" " + LineCode[i].ToHexString());
         }

         int lastAddress = -1;
         foreach (var arg in Args) {
            builder.Append(" ");
            if (arg is ScriptArg scriptArg) {
               if (allFillerIsZero && scriptArg.Name == "filler") continue;
               if (arg.Type == ArgType.Byte) builder.Append(scriptArg.Convert(data, data[start]));
               if (arg.Type == ArgType.Short) builder.Append(scriptArg.Convert(data, data.ReadMultiByteValue(start, 2)));
               if (arg.Type == ArgType.Word) builder.Append(scriptArg.Convert(data, data.ReadMultiByteValue(start, 4)));
               if (arg.Type == ArgType.Pointer) {
                  var address = data.ReadMultiByteValue(start, 4);
                  if (address < 0x8000000) {
                     builder.Append($"{address:X6}");
                  } else {
                     address -= 0x8000000;
                     builder.Append($"<{address:X6}>");
                     lastAddress = address;
                  }
               }
            } else if (arg is ArrayArg arrayArg) {
               builder.Append(arrayArg.ConvertMany(data, start));
            } else {
               throw new NotImplementedException();
            }
            start += arg.Length(data, start);
         }
         if (PointsToText || PointsToMovement || PointsToMart || PointsToSpriteTemplate) {
            if (data.GetNextRun(lastAddress) is IStreamRun stream && stream.Start == lastAddress) {
               builder.AppendLine();
               builder.AppendLine("{");
               builder.AppendLine(stream.SerializeRun());
               builder.Append("}");
            }
         }
         return builder.ToString();
      }

      private bool IsAllFillerZero(IDataModel data, int start) {
         start += LineCode.Count;
         foreach (var arg in Args) {
            var value = data.ReadMultiByteValue(start, arg.Length(data, start));
            if (value != 0) return false;
            start += arg.Length(data, start);
         }
         return true;
      }

      public static string ReadString(IReadOnlyList<byte> data, int start) {
         var length = PCSString.ReadString(data, start, true);
         return PCSString.Convert(data, start, length);
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
      public override bool PointsToNextScript => LineCode.Count == 1 && LineCode[0].IsAny<byte>(4, 5, 6, 7);
      public override bool PointsToText => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x0F, 0x67);
      public override bool PointsToMovement => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x4F, 0x50);
      public override bool PointsToMart => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x86, 0x87, 0x88);
   }

   public class BSEScriptLine : ScriptLine {
      public BSEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x28, 0x3c, 0x3d, 0x3e, 0x3f);
      public override bool PointsToNextScript => LineCode.Count == 1 && LineCode[0].IsAny<byte>(
         0x01,
         0x1C,
         0x1D,
         0x1E,
         0x1F,
         0x20,
         0x21,
         0x22,
         0x28,
         0x29,
         0x2A,
         0x2B,
         0x2C,
         0x2D,
         0x41,
         0x7A,
         0x97,
         0xE1
         );
   }

   public class ASEScriptLine : ScriptLine {
      public ASEScriptLine(string engineLine) : base(engineLine) { }

      public override bool IsEndingCommand => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x08, 0x0F, 0x11, 0x13);
      public override bool PointsToNextScript => LineCode.Count == 1 && LineCode[0].IsAny<byte>(0x0E, 0x11, 0x12, 0x13);
      public override bool PointsToSpriteTemplate => LineCode.Count == 1 && LineCode[0] == 0x02;
   }

   public interface IScriptArg {
      ArgType Type { get; }
      string Name { get; }
      string EnumTableName { get; }

      int Length(IDataModel model, int start);
   }

   public class ScriptArg : IScriptArg {
      private int length;

      public ArgType Type { get; }
      public string Name { get; }
      public string EnumTableName { get; }

      public int Length(IDataModel model, int start) => length;

      public ScriptArg(string token) {
         (Type, Name, EnumTableName, length) = Construct(token);
      }

      public static (ArgType type, string name, string enumTableName, int length) Construct(string token) {
         if (token.Contains("<>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<>" }, StringSplitOptions.None).First();
            return (type, name, default, length);
         } else if (token.Contains("::")) {
            var (type, length) = (ArgType.Word, 4);
            var name = token.Split(new[] { "::" }, StringSplitOptions.None).First();
            var enumTableName = token.Split("::").Last();
            return (type, name, enumTableName, length);
         } else if (token.Contains(":")) {
            var (type, length) = (ArgType.Short, 2);
            var name = token.Split(':').First();
            var enumTableName = token.Split(':').Last();
            return (type, name, enumTableName, length);
         } else if (token.Contains(".")) {
            var (type, length) = (ArgType.Byte, 1);
            var name = token.Split('.').First();
            var enumTableName = token.Split('.').Last();
            return (type, name, enumTableName, length);
         } else {
            // didn't find a token :(
            // I guess it's a byte?
            var (type, length) = (ArgType.Byte, 1);
            var name = token;
            return (type, name, default, length);
         }
      }

      public string Convert(IDataModel model, int value) {
         var preferHex = EnumTableName?.EndsWith("|h") ?? false;
         var enumName = EnumTableName?.Split('|')[0];
         var table = string.IsNullOrEmpty(enumName) ? null : model.GetOptions(enumName);
         if (table == null || table.Count <= value) {
            if (preferHex || Math.Abs(value) >= 0x4000) {
               return "0x" + ((uint)value).ToString($"X{length * 2}");
            } else {
               return value.ToString();
            }
         }
         return table[value];
      }

      public int Convert(IDataModel model, string value) {
         int result;
         if (!string.IsNullOrEmpty(EnumTableName)) {
            if (ArrayRunEnumSegment.TryParse(EnumTableName, model, value, out result)) return result;
         }
         if (value.StartsWith("0x") && value.Substring(2).TryParseHex(out result)) return result;
         if (value.StartsWith("0X") && value.Substring(2).TryParseHex(out result)) return result;
         if (value.StartsWith("$") && value.Substring(1).TryParseHex(out result)) return result;
         if (int.TryParse(value, out result)) return result;
         return 0;
      }
   }

   public class ArrayArg : IScriptArg {
      public ArgType Type { get; }
      public string Name { get; }
      public string EnumTableName { get; }
      public int TokenLength { get; }

      public int Length(IDataModel model, int start) {
         return model[start] * TokenLength + 1;
      }

      public ArrayArg(string token) {
         (Type, Name, EnumTableName, TokenLength) = ScriptArg.Construct(token);
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
      public static ScriptLine GetMatchingLine(this IReadOnlyList<ScriptLine> self, IReadOnlyList<byte> data, int start) => self.FirstOrDefault(option => option.Matches(data, start));

      public static int GetScriptSegmentLength(this IReadOnlyList<ScriptLine> self, IDataModel model, int address) {
         int length = 0;
         while (true) {
            var line = self.GetMatchingLine(model, address + length);
            if (line == null) break;
            length += line.CompiledByteLength(model, address + length);
            if (line.IsEndingCommand) break;
         }
         return length;
      }
   }
}
