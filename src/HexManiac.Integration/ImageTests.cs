using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Tests;
using System.Collections.Generic;
using System.Linq;
using Xunit;


namespace HavenSoft.HexManiac.Integration {
   public class ImageTests : IntegrationTests {

      // for image import/export
      private short[] data;
      private int width;
      private string name;

      #region Constructors

      public ImageTests() {
         FileSystem.SaveImage = (d, w, n) => (data, width, name) = (d, w, n);
         FileSystem.SaveIndexedImage = (pixels, palette, text) => {
            width = pixels.GetLength(0);
            int height = pixels.GetLength(1);
            name = text;
            data = new short[width * height];
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) data[y * width + x] = palette[pixels[x, y]];
         };
         FileSystem.LoadImage = text => (data, width);
         FileSystem.TryLoadIndexImage = (ref string filename, out int[,] imageData, out IReadOnlyList<short> paletteData) => {
            filename = "chosefile.png";
            imageData = null;
            paletteData = null;
            return false;
         };
      }

      #endregion

      #region Utilities

      /// <summary>
      /// Return a list of images, where each image is one page of the image currently loaded into the tool.
      /// </summary>
      private List<short[]> ReadImageTool(SpriteTool tool) {
         var list = new List<short[]>();
         while (tool.PreviousPalettePage.CanExecute(default)) tool.PreviousPalettePage.Execute();
         while (tool.PreviousSpritePage.CanExecute(default)) tool.PreviousSpritePage.Execute();

         list.Add(tool.PixelData);
         while (tool.NextSpritePage.CanExecute(default)) {
            tool.NextSpritePage.Execute();
            if (tool.NextPalettePage.CanExecute(default)) tool.NextPalettePage.Execute();
            list.Add(tool.PixelData);
         }

         return list;
      }

      #endregion

      [SkippableFact]
      public void IntrosceneGengar_ExportThenImport_NoEdit() {
         var vp = LoadFireRed();
         var before = vp.Model.Get<LzSpriteRun>("graphics.titlescreen.introscene.gengar.sprite").GetData();

         vp.Goto.Execute("graphics.titlescreen.introscene.gengar.sprite");
         var address = vp.ConvertViewPointToAddress(vp.SelectionStart);
         vp.Tools.SpriteTool.Export(FileSystem, string.Empty, address, string.Empty);
         vp.Tools.SpriteTool.TryImport(FileSystem, string.Empty, address, string.Empty, ImportType.Cautious);

         var after = vp.Model.Get<LzSpriteRun>("graphics.titlescreen.introscene.gengar.sprite").GetData();
         Assert.Equal(before, after);
      }

      [SkippableFact]
      public void Castform_ExportTallThenImport_NoEdit() {
         var vp = LoadFireRed();
         var tool = vp.Tools.SpriteTool;
         vp.Goto.Execute("graphics.pokemon.sprites.front/castform/sprite/");
         var initialImages = ReadImageTool(tool);

         vp.Tools.SpriteTool.ExecuteExportMany(FileSystem, ImageExportMode.Vertical);
         vp.Tools.SpriteTool.TryImport(FileSystem, string.Empty, vp.Tools.SpriteTool.SpriteAddress, string.Empty, ImportType.Greedy);

         var newImages = ReadImageTool(vp.Tools.SpriteTool);
         for (int i = 0; i < initialImages.Count; i++) {
            Assert.All(initialImages[i].Length.Range(), j => Assert.Equal(initialImages[i][j], newImages[i][j]));
         }
      }

      // castform: export all pages should not care which palette/sprite is selected
      [SkippableTheory]
      [InlineData(0, 1)]
      [InlineData(1, 0)]
      [InlineData(3, 3)]
      public void Castform_ExportTall_SameExportRegardlessOfPageSelections(int spritePage, int palettePage) {
         var vp = LoadReadOnlyFireRed();
         var tool = vp.Tools.SpriteTool;
         vp.Goto.Execute("graphics.pokemon.sprites.front/castform/sprite/");

         (tool.SpritePage, tool.PalettePage) = (0, 0);
         vp.Tools.SpriteTool.ExecuteExportMany(FileSystem, ImageExportMode.Vertical);
         var reference = data.ToArray();

         (tool.SpritePage, tool.PalettePage) = (spritePage, palettePage);
         vp.Tools.SpriteTool.ExecuteExportMany(FileSystem, ImageExportMode.Vertical);
         Assert.All(data.Length.Range(), i => Assert.Equal(reference[i], data[i]));
      }

      [SkippableFact]
      public void ExportTilesetWithPalette2_EditTileset_ImportTilesetWithPalette2_DataMatches() {
         var blocks = "data.maps.banks/0/maps/9/map/0/layout/0/blockdata1/0";
         var tileset = $"{blocks}/tileset/";
         var palette = $"{blocks}/pal/";
         var firered = LoadFireRed();
         firered.Goto.Execute(palette);
         firered.Goto.Execute(tileset);
         firered.Tools.SpriteTool.PalettePage = 2;
         firered.Edit("^`lzs4x16x32`");
         firered.Refresh();
         firered.Tools.SpriteTool.ExportSpriteAndPalette(FileSystem);
         var originalExport = data;

         firered.Goto.Execute($"{tileset}+40");
         firered.Edit("00 ");
         firered.Tools.SpriteTool.ImportSpriteAndPalette(FileSystem);

         firered.Tools.SpriteTool.ExportSpriteAndPalette(FileSystem);
         var afterImport = data;
         Assert.All(originalExport.Length.Range(), i => Assert.Equal(originalExport[i], afterImport[i]));
      }
   }
}
