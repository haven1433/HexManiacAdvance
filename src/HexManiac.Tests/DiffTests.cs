using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class DiffTests {
      private readonly EditorViewModel editor;

      public DiffTests() {
         editor = new EditorViewModel(new StubFileSystem(), InstantDispatch.Instance);
         editor.Open.Execute(new LoadedFile("Left.gba", new byte[0x100]));
         editor.Open.Execute(new LoadedFile("Right.gba", new byte[0x100]));
         ViewModel0.Width = 16;
         ViewModel1.Width = 16;
      }

      private IViewPort ViewModel0 => (IViewPort)editor[0];
      private IViewPort ViewModel1 => (IViewPort)editor[1];
      private IViewPort ViewModel2 => (IViewPort)editor[2];
      private IDataModel Model0 => ViewModel0.Model;
      private IDataModel Model1 => ViewModel1.Model;
      private void Edit0(string text) => ((ViewPort)editor[0]).Edit(text);
      private void Edit1(string text) => ((ViewPort)editor[1]).Edit(text);

      [Fact]
      public void TwoTabs_Diff_NameIsFromBothTabs() {
         Model1[0] = 1;

         editor.DiffRight(editor[0]);

         Assert.Equal("Left -> Right", editor[2].Name);
      }

      [Fact]
      public void TwoTabsWithSameData_Diff_NoNewTab() {
         editor.DiffRight(editor[0]);
         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void TwoTabs_Diff_FullWidthPlusOne() {
         Model1[0] = 1;

         editor.DiffRight(editor[0]);

         Assert.Equal(16 + 1 + 16, ((IViewPort)editor[2]).Width);
      }

      [Fact]
      public void TwoTabs_Diff_DiffBytesAreSelected() {
         Model1[0] = 1;

         editor.DiffRight(editor[0]);

         Assert.True(ViewModel2.IsSelected(new Point(17, 1)));
      }

      // TODO if the left/right sides don't have the same number of lines (because the formatting of the bytes is different), render extra blank lines to make the height match
      // TODO jump down 1 screen height doesn't work right, only goes down ~4 lines?
   }
}
