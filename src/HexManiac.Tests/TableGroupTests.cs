using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TableGroupTests : BaseViewModelTestClass {
      [Fact]
      public void TableGroup_LoadTable_TableToolContainsGroup() {
         ViewPort.Edit("@10 ^table1[data:]4 @20 ^table2[data:]table1 @30 ^table3[data:]table1 @40 ^table4[data:]table1 ");
         Model.AppendTableGroup(Token, "tables", new[] { "table1", "table2" });

         ViewPort.Goto.Execute(0x10);
         var groups = ViewPort.Tools.TableTool.Groups;

         Assert.Equal(2, groups.Count);
         Assert.Equal("tables", groups[0].GroupName);
         Assert.Equal("Other", groups[1].GroupName);
      }

      // TODO undo/redo for SetTableGroup

      // TODO saving to metadata

      [Fact]
      public void TableGroupInMetadata_Load_TableGroupInModel() {
         Model.Load(new byte[0x200], new StoredMetadata(
            anchors: new[] {
               new StoredAnchor( 0, "table1", "[data:]4"),
               new StoredAnchor(20, "table2", "[data:]table1"),
               new StoredAnchor(40, "table3", "[data:]table1"),
               new StoredAnchor(60, "table4", "[data:]table1"),
            },
            tableGroups: new[] {
               new TableGroup("group1", new[] { "table1", "table2" }),
               new TableGroup("group2", new[] { "table3", "table4" }),
            }
         ));

         var groups = Model.GetTableGroups("table1");

         Assert.Equal(new[] { "group1", "group2" }, groups.Select(group => group.GroupName));
         Assert.Equal(new[] { "table1", "table2" }, groups[0].Tables);
         Assert.Equal(new[] { "table3", "table4" }, groups[1].Tables);
      }

      [Fact]
      public void Text_LoadMetadata_ContainsTableGroups() {
         var metadata = new StoredMetadata(new[] {
            "[[TableGroup]]",
            "Name = '''group1'''",
            "0 = [",
            "   '''table1''',",
            "   '''table2''',",
            "]",
         });

         Assert.Single(metadata.TableGroups);
         Assert.Equal("group1", metadata.TableGroups[0].GroupName);
         Assert.Equal(new[] { "table1", "table2" }, metadata.TableGroups[0].Tables);
      }

      [Fact]
      public void SplitTable_HasSplitSegment() {
         var error = ArrayRun.TryParse(Model, "[data: | more:]4", 0, SortedSpan<int>.None, out var table);

         Assert.False(error.HasError);
         Assert.IsType<ArrayRunSplitterSegment>(table.ElementContent[1]);
      }

      [Fact]
      public void SplitTable_SplitGroup_LoadMultipleGroups() {
         Model.Load(new byte[0x200], new StoredMetadata(
            anchors: new[] {
               new StoredAnchor(0, "table1", "[data: | more:]4"),
            },
            tableGroups: new[] {
               new TableGroup("group1", new[] { "table1|0" }),
               new TableGroup("group2", new[] { "table1|1" }),
            }
         ));

         var groups = Model.GetTableGroups("table1");

         Assert.Equal(new[] { "group1", "group2" }, groups.Select(group => group.GroupName));
         Assert.Equal(new[] { "table1|0" }, groups[0].Tables);
         Assert.Equal(new[] { "table1|1" }, groups[1].Tables);
      }

      [Fact]
      public void SplitTable_LoadTableTool_LoadMultipleGroups() {
         Model.Load(new byte[0x200], new StoredMetadata(
            anchors: new[] {
               new StoredAnchor(0, "table1", "[data: | more:]4"),
            },
            tableGroups: new[] {
               new TableGroup("group1", new[] { "table1|0" }),
               new TableGroup("group2", new[] { "table1|1" }),
            }
         ));

         ViewPort.Goto.Execute(4);

         var groups = ViewPort.Tools.TableTool.Groups;

         Assert.Equal(2, groups.Count);
         Assert.Equal("data", groups[0].Members.Single<FieldArrayElementViewModel>().Name);
         Assert.Equal("more", groups[1].Members.Single<FieldArrayElementViewModel>().Name);
      }
   }
}
