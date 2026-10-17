using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The "Animated Tiles" editor: shows a map's primary and secondary tilesets side by side, lists the animations on them
   /// (the game's own water/flowers/... and the ones added here), previews them live, and lets you add new ones:
   /// click the tiles that should animate, choose how many frames and how fast, then draw or import the frames.
   /// The tab writes the animation table and the code that plays it.
   /// </summary>
   public class TilesetAnimationTab : ViewModelCore, ITabContent {
      private readonly IFileSystem fileSystem;
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly TilesetAnimations animations;
      private readonly DoorAnimations doors;
      private readonly StubCommand close = new();
      private TilesetAnimationConstants constants;

      /// <summary>Petalburg City in Emerald: where the tab starts unless it was opened for a particular map.</summary>
      public const int DefaultGroup = 0, DefaultMap = 0;

      #region ITabContent

      public string Name => "Animated Tiles";
      public bool SpartanMode { get; set; }
      public IDataModel Model => model;
      public bool IsMetadataOnlyChange => false;
      public ICommand Save { get; }
      public ICommand SaveAs { get; }
      public ICommand ExportBackup { get; } = new StubCommand();
      public ICommand Undo { get; }
      public ICommand Redo { get; }
      public ICommand Copy { get; } = new StubCommand();
      public ICommand DeepCopy { get; } = new StubCommand();
      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close => close;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      event EventHandler ITabContent.ClearMessage { add { } remove { } }
      event EventHandler<Action> ITabContent.RequestDelayedWork { add { } remove { } }
      event EventHandler ITabContent.RequestMenuClose { add { } remove { } }
      event EventHandler<Direction> ITabContent.RequestDiff { add { } remove { } }
      event EventHandler<CanDiffEventArgs> ITabContent.RequestCanDiff { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }
      void ITabContent.Refresh() => Reload();
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      #endregion

      public static bool IsSupported(IDataModel model) => TilesetAnimations.IsSupported(model) && model.GetTable(HardcodeTablesModel.MapBankTable) != null;

      private string status = string.Empty;
      public string Status { get => status; private set => Set(ref status, value); }

      public TilesetAnimationTab(IFileSystem fileSystem, ViewPort viewPort, int group, int map, bool secondary) {
         this.fileSystem = fileSystem;
         this.viewPort = viewPort;
         model = viewPort.Model;
         Save = viewPort.Save;
         SaveAs = viewPort.SaveAs;
         Undo = viewPort.Undo;
         Redo = viewPort.Redo;
         close.CanExecute = arg => true;
         close.Execute = arg => Closed?.Invoke(this, EventArgs.Empty);
         animations = new TilesetAnimations(model, () => viewPort.CurrentChange, viewPort.Tools.CodeTool.Parser);
         doors = new DoorAnimations(model, () => viewPort.CurrentChange);
         TilesetAnimationConstants.TryRead(model, out constants);
         Primary = new TilesetPane(this, false);
         Secondary = new TilesetPane(this, true);
         Panes = new[] { Primary, Secondary };
         activePane = secondary ? Secondary : Primary;
         activePane.IsActive = true;
         LoadMaps();
         selectedMap = Maps.FirstOrDefault(option => option.Group == group && option.Map == map)
            ?? Maps.FirstOrDefault(option => option.Group == DefaultGroup && option.Map == DefaultMap)
            ?? Maps.FirstOrDefault();
         Reload();
      }

      #region Map / tileset selection

      public ObservableCollection<MapOption> Maps { get; } = new();

      private MapOption selectedMap;
      public MapOption SelectedMap {
         get => selectedMap;
         set { if (selectedMap == value || value == null) return; selectedMap = value; NotifyPropertyChanged(); Reload(); }
      }

      /// <summary>The map's primary and secondary tilesets: both are shown at the same time.</summary>
      public TilesetPane Primary { get; }
      public TilesetPane Secondary { get; }
      public IReadOnlyList<TilesetPane> Panes { get; }

      private TilesetPane activePane;
      /// <summary>The tileset the 'new animation' and 'new door' boxes work on: the one whose tile (or animation) was clicked last.</summary>
      public TilesetPane ActivePane {
         get => activePane;
         set {
            if (value == null || activePane == value) return;
            activePane.IsActive = false;
            activePane = value;
            activePane.IsActive = true;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(NewAnimationTitle));
            FirstTile = firstTile; // re-clamp to this tileset
            addAnimation?.RaiseCanExecuteChanged();
            addDoor?.RaiseCanExecuteChanged();
            NotifySelectionChanged();
         }
      }

      public string NewAnimationTitle => $"New animation on the {activePane.Title.ToLower()}";

      private MiniBlocksetModel GetBlockset(bool secondary) {
         if (selectedMap == null) return null;
         var all = AllMapsModel.Create(model, () => viewPort.CurrentChange);
         var bank = all[selectedMap.Group];
         var mapModel = bank?[selectedMap.Map];
         var layout = mapModel?.Layout;
         if (layout == null) return null;
         return secondary ? layout.SecondaryBlockset : layout.PrimaryBlockset;
      }

      private void LoadMaps() {
         Maps.Clear();
         try {
            var all = AllMapsModel.Create(model);
            for (int g = 0; g < all.Count; g++) {
               var bank = all[g];
               if (bank == null) continue;
               for (int m = 0; m < bank.Count; m++) {
                  Maps.Add(new MapOption(g, m, BlockMapViewModel.MapIDToText(model, g, m)));
               }
            }
         } catch (Exception) {
            // a broken map table just leaves the list short
         }
      }

      #endregion

      #region Tilesets

      private short[][] palettes;      // all 13 palettes (primary + secondary)

      private double spriteScale = 2;
      public double SpriteScale {
         get => spriteScale;
         set => Set(ref spriteScale, value.LimitToRange(1, 4), old => {
            foreach (var entry in Entries) entry.SpriteScale = spriteScale;
            foreach (var door in Doors) door.SpriteScale = spriteScale;
            foreach (var pane in Panes) pane.SpriteScale = spriteScale;
            NotifySelectionChanged();
            RefreshHighlights();
         });
      }

      public int TilesPerRow => 16;
      public int TileCountInTileset => activePane.TileCount;

      private void LoadPane(TilesetPane pane) {
         pane.Clear();
         var blockset = GetBlockset(pane.IsSecondary);
         pane.Blockset = blockset;
         if (blockset == null || blockset.Start < 0 || blockset.Start >= model.Count) {
            pane.Description = "This map has no " + pane.Title.ToLower() + ".";
            return;
         }
         var full = blockset.FullBlocksetModel;
         pane.Tiles = full.ReadTiles();
         pane.TileCount = pane.Tiles?.Length ?? 0;
         pane.RawTiles = ReadRawTiles(full, pane.TileCount);
         GuessTilePalettes(pane, full);
         pane.SetImage(RenderTileset(pane), spriteScale);
         pane.Description = $"At {blockset.Start:X6}: {pane.TileCount} tiles. {animations.DescribeCallback(blockset.Start)}";
      }

      private static byte[] ReadRawTiles(BlocksetModel blockset, int tileCount) {
         var data = new byte[tileCount * 32];
         var tiles = blockset.ReadTiles();
         for (int t = 0; t < tileCount; t++) {
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x += 2) {
                  data[t * 32 + y * 4 + x / 2] = (byte)((tiles[t][x, y] & 0xF) | ((tiles[t][x + 1, y] & 0xF) << 4));
               }
            }
         }
         return data;
      }

      private void LoadPalettes() {
         palettes = null;
         if (selectedMap == null) return;
         var all = AllMapsModel.Create(model);
         var layout = all[selectedMap.Group]?[selectedMap.Map]?.Layout;
         if (layout?.PrimaryBlockset == null || layout.SecondaryBlockset == null) return;
         palettes = BlockmapRun.ReadPalettes(layout.PrimaryBlockset.FullBlocksetModel, layout.SecondaryBlockset.FullBlocksetModel, model.IsFRLG() ? 7 : 6);
      }

      /// <summary>
      /// Tiles don't store which palette they use: the blocks do. Count which palette each tile is drawn with, and use the most common one.
      /// </summary>
      private void GuessTilePalettes(TilesetPane pane, BlocksetModel blockset) {
         var tilePalette = new int[Math.Max(1, pane.TileCount)];
         var counts = new Dictionary<int, int[]>();
         var tileBase = pane.IsSecondary ? constants.PrimaryTiles : 0;
         try {
            var blocks = blockset.ReadBlocks(-1);
            foreach (var block in blocks) {
               for (int i = 0; i < 8; i++) {
                  var value = block[i * 2] | (block[i * 2 + 1] << 8);
                  var tile = (value & 0x3FF) - tileBase;
                  var palette = value >> 12;
                  if (tile < 0 || tile >= tilePalette.Length) continue;
                  if (!counts.TryGetValue(tile, out var tally)) counts[tile] = tally = new int[16];
                  tally[palette]++;
               }
            }
         } catch (Exception) {
            // no block information: every tile gets the default palette
         }
         var defaultPalette = pane.IsSecondary ? (model.IsFRLG() ? 7 : 6) : 0;
         for (int t = 0; t < tilePalette.Length; t++) {
            tilePalette[t] = defaultPalette;
            if (!counts.TryGetValue(t, out var tally)) continue;
            int best = 0;
            for (int p = 1; p < 16; p++) if (tally[p] > tally[best]) best = p;
            if (tally[best] > 0) tilePalette[t] = best;
         }
         pane.TilePalette = tilePalette;
      }

      public IReadOnlyList<short> PaletteFor(TilesetPane pane, int tile) {
         var index = pane?.TilePalette != null && tile >= 0 && tile < pane.TilePalette.Length ? pane.TilePalette[tile] : 0;
         if (palettes != null && index < palettes.Length && palettes[index] != null) return palettes[index];
         return Enumerable.Range(0, 16).Select(i => (short)(i * 0x0421 * 2)).ToList(); // grayscale fallback
      }

      private IPixelViewModel RenderTileset(TilesetPane pane) {
         int rows = (pane.TileCount + TilesPerRow - 1) / TilesPerRow;
         var canvas = new CanvasPixelViewModel(TilesPerRow * 8, Math.Max(8, rows * 8)) { SpriteScale = spriteScale };
         var pixels = new short[canvas.PixelWidth * canvas.PixelHeight];
         for (int t = 0; t < pane.TileCount; t++) {
            var palette = PaletteFor(pane, t);
            int tx = (t % TilesPerRow) * 8, ty = (t / TilesPerRow) * 8;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) pixels[(ty + y) * canvas.PixelWidth + tx + x] = palette[pane.Tiles[t][x, y] & 0xF];
         }
         canvas.Fill(pixels);
         return canvas;
      }

      /// <summary>
      /// Render 'count' tiles from raw 4bpp data as one row (used for the selection preview and each animation frame).
      /// </summary>
      public IPixelViewModel RenderTileRow(TilesetPane pane, int firstTile, int count, byte[] data, int dataTileOffset = -1, double scale = 1) {
         count = Math.Max(1, count);
         var pixels = new short[count * 8 * 8];
         if (data != null) {
            var palette = PaletteFor(pane, firstTile);
            for (int t = 0; t < count; t++) {
               int sourceTile = dataTileOffset < 0 ? firstTile + t : dataTileOffset + t;
               if (sourceTile < 0 || (sourceTile + 1) * 32 > data.Length) break;
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x++) {
                     var b = data[sourceTile * 32 + y * 4 + x / 2];
                     var index = x % 2 == 0 ? b & 0xF : b >> 4;
                     pixels[y * count * 8 + t * 8 + x] = palette[index];
                  }
               }
            }
         }
         return new ReadonlyPixelViewModel(count * 8, 8, pixels) { SpriteScale = scale };
      }

      public IPixelViewModel RenderFrame(TilesetPane pane, TilesetAnimationEntry entry, int frame) {
         var data = animations.ReadFrame(entry, frame);
         return RenderTileRow(pane, entry.FirstTile, entry.TileCount, data, 0);
      }

      public byte[] ReadRawFrame(TilesetAnimationEntry entry, int frame) => animations.ReadFrame(entry, frame);

      #endregion

      #region New animation

      private int firstTile;
      /// <summary>The first tile of the new animation (click a tileset image to pick it).</summary>
      public int FirstTile { get => firstTile; set { Set(ref firstTile, value.LimitToRange(0, Math.Max(0, activePane.TileCount - 1))); NotifySelectionChanged(); } }

      private void NotifySelectionChanged() {
         NotifyPropertyChanged(nameof(SelectionPreview));
         NotifyPropertyChanged(nameof(SelectionText));
         foreach (var pane in Panes) {
            pane.SetSelection(pane == activePane ? firstTile : -1, tileCount, spriteScale);
         }
      }

      private int tileCount = 4;
      public int TileCount { get => tileCount; set { Set(ref tileCount, value.LimitToRange(1, 64)); NotifySelectionChanged(); } }

      private int frameCount = 4;
      public int FrameCount { get => frameCount; set => Set(ref frameCount, value.LimitToRange(1, 64)); }

      private int speed = 4;
      /// <summary>The frame changes every 2^Speed game frames (4 = every 16 frames, about 4 times a second).</summary>
      public int Speed { get => speed; set { Set(ref speed, value.LimitToRange(0, TilesetAnimations.MaxTimer)); NotifyPropertyChanged(nameof(SpeedText)); } }
      public string SpeedText => DescribeSpeed(speed);

      public static string DescribeSpeed(int timer) {
         var frames = 1 << timer;
         return $"every {frames} game frame{(frames == 1 ? "" : "s")} ({60.0 / frames:0.#} changes per second)";
      }

      public string SelectionText => $"{activePane.Title}: tiles {firstTile} to {Math.Max(firstTile, Math.Min(activePane.TileCount - 1, firstTile + tileCount - 1))}";
      public IPixelViewModel SelectionPreview => RenderTileRow(activePane, firstTile, tileCount, activePane.RawTiles, scale: Math.Max(2, spriteScale));

      /// <summary>Called by the view when the user clicks a tile in a tileset image (pixel coordinates, unscaled).</summary>
      public void PickTile(TilesetPane pane, int x, int y) {
         if (pane?.Tiles == null || x < 0 || y < 0 || x / 8 >= TilesPerRow) return;
         var tile = (y / 8) * TilesPerRow + (x / 8);
         if (tile < 0 || tile >= pane.TileCount) return;
         ActivePane = pane;
         FirstTile = tile;
         // clicking an animated tile shows its animation
         var covering = Entries.FirstOrDefault(item => item.Pane == pane && tile >= item.Entry.FirstTile && tile < item.Entry.FirstTile + item.Entry.TileCount);
         if (covering != null && covering != selectedEntry) SelectedEntry = covering;
      }

      /// <summary>How many tiles the game has room for in the tileset's half of video memory (512 for Emerald's primary tileset and 512 for the secondary one).</summary>
      private int VideoTileLimit(TilesetPane pane) {
         var primaryTiles = constants?.PrimaryTiles ?? (model.IsFRLG() ? 640 : 512);
         return pane.IsSecondary ? Math.Max(1, 1024 - primaryTiles) : primaryTiles;
      }

      private StubCommand addAnimation;
      public ICommand AddAnimation => StubCommand(ref addAnimation, ExecuteAddAnimation, () => activePane.Tiles != null && constants != null);

      private void ExecuteAddAnimation() {
         var pane = activePane;
         var blockset = pane.Blockset;
         if (blockset == null || constants == null) return;
         try {
            // an animation can't run into the other tileset's tiles (it may run past the last tile of its own tileset: that is free video memory)
            var count = Math.Min(tileCount, VideoTileLimit(pane) - firstTile);
            if (count < 1) { OnError?.Invoke(this, "Pick a tile inside the tileset first."); return; }
            var table = animations.EnsureTable(blockset.Start, pane.IsSecondary, constants, out _);
            var added = animations.AddEntry(table, firstTile, count, frameCount, speed, pane.IsSecondary, constants, pane.RawTiles);
            // frame 1 is also stored as the tileset's own picture of these tiles (a tileset that is shorter than the animation reaches grows to hold them),
            // so the tiles exist in the tileset picture and in the map editor's tile picker, where blocks can use them
            animations.ShowFirstFrameInTileset(viewPort.CurrentChange, blockset.Start, added);
            viewPort.ChangeHistory.ChangeCompleted();
            viewPort.Refresh();
            Reload();
            SelectedEntry = Entries.LastOrDefault(item => item.Pane == pane && !item.IsBuiltIn);
            OnMessage?.Invoke(this, $"Added an animation for tiles {firstTile}-{firstTile + count - 1} with {frameCount} frames." + (count < tileCount ? $" (Only {count} tiles fit: an animation can't run into the other tileset's tiles.)" : string.Empty) + " Every frame starts as a copy of the tiles: edit or import the frames to make it move. Frame 1 is also the picture of these tiles in the tileset, so you can pick them in the map editor to build blocks.");
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not add the animation: " + e.Message);
         }
      }

      #endregion

      #region Existing animations

      /// <summary>Every animation on both tilesets: the game's own first, then the ones added with this editor.</summary>
      public ObservableCollection<TilesetAnimationItem> Entries { get; } = new();

      private int tickCount;
      /// <summary>Called by the view about 60 times a second (once per game frame): advance every live preview the way the game would.</summary>
      public void Tick() {
         tickCount++;
         foreach (var entry in Entries) {
            if (entry.Tick(tickCount)) entry.Pane.PaintLive(this, entry);
         }
         foreach (var pane in Panes) pane.FlushLive();
         foreach (var door in Doors) door.Tick();
      }

      private TilesetAnimationItem selectedEntry;
      public TilesetAnimationItem SelectedEntry {
         get => selectedEntry;
         set {
            if (selectedEntry == value) return;
            if (selectedEntry != null) selectedEntry.Selected = false;
            selectedEntry = value;
            if (selectedEntry != null) selectedEntry.Selected = true;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasSelectedEntry));
            removeEntry?.RaiseCanExecuteChanged();
            updateTileset?.RaiseCanExecuteChanged();
            importFrames?.RaiseCanExecuteChanged();
            exportFrames?.RaiseCanExecuteChanged();
            editFrames?.RaiseCanExecuteChanged();
            RefreshHighlights();
         }
      }
      public bool HasSelectedEntry => selectedEntry != null;

      private StubCommand removeEntry, importFrames, exportFrames, editFrames, gotoTable, refresh, updateTileset;
      /// <summary>Open the selected animation in the image editor (all its frames are in that one tab).</summary>
      public ICommand EditFrames => StubCommand(ref editFrames, () => EditFrame(selectedEntry, 0), () => selectedEntry != null);
      public ICommand RemoveEntry => StubCommand(ref removeEntry, ExecuteRemoveEntry, () => selectedEntry != null && !selectedEntry.IsBuiltIn);
      /// <summary>Store frame 1 of the selected animation as the tileset's own picture of its tiles (what the map editor's tile picker and block editor show).</summary>
      public ICommand UpdateTileset => StubCommand(ref updateTileset, ExecuteUpdateTileset, () => selectedEntry != null && !selectedEntry.IsBuiltIn);

      private void ExecuteUpdateTileset() {
         if (selectedEntry == null || selectedEntry.IsBuiltIn) return;
         var item = selectedEntry;
         var blockset = item.Pane.Blockset;
         if (blockset == null) return;
         try {
            var result = animations.ShowFirstFrameInTileset(viewPort.CurrentChange, blockset.Start, item.Entry);
            viewPort.ChangeHistory.ChangeCompleted();
            viewPort.Refresh();
            var (pane, index) = (item.Pane, item.Entry.Index);
            Reload();
            SelectedEntry = Entries.FirstOrDefault(other => other.Pane == pane && !other.IsBuiltIn && other.Entry.Index == index);
            if (result.Failed) OnError?.Invoke(this, "Could not read or write this tileset's tiles.");
            else if (result.ChangedTiles == 0 && result.SkippedTiles == 0) OnMessage?.Invoke(this, "The tileset already shows frame 1 of this animation.");
            else OnMessage?.Invoke(this, $"Frame 1 is now the tileset's picture of tiles {item.Entry.FirstTile}-{item.Entry.FirstTile + item.Entry.TileCount - 1} ({result.ChangedTiles} tile{(result.ChangedTiles == 1 ? "" : "s")} changed)." + (result.SkippedTiles > 0 ? $" {result.SkippedTiles} tile{(result.SkippedTiles == 1 ? " doesn't" : "s don't")} fit in this tileset's graphics." : string.Empty));
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not update the tileset: " + e.Message);
         }
      }

      /// <summary>How the tileset's picture of an animation's tiles compares with the animation's first frame.</summary>
      public FirstFrameState CompareFirstFrame(TilesetPane pane, TilesetAnimationEntry entry) =>
         entry.IsBuiltIn || pane?.RawTiles == null ? FirstFrameState.InSync : animations.CompareFirstFrame(pane.RawTiles, entry);
      public ICommand ImportFrames => StubCommand(ref importFrames, ExecuteImportFrames, () => selectedEntry != null);
      public ICommand ExportFrames => StubCommand(ref exportFrames, ExecuteExportFrames, () => selectedEntry != null);
      public ICommand GotoTable => StubCommand(ref gotoTable, ExecuteGotoTable, () => true);
      public ICommand Refresh => StubCommand(ref refresh, Reload, () => true);

      private void ExecuteRemoveEntry() {
         if (selectedEntry == null || selectedEntry.IsBuiltIn) return;
         var blockset = selectedEntry.Pane.Blockset;
         if (blockset == null) return;
         if (!animations.TryGetTable(blockset.Start, out var table, out _)) return;
         var removedPane = selectedEntry.Pane;
         CloseFrameEditors(key => key.StartsWith($"anim:{removedPane.IsSecondary}:False:")); // the animations behind the removed one move up
         animations.RemoveEntry(table, selectedEntry.Entry.Index);
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         Reload();
         // the tileset keeps its picture of the tiles (frame 1 as it was): blocks the map editor built from them keep looking right, and the tiles can be drawn on as part of the tileset
         OnMessage?.Invoke(this, "Removed the animation. The tiles keep frame 1 as their picture in the tileset, so blocks that use them still look the same (they just stop moving).");
      }

      public void SetEntrySpeed(TilesetAnimationItem item, int timer) {
         if (item == null || item.IsBuiltIn) return;
         var blockset = item.Pane.Blockset;
         if (blockset == null || !animations.TryGetTable(blockset.Start, out var table, out _)) return;
         animations.SetTimer(table, item.Entry.Index, timer);
         viewPort.ChangeHistory.ChangeCompleted();
         item.Refresh(animations.ReadEntries(table, item.Pane.IsSecondary, constants)[item.Entry.Index], this);
      }

      public void SetEntryFrameCount(TilesetAnimationItem item, int count) {
         if (item == null || item.IsBuiltIn) return;
         var blockset = item.Pane.Blockset;
         if (blockset == null || !animations.TryGetTable(blockset.Start, out var table, out _)) return;
         animations.SetFrameCount(table, item.Entry.Index, count);
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         var pane = item.Pane;
         var index = item.Entry.Index;
         Reload();
         SelectedEntry = Entries.FirstOrDefault(entry => entry.Pane == pane && !entry.IsBuiltIn && entry.Entry.Index == index);
      }

      #region Image editor tabs

      // One image editor tab per animation (and per door): its dots choose the frame, the palette has a row of its own.
      private readonly Dictionary<string, (ImageEditorViewModel editor, IImageFrameSource source)> frameEditors = new();

      private static string AnimationKey(TilesetAnimationItem item) => $"anim:{item.Pane.IsSecondary}:{item.IsBuiltIn}:{item.Entry.Index}";
      private static string DoorKey(int doorIndex) => $"door:{doorIndex}";

      private TilesetAnimationEntry ResolveEntry(int tilesetStart, bool secondary, bool builtIn, int index) {
         if (constants == null) return null;
         if (builtIn) return animations.ReadBuiltInEntries(tilesetStart, secondary, constants).FirstOrDefault(entry => entry.Index == index);
         if (!animations.TryGetTable(tilesetStart, out var table, out _)) return null;
         var entries = animations.ReadEntries(table, secondary, constants);
         return index >= 0 && index < entries.Count ? entries[index] : null;
      }

      /// <summary>The palette (0-15) most of the given tiles are drawn with, which is where the image editor starts.</summary>
      private static int MostCommonPalette(IEnumerable<int> palettes, int fallback) {
         var counts = new int[16];
         foreach (var palette in palettes) counts[palette & 15]++;
         int best = -1;
         for (int p = 0; p < 16; p++) if (counts[p] > 0 && (best < 0 || counts[p] > counts[best])) best = p;
         return best < 0 ? fallback : best;
      }

      /// <summary>
      /// The image editor for an animation, showing the given frame: the one already open for this animation if there is one (it is brought up on that frame),
      /// otherwise a new one. Returns null if the frames can't be edited as pictures.
      /// </summary>
      public ImageEditorViewModel GetFrameEditor(TilesetAnimationItem item, int frame) {
         if (item == null || frame < 0 || frame >= item.Entry.FrameCount) return null;
         var blockset = item.Pane.Blockset;
         if (blockset == null) return null;
         var key = AnimationKey(item);
         if (TryReuseFrameEditor(key, frame, out var existing)) return existing;

         var (tilesetStart, secondary, builtIn, index) = (blockset.Start, item.Pane.IsSecondary, item.IsBuiltIn, item.Entry.Index);
         var title = builtIn ? $"Anim: {item.Entry.Name}" : $"Anim #{index + 1} ({(secondary ? "secondary" : "primary")})";
         var source = new AnimationFrameSource(model, animations, tilesetStart, item.Entry, title, () => ResolveEntry(tilesetStart, secondary, builtIn, index));
         if (!source.Prepare()) return null;
         var tilePalette = item.Pane.TilePalette;
         var tiles = tilePalette == null ? Enumerable.Empty<int>() : Enumerable.Range(item.Entry.FirstTile, item.Entry.TileCount).Where(tile => tile >= 0 && tile < tilePalette.Length).Select(tile => tilePalette[tile]);
         var page = MostCommonPalette(tiles, secondary ? (model.IsFRLG() ? 7 : 6) : 0);
         return CreateFrameEditor(key, source, model.ReadPointer(source.SpritePointer(frame)), page, frame);
      }

      /// <summary>Open the animation's frames in the image editor tab (with the palette these tiles use), where each frame can be drawn on or imported over.</summary>
      public void EditFrame(TilesetAnimationItem item, int frame) {
         if (item == null || frame < 0 || frame >= item.Entry.FrameCount) return;
         var editor = GetFrameEditor(item, frame);
         if (editor != null && ShowEditor(editor)) return;
         // the frames can't be shown as a picture: show their data in the main tab instead
         var frameAddress = animations.FrameAddress(item.Entry, frame);
         if (frameAddress < 0) return;
         viewPort.Goto.Execute(frameAddress);
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      private bool ShowEditor(ImageEditorViewModel editor) {
         var args = new TabChangeRequestedEventArgs(editor);
         RequestTabChange?.Invoke(this, args);
         return args.RequestAccepted;
      }

      private bool TryReuseFrameEditor(string key, int frame, out ImageEditorViewModel editor) {
         editor = null;
         if (!frameEditors.TryGetValue(key, out var existing)) return false;
         existing.editor.Frame = frame; // an editor whose animation is gone closes itself here
         if (!frameEditors.TryGetValue(key, out existing)) return false;
         editor = existing.editor;
         return true;
      }

      private ImageEditorViewModel CreateFrameEditor(string key, IImageFrameSource source, int address, int palettePage, int frame) {
         // registering the frames as sprites is not something undo should be able to take back from under the editor
         viewPort.ChangeHistory.ChangeCompleted();
         ImageEditorViewModel editor;
         try {
            editor = viewPort.CreateImageEditor(address, 0, palettePage);
         } catch (ImageEditorViewModelCreationException e) {
            OnError?.Invoke(this, e.Message);
            return null;
         }
         editor.SetFrameSource(source, frame);
         frameEditors[key] = (editor, source);
         editor.Closed += (sender, e) => { if (frameEditors.TryGetValue(key, out var current) && current.editor == editor) frameEditors.Remove(key); };
         return editor;
      }

      /// <summary>Close the editors whose animation or door is about to move or disappear (the lists shift when one is removed).</summary>
      private void CloseFrameEditors(Func<string, bool> which) {
         foreach (var key in frameEditors.Keys.Where(which).ToList()) {
            if (frameEditors.TryGetValue(key, out var info)) info.editor.Close.Execute(null);
            frameEditors.Remove(key);
         }
      }

      /// <summary>Frames can have been added since an editor opened: make sure every frame of every open editor is a registered sprite.</summary>
      private void PrepareFrameEditors() {
         if (frameEditors.Count == 0) return;
         foreach (var info in frameEditors.Values.ToList()) {
            if (info.source is AnimationFrameSource animation) animation.Prepare();
            else if (info.source is DoorFrameSource door) door.Prepare();
         }
         viewPort.ChangeHistory.ChangeCompleted();
      }

      #endregion

      private void ExecuteGotoTable() {
         int address = -1;
         var pane = selectedEntry?.Pane ?? activePane;
         if (selectedEntry != null && selectedEntry.IsBuiltIn) {
            address = selectedEntry.Entry.EntryAddress;
         } else if (pane.Blockset != null && animations.TryGetTable(pane.Blockset.Start, out var table, out _)) {
            address = table.Start;
         }
         if (address < 0) {
            OnMessage?.Invoke(this, $"The {pane.Title.ToLower()} has no animation table of its own yet: add an animation first.");
            return;
         }
         viewPort.Goto.Execute(address);
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      /// <summary>
      /// Import frames from an image: each frame is (tiles*8) pixels wide and 8 pixels tall; frames are stacked top to bottom (or side by side if the image is 8 pixels tall).
      /// Colors are matched to the palette the tiles use in this tileset.
      /// </summary>
      private void ExecuteImportFrames() {
         if (selectedEntry == null) return;
         var item = selectedEntry;
         var pane = item.Pane;
         var entry = item.Entry;
         (short[] image, int width) loaded;
         try {
            loaded = fileSystem.LoadImage();
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not load the image: " + e.Message);
            return;
         }
         if (loaded.image == null || loaded.width <= 0) return;
         int height = loaded.image.Length / loaded.width;
         int frameWidth = entry.TileCount * 8;
         int frames;
         bool horizontal;
         if (loaded.width == frameWidth && height % 8 == 0) { frames = height / 8; horizontal = false; } else if (height == 8 && loaded.width % frameWidth == 0) { frames = loaded.width / frameWidth; horizontal = true; } else {
            OnError?.Invoke(this, $"Expected an image {frameWidth} pixels wide with the frames stacked (8 pixels per frame), or 8 pixels tall with the frames side by side. This image is {loaded.width}x{height}.");
            return;
         }
         if (frames < 1 || frames > 64) { OnError?.Invoke(this, "Frame count must be between 1 and 64."); return; }
         var blockset = pane.Blockset;
         if (blockset == null) return;
         if (entry.IsBuiltIn) {
            // the game's code decides how many frames it plays
            if (frames != entry.FrameCount) { OnError?.Invoke(this, $"{entry.Name} always plays {entry.FrameCount} frames, but this image has {frames}."); return; }
         } else {
            if (!animations.TryGetTable(blockset.Start, out var table, out _)) return;
            if (frames != entry.FrameCount) animations.SetFrameCount(table, entry.Index, frames);
            entry = animations.ReadEntries(table, pane.IsSecondary, constants)[entry.Index];
         }
         var palette = PaletteFor(pane, entry.FirstTile);
         for (int f = 0; f < frames; f++) {
            var data = new byte[32 * entry.TileCount];
            for (int t = 0; t < entry.TileCount; t++) {
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x++) {
                     int px = horizontal ? f * frameWidth + t * 8 + x : t * 8 + x;
                     int py = horizontal ? y : f * 8 + y;
                     var color = loaded.image[py * loaded.width + px];
                     int index = NearestColor(palette, color);
                     int offset = t * 32 + y * 4 + x / 2;
                     if (x % 2 == 0) data[offset] = (byte)((data[offset] & 0xF0) | index); else data[offset] = (byte)((data[offset] & 0x0F) | (index << 4));
                  }
               }
            }
            animations.WriteFrame(entry, f, data);
         }
         // frame 1 is the tileset's picture of these tiles too
         if (!entry.IsBuiltIn) animations.ShowFirstFrameInTileset(viewPort.CurrentChange, blockset.Start, entry);
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         var builtIn = entry.IsBuiltIn;
         var selected = entry.Index;
         Reload();
         SelectedEntry = Entries.FirstOrDefault(other => other.Pane == pane && other.IsBuiltIn == builtIn && other.Entry.Index == selected);
         OnMessage?.Invoke(this, $"Imported {frames} frame{(frames == 1 ? "" : "s")}.");
      }

      private void ExecuteExportFrames() {
         if (selectedEntry == null) return;
         var entry = selectedEntry.Entry;
         int frameWidth = entry.TileCount * 8;
         var image = new short[frameWidth * 8 * entry.FrameCount];
         var palette = PaletteFor(selectedEntry.Pane, entry.FirstTile);
         for (int f = 0; f < entry.FrameCount; f++) {
            var data = animations.ReadFrame(entry, f);
            if (data == null) continue;
            for (int t = 0; t < entry.TileCount; t++) {
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x++) {
                     var b = data[t * 32 + y * 4 + x / 2];
                     var index = x % 2 == 0 ? b & 0xF : b >> 4;
                     image[(f * 8 + y) * frameWidth + t * 8 + x] = palette[index];
                  }
               }
            }
         }
         try {
            fileSystem.SaveImage(image, frameWidth);
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not save the image: " + e.Message);
         }
      }

      private static int NearestColor(IReadOnlyList<short> palette, short color) {
         int best = 0, bestDistance = int.MaxValue;
         int r = color & 31, g = (color >> 5) & 31, b = (color >> 10) & 31;
         for (int i = 0; i < palette.Count; i++) {
            int pr = palette[i] & 31, pg = (palette[i] >> 5) & 31, pb = (palette[i] >> 10) & 31;
            int distance = (r - pr) * (r - pr) + (g - pg) * (g - pg) + (b - pb) * (b - pb);
            if (distance < bestDistance) { bestDistance = distance; best = i; }
         }
         return best;
      }

      /// <summary>Outline every animated range on the tileset images, and the selected animation more strongly.</summary>
      private void RefreshHighlights() {
         foreach (var pane in Panes) pane.SetHighlights(Entries.Where(item => item.Pane == pane).ToList(), selectedEntry, spriteScale);
      }

      #endregion

      #region Doors

      public bool HasDoors => DoorAnimations.IsSupported(model);
      public ObservableCollection<DoorItem> Doors { get; } = new();
      public ObservableCollection<string> DoorSizes { get; } = new() { "1x1 (one block)", "1x2 (two blocks tall)", "2x2 left half", "2x2 right half" };
      public ObservableCollection<string> DoorSounds { get; } = new() { "normal", "sliding", "arena" };

      private DoorItem selectedDoor;
      public DoorItem SelectedDoor {
         get => selectedDoor;
         set {
            if (selectedDoor == value) return;
            if (selectedDoor != null) selectedDoor.Selected = false;
            selectedDoor = value;
            if (selectedDoor != null) selectedDoor.Selected = true;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasSelectedDoor));
            removeDoor?.RaiseCanExecuteChanged();
            importDoorFrames?.RaiseCanExecuteChanged();
            exportDoorFrames?.RaiseCanExecuteChanged();
            editDoorFrames?.RaiseCanExecuteChanged();
         }
      }
      public bool HasSelectedDoor => selectedDoor != null;

      private int newDoorMetatile;
      /// <summary>The block (metatile) number the new door sits on. For a 1x2 door, the bottom block.</summary>
      public int NewDoorMetatile { get => newDoorMetatile; set { Set(ref newDoorMetatile, value.LimitToRange(0, 0x3FF)); NotifyPropertyChanged(nameof(NewDoorPreview)); } }
      private int newDoorSize = 1;
      public int NewDoorSize { get => newDoorSize; set => Set(ref newDoorSize, value.LimitToRange(0, 3)); }
      private int newDoorSound;
      public int NewDoorSound { get => newDoorSound; set => Set(ref newDoorSound, value.LimitToRange(0, 2)); }
      public IPixelViewModel NewDoorPreview => RenderBlock(newDoorMetatile);

      private StubCommand addDoor, removeDoor, importDoorFrames, exportDoorFrames, editDoorFrames, gotoDoorTable;
      /// <summary>Open the selected door in the image editor (its 3 frames are in that one tab).</summary>
      public ICommand EditDoorFrames => StubCommand(ref editDoorFrames, () => EditDoorFrame(selectedDoor, 0), () => selectedDoor != null);
      public ICommand AddDoor => StubCommand(ref addDoor, ExecuteAddDoor, () => HasDoors && activePane.Tiles != null);
      public ICommand RemoveDoor => StubCommand(ref removeDoor, ExecuteRemoveDoor, () => selectedDoor != null);
      public ICommand ImportDoorFrames => StubCommand(ref importDoorFrames, ExecuteImportDoorFrames, () => selectedDoor != null);
      public ICommand ExportDoorFrames => StubCommand(ref exportDoorFrames, ExecuteExportDoorFrames, () => selectedDoor != null);
      public ICommand GotoDoorTable => StubCommand(ref gotoDoorTable, () => { var table = doors.Table; if (table == null) return; viewPort.Goto.Execute(table.Start); RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort)); }, () => HasDoors);

      private byte[][] allBlocks;          // every block of the map (primary + secondary), numbered the way the game numbers them
      private byte[][] allAttributes;      // the attributes (behavior, layer type) of those blocks
      private int[][,] allTiles;           // every tile of the map (primary + secondary)

      private void LoadDoors(TilesetPane pane) {
         if (!HasDoors || pane.Blockset == null) return;
         foreach (var entry in doors.ReadEntries()) {
            // the game plays a door when its tileset is either one of the map's two tilesets: so a door belongs to the tileset it names, and no other
            if (entry.TilesetAddress != pane.Blockset.Start) continue;
            Doors.Add(new DoorItem(entry, this, pane) { SpriteScale = spriteScale });
         }
      }

      private void EnsureBlocks() {
         if (allBlocks != null || selectedMap == null) return;
         try {
            var all = AllMapsModel.Create(model);
            var layout = all[selectedMap.Group]?[selectedMap.Map]?.Layout;
            if (layout?.PrimaryBlockset == null || layout.SecondaryBlockset == null) return;
            var primary = layout.PrimaryBlockset.FullBlocksetModel;
            var secondary = layout.SecondaryBlockset.FullBlocksetModel;
            allTiles = BlockmapRun.ReadTiles(primary, secondary, constants?.PrimaryTiles ?? 512);
            // the doors may sit on blocks past where the blockset data seems to end: make sure those blocks are read too
            int maxPrimary = -1, maxSecondary = -1;
            foreach (var entry in doors.ReadEntries()) {
               if (entry.TilesetAddress == primary.Start) maxPrimary = Math.Max(maxPrimary, Math.Min(entry.Metatile, primary.PrimaryBlocks - 1));
               if (entry.TilesetAddress == secondary.Start && entry.Metatile >= primary.PrimaryBlocks) maxSecondary = Math.Max(maxSecondary, entry.Metatile - primary.PrimaryBlocks);
            }
            maxSecondary = Math.Min(maxSecondary, 1023 - primary.PrimaryBlocks);
            // block numbers 512 and up (640 in FRLG) are the secondary tileset's own blocks 0, 1, 2...
            try {
               allBlocks = BlockmapRun.ReadBlocks(maxPrimary, maxSecondary, primary, secondary);
            } catch (Exception) {
               allBlocks = BlockmapRun.ReadAllBlocks(primary, secondary); // a door pointing past the end of the ROM must not hide the others
            }
            try {
               allAttributes = BlockmapRun.ReadBlockAttributes(maxPrimary, maxSecondary, primary, secondary);
            } catch (Exception) {
               try { allAttributes = BlockmapRun.ReadAllBlockAttributes(primary, secondary); } catch (Exception) { allAttributes = null; }
            }
         } catch (Exception) {
            allBlocks = null;
            allAttributes = null;
            allTiles = null;
         }
      }

      /// <summary>The metatile behavior of a block of the current map, or -1 if it can't be read.</summary>
      public int BlockBehavior(int metatile) {
         EnsureBlocks();
         if (allAttributes == null || metatile < 0 || metatile >= allAttributes.Length) return -1;
         var attribute = allAttributes[metatile];
         if (attribute == null || attribute.Length == 0) return -1;
         // Emerald: one byte of behavior, then layer bits. FireRed/LeafGreen: nine bits of behavior in a 4-byte attribute.
         return model.IsFRLG() && attribute.Length >= 2 ? (attribute[0] | ((attribute[1] & 1) << 8)) : attribute[0];
      }

      /// <summary>Why the game would never play this door on the current map (null if nothing seems wrong).</summary>
      public string DescribeDoorProblem(DoorEntry entry) {
         EnsureBlocks();
         if (allBlocks == null) return null;
         if (entry.Metatile >= allBlocks.Length) return $"Block {entry.Metatile} doesn't exist in this map's tilesets (they have {allBlocks.Length} blocks).";
         var behavior = BlockBehavior(entry.Metatile);
         var isDoor = DoorAnimations.IsDoorBehavior(model, behavior);
         if (isDoor == false) return $"Block {entry.Metatile} has the behavior {DoorAnimations.BehaviorName(model, behavior)}, so the game won't play this door on it. Give the block the ANIMATED_DOOR behavior in the map editor.";
         return null;
      }

      /// <summary>Render one 16x16 block of the current map (for door previews).</summary>
      public IPixelViewModel RenderBlock(int metatile) {
         EnsureBlocks();
         if (allBlocks == null || allTiles == null || palettes == null || metatile < 0 || metatile >= allBlocks.Length) return new ReadonlyPixelViewModel(16, 16);
         try {
            var paletteList = palettes.Select(p => (IReadOnlyList<short>)(p ?? new short[16])).ToArray();
            return BlocksetModel.RenderBlock(metatile, allBlocks, null, allTiles, paletteList);
         } catch (Exception) {
            return new ReadonlyPixelViewModel(16, 16);
         }
      }

      /// <summary>Render one frame of a door using the tileset palettes the door's palette table names.</summary>
      public IPixelViewModel RenderDoorFrame(DoorEntry entry, int frame) {
         var data = doors.ReadTiles(entry);
         var paletteIndices = doors.ReadPaletteIndices(entry);
         int width = entry.WidthTiles * 8, height = entry.HeightTiles * 8;
         var pixels = new short[width * height];
         if (data == null) return new ReadonlyPixelViewModel(width, height, pixels);
         var tilesetPalettes = ReadTilesetPalettes(entry.TilesetAddress);
         for (int t = 0; t < entry.TilesPerFrame; t++) {
            var palette = tilesetPalettes[paletteIndices[t] & 15];
            var (tileX, tileY) = entry.TilePosition(t); // the tiles of a 2x2 door are stored block by block, not row by row
            int tx = tileX * 8, ty = tileY * 8;
            int offset = (frame * entry.TilesPerFrame + t) * 32;
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  var b = data[offset + y * 4 + x / 2];
                  var index = x % 2 == 0 ? b & 0xF : b >> 4;
                  pixels[(ty + y) * width + tx + x] = palette[index];
               }
            }
         }
         return new ReadonlyPixelViewModel(width, height, pixels);
      }

      private short[][] ReadTilesetPalettes(int tilesetAddress) {
         var result = new short[16][];
         var start = tilesetAddress >= 0 && tilesetAddress + 12 <= model.Count ? model.ReadPointer(tilesetAddress + 8) : -1;
         for (int p = 0; p < 16; p++) {
            result[p] = new short[16];
            if (start < 0 || start + 512 > model.Count) continue;
            for (int c = 0; c < 16; c++) result[p][c] = PaletteRun.FlipColorChannels((short)model.ReadMultiByteValue(start + p * 32 + c * 2, 2));
            // the secondary tileset's palette table only holds its own palettes (6..12): fall back to the map's merged palettes for the rest
            if (palettes != null && p < palettes.Length && palettes[p] != null && result[p].All(color => color == 0)) result[p] = palettes[p];
         }
         return result;
      }

      private void ExecuteAddDoor() {
         var pane = activePane;
         var blockset = pane.Blockset;
         if (blockset == null) return;
         try {
            // first frame: whatever the block looks like now (its bottom layer), so the door starts out looking right
            var tilePalette = pane.TilePalette;
            var palette = tilePalette != null && tilePalette.Length > 0 ? tilePalette[Math.Min(tilePalette.Length - 1, firstTile)] : (pane.IsSecondary ? 6 : 0);
            var entry = doors.AddDoor(blockset.Start, newDoorMetatile, newDoorSize, newDoorSound, palette, null);
            viewPort.ChangeHistory.ChangeCompleted();
            viewPort.Refresh();
            Reload();
            SelectedDoor = Doors.FirstOrDefault(door => door.Entry.Index == entry.Index);
            OnMessage?.Invoke(this, $"Added a {entry.SizeName} door on block {entry.Metatile}. Its frames are blank: import a picture ({entry.WidthTiles * 8}x{entry.HeightTiles * 8 * DoorEntry.FrameCount}, the 3 opening stages stacked) or click a frame to draw it.");
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not add the door: " + e.Message);
         }
      }

      private void ExecuteRemoveDoor() {
         if (selectedDoor == null) return;
         var removedIndex = selectedDoor.Entry.Index;
         CloseFrameEditors(key => key.StartsWith("door:") && int.Parse(key.Substring("door:".Length)) >= removedIndex); // the doors behind the removed one move up
         doors.RemoveDoor(selectedDoor.Entry.Index);
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         Reload();
      }

      /// <summary>The image editor for a door, showing the given frame: the one already open for this door if there is one, otherwise a new one.</summary>
      public ImageEditorViewModel GetDoorEditor(DoorItem item, int frame) {
         if (item == null || frame < 0 || frame >= DoorEntry.FrameCount) return null;
         var entry = item.Entry;
         if (entry.TilesAddress < 0 || entry.TilesAddress + entry.TilesLength > model.Count) return null;
         var key = DoorKey(entry.Index);
         if (TryReuseFrameEditor(key, frame, out var existing)) return existing;

         var source = new DoorFrameSource(doors, entry, $"Door: block {entry.Metatile}");
         if (!source.Prepare()) return null;
         var page = MostCommonPalette(doors.ReadPaletteIndices(entry), item.Pane.IsSecondary ? (model.IsFRLG() ? 7 : 6) : 0);
         return CreateFrameEditor(key, source, entry.TilesAddress, page, frame);
      }

      /// <summary>Open the door's frames in the image editor tab, using the palette the door's tiles use.</summary>
      public void EditDoorFrame(DoorItem item, int frame) {
         if (item == null) return;
         var entry = item.Entry;
         if (entry.TilesAddress < 0 || entry.TilesAddress + entry.TilesLength > model.Count) return;
         var editor = GetDoorEditor(item, frame);
         if (editor != null && ShowEditor(editor)) return;
         viewPort.Goto.Execute(entry.TilesAddress);
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      public void SetDoorSound(DoorItem item, int sound) {
         if (item == null) return;
         doors.SetSound(item.Entry, sound);
         viewPort.ChangeHistory.ChangeCompleted();
         item.Refresh(doors.ReadEntries()[item.Entry.Index], this);
      }

      public void SetDoorMetatile(DoorItem item, int metatile) {
         if (item == null) return;
         doors.SetMetatile(item.Entry, metatile);
         viewPort.ChangeHistory.ChangeCompleted();
         item.Refresh(doors.ReadEntries()[item.Entry.Index], this);
      }

      /// <summary>Import all 3 frames from one picture: (width x 8) by (height x 8 x 3) pixels, frames stacked top to bottom.</summary>
      private void ExecuteImportDoorFrames() {
         if (selectedDoor == null) return;
         var entry = selectedDoor.Entry;
         (short[] image, int width) loaded;
         try { loaded = fileSystem.LoadImage(); } catch (Exception e) { OnError?.Invoke(this, "Could not load the image: " + e.Message); return; }
         if (loaded.image == null || loaded.width <= 0) return;
         int height = loaded.image.Length / loaded.width;
         int expectedWidth = entry.WidthTiles * 8, expectedHeight = entry.HeightTiles * 8 * DoorEntry.FrameCount;
         if (loaded.width != expectedWidth || height != expectedHeight) {
            OnError?.Invoke(this, $"A {entry.SizeName} door's picture must be {expectedWidth}x{expectedHeight} pixels (the 3 frames stacked top to bottom). This image is {loaded.width}x{height}.");
            return;
         }
         var paletteIndices = doors.ReadPaletteIndices(entry);
         var tilesetPalettes = ReadTilesetPalettes(entry.TilesetAddress);
         var data = new byte[entry.TilesLength];
         for (int f = 0; f < DoorEntry.FrameCount; f++) {
            for (int t = 0; t < entry.TilesPerFrame; t++) {
               var palette = tilesetPalettes[paletteIndices[t] & 15];
               var (tileX, tileY) = entry.TilePosition(t);
               int tx = tileX * 8, ty = f * entry.HeightTiles * 8 + tileY * 8;
               int offset = (f * entry.TilesPerFrame + t) * 32;
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x++) {
                     int colorIndex = NearestColor(palette, loaded.image[(ty + y) * loaded.width + tx + x]);
                     int o = offset + y * 4 + x / 2;
                     if (x % 2 == 0) data[o] = (byte)((data[o] & 0xF0) | colorIndex); else data[o] = (byte)((data[o] & 0x0F) | (colorIndex << 4));
                  }
               }
            }
         }
         doors.WriteTiles(entry, data);
         viewPort.ChangeHistory.ChangeCompleted();
         viewPort.Refresh();
         var index = entry.Index;
         Reload();
         SelectedDoor = Doors.FirstOrDefault(door => door.Entry.Index == index);
         OnMessage?.Invoke(this, "Imported the door's frames.");
      }

      private void ExecuteExportDoorFrames() {
         if (selectedDoor == null) return;
         var entry = selectedDoor.Entry;
         int width = entry.WidthTiles * 8, frameHeight = entry.HeightTiles * 8;
         var image = new short[width * frameHeight * DoorEntry.FrameCount];
         for (int f = 0; f < DoorEntry.FrameCount; f++) {
            var frame = RenderDoorFrame(entry, f);
            Array.Copy(frame.PixelData, 0, image, f * width * frameHeight, width * frameHeight);
         }
         try { fileSystem.SaveImage(image, width); } catch (Exception e) { OnError?.Invoke(this, "Could not save the image: " + e.Message); }
      }

      #endregion

      /// <summary>
      /// Animations added before the tileset showed their first frame (or whose first frame was drawn in the main tab's sprite view) leave the tileset's tiles blank,
      /// so there is nothing to pick in the map editor's block editor. Where the tileset has nothing at all under an animation, store frame 1 there (undoable).
      /// A tileset that has some other picture there is never overwritten on its own: the animation's 'Update tileset' button does that when you ask for it.
      /// </summary>
      private int HealBlankTilesetTiles() {
         int healed = 0;
         foreach (var pane in Panes) {
            var blockset = GetBlockset(pane.IsSecondary);
            if (blockset == null || blockset.Start < 0 || blockset.Start + 24 > model.Count) continue;
            healed += animations.ShowFirstFramesInBlankTiles(viewPort.CurrentChange, blockset.Start, pane.IsSecondary, constants);
         }
         if (healed > 0) {
            viewPort.ChangeHistory.ChangeCompleted();
            viewPort.Refresh();
         }
         return healed;
      }

      public void Reload() {
         // remember what was selected, so a refresh (or an edit that reloads) keeps it
         var keepEntry = selectedEntry == null ? null : new { selectedEntry.Pane, selectedEntry.IsBuiltIn, selectedEntry.Entry.Index };
         var keepDoor = selectedDoor == null ? null : new { selectedDoor.Pane, selectedDoor.Entry.Index };
         selectedEntry = null;
         selectedDoor = null;
         Entries.Clear();
         Doors.Clear();
         allBlocks = null;
         allTiles = null;
         foreach (var pane in Panes) pane.Clear();
         NotifyPropertyChanged(nameof(SelectedEntry));
         NotifyPropertyChanged(nameof(HasSelectedEntry));
         NotifyPropertyChanged(nameof(SelectedDoor));
         NotifyPropertyChanged(nameof(HasSelectedDoor));
         if (constants == null) {
            Status = "This ROM's metadata doesn't say where the tileset animation engine keeps its data (tilesetanim.* constants), so animations can't be installed.";
            return;
         }
         if (selectedMap == null) {
            Status = "Pick a map to edit its tileset animations.";
            return;
         }
         var problems = new List<string>();
         var healed = 0;
         try {
            healed = HealBlankTilesetTiles();
         } catch (Exception e) {
            problems.Add("tileset tiles: " + e.Message);
         }
         try {
            LoadPalettes();
         } catch (Exception e) {
            problems.Add("palettes: " + e.Message);
         }
         foreach (var pane in Panes) {
            try {
               LoadPane(pane);
               if (pane.Blockset == null) continue;
               foreach (var entry in animations.ReadBuiltInEntries(pane.Blockset.Start, pane.IsSecondary, constants)) Entries.Add(new TilesetAnimationItem(entry, this, pane) { SpriteScale = spriteScale });
               if (animations.TryGetTable(pane.Blockset.Start, out var table, out _)) {
                  foreach (var entry in animations.ReadEntries(table, pane.IsSecondary, constants)) Entries.Add(new TilesetAnimationItem(entry, this, pane) { SpriteScale = spriteScale });
               }
               LoadDoors(pane);
            } catch (Exception e) {
               problems.Add($"{pane.Title}: {e.Message}");
            }
         }
         NotifyPropertyChanged(nameof(ActivePane));
         NotifyPropertyChanged(nameof(NewAnimationTitle));
         FirstTile = Math.Min(firstTile, Math.Max(0, activePane.TileCount - 1));
         NotifySelectionChanged();
         NotifyPropertyChanged(nameof(TileCountInTileset));
         NotifyPropertyChanged(nameof(NewDoorPreview));
         if (keepEntry != null) SelectedEntry = Entries.FirstOrDefault(item => item.Pane == keepEntry.Pane && item.IsBuiltIn == keepEntry.IsBuiltIn && item.Entry.Index == keepEntry.Index);
         else RefreshHighlights();
         if (keepDoor != null) SelectedDoor = Doors.FirstOrDefault(door => door.Pane == keepDoor.Pane && door.Entry.Index == keepDoor.Index);
         var builtIn = Entries.Count(item => item.IsBuiltIn);
         var custom = Entries.Count - builtIn;
         Status = problems.Count > 0
            ? "Could not read everything: " + string.Join("; ", problems)
            : $"{builtIn} animation{(builtIn == 1 ? "" : "s")} built into the game, {custom} added with HexManiac, and {Doors.Count} door{(Doors.Count == 1 ? "" : "s")} on these tilesets."
              + (animations.HasBuiltInTable ? string.Empty : " (This ROM doesn't list the game's own animations.)")
              + (healed > 0 ? $" Frame 1 of {healed} animation{(healed == 1 ? "" : "s")} was copied into blank tileset tiles, so the tiles show up in the map editor." : string.Empty);
         if (healed > 0) OnMessage?.Invoke(this, healed == 1
            ? "The tileset's tiles under one of your animations were still blank, so its frame 1 was copied into them: the animated tiles now show up in the tileset and in the map editor's tile picker."
            : $"The tileset's tiles under {healed} of your animations were still blank, so their frame 1 was copied into them: the animated tiles now show up in the tileset and in the map editor's tile picker.");
         addAnimation?.RaiseCanExecuteChanged();
         addDoor?.RaiseCanExecuteChanged();
         removeEntry?.RaiseCanExecuteChanged();
         updateTileset?.RaiseCanExecuteChanged();
         importFrames?.RaiseCanExecuteChanged();
         exportFrames?.RaiseCanExecuteChanged();
         editFrames?.RaiseCanExecuteChanged();
         editDoorFrames?.RaiseCanExecuteChanged();
         PrepareFrameEditors();
      }
   }

   /// <summary>One of the two tilesets (primary or secondary) of the chosen map: its picture, and the outlines drawn over it.</summary>
   public class TilesetPane : ViewModelCore {
      private readonly TilesetAnimationTab tab;
      private const int TilesPerRow = 16;

      public bool IsSecondary { get; }
      public string Title => IsSecondary ? "Secondary tileset" : "Primary tileset";

      internal MiniBlocksetModel Blockset { get; set; }
      internal int[][,] Tiles { get; set; }
      internal byte[] RawTiles { get; set; }
      internal int[] TilePalette { get; set; }
      public int TileCount { get; internal set; }

      private IPixelViewModel image = new ReadonlyPixelViewModel(128, 256);
      public IPixelViewModel Image { get => image; private set { image = value; NotifyPropertyChanged(); } }

      private string description = string.Empty;
      public string Description { get => description; internal set => Set(ref description, value); }

      private bool isActive;
      /// <summary>True for the tileset the 'new animation' box currently works on.</summary>
      public bool IsActive { get => isActive; internal set => Set(ref isActive, value); }

      private double spriteScale = 2;
      public double SpriteScale {
         get => spriteScale;
         set {
            spriteScale = value;
            if (image is CanvasPixelViewModel canvas) canvas.SpriteScale = value;
         }
      }

      /// <summary>An outline around each animated range (the selected animation stands out).</summary>
      public ObservableCollection<TileHighlight> Highlights { get; } = new();
      /// <summary>The tiles the 'new animation' box is set to.</summary>
      public ObservableCollection<TileHighlight> SelectionRects { get; } = new();

      public TilesetPane(TilesetAnimationTab tab, bool isSecondary) => (this.tab, IsSecondary) = (tab, isSecondary);

      internal void Clear() {
         Blockset = null;
         Tiles = null;
         RawTiles = null;
         TilePalette = null;
         TileCount = 0;
         live = null;
         liveDirty = false;
         Image = new ReadonlyPixelViewModel(128, 256);
         Description = string.Empty;
         Highlights.Clear();
         SelectionRects.Clear();
      }

      private CanvasPixelViewModel live;
      private bool liveDirty;

      internal void SetImage(IPixelViewModel picture, double scale) {
         spriteScale = scale;
         live = picture as CanvasPixelViewModel;
         Image = picture;
      }

      /// <summary>Draw the animation's current frame into the tileset picture, the way the game does in video memory.</summary>
      internal void PaintLive(TilesetAnimationTab owner, TilesetAnimationItem item) {
         if (live == null) return;
         var data = item.LiveData;
         if (data == null) return;
         var entry = item.Entry;
         var pixels = live.PixelData;
         for (int t = 0; t < entry.TileCount; t++) {
            var tile = entry.FirstTile + t;
            if (tile < 0 || tile >= TileCount || (t + 1) * 32 > data.Length) continue;
            var palette = owner.PaletteFor(this, tile);
            int tx = (tile % TilesPerRow) * 8, ty = (tile / TilesPerRow) * 8;
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  var b = data[t * 32 + y * 4 + x / 2];
                  var index = x % 2 == 0 ? b & 0xF : b >> 4;
                  pixels[(ty + y) * live.PixelWidth + tx + x] = palette[index];
               }
            }
         }
         liveDirty = true;
      }

      internal void FlushLive() {
         if (!liveDirty || live == null) return;
         liveDirty = false;
         live.Fill(live.PixelData);
      }

      /// <summary>Rectangles (one per tileset row) covering 'count' consecutive tiles.</summary>
      internal static IEnumerable<(double x, double y, double width, double height)> Segments(int first, int count, int tileCount, double scale) {
         int tile = first, remaining = count;
         while (remaining > 0 && tile < tileCount) {
            if (tile < 0) { remaining += tile; tile = 0; continue; }
            int column = tile % TilesPerRow;
            int inRow = Math.Min(Math.Min(remaining, TilesPerRow - column), tileCount - tile);
            yield return (column * 8 * scale, tile / TilesPerRow * 8 * scale, inRow * 8 * scale, 8 * scale);
            tile += inRow;
            remaining -= inRow;
         }
      }

      internal void SetHighlights(IReadOnlyList<TilesetAnimationItem> items, TilesetAnimationItem selected, double scale) {
         Highlights.Clear();
         foreach (var item in items) {
            if (item == selected) continue;
            foreach (var (x, y, width, height) in Segments(item.Entry.FirstTile, item.Entry.TileCount, TileCount, scale)) Highlights.Add(new TileHighlight(x, y, width, height, false));
         }
         if (selected != null && items.Contains(selected)) {
            foreach (var (x, y, width, height) in Segments(selected.Entry.FirstTile, selected.Entry.TileCount, TileCount, scale)) Highlights.Add(new TileHighlight(x, y, width, height, true));
         }
      }

      internal void SetSelection(int first, int count, double scale) {
         SelectionRects.Clear();
         if (first < 0) return;
         foreach (var (x, y, width, height) in Segments(first, count, TileCount, scale)) SelectionRects.Add(new TileHighlight(x, y, width, height, true));
      }
   }

   /// <summary>A rectangle drawn over a tileset picture (scaled pixels).</summary>
   public record TileHighlight(double X, double Y, double Width, double Height, bool Selected);

   public record MapOption(int Group, int Map, string Name) {
      public string Label => $"{Group}-{Map} {Name}";
      public override string ToString() => Label;
   }

   public class TilesetAnimationItem : ViewModelCore {
      private readonly TilesetAnimationTab tab;
      public TilesetPane Pane { get; }
      public TilesetAnimationEntry Entry { get; private set; }
      public ObservableCollection<TilesetAnimationFrame> Frames { get; } = new();
      private List<byte[]> rawFrames = new();

      public bool IsBuiltIn => Entry.IsBuiltIn;
      /// <summary>The speed and the number of frames of the game's own animations are decided by its code.</summary>
      public bool IsEditable => !Entry.IsBuiltIn;

      public string Header => $"{Pane.Title} - {(IsBuiltIn ? "built into the game" : "added with HexManiac")}";
      public string Label => IsBuiltIn
         ? $"{Entry.Name}: tiles {Entry.FirstTile}-{Entry.FirstTile + Entry.TileCount - 1}, {Entry.FrameCount} frame{(Entry.FrameCount == 1 ? "" : "s")}"
         : $"#{Entry.Index + 1}: tiles {Entry.FirstTile}-{Entry.FirstTile + Entry.TileCount - 1}, {Entry.FrameCount} frame{(Entry.FrameCount == 1 ? "" : "s")}";
      public string Details => $"Changes {TilesetAnimationTab.DescribeSpeed(Entry.Timer)}. Frames at {Entry.FramesAddress:X6}." + (IsBuiltIn ? " Speed and frame count are set by the game's code." : string.Empty) + TilesetNote;

      private FirstFrameState tilesetState = FirstFrameState.InSync;
      /// <summary>True when the tileset's own picture of these tiles is not frame 1 (so the map editor's tile picker shows something else than the animation).</summary>
      public bool TilesetDiffers => tilesetState == FirstFrameState.Different || tilesetState == FirstFrameState.TilesetBlank;
      private string TilesetNote => !TilesetDiffers ? string.Empty
         : tilesetState == FirstFrameState.TilesetBlank ? " The tileset's tiles are still blank, so there is nothing to pick in the map editor: press Update tileset."
         : " The tileset shows a different picture of these tiles than frame 1 (the game replaces it with the animation): press Update tileset to use frame 1.";

      private IPixelViewModel baseTiles;
      /// <summary>What these tiles look like in the tileset itself (frame 0 of the animation replaces them in game).</summary>
      public IPixelViewModel BaseTiles { get => baseTiles; private set { baseTiles = value; NotifyPropertyChanged(); } }

      public int Speed { get => Entry.Timer; set { if (IsEditable && value != Entry.Timer) tab.SetEntrySpeed(this, value); } }
      public string SpeedText => TilesetAnimationTab.DescribeSpeed(Entry.Timer);
      public int FrameCount { get => Entry.FrameCount; set { if (IsEditable && value != Entry.FrameCount) tab.SetEntryFrameCount(this, value); } }

      private double spriteScale = 2;
      public double SpriteScale {
         get => spriteScale;
         set {
            Set(ref spriteScale, value);
            foreach (var frame in Frames) frame.SpriteScale = value;
            if (baseTiles is TilesetAnimationFrame wrapped) wrapped.SpriteScale = value;
            NotifyPropertyChanged(nameof(LiveFrame));
         }
      }

      private bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

      private int liveIndex;
      /// <summary>The frame the game would be showing right now (the view advances this with Tick).</summary>
      public IPixelViewModel LiveFrame => Frames.Count == 0 ? BaseTiles : Frames[Math.Min(liveIndex, Frames.Count - 1)];
      /// <summary>The raw 4bpp tiles of the frame being shown.</summary>
      public byte[] LiveData => liveIndex < rawFrames.Count ? rawFrames[liveIndex] : null;

      /// <summary>Returns true when the animation moved on to its next frame.</summary>
      public bool Tick(int tick) {
         if (Frames.Count < 2) return false;
         if ((tick & ((1 << Entry.Timer) - 1)) != 0) return false;
         liveIndex = (liveIndex + 1) % Frames.Count;
         NotifyPropertyChanged(nameof(LiveFrame));
         return true;
      }

      public TilesetAnimationItem(TilesetAnimationEntry entry, TilesetAnimationTab tab, TilesetPane pane) {
         this.tab = tab;
         Pane = pane;
         Refresh(entry, tab);
      }

      public void Refresh(TilesetAnimationEntry entry, TilesetAnimationTab tab) {
         Entry = entry;
         Frames.Clear();
         rawFrames = new List<byte[]>();
         for (int f = 0; f < entry.FrameCount; f++) {
            Frames.Add(new TilesetAnimationFrame(this, f, tab.RenderFrame(Pane, entry, f)) { SpriteScale = spriteScale });
            rawFrames.Add(tab.ReadRawFrame(entry, f));
         }
         BaseTiles = new TilesetAnimationFrame(this, -1, tab.RenderTileRow(Pane, entry.FirstTile, entry.TileCount, Pane.RawTiles)) { SpriteScale = spriteScale };
         tilesetState = tab.CompareFirstFrame(Pane, entry);
         NotifyPropertyChanged(nameof(TilesetDiffers));
         liveIndex = Frames.Count == 0 ? 0 : entry.Phase % Frames.Count;
         NotifyPropertyChanged(nameof(LiveFrame));
         NotifyPropertyChanged(nameof(Label));
         NotifyPropertyChanged(nameof(Details));
         NotifyPropertyChanged(nameof(Speed));
         NotifyPropertyChanged(nameof(SpeedText));
         NotifyPropertyChanged(nameof(FrameCount));
      }

      public void EditFrame(TilesetAnimationFrame frame) => tab.EditFrame(this, frame.Index);
   }

   public class DoorItem : ViewModelCore {
      private readonly TilesetAnimationTab tab;
      public TilesetPane Pane { get; }
      public DoorEntry Entry { get; private set; }
      public ObservableCollection<TilesetAnimationFrame> Frames { get; } = new();
      public string Label => $"{Pane.Title}: door on block {Entry.Metatile} ({Entry.SizeName}, {Entry.SoundName} sound)";
      public string Details => $"Frames at {Entry.TilesAddress:X6}, palettes at {Entry.PalettesAddress:X6}.";

      private IPixelViewModel block;
      public IPixelViewModel Block { get => block; private set { block = value; NotifyPropertyChanged(); } }

      /// <summary>Set when something keeps the game from ever playing this door on the map being shown (for example the block isn't an animated door block).</summary>
      public string Warning { get; private set; } = string.Empty;
      public bool HasWarning => !string.IsNullOrEmpty(Warning);

      public int Sound { get => Entry.Sound; set { if (value != Entry.Sound) tab.SetDoorSound(this, value); } }
      public int Metatile { get => Entry.Metatile; set { if (value != Entry.Metatile) tab.SetDoorMetatile(this, value); } }

      private double spriteScale = 2;
      public double SpriteScale { get => spriteScale; set { Set(ref spriteScale, value); foreach (var frame in Frames) frame.SpriteScale = value; NotifyPropertyChanged(nameof(LiveFrame)); } }

      private bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

      // closed, open (4 game frames per step, like the game), stay open while the player walks through, close again
      private static readonly (int frame, int ticks)[] Timeline = { (0, 45), (1, 4), (2, 4), (2, 40), (1, 4), (0, 4) };
      private int timelineStep, stepTicks;
      /// <summary>The frame the game would be showing right now as the door opens and closes (the view advances this with Tick).</summary>
      public IPixelViewModel LiveFrame => Frames.Count == 0 ? Block : Frames[Math.Min(Timeline[timelineStep].frame, Frames.Count - 1)];
      public void Tick() {
         if (Frames.Count == 0) return;
         stepTicks++;
         if (stepTicks < Timeline[timelineStep].ticks) return;
         stepTicks = 0;
         timelineStep = (timelineStep + 1) % Timeline.Length;
         NotifyPropertyChanged(nameof(LiveFrame));
      }

      public DoorItem(DoorEntry entry, TilesetAnimationTab tab, TilesetPane pane) {
         this.tab = tab;
         Pane = pane;
         Refresh(entry, tab);
      }

      public void Refresh(DoorEntry entry, TilesetAnimationTab tab) {
         Entry = entry;
         Frames.Clear();
         for (int f = 0; f < DoorEntry.FrameCount; f++) Frames.Add(new TilesetAnimationFrame(null, f, tab.RenderDoorFrame(entry, f)) { SpriteScale = spriteScale, Door = this });
         Block = tab.RenderBlock(entry.Metatile);
         Warning = tab.DescribeDoorProblem(entry) ?? string.Empty;
         NotifyPropertyChanged(nameof(Warning));
         NotifyPropertyChanged(nameof(HasWarning));
         timelineStep = 0; stepTicks = 0;
         NotifyPropertyChanged(nameof(LiveFrame));
         NotifyPropertyChanged(nameof(Label));
         NotifyPropertyChanged(nameof(Details));
         NotifyPropertyChanged(nameof(Sound));
         NotifyPropertyChanged(nameof(Metatile));
      }

      public void EditFrame(TilesetAnimationFrame frame) => tab.EditDoorFrame(this, frame.Index);
   }

   public class TilesetAnimationFrame : ViewModelCore, IPixelViewModel {
      private readonly IPixelViewModel image;
      public TilesetAnimationItem Owner { get; }
      public DoorItem Door { get; init; }
      public int Index { get; }
      public string Label => $"Frame {Index + 1}";
      public short Transparent => image.Transparent;
      public int PixelWidth => image.PixelWidth;
      public int PixelHeight => image.PixelHeight;
      public short[] PixelData => image.PixelData;
      private double spriteScale = 2;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }
      public TilesetAnimationFrame(TilesetAnimationItem owner, int index, IPixelViewModel image) => (Owner, Index, this.image) = (owner, index, image);
   }
}
