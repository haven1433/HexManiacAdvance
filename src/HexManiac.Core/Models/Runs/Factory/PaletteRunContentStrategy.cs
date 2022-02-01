using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `ucpB:P` where B=bits, P=Pages. Ex: `ucp4`
   /// Represents an uncompressed stream of bytes for a palette, where each color is 2 bytes long.
   /// The palette will either be for a 4-bit-per-color image (16 colors) or 8-bit-per-color image (256 colors)
   /// Uncompressed palettes do not currently support paging. The byte length is determined soley by the bitness.
   /// </summary>
   public class PaletteRunContentStrategy : RunStrategy {
      private readonly PaletteFormat paletteFormat;
      public PaletteRunContentStrategy(PaletteFormat paletteFormat) => this.paletteFormat = paletteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => paletteFormat.ExpectedByteLengthPerPage;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var palRun = new PaletteRun(destination, paletteFormat, new SortedSpan<int>(source));
         // TODO deal with the run being too long?
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, palRun);
         return true;
      }
      public override bool Matches(IFormattedRun run) => run is PaletteRun palRun && palRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var length = paletteFormat.ExpectedByteLengthPerPage;
         for (int i = 0; i < length; i++) token.ChangeData(owner, destination + i, 0);
         return new PaletteRun(destination, paletteFormat);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var runAttempt = new PaletteRun(run.Start, paletteFormat, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = IsValid(paletteFormat.Bits);
         if (error.HasError) return error;
         run = new PaletteRun(dataIndex, paletteFormat, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public static ErrorInfo IsValid(int bitness) {
         if (bitness != 4 && bitness != 8) {
            return new ErrorInfo("Palette bpp must be 4 or 8.");
         }
         return ErrorInfo.NoError;
      }
   }
}
