using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

// example for making a bug trainer: templates.CreateTrainer(objectEvent, history.CurrentChange, 20 /* bug catcher */, 30, 9, 6 /*bug*/, true);


namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public interface IDataInvestigator {
      int FindNextUnusedFlag();
      int FindNextUnusedVariable();
   }

   public class EventTemplate : ViewModelCore, IDataInvestigator {
      private readonly Random rnd = new();
      private readonly IDataModel model;
      private readonly ScriptParser parser;
      private readonly Task initializationWorkload;
      private ISet<int> usedFlags, usedTrainerFlags, usedVariables;
      private IReadOnlyDictionary<int, TrainerPreference> trainerPreferences;
      private IReadOnlyDictionary<int, int> minLevel;

      private ISet<int> UsedFlags {
         get {
            initializationWorkload.Wait();
            return usedFlags;
         }
      }
      private ISet<int> UsedTrainerFlags {
         get {
            initializationWorkload.Wait();
            return usedTrainerFlags;
         }
      }
      private ISet<int> UsedVariables {
         get {
            initializationWorkload.Wait();
            return usedVariables;
         }
      }

      public void UseTrainerFlag(int flag) => UsedTrainerFlags.Add(flag);
      public bool IsTrainerFlagInUse(int flag) => UsedTrainerFlags.Contains(flag);

      private IReadOnlyDictionary<int, TrainerPreference> TrainerPreferences {
         get {
            if (trainerPreferences == null) trainerPreferences = Flags.GetTrainerPreference(model, parser);
            return trainerPreferences;
         }
      }

      private IReadOnlyDictionary<int, int> MinLevel {
         get {
            if (minLevel == null) minLevel = Flags.GetMinimumLevelForPokemon(model);
            return minLevel;
         }
      }

      public IPixelViewModel ObjectTemplateImage { get; private set; }

      public IReadOnlyList<IPixelViewModel> OverworldGraphics { get; private set; }

      public EventTemplate(IWorkDispatcher dispatcher, IDataModel model, ScriptParser parser, IReadOnlyList<IPixelViewModel> owGraphics) {
         (this.model, this.parser) = (model, parser);
         RefreshLists(owGraphics);
         if (model.IsFRLG()) UseNationalDex = true;

         HMObjectOptions.Add("Cut Tree");
         HMObjectOptions.Add("Smash Rock");
         HMObjectOptions.Add("Strength Boulder");
         TrainerOptions.Bind(nameof(TrainerOptions.SelectedIndex), (options, args) => {
            if (useExistingTrainer) UseSelectedTrainerSprite(); // the list is hidden otherwise: its selection doesn't matter
         });

         initializationWorkload = dispatcher.RunBackgroundWork(() => {
            try {
               // one pass over every script finds all three kinds of usage
               var usage = Flags.GetScriptUsage(model, parser);
               usedFlags = usage.ItemFlags;
               usedTrainerFlags = usage.TrainerFlags;
               usedVariables = usage.Variables;
            } catch (Exception ex) {
               // Malformed map/script data in a hack shouldn't make every event creation crash.
               // Without the scan, new flags/variables may collide with existing ones, but the editor keeps working.
               System.Diagnostics.Debug.WriteLine($"Could not scan scripts for used flags: {ex}");
               usedFlags ??= new HashSet<int>();
               usedTrainerFlags ??= new HashSet<int>();
               usedVariables ??= new HashSet<int>();
            }
         });
      }

      public void RefreshLists(IReadOnlyList<IPixelViewModel> owGraphics) {
         OverworldGraphics = owGraphics;
         AvailableTemplateTypes.Clear();
         AvailableTemplateTypes.Add(TemplateType.None);
         AvailableTemplateTypes.Add(TemplateType.Npc);
         AvailableTemplateTypes.Add(TemplateType.Item);
         AvailableTemplateTypes.Add(TemplateType.Trainer);
         AvailableTemplateTypes.Add(TemplateType.Mart);
         AvailableTemplateTypes.Add(TemplateType.Trade);
         if (model.IsFRLG() || model.IsEmerald()) AvailableTemplateTypes.Add(TemplateType.Tutor); // Ruby/Sapphire don't have tutors
         AvailableTemplateTypes.Add(TemplateType.Legendary);
         AvailableTemplateTypes.Add(TemplateType.HMObject);

         GraphicsOptions.Clear();
         for (int i = 0; i < owGraphics.Count; i++) GraphicsOptions.Add(VisualComboOption.CreateFromSprite(i.ToString(), owGraphics[i].PixelData, owGraphics[i].PixelWidth, i, 2, true));

         TypeOptions.Clear();
         var types = model.GetTableModel(HardcodeTablesModel.TypesTableName);
         if (types != null) {
            foreach (var type in types) {
               TypeOptions.Add(type.GetStringValue("name"));
            }
         }

         ItemOptions.Clear();
         var items = model.GetTableModel(HardcodeTablesModel.ItemsTableName);
         if (items != null) {
            foreach (var item in items) {
               ItemOptions.Add(item.GetStringValue("name"));
            }
         }

         var trainerOptions = new List<ComboOption>();
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var trainerClasses = model.GetTableModel(HardcodeTablesModel.TrainerClassNamesTable);
         if (trainers != null && trainerClasses != null) {
            var options = model.GetOptions(HardcodeTablesModel.TrainerClassNamesTable);
            for (int i = 0; i < trainers.Count; i++) {
               trainerOptions.Add(ObjectEventViewModel.CreateOption(options, i, trainers[i].TryGetValue("class", out var trainerClass) ? trainerClass : trainers[i].GetValue(1), trainers[i].GetStringValue("name")));
            }
         }
         TrainerOptions.Update(trainerOptions, TrainerOptions.SelectedIndex);

         UpdateObjectTemplateImage();
      }

      private TemplateType selectedTemplate;
      public TemplateType SelectedTemplate {
         get => selectedTemplate;
         set {
            SetEnum(ref selectedTemplate, value, UpdateObjectTemplateImage);
            if (selectedTemplate == TemplateType.HMObject) UpdateSpriteFromHMObject();
         }
      }

      public ObservableCollection<TemplateType> AvailableTemplateTypes { get; } = new();

      /// <summary>Hand out a flag nothing else uses (see Flags.NextFreeFlag).</summary>
      public int FindNextUnusedFlag() {
         var flag = Flags.NextFreeFlag(model, UsedFlags);
         UsedFlags.Add(flag);
         return flag;
      }

      public int FindNextUnusedVariable() {
         var variable = Flags.NextFreeVariable(model, UsedVariables);
         UsedVariables.Add(variable);
         return variable;
      }

      public void ApplyTemplate(ObjectEventViewModel objectEventModel, ModelDelta token) {
         if (selectedTemplate == TemplateType.Trainer) CreateTrainer(objectEventModel, token);
         if (selectedTemplate == TemplateType.Npc) CreateNPC(objectEventModel, token);
         if (selectedTemplate == TemplateType.Item) CreateItem(objectEventModel, token);
         if (selectedTemplate == TemplateType.Mart) CreateMart(objectEventModel, token);
         if (selectedTemplate == TemplateType.Tutor) CreateTutor(objectEventModel, token);
         if (selectedTemplate == TemplateType.Trade) CreateTrade(objectEventModel, token);
         if (selectedTemplate == TemplateType.Legendary) CreateLegendary(objectEventModel, token);
         if (selectedTemplate == TemplateType.HMObject) CreateHMObject(objectEventModel, token);
      }

      #region Trainer

      public ObservableCollection<VisualComboOption> GraphicsOptions { get; } = new();
      public ObservableCollection<string> TypeOptions { get; } = new();

      private bool useExistingTrainer;
      public bool UseExistingTrainer {
         get => useExistingTrainer;
         set => Set(ref useExistingTrainer, value, old => UseSelectedTrainerSprite());
      }

      public FilteringComboOptions TrainerOptions { get; } = new();

      private int trainerGraphics, maxPokedex = 25, maxLevel = 9, preferredType = 6;
      public int TrainerGraphics {
         get => trainerGraphics;
         set {
            Set(ref trainerGraphics, value, old => {
               UpdateTrainerSprite();
               UpdateObjectTemplateImage();
            });
         }
      }

      /// <summary>The index in the trainer table of the trainer that is selected in the "existing trainer" list, or -1.</summary>
      private int SelectedExistingTrainer {
         get {
            var index = TrainerOptions.SelectedIndex;
            var options = TrainerOptions.FilteredOptions; // while the list is filtered, SelectedIndex counts in the filtered list
            return index >= 0 && index < options.Count ? options[index].Index : -1;
         }
      }

      /// <summary>
      /// A new trainer gets the picture that goes with the overworld sprite (see TrainerPreferences).
      /// An existing trainer keeps its own picture, whatever overworld sprite is selected.
      /// </summary>
      private void UpdateTrainerSprite() {
         var picture = -1;
         if (useExistingTrainer && model.GetTableModel(HardcodeTablesModel.TrainerTableName) is ModelTable trainers) {
            var trainer = SelectedExistingTrainer;
            if (trainer >= 0 && trainer < trainers.Count) picture = trainers[trainer].GetValue("sprite");
         }
         if (picture < 0) {
            if (!TrainerPreferences.TryGetValue(trainerGraphics, out var pref)) pref = new(0, 0, 0);
            picture = pref.Sprite;
         }
         var spriteAddress = GetTrainerSpriteAddress(model, picture);
         var spriteRun = spriteAddress >= 0 && spriteAddress < model.Count ? model.GetNextRun(spriteAddress) as ISpriteRun : null;
         TrainerSprite = spriteRun == null ? null : ReadonlyPixelViewModel.Create(model, spriteRun, true);
         NotifyPropertyChanged(nameof(TrainerSprite));
      }

      /// <summary>
      /// When an existing trainer is selected, the event should look like that trainer:
      /// select the overworld sprite that goes with the trainer's picture. If there is none, leave the selection alone.
      /// </summary>
      private void UseSelectedTrainerSprite() {
         if (useExistingTrainer && model.GetTableModel(HardcodeTablesModel.TrainerTableName) is ModelTable trainers) {
            var trainer = SelectedExistingTrainer;
            if (trainer >= 0 && trainer < trainers.Count) {
               var overworld = FindOverworldSprite(trainers[trainer].GetValue("sprite"));
               if (overworld >= 0 && overworld < GraphicsOptions.Count) TrainerGraphics = overworld;
            }
         }
         UpdateTrainerSprite();
      }

      /// <summary>The overworld sprite for a trainer picture, or -1 if there isn't one.</summary>
      public int FindOverworldSprite(int trainerPicture) {
         var pictureNames = model.GetOptions(HardcodeTablesModel.TrainerSpritesName);
         if (!TrainerOverworldMatcher.HasNames(pictureNames)) pictureNames = model.GetOptions("data.trainers.sprites");
         var overworldNames = model.GetOptions(HardcodeTablesModel.OverworldSprites);
         if (TrainerOverworldMatcher.HasNames(pictureNames) && TrainerOverworldMatcher.HasNames(overworldNames)) {
            if (trainerPicture < 0 || trainerPicture >= pictureNames.Count) return -1;
            return TrainerOverworldMatcher.Find(pictureNames[trainerPicture], overworldNames);
         }

         // the lists have no names (not a decomp hack): the best we can do is the sprite that the map data pairs with this picture
         foreach (var key in TrainerPreferences.Keys) {
            if (TrainerPreferences[key].Sprite == trainerPicture) return key;
         }
         return -1;
      }
      public int MaxPokedex { get => maxPokedex; set => Set(ref maxPokedex, value); }
      public int MaxLevel { get => maxLevel; set => Set(ref maxLevel, value); }
      public int PreferredType { get => preferredType; set => Set(ref preferredType, value); }

      private bool useNationalDex;
      public bool UseNationalDex { get => useNationalDex; set => Set(ref useNationalDex, value); }

      public IPixelViewModel TrainerSprite { get; private set; }

      /// <summary>True when the trainer template has everything it needs: the pokedex tables (vanilla) or the species stats (expansion).</summary>
      public bool CanCreateTrainer(out string reason) {
         reason = null;
         if (UseExistingTrainer) return true;
         if (Flags.IsUnifiedTrainerBattle(model)) {
            if (model.GetTable(HardcodeTablesModel.PokemonStatsTable) == null) reason = $"Cannot create trainer without the species table {HardcodeTablesModel.PokemonStatsTable}.";
            return reason == null;
         }
         var dexName = UseNationalDex ? HardcodeTablesModel.NationalDexTableName : HardcodeTablesModel.RegionalDexTableName;
         if (model.GetTable(dexName) == null) reason = $"Cannot create trainer without pokedex table {dexName}.";
         return reason == null;
      }

      // TODO use all-caps name or mixed-caps name depending on other trainers in the table
      // TODO use reference file to get names and before/win/after text
      public void CreateTrainer(ObjectEventViewModel objectEventModel, ModelDelta token) {
         var expansion = Flags.IsUnifiedTrainerBattle(model);
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName, () => token);
         var trainerFlag = 1;
         if (UseExistingTrainer) {
            trainerFlag = TrainerOptions.SelectedIndex;
            UsedTrainerFlags.Add(trainerFlag);
         } else {
            // part 1: the team
            var teamSize = rnd.Next(3) + 1;
            var availablePokemon = expansion ? GetExpansionPokemonPool() : GetVanillaPokemonPool(token);

            // part 2: the trainer
            while (UsedTrainerFlags.Contains(trainerFlag)) trainerFlag++;
            if (trainerFlag >= trainers.Count) {
               throw new InvalidOperationException("Every trainer in the trainer table is already in use.");
            }
            usedTrainerFlags.Add(trainerFlag);

            var trainer = trainers[trainerFlag];
            if (!TrainerPreferences.TryGetValue(trainerGraphics, out var pref)) pref = new(0, 0, 0);
            if (expansion) {
               var teamStart = WriteExpansionTeam(token, teamSize, availablePokemon);
               WriteExpansionTrainer(token, trainer, trainerFlag, pref, teamSize, teamStart);
            } else {
               var teamStart = WriteVanillaTeam(token, teamSize, availablePokemon);
               // structType. class. introMusicAndGender. sprite. name""12 item1: item2: item3: item4: doubleBattle:: ai:: pokemonCount:: pokemon<>
               trainer.SetValue("structType", 0);
               trainer.SetStringValue("name", "NAME ME");
               trainer.SetValue("item1", 0);
               trainer.SetValue("item2", 0);
               trainer.SetValue("item3", 0);
               trainer.SetValue("item4", 0);
               trainer.SetValue("doubleBattle", 0);
               trainer.SetValue("ai", 0);
               trainer.SetValue("pokemonCount", teamSize);
               trainer.SetAddress("pokemon", teamStart);
               trainer.SetValue("class", pref.TrainerClass);
               trainer.SetValue("introMusicAndGender", pref.MusicAndGender);
               trainer.SetValue("sprite", pref.Sprite);
            }
         }

         // part 3: the script
         var before = WriteText(token, "Let's battle!");
         var win = WriteText(token, "You Win!");
         var after = WriteText(token, "Post-battle chat!");
         int scriptStart;
         if (expansion) {
            scriptStart = WriteExpansionTrainerScript(token, trainerFlag, before, win, after);
         } else {
            /*
                 trainerbattle 00 102 0 <before> <during>
                 loadpointer 0 <after>
                 callstd 6
                 end
             */
            //       2                  6       10          16
            // 5C 00 trainerFlag: 00 00 <before> <win> 0F 00 <after> 09 06 02
            scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 24);
            token.ChangeData(model, scriptStart, "5C 00 00 00 00 00 00 00 00 00 00 00 00 00 0F 00 00 00 00 00 09 06 02 00".ToByteArray());
            model.WriteMultiByteValue(scriptStart + 2, 2, token, trainerFlag);
            model.WritePointer(token, scriptStart + 6, before);
            model.WritePointer(token, scriptStart + 10, win);
            model.WritePointer(token, scriptStart + 16, after);
            model.ObserveRunWritten(token, new PointerRun(scriptStart + 6));
            model.ObserveRunWritten(token, new PointerRun(scriptStart + 10));
            model.ObserveRunWritten(token, new PointerRun(scriptStart + 16));
            var factory = new PCSRunContentStrategy();
            factory.TryAddFormatAtDestination(model, token, scriptStart + 6, before, default, default, default);
            factory.TryAddFormatAtDestination(model, token, scriptStart + 10, win, default, default, default);
            factory.TryAddFormatAtDestination(model, token, scriptStart + 16, after, default, default, default);
         }

         // part 4: the event
         objectEventModel.Graphics = trainerGraphics;
         objectEventModel.Elevation = FindPreferredTrainerElevation(model, trainerGraphics);
         objectEventModel.MoveType = new[] { 7, 8, 9, 10 }[rnd.Next(4)];
         objectEventModel.RangeX = objectEventModel.RangeY = 1;
         objectEventModel.TrainerType = 1;
         objectEventModel.TrainerRangeOrBerryID = 5;
         objectEventModel.ScriptAddress = scriptStart;
         objectEventModel.Flag = 0;
         objectEventModel.RefreshTrainerOptions();

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventModel.Start + 16)));
      }

      #region Vanilla trainer data

      private List<int> GetVanillaPokemonPool(ModelDelta token) {
         const int ChosenTypeOddsMultiplier = 100;
         var availablePokemon = new List<int>();
         var dexName = HardcodeTablesModel.RegionalDexTableName;
         if (useNationalDex) dexName = HardcodeTablesModel.NationalDexTableName;
         var pokedex = model.GetTableModel(dexName, () => token);
         var pokestats = model.GetTableModel(HardcodeTablesModel.PokemonStatsTable, () => token);
         for (int i = 1; i < pokedex.Count; i++) {
            if (pokedex[i - 1].GetValue(0) > maxPokedex) continue;
            if (MinLevel is { } minLevels && minLevels.TryGetValue(i, out var level) && level > maxLevel) continue;
            availablePokemon.Add(i);
            if (pokestats != null) {
               if (pokestats[i].GetValue("type1") == preferredType || pokestats[i].GetValue("type2") == preferredType) {
                  for (int j = 0; j < ChosenTypeOddsMultiplier; j++) availablePokemon.Add(i);
               }
            }
         }
         if (availablePokemon.Count == 0) availablePokemon.Add(1);
         return availablePokemon;
      }

      private int WriteVanillaTeam(ModelDelta token, int teamSize, IReadOnlyList<int> availablePokemon) {
         var teamStart = model.FindFreeSpace(model.FreeSpaceStart, 8 * teamSize);
         for (int i = 0; i < teamSize; i++) {
            // ivSpread: level: mon: padding:
            var pokemon = availablePokemon[rnd.Next(availablePokemon.Count)];
            var level = maxLevel;
            while (level > maxLevel - 5 && rnd.Next(2) == 1) level--;
            model.WriteMultiByteValue(teamStart + i * 8 + 0, 2, token, 0);
            model.WriteMultiByteValue(teamStart + i * 8 + 2, 2, token, level);
            model.WriteMultiByteValue(teamStart + i * 8 + 4, 2, token, pokemon);
            model.WriteMultiByteValue(teamStart + i * 8 + 6, 2, token, 0);
         }
         return teamStart;
      }

      #endregion

      #region Expansion trainer data

      // the expansion's species ids start out following the national dex, but the dex can be reordered: the species table's 'dexNumber' is the real national number
      private List<int> GetExpansionPokemonPool() {
         const int ChosenTypeOddsMultiplier = 100;
         var stats = model.GetTableModel(HardcodeTablesModel.PokemonStatsTable);
         var available = new List<int>();
         if (stats == null) return new List<int> { 1 };

         // which species may appear: the first maxPokedex of the regional (hoenn) dex or of the national dex
         IEnumerable<int> candidates;
         var hoenn = ExpansionDex.ReadRegional(model);
         var byNational = ExpansionDex.SpeciesByNational(model);
         if (!useNationalDex && hoenn.Length > 0) {
            candidates = hoenn.Take(maxPokedex).Select(national => ExpansionDex.SpeciesForNational(byNational, national));
         } else if (byNational.Count > 0) {
            candidates = Enumerable.Range(1, Math.Max(0, Math.Min(maxPokedex, byNational.Keys.Max()))).Select(national => ExpansionDex.SpeciesForNational(byNational, national));
         } else {
            candidates = Enumerable.Range(1, Math.Min(maxPokedex, stats.Count - 1));
         }

         // there is no usable evolution table to ask for each species' minimum level, so the base stat total stands in for it:
         // a level 9 trainer can use ~345 total stats (starters, bugs, normals), a level 50 trainer ~550, and so on.
         var maxTotal = Math.Min(780, 300 + 5 * maxLevel);
         foreach (var species in candidates) {
            if (species <= 0 || species >= stats.Count) continue;
            var entry = stats[species];
            var total = 0;
            foreach (var stat in new[] { "hp", "attack", "def", "speed", "spatk", "spdef" }) if (entry.TryGetValue(stat, out var value)) total += value;
            if (total > maxTotal) continue;
            available.Add(species);
            if (entry.GetValue("type1") == preferredType || entry.GetValue("type2") == preferredType) {
               for (int j = 0; j < ChosenTypeOddsMultiplier; j++) available.Add(species);
            }
         }
         if (available.Count == 0) available.Add(1);
         return available;
      }

      // a freshly added pokemon, like the team editor makes: default ball, any gender, default dynamax level
      private const int Mon_Species = 20, Mon_Level = 26, Mon_Ball = 27, Mon_NatureGenderShiny = 29, Mon_DynamaxLevel = 31;
      private const byte Mon_DefaultBall = 28, Mon_AnyGender = 3 << 5, Mon_DefaultDynamaxLevel = 10;

      private int WriteExpansionTeam(ModelDelta token, int teamSize, IReadOnlyList<int> availablePokemon) {
         const int Size = ExpansionTrainerTeamRun.ElementSize;
         var teamStart = model.FindFreeSpace(model.FreeSpaceStart, Size * teamSize);
         var team = new byte[Size * teamSize];
         for (int i = 0; i < teamSize; i++) {
            var pokemon = availablePokemon[rnd.Next(availablePokemon.Count)];
            var level = maxLevel;
            while (level > maxLevel - 5 && level > 1 && rnd.Next(2) == 1) level--;
            var offset = i * Size;
            team[offset + Mon_Species] = (byte)pokemon; team[offset + Mon_Species + 1] = (byte)(pokemon >> 8);
            team[offset + Mon_Level] = (byte)level;
            team[offset + Mon_Ball] = Mon_DefaultBall;
            team[offset + Mon_NatureGenderShiny] = Mon_AnyGender;
            team[offset + Mon_DynamaxLevel] = Mon_DefaultDynamaxLevel;
         }
         token.ChangeData(model, teamStart, team);
         return teamStart;
      }

      private void WriteExpansionTrainer(ModelDelta token, ModelArrayElement trainer, int trainerIndex, TrainerPreference pref, int teamSize, int teamStart) {
         // start from a clean record: ai flags, items, starting status, mugshot, ... are all 0
         ClearPointerFields(trainer); // (the old team must stop counting this record as a pointer to it)
         for (int i = 0; i < trainer.Length; i++) token.ChangeData(model, trainer.Start + i, 0);
         trainer.SetStringValue("name", "Name Me");
         trainer.SetValue("class", pref.TrainerClass);
         trainer.SetValue("introMusicAndGender", pref.MusicAndGender);
         trainer.SetValue("sprite", pref.Sprite);
         trainer.SetValue("pokemonCount", teamSize);
         trainer.SetAddress("pokemon", teamStart);

         // the same trainer exists once per difficulty (easy/normal/hard); keep them identical so the new trainer works at any difficulty
         foreach (var other in new[] { "data.trainers.stats.easy", "data.trainers.stats.hard" }) {
            var table = model.GetTable(other);
            if (table == null || table.ElementLength != trainer.Length || trainerIndex >= table.ElementCount) continue;
            var start = table.Start + table.ElementLength * trainerIndex;
            ClearPointerFields(new ModelArrayElement(model, table.Start, trainerIndex, () => token, table));
            for (int i = 0; i < trainer.Length; i++) token.ChangeData(model, start + i, model[trainer.Start + i]);
            if (trainer.Table.ElementContent.Any(segment => segment.Name == "pokemon" && segment.Type == ElementContentType.Pointer)) {
               var otherElement = new ModelArrayElement(model, table.Start, trainerIndex, () => token, table);
               otherElement.SetAddress("pokemon", teamStart);
            }
         }
      }

      /// <summary>
      /// Overwriting a record byte by byte leaves the model thinking the old destinations of its pointers are still pointed to by it.
      /// Pointing the fields at nothing first lets the model forget them.
      /// </summary>
      private void ClearPointerFields(ModelArrayElement element) {
         foreach (var segment in element.Table.ElementContent) {
            if (segment.Type == ElementContentType.Pointer) element.SetAddress(segment.Name, Pointer.NULL, false);
         }
      }

      /// <summary>
      /// trainerbattle (unified, 41 bytes) followed by the post-battle message:
      /// 5C flags localIdA trainerA: introA loseA scriptA localIdB trainerB: introB loseB scriptB victory cannotBattle rivalFlags
      /// 0F 00 &lt;after&gt; 09 06 02   (msgbox after, MSGBOX_AUTOCLOSE; end)
      /// </summary>
      private int WriteExpansionTrainerScript(ModelDelta token, int trainerIndex, int before, int win, int after) {
         const byte Flags_PlayMusicA = 1 << 2, Flags_FacePlayer = 1 << 5;
         var script = new byte[ExpansionTrainerBattleLength + 9];
         script[0] = 0x5C;
         script[1] = Flags_PlayMusicA | Flags_FacePlayer;
         script[3] = (byte)trainerIndex; script[4] = (byte)(trainerIndex >> 8);
         script[ExpansionTrainerBattleLength + 0] = 0x0F;
         script[ExpansionTrainerBattleLength + 6] = 0x09;
         script[ExpansionTrainerBattleLength + 7] = 0x06;
         script[ExpansionTrainerBattleLength + 8] = 0x02;
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, script.Length + 3);
         token.ChangeData(model, scriptStart, script);
         var pointers = new[] { (scriptStart + 5, before), (scriptStart + 9, win), (scriptStart + ExpansionTrainerBattleLength + 2, after) };
         var factory = new PCSRunContentStrategy();
         foreach (var (source, destination) in pointers) {
            model.WritePointer(token, source, destination);
            model.ObserveRunWritten(token, new PointerRun(source));
            factory.TryAddFormatAtDestination(model, token, source, destination, default, default, default);
         }
         return scriptStart;
      }

      #endregion

      private int FindPreferredTrainerElevation(IDataModel model, int graphics) {
         var histogram = new Dictionary<int, int>();
         var banks = AllMapsModel.Create(model, null);
         if (banks == null) return 3;
         foreach (var bank in banks) {
            foreach (var map in bank) {
               if (map == null) continue;
               foreach (var obj in map.Events.Objects) {
                  if (obj.Graphics != graphics) continue;
                  if (!histogram.ContainsKey(obj.Elevation)) histogram[obj.Elevation] = 0;
                  histogram[obj.Elevation]++;
               }
            }
         }
         return histogram.MostCommonKey();
      }

      public TrainerEventContent GetTrainerContent(IEventViewModel eventModel) => GetTrainerContent(model, eventModel);
      public static TrainerEventContent GetTrainerContent(IDataModel model, IEventViewModel eventModel) {
         if (eventModel is not ObjectEventViewModel objectModel) return null;
         var address = objectModel.ScriptAddress;
         if (address < 0) return null;
         var trainersTable = model.GetTableModel(HardcodeTablesModel.TrainerTableName, null);
         if (trainersTable == null) return null;
         var layout = TrainerLayout.For(trainersTable.Run);
         if (layout != TrainerLayout.Vanilla) {
            var content = GetExpansionTrainerContent(model, address, trainersTable, layout);
            if (content != null) return content;
            // rivals and gym leaders: the battle is somewhere inside a longer script
            var battle = FindTrainerBattleInScript(model, objectModel.Parser, objectModel);
            return battle == Pointer.NULL ? null : GetExpansionTrainerContent(model, battle, trainersTable, layout);
         }
         // 5C 00 trainerFlag: 00 00 <before> <win> 0F 00 <after> 09 06 02
         var expectedValues = new Dictionary<int, byte> {
            { 0, 0x5C },
            { 1, 0x00 },
            { 4, 0x00 },
            { 5, 0x00 },
            { 14, 0x0F },
            { 15, 0x00 },
            { 20, 0x09 },
            { 21, 0x06 },
            { 22, 0x02 },
         };
         if (address >= model.Count - expectedValues.Count) return null;
         foreach (var (k, v) in expectedValues) {
            if (model[address + k] != v) return null;
         }
         var trainerID = model.ReadMultiByteValue(address + 2, 2);
         if (trainerID < 0) return null;
         var trainers = trainersTable;
         if (trainerID >= trainers.Count) return null;

         var beforePointer = address + 6;
         var winPointer = address + 10;
         var afterPointer = address + 16;
         var trainerStart = trainers[trainerID].Start;

         return new TrainerEventContent(beforePointer, winPointer, afterPointer, trainerStart + layout.ClassOffset, trainerID, address + 2, trainerStart + layout.NameOffset, trainerStart + layout.TeamOffset,
            trainerStart, trainerStart + layout.SpriteOffset, layout.NameLength);
      }

      private const int ExpansionTrainerBattleLength = 41;
      private static readonly ConditionalWeakTable<IEventViewModel, int[]> trainerBattleCache = new();

      /// <summary>
      /// The address of the first trainerbattle command (with a real trainer) reachable from this event's script, or Pointer.NULL.
      /// Only meaningful for ROMs with the unified trainerbattle command. The answer is remembered per event as long as it stays valid.
      /// </summary>
      public static int FindTrainerBattleInScript(IDataModel model, ScriptParser parser, IEventViewModel eventModel) {
         if (eventModel is not ObjectEventViewModel objectModel || parser == null) return Pointer.NULL;
         var scriptAddress = objectModel.ScriptAddress;
         if (scriptAddress < 0 || scriptAddress + 1 >= model.Count) return Pointer.NULL;
         var trainersTable = model.GetTableModel(HardcodeTablesModel.TrainerTableName, null);
         if (trainersTable == null || TrainerLayout.For(trainersTable.Run) == TrainerLayout.Vanilla) return Pointer.NULL;
         if (trainerBattleCache.TryGetValue(eventModel, out var cached) && cached[0] == scriptAddress && (cached[1] == Pointer.NULL || (cached[1] >= 0 && model[cached[1]] == 0x5C))) return cached[1];
         var found = Pointer.NULL;
         foreach (var spot in Flags.GetAllScriptSpots(model, parser, new[] { scriptAddress }, 0x5C)) {
            if (spot.Address + ExpansionTrainerBattleLength > model.Count) continue;
            var trainerID = model.ReadMultiByteValue(spot.Address + 3, 2);
            if (trainerID <= 0 || trainerID >= trainersTable.Count) continue;
            found = spot.Address;
            break;
         }
         trainerBattleCache.Remove(eventModel);
         trainerBattleCache.Add(eventModel, new[] { scriptAddress, found });
         return found;
      }

      /// <summary>
      /// pokeemerald-expansion (1.17+) trainer scripts start with the unified 41-byte trainerbattle command:
      /// 5C flags localIdA trainerA: introA(pointer) loseA(pointer) scriptA(pointer) localIdB trainerB: introB(pointer) loseB(pointer) scriptB(pointer) victory(pointer) cannotBattle(pointer) rivalFlags
      /// The post-battle text is the first msgbox of scriptA (0F 00 text ...), when there is one.
      /// </summary>
      private static TrainerEventContent GetExpansionTrainerContent(IDataModel model, int address, ModelTable trainers, TrainerLayout layout) {
         const int CommandLength = ExpansionTrainerBattleLength;
         if (address < 0 || address + CommandLength > model.Count) return null;
         if (model[address] != 0x5C) return null;
         var trainerID = model.ReadMultiByteValue(address + 3, 2);
         if (trainerID <= 0 || trainerID >= trainers.Count) return null;
         var introPointer = address + 5;
         var losePointer = address + 9;
         // battles without an intro (gym leaders and the like) have a null pointer here: the intro editor simply stays hidden
         // the usual post-battle message is the msgbox right after the command (0F 00 text); gym leaders and the like have it in the script they point to
         var afterPointer = Pointer.NULL;
         var scriptA = model.ReadPointer(address + 13);
         var next = address + CommandLength;
         if (next + 6 <= model.Count && model[next] == 0x0F && model[next + 1] == 0x00 && GoodPointer(model, next + 2)) afterPointer = next + 2;
         else if (scriptA >= 0 && scriptA + 6 <= model.Count && model[scriptA] == 0x0F && model[scriptA + 1] == 0x00) afterPointer = scriptA + 2;
         var trainerStart = trainers[trainerID].Start;
         return new TrainerEventContent(introPointer, losePointer, afterPointer, trainerStart + layout.ClassOffset, trainerID, address + 3, trainerStart + layout.NameOffset, trainerStart + layout.TeamOffset,
            trainerStart, trainerStart + layout.SpriteOffset, layout.NameLength);
      }

      /// <summary>
      /// Resolve the address of a trainer's front sprite, for either the vanilla layout (graphics.trainers.sprites.front)
      /// or a decomp layout where the pic table holds {sprite, palette, ...} records (data.trainers.sprites/N/front/0/sprite/).
      /// </summary>
      public static int GetTrainerSpriteAddress(IDataModel model, int spriteIndex) {
         var spriteTable = model.GetTableModel(HardcodeTablesModel.TrainerSpritesName);
         if (spriteTable != null) {
            if (spriteIndex >= spriteTable.Count) return Pointer.NULL;
            return spriteTable[spriteIndex].GetAddress("sprite");
         }
         var noChange = new NoDataChangeDeltaModel();
         var picTable = model.GetTable("data.trainers.sprites");
         if (picTable == null || spriteIndex >= picTable.ElementCount) return Pointer.NULL;
         var address = model.GetAddressFromAnchor(noChange, -1, $"data.trainers.sprites/{spriteIndex}/front/0/sprite/");
         if (address < 0) address = model.GetAddressFromAnchor(noChange, -1, $"data.trainers.sprites/{spriteIndex}/sprite/");
         return address;
      }

      #endregion

      #region Rematch Trainer

      public static RematchTrainerEventContent GetRematchTrainerContent(IDataModel model, ScriptParser parser, ObjectEventViewModel eventModel) {
         if (eventModel.ScriptAddress < 0) return null;
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { eventModel.ScriptAddress }, 0x5C).ToList();
         var rematches = spots.Where(spot => ((int)model[spot.Address + 1]).IsAny(5, 7)).ToList();
         var trainers = spots.Select(spot => model.ReadMultiByteValue(spot.Address + 2, 2)).Distinct().ToList();
         if (rematches.Count != 1 || trainers.Count != 1) return null;
         var beforeTextStart = model.ReadPointer(rematches[0].Address + 6);
         var winTextStart = model.ReadPointer(rematches[0].Address + 10);

         var textSpot = Flags.GetAllScriptSpots(model, parser, new[] { eventModel.ScriptAddress }, 0x0F).FirstOrDefault();
         var afterTextStart = textSpot != null ? model.ReadPointer(textSpot.Address + 2) : Pointer.NULL;

         return new(trainers[0], beforeTextStart, winTextStart, afterTextStart);
      }

      #endregion

      #region NPC

      public void CreateNPC(ObjectEventViewModel eventModel, ModelDelta token) {
         // loadpointer 0 <text>; callstd 2; end; text="I'm an NPC!"
         // 0F 00 <text> 09 02 02 "I'm an NPC!"
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 24);
         token.ChangeData(model, scriptStart, "0F 00 00 00 00 00 09 02 02 C3 B4 E1 00 D5 E2 00 C8 CA BD AB FF".ToByteArray());
         model.WritePointer(token, scriptStart + 2, scriptStart + 9);
         model.ObserveRunWritten(token, new PointerRun(scriptStart + 2));
         var factory = new PCSRunContentStrategy();
         factory.TryAddFormatAtDestination(model, token, scriptStart + 2, scriptStart + 9, default, default, default);

         eventModel.Graphics = trainerGraphics;
         eventModel.Elevation = 3;
         eventModel.MoveType = 2;
         eventModel.RangeXY = "(3, 3)";
         eventModel.TrainerType = 0;
         eventModel.TrainerRangeOrBerryID = 0;
         eventModel.ScriptAddress = scriptStart;
         eventModel.Flag = 0;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(eventModel.Start + 16)));
      }

      public int GetNPCTextPointer(IEventViewModel eventModel) => GetNPCTextPointer(model, eventModel);
      public static int GetNPCTextPointer(IDataModel model, IEventViewModel eventModel) {
         if (eventModel is not ObjectEventViewModel objectModel) return Pointer.NULL;
         var address = objectModel.ScriptAddress;
         if (address < 0) return Pointer.NULL;
         var expectedValues = new Dictionary<int, byte> {
            { 0, 0x0F },
            { 1, 0x00 },
            { 6, 0x09 },
            { 7, 0x02 },
            { 8, 0x02 },
         };
         if (address >= model.Count - expectedValues.Count) return Pointer.NULL;
         foreach (var (k, v) in expectedValues) {
            if (model[address + k] != v) return Pointer.NULL;
         }
         return address + 2;
      }

      #endregion

      #region Item

      public ObservableCollection<string> ItemOptions { get; } = new();

      private int itemID = 20;
      public int ItemID { get => itemID; set => Set(ref itemID, value); }

      public void CreateItem(ObjectEventViewModel objectEventModel, ModelDelta token) {
         //   copyvarifnotzero 0x8000 item:
         //   copyvarifnotzero 0x8001 1
         //   callstd 1
         //   end
         //                     item:
         var script = "1A 00 80 00 00 1A 01 80 01 00 09 01 02".ToByteArray();
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, script.Length);
         token.ChangeData(model, scriptStart, script);
         model.WriteMultiByteValue(scriptStart + 3, 2, token, itemID);

         var itemFlag = FindNextUnusedFlag();

         objectEventModel.Graphics = ItemGraphics;
         objectEventModel.Elevation = 3;
         objectEventModel.MoveType = 8;
         objectEventModel.RangeX = objectEventModel.RangeY = 1;
         objectEventModel.TrainerType = objectEventModel.TrainerRangeOrBerryID = 0;
         objectEventModel.ScriptAddress = scriptStart;
         objectEventModel.Flag = itemFlag;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventModel.Start + 16)));
      }

      public int GetItemAddress(IEventViewModel eventModel) => GetItemAddress(model, eventModel);
      public static int GetItemAddress(IDataModel model, IEventViewModel eventModel) {
         if (eventModel is not ObjectEventViewModel objectModel) return Pointer.NULL;
         var address = objectModel.ScriptAddress;
         return GetItemAddress(model, address);
      }

      public static int GetItemAddress(IDataModel model, int address) {
         if (address < 0) return Pointer.NULL;
         // 1A 00 80 item: 1A 01 80 01 00 09 01 02
         var expectedValues = new Dictionary<int, byte> {
            { 0, 0x1A },
            { 1, 0x00 },
            { 2, 0x80 },
            { 5, 0x1A },
            { 6, 0x01 },
            { 7, 0x80 },
            { 8, 0x01 },
            { 9, 0x00 },
            { 10, 0x09 },
            { 11, 0x01 },
         };
         if (address >= model.Count - expectedValues.Count) return Pointer.NULL;
         foreach (var (k, v) in expectedValues) {
            if (model[address + k] != v) return Pointer.NULL;
         }
         return address + 3;
      }

      private int ItemGraphics => model.IsFRLG() ? 92 : 59;

      #endregion

      #region Signpost

      public void ApplyTemplate(SignpostEventViewModel signpost, ModelDelta token) {
         if (signpost == null) return;
         signpost.Elevation = 0;
         signpost.Kind = 0;

         // loadpointer 0 <text>; callstd 3; end; text="Signpost Text"
         // 0F 00 <text> 09 03 02 "Signpost Text"
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 24);
         token.ChangeData(model, scriptStart, "0F 00 00 00 00 00 09 03 02 CD DD DB E2 E4 E3 E7 E8 00 CE D9 EC E8 FF".ToByteArray());
         model.WritePointer(token, scriptStart + 2, scriptStart + 9);
         model.ObserveRunWritten(token, new PointerRun(scriptStart + 2));
         var factory = new PCSRunContentStrategy();
         factory.TryAddFormatAtDestination(model, token, scriptStart + 2, scriptStart + 9, default, default, default);

         // this XSE run has no pointer source, because the signpost Arg isn't always a pointer.
         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan<int>.None));

         signpost.Pointer = scriptStart;
      }

      public int GetSignpostTextPointer(IEventViewModel eventModel) => GetSignpostTextPointer(model, eventModel);
      public static int GetSignpostTextPointer(IDataModel model, IEventViewModel eventModel) {
         if (eventModel is not SignpostEventViewModel signpost) return Pointer.NULL;
         if (!signpost.ShowPointer) return Pointer.NULL;
         int address = signpost.Pointer;
         var expectedValues = new Dictionary<int, byte> {
            { 0, 0x0F },
            { 1, 0x00 },
            { 6, 0x09 },
            { 7, 0x03 },
            { 8, 0x02 },
         };
         foreach (var (k, v) in expectedValues) {
            if (address + k < 0 || address + k >= model.Count) return Pointer.NULL;
            if (model[address + k] != v) return Pointer.NULL;
         }
         return address + 2;
      }

      #endregion

      #region Mart

      private int ClerkGraphics => model.IsFRLG() ? 68 : 83;

      public void CreateMart(ObjectEventViewModel objectEventViewModel, ModelDelta token) {
         // FireRed template:
         // lock faceplayer preparemsg   waitmsg pokemart          loadpointer 0 msg   callstd 4   release end
         // 6A  5A  67  11 62 1A 08      66      86 08 A7 16 08    0F 00 90 51 1A 08   09 04       6C 02
         // 0x20 bytes total
         //                      <pointer>         <pointer>         <pointer>               pokeball/potion/antidote
         var script = "6A 5A 67 00 00 00 00 66 86 00 00 00 00 0F 00 00 00 00 00 09 04 6C 02 FF 04 00 0D 00 0E 00 00 00".ToByteArray();
         // 3 pointer to start text
         // 9 pointer to mart
         // 15 pointer to end text
         // 24 start of mart data

         var hello = WriteText(token, "Hi, there!\\nMay I help you?");
         var goodbye = WriteText(token, "Please come again!");

         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, script.Length);
         token.ChangeData(model, scriptStart, script);
         model.WritePointer(token, scriptStart + 3, hello);
         model.WritePointer(token, scriptStart + 9, scriptStart + 24);
         model.WritePointer(token, scriptStart + 15, goodbye);

         objectEventViewModel.Graphics = ClerkGraphics;
         objectEventViewModel.Elevation = 3;
         objectEventViewModel.MoveType = 10;
         objectEventViewModel.RangeX = objectEventViewModel.RangeY = 0;
         objectEventViewModel.TrainerType = objectEventViewModel.TrainerRangeOrBerryID = objectEventViewModel.Flag = 0;
         objectEventViewModel.ScriptAddress = scriptStart;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventViewModel.Start + 16)));
         parser.WriteMartStream(model, token, scriptStart + 24, scriptStart + 9);
         foreach (var offset in new[] { 3, 9, 15 }) model.ObserveRunWritten(token, new PointerRun(scriptStart + offset));
      }

      public MartEventContent GetMartContent(ObjectEventViewModel eventModel) => GetMartContent(model, parser, eventModel);
      public static MartEventContent GetMartContent(IDataModel model, ScriptParser parser, ObjectEventViewModel eventViewModel) {
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { eventViewModel.ScriptAddress }, 0x67, 0x86, 0x0F); // preparemsg, pokemart, loadpointer
         // look for the first preparemsg, then the first pokemart, then the first loadpointer
         var results = spots.GetEnumerator();
         if (!results.MoveNext()) return null;
         var messageStart = results.Current;
         if (model[messageStart.Address] != 0x67) return null;
         if (!results.MoveNext()) return null;
         var martStart = results.Current;
         if (model[martStart.Address] != 0x86) return null;
         if (!results.MoveNext()) return null;
         var loadStart = results.Current;
         if (model[loadStart.Address] != 0x0F) return null;
         var messageAddress = model.ReadPointer(messageStart.Address + 1);
         var martAddress = model.ReadPointer(martStart.Address + 1);
         var loadAddress = model.ReadPointer(loadStart.Address + 2);
         if (messageAddress < 0 || messageAddress >= model.Count) return null;
         if (martAddress < 0 || martAddress >= model.Count) return null;
         if (loadAddress < 0 || loadAddress >= model.Count) return null;
         return new(messageStart.Address + 1, martStart.Address + 1, loadStart.Address + 2);
      }

      #endregion

      #region Tutor

      public void CreateTutor(ObjectEventViewModel objectEventViewModel, ModelDelta token) {
         /* pseudo code:
          *    if flag: goto end
          *    print "forward text" -> yes/no
          *    if no:   goto failed
          *    print "only can learn once!" -> yes/no
          *    if no:   goto failed
          *    print "which pokemon will learn?"
          *    ChooseMonForMoveTutor
          *    if no:   goto failed
          *    setflag
          * end:
          *    print "done text"
          *    end
          * failed:
          *    print "failed text"
          *    end
          */

         var tutorFlag = FindNextUnusedFlag();

         // vanilla scripts name the tutor by its index in the tutor table, the expansion's by the move itself
         int tutor = 0;
         if (Flags.IsExpansion(model) && model.GetTableModel(HardcodeTablesModel.MoveTutors) is { Count: > 0 } tutorMoves) {
            tutor = model.ReadMultiByteValue(tutorMoves[0].Start, 2);
            if (tutor < 0 || tutor >= 0xFFFF) tutor = 0;
         }
         int move = GetTutorMove(model, tutor);
         int infoStart = WriteText(token, TutorInfoText);
         int warningStart = WriteText(token, "This move can be learned only\\nonce. Is that okay?");
         int whichStart = WriteText(token, "Which POKéMON wants to learn\\nthe move?");
         int doneStart = WriteText(token, "Enjoy the move!");
         int failedStart = WriteText(token, "I guess not.");
         var fr = model.IsFRLG();

         var script = $@"
   lock
   faceplayer
   checkflag {tutorFlag}
   if1 = <success>
   bufferattack 0 {move}
   loadpointer 0 <{infoStart:X6}>
   callstd 5
   compare 0x800D 0
   if1 = <failed>
   {(fr?"textcolor 3":string.Empty)}
   {(fr?"special DisableMsgBoxWalkaway":string.Empty)}
   {(fr?"signmsg":string.Empty)}
   loadpointer 0 <{warningStart:X6}>
   callstd 5
   {(fr?"normalmsg":string.Empty)}
   copyvar 0x8012 0x8013
   compare 0x800D 0
   if1 = <failed>
   loadpointer 0 <{whichStart:X6}>
   callstd 4
   setvar 0x8005 {tutor}
   special ChooseMonForMoveTutor
   waitstate
   lock
   faceplayer
   compare 0x800D 0
   if1 = <failed>
   setflag {tutorFlag}
success:
   loadpointer 0 <{doneStart:X6}>
   callstd 4
   release
   end
failed:
   loadpointer 0 <{failedStart:X6}>
   callstd 4
   release
   end
";
         // script length = 101 for Emerald and 108 for FireRed/LeafGreen (4 bytes less before it named the move): leave some room
         // note that the condition for recognizing the `warningStart` message is different in
         // the Emerald case, since there's no `signmsg` command to use for reference
         // instead, it's just 0 or 1 pointers, and has `callstd 5` after it.
         // maybe just expect a `callstd 5` after it, since it's the only one after infoStart that has that in both FR and Em

         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, TutorScriptSpace);
         var content = parser.CompileWithoutErrors(token, model, scriptStart, ref script);
         token.ChangeData(model, scriptStart, content);

         objectEventViewModel.Graphics = trainerGraphics;
         objectEventViewModel.Elevation = 3;
         objectEventViewModel.MoveType = 8;
         objectEventViewModel.RangeX = objectEventViewModel.RangeY = 0;
         objectEventViewModel.TrainerType = objectEventViewModel.TrainerRangeOrBerryID = 0;
         objectEventViewModel.ScriptAddress = scriptStart;
         objectEventViewModel.Flag = 0;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventViewModel.Start + 16)));
         parser.FormatScript<XSERun>(token, model, scriptStart);
      }

      /// <summary>The room that the tutor script looks for in the free space: more than the script needs in any game (see the tests).</summary>
      public const int TutorScriptSpace = 140;

      /// <summary>
      /// What the tutor asks. [buffer1] (\\02, written as an escape like the trade messages do, so that it does not depend on the text macros of the ROM)
      /// is the name of the tutor's move: the script puts it there (bufferattack) before it asks.
      /// It stays on one line with the move names of the vanilla games (13 letters at most); the longest names that a tutor can teach in the expansion
      /// (16 letters, like "Stomping Tantrum") fill the box exactly, as seen in the emulator. Z-moves and G-Max moves are longer, but nobody teaches those.
      /// </summary>
      public const string TutorInfoText = "Want to learn the move \\\\02?";

      /// <summary>The move that a tutor teaches. The expansion's scripts hand the special the move itself, the vanilla scripts the tutor's index in the table of tutor moves.</summary>
      public static int GetTutorMove(IDataModel model, int tutor) {
         if (Flags.IsExpansion(model)) return tutor;
         if (model.GetTableModel(HardcodeTablesModel.MoveTutors) is not { } tutorMoves || tutor < 0 || tutor >= tutorMoves.Count) return 0;
         return model.ReadMultiByteValue(tutorMoves[tutor].Start, 2);
      }

      /// <summary>
      /// The tutor of the script is a different one now: the question that the tutor asks names the move that is taught, so it has to follow.
      /// Scripts that were not made by the template (no buffered move, or one that comes from a variable) are left alone.
      /// </summary>
      public static void UpdateTutorMoveName(IDataModel model, TutorEventContent content, int tutor, ModelDelta token) {
         if (content == null || content.MoveBufferAddress == Pointer.NULL) return;
         if (model.ReadMultiByteValue(content.MoveBufferAddress, 2) >= 0x4000) return; // a variable: it follows on its own
         var move = GetTutorMove(model, tutor);
         if (move < 0 || move >= 0x4000) return;
         model.WriteMultiByteValue(content.MoveBufferAddress, 2, token, move);
      }

      public TutorEventContent GetTutorContent(ScriptParser parser, ObjectEventViewModel eventModel) => GetTutorContent(model, parser, eventModel);
      public static TutorEventContent GetTutorContent(IDataModel model, ScriptParser parser, ObjectEventViewModel eventViewModel) {
         // tutors must have a `special ChooseMonForMoveTutor`
         if (!model.TryGetList("specials", out var specials)) return null;
         var tutorSpecial = specials.IndexOf("ChooseMonForMoveTutor");
         if (tutorSpecial == -1) return null;
         if (!Flags.GetAllScriptSpots(
            model, parser, new[] { eventViewModel.ScriptAddress }, 0x25
         ).Any(
            spot => model.ReadMultiByteValue(spot.Address + 1, 2) == tutorSpecial)
         ) return null;

         var content = new TutorEventContent(Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL);
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { eventViewModel.ScriptAddress }, 0x16, 0x0F, 0x82); // setvar, loadpointer, bufferattack

         foreach (var spot in spots) {
            if (model[spot.Address] == 0x82) {
               // the move that the question names: `bufferattack buffer1 <move>`. Tutors made before the template named the move have none.
               if (model[spot.Address + 1] == 0 && content.MoveBufferAddress == Pointer.NULL) content = content with { MoveBufferAddress = spot.Address + 2 };
               continue;
            }
            if (model[spot.Address] == 0x16) {
               if (content.TutorAddress != Pointer.NULL) return null;
               if (model.ReadMultiByteValue(spot.Address + 1, 2) != 0x8005) return null;
               content = content with { TutorAddress = spot.Address + 3 };
               continue;
            }
            if (content.InfoPointer == Pointer.NULL) {
               content = content with { InfoPointer = spot.Address + 2 };
               if (!GoodPointer(model, spot.Address + 2)) return null;
               continue;
            }
            if (model[spot.Address + 6] == 9 && model[spot.Address + 7] == 5) continue; // skip warningpointer (has a `callstd 5` after it)

            // it's either which, success, or fail. We can tell by the number of pointers
            var run = model.GetNextRun(spot.Address);
            if (spot.Address != run.Start) {
               // 0 pointers -> WhichPokemon
               if (content.WhichPokemonPointer != Pointer.NULL) return null;
               content = content with { WhichPokemonPointer = spot.Address + 2 };
               if (!GoodPointer(model, spot.Address + 2)) return null;
               continue;
            }
            if (run.PointerSources == null) return null;
            if (run.PointerSources.Count == 3) {
               // 3 pointers -> Failed
               if (content.FailedPointer != Pointer.NULL) return null;
               content = content with { FailedPointer = spot.Address + 2 };
               if (!GoodPointer(model, spot.Address + 2)) return null;
               continue;
            }
            if (run.PointerSources.Count.IsAny(1, 2)) {
               // 1 or 2 pointers -> Success
               if (content.SuccessPointer == spot.Address + 2) continue;
               if (content.SuccessPointer != Pointer.NULL) return null;
               content = content with { SuccessPointer = spot.Address + 2 };
               if (!GoodPointer(model, spot.Address + 2)) return null;
               continue;
            }
            return null;
         }

         if (Pointer.NULL.IsAny(content.InfoPointer, content.WhichPokemonPointer, content.SuccessPointer, content.FailedPointer, content.TutorAddress)) return null;
         return content;
      }

      #endregion

      #region Trade

      // The messages of the trade template. [buffer1] (\\02) is the species the NPC wants, [buffer2] (\\03) the one it offers.
      // Line breaks are written with the \n escape and never as a line-break character: the text converter only recognizes the line break
      // of the platform it runs on (Environment.NewLine, which is "\r\n" on Windows), so a '\n' character vanished there and the offer
      // came out as one long line ("Want to trade your Nidorinafor my Nidorino") that ran off the text box.
      // A text box holds two lines of about 214 pixels: the longest line below is "Your old <species> is doing great!".
      public const string TradeInfoText = "Want to trade your \\\\02\\nfor my \\\\03?";
      public const string TradeThanksText = "Thank you!";
      public const string TradeSuccessText = "How is my old \\\\03?\\pnYour old \\\\02 is doing great!";
      public const string TradeFailText = "That's too bad.";
      public const string TradeWrongSpeciesText = "\\.This is no \\\\02.\\pnIf you get one, please trade it\\nfor my \\\\03!";

      /// <summary>All the messages of the trade template, in the order the trade script shows them: offer, thanks, after the trade, cancel, wrong Pokémon.</summary>
      public static IReadOnlyList<string> TradeTexts { get; } = new[] { TradeInfoText, TradeThanksText, TradeSuccessText, TradeFailText, TradeWrongSpeciesText };

      public void CreateTrade(ObjectEventViewModel objectEventViewModel, ModelDelta token) {
         var tradeFlag = FindNextUnusedFlag();

         int tradeId = 0;
         int initialText = WriteText(token, TradeInfoText);
         int thanksText = WriteText(token, TradeThanksText);
         int successText = WriteText(token, TradeSuccessText);
         int failText = WriteText(token, TradeFailText);
         int wrongSpeciesText = WriteText(token, TradeWrongSpeciesText);

         var script = BuildTradeScript(Flags.IsExpansion(model), tradeId, tradeFlag, initialText, thanksText, successText, failText, wrongSpeciesText);

         // 160 bytes is the script as it was before it learned to re-read the species names; leave some room
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 200);
         var content = parser.CompileWithoutErrors(token, model, scriptStart, ref script);
         token.ChangeData(model, scriptStart, content);

         objectEventViewModel.Graphics = trainerGraphics;
         objectEventViewModel.Elevation = 3;
         objectEventViewModel.MoveType = 8;
         objectEventViewModel.RangeX = objectEventViewModel.RangeY = 0;
         objectEventViewModel.TrainerType = objectEventViewModel.TrainerRangeOrBerryID = 0;
         objectEventViewModel.ScriptAddress = scriptStart;
         objectEventViewModel.Flag = 0;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventViewModel.Start + 16)));
         parser.FormatScript<XSERun>(token, model, scriptStart);
      }

      /// <summary>
      /// The in-game trade script. The text pointers are the addresses of the five messages (all of them are written before the script).
      /// Messages talk about the species through the string buffers: [buffer1] is the species the NPC wants and [buffer2] the one it offers,
      /// both filled in by the special GetInGameTradeSpeciesInfo.
      /// </summary>
      /// <param name="expansion">
      /// The expansion's trade specials read the trade number from script variable 0x8005 and the chosen party slot from 0x8004,
      /// the vanilla ones use 0x8004 for the trade and 0x8005 for the slot.
      /// </param>
      public static string BuildTradeScript(bool expansion, int tradeId, int tradeFlag, int initialText, int thanksText, int successText, int failText, int wrongSpeciesText) {
         var selectTrade = expansion ? "copyvar 0x8005 0x8008" : "copyvar 0x8004 0x8008";
         var chosenSlotToSpecialArgs = expansion ? string.Empty : "copyvar 0x8005 0x800A";
         var tradeAndSlotForCreate = expansion ? "copyvar 0x8005 0x8008" : "copyvar 0x8004 0x8008\n  copyvar 0x8005 0x800A";

         // The party menu uses the same string buffers to print each Pokémon's HP ("/ 20"), so by the time the player has chosen
         // the wrong Pokémon the buffers hold garbage: ask the game for the two species names again before talking about them.
         return @$"
  lock
  faceplayer
  setvar 0x8008 {tradeId}
  {selectTrade}
  special2 0x800D GetInGameTradeSpeciesInfo
  copyvar 0x8009 0x800D
  checkflag {tradeFlag}
  if1 = <success>
  loadpointer 0 <{initialText:X6}>
  callstd 5
  compare 0x800D 0
  if1 = <fail>
  special ChoosePartyMon
  waitstate
  lock
  faceplayer
  copyvar 0x800A 0x8004
  compare 0x8004 6
  if1 >= <fail>
  {chosenSlotToSpecialArgs}
  special2 0x800D GetTradeSpecies
  copyvar 0x800B 0x800D
  comparevars 0x800D 0x8009
  if1 != <wrongspecies>
  {tradeAndSlotForCreate}
  special CreateInGameTradePokemon
  special DoInGameTradeScene
  waitstate
  lock
  faceplayer
  loadpointer 0 <{thanksText:X6}>
  callstd 4
  setflag {tradeFlag}
  release
  end
success:
  loadpointer 0 <{successText:X6}>
  callstd 4
  release
  end
fail:
  loadpointer 0 <{failText:X6}>
  callstd 4
  release
  end
wrongspecies:
  {selectTrade}
  special2 0x800D GetInGameTradeSpeciesInfo
  loadpointer 0 <{wrongSpeciesText:X6}>
  callstd 4
  release
  end
";
      }

      public TradeEventContent GetTradeEventContent(ScriptParser parser, ObjectEventViewModel eventModel) => GetTradeContent(model, parser, eventModel.ScriptAddress);
      public static TradeEventContent GetTradeContent(IDataModel model, ScriptParser parser, int scriptAddress) {
         // tardes must have a `special CreateInGameTradePokemon`
         if (!model.TryGetList("specials", out var specials)) return null;
         var tradeSpecial = specials.IndexOf("CreateInGameTradePokemon");
         if (tradeSpecial == -1) return null;
         if (!Flags.GetAllScriptSpots(
            model, parser, new[] { scriptAddress }, 0x25
         ).Any(
            spot => model.ReadMultiByteValue(spot.Address + 1, 2) == tradeSpecial)
         ) return null;

         var content = new TradeEventContent(Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL);
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { scriptAddress }, 0x16, 0x0F); // setvar, loadpointer

         foreach (var spot in spots) {
            // TradeAddress is the only `setvar 0x8008` command
            if (model[spot.Address] == 0x16) {
               if (model.ReadMultiByteValue(spot.Address + 1, 2) != 0x8008) continue;
               if (content.TradeAddress != Pointer.NULL) return null;
               content = content with { TradeAddress = spot.Address + 3 };
               continue;
            }

            // loadpointer InfoPointer is right before callstd 5
            if (model[spot.Address + 7] == 5) {
               if (content.InfoPointer != Pointer.NULL) return null;
               content = content with { InfoPointer = spot.Address + 2 };
               continue;
            }

            // ThanksPointer, SuccessPointer, FailedPointer, and WrongSpecies all look exactly the same, but come in that order
            if (content.ThanksPointer == Pointer.NULL) {
               content = content with { ThanksPointer = spot.Address + 2 };
               continue;
            } else if (content.SuccessPointer == Pointer.NULL) {
               content = content with { SuccessPointer = spot.Address + 2 };
               continue;
            } else if (content.FailedPointer == Pointer.NULL) {
               content = content with { FailedPointer = spot.Address + 2 };
               continue;
            } else if (content.WrongSpeciesPointer == Pointer.NULL) {
               content = content with { WrongSpeciesPointer = spot.Address + 2 };
               continue;
            }
            return null;
         }

         if (Pointer.NULL.IsAny(content.InfoPointer, content.ThanksPointer, content.SuccessPointer, content.FailedPointer, content.WrongSpeciesPointer, content.TradeAddress)) return null;
         return content;
      }

      #endregion

      #region Legendary Encounter

      public void CreateLegendary(ObjectEventViewModel objectEventModel, ModelDelta token) {
         var legendFlag = FindNextUnusedFlag();
         var catchFlag = FindNextUnusedFlag();

         int cryText = model.IsFRLG() ? WriteText(token, "Roar!") : Pointer.NULL;

         var scriptText = BuildLegendaryScript(model, parser, catchFlag, cryText);
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 160);
         var content = parser.CompileWithoutErrors(token, model, scriptStart, ref scriptText);
         token.ChangeData(model, scriptStart, content);

         objectEventModel.Graphics = trainerGraphics;
         objectEventModel.Elevation = FindPreferredTrainerElevation(model, trainerGraphics);
         objectEventModel.MoveType = 8;
         objectEventModel.RangeX = objectEventModel.RangeY = 0;
         objectEventModel.TrainerType = objectEventModel.TrainerRangeOrBerryID = 0;
         objectEventModel.ScriptAddress = scriptStart;
         objectEventModel.Flag = legendFlag;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventModel.Start + 16)));
         parser.FormatScript<XSERun>(token, model, scriptStart);
      }

      // vanilla Emerald/FireRed call it StartLegendaryBattle, the expansion BattleSetup_StartLegendaryBattle
      private static readonly string[] LegendaryBattleSpecialNames = { "BattleSetup_StartLegendaryBattle", "StartLegendaryBattle" };
      private const string HideObjectsFlagName = "FLAG_SYS_CTRL_OBJ_DELETE";

      /// <summary>
      /// The script for a legendary encounter: cry, wild battle, and what to do about the outcome.
      /// Everything that differs between games is looked up in the ROM instead of assumed:
      /// the number of arguments of setwildbattle (3 in vanilla, 6 in the expansion), the number of the legendary-battle special
      /// (the expansion renamed it and its place in the specials list moved) and of the flag that keeps the object on screen during the battle.
      /// </summary>
      public static string BuildLegendaryScript(IDataModel model, ScriptParser parser, int catchFlag, int cryText) {
         var script = new StringBuilder(@"
lock
faceplayer
waitsound
cry 1 2
");
         script.AppendLine(parser.BuildCommand("setwildbattle", "1", "50", "0"));
         if (model.IsFRLG()) {
            script.AppendLine($"preparemsg <{cryText:X6}>");
            script.AppendLine("waitmsg");
         }
         script.AppendLine("waitcry");
         script.AppendLine("pause 10");
         // FireRed/LeafGreen play the gym leader music and wait for a button; Ruby/Sapphire/Emerald go straight to the battle
         var hideFlag = FindFlagNumber(model, HideObjectsFlagName, model.IsFRLG() ? 0x0807 : model.IsEmerald() ? 0x08C1 : 0x0861);
         var special = FindSpecialName(model, model.IsFRLG() ? "0x138" : model.IsEmerald() ? "0x13B" : "0x137");
         if (model.IsFRLG()) {
            script.AppendLine(@"playsong mus_encounter_gym_leader playOnce
waitkeypress");
         }
         script.AppendLine($@"setflag 0x{hideFlag:X4}
special {special}
waitstate
clearflag 0x{hideFlag:X4}");
         script.AppendLine(@$"
fadescreen 1
hidesprite 0x800F
fadescreen 0
special2 0x800D GetBattleOutcome
if.compare.goto 0x800D = 7 <caught>
bufferPokemon 0 1
msgbox.default <auto>
{{
The [buffer1] disappeared!
}}
release
end

caught:
setflag 0x{catchFlag:X4}
release
end

");
         return script.ToString();
      }

      /// <summary>The number of a flag, looked up by name in the ROM's flag list, or the vanilla number when the ROM doesn't name it.</summary>
      private static int FindFlagNumber(IDataModel model, string name, int vanillaNumber) {
         if (model.TryGetList(Flags.FlagListName, out var flags)) {
            var index = flags.IndexOf(name);
            if (index >= 0) return index;
         }
         return vanillaNumber;
      }

      /// <summary>The name of the special that starts a legendary battle in this ROM, or the vanilla number when none of the usual names is in the specials list.</summary>
      private static string FindSpecialName(IDataModel model, string vanillaNumber) {
         if (model.TryGetList("specials", out var specials)) {
            foreach (var name in LegendaryBattleSpecialNames) if (specials.IndexOf(name) >= 0) return name;
         }
         return vanillaNumber;
      }

      public LegendaryEventContent GetLegendaryEventContent(ScriptParser parser, ObjectEventViewModel eventModel) => GetLegendaryEventContent(model, parser, eventModel);
      public static LegendaryEventContent GetLegendaryEventContent(IDataModel model, ScriptParser parser, ObjectEventViewModel ev) {
         var content = new LegendaryEventContent(Pointer.NULL, Pointer.NULL, null, null, Pointer.NULL);
         /*
            67 preparemsg text<"">
            A1 cry species:data.pokemon.names effect:
            B6 setwildbattle species: level. item:
            29 setflag flag:
            2A clearflag flag:
            7D bufferPokemon buffer.3 species:data.pokemon.names
         */
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { ev.ScriptAddress }, false, 0x67, 0xA1, 0xB6, 0x29, 0x2A, 0x7D);
         var flagsSet = new Dictionary<int, int>(); // address of flag -> flag value
         var flagsCleared = new Dictionary<int, int>(); // address of flag -> flag value
         var bufferSpots = new Dictionary<int, int>(); // address of buffer -> pokemon to buffer
         foreach (var spot in spots) {
            if (spot.Line.LineCode[0] == 0x29) {
               flagsSet[spot.Address + 1] = model.ReadMultiByteValue(spot.Address + 1, 2);
            } else if (spot.Line.LineCode[0] == 0x2A) {
               flagsCleared[spot.Address + 1] = model.ReadMultiByteValue(spot.Address + 1, 2);
            } else if (spot.Line.LineCode[0] == 0x7D) {
               bufferSpots[spot.Address + 2] = model.ReadMultiByteValue(spot.Address + 2, 2);
            } else {
               content = spot.Line.LineCode[0] switch {
                  0x67 => content with { CryTextPointer = spot.Address + 1 },
                  0xA1 => content with { Cry = spot.Address },
                  0xB6 => content with { SetWildBattle = spot.Address },
                  _ => throw new NotImplementedException(),
               };
            }
         }
         if (content.Cry == Pointer.NULL) return null;
         if (content.SetWildBattle == Pointer.NULL) return null;
         var setOnlyFlags = flagsSet.Values.Except(flagsCleared.Values).ToHashSet();
         if (setOnlyFlags.Count != 1) return null;
         var bufferPokemon = new List<int>();
         foreach (var kvp in bufferSpots) {
            if (kvp.Value == model.ReadMultiByteValue(content.SetWildBattle + 1, 2)) bufferPokemon.Add(kvp.Key - 2);
         }

         var legendFlag = setOnlyFlags.Single();
         var legendFlagAddress = flagsSet.Keys.Where(key => flagsSet[key] == legendFlag);
         content = content with { SetFlag = legendFlagAddress.Select(flag => flag - 1).ToList() };
         content = content with { BufferPokemon = bufferPokemon };

         return content;
      }

      #endregion

      #region HM Object

      public ObservableCollection<string> HMObjectOptions { get; } = new();

      private int hmObjectIndex;
      public int HMObjectIndex {
         get => hmObjectIndex;
         set {
            Set(ref hmObjectIndex, value);
            UpdateSpriteFromHMObject();
         }
      }

      private void UpdateSpriteFromHMObject() {
         if (hmObjectIndex < 0 || hmObjectIndex > 2) return;
         // FR/LG:  95, 96, 97
         // R/S/EE: 82, 86, 87
         if (model.IsFRLG()) TrainerGraphics = new[] { 95, 96, 97 }[hmObjectIndex];
         else TrainerGraphics = new[] { 82, 86, 87 }[hmObjectIndex];
      }

      public void CreateHMObject(ObjectEventViewModel objectEventViewModel, ModelDelta token) {
         var scriptStart = AllMapsModel.Create(model, default)
            .SelectMany(bank => bank)
            .SelectMany(map => map?.Events.Objects ?? new())
            .Where(obj => obj.Graphics == trainerGraphics)
            .Select(obj => obj.ScriptAddress)
            .ToHistogram()
            .MostCommonKey();

         objectEventViewModel.Graphics = trainerGraphics;
         objectEventViewModel.Elevation = 3;
         objectEventViewModel.MoveType = 8;
         objectEventViewModel.RangeX = objectEventViewModel.RangeY = 0;
         objectEventViewModel.TrainerType = objectEventViewModel.TrainerRangeOrBerryID = 0;
         objectEventViewModel.ScriptAddress = scriptStart;
         objectEventViewModel.Flag = (objectEventViewModel.ObjectID % 0x10) + 0x10;
      }

      #endregion

      #region Helper Methods

      private static bool GoodPointer(IDataModel model, int address) {
         if (address < 0 || address >= model.Count - 3) return false;
         address = model.ReadPointer(address);
         return 0 <= address && address < model.Count;
      }

      private int WriteText(ModelDelta token, string text) {
         var bytes = model.TextConverter.Convert(text, out var _);
         var start = model.FindFreeSpace(model.FreeSpaceStart, bytes.Count);
         token.ChangeData(model, start, bytes);
         return start;
      }

      private void UpdateObjectTemplateImage(TemplateType old = default) {
         if (selectedTemplate == TemplateType.None) {
            ObjectTemplateImage = GraphicsOptions[0];
         } else if (selectedTemplate.IsAny(TemplateType.Trainer, TemplateType.Npc, TemplateType.Tutor, TemplateType.Trade, TemplateType.Legendary, TemplateType.HMObject)) {
            ObjectTemplateImage = GraphicsOptions[TrainerGraphics];
         } else if (selectedTemplate == TemplateType.Item) {
            ObjectTemplateImage = GraphicsOptions[ItemGraphics];
         } else if (selectedTemplate == TemplateType.Mart) {
            ObjectTemplateImage = GraphicsOptions[ClerkGraphics];
         }
         if (ObjectTemplateImage.PixelData.Length > 0) {
            ObjectTemplateImage = new ReadonlyPixelViewModel(ObjectTemplateImage.PixelWidth, ObjectTemplateImage.PixelHeight, ObjectTemplateImage.PixelData, ObjectTemplateImage.PixelData[0]);
         }
         ObjectTemplateImage = ObjectTemplateImage.AutoCrop();
         NotifyPropertyChanged(nameof(ObjectTemplateImage));
      }

      #endregion
   }

   public record TrainerEventContent(int BeforeTextPointer, int WinTextPointer, int AfterTextPointer, int TrainerClassAddress, int TrainerIndex, int TrainerIndexAddress, int TrainerNameAddress, int TeamPointer, int TrainerStart, int TrainerSpriteAddress, int TrainerNameLength);

   /// <summary>
   /// Where the fields the trainer editors care about live inside a trainer record.
   /// Vanilla: [structType. class. introMusic. sprite. name""12 ... pokemonCount:: pokemon(pointer)] (class +1, sprite +3, name +4, team +36).
   /// Other layouts (decomp hacks) are read from the table's segment names: class, sprite, name, pokemon.
   /// </summary>
   public record TrainerLayout(int ClassOffset, int SpriteOffset, int NameOffset, int NameLength, int TeamOffset) {
      public static readonly TrainerLayout Vanilla = new(1, 3, 4, 12, 36);
      public static TrainerLayout For(ITableRun trainers) {
         if (trainers == null) return Vanilla;
         int classOffset = -1, spriteOffset = -1, nameOffset = -1, nameLength = 12, teamOffset = -1, offset = 0;
         foreach (var segment in trainers.ElementContent) {
            switch (segment.Name) {
               case "class": classOffset = offset; break;
               case "sprite": spriteOffset = offset; break;
               case "name": nameOffset = offset; nameLength = segment.Length; break;
               case "pokemon": if (segment.Type == ElementContentType.Pointer) teamOffset = offset; break;
            }
            offset += segment.Length;
         }
         if (classOffset < 0 || spriteOffset < 0 || nameOffset < 0 || teamOffset < 0) return Vanilla;
         return new(classOffset, spriteOffset, nameOffset, nameLength, teamOffset);
      }
   }

   public record RematchTrainerEventContent(int TrainerID, int BeforeTextPointer, int WinTextPointer, int AfterTextPointer);

   public record MartEventContent(int HelloPointer, int MartPointer, int GoodbyePointer);

   public record TutorEventContent(int InfoPointer, int WhichPokemonPointer, int FailedPointer, int SuccessPointer, int TutorAddress, int MoveBufferAddress = Pointer.NULL);

   public record TradeEventContent(int InfoPointer, int ThanksPointer, int SuccessPointer, int FailedPointer, int WrongSpeciesPointer, int TradeAddress);

   public record LegendaryEventContent(int Cry, int SetWildBattle, List<int> BufferPokemon, List<int> SetFlag, int CryTextPointer);

   public enum TemplateType { None, Npc, Item, Trainer, Mart, Tutor, Trade, Legendary, HMObject }
}

/*
 * FireRed flags that get missed by the current algorithm:
// 2A2 visited sevii island 2
// 2A7 -> aurora ticket
// 2A8 -> mystic ticket
// 2CF -> visited Oak's Lab
// 2D2/2D3 -> seafoam B3F/B4F current
// 2DE -> tutor frezy plant?
// 2DF -> tutor blast burn?
// 2E0 -> tutor hydro cannon?
// missing 3E8 to 4A6 (hidden items)
// missing 4BC -> defeat champ

 * known gaps in the current algorithm:
 * -> doesn't check map header scripts
 * -> doesn't understand flags that are set using variables
 */
