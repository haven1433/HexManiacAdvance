using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TilemapTableRun : BaseRun, ITableRun {
      private readonly IDataModel model;

      public ArrayRunElementSegment Segment { get; }
      public string TilemapAnchor { get; }

      public TilemapMargins Margins { get; }

      public int ElementCount { get; }

      public int ElementLength { get; }

      public IReadOnlyList<string> ElementNames { get; } = new string[0];

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public bool CanAppend => false;

      public override int Length { get; }

      public override string FormatString { get; }

      public TilemapTableRun(IDataModel model, string anchorName, ArrayRunElementSegment segment, TilemapMargins margins, int start, SortedSpan<int> sources = null) : base(start, sources) {
         this.model = model;
         TilemapAnchor = anchorName;
         Segment = segment;
         Margins = margins;

         var tilemapAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchorName);
         var tilemap = model.GetNextRun(tilemapAddress) as ITilemapRun;
         if (tilemap != null) {
            var height = tilemap.SpriteFormat.TileHeight + margins.Top + margins.Bottom;
            var width = tilemap.SpriteFormat.TileWidth + margins.Left + margins.Right;
            ElementCount = height * margins.LengthMultiplier;
            ElementLength = width * Segment.Length;
            ElementContent = width.Range().Select(i => segment).ToArray();
         }

         Length = ElementCount * ElementLength;
         FormatString = $"[{segment.SerializeFormat}]{anchorName}{Margins}";
      }

      public ITableRun Append(ModelDelta token, int length) => throw new NotImplementedException();

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         ITableRunExtensions.Clear(this, model, changeToken, start, length);
      }

      public int lastFormatCreated = int.MaxValue;

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Margins, start, pointerSources);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Margins, Start, newPointerSources);
      }
   }

   public class TilemapMargins {
      public static readonly TilemapMargins Default = new TilemapMargins(1, 0);
      public int LengthMultiplier { get; }
      public int Left { get; }
      public int Top { get; }
      public int Right { get; }
      public int Bottom { get; }

      public TilemapMargins(int multiplier, int uniform) => (LengthMultiplier, Left, Top, Right, Bottom) = (multiplier, uniform, uniform, uniform, uniform);
      public TilemapMargins(int multiplier, int left, int top, int right, int bottom) => (LengthMultiplier, Left, Top, Right, Bottom) = (multiplier, left, top, right, bottom);

      public static TilemapMargins ExtractMargins(ref string token) {
         if (string.IsNullOrWhiteSpace(token)) return Default;
         var name = token;
         var separators = name.Length.Range().Where(i => name[i].IsAny("*+-".ToCharArray())).Concat(new[] { name.Length }).ToArray();
         if (separators.Length == 1) return Default;
         int multiplier = 1;
         if (name[separators[0]] == '*') {
            var tokenLength = separators[1] - separators[0];
            var multiplierText = name.Substring(separators[0] + 1, tokenLength - 1);
            if (!int.TryParse(multiplierText, out multiplier)) multiplier = 1;
            name = name.Substring(0, separators[0]) + name.Substring(separators[1]);
            token = name;
            separators = separators.Skip(1).Select(i => i - tokenLength).ToArray();
         }
         if (separators.Length != 5) return new TilemapMargins(multiplier, 0);
         if (separators.Take(4).Any(i => name[i] == '*')) return new TilemapMargins(multiplier, 0);
         var textMargins = 4.Range().Select(i => name.Substring(separators[i], separators[i + 1] - separators[i]));
         var margins = textMargins.Select(str => int.TryParse(str, out var value) ? value : default).ToArray();
         token = name.Substring(0, separators[0]);
         return new TilemapMargins(multiplier, margins[0], margins[1], margins[2], margins[3]);
      }

      public override string ToString() {
         var result = string.Empty;
         if (LengthMultiplier != 1) result += "*" + LengthMultiplier;
         if (Left == 0 && Top == 0 && Right == 0 && Bottom == 0) return result;
         foreach (var side in new[] { Left, Top, Right, Bottom })
            result += side < 0 ? side.ToString() : "+" + side;
         return result;
      }
   }
}
