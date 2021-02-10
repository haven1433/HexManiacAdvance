using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class EggMoveTests {
      private readonly byte[] data;
      private readonly PokemonModel model;
      private readonly ViewPort viewPort;
      private readonly List<string> messages = new List<string>();

      #region Setup

      public EggMoveTests() {
         data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.gba", model, InstantDispatch.Instance);

         viewPort.Goto.Execute("000080");
         viewPort.Edit($"^{HardcodeTablesModel.PokemonNameTable}[name\"\"8]8 \"Bob\" \"Steve\" \"Carl\" \"Sam\" \"Bryan\" \"Ryan\" \"Ian\" \"Matt\"");

         viewPort.Goto.Execute("000100");
         viewPort.Edit($"^{HardcodeTablesModel.MoveNamesTable}[name\"\"8]9 \"Fire\" \"Water\" \"Earth\" \"Wind\" \"Light\" \"Dark\" \"Normal\" \"Magic\" \"Th is\"");

         viewPort.Goto.Execute("000180");
         viewPort.Edit("<000000> Dead Beef 01 00 00 00 <000000>"); // limiter is at 188 for an eggrun at 000

         viewPort.Goto.Execute("000000");

         viewPort.OnMessage += (sender, e) => messages.Add(e);
      }

      private void CreateSimpleRun() {
         var token = new ModelDelta();
         model.WriteMultiByteValue(0, 2, token, EggMoveRun.MagicNumber + 2); // Carl
         model.WriteMultiByteValue(2, 2, token, 3);                          // Wind

         viewPort.Edit($"^{HardcodeTablesModel.EggMovesTableName}`egg` ");
      }

      #endregion

      [Fact]
      public void CanCreateEggMoveStream() {
         viewPort.Edit($"^{HardcodeTablesModel.EggMovesTableName}`egg` ");

         Assert.Equal(2, model.GetNextRun(0).Length);
      }

      [Fact]
      public void CanSeeEggMoveStreamWithCorrectFormat() {
         CreateSimpleRun();

         Assert.Equal(6, model.GetNextRun(0).Length);
         var section = (EggSection)viewPort[1, 0].Format;
         var item = (EggItem)viewPort[2, 0].Format;
         var endSection = (EggSection)viewPort[5, 0].Format;
         Assert.Equal("[Carl]", section.SectionName);
         Assert.Equal("Wind", item.ItemName);
         Assert.Equal("[]", endSection.SectionName);
      }

      [Fact]
      public void SelectionDoneInPairs() {
         CreateSimpleRun();

         viewPort.SelectionStart = new Point(2, 0); // should select "Wind"
         Assert.True(viewPort.IsSelected(new Point(3, 0)));

         viewPort.MoveSelectionStart.Execute(Direction.Right); // should select "[]"
         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
      }

      [Fact]
      public void CanEditEggStreamManually() {
         CreateSimpleRun();

         viewPort.Edit("Dark ");
         Assert.Equal(5, model[0]);

         viewPort.Edit("[Bryan]");
         Assert.Equal(EggMoveRun.MagicNumber + 4, model.ReadMultiByteValue(2, 2));
      }

      [Fact]
      public void CanCopyPaste() {
         CreateSimpleRun();
         var fileSystem = new StubFileSystem();

         viewPort.SelectionStart = new Point(2, 0);
         viewPort.SelectionEnd = new Point(4, 0);
         viewPort.Copy.Execute(fileSystem);

         Assert.Equal("Wind []", fileSystem.CopyText.value);
      }

      [Fact]
      public void RunAutoExtends() {
         CreateSimpleRun();
         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("[Carl]");

         Assert.Equal(8, model.GetNextRun(0).Length);
         Assert.Equal(2, model.ReadMultiByteValue(0x188, 4));
      }

      [Fact]
      public void RunAutoMoves() {
         viewPort.Edit($"^{HardcodeTablesModel.EggMovesTableName}`egg` ");
         model.WriteMultiByteValue(2, 2, new ModelDelta(), 0x0206);
         viewPort.Edit("[Carl]");

         Assert.Single(messages);
      }

      [Fact]
      public void RunAutoShortens() {
         CreateSimpleRun();
         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Edit("[]");

         Assert.Equal(4, model.GetNextRun(0).Length);
         Assert.Equal(0, model.ReadMultiByteValue(0x188, 4));
      }

      [Fact]
      public void CanViewInTextTool() {
         CreateSimpleRun();
         viewPort.SelectionStart = new Point(2, 2); // select off it
         viewPort.SelectionStart = new Point(2, 0); // and then back on
         viewPort.Tools.StringToolCommand.Execute();

         Assert.Equal(@"[Carl]
Wind", viewPort.Tools.StringTool.Content);

         viewPort.Tools.StringTool.Content = @"[Carl]
Earth
Light
[Ryan]
Fire
Water";

         Assert.Equal(14, model.GetNextRun(0).Length);
         Assert.Equal(5, model.ReadMultiByteValue(0x188, 4));
      }

      [Fact]
      public void FollowLinkOpensTextTool() {
         CreateSimpleRun();
         viewPort.SelectionStart = new Point(2, 0);
         viewPort.FollowLink(2, 0);

         Assert.Equal(viewPort.Tools.IndexOf(viewPort.Tools.StringTool), viewPort.Tools.SelectedIndex);
      }

      [Fact]
      public void RightClickOptionToOpenTextTool() {
         CreateSimpleRun();
         viewPort.SelectionStart = new Point(2, 0);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         Assert.Contains(items, item => item.ShortcutText == "Ctrl+Click");
      }

      [Fact]
      public void ToolCursorChangesCauseSelectionChanges() {
         CreateSimpleRun();
         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Tools.StringToolCommand.Execute();
         viewPort.Tools.StringTool.ContentIndex = 5;

         Assert.True(viewPort.IsSelected(new Point(0, 0)));
         Assert.True(viewPort.IsSelected(new Point(1, 0)));
      }

      [Fact]
      public void SearchFindsEggMoveData() {
         CreateSimpleRun();

         var pairs = viewPort.Find("wind");
         Assert.Contains((2, 3), pairs);

         pairs = viewPort.Find("carl");
         Assert.Contains((0, 1), pairs);
      }

      [Fact]
      public void BackspaceWorks() {
         CreateSimpleRun();

         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Edit(ConsoleKey.Backspace);

         var format = (UnderEdit)viewPort[2, 0].Format;
         Assert.Equal("Win", format.CurrentText); // Wind, but backspaced
      }

      /// <summary>
      /// Since egg moves are multiple cells long, we want to know that
      /// changes get reverted when 'selectionstart' is the rightmost cell
      /// </summary>
      [Fact]
      public void SelectLeftThenBackspaceThenDownCompletesEdits() {
         CreateSimpleRun();

         viewPort.SelectionStart = new Point(2, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.IsNotType<UnderEdit>(viewPort[1, 0].Format);
      }

      [Fact]
      public void CanClearCellContent() {
         CreateSimpleRun();

         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Edit(ConsoleKey.Backspace); // d
         viewPort.Edit(ConsoleKey.Backspace); // n
         viewPort.Edit(ConsoleKey.Backspace); // i
         viewPort.Edit(ConsoleKey.Backspace); // W

         var format = (UnderEdit)viewPort[2, 0].Format;
         Assert.Equal(string.Empty, format.CurrentText);
      }

      [Fact]
      public void CanRemoveEggMoveFormatFromContextMenu() {
         CreateSimpleRun();

         viewPort.SelectionStart = new Point(2, 0);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Clear Format");
      }

      [Fact]
      public void EggMoves_AutocompletePokemon_GetPokemonOptions() {
         CreateSimpleRun();
         var run = (EggMoveRun)model.GetNextRun(0);

         var options = run.GetAutoCompleteOptions("[an", 3, 3);

         Assert.Equal(3, options.Count);
         Assert.Equal("[Bryan]", options[0].Text);
         Assert.Equal("[Ryan]", options[1].Text);
         Assert.Equal("[Ian]", options[2].Text);
         Assert.All(options, option => Assert.Equal(option.Text, option.LineText));
      }

      [Fact]
      public void EggMoves_AutocompleteMove_GetMoveOptions() {
         CreateSimpleRun();
         var run = (EggMoveRun)model.GetNextRun(0);

         var options = run.GetAutoCompleteOptions("er", 2, 2);

         Assert.Equal(2, options.Count);
         Assert.Equal("Water", options[0].Text);
         Assert.Equal("Earth", options[1].Text);
         Assert.All(options, option => Assert.Equal(option.Text, option.LineText));
      }

      [Fact]
      public void EggMovesWithQuotes_Autocomplete_NoQuotes() {
         CreateSimpleRun();
         var run = (EggMoveRun)model.GetNextRun(0);

         var options = run.GetAutoCompleteOptions("this", 2, 4);

         Assert.Equal("Th is", options[0].LineText);
      }
   }
}
