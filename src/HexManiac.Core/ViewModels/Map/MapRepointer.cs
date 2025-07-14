using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Linq;
using System.Text;


namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public record ChangeMapEventArgs(int Bank, int Map);
   public record DataMovedEventArgs(string Type, int Address);

   public class MapRepointer : ViewModelCore {

      public const int MaxMapsPerBank = 127;
      public const string MapBankFullError = "Banks cannot have more than 127 maps.";
      public const string MapLayoutMissing = "Cannot create a new map when data.maps.layouts is missing.";

      private readonly Format format;
      private readonly IFileSystem fileSystem;
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly int mapID; // bank * 1000 + map

      private readonly Action refreshHeader;

      public event EventHandler<DataMovedEventArgs> DataMoved; // also works as a "data changed" (request refresh) if the arg is null
      public event EventHandler<ChangeMapEventArgs> ChangeMap;

      public MapRepointer(Format format, IFileSystem fileSystem, IEditableViewPort viewPort, ChangeHistory<ModelDelta> history, int mapID, Action refreshHeader) {
         this.format = format;
         this.fileSystem = fileSystem;
         this.viewPort = viewPort;
         this.model = viewPort.Model;
         this.history = history;
         this.mapID = mapID;
         this.refreshHeader = refreshHeader;
      }

      #region General

      public string DuplicateMapText => "Create a new map with no connections or events, but the same layout.";

      #endregion

      #region Blocks / Tilesets / Palettes

      #region Blocks

      public (int currentCount, int maxCount) EstimateBlockCount(ModelArrayElement layout, bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         if (layout == null) return (0, 0);
         var blocksetTable = layout.GetSubTable(blocksetName);
         if (blocksetTable == null) return (0, 0);
         var blockset = blocksetTable[0];
         var blockCount = model.IsFRLG() ? 640 : 512;
         if (!primary) blockCount = 1024 - blockCount;
         var maxBlockCount = blockCount;
         var blockStart = blockset.GetAddress(Format.Blocks);
         var attributeStart = blockset.GetAddress(Format.BlockAttributes);
         BlocksetModel.EstimateBlockCount(model, ref blockCount, blockStart, attributeStart);
         return (blockCount, maxBlockCount);
      }

      #endregion

      #region Tilesets

      private (int, int) EstimateTileCount(ModelArrayElement blockset) {
         if (blockset == null) return (0, 0);
         int maxTiles = model.IsFRLG() ? 640 : 512;
         if (blockset.GetValue("isSecondary") != 0) maxTiles = 1024 - maxTiles;
         int currentTiles = maxTiles;
         var tilesetAddress = blockset.GetAddress(Format.Tileset);
         if (blockset.GetValue("isCompressed") == 0) {
            BlocksetModel.EstimateTileCount(model, ref currentTiles, tilesetAddress);
         } else {
            var run = new LZRun(model, tilesetAddress);
            currentTiles = run.DecompressedLength / 0x20;
         }
         return (currentTiles, maxTiles);
      }

      #endregion

      #region Palettes

      public string ExpandPrimaryPaletteText => string.Empty;

      private bool CanExpandPrimaryPalette() => false;

      private void ExecuteExpandPrimaryPalette() => throw new NotImplementedException();

      public string ExpandSecondaryPaletteText => string.Empty;

      private bool CanExpandSecondaryPalette() => false;

      private void ExecuteExpandSecondaryPalette() => throw new NotImplementedException();

      private void ExportPal(string folder, int nameIndex, int start) {
         var content = new StringBuilder();
         content.AppendLine("JASC-PAL");
         content.AppendLine("0100");
         content.AppendLine("16");
         for (int i = 0; i < 16; i++) {
            var color = model.ReadMultiByteValue(start + i * 2, 2);
            var (r, g, b) = UncompressedPaletteColor.ToRGB((short)color);
            r = r * 255 / 31;
            g = g * 255 / 31;
            b = b * 255 / 31;
            content.AppendLine($"{r} {g} {b}");
         }
         fileSystem.Save(new(folder + "/" + nameIndex.ToString("X2") + ".pal", Encoding.ASCII.GetBytes(content.ToString())));
         viewPort.RaiseMessage($"Exported palettes to {folder}");
      }

      #endregion

      #endregion

      #region Animations

      public string CreatePrimaryTilesetAnimationsText => string.Empty;

      private bool CanCreatePrimaryTilesetAnimations() {
         // we should be able to run the utility to create a tileset animation unless we already have
         return false;
      }

      private void ExecuteCreatePrimaryTilesetAnimations() {
         // TODO grab the code from the utility and use that here
         throw new NotImplementedException();
      }

      public string CreateSecondaryTilesetAnimationsText => string.Empty;

      private bool CanCreateSecondaryTilesetAnimations() {
         // we should be able to run the utility to create a tileset animation unless we already have
         return false;
      }

      private void ExecuteCreateSecondaryTilesetAnimations() {
         // grab the code from the utility and use that here
      }

      #endregion

      #region Helpers

      public int CreateNewBlockMap(ModelDelta token, int width, int height) {
         var blockmapLength = width * height * 2;
         var blockmapStart = model.FindFreeSpace(model.FreeSpaceStart, blockmapLength);
         token.ChangeData(model, blockmapStart, new byte[blockmapLength]);
         var run = new BlockmapRun(model, blockmapStart, SortedSpan<int>.None, width, height);
         model.ObserveRunWritten(token, run);
         return blockmapStart;
      }

      public int CreateNewEvents(ModelDelta token) {
         // objectCount. warpCount. scriptCount. signpostCount. objects<> warps<> scripts<> signposts<>
         var eventStart = model.FindFreeSpace(model.FreeSpaceStart, 20);
         token.ChangeData(model, eventStart, new byte[20]);
         return eventStart;
      }

      public int CreateNewMapScripts(ModelDelta token) {
         // mapscripts<[type. pointer<>]!00>
         var eventStart = model.FindFreeSpace(model.FreeSpaceStart, 4);
         token.ChangeData(model, eventStart, new byte[] { 0, 0xFF, 0xFF, 0xFF });
         return eventStart;
      }

      public int CreateNewConnections(ModelDelta token) {
         // count:: connections<>
         var connectionStart = model.FindFreeSpace(model.FreeSpaceStart, 8);
         token.ChangeData(model, connectionStart, new byte[8]);
         var run = new TableStreamRun(model, connectionStart, SortedSpan<int>.None, $"[{ConnectionInfo.ConnectionTableContent}]", null, new FixedLengthStreamStrategy(1));
         model.ObserveRunWritten(token, run);
         return connectionStart;
      }

      private void WritePointerAndSource(ModelDelta token, int source, int destination) {
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, NoInfoRun.FromPointer(model, source));
      }

      private bool ExportBytes(int address, int size) {
         var content = new byte[size];
         Array.Copy(model.RawData, address, content, 0, size);
         return ExportBytes(content);
      }

      private bool ExportBytes(byte[] content) {
         var newName = fileSystem.RequestNewName(string.Empty, "Byte File", "bin");
         if (newName == null) return false;
         return fileSystem.Save(new(newName, content));
      }

      #endregion
   }
}
