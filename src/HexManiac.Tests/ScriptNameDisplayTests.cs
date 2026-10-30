using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// View > Script Flags&amp;Vars: the script editor shows numbers (default) or every define.
   /// In Numbers mode the flags the ROM named on purpose (list 'scriptnamedflags') keep their names.
   /// </summary>
   [Collection("ScriptNameDisplay")]
   public class ScriptNameDisplayTests : BaseViewModelTestClass {
      private static readonly string[] Toml = {
         "[[List]]", "Name = '''scriptvariablealiases'''", "16384 = [", "   '''VAR_TEMP_0''',", "   '''VAR_TEMP_1''',", "]",
         "[[List]]", "Name = '''scriptflagaliases'''", "0 = [", "   '''FLAG_ZERO''',", "   '''FLAG_ONE''',", "   '''FLAG_TWO''',", "]",
         "[[List]]", "Name = '''scriptnamedflags'''", "2 = [", "   '''FLAG_TWO''',# the named one", "]",
      };

      private ScriptParser Parser => ViewPort.Tools.CodeTool.ScriptParser;

      private string Decompile(string hexBytes) {
         var bytes = hexBytes.ToByteArray();
         Token.ChangeData(Model, 0, bytes);
         return Parser.Parse(Model, 0, bytes.Length);
      }

      private byte[] Compile(string script) => Parser.Compile(Token, Model, 0x20, ref script, out var _);

      private void WithMode(ScriptNameDisplay mode, Action test) {
         var before = ScriptArg.NameDisplay;
         try {
            ScriptArg.NameDisplay = mode;
            Model.LoadMetadata(new StoredMetadata(Toml));
            test();
         } finally {
            ScriptArg.NameDisplay = before;
         }
      }

      [Fact]
      public void TheDefaultIsNumbers() => Assert.Equal(ScriptNameDisplay.Numbers, ScriptArg.NameDisplay);

      [Fact]
      public void Numbers_ShowsVariablesAsNumbersAndOnlyNamedFlagsByName() => WithMode(ScriptNameDisplay.Numbers, () => {
         var text = Decompile("16 00 40 01 00 29 01 00 29 02 00 02");
         Assert.Contains("setvar 0x4000 1", text);
         Assert.DoesNotContain("VAR_TEMP_0", text);
         Assert.Contains("setflag 0x0001", text);
         Assert.DoesNotContain("FLAG_ONE", text);
         Assert.Contains("setflag FLAG_TWO", text);
      });

      [Fact]
      public void Defines_ShowsEveryVariableAndFlagByName() => WithMode(ScriptNameDisplay.Defines, () => {
         var text = Decompile("16 00 40 01 00 29 01 00 29 02 00 02");
         Assert.Contains("setvar VAR_TEMP_0 1", text);
         Assert.Contains("setflag FLAG_ONE", text);
         Assert.Contains("setflag FLAG_TWO", text);
      });

      [Fact]
      public void Numbers_CompilesNumbersAndTheNamedFlagsToTheSameBytes() => WithMode(ScriptNameDisplay.Numbers, () => {
         Assert.Equal(new byte[] { 0x29, 0x02, 0x00 }, Compile("setflag FLAG_TWO").Take(3).ToArray());
         Assert.Equal(new byte[] { 0x29, 0x01, 0x00 }, Compile("setflag 1").Take(3).ToArray());
         Assert.Equal(new byte[] { 0x16, 0x00, 0x40, 0x01, 0x00 }, Compile("setvar 0x4000 1").Take(5).ToArray());
      });

      [Fact]
      public void Defines_CompilesEveryName() => WithMode(ScriptNameDisplay.Defines, () => {
         Assert.Equal(new byte[] { 0x29, 0x01, 0x00 }, Compile("setflag FLAG_ONE").Take(3).ToArray());
         Assert.Equal(new byte[] { 0x16, 0x01, 0x40, 0x01, 0x00 }, Compile("setvar VAR_TEMP_1 1").Take(5).ToArray());
      });

      [Fact]
      public void Suggestions_FollowTheMode() {
         WithMode(ScriptNameDisplay.Numbers, () => Assert.DoesNotContain(Parser.ReadOptions(Model, "scriptnamedflags|h", "FLAG_"), o => o.StartsWith("FLAG_ONE")));
         WithMode(ScriptNameDisplay.Defines, () => Assert.Contains(Parser.ReadOptions(Model, "scriptnamedflags|h", "FLAG_"), o => o.StartsWith("FLAG_ONE")));
      }

      [Fact]
      public void TheChoiceIsSavedWithTheSettingsAndLoadedBack() {
         var before = ScriptArg.NameDisplay;
         try {
            string[] saved = null;
            var firstRun = new EditorViewModel(new StubFileSystem { SaveMetadata = (name, lines) => { saved = lines; return true; } });
            firstRun.ScriptNameDisplay = ScriptNameDisplay.Defines;
            firstRun.WriteAppLevelMetadata();
            Assert.Contains("ScriptNameDisplay = 1", saved);

            ScriptArg.NameDisplay = ScriptNameDisplay.Numbers;
            var secondRun = new EditorViewModel(new StubFileSystem { MetadataFor = name => saved });
            Assert.Equal(ScriptNameDisplay.Defines, secondRun.ScriptNameDisplay);

            // a settings file from before the choice existed, or a broken line: numbers
            var oldFile = new EditorViewModel(new StubFileSystem { MetadataFor = name => new[] { "[GeneralSettings]", "AnimateScroll = True" } });
            Assert.Equal(ScriptNameDisplay.Numbers, oldFile.ScriptNameDisplay);
            var broken = new EditorViewModel(new StubFileSystem { MetadataFor = name => new[] { "[GeneralSettings]", "ScriptNameDisplay = 9" } });
            Assert.Equal(ScriptNameDisplay.Numbers, broken.ScriptNameDisplay);
         } finally {
            ScriptArg.NameDisplay = before;
         }
      }
   }
}
