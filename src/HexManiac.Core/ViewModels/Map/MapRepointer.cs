using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public record ChangeMapEventArgs(int Bank, int Map);

   public class MapRepointer : ViewModelCore {

      private readonly Format format;
      private readonly IFileSystem fileSystem;
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly int mapID; // bank * 1000 + map

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

      public event EventHandler DataMoved;
      public event EventHandler<ChangeMapEventArgs> ChangeMap;

      public MapRepointer(Format format, IFileSystem fileSystem, IDataModel model, ChangeHistory<ModelDelta> history, int mapID) {
         this.format = format;
         this.fileSystem = fileSystem;
         this.model = model;
         this.history = history;
         this.mapID = mapID;
      }

      #region General

      private bool CanRepointLayout() {
         var map = GetMapModel();
         if (map == null) return false;
         var layoutStart = map.GetAddress(Format.Layout);
         if (layoutStart < 0 || layoutStart >= model.Count) return false;
         var layoutRun = model.GetNextRun(layoutStart);
         return layoutRun.PointerSources != null && layoutRun.PointerSources.Count > 1;
      }

      private void ExecuteRepointLayout() {
         var map = GetMapModel();
         var layoutStart = map.GetAddress(Format.Layout);
         var layoutRun = model.GetNextRun(layoutStart);
         var newDataStart = DuplicateData(layoutRun.Start, layoutRun.Length);
         map.SetAddress(Format.Layout, newDataStart);
         DataMoved.Raise(this);
         repointLayout.RaiseCanExecuteChanged();
         repointBorderBlock.RaiseCanExecuteChanged();
         repointBlockMap.RaiseCanExecuteChanged();
         repointPrimaryBlockset.RaiseCanExecuteChanged();
         repointSecondaryBlockset.RaiseCanExecuteChanged();
      }

      private bool CanRepointBorderBlock() => CanRepointLayoutMember(Format.BorderBlock);

      private void ExecuteRepointBorderBlock() {
         var layout = GetLayout();
         var count = 4;
         if (layout.HasField(Format.BorderWidth) && layout.HasField(Format.BorderHeight)) {
            count = Math.Max(4, layout.GetValue(Format.BorderWidth) * layout.GetValue(Format.BorderHeight));
         }
         ExecuteRepointLayoutMember(Format.BorderBlock, count * 2, repointBorderBlock);
      }

      private bool CanRepointBlockMap() => CanRepointLayoutMember(Format.BlockMap);

      private void ExecuteRepointBlockMap() {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         ExecuteRepointLayoutMember(Format.BlockMap, width * height * 2, repointBlockMap);
      }

      private bool CanRepointPrimaryBlockset() => CanRepointLayoutMember(Format.PrimaryBlockset);

      private void ExecuteRepointPrimaryBlockset() {
         ExecuteRepointLayoutMember(Format.PrimaryBlockset, 6 * 4,
            repointPrimaryBlockset, repointPrimaryBlocks,
            repointPrimaryTileset, repointPrimaryPalette,
            expandPrimaryBlocks, expandPrimaryTileset,
            expandPrimaryPalette, createPrimaryTilesetAnimations);
      }

      private bool CanRepointSecondaryBlockset() => CanRepointLayoutMember(Format.SecondaryBlockset);

      private void ExecuteRepointSecondaryBlockset() {
         ExecuteRepointLayoutMember(Format.SecondaryBlockset, 6 * 4,
            repointSecondaryBlockset, repointSecondaryBlocks,
            repointSecondaryTileset, repointSecondaryPalette,
            expandSecondaryBlocks, expandSecondaryTileset,
            expandSecondaryPalette, createSecondaryTilesetAnimations);
      }

      private bool CanDuplicateMap() {
         return true;
      }

      private void ExecuteDuplicateMap() {
         var option = GetMapBankForNewMap("Duplicate map into which group?");
         if (option == -1) return;
         var table = AddNewMapToBank(option);
         var newMapStart = CreateNewMap(history.CurrentChange);
         model.UpdateArrayPointer(history.CurrentChange, null, null, -1, table.Start + table.Length - 4, newMapStart);
         ChangeMap.Raise(this, new(option, table.ElementCount - 1));
         repointLayout.RaiseCanExecuteChanged();
      }

      private bool CanRepointLayoutMember(string member) {
         var layout = GetLayout();
         if (layout == null) return false;
         var start = layout.GetAddress(member);
         var run = model.GetNextRun(start);
         return run.PointerSources != null && run.PointerSources.Count > 1;
      }

      private void ExecuteRepointLayoutMember(string member, int length, params StubCommand[] commands) {
         var layout = GetLayout();
         var start = DuplicateData(layout.GetAddress(member), length);
         layout.SetAddress(member, start);
         DataMoved.Raise(this);
         foreach (var command in commands) command.RaiseCanExecuteChanged();
      }

      #endregion

      #region Blocks / Tilesets / Palettes

      private bool CanRepointPrimaryBlocks() => CanRepointBlocksetMember(Format.PrimaryBlockset, Format.Blocks);

      private void ExecuteRepointPrimaryBlocks() {
         var layout = GetLayout();
         var attributeSize = model.IsFRLG() ? 2 : 1;
         var (blockCount, _) = EstimateBlockCount(layout, true);
         ExecuteRepointBlocksetMember(Format.PrimaryBlockset, Format.Blocks, blockCount * 16);
         ExecuteRepointBlocksetMember(Format.PrimaryBlockset, Format.BlockAttributes, blockCount * attributeSize, repointPrimaryBlocks);
      }

      private bool CanRepointSecondaryBlocks() => CanRepointBlocksetMember(Format.SecondaryBlockset, Format.Blocks);

      private void ExecuteRepointSecondaryBlocks() {
         var layout = GetLayout();
         var attributeSize = model.IsFRLG() ? 2 : 1;
         var (blockCount, _) = EstimateBlockCount(layout, false);
         ExecuteRepointBlocksetMember(Format.PrimaryBlockset, Format.Blocks, blockCount * 16);
         ExecuteRepointBlocksetMember(Format.PrimaryBlockset, Format.BlockAttributes, blockCount * attributeSize, repointPrimaryBlocks);
      }

      private bool CanExpandPrimaryBlocks() {
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, true);
         return blockCount < maxBlockCount;
      }

      private void ExecuteExpandPrimaryBlocks() {
         ExecuteExpandBlocks(true);
         expandPrimaryBlocks.RaiseCanExecuteChanged();
      }

      private bool CanExpandSecondaryBlocks() {
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, false);
         return blockCount < maxBlockCount;
      }

      private void ExecuteExpandSecondaryBlocks() {
         ExecuteExpandBlocks(false);
         expandPrimaryBlocks.RaiseCanExecuteChanged();
      }

      private void ExecuteExpandBlocks(bool primary) {
         // expand blocks and attributes
         // neither have any formatting, so here's the plan:
         var token = history.CurrentChange;
         var layout = GetLayout();
         var (blockCount, maxBlockCount) = EstimateBlockCount(layout, primary);
         var attributeSize = model.IsFRLG() ? 2 : 1;
         // (1) copy all block/attribute data into a temp array
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var blockStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.Blocks);
         var attributeStart = layout.GetSubTable(blocksetName)[0].GetAddress(Format.BlockAttributes);
         var blockData = Cut(blockStart, blockCount * 16);
         var attributeData = Cut(attributeStart, blockCount * attributeSize);
         // (2) repoint blocks/attrbutes
         blockStart = model.RelocateForExpansion(token, model.GetNextRun(blockStart), maxBlockCount * 16).Start;
         attributeStart = model.RelocateForExpansion(token, model.GetNextRun(attributeStart), maxBlockCount * attributeSize).Start;
         // (3) paste old data and expand
         Paste(blockStart, blockData, maxBlockCount * 16);
         Paste(attributeStart, attributeData, maxBlockCount * attributeSize);

         DataMoved.Raise(this);
      }

      private byte[] Cut(int start, int length) {
         var result = new byte[length];
         var token = history.CurrentChange;
         for (int i = 0; i < length; i++) {
            result[i] = model[start + i];
            token.ChangeData(model, start + i, 0xFF);
         }
         return result;
      }

      private void Paste(int start, byte[] data, int length) {
         var token = history.CurrentChange;
         token.ChangeData(model, start, data);
         for (int i = data.Length; i < length; i++) token.ChangeData(model, start + i, 0);
      }

      private (int, int) EstimateBlockCount(ModelArrayElement layout, bool primary) {
         var blocksetName = primary ? Format.PrimaryBlockset : Format.SecondaryBlockset;
         var blockset = layout.GetSubTable(blocksetName)[0];
         var blockCount = model.IsFRLG() ? 640 : 512;
         if (!primary) blockCount = 1024 - blockCount;
         var maxBlockCount = blockCount;
         var blockStart = blockset.GetAddress(Format.Blocks);
         var attributeStart = blockset.GetAddress(Format.BlockAttributes);
         BlocksetModel.EstimateBlockCount(model, ref blockCount, blockStart, attributeStart);
         return (blockCount, maxBlockCount);
      }


      private bool CanRepointPrimaryTileset() => CanRepointBlocksetMember(Format.PrimaryBlockset, Format.Tileset);

      private void ExecuteRepointPrimaryTileset() => ExecuteRepointTileset(Format.PrimaryBlockset);
      private void ExecuteRepointTileset(string blocksetName) {
         var maxTileCount = model.IsFRLG() ? 640 : 512;
         if (blocksetName == Format.SecondaryBlockset) maxTileCount = 1024 - maxTileCount;
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var isCompressed = blockset.GetValue("isCompressed") != 0;
         if (isCompressed) RepointCompressedTileset(blocksetName, maxTileCount);
         else RepointUncompressedTileset(blocksetName, maxTileCount);
      }

      private bool CanRepointSecondaryTileset() => CanRepointBlocksetMember(Format.SecondaryBlockset, Format.Tileset);

      private void ExecuteRepointSecondaryTileset() => ExecuteRepointTileset(Format.PrimaryBlockset);

      private bool CanExpandPrimaryTileset() {
         return false;
      }

      private void ExecuteExpandPrimaryTileset() {
         // example uncompressed: emerald bank 25 map 0 -> primary tileset starts at 37AA48 and is 0x8000 bytes. 0x20 bytes is one tile, so that's 0x400 tiles, but the limit should be 0x200?
         // secondary tileset starts at 36B9DF and appears to be only 0xA60 bytes (0x53 tiles) long
      }

      private bool CanExpandSecondaryTileset() {
         return false;
      }

      private void ExecuteExpandSecondaryTileset() {

      }

      private void RepointCompressedTileset(string blocksetName, int maxTileCount) {
         throw new NotImplementedException();
      }

      private void RepointUncompressedTileset(string blocksetName, int maxTileCount) {
         throw new NotImplementedException();
      }

      private bool CanRepointPrimaryPalette() => CanRepointBlocksetMember(Format.PrimaryBlockset, Format.Palette);

      private void ExecuteRepointPrimaryPalette() {
         ExecuteRepointBlocksetMember(Format.PrimaryBlockset, Format.Palette, 32, repointPrimaryPalette);
      }

      private bool CanRepointSecondaryPalette() => CanRepointBlocksetMember(Format.SecondaryBlockset, Format.Palette);

      private void ExecuteRepointSecondaryPalette() {
         ExecuteRepointBlocksetMember(Format.SecondaryBlockset, Format.Palette, 32, repointSecondaryPalette);
      }

      private bool CanExpandPrimaryPalette() {
         return false;
      }

      private void ExecuteExpandPrimaryPalette() {

      }

      private bool CanExpandSecondaryPalette() {
         return false;
      }

      private void ExecuteExpandSecondaryPalette() {

      }


      private bool CanRepointBlocksetMember(string blocksetName, string member) {
         var layout = GetLayout();
         if (layout == null) return false;
         var blockset = layout.GetSubTable(blocksetName);
         if (blockset == null) return false;
         var start = blockset[0].GetAddress(member);
         var run = model.GetNextRun(start);
         return run.PointerSources != null && run.PointerSources.Count > 1;
      }

      private void ExecuteRepointBlocksetMember(string blocksetName, string member, int length, params StubCommand[] commands) {
         var layout = GetLayout();
         var blockset = layout.GetSubTable(blocksetName)[0];
         var start = DuplicateData(blockset.GetAddress(member), length);
         blockset.SetAddress(member, start);
         DataMoved.Raise(this);
         foreach (var command in commands) command.RaiseCanExecuteChanged();
      }

      #endregion

      #region Animations

      private bool CanCreatePrimaryTilesetAnimations() {
         // we should be able to run the utility to create a tileset animation unless we already have
         return false;
      }

      private void ExecuteCreatePrimaryTilesetAnimations() {
         // TODO grab the code from the utility and use that here
         throw new NotImplementedException();
      }

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
         var enumViewModel = new EnumViewModel(mapBanks.Count.Range(i => i.ToString()).ToArray());
         var option = fileSystem.ShowOptions(
            "Pick a group",
            prompt,
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Insert New Map" });
         if (option == -1) return option;
         return enumViewModel.Choice;
      }

      /// <summary>
      /// Expands the chosen map bank by one, adding a new map to the end.
      /// </summary>
      /// <returns>The table that contains the new map.</returns>
      public ITableRun AddNewMapToBank(int option) {
         var tokenFactory = () => history.CurrentChange;
         var token = history.CurrentChange;
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, tokenFactory);
         ITableRun mapTable;
         if (mapBanks.Count == option) {
            var newTable = model.RelocateForExpansion(token, mapBanks.Run, mapBanks.Run.Length + mapBanks.Run.ElementLength);
            newTable = newTable.Append(token, 1);
            model.ObserveRunWritten(token, newTable);
            mapBanks = new ModelTable(model, newTable.Start, tokenFactory, newTable);
            var tableStart = model.FindFreeSpace(model.FreeSpaceStart, 8);
            mapTable = new TableStreamRun(model, tableStart, SortedSpan.One(mapBanks[option].Start), $"[map<{format.MapFormat}1>]", null, new DynamicStreamStrategy(model, null), 0);
            model.UpdateArrayPointer(token, null, null, -1, mapBanks[option].Start, tableStart);
         } else {
            mapTable = mapBanks[option].GetSubTable("maps").Run;
         }
         mapTable = mapTable.Append(token, 1);
         model.ObserveRunWritten(token, mapTable);
         return mapTable;
      }

      /// <summary>
      /// Inserts a new map using the current map's layout.
      /// Creates event data with 0 events, map scripts data with 0 scripts, and connection data with 0 connections.
      /// Copies all the flags/header from the current map.
      /// </summary>
      public int CreateNewMap(ModelDelta token) {
         var currentMap = GetMapModel();
         var mapStart = model.FindFreeSpace(model.FreeSpaceStart, 28);
         // music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags. floorNum. battleType.
         for (int i = 16; i < 28; i++) token.ChangeData(model, mapStart + i, model[currentMap.Start + i]);

         WritePointerAndSource(token, mapStart + 4, CreateNewEvents(token));
         WritePointerAndSource(token, mapStart + 8, CreateNewMapScripts(token));
         WritePointerAndSource(token, mapStart + 12, CreateNewConnections(token));

         var table = new TableStreamRun(model, mapStart, SortedSpan<int>.None, format.MapFormat, null, new FixedLengthStreamStrategy(1));
         model.ObserveRunWritten(token, table);

         return mapStart;
      }

      /// <summary>
      /// Creates a new layout using the existing layout's borderblock, blockmap, primary blockset, and secondary blockset.
      /// Once this new layout is assigned to a map, you'll want to update that map's layout ID by calling BlockMapViewModel.UpdateLayoutID()
      /// </summary>
      public int CreateNewLayout(ModelDelta token) {
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
         return layoutStart;
      }

      public int CreateNewBlockMap(ModelDelta token, int width, int height) {
         var blockmapLength = width * height * 2;
         var blockmapStart = model.FindFreeSpace(model.FreeSpaceStart, blockmapLength);
         token.ChangeData(model, blockmapStart, new byte[blockmapLength]);
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
         var newStart = model.FindFreeSpace(model.FreeSpaceStart, length);
         var token = history.CurrentChange;
         for (int i = 0; i < length; i++) token.ChangeData(model, newStart + i, model[start + i]);
         return newStart;
      }

      private void WritePointerAndSource(ModelDelta token, int source, int destination) {
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, NoInfoRun.FromPointer(model, source));
      }

      #endregion
   }
}
