using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The Goto menu of CUBHMA must have every entry of the original HexManiacAdvance (graphics > pokemon > type > icons, ...).
   /// The first tests need no ROM; the real ROM tests run when HMA_INTRO_EMERALD / HMA_INTRO_CUBE (+ HMA_INTRO_CUBE_TOML) are set and are skipped otherwise.
   /// </summary>
   public class GotoMenuAuditTests : BaseViewModelTestClass {
      /// <summary>Original entries that really have no data in the expansion (a C switch, compile-time constant, different struct...). Explained in the audit table of the hand-off.</summary>
      private static readonly string[] CubeHasNoData = {
         "data.items.pokeball.catchrates",
         "data.pokemon.contest.paintings",
         "data.pokemon.contest.stats",
         "data.pokemon.moves.details.naturepower.list",
         "data.pokemon.moves.details.protect.successrate",
         "data.pokemon.type.camouflage",
         "data.pokemon.type.contest.names",
         "graphics.battle.animations.misc",
         "graphics.battle.animations.special",
         "graphics.battle.animations.status",
         "graphics.battle.background.sprites",
         "graphics.battle.hud.hpbar.sprite",
         "graphics.battle.hud.status",
         "graphics.items.ball.palettes",
         "graphics.items.ball.sprites",
         "graphics.menu.downarrow.palette",
         "graphics.menu.trade.windowtemplates.others",
         "graphics.overworld.reflection.palettes.player",
         "graphics.overworld.reflection.palettes.slot10",
         "graphics.pokedex.menu.palette",
         "graphics.pokemon.animations.options.front",
         "graphics.townmap.annotations.hoenn",
         "graphics.townmap.pokenav.zoom.tilemaps",
         "graphics.trainers.sprites.back.throw",
         "scripts.battle.ai.hpaware.discourage.when.self.high",
         "scripts.battle.ai.hpaware.discourage.when.self.low",
         "scripts.battle.ai.hpaware.discourage.when.self.medium",
         "scripts.battle.ai.hpaware.discourage.when.target.high",
         "scripts.battle.ai.hpaware.discourage.when.target.low",
         "scripts.battle.ai.hpaware.discourage.when.target.medium",
         "scripts.battle.badge.obey.0badges",
         "scripts.commands.battle.ai_script",
         "scripts.evolution.routines",
         "scripts.moves.setupeffects",
         "scripts.pokemon.animations.back.natureMod",
         "scripts.text.stringvars",
         "sound.newgame.titlescreen.song",
      };

      /// <summary>Names the original HexManiacAdvance itself does not resolve either when it opens a fresh Emerald (they only appear after the auto-search of a ROM that has the data; checked with upstream c8de1e8).</summary>
      private static readonly string[] UpstreamAlsoUnresolved = {
         "data.battlefrontier.clerks.helditems.icons",
         "data.battlefrontier.clerks.vitamin.icons",
         "data.items.effects",
         "data.pokemon.type.chart",
         "graphics.trainers.elite4.mugshots.boy.palette",
         "graphics.trainers.elite4.mugshots.drake.palette",
         "graphics.trainers.elite4.mugshots.girl.palette",
         "graphics.trainers.elite4.mugshots.glacia.palette",
         "graphics.trainers.elite4.mugshots.phoebe.palette",
         "graphics.trainers.elite4.mugshots.sidney.palette",
         "graphics.trainers.elite4.mugshots.wallace.palette",
         "scripts.commands.battle.animations",
      };

      [Fact]
      public void VanillaEmerald_HasEveryOriginalGotoEntry() {
         var names = Singletons.GameReferenceTables["BPEE0"].Select(table => table.Name).ToHashSet();
         Assert.Empty(UpstreamGotoEntries.BPEE0.Where(name => !names.Contains(name.Split('-')[0].Split('+')[0]) && !names.Contains(name)));
      }

      [Fact]
      public void VanillaFireRed_HasEveryOriginalGotoEntry() {
         var names = Singletons.GameReferenceTables["BPRE0"].Select(table => table.Name).ToHashSet();
         Assert.Empty(UpstreamGotoEntries.BPRE0.Where(name => !names.Contains(name.Split('-')[0].Split('+')[0]) && !names.Contains(name)));
      }

      [Fact]
      public void VanillaRuby_HasEveryOriginalGotoEntry() {
         var names = Singletons.GameReferenceTables["AXVE0"].Select(table => table.Name).ToHashSet();
         Assert.Empty(UpstreamGotoEntries.AXVE0.Where(name => !names.Contains(name.Split('-')[0].Split('+')[0]) && !names.Contains(name)));
      }

      [Fact]
      public void Alias_LeadsBehindTheAnchorWhenOnlyTheOtherTableExists() {
         ViewPort.Edit("@100 ^graphics.pokemon.type.palettes[x.]4 ");
         Assert.Equal(0x160, GotoAliases.FindAddress(Model, "graphics.pokemon.type.icons"));
         Assert.Equal(0x160, GotoAliases.FindAddress(Model, "Graphics.Pokemon.Type.Icons "));
         Assert.Contains("graphics.pokemon.type.icons", GotoAliases.Matching(Model, "pokemon type icons"));
      }

      [Fact]
      public void Alias_IsIgnoredWhenTheRealAnchorExists() {
         ViewPort.Edit("@100 ^graphics.pokemon.type.palettes[x.]4 @200 ^graphics.pokemon.type.icons[y.]4 ");
         Assert.Null(GotoAliases.FindTarget(Model, "graphics.pokemon.type.icons"));
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(Model, "graphics.pokemon.type.icons"));
         Assert.DoesNotContain("graphics.pokemon.type.icons", GotoAliases.Available(Model));
      }

      [Fact]
      public void Alias_WithoutAnyTarget_IsNotOffered() {
         Assert.Empty(GotoAliases.Available(Model));
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(Model, "graphics.pokemon.type.icons"));
      }

      [Fact]
      public void Goto_FollowsAnAlias() {
         ViewPort.Edit("@100 ^graphics.pokemon.type.palettes[x.]4 ");
         ViewPort.Goto.Execute("graphics.pokemon.type.icons");
         Assert.Equal(0x160, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      private static IDataModel Load(string variable, string tomlVariable = null) {
         var path = Environment.GetEnvironmentVariable(variable);
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), $"set {variable} to a ROM to run this test");
         StoredMetadata metadata = null;
         if (tomlVariable != null) {
            var toml = Environment.GetEnvironmentVariable(tomlVariable);
            Skip.If(string.IsNullOrEmpty(toml) || !File.Exists(toml), $"set {tomlVariable} to run this test");
            metadata = new StoredMetadata(File.ReadAllLines(toml));
         }
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), metadata);
         model.InitializationWorkload.Wait();
         return model;
      }

      private static bool Resolves(IDataModel model, string name) {
         var baseName = name.Split('-')[0].Split('+')[0];
         foreach (var candidate in new[] { name, baseName }) {
            if (model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, candidate) != Pointer.NULL) return true;
            if (model.GetMatchedWords(candidate).Count > 0) return true;
            if (GotoAliases.FindAddress(model, candidate) != Pointer.NULL) return true;
         }
         return false;
      }

      [SkippableFact]
      public void RealEmerald_EveryOriginalEntryResolves() {
         var model = Load("HMA_INTRO_EMERALD");
         Assert.Equal(UpstreamAlsoUnresolved.Where(UpstreamGotoEntries.BPEE0.Contains).OrderBy(n => n), UpstreamGotoEntries.BPEE0.Where(name => !Resolves(model, name)).OrderBy(n => n));
      }

      [SkippableFact]
      public void RealCube_EveryOriginalEntryResolvesOrIsExplained() {
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         var missing = UpstreamGotoEntries.BPEE0.Where(name => !Resolves(model, name)).ToList();
         var explained = CubeHasNoData.Concat(UpstreamAlsoUnresolved).ToHashSet();
         Assert.Empty(missing.Where(name => !explained.Contains(name))); // everything else leads somewhere
         Assert.Empty(CubeHasNoData.Where(name => UpstreamGotoEntries.BPEE0.Contains(name) && !missing.Contains(name))); // the list of 'no data' entries stays honest: once one resolves, remove it
      }

      [SkippableFact]
      public void RealCube_TypeIconsAreASpriteThatOpensInTheSpriteTool() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         var palettes = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "graphics.pokemon.type.palettes");
         var icons = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "graphics.pokemon.type.icons");
         Assert.Equal(palettes + 0x60, icons);
         var run = Assert.IsAssignableFrom<ISpriteRun>(model.GetNextRun(icons));
         Assert.Equal(icons, run.Start);
         Assert.Equal(32 * 4 * 52, run.DecompressedLength); // 34 type icons of 32x12 pixels, stacked
         Assert.True(run.SupportsEdit);
         Assert.NotNull(run.GetPixels(model, 0, 0));
         Assert.Equal("graphics.pokemon.type.palettes", run.SpriteFormat.PaletteHint);
         // the alias is no longer needed: the name is a real anchor
         Assert.Null(GotoAliases.FindTarget(model, "graphics.pokemon.type.icons"));

         // choosing the menu entry opens the image
         var viewModel = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         viewModel.Goto.Execute("graphics.pokemon.type.icons");
         Assert.Equal(icons, viewModel.ConvertViewPointToAddress(viewModel.SelectionStart));
         Assert.Equal(viewModel.Tools.SpriteTool, viewModel.Tools.SelectedTool);
         Assert.Equal(icons, viewModel.Tools.SpriteTool.SpriteAddress);
      }

      [SkippableFact]
      public void RealCube_TypeIconsCanBeEditedAndTheGameStillReadsThem() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), "set HMA_INTRO_CUBE to run this test");
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         var icons = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "graphics.pokemon.type.icons");
         var run = (ISpriteRun)model.GetNextRun(icons);
         var pixels = run.GetPixels(model, 0, 0);
         var oldPixel = pixels[1, 1];
         pixels[1, 1] = (oldPixel + 1) % 16;
         var changed = run.SetPixels(model, new ModelDelta(), 0, pixels);
         var newPixels = ((ISpriteRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "graphics.pokemon.type.icons"))).GetPixels(model, 0, 0);
         Assert.Equal((oldPixel + 1) % 16, newPixels[1, 1]);
         Assert.Equal(0x10, model[changed.Start]); // written as plain LZ77, which the expansion's DecompressDataWithHeader accepts
      }

      [Fact]
      public void SmolDecoder_RejectsDataThatIsNotSmol() {
         Assert.False(SmolDecoder.TryDecompress(new byte[64], 0, out _, out _));
         Assert.False(SmolDecoder.TryDecompress(new byte[] { 0x10, 0x20, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, out _, out _));
         var junk = Enumerable.Range(0, 200).Select(i => (byte)(i * 37 + 1)).ToArray();
         for (int i = 0; i < 100; i++) SmolDecoder.TryDecompress(junk, i, out _, out _); // must not throw
      }

      [Fact]
      public void LzRun_OfLz77Data_IsNotSmol() {
         var data = LZRun.Compress(new byte[64]).ToArray();
         Assert.False(new LZRun(new PokemonModel(data), 0).IsSmol);
         Assert.Equal(64, LZRun.ReadDecompressedLength(data, 0));
      }

      [SkippableTheory]
      [InlineData("HMA_INTRO_EMERALD")]
      [InlineData("HMA_INTRO_FIRERED")]
      public void RealVanilla_TypeIconsOpenAsASprite(string variable) {
         var model = Load(variable);
         var icons = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "graphics.pokemon.type.icons");
         Assert.NotEqual(Pointer.NULL, icons);
         var run = Assert.IsAssignableFrom<ISpriteRun>(model.GetNextRun(icons));
         Assert.True(run.SupportsEdit);
         var viewModel = new ViewPort("vanilla.gba", model, InstantDispatch.Instance, Singletons);
         viewModel.Goto.Execute("graphics.pokemon.type.icons");
         Assert.Equal(icons, viewModel.ConvertViewPointToAddress(viewModel.SelectionStart));
         Assert.Equal(viewModel.Tools.SpriteTool, viewModel.Tools.SelectedTool);
      }

      [SkippableFact]
      public void RealCube_MergedEntriesLeadToTheirColumns() {
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         foreach (var name in new[] { "graphics.pokemon.sprites.front", "graphics.pokemon.icons.sprites", "graphics.pokemon.palettes.normal", "data.pokemon.names", "data.pokemon.moves.names", "graphics.items.sprites", "data.pokedex.national" }) {
            Assert.NotEqual(Pointer.NULL, GotoAliases.FindAddress(model, name));
         }
      }
   }
}
