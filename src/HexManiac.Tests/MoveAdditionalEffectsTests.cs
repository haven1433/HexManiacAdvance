using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The additional effects of a move (a burn, a flinch, a stat change and its chance) can be seen and edited in the move editor.</summary>
   public class MoveAdditionalEffectsTests : BaseViewModelTestClass {
      [SkippableFact]
      public void RealCube_Blizzard_ShowsItsFreeze_AndTheEffectAndChanceCanBeChanged() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE"); var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var moves = model.GetTableModel("data.pokemon.moves.stats");
         var run = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "data.pokemon.moves.stats"));
         Assert.Contains("]/numAdditionalEffects>", run.FormatString);

         var blizzard = moves[59];
         var effects = blizzard.GetSubTable("additionalEffects");
         Assert.Equal(1, effects.Count);
         Assert.Equal(4, effects[0].GetValue("effect")); // FREEZE
         Assert.Equal(10, effects[0].GetValue("chance"));
         var stream = (TableStreamRun)model.GetNextRun(blizzard.GetAddress("additionalEffects"));
         Assert.Equal(12, stream.ElementLength);

         // Tri Attack: random from a list of three (burn, paralysis, freeze), 20%
         var tri = moves[161].GetSubTable("additionalEffects");
         Assert.Equal(11, tri[0].GetValue("effect"));
         Assert.Equal((3, 5, 4, 20), (tri[0].GetValue("argumentA"), tri[0].GetValue("argumentB"), tri[0].GetValue("argumentC"), tri[0].GetValue("chance")));
         // Shell Smash: two effects, the count comes from the three bits of flags1
         Assert.Equal(2, moves[504].GetSubTable("additionalEffects").Count);
         // Ancient Power: every stat +1
         var ancient = moves[246].GetSubTable("additionalEffects")[0];
         Assert.Equal(15, ancient.GetValue("effect"));
         Assert.Equal(0x09, model[ancient.Start + 7]); Assert.Equal(0x49, model[ancient.Start + 8]);

         // the move editor: a combo box for the effect with the names, and the chance field
         var port = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         port.Goto.Execute("data.pokemon.moves.stats/59");
         var tool = port.Tools.TableTool;
         var members = tool.Groups.SelectMany(g => g.Members).ToList();
         Console.WriteLine(string.Join(" | ", members.Select(m => m.GetType().Name + ":" + (m as FieldArrayElementViewModel)?.Name + (m as ComboBoxArrayElementViewModel)?.Name)));
         var effect = members.OfType<ComboBoxArrayElementViewModel>().First(c => c.Name == "effect1" && c.Start == stream.Start);
         Assert.Equal("FREEZE", effect.FilteringComboOptions.AllOptions[effect.SelectedIndex].Text);
         var chance = members.OfType<FieldArrayElementViewModel>().First(f => f.Name == "chance1" && f.Start == stream.Start + 5);
         Assert.True(members.OfType<ButtonArrayElementViewModel>().Any(b => b.Text.StartsWith("Add ")));
         Assert.Equal("10", chance.Content);

         effect.SelectedIndex = 3; // BURN
         chance.Content = "30";
         Assert.Equal(3, model[stream.Start]);
         Assert.Equal(30, model[stream.Start + 5]);
         // "Add" gives Blizzard a second effect: the three count bits of flags1 say 2 (the game allows up to 7)
         var flags1 = stream.PointerSources[0] - 4 * 4; // flags1, flags2, flags3, argument come before the pointer
         Assert.Equal(1, (model.ReadMultiByteValue(flags1, 4) >> 13) & 7);
         members.OfType<ButtonArrayElementViewModel>().First(b => b.Text.StartsWith("Add ")).Command.Execute(null);
         Assert.Equal(2, (model.ReadMultiByteValue(flags1, 4) >> 13) & 7);
         var grown = (TableStreamRun)model.GetNextRun(model.ReadPointer(stream.PointerSources[0]));
         Assert.Equal(2, grown.ElementCount);
         Assert.Equal(0, model[grown.Start + 12]); // the new effect starts empty
         members = tool.Groups.SelectMany(g => g.Members).ToList();
         Assert.True(members.OfType<ComboBoxArrayElementViewModel>().Any(c => c.Name == "effect2"));
         Assert.Equal(7, grown.MaxElementCount);
         Assert.True(model.TryGetList("additionalmoveeffects", out var list));
         Assert.Equal("STAT_CHANGE_PLUS", list[88]);
      }

      [Fact]
      public void TheCount_CanLiveInABitField_OfATuple() {
         // a parent table whose 'count' is two bits inside a flags byte, and a stream whose length is that count
         SetFullModel(0xFF);
         ViewPort.Edit("@00 ^parent[flags.|t|low:|count:|rest:: child<[a. b.]/count>]1 ");
         ViewPort.Edit("@00 ");
         Model[0] = 0b0000_1000; // count = 2
         ViewPort.Edit("@01 <020> @20 01 02 03 04 ");
         ViewPort.Refresh();
         var child = Model.GetNextRun(0x20) as TableStreamRun;
         Assert.NotNull(child);
         Assert.Equal(2, child.ElementCount);
         // the parent being edited (count 3) grows the stream
         var parent = (ArrayRun)Model.GetNextRun(0);
         Model[0] = 0b0000_1100;
         var info = parent.NotifyChildren(Model, ViewPort.CurrentChange, 0, 0);
         child = Model.GetNextRun(Model.ReadPointer(1)) as TableStreamRun;
         Assert.Equal(3, child.ElementCount);
         Assert.Equal(3, child.MaxElementCount);
         // and the other way: adding an element writes the count into the bit field
         child = (TableStreamRun)child.Append(ViewPort.CurrentChange, -1);
         Assert.Equal(0b0000_1000, Model[0]);
      }
   }
}
