using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `ucsBxWxH` where B=bits, W=width, H=height. Ex: `ucs4x8x8`
   /// Represents an uncompressed stream of bytes representing a tiled image with a given width/height.
   /// Uncompressed sprites do not currently support paging. The byte length is determined soley by the width/height and the bitness.
   /// </summary>
   public class SpriteRunContentStrategy : RunStrategy {
      private readonly SpriteFormat spriteFormat;
      public SpriteRunContentStrategy(SpriteFormat spriteFormat) => this.spriteFormat = spriteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => spriteFormat.ExpectedByteLength;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var spriteRun = new SpriteRun(owner, destination, spriteFormat, new SortedSpan<int>(source));
         // TODO deal with the run being too long?
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, spriteRun);
         return true;
      }
      public override bool Matches(IFormattedRun run) =>
         run is SpriteRun spriteRun &&
         spriteRun.SpriteFormat.BitsPerPixel == spriteFormat.BitsPerPixel &&
         spriteRun.SpriteFormat.TileWidth == spriteFormat.TileWidth &&
         spriteRun.SpriteFormat.TileHeight == spriteFormat.TileHeight;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         for (int i = 0; i < spriteFormat.ExpectedByteLength; i++) token.ChangeData(owner, destination + i, 0);
         return new SpriteRun(owner, destination, spriteFormat);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var runAttempt = new SpriteRun(model, run.Start, spriteFormat, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = IsValid(spriteFormat.BitsPerPixel);
         if (error.HasError) return error;
         run = new SpriteRun(model, dataIndex, spriteFormat, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public static ErrorInfo IsValid(int bitsPerPixel) {
         if (!new[] { 1, 2, 4, 8 }.Contains(bitsPerPixel)) {
            return new ErrorInfo("Sprite bpp must be 1, 2, 4, or 8.");
         }
         return ErrorInfo.NoError;
      }
   }
}
