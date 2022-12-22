using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;


namespace HavenSoft.HexManiac.Integration {
   public class ImageTests {
      public static Singletons singletons { get; } = new Singletons();
      private static readonly string fireredName = "sampleFiles/Pokemon FireRed.gba";

      public List<string> Errors { get; } = new();
      public List<string> Messages { get; } = new();
      public StubFileSystem FileSystem { get; } = new();

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
      }

      private static readonly Lazy<ViewPort> lazyFireRed;
      static ImageTests() {
         lazyFireRed = new Lazy<ViewPort>(() => {
            var model = new HardcodeTablesModel(singletons, File.ReadAllBytes(fireredName), new StoredMetadata(new string[0]));
            return new ViewPort(fireredName, model, InstantDispatch.Instance, singletons);
         });
      }

      private ViewPort LoadFireRed() {
         Skip.IfNot(File.Exists(fireredName));
         var model = new HardcodeTablesModel(singletons, File.ReadAllBytes(fireredName), new StoredMetadata(new string[0]));
         var vm = new ViewPort(fireredName, model, InstantDispatch.Instance, singletons);
         vm.OnError += (sender, e) => Errors.Add(e);
         vm.OnMessage += (sender, e) => Messages.Add(e);
         return vm;
      }

      private ViewPort LoadReadOnlyFireRed() {
         Skip.IfNot(File.Exists(fireredName));
         return lazyFireRed.Value;
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
   }
}
