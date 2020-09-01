using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class NamedByteTests : BaseViewModelTestClass {
      [Fact]
      public void TwoByteRuns_ChangeOne_ChangesTheOther() {
         ViewPort.Edit(".counter @02 .counter ");
         var run1 = (WordRun)Model.GetNextRun(0);
         var run2 = (WordRun)Model.GetNextRun(2);

         ViewPort.Edit("3 ");

         Assert.Equal(0, run1.Start);
         Assert.Equal(2, run2.Start);
         Assert.Equal(Model[run1.Start], Model[run2.Start]);
      }

      [Fact]
      public void SearchForByteRunName_ReturnsByteRuns() {
         ViewPort.Edit(".counter @02 .counter @00 ");

         var results = ViewPort.Find("counter");

         Assert.Equal((0, 0), results[0]);
         Assert.Equal((2, 2), results[1]);
      }

      [Fact]
      public void Goto_ByteNamePartialText_ReturnsByteRunName() {
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(ViewPort);
         ViewPort.Edit(".counter ");

         editor.GotoViewModel.Text = "count";

         Assert.Contains("counter", editor.GotoViewModel.AutoCompleteOptions.Select(option => option.CompletionText));
      }

      [Fact]
      public void ByteName_Goto_CreatesNewTab() {
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(ViewPort);
         ViewPort.Edit(".counter @02 .counter ");

         ViewPort.Goto.Execute("counter");

         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void ByteRuns_SupportOffset() {
         ViewPort.Edit(".counter-2 ");

         var format = (Integer)ViewPort[0, 0].Format;
         Assert.Equal(0, format.Value); // the display value matches the readback value, which includes the offset
      }

      [Fact]
      public void ByteRunOffsetCannotCauseNegativeValue() {
         ViewPort.Edit(".counter+2 ");
         Assert.Single(Errors);
      }

      [Fact]
      public void ByteRunOffsetCannotCauseOverflowValue() {
         ViewPort.Edit(".counter-70 200 ");
         Assert.Single(Errors);
      }

      [Fact]
      public void ByteRun_CanSerialize() {
         ViewPort.Edit(".counter-2 ");

         var newModel = new PokemonModel(Model.RawData, Model.ExportMetadata(Singletons.MetadataInfo), Singletons);

         var run = (WordRun)newModel.GetNextRun(0);
         Assert.Equal(1, run.Length);
         Assert.Equal(-2, run.ValueOffset);
      }
   }
}
