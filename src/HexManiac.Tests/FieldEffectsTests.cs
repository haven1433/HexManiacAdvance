using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The field effects (grass, ripples, dust, ...) are one table of sprite templates: the pictures, their palette (found by tag) and a way to edit that palette come from the row.
   /// </summary>
   public class FieldEffectsTests : BaseViewModelTestClass {
      private const int Image = 0x00, Palette = 0x100, PaletteTable = 0x120, Oam = 0x130, ImageList = 0x140, Template = 0x150, TemplateTable = 0x170;
      private const string Format = "[data<[tileTag:|h paletteid:|h oam<> anims<> sprites<`osl|graphics.overworld.palettes2:id=`> affine<> callback<>]1>]1";

      public FieldEffectsTests() : this("graphics.overworld.fieldeffects") { }

      private readonly string tableName;

      protected FieldEffectsTests(string tableName) {
         this.tableName = tableName;
         for (int i = 0; i < 0x80; i++) Model[Image + i] = 0x11;      // 16x16 pixels, all of them colour 1
         Model.WriteMultiByteValue(Palette + 2, 2, Token, 0x001F); // colour 1 is red (HexManiac keeps colours as 5-5-5 with red on top: 7C00)
         ViewPort.Edit($"@{PaletteTable:X2} <{Palette:X3}> 04 10 00 00 @{PaletteTable:X2} ^graphics.overworld.palettes2[pal<`ucp4`> id:|h unused:]1 ");
         ViewPort.Edit($"@{Oam:X2} 00 00 00 40 "); // OAM: 16x16
         ViewPort.Edit($"@{ImageList:X2} <{Image:X3}> 80 00 00 00 ");
         ViewPort.Edit($"@{Template:X2} FF FF 04 10 <{Oam:X3}> 00 00 00 00 <{ImageList:X3}> 00 00 00 00 00 00 00 00 ");
         ViewPort.Edit($"@{TemplateTable:X2} <{Template:X3}> @{TemplateTable:X2} ^{tableName}{Format} ");
      }

      [Fact]
      public void Setup_FindsTheTable() {
         Assert.Equal(tableName, FieldEffects.FindTableName(Model));
         Assert.True(FieldEffects.IsSupported(Model));
         Assert.Equal(1, FieldEffects.Count(Model));
         Assert.IsAssignableFrom<OverworldSpriteListRun>(Model.GetNextRun(ImageList));
      }

      [Fact]
      public void Describe_ReadsSizePaletteAndTags() {
         var info = FieldEffects.Describe(Model, 0);

         Assert.True(info.HasPicture);
         Assert.Equal(16, info.Width);
         Assert.Equal(16, info.Height);
         Assert.Equal(1, info.FrameCount);
         Assert.Equal(0xFFFF, info.TileTag);
         Assert.Equal(0x1004, info.PaletteTag);
         Assert.Equal("1004", info.PaletteTagText);
         Assert.False(info.Palette.IsPreviewOnly);
         Assert.Equal("graphics.overworld.palettes2", info.Palette.TableName);
         Assert.Equal(0, info.Palette.Index);
         Assert.Equal(Palette, info.Palette.Address);
         Assert.Equal(0x7C00, info.Palette.Colors[1]);
         Assert.Equal("graphics.overworld.palettes2/0", info.Palette.GotoDestination);
      }

      [Fact]
      public void Describe_OutOfRange_IsNull() {
         Assert.Null(FieldEffects.Describe(Model, 1));
         Assert.Null(FieldEffects.Describe(Model, -1));
         Assert.Empty(FieldEffects.RenderFrames(Model, 7));
      }

      [Fact]
      public void FrameSize_ComesFromTheOamData() => Assert.Equal((16, 16), FieldEffects.FrameSize(Model, 0));

      [Fact]
      public void RenderFrames_UsesThePaletteTheTagNames() {
         var frames = FieldEffects.RenderFrames(Model, 0);

         var frame = Assert.Single(frames);
         Assert.Equal(16, frame.PixelWidth);
         Assert.Equal(16, frame.PixelHeight);
         Assert.Equal(0x7C00, frame.PixelData[0]);
         Assert.Equal(0x7C00, frame.PixelData[16 * 16 - 1]);
      }

      [Fact]
      public void RenderFrames_PaletteEdited_ShowsTheNewColour() {
         Model.WriteMultiByteValue(Palette + 2, 2, Token, 0x03E0);

         var frame = FieldEffects.RenderFrames(Model, 0).Single();

         Assert.Equal(0x03E0, frame.PixelData[0]);
      }

      [Fact]
      public void Describe_UnknownPaletteTag_HasNoPaletteOfItsOwn() {
         Model.WriteMultiByteValue(Template + 2, 2, Token, 0x1234);

         var info = FieldEffects.Describe(Model, 0);

         Assert.True(info.HasPicture);
         Assert.True(info.Palette == null || info.Palette.IsPreviewOnly);
         Assert.Single(FieldEffects.RenderFrames(Model, 0)); // still drawn, with some palette
      }

      [Fact]
      public void Describe_NoPaletteTag_IsFlaggedAsPreviewOnly() {
         Model.WriteMultiByteValue(Template + 2, 2, Token, 0xFFFF);

         var info = FieldEffects.Describe(Model, 0);

         Assert.Equal("none", info.PaletteTagText);
         Assert.True(info.Palette == null || info.Palette.IsPreviewOnly);
      }

      [Fact]
      public void EmbeddedElement_HasTheTagsToTypeIn_AndNoLinkToTheReflectionEditor() {
         var element = new FieldEffectElementViewModel(ViewPort, 0, embedded: true);

         Assert.True(element.IsEmbedded);
         Assert.Equal(new[] { "Tile tag", "Palette tag" }, element.Fields.Select(field => field.Label).ToArray());
         Assert.Equal(new[] { "FFFF", "1004" }, element.Fields.Select(field => field.Text).ToArray());
         Assert.False(element.OpenReflectionPalettes.CanExecute(null));
         Assert.Empty(new FieldEffectElementViewModel(ViewPort, 0).Fields); // the table tool has the real fields
      }

      [Fact]
      public void EmbeddedElement_TypingATag_WritesTheTemplate_AndDrawsTheEffectAgain() {
         var element = new FieldEffectElementViewModel(ViewPort, 0, embedded: true);
         Assert.Contains("1004", element.PaletteTitle);

         element.Fields[1].Text = "ffff";

         Assert.Equal(0xFFFF, Model.ReadMultiByteValue(Template + 2, 2));
         Assert.Equal("FFFF", element.Fields[1].Text);
         Assert.Contains("none", element.Info);

         element.Fields[0].Text = "0x2A";

         Assert.Equal(0x2A, Model.ReadMultiByteValue(Template, 2));
         Assert.Equal("002A", element.Fields[0].Text);
      }

      [Fact]
      public void EmbeddedElement_AnInvalidTag_IsPutBack() {
         var element = new FieldEffectElementViewModel(ViewPort, 0, embedded: true);

         element.Fields[1].Text = "xyz";
         element.Fields[1].Text = "12345";

         Assert.Equal(0x1004, Model.ReadMultiByteValue(Template + 2, 2));
         Assert.Equal("1004", element.Fields[1].Text);
      }

      [Fact]
      public void EmbeddedElement_SelectingAnEffect_StaysInThisView() {
         var element = new FieldEffectElementViewModel(ViewPort, 0, embedded: true);
         var before = ViewPort.SelectionStart;

         element.SelectCommand.Execute(element.Elements.Single());

         Assert.Equal(before, ViewPort.SelectionStart);
         Assert.Equal(0, element.CurrentIndex);
      }

      [Fact]
      public void EmbeddedElement_OpenTable_ShowsTheEffectInTheTableTool() {
         var element = new FieldEffectElementViewModel(ViewPort, 0, embedded: true);

         Assert.True(element.OpenTable.CanExecute(null));
         element.OpenTable.Execute(null);

         Assert.Equal(TemplateTable, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void EmbeddedElement_UpdateIfEdited_NoticesAChangeMadeElsewhere() {
         var element = new FieldEffectElementViewModel(ViewPort, 0, embedded: true);
         element.UpdateIfEdited();
         Model.WriteMultiByteValue(Palette + 2, 2, Token, 0x03E0);
         Model.WriteMultiByteValue(Template + 2, 2, Token, 0xFFFF);

         element.UpdateIfEdited();

         Assert.Equal("FFFF", element.Fields[1].Text);
         Assert.Contains("none", element.Info);
      }

      [Fact]
      public void TableTool_OnAFieldEffect_ShowsThePictureAndThePalette() {
         ViewPort.Goto.Execute($"{tableName}/0");

         var element = ViewPort.Tools.TableTool.Children.OfType<FieldEffectElementViewModel>().Single();

         Assert.Equal(0, element.CurrentIndex);
         Assert.True(element.HasFrames);
         Assert.Single(element.Frames);
         Assert.True(element.HasPalette);
         Assert.Contains("1004", element.PaletteTitle);
         Assert.Single(element.Elements);
      }

      [Fact]
      public void FieldEffectElement_OpenPalette_GoesToThePaletteTable() {
         ViewPort.Goto.Execute($"{tableName}/0");
         var element = ViewPort.Tools.TableTool.Children.OfType<FieldEffectElementViewModel>().Single();

         Assert.True(element.OpenPalette.CanExecute(null));
         element.OpenPalette.Execute(null);

         Assert.Equal(PaletteTable, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void FieldEffectElement_ListsEveryEffectWithoutDrawingThem() {
         var element = new FieldEffectElementViewModel(ViewPort, 0);

         Assert.Equal(0, FieldEffectRenderer.Get(Model).RenderedCount);
         var item = Assert.Single(element.Elements);
         Assert.True(item.Selected);
         Assert.Equal(0, FieldEffectRenderer.Get(Model).RenderedCount);
      }

      [Theory]
      [InlineData(0x0000, 0x0000, 8, 8)]
      [InlineData(0x0000, 0x4000, 16, 16)]
      [InlineData(0x0000, 0xC000, 64, 64)]
      [InlineData(0x4000, 0x0000, 16, 8)]
      [InlineData(0x4000, 0x4000, 32, 8)]
      [InlineData(0x4000, 0xC000, 64, 32)]
      [InlineData(0x8000, 0x0000, 8, 16)]
      [InlineData(0x8000, 0x4000, 8, 32)]
      [InlineData(0x8000, 0x8000, 16, 32)]
      [InlineData(0x8000, 0xC000, 32, 64)]
      public void OamSize_FollowsShapeAndSize(int attribute0, int attribute1, int width, int height) {
         Assert.True(OverworldSpriteListRun.TryGetOamSize(attribute0, attribute1, out var w, out var h));
         Assert.Equal((width, height), (w, h));
      }

      [Fact]
      public void OamSize_ShapeThreeDoesNotExist() => Assert.False(OverworldSpriteListRun.TryGetOamSize(0xC000, 0, out _, out _));
   }

   /// <summary>Unedited games and old saved .toml files call the table graphics.overworld.sprites2: everything works with that name too.</summary>
   public class FieldEffectsLegacyNameTests : FieldEffectsTests {
      public FieldEffectsLegacyNameTests() : base("graphics.overworld.sprites2") { }

      [Fact]
      public void LegacyName_IsRecognised() {
         Assert.Equal("graphics.overworld.sprites2", FieldEffects.FindTableName(Model));
         Assert.True(FieldEffects.IsFieldEffectTable("graphics.overworld.sprites2"));
         Assert.True(FieldEffects.IsFieldEffectTable("graphics.overworld.fieldeffects"));
         Assert.False(FieldEffects.IsFieldEffectTable("graphics.overworld.sprites"));
      }

      [Fact]
      public void LegacyName_TableToolShowsTheElement() {
         ViewPort.Goto.Execute("graphics.overworld.sprites2/0");

         Assert.Single(ViewPort.Tools.TableTool.Children.OfType<FieldEffectElementViewModel>());
      }
   }
}
