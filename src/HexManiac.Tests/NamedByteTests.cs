using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
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

      [Fact]
      public void OldMetadata_UpgradeVersion_LoadConstants() {
         var anchors = new[] { new StoredAnchor(0, "bob", string.Empty) };
         var info = new StubMetadataInfo { VersionNumber = "0.3.0.0" };
         var metadata = new StoredMetadata(anchors, default, default, default, default, info, default, default);
         var gameReferenceTables = new GameReferenceTables(new ReferenceTable[0]);
         var singletons = new Singletons(
            new StubMetadataInfo { VersionNumber = "0.4.0.0" },
            new Dictionary<string, GameReferenceTables> { { "BPRE0", gameReferenceTables }
         });

         var data = new byte[0x1000000];
         for (int i = 0; i < 4; i++) data[0xAC + i] = (byte)"BPRE"[i];
         var model = new PokemonModel(data, metadata, singletons);

         var matches = model.GetMatchedWords("scripts.shiny.odds");
         Assert.Equal(6, matches.Count);
      }

      [Fact]
      public void NamedConstant_CreateTableWithMatchingLength_TableHasExpectedLength() {
         ViewPort.Edit(".length 12 @04 ^table[a:]length ");

         Assert.Equal(12, Model.GetTable("table").ElementCount);
      }

      [Fact]
      public void TableWithNamedConstantLength_ExpandTable_ConstantUpdates() {
         ViewPort.Edit(".length 12 @04 ^table[a:]length ");

         ViewPort.Edit("@1C +");

         var table = Model.GetTable("table");
         Assert.Equal(13, table.ElementCount);
         Assert.Equal(13, Model[0]);
      }

      [Fact]
      public void TableWithNamedConstantLength_DecreaseConstant_TableLengthChanges() {
         ViewPort.Edit(".length 12 @04 ^table[a:]length ");

         ViewPort.Edit("@00 11 ");

         var table = Model.GetTable("table");
         Assert.Equal(11, Model[0]);
         Assert.Equal(11, table.ElementCount);
      }

      [Fact]
      public void TableWithNamedConstantLength_IncreaseConstant_TableLengthChanges() {
         ViewPort.Edit(".length 12 @04 ^table[a:]length ");

         ViewPort.Edit("@00 13 ");

         var table = Model.GetTable("table");
         Assert.Equal(13, Model[0]);
         Assert.Equal(13, table.ElementCount);
      }
   }
}
