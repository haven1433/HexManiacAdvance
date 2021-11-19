using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class UpgradeTests {
      public static IEnumerable<object[]> GameAndTomlCombo {
         get {
            foreach (var game in new[] {
               "FireRed",
               "Emerald",
            }) {
               var gamePath = "sampleFiles/Pokemon " + game + ".gba";
               foreach (var toml in new[] {
                  "0.3.0",
                  "0.3.5",
                  "0.4.0",
               }) {
                  var tomlPath = "_" + toml + ".toml";
                  yield return new object[] { gamePath, tomlPath };
               }
            }
         }
      }

      [SkippableTheory]
      [MemberData(nameof(GameAndTomlCombo))]
      public void OldMetadata_Upgrade_MetadataMatchesNewLoad(string romName, string tomlName) {
         Skip.IfNot(File.Exists(romName), "Test cannot run without a real rom");

         var singletons = BaseViewModelTestClass.Singletons;
         var tomlPath = "../../../../../src/HexManiac.Tests/toml_versions/";
         var fileData = File.ReadAllBytes(romName);
         var code = fileData.GetGameCode();
         tomlPath += code + tomlName;
         var oldMetadata = new StoredMetadata(File.ReadAllLines(tomlPath));

         var upgradedModel = new HardcodeTablesModel(singletons, fileData, oldMetadata);
         var freshModel = new HardcodeTablesModel(singletons, fileData, new StoredMetadata(new string[0]));
         var upgradedMetadata = upgradedModel.ExportMetadata(singletons.MetadataInfo);
         var freshMetadata = freshModel.ExportMetadata(singletons.MetadataInfo);

         // verify that the content is the same
         Assert.Equal(upgradedMetadata.FreeSpaceBuffer, freshMetadata.FreeSpaceBuffer);
         // FreeSpaceSearch is allowed to be different
         Assert.Equal(upgradedMetadata.Version, freshMetadata.Version);
         Assert.Equal(upgradedMetadata.NextExportID, freshMetadata.NextExportID);

         // there may be some old non-deleted lists in upgradedMetadata. That's ok.
         // but for every list in the fresh metadata, make sure the upgraded metadata has it.
         foreach (var list in freshMetadata.Lists) {
            var updateList = upgradedMetadata.Lists.Single(updatedList => updatedList.Name == list.Name);
            Assert.Equal(list.Count, updateList.Count);
            Assert.All(updateList.Contents.Count.Range(), i => Assert.Equal(list.Contents[i], updateList.Contents[i]));
         }

         foreach (var matchedWord in upgradedMetadata.MatchedWords) {
            var newMatchedWord = freshMetadata.MatchedWords.Single(freshMatchedWord => freshMatchedWord.Address == matchedWord.Address);
            Assert.Equal(newMatchedWord.Name, matchedWord.Name);
            Assert.Equal(newMatchedWord.AddOffset, matchedWord.AddOffset);
            Assert.Equal(newMatchedWord.MultOffset, matchedWord.MultOffset);
            Assert.Equal(newMatchedWord.Note, matchedWord.Note);
         }

         // every anchor in the fresh metadata should be represented in the upgrade case
         var upgradeAnchorNames = upgradedMetadata.NamedAnchors.Select(na => na.Name).ToList();
         foreach (var namedAnchor in freshMetadata.NamedAnchors) {
            Assert.Contains(namedAnchor.Name, upgradeAnchorNames);
         }
         // every anchor in the upgraded metadata should have the right address and format to match the fresh versions
         foreach (var namedAnchor in upgradedMetadata.NamedAnchors) {
            bool exemptAddress = false, exemptFormat = false;
            if (tomlName == "_0.4.0.toml") {
               exemptAddress |= new[] { // legitimate moves: same name, new location
                  "graphics.gamecorner.game.palette",
                  "graphics.bag.inside2.palette",
               }.Contains(namedAnchor.Name);
               exemptFormat |= new[] { // legitimate format changes: same name, new format
                  "graphics.gamecorner.game.palette",
               }.Contains(namedAnchor.Name);
            }
            var newNamedAnchor = freshMetadata.NamedAnchors.Single(anchor => anchor.Name == namedAnchor.Name);
            if (!exemptAddress) Assert.True(newNamedAnchor.Address == namedAnchor.Address, $"Did {namedAnchor.Name} move?");
            if (!exemptFormat) Assert.True(newNamedAnchor.Format == namedAnchor.Format, $"Did {namedAnchor.Name} get a new format?");
         }

         foreach (var offsetPointer in upgradedMetadata.OffsetPointers) {
            var newOffsetPointer = freshMetadata.OffsetPointers.Single(pointer => pointer.Address == offsetPointer.Address);
            Assert.Equal(newOffsetPointer.Offset, offsetPointer.Offset);
         }
      }

      [SkippableTheory]
      [InlineData("sampleFiles/Pokemon FireRed.gba")]
      [InlineData("sampleFiles/Pokemon Emerald.gba")]
      [InlineData("sampleFiles/Pokemon Ruby.gba")]
      public void NewMetadata_Reopen_MetadataMatches(string romName) {
         Skip.IfNot(File.Exists(romName), "Test cannot run without a real rom");

         var singletons = BaseViewModelTestClass.Singletons;
         var fileData = File.ReadAllBytes(romName);
         var code = fileData.GetGameCode();

         var freshModel = new HardcodeTablesModel(singletons, fileData);
         var metadata1 = freshModel.ExportMetadata(singletons.MetadataInfo);
         var serializedBack = new StoredMetadata(metadata1.Serialize());
         var reopenModel = new HardcodeTablesModel(singletons, fileData, serializedBack);
         var metadata2 = reopenModel.ExportMetadata(singletons.MetadataInfo);

         // verify that the content is the same
         Assert.Equal(metadata1.FreeSpaceBuffer, metadata2.FreeSpaceBuffer);
         // FreeSpaceSearch is allowed to be different
         Assert.Equal(metadata1.Version, metadata2.Version);
         Assert.Equal(metadata1.NextExportID, metadata2.NextExportID);

         foreach (var list in metadata2.Lists) {
            var originalList = metadata1.Lists.Single(freshList => freshList.Name == list.Name);
            Assert.Equal(originalList.Count, list.Count);
            Assert.All(originalList.Contents.Count.Range(), i => Assert.Equal(originalList.Contents[i], list.Contents[i]));
         }

         foreach (var matchedWord in metadata2.MatchedWords) {
            var originalMatchedWord = metadata1.MatchedWords.Single(freshMatchedWord => freshMatchedWord.Address == matchedWord.Address);
            Assert.Equal(originalMatchedWord.Name, matchedWord.Name);
            Assert.Equal(originalMatchedWord.AddOffset, matchedWord.AddOffset);
            Assert.Equal(originalMatchedWord.MultOffset, matchedWord.MultOffset);
            Assert.Equal(originalMatchedWord.Note, matchedWord.Note);
         }

         foreach (var namedAnchor in metadata2.NamedAnchors) {
            var originalNamedAnchor = metadata1.NamedAnchors.Single(anchor => anchor.Name == namedAnchor.Name);
            Assert.Equal(originalNamedAnchor.Address, namedAnchor.Address);
            Assert.Equal(originalNamedAnchor.Format, namedAnchor.Format);
         }

         foreach (var offsetPointer in metadata2.OffsetPointers) {
            var originalOffsetPointer = metadata1.OffsetPointers.Single(pointer => pointer.Address == offsetPointer.Address);
            Assert.Equal(originalOffsetPointer.Offset, offsetPointer.Offset);
         }
      }
   }
}
