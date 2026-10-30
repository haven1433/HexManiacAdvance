using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>A list with a missing entry in the choices of a tuple field is shown as its number instead of crashing the drawing of the table.</summary>
   public class TupleNullOptionTests : BaseViewModelTestClass {
      [Fact]
      public void TupleField_WithAListThatHasAHole_ShowsTheNumber() {
         Model.SetList(new ModelDelta(), "holes", new string[] { "zero", null, "two" }, new Dictionary<int, string>(), null);
         var segment = new ArrayRunTupleSegment("t", "a:holes|", 1);
         Model[0] = 1;
         Assert.Equal("(1)", segment.ToText(Model, 0));
         Model[0] = 2;
         Assert.Equal("(two)", segment.ToText(Model, 0));
         Model[0] = 3;
         Assert.Equal("(3)", segment.ToText(Model, 0));
      }
   }
}
