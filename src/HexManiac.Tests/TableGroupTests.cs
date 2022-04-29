using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TableGroupTests : BaseViewModelTestClass {
      [Fact]
      public void TableGroup_LoadTable_TableToolContainsGroup() {
         ViewPort.Edit("@10 ^table1[data:]4 @20 ^table2[data:]table1 @30 ^table3[data:]table1 @40 ^table4[data:]table1 ");
         Model.AppendTableGroup(Token, "tables", new[] { "table1", "table2" }, null);

         ViewPort.Goto.Execute(0x10);
         var groups = ViewPort.Tools.TableTool.Groups;

         Assert.Equal(2, groups.Count);
         Assert.Equal("tables", groups[0].GroupName);
         Assert.Equal("Other", groups[1].GroupName);
      }

      // TODO undo/redo for SetTableGroup

      // TODO saving to metadata

      [Fact]
      public void TableGroupInMetadata_Load_TableGroupInModel() {
         Model.Load(new byte[0x200], new StoredMetadata(
            anchors: new[] {
               new StoredAnchor( 0, "table1", "[data:]4"),
               new StoredAnchor(20, "table2", "[data:]table1"),
               new StoredAnchor(40, "table3", "[data:]table1"),
               new StoredAnchor(60, "table4", "[data:]table1"),
            },
            tableGroups: new[] {
               new TableGroup("group1", new[] { "table1", "table2" }),
               new TableGroup("group2", new[] { "table3", "table4" }),
            }
         ));

         var groups = Model.GetTableGroups("table1");

         Assert.Equal(new[] { "group1", "group2" }, groups.Select(group => group.GroupName));
         Assert.Equal(new[] { "table1", "table2" }, groups[0].Tables);
         Assert.Equal(new[] { "table3", "table4" }, groups[1].Tables);
      }

      [Fact]
      public void Text_LoadMetadata_ContainsTableGroups() {
         var metadata = new StoredMetadata(new[] {
            "[[TableGroup]]",
            "Name = '''group1'''",
            "0 = [",
            "   '''table1''',",
            "   '''table2''',",
            "]",
         });

         Assert.Single(metadata.TableGroups);
         Assert.Equal("group1", metadata.TableGroups[0].GroupName);
         Assert.Equal(new[] { "table1", "table2" }, metadata.TableGroups[0].Tables);
      }

      [Fact]
      public void Metadata_Export_ContainsTableGroups() {
         var initialMetadata = new StoredMetadata(tableGroups: new[] { new TableGroup("group1", new[] { "table1" }) });
         Model.Load(new byte[0x200], initialMetadata);

         var newMetadata = Model.ExportMetadata(Singletons.MetadataInfo);
         var text = newMetadata.Serialize();

         Assert.Contains("[[TableGroup]]", text);
      }

      [Fact]
      public void NoGroups_UpdateVersion_Groups() {
         SetGameCode(HardcodeTablesModel.FireRed);
         var metadata = new StoredMetadata(
            anchors: new[] { new StoredAnchor(0x100, "anchor", string.Empty) },
            generalInfo: New.EarliestVersionInfo
         );

         var model = New.PokemonModel(Model.RawData, metadata, Singletons);

         var groups = model.ExportMetadata(Singletons.MetadataInfo).TableGroups;
         Assert.NotEmpty(groups);
      }

      [Fact]
      public void TableGroup_GenerateHash_HashMatches() {
         var group = new TableGroup("group", new[] { "table1", "table2" });
         Assert.True(group.HashMatches);
      }

      [Fact]
      public void TableGroup_IncludeWrongHash_HashDoesNotMatch() {
         var group = new TableGroup("group", new[] { "table1", "table2" }, "0");
         Assert.False(group.HashMatches);
      }

      [Fact]
      public void Groups_UpdateVersion_NoDuplicates() {
         // calculate 'default' TableGroups
         SetGameCode(HardcodeTablesModel.FireRed);
         var model = New.HardcodeTablesModel(Singletons, Model.RawData);
         var metadata = model.ExportMetadata(Singletons.MetadataInfo);
         var tableGroups = metadata.TableGroups;

         // upgrade version
         metadata = new StoredMetadata(
            tableGroups: tableGroups,
            generalInfo: New.EarliestVersionInfo);
         model = New.PokemonModel(model.RawData, metadata, Singletons);

         var newGroups = model.ExportMetadata(Singletons.MetadataInfo).TableGroups;
         Assert.Equal(tableGroups.Count, newGroups.Count);
         for (int i = 0; i < newGroups.Count; i++) {
            Assert.Equal(tableGroups[i].GroupName, newGroups[i].GroupName);
            Assert.Equal(tableGroups[i].Hash, newGroups[i].Hash);
         }
      }

      [Fact]
      public void TableGroupWithEditedHash_UpdateVersion_DoNotAddGroupsWithSameTables() {
         SetGameCode(HardcodeTablesModel.FireRed);
         var metadata = new StoredMetadata(
            tableGroups: new[] { new TableGroup("custom", new[] { HardcodeTablesModel.PokemonNameTable }, "0") },
            generalInfo: New.EarliestVersionInfo
         );

         var model = New.PokemonModel(Model.RawData, metadata, Singletons);
         var groups = model.ExportMetadata(Singletons.MetadataInfo).TableGroups;

         var group = groups.Single(group => group.GroupName == "custom");
         Assert.Single(group.Tables);
         Assert.Single(groups.Where(group => group.Tables.Contains(HardcodeTablesModel.PokemonNameTable)));
      }

      [Fact]
      public void Text_LoadMetadata_CaptureHash() {
         var text = @"
[[TableGroup]]
Name = '''custom'''
DefaultHash = '''0'''
0 = [
   '''table''',
]
";

         var metadata = new StoredMetadata(text.SplitLines());

         Assert.Equal("0", metadata.TableGroups.Single().Hash);
      }

      [Fact]
      public void SplitTable_HasSplitSegment() {
         var error = ArrayRun.TryParse(Model, "[data: | more:]4", 0, SortedSpan<int>.None, out var table);

         Assert.False(error.HasError);
         Assert.IsType<ArrayRunSplitterSegment>(table.ElementContent[1]);
      }

      [Fact]
      public void SplitTable_SplitGroup_LoadMultipleGroups() {
         Model.Load(new byte[0x200], new StoredMetadata(
            anchors: new[] {
               new StoredAnchor(0, "table1", "[data: | more:]4"),
            },
            tableGroups: new[] {
               new TableGroup("group1", new[] { "table1|0" }),
               new TableGroup("group2", new[] { "table1|1" }),
            }
         ));

         var groups = Model.GetTableGroups("table1");

         Assert.Equal(new[] { "group1", "group2" }, groups.Select(group => group.GroupName));
         Assert.Equal(new[] { "table1|0" }, groups[0].Tables);
         Assert.Equal(new[] { "table1|1" }, groups[1].Tables);
      }

      [Fact]
      public void SplitTable_LoadTableTool_LoadMultipleGroups() {
         Model.Load(new byte[0x200], new StoredMetadata(
            anchors: new[] {
               new StoredAnchor(0, "table1", "[data: | more:]4"),
            },
            tableGroups: new[] {
               new TableGroup("group1", new[] { "table1|0" }),
               new TableGroup("group2", new[] { "table1|1" }),
            }
         ));

         ViewPort.Goto.Execute(4);

         var groups = ViewPort.Tools.TableTool.Groups;

         Assert.Equal(2, groups.Count);
         Assert.Equal("data", groups[0].Members.Single<FieldArrayElementViewModel>().Name);
         Assert.Equal("more", groups[1].Members.Single<FieldArrayElementViewModel>().Name);
      }

      [Fact]
      public void SingleTableGroup_TableToolRefresh_StreamGroupObjectRemainsTheSame() {
         SetFullModel(0xFF);
         Model.WriteValue(Token, 0, 0);
         Model.WriteValue(Token, 4, 0);
         ViewPort.Edit("^table[ptr<\"\">]2 @{ Adam\" @} @{ Bob\" @} ");

         ViewPort.Goto.Execute(0);
         var oldGroup = ViewPort.Tools.TableTool.Groups[1];

         ViewPort.Goto.Execute(4);
         var newGroup = ViewPort.Tools.TableTool.Groups[1];

         Assert.Equal(oldGroup, newGroup, new TableGroupReferenceComparer());
         Assert.Single(newGroup.Members);
      }

      [Fact]
      public void StreamTable_RefreshTableTool_StreamGroupObjectRemainsTheSame() {
         SetFullModel(0xFF);
         Model.WriteValue(Token, 0, 0);
         Model.WriteValue(Token, 4, 0);
         ViewPort.Edit("^table[ptr<\"\">]!FFFF @{ Adam\" @} @{ Bob\" @} ");

         ViewPort.Goto.Execute(0);
         var oldGroup = ViewPort.Tools.TableTool.Groups[1];

         ViewPort.Goto.Execute(4);
         var newGroup = ViewPort.Tools.TableTool.Groups[1];

         Assert.Equal(oldGroup, newGroup, new TableGroupReferenceComparer());
         Assert.Single(newGroup.Members);
      }
   }

   internal class TableGroupReferenceComparer : IEqualityComparer<TableGroupViewModel> {
      public bool Equals(TableGroupViewModel? a, TableGroupViewModel? b) => a == b;

      public int GetHashCode([DisallowNull] TableGroupViewModel obj) => throw new System.NotImplementedException();
   }
}
