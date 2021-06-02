using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class DiffTests {
      [Fact]
      public void TwoTabs_Diff_NameIsFromBothTabs() {
         var editor = new EditorViewModel(new StubFileSystem(), InstantDispatch.Instance);
         editor.Open.Execute(new LoadedFile("Left.gba", new byte[0x100]));
         editor.Open.Execute(new LoadedFile("Right.gba", new byte[0x100]));
         ((ViewPort)editor[1]).Edit("01");

         editor.DiffRight(editor[0]);

         Assert.Equal("Left -> Right", editor[2].Name);
      }
   }
}
