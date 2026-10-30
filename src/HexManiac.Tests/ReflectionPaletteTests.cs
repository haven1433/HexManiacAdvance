using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The Reflection Palettes editor: which palettes there are, which sprites use them, the colour rules of a game that computes its reflections, the previews and editing.</summary>
   public class ReflectionPaletteTests : BaseViewModelTestClass {
      private const int SpriteTable = 0x100, Info0 = 0x140, Info1 = 0x180, List0 = 0x200, List1 = 0x220, Sheet0 = 0x800, Sheet1 = 0x2000, FrameBytes = 0x100;
      private const int PaletteTable = 0x400, BrendanPal = 0x600, BrendanReflection = 0x640, NpcPal = 0x680, NpcReflection = 0x6C0;
      private const int BrendanTag = 0x1100, BrendanReflectionTag = 0x1101, NpcTag = 0x1103, NpcReflectionTag = 0x1107;
      private readonly List<string> tabErrors = new();

      public ReflectionPaletteTests() : base(0x10000) {
         SetFullModel(0);
         Model.SetList(new ModelDelta(), "objecteventgfx", "BRENDAN_NORMAL", "NINJA_BOY");
         var tags = new string[0x1108];
         for (int i = 0; i < tags.Length; i++) tags[i] = string.Empty;
         tags[BrendanTag] = "OBJ_EVENT_PAL_TAG_BRENDAN";
         tags[BrendanReflectionTag] = "OBJ_EVENT_PAL_TAG_BRENDAN_REFLECTION";
         tags[NpcTag] = "OBJ_EVENT_PAL_TAG_NPC_1";
         tags[NpcReflectionTag] = "OBJ_EVENT_PAL_TAG_NPC_1_REFLECTION";
         Model.SetList(new ModelDelta(), ObjectPaletteTags.ListName, tags);

         // the pictures: frame f of a sheet starts with the pixels f and f+1; the bottom left pixel of the first frame of the first sheet is 3
         for (int f = 0; f < OverworldSheetFrameSource.PlayerSheetFrames; f++) {
            Data[Sheet0 + FrameBytes * f] = (byte)((f % 16) | (((f + 1) % 16) << 4));
            Data[Sheet1 + FrameBytes * f] = (byte)((f % 16) | (((f + 1) % 16) << 4));
         }
         Data[Sheet0 + 0xDC] = 0x03; // tile 6 (the bottom left one), its last row: the pixel at x=0, y=31

         ViewPort.Edit($"@{SpriteTable:X} ^{HardcodeTablesModel.OverworldSprites}[data<[tileTag: paletteid:|h reflectionPaletteTag:|h length: width: height: info. tracks. unused: oam<> subsprites<> anims<> sprites<`osl`> affine<>]1>]objecteventgfx ");
         Model.WritePointer(Token, SpriteTable, Info0);
         Model.WritePointer(Token, SpriteTable + 4, Info1);
         WriteInfo(Info0, BrendanTag, 0, List0);
         WriteInfo(Info1, NpcTag, 2, List1);
         ViewPort.Edit($"@{List0:X} <{Sheet0:X6}> 00 01 01 00 ");
         ViewPort.Edit($"@{List1:X} <{Sheet1:X6}> 00 01 01 00 ");
         ViewPort.Edit($"@{Info0:X} ^sheet0[tileTag: paletteid:|h reflectionPaletteTag:|h length: width: height: info. tracks. unused: oam<> subsprites<> anims<> sprites<`osl`> affine<>]1 ");
         ViewPort.Edit($"@{Info1:X} ^sheet1[tileTag: paletteid:|h reflectionPaletteTag:|h length: width: height: info. tracks. unused: oam<> subsprites<> anims<> sprites<`osl`> affine<>]1 ");

         // the palette table: four palettes, 16 colours each (the colours of a palette are its tag's low byte and its colour number, so they are easy to tell apart)
         ViewPort.Edit($"@{PaletteTable:X} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h unused:]4 ");
         var palettes = new[] { (BrendanPal, BrendanTag), (BrendanReflection, BrendanReflectionTag), (NpcPal, NpcTag), (NpcReflection, NpcReflectionTag) };
         for (int i = 0; i < palettes.Length; i++) {
            Model.WritePointer(Token, PaletteTable + i * 8, palettes[i].Item1);
            Model.WriteMultiByteValue(PaletteTable + i * 8 + 4, 2, Token, palettes[i].Item2);
            ViewPort.Edit($"@{palettes[i].Item1:X} ^pal{i}`ucp4` ");
         }
         foreach (var (address, seed) in new[] { (BrendanPal, 1), (BrendanReflection, 2), (NpcPal, 3), (NpcReflection, 4) }) {
            for (int i = 0; i < 16; i++) Model.WriteMultiByteValue(address + i * 2, 2, Token, GbaColor.Pack(seed + i, i, 31 - i));
         }
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      private void WriteInfo(int info, int tag, int slot, int list) {
         Model.WriteMultiByteValue(info + 2, 2, Token, tag);
         Model.WriteMultiByteValue(info + 4, 2, Token, 0x11FF); // no second tag
         Model.WriteMultiByteValue(info + 6, 2, Token, 0x200); // length
         Model.WriteMultiByteValue(info + 8, 2, Token, 16); // width
         Model.WriteMultiByteValue(info + 10, 2, Token, 32); // height
         Token.ChangeData(Model, info + 12, (byte)slot);
         Model.WritePointer(Token, info + 28, list);
      }

      private ReflectionPaletteTab CreateTab() {
         var tab = new ReflectionPaletteTab(ViewPort);
         tab.OnError += (sender, e) => tabErrors.Add(e);
         return tab;
      }

      #region The catalog

      [Fact]
      public void Catalog_FindsTheStoredReflectionPalettes_AndTheSpritesThatUseThem() {
         Assert.True(ReflectionCatalog.IsSupported(Model));
         var catalog = ReflectionCatalog.Build(Model);

         Assert.Equal(ReflectionMode.Stored, catalog.Mode);
         Assert.Equal(new[] { BrendanReflectionTag, NpcReflectionTag }, catalog.Stored.Select(p => p.Tag).ToArray());
         Assert.All(catalog.Stored, palette => Assert.True(palette.IsStored));
         Assert.Equal("Brendan reflection", catalog.Stored[0].Name);
         Assert.Equal(BrendanReflection, catalog.Stored[0].Address);
         Assert.Equal(PaletteTable + 8 + 0, catalog.Stored[0].PointerAddress);
         Assert.Equal("BRENDAN_NORMAL", catalog.SpritesFor(catalog.Stored[0]).Single().Name);
         Assert.Equal("NINJA_BOY", catalog.SpritesFor(catalog.Stored[1]).Single().Name);
         Assert.Empty(catalog.Computed);
      }

      [Fact]
      public void Catalog_ReadsTheSlotAndPaletteOfEachSprite() {
         var catalog = ReflectionCatalog.Build(Model);

         Assert.Equal(2, catalog.Sprites.Count);
         Assert.Equal((BrendanTag, 0, BrendanReflectionTag), (catalog.Sprites[0].Tag, catalog.Sprites[0].Slot, catalog.Sprites[0].ReflectionTag));
         Assert.Equal((NpcTag, 2, NpcReflectionTag), (catalog.Sprites[1].Tag, catalog.Sprites[1].Slot, catalog.Sprites[1].ReflectionTag));
         Assert.Equal(-1, catalog.Sprites[0].BridgeTag);
      }

      [Fact]
      public void Catalog_AGameThatComputesItsReflections_ListsTheThreeRulesFirstAndDoesNotUseTheSecondTag() {
         Model.SetList(new ModelDelta(), ReflectionCatalog.ModeListName, "filtered");
         ViewPort.ChangeHistory.ChangeCompleted();
         var catalog = ReflectionCatalog.Build(Model);

         Assert.Equal(ReflectionMode.Computed, catalog.Mode);
         Assert.Equal(new[] { "water", "ice", "bridge" }, catalog.Computed.Select(p => p.Key).ToArray());
         Assert.All(catalog.Sprites, sprite => Assert.Equal(-1, sprite.BridgeTag));
         Assert.NotNull(catalog.Find("tag:1101"));
         Assert.Null(catalog.Find("tag:9999"));
      }

      [Fact]
      public void Catalog_WithoutTheTables_IsNotSupported() {
         var empty = new BaseViewModelTestClass(0x1000);
         Assert.False(ReflectionCatalog.IsSupported(empty.Model));
         Assert.False(ReflectionCatalog.IsSupported(null));
         Assert.False(ReflectionPaletteTab.IsSupported(empty.Model));
      }

      [Fact]
      public void Catalog_ReadsTheColoursOfAPalette() {
         var catalog = ReflectionCatalog.Build(Model);

         var colors = catalog.ColorsOf(catalog.Stored[0]);

         Assert.Equal(16, colors.Count);
         Assert.Equal(GbaColor.ToDisplay(GbaColor.Pack(2 + 5, 5, 31 - 5)), colors[5]);
         Assert.Equal(BrendanReflection, catalog.AddressOfTag(BrendanReflectionTag));
         Assert.Equal(-1, catalog.AddressOfTag(0x4321));
      }

      [Fact]
      public void ObjectPaletteTags_NameThemselvesFromTheMetadataList() {
         Assert.Equal("Brendan reflection", ObjectPaletteTags.Name(Model, BrendanReflectionTag));
         Assert.Equal("NPC 1", ObjectPaletteTags.Name(Model, NpcTag));
         Assert.Null(ObjectPaletteTags.Name(Model, 0x1105));
         Assert.True(ObjectPaletteTags.IsReflectionName("NPC 1 reflection"));
         Assert.False(ObjectPaletteTags.IsReflectionName("NPC 1"));
         Assert.Equal("Quinty Plump reflection", ObjectPaletteTags.Humanize("OBJ_EVENT_PAL_TAG_QUINTY_PLUMP_REFLECTION"));
      }

      #endregion

      #region The colour rules

      [Fact]
      public void Water_AddsTenBlue_AndKeepsTheFirstColour() {
         var palette = new ushort[] { GbaColor.Pack(1, 2, 3), GbaColor.Pack(4, 5, 6), GbaColor.Pack(7, 8, 25) };

         var result = ReflectionFilters.Water(palette);

         Assert.Equal(palette[0], result[0]);
         Assert.Equal(GbaColor.Pack(4, 5, 16), result[1]);
         Assert.Equal(GbaColor.Pack(7, 8, 31), result[2]); // never above 31
      }

      [Fact]
      public void Ice_TakesFiveRed_AddsThreeGreenAndSixteenBlue() {
         var palette = new ushort[] { GbaColor.Pack(1, 2, 3), GbaColor.Pack(4, 30, 6), GbaColor.Pack(20, 8, 10) };

         var result = ReflectionFilters.Ice(palette);

         Assert.Equal(palette[0], result[0]);
         Assert.Equal(GbaColor.Pack(0, 31, 22), result[1]); // red can't go below 0, green can't go above 31
         Assert.Equal(GbaColor.Pack(15, 11, 26), result[2]);
      }

      [Fact]
      public void Bridge_IsOneFlatColour() {
         var result = ReflectionFilters.Bridge();

         Assert.Equal(16, result.Length);
         Assert.All(result, color => Assert.Equal(0x55C9, color));
      }

      [Fact]
      public void Filter_WorksOnTheColoursAPictureUses() {
         var normal = new[] { GbaColor.ToDisplay(GbaColor.Pack(1, 1, 1)), GbaColor.ToDisplay(GbaColor.Pack(4, 5, 6)) };

         var water = ReflectionPreview.Filter(ReflectionPaletteKind.Water, normal);

         Assert.Equal(normal[0], water[0]);
         Assert.Equal(GbaColor.ToDisplay(GbaColor.Pack(4, 5, 16)), water[1]);
         Assert.Null(ReflectionPreview.Filter(ReflectionPaletteKind.Stored, normal));
         Assert.Equal(16, ReflectionPreview.Filter(ReflectionPaletteKind.Bridge, null).Count);
      }

      #endregion

      #region Pictures

      [Fact]
      public void FlipVertical_PutsTheLastRowFirst() {
         var pixels = new int[2, 3];
         pixels[0, 0] = 1; pixels[1, 2] = 7;

         var flipped = ReflectionPreview.FlipVertical(pixels);

         Assert.Equal(1, flipped[0, 2]);
         Assert.Equal(7, flipped[1, 0]);
      }

      [Fact]
      public void ReadFrames_GivesThePicturesOfASprite_AndNothingForASpriteThatIsNotThere() {
         var frames = ReflectionPreview.ReadFrames(Model, 0, ReflectionPreview.PlayerFrames);

         Assert.Equal(3, frames.Count);
         Assert.Equal(16, frames[0].GetLength(0));
         Assert.Equal(32, frames[0].GetLength(1));
         Assert.Equal(3, frames[0][0, 31]);
         Assert.Equal(3, frames[1][0, 0]); // the second picture shown is frame 3
         Assert.Empty(ReflectionPreview.ReadFrames(Model, 99, new[] { 0 }));
      }

      [Fact]
      public void TheReflectionPicture_IsTheNormalPictureUpsideDown() {
         var frames = ReflectionPreview.ReadFrames(Model, 0, new[] { 0 });
         var palette = Enumerable.Range(0, 16).Select(i => GbaColor.ToDisplay(GbaColor.Pack(i, 31 - i, 5))).ToArray();

         var normal = ReflectionPreview.DrawNormal(frames[0], palette);
         var reflection = ReflectionPreview.DrawReflection(frames[0], palette);

         Assert.Equal(normal.PixelWidth, reflection.PixelWidth);
         Assert.Equal(normal.PixelData[31 * 16], reflection.PixelData[0]);
         Assert.Equal(normal.PixelData[0], reflection.PixelData[31 * 16]);
         Assert.NotEqual(normal.PixelData[0], normal.PixelData[31 * 16]);
         Assert.Null(ReflectionPreview.DrawNormal(frames[0], null));
      }

      #endregion

      #region The tab

      [Fact]
      public void Tab_ListsThePalettes_PlayersFirst_AndSelectsTheFirst() {
         var tab = CreateTab();

         Assert.Equal(new[] { "tag:1101", "tag:1107" }, tab.Entries.Select(entry => entry.Key).ToArray());
         Assert.Equal("tag:1101", tab.SelectedKey);
         Assert.True(tab.Entries[0].Selected);
         Assert.False(tab.Entries[1].Selected);
         Assert.True(tab.IsEditable);
         Assert.False(tab.ComputesReflections);
         Assert.Equal("used by 1 sprite", tab.Entries[0].Status);
         Assert.Equal(16, tab.Entries[0].Colors.Count);
         Assert.Contains("Tag 1101", tab.Where);
         Assert.Contains(BrendanReflection.ToString("X6"), tab.Where);
      }

      [Fact]
      public void Tab_SelectingAPalette_ShowsItsColoursAndItsSprites() {
         var tab = CreateTab();

         tab.Entries[1].Select.Execute(null);

         Assert.Equal("NPC 1 reflection", tab.Title);
         Assert.Equal("tag:1107", tab.SelectedKey);
         Assert.False(tab.Entries[0].Selected);
         Assert.Equal(16, tab.Palette.Elements.Count);
         Assert.Equal(GbaColor.ToDisplay(GbaColor.Pack(4 + 3, 3, 31 - 3)), tab.Palette.Elements[3].Color);
         var sprite = Assert.Single(tab.Sprites);
         Assert.Equal("Ninja Boy", sprite.Name);
         var frame = Assert.Single(sprite.Frames);
         Assert.True(frame.Normal.HasImage);
         Assert.True(frame.Reflection.HasImage);
         Assert.False(frame.InGame.HasImage);
         Assert.False(tab.ShowsInGame);
      }

      [Fact]
      public void Tab_ThePlayerShowsThreePictures_AndEachSpriteItsReflection() {
         var tab = CreateTab();

         var sprite = Assert.Single(tab.Sprites);

         Assert.Equal("Brendan Normal", sprite.Name);
         Assert.Equal(new[] { "standing", "walking", "walking" }, sprite.Frames.Select(frame => frame.Label).ToArray());
         Assert.All(sprite.Frames, frame => Assert.True(frame.Reflection.HasImage));
      }

      [Fact]
      public void Tab_EditingAColour_ReachesTheRomRedrawsThePreviewAndUndoTakesItBack() {
         var tab = CreateTab();
         var before = tab.Palette.Elements[4].Color;
         var edited = GbaColor.Pack(5, 9, 28);
         var previewBefore = tab.Sprites[0].Frames[0].Reflection.PixelData.ToArray();

         tab.Palette.Elements[1].Color = GbaColor.ToDisplay(edited);
         tab.Palette.PushColorsToModel();

         Assert.Equal(edited, Model.ReadMultiByteValue(BrendanReflection + 2, 2));
         Assert.Equal(GbaColor.ToDisplay(edited), tab.Entries[0].Colors[1]);
         Assert.Contains("1=5:9:28", tab.ColorSummary);
         Assert.Equal(before, tab.Palette.Elements[4].Color);
         Assert.Empty(tabErrors);
         // the other palettes are not touched
         Assert.Equal(GbaColor.Pack(4 + 1, 1, 31 - 1), Model.ReadMultiByteValue(NpcReflection + 2, 2));

         tab.Undo.Execute(null);

         Assert.Equal(GbaColor.Pack(2 + 1, 1, 31 - 1), Model.ReadMultiByteValue(BrendanReflection + 2, 2));
         Assert.Equal(GbaColor.ToDisplay(GbaColor.Pack(2 + 1, 1, 31 - 1)), tab.Palette.Elements[1].Color);
      }

      [Fact]
      public void Tab_AGameThatComputesItsReflections_ShowsTheRulesFirst_AndCannotEditThem() {
         Model.SetList(new ModelDelta(), ReflectionCatalog.ModeListName, "filtered");
         ViewPort.ChangeHistory.ChangeCompleted();
         var tab = CreateTab();

         Assert.True(tab.ComputesReflections);
         Assert.Equal(new[] { "water", "ice", "bridge", "tag:1101", "tag:1107" }, tab.Entries.Select(entry => entry.Key).ToArray());
         Assert.Equal("water", tab.SelectedKey);
         Assert.False(tab.IsEditable);
         Assert.Contains("adds 10 to the blue", tab.Description);
         Assert.False(tab.HasWhere);
         Assert.Equal(2, tab.Sprites.Count);
         var water = tab.Sprites[0].Frames[0];
         Assert.True(water.Reflection.HasImage);
         Assert.False(water.InGame.HasImage);

         tab.Entries[3].Select.Execute(null);

         Assert.True(tab.IsEditable);
         Assert.True(tab.ShowsInGame);
         Assert.Contains("never loads it", tab.Description);
         Assert.True(tab.Sprites[0].Frames[0].InGame.HasImage);
         Assert.Equal("stored, but this game doesn't read it", tab.Entries[3].Status);
      }

      [Fact]
      public void OpeningTheEditorWithoutTheTables_SaysWhyAndOpensNothing() {
         var plain = new PokemonModel(new byte[0x200], singletons: Singletons);
         var viewPort = new ViewPort("plain.txt", plain, InstantDispatch.Instance, Singletons);
         var errors = new List<string>();
         var opened = new List<ITabContent>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.RequestTabChange += (sender, e) => opened.Add(e.NewTab);

         viewPort.OpenOverworldSpritesEditor(OverworldSecondarySection.Reflections);

         Assert.Empty(opened);
         Assert.Single(errors);
         Assert.Contains("overworld sprite table", errors[0]);
      }

      [Fact]
      public void TheEditorGotoPrefix_ShowsTheReflectionSectionNextToTheOverworldSprites() {
         var opened = new List<ITabContent>();
         ViewPort.RequestTabChange += (sender, e) => opened.Add(e.NewTab);

         ViewPort.OpenEditorTab("reflections");

         Assert.DoesNotContain(opened, tab => tab is ReflectionPaletteTab); // no tab of its own any more: it is part of the table tool of the sprites
         Assert.Equal(SpriteTable, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
         Assert.Same(ViewPort.Tools.TableTool, ViewPort.Tools.SelectedTool);
         var panel = Assert.Single(ViewPort.Tools.TableTool.Children.OfType<OverworldSecondaryElementViewModel>());
         Assert.True(panel.ShowReflections);
         Assert.NotNull(panel.ReflectionsEditor);
         Assert.Empty(Errors);
      }

      [Fact]
      public void TheEditorGotoPrefix_AnOldSecondaryDestinationIsUnknownNow() {
         ViewPort.OpenEditorTab("secondary");

         Assert.Single(Errors);
         Assert.Contains("Unknown editor", Errors[0]);
      }

      [Fact]
      public void TheQuickEditItem_IsOnlyAvailableWhereTheTablesAre() {
         var item = new HavenSoft.HexManiac.Core.ViewModels.QuickEditItems.OpenReflectionPalettes();
         var plain = new ViewPort("plain.txt", new PokemonModel(new byte[0x200], singletons: Singletons), InstantDispatch.Instance, Singletons);

         Assert.True(item.CanRun(ViewPort));
         Assert.False(item.CanRun(plain));
      }

      [Fact]
      public void Tab_OpenPaletteOfASprite_ShowsItsOwnPaletteInTheMainTab() {
         var tab = CreateTab();
         tab.Entries[1].Select.Execute(null);

         tab.Sprites[0].OpenPalette.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(PaletteTable + 2 * 8, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart)); // the pointer to the palette of the sprite: the third row of the palette table
      }

      #endregion

      #region The secondary overworld panel (field effects + reflection palettes) next to the overworld sprites

      private const int FxImage = 0x6000, FxPalette = 0x6100, FxPaletteTable = 0x6120, FxOam = 0x6130, FxImageList = 0x6140, FxTemplate = 0x6150, FxTemplateTable = 0x6170;

      // the same field effect table the FieldEffectsTests use, placed where this ROM has room
      private void AddFieldEffects() {
         for (int i = 0; i < 0x80; i++) Model[FxImage + i] = 0x11;
         Model.WriteMultiByteValue(FxPalette + 2, 2, Token, 0x001F);
         ViewPort.Edit($"@{FxPaletteTable:X} <{FxPalette:X}> 04 10 00 00 @{FxPaletteTable:X} ^graphics.overworld.palettes2[pal<`ucp4`> id:|h unused:]1 ");
         ViewPort.Edit($"@{FxOam:X} 00 00 00 40 ");
         ViewPort.Edit($"@{FxImageList:X} <{FxImage:X}> 80 00 00 00 ");
         ViewPort.Edit($"@{FxTemplate:X} FF FF 04 10 <{FxOam:X}> 00 00 00 00 <{FxImageList:X}> 00 00 00 00 00 00 00 00 ");
         ViewPort.Edit($"@{FxTemplateTable:X} <{FxTemplate:X}> @{FxTemplateTable:X} ^graphics.overworld.fieldeffects[data<[tileTag:|h paletteid:|h oam<> anims<> sprites<`osl|graphics.overworld.palettes2:id=`> affine<> callback<>]1>]1 ");
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      private List<GotoShortcutViewModel> MenuButtons() {
         var editor = New.EditorViewModel();
         editor.Add(ViewPort);
         return editor.GotoViewModel.Shortcuts.ToList();
      }

      private OverworldSecondaryElementViewModel Panel() => Assert.Single(ViewPort.Tools.TableTool.Children.OfType<OverworldSecondaryElementViewModel>());

      [Fact]
      public void MainMenu_HasNoOwSecondaryButtonAnyMore_AndNoButtonForTheTwoEditors() {
         AddFieldEffects();

         var texts = MenuButtons().Select(button => button.DisplayText.Replace("\n", " ")).ToList();

         Assert.DoesNotContain("OW Secondary", texts);
         Assert.DoesNotContain("Field Effects", texts);
         Assert.DoesNotContain("Reflection Palettes", texts);
         Assert.Contains("OW Sprites", texts); // the neighbours stay
      }

      [Fact]
      public void TheSpriteTable_HasTheSecondaryPanelInAColumnOfItsOwn() {
         AddFieldEffects();

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");

         var panel = Panel();
         Assert.True(panel.HasFieldEffects);
         Assert.True(panel.HasReflections);
         Assert.True(panel.HasBoth);
         Assert.Equal(3, ViewPort.Tools.TableTool.Groups.Count); // the sprite table, its fields (the sprite itself), and the panel
         Assert.Contains(panel, ViewPort.Tools.TableTool.Groups[2].Members);
      }

      [Fact]
      public void OtherTables_HaveNoSecondaryPanel() {
         AddFieldEffects();

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldPalettes}/0");

         Assert.Empty(ViewPort.Tools.TableTool.Children.OfType<OverworldSecondaryElementViewModel>());
      }

      [Fact]
      public void ThePanel_ShowsTheFieldEffectsFirst_AndTheSectionsAreCreatedWhenFirstShown() {
         AddFieldEffects();
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");
         var panel = Panel();

         Assert.True(panel.ShowFieldEffects);
         Assert.NotNull(panel.FieldEffectsEditor);
         Assert.True(panel.FieldEffectsEditor.IsEmbedded);
         Assert.Null(panel.ReflectionsEditor); // not built until it is shown

         panel.SelectReflections.Execute(null);

         Assert.True(panel.ShowReflections);
         Assert.False(panel.ShowFieldEffects);
         Assert.NotNull(panel.ReflectionsEditor);
         Assert.Null(panel.FieldEffectsEditor);
         Assert.Equal(2, panel.ReflectionsEditor.Entries.Count);
      }

      [Fact]
      public void ThePanel_KeepsItsSectionWhenAnotherSpriteIsPicked() {
         AddFieldEffects();
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");
         var first = Panel();
         first.SelectReflections.Execute(null);
         var editor = first.ReflectionsEditor;

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/1");

         var panel = Panel();
         Assert.True(panel.ShowReflections);
         Assert.Same(editor, panel.ReflectionsEditor); // the same editor, not a new one every time the cursor moves
         Assert.Equal(ViewPort.SecondarySection, OverworldSecondarySection.Reflections);
      }

      [Fact]
      public void ThePanel_WithoutFieldEffects_ShowsOnlyTheReflections() {
         ViewPort.SecondarySection = OverworldSecondarySection.FieldEffects; // the usual first section, but there is none

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");

         var panel = Panel();
         Assert.False(panel.HasFieldEffects);
         Assert.False(panel.HasBoth);
         Assert.True(panel.ShowReflections);
         Assert.NotNull(panel.ReflectionsEditor);
         Assert.Null(panel.FieldEffectsEditor);
         Assert.False(panel.SelectFieldEffects.CanExecute(null));
      }

      [Fact]
      public void TheGotoPrefix_FieldEffects_ShowsThatSection_AndFallsBackWithoutTheTable() {
         ViewPort.OpenEditorTab("fieldeffects"); // no field effects in this ROM: the reflections are shown instead

         Assert.True(Panel().ShowReflections);
         Assert.Empty(Errors);

         AddFieldEffects();
         ViewPort.OpenEditorTab("reflections");
         Assert.True(Panel().ShowReflections);
         ViewPort.OpenEditorTab("fieldeffects");

         Assert.True(Panel().ShowFieldEffects); // already in the sprite table: the same panel switches
         Assert.Empty(Errors);
      }

      [Fact]
      public void ThePanel_EditingAPalette_ReachesTheRom_AndTheTableToolRefreshDoesNotResetIt() {
         ViewPort.OpenEditorTab("reflections");
         var editor = Panel().ReflectionsEditor;
         var edited = GbaColor.Pack(5, 9, 28);
         editor.Palette.Elements[1].Color = GbaColor.ToDisplay(edited);

         editor.Palette.PushColorsToModel();
         ViewPort.Tools.TableTool.DataForCurrentRunChanged(); // the table tool refreshes after every edit

         Assert.Equal(edited, Model.ReadMultiByteValue(BrendanReflection + 2, 2));
         Assert.Same(editor, Panel().ReflectionsEditor);
         Assert.Equal(GbaColor.ToDisplay(edited), editor.Palette.Elements[1].Color);
         Assert.Empty(Errors);
      }

      [Fact]
      public void ThePanel_AChangeMadeElsewhere_IsReadAgain() {
         ViewPort.OpenEditorTab("reflections");
         var editor = Panel().ReflectionsEditor;
         var edited = GbaColor.Pack(7, 8, 9);

         Model.WriteMultiByteValue(BrendanReflection + 2, 2, Token, edited);
         ViewPort.ChangeHistory.ChangeCompleted();
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();

         Assert.Equal(GbaColor.ToDisplay(edited), editor.Entries[0].Colors[1]);
      }

      [Fact]
      public void TheToolPanel_GrowsOnceToMakeRoomForThePanel_AndStaysWhereThePersonPutsIt() {
         Assert.Equal(ViewPort.DefaultToolPanelWidth, ViewPort.ToolPanelWidth);

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");

         Assert.True(ViewPort.ToolPanelWidth >= OverworldSecondaryElementViewModel.WantedToolPanelWidth);
         ViewPort.ToolPanelWidth = 800; // the person made it narrower again
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/1");
         Assert.Equal(800, ViewPort.ToolPanelWidth);
      }

      [Fact]
      public void TheToolPanel_IsNotMadeWiderThanThePersonHasItAlready() {
         ViewPort.ToolPanelWidth = 1300;

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");

         Assert.Equal(1300, ViewPort.ToolPanelWidth);
      }

      [Fact]
      public void FieldEffectsTable_HasAWayToTheReflectionSection() {
         AddFieldEffects();
         ViewPort.Goto.Execute("graphics.overworld.fieldeffects/0");
         var element = ViewPort.Tools.TableTool.Children.OfType<FieldEffectElementViewModel>().Single();

         Assert.True(element.OpenReflectionPalettes.CanExecute(null));
         element.OpenReflectionPalettes.Execute(null);

         Assert.Equal(SpriteTable, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
         Assert.True(Panel().ShowReflections);
      }

      [Fact]
      public void ThePanel_EmbeddedFieldEffectsEditor_CanBeUsedInTheSpriteTable() {
         AddFieldEffects();
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/0");
         var editor = Panel().FieldEffectsEditor;

         editor.Fields[1].Text = "1234";

         Assert.Equal(0x1234, Model.ReadMultiByteValue(FxTemplate + 2, 2));
         Assert.Equal(SpriteTable, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart)); // still looking at the sprites
         Assert.Empty(Errors);
      }

      [Fact]
      public void OpenOverworldSpritesEditor_WithNeitherTable_SaysWhyOnTheGotoPrefix() {
         var plain = new ViewPort("plain.txt", new PokemonModel(new byte[0x200], singletons: Singletons), InstantDispatch.Instance, Singletons);
         var errors = new List<string>();
         plain.OnError += (sender, e) => errors.Add(e);

         plain.OpenEditorTab("reflections");

         Assert.False(OverworldSecondaryElementViewModel.IsSupported(plain.Model));
         Assert.Single(errors);
      }

      [Fact]
      public void TheQuickEditItem_ShowsTheReflectionSection() {
         var item = new HavenSoft.HexManiac.Core.ViewModels.QuickEditItems.OpenReflectionPalettes();

         item.Run(ViewPort).Wait();

         Assert.True(Panel().ShowReflections);
      }

      #endregion
   }
}
