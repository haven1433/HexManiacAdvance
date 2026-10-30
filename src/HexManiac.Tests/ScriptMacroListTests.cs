using HavenSoft.HexManiac.Core.Models;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ScriptMacroListTests : BaseViewModelTestClass {
      [Fact]
      public void ScriptMacroList_LoadedAfterTheCodeToolExists_IsStillUsed() {
         // the model loads its metadata on a background thread, so the script editor can be built first
         var before = ViewPort.Tools.CodeTool.ScriptParser;

         Model.SetList(new ModelDelta(), "scriptmacros", new[] { "B6 mycommand species: level. item: species2: level2. item2:" }, null, null);

         var after = ViewPort.Tools.CodeTool.ScriptParser;
         Assert.NotSame(before, after);
         Assert.True(after.RequireCompleteAddresses == before.RequireCompleteAddresses);
         // B6 normally takes 5 bytes; the ROM's own definition takes 11
         var bytes = new byte[] { 0xB6, 0x79, 0x01, 40, 0, 0, 0, 0, 0, 0, 0, 0x02 };
         for (int i = 0; i < bytes.Length; i++) Model[i] = bytes[i];
         var text = after.Parse(Model, 0, bytes.Length);
         Assert.Contains("mycommand", text);
         Assert.DoesNotContain("nop", text);
      }

      [Fact]
      public void ScriptMacroList_LineNameWithADot_IsTheCommandNotAnArgument() {
         // the generated metadata names the line form of native commands like "createmon.native",
         // but a dot also marks a one-byte argument ("slot."): the first word after the code bytes is the name
         Model.SetList(new ModelDelta(), "scriptmacros", new[] { "23 DD E6 1F 0A mycommand.native side. slot. species:" }, null, null);
         var parser = ViewPort.Tools.CodeTool.ScriptParser;
         var bytes = new byte[] { 0x23, 0xDD, 0xE6, 0x1F, 0x0A, 1, 2, 0x34, 0x12, 0x02 };
         for (int i = 0; i < bytes.Length; i++) Model[i] = bytes[i];

         var line = parser.GetLine(Model, 0);

         Assert.Equal("mycommand.native", line.LineCommand);
         Assert.Equal(3, line.Args.Count);
         Assert.Equal(bytes.Length, parser.FindLength(Model, 0)); // used to throw a NullReferenceException (the line had no name)
      }

      [Fact]
      public void ScriptMacroList_NativeLineAndMacro_ScriptWithBothCompilesBackToTheSameBytes() {
         Model.SetList(new ModelDelta(), "scriptmacros", new[] {
            "mycommand 23 DD E6 1F 0A side. slot. species:",
            "23 DD E6 1F 0A mycommand.native side. slot. species:",
         }, null, null);
         var parser = ViewPort.Tools.CodeTool.ScriptParser;
         var bytes = new byte[] { 0x23, 0xDD, 0xE6, 0x1F, 0x0A, 1, 2, 0x34, 0x12, 0x02 };
         for (int i = 0; i < bytes.Length; i++) Model[i] = bytes[i];
         int sectionCount = 0;

         var text = parser.Parse(Model, 0, parser.FindLength(Model, 0), ref sectionCount);
         var compiled = parser.Compile(new ModelDelta(), Model, 0, ref text, out var _, out var _);

         Assert.Contains("mycommand", text);
         Assert.Equal(bytes, compiled);
      }

      [Fact]
      public void ScriptMacroList_FourByteNumberArgument_IsShownAsANumberNotAnAddress() {
         // addmoney/removemoney/addtime... take a plain 4-byte number: "value<>" made the script editor show <000BB8> and refuse "3000"
         Model.SetList(new ModelDelta(), "scriptmacros", new[] { "90 addmoney value::" }, null, null);
         var parser = ViewPort.Tools.CodeTool.ScriptParser;
         var bytes = new byte[] { 0x90, 0xB8, 0x0B, 0x00, 0x00, 0x02 };
         for (int i = 0; i < bytes.Length; i++) Model[i] = bytes[i];
         int sectionCount = 0;

         var text = parser.Parse(Model, 0, parser.FindLength(Model, 0), ref sectionCount);
         var compiled = parser.Compile(new ModelDelta(), Model, 0, ref text, out var _, out var _);

         Assert.Contains("addmoney 3000", text);
         Assert.Equal(bytes, compiled);
      }

      [Fact]
      public void ScriptMacroList_UnchangedLines_KeepTheSameParser() {
         Model.SetList(new ModelDelta(), "scriptmacros", new[] { "B6 mycommand species: level." }, null, null);
         var first = ViewPort.Tools.CodeTool.ScriptParser;
         var second = ViewPort.Tools.CodeTool.ScriptParser;
         Assert.Same(first, second);
      }
   }
}
