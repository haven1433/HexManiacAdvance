using HavenSoft.HexManiac.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

      // TODO loading metadata from text
   }
}
