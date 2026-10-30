using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// The RAM/ROM locations the tileset animation engine uses. They come from the ROM's metadata ([[UnmappedConstant]] entries),
   /// because they differ between games and between decomp builds.
   /// </summary>
   public record TilesetAnimationConstants(int Buffer, int BufferSize, int PrimaryCounter, int PrimaryMax, int PrimaryCallback, int SecondaryCounter, int SecondaryMax, int SecondaryCallback, int PrimaryTiles) {
      public const string Prefix = "tilesetanim.";

      public static bool TryRead(IDataModel model, out TilesetAnimationConstants constants) {
         constants = null;
         int Get(string name) => model.TryGetUnmappedConstant(Prefix + name, out var value) ? value : -1;
         var buffer = Get("buffer");
         var bufferSize = Get("buffersize");
         var pCounter = Get("primary.counter");
         var pMax = Get("primary.max");
         var pCallback = Get("primary.callback");
         var sCounter = Get("secondary.counter");
         var sMax = Get("secondary.max");
         var sCallback = Get("secondary.callback");
         var primaryTiles = Get("primarytiles");
         if (primaryTiles < 0) primaryTiles = model.IsFRLG() ? 640 : 512;
         if (new[] { buffer, bufferSize, pCounter, pMax, pCallback, sCounter, sMax, sCallback }.Any(value => value < 0)) return false;
         constants = new(buffer, bufferSize, pCounter, pMax, pCallback, sCounter, sMax, sCallback, primaryTiles);
         return true;
      }
   }

   /// <summary>
   /// One animated group of tiles: 'tiles' consecutive tiles starting at 'tileOffset' are replaced every (1 shl timer) frames with the next frame's graphics.
   /// </summary>
   public class TilesetAnimationEntry {
      public int Index { get; init; }
      public int FirstTile { get; init; }      // relative to the tileset (0..511 for a primary tileset)
      public int TileCount { get; init; }
      public int FrameCount { get; init; }
      public int Timer { get; init; }          // speed: the frame changes every 2^Timer game frames
      public int FramesAddress { get; init; }  // the `mat` run (table of frame pointers)
      public int EntryAddress { get; init; }
      public int FrameDelay => 1 << Timer;
      /// <summary>True for the game's own (code driven) animations: water, flowers... Their speed and frame count are fixed by the game code, but their frames can be edited.</summary>
      public bool IsBuiltIn { get; init; }
      public string Name { get; init; }
      /// <summary>How many frames this animation is ahead by (the second steam plume of Lavaridge runs 2 frames ahead of the first).</summary>
      public int Phase { get; init; }
   }

   /// <summary>How the tileset's own picture of the tiles an animation covers compares with the animation's first frame.</summary>
   public enum FirstFrameState {
      /// <summary>The frame or the tileset graphics can't be read.</summary>
      Unknown,
      /// <summary>The tileset shows the first frame: the map editor's tile picker and the game (before the first tick) show the same thing.</summary>
      InSync,
      /// <summary>Every one of the tileset's tiles in that range is blank, so the animation can't be seen in the tileset (nothing to pick in the block editor).</summary>
      TilesetBlank,
      /// <summary>The tileset has a different picture there.</summary>
      Different,
   }

   /// <summary>What <see cref="TilesetAnimations.ShowFirstFrameInTileset"/> did.</summary>
   /// <param name="ChangedTiles">How many tiles of the tileset now have a different picture.</param>
   /// <param name="SkippedTiles">How many tiles could not be written (past what the tileset can hold, or past the end of a tileset that is not compressed).</param>
   /// <param name="Failed">True if the tileset's graphics could not be read or written at all.</param>
   public record TilesetSyncResult(int ChangedTiles, int SkippedTiles, bool Failed);

   /// <summary>
   /// Installs and edits table-driven tileset animations in a pokeemerald/pokeemerald-expansion ROM.
   /// The table format is the same one HexManiacAdvance uses for FireRed: [animations{mat} frames: timer. tiles. tileOffset::]!FEFEFEFE,
   /// and a small thumb routine (written into free space) plays it. The tileset's own animation (flowers, water...) keeps running:
   /// the generated code calls the original animation first.
   /// </summary>
   public class TilesetAnimations {
      public const string AnchorPrefix = "graphics.maps.tilesets.animations.";
      public const string TableFormat = "[animations<`mat`> frames: timer. tiles. tileOffset::]!FEFEFEFE";
      public const int EntryLength = 12;
      public const int MaxTimer = 7;

      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly ThumbParser parser;

      public TilesetAnimations(IDataModel model, Func<ModelDelta> tokenFactory, ThumbParser parser) {
         this.model = model;
         this.tokenFactory = tokenFactory;
         this.parser = parser;
      }

      public static bool IsSupported(IDataModel model) => TilesetAnimationConstants.TryRead(model, out _);

      /// <summary>
      /// The offset of the 'animation' (callback) pointer within a tileset header: isCompressed. isSecondary. (2 bytes) tiles palette blocks attributes animation
      /// </summary>
      public static int CallbackOffset(IDataModel model) => model.IsFRLG() ? 16 : 20;

      public int ReadCallback(int tilesetStart) => model.ReadPointer(tilesetStart + CallbackOffset(model));

      /// <summary>
      /// If this tileset already uses a HexManiac animation table, return it.
      /// </summary>
      public bool TryGetTable(int tilesetStart, out ITableRun table, out string baseName) {
         table = null;
         baseName = null;
         var callback = ReadCallback(tilesetStart);
         if (callback < 0 || callback >= model.Count) return false;
         var anchor = model.GetAnchorFromAddress(-1, callback);
         if (string.IsNullOrEmpty(anchor) || !anchor.StartsWith(AnchorPrefix) || !anchor.EndsWith(".init")) return false;
         baseName = anchor.Substring(0, anchor.Length - ".init".Length);
         var tableAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, baseName + ".table");
         if (tableAddress < 0) return false;
         table = model.GetNextRun(tableAddress) as ITableRun;
         return table != null && table.Start == tableAddress;
      }

      /// <summary>
      /// Describes what animates this tileset right now.
      /// </summary>
      public string DescribeCallback(int tilesetStart) {
         var callback = ReadCallback(tilesetStart);
         if (callback < 0 || callback >= model.Count) return "This tileset has no animation.";
         if (TryGetTable(tilesetStart, out var table, out _)) return $"{table.ElementCount} custom animation{(table.ElementCount == 1 ? "" : "s")} (HexManiac table).";
         var anchor = model.GetAnchorFromAddress(-1, callback);
         return string.IsNullOrEmpty(anchor) ? $"The game's own animation code at {callback:X6}. It keeps running after you add animations." : $"The game's own animation code ({anchor}). It keeps running after you add animations.";
      }

      public IReadOnlyList<TilesetAnimationEntry> ReadEntries(ITableRun table, bool isSecondary, TilesetAnimationConstants constants) {
         var results = new List<TilesetAnimationEntry>();
         if (table == null) return results;
         int tileBase = isSecondary ? constants.PrimaryTiles : 0;
         for (int i = 0; i < table.ElementCount; i++) {
            var entry = table.Start + table.ElementLength * i;
            results.Add(new TilesetAnimationEntry {
               Index = i,
               EntryAddress = entry,
               FramesAddress = model.ReadPointer(entry),
               FrameCount = model.ReadMultiByteValue(entry + 4, 2),
               Timer = model[entry + 6],
               TileCount = model[entry + 7],
               FirstTile = model.ReadMultiByteValue(entry + 8, 4) - tileBase,
            });
         }
         return results;
      }

      /// <summary>
      /// The table that describes the game's own tileset animations (written into the ROM by the build, see BuiltInTableName):
      /// tileset<> frames<> frameCount: firstTile: tileCount. timerShift. phase. padding. name""32
      /// </summary>
      public const string BuiltInTableName = "data.maps.tilesets.builtinanimations";
      private const int BuiltInNameOffset = 16, BuiltInNameLength = 32;

      public bool HasBuiltInTable => model.GetTable(BuiltInTableName) != null;

      /// <summary>
      /// The animations the game's own code plays for the tileset that starts at 'tilesetStart'. Empty when the ROM has no such table.
      /// </summary>
      public IReadOnlyList<TilesetAnimationEntry> ReadBuiltInEntries(int tilesetStart, bool isSecondary, TilesetAnimationConstants constants) {
         var results = new List<TilesetAnimationEntry>();
         var table = model.GetTable(BuiltInTableName);
         if (table == null || table.ElementLength < BuiltInNameOffset + 1) return results;
         int tileBase = isSecondary ? constants.PrimaryTiles : 0;
         for (int i = 0; i < table.ElementCount; i++) {
            var entry = table.Start + table.ElementLength * i;
            if (model.ReadPointer(entry) != tilesetStart) continue;
            var frames = model.ReadPointer(entry + 4);
            var frameCount = model.ReadMultiByteValue(entry + 8, 2);
            var tileCount = model[entry + 12];
            if (frames < 0 || frameCount < 1 || tileCount < 1) continue;
            results.Add(new TilesetAnimationEntry {
               IsBuiltIn = true,
               Index = i,
               EntryAddress = entry,
               FramesAddress = frames,
               FrameCount = frameCount,
               FirstTile = model.ReadMultiByteValue(entry + 10, 2) - tileBase,
               TileCount = tileCount,
               Timer = Math.Min(MaxTimer, (int)model[entry + 13]),
               Phase = model[entry + 14],
               Name = ReadName(entry + BuiltInNameOffset),
            });
         }
         return results;
      }

      private string ReadName(int start) {
         int length = 0;
         while (length < BuiltInNameLength && start + length < model.Count && model[start + length] != 0xFF) length++;
         if (length == 0) return string.Empty;
         return PCSString.Convert(model.RawData, start, length).Trim('"');
      }

      /// <summary>
      /// Read the raw 4bpp tile data (32 bytes per tile) of a frame. Returns null if the frame pointer is invalid.
      /// </summary>
      public byte[] ReadFrame(TilesetAnimationEntry entry, int frame) {
         if (entry.FramesAddress < 0 || entry.FramesAddress + 4 * entry.FrameCount > model.Count || frame >= entry.FrameCount) return null;
         var frameAddress = model.ReadPointer(entry.FramesAddress + 4 * frame);
         if (frameAddress < 0 || frameAddress + 32 * entry.TileCount > model.Count) return null;
         var data = new byte[32 * entry.TileCount];
         Array.Copy(model.RawData, frameAddress, data, 0, data.Length);
         return data;
      }

      /// <summary>Where the pixels of a frame are, or -1 if the frame table or the pointer is bad.</summary>
      public int FrameAddress(TilesetAnimationEntry entry, int frame) {
         if (entry == null || entry.FramesAddress < 0 || frame < 0 || frame >= entry.FrameCount || entry.FramesAddress + 4 * entry.FrameCount > model.Count) return -1;
         var frameAddress = model.ReadPointer(entry.FramesAddress + 4 * frame);
         return frameAddress < 0 || frameAddress + 32 * entry.TileCount > model.Count ? -1 : frameAddress;
      }

      /// <summary>
      /// The tiles of a frame are stored one after the other. To draw them as a picture, they are laid out in rows 'widthTiles' wide
      /// (the last row may be short). The default is as close to a square as possible: 4 tiles are 2x2, which is how a 4-tile flower is built.
      /// </summary>
      public static (int width, int height) PictureShape(int tileCount, int widthTiles) {
         if (widthTiles < 1) widthTiles = DefaultPictureWidth(tileCount);
         widthTiles = Math.Min(widthTiles, Math.Max(1, tileCount));
         return (widthTiles, (Math.Max(1, tileCount) + widthTiles - 1) / widthTiles);
      }

      public static int DefaultPictureWidth(int tileCount) => (int)Math.Ceiling(Math.Sqrt(Math.Max(1, tileCount)));

      /// <summary>The frame as a picture of palette indices (see <see cref="PictureShape"/>); tiles past the end of the frame are blank.</summary>
      public int[,] ReadFramePixels(TilesetAnimationEntry entry, int frame, int widthTiles) {
         var (width, height) = PictureShape(entry.TileCount, widthTiles);
         var data = ReadFrame(entry, frame) ?? new byte[32 * entry.TileCount];
         return SpriteRun.GetPixels(data, 0, width, height, 4);
      }

      /// <summary>Write an edited picture (the same shape ReadFramePixels returned) back over the frame's tiles.</summary>
      public void WriteFramePixels(ModelDelta token, TilesetAnimationEntry entry, int frame, int widthTiles, int[,] pixels) {
         var address = FrameAddress(entry, frame);
         if (address < 0) return;
         var data = new byte[32 * entry.TileCount];
         SpriteRun.SetPixels(data, 0, pixels, 4); // tiles of the picture past the end of the frame are dropped
         for (int i = 0; i < data.Length; i++) {
            if (model[address + i] != data[i]) token.ChangeData(model, address + i, data[i]);
         }
      }

      public void WriteFrame(TilesetAnimationEntry entry, int frame, byte[] data) {
         if (entry.FramesAddress < 0 || frame >= entry.FrameCount) return;
         var frameAddress = model.ReadPointer(entry.FramesAddress + 4 * frame);
         if (frameAddress < 0 || frameAddress + data.Length > model.Count) return;
         var token = tokenFactory();
         for (int i = 0; i < data.Length; i++) {
            if (model[frameAddress + i] != data[i]) token.ChangeData(model, frameAddress + i, data[i]);
         }
      }

      /// <summary>
      /// Make sure the tileset runs a HexManiac animation table (creating the table and the code if needed), then return the table.
      /// </summary>
      public ITableRun EnsureTable(int tilesetStart, bool isSecondary, TilesetAnimationConstants constants, out string baseName) {
         if (TryGetTable(tilesetStart, out var existing, out baseName)) return existing;
         var token = tokenFactory();

         // the original animation, so it can keep running
         var originalInit = ReadCallback(tilesetStart);
         if (originalInit < 0 || originalInit >= model.Count) originalInit = Pointer.NULL;
         var originalCallback = originalInit == Pointer.NULL ? Pointer.NULL : FindCallbackInInit(originalInit);

         baseName = AnchorPrefix + $"t{tilesetStart:x6}";
         var tableName = baseName + ".table";
         var tableAddress = model.FindFreeSpace(model.FreeSpaceStart, 16);
         if (tableAddress < 0) { tableAddress = model.Count; model.ExpandData(token, model.Count + 0x100); }
         // an empty table: just the end token
         model.WriteMultiByteValue(tableAddress, 4, token, -1 - 0x01010101); // FEFEFEFE
         for (int i = 4; i < 12; i++) token.ChangeData(model, tableAddress + i, 0xFF);
         var error = ArrayRun.TryParse(model, TableFormat, tableAddress, SortedSpan<int>.None, out var tableRun);
         if (error.HasError) throw new InvalidOperationException(error.ErrorMessage);
         model.ObserveAnchorWritten(token, tableName, tableRun);
         EnsurePaletteAnchor(tilesetStart, baseName, token);

         var callbackAddress = InsertCallback(baseName + ".callback", tableAddress, originalCallback, constants);
         var initAddress = InsertInit(baseName + ".init", tableAddress, callbackAddress, originalInit, isSecondary, constants);

         // point the tileset at the new init routine (thumb: +1)
         var pointerAddress = tilesetStart + CallbackOffset(model);
         if (model.GetNextRun(tilesetStart) is ITableRun tilesetRun && tilesetRun.Start == tilesetStart && tilesetRun.ElementContent.Any(segment => segment.Name == "animation")) {
            new ModelArrayElement(model, tilesetRun.Start, 0, tokenFactory, tilesetRun).SetAddress("animation", initAddress + 1);
         } else {
            model.ClearFormat(token, pointerAddress, 4);
            model.WritePointer(token, pointerAddress, initAddress + 1);
            model.ObserveRunWritten(token, new PointerRun(pointerAddress));
         }
         return (ITableRun)model.GetNextRun(tableAddress);
      }

      /// <summary>
      /// Name the tileset's 16-palette table '<name>.palette' so the frames (uct4xN|<name>.palette) show in the right colours.
      /// </summary>
      public string EnsurePaletteAnchor(int tilesetStart, string baseName, ModelDelta token) {
         var paletteName = baseName + ".palette";
         if (model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, paletteName) >= 0) return paletteName;
         var paletteAddress = model.ReadPointer(tilesetStart + 8);
         if (paletteAddress < 0 || paletteAddress + 512 > model.Count) return null;
         var existing = model.GetNextRun(paletteAddress);
         if (existing is IPaletteRun paletteRun && paletteRun.Start == paletteAddress) {
            model.ObserveAnchorWritten(token, paletteName, paletteRun);
            return paletteName;
         }
         if (!PaletteRun.TryParsePaletteFormat("`ucp4:0123456789ABCDEF`", out var format)) return null;
         model.ClearFormat(token, paletteAddress, 512);
         model.ObserveAnchorWritten(token, paletteName, new PaletteRun(paletteAddress, format, SortedSpan.One(tilesetStart + 8)));
         return paletteName;
      }

      /// <summary>
      /// Make sure a frame is registered as tiles that know which palette they use, so the image editor shows them in colour.
      /// Returns the frame's address, or -1 if the frame pointer is bad.
      /// </summary>
      public int EnsureFrameFormat(int tilesetStart, TilesetAnimationEntry entry, int frame) {
         if (entry.FramesAddress < 0 || entry.FramesAddress + 4 * entry.FrameCount > model.Count || frame < 0 || frame >= entry.FrameCount) return -1;
         var frameAddress = model.ReadPointer(entry.FramesAddress + 4 * frame);
         var length = 32 * entry.TileCount;
         if (frameAddress < 0 || frameAddress + length > model.Count) return -1;
         // the tileset's palettes are named after the tileset, whether it uses a HexManiac table or only the game's own animations
         if (!TryGetTable(tilesetStart, out _, out var baseName)) baseName = AnchorPrefix + $"t{tilesetStart:x6}";
         var token = tokenFactory();
         var hint = EnsurePaletteAnchor(tilesetStart, baseName, token);
         if (hint == null) return frameAddress;
         var existing = model.GetNextRun(frameAddress);
         if (existing is ISpriteRun sprite && sprite.Start == frameAddress && sprite.SpriteFormat.PaletteHint == hint && sprite.PointerSources.Count > 0) return frameAddress;
         var sources = existing.Start == frameAddress && existing.PointerSources != null ? existing.PointerSources : SortedSpan<int>.None;
         // every slot of the frame table that points at this frame (the flower animation reuses its first frame)
         for (int f = 0; f < entry.FrameCount; f++) {
            if (model.ReadPointer(entry.FramesAddress + 4 * f) == frameAddress) sources = sources.Add1(entry.FramesAddress + 4 * f);
         }
         if (existing.Start != frameAddress) model.ClearFormat(token, frameAddress, length);
         else if (existing.Length < length) model.ClearFormat(token, frameAddress + existing.Length, length - existing.Length); // a word elsewhere that looks like a pointer into the frame leaves a run inside it
         model.ObserveRunWritten(token, new TilesetRun(new TilesetFormat(4, entry.TileCount, -1, hint), model, frameAddress, sources));
         return frameAddress;
      }

      private string PaletteHintFor(ITableRun table) {
         var anchor = model.GetAnchorFromAddress(-1, table.Start);
         if (string.IsNullOrEmpty(anchor) || !anchor.EndsWith(".table")) return null;
         var paletteName = anchor.Substring(0, anchor.Length - ".table".Length) + ".palette";
         return model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, paletteName) >= 0 ? paletteName : null;
      }

      /// <summary>
      /// Add an animation entry whose frames all start as copies of the tileset's current tiles.
      /// </summary>
      public TilesetAnimationEntry AddEntry(ITableRun table, int firstTile, int tileCount, int frameCount, int timer, bool isSecondary, TilesetAnimationConstants constants, byte[] currentTiles) {
         var token = tokenFactory();
         tileCount = tileCount.LimitToRange(1, 255);
         frameCount = frameCount.LimitToRange(1, 64);
         timer = timer.LimitToRange(0, MaxTimer);
         var paletteHint = PaletteHintFor(table);
         var index = table.ElementCount;
         table = model.RelocateForExpansion(token, table, table.Length + table.ElementLength);
         table = table.Append(token, 1);
         model.ObserveRunWritten(token, table);
         var entry = table.Start + table.ElementLength * index;

         // the frame pointer table first: its pointers are written (and registered) before anything else is allocated,
         // because free space is found by looking for FF bytes, and a frame full of color 15 is all FF.
         var frameSize = 32 * tileCount;
         var framesAddress = model.FindFreeSpace(model.FreeSpaceStart, 4 * frameCount);
         if (framesAddress < 0) { framesAddress = model.Count; model.ExpandData(token, model.Count + 4 * frameCount + 0x10); }
         for (int f = 0; f < frameCount; f++) model.WritePointer(token, framesAddress + 4 * f, 0);
         model.ObserveRunWritten(token, new NoInfoRun(framesAddress, SortedSpan.One(entry)));
         for (int f = 0; f < frameCount; f++) {
            var frameAddress = model.FindFreeSpace(model.FreeSpaceStart, frameSize);
            if (frameAddress < 0) { frameAddress = model.Count; model.ExpandData(token, model.Count + frameSize + 0x10); }
            for (int i = 0; i < frameSize; i++) {
               int source = firstTile * 32 + i;
               byte value = currentTiles != null && source < currentTiles.Length ? currentTiles[source] : (byte)0;
               token.ChangeData(model, frameAddress + i, value);
            }
            model.WritePointer(token, framesAddress + 4 * f, frameAddress);
            model.ObserveRunWritten(token, new TilesetRun(new TilesetFormat(4, tileCount, -1, paletteHint), model, frameAddress, SortedSpan.One(framesAddress + 4 * f)));
         }

         // the entry itself (write the pointer last so the `mat` run can read frames/tiles from its parent)
         model.WriteMultiByteValue(entry + 4, 2, token, frameCount);
         model.WriteMultiByteValue(entry + 6, 1, token, timer);
         model.WriteMultiByteValue(entry + 7, 1, token, tileCount);
         model.WriteMultiByteValue(entry + 8, 4, token, firstTile + (isSecondary ? constants.PrimaryTiles : 0));
         model.ClearFormat(token, framesAddress, 4 * frameCount);
         var element = new ModelArrayElement(model, table.Start, index, tokenFactory, table);
         element.SetAddress("animations", framesAddress);
         for (int f = 0; f < frameCount; f++) {
            var frameAddress = model.ReadPointer(framesAddress + 4 * f);
            if (model.GetNextRun(frameAddress) is TilesetRun) continue;
            model.ObserveRunWritten(token, new TilesetRun(new TilesetFormat(4, tileCount, -1, paletteHint), model, frameAddress, SortedSpan.One(framesAddress + 4 * f)));
         }
         return ReadEntries((ITableRun)model.GetNextRun(table.Start), isSecondary, constants)[index];
      }

      public void RemoveEntry(ITableRun table, int index) {
         var token = tokenFactory();
         if (index < 0 || index >= table.ElementCount) return;
         // clear the frame graphics and the frame table
         var entry = table.Start + table.ElementLength * index;
         var framesAddress = model.ReadPointer(entry);
         var frameCount = model.ReadMultiByteValue(entry + 4, 2);
         var tileCount = model[entry + 7];
         if (framesAddress >= 0 && framesAddress + 4 * frameCount <= model.Count) {
            for (int f = 0; f < frameCount; f++) {
               var frameAddress = model.ReadPointer(framesAddress + 4 * f);
               if (frameAddress >= 0 && frameAddress + 32 * tileCount <= model.Count) {
                  model.ClearPointer(token, framesAddress + 4 * f, frameAddress);
                  model.ClearFormatAndData(token, frameAddress, 32 * tileCount);
               }
            }
            // clear the frame table while the table that points at it still says how long it is:
            // once its pointer is cleared the run loses its length (the element count is read through that pointer),
            // and clearing it then would wipe everything that follows (up to 256KB) instead of the 4*frameCount bytes
            model.ClearFormatAndData(token, framesAddress, 4 * frameCount);
         }
         // shift later entries down
         for (int i = index + 1; i < table.ElementCount; i++) {
            var from = table.Start + table.ElementLength * i;
            var to = from - table.ElementLength;
            var destination = model.ReadPointer(from);
            model.ClearPointer(token, from, destination);
            for (int b = 4; b < table.ElementLength; b++) token.ChangeData(model, to + b, model[from + b]);
            model.WritePointer(token, to, destination);
         }
         var shorter = table.Append(token, -1);
         model.ObserveRunWritten(token, shorter);
      }

      public void SetTimer(ITableRun table, int index, int timer) {
         var entry = table.Start + table.ElementLength * index;
         model.WriteMultiByteValue(entry + 6, 1, tokenFactory(), timer.LimitToRange(0, MaxTimer));
      }

      /// <summary>
      /// Change the number of frames: new frames copy the last frame; removed frames are freed.
      /// </summary>
      public void SetFrameCount(ITableRun table, int index, int newCount) {
         newCount = newCount.LimitToRange(1, 64);
         var token = tokenFactory();
         var entry = table.Start + table.ElementLength * index;
         var oldCount = model.ReadMultiByteValue(entry + 4, 2);
         if (oldCount == newCount) return;
         var tileCount = model[entry + 7];
         var framesAddress = model.ReadPointer(entry);
         if (framesAddress < 0) return;
         var frames = (ITableRun)model.GetNextRun(framesAddress);
         if (newCount < oldCount) {
            for (int f = newCount; f < oldCount; f++) {
               var frameAddress = model.ReadPointer(framesAddress + 4 * f);
               if (frameAddress >= 0 && frameAddress + 32 * tileCount <= model.Count) {
                  model.ClearPointer(token, framesAddress + 4 * f, frameAddress);
                  model.ClearFormatAndData(token, frameAddress, 32 * tileCount);
               }
            }
            model.WriteMultiByteValue(entry + 4, 2, token, newCount);
            if (frames is IUpdateFromParentRun updatable) model.ObserveRunWritten(token, (IFormattedRun)updatable.UpdateFromParent(token, 1, entry));
         } else {
            var last = ReadFrameBytes(framesAddress, oldCount - 1, tileCount);
            model.WriteMultiByteValue(entry + 4, 2, token, newCount);
            if (frames is IUpdateFromParentRun updatable) frames = (ITableRun)updatable.UpdateFromParent(token, 1, entry);
            model.ObserveRunWritten(token, frames);
            framesAddress = frames.Start;
            for (int f = oldCount; f < newCount; f++) {
               var frameAddress = model.FindFreeSpace(model.FreeSpaceStart, 32 * tileCount);
               if (frameAddress < 0) { frameAddress = model.Count; model.ExpandData(token, model.Count + 32 * tileCount + 0x10); }
               for (int i = 0; i < 32 * tileCount; i++) token.ChangeData(model, frameAddress + i, last != null && i < last.Length ? last[i] : (byte)0);
               model.WritePointer(token, framesAddress + 4 * f, frameAddress);
               model.ObserveRunWritten(token, new TilesetRun(new TilesetFormat(4, tileCount, -1, PaletteHintFor(table)), model, frameAddress, SortedSpan.One(framesAddress + 4 * f)));
            }
         }
      }

      private byte[] ReadFrameBytes(int framesAddress, int frame, int tileCount) {
         var frameAddress = model.ReadPointer(framesAddress + 4 * frame);
         if (frameAddress < 0 || frameAddress + 32 * tileCount > model.Count) return null;
         var data = new byte[32 * tileCount];
         Array.Copy(model.RawData, frameAddress, data, 0, data.Length);
         return data;
      }

      #region The first frame in the tileset

      // The game copies each frame into video memory when the animation ticks, so the tileset graphics HexManiac shows (the Animated Tiles tab, the map editor's tile picker
      // and block editor) never contain an animated tile unless the first frame is also stored in the tileset's own picture. Frame 1 is what the game shows before the first tick, too.

      /// <summary>How many tiles fit in the tileset's half of video memory.</summary>
      private int VideoTileLimit(bool isSecondary) {
         var primaryTiles = TilesetAnimationConstants.TryRead(model, out var constants) ? constants.PrimaryTiles : (model.IsFRLG() ? 640 : 512);
         return isSecondary ? Math.Max(1, 1024 - primaryTiles) : primaryTiles;
      }

      /// <summary>
      /// The run that holds the tileset's compressed tiles. The tileset header is passed along as its pointer source, so that moving the tiles (they usually get a few bytes
      /// longer when they are compressed again) also changes the header, even when nothing registered the data as a tileset yet.
      /// </summary>
      private LzTilesetRun FindTilesetRun(int tilesetStart) {
         var start = model.ReadPointer(tilesetStart + 4);
         if (start < 0 || start >= model.Count) return null;
         if (model.GetNextRun(start) is LzTilesetRun registered && registered.Start == start) return registered;
         // reading the tiles registers them as a tileset (replacing a plain pointer target or a fixed-size picture), as every other reader of a tileset does
         new BlocksetModel(model, tilesetStart).ReadTiles();
         if (model.GetNextRun(start) is LzTilesetRun created && created.Start == start) return created;
         return new LzTilesetRun(new TilesetFormat(4, null), model, start, SortedSpan.One(tilesetStart + 4));
      }

      /// <summary>The tileset's own graphics as one block of 4bpp tiles (32 bytes each), or null if they can't be read.</summary>
      public byte[] ReadTilesetData(int tilesetStart) {
         if (tilesetStart < 0 || tilesetStart + 8 > model.Count) return null;
         var start = model.ReadPointer(tilesetStart + 4);
         if (start < 0 || start >= model.Count) return null;
         var blockset = new BlocksetModel(model, tilesetStart);
         if (blockset.IsCompressed) return FindTilesetRun(tilesetStart)?.GetData();
         var tiles = blockset.ReadTiles();
         if (tiles == null) return null;
         var data = new byte[Math.Min(tiles.Length * 32, model.Count - start)];
         Array.Copy(model.RawData, start, data, 0, data.Length);
         return data;
      }

      /// <summary>Compare the first frame of an animation with the tileset's graphics (see <see cref="ReadTilesetData"/>).</summary>
      public FirstFrameState CompareFirstFrame(byte[] tilesetData, TilesetAnimationEntry entry) {
         if (tilesetData == null || entry == null || entry.FirstTile < 0) return FirstFrameState.Unknown;
         var frame = ReadFrame(entry, 0);
         if (frame == null) return FirstFrameState.Unknown;
         var offset = entry.FirstTile * 32;
         bool same = true, blank = true;
         for (int i = 0; i < frame.Length; i++) {
            var have = offset + i < tilesetData.Length ? tilesetData[offset + i] : (byte)0;
            if (have != frame[i]) same = false;
            if (have != 0) blank = false;
         }
         if (same) return FirstFrameState.InSync;
         return blank ? FirstFrameState.TilesetBlank : FirstFrameState.Different;
      }

      /// <summary>
      /// Store the first frame of the animation in the tileset's own graphics (over the tiles the animation covers), so the tileset picture, the map editor's tile picker
      /// and block editor, and the game before its first animation tick all show it. The animation code still replaces the tiles when it ticks, exactly as before.
      /// The tiles are compressed again (the tileset moves to free space if they got longer) and everything is written to 'token', so undo takes it all back at once.
      /// A tileset with fewer tiles than the animation reaches grows by blank tiles (as far as video memory has room for them).
      /// </summary>
      public TilesetSyncResult ShowFirstFrameInTileset(ModelDelta token, int tilesetStart, TilesetAnimationEntry entry) {
         if (entry == null || entry.IsBuiltIn || entry.FirstTile < 0 || tilesetStart < 0 || tilesetStart + 8 > model.Count) return new(0, 0, true);
         var frame = ReadFrame(entry, 0);
         var start = model.ReadPointer(tilesetStart + 4);
         if (frame == null || start < 0 || start >= model.Count) return new(0, 0, true);
         var blockset = new BlocksetModel(model, tilesetStart);
         var firstByte = entry.FirstTile * 32;
         var limitBytes = VideoTileLimit(blockset.IsSecondary) * 32;
         // frame tiles that would land past video memory are not stored
         var storable = Math.Max(0, Math.Min(frame.Length, limitBytes - firstByte));
         var skipped = (frame.Length - storable + 31) / 32;

         if (!blockset.IsCompressed) {
            // a tileset that is not compressed has no room to grow: only the tiles it already has can be written
            var tileCount = blockset.ReadTiles()?.Length ?? 0;
            var writable = Math.Max(0, Math.Min(storable, tileCount * 32 - firstByte));
            skipped += (storable - writable + 31) / 32;
            int changedPlain = 0;
            for (int t = 0; t * 32 < writable; t++) {
               bool tileChanged = false;
               for (int i = 0; i < 32; i++) {
                  if (model[start + firstByte + t * 32 + i] == frame[t * 32 + i]) continue;
                  token.ChangeData(model, start + firstByte + t * 32 + i, frame[t * 32 + i]);
                  tileChanged = true;
               }
               if (tileChanged) changedPlain++;
            }
            return new(changedPlain, skipped, false);
         }

         var run = FindTilesetRun(tilesetStart);
         var data = run?.GetData();
         if (data == null) return new(0, 0, true);
         var newData = new byte[Math.Max(data.Length, firstByte + storable)];
         Array.Copy(data, newData, data.Length);
         Array.Copy(frame, 0, newData, firstByte, storable);
         int changed = 0;
         for (int t = 0; t * 32 < newData.Length; t++) {
            for (int i = 0; i < 32 && t * 32 + i < newData.Length; i++) {
               var before = t * 32 + i < data.Length ? data[t * 32 + i] : (byte)0;
               if (newData[t * 32 + i] == before) continue;
               changed++;
               break;
            }
         }
         if (changed == 0 && newData.Length == data.Length) return new(0, skipped, false);

         var compressed = LZRun.Compress(newData, 0, newData.Length);
         var newRun = model.RelocateForExpansion(token, run, compressed.Count);
         for (int i = 0; i < compressed.Count; i++) token.ChangeData(model, newRun.Start + i, compressed[i]);
         for (int i = compressed.Count; i < run.Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         model.ObserveRunWritten(token, new LzTilesetRun(run.TilesetFormat, model, newRun.Start, newRun.PointerSources));
         return new(changed, skipped, false);
      }

      /// <summary>
      /// Animations made before the tileset showed their first frame leave the tileset's tiles blank, so there is nothing to pick in the map editor's block editor.
      /// For every animation that was added with HexManiac to this tileset and has nothing but blank tiles under it in the tileset, store the first frame there.
      /// A tileset that has some other picture there is never overwritten here (<see cref="ShowFirstFrameInTileset"/> does that when asked).
      /// Returns how many animations got their first frame stored.
      /// </summary>
      public int ShowFirstFramesInBlankTiles(ModelDelta token, int tilesetStart, bool isSecondary, TilesetAnimationConstants constants) {
         if (!TryGetTable(tilesetStart, out var table, out _)) return 0;
         int count = 0;
         byte[] tileset = null;
         foreach (var entry in ReadEntries(table, isSecondary, constants)) {
            tileset ??= ReadTilesetData(tilesetStart);
            if (CompareFirstFrame(tileset, entry) != FirstFrameState.TilesetBlank) continue;
            if (ShowFirstFrameInTileset(token, tilesetStart, entry).ChangedTiles > 0) count++;
            tileset = null; // the tileset's data changed
         }
         return count;
      }

      #endregion

      /// <summary>
      /// The game's InitTilesetAnim_* routines are tiny: they store a counter max and a callback pointer that sits in their literal pool.
      /// Find that pointer (a thumb address in the ROM) so the generated code can keep calling it.
      /// </summary>
      private int FindCallbackInInit(int initAddress) => FindCallbackInInit(model, initAddress);

      /// <summary>
      /// Decode the init routine (a few thumb instructions up to the first return) and return the thumb pointer it loads from its own literal pool, or NULL.
      /// Only literals that the routine itself loads with 'ldr rN, [pc, #imm]' count: some inits (Petalburg, Fallarbor, Fortree, Lilycove, Mossdeep) install no callback,
      /// and looking at the words after them instead finds the literal pool of the NEXT init routine (Rustboro's windy water animation, which then corrupted the tiles
      /// of every map that used the tileset once an animation was added to it).
      /// </summary>
      public static int FindCallbackInInit(IDataModel model, int initAddress) {
         int start = initAddress & ~1;
         int result = Pointer.NULL;
         for (int offset = 0; offset < 0x40 && start + offset + 2 <= model.Count; offset += 2) {
            var instruction = model.ReadMultiByteValue(start + offset, 2);
            if ((instruction & 0xF800) == 0x4800) {
               // ldr rN, [pc, #imm8 * 4]: the address is word aligned, relative to the instruction + 4
               var literal = ((start + offset + 4) & ~3) + (instruction & 0xFF) * 4;
               if (literal + 4 <= model.Count) {
                  var word = model.ReadMultiByteValue(literal, 4);
                  if (word >= BaseModel.PointerOffset && word < BaseModel.PointerOffset + model.Count && (word & 1) != 0) {
                     var candidate = word - BaseModel.PointerOffset;
                     if (candidate != start + 1) result = candidate; // the routine stores the last one it loads
                  }
               }
            } else if (instruction == 0x4770 || (instruction & 0xFF00) == 0xBD00 || (instruction & 0xFF87) == 0x4700) {
               break; // bx lr, pop {.., pc}, bx rN: the routine is over, whatever follows belongs to something else
            }
         }
         return result;
      }

      private int InsertCallback(string name, int tableAddress, int originalCallback, TilesetAnimationConstants constants) {
         var token = tokenFactory();
         var start = model.FindFreeSpace(model.FreeSpaceStart, 0xC0);
         if (start < 0) { start = model.Count; model.ExpandData(token, model.Count + 0x100); }
         var chain = originalCallback == Pointer.NULL ? 0 : (originalCallback & ~1) + BaseModel.PointerOffset;
         var code = $@"
push {{r4-r7, lr}}
mov  r6, r0                     @ r6 = timer

@ keep the tileset's own animation running
ldr  r7, =0x{chain:X8}
cmp  r7, #0
beq  <read_table>
mov  r0, r6
bl   <long_branch>

read_table:
ldr  r4, =<{tableAddress:X6}>   @ r4 = current table entry
sub  r4, #12
ldr  r5, =0xFEFEFEFE            @ r5 = end token
loop:
  add  r4, #12
  ldr  r0, [r4, #0]
  cmp  r0, r5
  beq  <end>

  @ only animate when the timer is a multiple of 1 << entry.timer
  mov  r0, #1
  ldrb r1, [r4, #6]
  lsl  r0, r1
  sub  r0, #1
  and  r0, r6
  cmp  r0, #0
  bne  <loop>

  @ frame = (timer >> entry.timer) % entry.frames
  mov  r0, r6
  lsr  r0, r1
  ldrh r1, [r4, #4]
  cmp  r1, #0
  beq  <loop>
mod_loop:
  cmp  r0, r1
  blo  <mod_done>
  sub  r0, r0, r1
  b    <mod_loop>
mod_done:
  lsl  r0, #2
  ldr  r2, [r4, #0]
  ldr  r0, [r2, r0]             @ r0 = source graphics (this frame)

  @ r1 = destination in VRAM
  ldr  r1, [r4, #8]
  lsl  r1, #5
  ldr  r2, =0x06000000
  add  r1, r2

  @ r2 = byte count
  ldrb r2, [r4, #7]
  lsl  r2, #5

  @ queue the copy: if (bufferSize < 20) buffer[bufferSize++] = (src, dest, size)
  ldr  r3, =0x{constants.BufferSize:X8}
  ldrb r7, [r3, #0]
  cmp  r7, #20
  bhs  <loop>
  lsl  r5, r7, #3
  lsl  r7, r7, #2
  add  r7, r5
  ldr  r5, =0x{constants.Buffer:X8}
  add  r7, r5
  str  r0, [r7, #0]
  str  r1, [r7, #4]
  strh r2, [r7, #8]
  ldrb r7, [r3, #0]
  add  r7, #1
  strb r7, [r3, #0]
  ldr  r5, =0xFEFEFEFE
  b    <loop>

end:
pop  {{r4-r7, pc}}
long_branch:
  add  r7, #1
  bx   r7
";
         parser.Compile(token, model, start, code.SplitLines());
         PokemonModel.UniquifyName(model, start, ref name);
         model.ObserveAnchorWritten(token, name, new NoInfoRun(start + 1)); // +1: pointers to thumb code carry the thumb bit
         return start;
      }

      private int InsertInit(string name, int tableAddress, int callbackAddress, int originalInit, bool isSecondary, TilesetAnimationConstants constants) {
         var token = tokenFactory();
         var start = model.FindFreeSpace(model.FreeSpaceStart, 0xA0);
         if (start < 0) { start = model.Count; model.ExpandData(token, model.Count + 0x100); }
         var counter = isSecondary ? constants.SecondaryCounter : constants.PrimaryCounter;
         var max = isSecondary ? constants.SecondaryMax : constants.PrimaryMax;
         var callbackVariable = isSecondary ? constants.SecondaryCallback : constants.PrimaryCallback;
         var chain = originalInit == Pointer.NULL ? 0 : (originalInit & ~1) + BaseModel.PointerOffset;
         var code = $@"
push {{r4-r7, lr}}

@ run the tileset's own init first (it sets its counter max and callback)
ldr  r7, =0x{chain:X8}
cmp  r7, #0
beq  <counter>
bl   <long_branch>

counter:
ldr  r0, =0x{counter:X8}
mov  r1, #0
strh r1, [r0, #0]

@ r1 = 1 << (largest entry.timer)
ldr  r2, =<{tableAddress:X6}>
sub  r2, #12
ldr  r3, =0xFEFEFEFE
mov  r1, #0
loop1:
  add  r2, #12
  ldr  r0, [r2, #0]
  cmp  r0, r3
  beq  <done1>
  ldrb r0, [r2, #6]
  cmp  r0, r1
  blo  <loop1>
  mov  r1, r0
  b    <loop1>
done1:
mov  r0, #1
lsl  r0, r1
mov  r1, r0

@ r1 *= every entry.frames
ldr  r2, =<{tableAddress:X6}>
sub  r2, #12
loop2:
  add  r2, #12
  ldr  r0, [r2, #0]
  cmp  r0, r3
  beq  <done2>
  ldrh r0, [r2, #4]
  cmp  r0, #0
  beq  <loop2>
  mul  r1, r0
  b    <loop2>
done2:

@ combine with the original counter max (if any) so both animations stay in sync: max = original * ours, unless that overflows
ldr  r0, =0x{max:X8}
ldrh r2, [r0, #0]
cmp  r2, #0
beq  <store>
mul  r2, r1
ldr  r3, =0x00010000
cmp  r2, r3
bhs  <store>
mov  r1, r2
store:
strh r1, [r0, #0]

@ install the table callback
ldr  r0, =0x{callbackVariable:X8}
ldr  r1, =<{(callbackAddress + 1):X6}>
str  r1, [r0, #0]

pop  {{r4-r7, pc}}
long_branch:
  add  r7, #1
  bx   r7
";
         parser.Compile(token, model, start, code.SplitLines());
         PokemonModel.UniquifyName(model, start, ref name);
         model.ObserveAnchorWritten(token, name, new NoInfoRun(start + 1)); // +1: pointers to thumb code carry the thumb bit
         return start;
      }
   }
}
