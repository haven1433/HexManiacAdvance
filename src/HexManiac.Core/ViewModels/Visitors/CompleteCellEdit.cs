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
   internal class CompleteCellEdit : IDataFormatVisitor {
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

      public CompleteCellEdit(IDataModel model, int memoryLocation, string currentText, ModelDelta currentChange) {
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
         } else if (CurrentText.StartsWith("::")) {
            if (CurrentText.Last() != ' ') return;
            CompleteWordEdit();
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
         if (run is ITableRun && CurrentText[0] != PointerStart) {
            ErrorText = "Pointers in tables cannot be removed without removing the table.";
            return;
         }

         Visit((None)null, data);
      }

      public void Visit(Anchor anchor, byte data) {
         anchor.OriginalFormat.Visit(this, data);
         if (NewCell != null) NewCell = new HexElement(NewCell, new Anchor(NewCell.Format, anchor.Name, anchor.Format, anchor.Sources));
      }

      public void Visit(PCS pcs, byte data) => VisitPCS(pcs);

      private void VisitPCS(IDataFormatStreamInstance pcs) {
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

      public void Visit(ErrorPCS pcs, byte data) => VisitPCS(pcs);

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

      public void Visit(PlmItem item, byte data) {
         var memoryLocation = this.memoryLocation;
         var run = (PLMRun)Model.GetNextRun(memoryLocation);

         // part 1: contraction (if they entered the end token)
         if (CurrentText == EggMoveRun.GroupStart + EggMoveRun.GroupEnd) {
            for (int i = this.memoryLocation; i < run.Start + run.Length; i += 2) Model.WriteMultiByteValue(i, 2, CurrentChange, 0xFFFF);
            Model.ObserveRunWritten(CurrentChange, new PLMRun(Model, run.Start));
            NewDataIndex = memoryLocation + 2;
            Result = true;
            return;
         }

         // part 2: validation
         if (!CurrentText.Contains(" ")) return;
         var quoteCount = CurrentText.Count(c => c == StringDelimeter);
         if (quoteCount % 2 != 0) return;
         if (!CurrentText.EndsWith(StringDelimeter.ToString()) && !CurrentText.EndsWith(" ")) return;
         ErrorText = ValidatePlmText(run, quoteCount, out var level, out var move);
         if (ErrorText != null || move == -1) return;

         // part 3: write to the model
         NewDataIndex = memoryLocation + 2;
         Result = true;
         var value = (level << 9) + move;
         var initialItemValue = Model.ReadMultiByteValue(memoryLocation, 2);
         Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, value);

         // part 4: expansion
         if (initialItemValue == 0xFFFF) {
            var newRun = Model.RelocateForExpansion(CurrentChange, run, run.Length + 2);
            if (newRun.Start != run.Start) {
               MessageText = $"Level Up Moves were automatically moved to {newRun.Start.ToString("X6")}. Pointers were updated.";
               memoryLocation += newRun.Start - run.Start;
               NewDataIndex = memoryLocation + 2;
               DataMoved = true;
            }
            Model.WriteMultiByteValue(memoryLocation + 2, 2, CurrentChange, 0xFFFF);
            var lvlRun = new PLMRun(Model, newRun.Start);
            Model.ObserveRunWritten(CurrentChange, lvlRun);
         }
      }

      public void Visit(BitArray array, byte data) {
         var currentText = CurrentText.Replace(" ", "");
         if (currentText.All(ViewPort.AllHexCharacters.Contains) && currentText.Length == array.Length * 2) {
            HandleHexChangeToBitArray(array, currentText);
            return;
         }
         if (CurrentText.Equals("-")) {
            HandleBitArrayClear(array);
            return;
         }
         if (CurrentText.EndsWith(" ")) {
            currentText = CurrentText.Replace(" ", "");
            if (!currentText.All(ViewPort.AllHexCharacters.Contains)) {
               HandleBitArrayEntry(array, CurrentText);
               return;
            }
         }
         if (CurrentText.StartsWith("\"") && CurrentText.EndsWith("\"")) {
            HandleBitArrayEntry(array, CurrentText);
            return;
         }
      }

      public void Visit(MatchedWord word, byte data) => Visit((None)null, data);

      public void Visit(EndStream endStream, byte data) {
         // the only valid edit for an EndStream is to extend the stream.
         Result = true;
         var run = (TableStreamRun)Model.GetNextRun(memoryLocation);
         var newRun = run.Append(CurrentChange, 1);

         if (newRun.Start != run.Start) {
            MessageText = $"Stream was automatically moved to {newRun.Start.ToString("X6")}. Pointers were updated.";
            NewDataIndex = memoryLocation + newRun.Start - run.Start;
            DataMoved = true;
         }
         Model.ObserveRunWritten(CurrentChange, newRun);
      }

      public void Visit(LzMagicIdentifier lz, byte data) => Result = false;

      public void Visit(LzGroupHeader lz, byte data) {
         if (!CurrentText.EndsWith(" ")) return;
         if (byte.TryParse(CurrentText, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var result)) {
            CurrentChange.ChangeData(Model, memoryLocation, result);
            Result = true;
         }
      }

      public void Visit(LzCompressed lz, byte data) {
         if (!CurrentText.EndsWith(" ")) return;
         var sections = CurrentText.Trim().Split(":");
         if (sections.Length != 2) return;
         if (!int.TryParse(sections[0], out int runLength)) return;
         if (!int.TryParse(sections[1], out int runOffset)) return;
         var result = LZRun.CompressedToken((byte)runLength, (short)runOffset);
         CurrentChange.ChangeData(Model, memoryLocation, result[0]);
         CurrentChange.ChangeData(Model, memoryLocation + 1, result[1]);
         var run = (LZRun)Model.GetNextRun(memoryLocation);
         run = run.FixupEnd(Model, CurrentChange);
         Model.ObserveRunWritten(CurrentChange, run);
         NewDataIndex = memoryLocation + 2;
         Result = true;
      }

      public void Visit(LzUncompressed lz, byte data) {
         if (!CurrentText.EndsWith(" ")) return;
         if (byte.TryParse(CurrentText, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var result)) {
            CurrentChange.ChangeData(Model, memoryLocation, result);
            Result = true;
         }
      }

      /// <summary>
      /// Parses text in a PLM run to get the level and move.
      /// returns an error string if the parse fails.
      /// </summary>
      private string ValidatePlmText(PLMRun run, int quoteCount, out int level, out int move) {
         (level, move) = (default, default);
         if (quoteCount == 0) {
            var split = CurrentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2) { move = -1; return null; } // user hasn't entered a move yet
            if (!CurrentText.EndsWith(" ")) { move = -1; return null; } // user is still entering a move
            if (!int.TryParse(split[0], out level) || level < 1 || level > PLMRun.MaxLearningLevel) {
               return $"Could not parse '{split[0]}' as a pokemon level.";
            } else if (!run.TryGetMoveNumber(split[1], out move)) {
               return $"Could not parse {split[1]} as a pokemon move.";
            }
         } else {
            var rawLevel = CurrentText.Substring(0, CurrentText.IndexOf(' '));
            var rawMove = CurrentText.Substring(rawLevel.Length + 1);
            if (!int.TryParse(rawLevel, out level) || level < 1 || level > PLMRun.MaxLearningLevel) {
               return $"Could not parse '{rawLevel}' as a pokemon level.";
            } else if (!run.TryGetMoveNumber(rawMove, out move)) {
               return $"Could not parse {rawMove} as a pokemon move.";
            }
         }

         return null;
      }

      private void CompleteIntegerEdit(Integer integer) {
         if (!int.TryParse(CurrentText, out var result)) {
            ErrorText = $"Could not parse {CurrentText} as a number";
            return;
         }

         var run = (ITableRun)Model.GetNextRun(memoryLocation);
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
         var array = (ITableRun)Model.GetNextRun(memoryLocation);
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
         NewCell = new HexElement(content, true, new Ascii(asciiFormat.Source, asciiFormat.Position, CurrentText[0]));
         NewDataIndex = memoryLocation + 1;
      }

      private void CompletePointerEdit() {
         // if they just started a pointer and then clicked off, there's nothing to complete
         if (CurrentText == PointerStart + " ") return;

         var destination = CurrentText.Substring(1, CurrentText.Length - 2);

         if (destination.Length == 2 && destination.All(ViewPort.AllHexCharacters.Contains)) { CompleteHexEdit(destination); return; }

         Model.ExpandData(CurrentChange, memoryLocation + 3);

         var currentRun = Model.GetNextRun(memoryLocation);
         if (currentRun.Start > memoryLocation) currentRun = null;
         bool inArray = currentRun is ITableRun && currentRun.Start <= memoryLocation;
         var sources = currentRun?.PointerSources;

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
               UpdateArrayPointer((ITableRun)currentRun, fullValue);
            } else {
               if (Model.ReadPointer(memoryLocation) != fullValue) {
                  Model.WritePointer(CurrentChange, memoryLocation, fullValue);
               }
               Model.ObserveRunWritten(CurrentChange, new PointerRun(memoryLocation, sources));
            }

            NewDataIndex = memoryLocation + 4;
         } else {
            ErrorText = $"Address {fullValue.ToString("X2")} is not within the data.";
         }
      }

      private void CompleteWordEdit() {
         var parentName = CurrentText.Substring(2).Trim();
         Model.ExpandData(CurrentChange, memoryLocation + 3);
         Model.ClearFormat(CurrentChange, memoryLocation, 4);
         CurrentChange.AddMatchedWord(Model, memoryLocation, parentName);
         Model.ObserveRunWritten(CurrentChange, new WordRun(memoryLocation, parentName));
         NewDataIndex = memoryLocation + 4;
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
               Model.ObserveRunWritten(CurrentChange, new PCSRun(Model, run.Start, newRunLength, run.PointerSources));
            }
         } else if (run is ITableRun arrayRun) {
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
         var pcs = originalFormat as IDataFormatStreamInstance;
         var escaped = originalFormat as EscapedPCS;
         var run = Model.GetNextRun(memoryLocation);

         var byteValue = escaped != null ?
            byte.Parse(CurrentText, NumberStyles.HexNumber) :
            (byte)Enumerable.Range(0, 0x100).First(i => PCSString.PCS[i] == editText);

         var position = pcs == null ? escaped.Position : pcs.Position;
         HandleLastCharacterChange(memoryLocation, editText, run, position, byteValue);
      }

      private void HandleLastCharacterChange(int memoryLocation, string editText, IFormattedRun run, int position, byte byteValue) {
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
               run = new PCSRun(Model, run.Start, run.Length + extraBytesNeeded, run.PointerSources);
               Model.ObserveRunWritten(CurrentChange, run);
            }
         } else if (run is ITableRun arrayRun) {
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
         if (!$"{EggMoveRun.GroupEnd} {StringDelimeter}".Contains(endChar)) return;
         if (CurrentText.Count(c => c == StringDelimeter) % 2 != 0) return;

         NewDataIndex = memoryLocation + 2;
         Result = true;
         var run = (EggMoveRun)Model.GetNextRun(memoryLocation);

         if (CurrentText == EggMoveRun.GroupStart + EggMoveRun.GroupEnd) {
            Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, 0xFFFF);
            // clear all data after this and shorten the run
            for (int i = memoryLocation + 2; i < run.Start + run.Length; i += 2) {
               Model.WriteMultiByteValue(i, 2, CurrentChange, 0xFFFF);
            }
            var newRun = new EggMoveRun(Model, run.Start);
            Model.ObserveRunWritten(CurrentChange, newRun);
            newRun = (EggMoveRun)Model.GetNextRun(newRun.Start);
            newRun.UpdateLimiter(CurrentChange);
         } else if (CurrentText.EndsWith(EggMoveRun.GroupEnd)) {
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

      private void HandleHexChangeToBitArray(BitArray array, string currentText) {
         var parseArray = new byte[array.Length];

         for (int i = 0; i < array.Length; i++) {
            if (!byte.TryParse(currentText.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var result)) {
               ErrorText = $"Could not parse {CurrentText} as a bit-array.";
               return;
            }
            parseArray[i] = result;
         }
         for (int i = 0; i < array.Length; i++) CurrentChange.ChangeData(Model, memoryLocation + i, parseArray[i]);

         NewDataIndex = memoryLocation + array.Length;
         Result = true;
      }

      private void HandleBitArrayClear(BitArray array) {
         for (int i = 0; i < array.Length; i++) CurrentChange.ChangeData(Model, memoryLocation + i, 0);
         NewDataIndex = memoryLocation;
         Result = true;
      }

      private void HandleBitArrayEntry(BitArray array, string currentText) {
         currentText = currentText.Replace("\"", "").Trim();
         var run = (ITableRun)Model.GetNextRun(memoryLocation);
         var offset = run.ConvertByteOffsetToArrayOffset(memoryLocation);
         var segment = (ArrayRunBitArraySegment)run.ElementContent[offset.SegmentIndex];
         var sourceName = segment.SourceArrayName;
         var options = Model.GetBitOptions(sourceName);
         var bit = options.IndexOfPartial(currentText);
         if (bit == -1) {
            ErrorText = $"Could not parse {CurrentText} as a bit name.";
            return;
         }

         var dataIndex = memoryLocation;
         while (bit >= 8) { dataIndex++; bit -= 8; }
         var newData = (byte)(Model[dataIndex] | 1 << bit);
         CurrentChange.ChangeData(Model, dataIndex, newData);
         NewDataIndex = memoryLocation;
         Result = true;
      }

      private void UpdateArrayPointer(ITableRun run, int pointerDestination) {
         var offsets = run.ConvertByteOffsetToArrayOffset(memoryLocation);
         var segment = run.ElementContent[offsets.SegmentIndex];
         if (segment is ArrayRunPointerSegment pointerSegment) {
            if (!pointerSegment.DestinationDataMatchesPointerFormat(Model, CurrentChange, offsets.SegmentStart, pointerDestination, null)) {
               ErrorText = $"This pointer must point to {pointerSegment.InnerFormat} data.";
               return;
            }
         }

         Model.UpdateArrayPointer(CurrentChange, segment, memoryLocation, pointerDestination);
      }
   }
}
