using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The grid asks a table for the segment and the text of every cell, every time it scrolls (a table with a hundred fields is thousands of cells on one screen).
   /// These are the lookups that were made fast: the answers must be exactly what the plain way of working them out gives.
   /// </summary>
   public class ArrayRunLookupTests : BaseViewModelTestClass {
      /// <summary>The way the segment of a byte was always found: walk the segments of the element.</summary>
      private static (int element, int segment, int segmentStart, int segmentOffset) Walk(ITableRun table, int byteOffset) {
         var offset = byteOffset - table.Start;
         int elementIndex = offset / table.ElementLength;
         int elementOffset = offset % table.ElementLength;
         int segmentIndex = 0, segmentOffset = elementOffset;
         while (table.ElementContent[segmentIndex].Length <= segmentOffset) {
            segmentOffset -= table.ElementContent[segmentIndex].Length;
            segmentIndex++;
         }
         return (elementIndex, segmentIndex, byteOffset - segmentOffset, segmentOffset);
      }

      private void AssertSameAsWalking(ITableRun table, IEnumerable<int> byteOffsets) {
         foreach (var byteOffset in byteOffsets) {
            var expected = Walk(table, byteOffset);
            var actual = table.ConvertByteOffsetToArrayOffset(byteOffset);
            Assert.Equal(expected, (actual.ElementIndex, actual.SegmentIndex, actual.SegmentStart, actual.SegmentOffset));
         }
      }

      [Fact]
      public void SegmentOfAByte_EveryByteOfATable_IsWhatWalkingTheSegmentsGives() {
         ViewPort.Edit("@40 ^table[a. b: c:: d<\"\"> | e. f. g\"\"6]4 ");
         var table = (ITableRun)Model.GetNextRun(0x40);

         AssertSameAsWalking(table, Enumerable.Range(table.Start, table.Length));
      }

      [Fact]
      public void SegmentOfAByte_TableWithSplittersAndOneSegment_IsWhatWalkingTheSegmentsGives() {
         ViewPort.Edit("@20 ^one[a:]5 @60 ^two[a. | b. | c.]6 ");

         foreach (var name in new[] { "one", "two" }) {
            var table = Model.GetTable(name);
            AssertSameAsWalking(table, Enumerable.Range(table.Start, table.Length));
         }
      }

      [Fact]
      public void SegmentOfAByte_BeforeTheTableOrAtItsEnd_IsWhatWalkingTheSegmentsGives() {
         ViewPort.Edit("@40 ^table[a. b: c::]3 ");
         var table = (ITableRun)Model.GetNextRun(0x40);

         // not inside the table: the answer is still the one the walk gives (the grid asks about the byte after the last one, too)
         AssertSameAsWalking(table, new[] { table.Start - 1, table.Start - table.ElementLength, table.Start + table.Length, table.Start + table.Length + 1 });
      }

      [Fact]
      public void SegmentOfAByte_AfterTheTableIsMade_IsFoundForTheNewTable() {
         ViewPort.Edit("@20 ^first[a. b:]3 @60 ^second[x:: y.]3 ");
         var first = Model.GetTable("first");
         var second = Model.GetTable("second");

         AssertSameAsWalking(first, Enumerable.Range(first.Start, first.Length));
         AssertSameAsWalking(second, Enumerable.Range(second.Start, second.Length));
      }

      private static string Key(string option, int index) {
         option ??= index.ToString();
         if (option.StartsWith("\"")) option = option.Substring(1);
         if (option.EndsWith("\"")) option = option.Substring(0, option.Length - 1);
         return option.ToLower().Split("~")[0];
      }

      /// <summary>The way the text of an enum value was always worked out: count the options before this one that have the same name.</summary>
      private static string PlainText(IReadOnlyList<string> options, int value) {
         if (value < 0 || value >= options.Count) return value.ToString();
         var text = options[value] ?? value.ToString();
         var key = Key(text, value);
         var sameName = 1;
         for (int i = value - 1; i >= 0; i--) {
            if (Key(options[i], i) == key) sameName++;
         }
         if (text.StartsWith("\"") && text.EndsWith("\"")) text = text.Substring(1, text.Length - 2);
         if (text.Contains('~')) text = text.Split('~')[0];
         if (sameName > 1) text += "~" + sameName;
         if (text.Contains(' ')) text = $"\"{text}\"";
         return text;
      }

      private static readonly string[] AwkwardNames = new[] {
         "Trainer", "Bob", "Trainer~3", "Trainer", "Trainer~3", "Trainer", "Trainer~2", "&~", "&",
         "Two Words", "Two Words", "\"Quoted\"", "Quoted", "quoted", "\"Two Words\"", "Plain", "plain", "PLAIN",
      };

      [Fact]
      public void EnumText_ListWithRepeatedAndAwkwardNames_IsWhatCountingThePreviousOnesGives() {
         Model.SetList(new ModelDelta(), "names", AwkwardNames);
         var segment = new ArrayRunEnumSegment("v", 1, "names");
         for (int value = 0; value < AwkwardNames.Length; value++) {
            Model[0] = (byte)value;

            Assert.Equal(PlainText(AwkwardNames, value), segment.ToText(Model, 0, 0));
         }
      }

      [Fact]
      public void EnumText_AskedAgainAndAgain_GivesTheSameTextEveryTime() {
         Model.SetList(new ModelDelta(), "names", AwkwardNames);
         var segment = new ArrayRunEnumSegment("v", 1, "names");
         Model[0] = 5;

         var texts = Enumerable.Range(0, 5).Select(i => segment.ToText(Model, 0, 0)).ToList();

         Assert.Equal(PlainText(AwkwardNames, 5), texts[0]);
         Assert.All(texts, text => Assert.Equal(texts[0], text));
      }

      [Fact]
      public void EnumText_ValueOutsideTheList_IsTheNumber() {
         Model.SetList(new ModelDelta(), "names", AwkwardNames);
         var segment = new ArrayRunEnumSegment("v", 1, "names");
         Model[0] = 200;

         Assert.Equal("200", segment.ToText(Model, 0, 0));
      }

      [Fact]
      public void EnumText_WithAnOffset_CountsFromTheOffset() {
         var names = new[] { "zero", "one", "two", "one" };
         Model.SetList(new ModelDelta(), "names", names);
         var segment = new ArrayRunEnumSegment("v", 1, "names+1");
         for (int stored = 0; stored < 6; stored++) {
            Model[0] = (byte)stored;

            Assert.Equal(PlainText(names, stored - 1), segment.ToText(Model, 0, 0));
         }
      }

      [Fact]
      public void EnumText_TwoSegmentsOfTheSameList_ShowTheSame() {
         Model.SetList(new ModelDelta(), "names", AwkwardNames);
         var oneByte = new ArrayRunEnumSegment("a", 1, "names");
         var twoBytes = new ArrayRunEnumSegment("b", 2, "names");
         for (int value = 0; value < AwkwardNames.Length; value++) {
            Model[0] = (byte)value;
            Model[1] = 0;

            Assert.Equal(oneByte.ToText(Model, 0, 0), twoBytes.ToText(Model, 0, 0));
         }
      }

      [Fact]
      public void EnumText_TheNamesChangeWhenTheDataIsWritten_ShowTheNewNames() {
         CreateTextTable("pokenames", 0x80, "ABC", "DEF", "GHI");
         ViewPort.Edit("@20 ^species[name:pokenames]2 ");
         var segment = new ArrayRunEnumSegment("v", 1, "pokenames");
         Model[0] = 1;
         var before = segment.ToText(Model, 0, 0);

         ViewPort.Goto.Execute(0x84);
         ViewPort.Edit("XYZ\"");
         Model[0] = 1;

         Assert.Equal("DEF", before);
         Assert.Equal("XYZ", segment.ToText(Model, 0, 0));
      }

      [Fact]
      public void EnumText_ListWithManyNames_EveryValueMatchesCountingThePreviousOnes() {
         var names = Enumerable.Range(0, 300).Select(i => i % 7 == 0 ? "Same" : i % 11 == 0 ? "Other Name" : "Name" + i).ToArray();
         Model.SetList(new ModelDelta(), "names", names);
         var segment = new ArrayRunEnumSegment("v", 2, "names");
         for (int value = 0; value < names.Length; value++) {
            Model[0] = (byte)value;
            Model[1] = (byte)(value >> 8);

            Assert.Equal(PlainText(names, value), segment.ToText(Model, 0, 0));
         }
      }
   }
}
