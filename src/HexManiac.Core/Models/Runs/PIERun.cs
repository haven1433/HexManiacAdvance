using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   /// <summary>
   /// Represents a pokemon item effect.
   /// See https://www.pokecommunity.com/showthread.php?p=6745155#post6745155
   /// </summary>
   public class PIERun : BaseRun, IStreamRun, IAppendToBuilderRun {
      public const string SharedFormatString = "`pie`";
      private readonly IDataModel model;

      private bool hasArgByte;
      private bool hasLowHappinessByte, hasMidHappinessByte, hasHighHappinessByte;

      #region Properties

      // --- byte 0 ---
      public bool HealInfatuation {
         get => GetBit(Start, 7);
         set => SetBit(Start, 7, value);
      }

      public bool ApplyToFirstPokemonOnly {
         get => GetBit(Start, 6);
         set => SetBit(Start, 6, value);
      }

      public sbyte DireHit {
         get {
            var high = GetBit(Start, 5);
            var low = GetBit(Start, 4);
            return (sbyte)((high ? 2 : 0) + (low ? 1 : 0));
         }
         set {
            var high = value > 1;
            var low = value % 2 == 1;
            SetBit(Start, 5, high);
            SetBit(Start, 4, low);
         }
      }

      public int AttackStatIncrease {
         get => GetLow(Start);
         set => SetLow(Start, value);
      }

      // --- byte 1 ---
      public int SpeedStatIncrease {
         get => GetLow(Start + 1);
         set => SetLow(Start + 1, value);
      }

      public int DefenseStatIncrease {
         get => GetHigh(Start + 1);
         set => SetHigh(Start + 1, value);
      }

      // --- byte 2 ---
      public int SpecialAttackStatIncrease {
         get => GetLow(Start + 2);
         set => SetLow(Start + 2, value);
      }

      public int AccuracyStatIncrease {
         get => GetHigh(Start + 2);
         set => SetHigh(Start + 2, value);
      }

      // --- byte 3 ---
      public bool GuardSpec {
         get => GetBit(Start + 3, 7);
         set => SetBit(Start + 3, 7, value);
      }
      public bool LevelUp {
         get => GetBit(Start + 3, 6);
         set => SetBit(Start + 3, 6, value);
      }
      public bool ClearSleep {
         get => GetBit(Start + 3, 5);
         set => SetBit(Start + 3, 5, value);
      }
      public bool ClearPoison {
         get => GetBit(Start + 3, 4);
         set => SetBit(Start + 3, 4, value);
      }
      public bool ClearBurn {
         get => GetBit(Start + 3, 3);
         set => SetBit(Start + 3, 3, value);
      }
      public bool ClearIce {
         get => GetBit(Start + 3, 2);
         set => SetBit(Start + 3, 2, value);
      }
      public bool ClearParalyze {
         get => GetBit(Start + 3, 1);
         set => SetBit(Start + 3, 1, value);
      }
      public bool ClearConfusion {
         get => GetBit(Start + 3, 0);
         set => SetBit(Start + 3, 0, value);
      }

      // --- byte 4 ---
      public bool IncreaseHpEv {
         get => GetBit(Start + 4, 0);
         set => SetBit(Start + 4, 0, value);
      }
      public bool IncreaseAttackEv {
         get => GetBit(Start + 4, 1);
         set => SetBit(Start + 4, 1, value);
      }
      public bool HealHealth {
         get => GetBit(Start + 4, 2);
         set => SetBit(Start + 4, 2, value);
      }
      public bool HealPowerPoints {
         get => GetBit(Start + 4, 3);
         set => SetBit(Start + 4, 3, value);
      }
      public bool RequireAttackSelection {
         get => GetBit(Start + 4, 4);
         set => SetBit(Start + 4, 4, value);
      }
      public bool IncreaseMaxPowerPoints { // does this use Arg?
         get => GetBit(Start + 4, 5);
         set => SetBit(Start + 4, 5, value);
      }
      public bool ReviveAndHeal {
         get => GetBit(Start + 4, 6);
         set => SetBit(Start + 4, 6, value);
      }
      public bool EvolutionStone {
         get => GetBit(Start + 4, 7);
         set => SetBit(Start + 4, 7, value);
      }

      // --- byte 5 ---
      public bool ChangeHappinessWhenGreaterThan200 {
         get => GetBit(Start + 5, 7);
         set => SetBit(Start + 5, 7, value);
      }
      public bool ChangeHappinessWhenBetween100And199{
         get => GetBit(Start + 5, 6);
         set => SetBit(Start + 5, 6, value);
      }
      public bool ChangeHappinessWhenLessThan100 {
         get => GetBit(Start + 5, 5);
         set => SetBit(Start + 5, 5, value);
      }
      public bool IncreasePowerPointsToMax {
         get => GetBit(Start + 5, 4);
         set => SetBit(Start + 5, 4, value);
      }
      public bool IncreaseSpecialAttackEv {
         get => GetBit(Start + 5, 3);
         set => SetBit(Start + 5, 3, value);
      }
      public bool IncreaseSpecialDefenseEv {
         get => GetBit(Start + 5, 2);
         set => SetBit(Start + 5, 2, value);
      }
      public bool IncreaseSpeedEv {
         get => GetBit(Start + 5, 1);
         set => SetBit(Start + 5, 1, value);
      }
      public bool IncreaseDefenseEv {
         get => GetBit(Start + 5, 0);
         set => SetBit(Start + 5, 0, value);
      }

      // --- byte 6 ---
      public bool HasArg => hasArgByte;
      public const sbyte
         HealthRestore_Max = -1,
         HealthRestore_Half = -2,
         HealthRestore_FromLevelUp = -3;
      public short Arg {
         get {
            short value = (sbyte)model[Start + 6];
            if (value < -3) value += 0x100;
            return value;
         }
         set => editScope.ChangeToken.ChangeData(model, Start + 6, (byte)value);
      }

      // --- byte 6/7 ---
      public bool HasLowHappinessByte => hasLowHappinessByte;
      public sbyte LowHappinessChange {
         get => (sbyte)model[Start + 6 + (HasArg ? 1 : 0)];
         set => editScope.ChangeToken.ChangeData(model, Start + 6 + (HasArg ? 1 : 0), (byte)value);
      }
      // --- byte 6/7/8 ---
      public bool HasMidHappinessByte => hasMidHappinessByte;
      public sbyte MidHappinessChange {
         get => (sbyte)model[Start + 6 + (HasArg ? 1 : 0) + (HasLowHappinessByte ? 1 : 0)];
         set => editScope.ChangeToken.ChangeData(model, Start + 6 + (HasArg ? 1 : 0) + (HasLowHappinessByte ? 1 : 0), (byte)value);
      }
      // --- byte 8/9 ---
      public bool HasHighHappinessByte => hasHighHappinessByte;
      public sbyte HighHappinessChange {
         get => (sbyte)model[Start + 6 + (HasArg ? 1 : 0) + (HasLowHappinessByte ? 1 : 0) + (HasMidHappinessByte ? 1 : 0)];
         set => editScope.ChangeToken.ChangeData(model, Start + 6 + (HasArg ? 1 : 0) + (HasLowHappinessByte ? 1 : 0) + (HasMidHappinessByte ? 1 : 0), (byte)value);
      }

      #endregion

      #region Baseclass Stuff

      public override int Length => 6 + (hasArgByte ? 1 : 0) + (hasLowHappinessByte ? 1 : 0) + (hasMidHappinessByte ? 1 : 0) + (hasHighHappinessByte ? 1 : 0);

      public override string FormatString => SharedFormatString;

      public PIERun(IDataModel model, int start, SortedSpan<int> sources = null) : base(start, sources) {
         this.model = model;
         RefreshFlags();
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => new IntegerHex(index, 0, data[index], 1);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new PIERun(model, Start, newPointerSources);

      private void RefreshFlags() {
         hasLowHappinessByte = ChangeHappinessWhenLessThan100;
         hasMidHappinessByte = ChangeHappinessWhenBetween100And199;
         hasHighHappinessByte = ChangeHappinessWhenGreaterThan200;
         hasArgByte =
            IncreaseSpecialDefenseEv ||
            IncreaseDefenseEv ||
            IncreaseAttackEv ||
            IncreaseSpecialAttackEv ||
            IncreaseSpeedEv ||
            IncreaseHpEv ||
            HealHealth ||
            HealPowerPoints;
      }

      #endregion

      #region IStreamRun Stuff

      public string SerializeRun() {
         var result = new StringBuilder();
         result.AppendLine($"ApplyToFirstPokemonOnly = {ApplyToFirstPokemonOnly}");
         result.AppendLine($"RequireAttackSelection = {RequireAttackSelection}");
         result.AppendLine($"DireHit = {DireHit}");
         RenderFlagList(result, "General", new Dictionary<string, bool> {
            ["GuardSpec"] = GuardSpec,
            ["LevelUp"] = LevelUp,
            ["HealHealth"] = HealHealth,
            ["HealPowerPoints"] = HealPowerPoints,
            ["ReviveAndHeal"] = ReviveAndHeal,
            ["EvolutionStone"] = EvolutionStone,
         });
         RenderFlagList(result, "ClearStat", new Dictionary<string, bool> {
            ["Infatuation"] = HealInfatuation,
            ["Sleep"] = ClearSleep,
            ["Poison"] = ClearPoison,
            ["Burn"] = ClearBurn,
            ["Ice"] = ClearIce,
            ["Paralyze"] = ClearParalyze,
            ["Confusion"] = ClearConfusion,
         });
         RenderFlagList(result, "IncreaseStat", new Dictionary<string, bool> {
            ["HpEv"] = IncreaseHpEv,
            ["AttackEv"] = IncreaseAttackEv,
            ["DefenseEv"] = IncreaseDefenseEv,
            ["SpecialAttackEv"] = IncreaseSpecialAttackEv,
            ["SpecialDefenseEv"] = IncreaseSpecialDefenseEv,
            ["SpeedEv"] = IncreaseSpeedEv,
            ["MaxPowerPoints"] = IncreaseMaxPowerPoints,
            ["PowerPointsToMax"] = IncreasePowerPointsToMax,
         });
         RenderFlagList(result, "ChangeHappiness", new Dictionary<string, bool> {
            ["Low"] = ChangeHappinessWhenLessThan100,
            ["Mid"] = ChangeHappinessWhenBetween100And199,
            ["High"] = ChangeHappinessWhenGreaterThan200,
         });
         result.AppendLine();
         result.AppendLine($"AttackStatIncrease = {AttackStatIncrease}");
         result.AppendLine($"SpecialAttackStatIncrease = {SpecialAttackStatIncrease}");
         result.AppendLine($"DefenseStatIncrease = {DefenseStatIncrease}");
         result.AppendLine($"SpeedStatIncrease = {SpeedStatIncrease}");
         result.AppendLine($"AccuracyStatIncrease = {AccuracyStatIncrease}");

         if (HasArg) {
            result.AppendLine();
            var arg = Arg;
            var argText = arg.ToString();
            if (arg == HealthRestore_FromLevelUp) argText = "LevelUpHealth";
            if (arg == HealthRestore_Half) argText = "Half";
            if (arg == HealthRestore_Max) argText = "Max";
            result.AppendLine($"Arg = {argText}");
         }
         if (HasLowHappinessByte || HasMidHappinessByte || HasHighHappinessByte) result.AppendLine();
         if (HasLowHappinessByte) result.AppendLine($"LowHappinessChange = {LowHappinessChange}");
         if (HasMidHappinessByte) result.AppendLine($"MidHappinessChange = {MidHappinessChange}");
         if (HasHighHappinessByte) result.AppendLine($"HighHappinessChange = {HighHappinessChange}");

         return result.ToString();
      }

      private void RenderFlagList(StringBuilder builder, string setName, IDictionary<string, bool> flags) {
         builder.Append(setName + " = { ");
         var content = ", ".Join(flags.Keys.Where(key => flags[key]));
         if (!string.IsNullOrWhiteSpace(content)) content += " ";
         builder.Append(content);
         builder.AppendLine("}");
      }

      private void ReadFlagList(string list, IDictionary<string, Action<bool>> options) {
         var tokens = list.Split(new[] { '{', '}', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
         foreach (var key in options.Keys) {
            if (tokens.Contains(key.ToLower())) {
               options[key](true);
            } else {
               options[key](false);
            }
         }
      }

      private HashSet<int> changedAddresses;
      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         changedAddresses = new HashSet<int>();
         var pairs = content.Split(Environment.NewLine).Where(line => line.Count('=') == 1).Select(line => line.Split('=')).Select(pair => new KeyValuePair<string, string>(pair[0].Trim().ToLower(), pair[1].Trim().ToLower()));
         bool parseBool; sbyte parseInt;
         using (CreateEditScope(token)) {
            // check the bytes that everyone has
            foreach (var pair in pairs) {
               if (pair.Key == "applytofirstpokemononly" && bool.TryParse(pair.Value, out parseBool)) ApplyToFirstPokemonOnly = parseBool;
               if (pair.Key == "requireattackselection" && bool.TryParse(pair.Value, out parseBool)) RequireAttackSelection = parseBool;
               if (pair.Key == "direhit" && sbyte.TryParse(pair.Value, out parseInt)) DireHit = parseInt;
               if (pair.Key == "attackstatincrease" && sbyte.TryParse(pair.Value, out parseInt)) AttackStatIncrease = parseInt;
               if (pair.Key == "specialattackstatincrease" && sbyte.TryParse(pair.Value, out parseInt)) SpecialAttackStatIncrease = parseInt;
               if (pair.Key == "defensestatincrease" && sbyte.TryParse(pair.Value, out parseInt)) DefenseStatIncrease = parseInt;
               if (pair.Key == "speedstatincrease" && sbyte.TryParse(pair.Value, out parseInt)) SpeedStatIncrease = parseInt;
               if (pair.Key == "accuracystatincrease" && sbyte.TryParse(pair.Value, out parseInt)) AccuracyStatIncrease = parseInt;

               if (pair.Key == "general") ReadFlagList(pair.Value, new Dictionary<string, Action<bool>> {
                  ["GuardSpec"] = v => GuardSpec = v,
                  ["LevelUp"] = v => LevelUp = v,
                  ["HealHealth"] = v => HealHealth = v,
                  ["HealPowerPoints"] = v => HealPowerPoints = v,
                  ["ReviveAndHeal"] = v => ReviveAndHeal = v,
                  ["EvolutionStone"] = v => EvolutionStone = v,
               });

               if (pair.Key == "clearstat") ReadFlagList(pair.Value, new Dictionary<string, Action<bool>> {
                  ["Infatuation"] = v => HealInfatuation = v,
                  ["Sleep"] = v => ClearSleep = v,
                  ["Poison"] = v => ClearPoison = v,
                  ["Burn"] = v => ClearBurn = v,
                  ["Ice"] = v => ClearIce = v,
                  ["Paralyze"] = v => ClearParalyze = v,
                  ["Confusion"] = v => ClearConfusion = v,
               });

               if (pair.Key == "increasestat") ReadFlagList(pair.Value, new Dictionary<string, Action<bool>> {
                  ["HpEv"] = v => IncreaseHpEv = v,
                  ["AttackEv"] = v => IncreaseAttackEv = v,
                  ["DefenseEv"] = v => IncreaseDefenseEv = v,
                  ["SpecialAttackEv"] = v => IncreaseSpecialAttackEv = v,
                  ["SpecialDefenseEv"] = v => IncreaseSpecialDefenseEv = v,
                  ["SpeedEv"] = v => IncreaseSpeedEv = v,
                  ["MaxPowerPoints"] = v => IncreaseMaxPowerPoints = v,
                  ["PowerPointsToMax"] = v => IncreasePowerPointsToMax = v,
               });

               if (pair.Key == "changehappiness") ReadFlagList(pair.Value, new Dictionary<string, Action<bool>> {
                  ["Low"] = v => ChangeHappinessWhenLessThan100 = v,
                  ["Mid"] = v => ChangeHappinessWhenBetween100And199 = v,
                  ["High"] = v => ChangeHappinessWhenGreaterThan200 = v,
               });
            }

            Repoint(this);

            // check the bytes that we may have, now that we've possibly been repointed.
            foreach (var pair in pairs) {
               if (pair.Key == "arg" && editScope.Result.HasArg) {
                  var toParse = pair.Value;
                  if (toParse == "leveluphealth") editScope.Result.Arg = HealthRestore_FromLevelUp;
                  else if (toParse == "half") editScope.Result.Arg = HealthRestore_Half;
                  else if (toParse == "max") editScope.Result.Arg = HealthRestore_Max;
                  else if (sbyte.TryParse(pair.Value, out parseInt)) editScope.Result.Arg = parseInt;
               }

               if (pair.Key == "lowhappinesschange" && editScope.Result.HasLowHappinessByte && sbyte.TryParse(pair.Value, out parseInt)) editScope.Result.LowHappinessChange = parseInt;
               if (pair.Key == "midhappinesschange" && editScope.Result.HasMidHappinessByte && sbyte.TryParse(pair.Value, out parseInt)) editScope.Result.MidHappinessChange = parseInt;
               if (pair.Key == "highhappinesschange" && editScope.Result.HasHighHappinessByte && sbyte.TryParse(pair.Value, out parseInt)) editScope.Result.HighHappinessChange = parseInt;
            }

            changedOffsets = new List<int>(changedAddresses);
            changedAddresses = null;
            var result = editScope.Result;
            editScope = null;
            return result;
         }
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var result = new List<AutocompleteItem>();
         var parts = line.Split("=");
         if (parts.Length != 2) return result;
         var header = parts[0].Trim().ToLower();
         var partial = parts[1].Replace('{', ' ').Replace('}', ' ').Trim();
         caretCharacterIndex -= parts[0].Length + 1;
         string beforeText = string.Empty, afterText = string.Empty;
         if (caretCharacterIndex >= 0) {
            beforeText = parts[1].Substring(0, caretCharacterIndex);
            afterText = parts[1].Substring(caretCharacterIndex);
         }

         TryAdd(result, header, nameof(ApplyToFirstPokemonOnly), partial, "false", "true");
         TryAdd(result, header, nameof(Arg), partial, "LevelUpHealth", "Half", "Max");
         TryAddMulti(result, header, "General", beforeText, afterText,
            "GuardSpec", "LevelUp", "HealHealth", "HealPowerPoints", "ReviveAndHeal", "EvolutionStone");
         TryAddMulti(result, header, "ClearStat", beforeText, afterText,
            "Infatuation", "Sleep", "Poison", "Burn", "Ice", "Paralyze", "Confusion");
         TryAddMulti(result, header, "IncreaseStat", beforeText, afterText,
            "HpEv", "AttackEv", "DefenseEv", "SpecialAttackEv", "SpecialDefenseEv", "SpeedEv", "MaxPowerPoints", "PowerPointsToMax");
         TryAddMulti(result, header, "ChangeHappiness", beforeText, afterText,
            "Low", "Mid", "High");
         return result;
      }

      private bool TryAdd(List<AutocompleteItem> result, string header, string propertyName, string partial, params string[] options) {
         if (header != propertyName.ToLower()) return false;
         result.AddRange(options
            .Where(option => option.MatchesPartial(partial))
            .Select(option => new AutocompleteItem(option, $"{propertyName} = {option}")));
         return true;
      }

      private bool TryAddMulti(List<AutocompleteItem> result, string header, string propertyName, string beforeText, string afterText, params string[] options) {
         if (beforeText.Length == 0 && afterText.Length == 0) return false;
         if (header != propertyName.ToLower()) return false;
         beforeText = beforeText.Replace('{', ' ').Replace('}', ' ').Replace(", ", " ");
         afterText = afterText.Replace('{', ' ').Replace('}', ' ').Replace(",", " ");
         var afterTokens = afterText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         afterText = ", ".Join(afterTokens);
         if (afterText.Length > 0) afterText = ", " + afterText;
         var editToken = beforeText.Split(' ').Last();
         var beforeTokens = beforeText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         if (editToken.Length > 0) beforeTokens = beforeTokens.Take(beforeTokens.Length - 1).ToArray();
         beforeText = ", ".Join(beforeTokens);
         if (beforeText.Length > 0) beforeText = " " + beforeText + ",";

         result.AddRange(options
            .Where(option => option.MatchesPartial(editToken))
            .Where(option => !beforeTokens.Contains(option))
            .Where(option => !afterTokens.Contains(option))
            .Select(option => new AutocompleteItem(option, $"{propertyName} = {{{beforeText} {option}{afterText} }}")));
         return true;
      }

      public IReadOnlyList<IPixelViewModel> Visualizations => new List<IPixelViewModel>();
      public bool DependsOn(string anchorName) => false;

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         for (int i = 0; i < length; i++) {
            var index = i + start;
            if (index < Start) continue;
            if (index >= Start + Length) break;
            builder.Append(model[index].ToString("X2") + " ");
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) {
            var index = i + start;
            if (index < Start) continue;
            if (index >= Start + Length) break;
            changeToken.ChangeData(model, index, 0xFF);
         }
      }

      #endregion

      #region Edit Stuff

      private class EditScope : IDisposable {
         public ModelDelta ChangeToken { get; private set; }
         public PIERun Result { get; set; }
         public EditScope(ModelDelta token, PIERun initialResult) => (ChangeToken, Result) = (token, initialResult);
         public void Dispose() {
            ChangeToken = null;
            Result.editScope = null;
         }
      }
      private EditScope editScope;

      public IDisposable CreateEditScope(ModelDelta token) {
         Debug.Assert(editScope?.ChangeToken == null, "No nesting edit scopes for item effect runs!");
         return editScope = new EditScope(token, this);
      }

      public PIERun Refresh(ModelDelta token) {
         using var scope = CreateEditScope(token);
         AcceptChange();
         return editScope.Result;
      }

      private bool GetBit(int address, int bit) => (model[address] & (1 << bit)) != 0;
      private void SetBit(int address, int bit, bool value) {
         var otherBits = model[address] & ~(1 << bit);
         var thisBit = value ? (1 << bit) : 0;
         if (editScope.ChangeToken.ChangeData(model, address, (byte)(otherBits | thisBit))) changedAddresses?.Add(address);
         AcceptChange();
      }

      private void AcceptChange() {
         var newRun = new PIERun(model, Start, PointerSources);
         if (
            newRun.hasLowHappinessByte != hasLowHappinessByte ||
            newRun.hasMidHappinessByte != hasMidHappinessByte ||
            newRun.hasHighHappinessByte != hasHighHappinessByte ||
            newRun.hasArgByte != hasArgByte
         ) {
            Repoint(newRun);
         }
      }

      private int GetHigh(int address) => (model[address] & 0xF0) >> 4;
      private void SetHigh(int address, int value) {
         var low = model[address] & 0xF;
         var high = value.LimitToRange(0, 15) << 4;
         editScope.ChangeToken.ChangeData(model, address, (byte)(high | low));
      }

      private int GetLow(int address) => model[address] & 0xF;
      private void SetLow(int address, int value) {
         var high = model[address] & 0xF0;
         var low = value.LimitToRange(0, 15);
         editScope.ChangeToken.ChangeData(model, address, (byte)(high | low));
      }

      private void Repoint(PIERun newFlags) {
         for (int i = 0; i < Length - newFlags.Length; i++) {
            editScope.ChangeToken.ChangeData(model, Start + newFlags.Length + i, 0xFF);
         }
         var newRun = model.RelocateForExpansion(editScope.ChangeToken, this, newFlags.Length);
         for (int i = 0; i < newFlags.Length - Length; i++) {
            editScope.ChangeToken.ChangeData(model, newRun.Start + Length + i, 0x00);
         }
         editScope.Result = new PIERun(model, newRun.Start, newRun.PointerSources) { editScope = editScope };
      }

      #endregion
   }

   public class PIERunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 6;

      public override bool Matches(IFormattedRun run) => run is PIERun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         owner.ObserveRunWritten(token, new PIERun(owner, destination, new SortedSpan<int>(source)));
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new PIERun(model, dataIndex);
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         run = new PIERun(model, run.Start, run.PointerSources);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         for (int i = 0; i < 6; i++) token.ChangeData(owner, destination + i, 0);
         return new PIERun(owner, destination, new SortedSpan<int>(source));
      }
   }
}
