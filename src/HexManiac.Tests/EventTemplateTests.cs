using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>Templates that write scripts must keep working when a hack changes the script commands or the specials.</summary>
   public class EventTemplateTests : BaseViewModelTestClass {
      private const string VanillaSetWildBattle = "B6 setwildbattle species: level. item:";
      private const string ExpansionSetWildBattle = "B6 setwildbattle species: level. item: species2: level2. item2:";

      private static ScriptParser ParserWith(params string[] engineLines) {
         var lines = engineLines.Select(line => (IScriptLine)new XSEScriptLine(line)).ToList();
         return new ScriptParser(0, lines, 0x02);
      }

      private static List<string> Names(int count, params (int index, string name)[] named) {
         var list = Enumerable.Range(0, count).Select(i => (string)null).ToList();
         foreach (var (index, name) in named) list[index] = name;
         return list;
      }

      #region script command signatures

      [Fact]
      public void ArgumentCount_ComesFromTheScriptReference() {
         Assert.Equal(3, ParserWith(VanillaSetWildBattle).GetArgumentCount("setwildbattle"));
         Assert.Equal(6, ParserWith(ExpansionSetWildBattle).GetArgumentCount("setwildbattle"));
      }

      [Fact]
      public void ArgumentCount_UnknownCommand_IsMinusOne() {
         Assert.Equal(-1, ParserWith(VanillaSetWildBattle).GetArgumentCount("notacommand"));
      }

      [Fact]
      public void BuildCommand_PadsWithZerosUpToTheSignature() {
         Assert.Equal("setwildbattle 1 50 0", ParserWith(VanillaSetWildBattle).BuildCommand("setwildbattle", "1", "50", "0"));
         Assert.Equal("setwildbattle 1 50 0 0 0 0", ParserWith(ExpansionSetWildBattle).BuildCommand("setwildbattle", "1", "50", "0"));
      }

      [Fact]
      public void BuildCommand_PaddedLineCompilesWithTheMatchingEngine() {
         // the line the legendary template used to write has 3 arguments: it doesn't compile against the expansion's 6-argument command
         var parser = ParserWith(ExpansionSetWildBattle);
         var tooShort = "setwildbattle 1 50 0";
         Assert.Throws<ScriptCompileException>(() => parser.CompileWithoutErrors(Token, Model, 0, ref tooShort));

         var line = parser.BuildCommand("setwildbattle", "1", "50", "0");
         var bytes = parser.CompileWithoutErrors(Token, Model, 0, ref line);
         Assert.Equal(1 + 10 + 1, bytes.Length); // the command, its 10 bytes of arguments (2+1+2+2+1+2), and the end of the script
         Assert.Equal(0xB6, bytes[0]);
      }

      [Fact]
      public void CompileWithoutErrors_BadScript_SaysWhatIsWrongInsteadOfReturningNull() {
         var parser = ParserWith(VanillaSetWildBattle);
         var script = "setwildbattle 1";
         var error = Assert.Throws<ScriptCompileException>(() => parser.CompileWithoutErrors(Token, Model, 0, ref script));
         Assert.Contains("expects 3 arguments", error.Message);
         Assert.Single(error.Errors);
      }

      #endregion

      #region legendary

      [Fact]
      public void LegendaryScript_Vanilla_StaysTheSame() {
         SetGameCode("BPEE0");
         var script = EventTemplate.BuildLegendaryScript(Model, ParserWith(VanillaSetWildBattle), 0x0900, -1);
         Assert.Contains("setwildbattle 1 50 0" + Environment.NewLine, script);
         Assert.Contains("setflag 0x08C1", script);
         Assert.Contains("special 0x13B", script);
         Assert.Contains("setflag 0x0900", script);
      }

      [Fact]
      public void LegendaryScript_Expansion_SetWildBattleHasAllItsArguments() {
         SetGameCode("BPEE0");
         var script = EventTemplate.BuildLegendaryScript(Model, ParserWith(ExpansionSetWildBattle), 0x0900, -1);
         Assert.Contains("setwildbattle 1 50 0 0 0 0" + Environment.NewLine, script);
      }

      [Fact]
      public void LegendaryScript_UsesTheSpecialAndFlagNamesOfTheRom() {
         SetGameCode("BPEE0");
         // the expansion renamed the special and moved it: the vanilla number 0x13B is a different special there
         Model.SetList(new ModelDelta(), "specials", Names(0x140, (0x138, "BattleSetup_StartLegendaryBattle"), (0x13B, "DoSealedChamberShakingEffect_Short")), null, null);
         Model.SetList(new ModelDelta(), Flags.FlagListName, Names(0x900, (0x8C3, "FLAG_SYS_CTRL_OBJ_DELETE")), null, null);

         var script = EventTemplate.BuildLegendaryScript(Model, ParserWith(VanillaSetWildBattle), 0x0900, -1);

         Assert.Contains("special BattleSetup_StartLegendaryBattle", script);
         Assert.DoesNotContain("0x13B", script);
         Assert.Contains("setflag 0x08C3", script);
         Assert.Contains("clearflag 0x08C3", script);
      }

      [Fact]
      public void LegendaryScript_VanillaSpecialName_IsAlsoFound() {
         SetGameCode("BPEE0");
         Model.SetList(new ModelDelta(), "specials", Names(0x140, (0x13B, "StartLegendaryBattle")), null, null);
         var script = EventTemplate.BuildLegendaryScript(Model, ParserWith(VanillaSetWildBattle), 0x0900, -1);
         Assert.Contains("special StartLegendaryBattle", script);
      }

      [Fact]
      public void LegendaryScript_FireRed_WaitsForAButtonBeforeTheBattle() {
         SetGameCode("BPRE0");
         var script = EventTemplate.BuildLegendaryScript(Model, ParserWith(VanillaSetWildBattle), 0x0900, 0x123456);
         Assert.Contains("preparemsg <123456>", script);
         Assert.Contains("waitkeypress", script);
         Assert.Contains("setflag 0x0807", script);
         Assert.Contains("special 0x138", script);
      }

      #endregion

      #region trade

      private static string Section(string script, string label) {
         var start = script.IndexOf(label + ":", StringComparison.Ordinal);
         Assert.True(start >= 0, $"no label {label}");
         var end = script.IndexOf("end", start, StringComparison.Ordinal);
         return script.Substring(start, end - start + 3);
      }

      [Fact]
      public void TradeScript_Expansion_RereadsBothSpeciesNamesBeforeComplainingAboutTheWrongPokemon() {
         // the party menu draws "/ 20" (the Pokémon's HP) with the same string buffers the messages use,
         // so after it the wrong-species message needs the names again
         var script = EventTemplate.BuildTradeScript(true, 3, 0x0961, 0x100, 0x110, 0x120, 0x130, 0x140);
         var wrong = Section(script, "wrongspecies");

         var reread = wrong.IndexOf("special2 0x800D GetInGameTradeSpeciesInfo", StringComparison.Ordinal);
         var message = wrong.IndexOf("loadpointer 0 <000140>", StringComparison.Ordinal);
         Assert.True(reread >= 0);
         Assert.True(reread < message);
         Assert.Contains("copyvar 0x8005 0x8008", wrong.Substring(0, reread)); // expansion: the trade number goes in 0x8005
      }

      [Fact]
      public void TradeScript_Vanilla_RereadsBothSpeciesNamesToo() {
         var script = EventTemplate.BuildTradeScript(false, 3, 0x0961, 0x100, 0x110, 0x120, 0x130, 0x140);
         var wrong = Section(script, "wrongspecies");

         var reread = wrong.IndexOf("special2 0x800D GetInGameTradeSpeciesInfo", StringComparison.Ordinal);
         Assert.True(reread >= 0);
         Assert.True(reread < wrong.IndexOf("loadpointer 0 <000140>", StringComparison.Ordinal));
         Assert.Contains("copyvar 0x8004 0x8008", wrong.Substring(0, reread)); // vanilla: the trade number goes in 0x8004
      }

      [Fact]
      public void TradeScript_OtherMessages_DoNotNeedTheNamesAgain() {
         var script = EventTemplate.BuildTradeScript(true, 0, 0x0961, 0x100, 0x110, 0x120, 0x130, 0x140);
         // exactly two reads of the species info: the one at the start and the one before the wrong-species message
         var count = 0;
         for (var i = script.IndexOf("GetInGameTradeSpeciesInfo", StringComparison.Ordinal); i >= 0; i = script.IndexOf("GetInGameTradeSpeciesInfo", i + 1, StringComparison.Ordinal)) count++;
         Assert.Equal(2, count);
      }

      [Fact]
      public void TradeScript_KeepsTheLayoutTheTradeEditorLooksFor() {
         // GetTradeContent finds the messages by the order of the loadpointer commands: info, thanks, success, fail, wrong species
         var script = EventTemplate.BuildTradeScript(true, 0, 0x0961, 0x100, 0x110, 0x120, 0x130, 0x140);
         var order = new[] { "<000100>", "<000110>", "<000120>", "<000130>", "<000140>" }.Select(text => script.IndexOf(text, StringComparison.Ordinal)).ToArray();
         Assert.All(order, position => Assert.True(position >= 0));
         Assert.Equal(order.OrderBy(position => position), order);
      }

      #endregion

      #region trade text

      // The widest a species name can be: 13 characters (the name field of the expansion's species table), and no glyph of the font is wider than 6 pixels.
      private const int WidestSpeciesName = 13 * 6;

      // The pixel width of the normal font's glyphs (the font width table of the ROM): everything not listed is 6 pixels wide.
      private static int GlyphWidth(byte pcs) => pcs switch {
         0x00 or 0xAD or 0xB4 or 0xB8 => 3, // space . ' ,
         0xAB or 0xDD or 0xE0 => 4,         // ! i l
         0xDE or 0xE6 => 5,                 // j r
         _ => 6,
      };

      /// <summary>
      /// Lays out a message the way the text box does: returns the pixel width of every line and the row of the box (0 or 1) it is printed on.
      /// A new line goes to the next row, \p (0xFB) clears the box, \l (0xFA) scrolls the box up so the new line is on the bottom row again.
      /// A species buffer (0xFD and its id) counts as the widest species name.
      /// </summary>
      private static List<(int row, int width)> Lines(IReadOnlyList<byte> text) {
         var lines = new List<(int row, int width)>();
         int row = 0, width = 0;
         for (int i = 0; i < text.Count && text[i] != 0xFF; i++) {
            switch (text[i]) {
               case 0xFD: width += WidestSpeciesName; i++; break;
               case 0xFE: lines.Add((row, width)); row++; width = 0; break;
               case 0xFB: lines.Add((row, width)); row = 0; width = 0; break;
               case 0xFA: lines.Add((row, width)); row = 1; width = 0; break;
               default: width += GlyphWidth(text[i]); break;
            }
         }
         lines.Add((row, width));
         return lines;
      }

      private static int IndexOf(IReadOnlyList<byte> text, byte first, byte second) {
         for (int i = 0; i + 1 < text.Count; i++) if (text[i] == first && text[i + 1] == second) return i;
         return -1;
      }

      [Fact]
      public void TradeTexts_UseEscapesForLineBreaks_NotLineBreakCharacters() {
         // The text converter only knows the line break of the platform it runs on (Environment.NewLine): on Windows a '\n' character
         // is silently dropped, and "Want to trade your Nidorina\nfor my Nidorino?" came out as "...Nidorinafor my Nidorino" in the game.
         foreach (var text in EventTemplate.TradeTexts) {
            Assert.False(text.Contains('\n'), text);
            Assert.False(text.Contains('\r'), text);
         }
      }

      [Fact]
      public void TradeInfo_BreaksTheLineRightAfterTheFirstSpecies() {
         var text = Model.TextConverter.Convert(EventTemplate.TradeInfoText, out _);
         var first = IndexOf(text, 0xFD, 0x02);  // [buffer1]: the species the NPC wants
         var second = IndexOf(text, 0xFD, 0x03); // [buffer2]: the species the NPC offers

         Assert.True(first > 0, "the offer starts by naming the first species");
         Assert.Equal((byte)0xFE, text[first + 2]);       // the next line starts right behind the name
         Assert.Equal(1, text.Count(b => b == 0xFE));
         Assert.True(second > first + 2, "the second species is named on the second line");
         Assert.Equal((byte)0x00, text[first - 1]);       // "your <name>": the space stays in front of the name
         Assert.NotEqual((byte)0x00, text[first + 3]);    // and the second line does not start with one
         Assert.Equal((byte)0xFF, text[text.Count - 1]);
      }

      [Fact]
      public void TradeInfo_ReadsWantToTradeYourSpeciesForMySpecies() {
         var expected = new List<byte>();
         expected.AddRange(Model.TextConverter.Convert("Want to trade your ", out _).Where(b => b != 0xFF));
         expected.AddRange(new byte[] { 0xFD, 0x02, 0xFE });
         expected.AddRange(Model.TextConverter.Convert("for my ", out _).Where(b => b != 0xFF));
         expected.AddRange(new byte[] { 0xFD, 0x03 });
         expected.AddRange(Model.TextConverter.Convert("?", out _));

         Assert.Equal(expected, Model.TextConverter.Convert(EventTemplate.TradeInfoText, out _));
      }

      [Fact]
      public void TradeTexts_NeverShowMoreThanTwoLinesInTheBox() {
         // a box shows two lines: a third needs \l (scroll) or \p (clear) in front of it
         foreach (var text in EventTemplate.TradeTexts) {
            var lines = Lines(Model.TextConverter.Convert(text, out _));
            Assert.All(lines, line => Assert.True(line.row <= 1, text));
         }
      }

      [Fact]
      public void TradeTexts_EveryLineFitsTheBox_EvenWithTheLongestSpeciesName() {
         foreach (var text in EventTemplate.TradeTexts) {
            foreach (var line in Lines(Model.TextConverter.Convert(text, out _))) {
               Assert.True(line.width <= CodeBody.MaxEventTextWidth, $"{text}: {line.width}px");
            }
         }
      }

      #endregion

      #region tutor

      private const int TutorTableAddress = 0x100;
      private const int ChooseMonForMoveTutorSpecial = 5;

      /// <summary>
      /// A model with room for texts and scripts, the tables the tutor template reads, and one object event to put the template in.
      /// The expansion's trainers have another layout than the vanilla ones, and that is what tells the template which kind of ROM it is in.
      /// </summary>
      private (EventTemplate template, ObjectEventViewModel objectEvent, ScriptParser parser) TutorSetup(bool expansion, params int[] tutorMoves) {
         Model.ExpandData(Token, 0x3FFF); // everything past the first 0x200 bytes is free space now
         ViewPort.Edit("@000 ^objects[id. graphics: unused: x: y: elevation. moveType. range. trainerType. trainerRangeOrBerryID: script<> flag: unused:]2 ");
         ViewPort.Edit($"@{TutorTableAddress:X} ^{HardcodeTablesModel.MoveTutors}[move:]{tutorMoves.Length} ");
         for (int i = 0; i < tutorMoves.Length; i++) Model.WriteMultiByteValue(TutorTableAddress + i * 2, 2, Token, tutorMoves[i]);
         if (expansion) ViewPort.Edit($"@140 ^{HardcodeTablesModel.TrainerTableName}[class. sprite. name\"\"16 pokemon<>]2 ");
         Model.SetList(Token, "specials", Enumerable.Range(0, 10).Select(i => i == ChooseMonForMoveTutorSpecial ? "ChooseMonForMoveTutor" : i == 7 ? "DisableMsgBoxWalkaway" : "special" + i), null, null);
         Model.SetList(Token, "FacingOptions", Enumerable.Range(0, 12).Select(i => "movement" + i), null, null); // the template makes the NPC stand still: movement type 8
         Model.LoadMetadata(BaseModel.GetDefaultMetadatas().First()); // the script lists the scripts compare against, like script_compare

         var parser = ViewPort.Tools.CodeTool.ScriptParser;
         parser.RefreshGameHash(Model); // the commands of the game the test picked (SetGameCode)
         var picture = new ReadonlyPixelViewModel(16, 32);
         var template = new EventTemplate(InstantDispatch.Instance, Model, parser, new IPixelViewModel[] { picture });
         var element = Model.GetTableModel("objects", () => Token)[0];
         var objectEvent = new ObjectEventViewModel(null, address => { }, element, template, new IPixelViewModel[] { picture }, picture, null);
         return (template, objectEvent, parser);
      }

      /// <summary>The bytes of the question that the script asks, up to and including the end of the text.</summary>
      private List<byte> TutorQuestion(TutorEventContent content) {
         var start = Model.ReadPointer(content.InfoPointer);
         var length = PCSString.ReadString(Model, start, true);
         return Enumerable.Range(start, length).Select(i => Model[i]).ToList();
      }

      [Fact]
      public void TutorInfo_AsksToLearnTheMoveNamedInTheFirstBuffer() {
         var expected = new List<byte>();
         expected.AddRange(Model.TextConverter.Convert("Want to learn the move ", out _).Where(b => b != 0xFF));
         expected.AddRange(new byte[] { 0xFD, 0x02 }); // [buffer1]: the name of the move, put there by the bufferattack command at the start of the script
         expected.AddRange(Model.TextConverter.Convert("?", out _));

         Assert.Equal(expected, Model.TextConverter.Convert(EventTemplate.TutorInfoText, out _));
      }

      [Fact]
      public void TutorInfo_StaysOnOneLineWithALongMoveName() {
         // the buffer counts as the widest name of the tests, 13 letters (the longest names of the vanilla games); the longest teachable move of the expansion has 16 and fills the box
         var lines = Lines(Model.TextConverter.Convert(EventTemplate.TutorInfoText, out _));
         Assert.Single(lines);
         Assert.True(lines[0].width <= CodeBody.MaxEventTextWidth, $"{lines[0].width}px");
      }

      [Fact]
      public void TutorTemplate_Expansion_NamesTheMoveItTeachesBeforeAsking() {
         var (template, objectEvent, parser) = TutorSetup(expansion: true, 57, 19);

         template.CreateTutor(objectEvent, Token);

         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);
         Assert.NotNull(content);
         Assert.NotEqual(Pointer.NULL, content.MoveBufferAddress);
         Assert.Equal(0x82, Model[content.MoveBufferAddress - 2]);                          // bufferattack ...
         Assert.Equal(0, Model[content.MoveBufferAddress - 1]);                             // ... into the first buffer ([buffer1], FD 02 in a text) ...
         Assert.Equal(57, Model.ReadMultiByteValue(content.MoveBufferAddress, 2));          // ... the move that the special is given
         Assert.Equal(57, Model.ReadMultiByteValue(content.TutorAddress, 2));
         Assert.True(content.MoveBufferAddress < content.InfoPointer, "the name is buffered before the question is asked");
         var question = TutorQuestion(content);
         var buffer = question.IndexOf(0xFD);
         Assert.True(buffer > 0, "the question has a buffer in it");
         Assert.Equal(Model[content.MoveBufferAddress - 1] + 2, question[buffer + 1]); // script buffer 0 is [buffer1], FD 02, buffer 1 is FD 03, ...
         Assert.Equal(Model.TextConverter.Convert(EventTemplate.TutorInfoText, out _), TutorQuestion(content));
      }

      [Fact]
      public void TutorTemplate_Vanilla_NamesTheMoveOfTheTutorTable() {
         // the vanilla scripts hand the special the number of the tutor, the name needs the move itself
         var (template, objectEvent, parser) = TutorSetup(expansion: false, 0x123, 0x2A);

         template.CreateTutor(objectEvent, Token);

         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);
         Assert.NotNull(content);
         Assert.Equal(0, Model.ReadMultiByteValue(content.TutorAddress, 2));
         Assert.Equal(0x123, Model.ReadMultiByteValue(content.MoveBufferAddress, 2));
         Assert.Equal(Model.TextConverter.Convert(EventTemplate.TutorInfoText, out _), TutorQuestion(content));
      }

      [Fact]
      public void TutorTemplate_FireRed_NamesTheMoveToo() {
         SetGameCode("BPRE0");
         var (template, objectEvent, parser) = TutorSetup(expansion: false, 0x123, 0x2A);

         template.CreateTutor(objectEvent, Token);

         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);
         Assert.NotNull(content);
         Assert.Equal(0x123, Model.ReadMultiByteValue(content.MoveBufferAddress, 2));
         Assert.Equal(Model.TextConverter.Convert(EventTemplate.TutorInfoText, out _), TutorQuestion(content));
      }

      [Theory]
      [InlineData(false)]
      [InlineData(true)]
      public void TutorTemplate_ScriptFitsTheSpaceItLooksFor(bool fireRed) {
         // FireRed/LeafGreen scripts have a few more commands around the messages
         if (fireRed) SetGameCode("BPRE0");
         var (template, objectEvent, parser) = TutorSetup(expansion: !fireRed, 57);

         template.CreateTutor(objectEvent, Token);

         var length = parser.FindLength(Model, objectEvent.ScriptAddress);
         Assert.InRange(length, 90, EventTemplate.TutorScriptSpace);
      }

      [Fact]
      public void TutorMove_ChosenInThePanel_IsWhatTheQuestionNames() {
         var (template, objectEvent, parser) = TutorSetup(expansion: true, 57, 19);
         template.CreateTutor(objectEvent, Token);
         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);

         Model.WriteMultiByteValue(content.TutorAddress, 2, Token, 19); // what the panel's list does
         EventTemplate.UpdateTutorMoveName(Model, content, 19, Token);

         Assert.Equal(19, Model.ReadMultiByteValue(content.MoveBufferAddress, 2));
         Assert.Equal(Model.TextConverter.Convert(EventTemplate.TutorInfoText, out _), TutorQuestion(content)); // the text itself never changes: it names whatever is in the buffer
      }

      [Fact]
      public void TutorMove_Vanilla_ChosenInThePanel_NamesTheMoveOfThatTutor() {
         var (template, objectEvent, parser) = TutorSetup(expansion: false, 0x123, 0x2A, 0x77);
         template.CreateTutor(objectEvent, Token);
         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);

         EventTemplate.UpdateTutorMoveName(Model, content, 2, Token);

         Assert.Equal(0x77, Model.ReadMultiByteValue(content.MoveBufferAddress, 2));
      }

      [Fact]
      public void TutorMove_TutorsWithoutABufferedMove_AreLeftAlone() {
         // tutors made before the template named the move ask "Want to learn a cool move?": there is nothing to keep up to date
         var (template, objectEvent, parser) = TutorSetup(expansion: true, 57, 19);
         template.CreateTutor(objectEvent, Token);
         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);
         for (int i = 0; i < 4; i++) Model[content.MoveBufferAddress - 2 + i] = 0x00; // nop
         var old = EventTemplate.GetTutorContent(Model, parser, objectEvent);
         Assert.NotNull(old);
         Assert.Equal(Pointer.NULL, old.MoveBufferAddress);
         var before = Model.RawData.ToArray();

         EventTemplate.UpdateTutorMoveName(Model, old, 19, Token);

         Assert.Equal(before, Model.RawData);
      }

      [Fact]
      public void TutorMove_BufferedFromAVariable_IsLeftAlone() {
         // a hand-written script may buffer the content of the variable that the special reads: that follows the panel's choice on its own
         var (template, objectEvent, parser) = TutorSetup(expansion: true, 57, 19);
         template.CreateTutor(objectEvent, Token);
         var content = EventTemplate.GetTutorContent(Model, parser, objectEvent);
         Model.WriteMultiByteValue(content.MoveBufferAddress, 2, Token, 0x8005);

         EventTemplate.UpdateTutorMoveName(Model, content, 19, Token);

         Assert.Equal(0x8005, Model.ReadMultiByteValue(content.MoveBufferAddress, 2));
      }

      #endregion
   }
}
