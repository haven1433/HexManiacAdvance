using Xunit;

using Span = HavenSoft.HexManiac.Core.Models.Runs.SortedSpan<int>;

namespace HavenSoft.HexManiac.Tests {
   public class SortedSpanTests {
      [Fact]
      public void Empty_IsEmpty() {
         Assert.Empty(Span.None);
      }

      [Fact]
      public void Empty_AddOne_HasOne() {
         Assert.Single(Span.None.Add1(1));
      }

      [Fact]
      public void One_AddDifferent_Two() {
         Assert.Equal(2, new Span(1).Add1(2).Count);
      }

      [Fact]
      public void One_AddSame_One() {
         Assert.Single(new Span(1).Add1(1));
      }

      [Fact]
      public void Many_AddOthers_Sorted() {
         var set1 = new Span(new [] { 1,3,5 });
         var set2 = new Span(new[] { 2, 3, 4 });
         var result = set1.Add(set2);

         Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
      }
   }
}
