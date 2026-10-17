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
         Assert.Contains(options, option => option.Value == IntroSpawnEditor.MapValue(0, 9) && option.Text == "Littleroot Town (0.9)");
         Assert.Contains(options, option => option.Value == IntroSpawnEditor.MapValue(25, 40) && option.Text.Contains("Truck") && option.Text.EndsWith("(25.40)"));
         Assert.Equal(options.Count, options.Select(option => option.Value).Distinct().Count());
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
   }
}
