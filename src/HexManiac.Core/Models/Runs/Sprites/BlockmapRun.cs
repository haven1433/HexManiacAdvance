using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;

namespace HexManiac.Core.Models.Runs.Sprites {
   public class BlockmapRun : BaseRun {
      public int BlockWidth { get; }
      public int BlockHeight { get; }

      public override int Length => BlockWidth * BlockHeight * 2;

      public override string FormatString => $"`blm`";

      public BlockmapRun(int start, SortedSpan<int> sources = null) : base(start, sources) {
         // TODO calculate width/height from pointer sources
         // (BlockWidth, BlockHeight) = (width, height);
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         throw new NotImplementedException();
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         throw new NotImplementedException();
      }
   }
}
