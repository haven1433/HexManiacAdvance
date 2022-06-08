using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   // TODO this should be an ISpriteRun so we can see the tiles
   public class MapAnimationTilesRun : BaseRun, ITableRun {
      public const string ParentTileCountField = "tiles";
      public const string ParentFrameCountField = "frames";
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "mat" + AsciiRun.StreamDelimeter;
      private readonly IDataModel model;

      public bool CanAppend => true;
      public override int Length => ElementCount * ElementLength;
      public override string FormatString => SharedFormatString;
      public int ElementCount { get; }
      public int ElementLength => 4;
      public IReadOnlyList<string> ElementNames { get; } = new List<string>();
      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public MapAnimationTilesRun(IDataModel model, int start, SortedSpan<int> sources) : base(start, sources) {
         this.model = model;
         var primarySource = sources[0];
         ElementCount = model.ReadMultiByteValue(primarySource + 4, 2);
         int tileCount = model[primarySource + 7];
         ElementContent = new[] { new ArrayRunPointerSegment(model.FormatRunFactory, "frame", $"`uct4x{tileCount}`") };
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => this.CreateSegmentDataFormat(data, index);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new MapAnimationTilesRun(model, Start, newPointerSources);

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         return new MapAnimationTilesRun(model, start, pointerSources);
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);

      public ITableRun Append(ModelDelta token, int length) {
         var parent = PointerSources[0];
         model.WriteMultiByteValue(parent + 4, 2, token, ElementCount + length);
         return UpdateFromParent(token, 1, parent, out var _);
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         throw new NotImplementedException();
      }

      public MapAnimationTilesRun UpdateFromParent(ModelDelta token, int segmentIndex, int pointerSource, out bool childrenMoved) {
         childrenMoved = false;
         if (segmentIndex == 1) {
            var newTableCount = model.ReadMultiByteValue(pointerSource + 4, 2);
            var self = model.RelocateForExpansion(token, this, newTableCount * 4);
            for (int i = ElementCount; i < newTableCount; i++) {
               model.WritePointer(token, self.Start + 4 * i, Pointer.NULL);
            }
            return new MapAnimationTilesRun(model, self.Start, self.PointerSources);
         } else if (segmentIndex == 3) {
            var newTileCount = model[pointerSource + 7];
            var tilesetFormat = new TilesetFormat(4, newTileCount, -1, default);
            for (int i = 0; i < ElementCount; i++) {
               var destination = model.ReadPointer(Start + ElementLength * i);
               if (destination == Pointer.NULL) continue;
               var child = (ISpriteRun)model.GetNextRun(destination);
               var newChild = model.RelocateForExpansion(token, child, newTileCount * 32);
               if (newChild.Start != child.Start) childrenMoved = true;
               model.ObserveRunWritten(token, new TilesetRun(tilesetFormat, model, newChild.Start, newChild.PointerSources));
            }
            return this;
         } else {
            return this;
         }
      }
   }
}
