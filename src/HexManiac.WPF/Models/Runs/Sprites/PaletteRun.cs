using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class PaletteRun : BaseRun, IPaletteRun {
      private readonly int bits;

      public PaletteFormat PaletteFormat { get; }
      public int Pages { get; }
      public override int Length { get; }

      public override string FormatString { get; }

      public PaletteRun(int start, PaletteFormat format, SortedSpan<int> sources = null) : base(start, sources) {
         PaletteFormat = format;
         bits = format.Bits;
         Pages = format.Pages;
         if (bits == 8) Length = 512;
         if (bits == 4) Length = Pages * 32;
         var pagesPart = string.Empty;
         if (Pages > 1 || format.InitialBlankPages > 0) {
            pagesPart = ":" + GetPalettePages(format);
         }
         FormatString = $"`ucp{bits}{pagesPart}`";
      }

      public static string GetPalettePages(PaletteFormat format) {
         var pageIDs = Enumerable.Range(format.InitialBlankPages, format.Pages).Select(i => ViewModels.ViewPort.AllHexCharacters[i]);
         return new string(pageIDs.ToArray());
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

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new PaletteRun(Start, PaletteFormat, newPointerSources);

      public IPaletteRun Duplicate(PaletteFormat newFormat) => new PaletteRun(Start, newFormat, PointerSources);

      public IReadOnlyList<short> GetPalette(IDataModel model, int page) {
         page %= Pages;
         var paletteColorCount = (int)Math.Pow(2, bits);
         var pageLength = PaletteFormat.ExpectedByteLengthPerPage;
         return GetPalette(model, Start + page * pageLength, paletteColorCount);
      }

      public IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors) {
         page %= Pages;
         var pageLength = PaletteFormat.ExpectedByteLengthPerPage;
         var start = Start + page * pageLength;

         var data = new byte[pageLength];
         SetPalette(data, 0, colors);
         for (int i = 0; i < data.Length; i++) token.ChangeData(model, start + i, data[i]);
         return this;
      }

      /// <summary>
      /// The GBA stores colors in a 16-bit rgb value, 5 bits per channel. R is the high channel.
      /// We render colors as 16-bit bgr values, 5 bits per channel. B is the high channel.
      /// </summary>
      public static IReadOnlyList<short> GetPalette(IReadOnlyList<byte> data, int start, int count) {
         var results = new List<short>();
         for (int i = 0; i < count; i++) {
            var color = (short)data.ReadMultiByteValue(start + i * 2, 2);
            results.Add(FlipColorChannels(color));
         }
         return results;
      }

      /// <summary>
      /// We render colors as 16-bit bgr values, 5 bits per channel. B is the high channel.
      /// The GBA stores colors in a 16-bit rgb value, 5 bits per channel. R is the high channel.
      /// </summary>
      public static void SetPalette(byte[] data, int start, IReadOnlyList<short> colors) {
         for (int i = 0; i < colors.Count; i++) {
            var color = FlipColorChannels(colors[i]);
            data[start + i * 2 + 0] = (byte)(color >> 0);
            data[start + i * 2 + 1] = (byte)(color >> 8);
         }
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

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, int depth) {
         if (start < Start) {
            length -= Start - start;
            start = Start;
         }
         if (length > Length) length = Length;

         while (length > 0) {
            var format = (UncompressedPaletteColor)CreateDataFormat(model, start);
            builder.Append(format.ToString() + " ");
            start += 2 - format.Position;
            length -= 2 - format.Position;
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) {
            changeToken.ChangeData(model, start + i, 0x00);
         }
      }
   }
}
