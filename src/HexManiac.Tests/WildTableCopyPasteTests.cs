using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// 'Copy table' / 'Paste table' of the wild encounter row editor: the whole list as plain text,
   /// in the format of HMA's original text editor for these lists ("5, 10, bob" per line, "# comment" lines).
   /// </summary>
   public class WildTableCopyPasteTests : BaseViewModelTestClass {
      private const int TableStart = 0x20, ListStart = 0x80;

      private TableRowsStreamElementViewModel CreateEncounterList(string listFormat = "[lowLevel. highLevel. species:names]3") {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave", "Mr. Mime");
         var encounters = new[] { (5, 10, 1), (6, 12, 2), (7, 14, 3) };
         for (int i = 0; i < encounters.Length; i++) {
            Model[ListStart + i * 4 + 0] = (byte)encounters[i].Item1;
            Model[ListStart + i * 4 + 1] = (byte)encounters[i].Item2;
            Model[ListStart + i * 4 + 2] = (byte)encounters[i].Item3;
            Model[ListStart + i * 4 + 3] = 0;
         }
         Model[TableStart + 4] = ListStart;
         Model[TableStart + 7] = 0x08;
         ViewPort.Edit($"@{TableStart:X2} ^wild[rate. unused. unused. unused. list<{listFormat}>]1 ");
         ViewPort.Goto.Execute(TableStart);
         ViewPort.ChangeHistory.ChangeCompleted();
         Errors.Clear();
         return CurrentRows;
      }

      private TableRowsStreamElementViewModel CurrentRows => ViewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Single();

      private byte[] ListBytes => Enumerable.Range(ListStart, 12).Select(i => Model[i]).ToArray();

      private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

      [Fact]
      public void CopyTable_PutsTheOldTextFormatOnTheClipboard() {
         var rows = CreateEncounterList();
         var copiedFlags = 0;
         rows.Bind(nameof(rows.ShowCopied), (sender, e) => { if (sender.ShowCopied) copiedFlags++; });

         rows.CopyTable(FileSystem);
         rows.CopyTable(FileSystem); // a second copy flashes "Copied!" again

         Assert.Equal(Lines("5, 10, bob", "6, 12, carl", "7, 14, dave"), FileSystem.CopyText.value);
         Assert.Equal(2, copiedFlags);
         Assert.Empty(Errors);
         Assert.Contains(Messages, message => message.StartsWith("Copied the 3 rows"));
      }

      [Fact]
      public void CopyThenPaste_RoundTrip_SameData() {
         var rows = CreateEncounterList();
         rows.CopyTable(FileSystem);
         var copied = FileSystem.CopyText.value;
         var before = ListBytes;

         ((NumericTableCellViewModel)rows.Rows[0].Cells[0]).Content = "40";
         ViewPort.ChangeHistory.ChangeCompleted();
         rows.PasteTable(FileSystem);

         Assert.Empty(Errors);
         Assert.Equal(before, ListBytes);
         Assert.Equal("5", ((NumericTableCellViewModel)CurrentRows.Rows[0].Cells[0]).Content);
      }

      [Fact]
      public void PasteUpstreamText_WritesEveryRow() {
         var rows = CreateEncounterList();
         // the text the original HMA's text editor shows for an encounter list (FireRed's has '# comment' lines between the groups)
         FileSystem.CopyText.value = "# 20% common\r\n2, 3, adam\r\n4, 5, DAVE\r\n\r\n# 10% uncommon\r\n9, 11, \"Mr. Mime\"\r\n";

         rows.PasteTable(FileSystem);

         Assert.Empty(Errors);
         Assert.Equal(new byte[] { 2, 3, 0, 0, 4, 5, 3, 0, 9, 11, 4, 0 }, ListBytes);
         var cells = CurrentRows.Rows[2].Cells;
         Assert.Equal("9", ((NumericTableCellViewModel)cells[0]).Content);
         Assert.Contains("Mr. Mime", ((EnumTableCellViewModel)cells[2]).FilteringComboOptions.DisplayText);
         Assert.Contains("9, 11, \"Mr. Mime\"", CurrentRows.Content);
      }

      [Theory]
      [InlineData("2 3 adam\n4 5 bob\n6 7 carl")] // no commas
      [InlineData("+2, 3, adam\n+4, 5, bob\n+6, 7, carl")] // the '+' a deep copy puts in front of the extra rows
      [InlineData("  2, 3, adam  \n\n4, 5, bob\n6, 7, carl\n")] // spaces and empty lines
      [InlineData("0x02, 0x03, adam\n4, 5, bob\n6, 7, 2")] // hex levels, species by number
      public void PasteVariants_AreRead(string text) {
         var rows = CreateEncounterList();

         Assert.True(rows.TryPasteTable(text, out var error), error);

         Assert.Equal(new byte[] { 2, 3, 0, 0, 4, 5, 1, 0, 6, 7, 2, 0 }, ListBytes);
      }

      [Fact]
      public void PasteNameWithSpacesWithoutQuotes_IsRead() {
         var rows = CreateEncounterList();

         Assert.True(rows.TryPasteTable("1, 2, adam\n3, 4, Mr. Mime\n5, 6, bob", out var error), error);

         Assert.Equal(4, Model[ListStart + 6]);
      }

      [Theory]
      [InlineData("2, 3, adam\n4, 5, bob", "Expected 3 lines")] // too few rows
      [InlineData("2, 3, adam\n4, 5, bob\n6, 7, carl\n8, 9, dave", "Expected 3 lines")] // too many rows
      [InlineData("2, 3, adam\n4, x, bob\n6, 7, carl", "\"x\" is not a number")]
      [InlineData("2, 3, adam\n4, 5, zzzzzz\n6, 7, carl", "\"zzzzzz\" is not a known")]
      [InlineData("2, 3, adam\n4, 300, bob\n6, 7, carl", "out of range")]
      [InlineData("2, 3\n4, 5, bob\n6, 7, carl", "expected 3 values")]
      [InlineData("", "no text")]
      [InlineData("   \n  ", "no text")]
      public void PasteInvalidText_Error_NothingChanges(string text, string expectedError) {
         var rows = CreateEncounterList();
         var before = ListBytes;
         var contentBefore = rows.Content;
         FileSystem.CopyText.value = text;

         rows.PasteTable(FileSystem);

         var error = Assert.Single(Errors);
         Assert.Contains(expectedError, error);
         Assert.Equal(before, ListBytes);
         Assert.Equal(contentBefore, CurrentRows.Content);
      }

      [Fact]
      public void Paste_Undo_RestoresEveryRowInOneStep() {
         var rows = CreateEncounterList();
         ((NumericTableCellViewModel)rows.Rows[0].Cells[0]).Content = "8"; // an edit typed before the paste is its own step
         var afterEdit = ListBytes;

         Assert.True(rows.TryPasteTable("2, 3, adam\n4, 5, bob\n6, 7, carl", out var error), error);
         Assert.Equal(new byte[] { 2, 3, 0, 0, 4, 5, 1, 0, 6, 7, 2, 0 }, ListBytes);

         ViewPort.Undo.Execute();
         Assert.Equal(afterEdit, ListBytes);
         Assert.Equal("8", ((NumericTableCellViewModel)CurrentRows.Rows[0].Cells[0]).Content);
         Assert.Equal("dave", ((EnumTableCellViewModel)CurrentRows.Rows[2].Cells[2]).FilteringComboOptions.DisplayText);

         ViewPort.Redo.Execute();
         Assert.Equal(new byte[] { 2, 3, 0, 0, 4, 5, 1, 0, 6, 7, 2, 0 }, ListBytes);
      }

      [Fact]
      public void Paste_KeepsTheSameBoxes() {
         var rows = CreateEncounterList();
         var cell = rows.Rows[1].Cells[1];

         Assert.True(rows.TryPasteTable("2, 3, adam\n4, 5, bob\n6, 7, carl", out _));

         Assert.Same(rows, CurrentRows);
         Assert.Same(cell, CurrentRows.Rows[1].Cells[1]);
         Assert.Equal("5", ((NumericTableCellViewModel)cell).Content);
      }

      [Fact]
      public void ListWithComments_CopyIncludesCommentLines_PasteSkipsThem() {
         var rows = CreateEncounterList("[a|comment=0|common b|comment=2|rare lowLevel. highLevel. species:names]3");

         rows.CopyTable(FileSystem);
         var text = FileSystem.CopyText.value;
         Assert.Contains("# common", text);
         Assert.Contains("# rare", text);

         Assert.True(rows.TryPasteTable(text.Replace("7, 14, dave", "7, 14, adam"), out var error), error);
         Assert.Equal(0, Model[ListStart + 8 + 2]);
         Assert.Equal(5, Model[ListStart]);
      }

      /// <summary>The real CUBE wild table: copy the first list, change it with text, undo. Skipped without the ROM.</summary>
      [SkippableFact]
      public void Cube_CopyChangePasteUndo() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(toml)));
         model.InitializationWorkload.Wait();
         var viewPort = new ViewPort(path, model, InstantDispatch.Instance, Singletons);
         var errors = new System.Collections.Generic.List<string>();
         viewPort.OnError += (sender, e) => { if (!string.IsNullOrEmpty(e)) errors.Add(e); };
         var wild = model.GetTable(HardcodeTablesModel.WildTableName);
         viewPort.Goto.Execute(wild.Start);
         var rows = viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().First(editor => editor.Rows.Count > 0);
         var fs = new StubFileSystem();

         rows.CopyTable(fs);
         var text = fs.CopyText.value;
         var lines = text.Split(Environment.NewLine).Where(line => line.Length > 0 && !line.StartsWith("#")).ToList();
         var count = rows.Rows.Count;
         Assert.Equal(count, lines.Count);
         var listStart = rows.Rows[0].Cells[0].Start;
         var before = Enumerable.Range(listStart, count * 4).Select(i => model[i]).ToArray();

         // same text back: no change; then the first row's levels changed by text
         Assert.True(rows.TryPasteTable(text, out var error), error);
         Assert.Equal(before, Enumerable.Range(listStart, count * 4).Select(i => model[i]).ToArray());
         var species = lines[0].Split(',', 3)[2].Trim();
         var first = text.IndexOf(lines[0]);
         var changed = text.Substring(0, first) + $"42, 43, {species}" + text.Substring(first + lines[0].Length); // only the first row (other rows can have the same text)
         Assert.True(rows.TryPasteTable(changed, out error), error);
         Assert.Equal(42, model[listStart]);
         Assert.Equal(43, model[listStart + 1]);
         Assert.Equal(before.Skip(2), Enumerable.Range(listStart + 2, count * 4 - 2).Select(i => model[i]));

         viewPort.Undo.Execute();
         Assert.Equal(before, Enumerable.Range(listStart, count * 4).Select(i => model[i]).ToArray());
         Assert.Empty(errors);
      }
   }
}
