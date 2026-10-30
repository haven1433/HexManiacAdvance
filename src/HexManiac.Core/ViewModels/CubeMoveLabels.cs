using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The move table of the CUBE (pokeemerald-expansion) showed some fields wrong in every .toml made before the CUBE.toml of round 14g:
   /// the damage category list had no "none" (special moves read "status", status moves "-1"), the contest lists were the vanilla ones, and a negative priority showed as 10 to 15.
   /// When a CUBE is opened with such a .toml, this puts the right lists and field formats in, in memory only (they are saved to the .toml with the next save).
   /// A list is only replaced while it is still exactly the old default: a list that somebody edited is theirs. Nothing is written to the ROM.
   /// </summary>
   public static class CubeMoveLabels {
      public const string MovesAnchor = "data.pokemon.moves.stats", TypesAnchor = "data.pokemon.type.names";

      private static readonly string[] OldDamageCategory = {
         "physical", "special", "status"
      };

      private static readonly string[] NewDamageCategory = {
         "none", "physical", "special", "status"
      };

      private static readonly string[] OldContestEffects = {
         "HighlyAppealing", "UserMoreEasilyStartled", "GreatAppealButNoMoreMoves", "RepetitionNotBoring",
            "AvoidStartleOnce", "AvoidStartle", "AvoidStartleSlightly", "UserLessEasilyStartled", "StartleFrontMon",
            "SlightlyStartlePrevMons", "StartlePrevMon", "StartlePrevMons", "BadlyStartleFrontMon", "BadlyStartlePrevMons",
            "StartlePrevMon2", "StartPrevMons2", "ShiftJudgeAttention", "StartleMonWithJudgesAttention",
            "JamsOthersButMissOneTurn", "StartleMonsSameTypeAppeal", "StartMonsCoolAppeal", "StartleMonsBeautyAppeal",
            "StartleMonsCuteAppeal", "StartleMonsSmartAppeal", "StartleMonsToughAppeal", "MakeFollowingMonNervous",
            "MakeFollowingMonsNervous", "WorsenConditionOfPrevMons", "BadlyStartlesMonsInGoodCondition", "BetterIfFirst",
            "BetterIfLast", "AppealAsGoodAsPrevOnes", "AppealAsGoodAsPrevOne", "BetterWhenLater", "QualityDependsOnTiming",
            "BetterIfSameType", "BetterIfDiffType", "AffectedByPrevAppeal", "ImproveConditionPreventNervousness",
            "BetterWithGoodCondition", "NextAppealEarlier", "NextAppealLater", "MakeScramblingTurnOrderEasier",
            "ScrambleNextTurnOrder", "ExciteAudienceInAnyContest", "BadlyStartleMonsWithGoodAppeals",
            "BettenWhenAudienceExcited", "DontExciteAudience"
      };

      private static readonly string[] NewContestEffects = {
         "HighlyAppealing", "UserMoreEasilyStartled", "GreatAppealButNoMoreMoves", "RepetitionNotBoring",
            "AvoidStartleOnce", "AvoidStartle", "AvoidStartleSlightly", "UserLessEasilyStartled", "StartleFrontMon",
            "SlightlyStartlePrevMons", "StartlePrevMon", "StartlePrevMons", "BadlyStartleFrontMon", "BadlyStartlePrevMons",
            "StartlePrevMon2", "StartlePrevMons2", "ShiftJudgeAttention", "StartleMonWithJudgesAttention",
            "JamsOthersButMissOneTurn", "StartleMonsSameTypeAppeal", "StartleMonsCoolAppeal", "StartleMonsBeautyAppeal",
            "StartleMonsCuteAppeal", "StartleMonsSmartAppeal", "StartleMonsToughAppeal", "MakeFollowingMonNervous",
            "MakeFollowingMonsNervous", "WorsenConditionOfPrevMons", "BadlyStartlesMonsInGoodCondition", "BetterIfFirst",
            "BetterIfLast", "AppealAsGoodAsPrevOnes", "AppealAsGoodAsPrevOne", "BetterWhenLater", "QualityDependsOnTiming",
            "BetterIfSameType", "BetterIfDiffType", "AffectedByPrevAppeal", "ImproveConditionPreventNervousness",
            "BetterWithGoodCondition", "NextAppealEarlier", "NextAppealLater", "MakeScramblingTurnOrderEasier",
            "ScrambleNextTurnOrder", "ExciteAudienceInAnyContest", "BadlyStartleMonsWithGoodAppeals",
            "BetterWhenAudienceExcited", "DontExciteAudience"
      };

      private static readonly string[] OldComboStarters = {
         "-", "Rain Dance", "Rage", "Focus Energy", "Hypnosis", "Endure", "Horn Attack", "Swords Dance", "Stockpile",
            "Sunny Day", "Rest", "Vice Grip", "Defense Curl", "Charge", "Rock Throw", "Yawn", "Scary Face", "Powder Snow",
            "Lock On", "Soft Boiled", "Mean Look", "Scratch", "Growth", "Hail", "Sandstorm", "Belly Drum", "Mind Reader",
            "Dragon Breath", "Dragon Rage", "Dragon Dance", "Surf", "Dive", "String Shot", "Leer", "Taunt", "Charm",
            "Harden", "Sing", "Earthquake", "DoubleTeam", "Curse", "SweetScent", "Sludge", "Sludge Bomb", "Thunder Punch",
            "Fire Punch", "Ice Punch", "Peck", "Metal Sound", "Mud Sport", "Water Sport", "Bone Club", "Bonemerang",
            "Bone Rush", "Sand Attack", "Mud Slap", "Fake Out", "Psychic", "Kinesis", "Confusion", "Pound", "Smog",
            "Calm Mind"
      };

      private static readonly string[] NewComboStarters = {
         "-", "Fire Punch", "Ice Punch", "Thunder Punch", "Scratch", "Vice Grip", "Swords Dance", "Sand Attack",
            "Horn Attack", "Leer", "Sing", "Surf", "Peck", "Leech Seed", "Growth", "String Shot", "Dragon Rage",
            "Rock Throw", "Earthquake", "Toxic", "Confusion", "Psychic", "Hypnosis", "Rage", "Double Team", "Harden",
            "Defense Curl", "Focus Energy", "Smog", "Sludge", "Bone Club", "Kinesis", "Soft Boiled", "Bonemerang", "Rest",
            "Mind Reader", "Curse", "Powder Snow", "Scary Face", "Belly Drum", "Sludge Bomb", "Mud Slap", "Bone Rush",
            "Lock On", "Sandstorm", "Endure", "Charm", "Mean Look", "Heal Bell", "Dragon Breath", "Sweet Scent",
            "Rain Dance", "Sunny Day", "Fake Out", "Stockpile", "Hail", "Charge", "Taunt", "Revenge", "Yawn", "Dive",
            "Mud Sport", "Metal Sound", "Water Sport", "Calm Mind", "Dragon Dance", "Payback", "Lucky Chant", "Worry Seed",
            "Dragon Rush", "Brave Bird", "Thunder Fang", "Ice Fang", "Fire Fang", "Attack Order", "Defend Order",
            "Heal Order", "Scald", "Dragon Tail", "Hyperspace Hole", "Thousand Arrows", "Thousand Waves",
            "Hyperspace Fury", "Shadow Bone", "Electric Terrain", "Misty Terrain", "Grassy Terrain", "Psychic Terrain",
            "Force Palm", "Thunder Wave", "Agility", "Stealth Rock", "Inferno", "Will O Wisp", "Lovely Kiss", "Spore",
            "Celebrate", "Covet", "Happy Hour", "Wish", "Amnesia", "Hone Claws", "Entrainment", "Play Nice", "Block",
            "Encore", "Dark Void", "Grass Whistle", "Sleep Powder", "Poison Gas", "Poison Powder", "Nasty Plot",
            "Parabolic Charge", "Shift Gear", "Spikes", "Toxic Spikes", "Glare", "Rock Polish", "Rototiller", "Torment"
      };

      private static readonly string[] NewContestCategories = {
         "Cool", "Beautiful", "Cute", "Clever", "Tough"
      };

      private static readonly string[] NewPriorities = {
         "0", "1", "2", "3", "4", "5", "6", "7", "-8", "-7", "-6", "-5", "-4", "-3", "-2", "-1"
      };

      // the format fragments that were wrong, and what they become
      private static readonly (string anchor, string wrong, string right, string[] lists)[] FormatFixes = {
         (MovesAnchor, "priority::|", "priority::priorities|", new[] { "priorities" }),
         (MovesAnchor, "contestCategory. ", "contestCategory.contestcategories ", new[] { "contestcategories" }),
         (TypesAnchor, "damageCategory:|", "damageCategory:damagecategory|", new[] { "damagecategory" }),
      };

      public const string ChartAnchor = "data.pokemon.type.chart";
      public const string OldChartFormat = "[[mult::effectiveness]21]21";
      private static readonly string[] ChartTypes = {
         "None", "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel", "Mystery",
            "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark", "Fairy", "Stellar"
      };

      /// <summary>The type chart of the CUBE: one row per attacking type (named by the type table), one labeled column per defending type.</summary>
      public static string NewChartFormat => "[" + string.Join(" ", ChartTypes.Select(type => $"vs{type}::effectiveness")) + "]" + TypesAnchor;

      /// <summary>
      /// The type chart used to be 21 rows of 21 unlabeled "mult" fields, and the first row (the type "None") is all normal on purpose,
      /// so it looked as if the whole chart was normal. Every row is now named after its attacking type and every field after the defending type.
      /// </summary>
      public static void RepairTypeChart(IDataModel model) {
         try {
            var noChange = new NoDataChangeDeltaModel();
            var address = model.GetAddressFromAnchor(noChange, -1, ChartAnchor);
            if (address == Pointer.NULL || !(model.GetNextRun(address) is ArrayRun run) || run.Start != address) return;
            if (run.FormatString != OldChartFormat) return;
            var types = model.GetAddressFromAnchor(noChange, -1, TypesAnchor);
            if (types == Pointer.NULL || !(model.GetNextRun(types) is ArrayRun typeRun) || typeRun.ElementCount != ChartTypes.Length) return;
            PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + ChartAnchor + NewChartFormat, allowAnchorOverwrite: true);
         } catch (Exception) {
            // an odd ROM must load whatever this finds
         }
      }

      public const string TrainerPicsAnchor = "data.trainers.sprites", OneFrameBackPic = "image<`ucs4x8x8`>";

      /// <summary>
      /// The back pictures of the player's characters hold several poses one below the other (the pictures are 0x2000 bytes apart in CUBE, four poses of 0x800 bytes), but the table only knew the first:
      /// only that one could be seen and edited. The format now covers every pose.
      /// </summary>
      public static void RepairTrainerBackPics(IDataModel model) {
         try {
            var noChange = new NoDataChangeDeltaModel();
            var address = model.GetAddressFromAnchor(noChange, -1, TrainerPicsAnchor);
            if (address == Pointer.NULL || !(model.GetNextRun(address) is ArrayRun run) || run.Start != address) return;
            var at = run.FormatString.IndexOf(OneFrameBackPic, StringComparison.Ordinal);
            if (at < 0) return;
            var table = model.GetTableModel(TrainerPicsAnchor);
            var pictures = new SortedSet<int>();
            for (int i = 0; i < table.Count; i++) {
               var back = table[i].GetSubTable("back");
               if (back == null) continue;
               var picture = back[0].GetAddress("image");
               if (picture >= 0) pictures.Add(picture);
            }
            if (pictures.Count < 2) return;
            var gap = int.MaxValue;
            int previous = -1;
            foreach (var picture in pictures) { if (previous >= 0) gap = Math.Min(gap, picture - previous); previous = picture; }
            if (gap < 4096 || gap % 2048 != 0) return; // one pose (or something unexpected): leave it
            var frames = Math.Min(gap / 2048, 8);
            var format = run.FormatString.Replace(OneFrameBackPic, $"image<`ucs4x8x{8 * frames}`>");
            PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + TrainerPicsAnchor + format, allowAnchorOverwrite: true);
         } catch (Exception) {
            // an odd ROM must load whatever this finds
         }
      }

      public static readonly string[] TrainerTables = {
         "data.trainers.stats", "data.trainers.stats.easy", "data.trainers.stats.hard",
         "data.trainers.partners", "data.trainers.partners.easy", "data.trainers.partners.hard",
      };
      public const string OldBattleType = "|battleType: mugshot", NewBattleType = "|battleType:battletype mugshot";

      /// <summary>The trainer's battle type is a choice between Single and Double instead of a bare number.</summary>
      public static void RepairTrainerBattleType(IDataModel model) {
         try {
            var noChange = new NoDataChangeDeltaModel();
            if (!model.TryGetList("battletype", out _)) model.SetList(noChange, "battletype", new[] { "Single", "Double" }, new Dictionary<int, string>(), null);
            foreach (var name in TrainerTables) {
               var address = model.GetAddressFromAnchor(noChange, -1, name);
               if (address == Pointer.NULL || !(model.GetNextRun(address) is ArrayRun run) || run.Start != address) continue;
               var format = run.FormatString;
               var at = format.IndexOf(OldBattleType, StringComparison.Ordinal);
               if (at < 0) continue;
               var fixedFormat = format.Substring(0, at) + NewBattleType + format.Substring(at + OldBattleType.Length);
               PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + name + fixedFormat, allowAnchorOverwrite: true);
            }
         } catch (Exception) {
            // an odd ROM must load whatever this finds
         }
      }

      public const string StatsAnchor = "data.pokemon.stats";
      private static readonly (string wrong, string right)[] EvolutionFixes = {
         ("evolutions<[method:evolutionmethods param: species:", "evolutions<[method:evolutionmethods param:|s=method(3=data.items.stats) species:"),
         ("[condition:evolutionconditions arg1: arg2: arg3:]", "[condition:evolutionconditions arg1:|s=condition(7=data.items.stats|16=data.pokemon.stats|19=data.pokemon.moves.stats|20=data.pokemon.stats|21=data.pokemon.type.names|23=data.pokemon.type.names|24=natures|30=data.pokemon.moves.stats|36=data.items.stats) arg2: arg3:]"),
      };

      /// <summary>The evolution item, species, move, type and nature show as names instead of numbers (the item of an ITEM evolution, the item of IF_HOLD_ITEM, ...).</summary>
      public static void RepairEvolutionDisplay(IDataModel model) {
         try {
            var noChange = new NoDataChangeDeltaModel();
            var address = model.GetAddressFromAnchor(noChange, -1, StatsAnchor);
            if (address == Pointer.NULL || !(model.GetNextRun(address) is ArrayRun run) || run.Start != address) return;
            var format = run.FormatString;
            if (!format.Contains("evolutions<[method:evolutionmethods ")) return; // the expansion's evolutions only
            var changed = false;
            foreach (var (wrong, right) in EvolutionFixes) {
               var at = format.IndexOf(wrong, StringComparison.Ordinal);
               if (at < 0 || format.IndexOf(wrong, at + 1, StringComparison.Ordinal) >= 0) continue;
               format = format.Substring(0, at) + right + format.Substring(at + wrong.Length);
               changed = true;
            }
            if (changed) PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + StatsAnchor + format, allowAnchorOverwrite: true);
         } catch (Exception) {
            // an odd ROM must load whatever this finds
         }
      }

      #region The additional effects of a move (chance of a burn, a flinch, a stat change...)

      public const string AdditionalEffectsList = "additionalmoveeffects";
      public const string PlainAdditionalEffects = "additionalEffects<>";
      /// <summary>
      /// pokeemerald-expansion's struct AdditionalEffect (12 bytes): the effect, flags (self...), three argument bytes (the three effects of Tri Attack, the percent of Absorb,
      /// the message of Wrap), the chance (0 = always), and the stat stages for STAT_PLUS / STAT_MINUS (the attack and defense bits share a byte, the other five stats a word).
      /// The number of effects is two bits of the move's flags, so the stream takes its length from that bit field.
      /// </summary>
      public const string AdditionalEffectsFormat = "additionalEffects<[effect.additionalmoveeffects flags.|t|self.|onlyIfTargetRaisedStats.|onChargeTurnOnly.|sheerForceOverride.|preAttackEffect.|onSide.|pledgeCombo.|unused. argumentA. argumentB. argumentC. chance. stats. attackDefense.|t|attack:.|defense:.|unused: stages::|t|spAtk:.|spDef:.|speed:.|accuracy:.|evasion:.|unused::::::::.]/numAdditionalEffects>";

      /// <summary>The names of pokeemerald-expansion's enum MoveEffect, in order (checked against every move of CUBE).</summary>
      public static readonly string[] AdditionalEffectNames = {
         "NONE",
         "SLEEP",
         "POISON",
         "BURN",
         "FREEZE",
         "PARALYSIS",
         "TOXIC",
         "FROSTBITE",
         "CONFUSION",
         "FLINCH",
         "ABSORB",
         "RANDOM_FROM_LIST",
         "UPROAR",
         "PAYDAY",
         "WRAP",
         "STAT_PLUS",
         "STAT_MINUS",
         "REMOVE_ARG_TYPE",
         "RECHARGE",
         "RAGE",
         "PREVENT_ESCAPE",
         "NIGHTMARE",
         "GLAIVE_RUSH",
         "REMOVE_STATUS",
         "THRASH",
         "CLEAR_SMOG",
         "FLAME_BURST",
         "FEINT",
         "HAPPY_HOUR",
         "CORE_ENFORCER",
         "THROAT_CHOP",
         "INCINERATE",
         "BUG_BITE",
         "RECOIL_HP_25",
         "TRAP_BOTH",
         "ROUND",
         "SYRUP_BOMB",
         "FLORAL_HEALING",
         "SECRET_POWER",
         "PSYCHIC_NOISE",
         "TERA_BLAST",
         "ORDER_UP",
         "ION_DELUGE",
         "HAZE",
         "LEECH_SEED",
         "REFLECT",
         "LIGHT_SCREEN",
         "SALT_CURE",
         "EERIE_SPELL",
         "FLING",
         "RAINBOW",
         "SEA_OF_FIRE",
         "SWAMP",
         "SUN",
         "RAIN",
         "SANDSTORM",
         "HAIL",
         "MISTY_TERRAIN",
         "GRASSY_TERRAIN",
         "ELECTRIC_TERRAIN",
         "PSYCHIC_TERRAIN",
         "VINE_LASH",
         "WILDFIRE",
         "CANNONADE",
         "EFFECT_SPORE_SIDE",
         "PARALYZE_SIDE",
         "CONFUSE_PAY_DAY_SIDE",
         "CRIT_PLUS_SIDE",
         "PREVENT_ESCAPE_SIDE",
         "AURORA_VEIL",
         "INFATUATE_SIDE",
         "RECYCLE_BERRIES",
         "POISON_SIDE",
         "DEFOG",
         "POISON_PARALYZE_SIDE",
         "HEAL_TEAM",
         "SPITE",
         "GRAVITY",
         "VOLCALITH",
         "SANDBLAST_SIDE",
         "YAWN_FOE",
         "AROMATHERAPY",
         "CONFUSE_SIDE",
         "STEELSURGE",
         "STEALTH_ROCK",
         "TORMENT_SIDE",
         "FIRE_SPIN_SIDE",
         "FIXED_POWER",
         "STAT_CHANGE_PLUS",
         "STAT_CHANGE_MINUS",
         "BREAK_SCREEN",
         "STEAL_STATS",
         "BEAT_UP_MESSAGE",
         "ITEM_MESSAGE",
         "SECRET_POWER_ATK_MINUS_1",
         "SECRET_POWER_DEF_MINUS_1",
         "SECRET_POWER_SPD_MINUS_1",
         "SECRET_POWER_SP_ATK_MINUS_1",
         "SECRET_POWER_ACC_MINUS_1"
      };

      /// <summary>An older .toml (and CUBE's own) knows the pointer to the additional effects but not what it points to: give it the format, so the effects can be edited.</summary>
      public static void RepairAdditionalEffects(IDataModel model) {
         try {
            var noChange = new NoDataChangeDeltaModel();
            var address = model.GetAddressFromAnchor(noChange, -1, MovesAnchor);
            if (address == Pointer.NULL || !(model.GetNextRun(address) is ArrayRun run) || run.Start != address) return;
            var format = run.FormatString;
            var at = format.IndexOf(" " + PlainAdditionalEffects + " ", StringComparison.Ordinal);
            if (at < 0 || !format.Contains("numAdditionalEffects:.")) return;
            AddIfMissing(model, AdditionalEffectsList, AdditionalEffectNames);
            var fixedFormat = format.Substring(0, at + 1) + AdditionalEffectsFormat + format.Substring(at + 1 + PlainAdditionalEffects.Length);
            PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + MovesAnchor + fixedFormat, allowAnchorOverwrite: true);
         } catch (Exception) {
            // an odd ROM must load whatever this finds
         }
      }

      #endregion

      /// <summary>Called when a ROM is loaded (after its metadata). Never throws: an odd ROM must load whatever this finds.</summary>
      public static void Repair(IDataModel model) {
         RepairAdditionalEffects(model);
         RepairTypeChart(model);
         RepairTrainerBattleType(model);
         RepairEvolutionDisplay(model);
         RepairTrainerBackPics(model);
         try { TmMoveTables.NameCubeArrays(model); } catch (Exception) { /* an odd ROM must load whatever this finds */ }
         try {
            var noChange = new NoDataChangeDeltaModel();
            var moves = model.GetAddressFromAnchor(noChange, -1, MovesAnchor);
            if (moves == Pointer.NULL || !(model.GetNextRun(moves) is ArrayRun movesRun) || movesRun.Start != moves) return;
            // only the expansion's move table has these fields: the format says whether this .toml is from before the fix
            if (!movesRun.FormatString.Contains("priority::|") && !movesRun.FormatString.Contains("contestCategory. ")) return;

            ReplaceIfOld(model, "damagecategory", OldDamageCategory, NewDamageCategory);
            ReplaceIfOld(model, "contesteffectnames", OldContestEffects, NewContestEffects);
            ReplaceIfOld(model, "contestcombostarter", OldComboStarters, NewComboStarters);
            AddIfMissing(model, "contestcategories", NewContestCategories);
            AddIfMissing(model, "priorities", NewPriorities);

            foreach (var (anchor, wrong, right, lists) in FormatFixes) {
               if (lists.Any(list => !model.TryGetList(list, out _))) continue; // the list the new format names has to be there
               var address = model.GetAddressFromAnchor(noChange, -1, anchor);
               if (address == Pointer.NULL || !(model.GetNextRun(address) is ArrayRun run) || run.Start != address) continue;
               var format = run.FormatString;
               var at = format.IndexOf(wrong, StringComparison.Ordinal);
               if (at < 0 || format.IndexOf(wrong, at + 1, StringComparison.Ordinal) >= 0) continue; // not there, or not unique: leave it
               var fixedFormat = format.Substring(0, at) + right + format.Substring(at + wrong.Length);
               PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + anchor + fixedFormat, allowAnchorOverwrite: true);
            }
         } catch (Exception) {
            // see above
         }
      }

      private static void ReplaceIfOld(IDataModel model, string name, string[] old, string[] replacement) {
         if (!model.TryGetList(name, out var list) || !list.SequenceEqual(old)) return;
         model.SetList(new NoDataChangeDeltaModel(), name, replacement, list.Comments ?? new Dictionary<int, string>(), null);
      }

      private static void AddIfMissing(IDataModel model, string name, string[] content) {
         if (model.TryGetList(name, out _)) return;
         model.SetList(new NoDataChangeDeltaModel(), name, content, new Dictionary<int, string>(), null);
      }
   }
}
