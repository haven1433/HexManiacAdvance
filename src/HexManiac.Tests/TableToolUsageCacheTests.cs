using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The table tool lists the tables that use the table it is showing ("table2" within a name table), and keeps that list between refreshes:
   /// it may only be reused while no run (format) changed. Typing numbers does not change one, adding or removing a table does.
   /// </summary>
   public class TableToolUsageCacheTests : BaseViewModelTestClass {
      private int UsageButtons(string text) => ViewPort.Tools.TableTool.UsageChildren.OfType<ButtonArrayElementViewModel>().Count(button => button.Text.Contains(text));

      [Fact]
      public void UsageList_FollowsNewAndRemovedTables() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         ViewPort.Edit("@00 ^table1[data:]names-1 ");
         ViewPort.Goto.Execute("table1/bob");
         Assert.Equal(0, UsageButtons("table2"));

         ViewPort.Edit("@20 ^table2[value:names]4 bob bob carl dave ");
         ViewPort.Goto.Execute("table1/bob");
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();
         Assert.Equal(1, UsageButtons("table2"));

         ViewPort.Undo.Execute(null);
         ViewPort.Goto.Execute("table1/bob");
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();
         Assert.Equal(0, UsageButtons("table2"));
      }

      [Fact]
      public void UsageList_StaysRightAfterTypingNumbers() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         ViewPort.Edit("@00 ^table1[data:]names-1 @20 ^table2[value:names]4 bob bob carl dave ");
         ViewPort.Goto.Execute("table1/bob");
         Assert.Equal(1, UsageButtons("table2"));

         ViewPort.Edit("@20 carl "); // data only: the same tables use table1
         ViewPort.Goto.Execute("table1/carl");
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();

         Assert.Equal(1, UsageButtons("table2"));
      }

      [Fact]
      public void UnnamedTable_HasNoUsageButtonsAndStillShowsItsFields() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         ViewPort.Edit("@00 ^table2[value:names]4 bob bob carl dave ");
         ViewPort.Edit("@40 <000000> ^unnamed[a. b.]3 1 2 3 4 5 6 ");
         ViewPort.Goto.Execute(0x40);
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();

         Assert.Empty(ViewPort.Tools.TableTool.UsageChildren.OfType<ButtonArrayElementViewModel>());
         Assert.NotEmpty(ViewPort.Tools.TableTool.Children);
      }
   }
}
