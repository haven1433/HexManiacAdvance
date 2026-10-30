using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// Draws the first frame of overworld sprites for the galleries, one sprite at a time and only when somebody asks for it.
   /// There are over a thousand sprites in a ROM, but a gallery only shows a handful of them at once,
   /// so drawing them all up front (every time the data changed, every time a tab was selected) was just wasted waiting.
   /// The pictures are remembered for as long as the model's data doesn't change, and shared by every gallery of the model.
   /// </summary>
   public class OverworldSpriteRenderer {
      private const string CacheKey = "overworld-sprite-renderer";

      private readonly IDataModel model;
      private readonly Dictionary<int, (IPixelViewModel image, bool isPlaceholder)> rendered = new();
      private IPixelViewModel defaultOW;

      private OverworldSpriteRenderer(IDataModel model) => this.model = model;

      /// <summary>The renderer for the current data of the model. A new one is created after the data changes.</summary>
      public static OverworldSpriteRenderer Get(IDataModel model) => model.CurrentCacheScope.GetOrAdd(CacheKey, () => new OverworldSpriteRenderer(model));

      /// <summary>How many sprites have actually been drawn so far.</summary>
      public int RenderedCount { get { lock (rendered) return rendered.Count; } }

      /// <summary>The picture of a sprite. A sprite without usable graphics gets the default sprite, flagged as a placeholder.</summary>
      public (IPixelViewModel image, bool isPlaceholder) Get(int index) {
         lock (rendered) {
            if (rendered.TryGetValue(index, out var existing)) return existing;
            var result = Render(index) ?? (defaultOW, true);
            rendered[index] = result;
            return result;
         }
      }

      /// <summary>Draws a sprite again, because its graphics may have been edited. Returns null (and keeps the old picture) if it can't be drawn right now.</summary>
      public (IPixelViewModel image, bool isPlaceholder)? Redraw(int index) {
         lock (rendered) {
            var result = Render(index);
            if (result == null) return null;
            rendered[index] = result.Value;
            return result;
         }
      }

      private (IPixelViewModel image, bool isPlaceholder)? Render(int index) {
         defaultOW ??= BlockMapViewModel.GetDefaultOW(model);
         // look the table up every time: a gallery can outlive a change of the data (the table may have moved), and the data is always read live
         var ows = model.GetTable(HardcodeTablesModel.OverworldSprites);
         if (ows == null) return null;
         try {
            var image = ObjectEventViewModel.Render(model, new ModelTable(model, ows.Start), defaultOW, index, 0, () => -1);
            return (image, ReferenceEquals(image, defaultOW));
         } catch (Exception) {
            return null;
         }
      }
   }
}
