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
using System.Windows.Input;

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

      #region Commands

      private StubCommand
         repointLayout,
         repointBorderBlock,
         repointBlockMap,
         repointPrimaryBlockset,
         repointSecondaryBlockset,
         repointPrimaryTileset,
         repointSecondaryTileset,
         expandPrimaryTileset,
         expandSecondaryTileset,
         repointPrimaryBlocks,
         repointSecondaryBlocks,
         expandPrimaryBlocks,
         expandSecondaryBlocks,
         repointPrimaryPalette,
         repointSecondaryPalette,
         expandPrimaryPalette,
         expandSecondaryPalette,
         createPrimaryTilesetAnimations,
         createSecondaryTilesetAnimations,
         duplicateMap;

      public ICommand RepointLayout => StubCommand(ref repointLayout, ExecuteRepointLayout, CanRepointLayout);
      public ICommand RepointBorderBlock => StubCommand(ref repointBorderBlock, ExecuteRepointBorderBlock, CanRepointBorderBlock);
      public ICommand RepointBlockMap => StubCommand(ref repointBlockMap, ExecuteRepointBlockMap, CanRepointBlockMap);
      public ICommand RepointPrimaryBlockset => StubCommand(ref repointPrimaryBlockset, ExecuteRepointPrimaryBlockset, CanRepointPrimaryBlockset);
      public ICommand RepointSecondaryBlockset => StubCommand(ref repointSecondaryBlockset, ExecuteRepointSecondaryBlockset, CanRepointSecondaryBlockset);
      public ICommand RepointPrimaryTileset => StubCommand(ref repointPrimaryTileset, ExecuteRepointPrimaryTileset, CanRepointPrimaryTileset);
      public ICommand RepointSecondaryTileset => StubCommand(ref repointSecondaryTileset, ExecuteRepointSecondaryTileset, CanRepointSecondaryTileset);
      public ICommand ExpandPrimaryTileset => StubCommand(ref expandPrimaryTileset, ExecuteExpandPrimaryTileset, CanExpandPrimaryTileset);
      public ICommand ExpandSecondaryTileset => StubCommand(ref expandSecondaryTileset, ExecuteExpandSecondaryTileset, CanExpandSecondaryTileset);
      public ICommand RepointPrimaryBlocks => StubCommand(ref repointPrimaryBlocks, ExecuteRepointPrimaryBlocks, CanRepointPrimaryBlocks);
      public ICommand RepointSecondaryBlocks => StubCommand(ref repointSecondaryBlocks, ExecuteRepointSecondaryBlocks, CanRepointSecondaryBlocks);
      public ICommand ExpandPrimaryBlocks => StubCommand(ref expandPrimaryBlocks, ExecuteExpandPrimaryBlocks, CanExpandPrimaryBlocks);
      public ICommand ExpandSecondaryBlocks => StubCommand(ref expandSecondaryBlocks, ExecuteExpandSecondaryBlocks, CanExpandSecondaryBlocks);
      public ICommand RepointPrimaryPalette => StubCommand(ref repointPrimaryPalette, ExecuteRepointPrimaryPalette, CanRepointPrimaryPalette);
      public ICommand RepointSecondaryPalette => StubCommand(ref repointSecondaryPalette, ExecuteRepointSecondaryPalette, CanRepointSecondaryPalette);
      public ICommand ExpandPrimaryPalette => StubCommand(ref expandPrimaryPalette, ExecuteExpandPrimaryPalette, CanExpandPrimaryPalette);
      public ICommand ExpandSecondaryPalette => StubCommand(ref expandSecondaryPalette, ExecuteExpandSecondaryPalette, CanExpandSecondaryPalette);
      public ICommand CreatePrimaryTilesetAnimations => StubCommand(ref createPrimaryTilesetAnimations, ExecuteCreatePrimaryTilesetAnimations, CanCreatePrimaryTilesetAnimations);
      public ICommand CreateSecondaryTilesetAnimations => StubCommand(ref createSecondaryTilesetAnimations, ExecuteCreateSecondaryTilesetAnimations, CanCreateSecondaryTilesetAnimations);
      public ICommand DuplicateMap => StubCommand(ref duplicateMap, ExecuteDuplicateMap, CanDuplicateMap);

      #endregion

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

      public void Refresh() {
         foreach (var command in new[] {
            repointLayout,
            repointBorderBlock,
            repointBlockMap,
            repointPrimaryBlockset,
            repointSecondaryBlockset,
            repointPrimaryTileset,
            repointSecondaryTileset,
            expandPrimaryTileset,
            expandSecondaryTileset,
            repointPrimaryBlocks,
            repointSecondaryBlocks,
            expandPrimaryBlocks,
            expandSecondaryBlocks,
            repointPrimaryPalette,
            repointSecondaryPalette,
            expandPrimaryPalette,
            expandSecondaryPalette,
            createPrimaryTilesetAnimations,
            createSecondaryTilesetAnimations,
            duplicateMap,
         }) {
            command.RaiseCanExecuteChanged();
         }
      }

      #region General

      public string RepointLayoutText {
         get {
            var map = GetMapModel();
            if (map == null) return string.Empty;
            var layoutStart = map.GetAddress(Format.Layout);
            if (layoutStart < 0 || layoutStart >= model.Count) return string.Empty;
            var layoutRun = model.GetNextRun(layoutStart);
            if (layoutRun.PointerSources == null) return string.Empty;
            return $"This layout is used by {layoutRun.PointerSources.Count - 1} maps.";
         }
      }

      // expect that a layout is used once in the layout table, and then by some number of maps
      private bool CanRepointLayout() {
         var map = GetMapModel();
         if (map == null) return false;
         var layoutStart = map.GetAddress(Format.Layout);
         if (layoutStart < 0 || layoutStart >= model.Count) return false;
         var layoutRun = model.GetNextRun(layoutStart);
         return layoutRun.PointerSources != null && layoutRun.PointerSources.Count > 2;
      }

      private void ExecuteRepointLayout() {
         var map = GetMapModel();
         var layoutStart = map.GetAddress(Format.Layout);
         var layoutRun = model.GetNextRun(layoutStart);
         var newDataStart = DuplicateData(layoutRun.Start, layoutRun.Length);
         map.SetAddress(Format.Layout, newDataStart);
         DataMoved.Raise(this, new("Layout", newDataStart));
         repointLayout.RaiseCanExecuteChanged();
         repointBorderBlock.RaiseCanExecuteChanged();
         repointBlockMap.RaiseCanExecuteChanged();
         repointPrimaryBlockset.RaiseCanExecuteChanged();
         repointSecondaryBlockset.RaiseCanExecuteChanged();
         refreshHeader();
         // TODO we just made a new layout, we need to add it to the layout table
      }

      public string RepointBorderText {
         get {
            var count = CountLayoutMemberSources(Format.BorderBlock);
            return $"This border is used by {count} layout{(count != 1 ? "s" : string.Empty)}.";
         }
      }

      private bool CanRepointBorderBlock() => CountLayoutMemberSources(Format.BorderBlock) > 1;

      private void ExecuteRepointBorderBlock() {
         var layout = GetLayout();
         var count = 4;
         if (layout.HasField(Format.BorderWidth) && layout.HasField(Format.BorderHeight)) {
            count = Math.Max(4, layout.GetValue(Format.BorderWidth) * layout.GetValue(Format.BorderHeight));
         }
         ExecuteRepointLayoutMember(Format.BorderBlock, count * 2, repointBorderBlock);
      }

      public void ImportBorderBlock() {
         var layout = GetLayout();
         var count = 4;
         if (layout.HasField(Format.BorderWidth) && layout.HasField(Format.BorderHeight)) {
            count = Math.Max(4, layout.GetValue(Format.BorderWidth) * layout.GetValue(Format.BorderHeight));
         }

         if (ImportBytes(layout.GetAddress(Format.BorderBlock), count * 2, -1)) DataMoved.Raise(this, null);
      }

      public void ExportBorderBlock() {
         var layout = GetLayout();
         var count = 4;
         if (layout.HasField(Format.BorderWidth) && layout.HasField(Format.BorderHeight)) {
            count = Math.Max(4, layout.GetValue(Format.BorderWidth) * layout.GetValue(Format.BorderHeight));
         }

         ExportBytes(layout.GetAddress(Format.BorderBlock), count * 2);
      }

      public string RepointBlockMapText => $"This Blockmap is used by {CountLayoutMemberSources(Format.BlockMap)} layouts.";

      private bool CanRepointBlockMap() => CountLayoutMemberSources(Format.BlockMap) > 1;

      private void ExecuteRepointBlockMap() {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         ExecuteRepointLayoutMember(Format.BlockMap, width * height * 2, repointBlockMap);
      }

      public void ImportBlockMap() {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var size = width * height * 2;
         var address = layout.GetAddress(Format.BlockMap);

         if (ImportBytes(address, size, -1)) DataMoved.Raise(this, null);
      }

      public void ExportBlockMap() {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var size = width * height * 2;
         var address = layout.GetAddress(Format.BlockMap);

         ExportBytes(address, size);
      }

      public string RepointPrimaryBlocksetText => $"This primary blockset is used by {CountLayoutMemberSources(Format.PrimaryBlockset)} layouts.";

      private bool CanRepointPrimaryBlockset() => CountLayoutMemberSources(Format.PrimaryBlockset) > 1;

      private void ExecuteRepointPrimaryBlockset() {
         ExecuteRepointLayoutMember(Format.PrimaryBlockset, 6 * 4,
            repointPrimaryBlockset, repointPrimaryBlocks,
            repointPrimaryTileset, repointPrimaryPalette,
            expandPrimaryBlocks, expandPrimaryTileset,
            expandPrimaryPalette, createPrimaryTilesetAnimations);
      }

      public string RepointSecondaryBlocksetText => $"This secondary blockset is used by {CountLayoutMemberSources(Format.SecondaryBlockset)} layouts.";

      private bool CanRepointSecondaryBlockset() => CountLayoutMemberSources(Format.SecondaryBlockset) > 1;

      private void ExecuteRepointSecondaryBlockset() {
         ExecuteRepointLayoutMember(Format.SecondaryBlockset, 6 * 4,
            repointSecondaryBlockset, repointSecondaryBlocks,
            repointSecondaryTileset, repointSecondaryPalette,
            expandSecondaryBlocks, expandSecondaryTileset,
            expandSecondaryPalette, createSecondaryTilesetAnimations);
      }

      public string DuplicateMapText => "Create a new map with no connections or events, but the same layout.";

      private bool CanDuplicateMap() {
         return true;
      }

      private void ExecuteDuplicateMap() {
         var option = GetMapBankForNewMap(
            "Maps are organized into banks. The game doesn't care, so you can use the banks however you like."
            + Environment.NewLine +
            "Duplicate map into which bank?"
            );
         if (option == -1) return;
         var table = AddNewMapToBank(option);
         if (table == null) {
            viewPort.RaiseError(MapBankFullError);
            return;
         }
         var newMap = CreateNewMap(history.CurrentChange);
         model.UpdateArrayPointer(history.CurrentChange, null, null, -1, table.Start + table.Length - 4, newMap.Element.Start);
         ChangeMap.Raise(this, new(option, table.ElementCount - 1));
         repointLayout.RaiseCanExecuteChanged();
         refreshHeader();
      }

      private int CountLayoutMemberSources(string member) {
         var layout = GetLayout();
         if (layout == null) return 0;
         var start = layout.GetAddress(member);
         var run = model.GetNextRun(start);
         return run.PointerSources == null ? 0 : run.PointerSources.Count;
      }

      private void ExecuteRepointLayoutMember(string member, int length, params StubCommand[] commands) {
         var layout = GetLayout();
         var start = DuplicateData(layout.GetAddress(member), length);
         layout.SetAddress(member, start);
         refreshHeader?.Invoke();
         DataMoved.Raise(this, new(char.ToUpper(member[0]) + member.Substring(1), start));
         foreach (var command in commands) command.RaiseCanExecuteChanged();
      }

      #endregion

      #region Blocks / Tilesets / Palettes

      #region Blocks

      public string RepointPrimaryBlocksText => $"These primary blocks are used by {CountBlocksetMemberSources(Format.PrimaryBlockset, Format.Blocks)} blocksets.";

      private bool CanRepointPrimaryBlocks() => CountBlocksetMemberSources(Format.PrimaryBlockset, Format.Blocks) > 1;

      private void ExecuteRepointPrimaryBlocks() {
         var layout = GetLayout();
         var attributeSize = model.IsFRLG() ? 4 : 2;
         var (blockCount, _) = EstimateBlockCount(layout, true);
         RepointBlocksetMember(Format.PrimaryBlockset, Format.Blocks, blockCount * 16);
         RepointBlocksetMember(Format.PrimaryBlockset, Format.BlockAttributes, blockCount * attributeSize, repointPrimaryBlocks);
      }

      public string RepointSecondaryBlocksText => $"These secondary blocks are used by {CountBlocksetMemberSources(Format.SecondaryBlockset, Format.Blocks)} blocksets.";

      private bool CanRepointSecondaryBlocks() => CountBlocksetMemberSources(Format.SecondaryBlockset, Format.Blocks) > 1;

      private void ExecuteRepointSecondaryBlocks() {
         var layout = GetLayout();
         var attributeSize = model.IsFRLG() ? 4 : 2;
         var (blockCount, _) = EstimateBlockCount(layout, false);
         RepointBlocksetMember(Format.SecondaryBlockset, Format.Blocks, blockCount * 16);
         RepointBlocksetMember(Format.SecondaryBlockset, Format.BlockAttributes, blockCount * attributeSize, repointSecondaryBlocks);
      }

      public string ExpandPrimaryBlocksText {
         get {
            var (currentCount, maxCount) = EstimateBlockCount(GetLayout(), true);
            return $"This primary blockset contains {currentCount} of {maxCount} blocks.";
         }
      }

      private bool CanExpandPrimaryBlocks() {
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, true);
         return blockCount < maxBlockCount;
      }

      private void ExecuteExpandPrimaryBlocks() {
         ExpandBlocks(true);
         expandPrimaryBlocks.RaiseCanExecuteChanged();
      }

      public string ExpandSecondaryBlocksText {
         get {
            var (currentCount, maxCount) = EstimateBlockCount(GetLayout(), false);
            return $"This secondary blockset contains {currentCount} of {maxCount} blocks.";
         }
      }

      private bool CanExpandSecondaryBlocks() {
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, false);
         return blockCount < maxBlockCount;
      }

      private void ExecuteExpandSecondaryBlocks() {
         ExpandBlocks(false);
         expandSecondaryBlocks.RaiseCanExecuteChanged();
      }

      private void ExpandBlocks(bool primary) {
         // expand blocks and attributes
         // neither have any formatting, so here's the plan:
         var token = history.CurrentChange;
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, primary);
         var attributeSize = model.IsFRLG() ? 4 : 2;
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;

         // (1) copy/repoint/paste the blocks
         var blockStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.Blocks);
         var blockData = Cut(blockStart, blockCount * 16);
         blockStart = model.RelocateForExpansion(token, model.GetNextRun(blockStart), maxBlockCount * 16).Start;
         Paste(blockStart, blockData, maxBlockCount * 16);

         // (2) copy/repoint/paste the attributes
         var attributeStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.BlockAttributes);
         var attributeData = Cut(attributeStart, blockCount * attributeSize);
         attributeStart = model.RelocateForExpansion(token, model.GetNextRun(attributeStart), maxBlockCount * attributeSize).Start;
         Paste(attributeStart, attributeData, maxBlockCount * attributeSize);

         DataMoved.Raise(this, new("Block", blockStart));
         history.ChangeCompleted();
      }

      public (int currentCount, int maxCount) EstimateBlockCount(ModelArrayElement layout, bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         if (layout == null) return (0, 0);
         var blockset = layout.GetSubTable(blocksetName)[0];
         var blockCount = model.IsFRLG() ? 640 : 512;
         if (!primary) blockCount = 1024 - blockCount;
         var maxBlockCount = blockCount;
         var blockStart = blockset.GetAddress(Format.Blocks);
         var attributeStart = blockset.GetAddress(Format.BlockAttributes);
         BlocksetModel.EstimateBlockCount(model, ref blockCount, blockStart, attributeStart);
         return (blockCount, maxBlockCount);
      }

      public void ExportPrimaryBlocks() => ExportBlocks(true);

      public void ImportPrimaryBlocks() => ImportBlocks(true);

      public void ExportSecondaryBlocks() => ExportBlocks(false);

      public void ImportSecondaryBlocks() => ImportBlocks(false);

      private void ExportBlocks(bool primary) {
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, primary);
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var blockStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.Blocks);
         var attributeStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.BlockAttributes);
         var attributeSize = model.IsFRLG() ? 2 : 1;

         var exportData = new byte[(16 + attributeSize) * blockCount];
         Array.Copy(model.RawData, blockStart, exportData, 0, blockCount * 16);
         Array.Copy(model.RawData, attributeStart, exportData, blockCount * 16, blockCount * attributeSize);
         ExportBytes(exportData);
      }

      private void ImportBlocks(bool primary) {
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, primary);
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var blockStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.Blocks);
         var attributeStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.BlockAttributes);
         var attributeSize = model.IsFRLG() ? 2 : 1;

         var file = fileSystem.OpenFile("Byte File", "bin");
         if (file == null) return;
         var bytes = file.Contents;
         var unitLength = 16 + attributeSize;
         if (bytes.Length % unitLength != 0) {
            viewPort.RaiseError($"Import data should be a multiple of {unitLength} bytes, but was {bytes.Length} bytes.");
            return;
         }
         var unitCount = bytes.Length / unitLength;
         var blockData = new byte[unitCount * 16];
         var attributeData = new byte[attributeSize * unitCount];
         Array.Copy(bytes, blockData, blockData.Length);
         Array.Copy(bytes, blockData.Length, attributeData, 0, attributeData.Length);

         if (ImportBytes(blockStart, blockCount * 16, maxBlockCount * 16, blockData)) {
            if (ImportBytes(attributeStart, blockCount * attributeSize, maxBlockCount * attributeSize, attributeData)) {
               blockStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.Blocks);
               DataMoved.Raise(this, new("Blockset", blockStart));
            }
         }
      }

      #endregion

      #region Tilesets

      public string RepointPrimaryTilesetText {
         get {
            var count = CountBlocksetMemberSources(Format.PrimaryBlockset, Format.Tileset);
            return $"These primary tiles are used by {count} blocksets.";
         }
      }

      private bool CanRepointPrimaryTileset() => CountBlocksetMemberSources(Format.PrimaryBlockset, Format.Tileset) > 1;

      private void ExecuteRepointPrimaryTileset() => RepointTileset(Format.PrimaryBlockset, repointPrimaryTileset);

      public string RepointSecondaryTilesetText {
         get {
            var count = CountBlocksetMemberSources(Format.SecondaryBlockset, Format.Tileset);
            return $"These secondary tiles are used by {count} blocksets.";
         }
      }

      private bool CanRepointSecondaryTileset() => CountBlocksetMemberSources(Format.SecondaryBlockset, Format.Tileset) > 1;

      private void ExecuteRepointSecondaryTileset() => RepointTileset(Format.SecondaryBlockset, repointSecondaryTileset);

      private void RepointTileset(string blocksetName, StubCommand command) {
         var maxTileCount = model.IsFRLG() ? 640 : 512;
         if (blocksetName == Format.SecondaryBlockset) maxTileCount = 1024 - maxTileCount;
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var isCompressed = blockset.GetValue("isCompressed") != 0;
         if (isCompressed) {
            var address = blockset.GetAddress(Format.Tileset);
            var run = new LZRun(model, address);
            RepointBlocksetMember(blocksetName, Format.Tileset, run.Length, command);
         } else {
            var address = blockset.GetAddress(Format.Tileset);
            BlocksetModel.EstimateTileCount(model, ref maxTileCount, address);
            RepointBlocksetMember(blocksetName, Format.Tileset, maxTileCount * 32, command);
         }
      }

      public string ExpandPrimaryTilesetText {
         get {
            var layout = GetLayout();
            var (currentCount, maxCount) = layout == null ? (0, 0) : EstimateTileCount(layout.GetSubTable(Format.PrimaryBlockset)[0]);
            return $"This primary tileset contains {currentCount} of {maxCount} tiles.";
         }
      }

      private bool CanExpandPrimaryTileset() => CanExpandTileset(Format.PrimaryBlockset);

      private void ExecuteExpandPrimaryTileset() {
         ExpandTileset(Format.PrimaryBlockset);
         expandPrimaryTileset.RaiseCanExecuteChanged();
      }

      public string ExpandSecondaryTilesetText {
         get {
            var layout = GetLayout();
            var (currentCount, maxCount) = layout == null ? (0, 0) : EstimateTileCount(layout.GetSubTable(Format.SecondaryBlockset)[0]);
            return $"This secondary tileset contains {currentCount} of {maxCount} tiles.";
         }
      }

      private bool CanExpandSecondaryTileset() => CanExpandTileset(Format.SecondaryBlockset);

      private void ExecuteExpandSecondaryTileset() {
         ExpandTileset(Format.SecondaryBlockset);
         expandSecondaryTileset.RaiseCanExecuteChanged();
      }

      private bool CanExpandTileset(string blocksetName) {
         var layout = GetLayout();
         if (layout == null) return false;
         var blockset = layout.GetSubTable(blocksetName)[0];
         var (currentTiles, maxTiles) = EstimateTileCount(blockset);
         return currentTiles < maxTiles;
      }

      private void ExpandTileset(string blocksetName) {
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var (currentTiles, maxTiles) = EstimateTileCount(blockset);
         var start = blockset.GetAddress(Format.Tileset);
         if (blockset.GetValue("isCompressed") != 0) {
            var run = new LZRun(model, start);
            var compressedData = new byte[run.Length];
            Array.Copy(model.RawData, start, compressedData, 0, run.Length);
            var decompressedData = LZRun.Decompress(compressedData, 0);
            var newData = new byte[maxTiles * 0x20];
            Array.Copy(decompressedData, newData, decompressedData.Length);
            var newCompressedData = LZRun.Compress(newData).ToArray();
            var newRun = model.RelocateForExpansion(history.CurrentChange, model.GetNextRun(start), run.Length, newCompressedData.Length);
            Paste(newRun.Start, newCompressedData, newCompressedData.Length);
            start = newRun.Start;
            model.ObserveRunWritten(history.CurrentChange, newRun.Duplicate(newRun.Start, newRun.PointerSources)); // run length changed
         } else {
            var data = Cut(start, currentTiles * 0x20);
            var newRun = model.RelocateForExpansion(history.CurrentChange, model.GetNextRun(start), maxTiles * 0x20);
            Paste(newRun.Start, data, maxTiles * 0x20);
            start = newRun.Start;
         }

         DataMoved.Raise(this, new("Tileset", start));
         history.ChangeCompleted();
      }

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

      public void ExportPrimaryTileset() => ExportTileset(true);

      public void ImportPrimaryTileset() => ImportTileset(true);

      public void ExportSecondaryTileset() => ExportTileset(false);

      public void ImportSecondaryTileset() => ImportTileset(false);

      private void ExportTileset(bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var (currentTiles, maxTiles) = EstimateTileCount(blockset);
         var start = blockset.GetAddress(Format.Tileset);
         var isCompressed = blockset.GetValue("isCompressed") != 0;
         if (isCompressed) {
            var run = new LZRun(model, start);
            ExportBytes(start, run.Length);
         } else {
            ExportBytes(start, currentTiles * 0x20);
         }
      }

      private void ImportTileset(bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var (currentTiles, maxTiles) = EstimateTileCount(blockset);
         var start = blockset.GetAddress(Format.Tileset);
         var isCompressed = blockset.GetValue("isCompressed") != 0;
         if (isCompressed) {
            var file = fileSystem.OpenFile("Byte File", "bin");
            if (file == null) return;
            var existingRun = model.GetNextRun(start);
            var newRun = model.RelocateForExpansion(history.CurrentChange, model.GetNextRun(start), file.Contents.Length);
            history.CurrentChange.ChangeData(model, newRun.Start, file.Contents);
            if (newRun is LzTilesetRun tsRun) {
               tsRun = new LzTilesetRun(tsRun.TilesetFormat, model, tsRun.Start, tsRun.PointerSources);
               model.ObserveRunWritten(history.CurrentChange, tsRun);
            }
            if (newRun.Start != start) {
               DataMoved.Raise(this, new("Tileset", newRun.Start));
            } else {
               DataMoved.Raise(this, null);
            }
            for (int i = file.Contents.Length; i < existingRun.Length; i++) {
               history.CurrentChange.ChangeData(model, newRun.Start + i, 0xFF);
            }
         } else {
            if (ImportBytes(start, currentTiles * 0x20, maxTiles * 0x20)) {
               DataMoved.Raise(this, null);
            }
         }
      }

      #endregion

      #region Palettes

      public string RepointPrimaryPalettesText => $"These primary palettes are used by {CountBlocksetMemberSources(Format.PrimaryBlockset, Format.Palette)} blocksets.";

      private bool CanRepointPrimaryPalette() => CountBlocksetMemberSources(Format.PrimaryBlockset, Format.Palette) > 1;

      private void ExecuteRepointPrimaryPalette() {
         RepointBlocksetMember(Format.PrimaryBlockset, Format.Palette, 0x200, repointPrimaryPalette);
      }

      public string RepointSecondaryPalettesText => $"These secondary palettes are used by {CountBlocksetMemberSources(Format.SecondaryBlockset, Format.Palette)} blocksets.";

      private bool CanRepointSecondaryPalette() => CountBlocksetMemberSources(Format.SecondaryBlockset, Format.Palette) > 1;

      private void ExecuteRepointSecondaryPalette() {
         RepointBlocksetMember(Format.SecondaryBlockset, Format.Palette, 0x200, repointSecondaryPalette);
      }

      public string ExpandPrimaryPaletteText => string.Empty;

      private bool CanExpandPrimaryPalette() => false;

      private void ExecuteExpandPrimaryPalette() => throw new NotImplementedException();

      public string ExpandSecondaryPaletteText => string.Empty;

      private bool CanExpandSecondaryPalette() => false;

      private void ExecuteExpandSecondaryPalette() => throw new NotImplementedException();

      public void ExportPrimaryPalette() => ExportPalette(true);

      public void ImportPrimaryPalette() => ImportPalette(true);

      public void ExportSecondaryPalette() => ExportPalette(false);

      public void ImportSecondaryPalette() => ImportPalette(false);

      private void ExportPalette(bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var start = blockset.GetAddress(Format.Palette);
         var folder = fileSystem.OpenFolder();
         if (folder == null) return;
         for (int i = 0; i < 16; i++) ExportPal(folder, i, start + i * 32);
      }

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

      private void ImportPalette(bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var start = blockset.GetAddress(Format.Palette);
         var folder = fileSystem.OpenFolder();
         if (folder == null) return;
         for (int i = 0; i < 16; i++) ImportPal(folder, i, start + i * 32);
         DataMoved.Raise(this, null);
      }

      private void ImportPal(string folder, int nameIndex, int start) {
         var file = fileSystem.LoadFile(folder + "/" + nameIndex.ToString("X2") + ".pal");
         if (file == null) return;
         var lines = Encoding.ASCII.GetString(file.Contents).SplitLines();
         foreach (var line in lines) {
            var parts = line.Split(' ');
            if (parts.Length != 3) continue;
            if (!parts[0].TryParseInt(out var r)) continue;
            if (!parts[1].TryParseInt(out var g)) continue;
            if (!parts[2].TryParseInt(out var b)) continue;
            r /= 8;
            g /= 8;
            b /= 8;
            model.WriteMultiByteValue(start, 2, history.CurrentChange, UncompressedPaletteColor.Pack(r, g, b));
            start += 2;
         }
      }

      #endregion

      private int CountBlocksetMemberSources(string blocksetName, string member) {
         var layout = GetLayout();
         if (layout == null) return 0;
         var blockset = layout.GetSubTable(blocksetName);
         if (blockset == null) return 0;
         var start = blockset[0].GetAddress(member);
         var run = model.GetNextRun(start);
         return run.PointerSources == null ? 0 : run.PointerSources.Count;
      }

      private void RepointBlocksetMember(string blocksetName, string member, int length, params StubCommand[] commands) {
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var start = DuplicateData(blockset.GetAddress(member), length);
         blockset.SetAddress(member, start);
         DataMoved.Raise(this, new(char.ToUpper(member[0]) + member.Substring(1), start));
         foreach (var command in commands) command.RaiseCanExecuteChanged();
      }

      private byte[] Cut(int start, int length) => model.Cut(history.CurrentChange, start, length);

      private void Paste(int start, byte[] data, int length) => model.Paste(history.CurrentChange, start, data, length);

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

      public int GetMapBankForNewMap(string prompt) {
         var tokenFactory = () => history.CurrentChange;
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, tokenFactory);
         var options = mapBanks.Count.Range(i => i.ToString()).ToList();
         options.Add("Create New Bank");
         var enumViewModel = new EnumViewModel(options.ToArray()) { Choice = format.RecentBank.LimitToRange(0, options.Count) };
         var option = fileSystem.ShowOptions(
            "Pick a bank",
            prompt,
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Insert New Map" });
         if (option == -1) return option;
         format.RecentBank = enumViewModel.Choice;
         return enumViewModel.Choice;
      }

      /// <summary>
      /// Expands the chosen map bank by one, adding a new map to the end.
      /// </summary>
      /// <returns>The table that contains the new map.</returns>
      public ITableRun AddNewMapToBank(int bankIndex) {
         var tokenFactory = () => history.CurrentChange;
         var token = history.CurrentChange;
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, tokenFactory);
         ITableRun mapTable;
         if (mapBanks.Count == bankIndex) {
            var newTable = model.RelocateForExpansion(token, mapBanks.Run, mapBanks.Run.Length + mapBanks.Run.ElementLength);
            newTable = newTable.Append(token, 1);
            model.ObserveRunWritten(token, newTable);
            mapBanks = new ModelTable(model, newTable.Start, tokenFactory, newTable);
            var tableStart = model.FindFreeSpace(model.FreeSpaceStart, 8);
            mapTable = new TableStreamRun(model, tableStart, SortedSpan.One(mapBanks[bankIndex].Start), $"[map<{format.MapFormat}1>]", null, new DynamicStreamStrategy(model, null), 0);
            model.UpdateArrayPointer(token, null, null, -1, mapBanks[bankIndex].Start, tableStart);
         } else {
            mapTable = mapBanks[bankIndex].GetSubTable("maps").Run;
         }
         if (mapTable.ElementCount >= MaxMapsPerBank) return null; // don't add another map
         mapTable = mapTable.Append(token, 1);
         model.ObserveRunWritten(token, mapTable);
         return mapTable;
      }

      /// <summary>
      /// Inserts a new map using the current map's layout.
      /// Creates event data with 0 events, map scripts data with 0 scripts, and connection data with 0 connections.
      /// Copies all the flags/header from the current map.
      /// </summary>
      public MapModel CreateNewMap(ModelDelta token) {
         var currentMap = GetMapModel();
         var mapStart = model.FindFreeSpace(model.FreeSpaceStart, 28);
         // music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags. floorNum. battleType.
         for (int i = 16; i < 28; i++) token.ChangeData(model, mapStart + i, model[currentMap.Start + i]);

         WritePointerAndSource(token, mapStart + 0, model.ReadPointer(currentMap.Start));
         WritePointerAndSource(token, mapStart + 4, CreateNewEvents(token));
         WritePointerAndSource(token, mapStart + 8, CreateNewMapScripts(token));
         WritePointerAndSource(token, mapStart + 12, CreateNewConnections(token));

         var table = new TableStreamRun(model, mapStart, SortedSpan<int>.None, format.MapFormat, null, new FixedLengthStreamStrategy(1));
         model.ObserveRunWritten(token, table);

         DataMoved.Raise(this, new("Map", mapStart));
         return new MapModel(new ModelArrayElement(model, mapStart, 0, () => token, table));
      }

      /// <summary>
      /// Creates a new layout using the existing layout's borderblock, blockmap, primary blockset, and secondary blockset.
      /// Once this new layout is assigned to a map, you'll want to update that map's layout ID by calling BlockMapViewModel.UpdateLayoutID()
      /// </summary>
      public LayoutModel CreateNewLayout(ModelDelta token) {
         var layoutStart = model.FindFreeSpace(model.FreeSpaceStart, 28);
         var myLayout = GetLayout();

         model.WriteValue(token, layoutStart + 0, myLayout.GetValue("width"));
         model.WriteValue(token, layoutStart + 4, myLayout.GetValue("height"));
         WritePointerAndSource(token, layoutStart + 8, myLayout.GetAddress(Format.BorderBlock));
         WritePointerAndSource(token, layoutStart + 12, myLayout.GetAddress(Format.BlockMap));
         WritePointerAndSource(token, layoutStart + 16, myLayout.GetAddress(Format.PrimaryBlockset));
         WritePointerAndSource(token, layoutStart + 20, myLayout.GetAddress(Format.SecondaryBlockset));
         if (myLayout.HasField(Format.BorderWidth)) {
            model.WriteValue(token, layoutStart + 24, myLayout.GetValue(Format.BorderWidth));
            model.WriteValue(token, layoutStart + 25, myLayout.GetValue(Format.BorderHeight));
            model.WriteMultiByteValue(layoutStart + 26, 2, token, 0);
         }
         if (ArrayRun.TryParse(model, format.LayoutFormat, layoutStart, SortedSpan<int>.None, out var table) != ErrorInfo.NoError) throw new NotImplementedException();
         model.ObserveRunWritten(token, table);
         return new(new(model, table.Start, 0, () => token, table));
      }

      public int CreateNewBlockMap(ModelDelta token, int width, int height) {
         var blockmapLength = width * height * 2;
         var blockmapStart = model.FindFreeSpace(model.FreeSpaceStart, blockmapLength);
         token.ChangeData(model, blockmapStart, new byte[blockmapLength]);
         var run = new BlockmapRun(model, blockmapStart, SortedSpan<int>.None, width, height);
         model.ObserveRunWritten(token, run);
         return blockmapStart;
      }

      public int CreateNewBorderBlock(ModelDelta token) {
         var layout = GetLayout();
         var count = 4;
         if (layout.HasField(Format.BorderWidth) && layout.HasField(Format.BorderHeight)) {
            count = Math.Max(4, layout.GetValue(Format.BorderWidth) * layout.GetValue(Format.BorderHeight));
         }
         var myStart = layout.GetAddress(Format.BorderBlock);
         var start = model.FindFreeSpace(model.FreeSpaceStart, count * 2);
         for (int i = 0; i < count; i++) model.WriteMultiByteValue(start + i * 2, 2, token, model.ReadMultiByteValue(myStart + i * 2, 2));
         return start;
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

      private ModelArrayElement GetMapModel() {
         return BlockMapViewModel.GetMapModel(model, mapID / 1000, mapID % 1000, () => history.CurrentChange);
      }

      private ModelArrayElement GetLayout(ModelArrayElement map = null) {
         if (map == null) map = GetMapModel();
         if (map == null) return null;
         var layout = map.GetSubTable(Format.Layout);
         if (layout == null) return null;
         return layout[0];
      }

      private int DuplicateData(int start, int length) {
         return DuplicateData(model, history, start, length);
      }

      public static int DuplicateData(IDataModel model, ChangeHistory<ModelDelta> history, int start, int length) {
         var newStart = model.FindFreeSpace(model.FreeSpaceStart, length);
         var token = history.CurrentChange;
         for (int i = 0; i < length; i++) token.ChangeData(model, newStart + i, model[start + i]);
         return newStart;
      }

      private void WritePointerAndSource(ModelDelta token, int source, int destination) {
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, NoInfoRun.FromPointer(model, source));
      }

      private bool ImportBytes(int address, int currentSize, int maxSize, byte[] bytes = null) {
         if (bytes == null) {
            var file = fileSystem.OpenFile("Byte File", "bin");
            if (file == null) return false;
            bytes = file.Contents;
         }
         var minSize = 0;
         if (maxSize < 0) (minSize, maxSize) = (currentSize, currentSize);

         if (bytes.Length > maxSize) {
            viewPort.RaiseError($"Expected a file with no more than {maxSize} bytes, but got {bytes.Length} instead.");
            return false;
         }
         if (bytes.Length < minSize) {
            viewPort.RaiseError($"Expected a file with no less than {minSize} bytes, but got {bytes.Length} instead.");
            return false;
         }
         var token = viewPort.ChangeHistory.CurrentChange;
         var newRun = model.RelocateForExpansion(token, model.GetNextRun(address), currentSize, bytes.Length);
         token.ChangeData(model, newRun.Start, bytes);
         for (int i = bytes.Length; i < currentSize; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         return true;
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
