using HavenSoft.HexManiac.Core;
using Xunit;

namespace HavenSoft.HexManiac.Integration {
   public class ScriptTests : IntegrationTests {
      [SkippableFact]
      public void AIScript_NoChangeEdit_NoOrphans() {
         var firered = LoadFireRed();
         firered.Goto.Execute("scripts.battle.ai.trainer/TryToKO/ai/");

         firered.Tools.CodeTool.Contents[0].Content += " ";

         Assert.All(firered.Model.Anchors, anchor => Assert.DoesNotContain("orphan", anchor));
      }

      [SkippableTheory]
      [InlineData("SkyAttack")] // should not use sections because something points to <2CD184>
      public void AnimationScript_ContainsSectionPointers_ContainsSections(string move) {
         var emerald = LoadEmerald();
         emerald.Goto.Execute($"graphics.pokemon.moves.animations/{move}/animation/");

         var content = emerald.Tools.CodeTool.Contents[0].Content;

         Assert.All(5.Range(i => i + 1), i => Assert.NotEqual(2, content.Split($"section{i}").Length));
      }
   }
}
