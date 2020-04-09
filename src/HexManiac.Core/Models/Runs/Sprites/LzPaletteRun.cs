using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LzPaletteRun : LZRun, IPaletteRun {
      public PaletteFormat PaletteFormat { get; }

      public override string FormatString { get; }

      public int Pages {
         get {
            var length = Model.ReadMultiByteValue(Start + 1, 3);
            var paletteLength = (int)Math.Pow(2, PaletteFormat.Bits) * 2;
            return length / paletteLength;
         }
      }

      public LzPaletteRun(PaletteFormat paletteFormat, IDataModel data, int start, IReadOnlyList<int> sources = null)
         : base(data, start, sources) {
         PaletteFormat = paletteFormat;
         if ((int)Math.Pow(2, paletteFormat.Bits) * 2 > DecompressedLength) InvalidateLength();
         FormatString = $"`lzp{paletteFormat.Bits}`";
      }

      public static bool TryParsePaletteFormat(string pointerFormat, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         if (!pointerFormat.StartsWith("`lzp") || !pointerFormat.EndsWith("`")) return false;
         return TryParseDimensions(pointerFormat, out paletteFormat);
      }

      public static bool TryParseDimensions(string format, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         var formatContent = format.Substring(4, format.Length - 5);
         if (!int.TryParse(formatContent, out var bits)) return false;
         paletteFormat = new PaletteFormat(bits);
         return true;
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LzPaletteRun(PaletteFormat, Model, Start, newPointerSources);

      public IReadOnlyList<short> GetPalette(IDataModel model, int page) {
         var data = Decompress(model, Start);
         var colorCount = (int)Math.Pow(2, PaletteFormat.Bits);
         var pageLength = colorCount * 2;
         page %= Pages;
         return Sprites.PaletteRun.GetPalette(data, page * pageLength, colorCount);
      }

      public IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors) {
         throw new NotImplementedException();
      }
   }
}
