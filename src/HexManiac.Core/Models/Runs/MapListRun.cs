using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class MapListRun : BaseRun, ITableRun {
      // [map<[layout<[width:: height:: s1<> s2<> tiles<[a. b. c. d. tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> unknown<> blockend<>]1> s4<> borderwidth. borderheight. unused:]1> events<[e1 e2 e3 e4 ee1<> ee2<> ee3<> ee4<>]1> mapscripts<[type. pointer<>]!00> d<> song: map: label. flash. weather. type. unused: useLabel:]1>]
      private static readonly int[] BPRE0_Lengths = new[] { 5, 123, 60, 66, 4, 6, 8, 10, 6, 8, 20, 10, 8, 2, 10, 4, 2, 2, 2, 1, 1, 2, 2, 3, 2, 3, 2, 1, 1, 1, 1, 7, 5, 5, 8, 8, 5, 5, 1, 1, 1, 2, 1 };
      private static readonly string segmentFormat = "map<[layout<[width:: height:: s1<> s2<> tiles<[a. b. c. d. tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> unknown<> blockend<>]1> s4<> borderwidth. borderheight. unused:]1> events<[e1 e2 e3 e4 ee1<> ee2<> ee3<> ee4<>]1> mapscripts<[type. pointer<>]!00> d<> song: map: label. flash. weather. type. unused: useLabel:]1>";

      public override int Length => ElementCount * 4;
      public override string FormatString => $"[{segmentFormat}]{ElementCount}";
      public int ElementCount { get; }
      public int ElementLength => 4;
      public IDataModel Model { get; }
      public IReadOnlyList<string> ElementNames { get; } = new string[0];
      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }
      public bool CanAppend => false;

      public MapListRun(IDataModel model, int start, SortedSpan<int> sources) : base(start, sources) {
         this.Model = model;
         ElementContent = ArrayRun.ParseSegments(segmentFormat, model);
         var index = (sources[0] - model.GetNextRun(sources[0]).Start) / 4;
         ElementCount = BPRE0_Lengths[index];
      }

      public ITableRun Append(ModelDelta token, int length) {
         throw new NotImplementedException();
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, int depth) {
         throw new NotImplementedException();
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         throw new NotImplementedException();
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         return this.CreateSegmentDataFormat(data, index);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         return new MapListRun(Model, start, pointerSources);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         return new MapListRun(Model, Start, newPointerSources);
      }
   }
}
