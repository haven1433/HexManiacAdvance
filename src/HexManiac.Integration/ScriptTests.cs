using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using System.IO;
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

      [SkippableFact]
      public void FireRed_PixilateStyleAbilities_NoErrors() {
         var firered = LoadFireRed();
         var script = new LoadedFile("Pixilate.hma", File.ReadAllBytes("resources/Scripts/Add Mechanics From Later Generations/AnyGame_PixilateStyleAbilities.hma"));

         firered.TryImport(script, default);

         Assert.Empty(Errors);
      }
   }
}
