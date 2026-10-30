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
         var pageLength = PaletteFormat.ExpectedByteLengthPerPage;
         // More colors than one page holds are the colors of the whole palette (a 256 color picture shows all 16 pages of 16 colors at once): they start with the first page.
         page = colors.Count * 2 > pageLength ? 0 : page % Pages;
         var start = Start + page * pageLength;
         var colorsThatFit = (Pages - page) * pageLength / 2;
         if (colors.Count > colorsThatFit) colors = colors.Take(colorsThatFit).ToList();

         // start from the unused bit of each color that is stored now, so SetPalette keeps it (see UnusedColorBit)
         var data = new byte[Math.Max(pageLength, colors.Count * 2)];
         for (int i = 1; i < data.Length; i += 2) data[i] = start + i < model.Count ? (byte)(model[start + i] & 0x80) : (byte)0;
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
            // a palette that starts near the end of the data has no room for all its colors: the missing ones are black
            var color = start + i * 2 + 1 < data.Count && start + i * 2 >= 0 ? (short)data.ReadMultiByteValue(start + i * 2, 2) : (short)0;
            results.Add(FlipColorChannels(color));
         }
         return results;
      }

      /// <summary>
      /// We render colors as 16-bit bgr values, 5 bits per channel. B is the high channel.
      /// The GBA stores colors in a 16-bit rgb value, 5 bits per channel. R is the high channel.
      /// Bit 15 of each color that is already in <paramref name="data"/> is kept (see <see cref="UnusedColorBit"/>); a zeroed array gives colors with bit 15 clear.
      /// </summary>
      public static void SetPalette(byte[] data, int start, IReadOnlyList<short> colors) {
         for (int i = 0; i < colors.Count; i++) {
            var color = FlipColorChannels(colors[i]);
            data[start + i * 2 + 0] = (byte)(color >> 0);
            data[start + i * 2 + 1] = (byte)(((color >> 8) & 0x7F) | (data[start + i * 2 + 1] & 0x80));
         }
      }

      /// <summary>
      /// The GBA only looks at the lower 15 bits of a color. Bit 15 does nothing to the picture, but some games and hacks keep a flag there.
      /// The palette editor works with 15-bit colors (<see cref="GetPalette(IDataModel, int)"/> never shows bit 15), so every place that writes a color back
      /// must leave the bit that is stored now as it is: a color that was EE FA stays EE FA when only its red, green or blue changes. A color that is not stored yet has the bit clear.
      /// </summary>
      public const int UnusedColorBit = 0x8000;

      /// <summary>
      /// Makes the 16 bits to store for a color: the 15 bits of <paramref name="newColor"/> as they go in the ROM (red in the low bits), and bit 15 of what is <paramref name="storedNow"/>.
      /// </summary>
      public static short KeepUnusedColorBit(int storedNow, int newColor) => (short)((newColor & 0x7FFF) | (storedNow & UnusedColorBit));

      /// <summary>
      /// Writes one color (15 bits, as they go in the ROM: red in the low bits) to the two bytes at <paramref name="address"/>, keeping the bit 15 that is stored there now.
      /// Returns true if any byte changed.
      /// </summary>
      public static bool WriteStoredColor(IDataModel model, ModelDelta token, int address, int newColor) {
         var storedNow = model.ReadMultiByteValue(address, 2);
         return model.WriteMultiByteValue(address, 2, token, KeepUnusedColorBit(storedNow, newColor) & 0xFFFF);
      }

      /// <summary>
      /// Writes one color the way the palette editor holds it (<see cref="FlipColorChannels(short)"/> turns it into what the ROM stores), keeping the bit 15 that is stored there now.
      /// Returns true if any byte changed.
      /// </summary>
      public static bool WriteColor(IDataModel model, ModelDelta token, int address, short color) => WriteStoredColor(model, token, address, FlipColorChannels(color));

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
