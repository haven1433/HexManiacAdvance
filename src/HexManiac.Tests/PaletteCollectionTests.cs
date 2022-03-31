using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using Xunit;
using System.Linq;

namespace HavenSoft.HexManiac.Tests {
   public class PaletteCollectionTests : BaseViewModelTestClass {
      private readonly StubFileSystem fileSystem = new StubFileSystem();

      public PaletteCollectionTests() {
         ViewPort.Edit("@80 <pal> @00 ^pal`ucp4`");
      }

      [Fact]
      public void CanCopy() {
         var collection = ViewPort.Tools.SpriteTool.Colors;

         collection.SelectionStart = 0;
         collection.Copy.Execute(fileSystem);

         Assert.Equal("0:0:0", fileSystem.CopyText);
      }

      [Fact]
      public void CanPaste() {
         var collection = ViewPort.Tools.SpriteTool.Colors;

         collection.SelectionStart = 0;
         fileSystem.CopyText = "1:1:1";
         collection.Paste.Execute(fileSystem);

         Assert.Equal(0b0_00001_00001_00001, Model.ReadMultiByteValue(0, 2));
      }

      [Fact]
      public void CompleteDoesNotChangeDataIfNoReorder() {
         ViewPort.Edit("@00 10 20 00 00 @00 ^pal`lzp4`");
         var collection = ViewPort.Tools.SpriteTool.Colors;

         collection.SelectionStart = 2;
         collection.HandleMove(2, 2);
         collection.CompleteCurrentInteraction();

         Assert.Equal(0x00, Model[4]);
      }

      [Fact]
      public void CompressedDataWithBadLength_Reorder_DataRepointed() {
         Model.ClearFormat(Token, 0, Model.Count);
         SetFullModel(0xFF);
         // good palette
         ViewPort.Edit("@40 10 20 00 00 00 4F 4F BE 21 1B 43 A5 5E 00 E4 49 43 39 9F 4F 5E 47 00 FE 12 42 08 1F 33 5F 22 00 BA 29 15 01 5A 6B FF 7F ");
         // bad palette
         ViewPort.Edit("@80 10 20 00 00 00 4F 4F BE 21 1B 43 A5 5E 00 E4 49 43 39 9F 4F 5E 47 00 FE 12 42 08 1F 33 5F 22 08 BA 29 FF 7F 30 01 ");
         // pointers in collections to 'sync' the palettes
         ViewPort.Edit("@04 <040> <080> @04 ");
         ViewPort.Edit("^table1[ptr<`lzp4`>]1 @08 ^table2[ptr<`lzp4`>]table1 @40 ");

         var paletteViewModel = (PaletteElementViewModel)ViewPort.Tools.TableTool.Children.Last(child => child is PaletteElementViewModel);
         var collection = paletteViewModel.Colors;
         collection.SelectionStart = 2;
         collection.HandleMove(2, 3);
         collection.CompleteCurrentInteraction();

         Assert.NotEqual(0x80, Model.ReadPointer(0x8));
         Assert.Single(Messages);
         Assert.Empty(Errors);
      }
   }
}
