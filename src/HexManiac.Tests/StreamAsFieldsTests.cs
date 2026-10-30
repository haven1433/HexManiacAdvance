using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The evolutions of a CUBE Pokemon and the struct of an overworld sprite are edited as fields (like the evolutions of the old games), not as a line of text.</summary>
   public class StreamAsFieldsTests : BaseViewModelTestClass {
      private const int Table = 0x20, List = 0x80;
      public StreamAsFieldsTests() : base(0x400) { }

      [Fact]
      public void Evolutions_AreFields_WithAddAndRemove() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         // one evolution (level 16 into bob), then the end code
         var data = new byte[] { 1, 0, 16, 0, 1, 0, 0, 0, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0 };
         for (int i = 0; i < data.Length; i++) Model[List + i] = data[i];
         Model[Table + 4] = List; Model[Table + 7] = 0x08;
         ViewPort.Edit($"@{Table:X2} ^pokemon[hp. pad. pad. pad. evolutions<[method:evolutionmethods param: species:names unused:]!FFFF000000000000>]1 ");
         ViewPort.Goto.Execute(Table);
         ViewPort.ChangeHistory.ChangeCompleted();

         var children = ViewPort.Tools.TableTool.Children;
         Assert.Empty(children.OfType<TextStreamElementViewModel>());
         var names = children.OfType<IMultiEnabledArrayElementViewModel>().Select(child => child.Name).ToList();
         Assert.Contains("method1", names);
         Assert.Contains("param1", names);
         Assert.Contains("species1", names);
         var param = children.OfType<FieldArrayElementViewModel>().Single(field => field.Name == "param1");
         param.Content = "20";
         Assert.Equal(20, Model.ReadMultiByteValue(List + 2, 2));

         var add = children.OfType<ButtonArrayElementViewModel>().Single(button => button.Text.StartsWith("Add"));
         add.Command.Execute(null);
         var run = (TableStreamRun)Model.GetNextRun(Model.ReadPointer(Table + 4));
         Assert.Equal(2, run.ElementCount);
         Assert.Equal(0, Model.ReadMultiByteValue(run.Start + 8 + 2, 2)); // the new evolution starts empty
         Assert.Equal(20, Model.ReadMultiByteValue(run.Start + 2, 2));    // the first one is kept

         var remove = ViewPort.Tools.TableTool.Children.OfType<ButtonArrayElementViewModel>().Single(button => button.Text.StartsWith("Remove"));
         remove.Command.Execute(null);
         run = (TableStreamRun)Model.GetNextRun(Model.ReadPointer(Table + 4));
         Assert.Equal(1, run.ElementCount);
      }

      [SkippableFact]
      public void RealCube_EvolutionsOfAPokemon_AreFields() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var port = new HavenSoft.HexManiac.Core.ViewModels.ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         port.Goto.Execute("data.pokemon.stats/2"); // Ivysaur
         var children = port.Tools.TableTool.Children;
         Console.WriteLine(string.Join(" | ", children.OfType<IMultiEnabledArrayElementViewModel>().Select(child => child.Name).Where(name => name.EndsWith("1") || name.StartsWith("tileTag") || name.StartsWith("paletteid"))));
         Assert.True(children.OfType<IMultiEnabledArrayElementViewModel>().Any(child => child.Name == "method1"));
         Assert.True(children.OfType<IMultiEnabledArrayElementViewModel>().Any(child => child.Name == "species1"));
         Assert.True(children.OfType<ButtonArrayElementViewModel>().Any(button => button.Text.StartsWith("Add evolution")));
      }

      [SkippableFact]
      public void RealCube_OverworldSpriteData_IsFields() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var port = new HavenSoft.HexManiac.Core.ViewModels.ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         port.Goto.Execute("graphics.overworld.sprites/0");
         var children = port.Tools.TableTool.Children;
         Console.WriteLine(string.Join(" | ", children.Select(child => child.GetType().Name + ":" + ((child as IMultiEnabledArrayElementViewModel)?.Name ?? (child as StreamElementViewModel)?.ParentName))));
         Assert.False(children.OfType<TextStreamElementViewModel>().Any(text => text.ParentName == "data"));
         var palette = children.OfType<FieldArrayElementViewModel>().First(field => field.Name == "paletteid");
         palette.Content = "1234";
         Assert.Equal(0x1234, model.ReadMultiByteValue(palette.Start, 2));
      }
   }
}
