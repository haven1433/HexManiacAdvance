using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PointerRun;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   /// <summary>
   /// An an existing UnderEdit element is trying to be edited.
   /// This object just needs to set Result to true if the Input is valid for the given cell.
   /// </summary>
   public class ContinueCellEdit : IDataFormatVisitor {
      private IDataModel Model { get; }

      private char Input { get; }
      private UnderEdit UnderEdit { get; }

      public bool Result { get; private set; }

      public ContinueCellEdit(IDataModel model, char input, UnderEdit underEdit) => (Model, Input, UnderEdit) = (model, input, underEdit);

      public void Visit(Undefined dataFormat, byte data) => Visit((None)null, data);

      public void Visit(None dataFormat, byte data) {
         if (UnderEdit.CurrentText.Length > 0) {
            if (UnderEdit.CurrentText[0] == PointerStart) {
               Result = char.IsLetterOrDigit(Input) || Input.IsAny("/> .+-_".ToCharArray());
               return;
            }

            if (UnderEdit.CurrentText[0] == ':') {
               if (UnderEdit.CurrentText.Length == 1) {
                  Result = char.IsLetterOrDigit(Input) || Input.IsAny(' ', '.', ':');
               } else {
                  Result = char.IsLetterOrDigit(Input) || Input.IsAny(' ', '.');
               }
               return;
            }

            if (UnderEdit.CurrentText[0] == '.') {
               Result = char.IsLetterOrDigit(Input) || Input.IsAny(" .+-=".ToCharArray());
               return;
            }
         }

         Result = ViewPort.AllHexCharacters.Contains(Input);
      }

      // 'ContinueCellEdit' is expected to be passed the innerformat. It should never get an UnderEdit cell.
      // The UnderEdit object that's currently being edited was passed in separately to the constructor.
      public void Visit(UnderEdit dataFormat, byte data) => throw new NotImplementedException();

      // an in-process pointer edit acts the same way as an in-process None edit.
      public void Visit(Pointer pointer, byte data) => Visit((None)null, data);

      public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) {
         if (UnderEdit.CurrentText == "[" && Input == ']') {
            Result = true;
         } else {
            decorator.OriginalFormat.Visit(this, data);
         }
      }

      public void Visit(PCS pcs, byte data) {
         if (Input == StringDelimeter) { Result = true; return; }
         var currentText = UnderEdit.CurrentText;

         // if this is the start of a text segment, crop off the leading " before trying to convert to a byte
         if (pcs.Position == 0 && currentText[0] == StringDelimeter) currentText = currentText.Substring(1);

         Result = PCSString.PCS.Any(str => str != null && str.StartsWith(currentText + Input)) ||
            Model.TextConverter.AnyMacroStartsWith(currentText + Input);
      }

      public void Visit(EscapedPCS pcs, byte data) {
         Result = ViewPort.AllHexCharacters.Contains(Input);
      }

      public void Visit(ErrorPCS pcs, byte data) {
         throw new NotImplementedException();
      }

      // not possible: all ascii edits are single-stroke
      public void Visit(Ascii ascii, byte data) => throw new NotImplementedException();

      public void Visit(Braille braille, byte data) => throw new NotImplementedException();

      public void Visit(Integer integer, byte data) {
         Result = integer.CanStartWithCharacter(Input) || char.IsWhiteSpace(Input) || Input == ')';
      }

      public void Visit(IntegerEnum integer, byte data) {
         Result = integer.CanStartWithCharacter(Input) ||
            ".'~|,_&%)".Contains(Input) ||
            char.IsWhiteSpace(Input);
      }

      public void Visit(IntegerHex integerHex, byte data) => Visit((Integer)integerHex, data);

      public void Visit(EggSection section, byte data) => VisitEgg();
      public void Visit(EggItem item, byte data) => VisitEgg();
      public void VisitEgg() {
         var specialCharacters = ". '-\\"; // mr. mime, farfetch'd, double-edge, nidoran
         if (UnderEdit.CurrentText.StartsWith(EggMoveRun.GroupStart)) specialCharacters += ']';
         if (UnderEdit.CurrentText.StartsWith(StringDelimeter.ToString())) specialCharacters += StringDelimeter;
         Result =
            char.IsLetterOrDigit(Input) ||
            specialCharacters.Contains(Input) ||
            char.IsWhiteSpace(Input);
      }

      public void Visit(PlmItem item, byte data) {
         var specialCharacters = " -"; // double-edge, Vine Whip

         if (UnderEdit.CurrentText == "[") {
            Result = Input == ']';
         } else if (!UnderEdit.CurrentText.Contains(" ")) {
            // before the space, only numbers are allowed
            Result = char.IsDigit(Input) || Input == ' ';
         } else if (UnderEdit.CurrentText.EndsWith(" ") && !UnderEdit.CurrentText.Contains(StringDelimeter)) {
            // directly after the space, can be a quote, a letter, or a digit.
            Result = Input == StringDelimeter || char.IsLetterOrDigit(Input);
         } else if (UnderEdit.CurrentText.Contains(StringDelimeter)) {
            // if there is a quote, accept lots
            Result = Input == StringDelimeter || char.IsLetterOrDigit(Input) || specialCharacters.Contains(Input);
         } else {
            // if there is no quote, accept lots, but not a quote
            Result = Input == StringDelimeter || char.IsLetterOrDigit(Input) || specialCharacters.Contains(Input);
         }
      }

      public void Visit(BitArray array, byte data) {
         Result = char.IsLetterOrDigit(Input) || Input.IsAny('"', '-', ' ');
      }

      public void Visit(MatchedWord word, byte data) => Visit((None)null, data);

      public void Visit(EndStream endStream, byte data) {
         Result = "[]+".Contains(Input);
      }

      public void Visit(LzMagicIdentifier lz, byte data) => Result = char.ToLower(Input) == 'z';

      public void Visit(LzGroupHeader lz, byte data) => Result = ViewPort.AllHexCharacters.Contains(Input) || char.IsWhiteSpace(Input);

      public void Visit(LzCompressed lz, byte data) => Result = char.IsDigit(Input) || Input == ':' || char.IsWhiteSpace(Input);

      public void Visit(LzUncompressed lz, byte data) => Result = ViewPort.AllHexCharacters.Contains(Input) || char.IsWhiteSpace(Input);

      public void Visit(UncompressedPaletteColor color, byte data) {
         Result = ViewPort.AllHexCharacters.Contains(Input) || Input.IsAny(':', ' ');
      }

      public void Visit(DataFormats.Tuple tuple, byte data) => Result = true;
   }
}
