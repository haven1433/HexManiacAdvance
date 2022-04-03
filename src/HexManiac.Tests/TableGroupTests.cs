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

      // TODO loading from metadata
   }
}
