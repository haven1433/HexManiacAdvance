using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
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
      private readonly ScrollRegion scroll;

      public bool Result { get; private set; }         // if true, the edit was completed correctly
      public int NewDataIndex { get; private set; }    // for completed edits, where should the selection move to?
      public bool DataMoved { get; private set; }
      public string MessageText { get; private set; }  // is there a message to display to the user? For example, when data gets moved.
      public string ErrorText { get; private set; }    // is there an error to display to the user? For example, invalid pointer
      public HexElement NewCell { get; private set; }  // if result is true and this is not null, assign this one value back to the one cell
                                                       // and refresh the one cell (along with any other UnderEdit cells)
                                                       // if result is true and this _is_ null, then the entire screen needs to be refreshed.

      public CompleteCellEdit(IDataModel model, ScrollRegion scroll, int memoryLocation, string currentText, ModelDelta currentChange) {
         Model = model;
         this.scroll = scroll;
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
         } else if (CurrentText.StartsWith(":")) {
            if (CurrentText.Last() != ' ') return;
            CompleteNamedConstantEdit(2);
            Result = true;
         } else if (CurrentText.StartsWith(".")) {
            if (CurrentText.Last() != ' ') return;
            CompleteNamedConstantEdit(1);
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

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) {
         if (CurrentText == "[]") {
            var run = (TableStreamRun)Model.GetNextRun(memoryLocation);
            var newDesiredElementCount = (memoryLocation - run.Start) / run.ElementLength;
            var newRun = run.Append(CurrentChange, newDesiredElementCount - run.ElementCount);
            Model.ObserveRunWritten(CurrentChange, newRun);
            for (int i = newRun.Length; i < run.Length; i++) CurrentChange.ChangeData(Model, newRun.Start + i, 0xFF);
            var endTokenLength = run.Length - run.ElementLength * run.ElementCount;
            NewDataIndex = memoryLocation + endTokenLength;
            Result = true;
         } else {
            decorator.OriginalFormat.Visit(this, data);
         }
      }

      public void Visit(PCS pcs, byte data) => VisitPCS(pcs);

      private void VisitPCS(IDataFormatStreamInstance pcs) {
         var currentText = CurrentText;
         if (currentText.StartsWith(StringDelimeter.ToString())) currentText = currentText.Substring(1);
         if (pcs.Position != 0 && CurrentText == StringDelimeter.ToString()) {
            CompleteStringEdit();
            Result = true;
         } else if (pcs.Position == 0 && CurrentText == StringDelimeter.ToString() + StringDelimeter) {
            CompleteStringEdit();
            Result = true;
         } else if (PCSString.PCS.Any(str => str == currentText)) {
            CompleteCharacterEdit(pcs);
            Result = true;
         } else {
            var bytes = Model.TextConverter.Convert(currentText, out var containsBadCharacters);
            if (bytes.Count > 1 && !containsBadCharacters) {
               CompleteCharacterEdit(pcs);
               Result = true;
            }
         }

         if (Result) scroll.UpdateHeaders();
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

      public void Visit(Braille braille, byte data) {
         var currentText = CurrentText;
         if (currentText.StartsWith(StringDelimeter.ToString())) currentText = currentText.Substring(1);
         if (braille.Position != 0 && CurrentText == StringDelimeter.ToString()) {
            CompleteBrailleStringEdit(braille);
         } else if (braille.Position == 0 && CurrentText == StringDelimeter.ToString() + StringDelimeter) {
            CompleteBrailleStringEdit(braille);
         } else {
            CompleteBrailleCharacterEdit(braille);
         }

         Result = true;
         scroll.UpdateHeaders();
      }

      public void Visit(Integer integer, byte data) {
         if (CurrentText == "+" && Model.GetNextRun(memoryLocation) is LzSpriteRun spriteRun) {
            var newRun = spriteRun.IncreaseHeight(1, CurrentChange);
            if (newRun.Start != spriteRun.Start) {
               MessageText = $"Sprite was automatically moved to {newRun.Start:X6}. Pointers were updated.";
               DataMoved = true;
               NewDataIndex = newRun.Start + 1;
            }
            Result = true;
         }

         if (char.IsWhiteSpace(CurrentText.Last()) || CurrentText.Last() == ')') {
            CompleteIntegerEdit(integer);
            Result = true;
         }
      }

      public void Visit(IntegerEnum integer, byte data) {
         // must end in whitespace or must have matching quotation marks (ex. "Mr. Mime")
         var quoteCount = CurrentText.Count(c => c == '"');

         if (quoteCount == 0 && (char.IsWhiteSpace(CurrentText.Last()) || CurrentText.Last() == ')')) {
            CompleteIntegerEnumEdit();
            Result = true;
         } else if (quoteCount == 2) {
            CompleteIntegerEnumEdit();
            Result = true;
         }
      }

      public void Visit(IntegerHex integerHex, byte data) {
         if (char.IsWhiteSpace(CurrentText.Last()) || CurrentText.Last() == ')') {
            CompleteIntegerHexEdit(integerHex);
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
               MessageText = $"Level Up Moves were automatically moved to {newRun.Start:X6}. Pointers were updated.";
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
         if (CurrentText.EndsWith(" ") && !CurrentText.StartsWith("\"")) {
            currentText = CurrentText.Replace(" ", "");
            if (!(currentText.All(ViewPort.AllHexCharacters.Contains) && (CurrentText.Count(c => c == ' ') > 1 || currentText.Length == 2))) {
               HandleBitArrayEntry(CurrentText);
               return;
            }
         }
         if (CurrentText.StartsWith("\"") && CurrentText.Trim().EndsWith("\"") && CurrentText.Length > 1) {
            HandleBitArrayEntry(CurrentText);
            return;
         }
         if (CurrentText == "/") {
            NewDataIndex = memoryLocation + array.Length;
            Result = true;
         }
      }

      public void Visit(MatchedWord word, byte data) => Visit((None)null, data);

      public void Visit(EndStream endStream, byte data) {
         // the only valid edit for an EndStream is to extend the stream, or to write the end token to move past it.

         if (CurrentText == "[]") {
            var tableRun = (TableStreamRun)Model.GetNextRun(memoryLocation);
            var endTokenLength = tableRun.Length - tableRun.ElementLength * tableRun.ElementCount;
            NewDataIndex = memoryLocation + endTokenLength;
            Result = true;
         }
         if (CurrentText != "+") return;

         Result = true;
         var run = (TableStreamRun)Model.GetNextRun(memoryLocation);
         var newRun = run.Append(CurrentChange, 1);

         if (newRun.Start != run.Start) {
            MessageText = $"Stream was automatically moved to {newRun.Start:X6}. Pointers were updated.";
            NewDataIndex = memoryLocation + newRun.Start - run.Start;
            DataMoved = true;
         }

         Model.ObserveRunWritten(CurrentChange, newRun);

         if (newRun.Start == run.Start) {
            // table was expanded, update the scroll so it knows about the new table length
            scroll.SetTableMode(newRun.Start, newRun.Length);
         }
      }

      public void Visit(LzMagicIdentifier lz, byte data) {
         if (CurrentText.ToLower() == "lz") {
            Result = true;
            NewDataIndex = memoryLocation + 1;
         }
      }

      public void Visit(LzGroupHeader lz, byte data) {
         if (!CurrentText.EndsWith(" ")) return;
         if (byte.TryParse(CurrentText, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var result)) {
            var oldValue = Model[memoryLocation];
            CurrentChange.ChangeData(Model, memoryLocation, result);
            var run = (LZRun)Model.GetNextRun(memoryLocation);
            int runIndex = memoryLocation - run.Start;
            Result = true;
            if (!TryFixupLzRun(ref run, runIndex + 1)) {
               CurrentChange.ChangeData(Model, memoryLocation, oldValue);
               ErrorText = $"Could not write header {result:X2} without making the compressed data invalid.";
            } else {
               NewDataIndex = run.Start + runIndex + 1;
            }
         }
      }

      public void Visit(LzCompressed lz, byte data) {
         if (!CurrentText.EndsWith(" ")) return;
         var sections = CurrentText.Trim().Split(":");
         if (sections.Length != 2) return;
         if (!int.TryParse(sections[0], out int runLength)) return;
         if (!int.TryParse(sections[1], out int runOffset)) return;
         Result = true;
         if (runLength < 3 || runLength > 18) {
            ErrorText = "Run Length must be > 2 and < 19";
            return;
         } else if (runOffset < 1 || runOffset > 0x1000) {
            ErrorText = "Run Offset must be > 0 and <= 4096";
            return;
         }
         var result = LZRun.CompressedToken((byte)runLength, (short)runOffset);
         var previousValue = Model.ReadMultiByteValue(memoryLocation, 2);
         CurrentChange.ChangeData(Model, memoryLocation, result[0]);
         CurrentChange.ChangeData(Model, memoryLocation + 1, result[1]);
         var run = (LZRun)Model.GetNextRun(memoryLocation);
         int runIndex = memoryLocation - run.Start;
         if (!TryFixupLzRun(ref run, runIndex + 2)) {
            Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, previousValue);
            ErrorText = $"Could not write {runLength}:{runOffset} without making the compressed data invalid.";
         } else {
            NewDataIndex = run.Start + runIndex + 2;
         }
      }

      public void Visit(LzUncompressed lz, byte data) {
         if (CurrentText == "+" && Model.GetNextRun(memoryLocation) is SpriteRun spriteRun) {
            var newRun = spriteRun.IncreaseHeight(1, CurrentChange);
            if (newRun.Start != spriteRun.Start) {
               MessageText = $"Sprite was automatically moved to {newRun.Start:X6}. Pointers were updated.";
               DataMoved = true;
               NewDataIndex = newRun.Start + 1;
            }
            Result = true;
         }

         if (!CurrentText.EndsWith(" ")) return;
         if (byte.TryParse(CurrentText, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var result)) {
            CurrentChange.ChangeData(Model, memoryLocation, result);
            Result = true;
            NewDataIndex = memoryLocation + 1;
         }
      }

      public void Visit(UncompressedPaletteColor color, byte data) {
         if (!CurrentText.EndsWith(" ")) return;
         if (CurrentText.Length < 4) return;

         // option 1: 4 hex bytes
         if (CurrentText.Length == 5 && short.TryParse(CurrentText.Trim(), NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var result)) {
            Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, result);
            NewDataIndex = memoryLocation + 2;
            Result = true;
            return;
         }

         // option 2: 2 hex bytes, a space, and 2 more hex bytes
         if (CurrentText.Length == 6) {
            if (byte.TryParse(CurrentText.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var byte1)) {
               if (byte.TryParse(CurrentText.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var byte2)) {
                  CurrentChange.ChangeData(Model, memoryLocation + 0, byte1);
                  CurrentChange.ChangeData(Model, memoryLocation + 1, byte2);
                  NewDataIndex = memoryLocation + 2;
                  Result = true;
                  return;
               }
            }
         }

         // option 3: 3 decimal values, separated by :
         var channels = CurrentText.Trim().Split(':');
         if (channels.Length == 3 &&
            byte.TryParse(channels[0], out var red) &&
            byte.TryParse(channels[1], out var green) &&
            byte.TryParse(channels[2], out var blue)) {
            var newColor = (short)((blue << 10) | (green << 5) | red);
            Model.WriteMultiByteValue(memoryLocation, 2, CurrentChange, newColor);
            NewDataIndex = memoryLocation + 2;
            Result = true;
            return;
         }

         // incomplete
         if (CurrentText.Count(c => c == ' ') > 1) {
            Result = true;
            ErrorText = $"Could not parse {CurrentText} as a palette color.";
         }
      }

      public void Visit(DataFormats.Tuple tuple, byte data) {
         Result = CurrentText.EndsWith(")");
         if (CurrentText.EndsWith(" ")) {
            var tokens = CurrentText.Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            TableStreamRun.Recombine(tokens, "\"", "\"");
            if (tokens.Count == tuple.Model.VisibleElementCount) Result = true;
         }

         if (Result) {
            var text = CurrentText;
            tuple.Model.Write(null, Model, CurrentChange, memoryLocation, ref text);
            NewDataIndex = memoryLocation + tuple.Length;
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

      private void CompleteIntegerHexEdit(IntegerHex integerHex) {
         string sanitizedText = CurrentText.Replace(')', ' ');
         if (!int.TryParse(sanitizedText, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var result)) {
            ErrorText = $"Could not parse {CurrentText} as a hex number";
            return;
         }

         Model.WriteMultiByteValue(integerHex.Source, integerHex.Length, CurrentChange, result);
         if (result >= Math.Pow(2L, integerHex.Length * 8)) ErrorText = $"Warning: number was too big to fit in the available space.";
         if (Model.GetNextRun(memoryLocation) is PIERun run) {
            var newRun = run.Refresh(CurrentChange);
            if (newRun.Start != run.Start) {
               MessageText = $"Item effect was automatically moved to {newRun.Start:X6}. Pointers were updated.";
               NewDataIndex = memoryLocation + newRun.Start - run.Start + integerHex.Length;
               DataMoved = true;
            }
            Model.ObserveRunWritten(CurrentChange, newRun);
            NewDataIndex = memoryLocation + newRun.Start - run.Start + integerHex.Length;
         } else {
            NewDataIndex = integerHex.Source + integerHex.Length;
         }
      }

      private void CompleteIntegerEdit(Integer integer) {
         string sanitizedText = CurrentText.Replace(')', ' ');
         if (!int.TryParse(sanitizedText, out var result)) {
            ErrorText = $"Could not parse {CurrentText} as a number";
            return;
         }

         var run = Model.GetNextRun(memoryLocation);
         if (run is WordRun wordRun1) {
            var desiredValue = result - wordRun1.ValueOffset;
            var maxValue = (int)Math.Pow(2, wordRun1.Length * 8) - 1;
            if (desiredValue < 0 || desiredValue > maxValue) {
               ErrorText = "Virtual value out of range!";
               return;
            }
         }
         Model.WriteMultiByteValue(integer.Source, integer.Length, CurrentChange, result);
         if (result >= Math.Pow(2L, integer.Length * 8)) ErrorText = $"Warning: number was too big to fit in the available space.";
         int runIndex = integer.Source - run.Start;
         if (run is LZRun lzRun) {
            TryFixupLzRun(ref lzRun, runIndex + integer.Length); // this is before the first header: it cannot fail.
            run = lzRun;
         }
         if (run is ITableRun tableRun) {
            var offset = tableRun.ConvertByteOffsetToArrayOffset(integer.Source);
            var info = tableRun.NotifyChildren(Model, CurrentChange, offset.ElementIndex, offset.SegmentIndex);
            if (info != null && info.IsWarning) MessageText = info.ErrorMessage;
         }
         var (newDataIndex, messageText, errorText) = UpdateAllWords(Model, run, CurrentChange, result, alsoUpdateArrays: true);
         NewDataIndex = run.Start + runIndex + integer.Length;
         MessageText = messageText ?? MessageText;
         if (newDataIndex >= 0) (NewDataIndex, ErrorText) = (newDataIndex, errorText);
      }

      public static (int errorIndex, string message, string error) UpdateAllWords(IDataModel model, IFormattedRun run, ModelDelta token, int value, bool alsoUpdateArrays) {
         int newDataIndex = -1;
         string messageText = null;
         string errorText = null;

         if (run is WordRun wordRun) {
            // update the other word runs with the same token name
            var desiredValue = (value - wordRun.ValueOffset) / wordRun.MultOffset;

            if (alsoUpdateArrays) {
               foreach (var array in model.Arrays.Where(a => a.LengthFromAnchor == wordRun.SourceArrayName).ToList()) {
                  var delta = desiredValue - array.ElementCount;
                  if (delta == 0) continue;
                  var newArray = array;
                  if (!(token is NoDataChangeDeltaModel)) {
                     // relocate if needed
                     newArray = model.RelocateForExpansion(token, array, (array.ElementCount + delta) * array.ElementLength);
                  }
                  newArray = newArray.Append(token, delta);
                  if (token is NoDataChangeDeltaModel && newArray.SupportsInnerPointers) {
                     // the new 'added' elements may have stuff pointing to them, because they were already there
                     newArray = newArray.AddSourcesPointingWithinArray(token);
                  }
                  if (newArray != array) {
                     var name = newArray.Start.ToAddress();
                     var anchorName = model.GetAnchorFromAddress(-1, newArray.Start);
                     if (!string.IsNullOrWhiteSpace(anchorName)) name = anchorName;
                     messageText = $"Table {name} was moved. Pointers have been updated.";
                     if (newArray.Length > array.Length && newArray.Start == array.Start) {
                        model.ClearFormat(token, array.Start + array.Length, newArray.Length - array.Length);
                        // edge case: if the run in this spot isn't the run we're about to add, clear that too
                        if (model.GetNextRun(newArray.Start).Start != newArray.Start) {
                           model.ClearFormat(token, newArray.Start, array.Length);
                        }
                     }
                     model.ObserveRunWritten(token, newArray);

                     // if this run has pointers, those may have been cleared by some earlier update
                     model.InsertPointersToRun(token, newArray);
                  }
               }
            }

            if (token is NoDataChangeDeltaModel) return (newDataIndex, messageText, errorText);
            foreach (var address in model.GetMatchedWords(wordRun.SourceArrayName)) {
               if (address == run.Start) continue; // don't write the current run
               if (!(model.GetNextRun(address) is WordRun currentRun)) continue;
               var writeValue = desiredValue * currentRun.MultOffset + currentRun.ValueOffset;
               var maxValue = (int)Math.Pow(2, currentRun.Length * 8) - 1;
               if (writeValue < 0 || writeValue > maxValue) {
                  newDataIndex = currentRun.Start;
                  errorText = $"{currentRun.Start:X6}: value out of range!";
               }
               model.WriteMultiByteValue(address, currentRun.Length, token, writeValue);
            }
         }

         return (newDataIndex, messageText, errorText);
      }

      private void CompleteIntegerEnumEdit() {
         string sanitizedText = CurrentText.Replace(')', ' ');
         var array = (ITableRun)Model.GetNextRun(memoryLocation);
         var offsets = array.ConvertByteOffsetToArrayOffset(memoryLocation);
         var segment = (ArrayRunEnumSegment)array.ElementContent[offsets.SegmentIndex];
         if (segment.TryParse(Model, sanitizedText, out int value)) {
            Model.WriteMultiByteValue(offsets.SegmentStart, segment.Length, CurrentChange, value);
            NewDataIndex = offsets.SegmentStart + segment.Length;
         } else {
            ErrorText = $"Could not parse {CurrentText} as an enum from the {segment.EnumName} array";
         }
      }

      private void CompleteAsciiEdit(Ascii asciiFormat) {
         var content = (byte)CurrentText[0];

         CurrentChange.ChangeData(Model, memoryLocation, content);
         NewCell = new HexElement(content, true, new Ascii(asciiFormat.Source, asciiFormat.Position, CurrentText[0]));
         NewDataIndex = memoryLocation + 1;
      }

      private void CompleteBrailleCharacterEdit(Braille brailleFormat) {
         var memoryLocation = this.memoryLocation;

         // complete edit on this cell
         // move to next cell, potentially increasing the run length.
         var run = (BrailleRun)Model.GetNextRun(memoryLocation);
         var content = BrailleRun.DeserializeCharacter(CurrentText[0]);
         CurrentChange.ChangeData(Model, memoryLocation, content);
         NewCell = new HexElement(content, true, new Braille(brailleFormat.Source, brailleFormat.Position, CurrentText[0]));

         if (NewDataIndex == run.Start + run.Length) {
            var newRun = Model.RelocateForExpansion(CurrentChange, run, run.Length + 1);
            if (newRun != run) {
               MessageText = $"Braille text was automatically moved to {newRun.Start:X6}. Pointers were updated.";
               memoryLocation += newRun.Start - run.Start;
               run = newRun;
               DataMoved = true;
            }

            run = new BrailleRun(Model, run.Start, run.PointerSources);
            Model.ObserveRunWritten(CurrentChange, run);
         }

         NewDataIndex = memoryLocation + 1;
      }

      private void CompleteBrailleStringEdit(Braille format) {
         var run = (BrailleRun)Model.GetNextRun(memoryLocation);
         for (int i = format.Source + format.Position; i < run.Start + run.Length; i++) CurrentChange.ChangeData(Model, i, 0xFF);
         NewCell = new HexElement(0xFF, true, new Braille(format.Source, format.Position, StringDelimeter));
         NewDataIndex = memoryLocation + 1;
         Model.ObserveRunWritten(CurrentChange,new BrailleRun(Model,run.Start,run.PointerSources));
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
         var previousDestination = Model.ReadPointer(memoryLocation);

         if (!inArray) {
            Model.ClearFormat(CurrentChange, memoryLocation, 4);
            sources = null;
         }

         var (destinationValue, offset) = ParseDestination(destination);
         if (destinationValue + offset != previousDestination) {
            Model.ClearPointer(CurrentChange, memoryLocation, previousDestination);
            Model.ClearData(CurrentChange, memoryLocation, 4);
         }

         var fullValue = destinationValue + offset;
         if (destinationValue == Pointer.NULL || (0 <= destinationValue && destinationValue < Model.Count)) {
            if (inArray) {
               UpdateArrayPointer((ITableRun)currentRun, destinationValue);
            } else {
               if (Model.ReadPointer(memoryLocation) != fullValue) {
                  Model.WritePointer(CurrentChange, memoryLocation, fullValue);
               }
               var newRun = new PointerRun(memoryLocation, sources);
               if (offset != 0) newRun = new OffsetPointerRun(memoryLocation, offset, sources);
               Model.ObserveRunWritten(CurrentChange, newRun);
            }

            NewDataIndex = memoryLocation + 4;
         } else {
            ErrorText = $"Address {destinationValue:X2} is not within the data.";
         }
      }

      /// <summary>
      /// Reads the user's text and current context to decide the destination for a new pointer.
      /// Can set ErrorText if there is an error in this process.
      /// </summary>
      private (int, int) ParseDestination(string destination) {
         int destinationValue;
         int offset = 0;
         if (destination == string.Empty) {
            destinationValue = Model.ReadPointer(memoryLocation);
         } else if (destination.All(ViewPort.AllHexCharacters.Contains) && destination.Length <= 7) {
            while (destination.Length < 6) destination = "0" + destination;
            destinationValue = int.Parse(destination, NumberStyles.HexNumber);
         } else if (destination.Contains("+") && !destination.Contains("-")) {
            var destinationParts = destination.Split("+");
            if (!int.TryParse(destinationParts[0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out destinationValue)) {
               destinationValue = Model.GetAddressFromAnchor(CurrentChange, memoryLocation, destinationParts[0]);
            }
            if (!int.TryParse(destinationParts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset)) {
               ErrorText = $"Could not parse {destinationParts[0]}+{destinationParts[1]} into an address.";
            }
         } else if (destination.Contains("-") && !destination.Contains("+")) {
            var destinationParts = destination.Split("-");
            if (!int.TryParse(destinationParts[0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out destinationValue)) {
               destinationValue = Model.GetAddressFromAnchor(CurrentChange, memoryLocation, destinationParts[0]);
            }
            if (!int.TryParse(destinationParts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset)) {
               ErrorText = $"Could not parse {destinationParts[0]}-{destinationParts[1]} into an adress.";
            }
            offset = -offset;
         } else {
            destinationValue = Model.GetAddressFromAnchor(CurrentChange, memoryLocation, destination);
         }

         return (destinationValue, offset);
      }

      private void CompleteWordEdit() {
         var parentName = CurrentText.Substring(2).Trim();
         if (parentName == string.Empty) {
            ErrorText = "Cannot write constant with no name.";
            return;
         }

         Model.ExpandData(CurrentChange, memoryLocation + 3);
         Model.ClearFormat(CurrentChange, memoryLocation, 4);
         CurrentChange.AddMatchedWord(Model, memoryLocation, parentName, 4);
         Model.ObserveRunWritten(CurrentChange, new WordRun(memoryLocation, parentName, 4, 0, 1));
         NewDataIndex = memoryLocation + 4;
      }

      private void CompleteNamedConstantEdit(int byteCount) {
         var constantName = CurrentText.Substring(1).Trim();
         int offset = 0, multOffset = 1, newValue = int.MinValue;
         if (constantName.Contains("=")) {
            var split = constantName.Split('=');
            if (!int.TryParse(split[1], out newValue)) newValue = int.MinValue;
            constantName = split[0];
         }
         if (constantName.Contains("+")) {
            var split = constantName.Split('+');
            int.TryParse(split[1], out offset);
            constantName = split[0];
         }
         if (constantName.Contains("-")) {
            var split = constantName.Split('-');
            int.TryParse(split[1], out offset);
            constantName = split[0];
            offset = -offset;
         }
         if (constantName.Contains("*")) {
            var split = constantName.Split('*');
            int.TryParse(split[1], out multOffset);
            constantName = split[0];
            if (multOffset < 1) {
               ErrorText = $"Could not create {constantName} with multiplier {multOffset}: the multiplier must be positive.";
               return;
            }
         }
         if (constantName == string.Empty) {
            ErrorText = "Cannot write constant with no name.";
            return;
         }

         var coreValue = (Model[memoryLocation] / multOffset) - offset;
         if (newValue != int.MinValue) coreValue = newValue / multOffset - offset;
         var maxValue = Math.Pow(2, byteCount * 8) - 1;
         if (coreValue < 0) {
            if (offset != 0) {
               ErrorText = $"Could not create {constantName} with offset {offset} because then the virtual value would be below 0.";
            } else if (multOffset != 1) {
               ErrorText = $"Could not create {constantName} with multiplier {multOffset} because then the virtual value would be below 0.";
            }
         } else if (coreValue > maxValue) {
            if (offset != 0) {
               ErrorText = $"Could not create {constantName} with offset {offset} because then the virtual value would be above {maxValue}.";
            } else if (multOffset != 1) {
               ErrorText = $"Could not create {constantName} with multiplier {offset} because then the virtual value would be above {maxValue}.";
            }
         } else {
            if (newValue != int.MinValue) Model.WriteMultiByteValue(memoryLocation, byteCount, CurrentChange, newValue);
            CurrentChange.AddMatchedWord(Model, memoryLocation, constantName, byteCount);
            Model.ObserveRunWritten(CurrentChange, new WordRun(memoryLocation, constantName, byteCount, offset, multOffset));
            NewDataIndex = memoryLocation + (newValue != int.MinValue ? byteCount : 0);
         }
      }

      private void CompleteStringEdit() {
         int memoryLocation = this.memoryLocation;

         // all the bytes are already correct, just move to the next space
         var run = Model.GetNextRun(memoryLocation);
         if (run is PCSRun) {
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
         var escaped = originalFormat as EscapedPCS;
         var run = Model.GetNextRun(memoryLocation);

         if (Model.TextConverter.AnyMacroStartsWith(editText)) {
            var byteValues = Model.TextConverter.Convert(editText, out var containsBadCharacters);
            if (!containsBadCharacters && byteValues.Count > 2) {
               byteValues = byteValues.Take(byteValues.Count - 1).ToList(); // remove the FF at the end of the convert
               HandleLastCharacterChange(memoryLocation, run, byteValues);
               return;
            }
         }

         var byteValue = escaped != null ?
            byte.Parse(CurrentText, NumberStyles.HexNumber) :
            (byte)0x100.Range().First(i => PCSString.PCS[i] == editText);

         var position = originalFormat is IDataFormatStreamInstance pcs ? pcs.Position : escaped.Position;
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
                  MessageText = $"Text was automatically moved to {newRun.Start:X6}. Pointers were updated.";
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
            // if the last character is being edited for an array, truncate
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

      private void HandleLastCharacterChange(int memoryLocation, IFormattedRun run, IReadOnlyList<byte> bytes) {
         if (run is PCSRun) {
            if (run.Length < memoryLocation - run.Start + bytes.Count + 1) {
               var newRun = Model.RelocateForExpansion(CurrentChange, run, run.Length + bytes.Count);
               if (newRun != run) {
                  MessageText = $"Text was automatically moved to {newRun.Start:X6}. Pointers were updated.";
                  memoryLocation += newRun.Start - run.Start;
                  run = newRun;
                  DataMoved = true;
               }
               CurrentChange.ChangeData(Model, memoryLocation + bytes.Count, 0xFF);
               run = new PCSRun(Model, run.Start, memoryLocation - run.Start + bytes.Count + 1, run.PointerSources);
               Model.ObserveRunWritten(CurrentChange, run);
            }
         } else if (run is ITableRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(memoryLocation);
            while (array.ElementContent[offsets.SegmentIndex].Length < memoryLocation-offsets.SegmentStart+bytes.Count+1) {
               memoryLocation--; // move back one byte and edit that one instead
            }
            if (bytes.Count.Range().Any(i => Model[memoryLocation + i] == 0xFF)) {
               CurrentChange.ChangeData(Model, memoryLocation + bytes.Count, 0xFF); // overwrote the closing ", so add a new one after (since there's room)
            }
         }

         CurrentChange.ChangeData(Model, memoryLocation, bytes);
         NewDataIndex = memoryLocation + bytes.Count;
      }

      private void CompleteHexEdit(string currentText) {
         var byteValue = byte.Parse(currentText, NumberStyles.HexNumber);
         var run = Model.GetNextRun(memoryLocation);
         if (!(run is NoInfoRun || run is IScriptStartRun) || run.Start != memoryLocation) Model.ClearFormat(CurrentChange, memoryLocation, 1);
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
               MessageText = $"Egg Moves were automatically moved to {newRun.Start:X6}. Pointers were updated.";
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

      private void HandleBitArrayEntry(string currentText) {
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
            if (!pointerSegment.DestinationDataMatchesPointerFormat(Model, CurrentChange, offsets.SegmentStart, pointerDestination, run.ElementContent, -1)) {
               ErrorText = $"This pointer must point to {pointerSegment.InnerFormat} data.";
               return;
            }
         }

         Model.UpdateArrayPointer(CurrentChange, segment, run.ElementContent, offsets.ElementIndex, memoryLocation, pointerDestination);
      }

      private bool TryFixupLzRun(ref LZRun run, int runIndex) {
         var initialStart = run.Start;
         var newRun = run.FixupEnd(Model, CurrentChange, runIndex);
         if (newRun == null) return false;

         Model.ObserveRunWritten(CurrentChange, newRun);
         if (newRun.Start != initialStart) MessageText = $"LZ Compressed data was automatically moved to {newRun.Start:X6}. Pointers were updated.";
         run = newRun;
         return true;
      }
   }
}
