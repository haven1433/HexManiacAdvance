using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites;

public class MetatileRun : BaseRun, ISpriteRun {
   public static readonly string SharedFormatString = "`ucb`"; // uncompressed block

   private readonly IDataModel model;

   public bool SupportsImport => false;
   public bool SupportsEdit => false;
   public SpriteFormat SpriteFormat { get; }
   public int Pages => 1;
   public override int Length { get; }
   public override string FormatString => SharedFormatString;

   public MetatileRun(IDataModel model, int start, SortedSpan<int> sources) : base(start, sources) {
      this.model = model;

      //2 bytes for each tile/palette combo.
      //10 bits for the tile, for up to 1024 possible tiles
      //2  bits for x/y flip, for up to 4 orientations
      //4  bits for the color, for up to 16 possible palettes
      //Do this 8 times, once for each tile in the block (4 lower, 4 upper). A total of 16 bytes per tile.

      var source = sources[0];
      var tileset = model.ReadPointer(source - 8);
      var palette = model.ReadPointer(source - 4);
      var blockEnd = model.ReadPointer(source + 8);
      var Length = blockEnd - start;
      var blocks = Length / 16;
      int width = 40;
      int height = (blocks + width - 1) / width;
      SpriteFormat = new SpriteFormat(8, width * 2, height * 2, null);
   }

   public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
      // TODO support copy
   }

   public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
      // TODO make this better
      model.ClearFormatAndData(changeToken, start, length);
   }

   public ISpriteRun Duplicate(SpriteFormat newFormat) {
      return new MetatileRun(model, Start, PointerSources);
   }

   public byte[] GetData() {
      var tilesetAddress = model.ReadPointer(PointerSources[0] - 8);
      var tileset = (ITilesetRun)model.GetNextRun(tilesetAddress);
      var tilesetData = tileset.GetData();
      var blockData = new byte[Length];
      Array.Copy(model.RawData, Start, blockData, 0, Length);
      var result = new byte[8 * 8 * SpriteFormat.TileWidth * SpriteFormat.TileHeight];
      //var paletteAddress = model.ReadPointer(PointerSources[0] - 4);
      //var palette = (IPaletteRun)model.GetNextRun(paletteAddress);
      var lineWidth = SpriteFormat.TileWidth * 64;
      var cellWidth = 64;
      for (int y = 0; y < SpriteFormat.TileHeight / 2; y++) {
         for (int x = 0; x < SpriteFormat.TileWidth / 2; x++) {
            var block = y * SpriteFormat.TileWidth / 2 + x;
            int blockStart = block * 16;
            var resultOffset = y * lineWidth * 2 + x * cellWidth * 2;

            var tileBytes = GetTile(tilesetData, blockData, blockStart + 0);
            Array.Copy(result, resultOffset, tileBytes, 0, tileBytes.Length);
            tileBytes = GetTile(tilesetData, blockData, blockStart + 1);
            Array.Copy(result, resultOffset + cellWidth, tileBytes, 0, tileBytes.Length);
            tileBytes = GetTile(tilesetData, blockData, blockStart + 2);
            Array.Copy(result, resultOffset + lineWidth, tileBytes, 0, tileBytes.Length);
            tileBytes = GetTile(tilesetData, blockData, blockStart + 3);
            Array.Copy(result, resultOffset + lineWidth + cellWidth, tileBytes, 0, tileBytes.Length);

            tileBytes = GetTile(tilesetData, blockData, blockStart + 4);
            WriteTile(result, resultOffset, tileBytes);
            tileBytes = GetTile(tilesetData, blockData, blockStart + 5);
            WriteTile(result, resultOffset + cellWidth, tileBytes);
            tileBytes = GetTile(tilesetData, blockData, blockStart + 6);
            WriteTile(result, resultOffset + lineWidth, tileBytes);
            tileBytes = GetTile(tilesetData, blockData, blockStart + 7);
            WriteTile(result, resultOffset + lineWidth + cellWidth, tileBytes);
         }
      }
      model.ReadMultiByteValue(Start, 2);

      return result;
   }

   private byte[] GetTile(byte[] tileset, byte[] mapData, int index) {
      var (pal, hFlip, vFlip, tile) = LzTilemapRun.ReadTileData(mapData, index, 32);
      var result = new byte[64];

      pal <<= 4;
      var tileStart = tile * 32;
      var pixels = SpriteRun.GetPixels(tileset, tileStart, 1, 1, 4);
      for (int yy = 0; yy < 8; yy++) {
         for (int xx = 0; xx < 8; xx++) {
            var inX = hFlip ? 7 - xx : xx;
            var inY = vFlip ? 7 - yy : yy;
            result[yy * 8 + xx] = (byte)(pixels[inX, inY] + pal);
         }
      }
      return result;
   }

   private void WriteTile(byte[] result, int resultOffset, byte[] tileData) {
      for (int i = 0; i < tileData.Length; i++) {
         if (tileData[i] % 0x10 == 0) continue;
         result[resultOffset + i] = tileData[i];
      }
   }

   public int[,] GetPixels(IDataModel model, int page, int tableIndex) {
      // return everything as if a single layer, combining multiple tiles together, mixing palettes as needed
      throw new System.NotImplementedException();
   }

   public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
      throw new System.NotImplementedException();
   }

   public override IDataFormat CreateDataFormat(IDataModel data, int index) => new LzUncompressed(index);

   protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new MetatileRun(model, Start, newPointerSources);
}
