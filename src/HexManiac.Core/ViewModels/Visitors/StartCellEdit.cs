using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Linq;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.AsciiRun;
using static HavenSoft.HexManiac.Core.Models.Runs.BaseRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PointerRun;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   /// <summary>
   /// StartCellEdit is responsible for 2 things.
   /// (1) if Input is a valid change for MemoryLocation, Result should be true after calling the appropriate Visit() method.
   /// (2) if the new UnderEdit has something weird about it (such as being more than one space wide), create it and return it as NewFormat.
   ///     if the new UnderEdit is just the character, NewFormat can be left null, and the calling code will create the appropriate new UnderEdit object.
   /// </summary>
   public class StartCellEdit : IDataFormatVisitor {
      public static readonly char[] SpecialAnchorAllowedCharacters = new[] {
         AnchorStart,
         ArrayStart, ArrayEnd,
         '(', ')',
         StringDelimeter, StreamDelimeter,
         PointerStart, PointerEnd,
         '|', '!', '?', '-', '_', '=', '+', '*', '÷', ArrayAnchorSeparator,
         SingleByteIntegerFormat, DoubleByteIntegerFormat,
      };

      public IDataModel Model { get; }
      public int MemoryLocation { get; }
      public char Input { get; }

      public UnderEdit NewFormat { get; private set; }
      public bool Result { get; private set; }

      public StartCellEdit(IDataModel model, int memoryLocation, char input) => (Model, MemoryLocation, Input) = (model, memoryLocation, input);

      public void Visit(PCS pcs, byte data) {
         // don't let it start with a space unless it's in quotes (for copy/paste)
         var run = Model.GetNextRun(MemoryLocation);
         if (run is ITableRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(MemoryLocation);
            if (offsets.SegmentStart == MemoryLocation && Input == ' ') return;
         }
         if (run is PCSRun && run.Start == MemoryLocation && Input == ' ') return;

         Result = Input == StringDelimeter ||
            PCSString.PCS.Any(str => str != null && str.StartsWith(Input.ToString())) ||
            Model.TextConverter.AnyMacroStartsWith(Input.ToString());
      }

      public void Visit(ErrorPCS pcs, byte data) => Visit((PCS)null, data);

      public void Visit(Ascii ascii, byte data) {
         if (Input == ' ' && ascii.Position == 0) Result = false;
         else Result = true;
      }

      public void Visit(Braille braille, byte data) => Result = true;

      public void Visit(Integer intFormat, byte data) {
         var nextRun = Model.GetNextRun(MemoryLocation);
         if (Input == '+' && nextRun is LzSpriteRun spriteRun && nextRun.Start == MemoryLocation - 1 && spriteRun.Pages == 1) {
            Result = true;
            return;
         }
         if (nextRun is WordRun && nextRun.Start == MemoryLocation) {
            if (nextRun.Length == 1 && Input == '.') {
               Result = true;
               return;
            } else if (nextRun.Length == 2 && Input == ':') {
               Result = true;
               return;
            }
         }

         if (!intFormat.CanStartWithCharacter(Input)) return;

         NewFormat = new UnderEdit(intFormat, Input.ToString(), intFormat.Length, null);
         Result = true;
      }

      public void Visit(IntegerHex integerHex, byte data) {
         if (!integerHex.CanStartWithCharacter(Input)) return;

         NewFormat = new UnderEdit(integerHex, Input.ToString(), integerHex.Length, null);
         Result = true;
      }

      public void Visit(EggSection section, byte data) => VisitEgg(section);

      public void Visit(EggItem item, byte data) => VisitEgg(item);

      public void VisitEgg(IDataFormat eggFormat) {
         if (!char.IsLetterOrDigit(Input) && !$"{StringDelimeter}[".Contains(Input)) return;

         var stream = (EggMoveRun)Model.GetNextRun(MemoryLocation);
         var allOptions = stream.GetAutoCompleteOptions();
         var autocomplete = AutoCompleteSelectionItem.Generate(allOptions.Where(option => option.MatchesPartial(Input.ToString())), -1);
         NewFormat = new UnderEdit(eggFormat, Input.ToString(), 2, autocomplete);
         Result = true;
      }

      public void Visit(PlmItem item, byte data) {
         Result = char.IsDigit(Input) || Input == '[';
         if (Result) {
            var autocomplete = AutoCompleteSelectionItem.Generate(Enumerable.Empty<string>(), -1);
            NewFormat = new UnderEdit(item, Input.ToString(), 2, autocomplete);
         }
      }

      public void Visit(EndStream endStream, byte data) => Result = Input == ExtendArray || Input == '[';

      public void Visit(LzMagicIdentifier lz, byte data) => Result = char.ToLower(Input) == 'l';

      public void Visit(LzCompressed lz, byte data) => Result = char.IsDigit(Input);
   }
}

