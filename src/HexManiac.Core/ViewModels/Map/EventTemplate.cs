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
      public readonly Random rnd = new();
      public readonly IDataModel model;
      public readonly ScriptParser parser;
      public readonly Task initializationWorkload;
      public ISet<int> usedFlags, usedTrainerFlags, usedVariables;
      public IReadOnlyDictionary<int, TrainerPreference> trainerPreferences;
      public IReadOnlyDictionary<int, int> minLevel;

      public ISet<int> UsedFlags {
         get {
            initializationWorkload.Wait();
            return usedFlags;
         }
      }
      public ISet<int> UsedTrainerFlags {
         get {
            initializationWorkload.Wait();
            return usedTrainerFlags;
         }
      }
      public ISet<int> UsedVariables {
         get {
            initializationWorkload.Wait();
            return usedVariables;
         }
      }

      public void UseTrainerFlag(int flag) => UsedTrainerFlags.Add(flag);
      public bool IsTrainerFlagInUse(int flag) => UsedTrainerFlags.Contains(flag);

      public IReadOnlyDictionary<int, TrainerPreference> TrainerPreferences {
         get {
            if (trainerPreferences == null) trainerPreferences = Flags.GetTrainerPreference(model, parser);
            return trainerPreferences;
         }
      }

      public IReadOnlyDictionary<int, int> MinLevel {
         get {
            if (minLevel == null) minLevel = Flags.GetMinimumLevelForPokemon(model);
            return minLevel;
         }
      }

      public IPixelViewModel ObjectTemplateImage { get; set; }

      public IReadOnlyList<IPixelViewModel> OverworldGraphics { get; set; }

      public TemplateType selectedTemplate;

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

      #region Trainer

      public ObservableCollection<VisualComboOption> GraphicsOptions { get; } = new();
      public ObservableCollection<string> TypeOptions { get; } = new();

      public bool useExistingTrainer;
      public bool UseExistingTrainer { get => useExistingTrainer; set => Set(ref useExistingTrainer, value); }

      public FilteringComboOptions TrainerOptions { get; } = new();

      public int trainerGraphics, maxPokedex = 25, maxLevel = 9, preferredType = 6;
      public int MaxPokedex { get => maxPokedex; set => Set(ref maxPokedex, value); }
      public int MaxLevel { get => maxLevel; set => Set(ref maxLevel, value); }
      public int PreferredType { get => preferredType; set => Set(ref preferredType, value); }

      public bool useNationalDex;
      public bool UseNationalDex { get => useNationalDex; set => Set(ref useNationalDex, value); }

      public IPixelViewModel TrainerSprite { get; set; }

      public int FindPreferredTrainerElevation(IDataModel model, int graphics) {
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

      #endregion

      #region Item

      public ObservableCollection<string> ItemOptions { get; } = new();

      public int itemID = 20;
      public int ItemID { get => itemID; set => Set(ref itemID, value); }

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

      public int ItemGraphics => model.IsFRLG() ? 92 : 59;

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

      public int ClerkGraphics => model.IsFRLG() ? 68 : 83;

      #endregion

      #region Trade

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

      #region HM Object

      public ObservableCollection<string> HMObjectOptions { get; } = new();

      public int hmObjectIndex;

      #endregion

      #region Helper Methods

      public static bool GoodPointer(IDataModel model, int address) {
         if (address < 0 || address >= model.Count - 3) return false;
         address = model.ReadPointer(address);
         return 0 <= address && address < model.Count;
      }

      public int WriteText(ModelDelta token, string text) {
         var bytes = model.TextConverter.Convert(text, out var _);
         var start = model.FindFreeSpace(model.FreeSpaceStart, bytes.Count);
         token.ChangeData(model, start, bytes);
         return start;
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
