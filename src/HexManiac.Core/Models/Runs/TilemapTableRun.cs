using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TilemapTableRun : BaseRun, ITableRun {
      private readonly IDataModel model;

      public ArrayRunElementSegment Segment { get; }
      public string TilemapAnchor { get; }

      public int ElementCount { get; }

      public int ElementLength { get; }

      public IReadOnlyList<string> ElementNames { get; } = new string[0];

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public bool CanAppend => false;

      public override int Length { get; }

      public override string FormatString { get; }

      public TilemapTableRun(IDataModel model, string anchorName, ArrayRunElementSegment segment, int start, SortedSpan<int> sources = null) : base(start, sources) {
         this.model = model;
         TilemapAnchor = anchorName;
         Segment = segment;

         var tilemapAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchorName);
         var tilemap = model.GetNextRun(tilemapAddress) as ITilemapRun;
         if (tilemap != null) {
            ElementCount = tilemap.SpriteFormat.TileHeight;
            ElementLength = tilemap.SpriteFormat.TileWidth * Segment.Length;
            ElementContent = ElementCount.Range().Select(i => segment).ToArray();
         }

         Length = ElementCount * ElementLength;
         FormatString = $"[{segment.Name}{segment.SerializeFormat}]{anchorName}";
      }

      public ITableRun Append(ModelDelta token, int length) => throw new NotImplementedException();

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         return ITableRunExtensions.CreateSegmentDataFormat(this, data, index);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, start, pointerSources);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Start, newPointerSources);
      }
   }
}
