using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Compressed;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public abstract class RunStrategy {
      public string Format { get; set; }

      public abstract int LengthForNewRun(IDataModel model, int pointerAddress);

      /// <summary>
      /// Returns true if the format is capable of being added for the pointer at source.
      /// If the token is such that edits are allowed, actually add the format.
      /// </summary>
      public abstract bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string Name, IReadOnlyList<ArrayRunElementSegment> sourceSegments);

      public abstract bool Matches(IFormattedRun run);

      public abstract IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments);

      public abstract void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run);

      protected ITableRun GetTable(IDataModel model, int pointerAddress) => (ITableRun)model.GetNextRun(pointerAddress);

      protected ArrayRunPointerSegment GetSegment(ITableRun table, int pointerAddress) {
         var offsets = table.ConvertByteOffsetToArrayOffset(pointerAddress);
         return (ArrayRunPointerSegment)table.ElementContent[offsets.SegmentIndex];
      }

      public abstract ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run);
   }

   public class FormatRunFactory {
      public static RunStrategy GetStrategy(string format) {
         RunStrategy strategy = null;
         if (format == PCSRun.SharedFormatString) {
            strategy = new PCSRunContentStrategy();
         } else if (format.StartsWith(AsciiRun.SharedFormatString)) {
            strategy = new AsciiRunContentStrategy();
         } else if (format == EggMoveRun.SharedFormatString) {
            strategy = new EggRunContentStrategy();
         } else if (format == PLMRun.SharedFormatString) {
            strategy = new PLMRunContentStrategy();
         } else if (format == TrainerPokemonTeamRun.SharedFormatString) {
            strategy = new TrainerPokemonTeamRunContentStrategy();
         } else if (Models.Runs.Compressed.SpriteRun.TryParseSpriteFormat(format, out var spriteFormat)) {
            strategy = new LzSpriteRunContentStrategy(spriteFormat);
         } else if (Models.Runs.Compressed.PaletteRun.TryParsePaletteFormat(format, out var paletteFormat)) {
            strategy = new LzPaletteRunContentStrategy(paletteFormat);
         } else if (Models.Runs.Sprites.SpriteRun.TryParseSpriteFormat(format, out var spriteFormat1)) {
            strategy = new SpriteRunContentStrategy(spriteFormat1);
         } else if (Models.Runs.Sprites.PaletteRun.TryParsePaletteFormat(format, out var paletteFormat1)) {
            strategy = new PaletteRunContentStrategy(paletteFormat1);
         } else if (format.IndexOf("[") >= 0 && format.IndexOf("[") < format.IndexOf("]")) {
            strategy = new TableStreamRunContentStrategy();
         } else {
            Debug.Fail("Not Implemented!");
         }

         strategy.Format = format;
         return strategy;
      }
   }

   public class PCSRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => 1;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var length = PCSString.ReadString(owner, destination, true);

         if (length < 1) return false;

         // our token will be a no-change token if we're in the middle of exploring the data.
         // If so, don't actually add the run. It's enough to know that we _can_ add the run.
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, new PCSRun(owner, destination, length));

         // even if we didn't add the format, we're _capable_ of adding it... so return true
         return true;
      }
      public override bool Matches(IFormattedRun run) => run is PCSRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         // found freespace, so this should already be an FF. Just add the format.
         return new PCSRun(owner, destination, 1);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var length = PCSString.ReadString(model, run.Start, true);
         if (length > 0) {
            var newRun = new PCSRun(model, run.Start, length, run.PointerSources);
            if (!newRun.Equals(run)) model.ClearFormat(token, newRun.Start, newRun.Length);
            run = newRun;
         }
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var length = PCSString.ReadString(model, dataIndex, true);
         if (length < 0) {
            return new ErrorInfo($"Format was specified as a string, but no string was recognized.");
         } else if (PokemonModel.SpanContainsAnchor(model, dataIndex, length)) {
            return new ErrorInfo($"Format was specified as a string, but a string would overlap the next anchor.");
         }
         run = new PCSRun(model, dataIndex, length);

         return ErrorInfo.NoError;
      }
   }

   public class AsciiRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException();

      public override bool Matches(IFormattedRun run) => throw new NotImplementedException();

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string Name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) => throw new NotImplementedException();

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) => throw new NotImplementedException();

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) => throw new NotImplementedException();

      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         if (int.TryParse(Format.Substring(5), out var length)) {
            run = new AsciiRun(dataIndex, length);
         } else {
            return new ErrorInfo($"Ascii runs must include a length.");
         }

         return ErrorInfo.NoError;
      }
   }

   public class EggRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException();

      public override bool Matches(IFormattedRun run) => throw new NotImplementedException();

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string Name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) => throw new NotImplementedException();

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) => throw new NotImplementedException();

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) => throw new NotImplementedException();

      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new EggMoveRun(model, dataIndex);
         return ErrorInfo.NoError;
      }
   }

   public class PLMRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 2;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var plmRun = new PLMRun(owner, destination);
         var length = plmRun.Length;
         if (length < 2) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, plmRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is PLMRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         // PLM ends with FFFF, and this is already freespace, so just add the format.
         return new PLMRun(owner, destination);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new PLMRun(model, run.Start);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new PLMRun(model, dataIndex);
         if (run.Length == 0) return new ErrorInfo("Format specified was for pokemon level-up move data, but could not parse that location as level-up move data.");
         return ErrorInfo.NoError;
      }
   }

   public class TrainerPokemonTeamRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => new TrainerPokemonTeamRun(model, -1, new[] { pointerAdress }).Length;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var teamRun = new TrainerPokemonTeamRun(owner, destination, new[] { source });
         var length = teamRun.Length;
         if (length < 2) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, teamRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is TrainerPokemonTeamRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         return new TrainerPokemonTeamRun(owner, destination, new[] { source }).DeserializeRun("0 ???", token);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new TrainerPokemonTeamRun(model, run.Start, run.PointerSources);
         model.ClearFormat(token, run.Start, runAttempt.Length);
         run = runAttempt;
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new TrainerPokemonTeamRun(model, dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }
   }

   public class LzSpriteRunContentStrategy : RunStrategy {
      readonly SpriteFormat spriteFormat;
      public LzSpriteRunContentStrategy(SpriteFormat spriteFormat) => this.spriteFormat = spriteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var lzRun = new SpriteRun(spriteFormat, owner, destination, new[] { source });
         if (lzRun.Length <= 5 || owner.ReadMultiByteValue(destination + 1, 3) % 32 != 0) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, lzRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is SpriteRun spriteRun && spriteRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         throw new NotImplementedException();
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new Models.Runs.Compressed.SpriteRun(spriteFormat, model, run.Start, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new Models.Runs.Compressed.SpriteRun(spriteFormat, model, dataIndex, run.PointerSources);
         if (run.Length < 6) return new ErrorInfo($"Compressed data needs to be at least {spriteFormat.ExpectedByteLength} when decompressed, but was too short.");
         return ErrorInfo.NoError;
      }
   }

   public class LzPaletteRunContentStrategy : RunStrategy {
      private readonly PaletteFormat paletteFormat;
      public LzPaletteRunContentStrategy(PaletteFormat paletteFormat) => this.paletteFormat = paletteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var lzRun = new PaletteRun(paletteFormat, owner, destination, new[] { source });
         if (lzRun.Length <= 5 && owner.ReadMultiByteValue(destination + 1, 3) != Math.Pow(2, paletteFormat.Bits + 1)) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, lzRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is PaletteRun palRun && palRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         throw new NotImplementedException();
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new Models.Runs.Compressed.PaletteRun(paletteFormat, model, run.Start, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new Models.Runs.Compressed.PaletteRun(paletteFormat, model, dataIndex, run.PointerSources);
         if (run.Length < 6) return new ErrorInfo($"Compressed data needs to be at least {(int)Math.Pow(2, paletteFormat.Bits + 1)} bytes when decompressed, but was too short.");
         return ErrorInfo.NoError;
      }
   }

   public class SpriteRunContentStrategy : RunStrategy {
      private readonly SpriteFormat spriteFormat;
      public SpriteRunContentStrategy(SpriteFormat spriteFormat) => this.spriteFormat = spriteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var spriteRun = new Models.Runs.Sprites.SpriteRun(destination, spriteFormat, new[] { source });
         // TODO deal with the run being too long?
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, spriteRun);
         return true;
      }
      public override bool Matches(IFormattedRun run) => run is Models.Runs.Sprites.SpriteRun spriteRun && spriteRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         throw new NotImplementedException();
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new Models.Runs.Sprites.SpriteRun(run.Start, spriteFormat, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new Models.Runs.Sprites.SpriteRun(dataIndex, spriteFormat, run.PointerSources);
         return ErrorInfo.NoError;
      }
   }

   public class PaletteRunContentStrategy : RunStrategy {
      private readonly PaletteFormat paletteFormat;
      public PaletteRunContentStrategy(PaletteFormat paletteFormat) => this.paletteFormat = paletteFormat;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var palRun = new Models.Runs.Sprites.PaletteRun(destination, paletteFormat, new[] { source });
         // TODO deal with the run being too long?
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, palRun);
         return true;
      }
      public override bool Matches(IFormattedRun run) => run is Models.Runs.Sprites.PaletteRun palRun && palRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         throw new NotImplementedException();
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new Models.Runs.Sprites.PaletteRun(run.Start, paletteFormat, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new Models.Runs.Sprites.PaletteRun(dataIndex, paletteFormat, run.PointerSources);
         return ErrorInfo.NoError;
      }
   }

   public class TableStreamRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var tableRun = GetTable(model, pointerAddress);
         var pointerSegment = GetSegment(tableRun, pointerAddress);
         TableStreamRun.TryParseTableStream(model, -1, new int[] { pointerAddress }, pointerSegment.Name, pointerSegment.InnerFormat, tableRun.ElementContent, out var newStream);
         return newStream.Length;
      }
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (TableStreamRun.TryParseTableStream(owner, destination, new[] { source }, name, Format, sourceSegments, out var tsRun)) {
            if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, tsRun);
            return true;
         }
         return false;
      }
      public override bool Matches(IFormattedRun run) => run is TableStreamRun streamRun && streamRun.FormatString == Format;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         // don't bother checking the TryParse result: we very much expect that the data originally in the run won't fit the parse.
         TableStreamRun.TryParseTableStream(owner, destination, new[] { source }, name, Format, sourceSegments, out var tableStream);
         return tableStream.DeserializeRun("", token);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         if (!TableStreamRun.TryParseTableStream(model, run.Start, run.PointerSources, name, Format, null, out var runAttempt)) return;
         model.ClearFormat(token, run.Start, runAttempt.Length);
         run = runAttempt;
      }
      public override ErrorInfo TryParseFormat(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var errorInfo = ArrayRun.TryParse(model, name, Format, dataIndex, null, out var arrayRun);
         if (errorInfo == ErrorInfo.NoError) {
            run = arrayRun;
         } else if (Format != string.Empty) {
            return new ErrorInfo($"Format {Format} was not understood.");
         }

         return ErrorInfo.NoError;
      }
   }
}
