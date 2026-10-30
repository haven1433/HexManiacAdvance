using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class SingleTableModeTests : BaseViewModelTestClass {
      public SingleTableModeTests() {
         ViewPort.AllowSingleTableMode = true;
      }

      [Fact]
      public void TableWithOneElement_AddMoreCausesRepoint_CanSeeAllElements() {
         SetFullModel(0xFF);
         ViewPort.Edit("^table[a: b: c: d:]1 ");
         Model[8] = 1; // to force the repoint
         var tableTool = ViewPort.Tools.TableTool;

         tableTool.AddCount = 3;
         tableTool.Append.Execute();

         var viewStart = ViewPort.DataOffset;
         var table = (ITableRun)Model.GetNextRun(viewStart);
         Assert.Equal(table.Start, viewStart);
      }

      [Fact]
      public void SelectionInMiddleOfScreen_FollowPointerThenGoBack_SelectionStillInMiddleOfScreen() {
         ViewPort.SelectionStart = new Point(0, 2);

         ViewPort.Goto.Execute(0x100);
         ViewPort.Back.Execute();

         Assert.Equal(new Point(0, 2), ViewPort.SelectionStart);
      }

      [Fact]
      public void FocusTable_ClearFormat_SelectionDoesNotMoveSeeRawBytes() {
         ViewPort.Edit("@40 ^table[a:: b:: c:: d::]20 ");

         var menuItem = ViewPort.GetContextMenuItems(new Point(0, 0)).Single(item => item.Text == "Clear Format");
         menuItem.Command.Execute();

         Assert.Equal(0, ViewPort.MinimumScroll);
         Assert.Equal(4, ViewPort.ScrollValue);
         Assert.Equal(0x20 - 1, ViewPort.MaximumScroll);
      }
   }
}
