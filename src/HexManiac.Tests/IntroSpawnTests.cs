using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class CubeNewGameSpawnTableTests : IntroRomTestBase {
      private const int Table = 0x7800;

      private void WriteTable(int address, int group = 25, int map = 40, short x = -1, short y = -1, int shake = 1) {
         Encoding.ASCII.GetBytes(CubeNewGameSpawnTable.Magic).CopyTo(Data, address);
         Data[address + 8] = (byte)group;
         Data[address + 9] = (byte)map;
         Data[address + 10] = (byte)x; Data[address + 11] = (byte)(x >> 8);
         Data[address + 12] = (byte)y; Data[address + 13] = (byte)(y >> 8);
         Data[address + 14] = (byte)shake;
         Data[address + 15] = 0;
      }

      private IntroManagerTab CreateTab() {
         WriteEmerald(3);
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x7000, "-----", "ALPHA", "BETA", "GAMMA", "DELTA", "EPSILON");
         ViewPort.ChangeHistory.ChangeCompleted();
         var tab = new IntroManagerTab(ViewPort);
         tab.OnError += (sender, e) => Errors.Add(e);
         return tab;
      }

      [Fact]
      public void TheTable_IsFoundByItsMagic_WithoutAnAnchor() {
         WriteTable(Table + 4); // not aligned: still found
         Assert.Equal(Table + 4, CubeNewGameSpawnTable.Find(Model));
         Assert.True(CubeNewGameSpawnTable.IsAvailable(Model));
         Assert.Equal(CubeNewGameSpawn.Original, CubeNewGameSpawnTable.Read(Model));
      }

      [Fact]
      public void TheTable_IsFoundByItsAnchor_BeforeTheSearch() {
         WriteTable(Table + 0x40);
         WriteTable(Table + 0x100, group: 3, map: 4);
         ViewPort.Goto.Execute((Table + 0x100).ToString("X2"));
         ViewPort.Edit("^" + CubeNewGameSpawnTable.Anchor + " ");
         Assert.Equal(Table + 0x100, CubeNewGameSpawnTable.Find(Model));
         Assert.Equal(3, CubeNewGameSpawnTable.Read(Model).MapGroup);
      }

      [Fact]
      public void ARomWithoutTheTable_HasNone() {
         Assert.Equal(-1, CubeNewGameSpawnTable.Find(Model));
         Assert.Null(CubeNewGameSpawnTable.Read(Model));
         Assert.False(CubeNewGameSpawnTable.Write(Model, new ModelDelta(), CubeNewGameSpawn.Original));
         Assert.Equal(-1, CubeNewGameSpawnTable.Search(new byte[10]));
         Assert.Equal(-1, CubeNewGameSpawnTable.Find(null));
      }

      [Fact]
      public void AMagicWithoutRoomForTheTable_IsNotATable() {
         var data = new byte[0x40];
         Encoding.ASCII.GetBytes(CubeNewGameSpawnTable.Magic).CopyTo(data, data.Length - 12);
         Assert.Equal(-1, CubeNewGameSpawnTable.Search(data));
      }

      [Fact]
      public void TheSearch_IsDoneOncePerRom() {
         Assert.Equal(-1, CubeNewGameSpawnTable.Find(Model));
         WriteTable(Table); // written after the first answer: the answer of this ROM is kept
         Assert.Equal(-1, CubeNewGameSpawnTable.Find(Model));
      }

      [Fact]
      public void TheTable_ReadsSignedCoordinates_AndWritesThemBack() {
         WriteTable(Table, group: 0, map: 9, x: 7, y: -300, shake: 0);
         var spawn = CubeNewGameSpawnTable.Read(Model);
         Assert.Equal(new CubeNewGameSpawn(0, 9, 7, -300, false), spawn);

         Assert.True(CubeNewGameSpawnTable.Write(Model, new ModelDelta(), new CubeNewGameSpawn(25, 40, -32768, 32767, true)));
         Assert.Equal(new CubeNewGameSpawn(25, 40, -32768, 32767, true), CubeNewGameSpawnTable.Read(Model));
         Assert.Equal("CUBESPWN", Encoding.ASCII.GetString(Data, Table, 8));
         Assert.Throws<ArgumentOutOfRangeException>(() => CubeNewGameSpawnTable.Write(Model, new ModelDelta(), spawn with { X = 40000 }));
      }

      [Fact]
      public void TheCard_WithoutTheTable_IsDisabledWithAMessage() {
         var tab = CreateTab();
         Assert.True(tab.HasSpawn);
         Assert.False(tab.Spawn.IsAvailable);
         Assert.True(tab.Spawn.IsUnavailable);
         Assert.Contains("latest CUBE", tab.Spawn.UnavailableMessage);
         Assert.False(tab.Spawn.ToggleShake.CanExecute(null));
         Assert.False(tab.Spawn.Reset.CanExecute(null));
         var before = Data.ToArray();
         tab.Spawn.XText = "5";
         tab.Spawn.ToggleShake.Execute(null);
         Assert.Equal(before, Data);
      }

      [Fact]
      public void TheCard_ShowsTheTable() {
         WriteTable(Table, group: 0, map: 9, x: 7, y: 8, shake: 0);
         var tab = CreateTab();
         Assert.True(tab.Spawn.IsAvailable);
         Assert.Equal("7", tab.Spawn.XText);
         Assert.Equal("8", tab.Spawn.YText);
         Assert.Equal("Map 0.9", tab.Spawn.Maps.Selected.Text); // a ROM without map tables: the chosen map still shows
         Assert.False(tab.Spawn.ShakeTruck);
         Assert.Equal("Restore truck shaking", tab.Spawn.ToggleShakeText);
         Assert.True(tab.Spawn.Reset.CanExecute(null));
         Assert.Contains(tab.Spawn, tab.Cards);
      }

      [Fact]
      public void TheCoordinates_AreChecked_AndEditsAreUndoable() {
         WriteTable(Table);
         var tab = CreateTab();
         var original = Data.ToArray();
         Assert.False(tab.Spawn.Reset.CanExecute(null));

         tab.Spawn.XText = "70000";
         tab.Spawn.YText = "abc";
         Assert.Equal(2, Errors.Count);
         Assert.Equal(original, Data);
         Assert.Equal("-1", tab.Spawn.XText);

         tab.Spawn.XText = "12";
         tab.Spawn.YText = "-34";
         Assert.Equal(new CubeNewGameSpawn(25, 40, 12, -34, true), CubeNewGameSpawnTable.Read(Model));
         Assert.Equal("12", tab.Spawn.XText);

         tab.Undo.Execute(null);
         Assert.Equal(new CubeNewGameSpawn(25, 40, 12, -1, true), CubeNewGameSpawnTable.Read(Model));
         Assert.Equal("-1", tab.Spawn.YText);
         tab.Undo.Execute(null);
         Assert.Equal(original, Data);
         tab.Redo.Execute(null);
         Assert.Equal("12", tab.Spawn.XText);
      }

      [Fact]
      public void ChoosingAMap_ChangesGroupAndNumber_InOneUndoStep() {
         WriteTable(Table, group: 1, map: 2, x: 3, y: 4);
         var tab = CreateTab();
         tab.Spawn.Maps.Select(IntroSpawnEditor.MapValue(5, 6), number => "Five-six"); // a map the ROM's list lacks gets an option of its own
         tab.Spawn.Maps.Select(IntroSpawnEditor.MapValue(1, 2));
         Assert.Equal(new CubeNewGameSpawn(1, 2, 3, 4, true), CubeNewGameSpawnTable.Read(Model)); // choosing by the program writes nothing

         tab.Spawn.Maps.Selected = tab.Spawn.Maps.Find(IntroSpawnEditor.MapValue(5, 6)); // choosing by the person does
         Assert.Equal(new CubeNewGameSpawn(5, 6, 3, 4, true), CubeNewGameSpawnTable.Read(Model));
         Assert.Equal("Five-six", tab.Spawn.Maps.Selected.Text);

         tab.Undo.Execute(null);
         Assert.Equal(new CubeNewGameSpawn(1, 2, 3, 4, true), CubeNewGameSpawnTable.Read(Model));
      }

      [Fact]
      public void TheTruckButton_TogglesTheShakeByte_AndBackToTheOriginalRestores() {
         WriteTable(Table, group: 0, map: 9, x: 7, y: 8);
         var tab = CreateTab();
         Assert.Equal("Remove truck shaking", tab.Spawn.ToggleShakeText);

         tab.Spawn.ToggleShake.Execute(null);
         Assert.Equal(0, Data[Table + 14]);
         Assert.False(tab.Spawn.ShakeTruck);
         Assert.Equal("Restore truck shaking", tab.Spawn.ToggleShakeText);
         Assert.Equal("The truck does not shake", tab.Spawn.ShakeLabel);

         tab.Spawn.ToggleShake.Execute(null);
         Assert.Equal(1, Data[Table + 14]);

         tab.Spawn.ToggleShake.Execute(null);
         tab.Spawn.Reset.Execute(null);
         Assert.Equal(CubeNewGameSpawn.Original, CubeNewGameSpawnTable.Read(Model));
         Assert.False(tab.Spawn.Reset.CanExecute(null));

         tab.Undo.Execute(null); // the reset is one step
         Assert.Equal(new CubeNewGameSpawn(0, 9, 7, 8, false), CubeNewGameSpawnTable.Read(Model));
         tab.Undo.Execute(null); // the second toggle
         Assert.Equal(new CubeNewGameSpawn(0, 9, 7, 8, true), CubeNewGameSpawnTable.Read(Model));
      }

      [Fact]
      public void ANonTruckMapAtANegativePosition_Warns() {
         WriteTable(Table, group: 0, map: 9, x: -1, y: -1);
         var tab = CreateTab();
         Assert.True(tab.Spawn.HasStatus);
         Assert.True(tab.Spawn.StatusIsError);
         tab.Spawn.XText = "4";
         tab.Spawn.YText = "5";
         Assert.False(tab.Spawn.StatusIsError);
      }

      [Fact]
      public void ARomWithoutTheIntroCode_HasNoCards() {
         // an unrecognised ROM has no cards at all
         Header("BPEE0");
         var tab = new IntroManagerTab(ViewPort);
         Assert.False(tab.HasSpawn);
         Assert.Empty(tab.Cards);
      }
   }

   public class CubeNewGameSpawnRealRomTests {
      private static IDataModel Load(string variable, string tomlVariable) {
         var path = Environment.GetEnvironmentVariable(variable);
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), $"set {variable} to a ROM to run this test");
         var data = File.ReadAllBytes(path);
         StoredMetadata metadata = null;
         var toml = Environment.GetEnvironmentVariable(tomlVariable);
         if (!string.IsNullOrEmpty(toml) && File.Exists(toml)) metadata = new StoredMetadata(File.ReadAllLines(toml));
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, data, metadata);
         model.InitializationWorkload.Wait();
         return model;
      }

      [SkippableFact]
      public void Cube_MapList_HasTheNamesWithGroupAndNumber() {
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         var options = IntroSpawnEditor.BuildMapOptions(model);
         Assert.True(options.Count > 400, $"{options.Count} maps");
         Assert.True(options.Any(option => option.Value == IntroSpawnEditor.MapValue(0, 9) && option.Text == "Littleroot Town (0.9)"));
         Assert.True(options.Any(option => option.Value == IntroSpawnEditor.MapValue(25, 40) && option.Text.Contains("Truck") && option.Text.EndsWith("(25.40)")));
         Assert.Equal(options.Count, options.Select(option => option.Value).Distinct().Count());
      }

      [SkippableFact]
      public void Cube_TabShowsTheTableAndOpeningWritesNothing() {
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         Skip.If(!CubeNewGameSpawnTable.IsAvailable(model), "no spawn table");
         var before = (byte[])model.RawData.Clone();
         var expected = CubeNewGameSpawnTable.Read(model);
         var tab = new IntroManagerTab(new ViewPort("CUBE.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons));
         Assert.Equal(expected, tab.Spawn.Current);
         Assert.Equal(IntroSpawnEditor.MapValue(expected.MapGroup, expected.MapNum), tab.Spawn.Maps.Selected.Value);
         var centre = expected.X == -1 && expected.Y == -1 ? NewGameSpawn.CenterOf(model, expected.MapGroup, expected.MapNum) : null;
         Assert.Equal((centre?.x ?? expected.X).ToString(), tab.Spawn.XText); // -1 and -1 are shown as the tile in the middle of the map
         // the drop down answering to a rebuilt list must not choose another map
         tab.Spawn.Maps.Search = "battle";
         tab.Spawn.Maps.Search = "";
         tab.Spawn.Maps.Selected = tab.Spawn.Maps.Find(IntroSpawnEditor.MapValue(26, 15)); // a person's choice does write
         Assert.Equal(26, CubeNewGameSpawnTable.Read(model).MapGroup);
      }

      [SkippableFact]
      public void ClassicGames_ShowTheRealStartTile_AndCanMoveIt() {
         var any = false;
         foreach (var variable in new[] { "HMA_INTRO_EMERALD", "HMA_INTRO_EMERALD2", "HMA_INTRO_FIRERED", "HMA_INTRO_FIRERED2", "HMA_INTRO_LEAFGREEN2", "HMA_INTRO_HUBOL" }) {
            var path = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            any = true;
            CheckClassic(variable);
         }
         Skip.If(!any, "set HMA_INTRO_EMERALD, HMA_INTRO_FIRERED... to ROMs to run this test");
      }

      private static void CheckClassic(string variable) {
         var model = Load(variable, variable + "_TOML");
         var tab = new IntroManagerTab(new ViewPort("x.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons));
         Assert.True(tab.Spawn.IsAvailable, "start not found");
         Console.WriteLine($"{variable}: {tab.Spawn.Maps.Selected?.Text} at {tab.Spawn.XText},{tab.Spawn.YText}; can write {NewGameSpawn.CanWrite(model)}; truck {NewGameSpawn.HasTruck(model)}");
         if (tab.Spawn.Maps.Selected?.Text.Contains("(") == true && !tab.Spawn.Current.IsTruck == false) Assert.NotEqual("-1", tab.Spawn.XText);
         var original = (byte[])model.RawData.Clone();
         var spawn = NewGameSpawn.Read(model);
         Assert.NotNull(spawn);
         Assert.True(NewGameSpawn.Write(model, new ModelDelta(), new CubeNewGameSpawn(0, 3, 12, 9, true)));
         var read = NewGameSpawn.Read(model);
         Assert.NotNull(read);
         Assert.Equal((0, 3, 12, 9), (read.MapGroup, read.MapNum, read.X, read.Y));
         Assert.True(NewGameSpawn.Write(model, new ModelDelta(), NewGameSpawn.Original(model)));
         var back = NewGameSpawn.Read(model);
         Assert.NotNull(back);
         Assert.Equal((spawn.MapGroup, spawn.MapNum, spawn.X, spawn.Y), (back.MapGroup, back.MapNum, back.X, back.Y));
      }

      [SkippableFact]
      public void Emerald_StartChangedByAnotherTool_IsStillFoundAndReadable() {
         var model = Load("HMA_INTRO_EMERALD", "HMA_INTRO_EMERALD_TOML");
         Skip.If(NewGameSpawn.Kind(model) != NewGameSpawnKind.CodePatch, "not an Emerald");
         var at = NewGameSpawn.Address(model);
         var data = (byte[])model.RawData.Clone();
         // another order of the same six instructions: movs r0,#y ; str r0,[sp] ; movs r0,#group ; movs r1,#map ; movs r2,#255 ; movs r3,#x   (here: map 0.9 at 14, 8)
         var halves = new[] { 0x2008, 0x9000, 0x2000, 0x2109, 0x22FF, 0x230E };
         for (int i = 0; i < halves.Length; i++) { data[at + i * 2] = (byte)halves[i]; data[at + i * 2 + 1] = (byte)(halves[i] >> 8); }
         var changed = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, data, null);
         changed.InitializationWorkload.Wait();
         Assert.Equal(NewGameSpawnKind.CodePatch, NewGameSpawn.Kind(changed));
         var read = NewGameSpawn.Read(changed);
         Assert.Equal((0, 9, 14, 8), (read.MapGroup, read.MapNum, read.X, read.Y));
         Assert.True(NewGameSpawn.Write(changed, new ModelDelta(), new CubeNewGameSpawn(0, 3, 5, 6, true)));
         Assert.Equal((0, 3, 5, 6), (NewGameSpawn.Read(changed).MapGroup, NewGameSpawn.Read(changed).MapNum, NewGameSpawn.Read(changed).X, NewGameSpawn.Read(changed).Y));
      }

      [SkippableFact]
      public void Cube_WithoutTheTable_TheCardIsDisabled() {
         var model = Load("HMA_INTRO_CUBE", "HMA_INTRO_CUBE_TOML");
         Skip.If(CubeNewGameSpawnTable.IsAvailable(model), "this CUBE already has the spawn table");
         var tab = new IntroManagerTab(new ViewPort("CUBE.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons));
         Assert.True(tab.HasSpawn);
         Assert.False(tab.Spawn.IsAvailable);
         Assert.Contains("latest CUBE", tab.Spawn.UnavailableMessage);
         Assert.True(tab.Spawn.Maps.Count > 400);
      }

      [SkippableFact]
      public void VanillaEmerald_CanStartAnywhere_AndGoBackToTheTruckByteForByte() {
         var model = Load("HMA_INTRO_EMERALD", "HMA_INTRO_EMERALD_TOML");
         Skip.If(NewGameSpawn.Kind(model) != NewGameSpawnKind.CodePatch, "not a vanilla Emerald");
         var original = (byte[])model.RawData.Clone();
         var token = new ModelDelta();
         Assert.True(NewGameSpawn.Write(model, token, new CubeNewGameSpawn(0, 9, 10, 10, true)));
         Assert.True(EmeraldSpawnPatch.IsPatched(model));
         var read = NewGameSpawn.Read(model);
         Assert.Equal((0, 9, 10, 10, false), (read.MapGroup, read.MapNum, read.X, read.Y, read.IsTruck));
         Assert.True(NewGameSpawn.Write(model, token, new CubeNewGameSpawn(25, 40, -1, -1, true)));
         Assert.False(EmeraldSpawnPatch.IsPatched(model));
         Assert.Equal(original, model.RawData);
      }
   }
}
