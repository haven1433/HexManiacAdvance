using HavenSoft.HexManiac.Core.Models.Runs;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ExpansionTrainerTeamTests {
      [Fact]
      public void ShowdownPaste_BecomesNativeText() {
         var paste = string.Join("\n",
            "Geodude @ Oran Berry",
            "Level: 12",
            "Ability: Rock Head",
            "EVs: 252 Atk / 4 Def",
            "IVs: 31 HP / 0 Atk",
            "Adamant Nature",
            "- Tackle",
            "- Defense Curl",
            "",
            "Nosepass (F) @ Hard Stone",
            "- Rock Tomb");

         var native = ExpansionTrainerTeamRun.NormalizeShowdown(paste).Replace("\r", "");
         var lines = native.Split('\n');

         Assert.Equal("12 Geodude (IVs=31/0/31/31/31/31) @\"Oran Berry\"", lines[0]);
         Assert.Equal("- Tackle", lines[1]);
         Assert.Equal("- Defense Curl", lines[2]);
         Assert.Equal("* ability=Rock Head evs=0/252/4/0/0/0 nature=Adamant", lines[3]);
         Assert.Equal("100 Nosepass @\"Hard Stone\"", lines[4]); // .party default level
         Assert.Equal("- Rock Tomb", lines[5]);
         Assert.Equal("* gender=female", lines[6]);
      }

      [Fact]
      public void NativeText_PassesThrough() {
         var text = "12 Geodude (IVs=31) @\"Oran Berry\"\n- Tackle\n* ability=\"Rock Head\"\n";
         Assert.Equal(text.Replace("\n", System.Environment.NewLine), ExpansionTrainerTeamRun.NormalizeShowdown(text));
      }

      [Fact]
      public void LevelLine_UnderNativeHeader_ReplacesLevel() {
         var text = "12 Geodude (IVs=31)\nLevel: 20\n- Tackle";
         var native = ExpansionTrainerTeamRun.NormalizeShowdown(text).Replace("\r", "").Split('\n');
         Assert.Equal("20 Geodude (IVs=31)", native[0]);
         Assert.Equal("- Tackle", native[1]);
      }
   }
}
