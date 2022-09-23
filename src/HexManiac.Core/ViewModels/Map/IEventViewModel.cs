using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using IronPython.Runtime.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using static IronPython.Modules.PythonWeakRef;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {

   public interface IEventModel : IEquatable<IEventModel>, INotifyPropertyChanged {
      event EventHandler EventVisualUpdated;
      public event EventHandler<EventCycleDirection> CycleEvent;
      string EventType { get; }
      string EventIndex { get; }
      int TopOffset { get; }
      int LeftOffset { get; }
      int X { get; set; }
      int Y { get; set; }
      int Elevation { get; set; }
      IPixelViewModel EventRender { get; }
      void Render(IDataModel model);
      void Delete();
   }

   public enum EventCycleDirection { PreviousCategory, PreviousEvent, NextEvent, NextCategory }

   public abstract class BaseEventModel : ViewModelCore, IEventModel, IEquatable<IEventModel> {
      public event EventHandler EventVisualUpdated;
      public event EventHandler<EventCycleDirection> CycleEvent;

      private StubCommand cycleEventCommand;
      public ICommand CycleEventCommand => StubCommand<EventCycleDirection>(ref cycleEventCommand, direction => {
         CycleEvent?.Invoke(this, direction);
      });

      protected readonly ModelArrayElement element;
      private readonly string parentLengthField;

      public ModelDelta Token => element.Token;

      public string EventType => GetType().Name.Replace("EventModel", string.Empty);
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

      public BaseEventModel(ModelArrayElement element, string parentLengthField) => (this.element, this.parentLengthField) = (element, parentLengthField);

      public void Delete() => DeleteElement(parentLengthField);

      public bool Equals(IEventModel other) {
         if (other is not BaseEventModel bem) return false;
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

      protected void SetText(int pointer, string text, [CallerMemberName] string propertyName = null) {
         var address = element.Model.ReadPointer(pointer);
         if (address < 0 || address >= element.Model.Count) return;
         if (element.Model.GetNextRun(address) is not PCSRun pcs) return;
         element.Model.ObserveRunWritten(element.Token, pcs.DeserializeRun(text, element.Token, out _));
         NotifyPropertyChanged();
      }

      protected static IPixelViewModel BuildEventRender(short color) {
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

   public class ObjectEventModel : BaseEventModel {
      private readonly Action<int> gotoAddress;

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
            if (scriptAddressText == null) scriptAddressText = element.GetAddress("script").ToAddress();
            return scriptAddressText;
         }
         set {
            scriptAddressText = value;
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

      public bool ShowTrainerContent => EventTemplate.GetTrainerContent(element.Model, this) != null;

      public bool ShowNpcText => EventTemplate.GetNPCTextPointer(element.Model, this) != Pointer.NULL;

      public string NpcText {
         get => GetText(EventTemplate.GetNPCTextPointer(element.Model, this));
         set => SetText(EventTemplate.GetNPCTextPointer(element.Model, this), value);
      }

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
            SetText(trainerContent.BeforeTextPointer, value);
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
            SetText(trainerContent.WinTextPointer, value);
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
            SetText(trainerContent.AfterTextPointer, value);
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

      public ObjectEventModel(Action<int> gotoAddress, ModelArrayElement objectEvent, IReadOnlyList<IPixelViewModel> sprites) : base(objectEvent, "objectCount") {
         this.gotoAddress = gotoAddress;
         for (int i = 0; i < sprites.Count; i++) Options.Add(VisualComboOption.CreateFromSprite(i.ToString(), sprites[i].PixelData, sprites[i].PixelWidth, i));
         objectEvent.Model.TryGetList("FacingOptions", out var list);
         foreach (var item in list) FacingOptions.Add(item);
         foreach (var item in objectEvent.Model.GetOptions(HardcodeTablesModel.TrainerClassNamesTable)) ClassOptions.Add(item);
         foreach (var item in objectEvent.Model.GetOptions(HardcodeTablesModel.ItemsTableName)) ItemOptions.Add(item);
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
         bool flip = facing == 3;
         if (facing == 3) facing = 2;
         if (facing >= sprites.Count) facing = 0;
         var sprite = sprites[facing];
         var graphicsAddress = sprite.GetAddress("sprite");
         var graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         if (graphicsRun == null) {
            return new ReadonlyPixelViewModel(new SpriteFormat(4, 16, 16, null), new short[256], 0);
         }
         var ow = ReadonlyPixelViewModel.Create(model, graphicsRun, true);
         if (flip) ow = ow.ReflectX();
         return ow;
      }

      public void ClearUnused() {
         element.SetValue(2, 0);
         element.SetValue(12, 0);
      }
   }

   public class WarpEventModel : BaseEventModel {
      public WarpEventModel(ModelArrayElement warpEvent) : base(warpEvent, "warpCount") { }

      public int WarpID {
         get => element.GetValue("warpID");
         set => element.SetValue("warpID", value);
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

      public string TargetMapName => BlockMapViewModel.MapIDToText(element.Model, Bank * 1000 + Map);

      #endregion

      public override void Render(IDataModel model) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(0, 0, 31));
      }
   }

   public class ScriptEventModel : BaseEventModel {
      private readonly Action<int> gotoAddress;

      public ScriptEventModel(Action<int> gotoAddress, ModelArrayElement scriptEvent) : base(scriptEvent, "scriptCount") { this.gotoAddress = gotoAddress; }

      public int Trigger {
         get => element.GetValue("trigger");
         set => element.SetValue("trigger", value);
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

   public class SignpostEventModel : BaseEventModel {
      // kind. arg::|h
      // kind = 5/6/7 => arg is itemID: hiddenItemID. attr|t|quantity:::.|isUnderFoot.
      // kind = other => arg is script<`xse`>

      public SignpostEventModel(ModelArrayElement signpostEvent) : base(signpostEvent, "signpostCount") { }

      public int Kind {
         get => element.GetValue("kind");
         set => element.SetValue("kind", value);
      }

      public string Arg {
         get => element.GetValue("arg").ToString("X8");
         set => element.SetValue("arg", value.TryParseHex(out int result) ? result : 0);
      }

      public override void Render(IDataModel model) {
         EventRender = BuildEventRender(UncompressedPaletteColor.Pack(31, 0, 0));
      }

      public bool ShowSignpostText => EventTemplate.GetSignpostTextPointer(element.Model, this) != Pointer.NULL;

      public string SignpostText {
         get => GetText(EventTemplate.GetSignpostTextPointer(element.Model, this));
         set => SetText(EventTemplate.GetSignpostTextPointer(element.Model, this), value);
      }
   }
}
