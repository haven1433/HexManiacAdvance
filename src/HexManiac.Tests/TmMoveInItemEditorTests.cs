using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The editor of an item says which move a TM or HM teaches, and lets you change it. Other items don't get the extra row.</summary>
   public class TmMoveInItemEditorTests : BaseViewModelTestClass {
      private const int Items = 0x100, Tms = 0x200;
      public TmMoveInItemEditorTests() : base(0x400) { }

      // items: 0 none, 1 Potion, 2 TM01, 3 TM02, 4 HM01; the list holds TM01, TM02, HM01 (the item of TM01 is 2)
      private void Setup() {
         CreateTextTable("data.pokemon.moves.stats", 0x300, "-", "pound", "karate", "slam", "fly");
         var names = new[] { "-", "POTION", "TM01", "TM02", "HM01" };
         for (int i = 0; i < names.Length; i++) {
            var bytes = Model.TextConverter.Convert(names[i], out _);
            for (int j = 0; j < 8; j++) Model[Items + i * 10 + j] = j < bytes.Count ? bytes[j] : (byte)0xFF;
            if (bytes.Count < 8) Model[Items + i * 10 + bytes.Count] = 0xFF;
         }
         ViewPort.Edit($"@{Items:X2} ^data.items.stats[name\"\"8 price:]5 ");
         Model[Tms + 0] = 3; Model[Tms + 2] = 1; Model[Tms + 4] = 4; // TM01 slam, TM02 pound, HM01 fly
         ViewPort.Edit($"@{Tms:X2} ^data.pokemon.moves.tmhm[move:data.pokemon.moves.stats]3 ");
         Model[Tms + 0x20] = 3; Model[Tms + 0x22] = 1; Model[Tms + 0x24] = 4;
         ViewPort.Edit($"@{Tms + 0x20:X2} ^data.pokemon.moves.tmhm.copy[move:data.pokemon.moves.stats]3 ");
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      [Fact]
      public void Tm_ShowsItsMove_AndChangingItWritesEveryList() {
         Setup();
         ViewPort.Goto.Execute(Items + 2 * 10);
         var combo = ViewPort.Tools.TableTool.Children.OfType<ComboBoxArrayElementViewModel>().Single(box => box.Name.StartsWith("Move taught"));
         Assert.Equal("slam", combo.FilteringComboOptions.DisplayText);
         combo.FilteringComboOptions.DisplayText = "karate";
         combo.FilteringComboOptions.SelectConfirm();
         Assert.Equal(2, Model.ReadMultiByteValue(Tms, 2));
         Assert.Equal(2, Model.ReadMultiByteValue(Tms + 0x20, 2)); // the copy of the list follows
      }

      [Fact]
      public void Hm_UsesTheIndexAfterTheTms() {
         Setup();
         ViewPort.Goto.Execute(Items + 4 * 10);
         var combo = ViewPort.Tools.TableTool.Children.OfType<ComboBoxArrayElementViewModel>().Single(box => box.Name.StartsWith("Move taught"));
         Assert.Equal("fly", combo.FilteringComboOptions.DisplayText);
      }

      [Fact]
      public void Potion_HasNoMoveRow() {
         Setup();
         ViewPort.Goto.Execute(Items + 1 * 10);
         Assert.DoesNotContain(ViewPort.Tools.TableTool.Children, child => child is ComboBoxArrayElementViewModel box && box.Name.StartsWith("Move taught"));
      }

      [SkippableFact]
      public void RealCube_AllHundredTms_ShowTheirMove_AndChangeBothArrays() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var tables = TmMoveTables.Find(model);
         Assert.Equal(2, tables.Count);
         Assert.Equal(108, tables[0].ElementCount);
         var port = new HavenSoft.HexManiac.Core.ViewModels.ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         port.Goto.Execute("data.items.stats/631"); // TM50
         var combo = port.Tools.TableTool.Children.OfType<ComboBoxArrayElementViewModel>().Single(box => box.Name.StartsWith("Move taught"));
         Console.WriteLine("TM50: " + combo.Name + " = " + combo.FilteringComboOptions.DisplayText);
         combo.FilteringComboOptions.DisplayText = "Water Gun";
         combo.FilteringComboOptions.SelectConfirm();
         Assert.Equal(55, model.ReadMultiByteValue(tables[0].Start + 49 * 2, 2));
         Assert.Equal(55, model.ReadMultiByteValue(tables[1].Start + 49 * 2, 2));
         port.Goto.Execute("data.items.stats/632"); // TM51, has no move yet
         var combo51 = port.Tools.TableTool.Children.OfType<ComboBoxArrayElementViewModel>().Single(box => box.Name.StartsWith("Move taught"));
         Assert.Equal("Move taught by TM51", combo51.Name);
         port.Goto.Execute("data.items.stats/1");
         Assert.DoesNotContain(port.Tools.TableTool.Children, child => child is ComboBoxArrayElementViewModel box && box.Name.StartsWith("Move taught"));
      }
   }
}
