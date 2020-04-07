using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public abstract class RunStrategy {
      public string Format { get; set; }

      public abstract int LengthForNewRun(IDataModel model, int pointerAddress);

      public ITableRun GetTable(IDataModel model, int pointerAddress) => (ITableRun)model.GetNextRun(pointerAddress);

      public ArrayRunPointerSegment GetSegment(ITableRun table, int pointerAddress) {
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
   }

   public class PLMRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 2;
   }

   public class TrainerPokemonTeamRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => new TrainerPokemonTeamRun(model, -1, new[] { pointerAdress }).Length;
   }

   public class LzSpriteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
   }

   public class LzPaletteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
   }

   public class SpriteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
   }

   public class PaletteRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException(); // figure out the needed uncompressed size from the parent table
   }

   public class TableStreamRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var tableRun = GetTable(model, pointerAddress);
         var pointerSegment = GetSegment(tableRun, pointerAddress);
         TableStreamRun.TryParseTableStream(model, -1, new int[] { pointerAddress }, pointerSegment.Name, pointerSegment.InnerFormat, tableRun.ElementContent, out var newStream);
         return newStream.Length;
      }
   }
}
