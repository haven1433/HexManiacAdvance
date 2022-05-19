using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ToolTipTests : BaseViewModelTestClass {
      private readonly ToolTipContentVisitor visitor;

      public ToolTipTests() => visitor = new ToolTipContentVisitor(Model);

      [Fact]
      public void Pointer_DisplayText_ContentIncludes() {
         var format = new Pointer(default, default, default, default, "SomeLocation", default);

         format.Visit(visitor, default);

         Assert.Equal("<SomeLocation>", visitor.Content.Single());
      }

      [Fact]
      public void MatchedWord_ToolTip_DisplaysParentTableName() {
         var format = new MatchedWord(default, default, "table");

         format.Visit(visitor, default);

         Assert.Equal("table", visitor.Content.Single());
      }

      [Fact]
      public void IntEnum_HasDisplayValue_ShowInTooltip() {
         var format = new IntegerEnum(default, default, "Craig", default);

         format.Visit(visitor, default);

         Assert.Equal("Craig", visitor.Content.Single());
      }

      [Fact]
      public void Integer_IsNamedConstant_HasTooltip() {
         var format = new Integer(default, default, 150, 0);
         Model.ObserveRunWritten(ViewPort.CurrentChange, new WordRun(0, "value", 2, -2, 1, "note"));

         format.Visit(visitor, default);

         Assert.Equal(
            Environment.NewLine.Join(new[] {
               "value-2",
               "note",
               "Changing one copy of a constant will automatically update all other copies."
            }),
            visitor.Content.Single());
      }

      [Fact]
      public void BitArray_HasValues_ShowInToolTip() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         CreateEnumTable("enums", 0x180, "names", 0, 1, 2, 3);
         CreateBitArrayTable("bits", 0, "enums", 0xF);
         var format = new BitArray(default, default, 1, default);

         format.Visit(visitor, Model[0]);

         Assert.Equal(new[] { "adam", "bob", "carl", "dave" }, visitor.Content);
      }

      [Fact]
      public void BitArray_Empty_ShowNoneInToolTip() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         CreateEnumTable("enums", 0x180, "names", 0, 1, 2, 3);
         CreateBitArrayTable("bits", 0, "enums", 0);
         var format = new BitArray(default, default, 1, default);

         format.Visit(visitor, Model[0]);

         Assert.Equal("- None -", visitor.Content.Single());
      }
   }
}
