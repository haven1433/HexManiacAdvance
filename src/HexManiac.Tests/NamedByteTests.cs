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
         ViewPort.Edit(".some.counter @02 .some.counter ");
         var run1 = (WordRun)Model.GetNextRun(0);
         var run2 = (WordRun)Model.GetNextRun(2);

         ViewPort.Edit("3 ");

         Assert.Equal(0, run1.Start);
         Assert.Equal(2, run2.Start);
         Assert.Equal(Model[run1.Start], Model[run2.Start]);
      }

      [Fact]
      public void SearchForByteRunName_ReturnsByteRuns() {
         ViewPort.Edit(".some.counter @02 .some.counter @00 ");

         var results = ViewPort.Find("some.counter");

         Assert.Equal((0, 0), results[0]);
         Assert.Equal((2, 2), results[1]);
      }

      [Fact]
      public void Goto_ByteNamePartialText_ReturnsByteRunName() {
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(ViewPort);
         ViewPort.Edit(".some.counter ");

         editor.GotoViewModel.Text = "some.count";

         Assert.Contains("some.counter", editor.GotoViewModel.PrefixSelections[0].Tokens.Select(token => token.Content));
      }

      [Fact]
      public void ByteName_Goto_CreatesNewTab() {
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(ViewPort);
         ViewPort.Edit(".some.counter @02 .some.counter ");

         ViewPort.Goto.Execute("some.counter");

         Assert.Equal(2, editor.Count);
      }

      [Fact]
      public void ByteRuns_SupportOffset() {
         ViewPort.Edit(".some.counter-2 ");

         var format = (Integer)ViewPort[0, 0].Format;
         Assert.Equal(0, format.Value); // the display value matches the readback value, which includes the offset
      }

      [Fact]
      public void ByteRunOffsetCannotCauseNegativeValue() {
         ViewPort.Edit(".some.counter+2 ");
         Assert.Single(Errors);
      }

      [Fact]
      public void ByteRunOffsetCannotCauseOverflowValue() {
         ViewPort.Edit(".some.counter-70 200 ");
         Assert.Single(Errors);
      }

      [Fact]
      public void ByteRun_CanSerialize() {
         ViewPort.Edit(".some.counter-2 ");

         var newModel = new PokemonModel(Model.RawData, Model.ExportMetadata(Singletons.MetadataInfo), Singletons);

         var run = (WordRun)newModel.GetNextRun(0);
         Assert.Equal(1, run.Length);
         Assert.Equal(-2, run.ValueOffset);
      }

      [Fact]
      public void OldMetadata_UpgradeVersion_LoadConstants() {
         var anchors = new[] { new StoredAnchor(0, "bob", string.Empty) };
         var info = new StubMetadataInfo { VersionNumber = "0.3.0.0" };
         var metadata = new StoredMetadata(anchors, default, default, default, default, default, info, default, default, default);
         var gameReferenceTables = new GameReferenceTables(new ReferenceTable[0]);
         var singletons = new Singletons(
            new StubMetadataInfo { VersionNumber = "0.4.0.0" },
            new Dictionary<string, GameReferenceTables> { { "BPRE0", gameReferenceTables } },
            Singletons.GameReferenceConstants
         );

         var data = new byte[0x1000000];
         for (int i = 0; i < 4; i++) data[0xAC + i] = (byte)"BPRE"[i];
         // constant only gets added if all the values match... but for scripts.shiny.odds, all but one instance all have a -1 modifier.
         data[0x104A24] = 1;
         var model = new PokemonModel(data, metadata, singletons);

         var matches = model.GetMatchedWords("scripts.shiny.odds");
         Assert.Equal(6, matches.Count);
      }

      [Fact]
      public void NamedConstant_CreateTableWithMatchingLength_TableHasExpectedLength() {
         ViewPort.Edit(".some.length 12 @04 ^table[a:]some.length ");

         Assert.Equal(12, Model.GetTable("table").ElementCount);
      }

      [Fact]
      public void TableWithNamedConstantLength_ExpandTable_ConstantUpdates() {
         ViewPort.Edit(".some.length 12 @04 ^table[a:]some.length ");

         ViewPort.Edit("@1C +");

         var table = Model.GetTable("table");
         Assert.Equal(13, table.ElementCount);
         Assert.Equal(13, Model[0]);
      }

      [Fact]
      public void TableWithNamedConstantLength_DecreaseConstant_TableLengthChanges() {
         ViewPort.Edit(".some.length 12 @04 ^table[a:]some.length ");

         ViewPort.Edit("@00 11 ");

         var table = Model.GetTable("table");
         Assert.Equal(11, Model[0]);
         Assert.Equal(11, table.ElementCount);
      }

      [Fact]
      public void TableWithNamedConstantLength_IncreaseConstant_TableLengthChanges() {
         ViewPort.Edit(".some.length 12 @04 ^table[a:]some.length ");

         ViewPort.Edit("@00 13 ");

         var table = Model.GetTable("table");
         Assert.Equal(13, Model[0]);
         Assert.Equal(13, table.ElementCount);
      }

      [Theory]
      [InlineData(":: ")]
      [InlineData(": ")]
      [InlineData(". ")]
      public void ByteToken_NoName_Error(string edit) {
         ViewPort.Edit(edit);

         Assert.Single(Errors);
      }

      [Fact]
      public void MatchedWordOutOfRange_InitMetadata_IgnoreMatchedWord() {
         var metadata = new StoredMetadata(matchedWords: new[] { new StoredMatchedWord(0x300, "name", 2, 0, 1, default) });

         var model = New.PokemonModel(Data, metadata, Singletons);

         Assert.Empty(model.GetMatchedWords("name"));
      }

      [Fact]
      public void MatchedWordOutOfRange_LoadMetadata_IgnoreMatchedWords() {
         var metadata = new StoredMetadata(matchedWords: new[] { new StoredMatchedWord(0x300, "name", 2, 0, 1, default) });

         Model.LoadMetadata(metadata);

         Assert.Empty(Model.GetMatchedWords("name"));
      }

      [Fact]
      public void TypingConstant_TypeEquals_SetsValue() {
         ViewPort.Edit(".some.constant=3 ");
         Assert.Equal(3, Model[0]);
         Assert.Equal(new Point(1, 0), ViewPort.SelectionStart);
      }

      [Fact]
      public void TypingConstantWithOffset_TypeEquals_SetsValue() {
         ViewPort.Edit(".some.constant+4=12 ");
         Assert.Equal(12, Model[0]);
      }

      [Fact]
      public void NamedConstant_Copy_CopyConstantFormat() {
         ViewPort.Edit(".some.constant ");

         ViewPort.SelectionStart = new(0, 0);
         ViewPort.SelectionEnd = new(0, 0);
         ViewPort.Copy.Execute(FileSystem);

         Assert.Equal(".some.constant=0 ", FileSystem.CopyText.value);
      }
   }
}
