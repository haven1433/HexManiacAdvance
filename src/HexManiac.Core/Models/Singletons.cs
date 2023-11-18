using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;


namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Shared resources that involve an expensive setup, (so we only want to do it once) but then cannot be edited after being initialized.
   /// </summary>
   public class Singletons {
      private const string TableReferenceFileName = "resources/tableReference.txt";
      private const string ConstantReferenceFileName = "resources/constantReference.txt";
      private const string ThumbReferenceFileName = "resources/armReference.txt";
      private const string ScriptReferenceFileName = "resources/scriptReference.txt";
      private const string BattleScriptReferenceFileName = "resources/battleScriptReference.txt";
      private const string AnimationScriptReferenceFileName = "resources/animationScriptReference.txt";
      private const string BattleAIScriptReferenceFileName = "resources/battleAIScriptReference.txt";
      private const string ScriptReferenceDocumetationFileName = "resources/scriptReference.md";
      private const string DocReferenceFileName = "resources/docReference.txt";
      private const string PythonUtilityFileName = "resources/hma.py";

      public IMetadataInfo MetadataInfo { get; }
      public IReadOnlyDictionary<string, GameReferenceTables> GameReferenceTables { get; }
      public IReadOnlyDictionary<string, GameReferenceConstants> GameReferenceConstants { get; }
      public IReadOnlyList<ConditionCode> ThumbConditionalCodes { get; }
      public IReadOnlyList<IInstruction> ThumbInstructionTemplates { get; }
      public IReadOnlyList<IScriptLine> ScriptLines { get; }
      public IReadOnlyList<IScriptLine> BattleScriptLines { get; }
      public IReadOnlyList<IScriptLine> AnimationScriptLines { get; }
      public IReadOnlyList<IScriptLine> BattleAIScriptLines { get; }
      public IReadOnlyDictionary<string, IReadOnlyList<DocLabel>> DocReference { get; }
      public IWorkDispatcher WorkDispatcher { get; }
      public string PythonUtility { get; }
      public int CopyLimit { get; }

      public Singletons(IWorkDispatcher dispatcher = null, int copyLimit = 40000) {
         MetadataInfo = new MetadataInfo();
         GameReferenceTables = CreateGameReferenceTables();
         GameReferenceConstants = CreateGameReferenceConstants();
         (ThumbConditionalCodes, ThumbInstructionTemplates) = LoadThumbReference();
         ScriptLines = LoadScriptReference<XSEScriptLine>(ScriptReferenceFileName);
         BattleScriptLines = LoadScriptReference<BSEScriptLine>(BattleScriptReferenceFileName);
         AnimationScriptLines = LoadScriptReference<ASEScriptLine>(AnimationScriptReferenceFileName);
         BattleAIScriptLines = LoadScriptReference<TSEScriptLine>(BattleAIScriptReferenceFileName);
         DocReference = LoadDocReference(DocReferenceFileName);
         WorkDispatcher = dispatcher ?? InstantDispatch.Instance;
         PythonUtility = File.ReadAllText(PythonUtilityFileName);
         CopyLimit = copyLimit;
      }

      public Singletons(IMetadataInfo metadataInfo, IReadOnlyDictionary<string, GameReferenceTables> gameReferenceTables, int copyLimit = 40000) : this(metadataInfo, gameReferenceTables, null, copyLimit) { }

      public Singletons(IMetadataInfo metadataInfo, IReadOnlyDictionary<string, GameReferenceTables> gameReferenceTables, IReadOnlyDictionary<string, GameReferenceConstants> gameRefereneceConstants, int copyLimit = 40000) {
         MetadataInfo = metadataInfo;
         GameReferenceTables = gameReferenceTables;
         GameReferenceConstants = gameRefereneceConstants ?? new Dictionary<string, GameReferenceConstants>();
         ThumbConditionalCodes = new ConditionCode[0];
         ThumbInstructionTemplates = new IInstruction[0];
         ScriptLines = new ScriptLine[0];
         BattleScriptLines = new ScriptLine[0];
         AnimationScriptLines = new ScriptLine[0];
         BattleAIScriptLines = new ScriptLine[0];
         DocReference = new Dictionary<string, IReadOnlyList<DocLabel>>();
         WorkDispatcher = InstantDispatch.Instance;
         CopyLimit = copyLimit;
         PythonUtility = string.Empty;
      }

      private IReadOnlyList<IScriptLine> LoadScriptReference<TLine>(string file) where TLine : ScriptLine {
         if (!File.Exists(file)) return new List<ScriptLine>();
         Func<string, IScriptLine> factory = line => new XSEScriptLine(line);
         if (typeof(TLine) == typeof(BSEScriptLine)) factory = line => new BSEScriptLine(line);
         if (typeof(TLine) == typeof(ASEScriptLine)) factory = line => new ASEScriptLine(line);
         if (typeof(TLine) == typeof(TSEScriptLine)) factory = line => new TSEScriptLine(line);

         var lines = File.ReadAllLines(file);
         var scriptLines = new List<IScriptLine>();
         IScriptLine active = null;
         foreach (var line in lines) {
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith(" ") && active != null) active = null;
            if (line.StartsWith("#")) continue;
            if (line.Trim().StartsWith("#") && active != null) {
               active.AddDocumentation(line.Trim());
            } else {
               if (MacroScriptLine.IsMacroLine(line)) {
                  var macro = new MacroScriptLine(line);
                  if (macro.IsValid) active = macro;
               } else {
                  active = factory(line);
               }
               if (active != null) scriptLines.Add(active);
            }
         }

         return scriptLines.ToArray();
      }

      private IReadOnlyDictionary<string, IReadOnlyList<DocLabel>> LoadDocReference(string file) {
         var dict = new Dictionary<string, IReadOnlyList<DocLabel>>();
         if (!File.Exists(file)) return dict;
         var lines = File.ReadAllLines(file);
         var lists = new List<List<DocLabel>>();
         foreach (var line in lines) {
            var trim = line.Trim();
            if (string.IsNullOrWhiteSpace(trim)) continue;
            if (trim.StartsWith("[")) {
               lists.Clear();
               var content = trim.Split(new[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries)[0];
               var keys = content.Split("_");
               foreach (var key in keys) {
                  if (!dict.TryGetValue(key, out var list)) dict[key] = list = new List<DocLabel>();
                  lists.Add((List<DocLabel>)list);
               }
            } else if (trim.StartsWith("#")) {
               continue;
            } else {
               var content = trim.Split(" ", StringSplitOptions.RemoveEmptyEntries);
               if (content.Length != 2) continue;
               var label = new DocLabel(content[0], content[1]);
               lists.ForEach(list => list.Add(label));
            }
         }
         return dict;
      }

      public static IReadOnlyList<string> ReferenceOrder { get; } = new string[] { "name", Ruby, Sapphire, Ruby1_1, Sapphire1_1, FireRed, LeafGreen, FireRed1_1, LeafGreen1_1, Emerald, "format" };
      private IReadOnlyDictionary<string, GameReferenceTables> CreateGameReferenceTables() {
         if (!File.Exists(TableReferenceFileName)) return new Dictionary<string, GameReferenceTables>();
         var lines = File.ReadAllLines(TableReferenceFileName);
         var tables = new Dictionary<string, List<ReferenceTable>>();
         for (int i = 0; i < ReferenceOrder.Count - 2; i++) tables[ReferenceOrder[i + 1]] = new List<ReferenceTable>();
         foreach (var line in lines) {
            var row = line.Trim();
            if (row.StartsWith("//")) continue;
            var segments = row.Split("//")[0].Split(",");
            if (segments.Length != ReferenceOrder.Count) continue;
            var name = segments[0].Trim();
            var offset = 0;
            if (name.Contains("+")) {
               var parts = name.Split("+");
               name = parts[0];
               int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
            } else if (name.Contains("-")) {
               var parts = name.Split("-");
               name = parts[0];
               int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
               offset = -offset;
            }
            var format = segments.Last().Trim();
            for (int i = 0; i < ReferenceOrder.Count - 2; i++) {
               var addressHex = segments[i + 1].Trim();
               if (addressHex == string.Empty) continue;
               if (!int.TryParse(addressHex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int address)) continue;
               tables[ReferenceOrder[i + 1]].Add(new ReferenceTable(name, offset, address, format));
            }
         }

         var readonlyTables = new Dictionary<string, GameReferenceTables>();
         foreach (var pair in tables) readonlyTables.Add(pair.Key, new GameReferenceTables(pair.Value));
         return readonlyTables;
      }

      private IReadOnlyDictionary<string, GameReferenceConstants> CreateGameReferenceConstants() {
         if (!File.Exists(ConstantReferenceFileName)) return new Dictionary<string, GameReferenceConstants>();
         var lines = File.ReadAllLines(ConstantReferenceFileName);
         var constants = new Dictionary<string, List<ReferenceConstant>>();
         foreach (var line in lines) {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cleanLine = line.Trim();
            if (cleanLine.Length < 6) continue;
            if (!char.IsLetter(cleanLine[0])) continue;
            var gameCode = cleanLine.Substring(0, 5).ToUpper();
            if (!constants.TryGetValue(gameCode, out var collection)) {
               collection = new List<ReferenceConstant>();
               constants[gameCode] = collection;
            }
            collection.Add(new ReferenceConstant(cleanLine.Substring(5)));
         }

         var readonlyConstants = new Dictionary<string, GameReferenceConstants>();
         foreach (var pair in constants) readonlyConstants.Add(pair.Key, new GameReferenceConstants(pair.Value));
         return readonlyConstants;
      }

      private (IReadOnlyList<ConditionCode>, IReadOnlyList<IInstruction>) LoadThumbReference() {
         var conditionalCodes = new List<ConditionCode>();
         var instructionTemplates = new List<IInstruction>();
         if (!File.Exists(ThumbReferenceFileName)) return (new List<ConditionCode>(), new List<IInstruction>());
         var engineLines = File.ReadAllLines(ThumbReferenceFileName);
         foreach (var line in engineLines) {
            if (ConditionCode.TryLoadConditionCode(line, out var condition)) conditionalCodes.Add(condition);
            else if (Instruction.TryLoadInstruction(line, out var instruction)) instructionTemplates.Add(instruction);
         }
         return (conditionalCodes, instructionTemplates);
      }

      public void ExportReadableScriptReference(EditorViewModel editor) {
         var specials = new Dictionary<string, StoredList>(
            new[] { "axve", "axpe", "bpre", "bpge", "bpee" }
            .Select<string, KeyValuePair<string,StoredList>>(
               code => new(code, BaseModel.GetDefaultMetadatas(code).SelectMany(md => md.Lists).Single(list => list.Name == "specials"))
            )
         );

         ExportReadableScriptReference(editor, ScriptLines, specials, ScriptReferenceDocumetationFileName);
      }
      private void ExportReadableScriptReference(EditorViewModel editor, IReadOnlyList<IScriptLine> lines, Dictionary<string, StoredList> specials, string filename) {
         // setup: make sure all the scripts have been fully determined
         foreach (var tab in editor) {
            if (tab is not IEditableViewPort evp) continue;
            var top = Flags.GetAllTopLevelScripts(evp.Model);
            foreach (var script in top) evp.Tools.CodeTool.ScriptParser.FormatScript<XSERun>(new(), evp.Model, script);
         }

         var rnd = new Random(0x5eed);
         var text = new StringBuilder();
         var nl = Environment.NewLine;
         text.AppendLine(@"
This is a list of all the commands currently available within HexManiacAdvance when writing scripts.
For example scripts and tutorials, see the [HexManiacAdvance Wiki](https://github.com/haven1433/HexManiacAdvance/wiki).
");
         text.AppendLine("# Commands");
         string lastCommand = null;

         foreach (var line in lines.OrderBy(line => line.LineCommand)) {
            if (lastCommand != null && lastCommand != line.LineCommand) {
               text.AppendLine("</details>");
               text.AppendLine();
            }
            if (lastCommand != line.LineCommand) {
               text.AppendLine($"## {line.LineCommand}");
               text.AppendLine();
               text.AppendLine("<details>");
               text.AppendLine($"<summary> {line.LineCommand}</summary>");
               text.AppendLine();
            }
            lastCommand = line.LineCommand;
            text.AppendLine();

            text.Append(line.LineCommand);
            if (line.LineCode.Count > 1) text.Append(" " + line.LineCode[1]);
            foreach (var arg in line.Args) {
               if (arg is SilentMatchArg || arg.Name == "filler") continue;
               if (arg is ArrayArg array) {
                  text.Append($" `[{arg.Name}]`");
               } else {
                  text.Append($" `{arg.Name}`");
               }
            }
            text.AppendLine();
            var matches = ScriptLine.GetMatchingGames(line);
            if (matches.Count < 5) text.AppendLine($"{nl}  Only available in " + " ".Join(matches));
            foreach (var arg in line.Args) {
               if (arg is SilentMatchArg || arg.Name == "filler") continue;
               if (!string.IsNullOrEmpty(arg.EnumTableName) && !arg.EnumTableName.StartsWith("|")) {
                  text.AppendLine($"{nl}*  `{arg.Name}` from {arg.EnumTableName}");
               } else if (arg.Type != ArgType.Pointer) {
                  if (string.IsNullOrEmpty(arg.EnumTableName)) {
                     text.AppendLine($"{nl}*  `{arg.Name}` is a number.");
                  } else {
                     text.AppendLine($"{nl}*  `{arg.Name}` is a number (hex).");
                  }
               } else if (arg.Type == ArgType.Pointer) {
                  if (arg.PointerType == ExpectedPointerType.Script) {
                     text.AppendLine($"{nl}*  `{arg.Name}` points to a script or section");
                  } else if (arg.PointerType == ExpectedPointerType.Mart) {
                     text.AppendLine($"{nl}*  `{arg.Name}` points to pokemart data or auto");
                  } else if (arg.PointerType == ExpectedPointerType.Movement) {
                     text.AppendLine($"{nl}*  `{arg.Name}` points to movement data or auto");
                  } else if (arg.PointerType == ExpectedPointerType.SpriteTemplate) {
                     text.AppendLine($"{nl}*  `{arg.Name}` points to sprite-template data or auto");
                  } else if (arg.PointerType == ExpectedPointerType.Decor) {
                     text.AppendLine($"{nl}*  `{arg.Name}` points to decor data or auto");
                  } else if (arg.PointerType == ExpectedPointerType.Text) {
                     text.AppendLine($"{nl}*  `{arg.Name}` points to text or auto");
                  } else if (arg.PointerType == ExpectedPointerType.Unknown) {
                     text.AppendLine($"{nl}*  `{arg.Name}` is a pointer.");
                  }
               }
            }
            text.AppendLine("");
            text.AppendLine("Example:");
            text.AppendLine("```");

            bool exampleFound = false;
            if (line is ScriptLine && line.Args.Count > 0) {
               var examples = GetExamplesForBasicCommandReference(editor, line.LineCode);
               if (examples != null && examples.Count > 0) {
                  text.Append(rnd.From(examples));
                  exampleFound = true;
               }
            } else if (line is MacroScriptLine macro) {
               var examples = GetExamplesForMacroCommandReference(editor, macro);
               if (examples != null && examples.Count > 0) {
                  text.Append(rnd.From(examples));
                  exampleFound = true;
               }
            }
            if (!exampleFound) {
               text.Append(line.LineCommand);
               if (line.LineCode.Count > 1) text.Append(" " + line.LineCode[1]);
               foreach (var arg in line.Args) {
                  if (arg is SilentMatchArg || arg.Name == "filler") continue;
                  if (!string.IsNullOrEmpty(arg.EnumTableName) && !arg.EnumTableName.StartsWith("|")) {
                     var model = ((IViewPort)editor[0]).Model;
                     var options = model.GetOptions(arg.EnumTableName);
                     if (options.Count == 0 && int.TryParse(arg.EnumTableName, out var count)) options = count.Range().Select(i => i.ToString()).ToList();
                     text.Append(" " + rnd.From(options));
                  } else if (arg.Type != ArgType.Pointer) {
                     if (string.IsNullOrEmpty(arg.EnumTableName)) {
                        text.Append($" {rnd.Next(5)}");
                     } else {
                        text.Append($" 0x{rnd.Next(16):X2}");
                     }
                  } else if (arg.Type == ArgType.Pointer) {
                     if (arg.PointerType == ExpectedPointerType.Script) {
                        text.Append(" <section1>");
                     } else if (arg.PointerType == ExpectedPointerType.Unknown) {
                        text.AppendLine(" <F00000>");
                     } else {
                        text.Append(" <auto>");
                     }
                  }
               }
            }

            text.AppendLine();
            text.AppendLine("```");
            if (line.Documentation != null && line.Documentation.Count > 0) {
               text.AppendLine("Notes:");
               text.AppendLine("```");
               foreach (var doc in line.Documentation) {
                  text.AppendLine($"  {doc}");
               }
               text.AppendLine("```");
            }
         }
         text.AppendLine("</details>");
         text.AppendLine();

         text.AppendLine("# Specials");
         text.AppendLine(@"
This is a list of all the specials available within HexManiacAdvance when writing scripts.

Use `special name` when doing an action with no result.

Use `special2 variable name` when doing an action that has a result.
* The result will be returned to the variable.
");

         var variableForSpecial = GetVariableForSpecialReference(editor);
         ((IViewPort)editor[0]).Model.TryGetList("scriptvariablealiases", out var varNames);
         var names = specials.Values.SelectMany(list => list.Contents).Distinct().OrderBy(name => name).ToList();
         foreach (var name in names) {
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Length < 2) continue;
            var supportedGames = specials.Keys.Where(key => specials[key].Contents.Contains(name)).ToList();
            text.AppendLine($"## {name}");
            text.AppendLine();
            text.AppendLine("<details>");
            text.AppendLine($"<summary> {name} </summary>");
            text.AppendLine();
            if (supportedGames.Count == specials.Count) {
               text.AppendLine("*(Supports all games.)*");
            } else {
               text.AppendLine($"*(Supports {", ".Join(supportedGames)})*");
            }
            text.AppendLine();
            var usage = "special " + name;
            if (variableForSpecial.TryGetValue(name, out int variableID)) {
               var resultVar = varNames[variableID] ?? $"0x{variableID:X4}";
               usage = $"special2 {resultVar} {name}";
            }
            text.AppendLine("Example Usage:");
            text.AppendLine("```");
            text.AppendLine(usage);
            text.AppendLine("```");
            foreach (var game in supportedGames) {
               if (specials[game].Comments.TryGetValue(specials[game].IndexOf(name), out var comment)) {
                  text.AppendLine(comment);
                  text.AppendLine();
                  break;
               }
            }
            text.AppendLine("</details>");
            text.AppendLine();
         }

         File.WriteAllText(filename, text.ToString());
      }

      public static IReadOnlyList<string> GetExamplesForMacroCommandReference(EditorViewModel editor, MacroScriptLine line) {
         var collection = new List<string>();
         foreach (var tab in editor) {
            if (tab is not IEditableViewPort viewPort) continue;
            var model = viewPort.Model;
            var hash = model.GetShortGameCode();
            var top = Flags.GetAllTopLevelScripts(model);
            var start = ((SilentMatchArg)line.Args[0]).ExpectedValue;
            var header = "# From " + model.GetGameCode() + ", ";
            var spots = Flags.GetAllScriptSpots(model, viewPort.Tools.CodeTool.ScriptParser, top, start);
            foreach (var spot in spots.Where(spot => line.Matches(hash, model, spot.Address))) {
               // skip spots where the variables are 'weird'
               var argStart = spot.Address + spot.Line.LineCode.Count;
               var reasonableArgs = true;
               foreach (var arg in spot.Line.Args) {
                  if (arg is ScriptArg scriptArg && !scriptArg.FitsInRange(model, argStart)) {
                     reasonableArgs = false;
                     break;
                  }
                  argStart += arg.Length(model, argStart);
               }
               if (!reasonableArgs) continue;

               var library = new DecompileLabelLibrary(model, spot.Address - 0x1000, 0x2000);
               var streams = new List<ExpectedPointerType>();
               var text = spot.Line.Decompile(model, spot.Address, library, streams);
               int sectionCount = 0;
               var labels = library.FinalizeLabels(ref sectionCount);
               text = library.FinalizeLine(labels, text);
               collection.Add(header + spot.Address.ToAddress() + Environment.NewLine + text);
               if (collection.Count >= MaxExamples) return collection;
            }
         }
         return collection;
      }

      public static IReadOnlyList<string> GetExamplesForBasicCommandReference(EditorViewModel editor, IReadOnlyList<byte> command) {
         var collection = new List<string>();
         foreach (var tab in editor) {
            if (tab is not IEditableViewPort viewPort) continue;
            CollectBasicCommandExamples(viewPort.Model, viewPort.Tools.CodeTool.ScriptParser, collection, command, "# From " + viewPort.Model.GetGameCode() + ", ");
         }
         return collection;
      }

      private const int MaxExamples = 20;
      public static void CollectBasicCommandExamples(IDataModel model, ScriptParser parser, List<string> collection, IReadOnlyList<byte> command, string prefix) {

         if (collection.Count >= MaxExamples) return;
         foreach (var spot in Flags.GetAllScriptSpots(model, parser, Flags.GetAllTopLevelScripts(model), false, command[0])) {
            if (collection.Count >= MaxExamples) break;

            bool commandMatches = command.Count.Range().All(i => model[spot.Address + i] == command[i]);
            if (!commandMatches) continue;

            // skip spots where the variables are 'weird'
            var argStart = spot.Address + spot.Line.LineCode.Count;
            var reasonableArgs = true;
            foreach (var arg in spot.Line.Args) {
               if (arg is ScriptArg scriptArg && !scriptArg.FitsInRange(model, argStart)) {
                  reasonableArgs = false;
                  break;
               }
               argStart += arg.Length(model, argStart);
            }
            if (!reasonableArgs) continue;

            var library = new DecompileLabelLibrary(model, spot.Address - 0x1000, 0x2000);
            var streams = new List<ExpectedPointerType>();
            var text = spot.Line.Decompile(model, spot.Address, library, streams);
            int sectionCount = 0;
            var labels = library.FinalizeLabels(ref sectionCount);
            text = library.FinalizeLine(labels, text);
            collection.Add(prefix + spot.Address.ToAddress() + ":" + Environment.NewLine + text);
         }
      }

      public static IReadOnlyDictionary<string, int> GetVariableForSpecialReference(EditorViewModel editor) {
         var collection = new Dictionary<string, int>();
         foreach (var tab in editor) {
            if (tab is not IEditableViewPort viewPort) continue;
            CollectSpecialReference(viewPort.Model, viewPort.Tools.CodeTool.ScriptParser, collection);
         }
         return collection;
      }

      public static void CollectSpecialReference(IDataModel model, ScriptParser parser, Dictionary<string, int> collection) {
         if (!model.TryGetList("specials", out var list)) return;
         foreach (var spot in Flags.GetAllScriptSpots(model, parser, Flags.GetAllTopLevelScripts(model), 0x26)) {
            var variable = model.ReadMultiByteValue(spot.Address + 1, 2);
            if (variable < 0x100) continue;
            var specialID = model.ReadMultiByteValue(spot.Address + 3, 2);
            if (specialID.InRange(0, list.Count) && list[specialID] != null) {
               var name = list[specialID];
               if (collection.ContainsKey(name)) continue;
               collection[name] = variable;
            }
         }
      }
   }

   public static class GameReferenceTableDictionaryExtensions {
      public static string[] GuessSources(this IReadOnlyDictionary<string, GameReferenceTables> self, string code, int address) {
         var result = self.Count.Range().Select(i => string.Empty).ToArray();
         if (!self.TryGetValue(code, out var tables)) return result;
         var index = tables.GetIndexOfNearestAddress(address);
         var name = tables[index].Name;
         var diff = address - tables[index].Address;

         var keys = self.Keys.ToArray();
         for (int i = 0; i < self.Count; i++) {
            var currentTable = self[keys[i]].FirstOrDefault(table => table.Name == name);
            if (currentTable == null) continue;
            result[i] = (currentTable.Address + diff).ToAddress();
         }

         return result;
      }
   }

   public class GameReferenceTables : IReadOnlyList<ReferenceTable> {
      private readonly IReadOnlyList<ReferenceTable> core;
      public ReferenceTable this[string name] => core.FirstOrDefault(table => table.Name == name);
      public ReferenceTable this[int index] => core[index];
      public int Count => core.Count;

      public GameReferenceTables(IReadOnlyList<ReferenceTable> list) => core = list;

      public IEnumerator<ReferenceTable> GetEnumerator() => core.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => core.GetEnumerator();

      /// <summary>
      /// Given an address, finds the reference table nearest to that address
      /// and returns its index.
      /// </summary>
      public int GetIndexOfNearestAddress(int address) {
         var distance = int.MaxValue;
         var index = -1;
         for (int i = 0; i < Count; i++) {
            var currentDistance = Math.Abs(this[i].Address - address);
            if (currentDistance == 0) continue;
            if (currentDistance > distance) continue;
            distance = currentDistance;
            index = i;
         }
         return index;
      }
   }

   public class ReferenceTable {
      public string Name { get; }
      public int Offset { get; }
      public int Address { get; }
      public string Format { get; }
      public ReferenceTable(string name, int offset, int address, string format) => (Name, Offset, Address, Format) = (name, offset, address, format);
      public override string ToString() => $"{Address:X6} -> {Name}, {Offset}, {Format}";
   }

   public class GameReferenceConstants : IReadOnlyList<ReferenceConstant> {
      private readonly IReadOnlyList<ReferenceConstant> core;
      public ReferenceConstant this[string name] => core.FirstOrDefault(table => table.Name == name);
      public ReferenceConstant this[int index] => core[index];
      public int Count => core.Count;

      public GameReferenceConstants(IReadOnlyList<ReferenceConstant> list) => core = list;

      public IEnumerator<ReferenceConstant> GetEnumerator() => core.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => core.GetEnumerator();
   }

   public class ReferenceConstant {
      public IReadOnlyList<int> Addresses { get; }
      public string Name { get; }
      public int Length { get; }
      public int AddOffset { get; }
      public int MultOffset { get; } = 1;
      public string Note { get; }

      // BRPE0:constant.name+1 123456,123456,123456 # note
      public ReferenceConstant(string line) {
         var parts = line.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

         // Length
         if (line[0] == '.') Length = 1;
         else if (line[0] == ':') Length = 2;
         else throw new NotImplementedException();
         if (line[1] == ':') {
            Length = 4;
            parts = line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         }

         // Name/Offset
         if (parts[0].Contains("-")) {
            var offsetSplit = parts[0].Split('-');
            Name = offsetSplit[0];
            AddOffset = -int.Parse(offsetSplit[1]);
         } else if (parts[0].Contains("+")) {
            var offsetSplit = parts[0].Split('+');
            Name = offsetSplit[0];
            AddOffset = int.Parse(offsetSplit[1]);
         } else if (parts[0].Contains("*")) {
            var offsetSplit = parts[0].Split('*');
            Name = offsetSplit[0];
            MultOffset = int.Parse(offsetSplit[1]);
         } else {
            Name = parts[0];
         }

         // Addresses
         Addresses = parts[1].Split(',').Select(adr => int.Parse(adr, NumberStyles.HexNumber)).ToList();

         // Note
         var commentParts = line.Split(new[] { '#' }, 2);
         if (commentParts.Length > 1) Note = commentParts[1].Trim();
      }

      public IEnumerable<StoredMatchedWord> ToStoredMatchedWords() {
         foreach (var address in Addresses) {
            yield return new StoredMatchedWord(address, Name, Length, AddOffset, MultOffset, Note);
         }
      }
   }

   public interface IMetadataInfo {
      string VersionNumber { get; }
      bool IsPublicRelease { get; }
   }

   internal class MetadataInfo : IMetadataInfo {
      public string VersionNumber { get; }
      public bool IsPublicRelease {
         get {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FilePrivatePart == 0;
         }
      }
      public MetadataInfo() {
         var assembly = Assembly.GetExecutingAssembly();
         var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
         VersionNumber = $"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}";
         if (fvi.FilePrivatePart != 0) VersionNumber += "." + fvi.FilePrivatePart;
      }
   }

   public record DocLabel(string Label, string Url);
}
