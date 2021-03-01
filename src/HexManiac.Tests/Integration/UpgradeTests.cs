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
         var tomlPath = "../../../../src/HexManiac.Tests/toml_versions/";
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

         foreach (var list in upgradedMetadata.Lists) {
            var newList = freshMetadata.Lists.Single(freshList => freshList.Name == list.Name);
            Assert.Equal(newList.Count, list.Count);
            Assert.All(newList.Contents.Count.Range(), i => Assert.Equal(newList.Contents[i], list.Contents[i]));
         }

         foreach (var matchedWord in upgradedMetadata.MatchedWords) {
            var newMatchedWord = freshMetadata.MatchedWords.Single(freshMatchedWord => freshMatchedWord.Address == matchedWord.Address);
            Assert.Equal(newMatchedWord.Name, matchedWord.Name);
            Assert.Equal(newMatchedWord.AddOffset, matchedWord.AddOffset);
            Assert.Equal(newMatchedWord.MultOffset, matchedWord.MultOffset);
            Assert.Equal(newMatchedWord.Note, matchedWord.Note);
         }

         foreach (var namedAnchor in upgradedMetadata.NamedAnchors) {
            var newNamedAnchor = freshMetadata.NamedAnchors.Single(anchor => anchor.Name == namedAnchor.Name);
            Assert.Equal(newNamedAnchor.Address, namedAnchor.Address);
            Assert.Equal(newNamedAnchor.Format, namedAnchor.Format);
         }

         foreach (var offsetPointer in upgradedMetadata.OffsetPointers) {
            var newOffsetPointer = freshMetadata.OffsetPointers.Single(pointer => pointer.Address == offsetPointer.Address);
            Assert.Equal(newOffsetPointer.Offset, offsetPointer.Offset);
         }
      }
   }
}
