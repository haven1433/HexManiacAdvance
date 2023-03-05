using HavenSoft.HexManiac.Integration;
using Xunit;

namespace HexManiac.Integration {
   public class ScriptTests : IntegrationTests {
      [SkippableFact]
      public void AIScript_NoChangeEdit_NoOrphans() {
         var firered = LoadFireRed();
         firered.Goto.Execute("scripts.battle.ai.trainer/TryToKO/ai/");

         firered.Tools.CodeTool.Contents[0].Content += " ";

         Assert.All(firered.Model.Anchors, anchor => Assert.DoesNotContain("orphan", anchor));
      }
   }
}
