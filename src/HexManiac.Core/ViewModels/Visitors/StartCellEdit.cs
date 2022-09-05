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
      private static readonly char[] SpecialAnchorAllowedCharacters = new[] {
         AnchorStart,
         ArrayStart, ArrayEnd,
         '(', ')',
         StringDelimeter, StreamDelimeter,
         PointerStart, PointerEnd,
         '|', '!', '?', '-', '_', '=', '+', '*', ArrayAnchorSeparator,
         SingleByteIntegerFormat, DoubleByteIntegerFormat,
      };

      public IDataModel Model { get; }
      public int MemoryLocation { get; }
      public char Input { get; }

      public UnderEdit NewFormat { get; private set; }
      public bool Result { get; private set; }

      public StartCellEdit(IDataModel model, int memoryLocation, char input) => (Model, MemoryLocation, Input) = (model, memoryLocation, input);

      // Undefined edits happen when you try to edit the byte after the end of the file.
      // Treat it the same as a None.
      public void Visit(Undefined dataFormat, byte data) => Visit((None)null, data);

      public void Visit(None dataFormat, byte data) => BasicVisit(dataFormat, data);

      private void BasicVisit(IDataFormat dataFormat, byte data) {
         // you can write a pointer into a space with no current format
         if (Input == PointerStart) {
            var editText = Input.ToString();
            var autocompleteOptions = Model.GetNewPointerAutocompleteOptions(editText, -1);
            NewFormat = new UnderEdit(dataFormat, editText, 4, autocompleteOptions);
            Result = true;
            return;
         }

         if (Input == ':') {
            var editText = Input.ToString();
            var autocompleteOptions = Model.GetNewWordAutocompleteOptions("::", -1);
            NewFormat = new UnderEdit(dataFormat, editText, 4, autocompleteOptions);
            Result = true;
            return;
         }

         if (Input == '.') {
            var editText = Input.ToString();
            var autocompleteOptions = Model.GetNewWordAutocompleteOptions(".", -1);
            NewFormat = new UnderEdit(dataFormat, editText, 4, autocompleteOptions);
            Result = true;
            return;
         }

         Result = ViewPort.AllHexCharacters.Contains(Input);
      }

      // we were asked to start an edit, but there's already an edit under way!
      // just continue the existing edit.
      public void Visit(UnderEdit underEdit, byte data) {
         // handle special cases of "anywhere" formats first
         if (underEdit.CurrentText.StartsWith(ViewPort.GotoMarker.ToString())) {
            Result = char.IsLetterOrDigit(Input) || char.IsWhiteSpace(Input);
            Result |= Input.IsAny($"{ArrayAnchorSeparator}{ViewPort.CommandMarker}()~=._+-".ToCharArray());
            if (underEdit.CurrentText.Length == 1) Result |= Input.IsAny('{', '}');
            return;
         } else if (underEdit.CurrentText.StartsWith(AnchorStart.ToString())) {
            Result =
               char.IsLetterOrDigit(Input) ||
               char.IsWhiteSpace(Input) ||
               SpecialAnchorAllowedCharacters.Contains(Input);
            return;
         } else if (underEdit.CurrentText.StartsWith(ViewPort.DirectiveMarker.ToString())
         && underEdit.CurrentText.Count(c => c == ViewPort.DirectiveMarker) == 1) {
            Result = true;
            return;
         } else if (underEdit.CurrentText.StartsWith(ViewPort.CommentStart.ToString())) {
            Result = true;
            return;
         }

         // use the ContinueCellEdit class to continue the edit
         var editor = new ContinueCellEdit(Model, Input, underEdit);
         (underEdit.OriginalFormat ?? Undefined.Instance).Visit(editor, data);
         Result = editor.Result;
      }

      public void Visit(Pointer pointer, byte data) {
         if (Input == ':') { BasicVisit(pointer, data); return; }
         if (Input != PointerStart && !char.IsLetterOrDigit(Input)) return;

         var editText = Input.ToString();
         // if the user tries to edit the pointer but forgets the opening bracket, add it for them.
         if (Input != PointerStart) editText = PointerStart + editText;
         var autocompleteOptions = Model.GetNewPointerAutocompleteOptions(editText, -1);
         NewFormat = new UnderEdit(pointer, editText, 4, autocompleteOptions);
         Result = true;
      }

      public void Visit(Anchor anchor, byte data) {
         var innerFormat = anchor.OriginalFormat;
         innerFormat.Visit(this, data);
         if (NewFormat != null && NewFormat.OriginalFormat == innerFormat) NewFormat = new UnderEdit(anchor, NewFormat.CurrentText, NewFormat.EditWidth, NewFormat.AutocompleteOptions);
      }

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) {
         if (Input == '[') {
            Result = true;
         } else {
            decorator.OriginalFormat.Visit(this, data);
         }
      }

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

      public void Visit(EscapedPCS pcs, byte data) {
         Result = ViewPort.AllHexCharacters.Contains(Input);
      }

      public void Visit(ErrorPCS pcs, byte data) => Visit((PCS)null, data);

      public void Visit(Ascii ascii, byte data) => Result = true;

      public void Visit(Braille braille, byte data) => Result = true;

      public void Visit(Integer intFormat, byte data) {
         if (Input == '+' && Model.GetNextRun(MemoryLocation) is LzSpriteRun spriteRun && spriteRun.Start == MemoryLocation - 1 && spriteRun.Pages == 1) {
            Result = true;
            return;
         }

         if (!intFormat.CanStartWithCharacter(Input)) return;

         NewFormat = new UnderEdit(intFormat, Input.ToString(), intFormat.Length, null);
         Result = true;
      }

      public void Visit(IntegerEnum integer, byte data) {
         if (!integer.CanStartWithCharacter(Input)) return;

         var autocompleteVisitor = new AutocompleteCell(Model, Input.ToString(), MemoryLocation);
         integer.Visit(autocompleteVisitor, data);
         var autocomplete = autocompleteVisitor.Result;

         NewFormat = new UnderEdit(integer, Input.ToString(), integer.Length, autocomplete);
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

      public void Visit(BitArray array, byte data) {
         Result = char.IsLetterOrDigit(Input) || Input.IsAny('"', '-', '/');

         if (Result) {
            var autocompleteVisitor = new AutocompleteCell(Model, Input.ToString(), MemoryLocation);
            array.Visit(autocompleteVisitor, data);
            var autocomplete = autocompleteVisitor.Result;

            NewFormat = new UnderEdit(array, Input.ToString(), array.Length, autocomplete);
         }
      }

      public void Visit(MatchedWord word, byte data) => BasicVisit(word, data);

      public void Visit(EndStream endStream, byte data) => Result = Input == ExtendArray || Input == '[';

      public void Visit(LzMagicIdentifier lz, byte data) => Result = char.ToLower(Input) == 'l';

      public void Visit(LzGroupHeader lz, byte data) => Result = ViewPort.AllHexCharacters.Contains(Input);

      public void Visit(LzCompressed lz, byte data) => Result = char.IsDigit(Input);

      public void Visit(LzUncompressed lz, byte data) {
         if (Input == '+' && Model.GetNextRun(MemoryLocation) is SpriteRun spriteRun && spriteRun.Start == MemoryLocation) {
            Result = true;
            return;
         }

         Result = ViewPort.AllHexCharacters.Contains(Input);
      }

      public void Visit(UncompressedPaletteColor color, byte data) {
         Result = ViewPort.AllHexCharacters.Contains(Input);
         if (Result) NewFormat = new UnderEdit(color, Input.ToString(), 2);
      }

      public void Visit(Tuple tuple, byte data) {
         if (" +,".Contains(Input)) {
            Result = false;
            return;
         }

         Result = true;
         var autocompleteVisitor = new AutocompleteCell(Model, Input.ToString(), MemoryLocation);
         tuple.Visit(autocompleteVisitor, data);
         var autocomplete = autocompleteVisitor.Result;
         NewFormat = new UnderEdit(tuple, Input.ToString(), tuple.Length, autocomplete);
      }
   }
}

