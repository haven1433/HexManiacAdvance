using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// One animated door: when the player walks through the metatile, the game draws these frames over it.
   /// Decomp layout (20 bytes): metatile:2 pad:2 tileset:4 sound:1 size:1 pad:2 tiles:4 palettes:4
   /// </summary>
   public class DoorEntry {
      public int Index { get; init; }
      public int EntryAddress { get; init; }
      public int Metatile { get; init; }
      public int TilesetAddress { get; init; }
      public int Sound { get; init; }
      public int Size { get; init; }
      public int TilesAddress { get; init; }
      public int PalettesAddress { get; init; }

      public const int FrameCount = 3;
      public int WidthTiles => Size >= 2 ? 4 : 2;
      public int HeightTiles => Size == 0 ? 2 : 4;
      public int TilesPerFrame => WidthTiles * HeightTiles;
      public int TilesLength => TilesPerFrame * 32 * FrameCount;
      public string SizeName => Size switch { 0 => "1x1", 1 => "1x2", 2 => "2x2 left", 3 => "2x2 right", _ => Size.ToString() };
      public string SoundName => Sound switch { 0 => "normal", 1 => "sliding", 2 => "arena", _ => Sound.ToString() };
      public static int WidthTilesFor(int size) => size >= 2 ? 4 : 2;
      public static int HeightTilesFor(int size) => size == 0 ? 2 : 4;

      /// <summary>
      /// Where tile number 'index' of a frame (the order the tiles are stored in) is drawn in the door's picture, counted in tiles.
      /// One and two block tall doors store their tiles in rows, two tiles wide (the top block's 4 tiles, then the bottom block's).
      /// A 2x2 door is four blocks of 4 tiles stored top left, bottom left, top right, bottom right (that is the order the game draws them in).
      /// </summary>
      public (int x, int y) TilePosition(int index) => TilePositionFor(Size, index);
      public static (int x, int y) TilePositionFor(int size, int index) {
         if (size < 2) return (index % 2, index / 2);
         int block = index / 4, within = index % 4;
         return (block / 2 * 2 + within % 2, block % 2 * 2 + within / 2);
      }

      /// <summary>The storage order of the tile drawn at (x, y) of the door's picture: the inverse of <see cref="TilePosition"/>.</summary>
      public int TileIndexAt(int x, int y) => TileIndexFor(Size, x, y);
      public static int TileIndexFor(int size, int x, int y) {
         if (size < 2) return y * 2 + x;
         return (x / 2 * 2 + y / 2) * 4 + y % 2 * 2 + x % 2;
      }
   }

   /// <summary>
   /// Reads and edits the game's door animation table (data.maps.doors).
   /// </summary>
   public class DoorAnimations {
      public const string TableName = "data.maps.doors";
      public const int EntryLength = 20;
      /// <summary>The door tiles use the palettes of the tileset named by the entry's tileset pointer.</summary>
      public const string PaletteHint = "tileset/pal";

      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;

      public DoorAnimations(IDataModel model, Func<ModelDelta> tokenFactory) => (this.model, this.tokenFactory) = (model, tokenFactory);

      public static bool IsSupported(IDataModel model) => model.GetTable(TableName) is ITableRun table && table.ElementLength == EntryLength;

      public ITableRun Table => model.GetTable(TableName);

      public IReadOnlyList<DoorEntry> ReadEntries() {
         var results = new List<DoorEntry>();
         var table = Table;
         if (table == null || table.ElementLength != EntryLength) return results;
         for (int i = 0; i < table.ElementCount; i++) {
            var entry = table.Start + table.ElementLength * i;
            results.Add(new DoorEntry {
               Index = i,
               EntryAddress = entry,
               Metatile = model.ReadMultiByteValue(entry, 2),
               TilesetAddress = model.ReadPointer(entry + 4),
               Sound = model[entry + 8],
               Size = model[entry + 9],
               TilesAddress = model.ReadPointer(entry + 12),
               PalettesAddress = model.ReadPointer(entry + 16),
            });
         }
         return results;
      }

      /// <summary>All three frames, as raw 4bpp tiles (row-major within each frame).</summary>
      public byte[] ReadTiles(DoorEntry entry) {
         if (entry.TilesAddress < 0 || entry.TilesAddress + entry.TilesLength > model.Count) return null;
         var data = new byte[entry.TilesLength];
         Array.Copy(model.RawData, entry.TilesAddress, data, 0, data.Length);
         return data;
      }

      public void WriteTiles(DoorEntry entry, byte[] data) {
         if (entry.TilesAddress < 0 || entry.TilesAddress + data.Length > model.Count) return;
         var token = tokenFactory();
         for (int i = 0; i < data.Length && i < entry.TilesLength; i++) {
            if (model[entry.TilesAddress + i] != data[i]) token.ChangeData(model, entry.TilesAddress + i, data[i]);
         }
      }

      /// <summary>
      /// One frame of the door as a picture of palette indices, WidthTiles x HeightTiles tiles, with the tiles in the places the game draws them
      /// (see <see cref="DoorEntry.TilePosition"/>). Returns a blank picture if the door's tiles are out of range.
      /// </summary>
      public int[,] ReadFramePixels(DoorEntry entry, int frame) {
         var pixels = new int[entry.WidthTiles * 8, entry.HeightTiles * 8];
         if (entry.TilesAddress < 0 || entry.TilesAddress + entry.TilesLength > model.Count || frame < 0 || frame >= DoorEntry.FrameCount) return pixels;
         for (int t = 0; t < entry.TilesPerFrame; t++) {
            var (tileX, tileY) = entry.TilePosition(t);
            var start = entry.TilesAddress + (frame * entry.TilesPerFrame + t) * 32;
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  var b = model[start + y * 4 + x / 2];
                  pixels[tileX * 8 + x, tileY * 8 + y] = x % 2 == 0 ? b & 0xF : b >> 4;
               }
            }
         }
         return pixels;
      }

      /// <summary>Write an edited picture (the shape ReadFramePixels returned) back into the frame's tiles. Only the bytes that changed are written.</summary>
      public void WriteFramePixels(ModelDelta token, DoorEntry entry, int frame, int[,] pixels) {
         if (entry.TilesAddress < 0 || entry.TilesAddress + entry.TilesLength > model.Count || frame < 0 || frame >= DoorEntry.FrameCount) return;
         if (pixels.GetLength(0) != entry.WidthTiles * 8 || pixels.GetLength(1) != entry.HeightTiles * 8) return;
         for (int t = 0; t < entry.TilesPerFrame; t++) {
            var (tileX, tileY) = entry.TilePosition(t);
            var start = entry.TilesAddress + (frame * entry.TilesPerFrame + t) * 32;
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x += 2) {
                  var value = (byte)((pixels[tileX * 8 + x, tileY * 8 + y] & 0xF) | ((pixels[tileX * 8 + x + 1, tileY * 8 + y] & 0xF) << 4));
                  if (model[start + y * 4 + x / 2] != value) token.ChangeData(model, start + y * 4 + x / 2, value);
               }
            }
         }
      }

      /// <summary>Which of the tileset's 16 palettes each tile of a frame uses.</summary>
      public int[] ReadPaletteIndices(DoorEntry entry) {
         var result = new int[entry.TilesPerFrame];
         if (entry.PalettesAddress < 0 || entry.PalettesAddress + result.Length > model.Count) return result;
         for (int i = 0; i < result.Length; i++) result[i] = model[entry.PalettesAddress + i] & 15;
         return result;
      }

      public void WritePaletteIndices(DoorEntry entry, int palette) {
         if (entry.PalettesAddress < 0 || entry.PalettesAddress + entry.TilesPerFrame > model.Count) return;
         var token = tokenFactory();
         for (int i = 0; i < entry.TilesPerFrame; i++) token.ChangeData(model, entry.PalettesAddress + i, (byte)(palette & 15));
      }

      /// <summary>
      /// The shape the door's tiles are registered with: two tiles wide, and tall enough for all the tiles of all three frames.
      /// That is the order the tiles are stored in (see <see cref="DoorEntry.TilePosition"/> for how a frame is laid out when it is shown as a picture).
      /// </summary>
      public static SpriteFormat TilesFormat(DoorEntry entry) => new SpriteFormat(4, 2, entry.TilesPerFrame / 2 * DoorEntry.FrameCount, PaletteHint);

      /// <summary>Make sure the door's tiles are registered as a sprite that knows its palette (the tileset's), so the image editor shows them in colour.</summary>
      public void EnsureTilesFormat(DoorEntry entry) {
         if (entry.TilesAddress < 0 || entry.TilesAddress + entry.TilesLength > model.Count) return;
         var existing = model.GetNextRun(entry.TilesAddress);
         var format = TilesFormat(entry);
         if (existing is ISpriteRun sprite && sprite.Start == entry.TilesAddress && sprite.SpriteFormat.PaletteHint == PaletteHint &&
            sprite.SpriteFormat.TileWidth == format.TileWidth && sprite.SpriteFormat.TileHeight == format.TileHeight && sprite.SpriteFormat.BitsPerPixel == 4 &&
            sprite.PointerSources.Count > 0) return;
         var token = tokenFactory();
         var sources = existing.Start == entry.TilesAddress && existing.PointerSources != null ? existing.PointerSources : SortedSpan<int>.None;
         sources = sources.Add1(entry.EntryAddress + 12);
         if (existing.Start != entry.TilesAddress) model.ClearFormat(token, entry.TilesAddress, entry.TilesLength);
         else if (existing.Length < entry.TilesLength) model.ClearFormat(token, entry.TilesAddress + existing.Length, entry.TilesLength - existing.Length); // a word elsewhere that looks like a pointer into the tiles leaves a run inside them
         model.ObserveRunWritten(token, new SpriteRun(model, entry.TilesAddress, format, sources));
      }

      /// <summary>
      /// Does this metatile behavior make the game play a door animation? True or false if the ROM's behavior names say, null if they can't be read.
      /// The game only animates blocks with the ANIMATED_DOOR behavior (and the Petalburg gym door).
      /// </summary>
      public static bool? IsDoorBehavior(IDataModel model, int behavior) {
         if (behavior < 0 || !model.TryGetList("MapAttributeBehaviors", out var list) || behavior >= list.Count) return null;
         if (list[behavior] == null) return false; // (a behavior without a name: the list can have holes)
         var name = list[behavior].ToUpperInvariant().Replace(" ", "_");
         if (name.StartsWith("MB_")) name = name.Substring(3);
         return name == "ANIMATED_DOOR" || name == "PETALBURG_GYM_DOOR";
      }

      public static string BehaviorName(IDataModel model, int behavior) {
         if (behavior >= 0 && model.TryGetList("MapAttributeBehaviors", out var list) && behavior < list.Count && list[behavior] != null) return list[behavior];
         return behavior.ToString();
      }

      public void SetSound(DoorEntry entry, int sound) => tokenFactory().ChangeData(model, entry.EntryAddress + 8, (byte)sound.LimitToRange(0, 2));

      public void SetMetatile(DoorEntry entry, int metatile) => model.WriteMultiByteValue(entry.EntryAddress, 2, tokenFactory(), metatile.LimitToRange(0, 0x3FF));

      /// <summary>
      /// Add a door whose frames start as copies of the given tiles (or blank when null), and return it.
      /// </summary>
      public DoorEntry AddDoor(int tilesetAddress, int metatile, int size, int sound, int palette, byte[] initialTiles = null) {
         var token = tokenFactory();
         var table = Table;
         if (table == null) throw new InvalidOperationException($"This ROM has no {TableName} table.");
         size = size.LimitToRange(0, 3);
         int width = DoorEntry.WidthTilesFor(size), height = DoorEntry.HeightTilesFor(size);
         int tilesPerFrame = width * height, tilesLength = tilesPerFrame * 32 * DoorEntry.FrameCount;
         var tilesFormat = new SpriteFormat(4, 2, tilesPerFrame / 2 * DoorEntry.FrameCount, PaletteHint);

         // the game reads the table until an entry with no tiles, so the new door goes in front of that terminator
         var index = TerminatorIndex(table);
         table = model.RelocateForExpansion(token, table, table.Length + table.ElementLength);
         table = table.Append(token, 1);
         model.ObserveRunWritten(token, table);
         for (int i = table.ElementCount - 1; i > index; i--) MoveEntry(token, table, i - 1, i);
         var entry = table.Start + table.ElementLength * index;
         for (int b = 0; b < table.ElementLength; b++) token.ChangeData(model, entry + b, 0);

         // palette indices (one byte per tile of a frame), registered before the next allocation
         var palettesAddress = model.FindFreeSpace(model.FreeSpaceStart, tilesPerFrame + 4);
         if (palettesAddress < 0) { palettesAddress = model.Count; model.ExpandData(token, model.Count + tilesPerFrame + 0x10); }
         for (int i = 0; i < tilesPerFrame; i++) token.ChangeData(model, palettesAddress + i, (byte)(palette & 15));
         for (int i = tilesPerFrame; i < (tilesPerFrame + 3) / 4 * 4; i++) token.ChangeData(model, palettesAddress + i, 0);
         model.ObserveRunWritten(token, new NoInfoRun(palettesAddress, SortedSpan.One(entry + 16)));

         // the frames
         var tilesAddress = model.FindFreeSpace(model.FreeSpaceStart, tilesLength);
         if (tilesAddress < 0) { tilesAddress = model.Count; model.ExpandData(token, model.Count + tilesLength + 0x10); }
         for (int i = 0; i < tilesLength; i++) token.ChangeData(model, tilesAddress + i, initialTiles != null && i < initialTiles.Length ? initialTiles[i] : (byte)0);
         model.ObserveRunWritten(token, new SpriteRun(model, tilesAddress, tilesFormat, SortedSpan.One(entry + 12)));

         // the entry
         model.WriteMultiByteValue(entry, 2, token, metatile.LimitToRange(0, 0x3FF));
         model.WriteMultiByteValue(entry + 2, 2, token, 0);
         model.WritePointer(token, entry + 4, tilesetAddress);
         token.ChangeData(model, entry + 8, (byte)sound.LimitToRange(0, 2));
         token.ChangeData(model, entry + 9, (byte)size);
         model.WriteMultiByteValue(entry + 10, 2, token, 0);
         var element = new ModelArrayElement(model, table.Start, index, tokenFactory, table);
         element.SetAddress("tileset", tilesetAddress);
         element.SetAddress("palettes", palettesAddress);
         element.SetAddress("tiles", tilesAddress, writeDestinationFormat: false);
         if (model.GetNextRun(tilesAddress) is not ISpriteRun) {
            model.ObserveRunWritten(token, new SpriteRun(model, tilesAddress, tilesFormat, SortedSpan.One(entry + 12)));
         }
         return ReadEntries()[index];
      }

      public void RemoveDoor(int index) {
         var token = tokenFactory();
         var table = Table;
         if (table == null || index < 0 || index >= table.ElementCount) return;
         var entries = ReadEntries();
         var entry = entries[index];
         // free the graphics if no other door shares them
         if (entry.TilesAddress >= 0 && entry.TilesAddress + entry.TilesLength <= model.Count && entries.Count(e => e.TilesAddress == entry.TilesAddress) == 1) {
            model.ClearPointer(token, entry.EntryAddress + 12, entry.TilesAddress);
            model.ClearFormatAndData(token, entry.TilesAddress, entry.TilesLength);
         }
         if (entry.PalettesAddress >= 0 && entry.PalettesAddress + entry.TilesPerFrame <= model.Count && entries.Count(e => e.PalettesAddress == entry.PalettesAddress) == 1) {
            model.ClearPointer(token, entry.EntryAddress + 16, entry.PalettesAddress);
            model.ClearFormatAndData(token, entry.PalettesAddress, entry.TilesPerFrame);
         }
         // shift the later entries down
         for (int i = index + 1; i < table.ElementCount; i++) MoveEntry(token, table, i, i - 1);
         var shorter = table.Append(token, -1);
         model.ObserveRunWritten(token, shorter);
      }

      /// <summary>The index of the first entry with no tiles (the game stops reading there), or the element count if there is none.</summary>
      public int TerminatorIndex(ITableRun table) {
         for (int i = 0; i < table.ElementCount; i++) {
            var tiles = model.ReadPointer(table.Start + table.ElementLength * i + 12);
            if (tiles < 0 || tiles >= model.Count) return i;
         }
         return table.ElementCount;
      }

      /// <summary>Copy one entry over another, keeping the pointer bookkeeping straight.</summary>
      private void MoveEntry(ModelDelta token, ITableRun table, int fromIndex, int toIndex) {
         var from = table.Start + table.ElementLength * fromIndex;
         var to = table.Start + table.ElementLength * toIndex;
         var pointers = new[] { 4, 12, 16 }.Select(offset => model.ReadPointer(from + offset)).ToArray();
         foreach (var offset in new[] { 4, 12, 16 }) model.ClearPointer(token, from + offset, model.ReadPointer(from + offset));
         foreach (var offset in new[] { 4, 12, 16 }) model.ClearPointer(token, to + offset, model.ReadPointer(to + offset));
         for (int b = 0; b < table.ElementLength; b++) {
            if (b == 4 || b == 12 || b == 16) { b += 3; continue; }
            token.ChangeData(model, to + b, model[from + b]);
         }
         model.WritePointer(token, to + 4, pointers[0]);
         model.WritePointer(token, to + 12, pointers[1]);
         model.WritePointer(token, to + 16, pointers[2]);
      }
   }
}
