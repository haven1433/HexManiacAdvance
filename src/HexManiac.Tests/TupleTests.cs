using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Test that tupels in tables work correctly.
   /// </summary>
   public class TupleTests : BaseViewModelTestClass {
      [Fact]
      public void TupleSegment_Parse_GetTupleSegment() {
         var segments = ArrayRun.ParseSegments("value:|t", Model);
         var segment = (ArrayRunTupleSegment)segments[0];
         Assert.Empty(segment.Elements);
      }

      [Fact]
      public void TableFormat_TupleSpecified_CreatesTuple() {
         ViewPort.Edit("^table[value:|t]1 ");

         Assert.IsType<ArrayRunTupleSegment>(Model.GetTable("table").ElementContent[0]);
      }

      [Fact]
      public void TupleSegmentWithSingleBitLength_Parse_GetTupleSegmentWithSingleBitLength() {
         var segment = new ArrayRunTupleSegment("tuple", "|a.", 2);
         Assert.Single(segment.Elements);
         Assert.Equal("a", segment.Elements[0].Name);
         Assert.Equal(1, segment.Elements[0].BitWidth);
      }

      [Fact]
      public void TupleSegmentWithTwoBitLength_Parse_GetTupleSegmentWithTwoBitLength() {
         var segment = new ArrayRunTupleSegment("tuple", "|a:", 2);
         Assert.Single(segment.Elements);
         Assert.Equal("a", segment.Elements[0].Name);
         Assert.Equal(2, segment.Elements[0].BitWidth);
      }

      [Fact]
      public void SingleByteTupleSegmentWith9BitsOfData_Parse_Error() {
         Assert.Throws<ArrayRunParseException>(() => new ArrayRunTupleSegment("tuple", "|a:.|b:.|c:.", 1));
      }

      [Fact]
      public void TupleTable_TableTool_TupleSegment() {
         ViewPort.Edit("^table[value:|t|a:|b:]1 ");
         var tool = (TupleArrayElementViewModel)ViewPort.Tools.TableTool.Children.Where(child => child is TupleArrayElementViewModel).Single();
         Assert.Equal(2, tool.Children.Count);
         Assert.Equal("a", tool.Children[0].Name);
         Assert.Equal(0, tool.Children[0].BitOffset);
         Assert.Equal(2, tool.Children[1].BitOffset);
      }

      [Fact]
      public void TupleTable_ReadFromTableTool_CorrectResult() {
         Model[0] = 8 + 2 + 1;
         ViewPort.Edit("^table[value:|t|a:|b:]1 ");
         var tool = (TupleArrayElementViewModel)ViewPort.Tools.TableTool.Children.Where(child => child is TupleArrayElementViewModel).Single();
         var a = (NumericTupleElementViewModel)tool.Children[0];
         var b = (NumericTupleElementViewModel)tool.Children[1];
         Assert.Equal(3, a.Value);
         Assert.Equal(2, b.Value);
      }

      // TODO write to the table (numeric)
      // TODO read from the table (checkbox)
      // TODO write to the table (checkbox)
   }
}
