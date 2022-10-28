using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Map {
   public record MapModel(ModelArrayElement Element) {
      public LayoutModel Layout => Element.TryGetSubTable(Format.Layout, out var table) ? new(table[0]) : null;
      public EventGroupModel Events => Element.TryGetSubTable(Format.Events, out var table) ? new(table[0]) : null;
   }

   public record LayoutModel(ModelArrayElement Element) {
      public int Width => Element.GetValue("width");
      public int Height => Element.GetValue("height");
      public ModelArrayElement PrimaryBlockset => Element.TryGetSubTable(Format.PrimaryBlockset, out var table) ? table[0] : null;
      public ModelArrayElement SecondaryBlockset => Element.TryGetSubTable(Format.SecondaryBlockset, out var table) ? table[0] : null;
   }

   public record EventGroupModel(ModelArrayElement Element) {
      public List<ObjectEventModel> Objects {
         get {
            if (!Element.TryGetSubTable(Format.Objects, out var objects)) return null;
            return objects.Select(obj => new ObjectEventModel(obj)).ToList();
         }
      }
      public List<WarpEventModel> Warps {
         get {
            if (!Element.TryGetSubTable(Format.Warps, out var warps)) return null;
            return warps.Select(obj => new WarpEventModel(obj)).ToList();
         }
      }
   }

   public record BaseEventModel(ModelArrayElement Element) {
      public int X => Element.GetValue("x");
      public int Y => Element.GetValue("y");
      public int Elevation => Element.GetValue("elevation");
   }

   public record ObjectEventModel(ModelArrayElement Element) : BaseEventModel(Element) {

   }

   public record WarpEventModel(ModelArrayElement Element) : BaseEventModel(Element) {

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
      public static string Blocks => "block";
      public static string TileAnimationRoutine => "animation";
      public static string Palette => "pal";
      public static string BorderWidth => "borderwidth";
      public static string BorderHeight => "borderheight";

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

      public Format(bool isRSE) {
         BlockDataFormat = $"[isCompressed. isSecondary. padding: {Tileset}<> {Palette}<`ucp4:0123456789ABCDEF`> {Blocks}<> {TileAnimationRoutine}<> {BlockAttributes}<>]1";
         if (isRSE) BlockDataFormat = $"[isCompressed. isSecondary. padding: {Tileset}<> {Palette}<`ucp4:0123456789ABCDEF`> {Blocks}<> {BlockAttributes}<> {TileAnimationRoutine}<>]1";
         LayoutFormat = $"[width:: height:: {BorderBlock}<> {BlockMap}<`blm`> {PrimaryBlockset}<{BlockDataFormat}> {SecondaryBlockset}<{BlockDataFormat}> {BorderWidth}. {BorderHeight}. unused:]1";
         if (isRSE) LayoutFormat = $"[width:: height:: {BorderBlock}<> {BlockMap}<`blm`> {PrimaryBlockset}<{BlockDataFormat}> {PrimaryBlockset}<{BlockDataFormat}>]1";
         ObjectsFormat = $"[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/{ObjectCount}";
         WarpsFormat = $"[x:500 y:500 elevation. warpID. map. bank.]/{WarpCount}";
         ScriptsFormat = $"[x:500 y:500 elevation: trigger: index:: script<`xse`>]/{ScriptCount}";
         SignpostsFormat = $"[x:500 y:500 elevation. kind. unused: arg::|h]/{SignpostCount}";
         EventsFormat = $"[{ObjectCount}. {WarpCount}. {ScriptCount}. {SignpostCount}. {Objects}<{ObjectsFormat}> {Warps}<{WarpsFormat}> {Scripts}<{ScriptsFormat}> {Signposts}<{SignpostsFormat}>]1";
         ConnectionsFormat = "[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]1";
         HeaderFormat = "music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.";
         MapFormat = $"[{Layout}<{LayoutFormat}> events<{EventsFormat}> mapscripts<[type. pointer<>]!00> {Connections}<{ConnectionsFormat}> {HeaderFormat}]";
      }
   }
}
