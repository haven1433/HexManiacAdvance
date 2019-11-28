using System.Collections.Generic;
using System.Linq;
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
         if (index > naturalStop) return new EndStream(naturalStop, index - naturalStop, Length - naturalLength);
         return this.CreateSegmentDataFormat(data, index);
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new TableStreamRun(model, Start, newPointerSources, FormatString, ElementContent, endStream);

      #endregion

      #region StreamRun

      public string SerializeRun() {
         // TODO
         return string.Empty;
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token) {
         // TODO
         return this;
      }

      public bool DependsOn(string anchorName) {
         // TODO
         return false;
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
         // TODO
         return this;
      }

      #endregion
   }

   public interface IStreamEndStrategy {
      int ExtraLength { get; }
      int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources);
   }

   public class FixedLengthStreamStrategy : IStreamEndStrategy {
      public int Count { get; }
      public int ExtraLength => 0;

      public FixedLengthStreamStrategy(int count) => Count = count;

      public int GetCount(int start, int elementLength, IReadOnlyList<int> pointerSources) => Count;
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
         var parentIndex = 0;
         if (parentName == string.Empty) {
            parentIndex = pointerSources.Where(SourceIsFromParentTable).FirstOrDefault();
         } else {
            parentIndex = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, parentName);
         }
         var run = model.GetNextRun(parentIndex) as ITableRun;
         if (run == null) return 0;
         var segmentOffset = 0;
         foreach (var segment in run.ElementContent) {
            if (segment.Name == parentField) {
               foreach (var source in pointerSources) {
                  var offsets = run.ConvertByteOffsetToArrayOffset(source);
                  if (offsets.ElementIndex < 0 || offsets.ElementIndex > run.ElementCount) continue;
                  return model.ReadMultiByteValue(run.Start + offsets.ElementIndex * run.ElementLength + segmentOffset, segment.Length);
               }
            }
            segmentOffset += segment.Length;
         }
         return 0;
      }

      private bool SourceIsFromParentTable(int source) {
         if (!(model.GetNextRun(source) is ITableRun run)) return false;
         return run.ElementContent.Any(segment => segment.Name == parentField);
      }
   }
}
