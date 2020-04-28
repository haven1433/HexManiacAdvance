using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HavenSoft.HexManiac.Core.Models.Code;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;


namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Shared resources that involve an expensive setup, (so we only want to do it once) but then cannot be edited after being initialized.
   /// </summary>
   public class Singletons {
      private const string TableReferenceFileName = "resources/tableReference.txt";
      private const string ThumbReferenceFileName = "resources/armReference.txt";
      private const string ScriptReferenceFileName = "resources/scriptReference.txt";

      public IMetadataInfo MetadataInfo { get; }
      public IReadOnlyDictionary<string, GameReferenceTables> GameReferenceTables { get; }
      public IReadOnlyList<ConditionCode> ThumbConditionalCodes { get; }
      public IReadOnlyList<IInstruction> ThumbInstructionTemplates { get; }
      public IReadOnlyList<ScriptLine> ScriptLines { get; }

      public Singletons() {
         GameReferenceTables = CreateGameReferenceTables();
         (ThumbConditionalCodes, ThumbInstructionTemplates) = LoadThumbReference();
         ScriptLines = LoadScriptReference();
         MetadataInfo = new MetadataInfo();
      }

      private IReadOnlyList<ScriptLine> LoadScriptReference() {
         if (!File.Exists(ScriptReferenceFileName)) return new List<ScriptLine>();
         var lines = File.ReadAllLines(ScriptReferenceFileName);
         var scriptLines = new List<ScriptLine>();
         ScriptLine active = null;
         foreach (var line in lines) {
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith(" ") && active != null) active = null;
            if (line.StartsWith("#")) continue;
            if (line.Trim().StartsWith("#") && active != null) {
               active.AddDocumentation(line.Trim());
            } else {
               active = new ScriptLine(line);
               scriptLines.Add(active);
            }
         }

         return scriptLines.ToArray();
      }

      private static readonly string[] referenceOrder = new string[] { "name", Ruby, Sapphire, Ruby1_1, Sapphire1_1, FireRed, LeafGreen, FireRed1_1, LeafGreen1_1, Emerald, "format" };
      private IReadOnlyDictionary<string, GameReferenceTables> CreateGameReferenceTables() {
         if (!File.Exists(TableReferenceFileName)) return new Dictionary<string, GameReferenceTables>();
         var lines = File.ReadAllLines(TableReferenceFileName);
         var tables = new Dictionary<string, List<ReferenceTable>>();
         for (int i = 0; i < referenceOrder.Length - 2; i++) tables[referenceOrder[i + 1]] = new List<ReferenceTable>();
         foreach (var line in lines) {
            var row = line.Trim();
            if (row.StartsWith("//")) continue;
            var segments = row.Split("//")[0].Split(",");
            if (segments.Length != referenceOrder.Length) continue;
            var name = segments[0].Trim();
            var format = segments.Last().Trim();
            for (int i = 0; i < referenceOrder.Length - 2; i++) {
               var addressHex = segments[i + 1].Trim();
               if (addressHex == string.Empty) continue;
               if (!int.TryParse(addressHex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int address)) continue;
               tables[referenceOrder[i + 1]].Add(new ReferenceTable(name, address, format));
            }
         }

         var readonlyTables = new Dictionary<string, GameReferenceTables>();
         foreach (var pair in tables) readonlyTables.Add(pair.Key, new GameReferenceTables(pair.Value));
         return readonlyTables;
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
   }

   public class GameReferenceTables : IReadOnlyList<ReferenceTable> {
      private readonly IReadOnlyList<ReferenceTable> core;
      public ReferenceTable this[string name] => core.FirstOrDefault(table => table.Name == name);
      public ReferenceTable this[int index] => core[index];
      public int Count => core.Count;

      public GameReferenceTables(IReadOnlyList<ReferenceTable> list) => core = list;

      public IEnumerator<ReferenceTable> GetEnumerator() => core.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => core.GetEnumerator();
   }

   public class ReferenceTable {
      public string Name { get; }
      public int Address { get; }
      public string Format { get; }
      public ReferenceTable(string name, int address, string format) => (Name, Address, Format) = (name, address, format);
      public override string ToString() => $"{Address:X6} -> {Name}, {Format}";
   }

   public interface IMetadataInfo {
      string VersionNumber { get; }
   }

   internal class MetadataInfo : IMetadataInfo {
      public string VersionNumber { get; }
      public MetadataInfo() {
         var assembly = Assembly.GetExecutingAssembly();
         var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
         VersionNumber = $"{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}";
      }
   }
}
