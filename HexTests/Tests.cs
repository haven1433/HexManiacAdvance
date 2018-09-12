using HavenSoft.Gen3Hex.ViewModel;
using System.Reflection;
using Xunit;

[assembly: AssemblyTitle("HexTests")]

namespace HexTests {
   public class Tests {
      [Fact]
      public void ViewPortNotifiesOnSizeChange() {
         var viewPort = new ViewPort();
         var counter = 0;
         viewPort.PropertyChanged += (sender, e) => counter++;

         viewPort.Width = 12;
         viewPort.Height = 50;

         Assert.Equal(2, counter);
      }
   }
}
