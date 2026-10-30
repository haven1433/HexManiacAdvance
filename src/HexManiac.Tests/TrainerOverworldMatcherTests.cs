using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The trainer template picks the overworld sprite that goes with the trainer's picture, by name, and never guesses.</summary>
   public class TrainerOverworldMatcherTests {
      // a few of the names of the CUBE overworld list, in a different order than the ROM
      private static readonly string[] OverworldNames = {
         "BRENDAN_NORMAL", "RIVAL_BRENDAN_NORMAL", "RIVAL_MAY_NORMAL", "HIKER", "MAN_3", "MAN_5", "WOMAN_5", "MANIAC",
         "AQUA_MEMBER_M", "AQUA_MEMBER_F", "MAGMA_MEMBER_M", "MAGMA_MEMBER_F", "ARCHIE", "MAXIE", "TWIN",
         "ROXANNE", "DRAKE", "TATE", "LIZA", "COOLTRAINER_M", "COOLTRAINER_F", "BUG_CATCHER", "BUG_CATCHER_FRLG",
         "BROCK", "SCIENTIST_1", "SCIENTIST", "POKE_MANIAC_FRLG", "CAMERAMAN", "REPORTER_F", "LASS",
      };

      private static int Find(string pic, params string[] overworld) => TrainerOverworldMatcher.Find(pic, overworld);

      private static string FindName(string pic, params string[] overworld) {
         var index = TrainerOverworldMatcher.Find(pic, overworld);
         return index < 0 ? null : overworld[index];
      }

      [Fact]
      public void TeamMembers_UseTheTeamSprites() {
         Assert.Equal("AQUA_MEMBER_M", FindName("AQUA_GRUNT_M", OverworldNames));
         Assert.Equal("AQUA_MEMBER_F", FindName("AQUA_GRUNT_F", OverworldNames));
         Assert.Equal("AQUA_MEMBER_M", FindName("AQUA_ADMIN_M", OverworldNames));
         Assert.Equal("MAGMA_MEMBER_M", FindName("MAGMA_GRUNT_M", OverworldNames));
         Assert.Equal("MAGMA_MEMBER_F", FindName("MAGMA_GRUNT_F", OverworldNames));
         Assert.Equal("ARCHIE", FindName("AQUA_LEADER_ARCHIE", OverworldNames));
         Assert.Equal("MAXIE", FindName("MAGMA_LEADER_MAXIE", OverworldNames));
      }

      [Fact]
      public void RivalPicture_UsesTheRivalSpriteNotThePlayerSprite() {
         Assert.Equal("RIVAL_BRENDAN_NORMAL", FindName("BRENDAN", OverworldNames));
         Assert.Equal("RIVAL_MAY_NORMAL", FindName("MAY", OverworldNames));
      }

      [Fact]
      public void SameName_Matches() {
         Assert.Equal("HIKER", FindName("HIKER", OverworldNames));
         Assert.Equal("LASS", FindName("LASS", OverworldNames));
      }

      [Fact]
      public void ClassPrefix_IsIgnored() {
         Assert.Equal("ROXANNE", FindName("LEADER_ROXANNE", OverworldNames));
         Assert.Equal("DRAKE", FindName("ELITE_FOUR_DRAKE", OverworldNames));
      }

      [Fact]
      public void FrlgSuffix_IsIgnoredOnlyWhenTheFullNameHasNoSprite() {
         Assert.Equal("COOLTRAINER_M", FindName("COOLTRAINER_M_FRLG", OverworldNames));
         Assert.Equal("BROCK", FindName("LEADER_BROCK_FRLG", OverworldNames));
         Assert.Equal("BUG_CATCHER_FRLG", FindName("BUG_CATCHER_FRLG", OverworldNames));
         Assert.Equal("BUG_CATCHER", FindName("BUG_CATCHER", OverworldNames));
      }

      [Fact]
      public void Punctuation_DoesNotMatter() {
         Assert.Equal("POKE_MANIAC_FRLG", FindName("POKEMANIAC_FRLG", OverworldNames));
         Assert.Equal("AQUA_MEMBER_M", FindName("aqua grunt m", OverworldNames));
      }

      [Fact]
      public void DecompPrefixes_AreIgnored() {
         Assert.Equal(1, Find("TRAINER_PIC_AQUA_GRUNT_M", "OBJ_EVENT_GFX_HIKER", "OBJ_EVENT_GFX_AQUA_MEMBER_M"));
      }

      [Fact]
      public void ExplicitTable_WinsOverTheSameName() {
         // the Emerald cooltrainer wears the MAN_3 sprite; the sprite that has the same name is the FireRed look
         Assert.Equal("MAN_3", FindName("COOLTRAINER_M", OverworldNames));
         Assert.Equal("WOMAN_5", FindName("COOLTRAINER_F", OverworldNames));
      }

      [Fact]
      public void ExplicitTable_FallsBackToTheSameName_WhenItsSpriteIsMissing() {
         Assert.Equal("COOLTRAINER_M", FindName("COOLTRAINER_M", "COOLTRAINER_M", "HIKER"));
      }

      [Fact]
      public void TwoSpritesForOnePicture_TakesTheFirstThatExists() {
         Assert.Equal("TATE", FindName("LEADER_TATE_AND_LIZA", OverworldNames));
         Assert.Equal("LIZA", FindName("LEADER_TATE_AND_LIZA", "LIZA", "HIKER"));
      }

      [Fact]
      public void NoSpriteThatGoesWithThePicture_IsNotAMatch() {
         Assert.Equal(-1, Find("POKEDUDE", OverworldNames));
         Assert.Equal(-1, Find("SOMETHING_NEW", OverworldNames));
         Assert.Equal(-1, Find("NONE", OverworldNames));
      }

      [Fact]
      public void PictureThatTheGameSplitsOverTwoSprites_IsNotAMatch() {
         // interviewers are cameramen in some places and reporters in others
         Assert.Equal(-1, Find("INTERVIEWER", OverworldNames));
      }

      [Fact]
      public void SharedWord_IsNotAMatch() {
         Assert.Equal(-1, Find("SCIENTIST_FRLG_JR", OverworldNames));
         Assert.Equal(-1, Find("AQUA", OverworldNames));
         Assert.Equal(-1, Find("MEMBER_M", OverworldNames));
      }

      [Fact]
      public void ScientistPictureMatchesTheScientistSprite_NotJustAnyNpc() {
         Assert.Equal("SCIENTIST", FindName("SCIENTIST_FRLG", OverworldNames));
      }

      [Fact]
      public void NamesThatAreOnlyNumbers_AreNotNames() {
         var indexes = Enumerable.Range(0, 30).Select(i => i.ToString()).ToArray();
         Assert.False(TrainerOverworldMatcher.HasNames(indexes));
         Assert.Equal(-1, Find("12", indexes));
         Assert.Equal(-1, Find("AQUA_GRUNT_M", indexes));
      }

      [Fact]
      public void HasNames_NeedsOneRealName() {
         Assert.True(TrainerOverworldMatcher.HasNames(OverworldNames));
         Assert.True(TrainerOverworldMatcher.HasNames(new string[] { null, "", "3", "HIKER" }));
         Assert.False(TrainerOverworldMatcher.HasNames(new string[] { null, "", "3" }));
         Assert.False(TrainerOverworldMatcher.HasNames(null));
         Assert.False(TrainerOverworldMatcher.HasNames(new string[0]));
      }

      [Fact]
      public void MissingNames_DoNotThrow() {
         Assert.Equal(-1, Find(null, OverworldNames));
         Assert.Equal(-1, Find("", OverworldNames));
         Assert.Equal(-1, TrainerOverworldMatcher.Find("HIKER", null));
         Assert.Equal(1, Find("HIKER", null, "HIKER"));
         Assert.Equal(-1, Find("HIKER"));
      }

      [Fact]
      public void SeveralSpritesWithTheSameName_TakesTheFirst() {
         Assert.Equal(1, Find("HIKER", "BOY", "HIKER", "HIKER"));
      }

      [Fact]
      public void BuildMapping_HasOneEntryPerPicture() {
         var pictures = new List<string> { "NONE", "AQUA_GRUNT_M", "HIKER", "POKEDUDE" };
         var mapping = TrainerOverworldMatcher.BuildMapping(pictures, OverworldNames);
         Assert.Equal(4, mapping.Count);
         Assert.Equal(-1, mapping[0]);
         Assert.Equal("AQUA_MEMBER_M", OverworldNames[mapping[1]]);
         Assert.Equal("HIKER", OverworldNames[mapping[2]]);
         Assert.Equal(-1, mapping[3]);
      }
   }
}
