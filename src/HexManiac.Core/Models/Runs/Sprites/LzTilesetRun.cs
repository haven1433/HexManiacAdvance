using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LzTilesetRun : LZRun, ISpriteRun {
      SpriteFormat ISpriteRun.SpriteFormat => new SpriteFormat(Format.BitsPerPixel, Width, Height, Format.PaletteHint);
      public TilesetFormat Format { get; }
      public int Pages => 1;
      public int Width { get; }
      public int Height { get; }

      public override string FormatString => $"`lzt{Format.BitsPerPixel}" + (!string.IsNullOrEmpty(Format.PaletteHint) ? "|" + Format.PaletteHint : string.Empty) + "`";

      public LzTilesetRun(TilesetFormat format, IDataModel data, int start, IReadOnlyList<int> sources = null) : base(data, start, sources) {
         Format = format;
         var tileSize = (int)Math.Pow(2, format.BitsPerPixel + 1);
         var uncompressedSize = data.ReadMultiByteValue(start + 1, 3);
         var tileCount = uncompressedSize / tileSize;
         var roughSize = Math.Sqrt(tileCount);
         Width = (int)Math.Ceiling(roughSize);
         Height = (int)roughSize;
      }

      public static bool TryParseTilesetFormat(string format, out TilesetFormat tilesetFormat) {
         tilesetFormat = default;
         if (!(format.StartsWith("`lzt") && format.EndsWith("`"))) return false;
         format = format.Substring(4, format.Length - 5);

         // parse the paletteHint
         string hint = null;
         var pipeIndex = format.IndexOf('|');
         if (pipeIndex != -1) {
            hint = format.Substring(pipeIndex + 1);
            format = format.Substring(0, pipeIndex);
         }

         if (!int.TryParse(format, out int bits)) return false;
         tilesetFormat = new TilesetFormat(bits, hint);
         return true;
      }

      public int[,] GetPixels(IDataModel model, int page) {
         var data = Decompress(model, Start);
         return SpriteRun.GetPixels(data, 0, Width, Height);
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         throw new NotImplementedException();
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LzTilesetRun(Format, Model, Start, newPointerSources);
   }
}
