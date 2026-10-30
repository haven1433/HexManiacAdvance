using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The View menu choice for how the map editor draws an object event whose graphics is a graphics variable (VAR_OBJ_GFX_ID_x).
   /// The choice is static (shared by every map), so these tests share a collection with the other tests that draw graphics-variable events: xunit runs the tests of a collection one at a time.
   /// </summary>
   [Collection("GraphicsVariableDisplay")]
   public class GraphicsVariableDisplayTests : BaseViewModelTestClass, IDisposable {
      private static readonly IPixelViewModel QuestionMark = new ReadonlyPixelViewModel(16, 24);
      private static readonly IPixelViewModel FirstPokemon = new ReadonlyPixelViewModel(32, 24);
      private static readonly IPixelViewModel Placeholder = new ReadonlyPixelViewModel(16, 32);
      private static readonly IPixelViewModel SpriteNine = new ReadonlyPixelViewModel(16, 32), SpriteTwelve = new ReadonlyPixelViewModel(16, 32);
      private const int Script = 0x200;

      public GraphicsVariableDisplayTests() : base(0x1000) => ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.ScriptSprite;
      public void Dispose() => ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.ScriptSprite;

      private static IPixelViewModel Draw(Func<int, IPixelViewModel> sprites, GraphicsVariableDisplay mode, params Func<IEnumerable<int>>[] places) =>
         ObjectEventViewModel.ChooseDynamicPicture(mode, QuestionMark, () => FirstPokemon, () => Placeholder, places, sprites);

      private static IPixelViewModel Sprites(int sprite) => sprite switch { 9 => SpriteNine, 12 => SpriteTwelve, _ => null };

      private void WriteScript(params int[][] setvars) {
         int at = Script;
         foreach (var (variable, value) in setvars.Select(s => (s[0], s[1]))) {
            Data[at] = 0x16;
            Data[at + 1] = (byte)variable; Data[at + 2] = (byte)(variable >> 8);
            Data[at + 3] = (byte)value; Data[at + 4] = (byte)(value >> 8);
            at += 5;
         }
         Data[at] = 0x02; // end
      }

      #region the four choices

      [Fact]
      public void ScriptSprite_IsTheDefault() {
         Assert.Equal(GraphicsVariableDisplay.ScriptSprite, ObjectEventViewModel.GraphicsVariableDisplay);
         Assert.Equal(0, (int)GraphicsVariableDisplay.ScriptSprite);
      }

      [Fact]
      public void ScriptSprite_ShowsTheFirstSpriteAScriptGives() {
         var picture = Draw(Sprites, GraphicsVariableDisplay.ScriptSprite, () => new[] { 9 });
         Assert.Same(SpriteNine, picture);
      }

      [Fact]
      public void ScriptSprite_SkipsASpriteTheRomCannotDraw_AndTakesTheNextOne() {
         var picture = Draw(Sprites, GraphicsVariableDisplay.ScriptSprite, () => new[] { 77, 12, 9 });
         Assert.Same(SpriteTwelve, picture);
      }

      [Fact]
      public void ScriptSprite_LooksInTheBetterPlaceFirst() {
         var picture = Draw(Sprites, GraphicsVariableDisplay.ScriptSprite, () => new[] { -1 }, () => new[] { 12 }, () => new[] { 9 });
         Assert.Same(SpriteTwelve, picture);
      }

      [Fact]
      public void ScriptSprite_WithoutAnyAssignment_FallsBackToTheBlackedOutQuestionMark() {
         Assert.Same(Placeholder, Draw(Sprites, GraphicsVariableDisplay.ScriptSprite));
         Assert.Same(Placeholder, Draw(Sprites, GraphicsVariableDisplay.ScriptSprite, () => new[] { -1 }, () => Array.Empty<int>(), () => new[] { 400 }));
      }

      [Fact]
      public void BlackedOutQuestionMark_IgnoresTheScripts() {
         Assert.Same(Placeholder, Draw(Sprites, GraphicsVariableDisplay.BlackedOutQuestionMark, () => new[] { 9 }));
      }

      [Fact]
      public void QuestionMark_IsThePictureOriginalHexManiacAdvanceDraws_AndIgnoresTheScripts() {
         Assert.Same(QuestionMark, Draw(Sprites, GraphicsVariableDisplay.QuestionMark, () => new[] { 9 }));
      }

      [Fact]
      public void FirstPokemonIcon_IgnoresTheScripts() {
         Assert.Same(FirstPokemon, Draw(Sprites, GraphicsVariableDisplay.FirstPokemonIcon, () => new[] { 9 }));
      }

      [Fact]
      public void FirstPokemonIcon_WithoutPokemonIcons_UsesTheQuestionMark()
         => Assert.Same(QuestionMark, ObjectEventViewModel.ChooseDynamicPicture(GraphicsVariableDisplay.FirstPokemonIcon, QuestionMark, () => null, () => Placeholder, new Func<IEnumerable<int>>[0], Sprites));

      [Fact]
      public void QuestionMark_WithoutAPicture_UsesThePlaceholder()
         => Assert.Same(Placeholder, ObjectEventViewModel.ChooseDynamicPicture(GraphicsVariableDisplay.QuestionMark, null, () => null, () => Placeholder, new Func<IEnumerable<int>>[0], Sprites));

      #endregion

      #region Render (the one every place that draws an object event goes through)

      [Fact]
      public void Render_QuestionMarkChoice_DrawsTheDefaultSprite() {
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.QuestionMark;
         var defaultSprite = new ReadonlyPixelViewModel(16, 32);
         foreach (var variable in new[] { 240, 247, 255 }) {
            Assert.Same(defaultSprite, ObjectEventViewModel.Render(Model, null, defaultSprite, variable, 0, () => 5));
         }
      }

      [Fact]
      public void Render_BlackedOutChoice_DrawsThePlaceholder_ScriptChoiceWithoutScriptsToo() {
         var defaultSprite = new ReadonlyPixelViewModel(16, 32);
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.BlackedOutQuestionMark;
         var blacked = ObjectEventViewModel.Render(Model, null, defaultSprite, 240, 0, () => 5);
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.ScriptSprite;
         var noScript = ObjectEventViewModel.Render(Model, null, defaultSprite, 240, 0, () => -1);
         Assert.NotSame(defaultSprite, blacked);
         Assert.Same(blacked, noScript);
         Assert.Contains(blacked.PixelData, p => p == 0x7FFF);
      }

      [Fact]
      public void Render_FirstPokemonChoice_WithoutPokemonIcons_UsesTheDefaultSprite() {
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.FirstPokemonIcon;
         var defaultSprite = new ReadonlyPixelViewModel(16, 32);
         Assert.Same(defaultSprite, ObjectEventViewModel.Render(Model, null, defaultSprite, 250, 0, () => -1));
      }

      [Fact]
      public void Render_ANormalSprite_IsNotAffectedByTheChoice() {
         var defaultSprite = new ReadonlyPixelViewModel(16, 32);
         foreach (var mode in Enum.GetValues<GraphicsVariableDisplay>()) {
            ObjectEventViewModel.GraphicsVariableDisplay = mode;
            Assert.Same(defaultSprite, ObjectEventViewModel.Render(Model, null, defaultSprite, 12, 0, () => -1));
         }
      }

      [Fact]
      public void TheListOfSpriteRenders_IsMadeAgain_WhenTheChoiceChanges() {
         // the cached list that templates and the sprite lists use must follow the choice
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.QuestionMark;
         var first = BlockMapViewModel.GetOverworldSpriteRenders(Model);
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.BlackedOutQuestionMark;
         var second = BlockMapViewModel.GetOverworldSpriteRenders(Model);
         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.QuestionMark;
         Assert.NotSame(first, second);
         Assert.Same(first, BlockMapViewModel.GetOverworldSpriteRenders(Model));
      }

      #endregion

      #region finding the sprites in scripts

      private HavenSoft.HexManiac.Core.Models.Code.ScriptParser Parser => ViewPort.Tools.CodeTool.ScriptParser;

      [Fact]
      public void Scan_FindsSetvarOfTheGraphicsVariables_InScriptOrder() {
         WriteScript(new[] { 0x4011, 12 }, new[] { 0x4010, 9 }, new[] { 0x4010, 0x4001 }, new[] { 0x8004, 3 }, new[] { 0x4010, 400 });
         var found = GraphicsVariableSprites.Scan(Model, Parser, new[] { Script });
         Assert.Equal(new[] { 12, 9, 400 }, found.Any().ToArray()); // the variable-sourced (0x4001) and non-graphics variables (0x8004) are not sprites
         Assert.Equal(new[] { 9, 400 }, found.For(0).ToArray());
         Assert.Equal(new[] { 12 }, found.For(1).ToArray());
         Assert.Empty(found.For(2));
      }

      [Fact]
      public void Scan_WithoutAScriptParser_FindsNothing() {
         WriteScript(new[] { 0x4010, 9 });
         Assert.Empty(GraphicsVariableSprites.Scan(Model, null, new[] { Script }).All);
      }

      [Fact]
      public void Everywhere_IsKeptUntilItIsInvalidated() {
         WriteScript(new[] { 0x4010, 9 });
         var parser = Parser;
         Assert.Equal(new[] { 9 }, GraphicsVariableSprites.Everywhere(Model, parser, () => new[] { Script }).For(0).ToArray());
         WriteScript(new[] { 0x4010, 12 });
         Assert.Equal(new[] { 9 }, GraphicsVariableSprites.Everywhere(Model, parser, () => new[] { Script }).For(0).ToArray()); // not walked again
         GraphicsVariableSprites.Invalidate(Model);
         Assert.Equal(new[] { 12 }, GraphicsVariableSprites.Everywhere(Model, parser, () => new[] { Script }).For(0).ToArray());
      }

      [Fact]
      public void Render_WhenTheScriptsNameASpriteTheRomCannotDraw_KeepsThePlaceholder() {
         // this ROM has no sprite table, so sprite 9 cannot be drawn (the lookup itself is covered above; the real ROM tests draw real sprites)
         WriteScript(new[] { 0x4010, 9 });
         GraphicsVariableSprites.Everywhere(Model, Parser, () => new[] { Script });
         var defaultSprite = new ReadonlyPixelViewModel(16, 32);
         var picture = ObjectEventViewModel.Render(Model, null, defaultSprite, 240, 0, () => -1);
         Assert.NotSame(defaultSprite, picture);
         Assert.Contains(picture.PixelData, p => p == 0x7FFF);
      }

      #endregion

      #region the setting

      private static EditorViewModel CreateEditor(params string[] settings) {
         var fileSystem = new StubFileSystem { MetadataFor = name => settings.Length == 0 ? null : settings };
         return new EditorViewModel(fileSystem);
      }

      [Fact]
      public void Setting_IsSavedNextToTheOtherViewOptions_AndSurvivesARestart() {
         string[] saved = null;
         var firstRun = new EditorViewModel(new StubFileSystem { SaveMetadata = (name, lines) => { saved = lines; return true; } });
         firstRun.GraphicsVariableDisplay = GraphicsVariableDisplay.FirstPokemonIcon;
         firstRun.WriteAppLevelMetadata();

         Assert.Contains("GraphicsVariableDisplay = 3", saved);
         Assert.True(Array.IndexOf(saved, "GraphicsVariableDisplay = 3") > Array.IndexOf(saved, "[GeneralSettings]"));

         ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.ScriptSprite;
         var secondRun = CreateEditor(saved);
         Assert.Equal(GraphicsVariableDisplay.FirstPokemonIcon, secondRun.GraphicsVariableDisplay);
         Assert.Equal(GraphicsVariableDisplay.FirstPokemonIcon, ObjectEventViewModel.GraphicsVariableDisplay);
      }

      [Theory]
      [InlineData(0, GraphicsVariableDisplay.ScriptSprite)]
      [InlineData(1, GraphicsVariableDisplay.BlackedOutQuestionMark)]
      [InlineData(2, GraphicsVariableDisplay.QuestionMark)]
      [InlineData(3, GraphicsVariableDisplay.FirstPokemonIcon)]
      public void Setting_LoadsEveryChoice(int stored, GraphicsVariableDisplay expected) {
         Assert.Equal(expected, CreateEditor("[GeneralSettings]", $"GraphicsVariableDisplay = {stored}").GraphicsVariableDisplay);
      }

      [Theory]
      [InlineData("GraphicsVariableDisplay = banana")]
      [InlineData("GraphicsVariableDisplay = 9")]
      [InlineData("GraphicsVariableDisplay = -1")]
      [InlineData("GraphicsVariableDisplay = ")]
      [InlineData("AnimateScroll = False")] // a settings file from before the choice existed
      public void Setting_ThatIsMissingOrBroken_KeepsTheDefault(string line) {
         Assert.Equal(GraphicsVariableDisplay.ScriptSprite, CreateEditor("[GeneralSettings]", line).GraphicsVariableDisplay);
      }

      [Fact]
      public void Setting_Changing_NotifiesOnce_AndTheSameChoiceDoesNothing() {
         var editor = CreateEditor();
         var names = new List<string>();
         editor.PropertyChanged += (sender, e) => names.Add(e.PropertyName);
         editor.GraphicsVariableDisplay = GraphicsVariableDisplay.QuestionMark;
         editor.GraphicsVariableDisplay = GraphicsVariableDisplay.QuestionMark;
         Assert.Equal(1, names.Count(n => n == nameof(EditorViewModel.GraphicsVariableDisplay)));
         Assert.Equal(GraphicsVariableDisplay.QuestionMark, ObjectEventViewModel.GraphicsVariableDisplay);
      }

      #endregion
   }

   /// <summary>Real CUBE ROM (HMA_INTRO_CUBE + HMA_INTRO_CUBE_TOML): the scripts of the expansion's maps give the graphics variables sprites. Skipped otherwise.</summary>
   [Collection("GraphicsVariableDisplay")]
   public class GraphicsVariableDisplayRealRomTests : IDisposable {
      public void Dispose() => ObjectEventViewModel.GraphicsVariableDisplay = GraphicsVariableDisplay.ScriptSprite;

      private static (ViewPort, IDataModel) Load() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(toml)));
         model.InitializationWorkload.Wait();
         var viewPort = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         return (viewPort, model);
      }

      [SkippableFact]
      public void Cube_ScriptsGiveTheGraphicsVariablesSprites() {
         var (viewPort, model) = Load();
         var found = GraphicsVariableSprites.Everywhere(model, viewPort.Tools.CodeTool.ScriptParser);
         Assert.NotEmpty(found.All);
         Assert.All(found.All, a => Assert.InRange(a.Variable, 0, 15));
      }

      [SkippableFact]
      public void Cube_TheEventsFollowTheChoice() {
         var (viewPort, model) = Load();
         GraphicsVariableSprites.RegisterParser(model, viewPort.Tools.CodeTool.ScriptParser);
         var start = ObjectEventViewModel.GetDynamicGraphicsStart(model);
         var found = GraphicsVariableSprites.Everywhere(model);
         var table = model.GetTable(HardcodeTablesModel.OverworldSprites);
         var owTable = new ModelTable(model, table.Start);
         var defaultSprite = BlockMapViewModel.GetDefaultOW(model);
         // the first variable that a script gives a sprite this ROM can draw
         var variable = found.All.First(a => a.Sprite < owTable.Count).Variable;

         IPixelViewModel Draw(GraphicsVariableDisplay mode) {
            ObjectEventViewModel.GraphicsVariableDisplay = mode;
            return ObjectEventViewModel.Render(model, owTable, defaultSprite, start + variable, 0, () => -1);
         }
         var fromScript = Draw(GraphicsVariableDisplay.ScriptSprite);
         var blacked = Draw(GraphicsVariableDisplay.BlackedOutQuestionMark);
         var questionMark = Draw(GraphicsVariableDisplay.QuestionMark);
         var pokemon = Draw(GraphicsVariableDisplay.FirstPokemonIcon);

         Assert.Same(defaultSprite, questionMark);
         Assert.NotSame(blacked, fromScript);
         Assert.False(blacked.PixelData.SequenceEqual(fromScript.PixelData)); // a real sprite, not the black silhouette
         Assert.NotSame(defaultSprite, pokemon);
         Assert.True(pokemon.PixelWidth > 0);
         Assert.False(pokemon.PixelData.SequenceEqual(defaultSprite.PixelData)); // the first Pokemon is not the question mark
      }
   }
}
