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
         FlySpawns = "data.maps.fly.spawn",
         TradeTable = "data.pokemon.trades",
         MapNameTable = "data.maps.names",
         MapBankTable = "data.maps.banks",
         RematchTable = "data.trainers.vsseeker",
         RematchTableRSE = "data.trainers.rematches",
         WildTableName = "data.pokemon.wild",
         SpecialsTable = "scripts.commands.events.specials",
         MoveDataTable = "data.pokemon.moves.stats.battle",
         ItemsTableName = "data.items.stats",
         MapLayoutTable = "data.maps.layouts",
         BerryTableName = "data.items.berry.stats",
         TypesTableName = "data.pokemon.type.names",
         MoveNamesTable = "data.pokemon.moves.names",
         FlyConnections = "data.maps.fly.connections",
         PokeIconsTable = "graphics.pokemon.icons.sprites",
         FontWidthTable = "graphics.text.font.black.width",
         DexInfoTableName = "data.pokedex.stats",
         PokemonNameTable = "data.pokemon.names",
         TrainerTableName = "data.trainers.stats",
         MoveEffectsTable = "scripts.moves.effects",
         NaturesTableName = "data.pokemon.natures.names",
         OverworldSprites = "graphics.overworld.sprites",
         BallSpritesTable = "graphics.items.ball.sprites",
         BackSpritesTable = "graphics.pokemon.sprites.back",
         PokemonStatsTable = "data.pokemon.stats",
         AbilityNamesTable = "data.abilities.names",
         EggMovesTableName = "data.pokemon.moves.egg",
         OverworldPalettes = "graphics.overworld.palettes",
         BallPalettesTable = "graphics.items.ball.palettes",
         FrontSpritesTable = "graphics.pokemon.sprites.front",
         ContestTypesTable = "data.pokemon.type.contest.names",
         PokePalettesTable = "graphics.pokemon.palettes.normal",
         EvolutionTableName = "data.pokemon.evolutions",
         TypeChartTableName = "data.pokemon.type.chart",
         ShinyPalettesTable = "graphics.pokemon.palettes.shiny",
         TrainerSpritesName = "graphics.trainers.sprites.front",
         ItemImagesTableName = "graphics.items.sprites",
         LevelMovesTableName = "data.pokemon.moves.levelup",
         ItemEffectsTableName = "data.items.effects",
         RegionalDexTableName = "data.pokedex.regional",
         NationalDexTableName = "data.pokedex.national",
         DecorationsTableName = "data.decorations.stats",
         MultichoiceTableName = "scripts.text.multichoice",
         MoveDescriptionsName = "data.pokemon.moves.descriptions",
         BackupFontWidthTable = "graphics.text.font.default.width",
         PokeIconPalettesTable = "graphics.pokemon.icons.palettes",
         DefaultSpriteNamespace = "graphics.new.sprite",
         TrainerClassNamesTable = "data.trainers.classes.names",
         ConversionDexTableName = "data.pokedex.hoennToNational",
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

      /// <summary>
      /// The first 0x100 bytes of the GBA rom is always the header.
      /// The next 0x100 bytes contains some tables and some startup code, but nothing interesting to point to.
      /// Choosing 0x200 might prevent us from seeing an actual anchor, but it will also remove a bunch
      ///      of false positives and keep us from getting conflicts with the RomName (see DecodeHeader).
      /// </summary>
      public override int EarliestAllowedAnchor => 0x200;

      public HardcodeTablesModel(Singletons singletons, byte[] data, StoredMetadata metadata = null, bool devMode = false) : base(data, metadata, singletons, devMode) {
         gameCode = this.GetGameCode();
         if (metadata != null && !metadata.IsEmpty) {
            InitializationWorkload = (singletons?.WorkDispatcher ?? InstantDispatch.Instance).RunBackgroundWork(() => Initialize(metadata));
            return;
         }

         InitializationWorkload = (singletons?.WorkDispatcher ?? InstantDispatch.Instance).RunBackgroundWork(() => {
            {
               var noChangeDelta = new NoDataChangeDeltaModel();
               if (singletons.GameReferenceConstants.TryGetValue(gameCode, out var referenceConstants)) {
                  metadata = DecodeConstantsFromReference(this, singletons.MetadataInfo, metadata, referenceConstants);
               }
               Initialize(metadata);
               isCFRU = GetIsCFRU(this);

               // in vanilla emerald, this pointer isn't four-byte aligned
               // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
               if (gameCode == Emerald && data.Length > EarliestAllowedAnchor && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

               var gamesToDecode = new[] {
                  Ruby,
                  Sapphire,
                  Emerald,
                  FireRed,
                  LeafGreen,
                  Ruby1_1,
                  Sapphire1_1,
                  FireRed1_1,
                  LeafGreen1_1,
                  "BPRF0", // french firered
                  "BPEF0", // french emerald
                  "BPEI0", // italian emerald
                  "ABCD0", // for tests
               };

               foreach (var defaultMetadata in GetDefaultMetadatas(gameCode.PadRight(4).Substring(0, 4).ToLower(), gameCode.ToLower())) {
                  this.LoadMetadata(defaultMetadata);
               }
               if (gamesToDecode.Contains(gameCode)) {
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
         // add evolution types
         if (isCFRU && TryGetList(EvolutionMethodListName, out var evolutionmethods)) {
            var newList = new List<string>(evolutionmethods);
            newList.Add("Rain Or Fog");
            newList.Add("Move Type");      // 17 type
            newList.Add("Type in Party");  // 18 type
            newList.Add("Map");
            newList.Add("Male");
            newList.Add("Female");
            newList.Add("Level Night");
            newList.Add("Level Day");
            newList.Add("Hold Item Night"); // 24 hold item
            newList.Add("Hold Item Day");   // 25 hold item
            newList.Add("Move Name");       // 26 move name
            newList.Add("Mon in Party");    // 27 species
            newList.Add("Level Time Range");
            newList.Add("Flag Set");
            newList.Add("3 Critical Hits In One Battle");
            newList.Add("Nature High");
            newList.Add("Nature Low");
            newList.Add("Damage Location");
            newList.Add("Item Location");
            while (newList.Count <= 252) newList.Add(null); // skip to gigantamax = 253 mega = 254
            newList.Add("Gigantamax");
            newList.Add("Mega");
            SetList(new NoDataChangeDeltaModel(), EvolutionMethodListName, newList, null, StoredList.GenerateHash(newList));
         }

         // add move effects
         if (isCFRU && TryGetList(MoveEffectListName, out var moveeffectsoptions)) {
            var newList = new List<string>(moveeffectsoptions);
            newList.Add("Me First");
            newList.Add("Eat Berry");
            newList.Add("Natural Gift");
            newList.Add("Smack Down");
            newList.Add("Remove Target Stat Changes");
            newList.Add("Relic Song");
            newList.Add("Set Terrain");
            newList.Add("Pledge");
            newList.Add("Field Effects");
            newList.Add("Fling");
            newList.Add("Attack Blockers");
            newList.Add("Type Changes");
            newList.Add("Heal Target");
            newList.Add("Topsy Turvy Electrify");
            newList.Add("Fairy Lock Happy Hour");
            newList.Add("Instruct After You Quash");
            newList.Add("Sucker Punch");
            newList.Add("Team Effects");
            newList.Add("Camouflage");
            newList.Add("Synchronoise");
            SetList(new NoDataChangeDeltaModel(), MoveEffectListName, newList, null, StoredList.GenerateHash(newList));
         }

         // add item hold effects
         if (isCFRU && TryGetList("holdeffects", out var holdeffects)) {
            var newList = new List<string>(holdeffects);
            newList.AddRange(new[] {
               "UnusedA", "RockyHelmet", "UnusedB", "AssaultVest",
               "Eviolite", "Plate", "MegaStone", "LifeOrb",
               "ToxicOrb", "FlameOrb", "BlackSludge", "SmoothRock",
               "DampRock", "HeatRock", "IcyRock", "LightClay",
               "WideLens", "SafetyGoggles", "WeaknessPolicy", "Drive",
               "Memory", "AdamantOrb", "LustrousOrb", "GriseousOrb",
               "DestinyKnot", "ExpertBelt", "PrimalOrb", "Gem",
               "WeaknessBerry", "CustapBerry", "LaggingTail", "IronBall",
               "BindingBand", "UnusedC", "ProtectivePads", "AbsorbBulb",
               "AirBalloon", "Bigroot", "CellBattery", "EjectButton",
               "FloatStone", "GripClaw", "LuminousMoss", "UnusedD",
               "Metronome", "MuscleBand", "RedCard", "RingTarget",
               "ShedShell", "Snowball", "StickyBarb", "TerrainExtender",
               "WiseGlasses", "Seeds", "JabocaRowapBerry", "KeeBerry",
               "MarangaBerry", "ZoomLens", "AdrenalineOrb", "PowerHerb",
               "MicleBerry", "EnigmaBerry", "TypeBoosters", "ZCrystal",
               "AbilityCapsule", "EjectPack", "RoomService", "BlunderPolicy",
               "HeavyDutyBoots", "UtilityUmbrella", "ThroatSpray",
            });
            SetList(new NoDataChangeDeltaModel(), "holdeffects", newList, null, StoredList.GenerateHash(newList));
         }

         foreach (var table in tables) {
            // some tables have been removed from CFRU
            if (isCFRU && CfruIgnoreTables.Contains(table.Name)) continue;

            using (ModelCacheScope.CreateScope(this)) {
               var format = table.Format;
               AddTable(table.Address, table.Offset, table.Name, format);
            }
         }

         if (isCFRU) SetupCFRUSpecificTablesAndConstants();
      }

      public static readonly IReadOnlyList<string> CfruIgnoreTables = new List<string>() {
         "graphics.pokemon.sprites.coordinates.front",
         "data.pokedex.hoennToNational",        // causes problems with pokename count
         "graphics.pokemon.sprites.anchor",     // causes problems with pokename count
         "data.pokedex.search.alpha"            // causes problems with shiny palettes
      };

      public void SetupCFRUSpecificTablesAndConstants() {
         ShowRawIVByteForTrainer = true;

         // class-based pokeballs
         var balls = new List<string> {
            "Master Ball", "Ultra Ball", "Great Ball", "Poke Ball",
            "Safari Ball", "Net Ball", "Dive Ball", "Nest Ball",
            "Repeat Ball", "Timer Ball", "Luxury Ball", "Premier Ball",
            "Dusk Ball", "Heal Ball", "Quick Ball", "Cherish Ball",
            "Park Ball", "Fast Ball", "Level Ball", "Lure Ball",
            "Heavy Ball", "Love Ball", "Friend Ball", "Moon Ball",
            "Sport Ball", "Beast Ball", "Dream Ball",
         };
         while (balls.Count < 0xFE) balls.Add(null);
         balls.Add("Class Based");
         balls.Add("Random");
         SetList(new NoDataChangeDeltaModel(), "trainerballs", balls, null, StoredList.GenerateHash(balls));
         AddTable(0x1456790, 0, "data.trainers.classes.balls", "[ball.trainerballs]data.trainers.classes.names");

         // randomizer restrictions
         AddTable(0x1453828, 0, "data.randomizer.species.banlist", $"[species:{PokemonNameTable}]!FEFE");
         AddTable(0x1454730, 0, "data.randomizer.ability.banlist", $"[ability.{AbilityNamesTable}]!FF");

         // physical/special/split list
         var pss = new[] { "Physical", "Special", "Status" };
         SetList(new NoDataChangeDeltaModel(), "movecategory", pss, null, StoredList.GenerateHash(pss));

         // trainers-with-EVs table
         var trainerabilities = new List<string> { "Hidden", "Abiilty1", "Ability2", "RandomNormal", "RandomAny" };
         SetList(new NoDataChangeDeltaModel(), "trainerabilities", trainerabilities, null, StoredList.GenerateHash(trainerabilities));
         AddTable(0x1456798, 0, "data.trainers.evs", "[nature.data.pokemon.natures.names ivs. hpEv. atkEv. defEv. spdEv. spAtkEv. spDefEv. ball.data.trainers.classes.names ability.trainerabilities]121");

         // trainer class-based encounter music
         AddTable(0x144C110, 0, "data.trainers.classes.music", "[song:songnames]data.trainers.classes.names");

         // trainer sprite-based mugshots
         AddTable(0x144FC94, 0, "data.trainers.sprites.mugshots", "[sprite<`lzs4x8x8|data.trainers.sprites.mugshots`> pal<`lzp4`> size: x:|z y:|z unused:]graphics.trainers.sprites.front-0-1");

         // kanto dex
         // first hword is actually the length of the table - 1
         // every hword after that is what pokemon appears in that slot of the dex (slot 1, slot 2, etc)
         AddTable(0x160A52C, 0, "data.pokedex.kanto", "[mon:data.pokemon.names]152");

         // PSS icons/palette
         if (0x143B0EC < Count - 4) {
            var spriteStart = ReadPointer(0x143B0EC);
            AddTableDirect(spriteStart, "graphics.pokemon.moves.category.icons", "`ucs4x3x18|graphics.pokemon.moves.category.palette`");
            AddTableDirect(spriteStart + 0x6C0, "graphics.pokemon.moves.category.palette", "`ucp4`");
         }

         // z-effects
         var newList = new List<string>();
         newList.Add("None");
         newList.Add("Reset Stats");
         newList.Add("All Stats Up 1");
         newList.Add("Boost Crits");
         newList.Add("Follow Me");
         newList.Add("Curse");
         newList.Add("Recover Hp");
         newList.Add("Restore Replacement Hp");
         newList.Add("Atk Up 1");
         newList.Add("Def Up 1");
         newList.Add("Spd Up 1");
         newList.Add("Spatk Up 1");
         newList.Add("Spdef Up 1");
         newList.Add("Acc Up 1");
         newList.Add("Evsn Up 1");
         newList.Add("Atk Up 2");
         newList.Add("Def Up 2");
         newList.Add("Spd Up 2");
         newList.Add("Spatk Up 2");
         newList.Add("Spdef Up 2");
         newList.Add("Acc Up 2");
         newList.Add("Evsn Up 2");
         newList.Add("Atk Up 3");
         newList.Add("Def Up 3");
         newList.Add("Spd Up 3");
         newList.Add("Spatk Up 3");
         newList.Add("Spdef Up 3");
         newList.Add("Acc Up 3");
         newList.Add("Evsn Up 3");
         SetList(new NoDataChangeDeltaModel(), "zeffects", newList, null, StoredList.GenerateHash(newList));
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
         var noChangeDelta = new NoDataChangeDeltaModel();
         format = AdjustFormatForCFRU(name, format, ref source);
         if (source < 0 || source > RawData.Length) return;
         var destination = ReadPointer(source) - offset;
         if (destination < 0 || destination > RawData.Length) return;

         var interruptingSourceRun = GetNextRun(source);
         if (interruptingSourceRun.Start < source && interruptingSourceRun.Start + interruptingSourceRun.Length > source) {
            if (interruptingSourceRun is ITableRun tableRun) {
               var tableOffset = tableRun.ConvertByteOffsetToArrayOffset(source);
               if (tableOffset.SegmentOffset != 0) return;
               if (tableRun.ElementContent[tableOffset.SegmentIndex].Type != ElementContentType.Pointer) return;
            } else {
               // the source isn't actually a pointer, we shouldn't write anything
               return;
            }
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
         var noChangeDelta = new NoDataChangeDeltaModel();
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
      public static bool GetIsCFRU(IDataModel model) {
         var gameCode = model.GetGameCode();
         if (gameCode != FireRed) return false;
         if (model.RawData.Length < CFRU_Check_Address + 3) return false;
         for (int i = 0; i < CFRU_ValueRepeateCount; i++) {
            if (model.ReadMultiByteValue(CFRU_Check_Address + i * 2, 2) != CFRU_Check_Value) return false;
         }
         return true;
      }

      private string AdjustFormatForCFRU(string name, string format, ref int source) {
         if (!isCFRU) return format;

         // type names
         if (name == TypesTableName) format = format.Split("]")[0] + "]24";

         // type icons
         if (name == "graphics.pokemon.type.icons") format = format.Replace("ucs4x16x16", "ucs4x16x18");

         // remove the extra +28 from pokemon-related tables
         if (format.EndsWith(PokemonNameTable + "+28")) format = format.Substring(0, format.Length - 3);

         // remove the extra +1 from pokemon-related tables
         if (format.EndsWith(PokemonNameTable + "+1")) format = format.Substring(0, format.Length - 2);

         // hidden abilities stored in pokemon stats
         if (name == PokemonStatsTable) format = format.Replace("padding:", $"hiddenAbility.{AbilityNamesTable} padding.");

         // items: no constant
         if (name == ItemsTableName) format = format.Replace("data.items.count", string.Empty);

         // moves
         if (name == MoveNamesTable) format += "894";
         if (name == MoveDataTable) format = format.Replace("unused. unused:", "zMovePower. category.movecategory zMoveEffect.zeffects");

         // level-up moves uses Jambo format
         if (name == LevelMovesTableName) return $"[movesFromLevel<[move:{MoveNamesTable} level.]!0000FF>]{PokemonNameTable}";

         // tms / tutors
         if (name == MoveTutors) format = format.Replace("]15", "]128");
         if (name == TmMoves) format = format.Replace("]58", "]128");

         // overworld sprites
         if (name == OverworldSprites) format = format.Replace("graphics.overworld.tablelength", "240");

         // 16 evolutions per pokemon
         if (name == EvolutionTableName) {
            var vars = "|".Join(new[] {
               "6=" + ItemsTableName,
               "7=" + ItemsTableName,
               "17=" + TypesTableName,
               "18=" + TypesTableName,
               "24=" + ItemsTableName,
               "25=" + ItemsTableName,
               "26=" + MoveNamesTable,
               "27=" + PokemonNameTable,
               "254=" + ItemsTableName,
            });
            var methods = " ".Join("0123456789ABCDEF".Select(i => $"method{i}:evolutionmethods arg{i}:|s=method{i}({vars}) species{i}:{PokemonNameTable} value{i}:"));
            return $"[{methods}]{PokemonNameTable}";
         }

         // roaming locations
         if (name == "data.maps.roaming.sets") source = 0x14889B0;

         if (name == ItemEffectsTableName) format = format.Replace("-199", string.Empty); // item effects are as long as the items

         return format;
      }
   }
}
