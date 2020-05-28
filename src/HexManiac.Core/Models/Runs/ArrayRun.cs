using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public interface ITableRun : IAppendToBuilderRun {
      int ElementCount { get; }
      int ElementLength { get; }
      IReadOnlyList<string> ElementNames { get; }
      IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }
      ITableRun Append(ModelDelta token, int length);
   }

   public static class ITableRunExtensions {
      public static ArrayOffset ConvertByteOffsetToArrayOffset(this ITableRun self, int byteOffset) {
         var offset = byteOffset - self.Start;
         int elementIndex = offset / self.ElementLength;
         int elementOffset = offset % self.ElementLength;
         int segmentIndex = 0, segmentOffset = elementOffset;
         while (self.ElementContent[segmentIndex].Length <= segmentOffset) {
            segmentOffset -= self.ElementContent[segmentIndex].Length; segmentIndex++;
         }
         return new ArrayOffset(elementIndex, segmentIndex, byteOffset - segmentOffset, segmentOffset);
      }

      public static IDataFormat CreateSegmentDataFormat(this ITableRun self, IDataModel data, int index) {
         var offsets = self.ConvertByteOffsetToArrayOffset(index);
         var currentSegment = self.ElementContent[offsets.SegmentIndex];
         var position = index - offsets.SegmentStart;
         if (currentSegment.Type == ElementContentType.Integer) {
            if (currentSegment is ArrayRunEnumSegment enumSegment) {
               var value = enumSegment.ToText(data, index, false);
               return new IntegerEnum(offsets.SegmentStart, position, value, currentSegment.Length);
            } else {
               var value = ArrayRunElementSegment.ToInteger(data, offsets.SegmentStart, currentSegment.Length);
               return new Integer(offsets.SegmentStart, position, value, currentSegment.Length);
            }
         }

         if (currentSegment.Type == ElementContentType.Pointer) {
            var destination = data.ReadPointer(offsets.SegmentStart);
            var destinationName = data.GetAnchorFromAddress(offsets.SegmentStart, destination);
            var hasError = destination < 0 || data.GetNextRun(destination).Start != destination; // table pointers might not point at anchors
            return new Pointer(offsets.SegmentStart, position, destination, destinationName, hasError);
         }

         if (currentSegment.Type == ElementContentType.BitArray) {
            return new BitArray(offsets.SegmentStart, position, currentSegment.Length);
         }

         throw new NotImplementedException();
      }

      public static void AppendTo(ITableRun self, IDataModel data, StringBuilder text, int start, int length, bool deep) {
         var names = self.ElementNames;
         var offsets = self.ConvertByteOffsetToArrayOffset(start);
         length += offsets.SegmentOffset;
         for (int i = offsets.ElementIndex; i < self.ElementCount && length > 0; i++) {
            var offset = offsets.SegmentStart;
            if (offsets.SegmentIndex == 0 && offsets.ElementIndex > 0) text.Append(ArrayRun.ExtendArray);
            for (int j = offsets.SegmentIndex; j < self.ElementContent.Count && length > 0; j++) {
               var segment = self.ElementContent[j];
               if (j == 0 && segment.Type != ElementContentType.PCS && names != null && names.Count > i && !string.IsNullOrEmpty(names[i])) {
                  text.Append($"{ViewPort.CommentStart}{names[i]}{ViewPort.CommentStart}, ");
               }

               text.Append(segment.ToText(data, offset, deep).Trim());
               if (j + 1 < self.ElementContent.Count) text.Append(", ");
               offset += segment.Length;
               length -= segment.Length;
            }
            if (i + 1 < self.ElementCount) text.Append(Environment.NewLine);
            offsets = new ArrayOffset(i + 1, 0, offset, 0);
         }
      }

      public static ErrorInfo NotifyChildren(this ITableRun self, IDataModel model, ModelDelta token, int elementIndex, int segmentIndex) {
         int offset = 0;
         var info = ErrorInfo.NoError;
         foreach (var segment in self.ElementContent) {
            if (segment is ArrayRunPointerSegment pointerSegment) {
               var pointerSource = self.Start + elementIndex * self.ElementLength + offset;
               var destination = model.ReadPointer(pointerSource);
               var run = model.GetNextRun(destination);
               if (run.Start == destination && run is TrainerPokemonTeamRun teamRun) {
                  var newRun = teamRun.UpdateFromParent(token, segmentIndex, pointerSource);
                  model.ObserveRunWritten(token, newRun);
                  if (newRun.Start != teamRun.Start) info = new ErrorInfo($"Team was automatically moved to {newRun.Start:X6}. Pointers were updated.", isWarningLevel: true);
               } else if (run.Start == destination && run is TableStreamRun tableStreamRun) {
                  var newRun = tableStreamRun.UpdateFromParent(token, segmentIndex);
                  model.ObserveRunWritten(token, newRun);
                  if (newRun.Start != tableStreamRun.Start) info = new ErrorInfo($"Stream was automatically moved to {newRun.Start:X6}. Pointers were updated.", isWarningLevel: true);
               }
            }
            offset += segment.Length;
         }

         return info;
      }
   }

   public class ArrayOffset {
      /// <summary>
      /// Ranges from 0 to ElementCount
      /// </summary>
      public int ElementIndex { get; }
      /// <summary>
      /// Ranges from 0 to ElementContent.Count
      /// </summary>
      public int SegmentIndex { get; }
      /// <summary>
      /// The data address where the current segment starts. Ranges from n to n+ElementContent[SegmentIndex].Length.
      /// </summary>
      public int SegmentStart { get; }
      /// <summary>
      /// The index into the current segment. 0 means the start of the segment.
      /// </summary>
      public int SegmentOffset { get; }
      public ArrayOffset(int elementIndex, int segmentIndex, int segmentStart, int segmentOffset) {
         ElementIndex = elementIndex;
         SegmentIndex = segmentIndex;
         SegmentStart = segmentStart;
         SegmentOffset = segmentOffset;
      }
   }

   public class ArrayRun : BaseRun, ITableRun {
      public const char ExtendArray = '+';
      public const char ArrayStart = '[';
      public const char ArrayEnd = ']';
      public const char SingleByteIntegerFormat = '.';
      public const char DoubleByteIntegerFormat = ':';
      public const char ArrayAnchorSeparator = '/';

      private const int JunkLimit = 80;

      private readonly IDataModel owner;

      // length in bytes of the entire array
      public override int Length { get; }

      public override string FormatString { get; }

      // number of elements in the array
      public int ElementCount { get; }

      // length of each element
      public int ElementLength { get; }

      /// <summary>
      /// For some arrays, their length is determined by another named array.
      /// This way, we can know when we need to expand multiple arrays to keep them the same length.
      /// </summary>
      public string LengthFromAnchor { get; }

      /// <summary>
      /// For some dependendent arrays, the length doesn't exactly match.
      /// For example, move 0 doesn't have a description.
      /// </summary>
      public int ParentOffset { get; }

      public bool SupportsPointersToElements { get; }

      /// <summary>
      /// For Arrays that support pointers to individual elements within the array,
      /// This is the set of sources that points to each index of the array.
      /// The first set of sources (PointerSourcesForInnerElements[0]) should always be the same as PointerSources.
      /// </summary>
      public IReadOnlyList<IReadOnlyList<int>> PointerSourcesForInnerElements { get; }

      // composition of each element
      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public IReadOnlyList<string> ElementNames {
         get {
            var cache = ModelCacheScope.GetCache(owner);
            var options = cache.GetOptions(LengthFromAnchor);
            if (options.Count == 0) {
               var name = owner.GetAnchorFromAddress(-1, Start);
               options = cache.GetOptions(name);
            }

            if (options.Count == ElementCount - ParentOffset && ParentOffset != 0) {
               // if negative: pull values off the front
               if (ParentOffset < 0) options = options.Skip(-ParentOffset).ToList();
               // if positive: add values onto the back
               else options = options.Concat(Enumerable.Range(ElementCount, ParentOffset).Select(i => i.ToString())).ToList();
            }

            return options;
         }
      }

      private ArrayRun(IDataModel data, string format, int start, IReadOnlyList<int> pointerSources) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         SupportsPointersToElements = format.StartsWith(AnchorStart.ToString());
         if (SupportsPointersToElements) format = format.Substring(1);
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) throw new ArrayRunParseException($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}.");
         var segments = format.Substring(1, closeArray - 1);
         var length = format.Substring(closeArray + 1);
         if (!length.All(c => char.IsLetterOrDigit(c) || c.IsAny('-', '+'))) throw new ArrayRunParseException("Array length must be an anchor name or a number."); // the name might end with "-1" so also allow +/-
         ElementContent = ParseSegments(segments, data);
         if (ElementContent.Count == 0) throw new ArrayRunParseException("Array Content must not be empty.");
         ElementLength = ElementContent.Sum(e => e.Length);

         FormatMatchFlags flags = default;
         if (ElementContent.Count == 1) flags |= FormatMatchFlags.IsSingleSegment;

         if (length.Length == 0) {
            var nextRun = owner.GetNextAnchor(Start + ElementLength);
            for (; true; nextRun = owner.GetNextAnchor(nextRun.Start + nextRun.Length)) {
               if (nextRun.Start > owner.Count) break;
               var anchorName = owner.GetAnchorFromAddress(-1, nextRun.Start);
               if (string.IsNullOrEmpty(anchorName)) continue;
               if ((nextRun.Start - Start) % ElementLength != 0) break;
            }
            var byteLength = 0;
            var elementCount = 0;
            while (Start + byteLength + ElementLength <= nextRun.Start && DataMatchesElementFormat(owner, Start + byteLength, ElementContent, flags, nextRun)) {
               byteLength += ElementLength;
               elementCount++;
               if (elementCount == JunkLimit) flags |= FormatMatchFlags.AllowJunkAfterText;
            }
            LengthFromAnchor = string.Empty;
            ElementCount = Math.Max(1, elementCount); // if the user said there's a format here, then there is, even if the format it wrong.
            FormatString += ElementCount;
         } else if (int.TryParse(length, out int result)) {
            // fixed length is easy
            LengthFromAnchor = string.Empty;
            ElementCount = Math.Max(1, result);
         } else {
            (LengthFromAnchor, ParentOffset, ElementCount) = ParseLengthFromAnchor(length);
            if (ElementCount < 1) throw new ArrayRunParseException("Tables must have a length of at least 1 element.");
         }

         Length = ElementLength * ElementCount;
      }

      private ArrayRun(IDataModel data, string format, string lengthFromAnchor, int parentOffset, int start, int elementCount, IReadOnlyList<ArrayRunElementSegment> segments, IReadOnlyList<int> pointerSources, IReadOnlyList<IReadOnlyList<int>> pointerSourcesForInnerElements) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         ElementContent = segments;
         ElementLength = ElementContent.Sum(e => e.Length);
         ElementCount = elementCount;
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         LengthFromAnchor = lengthFromAnchor;
         ParentOffset = parentOffset;
         Length = ElementLength * ElementCount;
         SupportsPointersToElements = pointerSourcesForInnerElements != null;
         PointerSourcesForInnerElements = pointerSourcesForInnerElements;
      }

      public static ErrorInfo TryParse(IDataModel data, string format, int start, IReadOnlyList<int> pointerSources, out ITableRun self) => TryParse(data, "UNUSED", format, start, pointerSources, out self);

      public static ErrorInfo TryParse(IDataModel data, string name, string format, int start, IReadOnlyList<int> pointerSources, out ITableRun self) {
         self = null;
         var startArray = format.IndexOf(ArrayStart);
         var closeArray = format.LastIndexOf(ArrayEnd);
         if (startArray == -1 || startArray > closeArray) return new ErrorInfo($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}.");
         var length = format.Substring(closeArray + 1);

         var sourceSegments = 
            data.GetUnmappedSourcesToAnchor(name)
            .Select(source => data.GetNextRun(source) as ITableRun)
            .Where(tRun => tRun != null)
            .Select(tRun => tRun.ElementContent)
            .FirstOrDefault();

         if (length.All(c => char.IsLetterOrDigit(c) || c.IsAny('-', '+'))) {
            // option 1: the length looks like a standard table length (or is empty, and thus dynamic). Parse as a table.
            try {
               using (ModelCacheScope.CreateScope(data)) {
                  self = new ArrayRun(data, format, start, pointerSources);
               }
            } catch (ArrayRunParseException e) {
               return new ErrorInfo(e.Message);
            }
         } else if (TableStreamRun.TryParseTableStream(data, start, pointerSources, name, format, sourceSegments, out var tableStreamRun)) {
            // option 2: parse as a table stream, because the length contains characters like / or ! that make it look dependent on 
            self = tableStreamRun;
         } else {
            // option 3: table parsing failed. Something weird in the length.
            return new ErrorInfo($"Array length must be an anchor name or a number: {length}");
         }

         return ErrorInfo.NoError;
      }

      public static bool TrySearch(IDataModel data, ModelDelta changeToken, string originalFormat, out ArrayRun self, Func<IFormattedRun, bool> runFilter = null) {
         self = null;
         var format = originalFormat;
         var allowPointersToEntries = format.StartsWith(AnchorStart.ToString());
         if (allowPointersToEntries) format = format.Substring(1);
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) throw new ArrayRunParseException($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}");
         var segments = format.Substring(1, closeArray - 1);
         var length = format.Substring(closeArray + 1);
         var elementContent = ParseSegments(segments, data);
         if (elementContent.Count == 0) return false;
         var elementLength = elementContent.Sum(e => e.Length);

         using (ModelCacheScope.CreateScope(data)) {
            if (string.IsNullOrEmpty(length)) {
               var bestAddress = StandardSearch(data, elementContent, elementLength, out int bestLength, runFilter);
               if (bestAddress == Pointer.NULL) return false;
               self = new ArrayRun(data, originalFormat + bestLength, string.Empty, 0, bestAddress, bestLength, elementContent, data.GetNextRun(bestAddress).PointerSources, null);
            } else {
               var bestAddress = KnownLengthSearch(data, elementContent, elementLength, length, out int bestLength, runFilter);
               if (bestAddress == Pointer.NULL) return false;
               var lengthFromAnchor = int.TryParse(length, out var _) ? string.Empty : length;
               self = new ArrayRun(data, originalFormat, lengthFromAnchor, 0, bestAddress, bestLength, elementContent, data.GetNextRun(bestAddress).PointerSources, null);
            }
         }

         if (allowPointersToEntries) self = self.AddSourcesPointingWithinArray(changeToken);
         return true;
      }

      private static int StandardSearch(IDataModel data, List<ArrayRunElementSegment> elementContent, int elementLength, out int bestLength, Func<IFormattedRun, bool> runFilter) {
         int bestAddress = Pointer.NULL;
         bestLength = 0;

         var run = data.GetNextAnchor(0);
         for (var nextRun = data.GetNextAnchor(run.Start + run.Length); run.Start < int.MaxValue; nextRun = data.GetNextAnchor(nextRun.Start + nextRun.Length)) {
            if (run is ArrayRun || run.PointerSources == null) {
               run = nextRun;
               continue;
            }
            var nextArray = nextRun;

            // some searches allow special conditions on the run. For example, we could only be intersted in runs with >100 pointers leading to it.
            if (runFilter != null && !runFilter(run)) { run = nextRun; continue; }

            FormatMatchFlags flags = default;
            if (elementContent.Count == 1) flags |= FormatMatchFlags.IsSingleSegment;

            int currentLength = 0;
            int currentAddress = run.Start;
            while (true) {
               if (currentLength > JunkLimit) flags |= FormatMatchFlags.AllowJunkAfterText; // we've gone long enough without junk data to be fairly sure that we're looking at something real
               if (nextArray.Start < currentAddress) nextArray = data.GetNextAnchor(nextArray.Start + 1);
               if (DataMatchesElementFormat(data, currentAddress, elementContent, flags, nextArray)) {
                  currentLength++;
                  currentAddress += elementLength;
               } else {
                  break;
               }
            }

            // if what we found is just a text array, then remove any trailing elements starting with a space.
            if (elementContent.Count == 1 && elementContent[0].Type == ElementContentType.PCS) {
               while (data[currentAddress - elementLength] == 0x00) {
                  currentLength--;
                  currentAddress -= elementLength;
               }
            }

            // we think we found some data! Make sure it's not just a bunch of 00's and FF's
            var dataEmpty = true;
            for (int i = 0; i < currentLength && currentLength > bestLength && dataEmpty; i++) dataEmpty = data[run.Start + i] == 0xFF || data[run.Start + i] == 0x00;

            if (bestLength < currentLength && !dataEmpty) {
               bestLength = currentLength;
               bestAddress = run.Start;
            }

            run = nextRun;
         }

         return bestAddress;
      }

      private static int KnownLengthSearch(IDataModel data, List<ArrayRunElementSegment> elementContent, int elementLength, string lengthToken, out int bestLength, Func<IFormattedRun, bool> runFilter) {
         var noChange = new NoDataChangeDeltaModel();
         if (!int.TryParse(lengthToken, out bestLength)) {
            var matchedArrayName = lengthToken;
            var matchedArrayAddress = data.GetAddressFromAnchor(noChange, -1, matchedArrayName);
            if (matchedArrayAddress == Pointer.NULL) return Pointer.NULL;
            if (!(data.GetNextRun(matchedArrayAddress) is ArrayRun matchedRun)) return Pointer.NULL;
            bestLength = matchedRun.ElementCount;
         }

         FormatMatchFlags flags = default;
         if (elementContent.Count == 1) flags |= FormatMatchFlags.IsSingleSegment;

         for (var run = data.GetNextRun(0); run.Start < data.Count; run = data.GetNextRun(run.Start + run.Length + 1)) {
            if (!(run is PointerRun)) continue;
            var targetRun = data.GetNextRun(data.ReadPointer(run.Start));
            if (targetRun is ArrayRun) continue;

            // some searches allow special conditions on the run. For example, we could only be intersted in runs with >100 pointers leading to it.
            if (runFilter != null && !runFilter(targetRun)) continue;

            // tolerate a few errors in the data. We know what length we're looking for, so if most of the elements match, then
            // most likely we're just looking at the right collection but with some user-created bugs.
            int errorsToTolerate = bestLength / 80;
            int encounterErrors = 0;
            int lastGoodLength = 0;
            int currentLength = 0;
            int currentAddress = targetRun.Start;
            bool earlyExit = false;
            for (int i = 0; i < bestLength; i++) {
               var nextArray = data.GetNextAnchor(currentAddress + 1);
               bool match = DataMatchesElementFormat(data, currentAddress, elementContent, flags, nextArray);
               currentLength++;
               currentAddress += elementLength;
               if (match) {
                  lastGoodLength = currentLength;
               } else {
                  encounterErrors++;
                  if (encounterErrors > errorsToTolerate) {
                     // as long as this array is at least 80% of the passed in array, we're fine and can say that these are matched.
                     // (the other one might have bad data at the end that needs to be removed) (example: see Gaia)
                     earlyExit = bestLength * .8 > lastGoodLength;
                     break;
                  }
               }
            }
            currentLength = lastGoodLength;

            if (!earlyExit) {
               var dataEmpty = Enumerable.Range(targetRun.Start, currentLength * elementLength).Select(i => data[i]).All(d => d == 0xFF || d == 0x00);
               if (dataEmpty) continue; // don't accept the run if it contains no data
               bestLength = currentLength;
               return targetRun.Start;
            }
         }

         return Pointer.NULL;
      }

      private string cachedCurrentString;
      private int currentCachedStartIndex = -1, currentCachedIndex = -1;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var offsets = this.ConvertByteOffsetToArrayOffset(index);
         var currentSegment = ElementContent[offsets.SegmentIndex];
         if (currentSegment.Type == ElementContentType.PCS) {
            if (currentCachedStartIndex != offsets.SegmentStart || currentCachedIndex > offsets.SegmentOffset) {
               currentCachedStartIndex = offsets.SegmentStart;
               currentCachedIndex = offsets.SegmentOffset;
               cachedCurrentString = PCSString.Convert(data, offsets.SegmentStart, currentSegment.Length);
            }

            return PCSRun.CreatePCSFormat(data, offsets.SegmentStart, index, cachedCurrentString);
         }

         return this.CreateSegmentDataFormat(data, index);
      }

      ITableRun ITableRun.Append(ModelDelta token, int elementCount) => Append(token, elementCount);
      public ArrayRun Append(ModelDelta token, int elementCount) {
         var lastArrayCharacterIndex = FormatString.LastIndexOf(ArrayEnd);
         var newFormat = FormatString.Substring(0, lastArrayCharacterIndex + 1);
         if (newFormat != FormatString) {
            if (!string.IsNullOrEmpty(LengthFromAnchor)) {
               newFormat += LengthFromAnchor;
               if (ParentOffset != 0) {
                  var parentOffset = ParentOffset.ToString();
                  if (!parentOffset.StartsWith("-")) parentOffset = '+' + parentOffset;
                  newFormat += parentOffset;
               }
            } else {
               newFormat += ElementCount + elementCount;
            }
         }

         // set default values based on the bytes from the previous element
         if (!(token is NoDataChangeDeltaModel)) {
            for (int i = 0; i < elementCount; i++) {
               for (int j = 0; j < ElementLength; j++) {
                  var newDataAddress = Start + Length + i * ElementLength + j;
                  var validData = owner[Start + Length - ElementLength + j];
                  token.ChangeData(owner, newDataAddress, validData);
               }
            }
         }

         var newInnerElementsSources = PointerSourcesForInnerElements?.ToList();

         if (newInnerElementsSources != null) {
            newInnerElementsSources = new List<IReadOnlyList<int>>(PointerSourcesForInnerElements);

            // add extra elements at the back. Since this is a new element being added, it's fair to think that nothing points to it.
            for (int i = 0; i < elementCount; i++) {
               newInnerElementsSources.Add(new List<int>());
            }

            // remove extra elements at the back. Add NoInfoRuns for the pointers to point to.
            for (int i = 0; i < -elementCount; i++) {
               newInnerElementsSources.RemoveAt(newInnerElementsSources.Count - 1);

               // TODO add the run
               throw new NotImplementedException();
            }
         }

         return new ArrayRun(owner, newFormat, LengthFromAnchor, ParentOffset, Start, ElementCount + elementCount, ElementContent, PointerSources, newInnerElementsSources);
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);

      public ArrayRun GrowBitArraySegment(int bitSegmentIndex, int additionalBytes) {
         // all the data has been moved already
         // just return a new ArrayRun with the desired change.
         var content = ElementContent.ToList();
         var oldSegment = (ArrayRunBitArraySegment)content[bitSegmentIndex];
         content[bitSegmentIndex] = new ArrayRunBitArraySegment(oldSegment.Name, oldSegment.Length + additionalBytes, oldSegment.SourceArrayName);
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, content, PointerSources, PointerSourcesForInnerElements);
      }

      public ArrayRun AddSourcePointingWithinArray(int source) {
         var destination = owner.ReadPointer(source);
         var index = (destination - Start) / ElementLength;
         if (index < 0 || index >= ElementCount) throw new IndexOutOfRangeException();
         if (index == 0) throw new NotImplementedException();
         var newInnerPointerSources = PointerSourcesForInnerElements.ToList();
         newInnerPointerSources[index] = newInnerPointerSources[index].Concat(new[] { source }).ToList();
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, PointerSources, newInnerPointerSources);
      }

      public ArrayRun AddSourcesPointingWithinArray(ModelDelta changeToken) {
         if (ElementCount < 2) return this;

         var destinations = new int[ElementCount - 1];
         for (int i = 1; i < ElementCount; i++) destinations[i - 1] = Start + ElementLength * i;

         var sources = owner.SearchForPointersToAnchor(changeToken, destinations);

         var results = new List<List<int>> {
            PointerSources?.ToList() ?? new List<int>()
         };
         for (int i = 1; i < ElementCount; i++) results.Add(new List<int>());

         foreach (var source in sources) {
            var destination = owner.ReadPointer(source);
            int destinationIndex = (destination - Start) / ElementLength;
            results[destinationIndex].Add(source);
         }

         var pointerSourcesForInnerElements = results.Cast<IReadOnlyList<int>>().ToList();
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, PointerSources, pointerSourcesForInnerElements);
      }

      /// <summary>
      /// Arrays might want custom column headers.
      /// If so, this method can get them.
      /// </summary>
      public IReadOnlyList<HeaderRow> GetColumnHeaders(int columnCount, int startingDataIndex) {
         // check if it's a multiple of the array width
         if (columnCount >= ElementLength) {
            if (columnCount % ElementLength != 0) return null;
            return new[] { new HeaderRow(this, startingDataIndex - Start, columnCount) };
         }

         // check if it's a divisor of the array width
         if (ElementLength % columnCount != 0) return null;
         var segments = ElementLength / columnCount;
         return Enumerable.Range(0, segments).Select(i => new HeaderRow(this, columnCount * i + startingDataIndex - Start, columnCount)).ToList();
      }

      public ArrayRun Move(int newStart) {
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, newStart, ElementCount, ElementContent, PointerSources, PointerSourcesForInnerElements);
      }

      public override IFormattedRun RemoveSource(int source) {
         if (!SupportsPointersToElements) return base.RemoveSource(source);
         var newPointerSources = PointerSources.Where(item => item != source).ToList();
         var newInnerPointerSources = new List<IReadOnlyList<int>>();
         foreach (var list in PointerSourcesForInnerElements) {
            newInnerPointerSources.Add(list.Where(item => item != source).ToList());
         }

         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, newPointerSources, newInnerPointerSources);
      }

      public bool HasSameSegments(ArrayRun other) {
         if (other == null) return false;
         if (other.ElementContent.Count != ElementContent.Count) return false;
         for (int i = 0; i < ElementContent.Count; i++) {
            var mine = ElementContent[i];
            var theirs = other.ElementContent[i];
            if (mine.Type != theirs.Type || mine.Length != theirs.Length) return false;
            if (mine is ArrayRunEnumSegment enumSegment) {
               if (!(theirs is ArrayRunEnumSegment enumSegment2)) return false;
               if (enumSegment.EnumName != enumSegment2.EnumName) return false;
            }
            if (mine is ArrayRunPointerSegment pointerSegment) {
               if (!(theirs is ArrayRunPointerSegment pointerSegment2)) return false;
               if (pointerSegment.InnerFormat != pointerSegment2.InnerFormat) return false;
            }
         }
         return true;
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) {
         // since the inner pointer sources includes the first row, update the first row
         List<IReadOnlyList<int>> newInnerPointerSources = null;
         if (PointerSourcesForInnerElements != null) {
            newInnerPointerSources = new List<IReadOnlyList<int>> { newPointerSources };
            for (int i = 1; i < PointerSourcesForInnerElements.Count; i++) newInnerPointerSources.Add(PointerSourcesForInnerElements[i]);
         }

         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, newPointerSources, newInnerPointerSources);
      }

      public static List<ArrayRunElementSegment> ParseSegments(string segments, IDataModel model) {
         var list = new List<ArrayRunElementSegment>();
         segments = segments.Trim();
         while (segments.Length > 0) {
            if (segments.StartsWith("[")) {
               int subArrayClose = segments.LastIndexOf("]");
               if (subArrayClose == -1) throw new ArrayRunParseException("Found unmatched open bracket ([).");
               var innerSegments = ParseSegments(segments.Substring(1, subArrayClose - 1), model);
               segments = segments.Substring(subArrayClose + 1);
               int repeatLength = 0;
               while (repeatLength < segments.Length && char.IsDigit(segments[repeatLength])) repeatLength++;
               var innerCount = int.Parse(segments.Substring(0, repeatLength));
               for (int i = 0; i < innerCount; i++) list.AddRange(innerSegments);
               segments = segments.Substring(repeatLength);
               continue;
            }

            int nameEnd = 0;
            while (nameEnd < segments.Length && char.IsLetterOrDigit(segments[nameEnd])) nameEnd++;
            var name = segments.Substring(0, nameEnd);
            if (name == string.Empty) throw new ArrayRunParseException("expected name, but none was found: " + segments);
            segments = segments.Substring(nameEnd);
            var (format, formatLength, segmentLength) = ExtractSingleFormat(segments, model);

            // check to see if a name or length is part of the format
            if (format == ElementContentType.Integer && segments.Length > formatLength && segments[formatLength] != ' ') {
               segments = segments.Substring(formatLength);
               if (int.TryParse(segments, out var elementCount)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunEnumSegment(name, segmentLength, elementCount.ToString()));
               } else {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  var enumName = segments.Substring(0, endOfToken);
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunEnumSegment(name, segmentLength, enumName));
               }
            } else if (format == ElementContentType.Pointer && formatLength > 2) {
               var pointerSegment = new ArrayRunPointerSegment(name, segments.Substring(1, formatLength - 2));
               if (!pointerSegment.IsInnerFormatValid) throw new ArrayRunParseException($"pointer format '{pointerSegment.InnerFormat}' was not understood.");
               list.Add(pointerSegment);
               segments = segments.Substring(formatLength).Trim();
            } else if (format == ElementContentType.BitArray) {
               var sourceName = segments.Substring(BitArray.SharedFormatString.Length, formatLength - BitArray.SharedFormatString.Length);
               segments = segments.Substring(formatLength).Trim();
               list.Add(new ArrayRunBitArraySegment(name, segmentLength, sourceName));
            } else {
               segments = segments.Substring(formatLength).Trim();
               if (format == ElementContentType.Unknown) {
                  // default to single byte integer
                  format = ElementContentType.Integer;
                  segmentLength = 1;
               }
               list.Add(new ArrayRunElementSegment(name, format, segmentLength));
            }
         }

         return list;
      }

      private static (ElementContentType format, int formatLength, int segmentLength) ExtractSingleFormat(string segments, IDataModel model) {
         if (segments.Length >= 2 && segments.Substring(0, 2) == PCSRun.SharedFormatString) {
            var format = ElementContentType.PCS;
            var formatLength = 2;
            while (formatLength < segments.Length && char.IsDigit(segments[formatLength])) formatLength++;
            if (int.TryParse(segments.Substring(2, formatLength - 2), out var segmentLength)) {
               return (format, formatLength, segmentLength);
            }
         } else if (segments.StartsWith(DoubleByteIntegerFormat + string.Empty + DoubleByteIntegerFormat)) {
            return (ElementContentType.Integer, 2, 4);
         } else if (segments.StartsWith(DoubleByteIntegerFormat + string.Empty + SingleByteIntegerFormat) || segments.StartsWith(".:")) {
            return (ElementContentType.Integer, 2, 3);
         } else if (segments.StartsWith(DoubleByteIntegerFormat.ToString())) {
            return (ElementContentType.Integer, 1, 2);
         } else if (segments.StartsWith(SingleByteIntegerFormat.ToString())) {
            return (ElementContentType.Integer, 1, 1);
         } else if (segments.Length > 0 && segments[0] == PointerRun.PointerStart) {
            var openCount = 1;
            var endIndex = 1;
            while (openCount > 0 && endIndex < segments.Length) {
               if (segments[endIndex] == PointerRun.PointerStart) openCount += 1;
               else if (segments[endIndex] == PointerRun.PointerEnd) openCount -= 1;
               endIndex += 1;
            }
            if (openCount > 0) return (ElementContentType.Unknown, 0, 0);
            return (ElementContentType.Pointer, endIndex, 4);
         } else if (segments.StartsWith(BitArray.SharedFormatString)) {
            var endIndex = BitArray.SharedFormatString.Length;
            while (segments.Length > endIndex && char.IsLetterOrDigit(segments[endIndex])) endIndex++;
            var format = segments.Substring(0, endIndex);
            var name = format.Substring(BitArray.SharedFormatString.Length);
            var options = model.GetBitOptions(name);
            var count = options?.Count ?? 8;
            return (ElementContentType.BitArray, format.Length, (int)Math.Ceiling(count / 8.0));
         }

         return (ElementContentType.Unknown, 0, 0);
      }

      private (string lengthFromAnchor, int parentOffset, int elementCount) ParseLengthFromAnchor(string length) {
         var parts = length.Split("-");
         if (parts.Length == 1) parts = length.Split("+");
         if (parts.Length == 2 && int.TryParse(parts[1], out int parentOffset)) {
            if (length.Contains("-")) parentOffset = -parentOffset;
         } else {
            parentOffset = 0;
         }
         var lengthFromAnchor = parts[0];


         // length is based on another array
         int address = owner.GetAddressFromAnchor(new ModelDelta(), -1, lengthFromAnchor);
         if (address == Pointer.NULL) {
            // the requested name was unknown... length is zero for now
            return (lengthFromAnchor, parentOffset, 1);
         }

         if (!(owner.GetNextRun(address) is ArrayRun run) || run.Start != address) {
            // the requested name was not an array, or did not start where anticipated
            // length is zero for now
            return (lengthFromAnchor, parentOffset, 1);
         }

         return (lengthFromAnchor, parentOffset, run.ElementCount + parentOffset);
      }

      private static bool DataMatchesElementFormat(IDataModel owner, int start, IReadOnlyList<ArrayRunElementSegment> segments, FormatMatchFlags flags, IFormattedRun nextAnchor) {
         foreach (var segment in segments) {
            if (start + segment.Length > owner.Count) return false;
            if (start + segment.Length > nextAnchor.Start && nextAnchor is ArrayRun) return false; // don't blap over existing arrays
            if (!DataMatchesSegmentFormat(owner, start, segment, flags, segments)) return false;
            start += segment.Length;
         }
         return true;
      }

      public static bool DataMatchesSegmentFormat(IDataModel owner, int start, ArrayRunElementSegment segment, FormatMatchFlags flags, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         Debug.Assert(sourceSegments.Contains(segment), "Expected segment to be one among sourceSegments.");
         switch (segment.Type) {
            case ElementContentType.PCS:
               int readLength = PCSString.ReadString(owner, start, true, segment.Length);
               if (readLength < 1) return false;
               if (readLength > segment.Length) return false;
               if (Enumerable.Range(start, segment.Length).All(i => owner[i] == 0xFF)) return false;

               // if we end with a space, and the next one starts with a space, we probably have the data width wrong.
               // We might be the start of a different data segment that is no longer pointed to. (Example: Vega/pokenames)
               // only do this check if the current element seems useful
               var isBlank = Enumerable.Range(start, segment.Length).All(i => owner[i] == 0x00 || owner[i] == 0xFF);
               if (!isBlank && flags.HasFlag(FormatMatchFlags.IsSingleSegment) && start % 4 == 0 && owner[start + segment.Length - 1] == 0x00 && owner[start + segment.Length] == 0x00) {
                  // if the next one starts on a 4-byte boundary, then we probably just skipped a few bytes between different data types, and _this_ section is still part of the _last_ run (example, Emerald Ability names)
                  // if the next one doesn't start on a 4-byte boundary, then we probably have the length wrong
                  var nextWordStart = (start + segment.Length + 3) / 4 * 4;
                  if (Enumerable.Range(start + segment.Length, nextWordStart - start - segment.Length).Any(i => owner[i] != 0x00) || owner[nextWordStart] == 0x00) return false;
               }

               // require that the overall thing still ends with 'FF' or '00' to avoid finding text of the wrong width.
               // the width check is less important if we have more complex data, so relax the condition (example: Clover)
               // the width check is less important if we're already known to be in a long run (example: Gaia moves)
               var lastByteInText = owner[start + segment.Length - 1];
               var lastByteIsReasonablEnd = lastByteInText == 0x00 || lastByteInText == 0xFF;
               if (!flags.HasFlag(FormatMatchFlags.AllowJunkAfterText) && !lastByteIsReasonablEnd && flags.HasFlag(FormatMatchFlags.IsSingleSegment)) return false;

               return true;
            case ElementContentType.Integer:
               if (segment is ArrayRunEnumSegment enumSegment) {
                  var options = enumSegment.GetOptions(owner).ToList();
                  return owner.ReadMultiByteValue(start, segment.Length) < options.Count;
               } else {
                  if (flags.HasFlag(FormatMatchFlags.AllowJunkAfterText)) return true; // don't bother verifying if junk is allowed
                  return segment.Length < 4 || owner[start + 3] < 0x08; // we want an integer, not a pointer
               }
            case ElementContentType.Pointer:
               var destination = owner.ReadPointer(start);
               if (destination == Pointer.NULL) return true;
               if (0 > destination || destination > owner.Count) return false;
               if (segment is ArrayRunPointerSegment pointerSegment) {
                  if (!pointerSegment.DestinationDataMatchesPointerFormat(owner, new NoDataChangeDeltaModel(), start, destination, sourceSegments)) return false;
               }
               return true;
            case ElementContentType.BitArray:
               var bitArraySegment = (ArrayRunBitArraySegment)segment;
               var bits = bitArraySegment.GetOptions(owner).Count;
               bits %= 8;
               if (bits == 0) return true;
               var finalByte = owner[start + bitArraySegment.Length - 1];
               finalByte >>= bits;
               return finalByte == 0; // all the unneeded bits should be set to zero
            default:
               throw new NotImplementedException();
         }
      }

      [Flags]
      public enum FormatMatchFlags {
         IsSingleSegment = 0x01,
         AllowJunkAfterText = 0x02,
      }
   }

   public class ArrayRunParseException : Exception {
      public ArrayRunParseException(string message) : base(message) { }
   }
}
