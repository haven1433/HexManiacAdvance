using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HavenSoft.HexManiac.Core.Models.Map {

   public record AllMapsModel(ModelTable Table) : IEnumerable<MapBankModel> {
      public static AllMapsModel Create(IDataModel model, Func<ModelDelta> tokenFactory = null) => new(model.GetTableModel("data.maps.banks", tokenFactory));

      public IEnumerator<MapBankModel> GetEnumerator() => Enumerate().GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public MapBankModel? this[int index] {
         get {
            var bank = Table[index].GetSubTable("maps");
            if (bank == null) return null;
            return new MapBankModel(bank, index);
         }
      }
      public int Count => Table?.Count ?? 0;

      private IEnumerable<MapBankModel> Enumerate() {
         for (int i = 0; i < Count; i++) yield return this[i];
      }
   }

   public record MapBankModel(ModelTable Table, int Group) : IEnumerable<MapModel> {
      public IEnumerator<MapModel> GetEnumerator() => Enumerate().GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public MapModel? this[int index] {
         get {
            var table = Table[index].GetSubTable("map");
            if (table == null) return null;
            return new MapModel(table[0], Group, index);
         }
      }
      public int Count => Table.Count;

      private IEnumerable<MapModel> Enumerate() {
         for (int i = 0; i < Count; i++) yield return this[i];
      }
   }

   public record MapModel(ModelArrayElement Element, int Group = -1, int Map = -1) {
      public LayoutModel Layout => Element.TryGetSubTable(Format.Layout, out var table) ? new(table[0]) : new(null);

      public EventGroupModel Events => Element.TryGetSubTable(Format.Events, out var table) ? new(table[0]) : new(null);

      public int NameIndex {
         get {
            var code = Element.Model.GetShortGameCode();
            int offset = code.IsAny(0x45525042, 0x45475042) ? 88 : 0; // BPRE, BPGE
            if (!Element.TryGetValue("regionSectionID", out var value)) return -1;
            return value - offset;
         }
         set {
            if (!Element.HasField("regionSectionID")) return;
            var code = Element.Model.GetShortGameCode();
            int offset = code.IsAny(0x45525042, 0x45475042) ? 88 : 0; // BPRE, BPGE
            Element.SetValue("regionSectionID", value + offset);
         }
      }

      public IList<ConnectionModel> Connections {
         get {
            if (Group < 0 || Map < 0) throw new InvalidOperationException("bank/map location unknown.");
            if (!Element.TryGetSubTable(Format.Connections, out var outerTable)) return null;
            if (!outerTable[0].TryGetSubTable(Format.Connections, out var innerTable)) return null;
            return innerTable.Select(e => new ConnectionModel(e, Group, Map)).ToList();
         }
      }

      public BlockCells Blocks {
         get {
            var layout = Layout;
            if (layout == null) return null;
            return layout.BlockMap;
         }
      }

      public ModelTable MapScripts {
         get => Element.GetSubTable("mapscripts");
      }

      public override string ToString() => $"({Group}, {Map})";
   }

   public record LayoutPrototype(int PrimaryBlockset, int SecondaryBlockset, int BorderBlock);

   public record BlockCells(IDataModel Model, int Start, int Width, int Height) {
      public BlockmapRun Run => Model.GetNextRun(Start) as BlockmapRun;
      public BlockCell this[int x, int y] {
         get {
            if (Start == Pointer.NULL) return null;
            var data = Model.ReadMultiByteValue(Start + (y * Width + x) * 2, 2);
            return new(data & 0x3FF, data >> 10);
         }
      }
   }

   public record BlockCell(int Tile, int Collision) {
      public int Block => (Collision << 10) | Tile;
   }

   public record LayoutModel(ModelArrayElement? Element) {
      public int Width => Element?.GetValue("width") ?? -1;
      public int Height => Element?.GetValue("height") ?? -1;
      public MiniBlocksetModel PrimaryBlockset => Element?.TryGetSubTable(Format.PrimaryBlockset, out var table) ?? false ? new(table[0]) : null;
      public MiniBlocksetModel SecondaryBlockset => Element?.TryGetSubTable(Format.SecondaryBlockset, out var table) ?? false ? new(table[0]) : null;
      public int BorderBlockAddress => Element?.GetAddress(Format.BorderBlock) ?? Pointer.NULL;
      public BlockCells BlockMap {
         get {
            var start = Element?.GetAddress(Format.BlockMap) ?? Pointer.NULL;
            return new(Element.Model, start, Width, Height);
         }
      }
   }

   public record MiniBlocksetModel(ModelArrayElement? Element) {
      public int Start => Element?.Start ?? Pointer.NULL;
      public int BlocksAddress => Element?.GetAddress(Format.Blocks) ?? Pointer.NULL;
      public int TilesetAddress => Element?.GetAddress(Format.Tileset) ?? Pointer.NULL;
      public int PaletteAddress => Element?.GetAddress(Format.Palette) ?? Pointer.NULL;
      public int AttributeAddress => Element?.GetAddress(Format.BlockAttributes) ?? Pointer.NULL;
      public BlocksetModel FullBlocksetModel => new BlocksetModel(Element.Model, Element.Start);
      public TileAttribute Attribute(int index) {
         var start = AttributeAddress;
         if (start == Pointer.NULL) return null;
         var length = Element.Model.IsFRLG() ? 4 : 2;
         return TileAttribute.Create(Element.Model.RawData, start + length * index, length);
      }
   }

   public record EventGroupModel(ModelArrayElement? Element) {
      public List<ObjectEventModel> Objects {
         get {
            if (Element == null) return new List<ObjectEventModel>();
            if (!Element.TryGetSubTable(Format.Objects, out var objects)) return new List<ObjectEventModel>();
            return objects.Select(obj => new ObjectEventModel(obj)).ToList();
         }
      }
      public List<ScriptEventModel> Scripts {
         get {
            if (Element == null) return new List<ScriptEventModel>();
            if (!Element.TryGetSubTable(Format.Scripts, out var scripts)) return new List<ScriptEventModel>();
            return scripts.Select(obj => new ScriptEventModel(obj)).ToList();
         }
      }
      public List<WarpEventModel> Warps {
         get {
            if (Element == null) return new List<WarpEventModel>();
            if (!Element.TryGetSubTable(Format.Warps, out var warps)) return new List<WarpEventModel>();
            return warps.Select(obj => new WarpEventModel(obj)).ToList();
         }
      }
      public List<SignpostEventModel> Signposts {
         get {
            if (Element == null) return new();
            if (!Element.TryGetSubTable(Format.Signposts, out var signposts)) return new();
            return signposts.Select(sp => new SignpostEventModel(sp)).ToList();
         }
      }
   }

   public interface IEventModel {
      public ModelArrayElement Element { get; }
      int X { get; }
      int Y { get; }
      int Elevation { get; }
   }

   public record BaseEventModel(ModelArrayElement Element): IEventModel {
      public int X => Element.TryGetValue("x", out int x) ? x : 0;
      public int Y => Element.TryGetValue("y", out int y) ? y : 0;
      public int Elevation => Element.TryGetValue("elevation", out int elevation) ? elevation : 0;
   }

   public interface IScriptEventModel : IEventModel {
      int ScriptAddress { get; }
   }

   public record ObjectEventModel(ModelArrayElement Element) : BaseEventModel(Element), IScriptEventModel {
      public int Graphics => Element.TryGetValue("graphics", out var result) ? result : -1;
      public int ScriptAddress => Element.GetAddress("script");
      public int Flag => Element.GetAddress("flag");
   }

   public record ScriptEventModel(ModelArrayElement Element) : BaseEventModel(Element), IScriptEventModel {
      public int ScriptAddress => Element.GetAddress("script");
   }

   public record WarpEventModel(ModelArrayElement Element) : BaseEventModel(Element) {
      public MapModel? TargetMap {
         get {
            var banks = AllMapsModel.Create(Element.Model, () => Element.Token);
            var bank = banks[Bank];
            if (bank == null) return null;
            return bank[Map];
         }
      }
      public int WarpID => Element.GetValue("warpID");
      public int Bank => Element.GetValue("bank");
      public int Map => Element.GetValue("map");

      public WarpEventModel TargetWarp {
         get {
            var allmaps = AllMapsModel.Create(Element.Model);
            var bank = allmaps[Bank];
            if (bank == null) return null;
            var map = bank[Map];
            if (map == null) return null;
            if (map.Events.Warps.Count <= WarpID) return null;
            return map.Events.Warps[WarpID];
         }
      }
   }

   public record SignpostEventModel(ModelArrayElement Element) : BaseEventModel(Element), IScriptEventModel {
      public int Kind => Element.GetValue("kind");
      public int Arg => Element.GetValue("arg");
      public bool HasScript => Kind < 5;
      public bool IsHiddenItem => Kind.IsAny(5, 6, 7);
      public int ItemValue => Element.Model.ReadMultiByteValue(Element.Start + 8, 2);
      public int ScriptAddress => Element.Model.ReadPointer(Element.Start + 8);
   }

   public class Format {
      public static string RegionSection => "regionSectionID";
      public static string Events => "events";
      public static string Layout => "layout";
      public static string Warps => "warps";
      public static string Objects => "objects";
      public static string Connections => "connections";
      public static string Scripts => "scripts";
      public static string Signposts => "signposts";
      public static string ObjectCount => "objectCount";
      public static string WarpCount => "warpCount";
      public static string ScriptCount => "scriptCount";
      public static string SignpostCount => "signpostCount";
      public static string BorderBlock => "borderblock";
      public static string BlockMap => "blockmap";
      public static string PrimaryBlockset => "blockdata1";
      public static string SecondaryBlockset => "blockdata2";
      public static string Tileset => "tileset";
      public static string BlockAttributes => "attributes";
      public static string Blocks => "blockset";
      public static string TileAnimationRoutine => "animation";
      public static string Palette => "pal";
      public static string BorderWidth => "borderwidth";
      public static string BorderHeight => "borderheight";
      public static string IsSecondary => "isSecondary";

      private readonly IDataModel model;
      private BlocksetCache cache;

      public BlocksetCache BlocksetCache => cache;

      public string BlockDataFormat { get; }
      public string LayoutFormat { get; }
      public string ObjectsFormat { get; }
      public string WarpsFormat { get; }
      public string ScriptsFormat { get; }
      public string SignpostsFormat { get; }
      public string EventsFormat { get; }
      public string ConnectionsFormat { get; }
      public string HeaderFormat { get; }
      public string MapFormat { get; }

      public Format(IDataModel model) {
         this.model = model;
         cache = new BlocksetCache(new(), new());
         cache.CalculateBlocksetOptions(model);
         bool isRSE = !model.IsFRLG();
         BlockDataFormat = $"[isCompressed. isSecondary. padding: {Tileset}<> {Palette}<`ucp4:0123456789ABCDEF`> {Blocks}<> {TileAnimationRoutine}<> {BlockAttributes}<>]1";
         if (isRSE) BlockDataFormat = $"[isCompressed. isSecondary. padding: {Tileset}<> {Palette}<`ucp4:0123456789ABCDEF`> {Blocks}<> {BlockAttributes}<> {TileAnimationRoutine}<>]1";
         LayoutFormat = $"[width:: height:: {BorderBlock}<> {BlockMap}<`blm`> {PrimaryBlockset}<{BlockDataFormat}> {SecondaryBlockset}<{BlockDataFormat}> {BorderWidth}. {BorderHeight}. unused:]1";
         if (isRSE) LayoutFormat = $"[width:: height:: {BorderBlock}<> {BlockMap}<`blm`> {PrimaryBlockset}<{BlockDataFormat}> {SecondaryBlockset}<{BlockDataFormat}>]1";
         var regionSectionIDFormat = "data.maps.names+88";
         if (isRSE) regionSectionIDFormat = "data.maps.names";
         var field3 = !isRSE ? "kind:" : "unused:1";
         ObjectsFormat = $"[id. graphics.{HardcodeTablesModel.OverworldSprites} {field3} x:|z y:|z elevation.11 moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag:|h padding:]/{ObjectCount}";
         WarpsFormat = $"[x:|z y:|z elevation.11 warpID. map. bank.]/{WarpCount}";
         ScriptsFormat = $"[x:|z y:|z elevation:11 trigger: index:: script<`xse`>]/{ScriptCount}";
         SignpostsFormat = $"[x:|z y:|z elevation.11 kind. unused:1 arg::|h]/{SignpostCount}";
         EventsFormat = $"[{ObjectCount}. {WarpCount}. {ScriptCount}. {SignpostCount}. {Objects}<{ObjectsFormat}> {Warps}<{WarpsFormat}> {Scripts}<{ScriptsFormat}> {Signposts}<{SignpostsFormat}>]1";
         ConnectionsFormat = "[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]1";
         HeaderFormat = $"music:songnames layoutID:data.maps.layouts+1 regionSectionID.{regionSectionIDFormat} cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.";
         MapFormat = $"[{Layout}<{LayoutFormat}> events<{EventsFormat}> mapscripts<[type. pointer<>]!00> {Connections}<{ConnectionsFormat}> {HeaderFormat}]";
      }

      public void Refresh() {
         cache = new BlocksetCache(new(), new());
         cache.CalculateBlocksetOptions(model);
      }

      public int RecentBank { get; set; }
   }
}
