using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The back picture of the player's characters shows and edits every pose (four poses of 64x64 one below the other), not just the first.</summary>
   public class TrainerBackPicsTests : BaseViewModelTestClass {
      [SkippableFact]
      public void RealCube_BackPictures_CoverEveryPose() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var table = model.GetTableModel("data.trainers.sprites");
         var picture = table[0].GetSubTable("back")[0].GetAddress("image");
         var run = (ISpriteRun)model.GetNextRun(picture);
         Console.WriteLine($"back picture at {picture:X}: {run.SpriteFormat.TileWidth}x{run.SpriteFormat.TileHeight} tiles, {run.Length} bytes");
         Assert.Equal(4 * 8, run.SpriteFormat.TileHeight);
         var port = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         var tab = new CharacterCustomizationTab(port);
         var editor = port.CreateImageEditor(tab.Boy.Sprites.BackAddress, 0, 0);
         Assert.NotNull(editor);
      }
   }
}
