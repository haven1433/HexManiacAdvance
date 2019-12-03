using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TableStreamRun : BaseRun, IStreamRun, ITableRun {
      private readonly IDataModel model;
      private readonly IStreamEndStrategy endStream;

      #region Constructors

      public static bool TryParseTableStream(IDataModel model, int start, IReadOnlyList<int> sources, string content, out TableStreamRun tableStream) {
         tableStream = null;

         if (content.Length < 4 || content[0] != '[') return false;
         var close = content.LastIndexOf(']');
         if (close == -1) return false;
         var endStream = ParseEndStream(model, content.Substring(close + 1));
         if (endStream == null) return false;
         var segmentContent = content.Substring(1, close - 1);
         try {
            var segments = ArrayRun.ParseSegments(segmentContent, model);
            tableStream = new TableStreamRun(model, start, sources, content, segments, endStream);
         } catch (ArrayRunParseException) {
            return false;
         }

         for (int i = 0; i < tableStream.ElementCount; i++) {
            int subStart = start + tableStream.ElementLength * i;
            for (int j = 0; j < tableStream.ElementContent.Count; j++) {
               if (!ArrayRun.DataMatchesSegmentFormat(model, subStart, tableStream.ElementContent[j], default)) return false;
               subStart += tableStream.ElementContent[j].Length;
            }
         }
         return true;
      }

      public static IStreamEndStrategy ParseEndStream(IDataModel model, string endToken) {
         if (int.TryParse(endToken, out var number)) {
            return new FixedLengthStreamStrategy(number);
         }
         if (endToken.StartsWith("!") && endToken.Length % 2 == 1 && endToken.Substring(1).All(ViewModels.ViewPort.AllHexCharacters.Contains)) {
            return new EndCodeStreamStrategy(model, endToken.Substring(1).ToUpper());
         }
         var tokens = endToken.Split("/");
         if (tokens.Length == 2) {
            return new LengthFromParentStreamStrategy(model, tokens);
         }
         return null;
      }

      public TableStreamRun(IDataModel model, int start, IReadOnlyList<int> sources, string formatString, IReadOnlyList<ArrayRunElementSegment> segments, IStreamEndStrategy endStream) : base(start, sources) {
         this.model = model;
         ElementContent = segments;
         this.endStream = endStream;
         ElementLength = segments.Sum(segment => segment.Length);
         ElementCount = endStream.GetCount(start, ElementLength, sources);
         Length = ElementLength * ElementCount + endStream.ExtraLength;
         FormatString = formatString;
      }

      #endregion

      #region BaseRun

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var naturalLength = ElementCount * ElementLength;
         var naturalStop = Start + naturalLength;
         if (index >= naturalStop) return new EndStream(naturalStop, index - naturalStop, Length - naturalLength);
         return this.CreateSegmentDataFormat(data, index);
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new TableStreamRun(model, Start, newPointerSources, FormatString, ElementContent, endStream);

      #endregion

      #region StreamRun

      public string SerializeRun() {
         var builder = new StringBuilder();
         this.AppendTo(model, builder, Start, ElementLength * ElementCount);
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token) {
         var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var newRun = this;
         if (lines.Length != ElementCount) newRun = (TableStreamRun)Append(token, lines.Length - ElementCount);
         int start = newRun.Start;
         for (int i = 0; i < newRun.ElementCount; i++) {
            var line = lines.Length > i ? lines[i] : string.Empty;
            var tokens = Tokenize(line);
            int segmentOffset = 0;
            for (int j = 0; j < ElementContent.Count; j++) {
               var data = j < tokens.Count ? tokens[j] : string.Empty;
               ElementContent[j].Write(model, token, start + segmentOffset, data);
               segmentOffset += ElementContent[j].Length;
            }
            start += ElementLength;
         }
         return newRun;
      }

      private IReadOnlyList<string> Tokenize(string line) {
         // split at each space
         var tokens = new List<string>(line.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries));

         // recombine tokens so that each token that starts with " ends with "
         for (int i = 0; i < tokens.Count - 1; i++) {
            if (tokens[i].StartsWith("\"") == tokens[i].EndsWith("\"")) continue;
            tokens[i] += " " + tokens[i + 1];
            i--;
         }

         return tokens;
      }

      public bool DependsOn(string anchorName) {
         return ElementContent.OfType<ArrayRunEnumSegment>().Any(segment => segment.EnumName == anchorName);
      }

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

      public IEnumerable<(int, int)> Search(string baseName, int index) {
         int segmentOffset = 0;
         for (int i = 0; i < ElementContent.Count; i++) {
            if (ElementContent[i] is ArrayRunEnumSegment segment && segment.EnumName == baseName) {
               for (int j = 0; j < ElementCount; j++) {
                  var segmentStart = Start + j * ElementLength + segmentOffset;
                  if (model.ReadMultiByteValue(segmentStart, segment.Length) != index) continue;
                  yield return (segmentStart, segmentStart + segment.Length - 1);
               }
            }
            segmentOffset += ElementContent[i].Length;
         }
      }

      #endregion

      public TableStreamRun UpdateFromParent(ModelDelta token, int parentSegmentChange, int pointerSource) {
         if (!(endStream is LengthFromParentStreamStrategy strategy)) return this;
         return strategy.UpdateFromParentStream(this, token, parentSegmentChange, pointerSource);
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
         var newRun = (TableStreamRun)model.RelocateForExpansion(token, run, naturalLength + length * run.ElementLength);
         for (int i = 0; i < run.ElementLength * length; i++) token.ChangeData(model, newRun.Start + naturalLength + i, 0x00);
         for (int i = naturalLength + length * run.ElementLength; i < naturalLength; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         for (int i = 0; i < EndCode.Count; i++) token.ChangeData(model, newRun.Start + naturalLength + length * run.ElementLength + i, EndCode[i]);
         return new TableStreamRun(model, newRun.Start, run.PointerSources, run.FormatString, run.ElementContent, this);
      }
   }

   public class LengthFromParentStreamStrategy : IStreamEndStrategy {
      private readonly IDataModel model;
      private readonly string parentName, parentField;

      public int ExtraLength => 0;

      public LengthFromParentStreamStrategy(IDataModel model, string[] tokens) {
         this.model = model;
         parentName = tokens[0];
         parentField = tokens[1];
      }

      public int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources) {
         var parentIndex = GetParentIndex(pointerSources);
         var run = model.GetNextRun(parentIndex) as ITableRun;
         if (run == null) return 0;
         var segmentIndex = GetSegmentIndex(run, parentField);
         if (segmentIndex == -1) return 0;
         var segmentOffset = run.ElementContent.Take(segmentIndex).Sum(segment => segment.Length);

         foreach (var source in pointerSources) {
            var offsets = run.ConvertByteOffsetToArrayOffset(source);
            if (offsets.ElementIndex < 0 || offsets.ElementIndex > run.ElementCount) continue;
            return model.ReadMultiByteValue(run.Start + offsets.ElementIndex * run.ElementLength + segmentOffset, run.ElementContent[segmentIndex].Length);
         }

         return 0;
      }

      public TableStreamRun Append(TableStreamRun run, ModelDelta token, int length) {
         var parentIndex = GetParentIndex(run.PointerSources);
         var parent = model.GetNextRun(parentIndex) as ITableRun;
         if (parent == null) return run;
         var segmentIndex = GetSegmentIndex(parent, parentField);
         if (segmentIndex == -1) return run;

         UpdateParents(token, parent, segmentIndex, run.ElementCount + length, run.PointerSources);

         var naturalLength = run.Length;
         var newRun = (TableStreamRun)model.RelocateForExpansion(token, run, naturalLength + length * run.ElementLength);
         for (int i = 0; i < run.ElementLength * length; i++) token.ChangeData(model, newRun.Start + naturalLength + i, 0x00);
         for (int i = naturalLength + length * run.ElementLength; i < naturalLength; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         return new TableStreamRun(model, newRun.Start, run.PointerSources, run.FormatString, run.ElementContent, this);
      }

      public TableStreamRun UpdateFromParentStream(TableStreamRun run, ModelDelta token, int parentSegmentIndex, int pointerSource) {
         var parentAddress = GetParentIndex(run.PointerSources);
         var parent = model.GetNextRun(parentAddress) as ITableRun;
         if (parent == null) return run;
         var segmentIndex = GetSegmentIndex(parent, parentField);
         if (segmentIndex == -1 || segmentIndex != parentSegmentIndex) return run;
         var segmentOffset = parent.ElementContent.Take(segmentIndex).Sum(segment => segment.Length);
         var offsets = parent.ConvertByteOffsetToArrayOffset(parentAddress);

         var newElementCount = model.ReadMultiByteValue(parent.Start + offsets.ElementIndex * parent.ElementLength + segmentOffset, parent.ElementContent[segmentIndex].Length);

         var newRun = run;
         if (newElementCount != newRun.ElementCount) {
            newRun = (TableStreamRun)newRun.Append(token, newElementCount - newRun.ElementCount);
            UpdateParents(token, parent, segmentIndex, newElementCount, newRun.PointerSources);
         }

         return newRun;
      }

      private int GetParentIndex(IReadOnlyList<int> pointerSources) {
         if (parentName == string.Empty) {
            return pointerSources.Where(SourceIsFromParentTable).FirstOrDefault();
         } else {
            return model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, parentName);
         }
      }

      private int GetSegmentIndex(ITableRun parentRun, string segmentName) {
         for (int i = 0; i < parentRun.ElementContent.Count; i++) {
            if (parentRun.ElementContent[i].Name == segmentName) return i;
         }
         return -1;
      }

      private bool SourceIsFromParentTable(int source) {
         if (!(model.GetNextRun(source) is ITableRun run)) return false;
         return run.ElementContent.Any(segment => segment.Name == parentField);
      }

      private void UpdateParents(ModelDelta token, ITableRun parent, int segmentIndex, int newValue, IReadOnlyList<int> pointerSources) {
         var segmentOffset = parent.ElementContent.Take(segmentIndex).Sum(segment => segment.Length);
         var segmentLength = parent.ElementContent[segmentIndex].Length;
         foreach (var source in pointerSources) {
            var offsets = parent.ConvertByteOffsetToArrayOffset(source);
            if (offsets.ElementIndex < 0 || offsets.ElementIndex > parent.ElementCount) continue;
            if (model.ReadMultiByteValue(parent.Start + offsets.ElementIndex * parent.ElementLength + segmentOffset, segmentLength) != newValue) {
               model.WriteMultiByteValue(parent.Start + offsets.ElementIndex * parent.ElementLength + segmentOffset, segmentLength, token, newValue);
            }
         }
      }
   }
}
