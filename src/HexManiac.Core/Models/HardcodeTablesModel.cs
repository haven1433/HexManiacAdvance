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
            AddDefaultLists();
            DecodeHeader();
            DecodeTablesFromReference();
            DecodeStreams();
         }

         ResolveConflicts();
      }

      private void DecodeHeader() {
         ObserveAnchorWritten(noChangeDelta, "GameTitle", new AsciiRun(0xA0, 12));
         ObserveAnchorWritten(noChangeDelta, "GameCode", new AsciiRun(0xAC, 4));
         ObserveAnchorWritten(noChangeDelta, "MakerCode", new AsciiRun(0xB0, 2));

         if (gameCode != Ruby && gameCode != Sapphire && gameCode != Ruby1_1 && gameCode != Sapphire1_1) {
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
            AddTable(address, name, format);
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

      private void AddDefaultLists() {
         #region Move Effects
         var moveEffects = new string[214];
         moveEffects[0] = "None";
         moveEffects[1] = "SleepPrimary";
         moveEffects[2] = "Poison";
         moveEffects[3] = "HealHalf";
         moveEffects[4] = "Burn";
         moveEffects[5] = "Freeze";
         moveEffects[6] = "Paralyze";
         moveEffects[7] = "Suicide";
         moveEffects[8] = "HealHalfIfOpponentSleeping";
         moveEffects[9] = "RepeatFoeMove";
         moveEffects[10] = "RaiseAttackPrimary";
         moveEffects[11] = "RaiseDefensePrimary";
         moveEffects[12] = "???RaiseSpeedPrimary???";
         moveEffects[13] = "RaiseAttackSpAttackPrimary";
         moveEffects[14] = "unknown1";
         moveEffects[15] = "???RaiseAccuracyPrimary???";
         moveEffects[16] = "RaiseEvasivenessPrimary";
         moveEffects[17] = "NeverMiss";
         moveEffects[18] = "LowerAttackPrimary";
         moveEffects[19] = "LowerDefensePrimary";
         moveEffects[20] = "LowerSpeedPrimary";
         moveEffects[21] = "???LowerAttackSpAttackPrimary???";
         moveEffects[22] = "unknown2";
         moveEffects[23] = "LowerAccuracyPrimary";
         moveEffects[24] = "LowerEvasionPrimary";
         moveEffects[25] = "RemoveStateChanges";
         moveEffects[26] = "Bide";
         moveEffects[27] = "2to3turnsThenConfused";
         moveEffects[28] = "OpponentSwitch";
         moveEffects[29] = "2to5hits";
         moveEffects[30] = "ChangeTypeToFriendlyMove";
         moveEffects[31] = "Flinch";
         moveEffects[32] = "Recover";
         moveEffects[33] = "BadPoisonPrimary";
         moveEffects[34] = "Money";
         moveEffects[35] = "RaiseSpDef2Wall";
         moveEffects[36] = "ParalyzeBurnFreeze";
         moveEffects[37] = "Rest";
         moveEffects[38] = "OHKO";
         moveEffects[39] = "2turnHighCrit";
         moveEffects[40] = "HalfDamage";
         moveEffects[41] = "20Damage";
         moveEffects[42] = "2to5turnTrap";
         moveEffects[43] = "HighCrit";
         moveEffects[44] = "2hits";
         moveEffects[45] = "MissHurtSelf";
         moveEffects[46] = "PreventStatReduction";
         moveEffects[47] = "RaiseCriticalRate";
         moveEffects[48] = "25Recoil";
         moveEffects[49] = "ConfusionPrimary";
         moveEffects[50] = "RaiseAttack2Primary";
         moveEffects[51] = "RaiseDefense2Primary";
         moveEffects[52] = "RaiseSpeed2Primary";
         moveEffects[53] = "RaiseSpAtk2Primary";
         moveEffects[54] = "RaiseSpDef2Primary";
         moveEffects[55] = "???RaiseAccuracy2Primary???";
         moveEffects[56] = "???RaiseEvasiveness2Primary???";
         moveEffects[57] = "Transform";
         moveEffects[58] = "LowerAttack2Primary";
         moveEffects[59] = "LowerDefense2Primary";
         moveEffects[60] = "LowerSpeed2Primary";
         moveEffects[61] = "???LowerSpAtk2Primary???";
         moveEffects[62] = "LowerSpDef2Primary";
         moveEffects[63] = "???";
         moveEffects[64] = "????";
         moveEffects[65] = "RaiseDefense2Wall";
         moveEffects[66] = "PoisonPrimary";
         moveEffects[67] = "ParalyzePrimary";
         moveEffects[68] = "LowerAttack";
         moveEffects[69] = "LowerDefense";
         moveEffects[70] = "LowerSpeed";
         moveEffects[71] = "LowerSpAtk";
         moveEffects[72] = "LowerSpDef";
         moveEffects[73] = "LowerAccuracy";
         moveEffects[74] = "?????";
         moveEffects[75] = "2turnHighCritFlinch";
         moveEffects[76] = "Confusion";
         moveEffects[77] = "2hitsPoison";
         moveEffects[78] = "NeverMiss(VitalThrow)";
         moveEffects[79] = "Substitute";
         moveEffects[80] = "SkipNextTurn";
         moveEffects[81] = "StrongerForLessHealth";
         moveEffects[82] = "Mimic";
         moveEffects[83] = "RandomMove";
         moveEffects[84] = "SeedOpponent";
         moveEffects[85] = "Splash";
         moveEffects[86] = "Disable";
         moveEffects[87] = "DamageBasedOnLevel";
         moveEffects[88] = "DamageRandom";
         moveEffects[89] = "DoublePhysicalDamage";
         moveEffects[90] = "OpponentRepeatMoveFor2to6turns";
         moveEffects[91] = "PainSplit";
         moveEffects[92] = "WhileSleepingFlinch";
         moveEffects[93] = "ChangeTypeToResistPreviousHit";
         moveEffects[94] = "NextAttackHits";
         moveEffects[95] = "Sketch";
         moveEffects[96] = "??????";
         moveEffects[97] = "SleepTalk";
         moveEffects[98] = "DestinyBond";
         moveEffects[99] = "StrengthDependsOnHealth";
         moveEffects[100] = "ReducePP";
         moveEffects[101] = "FalseSwipe";
         moveEffects[102] = "HealPartyStatus";
         moveEffects[103] = "NormalPlusPriority";
         moveEffects[104] = "3turnTripleHit";
         moveEffects[105] = "StealItem";
         moveEffects[106] = "NoSwitch";
         moveEffects[107] = "Nightmare";
         moveEffects[108] = "RaiseEvasivenessAndBecomeSmaller";
         moveEffects[109] = "Curse";
         moveEffects[110] = "??";
         moveEffects[111] = "EvadeNextAttack";
         moveEffects[112] = "Spikes";
         moveEffects[113] = "FoeCannnotRaiseEvasion";
         moveEffects[114] = "PerishSong";
         moveEffects[115] = "Sandstorm";
         moveEffects[116] = "Endure";
         moveEffects[117] = "5turnsUntilMiss";
         moveEffects[118] = "ConfuseAndRaiseAttack2";
         moveEffects[119] = "GetStrongerEachHit";
         moveEffects[120] = "Attract";
         moveEffects[121] = "StrongerWithFriendship";
         moveEffects[122] = "Present";
         moveEffects[123] = "WeakerWithFriendship";
         moveEffects[124] = "PreventStatus5Turns";
         moveEffects[125] = "BurnRaiseSpeed";
         moveEffects[126] = "Magnitude";
         moveEffects[127] = "BatonPass";
         moveEffects[128] = "DoublePowerIfOpponentSwitching";
         moveEffects[129] = "RemoveBindSeedSpikes";
         moveEffects[130] = "20Damage";
         moveEffects[131] = "???????";
         moveEffects[132] = "MorningSun";
         moveEffects[133] = "Synthesis";
         moveEffects[134] = "Moonlight";
         moveEffects[135] = "HiddenPower";
         moveEffects[136] = "Rain5turns";
         moveEffects[137] = "Sun5turns";
         moveEffects[138] = "RaiseDefense";
         moveEffects[139] = "RaiseAttack";
         moveEffects[140] = "RaiseAllStats";
         moveEffects[141] = "????????";
         moveEffects[142] = "HalfHealthToRaiseAttack6";
         moveEffects[143] = "CopyFoeStatChangesPrimary";
         moveEffects[144] = "DoubleSpecialDamage";
         moveEffects[145] = "RaiseDefenseThenAttackTurn2";
         moveEffects[146] = "FlinchAndDoubleDamageToFly";
         moveEffects[147] = "DoubleDamageToDig";
         moveEffects[148] = "DamageIn2Turns";
         moveEffects[149] = "DoubleDamageToFly";
         moveEffects[150] = "FlinchAndDoubleDamageToMinimize";
         moveEffects[151] = "ChargeFirstTurn";
         moveEffects[152] = "ParalyzeAndIncreaseAccuracyInRain";
         moveEffects[153] = "Escape";
         moveEffects[154] = "DamageBasedOnPartySize";
         moveEffects[155] = "2turn";
         moveEffects[156] = "RaiseDefenseAndImproveRollingMoves";
         moveEffects[157] = "RecoverOrFriend";
         moveEffects[158] = "OnlyWorksOnce";
         moveEffects[159] = "2to5turnsNoSleep";
         moveEffects[160] = "Stockpile";
         moveEffects[161] = "Spit Up";
         moveEffects[162] = "Swallow";
         moveEffects[163] = "?????????";
         moveEffects[164] = "Hail5turns";
         moveEffects[165] = "Torment";
         moveEffects[166] = "ConfuseAndRaiseSpAtk2";
         moveEffects[167] = "BurnPrimary";
         moveEffects[168] = "SuicideLowerAtkSpAtk2";
         moveEffects[169] = "DoubleDamageIfStatus";
         moveEffects[170] = "SelfFlinchIfHit";
         moveEffects[171] = "DoubleDamageToParalyzeAndHealParalyze";
         moveEffects[172] = "ForceFoesAttackMe";
         moveEffects[173] = "NaturePower";
         moveEffects[174] = "BoostNextElectricMove";
         moveEffects[175] = "Taunt";
         moveEffects[176] = "BoostAllyPower";
         moveEffects[177] = "TradeHeldItems";
         moveEffects[178] = "CopyAbility";
         moveEffects[179] = "HealHalfNextTurn";
         moveEffects[180] = "UseAllyMove";
         moveEffects[181] = "Ingrain";
         moveEffects[182] = "LowerSelfAtkDef";
         moveEffects[183] = "ReflectStatusMoves";
         moveEffects[184] = "Recycle";
         moveEffects[185] = "DoubleDamageIfHitThisTurn";
         moveEffects[186] = "BreakWall";
         moveEffects[187] = "Yawn";
         moveEffects[188] = "KnockOff";
         moveEffects[189] = "Endeavor";
         moveEffects[190] = "DamageBasedOnHighRemainingHealth";
         moveEffects[191] = "SkillSwap";
         moveEffects[192] = "Imprison";
         moveEffects[193] = "HealSelfStatus";
         moveEffects[194] = "Grudge";
         moveEffects[195] = "Snatch";
         moveEffects[196] = "DamageBasedOnWeight";
         moveEffects[197] = "SecondEffectBasedOnTerrain";
         moveEffects[198] = "33Recoil";
         moveEffects[199] = "ConfuseAllPokemon";
         moveEffects[200] = "HighCritBurn";
         moveEffects[201] = "MudSport";
         moveEffects[202] = "BadPoison";
         moveEffects[203] = "WeatherBall";
         moveEffects[204] = "LowerSpAtk2Self";
         moveEffects[205] = "LowerAttackDefensePrimary";
         moveEffects[206] = "RaiseDefenseSpDef";
         moveEffects[207] = "CanDamageFly";
         moveEffects[208] = "RaiseAttackDefensePrimary";
         moveEffects[209] = "HighCritPoison";
         moveEffects[210] = "WaterSport";
         moveEffects[211] = "RaiseSpAtkSpDefPrimary";
         moveEffects[212] = "RaiseAttackSpeedPrimary";
         moveEffects[213] = "ChangetypeFromTerrain";
         SetList(MoveEffectListName, moveEffects);
         #endregion

         #region Move Info
         SetList(MoveInfoListName, new[] {
            "Makes Contact",
            "Affected by Protect",
            "Affected by Magic Coat",
            "Affected by Snatch",
            "Affected by Mirror Move",
            "Affected by King's Rock",
         });
         #endregion

         #region Move Target
         SetList(MoveTargetListName, new[] {
            "RecentAttacker",
            "Unused",
            "Random",
            "Both",
            "Self",
            "Everyone",
            "Hazard",
         });
         #endregion

         #region Decorations Permission
         SetList(DecorationsPermissionListName, new[] {
            "Normal",
            "Put On Floor",
            "Object",
            "Place On Wall",
            "Doll or Cushion",
         });
         #endregion

         #region Decorations Category
         SetList(DecorationsCategoryListName, new[] {
            "Desk",
            "Chair",
            "Plant",
            "Unique",
            "Mat",
            "Poster",
            "Doll",
            "Cushion",
         });
         #endregion

         #region Decorations Shape
         SetList(DecorationsShapeListName, new[] {
            "1x1",
            "unused",
            "unused",
            "1x1t",
            "2x2p",
            "1x1p",
            "unused",
            "3x1",
            "2x2",
            "2x1",
         });
         #endregion

         #region Evolution Methods
         SetList(EvolutionMethodListName, new[] {
            "None",
            "Happiness",
            "Happy Day",
            "Happy Night",
            "Level",
            "Trade",
            "Trade Item",
            "Stone",
            "Level High Attack",
            "Level Attack matches Defense",
            "Level High Defense",
            "Level Odd Personality",
            "Level Even Personality",
            "Level And New Pokemon",
            "Level But New Pokemon",
            "Beauty",
         });
         #endregion
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
