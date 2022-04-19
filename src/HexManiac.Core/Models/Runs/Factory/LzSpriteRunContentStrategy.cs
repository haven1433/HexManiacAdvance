using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `lzsBxWxH` B=Bits, W=Width, H=Height. Example: 4x8x8
   /// Represents a _lz compressed_ run of data for a sprite with the specified dimensions.
   /// The run format may optionally end with "|name", where "name" is the name of a palette or palette collection.
   /// This name serves as a hint, helping the sprite find its colors.
   /// </summary>
   public class LzSpriteRunContentStrategy : RunStrategy {
      readonly SpriteFormat spriteFormat;
      public LzSpriteRunContentStrategy(SpriteFormat spriteFormat) => this.spriteFormat = spriteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var data = LZRun.Compress(new byte[spriteFormat.ExpectedByteLength], 0, spriteFormat.ExpectedByteLength);
         return data.Count;
      }
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var lzRun = new LzSpriteRun(spriteFormat, owner, destination, new SortedSpan<int>(source));
         if (lzRun.Length <= 5 || owner.ReadMultiByteValue(destination + 1, 3) % 32 != 0) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, lzRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is LzSpriteRun spriteRun && spriteRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var data = LZRun.Compress(new byte[spriteFormat.ExpectedByteLength], 0, spriteFormat.ExpectedByteLength);
         for (int i = 0; i < data.Count; i++) token.ChangeData(owner, destination + i, data[i]);
         return new LzSpriteRun(spriteFormat, owner, destination);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var localSpriteFormat = spriteFormat;
         if (sourceSegments != null && parentIndex >= 0) localSpriteFormat = new SpriteFormat(spriteFormat.BitsPerPixel, spriteFormat.TileWidth, spriteFormat.TileHeight, spriteFormat.PaletteHint, allowLengthErrors: true);
         var runAttempt = new LzSpriteRun(localSpriteFormat, model, run.Start, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            if (run is LzSpriteRun lzRun && lzRun.SpriteFormat.Equals(runAttempt.SpriteFormat) && lzRun.Length == runAttempt.Length) {
               // no need to clear this one, the format matches
            } else {
               model.ClearFormat(token, run.Start, run.Length);
            }
         }
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = SpriteRunContentStrategy.IsValid(spriteFormat.BitsPerPixel);
         if (error.HasError) return error;
         run = new LzSpriteRun(spriteFormat, model, dataIndex, run.PointerSources);
         if (run.Length == LZRun.DecompressedTooLong) return new ErrorInfo("Decompressed more bytes than expected. Add a ! to override this.");
         if (run.Length < 6) return new ErrorInfo($"Compressed data needs to be at least {spriteFormat.ExpectedByteLength} when decompressed, but was too short.");
         return ErrorInfo.NoError;
      }
   }
}
