using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// A small ROM with the character customization tables the way the CUBE metadata describes them (anchors by name, never by address in the product code):
   /// 4 skin tones (row 0 = Original), 3 clothes colours, 3 secondary colours (the red parts), 6 roles, a counter byte for each, and pointers to the tables like the game's code has.
   /// The skin table is followed directly by the clothes table, so it can't grow in place; the clothes table and the secondary table have free space behind them.
   /// </summary>
   public abstract class CharacterCustomizationTestBase : BaseViewModelTestClass {
      protected const int SkinTable = 0x100, ClothesTable = 0x160, RoleTable = 0x200, SecondaryTable = 0x300;
      protected const int SkinCount = 0x250, ClothesCount = 0x251, RoleCount = 0x252, SecondaryCount = 0x253;
      protected const int PoolSkin = 0x260, PoolClothes = 0x264, PoolRoles = 0x268, PoolSecondary = 0x26C;
      protected const int RowLength = 24;
      private const string ColorRowFormat = "[name\"\"16 main:|c shadow:|c highlight:|c unused:]";
      private const string RoleFormat = "[gender.charcustomgenders context.charcustomcontexts skinMain. skinShadow. skinHighlight. skinOutline. clothesMain. clothesShadow. clothesHighlight. secondaryMain. secondaryShadow. secondaryHighlight.]";

      protected static ushort Gba(int red, int green, int blue) => GbaColor.Pack(red, green, blue);

      protected CharacterCustomizationTestBase() : base(0x10000) {
         SetFullModel(0xFF);
         Model.SetList(new ModelDelta(), "charcustomgenders", "Male (Brendan)", "Female (May)");
         Model.SetList(new ModelDelta(), "charcustomcontexts", "Overworld", "Trainer pic", "Reflection");

         ViewPort.Edit($"@{SkinTable:X} ^{CharacterCustomization.SkinTonesAnchor}{ColorRowFormat}4 ");
         ViewPort.Edit($"@{ClothesTable:X} ^{CharacterCustomization.ClothesAnchor}{ColorRowFormat}3 ");
         ViewPort.Edit($"@{SecondaryTable:X} ^{CharacterCustomization.SecondaryAnchor}{ColorRowFormat}3 ");
         ViewPort.Edit($"@{RoleTable:X} ^{CharacterCustomization.RolesAnchor}{RoleFormat}6 ");
         ViewPort.Edit($"@{SkinCount:X} ^{CharacterCustomization.SkinToneCountAnchor}[count.]1 ");
         ViewPort.Edit($"@{ClothesCount:X} ^{CharacterCustomization.ClothesCountAnchor}[count.]1 ");
         ViewPort.Edit($"@{SecondaryCount:X} ^{CharacterCustomization.SecondaryCountAnchor}[count.]1 ");
         ViewPort.Edit($"@{RoleCount:X} ^{CharacterCustomization.RoleCountAnchor}[count.]1 ");

         WriteRow(SkinTable, 0, "Original");
         WriteRow(SkinTable, 1, "Fair", Gba(31, 28, 25), Gba(26, 22, 19), Gba(31, 31, 29));
         WriteRow(SkinTable, 2, "Tan", Gba(26, 20, 14), Gba(21, 15, 10), Gba(29, 24, 18));
         WriteRow(SkinTable, 3, "Sky Blue", Gba(4, 20, 31), Gba(2, 14, 26), Gba(12, 26, 31));
         WriteRow(ClothesTable, 0, "Original");
         WriteRow(ClothesTable, 1, "Red", Gba(31, 4, 4), Gba(24, 2, 2), Gba(31, 12, 12));
         WriteRow(ClothesTable, 2, "Blue", Gba(4, 8, 31), Gba(2, 4, 24), Gba(12, 16, 31));
         WriteRow(SecondaryTable, 0, "Original", Gba(31, 12, 11), Gba(24, 8, 8), Gba(31, 12, 11));
         WriteRow(SecondaryTable, 1, "Gold", Gba(31, 22, 5), Gba(24, 16, 3), Gba(31, 22, 5));
         WriteRow(SecondaryTable, 2, "Navy", Gba(4, 6, 16), Gba(3, 4, 12), Gba(4, 6, 16));
         Data[SkinCount] = 4;
         Data[ClothesCount] = 3;
         Data[SecondaryCount] = 3;
         Data[RoleCount] = 6;

         // gender, context, skin main/shadow/highlight/outline, clothes main/shadow/highlight, secondary main/shadow/highlight (FF = not painted)
         WriteRole(0, 0, 0, new[] { 2, 3, 1, 4 }, new[] { 10, 11, 0xFF }, new[] { 12, 13, 0xFF });
         WriteRole(1, 1, 0, new[] { 2, 3, 1, 4 }, new[] { 10, 11, 0xFF }, new[] { 12, 13, 0xFF });
         WriteRole(2, 0, 1, new[] { 2, 3, 1, 4 }, new[] { 10, 11, 0xFF }, new[] { 12, 13, 0xFF });
         WriteRole(3, 1, 1, new[] { 2, 3, 1, 4 }, new[] { 10, 11, 0xFF }, new[] { 12, 13, 0xFF });
         WriteRole(4, 0, 2, new[] { 5, 6, 7, 0xFF }, new[] { 0xFF, 0xFF, 0xFF });
         WriteRole(5, 1, 2, new[] { 5, 6, 7, 0xFF }, new[] { 0xFF, 0xFF, 0xFF });

         AddPointer(PoolSkin, SkinTable);
         AddPointer(PoolClothes, ClothesTable);
         AddPointer(PoolRoles, RoleTable);
         AddPointer(PoolSecondary, SecondaryTable);
         ViewPort.ChangeHistory.ChangeCompleted(); // setting up is not something undo should take back
      }

      protected void WriteRow(int table, int index, string name, params ushort[] colors) {
         var address = table + RowLength * index;
         var bytes = Model.TextConverter.Convert(name, out _);
         for (int i = 0; i < 16; i++) Data[address + i] = i < bytes.Count ? bytes[i] : (byte)0xFF;
         for (int i = 0; i < 3; i++) Model.WriteMultiByteValue(address + 16 + 2 * i, 2, Token, i < colors.Length ? colors[i] : 0);
         Model.WriteMultiByteValue(address + 22, 2, Token, 0);
      }

      protected void WriteRole(int index, int gender, int context, int[] skin, int[] clothes, int[] secondary = null) {
         var address = RoleTable + 12 * index;
         Data[address] = (byte)gender;
         Data[address + 1] = (byte)context;
         for (int i = 0; i < 4; i++) Data[address + 2 + i] = (byte)skin[i];
         for (int i = 0; i < 3; i++) Data[address + 6 + i] = (byte)clothes[i];
         for (int i = 0; i < 3; i++) Data[address + 9 + i] = secondary == null ? (byte)0xFF : (byte)secondary[i];
      }

      protected CharacterCustomization Create() => new CharacterCustomization(Model, () => ViewPort.CurrentChange);

      protected void Done() => ViewPort.ChangeHistory.ChangeCompleted();

      protected void Undo() => ViewPort.Undo.Execute(null);

      protected void Redo() => ViewPort.Redo.Execute(null);

      protected int Word(int address) => Model.ReadMultiByteValue(address, 4);
   }

   public class CharacterCustomizationTests : CharacterCustomizationTestBase {
      #region Support and reading

      [Fact]
      public void ModelWithoutTheAnchors_IsNotSupported() {
         var plain = new PokemonModel(new byte[0x200], singletons: Singletons);

         Assert.False(CharacterCustomization.IsSupported(plain));
         Assert.True(CharacterCustomization.IsSupported(Model));
      }

      [Fact]
      public void MissingCounterAnchor_IsNotSupported() {
         var model = new PokemonModel(new byte[0x400], singletons: Singletons);
         var viewPort = new ViewPort("other.txt", model, InstantDispatch.Instance, Singletons);
         viewPort.Edit($"@100 ^{CharacterCustomization.SkinTonesAnchor}[name\"\"16 main:|c shadow:|c highlight:|c unused:]2 ");
         viewPort.Edit($"@200 ^{CharacterCustomization.ClothesAnchor}[name\"\"16 main:|c shadow:|c highlight:|c unused:]2 ");
         viewPort.Edit($"@300 ^{CharacterCustomization.RolesAnchor}[gender. context. a. b. c. d. e. f. g. h. i. j.]1 ");

         Assert.False(CharacterCustomization.IsSupported(model));
      }

      [Fact]
      public void ReadRows_ReadsNamesAndGbaColors() {
         var skin = Create().SkinTones;

         var rows = skin.ReadRows();

         Assert.Equal(4, rows.Count);
         Assert.Equal(new[] { "Original", "Fair", "Tan", "Sky Blue" }, rows.Select(row => row.Name));
         Assert.Equal(new[] { 0, 1, 2, 3 }, rows.Select(row => row.Index));
         Assert.Equal(new[] { Gba(31, 28, 25), Gba(26, 22, 19), Gba(31, 31, 29) }, rows[1].Colors);
         Assert.Equal("4:20:31", GbaColor.Describe(rows[3].Colors[0]));
         Assert.True(skin.IsHealthy);
         Assert.Equal(4, skin.StoredCount);
         Assert.Equal(3, Create().Clothes.ReadRows().Count);
      }

      [Fact]
      public void ReadRoles_ReadsTheSlots() {
         var roles = Create().ReadRoles();

         Assert.Equal(6, roles.Count);
         var maleOverworld = Create().FindRole(CharacterRole.Male, CharacterRole.OverworldContext);
         Assert.Equal(new[] { 2, 3, 1, 4 }, maleOverworld.Skin);
         Assert.Equal(new[] { 10, 11, -1 }, maleOverworld.Clothes);
         var femaleReflection = Create().FindRole(CharacterRole.Female, CharacterRole.ReflectionContext);
         Assert.Equal(new[] { 5, 6, 7, -1 }, femaleReflection.Skin);
         Assert.Equal(new[] { -1, -1, -1 }, femaleReflection.Clothes);
         Assert.Equal(new[] { 12, 13, -1 }, maleOverworld.Secondary);
         Assert.Equal(new[] { -1, -1, -1 }, femaleReflection.Secondary);
         Assert.Equal("palette slots 1-4 are painted with the skin tone, slots 10-11 with the clothes colour, slots 12-13 with the secondary colour", maleOverworld.Describe());
         Assert.Equal("Skin: main slot 2, shadow slot 3, highlight slot 1, outline slot 4 (made from the shadow). Clothes: main slot 10, shadow slot 11. Secondary: main slot 12, shadow slot 13.", maleOverworld.DescribeSlots());
         Assert.Equal("palette slots 5-7 are painted with the skin tone", femaleReflection.Describe());
      }

      [Fact]
      public void RoleCountOfZero_ListsNoRoles() {
         Data[RoleCount] = 0;

         Assert.Empty(Create().ReadRoles());
      }

      #endregion

      #region Colours

      [Fact]
      public void GbaColor_PacksFiveBitsPerChannelAndSwapsRedAndBlueForDrawing() {
         var color = GbaColor.Pack(31, 10, 2);

         Assert.Equal(31 | (10 << 5) | (2 << 10), color);
         Assert.Equal((31, 10, 2), GbaColor.Unpack(color));
         Assert.Equal("31:10:2", GbaColor.Describe(color));
         Assert.Equal((short)((31 << 10) | (10 << 5) | 2), GbaColor.ToDisplay(color));
         Assert.Equal(color, GbaColor.FromDisplay(GbaColor.ToDisplay(color)));
         Assert.Equal(GbaColor.Pack(31, 31, 0), GbaColor.Pack(99, 40, -5)); // out of range channels are limited
      }

      [Fact]
      public void DeriveShades_MakesTheShadowDarkerAndTheHighlightLighter_InWholeGbaSteps() {
         var main = Gba(20, 16, 10);

         var (shadow, highlight) = GbaColor.DeriveShades(main);

         var (sr, sg, sb) = GbaColor.Unpack(shadow);
         var (hr, hg, hb) = GbaColor.Unpack(highlight);
         Assert.True(sr < 20 && sg < 16 && sb < 10);
         Assert.True(hr > 20 && hg > 16 && hb > 10);
         Assert.True(hr <= 31 && hg <= 31 && hb <= 31);
      }

      [Fact]
      public void SetColor_WritesTheGbaColorToTheRow_AndUndoTakesItBack() {
         var skin = Create().SkinTones;
         var before = skin.ReadRow(2).Colors[1];

         var result = skin.SetColor(2, 1, Gba(31, 0, 4));
         Done();

         Assert.True(result.Success);
         Assert.Equal(Gba(31, 0, 4), skin.ReadRow(2).Colors[1]);
         Assert.Equal(Gba(31, 0, 4), Model.ReadMultiByteValue(SkinTable + RowLength * 2 + 18, 2));
         Assert.Equal(31 | (4 << 10), Model[SkinTable + RowLength * 2 + 18] | (Model[SkinTable + RowLength * 2 + 19] << 8)); // red in the low bits: how the GBA stores it
         Undo();
         Assert.Equal(before, skin.ReadRow(2).Colors[1]);
      }

      [Fact]
      public void SetColors_ChangesAllThree_InOneUndoStep() {
         var skin = Create().SkinTones;

         skin.SetColors(1, new[] { Gba(1, 2, 3), Gba(4, 5, 6), Gba(7, 8, 9) });
         Done();
         Assert.Equal(new[] { Gba(1, 2, 3), Gba(4, 5, 6), Gba(7, 8, 9) }, skin.ReadRow(1).Colors);

         Undo();
         Assert.Equal(new[] { Gba(31, 28, 25), Gba(26, 22, 19), Gba(31, 31, 29) }, skin.ReadRow(1).Colors);
      }

      [Fact]
      public void SetColor_RowZeroIsTheOriginalLook_AndCannotBeEdited() {
         var skin = Create().SkinTones;

         var result = skin.SetColor(0, 0, Gba(1, 1, 1));

         Assert.False(result.Success);
         Assert.Contains("Row 0", result.Message);
         Assert.Equal(0, skin.ReadRow(0).Colors[0]);
      }

      [Fact]
      public void DeriveShades_FromTheMainColourOfARow() {
         var skin = Create().SkinTones;
         var main = skin.ReadRow(2).Colors[0];

         var result = skin.DeriveShades(2);

         var expected = GbaColor.DeriveShades(main);
         Assert.True(result.Success);
         Assert.Equal(main, skin.ReadRow(2).Colors[0]);
         Assert.Equal(expected.shadow, skin.ReadRow(2).Colors[1]);
         Assert.Equal(expected.highlight, skin.ReadRow(2).Colors[2]);
      }

      [Fact]
      public void TableColorFields_ShowTheColourTheGbaShows_AndWriteTheGbaOrder() {
         var run = (ITableRun)Model.GetNextRun(SkinTable);
         var segment = run.ElementContent.OfType<ArrayRunColorSegment>().First();
         var address = SkinTable + RowLength + 16; // 'Fair' main: 31:28:25

         Assert.Equal("31:28:25", segment.ToText(Model, address, 0));
         var field = new ColorFieldArrayElementViewModel(ViewPort, "main", address);
         Assert.Equal("31:28:25", field.Content);
         Assert.Equal(GbaColor.ToDisplay(Gba(31, 28, 25)), field.Color);

         field.Content = "31:0:4";
         Done();

         Assert.Equal(Gba(31, 0, 4), Model.ReadMultiByteValue(address, 2));
         Assert.Equal("31:0:4", segment.ToText(Model, address, 0));

         field.Color = GbaColor.ToDisplay(Gba(2, 20, 30));
         Done();

         Assert.Equal(Gba(2, 20, 30), Model.ReadMultiByteValue(address, 2));
      }

      #endregion

      #region Applying a role

      private static IReadOnlyList<short> TestPalette() => Enumerable.Range(0, 16).Select(i => (short)(100 * (i + 1))).ToArray();

      [Fact]
      public void Apply_PaintsTheSkinAndClothesSlots_AndLeavesTheOtherSlotsAlone() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Male, CharacterRole.OverworldContext);
         var skin = customization.SkinTones.ReadRow(2);
         var clothes = customization.Clothes.ReadRow(1);
         var palette = TestPalette();

         var painted = role.Apply(palette, skin, clothes);

         Assert.Equal(GbaColor.ToDisplay(skin.Colors[0]), painted[2]);
         Assert.Equal(GbaColor.ToDisplay(skin.Colors[1]), painted[3]);
         Assert.Equal(GbaColor.ToDisplay(skin.Colors[2]), painted[1]);
         Assert.Equal(GbaColor.ToDisplay(CharacterRole.OutlineFromShadow(skin.Colors[1])), painted[4]);
         Assert.Equal(GbaColor.ToDisplay(clothes.Colors[0]), painted[10]);
         Assert.Equal(GbaColor.ToDisplay(clothes.Colors[1]), painted[11]);
         foreach (var untouched in new[] { 0, 5, 6, 7, 8, 9, 12, 13, 14, 15 }) Assert.Equal(palette[untouched], painted[untouched]);
         Assert.Equal(TestPalette(), palette); // the palette that was passed in is not changed
      }

      [Fact]
      public void Apply_RowZeroOfBothTables_LeavesThePaletteExactlyAsItWas() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Female, CharacterRole.TrainerPicContext);

         var painted = role.Apply(TestPalette(), customization.SkinTones.ReadRow(0), customization.Clothes.ReadRow(0));

         Assert.Equal(TestPalette(), painted);
      }

      [Fact]
      public void Apply_RowZeroOfOnlyOneTable_StillPaintsTheOther() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Male, CharacterRole.OverworldContext);
         var clothes = customization.Clothes.ReadRow(2);

         var painted = role.Apply(TestPalette(), customization.SkinTones.ReadRow(0), clothes);

         Assert.Equal(TestPalette()[2], painted[2]);
         Assert.Equal(GbaColor.ToDisplay(clothes.Colors[0]), painted[10]);
      }

      [Fact]
      public void OutlineFromShadow_IsNineSixteenthsOfEveryChannel() {
         Assert.Equal("17:11:4", GbaColor.Describe(CharacterRole.OutlineFromShadow(Gba(31, 20, 8))));
         Assert.Equal("0:0:0", GbaColor.Describe(CharacterRole.OutlineFromShadow(Gba(0, 0, 1))));
      }

      [Fact]
      public void FormatSlots_JoinsRuns() {
         Assert.Equal("1-4, 10", CharacterRole.FormatSlots(new[] { 4, 1, 3, 2, 10, -1 }));
         Assert.Equal("3", CharacterRole.FormatSlots(new[] { 3, 3 }));
         Assert.Equal(string.Empty, CharacterRole.FormatSlots(new[] { -1, -1 }));
      }

      #endregion

      #region Names

      [Fact]
      public void SetName_WritesGameTextPaddedWithTerminators_AndUndoTakesItBack() {
         var skin = Create().SkinTones;

         var result = skin.SetName(2, "Sunburnt");
         Done();

         Assert.True(result.Success);
         Assert.Equal("Sunburnt", skin.ReadRow(2).Name);
         var address = SkinTable + RowLength * 2;
         Assert.Equal(0xFF, Model[address + 8]); // the terminator
         Assert.Equal(0xFF, Model[address + 15]); // and the padding
         Undo();
         Assert.Equal("Tan", skin.ReadRow(2).Name);
      }

      [Fact]
      public void SetName_AcceptsFifteenCharacters_AndRefusesSixteen() {
         var skin = Create().SkinTones;

         Assert.True(skin.SetName(1, "123456789012345").Success);
         var tooLong = skin.SetName(1, "1234567890123456");

         Assert.False(tooLong.Success);
         Assert.Contains("15", tooLong.Message);
         Assert.Equal("123456789012345", skin.ReadRow(1).Name);
         Assert.Equal(0xFF, Model[SkinTable + RowLength + 15]); // the terminator is still there: the next field is not touched
         Assert.Equal(Gba(31, 28, 25), skin.ReadRow(1).Colors[0]);
      }

      [Fact]
      public void SetName_RefusesEmptyNamesAndTheOriginalRow() {
         var skin = Create().SkinTones;

         Assert.False(skin.SetName(1, "   ").Success);
         Assert.False(skin.SetName(0, "Mine").Success);
         Assert.False(skin.SetName(9, "Nowhere").Success);
         Assert.Equal("Fair", skin.ReadRow(1).Name);
         Assert.Equal("Original", skin.ReadRow(0).Name);
      }

      [Fact]
      public void UniqueName_AddsANumberAndStaysInsideFifteenCharacters() {
         var skin = Create().SkinTones;

         Assert.Equal("Fair 2", skin.UniqueName("Fair"));
         Assert.Equal("Brand New", skin.UniqueName("Brand New"));
         Assert.Equal("New skin tone", skin.UniqueName(""));
         var longName = skin.UniqueName("A name that is far too long");
         Assert.True(longName.Length <= CharacterColorTable.MaxNameLength);
      }

      #endregion

      #region Adding, duplicating, removing, moving

      [Fact]
      public void Add_ToATableWithRoomBehindIt_GrowsInPlaceAndRaisesTheCounter() {
         var clothes = Create().Clothes;

         var result = clothes.Add("Green", new[] { Gba(4, 28, 6), Gba(2, 20, 4), Gba(12, 31, 14) });
         Done();

         Assert.True(result.Success);
         Assert.Equal(3, result.Index);
         Assert.Equal(ClothesTable, clothes.Run.Start);
         Assert.Equal(4, clothes.Run.ElementCount);
         Assert.Equal(4, Model[ClothesCount]);
         Assert.Equal("Green", clothes.ReadRow(3).Name);
         Assert.Equal(Gba(4, 28, 6), clothes.ReadRow(3).Colors[0]);
         Assert.True(clothes.IsHealthy);
         Assert.Equal(ClothesTable, Model.ReadPointer(PoolClothes));
      }

      [Fact]
      public void Add_ToATableThatCannotGrowInPlace_MovesItAndUpdatesEveryReference() {
         var skin = Create().SkinTones;
         var skinBefore = skin.ReadRows().Select(row => (row.Name, row.Colors.ToArray())).ToList();

         var result = skin.Add("Mint", new[] { Gba(10, 28, 18), Gba(6, 22, 12), Gba(16, 31, 24) });
         Done();

         Assert.True(result.Success);
         Assert.Contains("moved to free space", result.Message);
         Assert.NotEqual(SkinTable, skin.Run.Start);
         Assert.Equal(5, skin.Run.ElementCount);
         Assert.Equal(skin.Run.Start, Model.ReadPointer(PoolSkin)); // the game's reference follows the table
         Assert.Equal(skin.Run.Start, skin.TableAddress);
         Assert.Equal(5, Model[SkinCount]);
         for (int i = 0; i < skinBefore.Count; i++) {
            Assert.Equal(skinBefore[i].Name, skin.ReadRow(i).Name);
            Assert.Equal(skinBefore[i].Item2, skin.ReadRow(i).Colors);
         }
         Assert.Equal("Mint", skin.ReadRow(4).Name);
         Assert.Equal(ClothesTable, Create().Clothes.Run.Start); // the neighbour did not move
      }

      [Fact]
      public void Add_ThatMovedTheTable_IsOneUndoStepAndRedoRepeatsIt() {
         var skin = Create().SkinTones;

         skin.Add("Mint", new[] { Gba(10, 28, 18), Gba(6, 22, 12), Gba(16, 31, 24) });
         Done();
         var moved = skin.Run.Start;
         Undo();

         Assert.Equal(SkinTable, skin.Run.Start);
         Assert.Equal(4, skin.Run.ElementCount);
         Assert.Equal(SkinTable, Model.ReadPointer(PoolSkin));
         Assert.Equal(4, Model[SkinCount]);
         Assert.Equal(4, skin.ReadRows().Count);

         Redo();

         Assert.Equal(moved, skin.Run.Start);
         Assert.Equal(5, skin.Run.ElementCount);
         Assert.Equal(moved, Model.ReadPointer(PoolSkin));
         Assert.Equal(5, Model[SkinCount]);
         Assert.Equal("Mint", skin.ReadRow(4).Name);
      }

      [Fact]
      public void Add_RefusesABadName_AndChangesNothing() {
         var skin = Create().SkinTones;

         var result = skin.Add("This name is much too long", new[] { Gba(1, 1, 1), Gba(1, 1, 1), Gba(1, 1, 1) });

         Assert.False(result.Success);
         Assert.Equal(4, skin.Run.ElementCount);
         Assert.Equal(4, Model[SkinCount]);
         Assert.Equal(SkinTable, skin.Run.Start);
      }

      [Fact]
      public void Add_StopsAtTheLimitOfTheCounterByte() {
         var skin = Create().SkinTones;
         int guard = 0;
         while (skin.StoredCount < CharacterColorTable.MaxRows && guard++ < 300) {
            var result = skin.Add($"Tone {skin.StoredCount}", new[] { Gba(1, 2, 3), Gba(1, 2, 3), Gba(1, 2, 3) });
            Assert.True(result.Success, result.Message);
         }
         Done();

         Assert.Equal(255, skin.StoredCount);
         Assert.Equal(255, skin.Run.ElementCount);
         Assert.True(skin.IsHealthy);
         var tooMany = skin.Add("One too many", new[] { Gba(1, 2, 3), Gba(1, 2, 3), Gba(1, 2, 3) });
         Assert.False(tooMany.Success);
         Assert.Contains("255", tooMany.Message);
         Assert.Equal(255, Model[SkinCount]);
         Assert.Equal(skin.Run.Start, Model.ReadPointer(PoolSkin));
         Assert.Equal("Tone 254", skin.ReadRow(254).Name);
      }

      [Fact]
      public void Duplicate_CopiesTheColoursWithAUniqueName() {
         var clothes = Create().Clothes;

         var result = clothes.Duplicate(2);
         Done();

         Assert.True(result.Success);
         Assert.Equal(3, result.Index);
         Assert.Equal("Blue 2", clothes.ReadRow(3).Name);
         Assert.Equal(clothes.ReadRow(2).Colors, clothes.ReadRow(3).Colors);
         Assert.Equal(4, Model[ClothesCount]);
         Undo();
         Assert.Equal(3, clothes.ReadRows().Count);
         Assert.Equal(3, Model[ClothesCount]);
      }

      [Fact]
      public void Remove_ShiftsTheLaterRowsUp_ShortensTheTable_AndUpdatesTheCounter() {
         var skin = Create().SkinTones;

         var result = skin.Remove(2);
         Done();

         Assert.True(result.Success);
         Assert.Equal(3, skin.Run.ElementCount);
         Assert.Equal(3, Model[SkinCount]);
         Assert.Equal(new[] { "Original", "Fair", "Sky Blue" }, skin.ReadRows().Select(row => row.Name));
         Assert.Equal(Gba(4, 20, 31), skin.ReadRow(2).Colors[0]);
         Assert.True(skin.IsHealthy);
         Undo();
         Assert.Equal(new[] { "Original", "Fair", "Tan", "Sky Blue" }, skin.ReadRows().Select(row => row.Name));
         Assert.Equal(4, Model[SkinCount]);
      }

      [Fact]
      public void Remove_NeverRemovesRowZeroOrAMissingRow() {
         var skin = Create().SkinTones;

         Assert.False(skin.Remove(0).Success);
         Assert.False(skin.Remove(4).Success);
         Assert.False(skin.Remove(-1).Success);
         Assert.Equal(4, skin.Run.ElementCount);
         Assert.Equal(4, Model[SkinCount]);
      }

      [Fact]
      public void Remove_DownToJustTheOriginalRow_IsAllowedButNotBelow() {
         var clothes = Create().Clothes;

         Assert.True(clothes.Remove(2).Success);
         Assert.True(clothes.Remove(1).Success);
         Assert.False(clothes.Remove(0).Success);

         Assert.Equal(1, clothes.Run.ElementCount);
         Assert.Equal(1, Model[ClothesCount]);
         Assert.Equal("Original", clothes.ReadRow(0).Name);
      }

      [Fact]
      public void Move_SwapsTwoRows_ButNeverMovesRowZero() {
         var skin = Create().SkinTones;

         var result = skin.Move(2, 1);
         Done();

         Assert.True(result.Success);
         Assert.Equal(3, result.Index);
         Assert.Equal(new[] { "Original", "Fair", "Sky Blue", "Tan" }, skin.ReadRows().Select(row => row.Name));
         Assert.Equal(Gba(26, 20, 14), skin.ReadRow(3).Colors[0]);
         Assert.False(skin.Move(1, -1).Success);
         Assert.False(skin.Move(0, 1).Success);
         Assert.False(skin.Move(3, 1).Success);
         Undo();
         Assert.Equal(new[] { "Original", "Fair", "Tan", "Sky Blue" }, skin.ReadRows().Select(row => row.Name));
      }

      #endregion

      #region Counters that do not match

      [Fact]
      public void CounterOfZero_IsAProblem_ThatBlocksAddRemoveAndMove_UntilFixed() {
         var clothes = Create().Clothes;
         Data[ClothesCount] = 0;

         Assert.False(clothes.IsHealthy);
         Assert.Contains("0", clothes.Problem);
         Assert.Contains("Fix", clothes.Problem);
         Assert.Equal(3, clothes.VisibleRows);
         Assert.False(clothes.Add("X", new ushort[3]).Success);
         Assert.False(clothes.Remove(1).Success);
         Assert.False(clothes.Move(1, 1).Success);
         Assert.Equal(3, clothes.Run.ElementCount);

         var fixedResult = clothes.FixCount();
         Done();

         Assert.True(fixedResult.Success);
         Assert.Equal(3, Model[ClothesCount]);
         Assert.True(clothes.IsHealthy);
         Undo();
         Assert.Equal(0, Model[ClothesCount]);
      }

      [Fact]
      public void CounterBiggerThanTheTable_OnlyRealRowsAreShown() {
         Data[SkinCount] = 200;
         var skin = Create().SkinTones;

         Assert.False(skin.IsHealthy);
         Assert.Equal(4, skin.VisibleRows);
         Assert.Equal(4, skin.ReadRows().Count);
         Assert.True(skin.FixCount().Success);
         Assert.Equal(4, Model[SkinCount]);
      }

      [Fact]
      public void CounterSmallerThanTheTable_OnlyTheCountedRowsAreShown() {
         Data[SkinCount] = 2;
         var skin = Create().SkinTones;

         Assert.False(skin.IsHealthy);
         Assert.Equal(2, skin.VisibleRows);
         Assert.Equal(2, skin.ReadRows().Count);
         Assert.True(skin.FixCount().Success);
         Assert.Equal(4, Model[SkinCount]);
         Assert.Equal(4, skin.ReadRows().Count);
      }

      [Fact]
      public void RowsTooShortForANameAndThreeColours_AreAProblemNotACrash() {
         var model = new PokemonModel(new byte[0x400], singletons: Singletons);
         var viewPort = new ViewPort("other.txt", model, InstantDispatch.Instance, Singletons);
         viewPort.Edit($"@100 ^{CharacterCustomization.SkinTonesAnchor}[a:: b::: c:: d:]2 ");
         viewPort.Edit($"@200 ^{CharacterCustomization.SkinToneCountAnchor}[count.]1 ");
         var table = new CharacterColorTable(model, () => viewPort.CurrentChange, CharacterColorKind.SkinTone);

         Assert.False(table.IsHealthy);
         Assert.Equal(0, table.VisibleRows);
         Assert.Empty(table.ReadRows());
         Assert.False(table.SetName(1, "x").Success);
      }

      [Fact]
      public void TableThatIsMissing_IsAProblemNotACrash() {
         var customization = new CharacterCustomization(new PokemonModel(new byte[0x100], singletons: Singletons), () => new ModelDelta());

         Assert.False(customization.SkinTones.Exists);
         Assert.False(customization.SkinTones.IsHealthy);
         Assert.Contains("no skin tone table", customization.SkinTones.Problem);
         Assert.Empty(customization.SkinTones.ReadRows());
         Assert.Empty(customization.ReadRoles());
         Assert.False(customization.SkinTones.Add("X", new ushort[3]).Success);
      }

      #endregion

      #region Secondary colours (the red parts)

      private const string TwoRowColorFormat = "[name\"\"16 main:|c shadow:|c highlight:|c unused:]2";

      [Fact]
      public void AnchorNames_LiveUnderGraphics_AndTheOldNamesAreOnlyAFallback() {
         Assert.Equal("graphics.characterCustomization.skinTones", CharacterCustomization.SkinTonesAnchor);
         Assert.Equal("graphics.characterCustomization.clothes", CharacterCustomization.ClothesAnchor);
         Assert.Equal("graphics.characterCustomization.secondary", CharacterCustomization.SecondaryAnchor);
         Assert.Equal("graphics.characterCustomization.roles", CharacterCustomization.RolesAnchor);
         Assert.Equal("graphics.characterCustomization.skinToneCount", CharacterCustomization.SkinToneCountAnchor);
         Assert.Equal("graphics.characterCustomization.clothesCount", CharacterCustomization.ClothesCountAnchor);
         Assert.Equal("graphics.characterCustomization.secondaryCount", CharacterCustomization.SecondaryCountAnchor);
         Assert.Equal("graphics.characterCustomization.roleCount", CharacterCustomization.RoleCountAnchor);
         Assert.Equal(CharacterAnchorNames.Current, CharacterAnchorNames.For(Model));
         Assert.True(CharacterAnchorNames.Current.HasSecondaryNames);
         Assert.False(CharacterAnchorNames.Legacy.HasSecondaryNames);
         Assert.Equal("characterCustomization.skinTones", CharacterAnchorNames.Legacy.SkinTones);
      }

      [Fact]
      public void WhenBothNameSetsExist_TheNewNamesWin() {
         ViewPort.Edit($"@800 ^characterCustomization.skinTones[name\"\"16 main:|c shadow:|c highlight:|c unused:]2 ");

         Assert.Equal(CharacterAnchorNames.Current, CharacterAnchorNames.For(Model));
         Assert.Equal(4, Create().SkinTones.ReadRows().Count);
      }

      [Fact]
      public void Secondary_IsFoundThroughItsOwnAnchors_AndReadLikeTheOtherTables() {
         var customization = Create();

         Assert.True(customization.HasSecondary);
         var rows = customization.Secondary.ReadRows();
         Assert.Equal(new[] { "Original", "Gold", "Navy" }, rows.Select(row => row.Name));
         Assert.Equal(new[] { Gba(31, 22, 5), Gba(24, 16, 3), Gba(31, 22, 5) }, rows[1].Colors);
         Assert.True(customization.Secondary.IsHealthy);
         Assert.Equal(3, customization.Secondary.StoredCount);
         Assert.Equal(SecondaryTable, customization.Secondary.Run.Start);
         Assert.Equal("secondary colour", customization.Secondary.Noun);
         Assert.Equal("secondary colours", customization.Secondary.NounPlural);
         Assert.Equal("New colour", customization.Secondary.DefaultName);
         Assert.Equal(CharacterColorKind.Secondary, customization.Secondary.Kind);
      }

      [Fact]
      public void Secondary_IsOptional_WithoutItsAnchorsTheRolesHaveNoSecondarySlots() {
         var model = new PokemonModel(new byte[0x800], singletons: Singletons);
         var viewPort = new ViewPort("other.txt", model, InstantDispatch.Instance, Singletons);
         viewPort.Edit($"@100 ^{CharacterCustomization.SkinTonesAnchor}{TwoRowColorFormat} ");
         viewPort.Edit($"@200 ^{CharacterCustomization.ClothesAnchor}{TwoRowColorFormat} ");
         viewPort.Edit($"@300 ^{CharacterCustomization.RolesAnchor}[gender. context. a. b. c. d. e. f. g. h. i. j.]1 ");
         viewPort.Edit($"@400 ^{CharacterCustomization.SkinToneCountAnchor}[count.]1 ");
         viewPort.Edit($"@401 ^{CharacterCustomization.ClothesCountAnchor}[count.]1 ");
         viewPort.Edit($"@402 ^{CharacterCustomization.RoleCountAnchor}[count.]1 ");
         model[0x402] = 1;
         model[0x309] = 12; // bytes that look like secondary slots, but this ROM has no secondary table: they are not used

         var customization = new CharacterCustomization(model, () => viewPort.CurrentChange);

         Assert.True(CharacterCustomization.IsSupported(model));
         Assert.False(customization.HasSecondary);
         Assert.False(customization.Secondary.Exists);
         Assert.Empty(customization.Secondary.ReadRows());
         Assert.Equal(new[] { -1, -1, -1 }, customization.ReadRoles()[0].Secondary);
      }

      [Fact]
      public void Apply_PaintsTheSecondarySlots_AndLeavesTheRestAlone() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Male, CharacterRole.OverworldContext);
         var gold = customization.Secondary.ReadRow(1);
         var palette = TestPalette();

         var painted = role.Apply(palette, null, null, gold);

         Assert.Equal(GbaColor.ToDisplay(gold.Colors[0]), painted[12]);
         Assert.Equal(GbaColor.ToDisplay(gold.Colors[1]), painted[13]);
         foreach (var untouched in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 14, 15 }) Assert.Equal(palette[untouched], painted[untouched]);
      }

      [Fact]
      public void Apply_SkinClothesAndSecondaryTogether_PaintEachTheirOwnSlots() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Female, CharacterRole.TrainerPicContext);
         var skin = customization.SkinTones.ReadRow(3);
         var clothes = customization.Clothes.ReadRow(2);
         var navy = customization.Secondary.ReadRow(2);

         var painted = role.Apply(TestPalette(), skin, clothes, navy);

         Assert.Equal(GbaColor.ToDisplay(skin.Colors[0]), painted[2]);
         Assert.Equal(GbaColor.ToDisplay(clothes.Colors[0]), painted[10]);
         Assert.Equal(GbaColor.ToDisplay(clothes.Colors[1]), painted[11]);
         Assert.Equal(GbaColor.ToDisplay(navy.Colors[0]), painted[12]);
         Assert.Equal(GbaColor.ToDisplay(navy.Colors[1]), painted[13]);
         Assert.Equal(TestPalette()[14], painted[14]);
         Assert.Equal(TestPalette()[15], painted[15]);
      }

      [Fact]
      public void Apply_SecondaryRowZeroOrNoRow_LeavesTheRedAsItWas() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Male, CharacterRole.OverworldContext);

         var withRowZero = role.Apply(TestPalette(), null, null, customization.Secondary.ReadRow(0));
         var withoutRow = role.Apply(TestPalette(), null, null, null);
         var threeArguments = role.Apply(TestPalette(), customization.SkinTones.ReadRow(0), customization.Clothes.ReadRow(0));

         Assert.Equal(TestPalette(), withRowZero);
         Assert.Equal(TestPalette(), withoutRow);
         Assert.Equal(TestPalette(), threeArguments);
      }

      [Fact]
      public void Apply_ARoleThatDoesNotPaintTheSecondaryColour_IgnoresIt() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Female, CharacterRole.ReflectionContext); // no clothes, no secondary slots

         var painted = role.Apply(TestPalette(), null, null, customization.Secondary.ReadRow(1));

         Assert.Equal(TestPalette(), painted);
      }

      [Fact]
      public void Draw_ShowsThePaintedSecondaryColoursInThePixels() {
         var customization = Create();
         var role = customization.FindRole(CharacterRole.Male, CharacterRole.TrainerPicContext);
         var navy = customization.Secondary.ReadRow(2);
         var pixels = new int[3, 1];
         pixels[0, 0] = 12;
         pixels[1, 0] = 13;
         pixels[2, 0] = 9;

         var picture = CharacterSprites.Draw(pixels, role.Apply(TestPalette(), null, null, navy));

         Assert.Equal(3, picture.PixelWidth);
         Assert.Equal(GbaColor.ToDisplay(navy.Colors[0]), picture.PixelData[0]);
         Assert.Equal(GbaColor.ToDisplay(navy.Colors[1]), picture.PixelData[1]);
         Assert.Equal(TestPalette()[9], picture.PixelData[2]);
      }

      [Fact]
      public void Secondary_SetColor_WritesTheGbaColor_AndUndoTakesItBack() {
         var secondary = Create().Secondary;
         var before = secondary.ReadRow(1).Colors[1];

         var result = secondary.SetColor(1, 1, Gba(3, 9, 28));
         Done();

         Assert.True(result.Success);
         Assert.Equal(Gba(3, 9, 28), secondary.ReadRow(1).Colors[1]);
         Assert.Equal(Gba(3, 9, 28), Model.ReadMultiByteValue(SecondaryTable + RowLength + 18, 2));
         Undo();
         Assert.Equal(before, secondary.ReadRow(1).Colors[1]);
      }

      [Fact]
      public void Secondary_RowZeroIsTheOriginalLook_AndCannotBeEditedOrRenamed() {
         var secondary = Create().Secondary;

         Assert.False(secondary.SetColor(0, 0, Gba(1, 1, 1)).Success);
         Assert.False(secondary.SetName(0, "Mine").Success);
         Assert.False(secondary.Remove(0).Success);
         Assert.Equal("Original", secondary.ReadRow(0).Name);
         Assert.Equal(Gba(31, 12, 11), secondary.ReadRow(0).Colors[0]);
      }

      [Fact]
      public void Secondary_DeriveShades_MakesTheShadowFromTheMain_AndKeepsTheHighlightEqualToTheMain() {
         var secondary = Create().Secondary;
         var main = Gba(20, 10, 4);
         secondary.SetColor(1, 0, main);

         var result = secondary.DeriveShades(1);

         Assert.True(result.Success);
         var row = secondary.ReadRow(1);
         Assert.Equal(main, row.Colors[0]);
         Assert.Equal("14:7:3", GbaColor.Describe(row.Colors[1])); // 68%, 70% and 72% of 20, 10 and 4
         Assert.Equal(main, row.Colors[2]);
         Assert.Equal(GbaColor.DeriveSecondaryShades(main), (row.Colors[1], row.Colors[2]));
      }

      [Fact]
      public void Secondary_Add_GrowsInPlace_RaisesItsOwnCounter_AndLeavesTheOtherCountersAlone() {
         var secondary = Create().Secondary;

         var result = secondary.Add("Teal", new[] { Gba(4, 24, 24), Gba(3, 17, 17), Gba(4, 24, 24) });
         Done();

         Assert.True(result.Success);
         Assert.Equal(3, result.Index);
         Assert.Equal(SecondaryTable, secondary.Run.Start);
         Assert.Equal(4, secondary.Run.ElementCount);
         Assert.Equal(4, Model[SecondaryCount]);
         Assert.Equal(3, Model[ClothesCount]);
         Assert.Equal(4, Model[SkinCount]);
         Assert.Equal("Teal", secondary.ReadRow(3).Name);
         Assert.Equal(SecondaryTable, Model.ReadPointer(PoolSecondary));
         Assert.True(secondary.IsHealthy);
         Undo();
         Assert.Equal(3, Model[SecondaryCount]);
         Assert.Equal(3, secondary.ReadRows().Count);
      }

      [Fact]
      public void Secondary_DuplicateRemoveAndMove_KeepTheCounterInStep() {
         var secondary = Create().Secondary;

         Assert.True(secondary.Duplicate(1).Success);
         Assert.Equal("Gold 2", secondary.ReadRow(3).Name);
         Assert.Equal(4, Model[SecondaryCount]);
         Assert.True(secondary.Move(3, -1).Success);
         Assert.Equal(new[] { "Original", "Gold", "Gold 2", "Navy" }, secondary.ReadRows().Select(row => row.Name));
         Assert.True(secondary.Remove(2).Success);
         Assert.Equal(new[] { "Original", "Gold", "Navy" }, secondary.ReadRows().Select(row => row.Name));
         Assert.Equal(3, Model[SecondaryCount]);
         Assert.True(secondary.IsHealthy);
      }

      [Fact]
      public void Secondary_ABrokenCounter_IsAProblemThatNamesTheSecondaryColours() {
         Data[SecondaryCount] = 0;
         var secondary = Create().Secondary;

         Assert.False(secondary.IsHealthy);
         Assert.Contains("secondary colours", secondary.Problem);
         Assert.False(secondary.Add("X", new ushort[3]).Success);
         Assert.True(secondary.FixCount().Success);
         Assert.Equal(3, Model[SecondaryCount]);
         Assert.True(secondary.IsHealthy);
      }

      #region Code that points straight at the colours of a row
      // The compiler is free to fold "address of the table + 16" into one 4-byte word of the game's code (the address of the colours of row 0). The model only knows
      // pointers to the very start of a table, so when a table moves to make room for a new row, such a word must be moved with it or the game goes on reading the erased old copy:
      // every colour 0xFFFF, plain white. These words are in the pool after the pointers.
      private const int FoldedMain = 0x280, FoldedShadow = 0x284, FoldedHighlight = 0x288, FoldedRowOne = 0x28C, FoldedOdd = 0x290, FoldedReserved = 0x294, FoldedInData = 0x2A0;

      private CharacterColorTable TableOf(CharacterColorKind kind) {
         var customization = Create();
         return kind == CharacterColorKind.SkinTone ? customization.SkinTones : kind == CharacterColorKind.Clothes ? customization.Clothes : customization.Secondary;
      }

      private static int StartOf(CharacterColorKind kind) => kind == CharacterColorKind.SkinTone ? SkinTable : kind == CharacterColorKind.Clothes ? ClothesTable : SecondaryTable;

      private static int PoolOf(CharacterColorKind kind) => kind == CharacterColorKind.SkinTone ? PoolSkin : kind == CharacterColorKind.Clothes ? PoolClothes : PoolSecondary;

      /// <summary>Puts a little table right behind the colour table, so it can't grow in place (the skin table already has the clothes table behind it).</summary>
      private void BlockTheGrowth(CharacterColorKind kind) {
         if (kind == CharacterColorKind.SkinTone) return;
         var run = TableOf(kind).Run;
         ViewPort.Edit($"@{run.Start + run.Length:X} ^charcustomblocker[x.]2 ");
         Done();
      }

      private void WriteWord(int address, int target) {
         Model.WriteMultiByteValue(address, 4, Token, BaseModel.PointerOffset + target);
         Done();
      }

      private int Target(int wordAddress) => Word(wordAddress) - BaseModel.PointerOffset;

      private void WriteFoldedWords(int start) {
         WriteWord(FoldedMain, start + 16);
         WriteWord(FoldedShadow, start + 18);
         WriteWord(FoldedHighlight, start + 20);
         WriteWord(FoldedRowOne, start + RowLength); // the start of row 1: not "the colours of row 0"
         WriteWord(FoldedOdd, start + 17);           // in the middle of a colour
         WriteWord(FoldedReserved, start + 22);      // behind the three colours
      }

      [Theory]
      [InlineData(CharacterColorKind.SkinTone)]
      [InlineData(CharacterColorKind.Clothes)]
      [InlineData(CharacterColorKind.Secondary)]
      public void Add_ThatMovedTheTable_MovesTheCodeWordsThatPointAtTheColoursOfRowZero(CharacterColorKind kind) {
         BlockTheGrowth(kind);
         var start = StartOf(kind);
         WriteFoldedWords(start);
         var table = TableOf(kind);
         var rowOne = table.ReadRow(1);

         var result = table.Add("Mint", new[] { Gba(10, 28, 18), Gba(6, 22, 12), Gba(16, 31, 24) });
         Done();

         Assert.True(result.Success);
         Assert.NotEqual(start, table.Run.Start);
         var moved = table.Run.Start;
         Assert.Equal(moved, Model.ReadPointer(PoolOf(kind)));
         Assert.Equal(moved + 16, Target(FoldedMain));
         Assert.Equal(moved + 18, Target(FoldedShadow));
         Assert.Equal(moved + 20, Target(FoldedHighlight));
         Assert.Equal(start + RowLength, Target(FoldedRowOne));
         Assert.Equal(start + 17, Target(FoldedOdd));
         Assert.Equal(start + 22, Target(FoldedReserved));
         Assert.Contains("3 more places", result.Message); // the person is told that three words of the game's code followed the table
         // what the game reads through the folded words: the colours of the rows of the table at its new place, the new row included
         Assert.Equal(rowOne.Colors[0], (ushort)Model.ReadMultiByteValue(Target(FoldedMain) + RowLength, 2));
         Assert.Equal(rowOne.Colors[1], (ushort)Model.ReadMultiByteValue(Target(FoldedShadow) + RowLength, 2));
         Assert.Equal(rowOne.Colors[2], (ushort)Model.ReadMultiByteValue(Target(FoldedHighlight) + RowLength, 2));
         var last = table.Run.ElementCount - 1;
         Assert.Equal(Gba(10, 28, 18), (ushort)Model.ReadMultiByteValue(Target(FoldedMain) + RowLength * last, 2));
      }

      [Theory]
      [InlineData(CharacterColorKind.SkinTone)]
      [InlineData(CharacterColorKind.Secondary)]
      public void Add_ThatMovedTheTable_AndItsUndoAndRedo_TakeTheCodeWordsAlong(CharacterColorKind kind) {
         BlockTheGrowth(kind);
         var start = StartOf(kind);
         WriteFoldedWords(start);
         var table = TableOf(kind);

         table.Add("Mint", new[] { Gba(10, 28, 18), Gba(6, 22, 12), Gba(16, 31, 24) });
         Done();
         var moved = table.Run.Start;
         Undo();

         Assert.Equal(start, table.Run.Start);
         Assert.Equal(start + 16, Target(FoldedMain));
         Assert.Equal(start + 18, Target(FoldedShadow));
         Assert.Equal(start + 20, Target(FoldedHighlight));

         Redo();

         Assert.Equal(moved, table.Run.Start);
         Assert.Equal(moved + 16, Target(FoldedMain));
         Assert.Equal(moved + 20, Target(FoldedHighlight));
      }

      [Fact]
      public void Add_ThatGrowsInPlace_LeavesTheCodeWordsAlone() {
         WriteFoldedWords(ClothesTable);
         var clothes = Create().Clothes;

         var result = clothes.Add("Green", new[] { Gba(4, 28, 6), Gba(2, 20, 4), Gba(12, 31, 14) });
         Done();

         Assert.True(result.Success);
         Assert.Equal(ClothesTable, clothes.Run.Start);
         Assert.Equal(ClothesTable + 16, Target(FoldedMain));
         Assert.Equal(ClothesTable + 20, Target(FoldedHighlight));
         Assert.DoesNotContain("more place", result.Message);
      }

      [Fact]
      public void Add_ThatMovedTheTable_DoesNotTouchAWordThatBelongsToKnownData() {
         WriteWord(FoldedMain, SkinTable + 16);
         ViewPort.Edit($"@{FoldedInData:X} ^charcustomotherdata[value::]1 ");
         WriteWord(FoldedInData, SkinTable + 16); // a number in a table the model knows: it only looks like the address
         var skin = Create().SkinTones;

         skin.Add("Mint", new[] { Gba(10, 28, 18), Gba(6, 22, 12), Gba(16, 31, 24) });
         Done();

         Assert.Equal(skin.Run.Start + 16, Target(FoldedMain));
         Assert.Equal(SkinTable + 16, Target(FoldedInData));
      }

      [Fact]
      public void AddingSeveralColours_KeepsTheCodeWordsOnTheTable() {
         BlockTheGrowth(CharacterColorKind.Secondary);
         WriteFoldedWords(SecondaryTable);
         var secondary = Create().Secondary;

         for (int i = 0; i < 5; i++) Assert.True(secondary.Add($"Added {i}", new[] { Gba(i, 20, 31 - i), Gba(2, 14, 26), Gba(12, 26, 31) }).Success);
         Done();

         Assert.Equal(8, secondary.Run.ElementCount);
         Assert.Equal(8, Model[SecondaryCount]);
         Assert.Equal(secondary.Run.Start + 16, Target(FoldedMain));
         Assert.Equal(secondary.Run.Start + 20, Target(FoldedHighlight));
         for (int row = 0; row < 8; row++) Assert.Equal(secondary.ReadRow(row).Colors[0], (ushort)Model.ReadMultiByteValue(Target(FoldedMain) + RowLength * row, 2));
      }
      #endregion

      [Fact]
      public void GbaColor_DeriveSecondaryShades_UsesTheRomsOwnRatios() {
         var (shadow, highlight) = GbaColor.DeriveSecondaryShades(Gba(31, 31, 31));

         Assert.Equal("21:22:22", GbaColor.Describe(shadow)); // 31 * .68 = 21.08, 31 * .70 = 21.7, 31 * .72 = 22.3
         Assert.Equal(Gba(31, 31, 31), highlight);
         Assert.Equal((Gba(0, 0, 0), Gba(0, 0, 0)), GbaColor.DeriveSecondaryShades(Gba(0, 0, 0)));
      }

      #endregion
   }

   public class CharacterCustomizationTabTests : CharacterCustomizationTestBase {
      private readonly List<string> tabErrors = new();
      private readonly List<string> tabMessages = new();

      private CharacterCustomizationTab CreateTab() {
         var tab = new CharacterCustomizationTab(ViewPort);
         tab.OnError += (sender, e) => tabErrors.Add(e);
         tab.OnMessage += (sender, e) => tabMessages.Add(e);
         return tab;
      }

      [Fact]
      public void Tab_IsSupportedOnlyWhereTheTablesAre() {
         Assert.True(CharacterCustomizationTab.IsSupported(Model));
         Assert.False(CharacterCustomizationTab.IsSupported(new PokemonModel(new byte[0x100], singletons: Singletons)));
      }

      [Fact]
      public void OpeningTheTabWithoutTheTables_SaysWhyAndOpensNothing() {
         var plain = new PokemonModel(new byte[0x200], singletons: Singletons);
         var viewPort = new ViewPort("plain.txt", plain, InstantDispatch.Instance, Singletons);
         var errors = new List<string>();
         var opened = new List<ITabContent>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.RequestTabChange += (sender, e) => opened.Add(e.NewTab);

         viewPort.OpenCharacterCustomizationTab();

         Assert.Empty(opened);
         Assert.Single(errors);
         Assert.Contains("no character customization tables", errors[0]);
      }

      [Fact]
      public void Tab_ListsBothTables_WithTheOriginalRowFirstAndNotEditable() {
         var tab = CreateTab();

         Assert.Equal(4, tab.SkinTones.Rows.Count);
         Assert.Equal(3, tab.Clothes.Rows.Count);
         Assert.True(tab.SkinTones.Rows[0].IsOriginal);
         Assert.False(tab.SkinTones.Rows[0].IsEditable);
         Assert.Equal("Original (the sprite's own colours)", tab.SkinTones.Rows[0].Caption);
         Assert.Equal("Tan", tab.SkinTones.Rows[2].Caption);
         Assert.True(tab.SkinTones.Rows[2].IsEditable);
         Assert.Equal(0, tab.SkinTones.SelectedIndex);
         Assert.False(tab.SkinTones.CanEditSelected);
         Assert.Equal(GbaColor.ToDisplay(Gba(31, 4, 4)), tab.Clothes.Rows[1].Main);
         Assert.False(tab.SkinTones.HasProblem);
      }

      [Fact]
      public void Tab_WithoutTheSpriteTables_StillWorksAndSaysWhatIsMissing() {
         var tab = CreateTab();

         Assert.All(tab.Panels, panel => Assert.True(panel.HasNote));
         Assert.All(tab.Panels, panel => Assert.False(panel.Standing.HasImage));
         tab.SkinTones.Select(2);
         tab.Clothes.Select(1);
         Assert.Empty(tabErrors);
         Assert.Equal(2, tab.SkinTones.SelectedIndex);
      }

      [Fact]
      public void Tab_SelectingARow_ShowsItsThreeColoursInThePaletteEditor() {
         var tab = CreateTab();

         tab.SkinTones.Select(3);

         Assert.Equal(3, tab.SkinTones.Palette.Elements.Count);
         Assert.Equal(GbaColor.ToDisplay(Gba(4, 20, 31)), tab.SkinTones.Palette.Elements[0].Color);
         Assert.Equal(GbaColor.ToDisplay(Gba(2, 14, 26)), tab.SkinTones.Palette.Elements[1].Color);
         Assert.True(tab.SkinTones.Palette.CanEditColors);
      }

      [Fact]
      public void Tab_EditingAColourInThePaletteEditor_ReachesTheRomAndUndoTakesItBack() {
         var tab = CreateTab();
         tab.SkinTones.Select(2);
         var before = tab.SkinTones.Data[2].Colors[1];
         var edited = Gba(5, 9, 28);

         tab.SkinTones.Palette.Elements[1].Color = GbaColor.ToDisplay(edited);
         tab.SkinTones.Palette.PushColorsToModel();

         Assert.Equal(edited, tab.SkinTones.Data[2].Colors[1]);
         Assert.Equal(edited, Model.ReadMultiByteValue(SkinTable + RowLength * 2 + 18, 2));
         Assert.Equal(GbaColor.ToDisplay(edited), tab.SkinTones.Rows[2].Shadow);
         Assert.Contains("5:9:28", tab.SkinTones.ColorSummary);

         tab.Undo.Execute(null);

         Assert.Equal(before, tab.SkinTones.Data[2].Colors[1]);
         Assert.Equal(GbaColor.ToDisplay(before), tab.SkinTones.Palette.Elements[1].Color);
      }

      [Fact]
      public void Tab_TheOriginalRowIsNeverWritten() {
         var tab = CreateTab();
         tab.SkinTones.Select(0);

         tab.SkinTones.Palette.Elements[0].Color = 1234;
         tab.SkinTones.Palette.PushColorsToModel();

         Assert.Equal(0, Model.ReadMultiByteValue(SkinTable + 16, 2));
      }

      [Fact]
      public void Tab_DeriveButton_MakesTheShadeAndHighlightFromTheMainColour() {
         var tab = CreateTab();
         tab.SkinTones.Select(2);
         var main = tab.SkinTones.Data[2].Colors[0];

         tab.SkinTones.DeriveShadeCommand.Execute(null);

         var expected = GbaColor.DeriveShades(main);
         Assert.Equal(main, tab.SkinTones.Data[2].Colors[0]);
         Assert.Equal(expected.shadow, tab.SkinTones.Data[2].Colors[1]);
         Assert.Equal(expected.highlight, tab.SkinTones.Data[2].Colors[2]);
      }

      [Fact]
      public void Tab_RenamingARow_WritesTheName_AndUndoTakesItBack() {
         var tab = CreateTab();

         var accepted = tab.SkinTones.Rows[2].Rename("Sunny");

         Assert.True(accepted);
         Assert.Equal("Sunny", tab.SkinTones.Rows[2].Name);
         Assert.Equal("Sunny", tab.SkinTones.Table.ReadRow(2).Name);
         tab.Undo.Execute(null);
         Assert.Equal("Tan", tab.SkinTones.Rows[2].Name);
         Assert.Empty(tabErrors);
      }

      [Fact]
      public void Tab_ANameThatIsTooLongOrOnTheOriginalRow_IsRefusedWithAMessage() {
         var tab = CreateTab();

         Assert.False(tab.SkinTones.Rows[2].Rename("A name that is far too long"));
         Assert.Single(tabErrors);
         Assert.Equal("Tan", tab.SkinTones.Rows[2].Name);
         tabErrors.Clear();

         Assert.False(tab.SkinTones.Rows[0].Rename("Mine"));
         Assert.Equal("Original", tab.SkinTones.Rows[0].Name);
      }

      [Fact]
      public void Tab_AddDuplicateMoveAndRemoveButtons_KeepTheCounterAndTheListsInStep() {
         var tab = CreateTab();
         var clothes = tab.Clothes;

         clothes.Select(2);
         clothes.Duplicate.Execute(null);
         Assert.Equal(4, clothes.Rows.Count);
         Assert.Equal(3, clothes.SelectedIndex);
         Assert.Equal("Blue 2", clothes.Rows[3].Name);
         Assert.Equal(4, Model[ClothesCount]);
         Assert.Equal(4, tab.ClothesGrid.Count);

         clothes.Add.Execute(null);
         Assert.Equal(5, clothes.Rows.Count);
         Assert.Equal("New colour", clothes.Rows[4].Name);
         Assert.Equal(4, clothes.SelectedIndex);

         clothes.MoveUp.Execute(null);
         Assert.Equal(3, clothes.SelectedIndex);
         Assert.Equal("New colour", clothes.Rows[3].Name);

         clothes.Remove.Execute(null);
         Assert.Equal(4, clothes.Rows.Count);
         Assert.DoesNotContain(clothes.Rows, row => row.Name == "New colour");

         clothes.Select(0);
         Assert.False(clothes.Remove.CanExecute(null));
         Assert.False(clothes.MoveUp.CanExecute(null));
         Assert.False(clothes.DeriveShadeCommand.CanExecute(null));

         tab.Undo.Execute(null); // the remove
         tab.Undo.Execute(null); // the move
         tab.Undo.Execute(null); // the add
         tab.Undo.Execute(null); // the duplicate
         Assert.Equal(3, clothes.Rows.Count);
         Assert.Equal(3, Model[ClothesCount]);
         Assert.Equal(3, tab.ClothesGrid.Count);
      }

      [Fact]
      public void Tab_AddingToATableThatMustMove_TellsThePersonAndUpdatesTheGameReference() {
         var tab = CreateTab();

         tab.SkinTones.Add.Execute(null);

         Assert.Equal(5, tab.SkinTones.Rows.Count);
         Assert.Equal("New skin tone", tab.SkinTones.Rows[4].Name);
         Assert.NotEqual(SkinTable, tab.SkinTones.Table.Run.Start);
         Assert.Equal(tab.SkinTones.Table.Run.Start, Model.ReadPointer(PoolSkin));
         Assert.Contains(tabMessages, message => message.Contains("moved to free space"));

         tab.Undo.Execute(null);

         Assert.Equal(SkinTable, tab.SkinTones.Table.Run.Start);
         Assert.Equal(SkinTable, Model.ReadPointer(PoolSkin));
         Assert.Equal(4, tab.SkinTones.Rows.Count);
      }

      [Fact]
      public void Tab_ABrokenCounter_ShowsAProblem_DisablesTheButtons_AndTheFixButtonRepairsIt() {
         Data[ClothesCount] = 0;
         var tab = CreateTab();
         var clothes = tab.Clothes;

         Assert.True(clothes.HasProblem);
         Assert.Equal(3, clothes.Rows.Count);
         Assert.False(clothes.Add.CanExecute(null));
         Assert.False(clothes.Remove.CanExecute(null));
         Assert.True(clothes.FixCount.CanExecute(null));

         clothes.FixCount.Execute(null);

         Assert.False(clothes.HasProblem);
         Assert.Equal(3, Model[ClothesCount]);
         tab.Undo.Execute(null);
         Assert.True(clothes.HasProblem);
      }

      [Fact]
      public void Tab_ZoomStaysBetweenOneAndSix() {
         var tab = CreateTab();

         tab.Zoom = 5;
         Assert.Equal(5, tab.Zoom);
         tab.Zoom = 99;
         Assert.Equal(6, tab.Zoom);
         tab.Zoom = -3;
         Assert.Equal(1, tab.Zoom);
      }

      [Fact]
      public void Tab_PreviewAllGrids_HaveATilePerRow_AndCanBeHidden() {
         var tab = CreateTab();

         Assert.Equal(4, tab.SkinGrid.Count);
         Assert.Equal(3, tab.ClothesGrid.Count);
         tab.ShowAllPreviews = false;
         Assert.Empty(tab.SkinGrid);
         Assert.Empty(tab.ClothesGrid);
         tab.ShowAllPreviews = true;
         Assert.Equal(4, tab.SkinGrid.Count);
      }

      [Fact]
      public void Tab_RoleLinesAndThePictureHint_SayWhichSlotsTheGamePaints() {
         var tab = CreateTab();

         Assert.NotEmpty(tab.RoleLines);
         Assert.Contains("Skin: main slot 2, shadow slot 3, highlight slot 1, outline slot 4 (made from the shadow).", tab.PictureHint);
         Assert.Contains("slot", string.Join(" ", tab.RoleLines));
      }

      #region Secondary colours (the red parts)

      [Fact]
      public void Tab_HasThreeLists_TheSecondaryOneLast() {
         var tab = CreateTab();

         Assert.True(tab.HasSecondary);
         Assert.Equal(3, tab.Lists.Count);
         Assert.Same(tab.SkinTones, tab.Lists[0]);
         Assert.Same(tab.Clothes, tab.Lists[1]);
         Assert.Same(tab.Secondary, tab.Lists[2]);
         Assert.Equal("Secondary colours (the red parts)", tab.Secondary.Title);
         Assert.Equal(new[] { "Original", "Gold", "Navy" }, tab.Secondary.Rows.Select(row => row.Name));
         Assert.True(tab.Secondary.Rows[0].IsOriginal);
         Assert.Equal(0, tab.Secondary.SelectedIndex);
         Assert.False(tab.Secondary.CanEditSelected);
         Assert.Equal(GbaColor.ToDisplay(Gba(31, 22, 5)), tab.Secondary.Rows[1].Main);
         Assert.Equal(3, tab.SecondaryGrid.Count);
         Assert.Contains("secondary colour", tab.Introduction);
         Assert.Contains("secondary colour", tab.PreviewHint);
         Assert.Contains("secondary colour", tab.GlanceHint);
         Assert.True(tab.OpenSecondaryTable.CanExecute(null));
      }

      [Fact]
      public void Tab_SelectingASecondaryRow_ShowsItsThreeColoursInThePaletteEditor() {
         var tab = CreateTab();

         tab.Secondary.Select(2);

         Assert.Equal(3, tab.Secondary.Palette.Elements.Count);
         Assert.Equal(GbaColor.ToDisplay(Gba(4, 6, 16)), tab.Secondary.Palette.Elements[0].Color);
         Assert.Equal(GbaColor.ToDisplay(Gba(3, 4, 12)), tab.Secondary.Palette.Elements[1].Color);
         Assert.True(tab.Secondary.CanEditSelected);
         Assert.True(tab.SecondaryGrid[2].Selected);
         Assert.False(tab.SecondaryGrid[0].Selected);
      }

      [Fact]
      public void Tab_EditingASecondaryColourInThePaletteEditor_ReachesTheRomAndUndoTakesItBack() {
         var tab = CreateTab();
         tab.Secondary.Select(1);
         var before = tab.Secondary.Data[1].Colors[1];
         var edited = Gba(5, 9, 28);

         tab.Secondary.Palette.Elements[1].Color = GbaColor.ToDisplay(edited);
         tab.Secondary.Palette.PushColorsToModel();

         Assert.Equal(edited, tab.Secondary.Data[1].Colors[1]);
         Assert.Equal(edited, Model.ReadMultiByteValue(SecondaryTable + RowLength + 18, 2));
         Assert.Equal(GbaColor.ToDisplay(edited), tab.Secondary.Rows[1].Shadow);
         Assert.Contains("5:9:28", tab.Secondary.ColorSummary);
         Assert.Equal(Gba(31, 22, 5), Model.ReadMultiByteValue(SecondaryTable + RowLength + 16, 2)); // the main colour was not touched

         tab.Undo.Execute(null);

         Assert.Equal(before, tab.Secondary.Data[1].Colors[1]);
         Assert.Equal(GbaColor.ToDisplay(before), tab.Secondary.Palette.Elements[1].Color);
      }

      [Fact]
      public void Tab_SecondaryButtons_AddDuplicateMoveAndRemove_KeepTheCounterAndTheGridInStep() {
         var tab = CreateTab();
         var secondary = tab.Secondary;

         secondary.Select(1);
         secondary.Duplicate.Execute(null);
         Assert.Equal(4, secondary.Rows.Count);
         Assert.Equal(3, secondary.SelectedIndex);
         Assert.Equal("Gold 2", secondary.Rows[3].Name);
         Assert.Equal(4, Model[SecondaryCount]);
         Assert.Equal(4, tab.SecondaryGrid.Count);

         secondary.Add.Execute(null);
         Assert.Equal(5, secondary.Rows.Count);
         Assert.Equal("New colour", secondary.Rows[4].Name);
         Assert.Equal(5, Model[SecondaryCount]);

         secondary.Remove.Execute(null);
         Assert.Equal(4, secondary.Rows.Count);
         secondary.Select(0);
         Assert.False(secondary.Remove.CanExecute(null));
         Assert.False(secondary.MoveUp.CanExecute(null));

         tab.Undo.Execute(null); // the remove
         tab.Undo.Execute(null); // the add
         tab.Undo.Execute(null); // the duplicate
         Assert.Equal(3, secondary.Rows.Count);
         Assert.Equal(3, Model[SecondaryCount]);
         Assert.Equal(3, tab.SecondaryGrid.Count);
         Assert.Equal(3, Model[ClothesCount]); // the other counters never moved
         Assert.Equal(4, Model[SkinCount]);
      }

      [Fact]
      public void Tab_RenamingASecondaryRow_WritesTheName_AndUndoTakesItBack() {
         var tab = CreateTab();

         var accepted = tab.Secondary.Rows[2].Rename("Midnight");

         Assert.True(accepted);
         Assert.Equal("Midnight", tab.Secondary.Table.ReadRow(2).Name);
         Assert.Equal("Midnight", tab.SecondaryGrid[2].Name);
         Assert.False(tab.Secondary.Rows[0].Rename("Mine"));
         tab.Undo.Execute(null);
         Assert.Equal("Navy", tab.Secondary.Rows[2].Name);
      }

      [Fact]
      public void Tab_SecondaryDeriveButton_KeepsTheMainAndMakesTheShadowAndTheEqualHighlight() {
         var tab = CreateTab();
         tab.Secondary.Select(2);
         var main = tab.Secondary.Data[2].Colors[0];

         tab.Secondary.DeriveShadeCommand.Execute(null);

         var expected = GbaColor.DeriveSecondaryShades(main);
         Assert.Equal(main, tab.Secondary.Data[2].Colors[0]);
         Assert.Equal(expected.shadow, tab.Secondary.Data[2].Colors[1]);
         Assert.Equal(main, tab.Secondary.Data[2].Colors[2]);
      }

      [Fact]
      public void Tab_RoleLinesAndTheHighlightNote_MentionTheSecondaryColour() {
         var tab = CreateTab();

         Assert.Contains("slots 12-13 with the secondary colour", string.Join(" ", tab.RoleLines));
         Assert.Contains("Secondary: main slot 12, shadow slot 13.", tab.PictureHint);
         Assert.Contains("the red parts", tab.PictureHint);
         Assert.Contains("secondary", tab.HighlightNote);
      }

      [Fact]
      public void Tab_PreviewAllGrids_IncludeTheSecondaryGrid_AndCanBeHidden() {
         var tab = CreateTab();

         Assert.Equal(3, tab.SecondaryGrid.Count);
         tab.ShowAllPreviews = false;
         Assert.Empty(tab.SecondaryGrid);
         tab.ShowAllPreviews = true;
         Assert.Equal(3, tab.SecondaryGrid.Count);
      }

      [Fact]
      public void Tab_UndoOfASecondaryChange_ReadsTheSecondaryListAgain() {
         var tab = CreateTab();
         tab.Secondary.Select(1);
         tab.Secondary.Palette.Elements[0].Color = GbaColor.ToDisplay(Gba(1, 2, 3));
         tab.Secondary.Palette.PushColorsToModel();
         Assert.Equal(Gba(1, 2, 3), tab.Secondary.Data[1].Colors[0]);

         tab.Undo.Execute(null);
         tab.Redo.Execute(null);

         Assert.Equal(Gba(1, 2, 3), tab.Secondary.Data[1].Colors[0]);
         Assert.Equal(GbaColor.ToDisplay(Gba(1, 2, 3)), tab.Secondary.Rows[1].Main);
      }

      #endregion
   }

   /// <summary>
   /// A ROM made before the secondary colour (the first release's anchor names "characterCustomization.*", roles with padding in the last three bytes) still opens:
   /// the tab has the skin tone and clothes colour lists and nothing about the secondary colour.
   /// </summary>
   public class CharacterCustomizationOldAnchorNameTests : BaseViewModelTestClass {
      private const int SkinTable = 0x100, ClothesTable = 0x160, RoleTable = 0x200, SkinCount = 0x250, ClothesCount = 0x251, RoleCount = 0x252;
      private const string ColorRowFormat = "[name\"\"16 main:|c shadow:|c highlight:|c unused:]";

      public CharacterCustomizationOldAnchorNameTests() : base(0x10000) {
         SetFullModel(0xFF);
         Model.SetList(new ModelDelta(), "charcustomgenders", "Male (Brendan)", "Female (May)");
         Model.SetList(new ModelDelta(), "charcustomcontexts", "Overworld", "Trainer pic", "Reflection");
         ViewPort.Edit($"@{SkinTable:X} ^characterCustomization.skinTones{ColorRowFormat}3 ");
         ViewPort.Edit($"@{ClothesTable:X} ^characterCustomization.clothes{ColorRowFormat}2 ");
         ViewPort.Edit($"@{RoleTable:X} ^characterCustomization.roles[gender.charcustomgenders context.charcustomcontexts skinMain. skinShadow. skinHighlight. skinOutline. clothesMain. clothesShadow. clothesHighlight. unused. unused. unused.]2 ");
         ViewPort.Edit($"@{SkinCount:X} ^characterCustomization.skinToneCount[count.]1 ");
         ViewPort.Edit($"@{ClothesCount:X} ^characterCustomization.clothesCount[count.]1 ");
         ViewPort.Edit($"@{RoleCount:X} ^characterCustomization.roleCount[count.]1 ");
         WriteRow(SkinTable, 0, "Original");
         WriteRow(SkinTable, 1, "Fair");
         WriteRow(SkinTable, 2, "Tan");
         WriteRow(ClothesTable, 0, "Original");
         WriteRow(ClothesTable, 1, "Red");
         Data[SkinCount] = 3;
         Data[ClothesCount] = 2;
         Data[RoleCount] = 2;
         for (int role = 0; role < 2; role++) {
            var address = RoleTable + 12 * role;
            var bytes = new byte[] { 0, (byte)role, 2, 3, 1, 4, 10, 11, 0xFF, 0, 0, 0 }; // the padding is 00 00 00: not slot 0 of anything
            for (int i = 0; i < bytes.Length; i++) Data[address + i] = bytes[i];
         }
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      private void WriteRow(int table, int index, string name) {
         var address = table + 24 * index;
         var bytes = Model.TextConverter.Convert(name, out _);
         for (int i = 0; i < 16; i++) Data[address + i] = i < bytes.Count ? bytes[i] : (byte)0xFF;
         for (int i = 16; i < 24; i++) Data[address + i] = 0;
      }

      [Fact]
      public void TheOldNames_AreFoundAndSupported() {
         Assert.Equal(CharacterAnchorNames.Legacy, CharacterAnchorNames.For(Model));
         Assert.True(CharacterCustomization.IsSupported(Model));
         Assert.True(CharacterCustomizationTab.IsSupported(Model));
      }

      [Fact]
      public void TheOldRoles_HaveNoSecondarySlots_EvenThoughTheirPaddingIsZero() {
         var customization = new CharacterCustomization(Model, () => ViewPort.CurrentChange);

         var roles = customization.ReadRoles();

         Assert.False(customization.HasSecondary);
         Assert.Equal(2, roles.Count);
         Assert.Equal(new[] { -1, -1, -1 }, roles[0].Secondary);
         Assert.Equal("palette slots 1-4 are painted with the skin tone, slots 10-11 with the clothes colour", roles[0].Describe());
         Assert.Equal("Skin: main slot 2, shadow slot 3, highlight slot 1, outline slot 4 (made from the shadow). Clothes: main slot 10, shadow slot 11.", roles[0].DescribeSlots());
      }

      [Fact]
      public void TheTab_HasTwoLists_AndHidesEverythingAboutTheSecondaryColour() {
         var tab = new CharacterCustomizationTab(ViewPort);

         Assert.False(tab.HasSecondary);
         Assert.Equal(2, tab.Lists.Count);
         Assert.Equal(3, tab.SkinTones.Rows.Count);
         Assert.Equal(2, tab.Clothes.Rows.Count);
         Assert.Empty(tab.Secondary.Rows);
         Assert.Empty(tab.SecondaryGrid);
         Assert.Contains("these two lists", tab.Introduction);
         Assert.DoesNotContain("secondary", tab.PreviewHint);
         Assert.DoesNotContain("secondary", tab.GlanceHint);
         Assert.DoesNotContain("secondary", tab.PictureHint);
         Assert.False(tab.OpenSecondaryTable.CanExecute(null));
         Assert.DoesNotContain("secondary", tab.HighlightNote);
      }

      [Fact]
      public void TheTab_StillEditsAndGrowsTheOldTables_AndKeepsTheOldCounters() {
         var tab = new CharacterCustomizationTab(ViewPort);

         tab.Clothes.Add.Execute(null);

         Assert.Equal(3, tab.Clothes.Rows.Count);
         Assert.Equal(3, Model[ClothesCount]);
         Assert.Equal("New colour", tab.Clothes.Rows[2].Name);
         tab.Undo.Execute(null);
         Assert.Equal(2, tab.Clothes.Rows.Count);
         Assert.Equal(2, Model[ClothesCount]);
      }
   }
}
