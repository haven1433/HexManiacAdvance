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
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {

   public interface IEventViewModel : IEquatable<IEventViewModel>, INotifyPropertyChanged {
      event EventHandler EventVisualUpdated;
      public event EventHandler<EventCycleDirection> CycleEvent;
      public ICommand CycleEventCommand { get; }
      string EventType { get; }
      string EventIndex { get; }
      int TopOffset { get; }
      int LeftOffset { get; }
      int X { get; set; }
      int Y { get; set; }
      IPixelViewModel EventRender { get; }
      void Render(IDataModel model);
      void Delete();
   }

   public enum EventCycleDirection { PreviousCategory, PreviousEvent, NextEvent, NextCategory }

   public class FlyEventViewModel : ViewModelCore, IEventViewModel {
      private readonly ModelArrayElement flySpot;
      private readonly ModelArrayElement connectionEntry;

      public string EventType => "Fly";

      public string EventIndex => "1/1";

      public virtual int TopOffset => 0;
      public virtual int LeftOffset => 0;

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

      public FlyEventViewModel(IDataModel model, int bank, int map, Func<ModelDelta> tokenFactory) {
         // get the region from the map
         var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable, tokenFactory);
         if (banks == null) return;
         var maps = banks[bank].GetSubTable("maps");
         if (maps == null) return;
         var table = maps[map].GetSubTable("map");
         if (table == null) return;
         var region = table[0].GetValue(Format.RegionSection);
         if (model.IsFRLG()) region -= 88;
         if (region < 0) return;
         var flyIndexTable = model.GetTableModel(HardcodeTablesModel.FlyConnections, tokenFactory);
         if (flyIndexTable == null) return;
         if (region >= flyIndexTable.Count) return;
         connectionEntry = flyIndexTable[region];
         if (flyIndexTable[region].GetValue("bank") != bank) return;
         if (flyIndexTable[region].GetValue("map") != map) return;
         var flyIndex = flyIndexTable[region].GetValue("flight") - 1;
         if (flyIndex < 0) return;
         var flyTable = model.GetTableModel(HardcodeTablesModel.FlySpawns, tokenFactory);
         if (flyTable == null) return;
         if (flyIndex >= flyTable.Count) return;
         flySpot = flyTable[flyIndex];
         if (flySpot.GetValue("bank") != bank) return;
         if (flySpot.GetValue("map") != map) return;
         Valid = true;
      }

      public void Delete() {
         if (!Valid) return;
         // set the connection table's index to 0
         connectionEntry.SetValue("flight", 0);
         flySpot.SetValue("bank", 0);
         flySpot.SetValue("map", 0);
         flySpot.SetValue("x", 0);
         flySpot.SetValue("y", 0);
      }

      public bool Equals(IEventViewModel? other) {
         if (other is not FlyEventViewModel fly) return false;
         return X == fly.X && Y == fly.Y && flySpot.Start == fly.flySpot.Start;
      }

      public void Render(IDataModel model) {
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
      public string EventIndex {
         get {
            var eventIndex = (element.Start - element.Table.Start) / element.Table.ElementLength + 1;
            return $"{eventIndex}/{element.Table.ElementCount}";
         }
      }

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

      public void Delete() => DeleteElement(parentLengthField);

      public virtual bool Equals(IEventViewModel other) {
         if (other is not BaseEventViewModel bem) return false;
         return bem.element.Start == element.Start;
      }

      public abstract void Render(IDataModel model);

      protected void RaiseEventVisualUpdated() => EventVisualUpdated.Raise(this);

      protected void DeleteElement(string parentCountField) {
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
      }

      protected string GetText(int pointer) {
         if (pointer == Pointer.NULL) return null;
         var address = element.Model.ReadPointer(pointer);
         if (address < 0 || address >= element.Model.Count) return null;
         var run = element.Model.GetNextRun(address);
         if (run.Start != address) {
            if (run.Start < address) return null;
            var length = PCSString.ReadString(element.Model, address, true);
            if (run.Start < address + length) return null;
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
         return pcs.SerializeRun();
      }

      protected int SetText(int pointer, string text, [CallerMemberName] string propertyName = null) {
         var address = element.Model.ReadPointer(pointer);
         if (address < 0 || address >= element.Model.Count) return -1;
         if (element.Model.GetNextRun(address) is not PCSRun pcs) return -1;
         var newRun = pcs.DeserializeRun(text, element.Token, out _);
         element.Model.ObserveRunWritten(element.Token, newRun);
         NotifyPropertyChanged(propertyName);
         return newRun.Start != pcs.Start ? newRun.Start : -1;
      }

      public static IPixelViewModel BuildEventRender(short color) {
         var pixels = new short[256];
         for (int x = 1; x < 15; x++) {
            for (int y = 1; y < 15; y++) {
               if (((x + y) & 1) != 0) continue;
               pixels[y * 16 + x] = color;
               y++;
            }
         }
         return new ReadonlyPixelViewModel(new SpriteFormat(4, 2, 2, default), pixels, transparent: 0);
      }
   }

   public class ObjectEventViewModel : BaseEventViewModel {
      private readonly ScriptParser parser;
      private readonly Action<int> gotoAddress;

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public int Start => element.Start;

      public int ObjectID {
         get => element.GetValue("id");
         set => element.SetValue("id", value);
      }

      public int Graphics {
         get => element.GetValue("graphics");
         set {
            element.SetValue("graphics", value);
            RaiseEventVisualUpdated();
         }
      }

      public int MoveType {
         get => element.GetValue("moveType");
         set {
            element.SetValue("moveType", value);
            RaiseEventVisualUpdated();
         }
      }

      #region Range

      public int RangeX {
         get => element.GetValue("range") & 0xF;
         set {
            element.SetValue("range", (RangeY << 4) | value);
            rangeXY = null;
            NotifyPropertyChanged(nameof(RangeXY));
         }
      }

      public int RangeY {
         get => element.GetValue("range") >> 4;
         set {
            element.SetValue("range", (value << 4) | RangeX);
            rangeXY = null;
            NotifyPropertyChanged(nameof(RangeXY));
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
         }
      }

      #endregion

      public int TrainerType {
         get => element.GetValue("trainerType");
         set => element.SetValue("trainerType", value);
      }

      public int TrainerRangeOrBerryID {
         get => element.GetValue("trainerRangeOrBerryID");
         set => element.SetValue("trainerRangeOrBerryID", value);
      }

      public int ScriptAddress {
         get => element.GetAddress("script");
         set => element.SetAddress("script", value);
      }

      public void GotoScript() => gotoAddress(ScriptAddress);

      private string scriptAddressText;
      public string ScriptAddressText {
         get {
            var value = element.GetAddress("script");
            if (scriptAddressText == null) {
               scriptAddressText = $"<{value.ToAddress()}>";
               if (value == Pointer.NULL) scriptAddressText = "<null>";
            }
            return scriptAddressText;
         }
         set {
            scriptAddressText = value;
            value = value.Trim("<null>".ToCharArray());
            element.SetAddress("script", value.TryParseHex(out int result) ? result : Pointer.NULL);
         }
      }

      public int Flag {
         get => element.GetValue("flag");
         set => element.SetValue("flag", value);
      }

      public string FlagText {
         get => element.GetValue("flag").ToString("X4");
         set => element.SetValue("flag", value.TryParseHex(out int result) ? result : 0);
      }

      public ObservableCollection<VisualComboOption> Options { get; } = new();
      public ObservableCollection<string> FacingOptions { get; } = new();
      public ObservableCollection<string> ClassOptions { get; } = new();
      public ObservableCollection<string> ItemOptions { get; } = new();

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
            NotifyPropertyChanged();
         }
      }

      public bool ShowNpcText => EventTemplate.GetNPCTextPointer(element.Model, this) != Pointer.NULL;

      public string NpcText {
         get => GetText(EventTemplate.GetNPCTextPointer(element.Model, this));
         set {
            var newStart = SetText(EventTemplate.GetNPCTextPointer(element.Model, this), value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      #region Trainer Content

      public bool ShowTrainerContent => EventTemplate.GetTrainerContent(element.Model, this) != null;

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

      public string TrainerName {
         get {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            var text = element.Model.TextConverter.Convert(element.Model, trainerContent.TrainerNameAddress, 12);
            return text.Trim('"');
         }
         set {
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
         }
      }

      public string TrainerBeforeText {
         get {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            return GetText(trainerContent.BeforeTextPointer);
         }
         set {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var newStart = SetText(trainerContent.BeforeTextPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      public string TrainerWinText {
         get {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            return GetText(trainerContent.WinTextPointer);
         }
         set {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var newStart = SetText(trainerContent.WinTextPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      public string TrainerAfterText {
         get {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            return GetText(trainerContent.AfterTextPointer);
         }
         set {
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var newStart = SetText(trainerContent.AfterTextPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

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
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var address = element.Model.ReadPointer(trainerContent.TeamPointer);
            if (address < 0 || address >= element.Model.Count) return;
            if (element.Model.GetNextRun(address) is not TrainerPokemonTeamRun run) return;
            if (run.Start != address) return;
            teamText = value;
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

      #endregion

      #region Mart Content

      private Lazy<MartEventContent> martContent;

      public bool ShowMartContents => martContent.Value != null;

      public string MartHello {
         get {
            if (martContent.Value == null) return null;
            return GetText(martContent.Value.HelloPointer);
         }
         set {
            if (martContent.Value == null) return;
            var newStart = SetText(martContent.Value.HelloPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      public string MartContent {
         get {
            if (martContent.Value == null) return null;
            var martStart = element.Model.ReadPointer(martContent.Value.MartPointer);
            if (element.Model.GetNextRun(martStart) is not IStreamRun stream) return null;
            return stream.SerializeRun();
         }
         set {
            if (martContent.Value == null) return;
            var martStart = element.Model.ReadPointer(martContent.Value.MartPointer);
            if (element.Model.GetNextRun(martStart) is not IStreamRun stream) return;
            var newStream = stream.DeserializeRun(value, Token, out var _);
            element.Model.ObserveRunWritten(Token, newStream);
            if (newStream.Start != stream.Start) DataMoved.Raise(this, new("Mart", newStream.Start));
         }
      }

      public string MartGoodbye {
         get {
            if (martContent.Value == null) return null;
            return GetText(martContent.Value.GoodbyePointer);
         }
         set {
            var newStart = SetText(martContent.Value.GoodbyePointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      #endregion

      #region Tutor Content

      private Lazy<TutorEventContent> tutorContent;

      public bool ShowTutorContent {
         get {
            var content = tutorContent.Value;
            if (content != null && TutorOptions.Count == 0) {
               TutorOptions.AddRange(element.Model.GetOptions(HardcodeTablesModel.MoveTutors));
            }
            return tutorContent.Value != null;
         }
      }

      public string TutorInfoText {
         get {
            if (tutorContent.Value == null) return null;
            return GetText(tutorContent.Value.InfoPointer);
         }
         set {
            if (tutorContent.Value == null) return;
            var newStart = SetText(tutorContent.Value.InfoPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      public string TutorWhichPokemonText {
         get {
            if (tutorContent.Value == null) return null;
            return GetText(tutorContent.Value.WhichPokemonPointer);
         }
         set {
            if (tutorContent.Value == null) return;
            var newStart = SetText(tutorContent.Value.WhichPokemonPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      public string TutorFailedText {
         get {
            if (tutorContent.Value == null) return null;
            return GetText(tutorContent.Value.FailedPointer);
         }
         set {
            if (tutorContent.Value == null) return;
            var newStart = SetText(tutorContent.Value.FailedPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

      public string TutorSucessText {
         get {
            if (tutorContent.Value == null) return null;
            return GetText(tutorContent.Value.SuccessPointer);
         }
         set {
            if (tutorContent.Value == null) return;
            var newStart = SetText(tutorContent.Value.SuccessPointer, value);
            if (newStart != -1) DataMoved.Raise(this, new("Text", newStart));
         }
      }

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

      public ObservableCollection<string> TutorOptions { get; } = new();

      public void GotoTutors() => gotoAddress(element.Model.GetTableModel(HardcodeTablesModel.MoveTutors)[TutorNumber].Start);

      #endregion

      #endregion

      public ObjectEventViewModel(ScriptParser parser, Action<int> gotoAddress, ModelArrayElement objectEvent, IReadOnlyList<IPixelViewModel> sprites) : base(objectEvent, "objectCount") {
         this.parser = parser;
         this.gotoAddress = gotoAddress;
         for (int i = 0; i < sprites.Count; i++) Options.Add(VisualComboOption.CreateFromSprite(i.ToString(), sprites[i].PixelData, sprites[i].PixelWidth, i, 2));
         objectEvent.Model.TryGetList("FacingOptions", out var list);
         foreach (var item in list) FacingOptions.Add(item);
         foreach (var item in objectEvent.Model.GetOptions(HardcodeTablesModel.TrainerClassNamesTable)) ClassOptions.Add(item);
         foreach (var item in objectEvent.Model.GetOptions(HardcodeTablesModel.ItemsTableName)) ItemOptions.Add(item);

         tutorContent = new Lazy<TutorEventContent>(() => EventTemplate.GetTutorContent(element.Model, parser, this));
         martContent = new Lazy<MartEventContent>(() => EventTemplate.GetMartContent(element.Model, parser, this));
      }

      public override int TopOffset => 16 - EventRender.PixelHeight;
      public override int LeftOffset => (16 - EventRender.PixelWidth) / 2;

      public override void Render(IDataModel model) {
         var owTable = new ModelTable(model, model.GetTable(HardcodeTablesModel.OverworldSprites).Start);
         var facing = MoveType switch {
            7 => 1,
            9 => 2,
            10 => 3,
            _ => 0,
         };
         EventRender = Render(model, owTable, Graphics, facing);
         NotifyPropertyChanged(nameof(EventRender));
      }

      /// <param name="facing">(0, 1, 2, 3) = (down, up, left, right)</param>
      public static IPixelViewModel Render(IDataModel model, ModelTable owTable, int index, int facing) {
         if (index >= owTable.Count) {
            return new ReadonlyPixelViewModel(new SpriteFormat(4, 2, 2, null), new short[256], 0);
         }
         var element = owTable[index];
         var data = element.GetSubTable("data")[0];
         var sprites = data.GetSubTable("sprites");
         if (sprites == null) return new ReadonlyPixelViewModel(16, 16);
         bool flip = facing == 3;
         if (facing == 3) facing = 2;
         if (facing >= sprites.Count) facing = 0;
         var sprite = sprites[facing];
         var graphicsAddress = sprite.GetAddress("sprite");
         var graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         if (graphicsRun == null) {
            return new ReadonlyPixelViewModel(16, 16);
         }
         var ow = ReadonlyPixelViewModel.Create(model, graphicsRun, true);
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
            if (Math.Abs(x - X) <= RangeX && Math.Abs(y - Y) <=    RangeY) return true;
         }
         return false;
      }
   }

   public class WarpEventViewModel : BaseEventViewModel {
      public WarpEventViewModel(ModelArrayElement warpEvent) : base(warpEvent, "warpCount") { }

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
            NotifyPropertyChanged(nameof(BankMap));
            NotifyPropertyChanged(nameof(TargetMapName));
         }
      }

      public int Map {
         get => element.GetValue("map");
         set {
            element.SetValue("map", value);
            NotifyPropertyChanged();
            if (!ignoreUpdateBankMap) bankMap = null;
            NotifyPropertyChanged(nameof(BankMap));
            NotifyPropertyChanged(nameof(TargetMapName));
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
         }
      }

      public string TargetMapName => BlockMapViewModel.MapIDToText(element.Model, Bank, Map);

      #endregion

      public override void Render(IDataModel model) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(0, 0, 31));
      }
   }

   public class ScriptEventViewModel : BaseEventViewModel {
      private readonly Action<int> gotoAddress;

      public ScriptEventViewModel(Action<int> gotoAddress, ModelArrayElement scriptEvent) : base(scriptEvent, "scriptCount") { this.gotoAddress = gotoAddress; }

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
         }
      }

      public int Index {
         get => element.GetValue("index");
         set => element.SetValue("index", value);
      }

      public int ScriptAddress {
         get => element.GetAddress("script");
         set => element.SetAddress("script", value);
      }

      public void GotoScript() => gotoAddress(ScriptAddress);

      private string scriptAddressText;

      public string ScriptAddressText {
         get {
            if (scriptAddressText == null) scriptAddressText = element.GetAddress("script").ToAddress();
            return scriptAddressText;
         }
         set {
            scriptAddressText = value;
            element.SetAddress("script", value.TryParseHex(out int result) ? result : Pointer.NULL);
         }
      }

      public override void Render(IDataModel model) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(0, 31, 0));
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
         new List<string> {
            "Facing Any",
            "Facing North",
            "Facing South",
            "Facing East",
            "Facing West",
            "Hidden Item (unused 1)",
            "Hidden Item (unused 2)",
            "Hidden Item",
            "Secret Base",
         }.ForEach(KindOptions.Add);

         foreach (var item in signpostEvent.Model.GetOptions(HardcodeTablesModel.ItemsTableName)) {
            ItemOptions.Add(item);
         }

         SetDestinationFormat();

         this.gotoAddress = gotoAddress;
      }

      public void SetDestinationFormat() {
         if (!ShowPointer) return;
         var destinationRun = new XSERun(Pointer, SortedSpan<int>.None);
         var existingRun = element.Model.GetNextRun(destinationRun.Start);
         if (existingRun.Start <= destinationRun.Start) return; // don't erase existing runs for this
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
         get => element.GetValue("kind");
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
            NotifyPropertiesChanged(nameof(ArgText), nameof(PointerText), nameof(ShowSignpostText), nameof(ItemID), nameof(HiddenItemID), nameof(Quantity), nameof(CanGotoScript));
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

      public bool ShowPointer => Kind < 5;

      public int Pointer {
         get => element.GetAddress("arg");
         set {
            ClearDestinationFormat();
            element.SetAddress("arg", value);
            pointerText = argText = null;
            NotifyPropertiesChanged(nameof(PointerText), nameof(ArgText), nameof(CanGotoScript));
            SetDestinationFormat();
         }
      }

      private string pointerText;
      public string PointerText {
         get {
            if (pointerText != null) return pointerText;
            var address = element.GetValue("arg") + DataFormats.Pointer.NULL;
            pointerText = AddressFieldStrategy.ConvertAddressToText(address);
            return pointerText;
         }
         set {
            pointerText = value;
            if (AddressFieldStrategy.TryParse(pointerText, out var address)) {
               ClearDestinationFormat();
               element.SetValue("arg", address - DataFormats.Pointer.NULL);
               SetDestinationFormat();
               NotifyPropertyChanged(nameof(PointerText), nameof(CanGotoScript));
            }
         }
      }

      public bool CanGotoScript => 0 <= Pointer && Pointer < element.Model.Count;
      public void GotoScript() => gotoAddress(Pointer);

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
         set => element.Token.ChangeData(element.Model, element.Start + 10, value);
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

      #endregion

      public override void Render(IDataModel model) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(31, 0, 0));
      }

      public bool ShowSignpostText => EventTemplate.GetSignpostTextPointer(element.Model, this) != DataFormats.Pointer.NULL;

      public string SignpostText {
         get => GetText(EventTemplate.GetSignpostTextPointer(element.Model, this));
         set {
            var newAddress = SetText(EventTemplate.GetSignpostTextPointer(element.Model, this), value);
            if (newAddress >= 0) DataMoved.Raise(this, new("Text", newAddress));
         }
      }
   }
}
