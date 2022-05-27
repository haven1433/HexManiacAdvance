using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
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
         Assert.Equal(2, TupleTable.Children.Count);
         Assert.Equal("a", TupleTable.Children[0].Name);
         Assert.Equal(0, TupleTable.Children[0].BitOffset);
         Assert.Equal(2, TupleTable.Children[1].BitOffset);
      }

      [Fact]
      public void TupleTable_ReadFromTableTool_CorrectResult() {
         Model[0] = 0b1011;

         ViewPort.Edit("^table[value:|t|a:|b:]1 ");
         var a = (NumericTupleElementViewModel)TupleTable.Children[0];
         var b = (NumericTupleElementViewModel)TupleTable.Children[1];

         Assert.Equal(3, a.Content);
         Assert.Equal(2, b.Content);
      }

      [Fact]
      public void TupleTable_WriteToTableTool_CorrectResult() {
         ViewPort.Edit("^table[value:|t|a:|b:]1 ");
         var a = (NumericTupleElementViewModel)TupleTable.Children[0];
         var b = (NumericTupleElementViewModel)TupleTable.Children[1];

         a.Content = 3;
         b.Content = 2;

         Assert.Equal(0b1011, Model[0]);
      }

      [Fact]
      public void TupleTable_ReadFromCheckBox_CorrectResult() {
         Model[0] = 0b10;

         ViewPort.Edit("^table[value:|t|a.|b.]1 ");
         var a = (CheckBoxTupleElementViewModel)TupleTable.Children[0];
         var b = (CheckBoxTupleElementViewModel)TupleTable.Children[1];

         Assert.False(a.IsChecked);
         Assert.True(b.IsChecked);
      }

      [Fact]
      public void TupleTable_WriteToCheckBox_CorrectResult() {
         ViewPort.Edit("^table[value:|t|a.|b.]1 ");
         var a = (CheckBoxTupleElementViewModel)TupleTable.Children[0];
         var b = (CheckBoxTupleElementViewModel)TupleTable.Children[1];

         a.IsChecked = true;
         b.IsChecked = true;

         Assert.Equal(0b11, Model[0]);
      }

      [Fact]
      public void TupleTable_SelectNextElement_SameElement() {
         ViewPort.Edit("^table[value:|t|a:|b:]2 ");
         var tool = ViewPort.Tools.TableTool;
         var tupleChild1 = TupleTable.Children[0];

         tool.Next.Execute();

         var tupleChild2 = TupleTable.Children[0];
         Assert.True(ReferenceEquals(tupleChild1, tupleChild2));
      }

      [Fact]
      public void TupleTable_LengthEnumFormat_EnumTupleCreated() {
         ViewPort.Edit("^table[value:|t|a:|b:2|c:]1 ");
         var child = (EnumTupleElementViewModel)TupleTable.Children[1];

         child.SelectedIndex = 2;

         Assert.Equal(new[] { "0", "1" }, child.Options);
         Assert.Equal(0b1000, Model[0]);
      }

      [Fact]
      public void TupleStream_Serialize_ElementsAreWrappedInParenthesis() {
         Model[10] = 0xFF;
         Model[11] = 0xFF;
         ViewPort.Edit("^table[value.|t|a:|b. next.|h]!FFFF ");

         var text = ((IStreamRun)Model.GetNextRun(0)).SerializeRun();

         var lines = text.Split(Environment.NewLine);
         Assert.Equal("(0 false), 00", lines[0]);
         Assert.Equal(5, lines.Length);
      }

      [Fact]
      public void TupleStream_Deserialize_ValuesAreUpdated() {
         Model[4] = 0xFF;
         Model[5] = 0xFF;
         ViewPort.Edit("^table[value.|t|a:|b. next.|h]!FFFF ");

         var run = (TableStreamRun)Model.GetNextRun(0);
         run = run.DeserializeRun(@"
(2 true), 04
", ViewPort.CurrentChange);

         Assert.Equal(1, run.ElementCount);
         Assert.Equal(0b110, Model[0]);
         Assert.Equal(0x04, Model[1]);
      }

      [Fact]
      public void TupleStream_SerializeEnum_ProducesText() {
         Model.SetList("options", new[] { "None", "Something Else" });
         Array.Copy(new byte[] { 0, 0b_10_10_01, 0xFF }, Model.RawData, 3);
         ViewPort.Edit("^table[value.|t|a:options|b:options|c:4]!FF ");

         var result = ((TableStreamRun)Model.GetNextRun(0)).SerializeRun();

         var lines = result.SplitLines();
         Assert.Equal("(None None 0)", lines[0]);
         Assert.Equal("(\"Something Else\" 2 2)", lines[1]);
      }

      [Fact]
      public void TupleStream_DeserializeEnum_ProducesEnum() {
         Model.SetList("options", new[] { "None", "Something Else" });
         Model[2] = 0xFF;
         ViewPort.Edit("^table[value.|t|a:options|b:options|c:options|d:4]!FF ");

         var run = (TableStreamRun)Model.GetNextRun(0);
         run.DeserializeRun("(\"Some Else\", somethels 0, 3)", ViewPort.CurrentChange);

         Assert.Equal(0b11_00_01_01, Model[0]);
      }

      [Fact]
      public void TupleStream_EnumDependency_DependencyRecognized() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         CreateEnumTable("enums", 0x80, "names", 0, 1, 2, 3);
         Model[1] = 0xFF;

         ViewPort.Edit("@00 ^table[value.|t|a:|b:enums]!FF ");

         var run = (IStreamRun)Model.GetNextRun(0);
         Assert.True(run.DependsOn("enums"));
      }

      [Fact]
      public void EmptyTupleSegmentName_CheckTableTool_DoesNotContainEmptyNameElements() {
         ViewPort.Edit("^table[a.|t|.|x:|.|y:]1 ");

         Assert.Equal(2, TupleTable.Children.Count);
         Assert.Equal("x", TupleTable.Children[0].Name);
         Assert.Equal("y", TupleTable.Children[1].Name);
      }

      [Fact]
      public void EmptyTupleSegmentName_Serialize_DoesNotContainEmptyNameElements() {
         Model[1] = 0xFF;
         ViewPort.Edit("^table[a.|t|.|x:|.|y:]!FF ");

         var run = (TableStreamRun)Model.GetNextRun(0);
         var text = run.SerializeRun();

         Assert.Equal("(0 0)", text);
      }

      [Fact]
      public void EmptyTupleSegmentName_Deserialize_DoesNotExpectEmptyNameElements() {
         Model[1] = 0xFF;
         ViewPort.Edit("^table[a.|t|.|x:|.|y:]!FF ");

         var run = (TableStreamRun)Model.GetNextRun(0);
         run.DeserializeRun("(1, 1)", ViewPort.CurrentChange);

         Assert.Equal(0b_01_0_01_0, Model[0]);
      }

      [Fact]
      public void Tuple_GetDataFormat_ReadsTuple() {
         ViewPort.Edit("^table[a.|t|.|x:|.|y:]1 ");

         var anchor = (Anchor)ViewPort[0, 0].Format;
         var tuple = (Core.ViewModels.DataFormats.Tuple)anchor.OriginalFormat;

         Assert.Equal("(0 0)", tuple.ToString());
      }

      [Fact]
      public void Tuple_EditText_WritesTuple() {
         ViewPort.Edit("^table[a.|t|.|x:|.|y:]1 ");

         ViewPort.Edit("1 1 ");

         Assert.Equal(0b_01_0_01_0, Model[0]);
         Assert.Equal(1, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void Tuple_EditInline_AutocompleteForFirstElement() {
         Model.SetList("options", new[] { "matchX", "Xmatch", "matXch", "other" });
         ViewPort.Edit("^table[a:|t|x::options|y::]2 ");

         ViewPort.Edit("match");

         var cell = (UnderEdit)ViewPort[0, 0].Format;
         var options = cell.AutocompleteOptions.Select(item => item.DisplayText).ToArray();
         Assert.Equal(new[] { "matchX", "Xmatch", "matXch" }, options);
      }

      [Fact]
      public void Tuple_ChooseInlineAutocompleteOption_StillEditing() {
         Model.SetList("options", new[] { "matchX", "Xmatch", "matXch", "other" });
         ViewPort.Edit("^table[a:|t|x::options|y::]2 ");

         ViewPort.Edit("match");
         var cell = (UnderEdit)ViewPort[0, 0].Format;
         ViewPort.Autocomplete(cell.AutocompleteOptions[1].CompletionText);

         Assert.IsType<UnderEdit>(ViewPort[0, 0].Format);
      }

      [Fact]
      public void Tuple_ChooseFinalInlineAutocompleteOption_DoneEditing() {
         Model.SetList("options", new[] { "matchX", "Xmatch", "matXch", "other" });
         ViewPort.Edit("^table[a:|t|x::|y::options]2 ");

         ViewPort.Edit("(3 match");
         var cell = (UnderEdit)ViewPort[0, 0].Format;
         ViewPort.Autocomplete(cell.AutocompleteOptions[1].CompletionText);

         Assert.IsNotType<UnderEdit>(ViewPort[0, 0].Format);
      }

      [Fact]
      public void Tuple_WriteWithNoParen_Commit() {
         Model.SetList("options", new[] { "matchX", "Xmatch", "matXch", "other" });
         ViewPort.Edit("^table[a:|t|x::|y::options]2 ");

         ViewPort.Edit("3 Xmatch ");

         Assert.Equal(0b0001_0011, Model[0]);
      }

      [Fact]
      public void ElementsWithSpaces_TypeTuple_Works() {
         Model.SetList("options", new[] { "simple", "with space", "other" });
         ViewPort.Edit("^table[a:|t|x::options|y::]2 ");

         ViewPort.Edit("(\"with space\" 2)");

         Assert.Equal(0b0010_0001, Model[0]);
      }

      [Fact]
      public void Tuple_TypeSpace_NoEdit() {
         ViewPort.Edit("^table[a:|t|x::|y::]2 ");

         ViewPort.Edit(" ");

         var cell = ViewPort[0, 0].Format;
         Assert.IsNotType<UnderEdit>(cell);
      }

      [Fact]
      public void Tuple_TypePlus_NoEdit() {
         ViewPort.Edit("^table[a:|t|x::|y::]2 ");

         ViewPort.Edit("+");

         var cell = ViewPort[0, 0].Format;
         Assert.IsNotType<UnderEdit>(cell);
      }

      [Fact]
      public void TupleInTableStream_TypeEndStreamAtStartOfElement_TruncateStream() {
         ViewPort.Edit("@!put(FFFF) ^table[a:|t|x::|y::]!FFFF +(2 3) +(3 4) @02 ");

         ViewPort.Edit("[]");

         var run = (TableStreamRun)Model.GetNextRun(0);
         Assert.Equal(1, run.ElementCount);
      }

      [Fact]
      public void TupleSyntax_ApplyToNormalElements_StillWorks() {
         Model.SetList("options", new[] { "adam", "brad", "carl", "dave" });
         ViewPort.Edit("^table[number: name:options]4 ");

         ViewPort.Edit("(10 brad) (20 carl)");

         Assert.Equal(10, Model[0]);
         Assert.Equal(1,  Model[2]);
         Assert.Equal(20, Model[4]);
         Assert.Equal(2,  Model[6]);
      }

      [Fact]
      public void EnumTuple_Create_HasFilteredText() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         var tuple = new EnumTupleElementViewModel(ViewPort, 0, 0, new TupleSegment("name", 8, "names"), default, default);
         Assert.Equal("adam", tuple.FilterText);
      }

      [Fact]
      public void EnumTuple_ValueOutsideOptionsRange_FilteredTextIsNumber() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         Model[0] = 9;

         var tuple = new EnumTupleElementViewModel(ViewPort, 0, 0, new TupleSegment("name", 8, "names"), default, default);

         Assert.Equal("9", tuple.FilterText);
      }

      private TupleArrayElementViewModel TupleTable => (TupleArrayElementViewModel)ViewPort.Tools.TableTool.Children.Where(child => child is TupleArrayElementViewModel).Single();
   }
}
