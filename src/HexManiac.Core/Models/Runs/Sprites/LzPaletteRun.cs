using System;
using System.Collections.Generic;
using System.Linq;

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

      public LzPaletteRun(PaletteFormat paletteFormat, IDataModel data, int start, SortedSpan<int> sources = null)
         : base(data, start, sources) {
         PaletteFormat = paletteFormat;
         if ((int)Math.Pow(2, paletteFormat.Bits) * 2 > DecompressedLength) InvalidateLength();
         FormatString = $"`lzp{paletteFormat.Bits}`";
         if (paletteFormat.InitialBlankPages != 0) {
            FormatString = $"`lzp{paletteFormat.Bits}:";
            for (int i = 0; i < paletteFormat.Pages; i++) FormatString += ViewModels.ViewPort.AllHexCharacters[paletteFormat.InitialBlankPages + i];
            FormatString += "`";
         }
      }

      public static bool TryParsePaletteFormat(string pointerFormat, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         if (!pointerFormat.StartsWith("`lzp") || !pointerFormat.EndsWith("`")) return false;
         return TryParseDimensions(pointerFormat, out paletteFormat);
      }

      public static bool TryParseDimensions(string format, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         var formatContent = format.Substring(4, format.Length - 5);
         var pageSplit = formatContent.Split(':');
         if (!int.TryParse(pageSplit[0], out var bits)) return false;
         int pages = 1, pageStart = 0;
         if (pageSplit.Length == 2) {
            var lastPageID = pageSplit[1].ToUpper().LastOrDefault();
            var lastPageIndex = ViewModels.ViewPort.AllHexCharacters.IndexOf(lastPageID);
            if (lastPageIndex > 0) pages = lastPageIndex + 1;
            var firstPageID = pageSplit[1].ToUpper().FirstOrDefault();
            var firstPageIndex = ViewModels.ViewPort.AllHexCharacters.IndexOf(firstPageID);
            if (firstPageIndex > 0) pageStart = firstPageIndex;
            pages -= firstPageIndex;
         }

         paletteFormat = new PaletteFormat(bits, pages, pageStart);
         return true;
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new LzPaletteRun(PaletteFormat, Model, Start, newPointerSources);

      public IPaletteRun Duplicate(PaletteFormat newFormat) => new LzPaletteRun(newFormat, Model, Start, PointerSources);

      public IReadOnlyList<short> GetPalette(IDataModel model, int page) {
         var data = Decompress(model, Start);
         var colorCount = (int)Math.Pow(2, PaletteFormat.Bits);
         var pageLength = colorCount * 2;
         page %= Pages;
         return PaletteRun.GetPalette(data, page * pageLength, colorCount);
      }

      public IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors) {
         var data = Decompress(model, Start);
         var colorCount = (int)Math.Pow(2, PaletteFormat.Bits);
         var pageLength = colorCount * 2;
         page %= Pages;
         PaletteRun.SetPalette(data, page * pageLength, colors);

         var newModelData = Compress(data, 0, data.Length);
         var newRun = (IPaletteRun)model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(model, newRun.Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         newRun = new LzPaletteRun(PaletteFormat, model, newRun.Start, newRun.PointerSources);
         model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      public LzPaletteRun AppendPage(ModelDelta token) {
         var data = Decompress(Model, Start);
         var lastPage = Pages - 1;
         var pageLength = (int)Math.Pow(2, PaletteFormat.Bits) * 2;
         var newData = new byte[data.Length + pageLength];
         Array.Copy(data, newData, data.Length);
         Array.Copy(data, lastPage * pageLength, newData, data.Length, pageLength);
         var newModelData = Compress(newData, 0, newData.Length);

         var newRun = Model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(Model, newRun.Start + i, newModelData[i]);
         newRun = new LzPaletteRun(PaletteFormat, Model, newRun.Start, newRun.PointerSources);
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      public LzPaletteRun DeletePage(int page, ModelDelta token) {
         var data = Decompress(Model, Start);
         var pageLength = (int)Math.Pow(2, PaletteFormat.Bits) * 2;
         var newData = new byte[data.Length - pageLength];
         Array.Copy(data, newData, page * pageLength);
         Array.Copy(data, (page + 1) * pageLength, newData, page * pageLength, (Pages - page - 1) * pageLength);
         var newModelData = Compress(newData, 0, newData.Length);

         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(Model, Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(Model, Start + i, 0xFF);
         var newRun = new LzPaletteRun(PaletteFormat, Model, Start, PointerSources);
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }
   }
}
