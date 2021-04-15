using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ArrayColumnHeaderTests : BaseViewModelTestClass {
      public ArrayColumnHeaderTests() {
         ViewPort.Edit("^array[data1. data2.]8 ");
      }

      [Fact]
      public void AnchorFormatAutoCompletesToSingleByte() {
         ViewPort.AnchorText = "^array[data1. b data2.]8";
         var array = (ArrayRun)Model.GetNextRun(0);
         Assert.Equal(3, array.ElementLength);
         Assert.Empty(Errors);
      }

      [Fact]
      public void HeadersChangeWhenAnchorChanges() {
         ViewPort.AnchorText = "^array[data1. b data2.]8";
         Assert.Equal(0, ViewPort.ColumnHeaders[0].ColumnHeaders.Count % 3);

         ViewPort.AnchorText = "^array[data1. bc data2.]8";
         Assert.Equal("bc", ViewPort.ColumnHeaders[0].ColumnHeaders[1].ColumnTitle);
         Assert.Equal(1, ViewPort.ColumnHeaders[0].ColumnHeaders[1].ByteWidth);
      }

      [Fact]
      public void TableToolUpdatesAfterAnchorChange() {
         ViewPort.AnchorText = "^array[data1. b data2.]8";
         Assert.Equal(4, ViewPort.Tools.TableTool.Children.Count); // header + 3 elements

         ViewPort.AnchorText = "^array[data1. bc data2.]8";
         Assert.Equal("bc", ((FieldArrayElementViewModel)ViewPort.Tools.TableTool.Children[2]).Name);
      }
   }
}
