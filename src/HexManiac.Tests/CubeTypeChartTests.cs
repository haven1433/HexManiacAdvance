using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The CUBE type chart: every row named after its attacking type, every field after its defending type.</summary>
   public class CubeTypeChartTests : BaseViewModelTestClass {
      private const int Chart = 0x100, Types = 0x800;
      public CubeTypeChartTests() : base(0x1000) { }

      private void Build(string chartFormat) {
         Model.SetList(new ModelDelta(), "effectiveness", Enumerable.Range(0, 8193).Select(i => i == 0 ? "no effect" : i == 2048 ? "not very effective" : i == 4096 ? "normal" : i == 8192 ? "super effective" : null).ToArray());
         ViewPort.Edit($"@{Types:X2} ^{CubeMoveLabels.TypesAnchor}[name\"\"9]21 ");
         ViewPort.Edit($"@{Chart:X2} ^{CubeMoveLabels.ChartAnchor}{chartFormat} ");
      }

      private ArrayRun Run() => (ArrayRun)Model.GetNextRun(Chart);

      [Fact]
      public void OldChart_GetsLabeledRowsAndFields() {
         Build(CubeMoveLabels.OldChartFormat);
         Assert.Equal(CubeMoveLabels.OldChartFormat, Run().FormatString);

         CubeMoveLabels.Repair(Model);

         Assert.Equal(CubeMoveLabels.NewChartFormat, Run().FormatString);
         Assert.Equal(21, Run().ElementCount);
         Assert.Equal(84, Run().ElementLength);
         Assert.Equal("vsRock", Run().ElementContent[6].Name);
      }

      [Fact]
      public void TheValueOfAFieldStillReads() {
         Build(CubeMoveLabels.OldChartFormat);
         // attacker 1 (Normal), defender 6 (Rock): half
         Model.WriteMultiByteValue(Chart + (21 + 6) * 4, 4, new ModelDelta(), 2048);
         CubeMoveLabels.Repair(Model);
         var row = Model.GetTableModel(CubeMoveLabels.ChartAnchor)[1];
         Assert.Equal("not very effective", row.GetEnumValue("vsRock"));
         Assert.Equal("no effect", row.GetEnumValue("vsFire")); // untouched zero bytes
      }

      [Fact]
      public void RunningItTwice_ChangesNothing() {
         Build(CubeMoveLabels.OldChartFormat);
         CubeMoveLabels.Repair(Model);
         var format = Run().FormatString;
         CubeMoveLabels.Repair(Model);
         Assert.Equal(format, Run().FormatString);
      }

      [Fact]
      public void AChartSomebodyChanged_IsKept() {
         Build("[mult::effectiveness]441");
         var format = Run().FormatString;
         CubeMoveLabels.Repair(Model);
         Assert.Equal(format, Run().FormatString);
      }

      [Fact]
      public void TrainerBattleType_BecomesSingleOrDouble() {
         ViewPort.Edit($"@800 ^{CubeMoveLabels.TrainerTables[0]}[class. flags.|t|battleType: mugshot.]2 ");
         CubeMoveLabels.Repair(Model);
         var run = (ArrayRun)Model.GetNextRun(0x800);
         Assert.Contains("battleType:battletype", run.FormatString);
         Assert.True(Model.TryGetList("battletype", out var list));
         Assert.Equal(new[] { "Single", "Double" }, list.ToArray());
      }

      [Fact]
      public void EvolutionItemsShowAsNames() {
         ViewPort.Edit("@800 ^data.pokemon.stats[hp. evolutions<[method:evolutionmethods param: species:data.pokemon.stats unused: conditions<[condition:evolutionconditions arg1: arg2: arg3:]!2700000000000000>]!FFFF00000000000000000000>]2 ");
         CubeMoveLabels.Repair(Model);
         var run = (ArrayRun)Model.GetNextRun(0x800);
         Assert.Contains("param:|s=method(3=data.items.stats)", run.FormatString);
         Assert.Contains("arg1:|s=condition(7=data.items.stats", run.FormatString);
      }
   }
}
