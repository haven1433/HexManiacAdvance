using HavenSoft.HexManiac.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PasteScriptTests : BaseViewModelTestClass {

      [Fact]
      public void ThumbPasteScript_Paste_Compiles() {
         ViewPort.Edit(@"
@10
.thumb
push {lr}
pop  {pc}
.end
30
");

         Assert.Equal(0xB5_00, Model.ReadMultiByteValue(0x10, 2));
         Assert.Equal(0xBD_00, Model.ReadMultiByteValue(0x12, 2));
         Assert.Equal(0x30, Model[0x14]);
      }
   }
}
