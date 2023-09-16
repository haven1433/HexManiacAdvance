using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.IO;
using System.Linq;
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

      [SkippableFact]
      public void Emerald_AllTextFromScripts_NoOverflow() {
         var emerald = LoadReadOnlyEmerald();
         var model = emerald.Model;
         var scripts = Flags.GetAllTopLevelScripts(model);
         Assert.All(Flags.GetAllScriptSpots(model, emerald.Tools.CodeTool.ScriptParser, scripts, 0x0F, 0x67), spot => {
            var textOffset = model[spot.Address] switch {
               0x0F => spot.Address + 2, // loadpointer
               0x67 => spot.Address + 1, // preparemsg
               _ => throw new NotImplementedException(),
            };
            // loadpointer
            var textStart = model.ReadPointer(spot.Address + 2);
            if (!textStart.InRange(0, model.Count)) return;
            var text = model.TextConverter.Convert(model, textStart, 1000);
            var exclude = new[] { "[rival]", "[player]", "[buffer" };
            if (exclude.Any(text.Contains)) return; // don't validate text with buffers
            var overflow = model.TextConverter.GetOverflow(text, CodeBody.MaxEventTextWidth);
            Assert.Empty(overflow);
         });
      }

      [SkippableFact]
      public void FireRed_AllTextFromScripts_NoOverflow() {
         var firered = LoadReadOnlyFireRed();
         var model = firered.Model;
         var scripts = Flags.GetAllTopLevelScripts(model);
         Assert.All(Flags.GetAllScriptSpots(model, firered.Tools.CodeTool.ScriptParser, scripts, 0x0F, 0x67), spot => {
            var textOffset = model[spot.Address] switch {
               0x0F => spot.Address + 2, // loadpointer
               0x67 => spot.Address + 1, // preparemsg
               _ => throw new NotImplementedException(),
            };
            // loadpointer
            var textStart = model.ReadPointer(spot.Address + 2);
            if (!textStart.InRange(0, model.Count)) return;
            var text = model.TextConverter.Convert(model, textStart, 1000);
            var exclude = new[] { "[rival]", "[player]", "[buffer" };
            if (exclude.Any(text.Contains)) return; // don't validate text with buffers
            var overflow = model.TextConverter.GetOverflow(text, CodeBody.MaxEventTextWidth);
            Assert.Empty(overflow);
         });
      }

      [SkippableFact]
      public void Emerald_HM04_HasUses() {
         var emerald = LoadEmerald();

         var results = emerald.Tools.TableTool.FindXseScriptUses(HardcodeTablesModel.ItemsTableName, 342).ToList();

         Assert.NotEmpty(results);
      }
   }
}
