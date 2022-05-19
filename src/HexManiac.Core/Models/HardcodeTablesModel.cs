using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using System.Diagnostics;
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
         MapNameTable = "data.maps.names",
         WildTableName = "data.pokemon.wild",
         SpecialsTable = "scripts.specials.thumb",
         MoveDataTable = "data.pokemon.moves.stats.battle",
         ItemsTableName = "data.items.stats",
         TypesTableName = "data.pokemon.type.names",
         MoveNamesTable = "data.pokemon.moves.names",
         PokeIconsTable = "graphics.pokemon.icons.sprites",
         DexInfoTableName = "data.pokedex.stats",
         PokemonNameTable = "data.pokemon.names",
         TrainerTableName = "data.trainers.stats",
         MoveEffectsTable = "scripts.moves.effects",
         NaturesTableName = "data.pokemon.natures.names",
         OverworldSprites = "graphics.overworld.sprites",
         BallSpritesTable = "graphics.items.ball.sprites",
         BackSpritesTable = "graphics.pokemon.sprites.front",
         PokemonStatsTable = "data.pokemon.stats",
         AbilityNamesTable = "data.abilities.names",
         EggMovesTableName = "data.pokemon.moves.egg",
         OverworldPalettes = "graphics.overworld.palettes",
         BallPalettesTable = "graphics.items.ball.palettes",
         FrontSpritesTable = "graphics.pokemon.sprites.front",
         ContestTypesTable = "data.pokemon.type.contest.names",
         PokePalettesTable = "graphics.pokemon.palettes.normal",
         ShinyPalettesTable = "graphics.pokemon.palettes.normal",
         EvolutionTableName = "data.pokemon.evolutions",
         TypeChartTableName = "data.pokemon.type.chart",
         ItemImagesTableName = "graphics.items.sprites",
         LevelMovesTableName = "data.pokemon.moves.levelup",
         MultichoiceTableName = "scripts.text.multichoice",
         DecorationsTableName = "data.decorations.stats",
         RegionalDexTableName = "data.pokedex.regional",
         NationalDexTableName = "data.pokedex.national",
         MoveDescriptionsName = "data.pokemon.moves.descriptions",
         PokeIconPalettesTable = "graphics.pokemon.icons.palettes",
         ConversionDexTableName = "data.pokedex.hoennToNational",
         TrainerClassNamesTable = "data.trainers.classes.names",
         DefaultSpriteNamespace = "graphics.new.sprite",
         DefaultTilemapNamespace = "graphics.new.tilemap",
         DefaultPaletteNamespace = "graphics.new.palette",
         AbilityDescriptionsTable = "data.abilities.descriptions",
         PokeIconPaletteIndexTable = "graphics.pokemon.icons.index",
         BattleParticleSpriteTable = "graphics.moves.particles.sprites",
         BattleParticlePaletteTable = "graphics.moves.particles.palettes";

      public const string
         MoveInfoListName = "moveinfo",
         MoveEffectListName = "moveeffectoptions",
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
         TmMoves = "data.pokemon.moves.tms",
         HmMoves = "data.pokemon.moves.hms",
         TmCompatibility = "data.pokemon.moves.tmcompatibility",
         MoveTutors = "data.pokemon.moves.tutors",
         TutorCompatibility = "data.pokemon.moves.tutorcompatibility";

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
         gameCode = this.GetGameCode();
         if (metadata != null && !metadata.IsEmpty) {
            InitializationWorkload = (singletons?.WorkDispatcher ?? InstantDispatch.Instance).RunBackgroundWork(() => Initialize(metadata));
            return;
         }

         InitializationWorkload = (singletons?.WorkDispatcher ?? InstantDispatch.Instance).RunBackgroundWork(() => {
            {
               if (singletons.GameReferenceConstants.TryGetValue(gameCode, out var referenceConstants)) {
                  metadata = DecodeConstantsFromReference(this, singletons.MetadataInfo, metadata, referenceConstants);
               }
               Initialize(metadata);
               isCFRU = GetIsCFRU();

               // in vanilla emerald, this pointer isn't four-byte aligned
               // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
               if (gameCode == Emerald && data.Length > EarliestAllowedAnchor && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

               var gamesToDecode = new[] { Ruby, Sapphire, Emerald, FireRed, LeafGreen, Ruby1_1, Sapphire1_1, FireRed1_1, LeafGreen1_1, "BPRF0" };
               if (gamesToDecode.Contains(gameCode)) {
                  foreach (var defaultMetadata in GetDefaultMetadatas(gameCode.Substring(0, 4).ToLower(), gameCode.ToLower())) {
                     this.LoadMetadata(defaultMetadata);
                  }
                  if (singletons.GameReferenceTables.TryGetValue(gameCode, out var referenceTables)) {
                     DecodeTablesFromReference(referenceTables);
                  }
               }

               ResolveConflicts();
            }
         });
      }

      [Conditional("DEBUG")]
      private void CheckForEmptyAnchors(int destination, string anchor) {
         var run = GetNextRun(destination);
         Debug.Assert(run.PointerSources == null || run.PointerSources.Count > 0, $"{anchor} refers to {destination:X6}, but has no pointers. So how did we find it?");
      }

      private void DecodeTablesFromReference(GameReferenceTables tables) {
         foreach (var table in tables) {
            if (isCFRU && table.Name == "graphics.pokemon.sprites.coordinates.front") continue;
            if (isCFRU && table.Name == EggMovesTableName) continue;
            using (ModelCacheScope.CreateScope(this)) {
               var format = table.Format;
               AddTable(table.Address, table.Offset, table.Name, format);
            }
         }
      }

      public static StoredMetadata DecodeConstantsFromReference(IReadOnlyList<byte> model, IMetadataInfo info, StoredMetadata metadata, GameReferenceConstants constants) {
         if (metadata == null) return metadata;
         var words = metadata.MatchedWords.ToList();
         var constantSet = new Dictionary<string, IList<StoredMatchedWord>>();
         foreach (var constant in constants.SelectMany(c => c.ToStoredMatchedWords())) {
            if (!constantSet.ContainsKey(constant.Name)) constantSet[constant.Name] = new List<StoredMatchedWord>();
            constantSet[constant.Name].Add(constant);
         }
         foreach (var constant in constantSet.Values) {
            if (constant.Any(c => c.Address + c.Length > model.Count)) continue;
            var virtualValues = constant.Select(c => (model.ReadMultiByteValue(c.Address, c.Length) - c.AddOffset) / c.MultOffset).ToList();
            var match = virtualValues.All(vv => vv == virtualValues[0]);
            if (match) words.AddRange(constant);
         }
         return new StoredMetadata(metadata.NamedAnchors, metadata.UnmappedPointers, words, metadata.OffsetPointers, metadata.Lists, metadata.UnmappedConstants, metadata.GotoShortcuts, info,
            new StoredMetadataFields {
               FreeSpaceSearch = metadata.FreeSpaceSearch,
               FreeSpaceBuffer = metadata.FreeSpaceBuffer,
               NextExportID = metadata.NextExportID,
               ShowRawIVByteForTrainer = metadata.ShowRawIVByteForTrainer
            });
      }

      /// <summary>
      /// Find a table given a pointer to that table
      /// The pointer at the source may not point directly to the table: it may point to an offset from the start of the table.
      /// </summary>
      private void AddTable(int source, int offset, string name, string format) {
         format = AdjustFormatForCFRU(name, format);
         if (source < 0 || source > RawData.Length) return;
         var destination = ReadPointer(source) - offset;
         if (destination < 0 || destination > RawData.Length) return;

         var interruptingSourceRun = GetNextRun(source);
         if (interruptingSourceRun.Start < source && interruptingSourceRun.Start + interruptingSourceRun.Length > source && interruptingSourceRun is not ITableRun) {
            // the source isn't actually a pointer, we shouldn't write anything
            return;
         }

         var interruptingRun = GetNextRun(destination);
         if (interruptingRun.Start < destination && interruptingRun is ArrayRun array) {
            var elementCount = (destination - array.Start) / array.ElementLength;
            var desiredChange = elementCount - array.ElementCount;
            while (!string.IsNullOrEmpty(array.LengthFromAnchor)) {
               if (GetNextRun(GetAddressFromAnchor(noChangeDelta, -1, array.LengthFromAnchor)) is ArrayRun nextArray) {
                  array = nextArray;
               } else {
                  break;
               }
            }
            if (array.ElementCount + desiredChange <= 0) {
               // erase the entire run
               ClearFormat(noChangeDelta, array.Start, array.Length);
            } else {
               var arrayName = GetAnchorFromAddress(-1, array.Start);
               array = array.Append(noChangeDelta, desiredChange); // if append is negative, the name might get erased. Store it.
               ObserveAnchorWritten(noChangeDelta, arrayName, array);
            }
         }

         AddTableDirect(destination, name, format, validatePointerFound: offset == 0);
         if (offset != 0) ObserveRunWritten(noChangeDelta, new OffsetPointerRun(source, offset));
      }

      /// <summary>
      /// Find a table given an address for that table
      /// </summary>
      private void AddTableDirect(int destination, string name, string format, bool validatePointerFound = false) {
         using (ModelCacheScope.CreateScope(this)) {
            var errorInfo = ApplyAnchor(this, noChangeDelta, destination, "^" + name + format, allowAnchorOverwrite: true);
            validatePointerFound &= !errorInfo.HasError;
         }

         if (validatePointerFound) {
            CheckForEmptyAnchors(destination, name);
         }
      }

      private bool isCFRU;
      private const int CFRU_Check_Address = 0x00051A, CFRU_Check_Value = 0x46C0, CFRU_ValueRepeateCount = 5;
      private bool GetIsCFRU() {
         if (gameCode != FireRed) return false;
         if (RawData.Length < CFRU_Check_Address + 3) return false;
         for (int i = 0; i < CFRU_ValueRepeateCount; i++) {
            if (this.ReadMultiByteValue(CFRU_Check_Address + i * 2, 2) != CFRU_Check_Value) return false;
         }
         return true;
      }
      private string AdjustFormatForCFRU(string name, string format) {
         if (!isCFRU) return format;

         // remove the extra +28 from pokemon-related tables
         if (format.EndsWith(PokemonNameTable + "+28")) return format.Substring(0, format.Length - 3);

         // remove the extra +1 from pokemon-related tables
         if (format.EndsWith(PokemonNameTable + "+1")) return format.Substring(0, format.Length - 2);

         // ability names are 17 characters, not 13
         if (name == AbilityNamesTable) return format.Replace("\"\"13", "\"\"17");

         // level-up moves uses Jambo format
         if (name == LevelMovesTableName) return $"[movesFromLevel<[move:{MoveNamesTable} level.]!0000FF>]{PokemonNameTable}";

         return format;
      }
   }
}
