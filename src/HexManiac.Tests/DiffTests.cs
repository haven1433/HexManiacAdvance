using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class DiffTests {
      private readonly EditorViewModel editor;

      public DiffTests() {
         editor = new EditorViewModel(new StubFileSystem(), InstantDispatch.Instance);
         editor.Open.Execute(new LoadedFile("Left.gba", new byte[0x200]));
         editor.Open.Execute(new LoadedFile("Right.gba", new byte[0x200]));
         ViewModel0.Width = 16;
         ViewModel1.Width = 16;
         ViewModel0.Height = 16;
         ViewModel1.Height = 16;
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

         editor.DiffRight.Execute(editor[0]);

         Assert.Equal("Left -> Right", editor[2].Name);
      }

      [Fact]
      public void TwoTabsWithSameData_Diff_NoNewTab() {
         editor.DiffRight.Execute(editor[0]);
         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void TwoTabs_Diff_FullWidthPlusOne() {
         Model1[0] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.Equal(16 + 1 + 16, ((IViewPort)editor[2]).Width);
      }

      [Fact]
      public void TwoTabs_Diff_DiffBytesAreSelected() {
         Model1[0] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.True(ViewModel2.IsSelected(new Point(17, 0)));
      }

      [Fact]
      public void TwoTabs_DifferentFormatsForDiff_AlignmentStillMatches() {
         Edit1("^table[a: b: c:]4 1 2 3 4 5 6 7 8 9 10 11 12 ");
         Model1[0x100] = 1;

         editor.DiffRight.Execute(editor[0]);

         Assert.All(10.Range(), y => {
            var leftIsUndefined = ViewModel2[0, y].Format == Undefined.Instance;
            var rightIsUndefined = ViewModel2[17, y].Format == Undefined.Instance;
            Assert.Equal(leftIsUndefined, rightIsUndefined);
         });
      }

      [Fact]
      public void TwoTabs_TwoDifferences_SeeBothDifferences() {
         Model1[0x000] = 10;
         Model1[0x100] = 20;

         editor.DiffRight.Execute(editor[0]);

         Assert.IsNotType<Undefined>(ViewModel2[0, 6].Format);
      }

      [Fact]
      public void DiffTab_PageDown_MoveDownOnePage() {
         foreach (var address in 7.Range().Select(i => i * 0x50))
            Model1[address] = 1;
         editor.DiffRight.Execute(editor[0]);

         ViewModel2.Scroll.Execute(Direction.PageDown);

         Assert.Equal(16, ViewModel2.ScrollValue);
      }

      [Fact]
      public void LeftMostTab_DiffLeft_CanNotExecute() {
         Assert.False(editor.DiffLeft.CanExecute(ViewModel0));
      }

      [Fact]
      public void RightMostTab_DiffRight_CanNotExecute() {
         Assert.False(editor.DiffRight.CanExecute(ViewModel1));
      }

      [Fact]
      public void LeftMostTab_ExecuteDiffLeft_NoTabAdded() {
         editor.DiffLeft.Execute(ViewModel0);
         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void RightMostTab_ExecuteDiffRight_NoTabAdded() {
         editor.DiffRight.Execute(ViewModel1);
         Assert.Equal(2, editor.Count);
      }

      // TODO test that CanExecute is disabled for non-viewport tabs
   }
}
