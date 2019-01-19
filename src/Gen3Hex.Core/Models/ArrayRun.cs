using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.Models {
   public enum ElementContentType {
      Unknown,
      PCS,
   }

   public class ArrayRunElementSegment {
      public string Name { get; }
      public ElementContentType Type { get; }
      public int Length { get; }
      public ArrayRunElementSegment(string name, ElementContentType type, int length) => (Name, Type, Length) = (name, type, length);
   }

   public class ArrayOffset {
      public int ElementIndex { get; }
      public int SegmentIndex { get; }
      public int SegmentStart { get; }
      public int SegmentOffset { get; }
      public ArrayOffset(int elementIndex, int segmentIndex, int segmentStart, int segmentOffset) {
         ElementIndex = elementIndex;
         SegmentIndex = segmentIndex;
         SegmentStart = segmentStart;
         SegmentOffset = segmentOffset;
      }
   }

   public class ArrayRun : BaseRun {
      public const char ArrayStart = '[';
      public const char ArrayEnd = ']';

      private readonly IModel owner;

      // length in bytes of the entire array
      public override int Length { get; }

      public override string FormatString { get; }

      // number of elements in the array
      public int ElementCount { get; }

      // length of each element
      public int ElementLength { get; }

      public string LengthFromAnchor { get; }

      // composition of each element
      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      private ArrayRun(IModel data, string format, int start, IReadOnlyList<int> pointerSources) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) throw new FormatException($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}");
         var segments = format.Substring(1, closeArray - 1);
         var length = format.Substring(closeArray + 1);
         ElementContent = ParseSegments(segments);
         ElementLength = ElementContent.Sum(e => e.Length);

         if (length.Length == 0) {
            var nextRunStart = owner.GetNextRun(Start).Start;
            var byteLength = 0;
            var elementCount = 0;
            while (Start + byteLength + ElementLength <= nextRunStart && DataMatchesElementFormat(Start + byteLength)) {
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

      public static bool TryParse(IModel data, string format, int start, IReadOnlyList<int> pointerSources, out ArrayRun self) {
         try {
            self = new ArrayRun(data, format, start, pointerSources);
         } catch {
            self = null;
            return false;
         }

         return true;
      }

      public override IDataFormat CreateDataFormat(IModel data, int index) {
         var offsets = ConvertByteOffsetToArrayOffset(index);

         if (ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) {
            var fullString = PCSString.Convert(data, offsets.SegmentStart, ElementContent[offsets.SegmentIndex].Length);
            return PCSRun.CreatePCSFormat(data, offsets.SegmentStart, index, fullString);
         }

         throw new NotImplementedException();
      }

      public ArrayOffset ConvertByteOffsetToArrayOffset(int byteOffset) {
         var offset = byteOffset - Start;
         int elementIndex = offset / ElementLength;
         int elementOffset = offset % ElementLength;
         int segmentIndex = 0, segmentOffset = elementOffset;
         while (ElementContent[segmentIndex].Length < segmentOffset) {
            segmentOffset -= ElementContent[segmentIndex].Length; segmentIndex++;
         }
         return new ArrayOffset(elementIndex, segmentIndex, byteOffset - segmentOffset, segmentOffset);
      }

      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         return new ArrayRun(owner, FormatString, Start, newPointerSources);
      }

      private List<ArrayRunElementSegment> ParseSegments(string segments) {
         var list = new List<ArrayRunElementSegment>();
         segments = segments.Trim();
         while (segments.Length > 0) {
            int nameEnd = 0;
            while (nameEnd < segments.Length && char.IsLetterOrDigit(segments[nameEnd])) nameEnd++;
            var name = segments.Substring(0, nameEnd);
            if (name == string.Empty) throw new FormatException("expected name, but none was found: " + segments);
            segments = segments.Substring(nameEnd);
            var format = ElementContentType.Unknown;
            int formatLength = 0;
            int segmentLength = 0;
            if (segments.Length >= 2 && segments.Substring(0, 2) == "\"\"") {
               format = ElementContentType.PCS;
               formatLength = 2;
               while (formatLength < segments.Length && char.IsDigit(segments[formatLength])) formatLength++;
               segmentLength = int.Parse(segments.Substring(2, formatLength - 2));
            }
            if (format == ElementContentType.Unknown) throw new FormatException($"Could not parse format '{segments}'");
            segments = segments.Substring(formatLength).Trim();
            list.Add(new ArrayRunElementSegment(name, format, segmentLength));
         }

         return list;
      }

      private int ParseLengthFromAnchor() {
         // length is based on another array
         int address = owner.GetAddressFromAnchor(new DeltaModel(), -1, LengthFromAnchor);
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

      private bool DataMatchesElementFormat(int start) {
         foreach (var segment in ElementContent) {
            if (!DataMatchesSegmentFormat(start, segment)) return false;
            start += segment.Length;
         }
         return true;
      }

      private bool DataMatchesSegmentFormat(int start, ArrayRunElementSegment segment) {
         switch (segment.Type) {
            case ElementContentType.PCS:
               int readLength = PCSString.ReadString(owner, start, true, segment.Length);
               if (readLength == -1) return false;
               if (!Enumerable.Range(start + readLength, segment.Length - readLength).All(i => owner[i] == 0x00)) return false;
               return true;
            default:
               throw new NotImplementedException();
         }
      }
   }
}
