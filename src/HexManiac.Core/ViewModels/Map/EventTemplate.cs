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
            var targetSprite = model.GetTableModel(HardcodeTablesModel.TrainerTableName)[TrainerOptions.SelectedIndex].GetValue("sprite");
            foreach (var key in TrainerPreferences.Keys) {
               if (TrainerPreferences[key].Sprite != targetSprite) continue;
               TrainerGraphics = key;
               break;
            }
         });

         initializationWorkload = dispatcher.RunBackgroundWork(() => {
            usedFlags = Flags.GetUsedItemFlags(model, parser);
            usedTrainerFlags = Flags.GetUsedTrainerFlags(model, parser);
            usedVariables = Flags.GetUsedVariables(model, parser);
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
               trainerOptions.Add(ObjectEventViewModel.CreateOption(options, i, trainers[i].GetValue(1), trainers[i].GetStringValue("name")));
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

      public int FindNextUnusedFlag() {
         var flag = 0x21;
         while (UsedFlags.Contains(flag)) flag++;
         UsedFlags.Add(flag);
         return flag;
      }

      public int FindNextUnusedVariable() {
         var variable = 0x4034;
         while (UsedVariables.Contains(variable)) variable++;
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
      public bool UseExistingTrainer { get => useExistingTrainer; set => Set(ref useExistingTrainer, value); }

      public FilteringComboOptions TrainerOptions { get; } = new();

      private int trainerGraphics, maxPokedex = 25, maxLevel = 9, preferredType = 6;
      public int TrainerGraphics {
         get => trainerGraphics;
         set {
            Set(ref trainerGraphics, value, old => {
               if (!TrainerPreferences.TryGetValue(trainerGraphics, out var pref)) pref = new(0, 0, 0);
               var spriteAddress = model.GetTableModel(HardcodeTablesModel.TrainerSpritesName)[pref.Sprite].GetAddress("sprite");
               var spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
               TrainerSprite = ReadonlyPixelViewModel.Create(model, spriteRun, true);
               NotifyPropertyChanged(nameof(TrainerSprite));
               UpdateObjectTemplateImage();
            });
         }
      }
      public int MaxPokedex { get => maxPokedex; set => Set(ref maxPokedex, value); }
      public int MaxLevel { get => maxLevel; set => Set(ref maxLevel, value); }
      public int PreferredType { get => preferredType; set => Set(ref preferredType, value); }

      private bool useNationalDex;
      public bool UseNationalDex { get => useNationalDex; set => Set(ref useNationalDex, value); }

      public IPixelViewModel TrainerSprite { get; private set; }

      // TODO use all-caps name or mixed-caps name depending on other trainers in the table
      // TODO use reference file to get names and before/win/after text
      public void CreateTrainer(ObjectEventViewModel objectEventModel, ModelDelta token) {
         const int ChosenTypeOddsMultiplier = 100;

         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName, () => token);
         var trainerFlag = 1;
         if (UseExistingTrainer) {
            trainerFlag = TrainerOptions.SelectedIndex;
            UsedTrainerFlags.Add(trainerFlag);
         } else {
            // part 1: the team
            var availablePokemon = new List<int>();
            var dexName = HardcodeTablesModel.RegionalDexTableName;
            if (useNationalDex) dexName = HardcodeTablesModel.NationalDexTableName;
            var pokedex = model.GetTableModel(dexName, () => token);
            var pokestats = model.GetTableModel(HardcodeTablesModel.PokemonStatsTable, () => token);
            for (int i = 1; i < pokedex.Count; i++) {
               if (pokedex[i - 1].GetValue(0) > maxPokedex) continue;
               if (MinLevel.TryGetValue(i, out var level) && level > maxLevel) continue;
               availablePokemon.Add(i);
               if (pokestats[i].GetValue("type1") == preferredType || pokestats[i].GetValue("type2") == preferredType) {
                  for (int j = 0; j < ChosenTypeOddsMultiplier; j++) availablePokemon.Add(i);
               }
            }

            var teamSize = rnd.Next(3) + 1;
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

            // part 2: the trainer
            while (UsedTrainerFlags.Contains(trainerFlag)) trainerFlag++;
            usedTrainerFlags.Add(trainerFlag);

            var trainer = trainers[trainerFlag];
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
            if (!TrainerPreferences.TryGetValue(trainerGraphics, out var pref)) pref = new(0, 0, 0);
            trainer.SetValue("class", pref.TrainerClass);
            trainer.SetValue("introMusicAndGender", pref.MusicAndGender);
            trainer.SetValue("sprite", pref.Sprite);
         }

         // part 3: the script
         /*
              trainerbattle 00 102 0 <before> <during>
              loadpointer 0 <after>
              callstd 6
              end
          */
         //       2                  6       10          16
         // 5C 00 trainerFlag: 00 00 <before> <win> 0F 00 <after> 09 06 02
         var before = WriteText(token, "Let's battle!");
         var win = WriteText(token, "You Win!");
         var after = WriteText(token, "Post-battle chat!");
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 24);
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
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName, null);
         if (trainerID >= trainers.Count) return null;

         var beforePointer = address + 6;
         var winPointer = address + 10;
         var afterPointer = address + 16;
         var trainerClassAddress = trainers[trainerID].Start + 1;
         var trainerNameAddress = trainers[trainerID].Start + 4;
         var teamPointer = trainers[trainerID].Start + 36;

         return new TrainerEventContent(beforePointer, winPointer, afterPointer, trainerClassAddress, trainerID, address + 2, trainerNameAddress, teamPointer);
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

         int tutor = 0;
         int infoStart = WriteText(token, "Want to learn a cool move?");
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
         // script length = 109
         // note that the condition for recognizing the `warningStart` message is different in
         // the Emerald case, since there's no `signmsg` command to use for reference
         // instead, it's just 0 or 1 pointers, and has `callstd 5` after it.
         // maybe just expect a `callstd 5` after it, since it's the only one after infoStart that has that in both FR and Em

         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 109);
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
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { eventViewModel.ScriptAddress }, 0x16, 0x0F); // setvar, loadpointer

         foreach (var spot in spots) {
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

      public void CreateTrade(ObjectEventViewModel objectEventViewModel, ModelDelta token) {
         var tradeFlag = FindNextUnusedFlag();

         int tradeId = 0;
         int initialText = WriteText(token, "Want to trade your \\\\02\nfor my \\\\03?");
         int thanksText = WriteText(token, "Thank you!");
         int successText = WriteText(token, "How is my old \\\\03?\\pnYour old \\\\02 is doing great!");
         int failText = WriteText(token, "That's too bad.");
         int wrongSpeciesText = WriteText(token, "\\.This is no \\\\02.\\pnIf you get one, please trade it\\nfor my \\\\03!");

         var script = @$"
  lock
  faceplayer
  setvar 0x8008 {tradeId}
  copyvar 0x8004 0x8008
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
  copyvar 0x8005 0x800A
  special2 0x800D GetTradeSpecies
  copyvar 0x800B 0x800D
  comparevars 0x800D 0x8009
  if1 != <wrongspecies>
  copyvar 0x8004 0x8008
  copyvar 0x8005 0x800A
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
  loadpointer 0 <{wrongSpeciesText:X6}>
  callstd 4
  release
  end
";

         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 160);
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

         #region script
         var script = new StringBuilder(@"
lock
faceplayer
waitsound
cry 1 2
setwildbattle 1 50 0
");
         if (model.IsFRLG()) {
            script.AppendLine($"preparemsg <{cryText:X6}>");
            script.AppendLine("waitmsg");
         }
         script.AppendLine("waitcry");
         script.AppendLine("pause 10");
         if (model.IsFRLG()) {
            script.AppendLine(@"
   playsong mus_encounter_gym_leader playOnce
   waitkeypress
   setflag 0x0807
   special 0x138
   waitstate
   clearflag 0x0807
");
         } else if (model.IsEmerald()) {
            script.AppendLine(@"
   setflag 0x08C1
   special 0x13B
   waitstate
   clearflag 0x08C1
");
         } else {
            script.AppendLine(@"
   setflag 0x861
   special 0x137
   waitstate
   clearflag 0x861
");
         }
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
         #endregion

         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 160);
         var scriptText = script.ToString();
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

   public record TrainerEventContent(int BeforeTextPointer, int WinTextPointer, int AfterTextPointer, int TrainerClassAddress, int TrainerIndex, int TrainerIndexAddress, int TrainerNameAddress, int TeamPointer);

   public record RematchTrainerEventContent(int TrainerID, int BeforeTextPointer, int WinTextPointer, int AfterTextPointer);

   public record MartEventContent(int HelloPointer, int MartPointer, int GoodbyePointer);

   public record TutorEventContent(int InfoPointer, int WhichPokemonPointer, int FailedPointer, int SuccessPointer, int TutorAddress);

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
