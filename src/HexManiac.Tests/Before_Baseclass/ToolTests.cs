using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ToolTests : BaseViewModelTestClass {
      private readonly ThumbParser parser;
      public ToolTests() => parser = new ThumbParser(Singletons);

      [Fact]
      public void ViewPortHasTools() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[100]));
         Assert.True(viewPort.HasTools);
      }

      [Fact]
      public void StringToolCanOpenOnChosenData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.SelectedIndex = -1;
         var toolProperties = new List<string>();
         viewPort.Tools.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.FollowLink(0, 0);

         Assert.Contains("SelectedIndex", toolProperties);
         Assert.IsType<PCSTool>(viewPort.Tools[viewPort.Tools.SelectedIndex]);
      }

      [Fact]
      public void StringToolEditsAreReflectedInViewPort() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some Test"; // Text -> Test
         var pcs = (PCS)viewPort[7, 0].Format;
         Assert.Equal("s", pcs.ThisCharacter);
      }

      [Fact]
      public void StringToolCanMoveData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         var toolProperties = new List<string>();
         viewPort.Tools.StringTool.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.Tools.StringTool.Address = 0;

         toolProperties.Clear();
         viewPort.Tools.StringTool.Content = "Some More Text";
         Assert.Contains("Address", toolProperties);
      }

      [Fact]
      public void ViewPortMovesWhenStringToolMovesData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some More Text";
         Assert.NotEqual(0, int.Parse(viewPort.Headers[0], NumberStyles.HexNumber));
      }

      [Fact]
      public void StringToolMultiCharacterDeleteCleansUpUnusedBytes() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some "; // removed 'Text' from the end

         Assert.Equal(0xFF, model[7]);
      }

      [Fact]
      public void HideCommandClosesAnyOpenTools() {
         var tools = ViewPort.Tools;
         tools.SelectedIndex = 1;
         tools.HideCommand.Execute();

         Assert.Equal(-1, tools.SelectedIndex);
      }

      [Fact]
      public void StringToolContentUpdatesWhenViewPortChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\"");

         viewPort.SelectionStart = new Point(3, 0);   // select the 'e' in 'Some'
         viewPort.FollowLink(3, 0);                   // open the string tool
         viewPort.Edit("i");                          // change the 'e' to 'i'

         Assert.Equal("Somi Text", viewPort.Tools.StringTool.Content);
      }

      [Fact]
      public void ToolSelectionChangeUpdatesViewPortSelection() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\"");
         viewPort.SelectionStart = new Point(3, 0);
         viewPort.FollowLink(3, 0);

         viewPort.Tools.StringTool.ContentIndex = 4;

         Assert.Equal(new Point(4, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void SelectingAPointerAddressInStringToolDisablesTheTool() {
         var token = new ModelDelta();
         var model = new PokemonModel(new byte[0x200]);
         model.WritePointer(token, 16, 100);
         model.ObserveRunWritten(token, new PointerRun(16));
         var tool = new PCSTool(
            model,
            new Selection(new ScrollRegion { Width = 0x10, Height = 0x10 }, model),
            new ChangeHistory<ModelDelta>(dm => dm),
            null) {
            Address = 18
         };

         Assert.Equal(18, tool.Address); // address updated correctly
         Assert.False(tool.Enabled);     // run is not one that this tool knows how to edit
      }

      [Fact]
      public void TableToolUpdatesWhenTextToolDataChanges() {
         // Arrange
         var data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.SelectionStart = new Point(8, 1);

         // Act: Update via the Text Tool
         viewPort.Tools.SelectedIndex = 10.Range().First(i => viewPort.Tools[i] == viewPort.Tools.StringTool);
         viewPort.Tools.StringTool.Content = Environment.NewLine + "Larry";

         // Assert: Table Tool is updated
         viewPort.Tools.SelectedIndex = 10.Range().First(i => viewPort.Tools[i] == viewPort.Tools.TableTool);
         var field = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[1];
         Assert.Equal("Larry", field.Content);
      }

      [Fact]
      public void TextToolToolUpdatesWhenTableToolDataChanges() {
         // Arrange
         var data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.SelectionStart = new Point(8, 1);

         // Act: Update via the Table Tool
         viewPort.Tools.SelectedIndex = 10.Range().First(i => viewPort.Tools[i] == viewPort.Tools.TableTool);
         var field = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[1];
         field.Content = "Larry";

         // Assert: Text Tool is updated
         viewPort.Tools.SelectedIndex = 10.Range().First(i => viewPort.Tools[i] == viewPort.Tools.StringTool);
         var textToolContent = viewPort.Tools.StringTool.Content.Split(Environment.NewLine)[1];
         Assert.Equal("Larry", textToolContent);
      }

      [Fact]
      public void TableToolUpdatesIndexOnCursorMove() {
         // Arrange
         var data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");

         // Act: move the cursor to change the selected table item
         viewPort.SelectionStart = new Point(8, 1);

         // Assert: table item index 1 is selected
         Assert.Contains("1", viewPort.Tools.TableTool.CurrentElementName);
      }

      [Fact]
      public void ContentUpdateFromAnotherToolDoesNotResetCaretInStringTool() {
         // Arrange
         var data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");

         // mock the view: whenever the stringtool content changes,
         // reset the cursor to the start position.
         viewPort.Tools.StringTool.PropertyChanged += (sender, e) => {
            if (e.PropertyName == "Content") viewPort.Tools.StringTool.ContentIndex = 0;
         };

         viewPort.SelectionStart = new Point(8, 1);                                                                         // move the cursor
         viewPort.Tools.SelectedIndex = 10.Range().First(i => viewPort.Tools[i] == viewPort.Tools.StringTool); // open the string tool
         viewPort.Tools.StringTool.ContentIndex = 12;                                                                       // place the cursor somewhere, like the UI would
         viewPort.Tools.SelectedIndex = 10.Range().First(i => viewPort.Tools[i] == viewPort.Tools.TableTool);  // open the table tool
         var field = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[1];
         field.Content = "Larry";                                                                                           // make a change with the table tool

         Assert.NotEqual(new Point(), viewPort.SelectionStart);
      }

      [Fact]
      public void TableToolCanExtendTable() {
         // Arrange
         var data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.Tools.SelectedIndex = viewPort.Tools.Count.Range().Single(i => viewPort.Tools[i] == viewPort.Tools.TableTool);
         Assert.True(viewPort.Tools.TableTool.Next.CanExecute(null)); // table has 3 entries

         // Act: move to end of table
         while (viewPort.Tools.TableTool.Next.CanExecute(null)) viewPort.Tools.TableTool.Next.Execute();

         Assert.True(viewPort.Tools.TableTool.Append.CanExecute(null));
         viewPort.Tools.TableTool.Append.Execute();
         Assert.Contains("3", viewPort.Tools.TableTool.CurrentElementName);
         Assert.Equal(16 * 4, model.GetNextRun(0).Length);
      }

      [Fact]
      public void TableToolNotOfferedOnNormalText() {
         // Arrange
         var data = 0x200.Range().Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^text\"\" Some Text\"");

         // Act
         viewPort.SelectionStart = new Point(2, 4);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);

         // Assert
         var matches = items.Where(item => item.Text.Contains("Table"));
         Assert.Empty(matches);
      }

      [Theory]
      [InlineData(0x0000, "nop")]
      [InlineData(0b0001100_010_001_000, "add   r0, r1, r2")]
      [InlineData(0b00000_00100_010_001, "lsl   r1, r2, #4")]
      [InlineData(0b1101_0000_00001100, "beq   <00001C>")] // 1C = 28 (current address is zero). 28 = 12*2+4
      [InlineData(0b01000110_0_1_000_111, "mov   r7, r8")]
      public void ThumbDecompilerTests(int input, string output) {
         var bytes = new[] { (byte)input, (byte)(input >> 8) };
         var model = new PokemonModel(bytes);
         var result = parser.Parse(model, 0, 2).Split(Environment.NewLine)[1].Trim();
         Assert.Equal(output, result);
      }

      [Fact]
      public void DecompileThumbRoutineTest() {
         // sample routine: If r0 is true, return double r1. Else, return 0
         var code = new ushort[] {
         // 000000:
            0b10110101_00110000,    // push  lr, {r4, r5}         (note that for push, r0-r7 run right-left)
            0b00101_000_00000001,   // cmp   r0, 1
            0b1101_0001_00000000,   // bne   pc(4)+(0)*2+4 = 8
            0b0001100_001_001_000,  // add   r0, r1, r1
         // 000008:
            0b10111101_00110000,    // pop   pc, {r4, r5}         (note that for pop, r0-07 run right-left)
         };

         var bytes = code.SelectMany(pair => new[] { (byte)pair, (byte)(pair >> 8) }).ToArray();
         var model = new PokemonModel(bytes);
         var lines = parser.Parse(model, 0, bytes.Length).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         Assert.Equal(7, lines.Length);
         Assert.Equal("000000:", lines[0]);
         Assert.Equal("    push  {r4-r5, lr}", lines[1]);
         Assert.Equal("    cmp   r0, #1", lines[2]);
         Assert.Equal("    bne   <000008>", lines[3]);
         Assert.Equal("    add   r0, r1, r1", lines[4]);
         Assert.Equal("000008:", lines[5]);
         Assert.Equal("    pop   {r4-r5, pc}", lines[6]);
      }

      [Fact]
      public void ShowDataTest() {
         var code = new ushort[] {
         // 000000: first code part
            0b01001_000_00000000,  // ldr   r0, pc(0)+(0)*4+4 = 4
            0b11100_00000000011,   // b     pc(2)+(3)*2+4 = 12
         // 000004: some data and some skipped bytes
            0x1234,                // (loaded)
            0x5678,                // (loaded)
            0x0000,                // (skipped)
            0x0000,                // (skipped)
         // 00000C: more code that uses the data
            0b010001110_0_000_000  // bx r0
         };

         var bytes = code.SelectMany(pair => new[] { (byte)pair, (byte)(pair >> 8) }).ToArray();
         var model = new PokemonModel(bytes);
         var lines = parser.Parse(model, 0, bytes.Length).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         Assert.Equal(7, lines.Length);
         Assert.Equal("000000:", lines[0]);
         Assert.Equal("    ldr   r0, [pc, <000004>]", lines[1]);
         Assert.Equal("    b     <00000C>", lines[2]);
         Assert.Equal("000004:", lines[3]);
         Assert.Equal("    .word 0x56781234", lines[4]);
         Assert.Equal("00000C:", lines[5]);
         Assert.Equal("    bx    r0", lines[6]);
      }

      [Fact]
      public void LoadRegisterCommmandsLoadWordAligned() {
         //01101 # rn rd
         var code = new ushort[] {
            0b0000_0000_0000_0000,  // nop
            0b01001_000_00000001,   // ldr #1 <-- 8, not 10
            0b10111101_00000000,    // pop pc {}
            0b00000000_00000000,    // nop
            0xBEEF,  // .word
            0xDEAD,
         };

         var bytes = code.SelectMany(pair => new[] { (byte)pair, (byte)(pair >> 8) }).ToArray();
         var model = new PokemonModel(bytes);
         var lines = parser.Parse(model, 0, bytes.Length).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

         Assert.Equal(6, lines.Length);
         Assert.Equal("000000:", lines[0]);
         Assert.Equal("    nop", lines[1]);
         Assert.Equal("    ldr   r0, [pc, <000008>]", lines[2]);
         Assert.Equal("    pop   {pc}", lines[3]);
         Assert.Equal("000008:", lines[4]);
         Assert.Equal("    .word 0xDEADBEEF", lines[5]);
      }

      /// <summary>
      /// bl is two Thumb instructions.
      /// Other than that, all instructions are 16 bits, no exceptions.
      /// This test shows that addresses after a 4-byte instruction are still calculated correctly.
      /// It also shows that 'bl' shows the right inline address.
      /// </summary>
      [Fact]
      public void BranchLinkInstructionsTakeDoubleSpace() {
         var model = new PokemonModel(new byte[20]);
         model.WriteValue(new ModelDelta(), 0, unchecked((int)0b11111_00000000100_11110_00000000000)); // bl

         var lines = parser.Parse(model, 0, 20).Split(Environment.NewLine).Where(s => !string.IsNullOrEmpty(s)).ToArray(); // pc(0)+#(4)*2+4 = C

         Assert.Equal(11, lines.Length);
         Assert.Equal("000000:", lines[0]);
         Assert.Equal("    bl    <00000C>", lines[1]);
         Assert.Equal("    nop", lines[2]);
         Assert.Equal("    nop", lines[3]);
         Assert.Equal("    nop", lines[4]);
         Assert.Equal("    nop", lines[5]);
         Assert.Equal("00000C:", lines[6]);
         Assert.Equal("    nop", lines[7]);
         Assert.Equal("    nop", lines[8]);
         Assert.Equal("    nop", lines[9]);
         Assert.Equal("    nop", lines[10]);
      }

      [Theory]
      [InlineData("add   r0, r1, r2", 0b0001100_010_001_000)]
      [InlineData("lsl   r1, r2, #4", 0b00000_00100_010_001)]
      [InlineData("bls   <000120>", 0b1101_1001_00001110)]
      [InlineData("push  lr, {}", 0b10110101_00000000)]
      [InlineData("bl    <000120>", 0b11111_00000001110_11110_00000000000)]
      [InlineData(".word 00004000", 0b0000_0000_0000_0000_0100_0000_0000_0000)]
      [InlineData(".word <004000>", 0b0000_1000_0000_0000_0100_0000_0000_0000)]
      [InlineData(".word <bob>", 0b0000_1000_0000_0000_0000_0000_1000_0000)] // test that we can use anchors to get pointer locations
      [InlineData("ldr   r0, [pc, <bob>]", 0b01001_000_11011111)]  //  -33*4+4=-128
      [InlineData("ldrb  r1, [r1, r2]", 0b0101110_010_001_001)]
      [InlineData("ldrh  r2, [r1, #0]", 0b10001_00000_001_010)]
      [InlineData("lsl   r1, r2", 0b0100000010_010_001)]
      [InlineData("lsl   r1, r2 @ comment", 0b0100000010_010_001)]
      [InlineData("mov   r7, r8", 0b01000110_0_1_000_111)]
      [InlineData("bx lr", 0b010001110_1_110_000)]
      [InlineData("ldr   r1, [r2]", 0b01101_00000_010_001)]
      [InlineData("lsl   r1, r2, #0x4", 0b00000_00100_010_001)]
      [InlineData("sub   sp, #4", 0b101100001_0000001)]
      [InlineData("mov   r3, sp", 0x46_6B)]
      [InlineData("push {r0-r2, lr}", 0b10110101_00000111)]
      [InlineData("ldrb r1, [r1, 0]", 0b01111_00000_001_001)]
      [InlineData("ldrb r1, [r1]", 0b01111_00000_001_001)]
      [InlineData("strb r3, [r4]", 0b01110_00000_100_011)]
      [InlineData("str  r2, [r0]", 0b01100_00000_000_010)]
      [InlineData("ldr  r1, [r2]", 0b01101_00000_010_001)]
      [InlineData("strh r1, [r2]", 0b10000_00000_010_001)]
      [InlineData("ldrh r1, [r2]", 0b10001_00000_010_001)]
      [InlineData("str  r1, [sp]", 0b10010_001_00000000)]
      [InlineData("ldr  r1, [sp]", 0b10011_001_00000000)]
      public void ThumbCompilerTests(string input, uint output) {
         var bytes = new List<byte> { (byte)output, (byte)(output >> 8) };
         var model = new PokemonModel(new byte[0x200]);
         model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x80)); // random anchor so we can test stuff that points to anchors
         var result = parser.Compile(model, 0x100, new string[] { input });

         Assert.Equal(bytes[0], result[0]);
         Assert.Equal(bytes[1], result[1]);

         if (result.Count > 2) {
            bytes.Add((byte)(output >> 16));
            bytes.Add((byte)(output >> 24));
            Assert.Equal(bytes[2], result[2]);
            Assert.Equal(bytes[3], result[3]);
         }
      }

      [Fact]
      public void ThumbLabelWithLeadingDot_Compile_CreatesCorrectCode() {
         var expected = new byte[] {
            0, 0b01001_000,
            unchecked((byte)-3), 0b11100_111,
            0x78, 0x56, 0x34, 0x08,
         };

         var result = ViewPort.Tools.CodeTool.Parser.Compile(Model, 0, "top:", "ldr r0, .Name", "b top", ".Name:", ".word 0x08345678");

         Assert.Equal(expected, result.ToArray());
      }

      [Fact]
      public void ThumbCompilerLabelTest() {
         var model = new PokemonModel(new byte[0x200]);
         model.ObserveAnchorWritten(new ModelDelta(), "DoStuff", new NoInfoRun(0x40));
         var result = parser.Compile(model, 0x100
            // sums all numbers from 1 to 10 in a loop
            // then calls the routine at "DoStuff"
            // then returns
            , "push lr, {}"
            , "mov r1, #1"
            , "mov r0, #0"
            , "Loop:"
            , "add r0, r0, r1"
            , "cmp r1, #10"
            , "bne <loop>"
            , "bl <DoStuff>"
            , "pop pc, {}"
            );

         var expected = new byte[] {
            0x00, 0b10110101,
            0x01, 0b00100_001,
            0x00, 0b00100_000,
            // loop
            0b01_000_000, 0b0001100_0,  // 0001100_001_000_000
            0x0A, 0b00101_001,
            0xFC, 0b1101_0001,
            0xFF, 0b11110_111, 0x98, 0b11111_111,  // (sbyte)0x98 = -68
            0x00, 0b10111101,
         };

         Assert.Equal(expected.Length, result.Count);
         for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], result[i]);
      }

      [Fact]
      public void ThumbCompilerLoadRegisterOffsetTest() {
         var model = new PokemonModel(new byte[0x200]);
         var result = parser.Compile(model, 0x100,
                  "    ldr  r0, [pc, <abc>]",
                  "    ldr  r1, [pc, <def>]",
                  "    ldr  r2, [pc, <ghi>]",
                  "    ldr  r3, [pc, <jkl>]",
                  "abc:",
                  "    .word 1234",
                  "def:",
                  "    .word 5678",
                  "ghi:",
                  "    .word 9abc",
                  "jkl:",
                  "    .word def0"
            );

         var expected = new byte[] {
            0x01, 0b01001_000,
            0x02, 0b01001_001,
            0x02, 0b01001_010,
            0x03, 0b01001_011,
         };

         for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], result[i]);
      }

      [Fact]
      public void CanCompileCodeWithNopAlignment() {
         var model = new PokemonModel(new byte[0x200]);
         var result = parser.Compile(model, 0x100,
                  "    ldr  r0, [pc, <abc>]",
                  "    mov  r0, #0",
                  "    b    <end>",
                  "abc:",
                  "    .word 1234",
                  "end:",
                  "    pop pc, {}"
            );

         var expected = new byte[] {
            0x01, 0b01001_000,
            0x00, 0b00100_000,
            0x02, 0b11100_000,
            0x00, 0b00000_000, // inserted nop to align for .word value
            0x34, 0x12, 0,  0,
            0x00, 0b1011110_1,
         };

         for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], result[i]);
      }

      [Fact]
      public void ThumbCode_InlineLoad_Compiles() {
         var model = new PokemonModel(new byte[0x200]);
         var result = parser.Compile(model, 0x100,
            "    ldr  r0, =256",
            "    mov  r0, #0",
            "    b    <end>",
            // implicit nop for alignment
            // implicit .word 256
            "end:",
            "    pop pc, {}"
         );

         var expected = new byte[] {
            0x01, 0b01001_000,
            0x00, 0b00100_000,
            0x02, 0b11100_000,
            0x00, 0b00000_000, // inserted nop to align for .word value
            0, 1, 0, 0,        // inserted word
            0x00, 0b1011110_1,
         };

         for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], result[i]);
      }

      [Fact]
      public void ThumbCode_InlineWithOffset_Compiles() {
         var model = new PokemonModel(new byte[0x200]);
         var result = parser.Compile(model, 0x100,
            "    ldr  r0, =(0xFF+1)",
            "    mov  r0, #0",
            "    b    <end>",
            // implicit nop for alignment
            // implicit .word 256
            "end:",
            "    pop pc, {}"
         );

         var expected = new byte[] {
            0x01, 0b01001_000,
            0x00, 0b00100_000,
            0x02, 0b11100_000,
            0x00, 0b00000_000, // inserted nop to align for .word value
            0, 1, 0, 0,        // inserted word
            0x00, 0b1011110_1,
         };

         for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], result[i]);
      }

      [Fact]
      public void RawCodeToolWorks() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[100]));
         viewPort.Tools.CodeTool.Mode = CodeMode.Raw;
         viewPort.SelectionEnd = new Point(3, 0);
         Assert.Equal("00 00 00 00", viewPort.Tools.CodeTool.Content.Trim());
      }

      [Fact]
      public void TableToolContainsListOfAllTables() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.Edit("^table1[data\"\"6]4 @30 ^table2[data\"\"6]4 ");
         Assert.Equal(2, test.ViewPort.Tools.TableTool.TableSections.Count());

         test.ViewPort.Tools.TableTool.SelectedTableSection = 1;
         Assert.Equal("000030", test.ViewPort.Headers[0]);
      }

      [Fact]
      public void TextToolSearchTest() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("FF @00 ^text\"\" Hello World Worble!");
         test.ViewPort.SelectionStart = new Point();
         var tool = test.ViewPort.Tools.StringTool;

         tool.SearchText = "wor";
         tool.Search.Execute();
         Assert.Equal(6, tool.ContentIndex);
         Assert.Equal(3, tool.ContentSelectionLength);

         tool.Search.Execute();
         Assert.Equal(12, tool.ContentIndex);
         Assert.Equal(3, tool.ContentSelectionLength);
      }

      [Fact]
      public void ToolSelectionDoesNotAutoChangeForCodeToolHexView() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("FF @00 ^text\"\" Hello World!");
         test.ViewPort.Tools.CodeToolCommand.Execute();
         test.ViewPort.Tools.CodeTool.Mode = CodeMode.Raw;

         test.ViewPort.SelectionStart = new Point(0, 1);
         test.ViewPort.SelectionStart = new Point(0, 0);

         Assert.Equal(test.ViewPort.Tools.CodeTool, test.ViewPort.Tools.SelectedTool);
      }

      [Fact]
      public void NextElementInTableToolWorksEvenIfOtherDataIsCurrentlySelected() {
         ViewPort.Edit("^table[data<>]3 <020> <030> <040> @04 ");
         ViewPort.ExpandSelection(0, 0); // follows the pointer to 030
         var tableTool = ViewPort.Tools.TableTool;

         ViewPort.Tools.SelectedIndex = ViewPort.Tools.IndexOf(tableTool);
         tableTool.Next.Execute();

         Assert.EndsWith("/2", tableTool.CurrentElementName);
      }

      [Fact]
      public void ClearTable_CheckTableTool_NoTable() {
         CreateTextTable("names", 0, "adam", "bob", "carl", "dave");

         ViewPort.Goto.Execute(0);
         ViewPort.GetContextMenuItems(new Point(0, 0))
            .Single(item => item.Text == "Clear Format")
            .Command.Execute();

         var tableTool = ViewPort.Tools.TableTool;
         Assert.Equal(0, tableTool.Address);
         Assert.Empty(tableTool.Children);
      }
   }
}
