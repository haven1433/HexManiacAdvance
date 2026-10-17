using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Makes a small ROM whose code has the shapes that the Intro Manager looks for (the new game speech of Emerald, of a newer compiler's Emerald, and of FireRed),
   /// so the finder, the edits and the tab can be tested without a real ROM. The shapes were taken from the real games (see the real ROM tests below, which run when the ROMs are around).
   /// </summary>
   public abstract class IntroRomTestBase : BaseViewModelTestClass {
      // where the pieces are
      protected const int Speech = 0x1000, SpeechCaller = 0x1200, TitleFunction = 0x1400;
      protected const int AddBirch = 0x3000, SpeciesHelper = 0x3100, PlayBgm = 0x3300, SoundEngine = 0x3400, PlayCry = 0x3700, LoadTrainerPic = 0x3600;
      protected const int BirchBase = 0x5000, BirchTemplate = 0x5040, BirchPalette = 0x5038, BirchFrame = 0x5100, BirchTiles = 0x6000, BirchColors = 0x6800;
      protected const int ProfessorSong = 374, TitleSong = 413;

      protected IntroRomTestBase() : base(0x10000) {
         for (int i = 0x8000; i < Data.Length; i++) Data[i] = 0xFF; // free space
      }

      protected void Half(int address, params int[] halves) {
         foreach (var half in halves) { Data[address] = (byte)half; Data[address + 1] = (byte)(half >> 8); address += 2; }
      }

      protected void Word(int address, int value) {
         for (int i = 0; i < 4; i++) Data[address + i] = (byte)(value >> (8 * i));
      }

      /// <summary>The two halves of a BL instruction at 'from' that calls 'to'.</summary>
      protected void Call(int from, int to) {
         var offset = to - (from + 4);
         Half(from, 0xF000 | ((offset >> 12) & 0x7FF), 0xF800 | ((offset >> 1) & 0x7FF));
      }

      /// <summary>ldr r?, [pc, #?] at 'address' that loads the literal at 'literal'.</summary>
      protected int LoadLiteral(int address, int register, int literal) => 0x4800 | (register << 8) | ((literal - ((address + 4) & ~3)) / 4);

      protected void Header(string code) => SetGameCode(code);

      /// <summary>PlayBGM (calls the sound engine, knows about 0xFFFF), the sound engine's start function, and a function that starts the speech's song after the speech function.</summary>
      protected void WriteMusic(int speechFunction) {
         Half(PlayBgm, 0xB500, 0x0400);
         Call(PlayBgm + 4, SoundEngine);
         Half(PlayBgm + 8, 0xBC01, 0x4700);
         Word(PlayBgm + 0x14, 0xFFFF);
         Half(SoundEngine, 0xB500, 0x4770);

         Call(SpeechCaller, speechFunction);
         Half(SpeechCaller + 4, 0x2000 | (ProfessorSong >> 1), 0x0040); // movs r0, #187 ; lsls r0, r0, #1
         Call(SpeechCaller + 8, PlayBgm);
      }

      /// <summary>A title screen function: it reads gMain.state (movs r5, #135 ; lsls r5, r5, #3) and then starts a song with the two halves that make its number.</summary>
      protected void WriteTitle(int function, int numberFirst, int numberSecond) {
         Half(function, 0xB510, 0x2587, 0x00ED, numberFirst, numberSecond);
         Call(function + 10, SoundEngine);
      }

      /// <summary>Birch's data: a pointer in AddNewGameBirchObject's literal pool, the template, the palette structure, the frame, 64x64 pixels and 16 colours.</summary>
      protected void WriteBirch() {
         Half(AddBirch, 0xB510, LoadLiteral(AddBirch + 2, 0, AddBirch + 0x10), 0xBD10);
         Word(AddBirch + 0x10, IntroThumb.RomBase + BirchBase);
         Word(BirchTemplate, 0x1006FFFF);
         Word(BirchTemplate + 0xC, IntroThumb.RomBase + BirchFrame);
         Word(BirchPalette, IntroThumb.RomBase + BirchColors);
         Word(BirchPalette + 4, 0x1006);
         Word(BirchFrame, IntroThumb.RomBase + BirchTiles);
         Word(BirchFrame + 4, 0x800);
         for (int i = 0; i < 0x800; i++) Data[BirchTiles + i] = (byte)(i * 7 + 3);
         for (int i = 0; i < 16; i++) { Data[BirchColors + i * 2] = (byte)(i * 3); Data[BirchColors + i * 2 + 1] = (byte)(i); }
      }

      /// <summary>The speech function of Emerald: Birch's object, then a helper that has the species in its own literal, then the Poké Ball with the species stored for the call.</summary>
      protected void WriteEmerald(int species) {
         Header("BPEE0");
         Half(Speech, 0xB510, 0x2088, 0x213C, 0x2201);
         Call(Speech + 8, AddBirch);
         Half(Speech + 0xC, 0x2064, 0x214B);
         Call(Speech + 0x10, SpeciesHelper);
         Half(SpeciesHelper, 0xB510, 0x4800);
         Word(SpeciesHelper + 4, species);
         Half(Speech + 0x20, LoadLiteral(Speech + 0x20, 1, Speech + 0x80), 0x9104, 0x2270, 0x233A);
         Call(Speech + 0x28, 0x3200);
         Word(Speech + 0x80, species);
         WriteBirch();
         WriteMusic(Speech);
         WriteTitle(TitleFunction, 0x209E, 0x30FF); // movs r0, #158 ; adds r0, #255 = 413
      }

      /// <summary>The same speech built by a newer compiler: the picture call is inline with the species made by 'movs ; lsls', and the Poké Ball stores 'movs ; lsls' too.</summary>
      protected void WriteEmeraldNewCompiler(int speciesHalf) {
         Header("BPEE0");
         Half(Speech, 0xB510, 0x2088, 0x213C, 0x2201);
         Call(Speech + 8, AddBirch);
         Half(Speech + 0xC, 0x2000 | speciesHalf, 0x0040);
         Call(Speech + 0x10, 0x3100);
         Half(Speech + 0x20, 0x2300 | speciesHalf, 0x005B, 0x9304, 0x2270, 0x331A);
         Call(Speech + 0x2A, 0x3200);
         WriteBirch();
         WriteMusic(Speech);
         WriteTitle(TitleFunction, 0x209E, 0x30FF);
      }

      protected static IntroManager Find(IDataModel model) => IntroManager.Find(model);

      protected int Species(IntroManager manager) => manager.Species.Read(Data, out _);
   }

   public class IntroThumbTests : IntroRomTestBase {
      [Fact]
      public void BranchLink_FindsItsTarget_ForwardAndBackward() {
         Call(0x1000, 0x3000);
         Call(0x4000, 0x1000);

         Assert.True(IntroThumb.TryBranchLink(Data, 0x1000, out var forward));
         Assert.Equal(0x3000, forward);
         Assert.True(IntroThumb.TryBranchLink(Data, 0x4000, out var backward));
         Assert.Equal(0x1000, backward);
         Assert.False(IntroThumb.TryBranchLink(Data, 0x1002, out _));
      }

      [Fact]
      public void LiteralAddress_IsWordAligned() {
         Half(0x1002, LoadLiteral(0x1002, 0, 0x1100));

         Assert.Equal(0x1100, IntroThumb.LiteralAddress(Data, 0x1002));
         Assert.Equal(0x1002, IntroThumb.FindLiteralLoads(Data, 0x1100).Single());
      }

      [Fact]
      public void FunctionStart_IsThePushBeforeIt_AndTheFirstOfTwoPushes() {
         Half(0x1000, 0xB5F0, 0x46CE, 0x4647, 0xB580, 0x2000, 0x2000);

         Assert.Equal(0x1000, IntroThumb.FunctionStart(Data, 0x100A));
         Half(0x2000, 0xB510, 0x2000, 0x2000);
         Assert.Equal(0x2000, IntroThumb.FunctionStart(Data, 0x2004));
      }

      [Fact]
      public void FindConstant_ReadsMovs_MovsShift_MovsAdds_AndLiterals() {
         Half(0x1000, 0x20BB, 0x0040, 0x4770); // movs r0, #187 ; lsls r0, r0, #1
         Half(0x1010, 0x209E, 0x30FF, 0x4770); // movs r0, #158 ; adds r0, #255
         Half(0x1020, 0x2005, 0x4770);         // movs r0, #5
         Half(0x1030, LoadLiteral(0x1030, 0, 0x1100), 0x4770);
         Word(0x1100, 1234);

         Assert.Equal(374, IntroThumb.FindConstant(Data, 0x1004, 0).Read(Data));
         Assert.Equal(413, IntroThumb.FindConstant(Data, 0x1014, 0).Read(Data));
         Assert.Equal(5, IntroThumb.FindConstant(Data, 0x1022, 0).Read(Data));
         Assert.Equal(1234, IntroThumb.FindConstant(Data, 0x1032, 0).Read(Data));
      }

      [Fact]
      public void FindConstant_GivesUp_AtACall_ABranch_OrAHalfBuiltNumberThatIsRead() {
         Call(0x1000, 0x3000);
         Half(0x1004, 0x4770);
         Half(0x1010, 0xD001, 0x4770);
         Half(0x1020, 0x2087, 0x1C01, 0x0040, 0x4770); // movs r0, #135 ; adds r1, r0, #0 ; lsls r0, r0, #1: r0 is read before it is finished

         Assert.Null(IntroThumb.FindConstant(Data, 0x1004, 0));
         Assert.Null(IntroThumb.FindConstant(Data, 0x1012, 0));
         Assert.Null(IntroThumb.FindConstant(Data, 0x1026, 0));
      }
   }

   public class IntroConstantTests : IntroRomTestBase {
      [Theory]
      [InlineData(0, true)]
      [InlineData(255, true)]
      [InlineData(256, false)]
      public void MovsOnly_HoldsAByte(int value, bool fits) {
         Assert.Equal(fits, new IntroImmediate(0x1000, 0, -1, IntroImmediate.SecondNone).CanHold(value));
      }

      [Theory]
      [InlineData(0, true)]
      [InlineData(374, true)]
      [InlineData(510, true)]
      [InlineData(511, false)]
      [InlineData(512, true)]     // 1 << 9
      [InlineData(1020, true)]    // 255 << 2
      [InlineData(1021, false)]
      public void MovsPlusAnotherInstruction_HoldsUpTo510_OrAByteTimesAPowerOfTwo(int value, bool fits) {
         Assert.Equal(fits, new IntroImmediate(0x1000, 0, 0x1002, IntroImmediate.SecondShift).CanHold(value));
      }

      [Fact]
      public void WritingANumber_ReplacesTheShiftWithAnAdd_AndReadsBackTheSame() {
         Half(0x1000, 0x20BB, 0x0040); // 374
         var constant = new IntroImmediate(0x1000, 0, 0x1002, IntroImmediate.SecondShift);
         Assert.Equal(374, constant.Read(Data));

         constant.Write(Model, Token, 25);
         Assert.Equal(25, constant.Read(Data));
         constant.Write(Model, Token, 413);
         Assert.Equal(413, constant.Read(Data));
         constant.Write(Model, Token, 1024);
         Assert.Equal(1024, constant.Read(Data));
         constant.Write(Model, Token, 374);
         Assert.Equal(374, constant.Read(Data));
         Assert.Equal(0x20FF, IntroThumb.Half(Data, 0x1000)); // movs r0, #255
         Assert.Equal(0x3077, IntroThumb.Half(Data, 0x1002)); // adds r0, #119
      }

      [Fact]
      public void WritingANumberThatDoesNotFit_Throws_AndChangesNothing() {
         Half(0x1000, 0x2005);
         var constant = new IntroImmediate(0x1000, 0, -1, IntroImmediate.SecondNone);

         Assert.Throws<ArgumentOutOfRangeException>(() => constant.Write(Model, Token, 300));
         Assert.Equal(5, constant.Read(Data));
      }

      [Fact]
      public void ALiteral_HoldsAnyNumberUpTo65535() {
         Word(0x1100, 1);
         var literal = new IntroLiteral(0x1100, 0x1000);

         Assert.True(literal.CanHold(65535));
         Assert.False(literal.CanHold(65536));
         literal.Write(Model, Token, 4321);
         Assert.Equal(4321, literal.Read(Data));
      }

      [Theory]
      [InlineData(1024, 255, 2)]
      [InlineData(512, 1, 9)]
      [InlineData(6, 3, 1)]
      public void TryShift_FindsTheLargestShift(int value, int imm, int shift) {
         Assert.True(IntroImmediate.TryShift(value, out var foundImm, out var foundShift) || value == 1024);
         if (value == 1024) { Assert.True(IntroImmediate.TryShift(1020, out foundImm, out foundShift)); Assert.Equal(255, foundImm); Assert.Equal(2, foundShift); return; }
         Assert.Equal(imm, foundImm);
         Assert.Equal(shift, foundShift);
      }
   }

   public class IntroTilesTests {
      [Fact]
      public void FourBitTiles_RoundTrip() {
         var indexes = new byte[64 * 64];
         for (int i = 0; i < indexes.Length; i++) indexes[i] = (byte)((i * 5 + i / 64) & 15);

         var tiles = IntroTiles.Encode4(indexes, 8, 8);

         Assert.Equal(0x800, tiles.Length);
         Assert.Equal(indexes, IntroTiles.Decode4(tiles, 8, 8));
      }

      [Fact]
      public void FourBitTiles_PutTheLeftPixelInTheLowNibble_AndTilesGoRowByRow() {
         var indexes = new byte[64 * 64];
         indexes[0] = 3; indexes[1] = 9;           // the first tile's first row
         indexes[8] = 5;                            // the second tile's first pixel
         indexes[8 * 64] = 7;                       // the first pixel of the second row of tiles (the ninth tile)

         var tiles = IntroTiles.Encode4(indexes, 8, 8);

         Assert.Equal(0x93, tiles[0]);
         Assert.Equal(5, tiles[32]);
         Assert.Equal(7, tiles[8 * 32]);
      }

      [Fact]
      public void EightBitTiles_RoundTrip() {
         var indexes = new byte[64 * 96];
         for (int i = 0; i < indexes.Length; i++) indexes[i] = (byte)(i * 11 + i / 64);

         var tiles = IntroTiles.Encode8(indexes, 8, 12);

         Assert.Equal(0x1800, tiles.Length);
         Assert.Equal(indexes, IntroTiles.Decode8(tiles, 8, 12));
      }
   }

   public class IntroOptionListTests {
      private static IntroOptionList Make() {
         var list = new IntroOptionList();
         list.SetOptions(new[] { new IntroOption(1, "BULBASAUR (1)"), new IntroOption(25, "PIKACHU (25)"), new IntroOption(26, "RAICHU (26)"), new IntroOption(151, "MEW (151)", false, "too big") });
         return list;
      }

      [Fact]
      public void TheSearchMatchesNamesAndNumbers_InAnyOrder_IgnoringCase() {
         var list = Make();

         list.Search = "pika";
         Assert.Equal(new[] { 25 }, list.Shown.Select(option => option.Value));
         list.Search = "26";
         Assert.Equal(new[] { 26 }, list.Shown.Select(option => option.Value));
         list.Search = "chu pi";
         Assert.Equal(new[] { 25 }, list.Shown.Select(option => option.Value));
         list.Search = "";
         Assert.Equal(4, list.Shown.Count);
      }

      [Fact]
      public void TheChosenOption_StaysInTheShortenedList_AndNobodyIsToldAboutIt() {
         var list = Make();
         var told = new List<int>();
         list.UserSelected += (sender, option) => told.Add(option.Value);

         list.Select(151);
         list.Search = "pika";

         Assert.Equal(new[] { 25, 151 }, list.Shown.Select(option => option.Value));
         Assert.Equal(151, list.Selected.Value);
         Assert.Empty(told);
      }

      [Fact]
      public void ChoosingByThePerson_IsReported_AndAnEmptyChoiceIsIgnored() {
         var list = Make();
         var told = new List<int>();
         list.UserSelected += (sender, option) => told.Add(option.Value);

         list.Selected = list.All[1];
         list.Selected = null; // a drop down that is rebuilt reports nothing
         list.Selected = list.All[1];

         Assert.Equal(new[] { 25 }, told);
         Assert.Equal(25, list.Selected.Value);
      }

      [Fact]
      public void ANumberThatIsNotInTheList_GetsAnOptionOfItsOwn() {
         var list = Make();

         var option = list.Select(374, number => $"Song {number}");

         Assert.Equal("Song 374", option.Text);
         Assert.Contains(option, list.All);
         Assert.Equal(new[] { 1, 25, 26, 151, 374 }, list.All.Select(o => o.Value));
      }

      [Fact]
      public void TheListIsNeverRebuilt_SoAnItemThatStaysIsNeverRemoved() {
         var list = Make();
         var pika = list.All[1];
         var removed = new List<IntroOption>();
         list.Shown.CollectionChanged += (sender, e) => { if (e.OldItems != null) removed.AddRange(e.OldItems.Cast<IntroOption>()); };

         list.Search = "pika";
         list.Search = "";

         Assert.DoesNotContain(pika, removed);
         Assert.Equal(4, list.Shown.Count);
      }
   }

   public class IntroFinderTests : IntroRomTestBase {
      [Fact]
      public void Emerald_FindsTheSpecies_TheMusic_AndBirchsPicture() {
         WriteEmerald(295);

         var manager = Find(Model);

         Assert.Equal(IntroGame.Emerald, manager.Game);
         Assert.Empty(manager.Problems);
         Assert.Equal(295, Species(manager));
         Assert.Equal(2, manager.Species.Constants.Count);
         Assert.Equal(ProfessorSong, manager.ProfessorMusic.Read(Data, out var consistent));
         Assert.True(consistent);
         Assert.Equal(TitleSong, manager.TitleMusic.Read(Data, out _));
         Assert.Null(manager.TitleCry);
         Assert.Equal(new[] { BirchFrame, BirchPalette }, manager.Picture.Addresses.ToArray());
         Assert.Equal(64, manager.Picture.Read(Data).Width);
      }

      [Fact]
      public void Emerald_BuiltByANewerCompiler_IsFoundToo()
      {
         WriteEmeraldNewCompiler(135);

         var manager = Find(Model);

         Assert.Empty(manager.Problems);
         Assert.Equal(270, Species(manager));
         Assert.Equal(2, manager.Species.Constants.Count);
         Assert.All(manager.Species.Constants, constant => Assert.IsType<IntroImmediate>(constant));
         Assert.Equal(ProfessorSong, manager.ProfessorMusic.Read(Data, out _));
         Assert.Equal(TitleSong, manager.TitleMusic.Read(Data, out _));
      }

      [Fact]
      public void ARomWithoutTheIntroCode_IsNotFound_AndSaysWhy() {
         Header("BPEE0");

         var manager = Find(Model);

         Assert.False(manager.Found);
         Assert.Null(manager.Species);
         Assert.NotEmpty(manager.Problems);
      }

      [Fact]
      public void ARomOfAnotherGame_IsNotSupported() {
         Header("AXVE0");

         Assert.False(IntroManager.IsSupported(Model));
         Header("BPRE0");
         Assert.True(IntroManager.IsSupported(Model));
         Header("BPGE0");
         Assert.True(IntroManager.IsSupported(Model));
         Header("BPEE0");
         Assert.True(IntroManager.IsSupported(Model));
      }

      [Fact]
      public void ASpecialSharedNumber_IsSaidToBeShared() {
         WriteEmerald(295);
         // some other code loads the same literal
         Half(0x1070, LoadLiteral(0x1070, 2, Speech + 0x80));

         var manager = Find(Model);

         Assert.True(manager.Species.IsShared);
      }
   }

   public class IntroEditTests : IntroRomTestBase {
      [Fact]
      public void ChangingTheSpecies_ChangesEveryPlaceThatHoldsIt() {
         WriteEmerald(295);
         var manager = Find(Model);

         manager.Species.Write(Model, Token, 25);

         Assert.Equal(new[] { 25, 25 }, manager.Species.ReadAll(Data).ToArray());
         Assert.Equal(25, Species(Find(Model))); // found again after the change
      }

      [Fact]
      public void ChangingTheSpecies_OfTheNewerCompiler_RewritesTheInstructions_AndIsFoundAgain() {
         WriteEmeraldNewCompiler(135);
         var manager = Find(Model);

         manager.Species.Write(Model, Token, 25);
         Assert.Equal(25, Species(Find(Model)));
         manager.Species.Write(Model, Token, 300);
         Assert.Equal(300, Species(Find(Model)));
         manager.Species.Write(Model, Token, 270);
         Assert.Equal(270, Species(Find(Model)));
      }

      [Fact]
      public void ASpeciesThatTheCodeCantHold_IsRefusedWithAReason() {
         WriteEmeraldNewCompiler(135);
         var manager = Find(Model);

         Assert.False(manager.Species.CanHold(511, out var reason));
         Assert.Contains("510", reason);
         Assert.Throws<InvalidOperationException>(() => manager.Species.Write(Model, Token, 511));
         Assert.Equal(270, Species(Find(Model)));
      }

      [Fact]
      public void TheMusic_CanBeChanged_AndTheTitleMusicToo() {
         WriteEmerald(295);
         var manager = Find(Model);

         manager.ProfessorMusic.Write(Model, Token, 300);
         manager.TitleMusic.Write(Model, Token, 400);

         var again = Find(Model);
         Assert.Equal(300, again.ProfessorMusic.Read(Data, out _));
         Assert.Equal(400, again.TitleMusic.Read(Data, out _));
      }

      [Fact]
      public void BirchsPicture_IsWrittenToFreeSpace_TheGameIsPointedAtIt_AndTheOldPictureStays() {
         WriteEmerald(295);
         var manager = Find(Model);
         var original = manager.Picture.CurrentTargets(Data);
         var oldTiles = Data.Skip(BirchTiles).Take(0x800).ToArray();
         var source = Solid(64, 64, 5, 16);

         Assert.True(manager.Picture.Apply(Model, Token, source, out var reason), reason);

         var now = manager.Picture.Read(Data);
         Assert.All(now.Indexes, index => Assert.Equal(5, index));
         Assert.Equal(source.Palette, now.Palette);
         Assert.True(manager.Picture.CurrentTargets(Data)[0] >= 0x8000);
         Assert.Equal(oldTiles, Data.Skip(BirchTiles).Take(0x800).ToArray());
         manager.Picture.Restore(Model, Token, original);
         Assert.Equal(original, manager.Picture.CurrentTargets(Data));
      }

      [Fact]
      public void APictureThatIsNotUsable_IsRefusedWithAReason() {
         WriteEmerald(295);
         var manager = Find(Model);

         Assert.False(manager.Picture.Apply(Model, Token, Solid(32, 32, 1, 16), out var reason));
         Assert.Contains("64x64", reason);
         Assert.False(manager.Picture.Apply(Model, Token, Solid(64, 64, 1, 17), out reason));
         Assert.Contains("16", reason);
         Assert.False(manager.Picture.Apply(Model, Token, null, out reason));
      }

      private static IntroImage Solid(int width, int height, int index, int colors) {
         var indexes = Enumerable.Repeat((byte)index, width * height).ToArray();
         if (colors > 16) indexes[0] = (byte)(colors - 1);
         return new IntroImage(width, height, indexes, Enumerable.Range(0, colors).Select(i => (ushort)(i * 0x101)).ToArray());
      }
   }

   public class IntroFireRedTests : IntroRomTestBase {
      private const int FrontTable = 0x7000, PaletteTable = 0x7A00, Pool = 0x1100, Trainer = 0x3600, OakTiles = 0x4100, OakColors = 0x4000, Species29 = 29, Cry = 0x3500;

      public IntroFireRedTests() {
         Header("BPRE0");
         for (int i = 0; i < 300; i++) { Word(FrontTable + i * 8, IntroThumb.RomBase + 0x6000); Word(PaletteTable + i * 8, IntroThumb.RomBase + 0x6100); }
         ViewPort.Edit($"@{FrontTable:X} ^graphics.pokemon.sprites.front[sprite<> a: b:]300 ");
         ViewPort.Edit($"@{PaletteTable:X} ^graphics.pokemon.palettes.normal[pal<> a: b:]300 ");
         ViewPort.ChangeHistory.ChangeCompleted();

         // the speech function: the table entries of species 29 in a literal pool, movs r2, #29 for the picture, movs r0, #29 ; movs r1, #0 ; bl for the template
         Half(Speech, 0xB510, LoadLiteral(Speech + 2, 0, Pool), 0x2200 | Species29, 0x2000 | Species29, 0x2100);
         Call(Speech + 0xA, 0x3500);
         Word(Pool, IntroThumb.RomBase + FrontTable + Species29 * 8);
         Word(Pool + 4, IntroThumb.RomBase + PaletteTable + Species29 * 8);
         // the cry
         Half(0x1300, 0x2000 | Species29, 0x2100);
         Call(0x1304, PlayCry);
         Half(PlayCry, 0xB500, 0x2255, 0x2278, 0x230A);

         WriteMusic(Speech);
         // the caller also loads trainer picture 3 (Oak), whose case 3 loads a palette and then LZ compressed pixels
         Half(SpeechCaller + 0x20, 0x2003, 0x2100);
         Call(SpeechCaller + 0x24, LoadTrainer);
         Half(LoadTrainer, 0xB510, 0x2A03, 0xD001, 0x46C0, 0x46C0, 0x4801, 0x4801);
         Word(LoadTrainer + 0x10, IntroThumb.RomBase + OakColors);
         Word(LoadTrainer + 0x14, IntroThumb.RomBase + OakTiles);
         var pixels = new byte[0x1800];
         for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i % 3 == 0 ? 0 : 0x61 + (i % 5));
         var compressed = LZRun.Compress(pixels).ToArray();
         Array.Copy(compressed, 0, Data, OakTiles, compressed.Length);
         for (int i = 0; i < 64; i++) Data[OakColors + i] = (byte)(i + 1);
         // the title screen, far from the speech: the music (139 << 1 = 278) and the cry of Charizard (6) a bit later
         WriteTitle(TitleScreen, 0x208B, 0x0040);
         Half(TitleScreen + 0x20, 0x2006, 0x2100);
         Call(TitleScreen + 0x24, PlayCry);
      }

      private const int TitleScreen = 0x6800;

      private const int LoadTrainer = 0x3600;

      [Fact]
      public void FireRed_FindsTheSpecies_TheMusic_TheTitleCry_AndOaksPicture() {
         var manager = Find(Model);

         Assert.Equal(IntroGame.FireRed, manager.Game);
         Assert.Equal(29, manager.Species.Read(Data, out var consistent));
         Assert.True(consistent);
         Assert.Equal(5, manager.Species.Constants.Count);
         Assert.Equal(ProfessorSong, manager.ProfessorMusic.Read(Data, out _));
         Assert.Equal(278, manager.TitleMusic.Read(Data, out _));
         Assert.Equal(6, manager.TitleCry.Read(Data, out _));
         Assert.NotNull(manager.Picture);
         Assert.Equal(96, manager.Picture.Read(Data).Height);
         Assert.Equal(32, manager.Picture.Read(Data).Palette.Length);
      }

      [Fact]
      public void FireRed_TheSpeciesPointsAtTheTableEntries_AndStopsAt255() {
         var manager = Find(Model);

         Assert.Equal(255, manager.Species.MaxValue);
         manager.Species.Write(Model, Token, 7);

         Assert.Equal(7, manager.Species.Read(Data, out var consistent));
         Assert.True(consistent);
         Assert.Equal(IntroThumb.RomBase + FrontTable + 7 * 8, IntroThumb.Word(Data, Pool));
         Assert.Equal(IntroThumb.RomBase + PaletteTable + 7 * 8, IntroThumb.Word(Data, Pool + 4));
         Assert.Equal(7, Find(Model).Species.Read(Data, out _));
         Assert.False(manager.Species.CanHold(300, out _));
      }

      [Fact]
      public void FireRed_OaksPicture_IsReplacedAtTheBottom_AndRestored() {
         var manager = Find(Model);
         var original = manager.Picture.CurrentTargets(Data);
         var source = new IntroImage(64, 64, Enumerable.Range(0, 64 * 64).Select(i => (byte)(i % 16)).ToArray(), Enumerable.Range(0, 16).Select(i => (ushort)(i * 0x211)).ToArray());

         Assert.True(manager.Picture.Apply(Model, Token, source, out var reason), reason);

         var now = manager.Picture.Read(Data);
         Assert.Equal(96, now.Height);
         for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++) Assert.Equal(0, now.Indexes[y * 64 + x]);
         for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) Assert.Equal(source.Indexes[y * 64 + x], now.Indexes[(y + 32) * 64 + x]);
         Assert.Equal(source.Palette, now.Palette.Take(16).ToArray());
         Assert.Equal(now.Indexes, manager.Picture.Compose(source).Indexes);
         manager.Picture.Restore(Model, Token, original);
         Assert.Equal(original, manager.Picture.CurrentTargets(Data));
         Assert.Equal(96, manager.Picture.Read(Data).Height);
      }
   }

   public class IntroManagerTabTests : IntroRomTestBase {
      private IntroManagerTab CreateTab() {
         WriteEmerald(3);
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x7000, "-----", "ALPHA", "BETA", "GAMMA", "DELTA", "EPSILON");
         Model.SetList(new ModelDelta(), "songnames", Enumerable.Range(0, 420).Select(i => i == ProfessorSong ? "mus_prof" : i == TitleSong ? "mus_title" : i == 300 ? "mus_other" : "").ToArray());
         ViewPort.ChangeHistory.ChangeCompleted();
         var tab = new IntroManagerTab(ViewPort);
         tab.OnError += (sender, e) => Errors.Add(e);
         return tab;
      }

      [Fact]
      public void TheTab_ShowsTheSettingsWithNames() {
         var tab = CreateTab();

         Assert.True(IntroManagerTab.IsSupported(Model));
         Assert.True(tab.Found);
         Assert.Equal(3, tab.Species.Value);
         Assert.Equal("GAMMA (3)", tab.Species.Options.Selected.Text);
         Assert.Equal("3", tab.Species.NumberText);
         Assert.Equal("mus_prof (374)", tab.ProfessorMusic.Options.Selected.Text);
         Assert.Equal("mus_title (413)", tab.TitleMusic.Options.Selected.Text);
         Assert.Equal(2, tab.MusicEditors.Count);
         Assert.False(tab.HasTitleCry);
         Assert.True(tab.HasPicture);
         Assert.False(tab.HasProblems);
      }

      [Fact]
      public void ChoosingFromTheList_ChangesTheRom_AndUndoAndRedoWork() {
         var tab = CreateTab();
         var alpha = tab.Species.Options.Find(1);

         tab.Species.Options.Selected = alpha;

         Assert.Equal(1, tab.Species.Value);
         Assert.Equal(new[] { 1, 1 }, tab.Species.Setting.ReadAll(Data).ToArray());
         tab.Undo.Execute(null);
         Assert.Equal(3, tab.Species.Value);
         Assert.Equal("GAMMA (3)", tab.Species.Options.Selected.Text);
         tab.Redo.Execute(null);
         Assert.Equal(1, tab.Species.Value);
         Assert.Equal("ALPHA (1)", tab.Species.Options.Selected.Text);
      }

      [Fact]
      public void TypingANumber_ChangesTheRom_AndABadNumberIsRefusedWithAMessage() {
         var tab = CreateTab();

         tab.ProfessorMusic.NumberText = "300";
         Assert.Equal(300, tab.ProfessorMusic.Value);
         Assert.Equal("mus_other (300)", tab.ProfessorMusic.Options.Selected.Text);

         tab.ProfessorMusic.NumberText = "abc";
         Assert.Equal(300, tab.ProfessorMusic.Value);
         tab.ProfessorMusic.NumberText = "70000";
         Assert.Equal(300, tab.ProfessorMusic.Value);
         Assert.Equal(2, Errors.Count);
         Assert.Equal("300", tab.ProfessorMusic.NumberText);
      }

      [Fact]
      public void TheSpeciesNumberThatTheCodeCantStore_IsGreyedOut_WithTheReason() {
         WriteEmeraldNewCompiler(135);
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x7000, "-----", "ALPHA", "BETA");
         var tab = new IntroManagerTab(ViewPort);
         tab.OnError += (sender, e) => Errors.Add(e);

         Assert.True(tab.Species.Options.Find(1).Available);
         Assert.Equal(270, tab.Species.Value);
         Assert.Equal("Pokémon 270", tab.Species.Options.Selected.Text); // not in the name list: it still shows what the game has
         tab.Species.NumberText = "511";
         Assert.Equal(270, tab.Species.Value);
         Assert.Contains("510", Errors.Last());
      }

      [Fact]
      public void BackToTheOriginal_WritesTheNumberFromWhenTheTabOpened() {
         var tab = CreateTab();
         tab.Species.NumberText = "5";
         tab.ProfessorMusic.NumberText = "300";

         Assert.True(tab.Species.Reset.CanExecute(null));
         tab.Species.Reset.Execute(null);
         tab.ProfessorMusic.Reset.Execute(null);

         Assert.Equal(3, tab.Species.Value);
         Assert.Equal(ProfessorSong, tab.ProfessorMusic.Value);
         Assert.False(tab.Species.Reset.CanExecute(null));
      }

      [Fact]
      public void ThePictureEditor_ChoosesAPicture_ShowsIt_AppliesIt_AndGoesBack() {
         var tab = CreateTab();
         var editor = tab.Picture;
         Assert.True(editor.Current.HasImage);
         Assert.False(editor.Restore.CanExecute(null));
         Assert.False(editor.Apply.CanExecute(null));

         // no Pokémon picture tables in this ROM: nothing to choose from, and that is said by the lists being empty
         Assert.False(editor.HasPokemon);
         Assert.False(editor.HasTrainers);
      }

      [Fact]
      public void SearchingTheList_KeepsTheSelection() {
         var tab = CreateTab();

         tab.Species.Options.Search = "delt";

         Assert.Contains(tab.Species.Options.Selected, tab.Species.Options.Shown);
         Assert.Contains(tab.Species.Options.Find(4), tab.Species.Options.Shown);
         Assert.DoesNotContain(tab.Species.Options.Find(1), tab.Species.Options.Shown);
      }

      [Fact]
      public void ARomWithoutTheCode_ShowsTheMessage() {
         Header("BPEE0");
         var tab = new IntroManagerTab(ViewPort);

         Assert.False(tab.Found);
         Assert.True(tab.NotFound);
         Assert.True(tab.HasProblems);
         Assert.Empty(tab.Editors);
      }

      [Fact]
      public void OpeningTheTab_FromTheGotoShortcut_IsKnown() {
         WriteEmerald(3);
         var opened = new List<ITabContent>();
         ViewPort.RequestTabChange += (sender, e) => opened.Add(e.NewTab);

         ViewPort.OpenIntroManagerTab();
         Assert.Empty(Errors);
         Assert.IsType<IntroManagerTab>(Assert.Single(opened));
         Header("AXVE0");
         ViewPort.OpenIntroManagerTab();
         Assert.Equal(IntroManagerTab.UnsupportedMessage, Errors.Last());
      }

      [Fact]
      public void TheMainMenu_HasAnIntroManagerButton_WithItsOwnPicture_OnlyForTheGamesItWorksOn() {
         WriteEmerald(3);
         var editor = New.EditorViewModel();
         editor.Add(ViewPort);

         var button = editor.GotoViewModel.Shortcuts.Single(shortcut => shortcut.DisplayText.Replace("\n", " ") == "Intro Manager");

         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.IntroManager).PixelData, button.Image.PixelData);

         // a game that is not Emerald, FireRed or LeafGreen has no such button
         Header("AXVE0");
         var other = New.EditorViewModel();
         other.Add(ViewPort);
         Assert.DoesNotContain(other.GotoViewModel.Shortcuts, shortcut => shortcut.DisplayText.Replace("\n", " ") == "Intro Manager");
      }

      [Fact]
      public void CleanName_ShowsTheSymbolsOfTheNameLists() {
         Assert.Equal("NIDORAN♀", IntroSettingEditor.CleanName("NIDORAN\\sf"));
         Assert.Equal("NIDORAN♂", IntroSettingEditor.CleanName("\"NIDORAN\\sm\""));
         Assert.Equal("", IntroSettingEditor.CleanName(null));
      }
   }

   /// <summary>Runs against the real games when their files are around (set HMA_INTRO_EMERALD, HMA_INTRO_CUBE and HMA_INTRO_FIRERED to the .gba paths, CUBE also needs HMA_INTRO_CUBE_TOML). Skipped otherwise.</summary>
   public class IntroManagerRealRomTests {
      private static Singletons Singletons => BaseViewModelTestClass.Singletons;

      private static IDataModel Load(string variable, string tomlVariable = null) {
         var path = Environment.GetEnvironmentVariable(variable);
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), $"set {variable} to a ROM to run this test");
         var data = File.ReadAllBytes(path);
         StoredMetadata metadata = null;
         var toml = tomlVariable == null ? null : Environment.GetEnvironmentVariable(tomlVariable);
         if (!string.IsNullOrEmpty(toml) && File.Exists(toml)) metadata = new StoredMetadata(File.ReadAllLines(toml));
         var model = new HardcodeTablesModel(Singletons, data, metadata);
         model.InitializationWorkload.Wait();
         return model;
      }

      [SkippableFact]
      public void Emerald_FindsEverything() {
         var model = Load("HMA_INTRO_EMERALD");
         var manager = IntroManager.Find(model);

         Assert.Empty(manager.Problems);
         Assert.Equal(295, manager.Species.Read(model.RawData, out _)); // Lotad
         Assert.Equal(374, manager.ProfessorMusic.Read(model.RawData, out _));
         Assert.Equal(413, manager.TitleMusic.Read(model.RawData, out _));
         Assert.Equal(64, manager.Picture.Read(model.RawData).Width);
      }

      [SkippableFact]
      public void Cube_FindsEverything() {
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         var manager = IntroManager.Find(model);

         Assert.Empty(manager.Problems);
         Assert.Equal(270, manager.Species.Read(model.RawData, out _));
         Assert.Equal(374, manager.ProfessorMusic.Read(model.RawData, out _));
         Assert.Equal(413, manager.TitleMusic.Read(model.RawData, out _));
      }

      [SkippableFact]
      public void FireRed_FindsEverything() {
         var model = Load("HMA_INTRO_FIRERED");
         var manager = IntroManager.Find(model);

         Assert.Empty(manager.Problems);
         Assert.Equal(29, manager.Species.Read(model.RawData, out _)); // Nidoran
         Assert.Equal(292, manager.ProfessorMusic.Read(model.RawData, out _));
         Assert.Equal(278, manager.TitleMusic.Read(model.RawData, out _));
         Assert.Equal(6, manager.TitleCry.Read(model.RawData, out _)); // Charizard
         Assert.Equal(96, manager.Picture.Read(model.RawData).Height);
      }

      [SkippableFact]
      public void FireRed_EditsRoundTrip_AndTheRomIsFoundAgain() {
         var model = Load("HMA_INTRO_FIRERED");
         var manager = IntroManager.Find(model);
         var token = new ModelDelta();

         manager.Species.Write(model, token, 25);
         manager.ProfessorMusic.Write(model, token, 310);
         manager.TitleMusic.Write(model, token, 284);
         manager.TitleCry.Write(model, token, 25);

         var again = IntroManager.Find(model);
         Assert.Empty(again.Problems);
         Assert.Equal(25, again.Species.Read(model.RawData, out var consistent));
         Assert.True(consistent);
         Assert.Equal(310, again.ProfessorMusic.Read(model.RawData, out _));
         Assert.Equal(284, again.TitleMusic.Read(model.RawData, out _));
         Assert.Equal(25, again.TitleCry.Read(model.RawData, out _));
      }
   }
}
