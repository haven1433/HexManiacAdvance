using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ListTests : BaseViewModelTestClass {
      [Fact]
      public void MetadataCanLoadList() {
         var lines = new List<string>();
         lines.Add("[[List]]");
         lines.Add("Name = '''moveeffects'''");
         lines.Add("1 = [");
         lines.Add("   '''abc''',");
         lines.Add("   '''\"def\"''',");
         lines.Add("]");
         lines.Add("5 = ['''xyz''']");
         lines.Add("7 = '''bob'''");

         var metadata = new StoredMetadata(lines.ToArray());

         var moveEffects = metadata.Lists.Single(list => list.Name == "moveeffects");
         Assert.Equal(8, moveEffects.Count);
         Assert.Equal("0", moveEffects[0]);
         Assert.Equal("abc", moveEffects[1]);
         Assert.Equal("\"def\"", moveEffects[2]);
         Assert.Equal("3", moveEffects[3]);
         Assert.Equal("4", moveEffects[4]);
         Assert.Equal("xyz", moveEffects[5]);
         Assert.Equal("6", moveEffects[6]);
         Assert.Equal("bob", moveEffects[7]);
      }

      [Fact]
      public void MetadataCanSaveList() {
         var input = new List<string> {
            "bob",
            "tom",
            null,
            "sam",
            "fry",
            "kevin",
            null,
            null,
            "carl",
         };
         var list = new StoredList("Input", input);

         var lines = new List<string>();
         list.AppendContents(lines);

         Assert.Equal(@"[[List]]
Name = '''Input'''
DefaultHash = '''0BEEDA92'''
0 = [
   '''bob''',
   '''tom''',
]
3 = [
   '''sam''',
   '''fry''',
   '''kevin''',
]
8 = '''carl'''
".Split(Environment.NewLine).ToList(), lines);
      }

      [Fact]
      public void ListWithPipe_WritePipeInEnum_EditWorks() {
         Model.SetList("list", new List<string> { "some|text", "other|text", "content" });
         ViewPort.Edit("^table[a:list]2 ");

         ViewPort.Edit("other|text ");

         Assert.Equal(1, Model.ReadMultiByteValue(0, 2));
      }

      [Fact]
      public void ListWithSpaceAndComma_WriteInEnum_EditWorks() {
         Model.SetList("list", new List<string> { "some|text", "other, text", "content" });
         ViewPort.Edit("^table[a:list]2 ");

         ViewPort.Edit("\"other, text\" ");

         Assert.Equal(1, Model.ReadMultiByteValue(0, 2));
         Assert.Equal(2, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void ListLengthTable_Append_CanAppend() {
         Model.SetList(new ModelDelta(), "list", "item1", "item2");

         ViewPort.Edit("^table[a:]list ");
         var table = Model.GetTable("table");

         Assert.True(table.CanAppend);
      }

      [Fact]
      public void ModelDelta_ExpressListChange_HasChange() {
         var token = new ModelDelta();
         token.ChangeList("list", new[] { "oldcontents" }, new[] { "newcontents" });
         Assert.False(token.HasDataChange);
         Assert.True(token.HasAnyChange);
      }

      [Fact]
      public void Model_ChangeList_TokenSeesChange() {
         Model.SetList(new ModelDelta(), "name", "a", "b");

         var token = new ModelDelta();
         Model.SetList(token, "name", "a", "b", "c");

         Assert.True(token.HasAnyChange);
      }

      [Fact]
      public void DeltaObjectWithListChange_ProcessWithModel_ListChanged() {
         Model.SetList(new ModelDelta(), "name", "a", "b");

         var token = new ModelDelta();
         token.ChangeList("name", new[] { "a" }, new[] { "a", "b" });
         token.Revert(Model);

         Model.TryGetList("name", out var list);
         Assert.Equal(new[] { "a" }, list);
      }

      [Fact]
      public void ListLengthArray_Append_ListGrows() {
         Model.SetList(new ModelDelta(), "list", "a", "b");
         ViewPort.Edit("^table[x:]list ");

         ViewPort.Goto.Execute(4);
         ViewPort.Edit("+");

         Model.TryGetList("list", out var list);
         var table = Model.GetTable("table");
         Assert.Equal(list, new[] { "a", "b", "unnamed2" });
      }

      [Fact]
      public void OneListLengthTable_EnumsUseList_AllowJumpToTable() {
         Model.SetList("list", new[] { "a", "b" });
         ViewPort.Edit("@00 ^table1[x:]list ");

         ViewPort.Edit("@100 ^table2[y:list]2 ");
         var tool = (ComboBoxArrayElementViewModel)ViewPort.Tools.TableTool.Children.Single(e => e is ComboBoxArrayElementViewModel cbaevm && cbaevm.Name == "y");
         tool.GotoSource.Execute();

         Assert.Equal(0, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void ElementWithSpace_CopyBitFlags_ResultHasQuotes() {
         Model.SetList("list", new[] { "noSpaces", "with spaces" });
         Model[0] = 2;
         var segment = new ArrayRunBitArraySegment("name", 1, "list");

         var text = segment.ToText(Model, 0, false);

         Assert.Equal("- \"with spaces\" /", text);
      }

      [Fact]
      public void DefaultToml_LoadList_HashMatches() {
         var metadatas = BaseModel.GetDefaultMetadatas("AXVE0", "BPRE", "BPEE0");

         foreach (var list in metadatas.SelectMany(metadata => metadata.Lists)) {
            Assert.True(list.HashMatches);
         }
      }

      [Fact]
      public void DefaultList_ChangeContent_HashDoesNotMatch() {
         var text = new List<string>();
         BaseModel.GetDefaultMetadatas().First().Lists[0].AppendContents(text);

         var hashLine = text.Single(line => line.Trim().StartsWith("DefaultHash = '''"));
         text[text.IndexOf(hashLine)] = "DefaultHash = '''0'''";
         var metadata = new StoredMetadata(text.ToArray());

         Assert.False(metadata.Lists[0].HashMatches);
      }

      [Fact]
      public void DefaultList_HashMatches() {
         var list = BaseModel.GetDefaultMetadatas().First().Lists[0];

         Assert.True(list.HashMatches);
      }

      [Fact]
      public void HashMatches_UpdateVersion_ListUpdates() {
         var someRealList = BaseModel.GetDefaultMetadatas().First().Lists.First();
         SetGameCode("BPRE0");

         var content = new[] { "some", "content" };
         var metadata = new StoredMetadata(
            generalInfo: new StubMetadataInfo { VersionNumber = "0.0.1" },
            lists: new[] { new StoredList(someRealList.Name, content, StoredList.GenerateHash(content)) }
         );
         var model = new PokemonModel(Model.RawData, metadata, Singletons);

         model.TryGetList(someRealList.Name, out var list);
         Assert.Equal(someRealList.Contents, list.ToList());
      }

      [Fact]
      public void HashDoesNotMatch_UpdateVersion_ListDoesNotUpdate() {
         var someRealList = BaseModel.GetDefaultMetadatas().First().Lists.First();
         SetGameCode("BPRE0");

         var content = new[] { "some", "content" };
         var metadata = new StoredMetadata(
            generalInfo: new StubMetadataInfo { VersionNumber = "0.0.1" },
            lists: new[] { new StoredList(someRealList.Name, content, hash: "0") }
         );
         var model = new PokemonModel(Model.RawData, metadata, Singletons);

         model.TryGetList(someRealList.Name, out var list);
         Assert.Equal(content, list.ToList());
      }

      [Fact]
      public void ListWithNullEntries_AutoComplete_NoThrow() {
         Model.SetList("list", new[] { "zero", "one", null, "three" });
         ViewPort.Edit("^table[a:list]2 ");

         var visitor = new AutocompleteCell(Model, "e", 0);
         ViewPort[0, 0].Format.Visit(visitor, 0);
         var result = visitor.Result.Select(item => item.DisplayText.Trim()).ToArray();

         Assert.Equal(new[] { "zero", "one", "three" }, result);
      }

      [Fact]
      public void ListWithNullEntries_FilterOptionInTableTool_FilterOptions() {
         Model.SetList("list", new[] { "zero", "one", null, "three" });
         ViewPort.Edit("^table[a:list]2 ");
         ViewPort.Refresh();

         var item = ViewPort.Tools.TableTool.Groups[0].Members.Single<ComboBoxArrayElementViewModel>();
         item.IsFiltering = true;
         item.FilterText = "r";

         Assert.Equal(new[] { "zero", "three" }, item.Options.Select(option => option.Text));
      }

      [Fact]
      public void ListWithNullEntries_NoFilter_DoNotShowNullOptions() {
         Model.SetList("list", new[] { "zero", "one", null, "three" });
         ViewPort.Edit("^table[a:list]2 ");
         ViewPort.Refresh();

         var item = ViewPort.Tools.TableTool.Groups[0].Members.Single<ComboBoxArrayElementViewModel>();

         Assert.Equal(new[] { "zero", "one", "three" }, item.Options.Select(option => option.Text));
      }

      [Fact]
      public void ListWithNullEntries_ByteHasNullName_ShowAsNumber() {
         Model[0] = 2;
         Model.SetList("list", new[] { "zero", "one", null, "three" });
         ViewPort.Edit("^table[a:list]2 ");
         ViewPort.Refresh();

         var item = ViewPort.Tools.TableTool.Groups[0].Members.Single<ComboBoxArrayElementViewModel>();

         Assert.Equal(new[] { "zero", "one", "2", "three" }, item.Options.Select(option => option.Text));
      }

      [Fact]
      public void ListWithNullEntries_SelectValueInTableTool_CorrectValueInModel() {
         Model.SetList("list", new[] { "zero", "one", null, "three" });
         ViewPort.Edit("^table[a:list]2 ");
         ViewPort.Refresh();

         var item = ViewPort.Tools.TableTool.Groups[0].Members.Single<ComboBoxArrayElementViewModel>();
         item.SelectedIndex = 2; // "three"

         Assert.Equal(3, Model[0]);
      }

      [Fact]
      public void ListWithNullEntries_ValueAfterNullInModel_ShowInTableTool() {
         Model[0] = 3;
         Model.SetList("list", new[] { "zero", "one", null, "three" });
         ViewPort.Edit("^table[a:list]2 ");
         ViewPort.Refresh();

         var item = ViewPort.Tools.TableTool.Groups[0].Members.Single<ComboBoxArrayElementViewModel>();

         Assert.Equal("three", item.FilterText);
      }

      [Fact]
      public void ListWithHexIndex_Load_ListLoaded() {
         var input = new List<string> {
            "[[List]]",
            "Name = '''test'''",
            "0x00 = '''name0'''",
            "0x02 = '''name2'''",
         };

         var metadata = new StoredMetadata(input.ToArray());

         var list = metadata.Lists.Single();
         Assert.Equal("name0", list[0]);
         Assert.Equal("1", list[1]);
         Assert.Equal("name2", list[2]);
         Assert.Equal(3, list.Count);
      }
   }
}
