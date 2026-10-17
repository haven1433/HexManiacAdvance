using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// Pictures for a drop-down that picks an overworld sprite (the berry tree stages, a Battle Frontier brain's look, an object's graphics, ...).
   /// The sprites are drawn the same way the map editor and the sprite gallery draw them: first frame, with the palette the sprite asks for.
   /// </summary>
   public static class OverworldComboOptions {
      /// <summary>True for the format of a struct that holds a list of overworld frames, like the elements of graphics.overworld.sprites.</summary>
      public static bool IsOverworldSpriteTable(string innerFormat) => !string.IsNullOrEmpty(innerFormat) && innerFormat.StartsWith("[") && innerFormat.Contains("`osl");

      /// <summary>
      /// One option per text option. A sprite that has no graphics (an unused entry) keeps its text-only option, so the others can still show pictures.
      /// </summary>
      public static IReadOnlyList<ComboOption> Render(IDataModel model, ITableRun spriteTable, IReadOnlyList<ComboOption> textOptions) {
         var table = new ModelTable(model, spriteTable.Start);
         var defaultOW = BlockMapViewModel.GetDefaultOW(model);
         var result = new List<ComboOption>(textOptions.Count);
         foreach (var option in textOptions) {
            var image = RenderOne(model, table, defaultOW, option.Index);
            if (image == null || ReferenceEquals(image, defaultOW) || image.PixelData == null || image.PixelData.Length == 0) {
               result.Add(option);
               continue;
            }
            // option pictures have no transparent color of their own: the sprite's background becomes -1
            var pixels = new short[image.PixelData.Length];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = image.PixelData[i] == image.Transparent ? (short)-1 : image.PixelData[i];
            result.Add(VisualComboOption.CreateFromSprite(option.Text, pixels, image.PixelWidth, option.Index, 1, true));
         }
         return result;
      }

      private static IPixelViewModel RenderOne(IDataModel model, ModelTable table, IPixelViewModel defaultOW, int index) {
         try {
            return RenderBerryTree(model, table, index) ?? ObjectEventViewModel.Render(model, table, defaultOW, index, 0, () => -1);
         } catch (Exception) {
            return null;
         }
      }

      /// <summary>
      /// The berry tree graphics only say how big a tree is: the picture comes from the berry's own frame table (listed in the berry stats).
      /// So a sprite whose frame table belongs to a berry is drawn as that berry's tree, at a growth stage with the same size.
      /// </summary>
      private static IPixelViewModel RenderBerryTree(IDataModel model, ModelTable table, int index) {
         var berries = model.GetTable(HardcodeTablesModel.BerryTableName);
         if (berries == null || index < 0 || index >= table.Count) return null;
         var info = table[index].GetSubTable("data");
         if (info == null || info.Count == 0 || !info[0].HasField("sprites")) return null;
         if (!(model.GetNextRun(info[0].GetAddress("sprites")) is OverworldSpriteListRun frames) || frames.PointerSources == null) return null;
         var treeHeight = info[0].HasField("height") ? info[0].GetValue("height") : 32;
         foreach (var source in frames.PointerSources) {
            if (source < berries.Start || source >= berries.Start + berries.Length) continue;
            return ObjectEventViewModel.RenderBerryStage(model, (source - berries.Start) / berries.ElementLength, treeHeight <= 16 ? 1 : 4);
         }

         // the vanilla games don't list the frame tables in the berry stats: their generic berry tree graphics use the frame table of one berry (Pecha)
         var berry = BerryTreeGraphics.FindBerryWithPictureTable(model, frames.Start);
         if (berry >= 0) return ObjectEventViewModel.RenderBerryStage(model, berry, treeHeight <= 16 ? 1 : 4);
         return null;
      }
   }
}
