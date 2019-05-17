using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PointerRun;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   internal class CompleteEditOperation : IDataFormatVisitor {
      private readonly IDataModel Model;
      private readonly int memoryLocation;
      private readonly string CurrentText;
      private readonly ModelDelta CurrentChange;

      public bool Result { get; private set; }         // if true, the edit was completed correctly
      public int NewDataIndex { get; private set; }    // for completed edits, where should the selection move to?
      public bool DataMoved { get; private set; }
      public string MessageText { get; private set; }  // is there a message to display to the user? For example, when data gets moved.
      public string ErrorText { get; private set; }    // is there an error to display to the user? For example, invalid pointer
      public HexElement NewCell { get; private set; }  // if result is true and this is not null, assign this one value back to the one cell
                                                       // and refresh the one cell (along with any other UnderEdit cells)
                                                       // if result is true and this _is_ null, then the entire screen needs to be refreshed.

      public CompleteEditOperation(IDataModel model, int memoryLocation, string currentText, ModelDelta currentChange) {
         Model = model;
         this.memoryLocation = memoryLocation;
         CurrentText = currentText;
         CurrentChange = currentChange;

         NewDataIndex = memoryLocation;
      }

      public void Visit(Undefined dataFormat, byte data) => Visit((None)null, data);

      public void Visit(None dataFormat, byte data) {
         if (CurrentText.StartsWith(PointerStart.ToString())) {
            if (CurrentText.Last() != PointerEnd && CurrentText.Last() != ' ') return;
            CompletePointerEdit();
            Result = true;
         } else {
            if (CurrentText.Length < 2) return;
            CompleteHexEdit(CurrentText);
            Result = true;
         }
      }

      public void Visit(UnderEdit dataFormat, byte data) => throw new NotImplementedException();

      public void Visit(Pointer pointer, byte data) {
         var run = Model.GetNextRun(memoryLocation);
         if (run is ArrayRun && CurrentText[0] != PointerStart) {
            ErrorText = "Pointers in tables cannot be removed without removing the table.";
            return;
         }

         Visit((None)null, data);
      }

      public void Visit(Anchor anchor, byte data) {
         anchor.OriginalFormat.Visit(this, data);
         if (NewCell != null) NewCell = new HexElement(NewCell.Value, new Anchor(NewCell.Format, anchor.Name, anchor.Format, anchor.Sources));
      }

      public void Visit(PCS pcs, byte data) {
         var currentText = CurrentText;
         if (currentText.StartsWith(StringDelimeter.ToString())) currentText = currentText.Substring(1);
         if (pcs.Position != 0 && CurrentText == StringDelimeter.ToString()) {
            CompleteStringEdit();
            Result = true;
         } else if (PCSString.PCS.Any(str => str == currentText)) {
            CompleteCharacterEdit(pcs);
            Result = true;
         }
      }

      public void Visit(EscapedPCS pcs, byte data) {
         if (CurrentText.Length < 2) return;
         CompleteCharacterEdit(pcs);
         Result = true;
      }

      public void Visit(ErrorPCS pcs, byte data) => throw new NotImplementedException();

      public void Visit(Ascii ascii, byte data) {
         CompleteAsciiEdit(ascii);
         Result = true;
      }

      public void Visit(Integer integer, byte data) {
         if (char.IsWhiteSpace(CurrentText.Last())) {
            CompleteIntegerEdit(integer);
            Result = true;
         }
      }

      public void Visit(IntegerEnum integer, byte data) {
         // must end in whitespace or must have matching quotation marks (ex. "Mr. Mime")
         var quoteCount = CurrentText.Count(c => c == '"');

         if (quoteCount == 0 && char.IsWhiteSpace(CurrentText.Last())) {
            CompleteIntegerEnumEdit(integer);
            Result = true;
         } else if (quoteCount == 2) {
            CompleteIntegerEnumEdit(integer);
            Result = true;
         }
      }

      public void Visit(EggSection section, byte data) => CompleteEggEdit();

      public void Visit(EggItem item, byte data) => CompleteEggEdit();

      private void CompleteIntegerEdit(Integer integer) {
         if (!int.TryParse(CurrentText, out var result)) {
            ErrorText = $"Could not parse {CurrentText} as a number";
            return;
         }

         var run = (ArrayRun)Model.GetNextRun(memoryLocation);
         var offsets = run.ConvertByteOffsetToArrayOffset(memoryLocation);
         int length = run.ElementContent[offsets.SegmentIndex].Length;
         for (int i = 0; i < length; i++) {
            CurrentChange.ChangeData(Model, offsets.SegmentStart + i, (byte)result);
            result /= 0x100;
         }

         if (result != 0) ErrorText = $"Warning: number was too big to fit in the available space.";
         NewDataIndex = offsets.SegmentStart + length;
      }

      private void CompleteIntegerEnumEdit(IntegerEnum integer) {
         var array = (ArrayRun)Model.GetNextRun(memoryLocation);
         var offsets = array.ConvertByteOffsetToArrayOffset(memoryLocation);
         var segment = (ArrayRunEnumSegment)array.ElementContent[offsets.SegmentIndex];
         if (segment.TryParse(Model, CurrentText, out int value)) {
            Model.WriteMultiByteValue(offsets.SegmentStart, segment.Length, CurrentChange, value);
            NewDataIndex = offsets.SegmentStart + segment.Length;
         } else {
            ErrorText = $"Could not parse {CurrentText}as an enum from the {segment.EnumName} array";
         }
      }

      private void CompleteAsciiEdit(Ascii asciiFormat) {
         var content = (byte)CurrentText[0];

         CurrentChange.ChangeData(Model, memoryLocation, content);
         NewCell = new HexElement(content, new Ascii(asciiFormat.Source, asciiFormat.Position, CurrentText[0]));
         NewDataIndex = memoryLocation + 1;
      }

      private void CompletePointerEdit() {
         // if they just started a pointer and then clicked off, there's nothing to complete
         if (CurrentText == PointerStart + " ") return;

         var destination = CurrentText.Substring(1, CurrentText.Length - 2);

         if (destination.Length == 2 && destination.All(ViewPort.AllHexCharacters.Contains)) { CompleteHexEdit(destination); return; }

         Model.ExpandData(CurrentChange, memoryLocation + 3);

         var currentRun = Model.GetNextRun(memoryLocation);
         bool inArray = currentRun.Start <= memoryLocation && currentRun is ArrayRun;
         var sources = currentRun.PointerSources;

         if (!inArray) {
            if (destination != string.Empty) {
               Model.ClearFormatAndData(CurrentChange, memoryLocation, 4);
               sources = null;
            } else if (!(currentRun is NoInfoRun)) {
               Model.ClearFormat(CurrentChange, memoryLocation, 4);
               sources = null;
            }
         }

         int fullValue;
         if (destination == string.Empty) {
            fullValue = Model.ReadPointer(memoryLocation);
         } else if (destination.All(ViewPort.AllHexCharacters.Contains) && destination.Length <= 7) {
            while (destination.Length < 6) destination = "0" + destination;
            fullValue = int.Parse(destination, NumberStyles.HexNumber);
         } else {
            fullValue = Model.GetAddressFromAnchor(CurrentChange, memoryLocation, destination);
         }

         if (fullValue == Pointer.NULL || (0 <= fullValue && fullValue < Model.Count)) {
            if (inArray) {
               Model.UpdateArrayPointer(CurrentChange, memoryLocation, fullValue);
               // Tools.Schedule(Tools.TableTool.DataForCurrentRunChanged);
            } else {
               Model.WritePointer(CurrentChange, memoryLocation, fullValue);
               Model.ObserveRunWritten(CurrentChange, new PointerRun(memoryLocation, sources));
            }

            NewDataIndex = memoryLocation + 4;
         } else {
            ErrorText = $"Address {fullValue.ToString("X2")} is not within the data.";
         }
      }

      private void CompleteStringEdit() {
         int memoryLocation = this.memoryLocation;

         // all the bytes are already correct, just move to the next space
         var run = Model.GetNextRun(memoryLocation);
         if (run is PCSRun pcsRun) {
            while (run.Start + run.Length > memoryLocation) {
               CurrentChange.ChangeData(Model, memoryLocation, 0xFF);
               memoryLocation++;
               NewDataIndex = memoryLocation;
               var newRunLength = PCSString.ReadString(Model, run.Start, true);
               Model.ObserveRunWritten(CurrentChange, new PCSRun(run.Start, newRunLength, run.PointerSources));
            }
         } else if (run is ArrayRun arrayRun) {
            var offsets = arrayRun.ConvertByteOffsetToArrayOffset(memoryLocation);
            CurrentChange.ChangeData(Model, memoryLocation, 0xFF);
            memoryLocation++;
            NewDataIndex = memoryLocation;
            while (offsets.SegmentStart + arrayRun.ElementContent[offsets.SegmentIndex].Length > memoryLocation) {
               CurrentChange.ChangeData(Model, memoryLocation, 0x00);
               memoryLocation++;
               NewDataIndex = memoryLocation;
            }
         }
      }

      private void CompleteCharacterEdit(IDataFormat originalFormat) {
         var editText = CurrentText;
         if (editText.StartsWith("\"")) editText = editText.Substring(1);
         var pcs = originalFormat as PCS;
         var escaped = originalFormat as EscapedPCS;
         var run = Model.GetNextRun(memoryLocation);

         var byteValue = escaped != null ?
            byte.Parse(CurrentText, NumberStyles.HexNumber) :
            (byte)Enumerable.Range(0, 0x100).First(i => PCSString.PCS[i] == editText);

         var position = pcs != null ? pcs.Position : escaped.Position;
         HandleLastCharacterChange(memoryLocation, editText, pcs, run, position, byteValue);
      }

      private void HandleLastCharacterChange(int memoryLocation, string editText, PCS pcs, IFormattedRun run, int position, byte byteValue) {
         if (run is PCSRun) {
            // if its the last character being edited on a normal string, try to expand
            if (run.Length == position + 1) {
               int extraBytesNeeded = editText == "\\\\" ? 2 : 1;
               // last character edit: might require relocation
               var newRun = Model.RelocateForExpansion(CurrentChange, run, run.Length + extraBytesNeeded);
               if (newRun != run) {
                  MessageText = $"Text was automatically moved to {newRun.Start.ToString("X6")}. Pointers were updated.";
                  memoryLocation += newRun.Start - run.Start;
                  run = newRun;
                  DataMoved = true;
               }

               CurrentChange.ChangeData(Model, memoryLocation + 1, 0xFF);
               if (editText == "\\\\") CurrentChange.ChangeData(Model, memoryLocation + 2, 0xFF);
               run = new PCSRun(run.Start, run.Length + extraBytesNeeded, run.PointerSources);
               Model.ObserveRunWritten(CurrentChange, run);
            }
         } else if (run is ArrayRun arrayRun) {
            // if the last characet is being edited for an array, truncate
            var offsets = arrayRun.ConvertByteOffsetToArrayOffset(memoryLocation);
            if (arrayRun.ElementContent[offsets.SegmentIndex].Length == position + 1) {
               memoryLocation--; // move back one byte and edit that one instead
            } else if (Model[memoryLocation] == 0xFF) {
               CurrentChange.ChangeData(Model, memoryLocation + 1, 0xFF); // overwrote the closing ", so add a new one after (since there's room)
            }
         } else {
            Debug.Fail("Why are we completing a character edit on something other than a PCSRun or an Array?");
         }

         CurrentChange.ChangeData(Model, memoryLocation, byteValue);
         NewDataIndex = memoryLocation + 1;
      }

      private void CompleteHexEdit(string currentText) {
         var byteValue = byte.Parse(currentText, NumberStyles.HexNumber);
         var run = Model.GetNextRun(memoryLocation);
         if (!(run is NoInfoRun) || run.Start != memoryLocation) Model.ClearFormat(CurrentChange, memoryLocation, 1);
         CurrentChange.ChangeData(Model, memoryLocation, byteValue);
         NewDataIndex = memoryLocation + 1;
      }

      private void CompleteEggEdit() {
         var endChar = CurrentText[CurrentText.Length - 1];
         if ($"] {StringDelimeter}".All(c => endChar != c)) return;
         if (CurrentText.Count(c => c == StringDelimeter) % 2 != 0) return;

         NewDataIndex = memoryLocation + 2;
         Result = true;
         var run = (EggMoveRun)Model.GetNextRun(memoryLocation);

         if (CurrentText == "[]") {
            Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, 0xFFFF);
            // clear all data after this and shorten the run
            for (int i = memoryLocation + 2; i < run.Start + run.Length; i += 2) {
               Model.WriteMultiByteValue(i, 2, CurrentChange, 0xFFFF);
            }
            var newRun = new EggMoveRun(Model, run.Start);
            Model.ObserveRunWritten(CurrentChange, newRun);
            newRun = (EggMoveRun)Model.GetNextRun(newRun.Start);
            newRun.UpdateLimiter(CurrentChange);
         } else if (CurrentText.EndsWith("]")) {
            var value = run.GetPokemonNumber(CurrentText);
            if (value == -1) {
               ErrorText = $"Could not parse {CurrentText} as a pokemon name";
               NewDataIndex -= 2;
            } else {
               WriteNormalEggEdit(run, value + EggMoveRun.MagicNumber);
            }
         } else {
            var text = CurrentText.Trim();
            var value = run.GetMoveNumber(text);
            if (value == -1) {
               // wasn't a move... try again as a pokemon even though they didn't use the []
               value = run.GetPokemonNumber(text);
               if (value == -1) {
                  ErrorText = $"Could not parse {text} as a move name or pokemon name";
                  NewDataIndex -= 2;
               } else {
                  WriteNormalEggEdit(run, value + EggMoveRun.MagicNumber);
               }
            } else {
               WriteNormalEggEdit(run, value);
            }
         }
      }

      /// <summary>
      /// Before we write this change to the model, see if we need to extend the egg run to make it fit.
      /// </summary>
      private void WriteNormalEggEdit(EggMoveRun run, int value) {
         int memoryLocation = this.memoryLocation;
         var initialItemValue = Model.ReadMultiByteValue(memoryLocation, 2);
         Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, value);
         if (initialItemValue == 0xFFFF) {
            var newRun = Model.RelocateForExpansion(CurrentChange, run, run.Length + 2);
            if (newRun.Start != run.Start) {
               MessageText = $"Egg Moves were automatically moved to {newRun.Start.ToString("X6")}. Pointers were updated.";
               memoryLocation += newRun.Start - run.Start;
               NewDataIndex = memoryLocation + 2;
               DataMoved = true;
            }
            Model.WriteMultiByteValue(memoryLocation + 2, 2, CurrentChange, 0xFFFF);
            var eggRun = new EggMoveRun(Model, newRun.Start);
            Model.ObserveRunWritten(CurrentChange, eggRun);
            eggRun = (EggMoveRun)Model.GetNextRun(eggRun.Start);
            eggRun.UpdateLimiter(CurrentChange);
         }
      }
   }
}
