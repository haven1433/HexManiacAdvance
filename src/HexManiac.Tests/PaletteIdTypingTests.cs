using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PaletteIdTypingTests : BaseViewModelTestClass {
      public PaletteIdTypingTests() : base(0x400) { }

      private FieldArrayElementViewModel Field() {
         ViewPort.Edit("@60 <020> 00 00 <040> 01 00 @60 ^graphics.overworld.palettes[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit("@80 <000> 20 ");
         ViewPort.Edit("@A0 FF FF 05 10 40 00 08 00 08 00 <080>");
         ViewPort.Edit("@A0 ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1 ");
         return new FieldArrayElementViewModel(ViewPort, "paletteid", 0xA2, 2, HexFieldStrategy.Instance);
      }

      [Theory]
      [InlineData("1002", 0x1002)]
      [InlineData("0x1002", 0x1002)]
      [InlineData("12AB", 0x12AB)]
      [InlineData("0x12ab", 0x12AB)]
      [InlineData("A", 0xA)]
      public void TypedHex_IsReadAsHex(string text, int expected) {
         var field = Field();
         field.Content = text;
         Assert.Equal(expected, Model.ReadMultiByteValue(0xA2, 2));
         Assert.False(field.IsInError);
      }

      [Theory]
      [InlineData("abcg")]
      [InlineData("")]
      [InlineData("0x")]
      [InlineData("12345")]   // too big for 2 bytes
      public void NotAValue_IsAnErrorAndChangesNothing(string text) {
         var field = Field();
         field.Content = text;
         Assert.True(field.IsInError);
         Assert.Equal(0x1005, Model.ReadMultiByteValue(0xA2, 2));
      }
   }
}
