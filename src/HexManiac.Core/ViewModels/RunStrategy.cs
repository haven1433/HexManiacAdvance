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

      protected ITableRun GetTable(IDataModel model, int pointerAddress) => (ITableRun)model.GetNextRun(pointerAddress);

      protected ArrayRunPointerSegment GetSegment(ITableRun table, int pointerAddress) {
         var offsets = table.ConvertByteOffsetToArrayOffset(pointerAddress);
         return (ArrayRunPointerSegment)table.ElementContent[offsets.SegmentIndex];
      }
   }

   public class FormatRunFactory {
      public static RunStrategy GetStrategy(string format) {
         RunStrategy strategy = null;
         if (format == PCSRun.SharedFormatString) {
            strategy = new PCSRunContentStrategy();
         } else if (format == PLMRun.SharedFormatString) {
            strategy = new PLMRunContentStrategy();
         } else if (format == TrainerPokemonTeamRun.SharedFormatString) {
            strategy = new TrainerPokemonTeamRunContentStrategy();
         } else if (Models.Runs.Compressed.SpriteRun.TryParseSpriteFormat(format, out var spriteFormat)) {
            strategy = new LzSpriteRunContentStrategy();
         } else if (Models.Runs.Compressed.PaletteRun.TryParsePaletteFormat(format, out var paletteFormat)) {
            strategy = new LzPaletteRunContentStrategy();
         } else if (Models.Runs.Sprites.SpriteRun.TryParseSpriteFormat(format, out var spriteFormat1)) {
            strategy = new SpriteRunContentStrategy();
         } else if (Models.Runs.Sprites.PaletteRun.TryParsePaletteFormat(format, out var paletteFormat1)) {
            strategy = new PaletteRunContentStrategy();
         } else if (format.StartsWith("[") && format.Contains("]")) {
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
   }

   public class LzSpriteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (!SpriteRun.TryParseSpriteFormat(Format, out var spriteFormat)) return false;

         var lzRun = new SpriteRun(spriteFormat, owner, destination, new[] { source });
         if (lzRun.Length <= 5 || owner.ReadMultiByteValue(destination + 1, 3) % 32 != 0) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, lzRun);

         return true;
      }
   }

   public class LzPaletteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (!PaletteRun.TryParsePaletteFormat(Format, out var paletteFormat)) return false;

         var lzRun = new PaletteRun(paletteFormat, owner, destination, new[] { source });
         if (lzRun.Length <= 5 && owner.ReadMultiByteValue(destination + 1, 3) != Math.Pow(2, paletteFormat.Bits + 1)) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, lzRun);

         return true;
      }
   }

   public class SpriteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (!Models.Runs.Sprites.SpriteRun.TryParseSpriteFormat(Format, out var spriteFormat)) return false;
         var spriteRun = new Models.Runs.Sprites.SpriteRun(destination, spriteFormat, new[] { source });
         // TODO deal with the run being too long?
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, spriteRun);
         return true;
      }
   }

   public class PaletteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (!Models.Runs.Sprites.PaletteRun.TryParsePaletteFormat(Format, out var paletteFormat)) return false;
         var palRun = new Models.Runs.Sprites.PaletteRun(destination, paletteFormat, new[] { source });
         // TODO deal with the run being too long?
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, palRun);
         return true;
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
   }
}
