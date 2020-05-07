using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;


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
      public const string
         WildTableName = "wild",
         SpecialsTable = "specials",
         ItemsTableName = "items",
         TypesTableName = "types",
         MoveNamesTable = "movenames",
         DexInfoTableName = "dexinfo",
         PokemonNameTable = "pokenames",
         TrainerTableName = "trainerdata",
         PokemonStatsTable = "pokestats",
         EggMovesTableName = "eggmoves",
         EvolutionTableName = "evolutions",
         TypeChartTableName = "typeChart",
         LevelMovesTableName = "lvlmoves",
         MultichoiceTableName = "multichoice",
         DecorationsTableName = "decorations",
         RegionalDexTableName = "regionaldex",
         NationalDexTableName = "nationaldex",
         MoveDescriptionsName = "movedescriptions",
         ConversionDexTableName = "hoennToNational";

      public const string
         MoveInfoListName = "moveinfo",
         MoveEffectListName = "moveeffects",
         MoveTargetListName = "movetarget",
         EvolutionMethodListName = "evolutionmethods",
         DecorationsShapeListName = "decorshape",
         DecorationsCategoryListName = "decorcategory",
         DecorationsPermissionListName = "decorpermissions";

      public const string
         Ruby = "AXVE0",
         Sapphire = "AXPE0",
         Emerald = "BPEE0",
         FireRed = "BPRE0",
         LeafGreen = "BPGE0",
         Ruby1_1 = "AXVE1",
         Sapphire1_1 = "AXPE1",
         FireRed1_1 = "BPRE1",
         LeafGreen1_1 = "BPGE1";

      public const string
         TmMoves = "tmmoves",
         HmMoves = "hmmoves",
         TmCompatibility = "tmcompatibility",
         MoveTutors = "tutormoves",
         TutorCompatibility = "tutorcompatibility";

      private readonly string gameCode;
      private readonly ModelDelta noChangeDelta = new NoDataChangeDeltaModel();

      /// <summary>
      /// The first 0x100 bytes of the GBA rom is always the header.
      /// The next 0x100 bytes contains some tables and some startup code, but nothing interesting to point to.
      /// Choosing 0x200 might prevent us from seeing an actual anchor, but it will also remove a bunch
      ///      of false positives and keep us from getting conflicts with the RomName (see DecodeHeader).
      /// </summary>
      public override int EarliestAllowedAnchor => 0x200;

      public HardcodeTablesModel(Singletons singletons, byte[] data, StoredMetadata metadata = null) : base(data, metadata, singletons) {
         if (metadata != null && !metadata.IsEmpty) return;

         gameCode = this.GetGameCode();

         // in vanilla emerald, this pointer isn't four-byte aligned
         // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
         if (gameCode == Emerald && data.Length > EarliestAllowedAnchor && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

         var gamesToDecode = new[] { Ruby, Sapphire, Emerald, FireRed, LeafGreen, Ruby1_1, Sapphire1_1, FireRed1_1, LeafGreen1_1 };
         if (gamesToDecode.Contains(gameCode)) {
            LoadDefaultMetadata(gameCode.Substring(0, 4).ToLower());
            DecodeHeader();
            if (singletons.GameReferenceTables.TryGetValue(gameCode, out var referenceTables)) {
               DecodeTablesFromReference(referenceTables);
            }
         }

         ResolveConflicts();
      }

      private void LoadDefaultMetadata(string code) {
         if (File.Exists("resources/default.toml")) {
            var lines = File.ReadAllLines("resources/default.toml");
            var metadata = new StoredMetadata(lines);
            foreach (var list in metadata.Lists) SetList(list.Name, list.Contents);
            foreach (var anchor in metadata.NamedAnchors) ApplyAnchor(this, new NoDataChangeDeltaModel(), anchor.Address, BaseRun.AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
         }

         foreach (var fileName in Directory.GetFiles("resources", "default.*.toml")) {
            if (!fileName.ToLower().Contains($".{code}.")) continue;
            var lines = File.ReadAllLines(fileName);
            var metadata = new StoredMetadata(lines);
            foreach (var list in metadata.Lists) SetList(list.Name, list.Contents);
            foreach (var anchor in metadata.NamedAnchors) ApplyAnchor(this, new NoDataChangeDeltaModel(), anchor.Address, BaseRun.AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
         }
      }

      private void DecodeHeader() {
         if (!gameCode.IsAny(Ruby, Sapphire, Ruby1_1, Sapphire1_1)) {
            ObserveAnchorWritten(noChangeDelta, "RomName", new AsciiRun(0x108, 0x20));
         }
      }

      private void DecodeTablesFromReference(GameReferenceTables tables) {
         foreach (var table in tables) {
            using (ModelCacheScope.CreateScope(this)) {
               AddTable(table.Address, table.Name, table.Format);
            }
         }
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
            var elementCount = (destination - array.Start) / array.ElementLength;
            var desiredChange = elementCount - array.ElementCount;
            while (!string.IsNullOrEmpty(array.LengthFromAnchor)) {
               var nextArray = GetNextRun(GetAddressFromAnchor(noChangeDelta, -1, array.LengthFromAnchor)) as ArrayRun;
               if (nextArray == null) break;
               array = nextArray;
            }
            array = array.Append(noChangeDelta, desiredChange);
            ObserveAnchorWritten(noChangeDelta, GetAnchorFromAddress(-1, array.Start), array);
         }

         AddTableDirect(destination, name, format);
      }

      /// <summary>
      /// Find a table given an address for that table
      /// </summary>
      private void AddTableDirect(int destination, string name, string format) {
         using (ModelCacheScope.CreateScope(this)) {
            ApplyAnchor(this, noChangeDelta, destination, "^" + name + format, allowAnchorOverwrite: true);
         }
      }
   }
}
