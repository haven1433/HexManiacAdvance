using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The animation preview of the unedited games, whose pieces are tables of their own (graphics.pokemon.sprites.front, .palettes.normal, .animations.front/.delay/.frames),
   /// next to the layout of pokeemerald-expansion, whose pieces are fields of the pokemon table.
   /// The ROM is synthetic: species 0 is the placeholder, species 1 to 3 have an (uncompressed) 64x128 sheet, a palette, a movement id, a delay and a frame list.
   /// </summary>
   public class PokemonAnimationLayoutTests : BaseViewModelTestClass {
      private const int Names = 0x00, Pictures = 0x20, Palettes = 0x30, Ids = 0x40, Delays = 0x44, FrameLists = 0x48;
      private const int SheetBase = 0x1000, PaletteBase = 0x5000, PairBase = 0x6000, CommandBase = 0x6400;

      public PokemonAnimationLayoutTests() : base(0x8000) { }

      private void WriteWord(int address, uint value) {
         for (int i = 0; i < 4; i++) Data[address + i] = (byte)(value >> (8 * i));
      }

      private void WritePointer(int address, int destination) => WriteWord(address, 0x08000000u + (uint)destination);

      /// <summary>The data of the pieces. Species 1 to 3 (row 1 to 3 of the sprite tables; the movement ids and delays start at species 1).</summary>
      private void WriteSpeciesData() {
         for (int species = 1; species <= 3; species++) {
            WritePointer(Pictures + species * 4, SheetBase + species * 0x1000 - 0x1000);
            for (int i = 0; i < 0x1000; i++) Data[SheetBase + species * 0x1000 - 0x1000 + i] = (byte)(0x11 * species);
            WritePointer(Palettes + species * 4, PaletteBase + species * 0x20);
            for (int i = 0; i < 16; i++) { Data[PaletteBase + species * 0x20 + i * 2] = (byte)(i * 2 + species); Data[PaletteBase + species * 0x20 + i * 2 + 1] = (byte)i; }
            Data[Ids + species - 1] = (byte)(10 + species);
            Data[Delays + species - 1] = (byte)(2 * species);
            var pair = PairBase + species * 8;
            WritePointer(FrameLists + species * 4, pair);
            WritePointer(pair, CommandBase);                    // the still picture
            WritePointer(pair + 4, CommandBase + species * 0x10); // the animation
            WriteWord(CommandBase + species * 0x10, (uint)(5 * species << 16) | 1);
            WriteWord(CommandBase + species * 0x10 + 4, (uint)(7 << 16) | 0);
            WriteWord(CommandBase + species * 0x10 + 8, 0xFFFF);
         }
         WriteWord(CommandBase, 0xFFFF);
      }

      private void CreateSeparateTables(bool withDelays = true) {
         WriteSpeciesData();
         CreateTextTable("data.pokemon.names", Names, "?", "A", "B", "C");
         Create(Pictures, "graphics.pokemon.sprites.front[sprite<`ucs4x8x16`>]data.pokemon.names");
         Create(Palettes, "graphics.pokemon.palettes.normal[palette<`ucp4`>]data.pokemon.names");
         Create(Ids, "graphics.pokemon.animations.front[index.]data.pokemon.names-1");
         if (withDelays) Create(Delays, "graphics.pokemon.animations.delay[delay.]data.pokemon.names-1");
         Create(FrameLists, "graphics.pokemon.animations.frames[data<>]data.pokemon.names");
      }

      private void Create(int address, string format) {
         ViewPort.Goto.Execute(address.ToString("X2"));
         ViewPort.Edit("^" + format + " ");
      }

      private void MakeEmerald() {
         foreach (var (c, i) in "BPEE".Select((c, i) => (c, i))) Data[0xAC + i] = (byte)c;
      }

      #region finding the layout

      [Fact]
      public void ARomWithoutTheTables_HasNoLayout() {
         Assert.Null(PokemonAnimationLayout.Find(Model));
         Assert.Null(PokemonAnimationLayout.Find(null));
      }

      [Fact]
      public void SeparateTables_AreRecognized() {
         CreateSeparateTables();
         Assert.IsType<SeparateTablesLayout>(PokemonAnimationLayout.Find(Model));
      }

      [Fact]
      public void SeparateTables_NeedTheMovementAndTheFrames() {
         WriteSpeciesData();
         CreateTextTable("data.pokemon.names", Names, "?", "A", "B", "C");
         Create(Pictures, "graphics.pokemon.sprites.front[sprite<`ucs4x8x16`>]data.pokemon.names");
         Create(Palettes, "graphics.pokemon.palettes.normal[palette<`ucp4`>]data.pokemon.names");
         Assert.Null(PokemonAnimationLayout.Find(Model));
      }

      [Fact]
      public void TheDelayTable_IsOptional() {
         CreateSeparateTables(withDelays: false);
         var layout = Assert.IsType<SeparateTablesLayout>(PokemonAnimationLayout.Find(Model));
         Assert.True(layout.TryReadInputs(Model, 2, out var inputs));
         Assert.Equal(0, inputs.Delay);
         Assert.Equal(12, inputs.AnimId);
      }

      [Fact]
      public void ThePokemonTableWithTheAnimationFields_IsTheSpeciesLayout() {
         ViewPort.Goto.Execute("20");
         ViewPort.Edit("^data.pokemon.stats[frontAnimId. frontAnimDelay. frontAnimFrames<> frontPic<`ucs4x8x16`> palette<`ucp4`>]3 ");
         Assert.IsType<SpeciesTableLayout>(PokemonAnimationLayout.Find(Model));
      }

      [Fact]
      public void ThePokemonTableWithoutTheAnimationFields_IsNotEnough() {
         ViewPort.Goto.Execute("20");
         ViewPort.Edit("^data.pokemon.stats[hp. attack.]3 ");
         Assert.Null(PokemonAnimationLayout.Find(Model));
      }

      #endregion

      #region reading

      [Fact]
      public void TheInputs_AreThoseOfTheSpecies_NotOfTheRowAtTheSameIndex() {
         CreateSeparateTables();
         var layout = PokemonAnimationLayout.Find(Model);
         for (int species = 1; species <= 3; species++) {
            Assert.True(layout.TryReadInputs(Model, species, out var inputs));
            Assert.Equal(10 + species, inputs.AnimId);       // the movement table starts at species 1
            Assert.Equal(2 * species, inputs.Delay);
            Assert.Equal(PairBase + species * 8, inputs.FramesAddress);
         }
      }

      [Fact]
      public void SpeciesWithoutARow_HaveNoInputs() {
         CreateSeparateTables();
         var layout = PokemonAnimationLayout.Find(Model);
         Assert.False(layout.TryReadInputs(Model, 4, out _));
         Assert.False(layout.TryReadInputs(Model, 100, out _));
         Assert.False(layout.TryReadInputs(Model, -1, out _));
         Assert.Null(layout.Read(Model, 100, null));
      }

      [Fact]
      public void ReadingASpecies_FindsThePicturesThePaletteAndTheCommands() {
         CreateSeparateTables();
         var source = PokemonAnimationLayout.Find(Model).Read(Model, 2, null);
         Assert.NotNull(source);
         Assert.NotEmpty(source.Images);
         Assert.Equal(12, source.AnimId);
         Assert.Equal(4, source.Delay);
         Assert.Equal(3, source.Frames.Length);
         Assert.Equal(10, source.Frames[0].Duration);
         Assert.Equal(FrameCommand.KindEnd, source.Frames[2].Kind);
         Assert.Equal(16, source.Palette.Length);
      }

      [Fact]
      public void ReadingASpecies_UsesThePaletteThatWasChosen() {
         CreateSeparateTables();
         var layout = PokemonAnimationLayout.Find(Model);
         var chosen = Enumerable.Range(0, 16).Select(i => (short)(0x100 + i)).ToArray();
         var source = layout.Read(Model, 1, chosen);
         Assert.Equal(PokemonAnimationSource.ToGbaPalette(chosen), source.Palette);
         Assert.NotEqual(source.Palette, layout.Read(Model, 1, null).Palette);
      }

      [Fact]
      public void ThePicturesAndPalette_AreNotMixedUpBetweenSpecies() {
         CreateSeparateTables();
         var layout = PokemonAnimationLayout.Find(Model);
         var one = layout.Read(Model, 1, null);
         var three = layout.Read(Model, 3, null);
         Assert.NotEqual(one.Images[0][0], three.Images[0][0]);
         Assert.NotEqual(one.Palette, three.Palette);
      }

      [Fact]
      public void TheTimelineEntryPoint_UsesTheLayout() {
         CreateSeparateTables();
         Assert.NotNull(PokemonAnimationSource.Read(Model, 1));
         Assert.Null(PokemonAnimationSource.Read(Model, 9));
      }

      [Fact]
      public void Emerald_NeverStartsTheSecondPicture_OfUnownSpindaCastformAndDeoxys() {
         CreateSeparateTables();
         var layout = (SeparateTablesLayout)PokemonAnimationLayout.Find(Model);
         Assert.True(layout.StartsSecondPicture(201)); // not an Emerald: the species numbers mean something else
         MakeEmerald();
         layout = (SeparateTablesLayout)PokemonAnimationLayout.Find(Model);
         foreach (var species in new[] { 201, 308, 385, 410 }) Assert.False(layout.StartsSecondPicture(species));
         foreach (var species in new[] { 1, 25, 151, 386 }) Assert.True(layout.StartsSecondPicture(species));
      }

      #endregion

      #region the table tool

      [Fact]
      public void ThePreview_BelongsToThePictureFieldOfTheFrontSpriteTable() {
         CreateSeparateTables();
         var pictures = Model.GetTable("graphics.pokemon.sprites.front");
         var palettes = Model.GetTable("graphics.pokemon.palettes.normal");
         var spriteField = pictures.ElementContent[0];
         Assert.True(PokemonAnimationPreviewViewModel.IsFramesField(Model, pictures, spriteField));
         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, palettes, palettes.ElementContent[0]));
         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, pictures, null));
         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, null, spriteField));
      }

      [Fact]
      public void WithoutTheOtherTables_TheFrontSpriteTable_GetsNoPreview() {
         WriteSpeciesData();
         CreateTextTable("data.pokemon.names", Names, "?", "A", "B", "C");
         Create(Pictures, "graphics.pokemon.sprites.front[sprite<`ucs4x8x16`>]data.pokemon.names");
         var pictures = Model.GetTable("graphics.pokemon.sprites.front");
         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, pictures, pictures.ElementContent[0]));
      }

      [Fact]
      public void TheTableTool_ShowsThePreview_OfTheSpeciesOfTheRow() {
         CreateSeparateTables();
         ViewPort.Goto.Execute("graphics.pokemon.sprites.front/2");
         var previews = ViewPort.Tools.TableTool.Children.OfType<PokemonAnimationPreviewViewModel>().ToList();
         Assert.Single(previews);
      }

      #endregion
   }
}
