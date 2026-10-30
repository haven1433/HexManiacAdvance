using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Linq;
using Xunit;
namespace HavenSoft.HexManiac.Tests {
   public class ItemEffectTests : BaseViewModelTestClass {
      private readonly PIERun run;
      public ItemEffectTests() {
         run = new PIERun(Model, 0, SortedSpan<int>.None);
         Model.ObserveRunWritten(Token, run);
         Model.WriteValue(Token, run.Length, -1);
      }

      [Fact]
      public void NoLabel_Autocomplete_NoOptions() {
         var options = run.GetAutoCompleteOptions("some text", 0, 9);
         Assert.Empty(options);
      }

      [Fact]
      public void ApplyToFirstPokemon_Autocomplete_BoolOptions() {
         var options = run.GetAutoCompleteOptions("ApplyToFirstPokemonOnly=", 0, 24);

         Assert.Equal("false", options[0].Text);
         Assert.Equal("ApplyToFirstPokemonOnly = true", options[1].LineText);
      }

      [Fact]
      public void Arg_Autocomplete_ArgOptions() {
         var options = run.GetAutoCompleteOptions("Arg=", 0, 4);

         Assert.Equal("LevelUpHealth", options[0].Text);
         Assert.Equal("Half", options[1].Text);
         Assert.Equal("Arg = Max", options[2].LineText);
      }

      [Fact]
      public void General_AutoCompleteBeforeEquals_NoOptions() {
         var options = run.GetAutoCompleteOptions("General = {", 0, 8);
         Assert.Empty(options);
      }

      [Fact]
      public void General_AutoCompleteAfterCurly_RemainingOptions() {
         var options = run.GetAutoCompleteOptions("General = { GuardSpec,  LevelUp }", 0, 23);
         Assert.Equal(4, options.Count);
         Assert.Equal("HealHealth", options[0].Text);
         Assert.Equal("General = { GuardSpec, HealPowerPoints, LevelUp }", options[1].LineText);
      }

      [Fact]
      public void General_AutoCompleteMidWord_FilteredOptions() {
         var options = run.GetAutoCompleteOptions("General = { heal }", 0, 16);
         Assert.Equal(3, options.Count);
         Assert.Equal("HealHealth", options[0].Text);
         Assert.Equal("General = { HealPowerPoints }", options[1].LineText);
      }

      [Fact]
      public void ClearStat_AutoComplete_StatusEffectOptions() {
         var options = run.GetAutoCompleteOptions("ClearStat = {}", 0, 13);
         Assert.Equal(7, options.Count);
         Assert.All(new[] { "Infatuation", "Sleep", "Poison", "Burn", "Ice", "Paralyze", "Confusion" },
            option => Assert.Contains(option, options.Select(o => o.Text)));
      }

      [Fact]
      public void IncreaseStat_AutoComplete_StatOptions() {
         var options = run.GetAutoCompleteOptions("IncreaseStat = {  }", 0, 17);
         Assert.Equal(8, options.Count);
         Assert.All(new[] { "HpEv", "AttackEv", "DefenseEv", "SpecialAttackEv", "SpecialDefenseEv", "SpeedEv", "MaxPowerPoints", "PowerPointsToMax" },
            option => Assert.Contains(option, options.Select(o => o.Text)));
      }

      [Fact]
      public void ChangeHappiness_AutoComplete_RangeOptions() {
         var options = run.GetAutoCompleteOptions("ChangeHappiness = { }", 0, 18);
         Assert.Equal(3, options.Count);
         Assert.All(new[] { "Low", "Mid", "High" },
            option => Assert.Contains(option, options.Select(o => o.Text)));
      }

      [Fact]
      public void AddBitRequiringArg_RunExtended() {
         ViewPort.Goto.Execute(4);

         ViewPort.Edit("4 ");

         Assert.Equal(7, Model.GetNextRun(0).Length);
      }

      [Fact]
      public void RemoveBitRequiringArg_Contract() {
         using (run.CreateEditScope(Token)) run.HealHealth = true;
         PIERun newRun = new PIERun(Model, 0, SortedSpan<int>.None);
         Model.ObserveRunWritten(Token, newRun);

         ViewPort.Goto.Execute(4);
         ViewPort.Edit("0 ");

         Assert.Equal(6, Model.GetNextRun(0).Length);
      }

      [Fact]
      public void Copy_CopiesBytes() {
         ViewPort.SelectionStart = new(0, 0);
         ViewPort.SelectionEnd = new(5, 0);

         ViewPort.Copy.Execute(FileSystem);

         Assert.Contains("00 00 00 00 00 00 ", FileSystem.CopyText.value);
      }

      [Fact]
      public void ItemEffectContent_Paste_DataChanges() {
         SetFullModel(0xFF);

         ViewPort.Edit("@010 ^misc._252705`pie` 00 00 00 20 00 00 ");

         var data = Model.RawData.Skip(0x10).Take(7).ToArray();
         Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0xFF }, data);
      }

      [Fact]
      public void TextTool_EditItemEffect_ChangeModel() {
         ViewPort.Edit("00 00 00 00 04 00 14 @000 ^run`pie` ");
         var textTool = ViewPort.Tools.StringTool;

         var lines = textTool.Content.SplitLines();
         var lineIndex = lines.IndexOfPartial("Arg =");
         lines[lineIndex] = "Arg = 30";
         textTool.Content = Environment.NewLine.Join(lines);

         Assert.Equal(30, Model[6]);
      }

      [Fact]
      public void TableTool_EditArgWithNamedValue_ChangeModel() {
         ViewPort.Edit("00 00 00 00 04 00 14 @000 ^run`pie` @100 <run> @100 ^table[ptr<`pie`>]1 ");

         var stream = ViewPort.Tools.TableTool.Groups[1].Members.Single<TextStreamElementViewModel>();
         var lines = stream.Content.SplitLines();
         var lineIndex = lines.IndexOfPartial("Arg =");
         lines[lineIndex] = "Arg = Half";
         stream.Content = Environment.NewLine.Join(lines);

         Assert.Equal(PIERun.HealthRestore_Half, (sbyte)Model[6]);
      }
   }
}
