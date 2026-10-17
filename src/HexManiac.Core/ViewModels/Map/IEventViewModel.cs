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
using System.Data;
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

      /// <summary>the segment as it is for the element at this address: a switch field (like the argument of a signpost) is a pointer or a number, depending on another field. Null if it is not a pointer.</summary>
      private static ArrayRunElementSegment GetPointerSegment(IDataModel model, ITableRun table, ArrayRunElementSegment segment, int address) {
         if (segment is ArrayRunRecordSegment record) segment = record.CreateConcrete(model, table, address);
         return segment.Type == ElementContentType.Pointer ? segment : null;
      }

      protected bool DeleteElement(string parentCountField) {
         var table = element.Table;
         var model = element.Model;
         var token = element.Token;
         var offset = table.ConvertByteOffsetToArrayOffset(element.Start);
         var editCount = table.ElementCount - offset.ElementIndex - 1;
         for (int i = 0; i < editCount; i++) {
            // switch fields depend on another field of their element: find out what they are before that other field gets overwritten
            var destinationStart = element.Start + i * element.Length;
            var sourceStart = destinationStart + element.Length;
            var oldPointer = new ArrayRunElementSegment[table.ElementContent.Count];
            var newPointer = new ArrayRunElementSegment[table.ElementContent.Count];
            int segmentStart = 0;
            for (int j = 0; j < table.ElementContent.Count; j++) {
               if (table.ElementContent[j] is ArrayRunRecordSegment) {
                  oldPointer[j] = GetPointerSegment(model, table, table.ElementContent[j], destinationStart + segmentStart);
                  newPointer[j] = GetPointerSegment(model, table, table.ElementContent[j], sourceStart + segmentStart);
               }
               segmentStart += table.ElementContent[j].Length;
            }

            int segmentOffset = 0;
            for (int j = 0; j < table.ElementContent.Count; j++) {
               var source = element.Start + (i + 1) * element.Length + segmentOffset;
               var destination = source - element.Length;
               var length = table.ElementContent[j].Length;
               if (table.ElementContent[j] is ArrayRunRecordSegment) {
                  // the one that is moved over is no longer pointing at its script: its pointer source goes away
                  if (oldPointer[j] != null) model.UpdateArrayPointer(token, null, null, 0, destination, Pointer.NULL);
                  var pointer = newPointer[j] == null ? Pointer.NULL : model.ReadPointer(source);
                  if (pointer >= 0) {
                     model.UpdateArrayPointer(token, newPointer[j], table.ElementContent, offset.ElementIndex, destination, pointer);
                  } else {
                     model.WriteMultiByteValue(destination, length, token, model.ReadMultiByteValue(source, length));
                  }
               } else if (table.ElementContent[j].Type == ElementContentType.Pointer) {
                  model.UpdateArrayPointer(token, table.ElementContent[j], table.ElementContent, offset.ElementIndex, destination, model.ReadPointer(source));
               } else {
                  model.WriteMultiByteValue(destination, length, token, model.ReadMultiByteValue(source, length));
               }
               segmentOffset += length;
            }
         }
         if (table.ElementCount > 1) {
            // the last element falls off the table. Its switch fields are cleared without knowing what they were, so let go of its pointers first.
            var lastStart = table.Start + (table.ElementCount - 1) * table.ElementLength;
            int segmentStart = 0;
            foreach (var segment in table.ElementContent) {
               if (segment is ArrayRunRecordSegment && GetPointerSegment(model, table, segment, lastStart + segmentStart) != null) {
                  model.UpdateArrayPointer(token, null, null, 0, lastStart + segmentStart, Pointer.NULL);
               }
               segmentStart += segment.Length;
            }
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

   public class EventTextViewModel : ViewModelCore {
      private int pointerAddress;
      public int PointerAddress { get => pointerAddress; set => Set(ref pointerAddress, value, old => NotifyPropertyChanged(nameof(PointerAddressText))); }
      public string PointerAddressText => PointerAddress.ToAddress();
      public TextEditorViewModel Text { get; } = new();
   }

   public class ObjectEventViewModel : BaseEventViewModel {
      private readonly BlockMapViewModel parent;
      private ScriptParser parser => parent.ViewPort.Tools.CodeTool.ScriptParser; // not cached: the editor rebuilds its parser once the ROM's own commands are loaded
      internal ScriptParser Parser => parser;
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
            NotifyPropertiesChanged(nameof(HasGraphicsInTransitionScript), nameof(CanAddDynamicGraphicsToTransitionScript));
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
            NotifyPropertiesChanged(nameof(CanAddDynamicGraphicsToTransitionScript), nameof(HasGraphicsInTransitionScript));
            if (HasGraphicsInTransitionScript) LoadDynamicOverworldGraphicsCategories();
         }
      }

      private bool showGraphicsAsText;
      public bool ShowGraphicsAsText {
         get => showGraphicsAsText;
         set {
            Set(ref showGraphicsAsText, value, old => {
               graphicsText = null;
               NotifyPropertiesChanged(nameof(GraphicsText), nameof(CanAddDynamicGraphicsToTransitionScript), nameof(HasGraphicsInTransitionScript));
               if (value) {
                  LoadDynamicOverworldGraphicsCategories();
               }
            });
         }
      }

      #region Dynamic Overworld Graphics

      public const string DynamicGraphicsStartConstant = "map.dynamicgfx.start";
      /// <summary>
      /// The first of the 16 'graphics variable' ids (OBJ_EVENT_GFX_VAR_0): their picture is whatever the var holds at runtime.
      /// It is 240 in the vanilla games, but pokeemerald-expansion has far more sprites, so its ROM metadata says where they start.
      /// </summary>
      public static int GetDynamicGraphicsStart(IDataModel model) => model.TryGetUnmappedConstant(DynamicGraphicsStartConstant, out var start) && start > 0 ? start : 240;
      private int DynamicStart => GetDynamicGraphicsStart(element.Model);
      /// <summary>true for the 16 graphics-variable ids (OBJ_EVENT_GFX_VAR_0 .. OBJ_EVENT_GFX_VAR_F).</summary>
      public static bool IsDynamicGraphics(IDataModel model, int graphics) {
         var start = GetDynamicGraphicsStart(model);
         return graphics >= start && graphics < start + 16;
      }

      public bool CanAddDynamicGraphicsToTransitionScript => !HasGraphicsInTransitionScript && ShowGraphicsAsText && Graphics >= DynamicStart;

      public void AddDynamicGraphicsToTransitionScript() {
         if (HasGraphicsInTransitionScript) return;
         var scripts = new MapScriptCollection(parent.ViewPort, eventTemplate);
         scripts.Load(parent.GetMapModel());
         bool transitionScriptFound = false;
         foreach (var script in scripts.Scripts) {
            if (script.ScriptOptions[script.ScriptTypeIndex].Option != "Transition") continue;
            transitionScriptFound = true;
            var tool = parent.ViewPort.Tools.CodeTool;

            parent.ViewPort.GotoScript(script.ScriptAddress);
            var body = tool.Contents[0];
            body.Content = $"setvar gfx{Graphics - DynamicStart} 18" + Environment.NewLine + body.Content;
            GraphicsVariableSprites.Invalidate(element.Model);
            break;
         }
         if (!transitionScriptFound) {
            scripts.AddScript();
            scripts.Scripts.Last().ScriptTypeIndex = 2; // Transition
            AddDynamicGraphicsToTransitionScript();
         }
      }

      /// <summary>The first sprite that a script of this map gives to the graphics variable of this event (-1 if none does).</summary>
      private int FindGraphicsVariableSpriteInMap() {
         if (parent == null) return -1;
         var assignments = GraphicsVariableSprites.InMap(element.Model, parser, parent.GetMapModel());
         return assignments.For(Graphics - DynamicStart).DefaultIfEmpty(-1).First();
      }

      /// <summary>The first sprite that a script of this map gives to any graphics variable (-1 if none does).</summary>
      private int FindAnyGraphicsVariableSpriteInMap() {
         if (parent == null) return -1;
         var assignments = GraphicsVariableSprites.InMap(element.Model, parser, parent.GetMapModel());
         return assignments.Any().DefaultIfEmpty(-1).First();
      }

      public int PullValueFromTransitionScript() {
         var scripts = new MapScriptCollection(parent.ViewPort, eventTemplate);
         scripts.Load(parent.GetMapModel());
         foreach (var script in scripts.Scripts) {
            if (script.ScriptOptions[script.ScriptTypeIndex].Option != "Transition") continue;

            var spots = Flags.GetAllScriptSpots(Element.Model, parser, new[] { script.ScriptAddress }, 0x16).
               Where(spot => Element.Model.ReadMultiByteValue(spot.Address + 1, 2) == 0x4010 + Graphics - DynamicStart); // setvar gfxN

            var spot = spots.FirstOrDefault();
            if (spot is null) continue;
            var categoryIndex = dynamicCategories.SelectedIndex;
            var dynamicValue = element.Model.ReadMultiByteValue(spot.Address + 3, 2);
            return dynamicValue;
         }
         return -1;
      }

      public bool HasGraphicsInTransitionScript {
         get {
            if (!ShowGraphicsAsText) return false; // short circuit: if we're not doing dynamic graphics, don't show anything
            // check that we have a transition script that sets graphics
            var scripts = new MapScriptCollection(parent.ViewPort, eventTemplate);
            scripts.Load(parent.GetMapModel());
            foreach (var script in scripts.Scripts) {
               if (script.ScriptOptions[script.ScriptTypeIndex].Option != "Transition") continue;
               var spots = Flags.GetAllScriptSpots(Element.Model, parser, new[] { script.ScriptAddress }, 0x16).
                  Where(spot => Element.Model.ReadMultiByteValue(spot.Address + 1, 2) == 0x4010 + Graphics - DynamicStart); // setvar gfxN

               if (spots.Count() > 0) return true;
            }
            return false;
         }
      }
      public void UpdateDynamicOverworldGraphicsInScript() {
         if (!dynamicGraphicsLoaded || dynamicGraphicsIndex == -1) return;
         var scripts = new MapScriptCollection(parent.ViewPort, eventTemplate);
         scripts.Load(parent.GetMapModel());
         foreach (var script in scripts.Scripts) {
            if (script.ScriptOptions[script.ScriptTypeIndex].Option != "Transition") continue;

            var spots = Flags.GetAllScriptSpots(Element.Model, parser, new[] { script.ScriptAddress }, 0x16).
               Where(spot => Element.Model.ReadMultiByteValue(spot.Address + 1, 2) == 0x4010 + Graphics - DynamicStart); // setvar gfxN

            var spot = spots.FirstOrDefault();
            if (spot is null) continue;
            var categoryIndex = dynamicCategories.SelectedIndex;
            var dynamicOffset = 0;
            if (dynamicCategories.FilteredOptions[categoryIndex].Text == "Pokemon Icons") dynamicOffset = 10000;
            if (dynamicCategories.FilteredOptions[categoryIndex].Text == "Expanded Overworld Sprites") dynamicOffset = 20000;
            var existingValue = Element.Model.ReadMultiByteValue(spot.Address + 3, 2);
            if (existingValue == dynamicGraphicsIndex + dynamicOffset) continue;
            Element.Model.WriteMultiByteValue(spot.Address + 3, 2, Token, dynamicGraphicsIndex + dynamicOffset);
            GraphicsVariableSprites.Invalidate(Element.Model);
            RaiseEventVisualUpdated();
         }
      }
      private IDisposable previousCategoryListener;
      private void LoadDynamicOverworldGraphicsCategories() {
         dynamicGraphicsLoaded = true;
         var options = new List<string>();
         if (Element.Model.GetTable("graphics.overworld.sprites") != null) options.Add("Overworld Sprites");
         if (Element.Model.GetTable("graphics.overworld.pokemon") != null) options.Add("Pokemon Icons");
         if (Element.Model.GetTable("graphics.overworld.sprites20k") != null) options.Add("Expanded Overworld Sprites");
         if (options.Count == 0) return;
         var scripts = new MapScriptCollection(parent.ViewPort, eventTemplate);
         scripts.Load(parent.GetMapModel());
         var selectedIndex = 0;
         foreach (var script in scripts.Scripts) {
            if (script.ScriptOptions[script.ScriptTypeIndex].Option != "Transition") continue;
            var spots = Flags.GetAllScriptSpots(Element.Model, parser, new[] { script.ScriptAddress }, 0x16).
               Where(spot => Element.Model.ReadMultiByteValue(spot.Address + 1, 2) == 0x4010 + Graphics - DynamicStart); // setvar gfxN
            var spot = spots.FirstOrDefault();
            if (spot is null) continue;
            var desiredTable = Element.Model.ReadMultiByteValue(spot.Address + 3, 2) / 10000;
            if (desiredTable == 0) selectedIndex = options.IndexOf("Overworld Sprites");
            if (desiredTable == 1) selectedIndex = options.IndexOf("Pokemon Icons");
            if (desiredTable == 2) selectedIndex = options.IndexOf("Expanded Overworld Sprites");
            if (selectedIndex == -1) selectedIndex = 0;
         }

         previousCategoryListener?.Dispose();
         dynamicCategories.Update(options.Select((option, i) => new ComboOption(option, i)), selectedIndex);
         LoadDynamicOverworldGraphicsOptions();
         previousCategoryListener = dynamicCategories.Bind(nameof(FilteringComboOptions.SelectedIndex), (options, args) => LoadDynamicOverworldGraphicsOptions());
      }

      // depending on the selected category, load the sprites
      private void LoadDynamicOverworldGraphicsOptions() {
         var model = element.Model;
         var categoryText = dynamicCategories.FilteredOptions[dynamicCategories.SelectedIndex].Text;
         if (categoryText == "Overworld Sprites") {
            dynamicOptions.Clear();
            foreach (var option in Options) dynamicOptions.Add(VisualComboOption.CreateFromSprite(option.Text, option.PixelData, option.PixelWidth, option.Index, 2, true));
            DynamicOverworldGraphicsSelectedIndex = PullValueFromTransitionScript();
         } else if (categoryText == "Pokemon Icons") {
            int index = -1;
            dynamicOptions.Clear();
            foreach (var mon in new ModelTable(model, model.GetTable("graphics.overworld.pokemon"))) {
               index += 1;
               var run = model.GetNextRun(mon.GetAddress("sprite")) as ISpriteRun;
               if (run is null) continue;
               var sprite = ReadonlyPixelViewModel.Create(model, run);
               sprite = ReadonlyPixelViewModel.Crop(sprite, 0, 0, sprite.PixelWidth, sprite.PixelWidth);
               dynamicOptions.Add(VisualComboOption.CreateFromSprite(index.ToString(), sprite.PixelData, sprite.PixelWidth, index, 2, true));
            }
            DynamicOverworldGraphicsSelectedIndex = PullValueFromTransitionScript() - 10000;
         } else if (categoryText == "Expanded Overworld Sprites") {
            int index = -1;
            dynamicOptions.Clear();
            foreach (var item in new ModelTable(model, model.GetTable("graphics.overworld.sprites20k"))) {
               index += 1;
               var sprites = item.GetSubTable("sprites");
               if (sprites is null) continue;
               var run = model.GetNextRun(sprites[0].GetAddress("sprite")) as ISpriteRun;
               if (run is null) continue;
               var sprite = ReadonlyPixelViewModel.Create(model, run);
               dynamicOptions.Add(VisualComboOption.CreateFromSprite(index.ToString(), sprite.PixelData, sprite.PixelWidth, index, 2, true));
            }
            DynamicOverworldGraphicsSelectedIndex = PullValueFromTransitionScript() - 20000;
         }
      }
      // Filling these means rendering every sprite of a category (hundreds of pictures at twice the size), and the object events of a map are made again whenever the map's data
      // changes (every tile an event is dragged over, every property of a new event). Only the event whose panel is open needs them, so they are filled when the panel first asks.
      private readonly FilteringComboOptions dynamicCategories = new();
      private readonly ObservableCollection<VisualComboOption> dynamicOptions = new();
      private bool dynamicGraphicsLoaded;
      private int dynamicGraphicsIndex;
      private void EnsureDynamicGraphicsLoaded() {
         if (dynamicGraphicsLoaded || !showGraphicsAsText) return;
         LoadDynamicOverworldGraphicsCategories();
      }
      public FilteringComboOptions DynamicOverworldGraphicsCategories { get { EnsureDynamicGraphicsLoaded(); return dynamicCategories; } }
      public int DynamicOverworldGraphicsSelectedIndex { get { EnsureDynamicGraphicsLoaded(); return dynamicGraphicsIndex; } set => Set(ref dynamicGraphicsIndex, value, old => UpdateDynamicOverworldGraphicsInScript()); }
      public ObservableCollection<VisualComboOption> DynamicOverworldOptions { get { EnsureDynamicGraphicsLoaded(); return dynamicOptions; } }

      #endregion

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
            element.SetValue("range", (RangeY << 4) | (value & 0xF)); // (each is 4 bits: a bigger number must not spill into the other)
            rangeXY = null;
            NotifyPropertyChanged(nameof(RangeXY));
            RaiseEventVisualUpdated();
         }
      }

      public int RangeY {
         get => element.GetValue("range") >> 4;
         set {
            element.SetValue("range", ((value & 0xF) << 4) | RangeX);
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
            if (parts[0].TryParseInt(out int x) && parts[1].TryParseInt(out int y)) element.SetValue("range", ((y & 0xF) << 4) | (x & 0xF));
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
            berryStageVisuals = null;
            NotifyPropertiesChanged(nameof(ShowBerryContent), nameof(BerryText), nameof(HasBerrySpot), nameof(HasNoBerrySpot), nameof(SelectedBerry), nameof(SelectedBerryStage), nameof(BerryStageVisuals));
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
               nameof(ShowItemContents), nameof(ItemContents), nameof(ShowItemQuantity), nameof(ItemQuantity),
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
               nameof(ShowItemContents), nameof(ItemContents), nameof(ShowItemQuantity), nameof(ItemQuantity),
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

      // The option lists below are large (every overworld sprite, every item, every trainer)
      // and are only needed when this event is shown in the event panel.
      // Object events get created for every event on the map, so build these lists on first use instead of in the constructor.
      private readonly IReadOnlyList<IPixelViewModel> overworldSprites;
      private ObservableCollection<VisualComboOption> options;
      public ObservableCollection<VisualComboOption> Options {
         get {
            if (options == null) {
               var list = new ObservableCollection<VisualComboOption>();
               for (int i = 0; i < overworldSprites.Count; i++) list.Add(VisualComboOption.CreateFromSprite(i.ToString(), overworldSprites[i].PixelData, overworldSprites[i].PixelWidth, i, 2, true));
               options = list;
            }
            return options;
         }
      }

      public FilteringComboOptions FacingOptions { get; } = new();

      private ObservableCollection<string> classOptions;
      public ObservableCollection<string> ClassOptions => classOptions ??= new(element.Model.GetOptions(HardcodeTablesModel.TrainerClassNamesTable));

      private readonly FilteringComboOptions itemOptions = new();
      private bool itemOptionsLoaded;
      public FilteringComboOptions ItemOptions {
         get {
            if (!itemOptionsLoaded) {
               itemOptionsLoaded = true;
               itemOptions.Update(ComboOption.Convert(element.Model.GetOptions(HardcodeTablesModel.ItemsTableName)), ItemContents);
               itemOptions.Bind(nameof(itemOptions.SelectedIndex), (sender, e) => ItemContents = itemOptions.ModelValue);
            }
            return itemOptions;
         }
      }

      #region Extended Properties

      // For certain simple events (npcs, trainers, items, signposts),
      // We can provide an enriched editing experience in the event panel.
      // These are the 'show' properties for those controls.

      /// <summary>
      /// pokeemerald-expansion's item balls all run the same script (Common_EventScript_FindItem) and keep the item
      /// in the event's 'berry id' field and the amount in the X range, so the item is changed without touching any script.
      /// </summary>
      public bool IsSharedScriptItemBall => element.Model.TryGetUnmappedConstant(FindItemScriptConstant, out var script) && script >= 0 && ScriptAddress == script;
      public const string FindItemScriptConstant = "map.finditem.script";

      public bool ShowItemContents => IsSharedScriptItemBall || EventTemplate.GetItemAddress(element.Model, this) != Pointer.NULL;

      public int ItemContents {
         get {
            if (IsSharedScriptItemBall) return TrainerRangeOrBerryID;
            var itemAddress = EventTemplate.GetItemAddress(element.Model, this);
            if (itemAddress == Pointer.NULL) return -1;
            return element.Model.ReadMultiByteValue(itemAddress, 2);
         }
         set {
            if (IsSharedScriptItemBall) {
               TrainerRangeOrBerryID = value;
               ItemOptions.Update(ItemOptions.AllOptions, value);
               NotifyPropertyChanged();
               return;
            }
            var itemAddress = EventTemplate.GetItemAddress(element.Model, this);
            if (itemAddress == Pointer.NULL) return;
            element.Model.WriteMultiByteValue(itemAddress, 2, element.Token, value);
            ItemOptions.Update(ItemOptions.AllOptions, value);
            NotifyPropertyChanged();
         }
      }

      public bool ShowItemQuantity => IsSharedScriptItemBall;
      public int ItemQuantity {
         get => RangeX == 0 ? 1 : RangeX;
         set {
            RangeX = value.LimitToRange(1, 15);
            NotifyPropertyChanged();
         }
      }

      #region Npc Content

      public bool ShowNpcText => EventTemplate.GetNPCTextPointer(element.Model, this) != Pointer.NULL;

      private TextEditorViewModel npcTextEditor;
      public TextEditorViewModel NpcTextEditor => CreateTextEditor(ref npcTextEditor, () => EventTemplate.GetNPCTextPointer(element.Model, this));

      #endregion

      #region Trainer Content

      private readonly FilteringComboOptions trainerOptions = new();
      private bool trainerOptionsLoaded;
      public FilteringComboOptions TrainerOptions {
         get {
            if (!trainerOptionsLoaded) {
               trainerOptionsLoaded = true;
               RefreshTrainerOptions();
               trainerOptions.Bind(nameof(trainerOptions.SelectedIndex), (options, args) => {
                  this.eventTemplate.UseTrainerFlag(trainerOptions.SelectedIndex);
                  var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
                  element.Model.WriteMultiByteValue(trainerContent.TrainerIndexAddress, 2, () => element.Token, trainerOptions.SelectedIndex);
                  TeamVisualizations.Clear();
                  trainerSprite = null;
                  trainerName = teamText = null;
                  trainerBeforeText = trainerWinText = trainerAfterText = null;
                  NotifyPropertiesChanged(nameof(TrainerSprite), nameof(TrainerName), nameof(TrainerBeforeTextEditor), nameof(TrainerWinTextEditor), nameof(TrainerAfterTextEditor), nameof(TrainerTeam), nameof(TrainerClass));
               });
            }
            return trainerOptions;
         }
      }

      public bool ShowTrainerContent => EventTemplate.GetTrainerContent(element.Model, this) != null && (TrainerType != 0 || IsBattleInsideScript);

      /// <summary>
      /// Rivals and gym leaders don't use the trainer-type fields: their (longer) script starts a trainerbattle somewhere after
      /// the dialogue. The trainer editor is still useful for them.
      /// </summary>
      public bool IsBattleInsideScript => EventTemplate.FindTrainerBattleInScript(element.Model, parser, this) != Pointer.NULL;

      // The trainer team editor is only needed when this event is shown in the event panel.
      // Serializing the team (and rendering its icons) is expensive, so build it on first use.
      private TextEditorViewModel trainerContentEditor;
      private bool updatingTrainerContentEditor;
      public TextEditorViewModel TrainerContent {
         get {
            if (trainerContentEditor == null) {
               trainerContentEditor = new TextEditorViewModel { PreFormatter = ExpansionTrainerTeamRun.TeamFormatterFor(element.Model) };
               RefreshTrainerContentEditor();
               trainerContentEditor.Bind(nameof(TextEditorViewModel.Content), (sender, e) => {
                  if (updatingTrainerContentEditor) return;
                  TrainerTeam = sender.Content;
               });
            }
            return trainerContentEditor;
         }
      }

      /// <summary>
      /// Keep the team editor in sync with the team (for example, after picking a different trainer).
      /// Without this, the editor keeps showing the previous trainer's team, and editing it would write that team into the new trainer.
      /// </summary>
      private void RefreshTrainerContentEditor() {
         if (trainerContentEditor == null) return;
         var text = TrainerTeam ?? string.Empty;
         if (trainerContentEditor.Content == text) return;
         updatingTrainerContentEditor = true;
         try {
            trainerContentEditor.Content = text;
         } finally {
            updatingTrainerContentEditor = false;
         }
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
            if (!trainerContent.TrainerSpriteAddress.InRange(0, element.Model.Count)) return null;
            var spriteIndex = element.Model[trainerContent.TrainerSpriteAddress];
            // hacks can point at a sprite that doesn't exist in the table: show no sprite rather than failing to show the event
            var spriteAddress = EventTemplate.GetTrainerSpriteAddress(element.Model, spriteIndex);
            if (!spriteAddress.InRange(0, element.Model.Count)) return null;
            if (element.Model.GetNextRun(spriteAddress) is not ISpriteRun spriteRun || spriteRun.Start != spriteAddress) return null;
            return trainerSprite = ReadonlyPixelViewModel.Create(element.Model, spriteRun, true, .5);
         }
      }

      private string trainerName;
      public string TrainerName {
         get {
            if (trainerName != null) return trainerName;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return null;
            var text = element.Model.TextConverter.Convert(element.Model, trainerContent.TrainerNameAddress, trainerContent.TrainerNameLength);
            return trainerName = text.Trim('"');
         }
         set {
            trainerName = value;
            var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
            if (trainerContent == null) return;
            var bytes = element.Model.TextConverter.Convert(value, out _);
            var nameLength = trainerContent.TrainerNameLength;
            while (bytes.Count > nameLength) {
               bytes.RemoveAt(bytes.Count - 1);
               bytes[bytes.Count - 1] = 0xFF;
            }
            while (bytes.Count < nameLength) bytes.Add(0);
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
         var layout = TrainerLayout.For(trainerTable.Run);
         for (int i = 0; i < trainers.Count; i++) {
            options.Add(CreateOption(i, element.Model[trainerTable[i].Start + layout.ClassOffset], trainers[i]));
         }
         trainerOptions.Update(options, trainerContent?.TrainerIndex ?? 0);
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
            if (element.Model.GetNextRun(address) is not ITrainerTeamRun run) return null;
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
            if (element.Model.GetNextRun(address) is not ITrainerTeamRun run) return;
            if (run.Start != address) return;
            var newRun = run.DeserializeRun(value, element.Token, out _, out _);
            element.Model.ObserveRunWritten(element.Token, newRun);
            if (newRun.Start != run.Start) DataMoved.Raise(this, new("Trainer Team", newRun.Start));
            UpdateTeamVisualizations(newRun as IStreamRun);
            NotifyPropertyChanged();
         }
      }

      public ObservableCollection<IPixelViewModel> TeamVisualizations { get; } = new();

      private void UpdateTeamVisualizations(IStreamRun team) {
         TeamVisualizations.Clear();
         if (team == null) return;
         foreach (var vis in team.Visualizations) {
            TeamVisualizations.Add(vis);
         }
      }

      private StubCommand openTrainerData;
      public ICommand OpenTrainerData => StubCommand(ref openTrainerData, () => {
         var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
         if (trainerContent == null) return;
         gotoAddress(trainerContent.TrainerStart);
      });

      public static ComboOption CreateOption(IReadOnlyList<string> classOptions, int index, int trainerClass, string name) => new($"{index} - {classOptions[trainerClass.LimitToRange(0, classOptions.Count - 1)]} {name}", index);

      private ComboOption CreateOption(int index, int trainerClass, string name) => CreateOption(ClassOptions, index, trainerClass, name);

      public IReadOnlyList<AutocompleteItem> GetTrainerAutocomplete(string line, int lineIndex, int characterIndex) {
         var trainerContent = EventTemplate.GetTrainerContent(element.Model, this);
         if (trainerContent == null) return null;
         var address = element.Model.ReadPointer(trainerContent.TeamPointer);
         if (address < 0 || address >= element.Model.Count) return null;
         if (element.Model.GetNextRun(address) is not ITrainerTeamRun run) return null;
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
         if (content == null) return; // not a rematch trainer: there is nothing to replace
         string before = "<auto>", win = "<auto>", after = "<auto>";
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
            if (content != null && TutorOptions.AllOptions == null) {
               // the expansion hands the tutor script a move id: the list is every move. Vanilla hands it an index into the tutor table.
               var tutorList = Flags.IsExpansion(element.Model) ? HardcodeTablesModel.GetMoveNameTable(element.Model) : HardcodeTablesModel.MoveTutors;
               TutorOptions.Update(ComboOption.Convert(element.Model.GetOptions(tutorList)), TutorNumber);
               TutorOptions.Bind(nameof(TutorOptions.SelectedIndex), (sender, e) => TutorNumber = TutorOptions.SelectedIndex);
            }
            return tutorContent.Value != null;
         }
      }

      private TextEditorViewModel tutorInfoEditor, tutorWhichPokemonEditor, tutorFailedEditor, tutorSuccessEditor;
      public TextEditorViewModel TutorInfoText => CreateTextEditor(ref tutorInfoEditor, () => tutorContent.Value?.InfoPointer);
      public TextEditorViewModel TutorWhichPokemonText => CreateTextEditor(ref tutorWhichPokemonEditor, () => tutorContent.Value?.WhichPokemonPointer);
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
            EventTemplate.UpdateTutorMoveName(element.Model, tutorContent.Value, value, Token); // the question names the move
         }
      }

      public FilteringComboOptions TutorOptions { get; } = new();

      public void GotoTutors() {
         var table = Flags.IsExpansion(element.Model) ? HardcodeTablesModel.GetMoveNameTable(element.Model) : HardcodeTablesModel.MoveTutors;
         var tutors = element.Model.GetTableModel(table);
         if (tutors == null || TutorNumber < 0 || TutorNumber >= tutors.Count) return;
         gotoAddress(tutors[TutorNumber].Start);
      }

      #endregion

      #region Trade Content

      private Lazy<TradeEventContent> tradeContent;

      public FilteringComboOptions TradeOptions { get; } = new();

      public bool ShowTradeContent {
         get {
            var content = tradeContent.Value;
            if (content != null && TradeOptions.AllOptions == null) {
               var pokenames = element.Model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(element.Model));
               var options = new List<string>();
               foreach (var trade in element.Model.GetTableModel(HardcodeTablesModel.TradeTable)) {
                  // vanilla calls the species you hand over 'give'; the expansion calls it 'requested'
                  var giveField = trade.HasField("give") ? "give" : "requested";
                  if (!trade.TryGetValue("receive", out int receive) || !trade.TryGetValue(giveField, out int give) || give >= pokenames.Count || receive >= pokenames.Count) {
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
               var options = ComboOption.Convert(element.Model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(element.Model)));
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
      public void GotoPokemon() => gotoAddress(element.Model.GetTableModel(HardcodeTablesModel.GetSpeciesNameTable(element.Model))[PokemonOptions.SelectedIndex].Start);
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

      /// <summary>A berry tree: an object with the berry-tree movement type whose 'trainer range' field holds the tree id.</summary>
      public bool ShowBerryContent => TrainerType == 0 && TrainerRangeOrBerryID != 0 && IsBerryTreeMovement(element.Model, MoveType);

      /// <summary>True when the new-game script plants something on this tree (so there's a setberrytree command to edit).</summary>
      public bool HasBerrySpot => ShowBerryContent && berries.BerryMap.ContainsKey(TrainerRangeOrBerryID);
      public bool HasNoBerrySpot => ShowBerryContent && !HasBerrySpot;

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

      public ObservableCollection<string> BerryOptions => berries.BerryOptions;

      /// <summary>Which berry the new-game script plants here. Changing it rewrites the setberrytree command.</summary>
      public int SelectedBerry {
         get => berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out var spot) ? spot.BerryID : -1;
         set {
            if (value < 0 || !berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out var spot) || spot.BerryID == value) return;
            var model = element.Model;
            if (spot.Address + 3 >= model.Count) return;
            var token = element.Token;
            token.ChangeData(model, spot.Address + 2, (byte)(value + berries.IdOffset));
            berries.BerryMap[TrainerRangeOrBerryID] = spot with { BerryID = value };
            berryStageVisuals = null;
            NotifyPropertiesChanged(nameof(SelectedBerry), nameof(BerryText), nameof(BerryStageVisuals));
            RaiseEventVisualUpdated();
         }
      }

      public static readonly IReadOnlyList<string> BerryStageNames = new[] { "Planted", "Sprouted", "Taller", "Flowering", "Has berries", "Trunk", "Budding" };

      /// <summary>The growth stages this ROM's trees have: the vanilla games stop at 'Has berries', the expansion also has the trunk and budding stages.</summary>
      private int BerryStageCount => BerryTreeGraphics.StageCount(element.Model).LimitToRange(1, BerryStageNames.Count);
      public IReadOnlyList<string> BerryStageOptions => BerryStageNames.Take(BerryStageCount).ToList();

      /// <summary>How far along the tree is when the game starts (0 = planted ... 4 = has berries).</summary>
      public int SelectedBerryStage {
         get => berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out var spot) ? (spot.Stage - 1).LimitToRange(0, BerryStageCount - 1) : -1;
         set {
            if (value < 0 || !berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out var spot) || spot.Stage == value + 1) return;
            var model = element.Model;
            if (spot.Address + 3 >= model.Count) return;
            element.Token.ChangeData(model, spot.Address + 3, (byte)(value + 1));
            berries.BerryMap[TrainerRangeOrBerryID] = spot with { Stage = value + 1 };
            NotifyPropertyChanged();
            RaiseEventVisualUpdated();
         }
      }

      public void GotoBerryCode() {
         if (berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out BerrySpot spot)) {
            gotoAddress(spot.Address);
         } else {
            var script = element.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "scripts.newgame.berries");
            if (script < 0) script = element.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "scripts.newgame.setflags");
            if (script >= 0) gotoAddress(script);
         }
      }

      /// <summary>
      /// Berry trees don't use their own graphics: the game draws gBerries[berry].treePicTable with the tree's growth stage.
      /// Returns null if this object isn't a berry tree the editor knows about.
      /// </summary>
      private IPixelViewModel RenderBerryTree(IDataModel model) {
         if (TrainerType != 0 || TrainerRangeOrBerryID == 0) return null;
         if (!IsBerryTreeMovement(model, MoveType)) return null;
         if (!berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out var spot)) return null;
         return RenderBerryStage(model, spot.BerryID, spot.Stage - 1);
      }

      /// <summary>
      /// Which frame of the berry's picture table the game shows (first frame of that stage's animation) for each growth stage,
      /// in the order of BerryStageNames: planted, sprouted, taller, flowering, berries, then the two save-compatibility stages
      /// (trunk, budding) that look like 'taller'.
      /// </summary>
      private static readonly int[] BerryFrameForStage = { 0, 1, 3, 5, 7, 3, 3 };

      /// <param name="stage">0 = planted ... 6 = budding</param>
      public static IPixelViewModel RenderBerryStage(IDataModel model, int berryIndex, int stage) {
         // the expansion lists the tree's frames and palettes in the berry stats, the vanilla games in two arrays (see BerryTreeGraphics)
         if (!BerryTreeGraphics.TryGetTables(model, berryIndex, out var picTable, out var slots)) return null;
         stage = stage.LimitToRange(0, Math.Min(BerryFrameForStage.Length, BerryTreeGraphics.StageCount(model)) - 1);
         int frame = BerryFrameForStage[stage];
         // the frame table ends where the data isn't a plausible {pointer, size} pair: clamp to the last real frame
         int frameCount = 0;
         while (frameCount < 16 && picTable + frameCount * 8 + 8 <= model.Count && model.ReadPointer(picTable + frameCount * 8) >= 0 && model.ReadMultiByteValue(picTable + frameCount * 8 + 4, 2) is > 0 and <= 4096) frameCount++;
         if (frameCount == 0) return null;
         frame = Math.Min(frame, frameCount - 1);
         var data = model.ReadPointer(picTable + frame * 8);
         var size = model.ReadMultiByteValue(picTable + frame * 8 + 4, 2);
         const int widthTiles = 2;
         int heightTiles = size / 32 / widthTiles;
         if (heightTiles <= 0 || data < 0 || data + size > model.Count) return null;
         var palettes = model.GetTableModel(HardcodeTablesModel.OverworldPalettes);
         int slot = slots + stage < model.Count ? model[slots + stage] - 2 : -1;
         IReadOnlyList<short> palette = null;
         if (palettes != null && slot >= 0 && slot < palettes.Count && model.GetNextRun(palettes[slot].GetAddress("pal")) is IPaletteRun paletteRun) palette = paletteRun.GetPalette(model, 0);
         palette ??= TileViewModel.CreateDefaultPalette(16);
         // like every other sprite: palette entry 0 is the background, drawn as transparent
         // (the map treats -1 as 'no transparency', so the background needs a real color that no other pixel uses)
         palette = SpriteTool.CreatePaletteWithUniqueTransparentColor(palette);
         int width = widthTiles * 8, height = heightTiles * 8;
         var pixels = new short[width * height];
         for (int t = 0; t < widthTiles * heightTiles; t++) {
            int tx = (t % widthTiles) * 8, ty = (t / widthTiles) * 8;
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  var b = model[data + t * 32 + y * 4 + x / 2];
                  var index = x % 2 == 0 ? b & 0xF : b >> 4;
                  pixels[(ty + y) * width + tx + x] = palette[index];
               }
            }
         }
         return new ReadonlyPixelViewModel(width, height, pixels, palette[0]);
      }

      /// <summary>A picture of the tree at every growth stage, for the 'Growth' dropdown.</summary>
      private ObservableCollection<VisualComboOption> berryStageVisuals;
      public ObservableCollection<VisualComboOption> BerryStageVisuals {
         get {
            if (berryStageVisuals != null) return berryStageVisuals;
            var options = berryStageVisuals = new ObservableCollection<VisualComboOption>();
            if (!berries.BerryMap.TryGetValue(TrainerRangeOrBerryID, out var spot)) return options;
            for (int stage = 0; stage < BerryStageCount; stage++) {
               var image = RenderBerryStage(element.Model, spot.BerryID, stage);
               if (image == null) { options.Add(VisualComboOption.CreateFromSprite(BerryStageNames[stage], new short[16 * 16], 16, stage, 1, false)); continue; }
               options.Add(VisualComboOption.CreateFromSprite(BerryStageNames[stage], image.PixelData, image.PixelWidth, stage, 2, false));
            }
            return options;
         }
      }

      private static int berryTreeMovementType = -2;
      private static bool IsBerryTreeMovement(IDataModel model, int moveType) {
         if (berryTreeMovementType == -2) {
            berryTreeMovementType = 57; // MOVEMENT_TYPE_BERRY_TREE_GROWTH in the vanilla games
            // 'movementtypes' is HMA's list of movement actions; a ROM's object movement types are in 'objectmovementtypes' (or the CamelCase 'FacingOptions')
            foreach (var listName in new[] { "objectmovementtypes", "FacingOptions" }) {
               if (!model.TryGetList(listName, out var types)) continue;
               var index = types.FindIndex(name => name != null && name.Replace("_", "").Contains("BerryTree", StringComparison.OrdinalIgnoreCase));
               if (index >= 0) { berryTreeMovementType = index; break; }
            }
         }
         return moveType == berryTreeMovementType;
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

      // The 'basic' content requires walking the event's script several times.
      // It's only shown for the selected event, so it's filled the first time any of it is requested.
      private bool basicContentLoaded;
      private void EnsureBasicContent() {
         if (basicContentLoaded) return;
         basicContentLoaded = true;
         if (ShowNoContent) FillBasicContent();
      }

      private readonly ObservableCollection<EventTextViewModel> basicText = new();
      private readonly ObservableCollection<int> basicItemAddresses = new(), basicFlagAddresses = new();
      private readonly FilteringComboOptions basicItem = new();
      public ObservableCollection<EventTextViewModel> BasicText { get { EnsureBasicContent(); return basicText; } }
      public ObservableCollection<int> BasicItemAddresses { get { EnsureBasicContent(); return basicItemAddresses; } }
      public ObservableCollection<int> BasicFlagAddresses { get { EnsureBasicContent(); return basicFlagAddresses; } }
      private int basicFlag = -1;
      public FilteringComboOptions BasicItem { get { EnsureBasicContent(); return basicItem; } }
      public bool HasBasicItem => (BasicItem.AllOptions?.Count ?? 0) > 0;
      public int BasicFlag {
         get { EnsureBasicContent(); return basicFlag; }
         set => Set(ref basicFlag, value, old => NotifyPropertyChanged(nameof(HasBasicFlag)));
      }
      public bool HasBasicFlag => BasicFlag != -1;
      private string basicFlagText = string.Empty;
      public string BasicFlagText {
         get { EnsureBasicContent(); return basicFlagText; }
         set => Set(ref basicFlagText, value, old => {
            if (!basicFlagText.TryParseHex(out int result)) return;
            foreach (var address in BasicFlagAddresses) element.Model.WriteMultiByteValue(address, 2, Token, result);
            basicFlag = result;
            NotifyPropertyChanged(nameof(BasicFlag));
         });
      }

      private void FillBasicContent() {
         // text
         BasicText.Clear();
         foreach (var spot in Flags.GetAllScriptSpots(Element.Model, parser, new[] { ScriptAddress }, 0x0F)) {
            var element = new EventTextViewModel() { PointerAddress = spot.Address + 2, Text = { Content = GetText(spot.Address + 2) } };
            element.Text.Bind(nameof(TextEditorViewModel.Content), (editor, e) => {
               var newStart = SetText(spot.Address + 2, editor.Content, "Unknown", "BasicText");
               if (newStart != -1) element.PointerAddress = newStart;
            });
            BasicText.Add(element);
         }

         // items
         BasicItemAddresses.Clear();
         var filter = new List<byte>();
         foreach (var line in parser.DependsOn(HardcodeTablesModel.ItemsTableName)) {
            if (line is MacroScriptLine macro && macro.Args[0] is SilentMatchArg silent) filter.Add(silent.ExpectedValue);
            if (line is ScriptLine sl) filter.Add(line.LineCode[0]);
         }
         var spots = new List<int>();
         var item = -1;
         foreach (var spot in Flags.GetAllScriptSpots(element.Model, parser, new[] { ScriptAddress }, true, filter.ToArray())) {
            var offset = spot.Address + spot.Line.Args.Until(arg => arg.EnumTableName == HardcodeTablesModel.ItemsTableName).Sum(arg => arg.Length(Element.Model, -1)) + spot.Line.LineCode.Count;
            var chosenItem = Element.Model.ReadMultiByteValue(offset, 2);
            if (item == -1 || item == chosenItem) {
               item = chosenItem;
               spots.Add(offset);
            } else {
               spots.Clear();
               break;
            }
         }
         if (spots.Count > 0) {
            BasicItem.Update(ComboOption.Convert(element.Model.GetOptions(HardcodeTablesModel.ItemsTableName)), item);
            foreach (var address in spots.Distinct()) BasicItemAddresses.Add(address);
            BasicItem.Bind(nameof(BasicItem.SelectedIndex), (sender, e) => {
               foreach (var address in BasicItemAddresses) element.Model.WriteMultiByteValue(address, 2, Token, sender.SelectedIndex);
            });
         }

         // flags
         BasicFlagAddresses.Clear();
         spots.Clear();
         var flag = -1;
         foreach (var spot in Flags.GetAllScriptSpots(element.Model, parser, new[] { ScriptAddress }, false, 0x29, 0x2A, 0x2B)) { // setflag, clearflag, checkflag
            var offset = spot.Address + 1;
            var chosenFlag = element.Model.ReadMultiByteValue(offset, 2);
            if (flag == -1 || flag == chosenFlag) {
               flag = chosenFlag;
               spots.Add(offset);
            } else {
               spots.Clear();
               break;
            }
         }
         if (spots.Count > 0) {
            BasicFlag = flag;
            foreach (var address in spots.Distinct()) BasicFlagAddresses.Add(address);
            basicFlagText = flag.ToString("X4");
         }
      }

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

      private int SetText(int? pointer, string value, string type, [CallerMemberName] string propertyName = null) {
         if (pointer == null) return -1;
         var newStart = base.SetText((int)pointer, value, propertyName);
         if (newStart != -1) DataMoved.Raise(this, new(type, newStart));
         return newStart;
      }

      #endregion

      public ObjectEventViewModel(BlockMapViewModel parent, Action<int> gotoAddress, ModelArrayElement objectEvent, EventTemplate eventTemplate, IReadOnlyList<IPixelViewModel> sprites, IPixelViewModel defaultSprite, BerryInfo berries) : base(objectEvent, "objectCount") {
         this.parent = parent;
         this.gotoAddress = gotoAddress;
         this.eventTemplate = eventTemplate;
         this.berries = berries;
         overworldSprites = sprites;
         DefaultOW = defaultSprite;
         showGraphicsAsText = Graphics >= sprites.Count; // the lists of dynamic graphics are filled when the event panel first asks for them
         objectEvent.Model.TryGetList("FacingOptions", out var list);
         FacingOptions.Update(ComboOption.Convert(list), MoveType);
         FacingOptions.Bind(nameof(FacingOptions.SelectedIndex), (sender, e) => {
            if (FacingOptions.SelectedIndex >= 0) MoveType = FacingOptions.ModelValue; // (a movement type without a name selects nothing: keep the number it has)
         });

         tutorContent = new Lazy<TutorEventContent>(() => EventTemplate.GetTutorContent(element.Model, parser, this));
         martContent = new Lazy<MartEventContent>(() => EventTemplate.GetMartContent(element.Model, parser, this));
         tradeContent = new Lazy<TradeEventContent>(() => EventTemplate.GetTradeContent(element.Model, parser, this.ScriptAddress));
         legendaryContent = new Lazy<LegendaryEventContent>(() => EventTemplate.GetLegendaryEventContent(element.Model, parser, this));

         UpdateScriptError(ScriptAddress);

         PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(TrainerTeam)) RefreshTrainerContentEditor();
         };
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
         if (parent != null) GraphicsVariableSprites.RegisterParser(model, parser);
         EventRender = RenderBerryTree(model) ?? Render(model, owTable, DefaultOW, Graphics, facing, FindGraphicsVariableSpriteInMap, FindAnyGraphicsVariableSpriteInMap);
         NotifyPropertyChanged(nameof(EventRender));
      }

      /// <param name="facing">(0, 1, 2, 3) = (down, up, left, right)</param>
      /// <param name="dynamicSprites">For a graphics variable: the sprite that a script of the map gives to this variable (-1 if none does).</param>
      /// <param name="dynamicFallback">For a graphics variable: a sprite that a script of the map gives to any graphics variable (-1 if none does).</param>
      public static IPixelViewModel Render(IDataModel model, ModelTable owTable, IPixelViewModel defaultOW, int index, int facing, Func<int> dynamicSprites, Func<int> dynamicFallback = null) {
         // special case: gfx variables
         // Which sprite the var holds is decided by scripts at runtime (a transition script on some maps, a trigger, an NPC's own script, ... on others).
         // What the editor shows instead is a View menu choice (GraphicsVariableDisplay).
         if (IsDynamicGraphics(model, index)) return RenderDynamicGraphics(model, owTable, defaultOW, index, dynamicSprites, dynamicFallback);

         // special case: expanded OWs for pokemon / sprites20k (HMA expansion utility integration)
         ModelTable dataTable;
         if (index.InRange(10000, 20000)) {
            dataTable = model.GetTableModel("graphics.overworld.pokemon");
            index -= 10000;

         } else if (index.InRange(20000, 30000)) {
            dataTable = model.GetTableModel("graphics.overworld.sprites20k");
            index -= 20000;

         } else {
            if (owTable == null || index >= owTable.Count) return defaultOW;
            dataTable = owTable[index].GetSubTable("data");
            index = 0;

         }

         if (dataTable is null) return defaultOW;
         if (index >= dataTable.Count) return defaultOW;
         var data = dataTable[index];
         var sprites = data.GetSubTable("sprites");

         int graphicsAddress;
         var requestedFacing = -1;
         bool invisible = false;
         bool flip = false;
         if (sprites == null) {
            // overworld.pokemon
            graphicsAddress = data.GetAddress("sprite");
            if (graphicsAddress == Pointer.NULL) return defaultOW;
         } else {
            // overworld.sprites or overworld.sprites20k
            invisible = facing == 76;
            flip = facing == 3;
            if (facing == 3) facing = 2;
            requestedFacing = facing;
            if (facing >= sprites.Count) facing = 0;
            graphicsAddress = sprites.Run.Start;
         }

         var pointerAddress = data.Start;
         var graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         var paletteRun = graphicsRun.FindRelatedPalettes(model, pointerAddress).FirstOrDefault();
         if (facing != -1 && sprites is not null) {
            var sprite = sprites[facing];
            graphicsAddress = sprite.GetAddress("sprite");
            graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         }
         if (graphicsRun == null) return defaultOW;
         if (paletteRun == null) return defaultOW;
         var ow = ReadonlyPixelViewModel.Create(model, graphicsRun, paletteRun, true);
         if (sprites is null) ow = ReadonlyPixelViewModel.Crop(ow, 0, 0, ow.PixelWidth, ow.PixelHeight / 2);
         else ow = CropToFrame(ow, data.GetValue("width"), data.GetValue("height"), sprites.Count, requestedFacing);
         if (invisible) ow = BuildInvisibleEventRender(ow);
         if (flip) ow = ow.ReflectX();
         return ow;
      }

      /// <summary>
      /// A sprite whose frames are all in one tall image (pokeemerald-expansion 'relative frames': the player characters, the bike, surfing, fishing, ...)
      /// is a single list entry that is several frames high. Only one frame is shown: the facing it is drawn with (down, up, left) or the first frame (down).
      /// The size of one frame is the width and height the sprite declares.
      /// </summary>
      public static IPixelViewModel CropToFrame(IPixelViewModel image, int frameWidth, int frameHeight, int listCount, int facing) {
         if (image == null || frameWidth <= 0 || frameHeight <= 0) return image;
         frameWidth = (frameWidth + 7) / 8 * 8; // the pictures are made of 8x8 tiles
         frameHeight = (frameHeight + 7) / 8 * 8;
         if (image.PixelHeight <= frameHeight) return image; // not a sheet
         var frames = image.PixelHeight / frameHeight;
         var frame = listCount == 1 && facing > 0 && facing < frames && facing < 3 ? facing : 0;
         return ReadonlyPixelViewModel.Crop(image, 0, frame * frameHeight, Math.Min(frameWidth, image.PixelWidth), frameHeight);
      }

      /// <summary>The View menu choice for how graphics-variable events are drawn. Shared by every map (saved with the settings).</summary>
      public static GraphicsVariableDisplay GraphicsVariableDisplay { get; set; } = GraphicsVariableDisplay.ScriptSprite;

      private static IPixelViewModel RenderDynamicGraphics(IDataModel model, ModelTable owTable, IPixelViewModel defaultOW, int index, Func<int> mapSprite, Func<int> mapAnySprite) {
         var variable = index - GetDynamicGraphicsStart(model);
         return ChooseDynamicPicture(
            GraphicsVariableDisplay,
            defaultOW,
            () => GetFirstPokemonIcon(model) is { PixelWidth: > 0 } icon ? icon : null,
            () => GetDynamicPlaceholder(model, owTable, defaultOW),
            new Func<IEnumerable<int>>[] {
               // the first sprite that a script gives to this variable: in the map first, then in any other script, then (the map's) other variables
               () => new[] { mapSprite?.Invoke() ?? -1 },
               () => GraphicsVariableSprites.Everywhere(model).For(variable),
               () => new[] { mapAnySprite?.Invoke() ?? -1 },
            },
            sprite => {
               if (!CanRenderSprite(model, owTable, sprite)) return null;
               var picture = Render(model, owTable, defaultOW, sprite, 0, () => -1);
               return ReferenceEquals(picture, defaultOW) ? null : picture;
            });
      }

      /// <summary>
      /// The picture of a graphics-variable event for the chosen display mode. The pieces that cost something are only asked for when the mode needs them.
      /// </summary>
      /// <param name="questionMark">The question mark picture of the original HexManiacAdvance.</param>
      /// <param name="firstPokemon">The first frame of the icon of the first Pokemon of the Pokedex (null if there is none).</param>
      /// <param name="placeholder">The blacked-out sprite with a question mark.</param>
      /// <param name="candidates">Where to look for the sprite a script gives to the variable, best place first: each gives the sprites in the order the scripts name them.</param>
      /// <param name="renderSprite">The first frame of a sprite as a script names it (null if this ROM cannot draw it).</param>
      public static IPixelViewModel ChooseDynamicPicture(GraphicsVariableDisplay mode, IPixelViewModel questionMark, Func<IPixelViewModel> firstPokemon, Func<IPixelViewModel> placeholder, IEnumerable<Func<IEnumerable<int>>> candidates, Func<int, IPixelViewModel> renderSprite) {
         switch (mode) {
            case GraphicsVariableDisplay.QuestionMark:
               return questionMark ?? placeholder();
            case GraphicsVariableDisplay.FirstPokemonIcon:
               return firstPokemon?.Invoke() ?? questionMark ?? placeholder();
            case GraphicsVariableDisplay.BlackedOutQuestionMark:
               return placeholder();
         }
         foreach (var candidate in candidates) {
            foreach (var sprite in candidate()) {
               var picture = renderSprite(sprite);
               if (picture != null) return picture;
            }
         }
         return placeholder(); // no script gives the variable a sprite that can be drawn
      }

      /// <summary>True if 'sprite' (as a script writes it: a list index, +10000 for a Pokemon icon, +20000 for the expanded sprites) names a sprite this ROM has.</summary>
      private static bool CanRenderSprite(IDataModel model, ModelTable owTable, int sprite) {
         if (sprite < 0 || IsDynamicGraphics(model, sprite)) return false;
         if (sprite.InRange(10000, 20000)) return model.GetTableModel("graphics.overworld.pokemon") is { } mons && sprite - 10000 < mons.Count;
         if (sprite.InRange(20000, 30000)) return model.GetTableModel("graphics.overworld.sprites20k") is { } big && sprite - 20000 < big.Count;
         return owTable != null && sprite < owTable.Count;
      }

      /// <summary>The first frame of the icon of the first Pokemon of the Pokedex (species 1), drawn like the question mark picture, or null if the ROM has no Pokemon icons.</summary>
      public static IPixelViewModel GetFirstPokemonIcon(IDataModel model) {
         return model.CurrentCacheScope.GetOrAdd("first-pokemon-icon", () => BlockMapViewModel.GetPokemonIconAsSprite(model, 1) ?? new ReadonlyPixelViewModel(0, 0));
      }

      private static readonly string[] QuestionMarkGlyph = { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." };

      /// <summary>
      /// What a dynamic-graphics object looks like before the script says which sprite it is: a blacked-out
      /// regular NPC with a small question mark on it.
      /// </summary>
      private static IPixelViewModel GetDynamicPlaceholder(IDataModel model, ModelTable owTable, IPixelViewModel defaultOW) {
         return model.CurrentCacheScope.GetOrAdd("dynamic-graphics-placeholder", () => BuildDynamicPlaceholder(model, owTable, defaultOW));
      }

      private static IPixelViewModel BuildDynamicPlaceholder(IDataModel model, ModelTable owTable, IPixelViewModel defaultOW) {
         IPixelViewModel person = null;
         // use an ordinary NPC for the silhouette (BOY_1, LITTLE_BOY, ...): the first one that renders at 16x32
         if (owTable != null) {
            var preferred = new List<int>();
            if (model.TryGetList("objecteventgfx", out var names)) {
               foreach (var name in new[] { "BOY_1", "LITTLE_BOY", "MAN_1", "WOMAN_1" }) {
                  var i = names.FindIndex(n => n == name);
                  if (i >= 0) preferred.Add(i);
               }
            }
            preferred.AddRange(new[] { 7, 11, 16, 19 });
            foreach (var i in preferred) {
               if (i >= owTable.Count) continue;
               try {
                  var candidate = Render(model, owTable, defaultOW, i, 0, () => -1);
                  if (candidate != null && !ReferenceEquals(candidate, defaultOW) && candidate.PixelWidth == 16 && candidate.PixelHeight >= 24) { person = candidate; break; }
               } catch (Exception) { /* try the next one */ }
            }
         }
         const short black = 0, white = 0x7FFF;
         if (person == null) {
            // no usable NPC sprite in this ROM: draw a plain 16x32 figure
            var generic = new short[16 * 32];
            var transparentColor = UncompressedPaletteColor.Pack(31, 0, 31);
            for (int i = 0; i < generic.Length; i++) generic[i] = transparentColor;
            for (int y = 4; y < 14; y++) for (int x = 4; x < 12; x++) generic[y * 16 + x] = black;
            for (int y = 14; y < 29; y++) for (int x = 3; x < 13; x++) generic[y * 16 + x] = black;
            person = new ReadonlyPixelViewModel(16, 32, generic, transparentColor);
         }
         var width = person.PixelWidth; var height = person.PixelHeight;
         var transparent = person.Transparent;
         var data = new short[width * height];
         for (int i = 0; i < data.Length; i++) {
            var pixel = person.PixelData[i];
            data[i] = (transparent != -1 && pixel == transparent) ? transparent : black;
         }
         // the question mark goes on the chest, about 5x7 pixels
         int left = (width - 5) / 2, top = Math.Max(0, height - 32) + 11;
         for (int y = 0; y < QuestionMarkGlyph.Length; y++) {
            for (int x = 0; x < 5; x++) {
               if (QuestionMarkGlyph[y][x] != '#') continue;
               var (px, py) = (left + x, top + y);
               if (px >= 0 && px < width && py >= 0 && py < height) data[py * width + px] = white;
            }
         }
         return new ReadonlyPixelViewModel(width, height, data, transparent);
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
         if (X < 0 || Y < 0 || X >= layout.Width || Y >= layout.Height) return false; // outside the map there is no block to warp from (and no cell to read)
         var cell = layout.BlockMap[X, Y];
         if (cell == null) return true; // failure to read layout, this warp rendering doesn't matter.
         var tile = cell.Tile;
         var blockset = layout.PrimaryBlockset;
         if (tile >= primaryBlockCount) {
            tile -= primaryBlockCount;
            blockset = layout.SecondaryBlockset;
         }

         var attribute = blockset?.Attribute(tile);
         if (attribute == null) return true; // failure to read the attributes, this warp rendering doesn't matter.
         var behavior = attribute.Behavior;
         if (behavior < 0 || list.Count <= behavior) return false;
         var behaviorName = list[behavior];
         if (behaviorName == null) return false; // a behavior without a name (the list has holes) is not a warp behavior
         return new[] { "Warp", "Door", "Stairs", "Ladder", "Escalator" }.Any(behaviorName.Contains);
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

      public void SetDestinationFormat() => SetDestinationFormat(new ModelDelta()); // don't track this change: showing a signpost is not an edit

      private void SetDestinationFormat(ModelDelta token) {
         if (!ShowPointer) return;
         if (Pointer < 0 || Pointer >= element.Model.Count) return; // a null (or out of file) pointer has nowhere to format
         var destinationRun = new XSERun(Pointer, SortedSpan<int>.None);
         var existingRun = element.Model.GetNextRun(destinationRun.Start);
         if (existingRun.Start < destinationRun.Start) return; // don't erase existing runs for this
         if (existingRun.Start == destinationRun.Start && existingRun is not NoInfoRun) return;
         element.Model.ObserveRunWritten(token, destinationRun);
      }

      public void ClearDestinationFormat() {
         if (!ShowPointer) return;
         if (Pointer < 0 || Pointer >= element.Model.Count) return;
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
            var wasPointer = old < 5;
            var isPointer = value < 5;
            // a signpost that stops showing a script is no longer a pointer to it: the script must not keep it as a source
            // (otherwise moving the script later would write its address over the hidden item)
            if (wasPointer && !isPointer) element.SetAddress("arg", DataFormats.Pointer.NULL, false);
            element.SetValue("kind", value);
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
         // the script is part of this change, so is its format (the script, the pointer in it, the text it points to):
         // going to the script formats it too, but that is not tracked, and undo would leave the format behind over the erased bytes
         element.Model.ObserveRunWritten(Token, new PointerRun(start + 2));
         element.Model.ObserveRunWritten(Token, new PCSRun(element.Model, textStart, 1, SortedSpan.One(start + 2)));
         SetPointer(start, Token);
         gotoAddress(start);
      }

      public bool ShowPointer => Kind < 5;

      public int Pointer {
         get => element.GetAddress("arg");
         set => SetPointer(value, new ModelDelta()); // (the format of a script that is already there is not tracked, like when the signpost is shown)
      }

      private void SetPointer(int value, ModelDelta formatToken) {
         ClearDestinationFormat();
         element.SetAddress("arg", value, false);
         var run = element.Model.GetNextRun(Pointer);
         if (run.Start == Pointer && run is NoInfoRun && !IsValidScriptFreespace(Pointer)) SetDestinationFormat(formatToken);
         UpdateScriptError(element.GetAddress("arg"));
         pointerText = argText = null;
         NotifyPropertiesChanged(nameof(PointerText), nameof(ArgText), nameof(CanGotoScript), nameof(CanCreateScript));
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
         get => HiddenItemEncoding.GetItem(element.Model, element.Start + 8);
         set {
            HiddenItemEncoding.SetItem(element.Model, element.Token, element.Start + 8, value);
            NotifyPropertyChanged(nameof(ItemID));
         }
      }

      /// <summary>The hidden item's flag is FLAG_HIDDEN_ITEMS_START plus this.</summary>
      public int HiddenItemID {
         get => HiddenItemEncoding.GetFlagOffset(element.Model, element.Start + 8);
         set {
            HiddenItemEncoding.SetFlagOffset(element.Model, element.Token, element.Start + 8, value.LimitToRange(0, HiddenItemEncoding.MaxFlagOffset(element.Model)));
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
         var model = element.Model;
         var usedIDs = new HashSet<int>(
            AllMapsModel.Create(model)
            .SelectMany(bank => bank)
            .SelectMany(map => map.Events.Signposts)
            .Where(signpost => signpost.IsHiddenItem)
            .Select(signpost => signpost.HiddenItemFlag));

         var match = 0;
         for (int i = 1; i <= HiddenItemEncoding.MaxFlagOffset(model); i++) {
            if (usedIDs.Contains(i)) continue;
            match = i;
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
         TrainerContent.PreFormatter = ExpansionTrainerTeamRun.TeamFormatterFor(model);
         TrainerContent.Content = TrainerTeam;
         TrainerContent.Bind(nameof(TextEditorViewModel.Content), (sender, e) => TrainerTeam = sender.Content);
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
            if (model.GetNextRun(teamAddress) is not ITrainerTeamRun team) return string.Empty;

            if (TeamVisualizations.Count == 0) UpdateTeamVisualizations(team);
            return teamText = team.SerializeRun();
         }

         set {
            teamText = value;
            var table = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
            if (table == null || !trainerID.InRange(0, table.Count)) return;
            var teamAddress = table[trainerID].GetAddress("pokemon");
            if (model.GetNextRun(teamAddress) is not ITrainerTeamRun team) return;

            var newRun = team.DeserializeRun(value, tokenGenerator(), out _, out _);
            model.ObserveRunWritten(tokenGenerator(), newRun);
            if (newRun.Start != team.Start) DataMoved.Raise(this, new("Trainer Team", newRun.Start));
            UpdateTeamVisualizations(newRun as IStreamRun);
            NotifyPropertyChanged();
         }
      }

      public TextEditorViewModel TrainerContent { get; } = new();

      public ObservableCollection<IPixelViewModel> TeamVisualizations { get; } = new();

      private void UpdateTeamVisualizations(IStreamRun team) {
         TeamVisualizations.Clear();
         if (team == null) return;
         foreach (var vis in team.Visualizations) {
            TeamVisualizations.Add(vis);
         }
      }

      public IReadOnlyList<AutocompleteItem> GetTrainerAutocomplete(string line, int lineIndex, int characterIndex) {
         var table = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         if (table == null || !trainerID.InRange(0, table.Count)) return null;
         var teamAddress = table[trainerID].GetAddress("pokemon");
         if (model.GetNextRun(teamAddress) is not ITrainerTeamRun team) return null;
         return team.GetAutoCompleteOptions(line, lineIndex, characterIndex);
      }

      public void Refresh() {
         teamText = null;
         TeamVisualizations.Clear();
         NotifyPropertyChanged(nameof(TrainerTeam));
      }
   }
}
