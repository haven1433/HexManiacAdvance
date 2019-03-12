using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public enum ElementContentType {
      Unknown,
      PCS,
      Integer,
      Pointer,
   }

   public class ArrayRunElementSegment {
      public string Name { get; }
      public ElementContentType Type { get; }
      public int Length { get; }
      public ArrayRunElementSegment(string name, ElementContentType type, int length) => (Name, Type, Length) = (name, type, length);

      public string ToText(IReadOnlyList<byte> rawData, int offset) {
         switch (Type) {
            case ElementContentType.PCS:
               return PCSString.Convert(rawData, offset, Length);
            case ElementContentType.Integer:
               return ToInteger(rawData, offset, Length).ToString();
            default:
               throw new NotImplementedException();
         }
      }

      public static int ToInteger(IReadOnlyList<byte> data, int offset, int length) {
         int result = 0;
         int multiplier = 1;
         for (int i = 0; i < length; i++) {
            result += data[offset + i] * multiplier;
            multiplier *= 0x100;
         }
         return result;
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

   public class ArrayRun : BaseRun {
      public const char ExtendArray = '+';
      public const char ArrayStart = '[';
      public const char ArrayEnd = ']';
      public const char SingleByteIntegerFormat = '.';
      public const char DoubleByteIntegerFormat = ':';

      private readonly IDataModel owner;

      // length in bytes of the entire array
      public override int Length { get; }

      public override string FormatString { get; }

      // number of elements in the array
      public int ElementCount { get; }

      // length of each element
      public int ElementLength { get; }

      public string LengthFromAnchor { get; }

      public bool SupportsPointersToElements { get; }

      /// <summary>
      /// For Arrays that support pointers to individual elements within the array,
      /// This is the set of sources that points to each index of the array.
      /// The first set of sources (PointerSourcesForInnerElements[0]) should always be the same as PointerSources.
      /// </summary>
      public IReadOnlyList<IReadOnlyList<int>> PointerSourcesForInnerElements { get; }

      // composition of each element
      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      private ArrayRun(IDataModel data, string format, int start, IReadOnlyList<int> pointerSources) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         SupportsPointersToElements = format.StartsWith(AnchorStart.ToString());
         if (SupportsPointersToElements) format = format.Substring(1);
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) throw new ArrayRunParseException($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}.");
         var segments = format.Substring(1, closeArray - 1);
         var length = format.Substring(closeArray + 1);
         ElementContent = ParseSegments(segments);
         if (ElementContent.Count == 0) throw new ArrayRunParseException("Array Content must not be empty.");
         ElementLength = ElementContent.Sum(e => e.Length);

         if (length.Length == 0) {
            var nextRun = owner.GetNextRun(Start);
            while (nextRun is NoInfoRun && nextRun.Start < owner.Count) nextRun = owner.GetNextRun(nextRun.Start + 1);
            var byteLength = 0;
            var elementCount = 0;
            while (Start + byteLength + ElementLength <= nextRun.Start && DataMatchesElementFormat(owner, Start + byteLength, ElementContent, nextRun)) {
               byteLength += ElementLength;
               elementCount++;
            }
            ElementCount = elementCount;
         } else if (int.TryParse(length, out int result)) {
            // fixed length is easy
            ElementCount = result;
         } else {
            LengthFromAnchor = length;
            ElementCount = ParseLengthFromAnchor();
         }

         Length = ElementLength * ElementCount;
      }

      private ArrayRun(IDataModel data, string format, int start, int elementCount, IReadOnlyList<ArrayRunElementSegment> segments, IReadOnlyList<int> pointerSources, IReadOnlyList<IReadOnlyList<int>> pointerSourcesForInnerElements) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         ElementContent = segments;
         ElementLength = ElementContent.Sum(e => e.Length);
         ElementCount = elementCount;
         LengthFromAnchor = string.Empty;
         Length = ElementLength * ElementCount;
         SupportsPointersToElements = pointerSourcesForInnerElements != null;
         PointerSourcesForInnerElements = pointerSourcesForInnerElements;
      }

      public static ErrorInfo TryParse(IDataModel data, string format, int start, IReadOnlyList<int> pointerSources, out ArrayRun self) {
         try {
            self = new ArrayRun(data, format, start, pointerSources);
         } catch (ArrayRunParseException e) {
            self = null;
            return new ErrorInfo(e.Message);
         }

         return ErrorInfo.NoError;
      }

      public static bool TrySearch(IDataModel data, ModelDelta changeToken, string format, out ArrayRun self) {
         self = null;
         var allowPointersToEntries = format.StartsWith(AnchorStart.ToString());
         if (allowPointersToEntries) format = format.Substring(1);
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) throw new ArrayRunParseException($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}");
         var segments = format.Substring(1, closeArray - 1);
         var length = format.Substring(closeArray + 1);
         var elementContent = ParseSegments(segments);
         if (elementContent.Count == 0) return false;
         var elementLength = elementContent.Sum(e => e.Length);

         int bestAddress = Pointer.NULL;
         int bestLength = 0;

         var run = data.GetNextAnchor(0);
         for (var nextRun = data.GetNextAnchor(run.Start+run.Length); run.Start < int.MaxValue; nextRun = data.GetNextAnchor(nextRun.Start + nextRun.Length)) {
            if (run is ArrayRun || run.PointerSources == null) {
               run = nextRun;
               continue;
            }
            var nextArray = nextRun;

            int currentLength = 0;
            int currentAddress = run.Start;
            while (true) {
               if (nextArray.Start < currentAddress) nextArray = data.GetNextAnchor(nextArray.Start + 1);
               if (DataMatchesElementFormat(data, currentAddress, elementContent, nextArray)) {
                  currentLength++;
                  currentAddress += elementLength;
               } else {
                  break;
               }
            }
            if (bestLength < currentLength) {
               bestLength = currentLength;
               bestAddress = run.Start;
            }

            run = nextRun;
         }

         if (bestAddress == Pointer.NULL) return false;

         self = new ArrayRun(data, format + bestLength, bestAddress, bestLength, elementContent, data.GetNextRun(bestAddress).PointerSources, null);
         if (allowPointersToEntries) self = self.AddSourcesPointingWithinArray(changeToken);
         return true;
      }

      private string cachedCurrentString;
      private int currentCachedStartIndex = -1, currentCachedIndex = -1;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var offsets = ConvertByteOffsetToArrayOffset(index);
         var currentSegment = ElementContent[offsets.SegmentIndex];
         if (currentSegment.Type == ElementContentType.PCS) {
            if (currentCachedStartIndex != offsets.SegmentStart || currentCachedIndex > offsets.SegmentOffset) {
               currentCachedStartIndex = offsets.SegmentStart;
               currentCachedIndex = offsets.SegmentOffset;
               cachedCurrentString = PCSString.Convert(data, offsets.SegmentStart, currentSegment.Length);
            }

            return PCSRun.CreatePCSFormat(data, offsets.SegmentStart, index, cachedCurrentString);
         }

         if (currentSegment.Type == ElementContentType.Integer) {
            var value = ArrayRunElementSegment.ToInteger(data, offsets.SegmentStart, currentSegment.Length);
            return new Integer(offsets.SegmentStart, index, value, currentSegment.Length);
         }

         if (currentSegment.Type == ElementContentType.Pointer) {
            var destination = data.ReadPointer(offsets.SegmentStart);
            var destinationName = data.GetAnchorFromAddress(offsets.SegmentStart, destination);
            return new Pointer(offsets.SegmentStart, index - offsets.SegmentStart, destination, destinationName);
         }

         throw new NotImplementedException();
      }

      public ArrayOffset ConvertByteOffsetToArrayOffset(int byteOffset) {
         var offset = byteOffset - Start;
         int elementIndex = offset / ElementLength;
         int elementOffset = offset % ElementLength;
         int segmentIndex = 0, segmentOffset = elementOffset;
         while (ElementContent[segmentIndex].Length <= segmentOffset) {
            segmentOffset -= ElementContent[segmentIndex].Length; segmentIndex++;
         }
         return new ArrayOffset(elementIndex, segmentIndex, byteOffset - segmentOffset, segmentOffset);
      }

      public ArrayRun Append(int elementCount) {
         var lastArrayCharacterIndex = FormatString.LastIndexOf(ArrayEnd);
         var newFormat = FormatString.Substring(0, lastArrayCharacterIndex + 1);
         if (newFormat != FormatString) newFormat += ElementCount + elementCount;
         return new ArrayRun(owner, newFormat, Start, ElementCount + elementCount, ElementContent, PointerSources, PointerSourcesForInnerElements);
      }

      public ArrayRun AddSourcesPointingWithinArray(ModelDelta changeToken) {
         if (ElementCount < 2) return this;

         var destinations = new int[ElementCount - 1];
         for (int i = 1; i < ElementCount; i++) destinations[i - 1] = Start + ElementLength * i;

         var sources = owner.SearchForPointersToAnchor(changeToken, destinations);

         var results = new List<List<int>>();
         results.Add(PointerSources.ToList());
         for (int i = 1; i < ElementCount; i++) results.Add(new List<int>());

         foreach (var source in sources) {
            var destination = owner.ReadPointer(source);
            int destinationIndex = (destination - Start) / ElementLength;
            results[destinationIndex].Add(source);
         }

         var pointerSourcesForInnerElements = results.Cast<IReadOnlyList<int>>().ToList();
         return new ArrayRun(owner, FormatString, Start, ElementCount, ElementContent, PointerSources, pointerSourcesForInnerElements);
      }

      public void AppendTo(IReadOnlyList<byte> data, StringBuilder text) {
         for (int i = 0; i < ElementCount; i++) {
            var offset = Start + i * ElementLength;
            text.Append(ExtendArray);
            foreach (var segment in ElementContent) {
               text.Append(segment.ToText(data, offset).Trim());
               offset += segment.Length;
            }
            text.Append(Environment.NewLine);
         }
      }

      public IFormattedRun Move(int newStart) {
         return new ArrayRun(owner, FormatString, newStart, ElementCount, ElementContent, PointerSources, PointerSourcesForInnerElements);
      }

      public override IFormattedRun RemoveSource(int source) {
         if (!SupportsPointersToElements) return base.RemoveSource(source);
         var newPointerSources = PointerSources.Where(item => item != source).ToList();
         var newInnerPointerSources = new List<IReadOnlyList<int>>();
         foreach (var list in PointerSourcesForInnerElements) {
            newInnerPointerSources.Add(list.Where(item => item != source).ToList());
         }

         return new ArrayRun(owner, FormatString, Start, ElementCount, ElementContent, newPointerSources, newInnerPointerSources);
      }

      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         // since the inner pointer sources includes the first row, update the first row
         List<IReadOnlyList<int>> newInnerPointerSources = null;
         if (PointerSourcesForInnerElements != null) {
            newInnerPointerSources = new List<IReadOnlyList<int>>();
            newInnerPointerSources.Add(newPointerSources);
            for (int i = 1; i < PointerSourcesForInnerElements.Count; i++) newInnerPointerSources.Add(PointerSourcesForInnerElements[i]);
         }

         return new ArrayRun(owner, FormatString, Start, ElementCount, ElementContent, newPointerSources, newInnerPointerSources);
      }

      private static List<ArrayRunElementSegment> ParseSegments(string segments) {
         var list = new List<ArrayRunElementSegment>();
         segments = segments.Trim();
         while (segments.Length > 0) {
            int nameEnd = 0;
            while (nameEnd < segments.Length && char.IsLetterOrDigit(segments[nameEnd])) nameEnd++;
            var name = segments.Substring(0, nameEnd);
            if (name == string.Empty) throw new ArrayRunParseException("expected name, but none was found: " + segments);
            segments = segments.Substring(nameEnd);
            var format = ElementContentType.Unknown;
            int formatLength = 0;
            int segmentLength = 0;
            if (segments.Length >= 2 && segments.Substring(0, 2) == "\"\"") {
               format = ElementContentType.PCS;
               formatLength = 2;
               while (formatLength < segments.Length && char.IsDigit(segments[formatLength])) formatLength++;
               segmentLength = int.Parse(segments.Substring(2, formatLength - 2));
            } else if (segments.StartsWith(DoubleByteIntegerFormat + string.Empty + DoubleByteIntegerFormat)) {
               (format, formatLength, segmentLength) = (ElementContentType.Integer, 2, 4);
            } else if (segments.StartsWith(DoubleByteIntegerFormat + string.Empty + SingleByteIntegerFormat) || segments.StartsWith(".:")) {
               (format, formatLength, segmentLength) = (ElementContentType.Integer, 2, 3);
            } else if (segments.StartsWith(DoubleByteIntegerFormat.ToString())) {
               (format, formatLength, segmentLength) = (ElementContentType.Integer, 1, 2);
            } else if (segments.StartsWith(SingleByteIntegerFormat.ToString())) {
               (format, formatLength, segmentLength) = (ElementContentType.Integer, 1, 1);
            } else if (segments.StartsWith(PointerRun.PointerStart + string.Empty + PointerRun.PointerEnd)) {
               (format, formatLength, segmentLength) = (ElementContentType.Pointer, 2, 4);
            }

            if (format == ElementContentType.Unknown) throw new ArrayRunParseException($"Could not parse format '{segments}'");
            segments = segments.Substring(formatLength).Trim();
            list.Add(new ArrayRunElementSegment(name, format, segmentLength));
         }

         return list;
      }

      private int ParseLengthFromAnchor() {
         // length is based on another array
         int address = owner.GetAddressFromAnchor(new ModelDelta(), -1, LengthFromAnchor);
         if (address == Pointer.NULL) {
            // the requested name was unknown... length is zero for now
            return 0;
         }

         var run = owner.GetNextRun(address) as ArrayRun;
         if (run == null || run.Start != address) {
            // the requested name was not an array, or did not start where anticipated
            // length is zero for now
            return 0;
         }

         return run.ElementCount;
      }

      private static bool DataMatchesElementFormat(IDataModel owner, int start, IReadOnlyList<ArrayRunElementSegment> segments, IFormattedRun nextAnchor) {
         foreach (var segment in segments) {
            if (start + segment.Length > owner.Count) return false;
            if (!DataMatchesSegmentFormat(owner, start, segment, nextAnchor)) return false;
            start += segment.Length;
         }
         return true;
      }

      private static bool DataMatchesSegmentFormat(IDataModel owner, int start, ArrayRunElementSegment segment, IFormattedRun nextAnchor) {
         if (start + segment.Length > nextAnchor.Start && nextAnchor is ArrayRun) return false; // don't blap over existing arrays
         switch (segment.Type) {
            case ElementContentType.PCS:
               int readLength = PCSString.ReadString(owner, start, true, segment.Length);
               if (readLength == -1) return false;
               if (readLength > segment.Length) return false;
               if (Enumerable.Range(start, segment.Length).All(i => owner[i] == 0xFF)) return false;
               if (!Enumerable.Range(start + readLength, segment.Length - readLength).All(i => owner[i] == 0x00 || owner[i] == 0xFF)) return false;
               return true;
            case ElementContentType.Integer:
               return true;
            case ElementContentType.Pointer:
               var destination = owner.ReadPointer(start);
               if (destination == Pointer.NULL) return true;
               return 0 <= destination && destination <= owner.Count;
            default:
               throw new NotImplementedException();
         }
      }
   }

   public class ArrayRunParseException : Exception {
      public ArrayRunParseException(string message) : base(message) { }
   }
}
