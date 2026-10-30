using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
            int offset = code.IsAny(0x45525042, 0x45475042, 0x49525042) ? 88 : 0; // BPRE, BPGE, BPRI
            if (!Element.TryGetValue("regionSectionID", out var value)) return -1;
            return value - offset;
         }
         set {
            if (!Element.HasField("regionSectionID")) return;
            var code = Element.Model.GetShortGameCode();
            int offset = code.IsAny(0x45525042, 0x45475042, 0x49525042) ? 88 : 0; // BPRE, BPGE, BPRI
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
            if (Start < 0) return null;
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
            return new(Element?.Model, start, Width, Height);
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

   public record BaseEventModel(ModelArrayElement Element) : IEventModel {
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
      public int ItemValue => HiddenItemEncoding.GetItem(Element.Model, Element.Start + 8);
      public int HiddenItemFlag => HiddenItemEncoding.GetFlagOffset(Element.Model, Element.Start + 8);
      public int HiddenItemCount => Element.Model[Element.Start + 11];
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

      public string BlockDataFormat { get; private set; }
      public string LayoutFormat { get; private set; }
      public string ObjectsFormat { get; private set; }
      public string WarpsFormat { get; private set; }
      public string ScriptsFormat { get; private set; }
      public string SignpostsFormat { get; private set; }
      public string EventsFormat { get; private set; }
      public string ConnectionsFormat { get; private set; }
      public string HeaderFormat { get; }
      public string MapFormat { get; private set; }

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
         ScriptsFormat = $"[x:|z y:|z elevation:11 trigger:|h index:: script<`xse`>]/{ScriptCount}";
         SignpostsFormat = $"[x:|z y:|z elevation.11 kind. unused:1 arg::|h]/{SignpostCount}";
         EventsFormat = $"[{ObjectCount}. {WarpCount}. {ScriptCount}. {SignpostCount}. {Objects}<{ObjectsFormat}> {Warps}<{WarpsFormat}> {Scripts}<{ScriptsFormat}> {Signposts}<{SignpostsFormat}>]1";
         ConnectionsFormat = "[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]1";
         HeaderFormat = $"music:songnames layoutID:data.maps.layouts+1 regionSectionID.{regionSectionIDFormat} cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.";
         MapFormat = $"[{Layout}<{LayoutFormat}> events<{EventsFormat}> mapscripts<[type. pointer<>]!00> {Connections}<{ConnectionsFormat}> {HeaderFormat}]";

         // If the ROM's map bank table already describes maps with a different layout (for example a pokeemerald-expansion build,
         // where the map header and map layout structs are not the vanilla ones), create new maps/layouts with that format
         // instead of the vanilla one, so that what the map editor writes matches what the game reads.
         var banks = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapFormat = ExtractPointerContent(banks?.FormatString, "map<");
         if (mapFormat != null && mapFormat.EndsWith("]1") && !IsVanillaMapFormat(mapFormat)) {
            MapFormat = mapFormat.Substring(0, mapFormat.Length - 1);
            var layoutFormat = ExtractPointerContent(mapFormat, Layout + "<");
            if (layoutFormat != null) LayoutFormat = layoutFormat;
            var eventsFormat = ExtractPointerContent(mapFormat, Events + "<");
            if (eventsFormat != null) {
               EventsFormat = eventsFormat;
               ObjectsFormat = ExtractPointerContent(eventsFormat, Objects + "<") ?? ObjectsFormat;
               WarpsFormat = ExtractPointerContent(eventsFormat, Warps + "<") ?? WarpsFormat;
               ScriptsFormat = ExtractPointerContent(eventsFormat, Scripts + "<") ?? ScriptsFormat;
               SignpostsFormat = ExtractPointerContent(eventsFormat, Signposts + "<") ?? SignpostsFormat;
            }
            var connectionsFormat = ExtractPointerContent(mapFormat, Connections + "<");
            if (connectionsFormat != null) ConnectionsFormat = connectionsFormat;
            var blockData = ExtractPointerContent(LayoutFormat, PrimaryBlockset + "<");
            if (blockData != null) BlockDataFormat = blockData;
         }
      }

      /// <summary>
      /// True if the map format uses the vanilla header fields (cave/allowBiking/floorNum), in which case the built-in formats are kept.
      /// </summary>
      private static bool IsVanillaMapFormat(string mapFormat) => mapFormat.Contains(" cave. ");

      /// <summary>
      /// Given a format like "[a<[x. y.]1> b<[z.]1>]4" and a field prefix like "a<", returns the bracketed content "[x. y.]1".
      /// Returns null if the field isn't found.
      /// </summary>
      public static string ExtractPointerContent(string format, string fieldPrefix) {
         if (format == null) return null;
         var index = format.IndexOf(fieldPrefix);
         while (index > 0 && !" [<".Contains(format[index - 1])) index = format.IndexOf(fieldPrefix, index + 1);
         if (index < 0 || index + fieldPrefix.Length >= format.Length) return null;
         var start = index + fieldPrefix.Length;
         if (format[start] != '[') return null;
         int depth = 0;
         for (int i = start; i < format.Length; i++) {
            if (format[i] == '[') depth++;
            if (format[i] == ']') depth--;
            if (depth == 0) {
               var end = i + 1;
               while (end < format.Length && format[end] != '>') end++;
               return format.Substring(start, end - start);
            }
         }
         return null;
      }

      public void Refresh() {
         cache = new BlocksetCache(new(), new());
         cache.CalculateBlocksetOptions(model);
      }

      public int RecentBank { get; set; }
   }
}

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// A hidden item is packed into the 4 bytes where other signposts keep a script pointer:
   ///   item (16 bits), flag offset from FLAG_HIDDEN_ITEMS_START (8 bits), quantity (7 bits), underfoot (1 bit).
   /// pokeemerald-expansion has many more items and hidden-item flags, so it packs item into 11 bits and the flag offset into 13.
   /// A ROM's metadata says so with the unmapped constants 'map.hiddenitem.itembits' and 'map.hiddenitem.flagbits'.
   /// </summary>
   public static class HiddenItemEncoding {
      public const string ItemBitsName = "map.hiddenitem.itembits", FlagBitsName = "map.hiddenitem.flagbits";

      public static (int itemBits, int flagBits) GetLayout(IDataModel model) {
         var itemBits = model.TryGetUnmappedConstant(ItemBitsName, out var i) && i.InRange(1, 24) ? i : 16;
         var flagBits = model.TryGetUnmappedConstant(FlagBitsName, out var f) && f.InRange(1, 24) ? f : 8;
         return (itemBits, flagBits);
      }

      /// <param name="address">the start of the 4-byte union (offset 8 of the signpost)</param>
      public static int GetItem(IDataModel model, int address) {
         var (itemBits, _) = GetLayout(model);
         return (int)((uint)model.ReadMultiByteValue(address, 4) & ((1u << itemBits) - 1));
      }

      public static int GetFlagOffset(IDataModel model, int address) {
         var (itemBits, flagBits) = GetLayout(model);
         return (int)(((uint)model.ReadMultiByteValue(address, 4) >> itemBits) & ((1u << flagBits) - 1));
      }

      public static void SetItem(IDataModel model, ModelDelta token, int address, int item) {
         var (itemBits, _) = GetLayout(model);
         var mask = (1u << itemBits) - 1;
         var word = (uint)model.ReadMultiByteValue(address, 4);
         word = (word & ~mask) | ((uint)item & mask);
         model.WriteMultiByteValue(address, 4, token, (int)word);
      }

      public static void SetFlagOffset(IDataModel model, ModelDelta token, int address, int flagOffset) {
         var (itemBits, flagBits) = GetLayout(model);
         var mask = ((1u << flagBits) - 1) << itemBits;
         var word = (uint)model.ReadMultiByteValue(address, 4);
         word = (word & ~mask) | (((uint)flagOffset << itemBits) & mask);
         model.WriteMultiByteValue(address, 4, token, (int)word);
      }

      public static int MaxFlagOffset(IDataModel model) => (1 << GetLayout(model).flagBits) - 1;
   }
}
