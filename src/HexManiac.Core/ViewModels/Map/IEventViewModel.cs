using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using Microsoft.Scripting.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {

   public interface IEventViewModel : IEquatable<IEventViewModel>, INotifyPropertyChanged {
      event EventHandler EventVisualUpdated;
      public event EventHandler<EventCycleDirection> CycleEvent;
      public ICommand CycleEventCommand { get; }
      public ModelArrayElement Element { get; }
      string EventType { get; }
      string EventIndex { get; }
      int TopOffset { get; }
      int LeftOffset { get; }
      int X { get; set; }
      int Y { get; set; }
      IPixelViewModel EventRender { get; }
      void Render(IDataModel model, LayoutModel layout);
      bool Delete();
   }

   public enum EventCycleDirection { PreviousCategory, PreviousEvent, NextEvent, NextCategory, None }

   public class FlyEventViewModel : ViewModelCore, IEventViewModel {
      private readonly ModelArrayElement flySpot;
      private readonly ModelArrayElement connectionEntry;

      public string EventType => "Fly";

      public string EventIndex => "1/1";

      public virtual int TopOffset => 0;
      public virtual int LeftOffset => 0;

      public ModelArrayElement Element => flySpot;

      #region X/Y

      public int X {
         get => !Valid ? -1 : flySpot.GetValue("x");
         set {
            if (!Valid) return;
            flySpot.SetValue("x", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateXY) xy = null;
            NotifyPropertyChanged(nameof(XY));
            EventVisualUpdated.Raise(this);
         }
      }

      public int Y {
         get => !Valid ? -1 : flySpot.GetValue("y");
         set {
            if (!Valid) return;
            flySpot.SetValue("y", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateXY) xy = null;
            NotifyPropertyChanged(nameof(XY));
            EventVisualUpdated.Raise(this);
         }
      }

      private bool ignoreUpdateXY;
      private string xy;
      public string XY {
         get {
            if (!Valid) return "(-1, -1)";
            if (xy == null) xy = $"({X}, {Y})";
            return xy;
         }
         set {
            if (!Valid) return;
            xy = value;
            var parts = value.Split(new[] { ',', ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            ignoreUpdateXY = true;
            if (parts[0].TryParseInt(out int x)) X = x;
            if (parts[1].TryParseInt(out int y)) Y = y;
            ignoreUpdateXY = false;
         }
      }


      #endregion

      public IPixelViewModel EventRender { get; private set; }

      public bool Valid { get; }

      private StubCommand cycleEventCommand;
      public ICommand CycleEventCommand => StubCommand<EventCycleDirection>(ref cycleEventCommand, direction => {
         CycleEvent.Raise(this, direction);
      });

      public event EventHandler EventVisualUpdated;
      public event EventHandler<EventCycleDirection> CycleEvent;

      public static IEnumerable<FlyEventViewModel> Create(IDataModel model, int bank, int map, Func<ModelDelta> tokenFactory) {
         var flyTable = model.GetTableModel(HardcodeTablesModel.FlySpawns, tokenFactory);
         if (flyTable == null) yield break;
         for (int i = 0; i < flyTable.Count; i++) {
            var flight = flyTable[i];
            if (flight.GetValue("bank") != bank) continue;
            if (flight.GetValue("map") != map) continue;
            yield return new FlyEventViewModel(flight, bank, map, i + 1);
         }
      }

      public FlyEventViewModel(ModelArrayElement flySpot, int bank, int map, int expectedFlight) {
         this.flySpot = flySpot;
         var model = flySpot.Model;
         var tokenFactory = () => flySpot.Token;
         // get the region from the map
         var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable, tokenFactory);
         if (banks == null) return; // not valid map table
         var maps = banks[bank].GetSubTable("maps");
         if (maps == null) return;  // not valid bank
         var table = maps[map].GetSubTable("map");
         if (table == null) return; // not valid map
         var region = table[0].GetValue(Format.RegionSection);
         if (model.IsFRLG()) region -= 88;
         if (region < 0) return;    // not valid region section

         Valid = true; // connection entries are optional

         var flyIndexTable = model.GetTableModel(HardcodeTablesModel.FlyConnections, tokenFactory);
         if (flyIndexTable == null) return;
         if (region >= flyIndexTable.Count) return;
         var entry = flyIndexTable[region];
         if (entry.TryGetValue("flight", out int savedFlight) && savedFlight == expectedFlight) {
            connectionEntry = entry;
         }
      }

      /// <returns>true if the event was deleted</returns>
      public bool Delete() {
         if (!Valid) return false;
         if (connectionEntry == null) return false; // cannot delete 'special' fly events (such as the player's house)
         // set the connection table's index to 0
         connectionEntry.SetValue("flight", 0);
         flySpot.SetValue("bank", 0);
         flySpot.SetValue("map", 0);
         flySpot.SetValue("x", 0);
         flySpot.SetValue("y", 0);
         return true;
      }

      public bool Equals(IEventViewModel? other) {
         if (other is not FlyEventViewModel fly) return false;
         return X == fly.X && Y == fly.Y && flySpot.Start == fly.flySpot.Start;
      }

      public void Render(IDataModel model, LayoutModel layout) {
         EventRender = BaseEventViewModel.BuildEventRender(UncompressedPaletteColor.Pack(31, 31, 0));
      }
   }

   public abstract class BaseEventViewModel : ViewModelCore, IEventViewModel, IEquatable<IEventViewModel> {
      public event EventHandler EventVisualUpdated;
      public event EventHandler<EventCycleDirection> CycleEvent;

      private StubCommand cycleEventCommand;
      public ICommand CycleEventCommand => StubCommand<EventCycleDirection>(ref cycleEventCommand, direction => {
         CycleEvent?.Invoke(this, direction);
      });

      protected readonly ModelArrayElement element;
      private readonly string parentLengthField;

      public ModelArrayElement Element => element;
      public ModelDelta Token => element.Token;

      public string EventType => GetType().Name.Replace("EventViewModel", " Event");
      public string EventIndex => $"{element.ArrayIndex + 1} / {element.Table.ElementCount}";
      public virtual int TopOffset => 0;
      public virtual int LeftOffset => 0;

      #region X/Y

      public int X {
         get => element.GetValue("x");
         set {
            element.SetValue("x", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateXY) xy = null;
            NotifyPropertyChanged(nameof(XY));
            RaiseEventVisualUpdated();
         }
      }

      public int Y {
         get => element.GetValue("y");
         set {
            element.SetValue("y", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateXY) xy = null;
            NotifyPropertyChanged(nameof(XY));
            RaiseEventVisualUpdated();
         }
      }

      private bool ignoreUpdateXY;
      private string xy;
      public string XY {
         get {
            if (xy == null) xy = $"({X}, {Y})";
            return xy;
         }
         set {
            xy = value;
            var parts = value.Split(new[] { ',', ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            ignoreUpdateXY = true;
            if (parts[0].TryParseInt(out int x)) X = x;
            if (parts[1].TryParseInt(out int y)) Y = y;
            ignoreUpdateXY = false;
         }
      }

      #endregion

      #region Elevation

      public int Elevation {
         get => element.GetValue("elevation");
         set => element.SetValue("elevation", value);
      }

      #endregion

      public IPixelViewModel EventRender { get; protected set; }

      public BaseEventViewModel(ModelArrayElement element, string parentLengthField) => (this.element, this.parentLengthField) = (element, parentLengthField);

      #region Script Error

      private bool hasScriptAddressError;
      public bool HasScriptAddressError { get => hasScriptAddressError; private set => Set(ref hasScriptAddressError, value); }

      private string scriptAddressError;
      public string ScriptAddressError { get => scriptAddressError; private set => Set(ref scriptAddressError, value); }

      protected void UpdateScriptError(int address) {
         if (address == Pointer.NULL) {
            ScriptAddressError = "Event has no script.";
            HasScriptAddressError = true;
            return;
         }

         if (address < 0 || address >= element.Model.Count) {
            ScriptAddressError = "Address is not valid.";
            HasScriptAddressError = true;
            return;
         }

         var run = element.Model.GetNextRun(address);
         if (run.Start != address || (run is not XSERun && run is not NoInfoRun)) {
            ScriptAddressError = IsValidScriptFreespace(address) ? "Freespace found at that address." : "No script found at that address.";
            HasScriptAddressError = true;
            return;
         }

         HasScriptAddressError = false;
         ScriptAddressError = string.Empty;
      }

      protected bool IsValidScriptFreespace(int address) {
         const int MinLength = 10;
         if (address == Pointer.NULL) return true;
         if (address < 0 || address >= element.Model.Count - MinLength) return false;
         for (int i = 0; i < MinLength; i++)
            if (element.Model[address + i] != 0xFF)
               return false;
         var run = element.Model.GetNextRun(address);
         var nextRun = element.Model.GetNextRun(address + 1);
         if (address != run.Start) return false;
         return run is NoInfoRun && nextRun.Start > address + MinLength;
      }

      #endregion

      public bool Delete() => DeleteElement(parentLengthField);

      public virtual bool Equals(IEventViewModel other) {
         if (other is not BaseEventViewModel bem) return false;
         return bem.element.Start == element.Start;
      }

      public abstract void Render(IDataModel model, LayoutModel layout);

      protected void RaiseEventVisualUpdated() => EventVisualUpdated.Raise(this);

      protected bool DeleteElement(string parentCountField) {
         var table = element.Table;
         var model = element.Model;
         var token = element.Token;
         var offset = table.ConvertByteOffsetToArrayOffset(element.Start);
         var editCount = table.ElementCount - offset.ElementIndex - 1;
         for (int i = 0; i < editCount; i++) {
            int segmentOffset = 0;
            for (int j = 0; j < table.ElementContent.Count; j++) {
               var source = element.Start + (i + 1) * element.Length + segmentOffset;
               var destination = source - element.Length;
               var length = table.ElementContent[j].Length;
               if (table.ElementContent[j].Type == ElementContentType.Pointer) {
                  model.UpdateArrayPointer(token, table.ElementContent[j], table.ElementContent, offset.ElementIndex, destination, model.ReadPointer(source));
               } else {
                  model.WriteMultiByteValue(destination, length, token, model.ReadMultiByteValue(source, length));
               }
               segmentOffset += length;
            }
         }
         if (table.ElementCount > 1) {
            var shorterTable = table.Append(token, -1);
            model.ObserveRunWritten(token, shorterTable);
         } else {
            foreach (var source in table.PointerSources) {
               model.UpdateArrayPointer(token, null, null, 0, source, Pointer.NULL);
               if (model.GetNextRun(source) is ITableRun parentTable) {
                  var parent = new ModelArrayElement(model, parentTable.Start, 0, () => token, parentTable);
                  parent.SetValue(parentCountField, 0);
               }
            }
            model.ClearFormatAndData(token, table.Start, table.Length);
         }
         return true;
      }

      protected string GetText(int pointer) {
         if (pointer == Pointer.NULL) return null;
         var address = element.Model.ReadPointer(pointer);
         if (address < 0 || address >= element.Model.Count) return null;
         var run = element.Model.GetNextRun(address);
         if (run.Start != address) {
            if (run.Start < address) return null;
            var length = PCSString.ReadString(element.Model, address, true);
            if (run.Start < address + length || length < 1) return null;
            // we can add a PCSRun here
            run = new PCSRun(element.Model, address, length, SortedSpan.One(pointer));
            if (element.Model.GetNextRun(pointer).Start >= pointer + 4) element.Model.ObserveRunWritten(element.Token, new PointerRun(pointer));
            element.Model.ObserveRunWritten(element.Token, run);
         }
         if (run is not PCSRun pcs) {
            var length = PCSString.ReadString(element.Model, address, true);
            element.Model.ClearFormat(element.Token, address, length);
            pcs = new PCSRun(element.Model, address, length, run.PointerSources);
         }
         if (pcs.Length < 1) return string.Empty;
         return pcs.SerializeRun();
      }

      protected int SetText(int pointer, string text, [CallerMemberName] string propertyName = null) {
         if (pointer == Pointer.NULL) return Pointer.NULL;
         var address = element.Model.ReadPointer(pointer);
         if (address < 0 || address >= element.Model.Count) return -1;
         if (element.Model.GetNextRun(address) is not PCSRun pcs) return -1;
         var newRun = pcs.DeserializeRun(text, element.Token, out _, out _);
         element.Model.ObserveRunWritten(element.Token, newRun);
         NotifyPropertyChanged(propertyName);
         return newRun.Start != pcs.Start ? newRun.Start : -1;
      }

      protected string GetAddressText(int address, ref string field) {
         if (field == null) {
            field = $"<{address.ToAddress()}>";
            if (address == Pointer.NULL) field = "<null>";
         }
         return field;
      }

      protected void SetAddressText(string value, ref string field, string fieldName, bool writeFormat) {
         field = value;
         value = field.Trim("<nul> ".ToCharArray());
         element.SetAddress(fieldName, value.TryParseHex(out int result) ? result : Pointer.NULL, writeFormat);
      }

      private static readonly Point[] focalPoints = new[] { new Point(0, 7), new Point(7, 0), new Point(15, 8), new Point(8, 15) };
      public static IPixelViewModel BuildEventRender(short color, bool indentSides = false) {
         var pixels = new short[256];
         
         for (int x = 1; x < 15; x++) {
            for (int y = 1; y < 15; y++) {
               if (((x + y) & 1) != 0) continue;
               if (indentSides && focalPoints.Any(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y) < 4)) continue;
               pixels[y * 16 + x] = color;
               y++;
            }
         }
         return new ReadonlyPixelViewModel(new SpriteFormat(4, 2, 2, default), pixels, transparent: 0);
      }

      public static IPixelViewModel BuildInvisibleEventRender(IPixelViewModel colors) {
         var pixels = new short[colors.PixelData.Length];
         for (int x = 0; x < colors.PixelWidth; x++) {
            for (int y = 0; y < colors.PixelHeight; y++) {
               if (((x + y) & 1) != 0) pixels[y * colors.PixelWidth + x] = colors.Transparent;
               else pixels[y * colors.PixelWidth + x] = colors.PixelData[y * colors.PixelWidth + x];
            }
         }
         return new ReadonlyPixelViewModel(new SpriteFormat(4, colors.PixelWidth / 8, colors.PixelHeight / 8, default), pixels, colors.Transparent);
      }
   }

   public class ObjectEventViewModel : BaseEventViewModel {
      private readonly ScriptParser parser;
      private readonly EventTemplate eventTemplate;
      private readonly BerryInfo berries;
      private readonly Action<int> gotoAddress;

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public int Start => element.Start;

      public int ObjectID {
         get => element.GetValue("id");
         set {
            element.SetValue("id", value);
            NotifyPropertyChanged();
         }
      }

      public int Graphics {
         get => element.GetValue("graphics");
         set {
            element.SetValue("graphics", value);
            RaiseEventVisualUpdated();
            NotifyPropertyChanged();
         }
      }

      private string graphicsText;
      public string GraphicsText {
         get {
            if (graphicsText != null) return graphicsText;
            graphicsText = Graphics.ToString();
            return graphicsText;
         }
         set {
            graphicsText = value;
            if (!graphicsText.TryParseInt(out var result)) return;
            Graphics = result;
         }
      }

      private bool showGraphicsAsText;
      public bool ShowGraphicsAsText {
         get => showGraphicsAsText;
         set {
            Set(ref showGraphicsAsText, value, old => {
               graphicsText = null;
               NotifyPropertyChanged(nameof(GraphicsText));
            });
         }
      }

      /// <summary>
      /// FireRed Only.
      /// Kind is either 0 or 255.
      /// If it's 255, then this is an 'offscreen' object, which is a copy of an object in a connected map.
      /// The trainerType and trainerRangeOrBerryID have the map and bank information, respectively.
      /// </summary>
      public bool HasKind => element.HasField("kind");
      public bool Kind {
         get => element.TryGetValue("kind", out int value) ? value != 0 : false;
         set {
            if (element.HasField("kind")) element.SetValue("kind", value ? 0xFF : 0);
         }
      }

      public int MoveType {
         get => element.GetValue("moveType");
         set {
            element.SetValue("moveType", value);
            FacingOptions.Update(FacingOptions.AllOptions, MoveType);
            RaiseEventVisualUpdated();
            NotifyPropertyChanged();
         }
      }

      #region Range

      public int RangeX {
         get => element.GetValue("range") & 0xF;
         set {
            element.SetValue("range", (RangeY << 4) | value);
            rangeXY = null;
            NotifyPropertyChanged(nameof(RangeXY));
            RaiseEventVisualUpdated();
         }
      }

      public int RangeY {
         get => element.GetValue("range") >> 4;
         set {
            element.SetValue("range", (value << 4) | RangeX);
            rangeXY = null;
            NotifyPropertyChanged(nameof(RangeXY));
            RaiseEventVisualUpdated();
         }
      }

      private string rangeXY;
      public string RangeXY {
         get {
            if (rangeXY == null) rangeXY = $"({RangeX}, {RangeY})";
            return rangeXY;
         }
         set {
            rangeXY = value;
            var parts = value.Split(new[] { ',', ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            if (parts[0].TryParseInt(out int x) && parts[1].TryParseInt(out int y)) element.SetValue("range", (y << 4) | x);
            NotifyPropertyChanged(nameof(RangeX));
            NotifyPropertyChanged(nameof(RangeY));
            NotifyPropertyChanged();
            RaiseEventVisualUpdated();
         }
      }

      #endregion

      public int TrainerType {
         get => element.GetValue("trainerType");
         set {
            element.SetValue("trainerType", value);
            NotifyPropertiesChanged(nameof(ShowBerryContent), nameof(ShowTrainerContent), nameof(ShowRematchTrainerContent), nameof(ShowNoContent));
            NotifyPropertyChanged();
         }
      }

      public int TrainerRangeOrBerryID {
         get => element.GetValue("trainerRangeOrBerryID");
         set {
            element.SetValue("trainerRangeOrBerryID", value);
            RaiseEventVisualUpdated();
            NotifyPropertiesChanged(nameof(ShowBerryContent), nameof(BerryText));
            NotifyPropertyChanged();
         }
      }

      public int ScriptAddress {
         get => element.GetAddress("script");
         set {
            element.SetAddress("script", value, false);
            UpdateScriptError(element.GetAddress("script"));
            NotifyPropertyChanged();
            trainerSprite = null;
            npcTextEditor =
               trainerAfterText = trainerBeforeText = trainerWinText =
               martHelloEditor = martGoodbyeEditor =
               tutorFailedEditor = tutorInfoEditor = tutorSuccessEditor = tutorWhichPokemonEditor =
               tradeFailedText = tradeInitialText = tradeSuccessText = tradeThanksText =
               cryText = sampleLegendClearScript =
               null;
            scriptAddressText =
               martContentText =
               null;
            NotifyPropertiesChanged(
               nameof(ScriptAddressText),
               nameof(ShowItemContents), nameof(ItemContents),
               nameof(ShowNpcText), nameof(NpcTextEditor),
               nameof(ShowTrainerContent), nameof(TrainerClass), nameof(TrainerSprite), nameof(TrainerName), nameof(TrainerBeforeTextEditor), nameof(TrainerAfterTextEditor), nameof(TrainerWinTextEditor), nameof(TrainerTeam),
               nameof(ShowRematchTrainerContent),
               nameof(ShowMartContents), nameof(MartHelloEditor), nameof(MartContent), nameof(MartGoodbyeEditor),
               nameof(ShowTutorContent), nameof(TutorInfoText), nameof(TutorWhichPokemonText), nameof(TutorFailedText), nameof(TutorSucessText), nameof(TutorNumber),
               nameof(ShowTradeContent), nameof(TradeFailedEditor), nameof(TradeIndex), nameof(TradeInitialEditor), nameof(TradeSuccessEditor), nameof(TradeThanksEditor), nameof(TradeWrongSpeciesEditor),
               nameof(ShowLegendaryContent), nameof(Level), nameof(LegendaryFlagText), nameof(HasCryText), nameof(CryEditor), nameof(SampleLegendClearScript),
               nameof(ShowBerryContent), nameof(BerryText), nameof(CanCreateScript));
         }
      }

      public void GotoScript() {
         SetDestinationFormat();
         gotoAddress(ScriptAddress);
      }
      public bool CanGotoScript => 0 <= ScriptAddress && ScriptAddress < element.Model.Count;

      public bool CanCreateScript => IsValidScriptFreespace(ScriptAddress) || ScriptAddress == Pointer.NULL;
      public void CreateScript() {
         int start;
         if (ScriptAddress != Pointer.NULL && IsValidScriptFreespace(ScriptAddress)) start = ScriptAddress;
         else start = element.Model.FindFreeSpace(element.Model.FreeSpaceStart, 0x10);
         Token.ChangeData(element.Model, start, new byte[] { 0x6A, 0x5A, 0x6C, 0x02 }); // lock, faceplayer, release, end
         ScriptAddress = start;
         SetDestinationFormat();
         gotoAddress(start);
      }

      private string scriptAddressText;
      public string ScriptAddressText {
         get {
            if (scriptAddressText != null) return scriptAddressText;
            var value = element.GetAddress("script");
            return GetAddressText(value, ref scriptAddressText);
         }
         set {
            SetAddressText(value, ref scriptAddressText, "script", false);
            NotifyPropertyChanged();
            trainerSprite = null;
            npcTextEditor =
               trainerAfterText = trainerBeforeText = trainerWinText =
               martHelloEditor = martGoodbyeEditor =
               tutorFailedEditor = tutorInfoEditor = tutorSuccessEditor = tutorWhichPokemonEditor =
               tradeFailedText = tradeInitialText = tradeSuccessText = tradeThanksText =
               cryText = sampleLegendClearScript =
               null;
            martContentText =
               null;

            NotifyPropertiesChanged(
               nameof(ScriptAddress),
               nameof(ShowItemContents), nameof(ItemContents),
               nameof(ShowNpcText), nameof(NpcTextEditor),
               nameof(ShowTrainerContent), nameof(TrainerClass), nameof(TrainerSprite), nameof(TrainerName), nameof(TrainerBeforeTextEditor), nameof(TrainerAfterTextEditor), nameof(TrainerWinTextEditor), nameof(TrainerTeam),
               nameof(ShowRematchTrainerContent),
               nameof(ShowMartContents), nameof(MartHelloEditor), nameof(MartContent), nameof(MartGoodbyeEditor),
               nameof(ShowTutorContent), nameof(TutorInfoText), nameof(TutorWhichPokemonText), nameof(TutorFailedText), nameof(TutorSucessText), nameof(TutorNumber),
               nameof(ShowTradeContent), nameof(TradeFailedEditor), nameof(TradeIndex), nameof(TradeInitialEditor), nameof(TradeSuccessEditor), nameof(TradeThanksEditor), nameof(TradeWrongSpeciesEditor),
               nameof(ShowLegendaryContent), nameof(Level), nameof(LegendaryFlagText), nameof(HasCryText), nameof(CryEditor), nameof(SampleLegendClearScript),
               nameof(ShowBerryContent), nameof(BerryText), nameof(CanCreateScript));
            UpdateScriptError(ScriptAddress);
         }
      }

      #region Flag

      public int Flag {
         get => element.GetValue("flag");
         set {
            element.SetValue("flag", value);
            NotifyPropertyChanged();
            flagText = null;
            NotifyPropertyChanged(nameof(FlagText), nameof(SampleLegendClearScript));
         }
      }

      string flagText;
      public string FlagText {
         get {
            if (flagText == null) flagText = element.GetValue("flag").ToString("X4");
            return flagText;
         }
         set {
            flagText = value;
            element.SetValue("flag", value.TryParseHex(out int result) ? result : 0);
            NotifyPropertyChanged();
            NotifyPropertiesChanged(nameof(Flag), nameof(SampleLegendClearScript), nameof(CanGenerateNewFlag));
         }
      }

      public bool CanGenerateNewFlag => Flag == 0;

      public void GenerateNewFlag() {
         Flag = eventTemplate.FindNextUnusedFlag();
         NotifyPropertiesChanged(nameof(FlagText), nameof(CanGenerateNewFlag));
      }

      #endregion

      public int Padding {
         get => element.TryGetValue("padding", out var value) ? value : 0;
         set {
            element.SetValue("padding", value);
            NotifyPropertyChanged();
         }
      }

      public IPixelViewModel DefaultOW { get; }
      public ObservableCollection<VisualComboOption> Options { get; } = new();
      public FilteringComboOptions FacingOptions { get; } = new();
      public ObservableCollection<string> ClassOptions { get; } = new();
      public FilteringComboOptions ItemOptions { get; } = new();

      #region Extended Properties

      // For certain simple events (npcs, trainers, items, signposts),
      // We can provide an enriched editing experience in the event panel.
      // These are the 'show' properties for those controls.

      public bool ShowItemContents => EventTemplate.GetItemAddress(element.Model, this) != Pointer.NULL;

      public int ItemContents {
         get {
            var itemAddress = EventTemplate.GetItemAddress(element.Model, this);
            if (itemAddress == Pointer.NULL) return -1;
            return element.Model.ReadMultiByteValue(itemAddress, 2);
         }
         set {
            var itemAddress = EventTemplate.GetItemAddress(element.Model, this);
            if (itemAddress == Pointer.NULL) return;
            element.Model.WriteMultiByteValue(itemAddress, 2, element.Token, value);
            ItemOptions.Update(ItemOptions.AllOptions, value);
            NotifyPropertyChanged();
         }
      }

      #region Npc Content

      public bool ShowNpcText => EventTemplate.GetNPCTextPointer(element.Model, this) != Pointer.NULL;

      private TextEditorViewModel npcTextEditor;
      public TextEditorViewModel NpcTextEditor => CreateTextEditor(ref npcTextEditor, () => EventTemplate.GetNPCTextPointer(element.Model, this));

      #endregion

      #region Trainer Content

      public FilteringComboOptions TrainerOptions { get; } = new();

      public bool ShowTrainerContent => EventTemplate.GetTrainerContent(element.Model, this) != null && TrainerType != 0;

      public int TrainerClass {
         get {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return -1;
            return element.Model[trainerContent.TrainerClassAddress];
         }
         set {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            element.Token.ChangeData(element.Model, trainerContent.TrainerClassAddress, (byte)value);

            var options = TrainerOptions.AllOptions.ToList();
            options[trainerContent.TrainerIndex] = CreateOption(trainerContent.TrainerIndex, value, TrainerName);
            TrainerOptions.Update(options, trainerContent.TrainerIndex);
         }
      }

      private IPixelViewModel trainerSprite;
      public IPixelViewModel TrainerSprite {
         get {
            if (trainerSprite != null) return trainerSprite;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            var spriteIndex = element.Model[trainerContent.TrainerClassAddress + 2];
            var spriteAddress = element.Model.GetTableModel(HardcodeTablesModel.TrainerSpritesName)[spriteIndex].GetAddress("sprite");
            var spriteRun = element.Model.GetNextRun(spriteAddress) as ISpriteRun;
            return trainerSprite = ReadonlyPixelViewModel.Create(element.Model, spriteRun, true, .5);
         }
      }

      private string trainerName;
      public string TrainerName {
         get {
            if (trainerName != null) return trainerName;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            var text = element.Model.TextConverter.Convert(element.Model, trainerContent.TrainerNameAddress, 12);
            return trainerName = text.Trim('"');
         }
         set {
            trainerName = value;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var bytes = element.Model.TextConverter.Convert(value, out _);
            while (bytes.Count > 12) {
               bytes.RemoveAt(bytes.Count - 1);
               bytes[bytes.Count - 1] = 0xFF;
            }
            while (bytes.Count < 12) bytes.Add(0);
            element.Token.ChangeData(element.Model, trainerContent.TrainerNameAddress, bytes);
            NotifyPropertyChanged();
            var options = TrainerOptions.AllOptions.ToList();
            options[trainerContent.TrainerIndex] = CreateOption(trainerContent.TrainerIndex, element.Model[trainerContent.TrainerClassAddress], value);
            TrainerOptions.Update(options, trainerContent.TrainerIndex);
         }
      }

      public void RefreshTrainerOptions() {
         var trainerTable = element.Model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var trainers = element.Model.GetOptions(HardcodeTablesModel.TrainerTableName);
         if (trainerTable == null) return;

         var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
         var options = new List<ComboOption>();
         for (int i = 0; i < trainers.Count; i++) {
            options.Add(CreateOption(i, trainerTable[i].GetValue(1), trainers[i]));
         }
         TrainerOptions.Update(options, trainerContent?.TrainerIndex ?? 0);
      }

      private TextEditorViewModel trainerBeforeText, trainerWinText, trainerAfterText;
      public TextEditorViewModel TrainerBeforeTextEditor => CreateTextEditor(ref trainerBeforeText, () => EventTemplate.GetTrainerContent(element.Model, this)?.BeforeTextPointer);
      public TextEditorViewModel TrainerWinTextEditor => CreateTextEditor(ref trainerWinText, () => EventTemplate.GetTrainerContent(element.Model, this)?.WinTextPointer);
      public TextEditorViewModel TrainerAfterTextEditor => CreateTextEditor(ref trainerAfterText, () => EventTemplate.GetTrainerContent(element.Model, this)?.AfterTextPointer);

      private string teamText;
      public string TrainerTeam {
         get {
            if (teamText != null) return teamText;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            var address = element.Model.ReadPointer(trainerContent.TeamPointer);
            if (address < 0 || address >= element.Model.Count) return null;
            if (element.Model.GetNextRun(address) is not TrainerPokemonTeamRun run) return null;
            if (run.Start != address) return null;
            if (TeamVisualizations.Count == 0) UpdateTeamVisualizations(run);
            return teamText = run.SerializeRun();
         }
         set {
            teamText = value;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var address = element.Model.ReadPointer(trainerContent.TeamPointer);
            if (address < 0 || address >= element.Model.Count) return;
            if (element.Model.GetNextRun(address) is not TrainerPokemonTeamRun run) return;
            if (run.Start != address) return;
            var newRun = run.DeserializeRun(value, element.Token, false, false, out _);
            element.Model.ObserveRunWritten(element.Token, newRun);
            if (newRun.Start != run.Start) DataMoved.Raise(this, new("Trainer Team", newRun.Start));
            UpdateTeamVisualizations(newRun);
            NotifyPropertyChanged();
         }
      }

      public ObservableCollection<IPixelViewModel> TeamVisualizations { get; } = new();

      private void UpdateTeamVisualizations(TrainerPokemonTeamRun team) {
         TeamVisualizations.Clear();
         foreach (var vis in team.Visualizations) {
            TeamVisualizations.Add(vis);
         }
      }

      private StubCommand openTrainerData;
      public ICommand OpenTrainerData => StubCommand(ref openTrainerData, () => {
         var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
         if (trainerContent == null) return;
         gotoAddress(trainerContent.TrainerClassAddress - 1);
      });

      public static ComboOption CreateOption(IReadOnlyList<string> classOptions, int index, int trainerClass, string name) => new($"{index} - {classOptions[trainerClass.LimitToRange(0, classOptions.Count - 1)]} {name}", index);

      private ComboOption CreateOption(int index, int trainerClass, string name) => CreateOption(ClassOptions, index, trainerClass, name);

      public IReadOnlyList<AutocompleteItem> GetTrainerAutocomplete(string line, int lineIndex, int characterIndex) {
         var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
         if (trainerContent == null) return null;
         var address = element.Model.ReadPointer(trainerContent.TeamPointer);
         if (address < 0 || address >= element.Model.Count) return null;
         if (element.Model.GetNextRun(address) is not TrainerPokemonTeamRun run) return null;
         if (run.Start != address) return null;
         return run.GetAutoCompleteOptions(line, lineIndex, characterIndex);
      }

      #endregion

      #region Rematch Trainer Content

      public bool ShowRematchTrainerContent {
         get {
            if (TrainerType == 0) return false;
            if (element.Model.GetTableModel(HardcodeTablesModel.TrainerTableName) is not ModelTable trainers) return false;
            var content = EventTemplate.GetRematchTrainerContent(element.Model, parser, this);
            if (content == null) return false;
            var table = element.Model.GetTableModel(HardcodeTablesModel.RematchTable) ?? element.Model.GetTableModel(HardcodeTablesModel.RematchTableRSE);
            if (table == null) return false;
            var rematchIndex = table.Count.Range().FirstOrDefault(i => table[i].GetValue(0) == content.TrainerID, -1);
            if (rematchIndex == -1) return false;
            TrainerTeams.Clear();
            for (int i = 0; i < table[rematchIndex].Table.ElementContent.Count; i++) {
               if (table[rematchIndex].Table.ElementContent[i] is not ArrayRunEnumSegment seg) continue;
               if (seg.EnumName != HardcodeTablesModel.TrainerTableName) continue;
               var trainerID = table[rematchIndex].GetValue(i);
               if (!trainerID.InRange(1, trainers.Count)) continue;
               TrainerTeams.Add(new(element.Model, trainerID, () => element.Token));
            }
            return true;
         }
      }

      public ObservableCollection<TrainerTeamViewModel> TrainerTeams { get; } = new();

      public void GotoRematches() {
         if (element.Model.GetTableModel(HardcodeTablesModel.TrainerTableName) is not ModelTable trainers) return;
         var content = EventTemplate.GetRematchTrainerContent(element.Model, parser, this);
         if (content == null) return;
         var table = element.Model.GetTableModel(HardcodeTablesModel.RematchTable) ?? element.Model.GetTableModel(HardcodeTablesModel.RematchTableRSE);
         if (table == null) return;
         var rematchIndex = table.Count.Range().FirstOrDefault(i => table[i].GetValue(0) == content.TrainerID, -1);
         if (rematchIndex == -1) return;
         gotoAddress(table[rematchIndex].Start);
      }

      public void ReplaceRematchScript() {
         // TODO replace script with simple trainer script (keep before/during/after battle text if we can)
         var content = EventTemplate.GetRematchTrainerContent(element.Model, parser, this);
         string before = "<auto>", win = "<auto", after = "<auto>";
         if (content.BeforeTextPointer != Pointer.NULL) before = $"<{content.BeforeTextPointer:X6}>";
         if (content.WinTextPointer != Pointer.NULL) win = $"<{content.WinTextPointer:X6}>";
         if (content.AfterTextPointer != Pointer.NULL) after = $"<{content.AfterTextPointer:X6}>";

         /*
            trainerbattle 00 [trainerID] 0 <before> <during>
            loadpointer 0 <after>
            callstd 6
            end
         */
         var nl = Environment.NewLine;
         var script = new StringBuilder();
         script.AppendLine($"trainerbattle 0 {content.TrainerID} 0 {before} {win}");
         if (before == "<auto>") script.AppendLine($"{{{nl}Let's fight!{nl}}}");
         if (win == "<auto>") script.AppendLine($"{{{nl}You win!{nl}}}");
         script.AppendLine($"loadpointer 0 {after}");
         if (after == "<auto>") script.AppendLine($"{{{nl}Later!{nl}}}");
         script.AppendLine("callstd 6");
         script.AppendLine("end");
         var scriptText = script.ToString();
         var compiled = parser.CompileWithoutErrors(Token, element.Model, ScriptAddress, ref scriptText);
         Token.ChangeData(element.Model, ScriptAddress, compiled);
         parser.FormatScript<XSERun>(Token, element.Model, ScriptAddress);

         NotifyPropertiesChanged(nameof(ShowRematchTrainerContent),
            nameof(ShowTrainerContent), nameof(TrainerClass), nameof(TrainerSprite), nameof(TrainerName), nameof(TrainerBeforeTextEditor), nameof(TrainerAfterTextEditor), nameof(TrainerWinTextEditor), nameof(TrainerTeam));
      }

      #endregion

      #region Mart Content

      private Lazy<MartEventContent> martContent;

      public bool ShowMartContents => martContent.Value != null;

      private string martContentText;

      public string MartContent {
         get {
            if (martContentText != null) return martContentText;
            if (martContent.Value == null) return null;
            var martStart = element.Model.ReadPointer(martContent.Value.MartPointer);
            if (element.Model.GetNextRun(martStart) is not IStreamRun stream) return null;
            var lines = stream.SerializeRun().SplitLines().Select(line => line.Trim('"'));
            return martContentText = Environment.NewLine.Join(lines);
         }
         set {
            martContentText = value;
            if (martContent.Value == null) return;
            var martStart = element.Model.ReadPointer(martContent.Value.MartPointer);
            if (element.Model.GetNextRun(martStart) is not IStreamRun stream) return;
            var newStream = stream.DeserializeRun(value, Token, out var _, out var _);
            element.Model.ObserveRunWritten(Token, newStream);
            if (newStream.Start != stream.Start) DataMoved.Raise(this, new("Mart", newStream.Start));
         }
      }

      private TextEditorViewModel martHelloEditor, martGoodbyeEditor;
      public TextEditorViewModel MartHelloEditor => CreateTextEditor(ref martHelloEditor, () => martContent.Value?.HelloPointer);
      public TextEditorViewModel MartGoodbyeEditor => CreateTextEditor(ref martGoodbyeEditor, () => martContent.Value?.GoodbyePointer);

      public IReadOnlyList<AutocompleteItem> GetMartAutocomplete(string line, int lineIndex, int characterIndex) {
         if (martContent.Value == null) return null;
         var martStart = element.Model.ReadPointer(martContent.Value.MartPointer);
         if (element.Model.GetNextRun(martStart) is not IStreamRun stream) return null;
         return stream.GetAutoCompleteOptions(line, lineIndex, characterIndex);
      }

      #endregion

      #region Tutor Content

      private Lazy<TutorEventContent> tutorContent;

      public bool ShowTutorContent {
         get {
            var content = tutorContent.Value;
            if (content != null && TutorOptions .AllOptions == null) {
               TutorOptions.Update(ComboOption.Convert(element.Model.GetOptions(HardcodeTablesModel.MoveTutors)), TutorNumber);
               TutorOptions.Bind(nameof(TutorOptions.SelectedIndex), (sender, e) => TutorNumber = TutorOptions.SelectedIndex);
            }
            return tutorContent.Value != null;
         }
      }

      private TextEditorViewModel tutorInfoEditor, tutorWhichPokemonEditor, tutorFailedEditor, tutorSuccessEditor;
      public TextEditorViewModel TutorInfoText => CreateTextEditor(ref tutorInfoEditor, () => tutorContent.Value?.InfoPointer);
      public TextEditorViewModel TutorWhichPokemonText => CreateTextEditor(ref tutorWhichPokemonEditor , () => tutorContent.Value?.WhichPokemonPointer);
      public TextEditorViewModel TutorFailedText => CreateTextEditor(ref tutorFailedEditor, () => tutorContent.Value?.FailedPointer);
      public TextEditorViewModel TutorSucessText => CreateTextEditor(ref tutorSuccessEditor, () => tutorContent.Value?.SuccessPointer);

      public int TutorNumber {
         get {
            if (tutorContent.Value == null) return -1;
            return element.Model.ReadMultiByteValue(tutorContent.Value.TutorAddress, 2);
         }
         set {
            if (tutorContent.Value == null) return;
            element.Model.WriteMultiByteValue(tutorContent.Value.TutorAddress, 2, Token, value);
         }
      }

      public FilteringComboOptions TutorOptions { get; } = new();

      public void GotoTutors() => gotoAddress(element.Model.GetTableModel(HardcodeTablesModel.MoveTutors)[TutorNumber].Start);

      #endregion

      #region Trade Content

      private Lazy<TradeEventContent> tradeContent;

      public FilteringComboOptions TradeOptions { get; } = new();

      public bool ShowTradeContent {
         get {
            var content = tradeContent.Value;
            if (content != null && TradeOptions.AllOptions == null) {
               var pokenames = element.Model.GetOptions(HardcodeTablesModel.PokemonNameTable);
               var options = new List<string>();
               foreach (var trade in element.Model.GetTableModel(HardcodeTablesModel.TradeTable)) {
                  if (!trade.TryGetValue("receive", out int receive) || !trade.TryGetValue("give", out int give)) {
                     options.Add(options.Count.ToString());
                  } else {
                     options.Add($"{pokenames[give]} -> {pokenames[receive]}");
                  }
               }
               TradeOptions.Update(ComboOption.Convert(options), TradeIndex);
               TradeOptions.Bind(nameof(TradeOptions.SelectedIndex), (sender, e) => TradeIndex = TradeOptions.SelectedIndex);
            }
            return tradeContent.Value != null;
         }
      }

      private TextEditorViewModel tradeInitialText, tradeThanksText, tradeSuccessText, tradeFailedText, tradeWrongSpeciesText;
      public TextEditorViewModel TradeInitialEditor => CreateTextEditor(ref tradeInitialText, () => tradeContent.Value?.InfoPointer);
      public TextEditorViewModel TradeThanksEditor => CreateTextEditor(ref tradeThanksText, () => tradeContent.Value?.ThanksPointer);
      public TextEditorViewModel TradeSuccessEditor => CreateTextEditor(ref tradeSuccessText, () => tradeContent.Value?.SuccessPointer);
      public TextEditorViewModel TradeFailedEditor => CreateTextEditor(ref tradeFailedText, () => tradeContent.Value?.FailedPointer);
      public TextEditorViewModel TradeWrongSpeciesEditor => CreateTextEditor(ref tradeWrongSpeciesText, () => tradeContent.Value?.WrongSpeciesPointer);

      public int TradeIndex {
         get {
            if (tradeContent.Value == null) return -1;
            return element.Model.ReadMultiByteValue(tradeContent.Value.TradeAddress, 2);
         }
         set {
            if (tradeContent.Value == null) return;
            element.Model.WriteMultiByteValue(tradeContent.Value.TradeAddress, 2, Token, value);
         }
      }

      public void GotoTrades() => gotoAddress(element.Model.GetTableModel(HardcodeTablesModel.TradeTable)[TradeIndex].Start);

      #endregion

      #region Legendary Content

      private Lazy<LegendaryEventContent> legendaryContent;

      public bool ShowLegendaryContent {
         get {
            var content = legendaryContent.Value;
            if (content != null && PokemonOptions.AllOptions == null) {
               var options = ComboOption.Convert(element.Model.GetOptions(HardcodeTablesModel.PokemonNameTable));
               PokemonOptions.Update(options, element.Model.ReadMultiByteValue(content.SetWildBattle + 1, 2));
               PokemonOptions.Bind(nameof(PokemonOptions.SelectedIndex), (sender, e) => {
                  element.Model.WriteMultiByteValue(content.Cry + 1, 2, element.Token, PokemonOptions.SelectedIndex);
                  element.Model.WriteMultiByteValue(content.SetWildBattle + 1, 2, element.Token, PokemonOptions.SelectedIndex);
                  foreach (var buffer in content.BufferPokemon) element.Model.WriteMultiByteValue(buffer + 2, 2, element.Token, PokemonOptions.SelectedIndex);
               });
            }
            if (content != null && HoldItemOptions.AllOptions == null) {
               var options = ComboOption.Convert(element.Model.GetOptions(HardcodeTablesModel.ItemsTableName));
               HoldItemOptions.Update(options, element.Model.ReadMultiByteValue(content.SetWildBattle + 4, 2));
               HoldItemOptions.Bind(nameof(HoldItemOptions.SelectedIndex), (sender, e) => element.Model.WriteMultiByteValue(content.SetWildBattle + 4, 2, element.Token, HoldItemOptions.SelectedIndex));
            }
            return content != null;
         }
      }

      public FilteringComboOptions PokemonOptions { get; } = new();
      public void GotoPokemon() => gotoAddress(element.Model.GetTableModel(HardcodeTablesModel.PokemonNameTable)[PokemonOptions.SelectedIndex].Start);
      public int Level {
         get => legendaryContent.Value == null ? -1 : element.Model[legendaryContent.Value.SetWildBattle + 3];
         set {
            if (legendaryContent.Value == null) return;
            element.Token.ChangeData(element.Model, legendaryContent.Value.SetWildBattle + 3, (byte)value);
         }
      }
      public FilteringComboOptions HoldItemOptions { get; } = new();
      public void GotoHoldItem() => gotoAddress(element.Model.GetTableModel(HardcodeTablesModel.ItemsTableName)[HoldItemOptions.SelectedIndex].Start);
      private string legendaryFlagText;
      public string LegendaryFlagText {
         get {
            if (legendaryContent.Value == null) return null;
            if (legendaryFlagText == null) legendaryFlagText = element.Model.ReadMultiByteValue(legendaryContent.Value.SetFlag[0] + 1, 2).ToString("X4");
            return legendaryFlagText;
         }
         set {
            if (legendaryContent.Value == null) return;
            legendaryFlagText = value;
            foreach (var flag in legendaryContent.Value.SetFlag) {
               element.Model.WriteMultiByteValue(flag + 1, 2, element.Token, value.TryParseHex(out int result) ? result : 0);
            }
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(SampleLegendClearScript));
         }
      }
      public bool HasCryText => (legendaryContent.Value?.CryTextPointer ?? Pointer.NULL) != Pointer.NULL;

      private TextEditorViewModel cryText;
      public TextEditorViewModel CryEditor => CreateTextEditor(ref cryText, () => legendaryContent.Value?.CryTextPointer);

      private TextEditorViewModel sampleLegendClearScript;
      public TextEditorViewModel SampleLegendClearScript {
         get {
            var script = @$"  # somewhere in your script
  if.flag.clear.call 0x{LegendaryFlagText} <show>

  # add this at the bottom
show:
  clearflag 0x{FlagText}
  return";
            if (sampleLegendClearScript == null) {
               sampleLegendClearScript = new TextEditorViewModel() { LineCommentHeader = "#" };
               sampleLegendClearScript.Keywords.Add("if.flag.clear.call");
               sampleLegendClearScript.Keywords.Add("end");
               sampleLegendClearScript.Keywords.Add("clearflag");
               sampleLegendClearScript.Keywords.Add("return");
            }
            sampleLegendClearScript.Content = script;
            return sampleLegendClearScript;
         }
      }

      #endregion

      #region Berry Content

      public bool ShowBerryContent => TrainerType == 0 && TrainerRangeOrBerryID != 0;

      public string BerryText {
         get {
            if (berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out BerrySpot spot)) {
               if (spot.BerryID >= 0 && spot.BerryID < berries.BerryOptions.Count) {
                  return berries.BerryOptions[spot.BerryID];
               }
            }
            return "Unknown";
         }
      }

      public void GotoBerryCode() {
         if (berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out BerrySpot spot)) {
            gotoAddress(spot.Address);
         }
      }

      #endregion

      #region NoContent

      public bool ShowNoContent => ScriptAddress > 0 && !(
         ShowItemContents ||
         ShowNpcText ||
         ShowTrainerContent ||
         ShowRematchTrainerContent ||
         ShowMartContents ||
         ShowTutorContent ||
         ShowTradeContent ||
         ShowLegendaryContent ||
         ShowBerryContent
      );

      #endregion

      private string GetText(ref string cache, int? pointer) {
         if (cache != null) return cache;
         if (pointer == null) return null;
         return cache = GetText((int)pointer);
      }

      private void SetText(ref string cache, int? pointer, string value, string type, [CallerMemberName] string propertyName = null) {
         cache = value;
         if (pointer == null) return;
         var newStart = base.SetText((int)pointer, value, propertyName);
         if (newStart != -1) DataMoved.Raise(this, new(type, newStart));
      }

      private string GetText(int? pointer) {
         if (pointer == null) return null;
         return base.GetText((int)pointer);
      }

      private void SetText(int? pointer, string value, string type, [CallerMemberName] string propertyName = null) {
         if (pointer == null) return;
         var newStart = base.SetText((int)pointer, value, propertyName);
         if (newStart != -1) DataMoved.Raise(this, new(type, newStart));
      }

      #endregion

      public ObjectEventViewModel(ScriptParser parser, Action<int> gotoAddress, ModelArrayElement objectEvent, EventTemplate eventTemplate, IReadOnlyList<IPixelViewModel> sprites, IPixelViewModel defaultSprite, BerryInfo berries) : base(objectEvent, "objectCount") {
         this.parser = parser;
         this.gotoAddress = gotoAddress;
         this.eventTemplate = eventTemplate;
         this.berries = berries;
         for (int i = 0; i < sprites.Count; i++) Options.Add(VisualComboOption.CreateFromSprite(i.ToString(), sprites[i].PixelData, sprites[i].PixelWidth, i, 2, true));
         DefaultOW = defaultSprite;
         ShowGraphicsAsText = Graphics >= Options.Count;
         objectEvent.Model.TryGetList("FacingOptions", out var list);
         FacingOptions.Update(ComboOption.Convert(list), MoveType);
         FacingOptions.Bind(nameof(FacingOptions.SelectedIndex), (sender, e) => MoveType = FacingOptions.ModelValue);
         foreach (var item in objectEvent.Model.GetOptions(HardcodeTablesModel.TrainerClassNamesTable)) ClassOptions.Add(item);
         ItemOptions.Update(ComboOption.Convert(objectEvent.Model.GetOptions(HardcodeTablesModel.ItemsTableName)), ItemContents);
         ItemOptions.Bind(nameof(ItemOptions.SelectedIndex), (sender, e) => ItemContents = ItemOptions.ModelValue);

         RefreshTrainerOptions();
         TrainerOptions.Bind(nameof(TrainerOptions.SelectedIndex), (options, args) => {
            this.eventTemplate.UseTrainerFlag(TrainerOptions.SelectedIndex);
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            element.Model.WriteMultiByteValue(trainerContent.TrainerIndexAddress, 2, () => element.Token, TrainerOptions.SelectedIndex);
            TeamVisualizations.Clear();
            trainerSprite = null;
            trainerName = teamText = null;
            trainerBeforeText = trainerWinText = trainerAfterText = null;
            NotifyPropertiesChanged(nameof(TrainerSprite), nameof(TrainerName), nameof(TrainerBeforeTextEditor), nameof(TrainerWinTextEditor), nameof(TrainerAfterTextEditor), nameof(TrainerTeam), nameof(TrainerClass));
         });

         tutorContent = new Lazy<TutorEventContent>(() => EventTemplate.GetTutorContent(element.Model, parser, this));
         martContent = new Lazy<MartEventContent>(() => EventTemplate.GetMartContent(element.Model, parser, this));
         tradeContent = new Lazy<TradeEventContent>(() => EventTemplate.GetTradeContent(element.Model, parser, this.ScriptAddress));
         legendaryContent = new Lazy<LegendaryEventContent>(() => EventTemplate.GetLegendaryEventContent(element.Model, parser, this));

         UpdateScriptError(ScriptAddress);
      }

      public override int TopOffset => 16 - (EventRender?.PixelHeight ?? 0);
      public override int LeftOffset => (16 - (EventRender?.PixelWidth ?? 0)) / 2;

      public override void Render(IDataModel model, LayoutModel layout) {
         var ows = model.GetTable(HardcodeTablesModel.OverworldSprites);
         var owTable = ows == null ? null : new ModelTable(model, ows.Start);
         var facing = MoveType switch {
            7 => 1,
            9 => 2,
            10 => 3,
            76 => 76, // invisible
            _ => 0,
         };
         EventRender = Render(model, owTable, DefaultOW, Graphics, facing);
         NotifyPropertyChanged(nameof(EventRender));
      }

      /// <param name="facing">(0, 1, 2, 3) = (down, up, left, right)</param>
      public static IPixelViewModel Render(IDataModel model, ModelTable owTable, IPixelViewModel defaultOW, int index, int facing) {
         if (owTable == null || index >= owTable.Count) return defaultOW;
         var element = owTable[index];
         var data = element.GetSubTable("data")[0];
         var sprites = data.GetSubTable("sprites");
         if (sprites == null) return defaultOW;
         bool invisible = facing == 76;
         bool flip = facing == 3;
         if (facing == 3) facing = 2;
         if (facing >= sprites.Count) facing = 0;
         var graphicsAddress = sprites.Run.Start;
         var pointerAddress = data.Start;
         var graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         var paletteRun = graphicsRun.FindRelatedPalettes(model, pointerAddress).FirstOrDefault();
         if (facing != -1) {
            var sprite = sprites[facing];
            graphicsAddress = sprite.GetAddress("sprite");
            graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         }
         if (graphicsRun == null) return defaultOW;
         if (paletteRun == null) return defaultOW;
         var ow = ReadonlyPixelViewModel.Create(model, graphicsRun, paletteRun, true);
         if (invisible) ow = BuildInvisibleEventRender(ow);
         if (flip) ow = ow.ReflectX();
         return ow;
      }

      public void ClearUnused() {
         element.SetValue(2, 0);
         element.SetValue(12, 0);
      }

      private static readonly Dictionary<int, Point> facingVectors = new() {
         [7] = new(0, -1),
         [8] = new(0, 1),
         [9] = new(-1, 0),
         [10] = new(1, 0),
      };
      public bool ShouldHighlight(int x, int y) {
         if (TrainerType != 0 && facingVectors.TryGetValue(MoveType, out var vector)) {
            var (xx, yy) = (X, Y);
            var range = TrainerRangeOrBerryID;
            if (Math.Sign(y - yy) == vector.Y && Math.Sign(x - xx) == vector.X && Math.Abs(y - yy) <= range && Math.Abs(x - xx) <= range) {
               return true;
            }
         } else {
            if (!MoveType.IsAny(2, 3, 4, 5, 6)) return false;
            if (Math.Abs(x - X) <= RangeX && Math.Abs(y - Y) <= RangeY) return true;
         }
         return false;
      }

      private void SetDestinationFormat() {
         var existingRun = element.Model.GetNextRun(ScriptAddress);
         if (existingRun.Start == ScriptAddress && existingRun is NoInfoRun) {
            element.Model.ObserveRunWritten(Token, new XSERun(ScriptAddress));
         }
      }

      private TextEditorViewModel CreateTextEditor(ref TextEditorViewModel field, Func<int?> source, [CallerMemberName] string propertyName = null) {
         if (field != null) return field;
         var text = GetText(source());
         if (text == null) return null;
         var newEditor = new TextEditorViewModel(false) { Content = text };
         newEditor.Bind(nameof(TextEditorViewModel.Content), (editor, e) => {
            SetText(source(), editor.Content, "Text", propertyName);
            UpdateTextErrorContent(editor);
         });
         return field = UpdateTextErrorContent(newEditor);
      }

      private TextEditorViewModel UpdateTextErrorContent(TextEditorViewModel editor) {
         editor.ErrorLocations.Clear();
         editor.ErrorLocations.AddRange(Element.Model.TextConverter.GetOverflow(editor.Content, CodeBody.MaxEventTextWidth));
         return editor;
      }
   }

   public class WarpEventViewModel : BaseEventViewModel {
      private readonly Action<int, int> gotoMap;

      public WarpEventViewModel(ModelArrayElement warpEvent, Action<int, int> gotoMap) : base(warpEvent, "warpCount") => this.gotoMap = gotoMap;

      public int WarpID {
         get => element.GetValue("warpID") + 1;
         set => element.SetValue("warpID", value - 1);
      }

      #region Bank/Map

      public int Bank {
         get => element.GetValue("bank");
         set {
            element.SetValue("bank", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateBankMap) bankMap = null;
            NotifyPropertiesChanged(nameof(BankMap), nameof(TargetMapName), nameof(CanGotoBankMap));
         }
      }

      public int Map {
         get => element.GetValue("map");
         set {
            element.SetValue("map", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateBankMap) bankMap = null;
            NotifyPropertiesChanged(nameof(BankMap), nameof(TargetMapName), nameof(CanGotoBankMap));
         }
      }

      private bool ignoreUpdateBankMap;
      private string bankMap;
      public string BankMap {
         get {
            if (bankMap == null) bankMap = $"({Bank}, {Map})";
            return bankMap;
         }
         set {
            bankMap = value;
            var parts = value.Split(new[] { ',', ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            ignoreUpdateBankMap = true;
            if (parts[0].TryParseInt(out int bank)) Bank = bank;
            if (parts[1].TryParseInt(out int map)) Map = map;
            ignoreUpdateBankMap = false;
            NotifyPropertyChanged(nameof(CanGotoBankMap));
         }
      }

      public string TargetMapName => BlockMapViewModel.MapIDToText(element.Model, Bank, Map);

      public WarpEventModel WarpModel => new WarpEventModel(element);

      public bool CanGotoBankMap => AllMapsModel.Create(element.Model) is AllMapsModel maps && maps.Count > Bank && maps[Bank].Count > Map;
      public void GotoBankMap() => gotoMap(Bank, Map);

      #endregion

      public override void Render(IDataModel model, LayoutModel layout) {
         if (WarpIsOnWarpableBlock(model, layout)) {
            EventRender = BuildEventRender(UncompressedPaletteColor.Pack(0, 0, 31));
         } else {
            EventRender = BuildEventRender(UncompressedPaletteColor.Pack(0, 0, 31), true);
         }
      }

      public bool WarpIsOnWarpableBlock(IDataModel model, LayoutModel layout) {
         if (!model.TryGetList("MapAttributeBehaviors", out var list)) return false;

         int primaryBlockCount = model.IsFRLG() ? 640 : 512;
         var cell = layout.BlockMap[X, Y];
         if (cell == null) return true; // failure to read layout, this warp rendering doesn't matter.
         var tile = cell.Tile;
         var blockset = layout.PrimaryBlockset;
         if (tile >= primaryBlockCount) {
            tile -= primaryBlockCount;
            blockset = layout.SecondaryBlockset;
         }

         var behavior = blockset.Attribute(tile).Behavior;
         if (list.Count <= behavior) return false;
         return new[] { "Warp", "Door", "Stairs", "Ladder", "Escalator" }.Any(list[behavior].Contains);
      }
   }

   public class ScriptEventViewModel : BaseEventViewModel {
      private readonly Action<int> gotoAddress;
      private readonly EventTemplate eventTemplate;

      public ScriptEventViewModel(Action<int> gotoAddress, ModelArrayElement scriptEvent, EventTemplate eventTemplate) : base(scriptEvent, "scriptCount") {
         this.gotoAddress = gotoAddress;
         this.eventTemplate = eventTemplate;
         UpdateScriptError(ScriptAddress);
      }

      public int Trigger {
         get => element.GetValue("trigger");
         set => element.SetValue("trigger", value);
      }

      private string triggerHex;
      public string TriggerHex {
         get {
            if (triggerHex != null) return triggerHex;
            return triggerHex = Trigger.ToString("X4");
         }
         set {
            triggerHex = value;
            if (!value.TryParseHex(out int result)) return;
            Trigger = result;
            NotifyPropertyChanged(nameof(CanGenerateNewTrigger));
         }
      }

      public bool CanGenerateNewTrigger => Trigger == 0;
      public void GenerateNewTrigger() {
         Trigger = eventTemplate.FindNextUnusedVariable();
         triggerHex = null;
         NotifyPropertiesChanged(nameof(TriggerHex), nameof(CanGenerateNewTrigger));
      }

      public int Index {
         get => element.GetValue("index");
         set => element.SetValue("index", value);
      }

      public int ScriptAddress {
         get => element.GetAddress("script");
         set {
            element.SetAddress("script", value, false);
            UpdateScriptError(element.GetAddress("script"));
            NotifyPropertiesChanged(nameof(ScriptAddressText), nameof(CanCreateScript));
         }
      }

      public void GotoScript() {
         SetDestinationFormat();
         gotoAddress(ScriptAddress);
      }

      public bool CanCreateScript => IsValidScriptFreespace(ScriptAddress) || ScriptAddress == Pointer.NULL;
      public void CreateScript() {
         int start;
         if (ScriptAddress != Pointer.NULL && IsValidScriptFreespace(ScriptAddress)) start = ScriptAddress;
         else start = element.Model.FindFreeSpace(element.Model.FreeSpaceStart, 0x10);
         Token.ChangeData(element.Model, start, 2);
         ScriptAddress = start;
         SetDestinationFormat();
         gotoAddress(start);
      }

      private string scriptAddressText;
      public string ScriptAddressText {
         get {
            if (scriptAddressText != null) return scriptAddressText;
            var value = element.GetAddress("script");
            return GetAddressText(value, ref scriptAddressText);
         }
         set {
            SetAddressText(value, ref scriptAddressText, "script", false);
            NotifyPropertyChanged();
            NotifyPropertiesChanged(nameof(ScriptAddress), nameof(CanCreateScript));
            UpdateScriptError(ScriptAddress);
         }
      }

      public override void Render(IDataModel model, LayoutModel layout) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(0, 31, 0));
      }

      private void SetDestinationFormat() {
         var existingRun = element.Model.GetNextRun(ScriptAddress);
         if (existingRun.Start == ScriptAddress && existingRun is NoInfoRun) {
            element.Model.ObserveRunWritten(Token, new XSERun(ScriptAddress));
         }
      }
   }

   public class SignpostEventViewModel : BaseEventViewModel {
      // kind. arg::|h
      // kind = 0/1/2/3/4 => arg is a pointer to an XSE script
      // kind = 5/6/7 => arg is itemID: hiddenItemID. attr|t|quantity:::.|isUnderFoot.
      // kind = 8 => arg is secret base ID, just a 4-byte hex number
      // hidden item IDs are just flags starting at 0x3E8 (1000).

      private readonly Action<int> gotoAddress;

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public SignpostEventViewModel(ModelArrayElement signpostEvent, Action<int> gotoAddress) : base(signpostEvent, "signpostCount") {
         if (signpostEvent.Model.TryGetList("MapSignpostKindOptions", out var names)) names.ForEach(KindOptions.Add);

         foreach (var item in signpostEvent.Model.GetOptions(HardcodeTablesModel.ItemsTableName)) {
            ItemOptions.Add(item);
         }

         SetDestinationFormat();

         this.gotoAddress = gotoAddress;
         if (ShowPointer) UpdateScriptError(Pointer);
      }

      public void SetDestinationFormat() {
         if (!ShowPointer) return;
         var destinationRun = new XSERun(Pointer, SortedSpan<int>.None);
         var existingRun = element.Model.GetNextRun(destinationRun.Start);
         if (existingRun.Start < destinationRun.Start) return; // don't erase existing runs for this
         if (existingRun.Start == destinationRun.Start && existingRun is not NoInfoRun) return;
         element.Model.ObserveRunWritten(new ModelDelta(), destinationRun); // don't track this change
      }

      public void ClearDestinationFormat() {
         if (!ShowPointer) return;
         var destination = Pointer;
         var run = element.Model.GetNextRun(Pointer);
         if (run.Start != destination || (run.PointerSources != null && run.PointerSources.Count > 0)) return;
         element.Model.ClearFormat(element.Token, destination, 1);
      }

      public ObservableCollection<string> KindOptions { get; } = new();

      public int Kind {
         get => element.TryGetValue("kind", out var value) ? value : -1;
         set {
            ClearDestinationFormat();
            var old = element.GetValue("kind");
            element.SetValue("kind", value);
            var wasPointer = old < 5;
            var isPointer = value < 5;
            NotifyPropertiesChanged(nameof(ShowArg), nameof(ShowPointer), nameof(ShowHiddenItemProperties));
            if (ShowHiddenItemProperties) NotifyPropertyChanged(nameof(ItemID));
            SetDestinationFormat();
            if (wasPointer == isPointer) return;
            element.SetValue("arg", 0);
            argText = null;
            pointerText = null;
            NotifyPropertiesChanged(
               nameof(ArgText), nameof(PointerText), nameof(ShowSignpostText), nameof(ItemID),
               nameof(HiddenItemID), nameof(Quantity), nameof(CanGotoScript), nameof(CanGenerateNewHiddenItemID));
         }
      }

      public bool ShowArg => Kind == 8;

      string argText;
      public string ArgText {
         get {
            if (argText != null) return argText;
            argText = element.GetValue("arg").ToString("X8");
            return argText;
         }
         set {
            argText = value;
            if (value.TryParseHex(out int result)) element.SetValue("arg", result);
         }
      }

      #region Show as Pointer

      public bool CanCreateScript => ShowPointer && IsValidScriptFreespace(Pointer);
      public void CreateScript() {
         int start;
         if (Pointer != DataFormats.Pointer.NULL && IsValidScriptFreespace(Pointer)) start = Pointer;
         else start = element.Model.FindFreeSpace(element.Model.FreeSpaceStart, 0x10);
         // 0F 00 <start+9> 09 03 02 FF
         var textStart = start + 9;
         Token.ChangeData(element.Model, start, new byte[] { 0x0F, 0, (byte)textStart, (byte)(textStart >> 8), (byte)(textStart >> 16), (byte)((textStart >> 24) + 8), 9, 3, 2, 0xFF });
         Pointer = start;
         gotoAddress(start);
      }

      public bool ShowPointer => Kind < 5;

      public int Pointer {
         get => element.GetAddress("arg");
         set {
            ClearDestinationFormat();
            element.SetAddress("arg", value, false);
            var run = element.Model.GetNextRun(Pointer);
            if (run.Start == Pointer && run is NoInfoRun && !IsValidScriptFreespace(Pointer)) SetDestinationFormat();
            UpdateScriptError(element.GetAddress("arg"));
            pointerText = argText = null;
            NotifyPropertiesChanged(nameof(PointerText), nameof(ArgText), nameof(CanGotoScript), nameof(CanCreateScript));
         }
      }

      private string pointerText;
      public string PointerText {
         get {
            if (pointerText != null) return pointerText;
            var value = element.GetAddress("arg");
            return GetAddressText(value, ref pointerText);
         }
         set {
            ClearDestinationFormat();
            SetAddressText(value, ref pointerText, "arg", false);
            var run = element.Model.GetNextRun(Pointer);
            if (run.Start == Pointer && run is NoInfoRun && !IsValidScriptFreespace(Pointer)) SetDestinationFormat();
            NotifyPropertiesChanged(nameof(PointerText), nameof(Pointer), nameof(ArgText), nameof(CanGotoScript), nameof(CanCreateScript));
            UpdateScriptError(Pointer);
         }
      }

      public bool CanGotoScript => 0 <= Pointer && Pointer < element.Model.Count;
      public void GotoScript() {
         var run = element.Model.GetNextRun(Pointer);
         if (run.Start == Pointer && run is NoInfoRun) SetDestinationFormat();
         gotoAddress(Pointer);
      }

      #endregion

      #region Item Properties

      public bool ShowHiddenItemProperties => Kind >= 5 && Kind <= 7;

      // itemID: hiddenItemID. attr|t|quantity:::.|isUnderFoot.
      // arg is at offset '8' of the element

      public ObservableCollection<string> ItemOptions { get; } = new();

      public int ItemID {
         get => element.Model.ReadMultiByteValue(element.Start + 8, 2);
         set {
            element.Model.WriteMultiByteValue(element.Start + 8, 2, element.Token, value);
            NotifyPropertyChanged(nameof(ItemID));
         }
      }

      public byte HiddenItemID {
         get => element.Model[element.Start + 10];
         set {
            element.Token.ChangeData(element.Model, element.Start + 10, value);
            NotifyPropertyChanged(nameof(CanGenerateNewHiddenItemID));
         }
      }

      public byte Quantity {
         get => (byte)(element.Model[element.Start + 11] & 0x7F);
         set {
            var newValue = (byte)((int)value).LimitToRange(0, 0x7F);
            var previous = element.Model[element.Start + 11];
            newValue |= (byte)(previous & 0x80);
            Token.ChangeData(element.Model, element.Start + 11, newValue);
            NotifyPropertyChanged(nameof(Quantity));
         }
      }

      public bool IsUnderFoot {
         get => element.Model[element.Start + 11] >= 0x80;
         set {
            byte newValue = value ? (byte)0x80 : (byte)0;
            var previous = element.Model[element.Start + 11];
            newValue |= (byte)(previous & 0x7F);
            Token.ChangeData(element.Model, element.Start + 11, newValue);
            NotifyPropertyChanged(nameof(IsUnderFoot));
         }
      }

      public bool CanGenerateNewHiddenItemID => ShowHiddenItemProperties && HiddenItemID == 0;

      public void GenerateNewHiddenItemID() {
         var usedIDs = new HashSet<byte>(
            AllMapsModel.Create(element.Model)
            .SelectMany(bank => bank)
            .SelectMany(map => map.Events.Signposts)
            .Where(signpost => signpost.IsHiddenItem)
            .Select(signpost => element.Model[signpost.Element.Start + 10]));

         byte match = 0;
         for (int i = 1; i < 256; i++) {
            if (usedIDs.Contains((byte)i)) continue;
            match = (byte)i;
            break;
         }

         HiddenItemID = match;
         NotifyPropertyChanged(nameof(HiddenItemID));
      }

      #endregion

      public override void Render(IDataModel model, LayoutModel layout) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(31, 0, 0));
      }

      public bool ShowSignpostText => EventTemplate.GetSignpostTextPointer(element.Model, this) != DataFormats.Pointer.NULL;

      private string signpostText;
      public string SignpostText {
         get {
            if (signpostText != null) return signpostText;
            signpostText = GetText(EventTemplate.GetSignpostTextPointer(element.Model, this));
            return signpostText;
         }
         set {
            signpostText = value;
            var newAddress = SetText(EventTemplate.GetSignpostTextPointer(element.Model, this), value);
            if (newAddress >= 0) DataMoved.Raise(this, new("Text", newAddress));
         }
      }
   }

   public class TrainerTeamViewModel : ViewModelCore {
      private readonly IDataModel model;
      private readonly int trainerID;
      private readonly Func<ModelDelta> tokenGenerator;

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public TrainerTeamViewModel(IDataModel model, int trainerID, Func<ModelDelta> tokenGenerator) {
         (this.model, this.trainerID) = (model, trainerID);
         this.tokenGenerator = tokenGenerator;
      }

      public string TrainerIDText {
         get {
            var name = model.GetOptions(HardcodeTablesModel.TrainerTableName)[trainerID];
            var splitter = name.IndexOf("~");
            if (splitter != -1) name = name.Substring(splitter);
            return name;
         }
      }

      private string teamText;
      public string TrainerTeam {
         get {
            if (teamText != null) return teamText;
            var table = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
            if (table == null || !trainerID.InRange(0, table.Count)) return string.Empty;
            var teamAddress = table[trainerID].GetAddress("pokemon");
            if (model.GetNextRun(teamAddress) is not TrainerPokemonTeamRun team) return string.Empty;

            if (TeamVisualizations.Count == 0) UpdateTeamVisualizations(team);
            return teamText = team.SerializeRun();
         }

         set {
            teamText = value;
            var table = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
            if (table == null || !trainerID.InRange(0, table.Count)) return;
            var teamAddress = table[trainerID].GetAddress("pokemon");
            if (model.GetNextRun(teamAddress) is not TrainerPokemonTeamRun team) return;

            var newRun = team.DeserializeRun(value, tokenGenerator(), false, false, out _);
            model.ObserveRunWritten(tokenGenerator(), newRun);
            if (newRun.Start != team.Start) DataMoved.Raise(this, new("Trainer Team", newRun.Start));
            UpdateTeamVisualizations(newRun);
            NotifyPropertyChanged();
         }
      }

      public ObservableCollection<IPixelViewModel> TeamVisualizations { get; } = new();

      private void UpdateTeamVisualizations(TrainerPokemonTeamRun team) {
         TeamVisualizations.Clear();
         foreach (var vis in team.Visualizations) {
            TeamVisualizations.Add(vis);
         }
      }

      public IReadOnlyList<AutocompleteItem> GetTrainerAutocomplete(string line, int lineIndex, int characterIndex) {
         var table = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         if (table == null || !trainerID.InRange(0, table.Count)) return null;
         var teamAddress = table[trainerID].GetAddress("pokemon");
         if (model.GetNextRun(teamAddress) is not TrainerPokemonTeamRun team) return null;
         return team.GetAutoCompleteOptions(line, lineIndex, characterIndex);
      }

      public void Refresh() {
         teamText = null;
         TeamVisualizations.Clear();
         NotifyPropertyChanged(nameof(TrainerTeam));
      }
   }
}
