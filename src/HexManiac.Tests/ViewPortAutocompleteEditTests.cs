using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ViewPortAutocompleteEditTests {
      private readonly ViewPort viewPort;

         public ViewPortAutocompleteEditTests() {
         var model = new PokemonModel(new byte[0x200]);
         viewPort = new ViewPort("name.txt", model) { Height = 0x10, Width = 0x10 };

         viewPort.SelectionStart = new Point(0, 8);
         viewPort.Edit("^label ");

         viewPort.SelectionStart = new Point(4, 8);
         viewPort.Edit("^labwork ");

         viewPort.SelectionStart = new Point(8, 8);
         viewPort.Edit("^othertext ");

         viewPort.SelectionStart = new Point(12, 8);
         viewPort.Edit("^sometext ");

         viewPort.SelectionStart = new Point();
      }

      [SkippableFact]
      public void UnderEditLoosePointerGetsAutoComplete() {
         Skip.If(true);
         viewPort.Edit("<labe");

         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.Single(format.AutocompleteOptions);

         format = (UnderEdit)viewPort[1, 0].Format;
         Assert.Null(format.AutocompleteOptions);
      }

      [SkippableFact]
      public void BackspaceWidensAutocompleteResults() {
         Skip.If(true);
         viewPort.Edit("<labe");
         viewPort.Edit(ConsoleKey.Backspace);

         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.Equal(2, format.AutocompleteOptions.Count);
      }

      [SkippableFact]
      public void UpDownDuringAutoCompleteSelectsResults() {
         Skip.If(true);
         viewPort.Edit("<lab");
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.True(format.AutocompleteOptions[0].IsSelected);
      }

      [SkippableFact]
      public void EmptyAutoCompleteArrowsActNormally() {
         Skip.If(true);
         viewPort.Edit("<xyz");
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.IsNotType<UnderEdit>(viewPort[0, 0].Format);
      }

      [SkippableFact]
      public void AutocompleteWorksForEnums() {
         Skip.If(true);
         throw new NotImplementedException();
      }
   }
}
