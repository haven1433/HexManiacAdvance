using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// Reads and writes the tiles of one tileset (the 'header' is the tileset record that map layouts point at: isCompressed, isSecondary, tiles, palettes...)
   /// as a picture file (<see cref="TilesetPicture"/>). Nothing here ever writes to a palette: the only bytes that change are the tile data,
   /// the pointer to it (when the tiles had to move), and the bookkeeping that goes with a moved run.
   /// Reading and checking never touch the model's history; <see cref="Write"/> does all of the changing, through the token it is given.
   /// </summary>
   public class TilesetPictureImporter {
      private readonly IDataModel model;
      private readonly int header;

      public TilesetPictureImporter(IDataModel model, int header) => (this.model, this.header) = (model, header);

      /// <summary>False for the few tilesets that store their tiles as they are.</summary>
      public bool IsCompressed => model[header] == 1;
      public bool IsSecondary => model[header + 1] != 0;
      public int TilesStart => model.ReadPointer(header + 4);
      public int PalettesStart => model.ReadPointer(header + 8);

      /// <summary>How many tiles video memory has room for: 512 for a primary and 512 for a secondary tileset of Emerald (640 and 384 in FireRed).</summary>
      public int MaxTiles {
         get {
            var primary = model.IsFRLG() ? 640 : 512;
            return IsSecondary ? 1024 - primary : primary;
         }
      }

      /// <summary>Which of the tileset's 16 palettes the picture carries: the first one the game really uses for the tileset (0 for a primary, 6 for a secondary tileset).</summary>
      public int PaletteNumber => IsSecondary ? (model.IsFRLG() ? 7 : 6) : 0;

      /// <summary>The tiles as they are in the ROM now (32 bytes per tile). Changes nothing.</summary>
      public bool TryReadTiles(out byte[] data, out int tileCount, out string error) {
         (data, tileCount, error) = (null, 0, null);
         var start = TilesStart;
         if (start < 0 || start >= model.Count) {
            error = "This tileset does not point at any tiles.";
            return false;
         }
         if (IsCompressed) {
            data = LZRun.Decompress(model.RawData, start, allowLengthErrors: true);
            if (data == null) {
               error = $"The tiles of this tileset (at {start:X6}) are not valid compressed data, so there is nothing to turn into a picture.";
               return false;
            }
         } else {
            // (nothing says where tiles stored without compression end: the program looks for the next thing in the ROM)
            var count = MaxTiles;
            BlocksetModel.EstimateTileCount(model, ref count, start);
            var length = Math.Max(0, Math.Min(count * 0x20, model.Count - start));
            data = new byte[length];
            Array.Copy(model.RawData, start, data, 0, length);
         }
         tileCount = data.Length / 0x20;
         if (tileCount < 1) {
            error = "This tileset has no tiles.";
            return false;
         }
         return true;
      }

      /// <summary>The colours of the palette the picture carries, as the palette editor holds them. Changes nothing.</summary>
      public bool TryReadPalette(out IReadOnlyList<short> palette, out string error) {
         palette = null;
         error = null;
         var start = PalettesStart;
         if (start < 0 || start + 16 * 0x20 > model.Count) {
            error = "This tileset does not point at its palettes, so its picture has no colours to use.";
            return false;
         }
         palette = PaletteRun.GetPalette(model, start + PaletteNumber * 0x20, 16);
         return true;
      }

      /// <summary>The PNG file with all the tiles of the tileset.</summary>
      public bool TryExport(out byte[] png, out int tileCount, out string error) {
         png = null;
         tileCount = 0;
         if (!TryReadTiles(out var tiles, out tileCount, out error) || !TryReadPalette(out var palette, out error)) return false;
         png = TilesetPicture.Export(tiles, tileCount, palette);
         return true;
      }

      /// <summary>
      /// Checks a picture file against the tileset and works out the tiles it gives. Changes nothing: a picture that can't be used is refused here, before anything is written.
      /// </summary>
      public bool TryRead(byte[] file, out TilesetPictureImport picture, out string error) {
         picture = null;
         if (!TryReadTiles(out var tiles, out var tileCount, out error) || !TryReadPalette(out var palette, out error)) return false;
         // tiles stored without compression are only ever rewritten where they are: nothing says how long they really are
         return TilesetPicture.TryRead(file, palette, tiles, tileCount, MaxTiles, out picture, out error, canGrow: IsCompressed);
      }

      /// <summary>
      /// Stores the tiles of a picture that <see cref="TryRead"/> accepted. Compressed tiles are compressed again and the tileset moves if they no longer fit;
      /// tiles stored without compression are rewritten where they are.
      /// </summary>
      /// <returns>Where the tiles are now.</returns>
      public int Write(ModelDelta token, TilesetPictureImport picture) {
         var start = TilesStart;
         if (IsCompressed) return WriteCompressed(model, token, header, Compress(picture.TileData));
         ForgetPointersTheTilesOverwrite(token, start, picture.TileData);
         token.ChangeData(model, start, picture.TileData);
         return start;
      }

      /// <summary>
      /// Tile data is full of bytes that look like pointers, and the model marks some of them as pointers. Where the new tiles change such bytes, they stop being pointers
      /// (otherwise the model would keep believing in a pointer to somewhere that the data no longer says). Only those pointer marks go: nothing else is touched.
      /// </summary>
      private void ForgetPointersTheTilesOverwrite(ModelDelta token, int start, byte[] data) {
         var end = start + data.Length;
         var overwritten = new List<IFormattedRun>();
         for (var run = model.GetNextRun(start); run.Start < end; run = model.GetNextRun(Math.Max(run.Start + run.Length, run.Start + 1))) {
            if (!(run is PointerRun) || run.Start < start || run.Start + run.Length > end) continue;
            var changed = false;
            for (int i = 0; i < run.Length; i++) changed |= model[run.Start + i] != data[run.Start + i - start];
            if (changed) overwritten.Add(run);
         }
         foreach (var run in overwritten) model.ClearFormat(token, run.Start, run.Length);
      }

      /// <summary>
      /// The tiles as LZ data. It is the smallest data the format allows (<see cref="TilesetLz"/>), which is usually smaller than the data the tileset has now:
      /// then a tileset whose picture changed a little stays where it is. The program's own compression takes over if that data would not decompress to exactly the tiles.
      /// </summary>
      public static byte[] Compress(byte[] tiles) {
         var smallest = TilesetLz.Compress(tiles);
         var check = LZRun.Decompress(smallest, 0);
         if (check != null && check.Length == tiles.Length && check.SequenceEqual(tiles)) return smallest;
         return LZRun.Compress(tiles, 0, tiles.Length).ToArray();
      }

      /// <summary>
      /// Puts new LZ data where the compressed tiles of the tileset with the given header are. If it doesn't fit, the data moves (and everything that points at it follows).
      /// </summary>
      /// <returns>Where the data is now.</returns>
      public static int WriteCompressed(IDataModel model, ModelDelta token, int header, byte[] contents) {
         var start = model.ReadPointer(header + 4);
         if (!(model.GetNextRun(start) is LzTilesetRun registered && registered.Start == start)) {
            // drawing a map does this: the data is registered as a tileset (only the format of the data, never its bytes)
            new BlocksetModel(model, header).ReadTiles();
         }
         var existingRun = model.GetNextRun(start);
         var newRun = model.RelocateForExpansion(token, existingRun, contents.Length);
         token.ChangeData(model, newRun.Start, contents);
         if (newRun is LzTilesetRun tsRun) {
            tsRun = new LzTilesetRun(tsRun.TilesetFormat, model, tsRun.Start, tsRun.PointerSources);
            model.ObserveRunWritten(token, tsRun);
         }
         for (int i = contents.Length; i < existingRun.Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         return newRun.Start;
      }

      /// <summary>
      /// The game's tile animations draw their own pictures over some tiles of the tileset while the map is shown.
      /// Those tiles can be part of the picture (the tileset keeps the first picture of the animation there), but changing them in the picture shows nothing in the game.
      /// </summary>
      /// <returns>A sentence about the changed tiles that an animation draws over, or null if there are none.</returns>
      public string DescribeAnimatedTiles(IReadOnlyList<int> changedTiles, Func<ModelDelta> tokenFactory) {
         try {
            if (!TilesetAnimationConstants.TryRead(model, out var constants)) return null;
            var animations = new TilesetAnimations(model, tokenFactory, null);
            var entries = new List<TilesetAnimationEntry>();
            if (animations.TryGetTable(header, out var table, out _)) entries.AddRange(animations.ReadEntries(table, IsSecondary, constants));
            entries.AddRange(animations.ReadBuiltInEntries(header, IsSecondary, constants));
            var hit = changedTiles.Where(tile => entries.Any(e => tile >= e.FirstTile && tile < e.FirstTile + e.TileCount)).Distinct().OrderBy(tile => tile).ToList();
            if (hit.Count == 0) return null;
            var ranges = new List<string>();
            for (int i = 0; i < hit.Count;) {
               var first = hit[i];
               var last = first;
               for (i++; i < hit.Count && hit[i] == last + 1; i++) last++;
               ranges.Add(first == last ? first.ToString() : $"{first}-{last}");
            }
            return $"Changed tiles {string.Join(", ", ranges)} are drawn by a tile animation while the game runs, so the game shows the animation there instead of the picture.";
         } catch (Exception) {
            return null; // (this is only extra information)
         }
      }
   }
}
