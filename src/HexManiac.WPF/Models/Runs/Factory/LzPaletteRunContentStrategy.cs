using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `lzpB` where B=bits. Example: `lzp4`
   /// Represents an _lz compressed_ run of bytes for a palette.
   /// The palette can either be 4 bits per color (16 colors) or 8 bits per color (256 colors).
   /// Either way, each color when uncompressed is 16 bits long, with 5 bits each for rgb.
   /// </summary>
   public class LzPaletteRunContentStrategy : RunStrategy {
      private readonly PaletteFormat paletteFormat;
      public LzPaletteRunContentStrategy(PaletteFormat paletteFormat) => this.paletteFormat = paletteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var data = LZRun.Compress(new byte[paletteFormat.ExpectedByteLengthPerPage], 0, paletteFormat.ExpectedByteLengthPerPage);
         return data.Count;
      }
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var lzRun = new LzPaletteRun(paletteFormat, owner, destination, new SortedSpan<int>(source));
         var numColors = (int)Math.Pow(2, paletteFormat.Bits);
         if (lzRun.Length <= 5 || lzRun.DecompressedLength % (numColors * 2) != 0) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, lzRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is LzPaletteRun palRun && palRun.PaletteFormat.Pages == paletteFormat.Pages && palRun.PaletteFormat.Bits == paletteFormat.Bits;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var data = LZRun.Compress(new byte[paletteFormat.ExpectedByteLengthPerPage], 0, paletteFormat.ExpectedByteLengthPerPage);
         for (int i = 0; i < data.Count; i++) token.ChangeData(owner, destination + i, data[i]);
         return new LzPaletteRun(paletteFormat, owner, destination);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var localFormat = paletteFormat;
         if (sourceSegments != null && parentIndex >= 0) localFormat = new    PaletteFormat(paletteFormat.Bits, paletteFormat.Pages, paletteFormat.InitialBlankPages, allowLengthErrors: true);
         var runAttempt = new LzPaletteRun(localFormat, model, run.Start, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = PaletteRunContentStrategy.IsValid(paletteFormat.Bits);
         if (error.HasError) return error;
         run = new LzPaletteRun(paletteFormat, model, dataIndex, run.PointerSources);
         if (run.Length == LZRun.DecompressedTooLong) return new ErrorInfo("Decompressed more bytes than expected. Add a ! to override this.");
         if (run.Length < 6) return new ErrorInfo($"Compressed data needs to be at least {(int)Math.Pow(2, paletteFormat.Bits + 1)} bytes when decompressed, but was too short.");
         return ErrorInfo.NoError;
      }
   }
}
