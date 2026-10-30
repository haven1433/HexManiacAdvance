using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>A picture as palette indexes (one byte each, row after row) plus its palette. Index 0 is see-through.</summary>
   public class IntroImage {
      public int Width { get; }
      public int Height { get; }
      public byte[] Indexes { get; }
      /// <summary>Colours as the GBA stores them (5 bits per channel).</summary>
      public ushort[] Palette { get; }

      public IntroImage(int width, int height, byte[] indexes, ushort[] palette) => (Width, Height, Indexes, Palette) = (width, height, indexes, palette);

      /// <summary>The indexes as pixels[x, y], the shape the pictures of the editor are drawn from.</summary>
      public int[,] Pixels() {
         var result = new int[Width, Height];
         for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) result[x, y] = Indexes[y * Width + x];
         return result;
      }

      public short[] DisplayPalette() => Palette.Select(GbaColor.ToDisplay).ToArray();

      /// <summary>The highest palette index that the picture really uses (0 for an empty picture).</summary>
      public int ColorsUsed => Indexes.Length == 0 ? 0 : Indexes.Max() + 1;
   }

   /// <summary>Reading and writing the tile layout of the GBA: the picture is cut in 8x8 tiles that are stored one after the other, row of tiles after row of tiles.</summary>
   public static class IntroTiles {
      /// <summary>4 bits per pixel, the left pixel in the low nibble.</summary>
      public static byte[] Decode4(byte[] tiles, int tilesWide, int tilesHigh) {
         var width = tilesWide * 8;
         var result = new byte[width * tilesHigh * 8];
         for (int ty = 0; ty < tilesHigh; ty++) {
            for (int tx = 0; tx < tilesWide; tx++) {
               var tile = (ty * tilesWide + tx) * 32;
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x++) {
                     var b = tiles[tile + y * 4 + x / 2];
                     result[(ty * 8 + y) * width + tx * 8 + x] = (byte)((x & 1) == 0 ? b & 15 : b >> 4);
                  }
               }
            }
         }
         return result;
      }

      public static byte[] Encode4(byte[] indexes, int tilesWide, int tilesHigh) {
         var width = tilesWide * 8;
         var result = new byte[tilesWide * tilesHigh * 32];
         for (int ty = 0; ty < tilesHigh; ty++) {
            for (int tx = 0; tx < tilesWide; tx++) {
               var tile = (ty * tilesWide + tx) * 32;
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x += 2) {
                     var left = indexes[(ty * 8 + y) * width + tx * 8 + x] & 15;
                     var right = indexes[(ty * 8 + y) * width + tx * 8 + x + 1] & 15;
                     result[tile + y * 4 + x / 2] = (byte)(left | (right << 4));
                  }
               }
            }
         }
         return result;
      }

      /// <summary>8 bits per pixel.</summary>
      public static byte[] Decode8(byte[] tiles, int tilesWide, int tilesHigh) {
         var width = tilesWide * 8;
         var result = new byte[width * tilesHigh * 8];
         for (int ty = 0; ty < tilesHigh; ty++) {
            for (int tx = 0; tx < tilesWide; tx++) {
               var tile = (ty * tilesWide + tx) * 64;
               for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) result[(ty * 8 + y) * width + tx * 8 + x] = tiles[tile + y * 8 + x];
            }
         }
         return result;
      }

      public static byte[] Encode8(byte[] indexes, int tilesWide, int tilesHigh) {
         var width = tilesWide * 8;
         var result = new byte[tilesWide * tilesHigh * 64];
         for (int ty = 0; ty < tilesHigh; ty++) {
            for (int tx = 0; tx < tilesWide; tx++) {
               var tile = (ty * tilesWide + tx) * 64;
               for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) result[tile + y * 8 + x] = indexes[(ty * 8 + y) * width + tx * 8 + x];
            }
         }
         return result;
      }

      public static ushort[] ReadPalette(byte[] data, int start, int colors) {
         var result = new ushort[colors];
         for (int i = 0; i < colors; i++) result[i] = (ushort)(IntroThumb.Half(data, start + i * 2));
         return result;
      }

      public static byte[] WritePalette(IReadOnlyList<ushort> palette, int colors) {
         var result = new byte[colors * 2];
         for (int i = 0; i < colors && i < palette.Count; i++) { result[i * 2] = (byte)palette[i]; result[i * 2 + 1] = (byte)(palette[i] >> 8); }
         return result;
      }
   }

   /// <summary>
   /// The picture of the professor in the intro. The picture of Birch is 64x64 with 16 colours and stored as it is; the picture of Oak is 64x96 with 32 colours at 8 bits per pixel and compressed.
   /// New pictures are written to free space and the game's pointers are moved to them, so nothing else that shares the old picture changes, and 'back to the original' is just moving the pointers back.
   /// </summary>
   public abstract class IntroPicture {
      public abstract string Title { get; }
      public abstract int Width { get; }
      public abstract int Height { get; }
      /// <summary>Where the game's code or tables point at the picture's pixels and at its colours.</summary>
      public abstract IEnumerable<int> Addresses { get; }
      /// <summary>The picture in the ROM now, or null if it can't be read.</summary>
      public abstract IntroImage Read(byte[] data);
      /// <summary>Writes the source picture (64x64, at most 16 colours) as the intro picture. Returns false with a reason if it can't be done.</summary>
      public abstract bool Apply(IDataModel model, ModelDelta token, IntroImage source, out string reason);
      /// <summary>What the picture will look like in the game after <see cref="Apply"/> with this source (the preview before the change).</summary>
      public virtual IntroImage Compose(IntroImage source) => source;
      /// <summary>The addresses the pointers point to now: use with <see cref="Restore"/> to go back.</summary>
      public abstract int[] CurrentTargets(byte[] data);
      public abstract void Restore(IDataModel model, ModelDelta token, int[] targets);

      public static bool CanUse(IntroImage source, out string reason) {
         reason = null;
         if (source == null) { reason = "That picture could not be read."; return false; }
         if (source.Width != 64 || source.Height != 64) { reason = $"The picture must be 64x64 pixels; this one is {source.Width}x{source.Height}."; return false; }
         if (source.ColorsUsed > 16) { reason = "The picture can use at most 16 colors."; return false; }
         return true;
      }
   }

   /// <summary>Professor Birch of Emerald (and CUBE): a sprite template whose frame points at 0x800 bytes of tiles, and a palette structure.</summary>
   public class BirchPicture : IntroPicture {
      public const int FrameSize = 0x800;
      /// <summary>The 4 bytes that point at the tiles (the frame's data pointer).</summary>
      public int TilesPointer { get; }
      /// <summary>The 4 bytes that point at the 16 colours (the palette structure's pointer).</summary>
      public int PalettePointer { get; }

      public BirchPicture(int tilesPointer, int palettePointer) => (TilesPointer, PalettePointer) = (tilesPointer, palettePointer);

      public override string Title => "Professor Birch";
      public override int Width => 64;
      public override int Height => 64;
      public override IEnumerable<int> Addresses => new[] { TilesPointer, PalettePointer };

      public override int[] CurrentTargets(byte[] data) => new[] { IntroThumb.Word(data, TilesPointer) - IntroThumb.RomBase, IntroThumb.Word(data, PalettePointer) - IntroThumb.RomBase };

      public override IntroImage Read(byte[] data) {
         var targets = CurrentTargets(data);
         if (targets[0] < 0 || targets[0] + FrameSize > data.Length || targets[1] < 0 || targets[1] + 32 > data.Length) return null;
         var tiles = new byte[FrameSize];
         Array.Copy(data, targets[0], tiles, 0, FrameSize);
         return new IntroImage(64, 64, IntroTiles.Decode4(tiles, 8, 8), IntroTiles.ReadPalette(data, targets[1], 16));
      }

      public override bool Apply(IDataModel model, ModelDelta token, IntroImage source, out string reason) {
         if (!CanUse(source, out reason)) return false;
         var block = new byte[FrameSize + 32];
         Array.Copy(IntroTiles.Encode4(source.Indexes, 8, 8), block, FrameSize);
         Array.Copy(IntroTiles.WritePalette(source.Palette, 16), 0, block, FrameSize, 32);
         var address = IntroEdit.AllocateData(model, token, block.Length);
         token.ChangeData(model, address, block);
         IntroEdit.Repoint(model, token, TilesPointer, address);
         IntroEdit.Repoint(model, token, PalettePointer, address + FrameSize);
         return true;
      }

      public override void Restore(IDataModel model, ModelDelta token, int[] targets) {
         IntroEdit.Repoint(model, token, TilesPointer, targets[0]);
         IntroEdit.Repoint(model, token, PalettePointer, targets[1]);
      }
   }

   /// <summary>Professor Oak of FireRed: the picture the game shows for trainer picture 3 (the first of the new game speech). 64x96, 8 bits per pixel, LZ compressed, palette of 32 colors.</summary>
   public class OakPicture : IntroPicture {
      public const int TilesWide = 8, TilesHigh = 12, PaletteSlot = 0x60;
      /// <summary>The literal that points at the 32 colours.</summary>
      public int PaletteLiteral { get; }
      /// <summary>The literal that points at the compressed pixels.</summary>
      public int TilesLiteral { get; }

      public OakPicture(int paletteLiteral, int tilesLiteral) => (PaletteLiteral, TilesLiteral) = (paletteLiteral, tilesLiteral);

      public override string Title => "Professor Oak";
      public override int Width => 64;
      public override int Height => 96;
      public override IEnumerable<int> Addresses => new[] { TilesLiteral, PaletteLiteral };

      public override int[] CurrentTargets(byte[] data) => new[] { IntroThumb.Word(data, TilesLiteral) - IntroThumb.RomBase, IntroThumb.Word(data, PaletteLiteral) - IntroThumb.RomBase };

      public override IntroImage Read(byte[] data) {
         var targets = CurrentTargets(data);
         if (targets[0] < 0 || targets[0] >= data.Length || targets[1] < 0 || targets[1] + 64 > data.Length) return null;
         var tiles = LZRun.Decompress(data, targets[0]);
         if (tiles == null || tiles.Length != TilesWide * TilesHigh * 64) return null;
         var raw = IntroTiles.Decode8(tiles, TilesWide, TilesHigh);
         var indexes = new byte[raw.Length];
         for (int i = 0; i < raw.Length; i++) indexes[i] = raw[i] < PaletteSlot ? (byte)0 : (byte)(raw[i] - PaletteSlot);
         return new IntroImage(64, 96, indexes, IntroTiles.ReadPalette(data, targets[1], 32));
      }

      public override bool Apply(IDataModel model, ModelDelta token, IntroImage source, out string reason) {
         if (!CanUse(source, out reason)) return false;
         var indexes = Stack(source, PaletteSlot);
         var compressed = LZRun.Compress(IntroTiles.Encode8(indexes, TilesWide, TilesHigh)).ToArray();
         var palette = IntroTiles.WritePalette(source.Palette.Take(16).ToArray(), 32);
         var length = compressed.Length + 4 - (compressed.Length & 3) + 64;
         var address = IntroEdit.AllocateData(model, token, length);
         token.ChangeData(model, address, compressed);
         var paletteAddress = address + compressed.Length + 4 - (compressed.Length & 3);
         token.ChangeData(model, paletteAddress, palette);
         IntroEdit.Repoint(model, token, TilesLiteral, address);
         IntroEdit.Repoint(model, token, PaletteLiteral, paletteAddress);
         return true;
      }

      /// <summary>The new picture sits at the bottom of Oak's taller picture, so that the feet stay where they were. Non-transparent pixels get 'offset' added (the palette slot).</summary>
      private static byte[] Stack(IntroImage source, int offset) {
         var indexes = new byte[64 * 96];
         for (int y = 0; y < 64; y++) {
            for (int x = 0; x < 64; x++) {
               var i = source.Indexes[y * 64 + x];
               indexes[(y + 32) * 64 + x] = i == 0 ? (byte)0 : (byte)(offset + i);
            }
         }
         return indexes;
      }

      public override IntroImage Compose(IntroImage source) {
         if (source == null || source.Width != 64 || source.Height != 64) return source;
         return new IntroImage(64, 96, Stack(source, 0), source.Palette);
      }

      public override void Restore(IDataModel model, ModelDelta token, int[] targets) {
         IntroEdit.Repoint(model, token, TilesLiteral, targets[0]);
         IntroEdit.Repoint(model, token, PaletteLiteral, targets[1]);
      }
   }

   /// <summary>One picture that can be chosen for the professor: a Pokémon's front picture or a trainer's front picture. Loaded when chosen.</summary>
   public class IntroPictureChoice {
      public string Name { get; }
      public int Index { get; }
      private readonly Func<IntroImage> load;
      public IntroPictureChoice(string name, int index, Func<IntroImage> load) => (Name, Index, this.load) = (name, index, load);
      public IntroImage Load() {
         try { return load(); } catch (Exception) { return null; }
      }
      public override string ToString() => Name;
   }

   /// <summary>The pictures that can replace the professor: every Pokémon's front picture, and every trainer's front picture.</summary>
   public static class IntroPictureSources {
      /// <summary>Reads a compressed 64x64 picture of 16 colors and its compressed palette.</summary>
      public static IntroImage ReadCompressed(byte[] data, int spriteAddress, int paletteAddress) {
         if (spriteAddress < 0 || spriteAddress >= data.Length || paletteAddress < 0 || paletteAddress >= data.Length) return null;
         var tiles = LZRun.Decompress(data, spriteAddress);
         // Pokémon pictures hold their animation frames one after the other: the first 0x800 bytes are the picture itself
         if (tiles == null || tiles.Length < 0x800) return null;
         if (tiles.Length > 0x800) tiles = tiles.Take(0x800).ToArray();
         var palette = LZRun.Decompress(data, paletteAddress);
         if (palette == null || palette.Length < 32) {
            if (paletteAddress + 32 > data.Length) return null;
            palette = data.Skip(paletteAddress).Take(32).ToArray();
         }
         return new IntroImage(64, 64, IntroTiles.Decode4(tiles, 8, 8), IntroTiles.ReadPalette(palette, 0, 16));
      }

      private static int PointerAt(byte[] data, int address) {
         var value = IntroThumb.Word(data, address);
         return value >= IntroThumb.RomBase && value < IntroThumb.RomBase + 0x02000000 ? value - IntroThumb.RomBase : -1;
      }

      public static IReadOnlyList<IntroPictureChoice> Pokemon(IDataModel model) {
         var result = new List<IntroPictureChoice>();
         try {
            var names = model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(model)) ?? new List<string>();
            var data = model.RawData;
            var front = model.GetTable(HardcodeTablesModel.FrontSpritesTable);
            var palettes = model.GetTable(HardcodeTablesModel.PokePalettesTable);
            if (front != null && palettes != null) {
               var count = Math.Min(front.ElementCount, palettes.ElementCount);
               for (int i = 1; i < count; i++) {
                  var index = i;
                  result.Add(new IntroPictureChoice(NameOf(names, i), i, () => ReadCompressed(data, PointerAt(data, front.Start + index * front.ElementLength), PointerAt(data, palettes.Start + index * palettes.ElementLength))));
               }
               return result;
            }
            var stats = model.GetTableModel(HardcodeTablesModel.PokemonStatsTable);
            if (stats != null && stats.Count > 0 && stats[0].HasField("frontPic") && stats[0].HasField("palette")) {
               for (int i = 1; i < stats.Count; i++) {
                  var index = i;
                  result.Add(new IntroPictureChoice(NameOf(names, i), i, () => ReadCompressed(model.RawData, stats[index].GetAddress("frontPic"), stats[index].GetAddress("palette"))));
               }
            }
         } catch (Exception) { }
         return result;
      }

      public static IReadOnlyList<IntroPictureChoice> Trainers(IDataModel model) {
         var result = new List<IntroPictureChoice>();
         try {
            var data = model.RawData;
            var names = model.GetOptions(HardcodeTablesModel.TrainerSpritesName) ?? model.GetOptions("data.trainers.sprites") ?? new List<string>();
            var table = model.GetTable(HardcodeTablesModel.TrainerSpritesName);
            if (table != null) {
               // the palettes are the table that follows the pictures: {pointer, tag} like the pictures themselves
               var paletteStart = table.Start + table.ElementCount * table.ElementLength;
               for (int i = 0; i < table.ElementCount; i++) {
                  var index = i;
                  result.Add(new IntroPictureChoice(TrainerName(names, i), i, () => ReadCompressed(data, PointerAt(data, table.Start + index * table.ElementLength), PointerAt(data, paletteStart + index * table.ElementLength))));
               }
               return result;
            }
            var pics = model.GetTableModel("data.trainers.sprites");
            if (pics == null) return result;
            for (int i = 0; i < pics.Count; i++) {
               var index = i;
               result.Add(new IntroPictureChoice(TrainerName(names, i), i, () => {
                  var front = pics[index].GetSubTable("front")?[0];
                  return front == null ? null : ReadCompressed(model.RawData, front.GetAddress("sprite"), front.GetAddress("palette"));
               }));
            }
         } catch (Exception) { }
         return result;
      }

      private static string NameOf(IReadOnlyList<string> names, int index) => index < names.Count && !string.IsNullOrWhiteSpace(names[index]) ? names[index].Trim('"').Replace("\\sf", "♀").Replace("\\sm", "♂").Trim() : $"Pokémon {index}";
      private static string TrainerName(IReadOnlyList<string> names, int index) => index < names.Count && !string.IsNullOrWhiteSpace(names[index]) ? names[index].Trim('"') : $"Trainer picture {index}";
   }
}
