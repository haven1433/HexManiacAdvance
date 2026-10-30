using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// setflag, clearflag, checkflag and the if.flag.* commands take a flag number.
   /// A ROM can also give names to some of the numbers with the list 'scriptnamedflags'
   /// (the battle options, the badges, the start menu entries).
   /// Those flags are shown, accepted and suggested by name. All other flags stay numbers.
   /// </summary>
   public class ScriptFlagNameTests : BaseViewModelTestClass {
      // Like the list in CUBE.toml: the index of a name is the flag number, and there are gaps.
      // 0x860 = 2144, 0x867 = 2151, 0x868 = 2152, 0x1155 = 4437, 0x1157 = 4439, 0x1158 = 4440
      private static readonly string[] NamedFlagsToml = {
         "[[List]]",
         "Name = '''scriptnamedflags'''",
         "2144 = [",
         "   '''FLAG_SYS_POKEMON_GET''',# Pokemon in the start menu",
         "]",
         "2151 = [",
         "   '''FLAG_BADGE01_GET''',# Stone Badge",
         "   '''FLAG_BADGE02_GET''',# Knuckle Badge",
         "]",
         "4437 = '''FLAG_DYNAMAX_BATTLE'''# Dynamax allowed (needs the Dynamax Band)",
         "4439 = [",
         "   '''FLAG_FORCE_NO_SHINY''',# wild and gift Pokemon are never shiny",
         "   '''FLAG_FORCE_SHINY''',# wild and gift Pokemon are always shiny",
         "]",
      };

      private CodeTool Tool => ViewPort.Tools.CodeTool;
      private ScriptParser Parser => ViewPort.Tools.CodeTool.ScriptParser;
      private string EventScript {
         get => ";".Join(Tool.Contents[0].Content.SplitLines().Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
         set {
            Tool.Mode = CodeMode.Script;
            Tool.Contents[0].Content = value.Replace(";", Environment.NewLine);
         }
      }

      private void LoadNamedFlags() => Model.LoadMetadata(new StoredMetadata(NamedFlagsToml));

      private byte[] Compile(string script) => Parser.Compile(Token, Model, 0x20, ref script, out var _);

      private string Decompile(string hexBytes) {
         var bytes = hexBytes.ToByteArray();
         Token.ChangeData(Model, 0, bytes);
         return Parser.Parse(Model, 0, bytes.Length);
      }

      private CodeBody CreateBody(string content) {
         var body = new CodeBody(Model, Parser, null);
         body.Content = content;
         return body;
      }

      private static int Count(IReadOnlyList<AutocompleteItem> items) => items?.Count ?? 0;

      #region Compile

      [Theory]
      [InlineData("setflag", 0x29)]
      [InlineData("clearflag", 0x2A)]
      [InlineData("checkflag", 0x2B)]
      public void FlagCommand_BattleFlagByName_CompilesLikeTheNumber(string command, int code) {
         LoadNamedFlags();

         var byName = Compile($"{command} FLAG_FORCE_SHINY");

         Assert.Equal(new byte[] { (byte)code, 0x58, 0x11 }, byName.Take(3).ToArray());
         Assert.Equal(Compile($"{command} 0x1158"), byName);
      }

      [Theory]
      [InlineData("FLAG_BADGE01_GET", 0x867)]
      [InlineData("FLAG_BADGE02_GET", 0x868)]
      [InlineData("FLAG_SYS_POKEMON_GET", 0x860)]
      [InlineData("FLAG_DYNAMAX_BATTLE", 0x1155)]
      [InlineData("FLAG_FORCE_NO_SHINY", 0x1157)]
      public void SetFlag_EveryNameInTheList_CompilesLikeItsNumber(string name, int flag) {
         LoadNamedFlags();

         var byName = Compile($"setflag {name}");

         Assert.Equal(new byte[] { 0x29, (byte)flag, (byte)(flag >> 8) }, byName.Take(3).ToArray());
         Assert.Equal(Compile($"setflag {flag}"), byName);
         Assert.Equal(Compile($"setflag 0x{flag:X}"), byName);
      }

      [Theory]
      [InlineData("if.flag.set.goto")]
      [InlineData("if.flag.set.call")]
      [InlineData("if.flag.clear.goto")]
      [InlineData("if.flag.clear.call")]
      public void IfFlagCommand_FlagByName_CompilesLikeTheNumber(string command) {
         LoadNamedFlags();

         var byName = Compile($"{command} FLAG_BADGE01_GET <080>");

         Assert.Equal(Compile($"{command} 0x867 <080>"), byName);
         Assert.Equal(new byte[] { 0x2B, 0x67, 0x08 }, byName.Take(3).ToArray());
      }

      [Theory]
      [InlineData("setflag 4440")]
      [InlineData("setflag 0x1158")]
      [InlineData("setflag 0X1158")]
      public void SetFlag_NumberOfANamedFlag_StillCompiles(string line) {
         LoadNamedFlags();
         Assert.Equal(new byte[] { 0x29, 0x58, 0x11 }, Compile(line).Take(3).ToArray());
      }

      [Theory]
      [InlineData("setflag 32")]
      [InlineData("setflag 0x20")]
      [InlineData("setflag 0x0020")]
      public void SetFlag_NumberOfAnUnnamedFlag_StillCompiles(string line) {
         LoadNamedFlags();
         Assert.Equal(new byte[] { 0x29, 0x20, 0x00 }, Compile(line).Take(3).ToArray());
      }

      [Fact]
      public void SetFlag_NameIsNotCaseSensitive() {
         LoadNamedFlags();
         var expected = Compile("setflag FLAG_FORCE_SHINY");

         Assert.Equal(expected, Compile("setflag flag_force_shiny"));
         Assert.Equal(expected, Compile("setflag Flag_Force_Shiny"));
      }

      [Fact]
      public void SetFlag_PartOfTheName_FindsTheFlag() {
         LoadNamedFlags();

         Assert.Equal(Compile("setflag 0x1158"), Compile("setflag force_shiny"));
         Assert.Equal(Compile("setflag 0x1155"), Compile("setflag dynamax"));
         Assert.Equal(Compile("setflag 0x867"), Compile("setflag badge01"));
      }

      [Fact]
      public void SetFlag_NameThatIsNotInTheList_IsAnError() {
         LoadNamedFlags();
         var errors = new List<string>();
         Parser.CompileError += (sender, e) => errors.Add(e.Message);

         Compile("setflag FLAG_PAUSE_TIME");

         Assert.Single(errors);
         Assert.Contains("FLAG_PAUSE_TIME", errors[0]);
      }

      [Fact]
      public void SetFlag_NotANumberOrName_IsAnError() {
         LoadNamedFlags();
         var errors = new List<string>();
         Parser.CompileError += (sender, e) => errors.Add(e.Message);

         Compile("setflag 12abc");

         Assert.Single(errors);
      }

      [Fact]
      public void SetFlag_RomWithoutTheList_NameIsAnErrorAndNumbersStillWork() {
         var errors = new List<string>();
         Parser.CompileError += (sender, e) => errors.Add(e.Message);

         Compile("setflag FLAG_FORCE_SHINY");
         var byNumber = Compile("setflag 0x1158");

         Assert.Single(errors);
         Assert.Contains("FLAG_FORCE_SHINY", errors[0]);
         Assert.Equal(new byte[] { 0x29, 0x58, 0x11 }, byNumber.Take(3).ToArray());
      }

      [Fact]
      public void ScriptEditor_TypeFlagName_WritesTheFlagNumber() {
         LoadNamedFlags();
         SetFullModel(0xFF);
         Tool.IsSelected = true;

         EventScript = "setflag FLAG_FORCE_SHINY;checkflag FLAG_BADGE02_GET;clearflag 0x20;end";

         Assert.Equal("29 58 11 2B 68 08 2A 20 00 02".ToByteArray(), Enumerable.Range(0, 10).Select(i => Model[i]).ToArray());
         Assert.False(Tool.ShowErrorText);
      }

      #endregion

      #region Decompile

      [Theory]
      [InlineData("29 58 11 02", "setflag FLAG_FORCE_SHINY")]
      [InlineData("2A 58 11 02", "clearflag FLAG_FORCE_SHINY")]
      [InlineData("2B 58 11 02", "checkflag FLAG_FORCE_SHINY")]
      [InlineData("29 67 08 02", "setflag FLAG_BADGE01_GET")]
      [InlineData("29 60 08 02", "setflag FLAG_SYS_POKEMON_GET")]
      [InlineData("29 55 11 02", "setflag FLAG_DYNAMAX_BATTLE")]
      public void FlagCommand_FlagWithAName_ShowsTheName(string bytes, string expected) {
         LoadNamedFlags();
         Assert.Contains(expected, Decompile(bytes));
      }

      [Theory]
      [InlineData("29 20 00 02", "setflag 0x0020")]
      [InlineData("29 61 08 02", "setflag 0x0861")]   // next to a named flag, but without a name
      [InlineData("29 59 11 02", "setflag 0x1159")]
      [InlineData("2B 00 40 02", "checkflag 0x4000")]
      public void FlagCommand_FlagWithoutAName_StaysANumber(string bytes, string expected) {
         LoadNamedFlags();
         Assert.Contains(expected, Decompile(bytes));
      }

      [Fact]
      public void FlagCommand_RomWithoutTheList_ShowsNumbers() {
         var text = Decompile("29 58 11 29 67 08 02");

         Assert.Contains("setflag 0x1158", text);
         Assert.Contains("setflag 0x0867", text);
         Assert.DoesNotContain("FLAG_", text);
      }

      [Theory]
      [InlineData("2B 58 11 06 01", "if.flag.set.goto FLAG_FORCE_SHINY")]
      [InlineData("2B 58 11 07 01", "if.flag.set.call FLAG_FORCE_SHINY")]
      [InlineData("2B 58 11 06 00", "if.flag.clear.goto FLAG_FORCE_SHINY")]
      [InlineData("2B 58 11 07 00", "if.flag.clear.call FLAG_FORCE_SHINY")]
      [InlineData("2B 20 00 07 00", "if.flag.clear.call 0x0020")]
      public void IfFlagCommand_Decompile_ShowsNameOnlyForNamedFlags(string bytes, string expected) {
         LoadNamedFlags();
         Assert.Contains(expected, Decompile(bytes + " 80 00 00 08 02"));
      }

      [Fact]
      public void FlagCommands_DecompileThenCompile_SameBytes() {
         LoadNamedFlags();
         var original = "29 58 11 2A 67 08 2B 20 00 2B 55 11 06 01 80 00 00 08 2B 60 08 07 00 80 00 00 08 29 FF 00 02".ToByteArray();
         Token.ChangeData(Model, 0x80, new byte[] { 0x02 });
         Token.ChangeData(Model, 0, original);

         var text = Parser.Parse(Model, 0, original.Length);
         var compiled = Parser.Compile(Token, Model, 0, ref text, out var _);

         Assert.Contains("FLAG_FORCE_SHINY", text);
         Assert.Contains("FLAG_BADGE01_GET", text);
         Assert.Contains("FLAG_DYNAMAX_BATTLE", text);
         Assert.Contains("FLAG_SYS_POKEMON_GET", text);
         Assert.Equal(original, compiled.Take(original.Length).ToArray());
      }

      [Fact]
      public void FlagCommands_EveryFlagNumber_ShowsANameOnlyForTheNamedOnes() {
         LoadNamedFlags();
         Model.TryGetList("scriptnamedflags", out var list);
         var named = Enumerable.Range(0, list.Count).Where(i => !string.IsNullOrEmpty(list[i]) && list[i] != i.ToString()).ToList();
         Assert.Equal(6, named.Count);

         foreach (var flag in new[] { 0, 1, 0x20, 0x860, 0x861, 0x867, 0x868, 0x869, 0xFFF, 0x1154, 0x1155, 0x1156, 0x1157, 0x1158, 0x1159, 0x115A, 0x4000 }) {
            var text = Decompile($"29 {flag & 0xFF:X2} {flag >> 8:X2} 02");
            if (named.Contains(flag)) {
               Assert.Contains($"setflag {list[flag]}", text);
            } else {
               Assert.Contains($"setflag 0x{flag:X4}", text);
            }
         }
      }

      #endregion

      #region Suggestions

      [Fact]
      public void ReadOptions_LetterTyped_ListsMatchingNamesWithTheirComments() {
         LoadNamedFlags();

         var options = Parser.ReadOptions(Model, "scriptnamedflags|h", "bad");

         Assert.Equal(new[] { "FLAG_BADGE01_GET # Stone Badge", "FLAG_BADGE02_GET # Knuckle Badge" }, options);
      }

      [Fact]
      public void ReadOptions_LowerCaseLetter_Matches() {
         LoadNamedFlags();

         var options = Parser.ReadOptions(Model, "scriptnamedflags|h", "f");

         Assert.Contains("FLAG_FORCE_SHINY # wild and gift Pokemon are always shiny", options);
         Assert.Contains("FLAG_SYS_POKEMON_GET # Pokemon in the start menu", options);
      }

      [Fact]
      public void ReadOptions_ManyMatches_ShowsAtMostNineAndDots() {
         Model.SetList(new ModelDelta(), "scriptnamedflags", Enumerable.Range(0, 20).Select(i => $"FLAG_{i:D2}").ToArray(), null, null);

         var options = Parser.ReadOptions(Model, "scriptnamedflags|h", "flag");

         Assert.Equal(10, options.Count);
         Assert.Equal("...", options[9]);
      }

      [Fact]
      public void ReadOptions_NamesAreSortedByNameNotByComment() {
         var comments = new Dictionary<int, string> { { 0, "a long comment about the first one" }, { 1, "short" } };
         Model.SetList(new ModelDelta(), "scriptnamedflags", new[] { "FLAG_AAA", "FLAG_AAB" }, comments, null);

         var options = Parser.ReadOptions(Model, "scriptnamedflags|h", "flag");

         // same name length, so the list order is kept: the comment must not push the first name down
         Assert.Equal(new[] { "FLAG_AAA # a long comment about the first one", "FLAG_AAB # short" }, options);
      }

      [Theory]
      [InlineData("")]
      [InlineData("1")]
      [InlineData("0")]
      [InlineData("0x")]
      [InlineData("0x1")]
      [InlineData("0X1158")]
      [InlineData("4440")]
      [InlineData("$1")]
      public void ReadOptions_NumberOrNothingTyped_ListsNothing(string token) {
         LoadNamedFlags();

         var options = Parser.ReadOptions(Model, "scriptnamedflags|h", token);

         Assert.NotNull(options);
         Assert.Empty(options);
      }

      [Fact]
      public void ReadOptions_NothingMatches_ListsNothing() {
         LoadNamedFlags();
         Assert.Empty(Parser.ReadOptions(Model, "scriptnamedflags|h", "zzz"));
      }

      [Fact]
      public void ReadOptions_RomWithoutTheList_ListsNothing() {
         var options = Parser.ReadOptions(Model, "scriptnamedflags|h", "bad");

         Assert.NotNull(options);
         Assert.Empty(options);
      }

      [Fact]
      public void ReadOptions_PlainList_StillListsEverythingForAnEmptyToken() {
         LoadNamedFlags();

         // only 'list|h' arguments are number-first: every other list behaves the way it did before
         var options = Parser.ReadOptions(Model, "scriptnamedflags", string.Empty);

         Assert.Equal(6, options.Count);
         Assert.Null(Parser.ReadOptions(Model, string.Empty, "bad"));
         Assert.Null(Parser.ReadOptions(Model, null, "bad"));
         Assert.Empty(Parser.ReadOptions(Model, "|h", "bad"));
      }

      [Theory]
      [InlineData("setflag")]
      [InlineData("clearflag")]
      [InlineData("checkflag")]
      [InlineData("if.flag.set.goto")]
      [InlineData("if.flag.set.call")]
      [InlineData("if.flag.clear.goto")]
      [InlineData("if.flag.clear.call")]
      public void Autocomplete_LetterTyped_OffersNamesAndReplacesTheToken(string command) {
         LoadNamedFlags();
         var body = CreateBody("end");
         var line = $"{command} bad";

         var items = body.GetTokenComplete(line, 0, line.Length);

         Assert.Equal(2, Count(items));
         Assert.Equal("FLAG_BADGE01_GET # Stone Badge", items[0].Text);
         Assert.StartsWith($"{command} FLAG_BADGE01_GET", items[0].LineText);
         Assert.DoesNotContain("#", items[0].LineText);
         Assert.StartsWith($"{command} FLAG_BADGE02_GET", items[1].LineText);
      }

      [Theory]
      [InlineData("setflag")]
      [InlineData("clearflag")]
      [InlineData("checkflag")]
      [InlineData("if.flag.set.goto")]
      [InlineData("if.flag.set.call")]
      [InlineData("if.flag.clear.goto")]
      [InlineData("if.flag.clear.call")]
      public void Autocomplete_NumberOrNothingTyped_OffersNothing(string command) {
         LoadNamedFlags();
         var body = CreateBody("end");

         foreach (var line in new[] { $"{command} ", $"{command} 1", $"{command} 0x", $"{command} 0x11", $"{command} 4440" }) {
            Assert.Equal(0, Count(body.GetTokenComplete(line, 0, line.Length)));
         }
      }

      [Theory]
      [InlineData("setflag")]
      [InlineData("clearflag")]
      [InlineData("checkflag")]
      public void Autocomplete_CompleteName_OffersNothing(string command) {
         LoadNamedFlags();
         var body = CreateBody("end");
         var line = $"{command} FLAG_FORCE_SHINY";

         Assert.Equal(0, Count(body.GetTokenComplete(line, 0, line.Length)));
      }

      [Fact]
      public void Autocomplete_RomWithoutTheList_OffersNothing() {
         var body = CreateBody("end");
         var line = "setflag bad";

         Assert.Equal(0, Count(body.GetTokenComplete(line, 0, line.Length)));
      }

      [Fact]
      public void Autocomplete_SecondArgumentAfterAFlag_IsNotAFlagName() {
         LoadNamedFlags();
         var body = CreateBody("end");
         var line = "if.flag.set.goto FLAG_BADGE01_GET b";

         // the pointer is not a flag: nothing from the flag list is offered there
         var items = body.GetTokenComplete(line, 0, line.Length);

         Assert.DoesNotContain(items ?? new List<AutocompleteItem>(), item => item.Text.StartsWith("FLAG_"));
      }

      [Fact]
      public void Help_LetterTyped_ListsNames() {
         LoadNamedFlags();
         var body = CreateBody("end");

         var help = Parser.GetHelp(Model, body, new HelpContext("setflag bad ", 11));

         Assert.Contains("FLAG_BADGE01_GET", help);
         Assert.Contains("FLAG_BADGE02_GET", help);
      }

      [Theory]
      [InlineData("setflag 12 ", 10)]
      [InlineData("setflag 0x1 ", 11)]
      [InlineData("setflag  ", 8)]
      public void Help_NumberOrNothingTyped_ListsNoNames(string line, int index) {
         LoadNamedFlags();
         var body = CreateBody("end");

         var help = Parser.GetHelp(Model, body, new HelpContext(line, index));

         Assert.DoesNotContain("FLAG_", help ?? string.Empty);
      }

      [Fact]
      public void Help_CompleteName_ListsNothing() {
         LoadNamedFlags();
         var body = CreateBody("end");

         Assert.Null(Parser.GetHelp(Model, body, new HelpContext("setflag FLAG_FORCE_SHINY ", 24)));
      }

      #endregion

      #region Find Uses

      [Fact]
      public void CanFindUses_CaretOnAFlagName_IsTrue() {
         LoadNamedFlags();
         var body = CreateBody("setflag FLAG_BADGE01_GET");

         body.CaretPosition = 12;

         Assert.True(body.CanFindUses);
      }

      [Fact]
      public void CanFindUses_CaretOnAFlagNumber_IsStillTrue() {
         LoadNamedFlags();
         var body = CreateBody("setflag 0x867");

         body.CaretPosition = 10;

         Assert.True(body.CanFindUses);
      }

      [Fact]
      public void CanFindUses_CaretOnAnUnknownName_IsFalse() {
         LoadNamedFlags();
         var body = CreateBody("setflag FLAG_PAUSE_TIME");

         body.CaretPosition = 12;

         Assert.False(body.CanFindUses);
      }

      [Fact]
      public void CanFindUses_CaretOnACommand_IsFalse() {
         LoadNamedFlags();
         var body = CreateBody("setflag FLAG_BADGE01_GET");

         body.CaretPosition = 3;

         Assert.False(body.CanFindUses);
      }

      [Fact]
      public void FindUses_FlagName_SearchesLikeTheNumber() {
         LoadNamedFlags();
         var byName = new List<ISet<(int, int)>>();
         var byNumber = new List<ISet<(int, int)>>();
         var nameBody = CreateBody("setflag FLAG_BADGE01_GET");
         var numberBody = CreateBody("setflag 0x867");
         nameBody.RequestShowSearchResult += (sender, e) => byName.Add(e);
         numberBody.RequestShowSearchResult += (sender, e) => byNumber.Add(e);
         nameBody.CaretPosition = 12;
         numberBody.CaretPosition = 10;

         nameBody.FindUses();
         numberBody.FindUses();

         Assert.Single(byName);
         Assert.Single(byNumber);
         Assert.Equal(byNumber[0].OrderBy(pair => pair.Item1).ToList(), byName[0].OrderBy(pair => pair.Item1).ToList());
      }

      #endregion

      #region Metadata

      [Fact]
      public void Metadata_CommentAfterElementOfMultiLineList_IsKept() {
         var metadata = new StoredMetadata(NamedFlagsToml);

         var list = metadata.Lists.Single(l => l.Name == "scriptnamedflags");

         Assert.Equal("Pokemon in the start menu", list.Comments[2144]);
         Assert.Equal("Stone Badge", list.Comments[2151]);
         Assert.Equal("Knuckle Badge", list.Comments[2152]);
         Assert.Equal("Dynamax allowed (needs the Dynamax Band)", list.Comments[4437]);
         Assert.Equal("wild and gift Pokemon are never shiny", list.Comments[4439]);
         Assert.Equal("wild and gift Pokemon are always shiny", list.Comments[4440]);
         Assert.Equal(6, list.Comments.Count);
      }

      [Fact]
      public void Metadata_ElementWithoutComment_HasNoComment() {
         var lines = new[] {
            "[[List]]",
            "Name = '''things'''",
            "0 = [",
            "   '''a''',",
            "   '''b''',# about b",
            "   '''c''',",
            "]",
         };

         var list = new StoredMetadata(lines).Lists.Single();

         Assert.Equal(new[] { "a", "b", "c" }, list.Contents);
         Assert.Single(list.Comments);
         Assert.Equal("about b", list.Comments[1]);
      }

      [Fact]
      public void Metadata_ListWithComments_SurvivesSaveAndLoad() {
         var list = new StoredMetadata(NamedFlagsToml).Lists.Single();
         var lines = new List<string>();
         list.AppendContents(lines);

         var again = new StoredMetadata(lines.ToArray()).Lists.Single();

         Assert.Equal(list.Count, again.Count);
         Assert.Equal(list.Comments.Count, again.Comments.Count);
         foreach (var pair in list.Comments) Assert.Equal(pair.Value, again.Comments[pair.Key]);
         for (int i = 0; i < list.Count; i++) Assert.Equal(list[i], again[i]);
      }

      [Fact]
      public void Metadata_LoadedList_KeepsCommentsInTheModel() {
         LoadNamedFlags();

         Assert.True(Model.TryGetList("scriptnamedflags", out var list));

         Assert.Equal("Stone Badge", list.Comments[2151]);
         Assert.Equal("FLAG_FORCE_SHINY", list[4440]);
      }

      #endregion

      #region ScriptArg

      [Theory]
      [InlineData("scriptnamedflags|h", true)]
      [InlineData("anything|h", true)]
      [InlineData("|h", false)]
      [InlineData("|z", false)]
      [InlineData("scriptnamedflags|z", false)]
      [InlineData("scriptnamedflags", false)]
      [InlineData("", false)]
      [InlineData(null, false)]
      public void ScriptArg_IsNumberOrNameSpec_OnlyForAListWithHexSuffix(string spec, bool expected) {
         Assert.Equal(expected, ScriptArg.IsNumberOrNameSpec(spec));
      }

      [Fact]
      public void ScriptArg_GetOptionsName_RemovesOnlyTheNumberOrNameSuffix() {
         Assert.Equal("scriptnamedflags", ScriptArg.GetOptionsName("scriptnamedflags|h"));
         Assert.Equal("scriptnamedflags", ScriptArg.GetOptionsName("scriptnamedflags"));
         Assert.Equal("|h", ScriptArg.GetOptionsName("|h"));
         Assert.Equal("x|z", ScriptArg.GetOptionsName("x|z"));
         Assert.Null(ScriptArg.GetOptionsName(null));
      }

      [Fact]
      public void ScriptArg_FlagArgumentOfSetFlag_IsNumberOrName() {
         var line = Parser.FirstMatch("setflag 1");
         var arg = line.Args.OfType<ScriptArg>().Single(a => a.Name == "flag");

         Assert.True(arg.IsNumberOrName);
         Assert.Equal("scriptnamedflags", arg.EnumListName);
      }

      #endregion
   }
}
