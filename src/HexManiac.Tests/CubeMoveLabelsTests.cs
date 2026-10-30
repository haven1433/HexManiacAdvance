using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>A CUBE opened with a .toml from before round 14g gets the right move category, priority and contest lists.</summary>
   public class CubeMoveLabelsTests : BaseViewModelTestClass {
      private const int Table = 0x100;
      public CubeMoveLabelsTests() : base(0x400) { }

      private const string OldFormat = "[effect. typeAndPower:|t|type::.|category:damagecategory|power::::. flags1::|t|priority::|strikeCount::|multiHit.|rest:::::::::::. contestCategory. last.]2";

      private void Build(params string[] damageCategory) {
         Model.SetList(new ModelDelta(), "damagecategory", damageCategory.Length > 0 ? damageCategory : new[] { "physical", "special", "status" });
         ViewPort.Edit($"@{Table:X2} ^{CubeMoveLabels.MovesAnchor}{OldFormat} ");
      }

      private string Format() => ((ArrayRun)Model.GetNextRun(Table)).FormatString;

      [Fact]
      public void OldToml_GetsTheRightListsAndFormats() {
         Build();
         Assert.Contains("priority::|", Format());

         CubeMoveLabels.Repair(Model);

         Assert.True(Model.TryGetList("damagecategory", out var categories));
         Assert.Equal(new[] { "none", "physical", "special", "status" }, categories.ToArray());
         Assert.True(Model.TryGetList("priorities", out var priorities));
         Assert.Equal(16, priorities.Count);
         Assert.Equal("0", priorities[0]); Assert.Equal("7", priorities[7]); Assert.Equal("-8", priorities[8]); Assert.Equal("-1", priorities[15]);
         Assert.True(Model.TryGetList("contestcategories", out var contest));
         Assert.Equal(5, contest.Count);
         Assert.Contains("priority::priorities|", Format());
         Assert.Contains("contestCategory.contestcategories ", Format());
      }

      [Fact]
      public void PriorityShowsNegativeNumbers() {
         Build();
         CubeMoveLabels.Repair(Model);
         // element 0: priority bits are the low 4 bits of flags1 (byte offset 3). 0b1010 = -6 (Whirlwind)
         Model.WriteMultiByteValue(Table + 3, 4, new ModelDelta(), 0xA);
         var move = Model.GetTableModel(CubeMoveLabels.MovesAnchor)[0];
         Assert.Equal("-6", move.GetTuple("flags1").GetEnumValue("priority"));
      }

      [Fact]
      public void CategoryReadsSpecialWhenTheValueIsTwo() {
         Build();
         CubeMoveLabels.Repair(Model);
         // typeAndPower is the 16 bits at byte offset 1: type 5 bits, category 2 bits, power 9 bits. category 2 = special
         Model.WriteMultiByteValue(Table + 1, 2, new ModelDelta(), 2 << 5);
         var move = Model.GetTableModel(CubeMoveLabels.MovesAnchor)[0];
         Assert.Equal("special", move.GetTuple("typeAndPower").GetEnumValue("category"));
      }

      [Fact]
      public void AListThatWasEdited_IsKept()  {
         Build("a", "b", "c");
         CubeMoveLabels.Repair(Model);
         Assert.True(Model.TryGetList("damagecategory", out var categories));
         Assert.Equal(new[] { "a", "b", "c" }, categories.ToArray());
      }

      [Fact]
      public void RunningItTwice_ChangesNothingTheSecondTime() {
         Build();
         CubeMoveLabels.Repair(Model);
         var format = Format();
         CubeMoveLabels.Repair(Model);
         Assert.Equal(format, Format());
      }

      [Fact]
      public void ATomlThatIsAlreadyRight_IsNotTouched() {
         Model.SetList(new ModelDelta(), "damagecategory", "none", "physical", "special", "status");
         Model.SetList(new ModelDelta(), "priorities", Enumerable.Range(0, 16).Select(i => (i < 8 ? i : i - 16).ToString()).ToArray());
         Model.SetList(new ModelDelta(), "contestcategories", "Cool", "Beautiful", "Cute", "Clever", "Tough");
         ViewPort.Edit($"@{Table:X2} ^{CubeMoveLabels.MovesAnchor}{OldFormat.Replace("priority::|", "priority::priorities|").Replace("contestCategory. ", "contestCategory.contestcategories ")} ");
         var format = Format();
         CubeMoveLabels.Repair(Model);
         Assert.Equal(format, Format());
         Assert.True(Model.TryGetList("damagecategory", out var categories));
         Assert.Equal(4, categories.Count);
      }

      [SkippableFact]
      public void EveryOlderCube_ShowsTheRightMoveLabels() {
         var versions = Environment.GetEnvironmentVariable("UPD_VERSIONS");
         Skip.If(string.IsNullOrEmpty(versions), "set UPD_VERSIONS (rom|toml;rom|toml...)");
         foreach (var version in versions.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            var parts = version.Split('|');
            var model = CubeScriptsTests.Load(File.ReadAllBytes(parts[0]), File.ReadAllLines(parts[1]));
            var moves = model.GetTableModel(CubeMoveLabels.MovesAnchor);
            Assert.True(moves.Count > 585, parts[0]);
            Assert.Equal("physical", moves[33].GetTuple("typeAndPower").GetEnumValue("category")); // Tackle
            Assert.Equal("special", moves[585].GetTuple("typeAndPower").GetEnumValue("category")); // Moonblast
            Assert.Equal("status", moves[580].GetTuple("typeAndPower").GetEnumValue("category")); // Grassy Terrain
            Assert.Equal("-6", moves[18].GetTuple("flags1").GetEnumValue("priority"));            // Whirlwind
            Assert.Equal("Grassy Terrain", moves[580].GetEnumValue("contestComboStarterId"));
         }
      }
   }
}
