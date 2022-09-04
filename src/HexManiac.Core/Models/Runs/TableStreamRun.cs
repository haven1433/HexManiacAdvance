using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TableStreamRun : BaseRun, IStreamRun, ITableRun {
      private readonly IDataModel model;
      private readonly IStreamEndStrategy endStream;

      public bool CanAppend => !(endStream is FixedLengthStreamStrategy || endStream is DynamicStreamStrategy);

      public bool AllowsZeroElements => endStream is EndCodeStreamStrategy;

      #region Constructors

      public static bool TryParseTableStream(IDataModel model, int start, SortedSpan<int> sources, string fieldName, string content, IReadOnlyList<ArrayRunElementSegment> sourceSegments, out TableStreamRun tableStream) {
         tableStream = null;

         if (content.Length < 4 || content[0] != '[') return false;
         var close = content.LastIndexOf(']');
         if (close == -1) return false;
         try {
            var segmentContent = content.Substring(1, close - 1);
            var segments = ArrayRun.ParseSegments(segmentContent, model);
            var endStream = ParseEndStream(model, fieldName, content.Substring(close + 1), segments, sourceSegments);
            if (endStream == null) return false;
            if (segments.Count == 0) return false;
            tableStream = new TableStreamRun(model, start, sources, content, segments, endStream);
         } catch (ArrayRunParseException) {
            return false;
         }

         if (start < 0) return false; // not a valid data location, so the data can't possibly be valid

         if (model.GetUnmappedSourcesToAnchor(fieldName).Count > 0) {
            // we're pasting this format and something else is expecting it. Don't expect the content to match yet.
            return tableStream.ElementCount > 0; 
         }

         // if the first 90% matches, we don't need to check the last 10%
         var mostElementsCount = (int)Math.Ceiling(tableStream.ElementCount * .85);
         return DataMatches(model, tableStream, mostElementsCount);
      }

      public static bool DataMatches(IDataModel model, TableStreamRun tableStream, int elementsCount) {
         for (int i = 0; i < elementsCount; i++) {
            int subStart = tableStream.Start + tableStream.ElementLength * i;
            for (int j = 0; j < tableStream.ElementContent.Count; j++) {
               if (!ArrayRun.DataMatchesSegmentFormat(model, subStart, tableStream.ElementContent[j], default, tableStream.ElementContent, i)) return false;
               subStart += tableStream.ElementContent[j].Length;
            }
         }
         return tableStream.ElementCount > 0 || tableStream.AllowsZeroElements;
      }

      public static bool TryWriteNewEndToken(ModelDelta token, ref TableStreamRun tableStream) {
         if (!(tableStream.endStream is EndCodeStreamStrategy strategy)) return false;
         for (int i = 0; i < strategy.EndCode.Count; i++) token.ChangeData(tableStream.model, tableStream.Start + i, strategy.EndCode[i]);
         tableStream = (TableStreamRun)tableStream.Clone(tableStream.PointerSources);
         return true;
      }

      /// <summary>
      /// ]3      -> FixedLength
      /// ]!00    -> EndCode
      /// ]/field -> LengthFromParent
      /// ]?      -> Dynamic
      /// </summary>
      public static IStreamEndStrategy ParseEndStream(IDataModel model, string fieldName, string endToken, IReadOnlyList<ArrayRunElementSegment> childSegments, IReadOnlyList<ArrayRunElementSegment> parentSegments) {
         if (int.TryParse(endToken, out var number)) {
            return new FixedLengthStreamStrategy(number);
         }
         if (endToken.StartsWith("!") && endToken.Length % 2 == 1 && endToken.Substring(1).All(ViewModels.ViewPort.AllHexCharacters.Contains)) {
            if (endToken.Length == 1) return null; // end token must be at least 1 byte long
            return new EndCodeStreamStrategy(model, endToken.Substring(1).ToUpper());
         }
         if (endToken == "?") {
            return new DynamicStreamStrategy(model, childSegments);
         }
         var tokens = endToken.Split("/");
         if (tokens.Length == 2) {
            return new LengthFromParentStreamStrategy(model, tokens, fieldName, parentSegments);
         }
         return null;
      }

      public TableStreamRun(
         IDataModel model,
         int start,
         SortedSpan<int> sources,
         string formatString,
         IReadOnlyList<ArrayRunElementSegment> parsedSegments,
         IStreamEndStrategy endStream
      ) : this(model, start, sources, formatString, parsedSegments, endStream, -1) { }

      public TableStreamRun(
         IDataModel model,
         int start,
         SortedSpan<int> sources,
         string formatString,
         IReadOnlyList<ArrayRunElementSegment> parsedSegments,
         IStreamEndStrategy endStream,
         int elementCountOverride
      ) : base(start, sources) {
         if (parsedSegments == null) parsedSegments = ArrayRun.ParseSegments(formatString.Substring(1, formatString.Length - 2), model);
         this.model = model;
         ElementContent = parsedSegments;
         this.endStream = endStream;
         ElementLength = parsedSegments.Sum(segment => segment.Length);
         ElementCount = elementCountOverride >= 0 ? elementCountOverride : endStream.GetCount(start, ElementLength, sources);
         Length = ElementLength * ElementCount + endStream.ExtraLength;
         FormatString = formatString;
      }

      #endregion

      #region BaseRun

      private string cachedCurrentString;
      private int currentCachedStartIndex = -1, currentCachedIndex = -1;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var naturalLength = ElementCount * ElementLength;
         var naturalStop = Start + naturalLength;
         if (index >= naturalStop) return new EndStream(naturalStop, index - naturalStop, Length - naturalLength);

         var offsets = this.ConvertByteOffsetToArrayOffset(index);
         var currentSegment = ElementContent[offsets.SegmentIndex];
         if (currentSegment.Type == ElementContentType.PCS) {
            if (currentCachedStartIndex != offsets.SegmentStart || currentCachedIndex > offsets.SegmentOffset) {
               currentCachedStartIndex = offsets.SegmentStart;
               currentCachedIndex = offsets.SegmentOffset;
               cachedCurrentString = data.TextConverter.Convert(data, offsets.SegmentStart, currentSegment.Length);
            }

            var pcsFormat = PCSRun.CreatePCSFormat(data, offsets.SegmentStart, index, cachedCurrentString);
            if (offsets.SegmentIndex == 0) return new StreamEndDecorator(pcsFormat);
            return pcsFormat;
         }

         var format = this.CreateSegmentDataFormat(data, index);
         if (offsets.SegmentIndex == 0) return new StreamEndDecorator(format);
         return format;
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) =>
         new TableStreamRun(model, Start, newPointerSources, FormatString, ElementContent, endStream, ElementCount);

      #endregion

      #region StreamRun

      public string SerializeRun() {
         if (endStream is FixedLengthStreamStrategy flss && flss.Count == 1) return SerializeSingleElementStream();
         var builder = new StringBuilder();
         AppendTo(model, builder, Start, ElementLength * ElementCount, false);
         var lines = builder.ToString().Split(Environment.NewLine);

         // AppendTo is used in copy/paste scenarios, and includes the required '+' to work in that case.
         // strip the '+', as it's not needed for stream serialization, which uses newlines instead.
         return string.Join(Environment.NewLine, lines.Select(line => {
            if (line.Length == 0) return line;
            if (line[0] != ArrayRun.ExtendArray) return line;
            return line.Substring(1);
         }).ToArray());
      }

      IStreamRun IStreamRun.DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) => DeserializeRun(content, token, out changedOffsets);

      public TableStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         if (endStream is FixedLengthStreamStrategy flss && flss.Count == 1) return DeserializeSingleElementStream(content, token, out changedOffsets);
         var changedAddresses = new List<int>();
         var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         if (lines.Length == 0) lines = content.Split(Environment.NewLine);
         var newRun = this;
         var appendCount = Math.Max(lines.Length, 1) - ElementCount;
         if (lines.Length != ElementCount) newRun = (TableStreamRun)Append(token, appendCount);
         int start = newRun.Start;
         for (int i = 0; i < newRun.ElementCount; i++) {
            var line = lines.Length > i ? lines[i] : string.Empty;
            var tokens = Tokenize(line);
            int segmentOffset = 0;
            for (int j = 0; j < ElementContent.Count; j++) {
               var data = j < tokens.Count ? tokens[j] : string.Empty;
               if (j == ElementContent.Count - 1 && tokens.Count > ElementContent.Count) data += " " + " ".Join(tokens.Skip(j + 1));
               if (ElementContent[j].Write(ElementContent, model, token, start + segmentOffset, ref data)) changedAddresses.Add(start + segmentOffset);
               if (data.Length > 0) tokens.Insert(j + 1, data);
               segmentOffset += ElementContent[j].Length;
            }
            start += ElementLength;
         }
         changedOffsets = changedAddresses;
         return newRun;
      }

      public IReadOnlyList<byte> CreateDefault() {
         if (endStream is EndCodeStreamStrategy endcode) return endcode.EndCode;
         return new byte[0];
      }

      private string SerializeSingleElementStream() {
         Debug.Assert(endStream is FixedLengthStreamStrategy flss && flss.Count == 1);
         var result = new StringBuilder();
         int offset = Start;
         var longestLabel = ElementContent.Select(seg => seg.Name.Length).Max();
         for (int i = 0; i < ElementContent.Count; i++) {
            var segment = ElementContent[i];
            var rawValue = model.ReadMultiByteValue(offset, segment.Length);
            var value = rawValue.ToString();
            if (segment is ArrayRunEnumSegment enumSeg) {
               var options = enumSeg.GetOptions(model).ToList();
               if (options.Count > rawValue) value = options[rawValue];
            } else if (segment is ArrayRunTupleSegment tupSeg) {
               value = tupSeg.ToText(model, offset);
            } else if (segment is ArrayRunHexSegment hexSeg) {
               value = "0x" + rawValue.ToString("X" + segment.Length * 2);
            } else if (segment.Type == ElementContentType.Pointer) {
               var pointerValue = rawValue - BaseModel.PointerOffset;
               value = $"<{pointerValue:X6}>";
               if (pointerValue == Pointer.NULL) value = "<null>";
            } else if (segment.Type == ElementContentType.PCS) {
               value = model.TextConverter.Convert(model, offset, segment.Length);
            }
            var extraWhitespace = new string(' ', longestLabel - segment.Name.Length);
            result.Append($"{segment.Name}:{extraWhitespace} {value}");
            if (i < ElementContent.Count - 1) result.AppendLine();
            offset += segment.Length;
         }
         return result.ToString();
      }

      private TableStreamRun DeserializeSingleElementStream(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         Debug.Assert(endStream is FixedLengthStreamStrategy flss && flss.Count == 1);
         var fields = content.SplitLines();
         int segmentOffset = 0;
         int fieldIndex = 0;
         var changeAddresses = new List<int>();
         for (int j = 0; j < ElementContent.Count; j++) {
            while (fieldIndex < fields.Length && string.IsNullOrWhiteSpace(fields[fieldIndex])) fieldIndex += 1;
            if (fieldIndex >= fields.Length) break;
            var data = j < fields.Length ? fields[fieldIndex].Split(new[] { ':' }, 2).Last() : string.Empty;
            if (ElementContent[j].Write(this.ElementContent, model, token, Start + segmentOffset, ref data)) {
               changeAddresses.Add(Start + segmentOffset);
            }
            segmentOffset += ElementContent[j].Length;
            fieldIndex += 1;
         }
         changedOffsets = changeAddresses;
         return this;
      }

      public static List<string> Tokenize(string line) {
         // split at each space
         var tokens = new List<string>(line.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries));

         // remove trailing ',' from tokens
         for (int i = 0; i < tokens.Count; i++) {
            if (!tokens[i].EndsWith(",")) continue;
            tokens[i] = tokens[i].Substring(0, tokens[i].Length - 1);
         }

         Recombine(tokens, "\"", "\"");
         Recombine(tokens, "(", ")");

         // remove comments
         var comment = ViewPort.CommentStart.ToString();
         tokens = tokens.Where(token => !token.StartsWith(comment)).ToList();

         return tokens;
      }

      public static void Recombine(List<string> tokens, string startToken, string endToken) {
         for (int i = 0; i < tokens.Count - 1; i++) {
            if (tokens[i].StartsWith(startToken) == tokens[i].EndsWith(endToken)) continue;
            tokens[i] += " " + tokens[i + 1];
            tokens.RemoveAt(i + 1);
            i--;
         }
      }

      public bool DependsOn(string anchorName) {
         return ITableRunExtensions.DependsOn(this, anchorName);
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         if (endStream is FixedLengthStreamStrategy flss && flss.Count == 1) {
            return GetAutoCompleteOptionsForSingleElementStream(line, caretCharacterIndex);
         }

         var results = new List<AutocompleteItem>();
         if (line.Trim().Length == 0) return results;
         var lineStart = line.Substring(0, caretCharacterIndex);
         var lineEnd = line.Substring(caretCharacterIndex);
         var tokens = Tokenize(lineStart);
         if (ElementContent.Count < tokens.Count) return results;
         var targetSegment = ElementContent[Math.Max(0, tokens.Count - 1)];
         var currentToken = tokens.Count == 0 ? string.Empty : tokens[tokens.Count - 1];
         if (targetSegment is ArrayRunEnumSegment enumSegment) {
            if (currentToken.StartsWith("\"")) currentToken = currentToken.Substring(1);
            if (currentToken.EndsWith("\"")) currentToken = currentToken.Substring(0, currentToken.Length - 1);
            var optionText = enumSegment.GetOptions(model).Where(option => option?.MatchesPartial(currentToken) ?? false);
            results.AddRange(CreateEnumAutocompleteOptions(tokens, optionText, lineEnd));
         } else if (targetSegment is ArrayRunTupleSegment tupleGroup) {
            var tupleTokens = currentToken.Split(" ").ToList();
            if (tupleTokens[0].StartsWith("(")) tupleTokens[0] = tupleTokens[0].Substring(1);
            Recombine(tupleTokens, "\"", "\"");
            var visibleTupleElements = tupleGroup.Elements.Where(element => !string.IsNullOrEmpty(element.Name)).ToList();
            var optionToken = tupleTokens.Last();
            if (visibleTupleElements.Count >= tupleTokens.Count) {
               var tupleToken = visibleTupleElements[tupleTokens.Count - 1];
               if (optionToken.StartsWith("\"")) optionToken = optionToken.Substring(1);
               if (optionToken.EndsWith("\"")) optionToken = optionToken.Substring(0, optionToken.Length - 1);
               if (!string.IsNullOrEmpty(tupleToken.SourceName)) {
                  var optionText = ArrayRunEnumSegment.GetOptions(model, tupleToken.SourceName).Where(option => option.MatchesPartial(optionToken));
                  results.AddRange(CreateTupleEnumAutocompleteOptions(tokens, tupleGroup, tupleTokens, optionText, lineEnd));
               } else if (tupleToken.BitWidth == 1) {
                  var options = new[] { "false", "true" }.Where(option => option.MatchesPartial(optionToken));
                  results.AddRange(CreateTupleEnumAutocompleteOptions(tokens, tupleGroup, tupleTokens, options, lineEnd));
               }
            }
         }
         return results;
      }

      private IReadOnlyList<AutocompleteItem> GetAutoCompleteOptionsForSingleElementStream(string line, int caretIndex) {
         var results = new List<AutocompleteItem>();

         var lineStart = line.Substring(0, caretIndex);
         var lineEnd = line.Substring(caretIndex);
         if (lineStart.Count(':') != 1) return results;
         var fieldName = lineStart.Split(':')[0].Trim();
         var currentContent = lineStart.Split(':')[1].Trim();
         var field = ElementContent.Where(segment => segment.Name == fieldName).FirstOrDefault();
         if (field == null) return null;
         if (field is ArrayRunEnumSegment enumSegment) {
            var optionText = enumSegment.GetOptions(model).Where(option => option.MatchesPartial(currentContent));
            results.AddRange(CreateSingleElementEnumAutocompleteOptions(lineEnd, fieldName, optionText));
         } else if (field is ArrayRunTupleSegment tupleGroup) {
            var tupleTokens = currentContent.Split(" ").ToList();
            Recombine(tupleTokens, "\"", "\"");
            if (tupleTokens[0].StartsWith("(")) tupleTokens[0] = tupleTokens[0].Substring(1);
            var visibleTupleElements = tupleGroup.Elements.Where(element => !string.IsNullOrEmpty(element.Name)).ToList();
            if (visibleTupleElements.Count >= tupleTokens.Count) {
               var tupleToken = visibleTupleElements[tupleTokens.Count - 1];
               var optionToken = tupleTokens.Last();
               if (optionToken.StartsWith("\"")) optionToken = optionToken.Substring(1);
               if (optionToken.EndsWith("\"")) optionToken = optionToken.Substring(0, optionToken.Length - 1);
               tupleTokens = tupleTokens.Take(tupleTokens.Count - 1).ToList();
               if (!string.IsNullOrEmpty(tupleToken.SourceName)) {
                  var optionText = ArrayRunEnumSegment.GetOptions(model, tupleToken.SourceName).Where(option => option.MatchesPartial(optionToken));
                  results.AddRange(CreateTupleEnumSingleElementAutocompleteOptions(fieldName, tupleGroup, tupleTokens, optionText, lineEnd));
               } else if (tupleToken.BitWidth == 1) {
                  var optionText = new[] { "false", "true" }.Where(option => option.MatchesPartial(optionToken));
                  results.AddRange(CreateTupleEnumSingleElementAutocompleteOptions(fieldName, tupleGroup, tupleTokens, optionText, lineEnd));
               }
            }
         }

         return results;
      }

      private IEnumerable<AutocompleteItem> CreateEnumAutocompleteOptions(IReadOnlyList<string> tokens, IEnumerable<string> optionText, string lineEnd) {
         foreach (var option in optionText) {
            string newLine = ", ".Join(tokens.Take(tokens.Count - 1));
            if (newLine.Length > 0) newLine += ", ";
            newLine += option;
            newLine += lineEnd;
            if (Tokenize(newLine).Count < ElementContent.Count) newLine += ", ";
            yield return new AutocompleteItem(option, newLine);
         }
      }

      private IEnumerable<AutocompleteItem> CreateTupleEnumAutocompleteOptions(IReadOnlyList<string> tokens, ArrayRunTupleSegment tupleGroup, List<string> tupleTokens, IEnumerable<string> optionText, string lineEnd) {
         foreach (var option in optionText) {
            string newLine = ", ".Join(tokens.Take(tokens.Count - 1));
            var previousTokens = tupleTokens.Take(tupleTokens.Count - 1).ToList();
            newLine += tupleGroup.ConstructAutocompleteLine(previousTokens, option);
            var thisLineEnd = lineEnd.Trim();
            if (thisLineEnd.StartsWith(")")) thisLineEnd = thisLineEnd.Substring(1);
            newLine += thisLineEnd;
            if (Tokenize(newLine).Count < ElementContent.Count) newLine += ", ";
            yield return new AutocompleteItem(option, newLine);
         }
      }

      private static IEnumerable<AutocompleteItem> CreateSingleElementEnumAutocompleteOptions(string lineEnd, string fieldName, IEnumerable<string> optionText) {
         foreach (var option in optionText) {
            string newLine = $"{fieldName}: {option}{lineEnd}";
            yield return new AutocompleteItem(option, newLine);
         }
      }

      private static IEnumerable<AutocompleteItem> CreateTupleEnumSingleElementAutocompleteOptions(string fieldName, ArrayRunTupleSegment tupleSegment, List<string> previousTupleElements, IEnumerable<string> optionText, string lineEnd) {
         foreach (var option in optionText) {
            var newLine = $"{fieldName}: ";
            newLine += tupleSegment.ConstructAutocompleteLine(previousTupleElements, option);
            newLine += lineEnd;
            yield return new AutocompleteItem(option, newLine);
         }
      }

      public IReadOnlyList<IPixelViewModel> Visualizations => new List<IPixelViewModel>();

      #endregion

      #region TableRun

      public int ElementCount { get; }

      public int ElementLength { get; }

      public IReadOnlyList<string> ElementNames { get; } = new List<string>();

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public override int Length { get; }

      public override string FormatString { get; }

      public ITableRun Append(ModelDelta token, int length) {
         return endStream.Append(this, token, length);
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);
         if (start + length >= Start + Length && endStream is EndCodeStreamStrategy) builder.Append(Environment.NewLine + "[]");
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         ITableRunExtensions.Clear(this, model, changeToken, start, length);
      }

      public IEnumerable<(int, int)> Search(string baseName, int index) {
         return ITableRunExtensions.Search(this, model, baseName, index);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         var closeSegments = FormatString.LastIndexOf(']');
         var endToken = FormatString.Substring(closeSegments + 1);

         var format = segments.Select(segment => segment.SerializeFormat).Aggregate((a, b) => a + " " + b);
         format = $"[{format}]{endToken}";
         return new TableStreamRun(model, start, pointerSources, format, segments, endStream);
      }

      #endregion

      public TableStreamRun UpdateFromParent(ModelDelta token, int parentSegmentChange) {
         if (!(endStream is LengthFromParentStreamStrategy strategy)) return this;
         return strategy.UpdateFromParentStream(this, token, parentSegmentChange);
      }
   }

   public interface IStreamEndStrategy {
      int ExtraLength { get; }
      int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources);
      TableStreamRun Append(TableStreamRun run, ModelDelta token, int length);
   }

   public class FixedLengthStreamStrategy : IStreamEndStrategy {
      public int Count { get; }
      public int ExtraLength => 0;

      public FixedLengthStreamStrategy(int count) => Count = count;

      public int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources) => Count;

      public TableStreamRun Append(TableStreamRun run, ModelDelta token, int length) => run;
   }

   public class DynamicStreamStrategy : IStreamEndStrategy {
      private readonly IDataModel model;
      private readonly IReadOnlyList<ArrayRunElementSegment> segments;
      public DynamicStreamStrategy(IDataModel model, IReadOnlyList<ArrayRunElementSegment> segments) => (this.model, this.segments) = (model, segments);

      public int ExtraLength => 0;

      public TableStreamRun Append(TableStreamRun run, ModelDelta token, int length) {
         var naturalLength = run.Length;
         var newRun = model.RelocateForExpansion(token, run, naturalLength + length * run.ElementLength);

         // add new element data
         for (int i = 0; i < run.ElementLength * length; i++) {
            var prevIndex = newRun.Start + naturalLength + i - newRun.ElementLength * length;
            while (prevIndex < newRun.Start) prevIndex += newRun.ElementLength;
            byte prevData = prevIndex >= newRun.Start ? model[prevIndex] : default;
            token.ChangeData(model, newRun.Start + naturalLength + i, prevData);
         }

         // remove excess element data
         for (int i = naturalLength + length * run.ElementLength; i < naturalLength; i++) token.ChangeData(model, newRun.Start + i, 0xFF);

         return new TableStreamRun(model, newRun.Start, run.PointerSources, run.FormatString, run.ElementContent, this, run.ElementCount + length);
      }

      public int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources) {
         Debug.Assert(elementLength == segments.Sum(seg => seg.Length), "ElementLength is expected to be the sum of the segments.");
         for (int i = 0; true; i++) {
            // verify that there's enough room for another element
            var existingRun = model.GetNextRun(start);
            if (existingRun.Start >= start && existingRun.Start < start + elementLength) {
               if (existingRun.PointerSources != null && !(existingRun is NoInfoRun)) return i;        // break if it's not a no-info run and has pointers
               if (!string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, existingRun.Start))) return i; // break if it's an anchor
            }

            // verify that the data matches the expected format
            if (!segments.DataFormatMatches(model, start, i)) return i;

            start += elementLength;
         }
      }
   }

   public class EndCodeStreamStrategy : IStreamEndStrategy {
      private readonly IDataModel model;

      public IReadOnlyList<byte> EndCode { get; }
      public int ExtraLength => EndCode.Count;

      public EndCodeStreamStrategy(IDataModel model, string endToken) {
         this.model = model;
         var hex = ViewModels.ViewPort.AllHexCharacters;
         var endCode = new List<byte>();
         while (endToken.Length > 0) {
            endCode.Add((byte)(hex.IndexOf(endToken[0]) * 16 + hex.IndexOf(endToken[1])));
            endToken = endToken.Substring(2);
         }
         EndCode = endCode;
      }

      public int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources) {
         if (start < 0) return 0;
         int length = 0;
         while (true) {
            bool match = true;
            for (int j = 0; j < EndCode.Count && match; j++) {
               if (model.Count <= start + j) return 0;
               if (model[start + j] != EndCode[j]) match = false;
            }
            if (match) return length;
            length++;
            start += elementLength;
         }
      }

      public TableStreamRun Append(TableStreamRun run, ModelDelta token, int length) {
         var naturalLength = run.Length - EndCode.Count;
         var newRun = model.RelocateForExpansion(token, run, naturalLength + length * run.ElementLength + EndCode.Count);
         if (naturalLength == 0) {
            for (int i = 0; i < run.ElementLength * length; i++) token.ChangeData(model, newRun.Start + naturalLength + i, 0);
         } else {
            for (int i = 0; i < run.ElementLength * length; i++) token.ChangeData(model, newRun.Start + naturalLength + i, model[newRun.Start + naturalLength + i - run.ElementLength]);
         }
         for (int i = naturalLength + length * run.ElementLength; i < naturalLength; i++) if (model[newRun.Start + i] != 0xFF) token.ChangeData(model, newRun.Start + i, 0xFF);
         for (int i = 0; i < EndCode.Count; i++) token.ChangeData(model, newRun.Start + naturalLength + length * run.ElementLength + i, EndCode[i]);
         return new TableStreamRun(model, newRun.Start, run.PointerSources, run.FormatString, run.ElementContent, this, run.ElementCount + length);
      }
   }

   public class LengthFromParentStreamStrategy : IStreamEndStrategy {
      private readonly IDataModel model;
      private readonly string parentName, parentFieldForLength, parentFieldForThis;
      private readonly IReadOnlyList<ArrayRunElementSegment> sourceSegments;

      public int ExtraLength => 0;

      public LengthFromParentStreamStrategy(IDataModel model, string[] tokens, string tableFieldName, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         this.model = model;
         parentName = tokens[0];
         parentFieldForLength = tokens[1];
         parentFieldForThis = tableFieldName;
         this.sourceSegments = sourceSegments;
      }

      public int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources) {
         int defaultValue = 1;
         var parentIndex = pointerSources != null ? GetParentIndex(pointerSources) : -1;
         var run = parentIndex >= 0 ? model.GetNextRun(parentIndex) as ITableRun : null;
         int countSegmentIndex = -1;
         var segments = run?.ElementContent ?? sourceSegments;
         countSegmentIndex = GetSegmentIndex(segments, parentFieldForLength);
         if (countSegmentIndex == -1) return defaultValue;
         var countSegmentOffset = segments.Take(countSegmentIndex).Sum(segment => segment.Length);
         var pointerSegmentIndex = GetSegmentIndex(segments, parentFieldForThis);
         var pointerSegmentOffset = segments.Take(pointerSegmentIndex).Sum(segment => segment.Length);

         foreach (var source in pointerSources.OrderBy(source => source)) {
            if (source < parentIndex) continue;
            return Math.Max(model.ReadMultiByteValue(source - pointerSegmentOffset + countSegmentOffset, segments[countSegmentIndex].Length), defaultValue);
         }

         return defaultValue;
      }

      public TableStreamRun Append(TableStreamRun run, ModelDelta token, int length) {
         var parentIndex = GetParentIndex(run.PointerSources);
         var parent = model.GetNextRun(parentIndex) as ITableRun;
         if (parent == null) return run;
         var segmentIndex = GetSegmentIndex(parent.ElementContent, parentFieldForLength);
         if (segmentIndex == -1) return run;

         UpdateParents(token, parent, segmentIndex, run.ElementCount + length, run.PointerSources);

         var naturalLength = run.Length;
         var newRun = model.RelocateForExpansion(token, run, naturalLength + length * run.ElementLength);
         for (int i = 0; i < run.ElementLength * length; i++) {
            var prevIndex = newRun.Start + naturalLength + i - newRun.ElementLength * length;
            while (prevIndex < newRun.Start) prevIndex += newRun.ElementLength;
            byte prevData = prevIndex >= newRun.Start ? model[prevIndex] : default;
            token.ChangeData(model, newRun.Start + naturalLength + i, prevData);
         }
         for (int i = naturalLength + length * run.ElementLength; i < naturalLength; i++) if (model[newRun.Start + i] != 0xFF) token.ChangeData(model, newRun.Start + i, 0xFF);
         return new TableStreamRun(model, newRun.Start, run.PointerSources, run.FormatString, run.ElementContent, this);
      }

      public TableStreamRun UpdateFromParentStream(TableStreamRun run, ModelDelta token, int parentSegmentIndex) {
         var parentAddress = GetParentIndex(run.PointerSources);
         var parent = model.GetNextRun(parentAddress) as ITableRun;
         if (parent == null) return run;
         var segmentIndex = GetSegmentIndex(parent.ElementContent, parentFieldForLength);
         if (segmentIndex == -1 || segmentIndex != parentSegmentIndex) return run;
         var segmentOffset = parent.ElementContent.Take(segmentIndex).Sum(segment => segment.Length);
         var offsets = parent.ConvertByteOffsetToArrayOffset(parentAddress);

         var newElementCount = model.ReadMultiByteValue(parent.Start + offsets.ElementIndex * parent.ElementLength + segmentOffset, parent.ElementContent[segmentIndex].Length);

         var newRun = run;
         if (newElementCount != newRun.ElementCount) {
            var endOfCurrentRun = run.Start + run.Length;
            var nextRunMinimumStart = newRun.Start + newRun.ElementLength * newElementCount;
            if (
               TableStreamRun.DataMatches(model, newRun, newElementCount) &&
               model.GetNextRun(nextRunMinimumStart).Start >= nextRunMinimumStart &&
               run.ElementCount <= 1
            ) {
               // no need to repoint: the next data matches
               // this is important for when we're pasting pointers to existing formats before pasting those formats' lengths.
               // example: paste ^newanchor <pointer> count, where pointer points to existing data. We don't want writing the 'count' to cause a repoint
               model.ClearFormat(token, endOfCurrentRun, nextRunMinimumStart - endOfCurrentRun);
               UpdateParents(token, parent, segmentIndex, newElementCount, newRun.PointerSources);
               newRun = new TableStreamRun(model, newRun.Start, newRun.PointerSources, newRun.FormatString, newRun.ElementContent, this);
            } else {
               newRun = (TableStreamRun)newRun.Append(token, newElementCount - newRun.ElementCount);
               UpdateParents(token, parent, segmentIndex, newElementCount, newRun.PointerSources);
            }
         }

         return newRun;
      }

      private int GetParentIndex(IReadOnlyList<int> pointerSources) {
         if (parentName == string.Empty) {
            var matches = pointerSources.Where(SourceIsFromParentTable).ToList();
            return matches.Count > 0 ? matches[0] : -1;
         } else {
            return model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, parentName);
         }
      }

      private int GetSegmentIndex(IReadOnlyList<ArrayRunElementSegment> sourceSegments, string segmentName) {
         if (sourceSegments == null) return -1;
         for (int i = 0; i < sourceSegments.Count; i++) {
            if (sourceSegments[i].Name == segmentName) return i;
         }
         return -1;
      }

      private bool SourceIsFromParentTable(int source) {
         if (!(model.GetNextRun(source) is ITableRun run)) return false;
         return run.ElementContent.Any(segment => segment.Name == parentFieldForLength);
      }

      private void UpdateParents(ModelDelta token, ITableRun parent, int segmentIndex, int newValue, IReadOnlyList<int> pointerSources) {
         var segmentOffset = parent.ElementContent.Take(segmentIndex).Sum(segment => segment.Length);
         var segmentLength = parent.ElementContent[segmentIndex].Length;
         foreach (var source in pointerSources) {
            var offsets = parent.ConvertByteOffsetToArrayOffset(source);
            if (offsets.ElementIndex < 0 || offsets.ElementIndex > parent.ElementCount) continue;
            var lengthAddress = parent.Start + offsets.ElementIndex * parent.ElementLength + segmentOffset;
            if (model.ReadMultiByteValue(lengthAddress, segmentLength) != newValue) {
               model.WriteMultiByteValue(lengthAddress, segmentLength, token, newValue);
            }
         }
      }
   }
}
