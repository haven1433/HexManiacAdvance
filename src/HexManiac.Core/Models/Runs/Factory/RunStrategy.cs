using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public abstract class RunStrategy {
      public string Format { get; set; }

      /// <summary>
      /// If a 'default' run is created for the pointer at the given address, how many bytes need to be available at the destination location?
      /// </summary>
      public abstract int LengthForNewRun(IDataModel model, int pointerAddress);

      /// <summary>
      /// Returns true if the format is capable of being added for the pointer at source.
      /// If the token is such that edits are allowed, actually add the format.
      /// </summary>
      public abstract bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex);

      /// <summary>
      /// Returns true if the input run is valid for this run 'type'.
      /// Often this is just a type comparison, but for runs with multiple
      /// formats (example, SpriteRun with width/height), it can be more complex.
      /// </summary>
      public abstract bool Matches(IFormattedRun run);

      /// <summary>
      /// Create a new run meant to go into a pointer in a table.
      /// The destination has been prepared, but is all FF.
      /// </summary>
      public abstract IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments);

      /// <summary>
      /// A pointer format in a table has changed.
      /// Replace the given run with a new run of the appropriate format.
      /// </summary>
      public abstract void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run);

      /// <summary>
      /// Attempt to parse the existing data into a run of the desired type.
      /// </summary>
      public abstract ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run);

      protected ITableRun GetTable(IDataModel model, int pointerAddress) => (ITableRun)model.GetNextRun(pointerAddress);

      protected ArrayRunPointerSegment GetSegment(ITableRun table, int pointerAddress) {
         var offsets = table.ConvertByteOffsetToArrayOffset(pointerAddress);
         return (ArrayRunPointerSegment)table.ElementContent[offsets.SegmentIndex];
      }
   }

   public interface IFormatRunFactory {
      RunStrategy GetStrategy(string format, bool allowStreamCompressionErrors = false);
   }

   public class FormatRunFactory : IFormatRunFactory {
      private readonly bool showFullIVByteRange;
      public FormatRunFactory(bool showFullIVByteRange) => this.showFullIVByteRange = showFullIVByteRange;

      public RunStrategy GetStrategy(string format, bool allowStreamCompressionErrors = false) {
         RunStrategy strategy;
         if (format == PCSRun.SharedFormatString) {
            strategy = new PCSRunContentStrategy();
         } else if (format.StartsWith(AsciiRun.SharedFormatString)) {
            strategy = new AsciiRunContentStrategy();
         } else if (format == BrailleRun.SharedFormatString) {
            strategy = new BrailleRunContentStrategy();
         } else if (format == EggMoveRun.SharedFormatString) {
            strategy = new EggRunContentStrategy();
         } else if (format == PIERun.SharedFormatString) {
            strategy = new PIERunContentStrategy();
         } else if (format == PLMRun.SharedFormatString) {
            strategy = new PLMRunContentStrategy();
         } else if (format == XSERun.SharedFormatString) {
            strategy = new XseRunContentStrategy();
         } else if (format == BSERun.SharedFormatString) {
            strategy = new BseRunContentStrategy();
         } else if (format == ASERun.SharedFormatString) {
            strategy = new AseRunContentStrategy();
         } else if (format.StartsWith(OverworldSpriteListRun.SharedFormatString.Substring(0, OverworldSpriteListRun.SharedFormatString.Length - 1))) {
            strategy = new OverworldSpriteListContentStrategy(this, format);
         } else if (format == TrainerPokemonTeamRun.SharedFormatString) {
            strategy = new TrainerPokemonTeamRunContentStrategy(showFullIVByteRange);
         } else if (format == MapAnimationTilesRun.SharedFormatString) {
            strategy = new MapAnimationTilesStrategy();
         } else if (LzSpriteRun.TryParseSpriteFormat(format, out var spriteFormat)) {
            if (allowStreamCompressionErrors) spriteFormat = new SpriteFormat(spriteFormat.BitsPerPixel, spriteFormat.TileWidth, spriteFormat.TileHeight, spriteFormat.PaletteHint, allowStreamCompressionErrors);
            strategy = new LzSpriteRunContentStrategy(spriteFormat);
         } else if (LzPaletteRun.TryParsePaletteFormat(format, out var paletteFormat)) {
            if (allowStreamCompressionErrors) paletteFormat = new PaletteFormat(paletteFormat.Bits, paletteFormat.Pages, paletteFormat.InitialBlankPages, allowStreamCompressionErrors);
            strategy = new LzPaletteRunContentStrategy(paletteFormat);
         } else if (TilesetRun.TryParseTilesetFormat(format, out var tilesetFormat)) {
            strategy = new TilesetRunContentStrategy(tilesetFormat);
         } else if (LzTilesetRun.TryParseTilesetFormat(format, out var lzTilesetFormat)) {
            strategy = new LzTilesetRunContentStrategy(lzTilesetFormat);
         } else if (LzTilemapRun.TryParseTilemapFormat(format, out var tilemapFormat)) {
            strategy = new LzTilemapRunContentStrategy(tilemapFormat);
         } else if (TilemapRun.TryParseTilemapFormat(format, out var tilemapFormat1)) {
            strategy = new TilemapRunContentStrategy(tilemapFormat1);
         } else if (SpriteRun.TryParseSpriteFormat(format, out var spriteFormat1)) {
            strategy = new SpriteRunContentStrategy(spriteFormat1);
         } else if (format == MetatileRun.SharedFormatString) {
            strategy = new MetatileRunContentStrategy();
         } else if (PaletteRun.TryParsePaletteFormat(format, out var paletteFormat1)) {
            strategy = new PaletteRunContentStrategy(paletteFormat1);
         } else if (format.IndexOf("[") >= 0 && format.IndexOf("[") < format.IndexOf("]")) {
            strategy = new TableStreamRunContentStrategy();
         } else {
            return null;
         }

         strategy.Format = format;
         return strategy;
      }
   }

   public class XseRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 1;

      public override bool Matches(IFormattedRun run) => run is XSERun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var run = new XSERun(destination, new SortedSpan<int>(source));
         if (run.Length < 1) return false;
         if (!(token is NoDataChangeDeltaModel)) {
            owner.ClearFormat(token, run.Start, run.Length);
            owner.ObserveRunWritten(token, run);
         }
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new XSERun(dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         if (run is XSERun) return;
         run = new XSERun(run.Start, run.PointerSources);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         token.ChangeData(owner, destination, 2);
         return new XSERun(destination);
      }
   }

   public class BseRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 1;

      public override bool Matches(IFormattedRun run) => run is BSERun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var run = new BSERun(destination, new SortedSpan<int>(source));
         if (run.Length < 1) return false;
         if (!(token is NoDataChangeDeltaModel)) {
            owner.ClearFormat(token, run.Start, run.Length);
            owner.ObserveRunWritten(token, run);
         }
         return true;
      }

      // TODO
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new BSERun(dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         run = new BSERun(run.Start, run.PointerSources);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         token.ChangeData(owner, destination, 0x3d); // end
         return new BSERun(destination, SortedSpan.One(source));
      }
   }

   public class AseRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 1;

      public override bool Matches(IFormattedRun run) => run is ASERun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var run = new ASERun(destination, new SortedSpan<int>(source));
         if (run.Length < 1) return false;
         if (!(token is NoDataChangeDeltaModel)) {
            owner.ClearFormat(token, run.Start, run.Length);
            owner.ObserveRunWritten(token, run);
         }
         return true;
      }

      // TODO
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new ASERun(dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         run = new ASERun(run.Start, run.PointerSources);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         token.ChangeData(owner, destination, 8); // end
         return new ASERun(destination, SortedSpan.One(source));
      }
   }
}
