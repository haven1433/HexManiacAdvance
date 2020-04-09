using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class PaletteRun : BaseRun, IPaletteRun {
      private readonly int bits;

      public PaletteFormat PaletteFormat { get; }
      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public PaletteRun(int start, PaletteFormat format, IReadOnlyList<int> sources = null) : base(start, sources) {
         PaletteFormat = format;
         bits = format.Bits;
         Length = 2 * (int)Math.Pow(2, bits);
         FormatString = $"`ucp{bits}`";
      }

      public static bool TryParsePaletteFormat(string pointerFormat, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         if (!pointerFormat.StartsWith("`ucp") || !pointerFormat.EndsWith("`")) return false;
         return LzPaletteRun.TryParseDimensions(pointerFormat, out paletteFormat);
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var runPosition = index - Start;
         var colorPosition = runPosition % 2;
         var colorStart = Start + runPosition - colorPosition;
         var color = (short)data.ReadMultiByteValue(colorStart, 2);
         color = FlipColorChannels(color);
         return new UncompressedPaletteColor(colorStart, colorPosition, color);
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new PaletteRun(Start, PaletteFormat, newPointerSources);

      public IReadOnlyList<short> GetPalette(IDataModel model, int page) {
         page %= Pages;
         var paletteColorCount = (int)Math.Pow(2, bits);
         var pageLength = paletteColorCount * 2;
         return GetPalette(model, Start + page * pageLength, paletteColorCount);
      }

      public IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> data) {
         for (int i = 0; i < Length; i += 2) {
            model.WriteMultiByteValue(Start + i, 2, token, data[i / 2]);
         }
         return this;
      }

      public static IReadOnlyList<short> GetPalette(IReadOnlyList<byte> data, int start, int count) {
         var results = new List<short>();
         for (int i = 0; i < count; i++) {
            var color = (short)data.ReadMultiByteValue(start + i * 2, 2);
            results.Add(FlipColorChannels(color));
         }
         return results;
      }

      /// <summary>
      /// the gba and WPF do color channels reversed
      /// </summary>
      public static short FlipColorChannels(short color) {
         var r = ((color >> 10) & 0x1F);
         var g = ((color >> 5) & 0x1F);
         var b = ((color >> 0) & 0x1F);
         return (short)((b << 10) | (g << 5) | (r << 0));
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length) {
         while (length > 0) {
            var format = (UncompressedPaletteColor)CreateDataFormat(model, start);
            builder.Append(format.ToString() + " ");
            start += 2 - format.Position;
            length -= 2 - format.Position;
         }
      }
   }
}
