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

// example for making a bug trainer: templates.CreateTrainer(objectEvent, history.CurrentChange, 20 /* bug catcher */, 30, 9, 6 /*bug*/, true);


namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class EventTemplate : ViewModelCore {
      private readonly Random rnd = new();
      private readonly IDataModel model;
      private readonly ScriptParser parser;
      private ISet<int> usedFlags;
      private ISet<int> usedTrainerFlags;
      private IReadOnlyDictionary<int, TrainerPreference> trainerPreferences;
      private IReadOnlyDictionary<int, int> minLevel;

      private ISet<int> UsedFlags {
         get {
            if (usedFlags == null) usedFlags = Flags.GetUsedItemFlags(model, parser);
            return usedFlags;
         }
      }

      private ISet<int> UsedTrainerFlags {
         get {
            if (usedTrainerFlags == null) usedTrainerFlags = Flags.GetUsedTrainerFlags(model, parser);
            return usedTrainerFlags;
         }
      }

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

      public EventTemplate(IDataModel model, ScriptParser parser, IReadOnlyList<IPixelViewModel> owGraphics) {
         (this.model, this.parser) = (model, parser);
         RefreshLists(owGraphics);
         if (model.IsFRLG()) UseNationalDex = true;
      }

      public void RefreshLists(IReadOnlyList<IPixelViewModel> owGraphics) {
         AvailableTemplateTypes.Clear();
         AvailableTemplateTypes.Add(TemplateType.None);
         AvailableTemplateTypes.Add(TemplateType.Npc);
         AvailableTemplateTypes.Add(TemplateType.Item);
         AvailableTemplateTypes.Add(TemplateType.Trainer);
         AvailableTemplateTypes.Add(TemplateType.Mart);
         AvailableTemplateTypes.Add(TemplateType.Trade);
         if (model.IsFRLG() || model.IsEmerald()) AvailableTemplateTypes.Add(TemplateType.Tutor); // Ruby/Sapphire don't have tutors

         GraphicsOptions.Clear();
         for (int i = 0; i < owGraphics.Count; i++) GraphicsOptions.Add(VisualComboOption.CreateFromSprite(i.ToString(), owGraphics[i].PixelData, owGraphics[i].PixelWidth, i, 2));

         TypeOptions.Clear();
         foreach (var type in model.GetTableModel(HardcodeTablesModel.TypesTableName)) {
            TypeOptions.Add(type.GetStringValue("name"));
         }

         ItemOptions.Clear();
         foreach (var item in model.GetTableModel(HardcodeTablesModel.ItemsTableName)) {
            ItemOptions.Add(item.GetStringValue("name"));
         }

         UpdateObjectTemplateImage();
      }

      private TemplateType selectedTemplate;
      public TemplateType SelectedTemplate {
         get => selectedTemplate;
         set => SetEnum(ref selectedTemplate, value, UpdateObjectTemplateImage);
      }

      public ObservableCollection<TemplateType> AvailableTemplateTypes { get; } = new();

      public void ApplyTemplate(ObjectEventViewModel objectEventModel, ModelDelta token) {
         if (selectedTemplate == TemplateType.Trainer) CreateTrainer(objectEventModel, token);
         if (selectedTemplate == TemplateType.Npc) CreateNPC(objectEventModel, token);
         if (selectedTemplate == TemplateType.Item) CreateItem(objectEventModel, token);
         if (selectedTemplate == TemplateType.Mart) CreateMart(objectEventModel, token);
         if (selectedTemplate == TemplateType.Tutor) CreateTutor(objectEventModel, token);
         if (selectedTemplate == TemplateType.Trade) CreateTrade(objectEventModel, token);
      }

      #region Trainer

      public ObservableCollection<VisualComboOption> GraphicsOptions { get; } = new();
      public ObservableCollection<string> TypeOptions { get; } = new();

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
               for (int j = 0; j < 9; j++) availablePokemon.Add(i);
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
         var trainerFlag = 1;
         while (UsedTrainerFlags.Contains(trainerFlag)) trainerFlag++;
         usedTrainerFlags.Add(trainerFlag);

         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName, () => token);
         var trainer = trainers[trainerFlag];
         // structType. class. introMusicAndGender. sprite. name""12 item1: item2: item3: item4: doubleBattle:: ai:: pokemonCount:: pokemon<>
         trainer.SetValue("structType", 0);
         trainer.SetStringValue("name", "Francis");
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

         // part 3: the script
         /*
              trainerbattle 00 102 0 <before> <during>
              loadpointer 0 <after>
              callstd 6
              end
          */
         //       2                  6       10          16
         // 5C 00 trainerFlag: 00 00 <before> <win> 0F 00 <after> 09 06 02
         var before = WriteText(token, "Before!");
         var win = WriteText(token, "You Win!");
         var after = WriteText(token, "After!");
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
         objectEventModel.Elevation = 3;
         objectEventModel.MoveType = new[] { 7, 8, 9, 10 }[rnd.Next(4)];
         objectEventModel.RangeX = objectEventModel.RangeY = 1;
         objectEventModel.TrainerType = 1;
         objectEventModel.TrainerRangeOrBerryID = 5;
         objectEventModel.ScriptAddress = scriptStart;
         objectEventModel.Flag = 0;

         model.ObserveRunWritten(token, new XSERun(scriptStart, SortedSpan.One(objectEventModel.Start + 16)));
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

         return new TrainerEventContent(beforePointer, winPointer, afterPointer, trainerClassAddress, trainerNameAddress, teamPointer);
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

         var itemFlag = 0x21;
         while (UsedFlags.Contains(itemFlag)) itemFlag++;
         UsedFlags.Add(itemFlag);

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
         foreach (var (k, v) in expectedValues) {
            if (model[address + k] != v) return Pointer.NULL;
         }
         return address + 3;
      }

      private int ItemGraphics => model.IsFRLG() ? 92 : 59;

      #endregion

      #region Signpost

      public void ApplyTemplate(SignpostEventViewModel signpost, ModelDelta token) {
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

         var tutorFlag = 0x21;
         while (UsedFlags.Contains(tutorFlag)) tutorFlag++;
         UsedFlags.Add(tutorFlag);

         int tutor = 0;
         int infoStart = WriteText(token, "Want to learn a cool move?");
         int warningStart = WriteText(token, "This move can be learned only\\nonce. Is that okay?");
         int whichStart = WriteText(token, "Which POKéMON wants to learn\\nthe move?");
         int doneStart = WriteText(token, "Enjoy the move!");
         int failedStart = WriteText(token, "I guess not.");

         var script = $@"
   lock
   faceplayer
   checkflag {tutorFlag}
   if1 = <success>
   loadpointer 0 <{infoStart:X6}>
   callstd 5
   compare 0x800D 0
   if1 = <failed>
   textcolor 3
   special DisableMsgBoxWalkaway
   signmsg
   loadpointer 0 <{warningStart:X6}>
   callstd 5
   normalmsg
   copyvar 0x8012 0x8013
   campare 0x800D 0
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

         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 109);
         var content = parser.Compile(token, model, scriptStart, ref script, out var _);
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
         // int InfoPointer, int WhichPokemonPointer, int FailedPointer, int SuccessPointer, int TutorAddress

         // InfoPointer is the first pointer
         // WhichPokemonPointer should have 0 pointers to it
         // WarningPointer comes directly after `signmsg`
         // SuccessPointer has at least 1 but less than 3 pointers to it
         // FailedPointer has 3 pointers to it
         // `setvar 0x8005` is the tutor number

         var content = new TutorEventContent(Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL, Pointer.NULL);
         var spots = Flags.GetAllScriptSpots(model, parser, new[] { eventViewModel.ScriptAddress }, 0x16, 0x0F); // setvar, loadpointer

         foreach (var spot in spots) {
            if (spot.Line.LineCode[0] == 0x16) {
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
            if (model[spot.Address - 1] == 0xCA) continue; // skip warningpointer

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
         // TODO
         throw new NotImplementedException();
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
         } else if (selectedTemplate == TemplateType.Trainer || selectedTemplate == TemplateType.Npc || selectedTemplate == TemplateType.Tutor || selectedTemplate == TemplateType.Trade) {
            ObjectTemplateImage = GraphicsOptions[TrainerGraphics];
         } else if (selectedTemplate == TemplateType.Item) {
            ObjectTemplateImage = GraphicsOptions[ItemGraphics];
         } else if (selectedTemplate == TemplateType.Mart) {
            ObjectTemplateImage = GraphicsOptions[ClerkGraphics];
         }
         ObjectTemplateImage = new ReadonlyPixelViewModel(ObjectTemplateImage.PixelWidth, ObjectTemplateImage.PixelHeight, ObjectTemplateImage.PixelData, ObjectTemplateImage.PixelData[0]);
         ObjectTemplateImage = ObjectTemplateImage.AutoCrop();
         NotifyPropertyChanged(nameof(ObjectTemplateImage));
      }

      #endregion
   }

   public record TrainerEventContent(int BeforeTextPointer, int WinTextPointer, int AfterTextPointer, int TrainerClassAddress, int TrainerNameAddress, int TeamPointer);

   public record MartEventContent(int HelloPointer, int MartPointer, int GoodbyePointer);

   public record TutorEventContent(int InfoPointer, int WhichPokemonPointer, int FailedPointer, int SuccessPointer, int TutorAddress);

   public enum TemplateType { None, Npc, Item, Trainer, Mart, Tutor, Trade }
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
