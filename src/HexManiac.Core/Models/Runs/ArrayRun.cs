using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public interface ITableRun : IAppendToBuilderRun {
      int ElementCount { get; }
      int ElementLength { get; }
      IReadOnlyList<string> ElementNames { get; }
      IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }
      bool CanAppend { get; }
      ITableRun Append(ModelDelta token, int length);
      ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments); // exists so that the segments can be replaced. Should not be used with fixed-style table runs, like osl and tpt.
   }

   public static class ITableRunExtensions {
      public static ITableRun GetTable(this IDataModel model, string name) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         return model.GetNextRun(address) as ITableRun;
      }

      /// <param name="byteOffset">Ranges from 0 to Model.Count</param>
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

      public static bool IsUnused(this ArrayRunElementSegment segment) {
         return segment.Name.StartsWith("unused") || segment.Name.StartsWith("padding");
      }

      public static IDataFormat CreateSegmentDataFormat(this ITableRun self, IDataModel data, int index) {
         var offsets = self.ConvertByteOffsetToArrayOffset(index);
         var currentSegment = self.ElementContent[offsets.SegmentIndex];
         var position = index - offsets.SegmentStart;
         if (currentSegment is ArrayRunRecordSegment recordSegment) currentSegment = recordSegment.CreateConcrete(data, index);
         if (currentSegment.Type == ElementContentType.Integer) {
            if (currentSegment is ArrayRunEnumSegment enumSegment) {
               var value = enumSegment.ToText(data, offsets.SegmentStart, false);
               return new IntegerEnum(offsets.SegmentStart, position, value, currentSegment.Length);
            } else if (currentSegment is ArrayRunTupleSegment tupleSegment) {
               return new ViewModels.DataFormats.Tuple(data, tupleSegment, offsets.SegmentStart, position);
            } else if (currentSegment is ArrayRunHexSegment) {
               var value = data.ReadMultiByteValue(offsets.SegmentStart, currentSegment.Length);
               return new IntegerHex(offsets.SegmentStart, position, value, currentSegment.Length) { IsUnused = currentSegment.IsUnused() };
            } else if (currentSegment is ArrayRunColorSegment) {
               var color = (short)data.ReadMultiByteValue(offsets.SegmentStart, currentSegment.Length);
               return new UncompressedPaletteColor(offsets.SegmentStart, position, color);
            } else if (currentSegment is ArrayRunSignedSegment signed) {
               var signedValue = signed.ReadValue(data, offsets.SegmentStart);
               return new Integer(offsets.SegmentStart, position, signedValue, currentSegment.Length);
            } else {
               var value = ArrayRunElementSegment.ToInteger(data, offsets.SegmentStart, currentSegment.Length);
               return new Integer(offsets.SegmentStart, position, value, currentSegment.Length) { IsUnused = currentSegment.IsUnused() };
            }
         }

         if (currentSegment.Type == ElementContentType.Pointer) {
            var destination = data.ReadPointer(offsets.SegmentStart);
            var destinationName = data.GetAnchorFromAddress(offsets.SegmentStart, destination);
            var destinationRun = data.GetNextRun(destination);
            var hasError = false;
            if (destination >= 0 && destination < data.Count) {
               if (destinationRun is ArrayRun arrayRun && arrayRun.SupportsInnerPointers) {
                  // it's an error unless the arrayRun starts before the destination and the destination is the start of one of the elements
                  hasError = !(arrayRun.Start <= destination && (destination - arrayRun.Start) % arrayRun.ElementLength == 0);
               } else {
                  IFormattedRun run = destinationRun;
                  hasError = destinationRun.Start != destination;
                  // if the run isn't a ITableRun, parse the data to see if it's valid
                  // if it _is_ an ITableRun, skip this step
                  if (currentSegment is ArrayRunPointerSegment pointerSegment && !(run is ITableRun)) {
                     hasError |= data.FormatRunFactory.GetStrategy(pointerSegment.InnerFormat).TryParseData(data, string.Empty, destination, ref run).HasError;
                  }
               }
            } else {
               hasError = true;
            }

            return new Pointer(offsets.SegmentStart, position, destination, 0, destinationName, hasError);
         }

         if (currentSegment.Type == ElementContentType.BitArray) {
            var displayValue = string.Join(" ", currentSegment.Length.Range()
                 .Select(i => data[offsets.SegmentStart + i].ToHexString()));
            return new BitArray(offsets.SegmentStart, position, currentSegment.Length, displayValue);
         }

         if (currentSegment.Type == ElementContentType.PCS) {
            return new PCS(offsets.SegmentStart, offsets.SegmentOffset, data.TextConverter.Convert(data, offsets.SegmentStart, currentSegment.Length), PCSString.PCS[index]);
         }

         throw new NotImplementedException();
      }

      public static void AppendTo(ITableRun self, IDataModel data, StringBuilder text, int start, int length, bool deep) {
         var names = self.ElementNames;
         var offsets = self.ConvertByteOffsetToArrayOffset(start);
         length += offsets.SegmentOffset;
         for (int i = offsets.ElementIndex; i < self.ElementCount && length > 0; i++) {
            var offset = offsets.SegmentStart;
            var couldBeExtension = offsets.ElementIndex > 0 || self is TableStreamRun streamRun && streamRun.AllowsZeroElements;
            if (offsets.SegmentIndex == 0 && couldBeExtension) text.Append(ArrayRun.ExtendArray);
            for (int j = offsets.SegmentIndex; j < self.ElementContent.Count && length > 0; j++) {
               var segment = self.ElementContent[j];
               if (j == 0 && segment.Type != ElementContentType.PCS && names != null && names.Count > i && !string.IsNullOrEmpty(names[i])) {
                  text.Append($"{ViewPort.CommentStart}{names[i]}{ViewPort.CommentStart}, ");
               }

               if (segment.Length > 0) {
                  text.Append(segment.ToText(data, offset, deep).Trim());
                  if (j + 1 < self.ElementContent.Count) text.Append(", ");
                  offset += segment.Length;
                  length -= segment.Length;
               }
            }
            if (i + 1 < self.ElementCount) text.Append(Environment.NewLine);
            offsets = new ArrayOffset(i + 1, 0, offset, 0);
         }
      }

      public static void Clear(ITableRun self, IDataModel data, ModelDelta token, int start, int length) {
         for (int i = 0; i < length; i++) {
            var offset = self.ConvertByteOffsetToArrayOffset(start + i);
            if (self.ElementContent[offset.SegmentIndex].Type == ElementContentType.PCS) {
               token.ChangeData(data, start + i, 0xFF);
            } else {
               token.ChangeData(data, start + i, 0x00);
            }
         }
      }

      public static bool DependsOn(this ITableRun self, string anchorName) {
         foreach (var segment in self.ElementContent) {
            if (segment is ArrayRunEnumSegment enumSegment && enumSegment.EnumName == anchorName) return true;
            if (segment is ArrayRunTupleSegment tupleSegment && tupleSegment.DependsOn(anchorName)) return true;
         }
         return false;
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
                  var newRun = teamRun.UpdateFromParent(token, segmentIndex, pointerSource, new HashSet<int>()); // we don't care about the changes that come back from here
                  model.ObserveRunWritten(token, newRun);
                  if (newRun.Start != teamRun.Start) info = new ErrorInfo($"Team was automatically moved to {newRun.Start:X6}. Pointers were updated.", isWarningLevel: true);
               } else if (run.Start == destination && run is OverworldSpriteListRun oslRun) {
                  var newRun = oslRun.UpdateFromParent(token, segmentIndex, pointerSource, out bool spritesMoved);
                  model.ObserveRunWritten(token, newRun);
                  if (newRun.Start != oslRun.Start) info = new ErrorInfo($"Overworld sprite was automatically moved to {newRun.Start:X6}. Pointers were updated.", isWarningLevel: true);
                  if (spritesMoved) info = new ErrorInfo($"Overworld sprites were automatically moved after resize. Pointers were updated.", isWarningLevel: true);
               } else if (run.Start == destination && run is TableStreamRun tableStreamRun) {
                  var newRun = tableStreamRun.UpdateFromParent(token, segmentIndex);
                  model.ObserveRunWritten(token, newRun);
                  if (newRun.Start != tableStreamRun.Start) info = new ErrorInfo($"Stream was automatically moved to {newRun.Start:X6}. Pointers were updated.", isWarningLevel: true);
               } else if (run.Start == destination && run is MapAnimationTilesRun matRun) {
                  var newRun = matRun.UpdateFromParent(token, segmentIndex, pointerSource, out bool childrenMoved);
                  model.ObserveRunWritten(token, newRun);
                  if (newRun.Start != matRun.Start) info = new ErrorInfo($"Tiles were automatically moved to {newRun.Start:X6}. Pointers were updated.", isWarningLevel: true);
               }
            }
            offset += segment.Length;
         }

         return info;
      }

      public static bool DataFormatMatches(this ITableRun self, ITableRun other) {
         if (self.ElementContent.Count != other.ElementContent.Count) return false;
         for (int i = 0; i < self.ElementContent.Count; i++) {
            if (self.ElementContent[i].Type != other.ElementContent[i].Type) return false;
            if (self.ElementContent[i].Length != other.ElementContent[i].Length) return false;
            if (self.ElementContent[i] is ArrayRunPointerSegment pSegment) {
               if (!(other.ElementContent[i] is ArrayRunPointerSegment pSegment2)) return false;
               if (pSegment.InnerFormat != pSegment2.InnerFormat) return false;
            }
            if (other.ElementContent[i] is ArrayRunPointerSegment && !(self.ElementContent[i] is ArrayRunPointerSegment)) return false;
         }
         return true;
      }

      public static bool DataFormatMatches(this IReadOnlyList<ArrayRunElementSegment> segments, IDataModel model, int start, int elementIndex) {
         ArrayRun.FormatMatchFlags flags = default;
         if (segments.Count == 1) flags = ArrayRun.FormatMatchFlags.IsSingleSegment;
         for (int i = 0; i < segments.Count; i++) {
            if (!ArrayRun.DataMatchesSegmentFormat(model, start, segments[i], flags, segments, elementIndex)) return false;
            start += segments[i].Length;
         }
         return true;
      }

      public static byte[] Sort(this ITableRun self, IDataModel model, int fieldIndex) {
         var fieldOffset = self.ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         var field = self.ElementContent[fieldIndex];
         var indexed = self.ElementCount.Range().ToList();
         int value(int index) => model.ReadMultiByteValue(self.Start + self.ElementLength * index + fieldOffset, field.Length);
         indexed.Sort((i, j) => value(i).CompareTo(value(j)));

         var result = new byte[self.ElementCount * self.ElementLength];
         for (int i = 0; i < self.ElementCount; i++) {
            var sourceIndex = indexed[i];
            for (int j = 0; j < self.ElementLength; j++) {
               result[i * self.ElementLength + j] = model[self.Start + self.ElementLength * sourceIndex + j];
            }
         }

         return result;
      }

      public static int ReadPointer(this ITableRun self, IDataModel model, int elementIndex, int fieldIndex = 0) {
         var fieldOffset = self.ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         return model.ReadPointer(self.Start + self.ElementLength * elementIndex + fieldOffset);
      }

      public static int ReadPointer(this ITableRun self, IDataModel model, int elementIndex, string fieldName) {
         var field = self.ElementContent.Single(seg => seg.Name == fieldName);
         var fieldIndex = self.ElementContent.IndexOf(field);
         return self.ReadPointer(model, elementIndex, fieldIndex);
      }

      public static void WritePointer(this ITableRun self, int destination, IDataModel model, ModelDelta token, int elementIndex, int fieldIndex = 0) {
         var fieldOffset = self.ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         model.WritePointer(token, self.Start + self.ElementLength * elementIndex + fieldOffset, destination);
      }

      public static void WritePointer(this ITableRun self, int destination, IDataModel model, ModelDelta token, int elementIndex, string fieldName) {
         var field = self.ElementContent.Single(seg => seg.Name == fieldName);
         var fieldIndex = self.ElementContent.IndexOf(field);
         self.WritePointer(destination, model, token, elementIndex, fieldIndex);
      }

      public static int ReadValue(this ITableRun self, IDataModel model, int elementIndex, int fieldIndex = 0) {
         var fieldOffset = self.ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         if (self.ElementContent[fieldIndex].Length == 0 && self.ElementContent[fieldIndex] is ArrayRunCalculatedSegment calcSeg) {
            return calcSeg.CalculatedValue(self.Start + elementIndex * self.ElementLength);
         }
         return model.ReadMultiByteValue(self.Start + self.ElementLength * elementIndex + fieldOffset, self.ElementContent[fieldIndex].Length);
      }

      public static int ReadValue(this ITableRun self, IDataModel model, int elementIndex, string fieldName) {
         var field = self.ElementContent.Single(seg => seg.Name == fieldName);
         var fieldIndex = self.ElementContent.IndexOf(field);
         return self.ReadValue(model, elementIndex, fieldIndex);
      }

      public static void WriteValue(this ITableRun self, int value, IDataModel model, ModelDelta token, int elementIndex, int fieldIndex = 0) {
         var fieldOffset = self.ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         if (self.ElementContent[fieldIndex].Length == 0 && self.ElementContent[fieldIndex] is ArrayRunCalculatedSegment calcSeg) {
            throw new NotImplementedException("Cannot write value to a calculated field!");
         }
         if (self.ElementContent[fieldIndex].Length == 0 && self.ElementContent[fieldIndex] is ArrayRunOffsetRenderSegment renderSeg) {
            throw new NotImplementedException("Cannot write value to a render field!");
         }
         model.WriteMultiByteValue(self.Start + self.ElementLength * elementIndex + fieldOffset, self.ElementContent[fieldIndex].Length, token, value);
      }

      public static void WriteValue(this ITableRun self, int value, IDataModel model, ModelDelta token, int elementIndex, string fieldName) {
         var field = self.ElementContent.Single(seg => seg.Name == fieldName);
         var fieldIndex = self.ElementContent.IndexOf(field);
         self.WriteValue(value, model, token, elementIndex, fieldIndex);
      }

      public static string ReadText(this ITableRun self, IDataModel model, int elementIndex, int fieldIndex = 0) {
         var segOffset = self.ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         var textStart = self.Start + self.ElementLength * elementIndex + segOffset;
         var length = PCSString.ReadString(model.RawData, textStart, true, self.ElementContent[fieldIndex].Length);
         return model.TextConverter.Convert(model.RawData, textStart, length);
      }

      public static IEnumerable<(int, int)> Search(this ITableRun self, IDataModel model, string baseName, int index) {
         int segmentOffset = 0;
         for (int i = 0; i < self.ElementContent.Count; i++) {
            if (self.ElementContent[i] is ArrayRunEnumSegment segment && segment.EnumName == baseName) {
               for (int j = 0; j < self.ElementCount; j++) {
                  var segmentStart = self.Start + j * self.ElementLength + segmentOffset;
                  if (model.ReadMultiByteValue(segmentStart, segment.Length) != index) continue;
                  yield return (segmentStart, segmentStart + segment.Length - 1);
               }
            }
            if (self.ElementContent[i] is ArrayRunTupleSegment tupSeg) {
               var bitOffset = 0;
               for (int tupleElementIndex = 0; tupleElementIndex < tupSeg.Elements.Count; tupleElementIndex++) {
                  if (tupSeg.Elements[tupleElementIndex].SourceName == baseName) {
                     for (int j = 0; j < self.ElementCount; j++) {
                        var segmentStart = self.Start + j * self.ElementLength + segmentOffset;
                        var enumValue = tupSeg.Elements[tupleElementIndex].Read(model, segmentStart, bitOffset);
                        if (enumValue != index) continue;
                        yield return (segmentStart, segmentStart + tupSeg.Length - 1);
                     }
                  }
                  bitOffset += tupSeg.Elements[tupleElementIndex].BitWidth;
               }
            }
            segmentOffset += self.ElementContent[i].Length;
         }
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

   public class ParentOffset : IEquatable<ParentOffset> {
      public static readonly ParentOffset Default = new ParentOffset();
      public int BeginningMargin { get; }
      public int EndMargin { get; }
      public ParentOffset(int start = 0, int end = 0) => (BeginningMargin, EndMargin) = (start, end);
      public static ParentOffset Parse(ref string nameToken) {
         var name = nameToken;
         var result = new ParentOffset();
         var separators = name.Length.Range().Where(i => name[i].IsAny("+-".ToCharArray())).Concat(new[] { name.Length }).ToArray();
         if (separators.Length == 1) return Default;
         var textMargins = (separators.Length - 1).Range().Select(i => name.Substring(separators[i], separators[i + 1] - separators[i]));
         var margins = textMargins.Select(str => int.TryParse(str, out var value) ? value : default).ToArray();
         if (margins.Length > 2 || margins.Length < 1) return Default;
         nameToken = name.Substring(0, separators[0]);
         if (margins.Length == 1 && margins[0] > 0) return new ParentOffset(end: margins[0]);
         if (margins.Length == 1 && margins[0] < 0) return new ParentOffset(start: margins[0]);
         if (margins.Length == 1) return Default;
         return new ParentOffset(margins[0], margins[1]);
      }
      public override string ToString() {
         if (BeginningMargin == 0) {
            if (EndMargin == 0) return string.Empty;
            if (EndMargin > 0) return "+" + EndMargin;
            if (EndMargin < 0) return "+0" + EndMargin;
         }
         if (EndMargin == 0) {
            if (BeginningMargin > 0) return $"+{BeginningMargin}+0";
            return BeginningMargin.ToString();
         }
         var text = string.Empty;
         if (BeginningMargin >= 0) text += "+";
         text += BeginningMargin;
         if (EndMargin >= 0) text += "+";
         return text + EndMargin;
      }

      public override bool Equals(object obj) => obj is ParentOffset other && Equals(other);
      public bool Equals(ParentOffset other) {
         return other != null && other.BeginningMargin == BeginningMargin && other.EndMargin == EndMargin;
      }
   }

   public interface ISupportInnerPointersRun : IFormattedRun {
      bool SupportsInnerPointers { get; }
      ISupportInnerPointersRun RemoveInnerSource(int source);
      ISupportInnerPointersRun AddSourcePointingWithinRun(int source);
   }

   public class ArrayRun : BaseRun, ITableRun, ISupportInnerPointersRun {
      public const char ExtendArray = '+';
      public const char ArrayStart = '[';
      public const char ArrayEnd = ']';
      public const char SingleByteIntegerFormat = '.';
      public const char DoubleByteIntegerFormat = ':';
      public const char ArrayAnchorSeparator = '/';
      public const string SignedFormatString = "|z";
      public const string HexFormatString = "|h";
      public const string RecordFormatString = "|s=";
      public const string TupleFormatString = "|t";
      public const string ColorFormatString = "|c";
      public const string CalculatedFormatString = "|=";
      public const string RenderFormatString = "|render=";
      public const string SplitterFormatString = "|";

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
      public ParentOffset ParentOffset { get; }

      public bool SupportsInnerPointers { get; }

      /// <summary>
      /// For Arrays that support pointers to individual elements within the array,
      /// This is the set of sources that points to each index of the array.
      /// The first set of sources (PointerSourcesForInnerElements[0]) should always be the same as PointerSources.
      /// </summary>
      public IReadOnlyList<SortedSpan<int>> PointerSourcesForInnerElements { get; }

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

            if (ParentOffset.BeginningMargin < 0) {
               options = options.Skip(-ParentOffset.BeginningMargin).ToList();
            } else if (ParentOffset.BeginningMargin > 0) {
               options = ParentOffset.BeginningMargin.Range().Select(i => (i - ParentOffset.BeginningMargin).ToString()).Concat(options).ToList();
            }
            if (ParentOffset.EndMargin > 0) {
               options = options.Concat(Enumerable.Range(ElementCount - ParentOffset.EndMargin, ParentOffset.EndMargin).Select(i => i.ToString())).ToList();
            } else if (ParentOffset.EndMargin < 0) {
               options = options.Take(options.Count + ParentOffset.EndMargin).ToList();
            }

            return options;
         }
      }

      public bool CanAppend => true;

      private ArrayRun(IDataModel data, string format, int start, SortedSpan<int> pointerSources) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         SupportsInnerPointers = format.StartsWith(AnchorStart.ToString());
         if (SupportsInnerPointers) format = format.Substring(1);
         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) throw new ArrayRunParseException($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}.");
         var segments = format.Substring(1, closeArray - 1);
         var length = format.Substring(closeArray + 1);
         if (!length.All(c => IsValidTableNameCharacter(c) || c.IsAny('-', '+'))) throw new ArrayRunParseException("Array length must be an anchor name or a number."); // the name might end with "-1" so also allow +/-
         ElementContent = ParseSegments(segments, data);
         if (ElementContent.Count == 0) throw new ArrayRunParseException("Array Content must not be empty.");
         ElementLength = ElementContent.Sum(e => e.Length);

         FormatMatchFlags flags = default;
         if (ElementContent.Count == 1) flags |= FormatMatchFlags.IsSingleSegment;

         if (length.Length == 0) {
            var nextRun = owner.GetNextAnchor(Start + ElementLength);
            for (; true; nextRun = owner.GetNextAnchor(nextRun.Start + Math.Max(1, nextRun.Length))) {
               if (nextRun.Start > owner.Count) break;
               var anchorName = owner.GetAnchorFromAddress(-1, nextRun.Start);
               if (string.IsNullOrEmpty(anchorName)) continue;
               if ((nextRun.Start - Start) % ElementLength != 0) break;
            }
            var thisRun = owner.GetNextAnchor(Start) as ArrayRun;
            var maxLength = int.MaxValue;
            if (thisRun != null && thisRun.Start == Start) maxLength = ElementLength * thisRun.ElementCount;
            var byteLength = 0;
            var elementCount = 0;
            while (Start + byteLength + ElementLength <= nextRun.Start && elementCount < 1000 && DataMatchesElementFormat(owner, Start + byteLength, ElementContent, elementCount, flags, nextRun)) {
               byteLength += ElementLength;
               elementCount++;
               if (elementCount == JunkLimit) flags |= FormatMatchFlags.AllowJunkAfterText;
               if (byteLength >= maxLength) break;
            }
            LengthFromAnchor = string.Empty;
            ParentOffset = ParentOffset.Default;
            ElementCount = Math.Max(1, elementCount); // if the user said there's a format here, then there is, even if the format it wrong.
            FormatString += ElementCount;
         } else if (length.TryParseInt(out int result)) {
            // fixed length is easy
            LengthFromAnchor = string.Empty;
            ParentOffset = ParentOffset.Default;
            ElementCount = Math.Max(1, result);
         } else {
            (LengthFromAnchor, ParentOffset, ElementCount) = ParseLengthFromAnchor(length);
            if (ElementCount < 1) throw new ArrayRunParseException("Tables must have a length of at least 1 element.");
         }

         Length = ElementLength * ElementCount;
         if (SupportsInnerPointers) PointerSourcesForInnerElements = ElementCount.Range().Select(i => SortedSpan<int>.None).ToList();
      }

      private ArrayRun(IDataModel data, string format, string lengthFromAnchor, ParentOffset parentOffset, int start, int elementCount, IReadOnlyList<ArrayRunElementSegment> segments, SortedSpan<int> pointerSources, IReadOnlyList<SortedSpan<int>> pointerSourcesForInnerElements) : base(start, pointerSources) {
         owner = data;
         FormatString = format;
         ElementContent = segments;
         ElementLength = ElementContent.Sum(e => e.Length);
         ElementCount = elementCount;
         LengthFromAnchor = lengthFromAnchor;
         ParentOffset = parentOffset;
         Length = ElementLength * ElementCount;
         SupportsInnerPointers = pointerSourcesForInnerElements != null;
         PointerSourcesForInnerElements = pointerSourcesForInnerElements;
      }

      public static ErrorInfo TryParse(IDataModel data, string format, int start, SortedSpan<int> pointerSources, out ITableRun self) => TryParse(data, "UNUSED", format, start, pointerSources, out self);

      public static ErrorInfo TryParse(IDataModel data, string name, string format, int start, SortedSpan<int> pointerSources, out ITableRun self) {
         return TryParse(data, name, format, start, pointerSources, null, out self);
      }

      public static ErrorInfo TryParse(IDataModel data, string name, string format, int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> sourceSegments, out ITableRun self) {
         self = null;
         var startArray = format.IndexOf(ArrayStart);
         var closeArray = format.LastIndexOf(ArrayEnd);
         if (startArray == -1 || startArray > closeArray) return new ErrorInfo($"Array Content must be wrapped in {ArrayStart}{ArrayEnd}.");
         var length = format.Substring(closeArray + 1);

         if (sourceSegments == null) {
            sourceSegments =
               data.GetUnmappedSourcesToAnchor(name)
               .Select(source => data.GetNextRun(source) as ITableRun)
               .Where(tRun => tRun != null)
               .Select(tRun => tRun.ElementContent)
               .FirstOrDefault();
         }

         var (singleSegment, margins, tilemapLength) = ParseTilemapTable(data, format, length);
         if (singleSegment is ArrayRunElementSegment) {
            // option 0: the length looks like a tilemap, and there's a single segment. Parse as a tilemap table.
            self = new TilemapTableRun(data, tilemapLength, singleSegment, margins, start, pointerSources);
         } else if (length.All(c => IsValidTableNameCharacter(c) || c.IsAny('-', '+'))) {
            // option 1: the length looks like a standard table length (or is empty, and thus dynamic). Parse as a table.
            try {
               using (ModelCacheScope.CreateScope(data)) {
                  var array = new ArrayRun(data, format, start, pointerSources);
                  self = array;
                  if (array.ElementContent.Count == 1 && array.ElementContent[0].Type == ElementContentType.Pointer) {
                     for (int i = 0; i < array.ElementCount; i++) {
                        if (!CheckPointerFormat(data, array.Start + array.ElementLength * i)) {
                           return new ErrorInfo($"{name}[{i}] doesn't match the expected format.");
                        }
                     }
                  }
               }
            } catch (ArrayRunParseException e) {
               return new ErrorInfo(e.Message);
            }
         } else if (TableStreamRun.TryParseTableStream(data, start, pointerSources, name, format, sourceSegments, out var tableStreamRun)) {
            // option 2: parse as a table stream, because the length contains characters like / or ! that make it look dependent on 
            self = tableStreamRun;
         } else {
            // option 3: table parsing failed. Something weird in the length.
            return new ErrorInfo($"Table format could not be parsed: {format}");
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
               self = new ArrayRun(data, originalFormat + bestLength, string.Empty, ParentOffset.Default, bestAddress, bestLength, elementContent, data.GetNextRun(bestAddress).PointerSources, null);
            } else {
               var bestAddress = KnownLengthSearch(data, elementContent, elementLength, length, out int bestLength, runFilter);
               if (bestAddress == Pointer.NULL) return false;
               var lengthFromAnchor = int.TryParse(length, out var _) ? string.Empty : length;
               self = new ArrayRun(data, originalFormat, lengthFromAnchor, ParentOffset.Default, bestAddress, bestLength, elementContent, data.GetNextRun(bestAddress).PointerSources, null);
            }
         }

         if (allowPointersToEntries) self = self.AddSourcesPointingWithinArray(changeToken);
         return true;
      }

      public static (ArrayRunElementSegment, TilemapMargins, string) ParseTilemapTable(IDataModel model, string format, string length) {
         var margins = TilemapMargins.ExtractMargins(ref length);

         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, length);
         var tilemap = model.GetNextRun(address) as ITilemapRun;
         if (tilemap == null || tilemap.Start != address) return default;

         var closeArray = format.LastIndexOf(ArrayEnd.ToString());
         if (!format.StartsWith(ArrayStart.ToString()) || closeArray == -1) return default;
         var segments = format.Substring(1, closeArray - 1);
         var content = ParseSegments(segments, model);
         if (content.Count != 1) return default;
         return (content[0], margins, length);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         var format = segments.Select(segment => segment.SerializeFormat).Aggregate((a, b) => a + " " + b);
         format = $"[{format}]{LengthFromAnchor}";
         if (string.IsNullOrEmpty(LengthFromAnchor)) format += ElementCount;
         return new ArrayRun(owner, format, LengthFromAnchor, ParentOffset, start, ElementCount, segments, pointerSources, PointerSourcesForInnerElements);
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
               if (DataMatchesElementFormat(data, currentAddress, elementContent, currentLength, flags, nextArray)) {
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
               bool match = DataMatchesElementFormat(data, currentAddress, elementContent, i, flags, nextArray);
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
               cachedCurrentString = data.TextConverter.Convert(data, offsets.SegmentStart, currentSegment.Length);
            }

            return PCSRun.CreatePCSFormat(data, offsets.SegmentStart, index, cachedCurrentString);
         }

         return this.CreateSegmentDataFormat(data, index);
      }

      ITableRun ITableRun.Append(ModelDelta token, int elementCount) => Append(token, elementCount);
      public ArrayRun Append(ModelDelta token, int elementCount) {
         if (elementCount == 0) return this;
         var lastArrayCharacterIndex = FormatString.LastIndexOf(ArrayEnd);
         var newFormat = FormatString.Substring(0, lastArrayCharacterIndex + 1);
         int endElementCount = Math.Max(ParentOffset.EndMargin, 0);
         var newParentOffset = ParentOffset.EndMargin < 0 ? new ParentOffset(ParentOffset.BeginningMargin, 0) : ParentOffset;
         if (token is NoDataChangeDeltaModel) newParentOffset = ParentOffset;
         if (newFormat != FormatString) {
            if (!string.IsNullOrEmpty(LengthFromAnchor)) {
               newFormat += LengthFromAnchor;
               newFormat += newParentOffset;
            } else {
               newFormat += ElementCount + elementCount;
            }
         }
         if (!(token is NoDataChangeDeltaModel)) elementCount -= Math.Min(ParentOffset.EndMargin, 0); // if EndMargin is negative, we need to add extra elements to make it 0
         if (ElementCount + elementCount < 1) elementCount = 1 - ElementCount;

         // set default values based on the bytes from the previous element
         if (!(token is NoDataChangeDeltaModel)) {
            // pull any end elements off the back
            var endElements = new byte[endElementCount * ElementLength];
            var endElementsPosition = Start + Length - endElements.Length;
            Array.Copy(owner.RawData, endElementsPosition, endElements, 0, endElements.Length);

            for (int i = 0; i < elementCount; i++) {
               int j = 0;
               foreach (var segment in ElementContent) {
                  var readPosition = endElementsPosition - ElementLength + j;
                  var writePosition = endElementsPosition + i * ElementLength + j;
                  WriteSegment(token, segment, owner, readPosition, writePosition);
                  j += segment.Length;
               }
            }

            // push end elements back on the back
            for (int i = 0; i < endElementCount; i++) {
               int j = 0;
               foreach (var segment in ElementContent) {
                  var readPosition = ElementLength * i + j;
                  var writePosition = endElementsPosition + (elementCount + i) * ElementLength + j;
                  WriteSegment(token, segment, endElements, readPosition, writePosition);
                  j += segment.Length;
               }
            }

            AdjustTableIndexValues(token, elementCount);
         }

         var newInnerElementsSources = PointerSourcesForInnerElements?.ToList();

         if (newInnerElementsSources != null) {
            newInnerElementsSources = new List<SortedSpan<int>>(PointerSourcesForInnerElements);

            // add extra elements at the back. Since this is a new element being added, it's fair to think that nothing points to it.
            for (int i = 0; i < elementCount; i++) {
               newInnerElementsSources.Add(SortedSpan<int>.None);
            }

            // remove extra elements at the back. Add NoInfoRuns for the pointers to point to.
            bool firstClear = true;
            for (int i = 0; i < -elementCount; i++) {
               var sources = newInnerElementsSources[newInnerElementsSources.Count - 1];
               newInnerElementsSources.RemoveAt(newInnerElementsSources.Count - 1);

               if (sources.Any()) {
                  var start = Start + Length - ElementLength * (i + 1);
                  // Since we're trying to add a run over space that we currently occupy, clear the existing run before adding this new one
                  if (firstClear) {
                     // clear the whole length, starting at Start, to preserve the pointers
                     owner.ClearFormat(token, Start, Length);
                     firstClear = false;
                  } else {
                     // clear only a single byte where we are, to preserve pointers to that element
                     owner.ClearFormat(token, start, 1);
                  }
                  owner.ObserveRunWritten(token, new NoInfoRun(start, sources));
               }
            }
         }

         elementCount += ElementCount; // elementCount is now the full count, not the delta
         UpdateNamedConstant(token, ref elementCount, alsoUpdateArrays: false);
         UpdateList(token, elementCount);

         return new ArrayRun(owner, newFormat, LengthFromAnchor, newParentOffset, Start, elementCount, ElementContent, PointerSources, newInnerElementsSources);
      }

      /// <summary>
      /// Look for number segments called 'index' or 'id'.
      /// Find whatever run contains the 'end' of the values and bump their values.
      /// Then set the new elements based on the new value gap.
      /// </summary>
      private void AdjustTableIndexValues(ModelDelta token, int newElementCount) {
         for (int i = 0; i < ElementContent.Count; i++) {
            if (ElementContent[i].Type != ElementContentType.Integer) continue;
            if (ElementContent[i] is ArrayRunRecordSegment || ElementContent[i] is ArrayRunHexSegment || ElementContent[i] is ArrayRunEnumSegment) continue;
            if (ElementContent[i].Name.ToLower() != "id" && ElementContent[i].Name.ToLower() != "index") continue;

            var currentCount = ElementCount;
            if (ParentOffset.EndMargin > 0) currentCount += newElementCount;
            var values = currentCount.Range().Select(j => this.ReadValue(owner, j, i)).ToArray();
            var maxIndex = values.IndexOf(values.Max());
            var maxRunStart = maxIndex;
            while (maxRunStart > 0 && values[maxRunStart - 1] == values[maxRunStart] - 1) maxRunStart -= 1;
            if (maxIndex != currentCount - 1 && maxRunStart != 0) {
               for (int j = maxRunStart; j <= maxIndex; j++) this.WriteValue(values[j] + newElementCount, owner, token, j, i);
               for (int j = 0; j < newElementCount; j++) this.WriteValue(values[maxRunStart] + j, owner, token, ElementCount + j, i);
            } else {
               // put the new values at the end
               var endMargin = Math.Max(0, ParentOffset.EndMargin);
               for (int j = 0; j < newElementCount; j++) this.WriteValue(values[maxIndex] + j + 1, owner, token, ElementCount + j - endMargin, i);
            }
         }
      }

      private void UpdateNamedConstant(ModelDelta token, ref int desiredValue, bool alsoUpdateArrays) {
         if (token is NoDataChangeDeltaModel) return; // nop during initial load
         var addresses = owner.GetMatchedWords(LengthFromAnchor);
         if (addresses.Count == 0) return;
         var lengthSource = (WordRun)owner.GetNextRun(addresses[0]);
         var length = desiredValue;
         if (lengthSource.Length == 1 && length > 255) {
            length = 255;
         } else if (length < 1) {
            length = 1;
         }
         desiredValue = length;
         length += lengthSource.ValueOffset;
         owner.WriteMultiByteValue(lengthSource.Start, lengthSource.Length, token, length);
         CompleteCellEdit.UpdateAllWords(owner, lengthSource, token, length, alsoUpdateArrays);
      }

      private void UpdateList(ModelDelta token, int desiredValue) {
         if (!owner.TryGetList(LengthFromAnchor, out var originalList)) return;
         var newList = originalList.ToList();
         while (newList.Count < desiredValue) newList.Add($"unnamed{newList.Count}");
         owner.SetList(token, LengthFromAnchor, newList, originalList.StoredHash);
      }

      private void WriteSegment(ModelDelta token, ArrayRunElementSegment segment, IReadOnlyList<byte> readData, int readPosition, int writePosition) {
         if (segment.Type == ElementContentType.Pointer) {
            var destination = readData.ReadMultiByteValue(readPosition, 4) + Pointer.NULL;
            var offset = this.ConvertByteOffsetToArrayOffset(writePosition);
            owner.UpdateArrayPointer(token, segment, ElementContent, offset.ElementIndex, writePosition, destination);
         } else {
            for (int k = 0; k < segment.Length; k++) {
               var validData = readData[readPosition + k];
               token.ChangeData(owner, writePosition + k, validData);
            }
         }
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         ITableRunExtensions.Clear(this, model, changeToken, start, length);
      }

      public ArrayRun GrowBitArraySegment(int bitSegmentIndex, int additionalBytes) {
         // all the data has been moved already
         // just return a new ArrayRun with the desired change.
         var content = ElementContent.ToList();
         var oldSegment = (ArrayRunBitArraySegment)content[bitSegmentIndex];
         content[bitSegmentIndex] = new ArrayRunBitArraySegment(oldSegment.Name, oldSegment.Length + additionalBytes, oldSegment.SourceArrayName);
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, content, PointerSources, PointerSourcesForInnerElements);
      }

      ISupportInnerPointersRun ISupportInnerPointersRun.AddSourcePointingWithinRun(int source) => AddSourcePointingWithinRun(source);
      public ArrayRun AddSourcePointingWithinRun(int source) {
         var destination = owner.ReadPointer(source);
         var index = (destination - Start) / ElementLength;
         if (index < 0 || index >= ElementCount) throw new IndexOutOfRangeException();
         var newInnerPointerSources = PointerSourcesForInnerElements.ToList();
         newInnerPointerSources[index] = newInnerPointerSources[index].Add1(source);
         if (index == 0) {
            return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, PointerSources.Add1(source), newInnerPointerSources);
         }
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, PointerSources, newInnerPointerSources);
      }

      public ArrayRun AddSourcesPointingWithinArray(ModelDelta changeToken) {
         if (ElementCount < 2) return this;

         var destinations = new int[ElementCount - 1];
         for (int i = 1; i < ElementCount; i++) destinations[i - 1] = Start + ElementLength * i;

         var sources = owner.SearchForPointersToAnchor(changeToken, destinations);

         var results = new List<SortedSpan<int>> {
            PointerSources ?? SortedSpan<int>.None
         };
         for (int i = 1; i < ElementCount; i++) results.Add(SortedSpan<int>.None);

         foreach (var source in sources) {
            var destination = owner.ReadPointer(source);
            int destinationIndex = (destination - Start) / ElementLength;
            // destinationIndex is expected to be within the table
            // but if the rom was modified by another program while open in HMA, it might not be.
            if (destinationIndex >= 0 && destinationIndex < results.Count) {
               results[destinationIndex] = results[destinationIndex].Add1(source);
            }
         }

         var pointerSourcesForInnerElements = results.Cast<SortedSpan<int>>().ToList();
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
         return segments.Range().Select(i => new HeaderRow(this, columnCount * i + startingDataIndex - Start, columnCount)).ToList();
      }

      public ArrayRun Move(int newStart) {
         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, newStart, ElementCount, ElementContent, PointerSources, PointerSourcesForInnerElements);
      }

      public override IFormattedRun RemoveSource(int source) => RemoveInnerSource(source);
      ISupportInnerPointersRun ISupportInnerPointersRun.RemoveInnerSource(int source) => RemoveInnerSource(source);
      public ArrayRun RemoveInnerSource(int source) {
         if (!SupportsInnerPointers) return (ArrayRun)base.RemoveSource(source);
         var newPointerSources = PointerSources.Remove1(source);
         var newInnerPointerSources = new List<SortedSpan<int>>();
         foreach (var list in PointerSourcesForInnerElements) {
            newInnerPointerSources.Add(list.Remove1(source));
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

      #region Seek Usages

      /// <summary>
      /// Finds all places in the current model that try to read the specified field.
      /// The field must be a single byte wide.
      /// Each result will be the address of a thumb instruction in the form `ldrb rD, [rN, #offset]`
      /// </summary>
      public IEnumerable<int> FindAllByteReads(ThumbParser parser, int fieldIndex) {
         Debug.Assert(ElementContent.Count > fieldIndex, $"Table does not have {fieldIndex} fields.");
         var results = new SortedSet<int>();
         if (fieldIndex >= ElementContent.Count) return results;
         Debug.Assert(ElementContent[fieldIndex].Length == 1, $"{ElementContent[fieldIndex].Name} is not a byte field.");
         var offset = ElementContent.Take(fieldIndex).Sum(seg => seg.Length);
         foreach (var source in PointerSources) {
            // ldr rX [pc, XXXXXX]=<address>
            foreach (var load in FindAllLoads(owner, parser, source)) {
               // add rY, rX <- rY points to a specific element in the table
               foreach (var add in FindAllCommands(owner, parser, load.address + 2, load.register, (line, reg) => line.StartsWith("add ") && line.Contains($", {reg}"))) {
                  var offsetText = $" #{offset}]";
                  // ldrb rZ, [rY, #offset] <- rZ now holds the specific byte
                  foreach (var ldrb in FindAllCommands(owner, parser, add.address + 2, add.register, (line, reg) => line.StartsWith("ldrb ") && line.Contains($"[{reg}, ") && line.Contains(offsetText))) {
                     results.Add(ldrb.address);
                  }
                  // mov rA, #offset
                  foreach (var mov in FindAllCommands(owner, parser, add.address + 2, $"!{add.register}", (line, reg) => line.StartsWith("mov ") && line.EndsWith($", #{offset}"), recurse: false)) {
                     // ldrsb rZ, [rY, rA]
                     if (FindAllCommands(owner, parser, mov.address + 2, add.register, (line, reg) => line.StartsWith("ldrsb ") && line.Contains($"[{reg}, ") && line.Contains($", {mov.register}]")).Any()) {
                        results.Add(mov.address);
                     }
                  }
               }
            }
         }
         return results;
      }

      /// <summary>
      /// Seeks backwards from the pointer to the start of the function.
      /// Then returns all locations that are loading the pointer.
      /// </summary>
      public static IEnumerable<(int address, string register)> FindAllLoads(IDataModel owner, ThumbParser parser, int pointerLocation) {
         var funcStart = pointerLocation - 2;
         while (funcStart >= 0 && owner[funcStart + 1] != 0xB5) funcStart -= 2;
         if (funcStart < 0) yield break;
         var pointerText = $"<{pointerLocation:X6}>";
         for (int i = funcStart + 2; i < pointerLocation; i += 2) {
            var loadArrayLine = parser.Parse(owner, i, 2).Trim().SplitLines().Last().Trim();
            if (!loadArrayLine.Contains(pointerText)) continue;
            if (!loadArrayLine.StartsWith("ldr ")) continue;
            var register = loadArrayLine.Split(',').First().Split(' ').Last();
            yield return (i, register);
         }
      }

      /// <summary>
      /// From a starting address, seeks all uses of a register that match a preidcate.
      /// Stops searching when the value of the register gets changed, or when the function returns.
      /// Follows branches / moves for the value.
      /// </summary>
      /// <param name="predicate">
      /// The first argument to the predicate is the line of assembly code that we're checking.
      /// The second argument to the predicate is the register that we're currently watching.
      /// This is usually registerSource, but may've changed because of mov operations.
      /// We're guaranteed that the line in question contains the source register that we're watching.
      /// If registerSource starts with !, we can be given a line that does _not_ contain that register, but we'll still stop searching when the value of the register gets changed.
      /// The predicate allows the line to be checked for other conditions, such as making sure that it's an 'add' instruction.
      /// </param>
      /// <returns>
      /// Returns the address of the command that used the source and the register of the result.
      /// </returns>
      public static IEnumerable<(int address, string register)> FindAllCommands(IDataModel owner, ThumbParser parser, int startAddress, string registerSource, Func<string, string, bool> predicate, IReadOnlyList<int> callTrail = null, bool recurse = true) {
         string watchRegister = registerSource;
         if (watchRegister != null && watchRegister.StartsWith("!")) watchRegister = watchRegister.Substring(1);
         if (registerSource != null && registerSource.StartsWith("!")) registerSource = null;
         callTrail = callTrail ?? new List<int>();
         if (callTrail.Contains(startAddress)) yield break;
         if (callTrail.Count > 4) yield break; // only allow so many mov / branch operations before we lose interest. This keeps the routine fast.
         var newTrail = callTrail.Concat(new[] { startAddress }).ToList();
         bool prevCommandIsCmp = false, prevCommandIsBl = false;
         for (int i = startAddress; true; i += 2) {
            var commandLine = parser.Parse(owner, i, 2).Trim().SplitLines().Last().Trim();
            if (!prevCommandIsBl && commandLine.Length == 4 && commandLine.All(ViewPort.AllHexCharacters.Contains)) break; // not a valid command
            if (commandLine.StartsWith("bx ")) break;
            if (commandLine.StartsWith("pop ") && commandLine.Contains("pc")) break;

            // follow branch logic (avoid recursion)
            if (recurse && commandLine.StartsWith("b") && !commandLine.StartsWith("bic ")) {
               var destination = commandLine.Split('<').Last().Split('>').First();
               if (destination.Length < 8 && destination.All(ViewPort.AllHexCharacters.Contains)) {
                  var nextStart = int.Parse(destination, NumberStyles.HexNumber);
                  if (commandLine.StartsWith("bl ") && owner[nextStart + 1] == 0xB5) {
                     // this is a branch link that pushes the link register
                     // no need to recurse here
                  } else if (prevCommandIsCmp || commandLine.IndexOf(" ") != 3) {
                     // This is a b/bl that we want to follow, or it's an actual conditional branch (because it's preceded by cmp).
                     // Either way, we want to recurse.
                     foreach (var result in FindAllCommands(owner, parser, nextStart, registerSource, predicate, newTrail)) yield return result;
                  }
               }
               if (commandLine.StartsWith("b ")) break;
            }

            var register = commandLine.Split(',').First().Split(' ').Last();

            // follow move operations
            if (recurse && registerSource != null && commandLine.StartsWith("mov ") && commandLine.EndsWith(registerSource)) {
               foreach (var result in FindAllCommands(owner, parser, i + 2, register, predicate, callTrail)) yield return result;
            }

            // look for the actual instruction we care about
            if ((registerSource == null || commandLine.Contains(registerSource)) && predicate(commandLine, registerSource)) yield return (i, register);
            if (register == registerSource || register == watchRegister) break;
            prevCommandIsCmp = commandLine.StartsWith("cmp ");
            prevCommandIsBl = commandLine.StartsWith("bl ");
         }
      }

      #endregion

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         // since the inner pointer sources includes the first row, update the first row
         List<SortedSpan<int>> newInnerPointerSources = null;
         if (PointerSourcesForInnerElements != null) {
            newInnerPointerSources = new List<SortedSpan<int>> { newPointerSources };
            for (int i = 1; i < PointerSourcesForInnerElements.Count; i++) newInnerPointerSources.Add(PointerSourcesForInnerElements[i]);
         }

         return new ArrayRun(owner, FormatString, LengthFromAnchor, ParentOffset, Start, ElementCount, ElementContent, newPointerSources, newInnerPointerSources);
      }

      private static bool IsValidFieldNameCharacter(char c) => char.IsLetterOrDigit(c) || c == '_'; // field names can contain underscores
      private static bool IsValidTableNameCharacter(char c) => char.IsLetterOrDigit(c) || c.IsAny('_', '.'); // table names can contain underscores or dots

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
               if (!int.TryParse(segments.Substring(0, repeatLength), out int innerCount)) {
                  throw new ArrayRunParseException($"Could not parse '{segments}' as a number.");
               }
               for (int i = 0; i < innerCount; i++) list.AddRange(innerSegments);
               segments = segments.Substring(repeatLength);
               continue;
            }

            int nameEnd = 0;
            while (nameEnd < segments.Length && IsValidFieldNameCharacter(segments[nameEnd])) nameEnd++;
            var name = segments.Substring(0, nameEnd);
            segments = segments.Substring(nameEnd);
            var (format, formatLength, segmentLength) = ExtractSingleFormat(segments, model);
            if (name == string.Empty && format != ElementContentType.Splitter) throw new ArrayRunParseException("expected name, but none was found: " + segments);

            // check to see if a name or length is part of the format
            if (format == ElementContentType.Integer && segments.Length > formatLength && segments[formatLength] != ' ') {
               segments = segments.Substring(formatLength);
               if (segments.StartsWith(HexFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunHexSegment(name, segmentLength));
               } else if (segments.StartsWith(SignedFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunSignedSegment(name, segmentLength));
               } else if (segments.StartsWith(TupleFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  var tupleContract = segments.Substring(TupleFormatString.Length, endOfToken - TupleFormatString.Length);
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunTupleSegment(name, tupleContract, segmentLength));
               } else if (segments.StartsWith(ColorFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunColorSegment(name));
               } else if (segments.StartsWith(CalculatedFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  var calculationContract = segments.Substring(CalculatedFormatString.Length, endOfToken - CalculatedFormatString.Length);
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunCalculatedSegment(model, name, calculationContract));
               } else if (segments.StartsWith(RenderFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  var contract = segments.Substring(RenderFormatString.Length, endOfToken - RenderFormatString.Length);
                  segments = segments.Substring(endOfToken).Trim();
                  list.Add(new ArrayRunOffsetRenderSegment(name, contract));
               } else if (segments.StartsWith(RecordFormatString)) {
                  var endOfToken = segments.IndexOf(' ');
                  if (endOfToken == -1) endOfToken = segments.Length;
                  var recordContract = segments.Substring(0, endOfToken);
                  segments = segments.Substring(endOfToken).Trim();
                  if (recordContract.Count('(') != 1 || recordContract.Count(')') != 1) {
                     throw new ArrayRunParseException("Record format is s={name}({number}={enum}|...).");
                  }
                  list.Add(new ArrayRunRecordSegment(name, segmentLength, recordContract));
               } else if (int.TryParse(segments, out var elementCount)) {
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
               var pointerSegment = new ArrayRunPointerSegment(model.FormatRunFactory, name, segments.Substring(1, formatLength - 2));
               if (!pointerSegment.IsInnerFormatValid) throw new ArrayRunParseException($"pointer format '{pointerSegment.InnerFormat}' was not understood.");
               list.Add(pointerSegment);
               segments = segments.Substring(formatLength).Trim();
            } else if (format == ElementContentType.BitArray) {
               var sourceName = segments.Substring(BitArray.SharedFormatString.Length, formatLength - BitArray.SharedFormatString.Length);
               segments = segments.Substring(formatLength).Trim();
               list.Add(new ArrayRunBitArraySegment(name, segmentLength, sourceName));
            } else if (format == ElementContentType.Splitter) {
               segments = segments.Substring(formatLength).Trim();
               list.Add(new ArrayRunSplitterSegment());
            } else {
               segments = segments.Substring(formatLength).Trim();
               if (format == ElementContentType.Unknown) {
                  // default to single byte integer
                  format = ElementContentType.Integer;
                  segmentLength = 1;
               }
               list.Add(new ArrayRunElementSegment(name, format, segmentLength, model.TextConverter));
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
         } else if (segments.StartsWith(CalculatedFormatString)) {
            return (ElementContentType.Integer, 0, 0);
         } else if (segments.StartsWith(RenderFormatString)) {
            return (ElementContentType.Integer, 0, 0);
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
            while (segments.Length > endIndex && IsValidTableNameCharacter(segments[endIndex])) endIndex++;
            var format = segments.Substring(0, endIndex);
            var name = format.Substring(BitArray.SharedFormatString.Length);
            var options = model.GetBitOptions(name);
            var count = options?.Count ?? 8;
            return (ElementContentType.BitArray, format.Length, (int)Math.Ceiling(count / 8.0));
         } else if (segments.StartsWith(SplitterFormatString + " ")) {
            return (ElementContentType.Splitter, 1, 0);
         }

         return (ElementContentType.Unknown, 0, 0);
      }

      private (string lengthFromAnchor, ParentOffset parentOffset, int elementCount) ParseLengthFromAnchor(string length) {
         var parentOffset = ParentOffset.Parse(ref length);
         var lengthFromAnchor = length;

         // length is based on another array
         int address = owner.GetAddressFromAnchor(new ModelDelta(), -1, lengthFromAnchor);
         if (address == Pointer.NULL) {
            // the requested name was unknown... is it a list name?
            if (owner.TryGetList(lengthFromAnchor, out var nameArray)) {
               return (lengthFromAnchor, parentOffset, nameArray.Count);
            } else {
               //  How about a constant name?
               var constantLocations = owner.GetMatchedWords(lengthFromAnchor);
               if (constantLocations.Count > 0 && owner.GetNextRun(constantLocations[0]) is WordRun constant) {
                  var elementCount = owner.ReadMultiByteValue(constant.Start, constant.Length) / constant.MultOffset - constant.ValueOffset;
                  return (lengthFromAnchor, parentOffset, elementCount);
               } else {
                  // length is zero for now
                  return (lengthFromAnchor, parentOffset, 1);
               }
            }
         }

         if (!(owner.GetNextRun(address) is ArrayRun run) || run.Start != address) {
            // the requested name was not an array, or did not start where anticipated
            // length is zero for now
            return (lengthFromAnchor, parentOffset, 1);
         }

         return (lengthFromAnchor, parentOffset, run.ElementCount + parentOffset.BeginningMargin + parentOffset.EndMargin);
      }

      // similar to DataMatchesElementFormat, but it only checks to make sure that things are pointers
      private static bool CheckPointerFormat(IDataModel owner, int start) {
         var destination = owner.ReadPointer(start);
         if (destination == Pointer.NULL) return true;
         if (destination < 0 || destination >= owner.Count) return false;
         return true;
      }

      private static bool DataMatchesElementFormat(IDataModel owner, int start, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, FormatMatchFlags flags, IFormattedRun nextAnchor) {
         foreach (var segment in segments) {
            if (start + segment.Length > owner.Count) return false;
            if (nextAnchor != null && start + segment.Length > nextAnchor.Start && nextAnchor is ArrayRun) return false; // don't blap over existing arrays
            if (!DataMatchesSegmentFormat(owner, start, segment, flags, segments, parentIndex)) return false;
            start += segment.Length;
         }
         return true;
      }

      public static bool DataMatchesSegmentFormat(IDataModel owner, int start, ArrayRunElementSegment segment, FormatMatchFlags flags, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         if (start < 0 || start >= owner.Count - segment.Length) return false;
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
                  // don't verify enums that are based on lists.
                  // There could be more elements in use than elements in the list, especially for edited ROMs with default lists.
                  if (options.Count == 0) return true; // unrecognized, so just allow anything
                  if (owner.TryGetList(enumSegment.EnumName, out var _)) return true;
                  var modelValue = owner.ReadMultiByteValue(start, segment.Length);
                  if (segment.Length == 2 && (short)modelValue == -2) return true; // allow FFFE short tokens: they often have special meaning
                  return modelValue - enumSegment.ValueOffset < options.Count;
               } else {
                  if (flags.HasFlag(FormatMatchFlags.AllowJunkAfterText)) return true; // don't bother verifying if junk is allowed
                  if (segment is ArrayRunHexSegment) return true; // don't validate raw hex content
                  return segment.Length < 4 || owner[start + 3] < 0x08 || owner[start + 3] > 0x09; // we want an integer, not a pointer
               }
            case ElementContentType.Pointer:
               var destination = owner.ReadPointer(start);
               if (destination == Pointer.NULL) return true;
               if (0 > destination || destination > owner.Count) return false;
               if (segment is ArrayRunPointerSegment pointerSegment) {
                  // allow the format to not match if the pointer points to zero
                  // this is because AdvanceMap inserts pointers to zero and expects them to act like NULLs
                  if (!pointerSegment.DestinationDataMatchesPointerFormat(owner, new NoDataChangeDeltaModel(), start, destination, sourceSegments, parentIndex) && destination != 0) return false;
               }
               return true;
            case ElementContentType.BitArray:
               var bitArraySegment = (ArrayRunBitArraySegment)segment;
               var bits = bitArraySegment.GetOptions(owner)?.Count() ?? 0;
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
