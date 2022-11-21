using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Tests;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ConstantTests : BaseViewModelTestClass {

      [Fact]
      public void ConstantFormat_TypeConstantFormat_AcceptInput() {
         ViewPort.Edit(".some.number ");

         ViewPort.Edit(".some.number");

         var edit = (UnderEdit)ViewPort[0, 0].Format;
         Assert.Equal(".some.number", edit.CurrentText);
      }

      [Fact]
      public void ConstantFormat_TypeSameFormatOver_Unchanged() {
         ViewPort.Edit(".some.number-1 ");

         ViewPort.Edit(".some.number-1 ");

         var format = (Integer)ViewPort[0, 0].Format;
         Assert.Equal(0, format.Value);
      }

      [Fact]
      public void ConstantFormat_EnterNewConstantOnByteWithDifferentValue_CurrentValueCHangesToMatchConstant() {
         ViewPort.Edit("@100 03 @100 .some.number+1 ");

         ViewPort.Edit("@000 .some.number ");

         Assert.Equal(2, Model[0]);
      }

      [Fact]
      public void NameTableWithInnerPointers_LowerConstant_ShorterTableKeepsInnerPointers() {
         SetFullModel(0xFF);
         CreateTextTableWithInnerPointers("names", 0x100, "adam", "bob", "carl", "dave", "eric", "fred", "0123456789ABDCE");
         ViewPort.Edit("@000 <100> <110> <120> <130> <140> <150> <160> ");
         ViewPort.Edit("@080 07 @080 .some.number @100 ^names^[name\"\"16]some.number ");

         Assert.Empty(Errors);

         ViewPort.Edit("@080 6 ");

         var array = (ArrayRun)Model.GetNextRun(0x100);
         Assert.All(array.PointerSourcesForInnerElements, span => Assert.Single(span));
         Assert.IsType<NoInfoRun>(Model.GetNextRun(0x100 + 0x60));
      }
   }
}
