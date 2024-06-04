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
            newList[12] = "RaiseSpeed1Primary";
            newList[14] = "RaiseSpDefese1Primary";
            newList[15] = "RaiseAccuracy1Primary";
            newList[21] = "LowerSpAtk1Primary";
            newList[22] = "LowerSpDef1Primary";
            newList[25] = "RemoveStatChanges";
            newList[55] = "RaiseAccuracy2Primary";
            newList[56] = "RaiseEvasion2Primary";
            newList[61] = "LowerSpAttack2Primary";
            newList[63] = "LowerAccuracy2Primary";
            newList[64] = "LowerEvasion2Primary";
            newList[74] = "LowerEvasion1HitChance";
            newList[77] = "Unused4D";
            newList[78] = "Unused4E";
            newList[96] = "RaiseSpeed1HitChance";
            newList[110] = "RaiseSpAtk1HitChance";
            newList[121] = "Unused79";
            newList[123] = "Unused7B";
            newList[125] = "BurnUp";
            newList[135] = "RaiseDefense2HitChance";
            newList[150] = "Unused96";
            newList[169] = "UnusedA9";
            newList[174] = "BoostNextElectricMoveAndRaiseSpDef";
            newList[185] = "UnusedB9";
            newList[196] = "UnusedC4";
            newList[198] = "RaiseAttack1SpAtk1";
            newList[199] = "RaiseAttack1Accuracy1";
            newList[200] = "UnusedC8";
            newList[202] = "UnusedCA";
            newList[203] = "UnusedCB";
            newList[207] = "RaiseAllStatsPrimary";
            newList[213] = "Stat Swap or Split";
            newList.AddRange(new string[] {null, null, null, null, "Me First", "Eat Berry", "Natural Gift", "Smack Down", "Remove Target Stat Changes", "SleepHitChance", null, null,
            "Set Terrain", "Pledge", "Field Effects", "Fling", "Feint", "Attack Blockers", "Type Changes", "Heal Target", "Topsy Turvy Electrify", "Fairy Lock Happy Hour",
            "Instruct After You Quash", "Sucker Punch", "Ignore Redirection", "Team Effects", "Camouflage", "Flame Burst", "Last Resort Sky Drop", "Damage Set Terrain", "Teatime"});
            for(var k = 0; k < 8; k++) newList.Add(null); // 245 to 252 are unused.
            newList.AddRange(new string[] {"Max Move", "Synchronoise"});
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
         
         // Update the type chart effectivenesses list
         if (isCFRU && TryGetList("effectiveness", out var multipliers)) {
            var newMultipliers = new string[21]; // 0 to 20
            newMultipliers[0] = "1x";
            newMultipliers[1] = "0x";
            newMultipliers[5] = "0.5x";
            newMultipliers[20] = "2x";
            SetList(new NoDataChangeDeltaModel(), "effectiveness", newMultipliers, null, StoredList.GenerateHash(newMultipliers));
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
         var trainerabilities = new List<string> { "Hidden", "Ability1", "Ability2", "RandomNormal", "RandomAny" };
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
         
         // Move effects in HUBOL
         /* var newList2 = new List<string>();
         newList2.AddRange(new[] {
            "EFFECT_HIT", "EFFECT_SLEEP", "EFFECT_POISON_HIT", "EFFECT_ABSORB",
            "EFFECT_BURN_HIT", "EFFECT_FREEZE_HIT", "EFFECT_PARALYZE_HIT", "EFFECT_EXPLOSION",
            "EFFECT_DREAM_EATER", "EFFECT_MIRROR_MOVE", "EFFECT_ATTACK_UP", "EFFECT_DEFENSE_UP",
            "EFFECT_SPEED_UP", "EFFECT_SPECIAL_ATTACK_UP", "EFFECT_SPECIAL_DEFENSE_UP", "EFFECT_ACCURACY_UP",
            "EFFECT_EVASION_UP", "EFFECT_ALWAYS_HIT", "EFFECT_ATTACK_DOWN", "EFFECT_DEFENSE_DOWN",
            "EFFECT_SPEED_DOWN", "EFFECT_SPECIAL_ATTACK_DOWN", "EFFECT_SPECIAL_DEFENSE_DOWN", "EFFECT_ACCURACY_DOWN",
            "EFFECT_EVASION_DOWN", "EFFECT_HAZE", "EFFECT_BIDE", "EFFECT_RAMPAGE",
            "EFFECT_ROAR", "EFFECT_MULTI_HIT", "EFFECT_CONVERSION", "EFFECT_FLINCH_HIT",
            "EFFECT_RESTORE_HP", "EFFECT_TOXIC", "EFFECT_PAY_DAY", "EFFECT_LIGHT_SCREEN",
            "EFFECT_TRI_ATTACK", "EFFECT_REST", "EFFECT_0HKO", "EFFECT_RAZOR_WIND",
            "EFFECT_SUPER_FANG", "EFFECT_DRAGON_RAGE", "EFFECT_TRAP", "EFFECT_HIGH_CRITICAL",
            "EFFECT_DOUBLE_HIT", "EFFECT_RECOIL_IF_MISS", "EFFECT_MIST", "EFFECT_FOCUS_ENERGY",
            "EFFECT_RECOIL", "EFFECT_CONFUSE", "EFFECT_ATTACK_UP_2", "EFFECT_DEFENSE_UP_2",
            "EFFECT_SPEED_UP_2", "EFFECT_SPECIAL_ATTACK_UP_2", "EFFECT_SPECIAL_DEFENSE_UP_2", "EFFECT_ACCURACY_UP_2",
            "EFFECT_EVASION_UP_2", "EFFECT_TRANSFORM", "EFFECT_ATTACK_DOWN_2", "EFFECT_DEFENSE_DOWN_2",
            "EFFECT_SPEED_DOWN_2", "EFFECT_SPECIAL_ATTACK_DOWN_2", "EFFECT_SPECIAL_DEFENSE_DOWN_2", "EFFECT_ACCURACY_DOWN_2",
            "EFFECT_EVASION_DOWN_2", "EFFECT_REFLECT", "EFFECT_POISON", "EFFECT_PARALYZE",
            "EFFECT_ATTACK_DOWN_HIT", "EFFECT_DEFENSE_DOWN_HIT", "EFFECT_SPEED_DOWN_HIT", "EFFECT_SPECIAL_ATTACK_DOWN_HIT",
            "EFFECT_SPECIAL_DEFENSE_DOWN_HIT", "EFFECT_ACCURACY_DOWN_HIT", "EFFECT_EVASION_DOWN_HIT", "EFFECT_SKY_ATTACK",
            "EFFECT_CONFUSE_HIT", "EFFECT_SPECIAL_DEFENSE_DOWN_2_HIT", "EFFECT_BLANK_78", "EFFECT_SUBSTITUTE",
            "EFFECT_RECHARGE", "EFFECT_RAGE", "EFFECT_MIMIC", "EFFECT_METRONOME",
            "EFFECT_LEECH_SEED", "EFFECT_SPLASH", "EFFECT_DISABLE", "EFFECT_LEVEL_DAMAGE",
            "EFFECT_PSYWAVE", "EFFECT_COUNTER", "EFFECT_ENCORE", "EFFECT_PAIN_SPLIT",
            "EFFECT_SNORE", "EFFECT_CONVERSION_2", "EFFECT_LOCK_ON", "EFFECT_SKETCH",
            "EFFECT_SPEED_UP_1_HIT", "EFFECT_SLEEP_TALK", "EFFECT_DESTINY_BOND", "EFFECT_FLAIL",
            "EFFECT_SPITE", "EFFECT_FALSE_SWIPE", "EFFECT_HEAL_BELL", "EFFECT_QUICK_ATTACK",
            "EFFECT_TRIPLE_KICK", "EFFECT_THIEF", "EFFECT_MEAN_LOOK", "EFFECT_NIGHTMARE",
            "EFFECT_MINIMIZE", "EFFECT_CURSE", "EFFECT_SPECIAL_ATTACK_UP_HIT", "EFFECT_PROTECT",
            "EFFECT_SPIKES", "EFFECT_FORESIGHT", "EFFECT_PERISH_SONG", "EFFECT_SANDSTORM",
            "EFFECT_BLANK_116", "EFFECT_ROLLOUT", "EFFECT_SWAGGER", "EFFECT_FURY_CUTTER",
            "EFFECT_ATTRACT", "EFFECT_BLANK_121", "EFFECT_PRESENT", "EFFECT_BLANK_123",
            "EFFECT_SAFEGUARD", "EFFECT_BURN_UP", "EFFECT_MAGNITUDE", "EFFECT_BATON_PASS",
            "EFFECT_PURSUIT", "EFFECT_RAPID_SPIN", "EFFECT_SONICBOOM", "EFFECT_BLANK_83",
            "EFFECT_MORNING_SUN", "EFFECT_BLANK_133", "EFFECT_BLANK_134", "EFFECT_DEFENSE_UP_2_HIT",
            "EFFECT_RAIN_DANCE", "EFFECT_SUNNY_DAY", "EFFECT_DEFENSE_UP_HIT", "EFFECT_ATTACK_UP_HIT",
            "EFFECT_ALL_STATS_UP_HIT", "EFFECT_HIGHER_OFFENSES_DEFENSES_UP_HIT", "EFFECT_BELLY_DRUM", "EFFECT_PSYCH_UP",
            "EFFECT_MIRROR_COAT", "EFFECT_SKULL_BASH", "EFFECT_TWISTER", "EFFECT_EARTHQUAKE",
            "EFFECT_FUTURE_SIGHT", "EFFECT_GUST", "EFFECT_SPLINTER", "EFFECT_SOLARBEAM",
            "EFFECT_THUNDER", "EFFECT_TELEPORT", "EFFECT_BEAT_UP", "EFFECT_SEMI_INVULNERABLE",
            "EFFECT_DEFENSE_CURL", "EFFECT_SPRINGTIDE_STORM", "EFFECT_FAKE_OUT", "EFFECT_UPROAR",
            "EFFECT_STOCKPILE", "EFFECT_SPIT_UP", "EFFECT_SWALLOW", "EFFECT_BLANK_A3",
            "EFFECT_HAIL", "EFFECT_TORMENT", "EFFECT_FLATTER", "EFFECT_WILL_O_WISP",
            "EFFECT_MEMENTO", "EFFECT_BLANK_169", "EFFECT_FOCUS_PUNCH", "EFFECT_SMELLINGSALT",
            "EFFECT_FOLLOW_ME", "EFFECT_NATURE_POWER", "EFFECT_CHARGE", "EFFECT_TAUNT",
            "EFFECT_HELPING_HAND", "EFFECT_TRICK", "EFFECT_ROLE_PLAY", "EFFECT_WISH",
            "EFFECT_ASSIST", "EFFECT_INGRAIN", "EFFECT_SUPERPOWER", "EFFECT_MAGIC_COAT",
            "EFFECT_RECYCLE", "EFFECT_BLANK_185", "EFFECT_BRICK_BREAK", "EFFECT_YAWN",
            "EFFECT_KNOCK_OFF", "EFFECT_ENDEAVOR", "EFFECT_BLANK_190", "EFFECT_SKILL_SWAP",
            "EFFECT_IMPRISON", "EFFECT_REFRESH", "EFFECT_GRUDGE", "EFFECT_SNATCH",
            "EFFECT_BLANK_196", "EFFECT_SECRET_POWER", "EFFECT_ATK_SPATK_UP", "EFFECT_ATK_ACC_UP",
            "EFFECT_DEF_SPD_UP", "EFFECT_MUD_SPORT", "EFFECT_VENOM_DRENCH", "EFFECT_PLAY_NICE",
            "EFFECT_OVERHEAT", "EFFECT_TICKLE", "EFFECT_COSMIC_POWER", "EFFECT_EXTREME_EVOBOOST",
            "EFFECT_BULK_UP", "EFFECT_BAD_POISON_HIT", "EFFECT_WATER_SPORT", "EFFECT_CALM_MIND",
            "EFFECT_DRAGON_DANCE", "EFFECT_STAT_SWAP_SPLIT", "EFFECT_BLANK_214", "EFFECT_BLANK_215",
            "EFFECT_BLANK_216", "EFFECT_BLANK_217", "EFFECT_ME_FIRST", "EFFECT_EAT_BERRY",
            "EFFECT_NATURAL_GIFT", "EFFECT_SMACK_DOWN", "EFFECT_REMOVE_TARGET_STAT_CHANGES", "EFFECT_RELIC_SONG",
            "EFFECT_BLANK_224", "EFFECT_BLANK_225", "EFFECT_SET_TERRAIN", "EFFECT_PLEDGE",
            "EFFECT_FIELD_EFFECTS", "EFFECT_FLING", "EFFECT_FEINT", "EFFECT_ATTACK_BLOCKERS",
            "EFFECT_TYPE_CHANGES", "EFFECT_HEAL_TARGET", "EFFECT_TOPSY_TURVY_ELECTRIFY", "EFFECT_FAIRY_LOCK_HAPPY_HOUR",
            "EFFECT_INSTRUCT_AFTER_YOU_QUASH", "EFFECT_SUCKER_PUNCH", "EFFECT_IGNORE_REDIRECTION", "EFFECT_TEAM_EFFECTS",
            "EFFECT_CAMOUFLAGE", "EFFECT_FLAMEBURST", "EFFECT_LAST_RESORT", "EFFECT_DAMAGE_SET_TERRAIN",
            "EFFECT_TEATIME", "EFFECT_POLTERGEIST", "EFFECT_SKY_DROP",
         });
         SetList(new NoDataChangeDeltaModel(), "newmoveeffectoptions", newList2, null, StoredList.GenerateHash(newList)); */

         /* Adding the new type chart
         AddTable(0x145BB74, 0, "data.pokemon.type.chart", 
         "[Norm.effectiveness Fight.effectiveness Fly.effectiveness Poison.effectiveness Grd.effectiveness Rock.effectiveness Bug.effectiveness Ghost.effectiveness Steel.effectiveness param9.effectiveness Fire.effectiveness Water.effectiveness Grass.effectiveness Elec.effectiveness Psych.effectiveness Ice.effectiveness Drag.effectiveness Dark.effectiveness param18.effectiveness param19.effectiveness param20.effectiveness param21.effectiveness param22.effectiveness Fairy.effectiveness]data.pokemon.type.names"); */
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
         if (name == MoveNamesTable) format += "896";
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

         

         // type chart
         if (name == "data.pokemon.type.chart") {
            source = 0x145BB74;
            format = "[Norm.effectiveness Fight.effectiveness Fly.effectiveness Poison.effectiveness Grd.effectiveness Rock.effectiveness Bug.effectiveness Ghost.effectiveness Steel.effectiveness param9.effectiveness Fire.effectiveness Water.effectiveness Grass.effectiveness Elec.effectiveness Psych.effectiveness Ice.effectiveness Drag.effectiveness Dark.effectiveness param18.effectiveness param19.effectiveness param20.effectiveness param21.effectiveness param22.effectiveness Fairy.effectiveness]data.pokemon.type.names";
         }

         return format;
      }
   }
}
