using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static HavenSoft.HexManiac.Core.Models.AutoSearchModel;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// An alternative to the AutoSearchModel.
   /// Instead of using a 'smart' search algorithm to find all the data,
   /// follow hard-coded expected pointers to the known data.
   /// This should still be somewhat robust: the data may move, but the pointers to the data are more likely to be stable.
   ///
   /// Lengths of some tables are still calculated dynamically based on best-fit, so operations like adding pokemon from a separate tool should still be picked up correctly.
   /// </summary>
   public class HardcodeTablesModel : PokemonModel {
      public const string WildTableName = "wild";
      public const string SpecialsTable = "specials";
      public const string ItemsTableName = "items";
      public const string DexInfoTableName = "dexinfo";
      public const string TrainerTableName = "trainerdata";
      public const string EggMovesTableName = "eggmoves";
      public const string EvolutionTableName = "evolutions";
      public const string TypeChartTableName = "typeChart";
      public const string TypeChartTableName2 = "typeChart2";
      public const string LevelMovesTableName = "lvlmoves";
      public const string MultichoiceTableName = "multichoice";
      public const string DecorationsTableName = "decorations";
      public const string RegionalDexTableName = "regionaldex";
      public const string NationalDexTableName = "nationaldex";
      public const string MoveDescriptionsName = "movedescriptions";
      public const string ConversionDexTableName = "hoennToNational";

      public const string MoveInfoListName = "moveinfo";
      public const string MoveEffectListName = "moveeffects";
      public const string MoveTargetListName = "movetarget";
      public const string EvolutionMethodListName = "evolutionmethods";
      public const string DecorationsShapeListName = "decorshape";
      public const string DecorationsCategoryListName = "decorcategory";
      public const string DecorationsPermissionListName = "decorpermissions";

      private readonly string gameCode;
      private readonly ModelDelta noChangeDelta = new NoDataChangeDeltaModel();

      /// <summary>
      /// The first 0x100 bytes of the GBA rom is always the header.
      /// The next 0x100 bytes contains some tables and some startup code, but nothing interesting to point to.
      /// Choosing 0x200 might prevent us from seeing an actual anchor, but it will also remove a bunch
      ///      of false positives and keep us from getting conflicts with the RomName (see DecodeHeader).
      /// </summary>
      public override int EarliestAllowedAnchor => 0x200;

      public HardcodeTablesModel(byte[] data, StoredMetadata metadata = null) : base(data, metadata) {
         if (metadata != null && !metadata.IsEmpty) return;

         gameCode = this.GetGameCode();

         // in vanilla emerald, this pointer isn't four-byte aligned
         // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
         if (gameCode == Emerald && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

         var gamesToDecode = new[] { Ruby, Sapphire, Emerald, FireRed, LeafGreen, Ruby1_1, Sapphire1_1, FireRed1_1, LeafGreen1_1 };
         if (gamesToDecode.Contains(gameCode)) {
            LoadDefaultMetadata();
            DecodeHeader();
            DecodeTablesFromReference();
            DecodeStreams();
         }

         ResolveConflicts();
      }

      private void LoadDefaultMetadata() {
         if (!File.Exists("resources/default.toml")) return;
         var lines = File.ReadAllLines("resources/tableReference.txt");
         var metadata = new StoredMetadata(lines);
         foreach (var list in metadata.Lists) SetList(list.Name, list.Contents);
         foreach (var anchor in metadata.NamedAnchors) ApplyAnchor(this, new NoDataChangeDeltaModel(), anchor.Address, BaseRun.AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
      }

      private void DecodeHeader() {
         if (!gameCode.IsAny(Ruby, Sapphire, Ruby1_1, Sapphire1_1)) {
            ObserveAnchorWritten(noChangeDelta, "RomName", new AsciiRun(0x108, 0x20));
         }
      }

      private static readonly string[] referenceOrder = new string[] { "name", Ruby, Sapphire, Ruby1_1, Sapphire1_1, FireRed, LeafGreen, FireRed1_1, LeafGreen1_1, Emerald, "format" };
      private void DecodeTablesFromReference() {
         var gameIndex = referenceOrder.IndexOf(gameCode);
         if (gameIndex == -1) return;
         if (!File.Exists("resources/tableReference.txt")) return;
         var lines = File.ReadAllLines("resources/tableReference.txt");
         foreach (var line in lines) {
            var row = line.Trim();
            if (row.StartsWith("//")) continue;
            var segments = row.Split(",");
            if (segments.Length != referenceOrder.Length) continue;
            var addressHex = segments[gameIndex].Trim();
            if (addressHex == string.Empty) continue;
            if (!int.TryParse(addressHex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int address)) continue;
            var name = segments[0].Trim();
            var format = segments.Last().Trim();
            using (ModelCacheScope.CreateScope(this)) {
               AddTable(address, name, format);
            }
         }
      }

      private void DecodeStreams() {
         // eggmoves
         int source = 0;
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x045C50; break;
            case FireRed1_1: case LeafGreen1_1: source = 0x045C64; break;
            case Emerald: source = 0x0703F0; break;
            case Ruby: case Sapphire: source = 0x041B44; break;
            case Ruby1_1: case Sapphire1_1: source = 0x041B64; break;
         }
         if (source > 0) ObserveAnchorWritten(noChangeDelta, EggMovesTableName, new EggMoveRun(this, ReadPointer(source)));

         // type chart
         source = 0;
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x01E944; break;
            case FireRed1_1: case LeafGreen1_1: source = 0x01E958; break;
            case Ruby: case Sapphire: case Ruby1_1: case Sapphire1_1: source = 0x01CDC8; break;
            case Emerald: source = 0x047134; break;
         }
         AddTable(source, TypeChartTableName, "[attack.types defend.types strength.]!FEFE00");
         var run = GetNextAnchor(GetAddressFromAnchor(noChangeDelta, -1, TypeChartTableName)) as ITableRun;
         if (run != null) AddTableDirect(run.Start + run.Length, TypeChartTableName2, "[attack.types defend.types strength.]!FFFF00");
      }

      private IReadOnlyList<int> AllSourcesToSameDestination(int source) {
         var destination = ReadPointer(source);
         var run = GetNextRun(destination);
         return run.PointerSources;
      }

      private int Find(string bytesText) {
         var bytes = new List<byte>();
         while (bytesText.Length > 0) {
            bytes.Add(byte.Parse(bytesText.Substring(0, 2), NumberStyles.HexNumber));
            bytesText = bytesText.Substring(2);
         }

         for (int i = 0; i < RawData.Length; i++) {
            if (RawData[i] != bytes[0]) continue;
            if (Enumerable.Range(1, bytes.Count - 1).All(
               j => i + j < RawData.Length && RawData[i + j] == bytes[j]
            )) {
               return i + bytes.Count;
            }
         }

         return -1;
      }

      /// <summary>
      /// Find a table given a pointer to that table
      /// </summary>
      private void AddTable(int source, string name, string format) {
         if (source < 0 || source > RawData.Length) return;
         var destination = ReadPointer(source);
         if (destination < 0 || destination > RawData.Length) return;

         var interruptingRun = GetNextRun(destination);
         if (interruptingRun.Start < destination && interruptingRun is ArrayRun array) {
            var elementLength = array.ElementLength;
            var elementCount = (destination - array.Start) / array.ElementLength;
            array = array.Append(noChangeDelta, elementCount - array.ElementCount);
            ObserveAnchorWritten(noChangeDelta, GetAnchorFromAddress(-1, array.Start), array);
         }

         AddTableDirect(destination, name, format);
      }

      /// <summary>
      /// Find a table given an address for that table
      /// </summary>
      private void AddTableDirect(int destination, string name, string format) {
         using (ModelCacheScope.CreateScope(this)) {
            var errorInfo = ArrayRun.TryParse(this, format, destination, null, out var tableRun);
            if (!errorInfo.HasError) {
               ObserveAnchorWritten(noChangeDelta, name, tableRun);
            }
         }
      }
   }
}
