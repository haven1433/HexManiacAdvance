using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// View > Graphics: "Use dedicated graphics card" and "Hardware accelerated rendering".
   /// The Windows registry and the WPF render mode are not touched here (the WPF layer does that): these tests cover what is decided, what is written for it, and how the two settings are loaded and saved.
   /// </summary>
   public class GraphicsSettingsTests {
      private static EditorViewModel CreateEditor(params string[] settings) {
         var fileSystem = new StubFileSystem { MetadataFor = name => settings.Length == 0 ? null : settings };
         return new EditorViewModel(fileSystem);
      }

      #region Names and the settings file

      [Fact]
      public void SettingNames_AreStable_BecauseTheyAreWrittenToTheUsersSettingsFile() {
         Assert.Equal("UseDedicatedGraphics", GraphicsSettings.UseDedicatedGraphicsSetting);
         Assert.Equal("UseHardwareAcceleration", GraphicsSettings.UseHardwareAccelerationSetting);
         Assert.Equal("UseDedicatedGraphics = True", GraphicsSettings.SettingLine(GraphicsSettings.UseDedicatedGraphicsSetting, true));
         Assert.Equal("UseHardwareAcceleration = False", GraphicsSettings.SettingLine(GraphicsSettings.UseHardwareAccelerationSetting, false));
      }

      [Fact]
      public void RegistryLocation_IsWhereWindowsKeepsTheGraphicsCardChoicePerProgram() {
         Assert.Equal(@"Software\Microsoft\DirectX\UserGpuPreferences", GraphicsSettings.GpuPreferenceRegistryKey);
         Assert.Equal("GpuPreference", GraphicsSettings.GpuPreferenceEntry);
      }

      [Fact]
      public void FrameRate_IsFarAboveAnyDisplay() {
         Assert.True(GraphicsSettings.UncappedFrameRate >= 360, "the 60 frames per second default of WPF animations must be gone, even for 360 Hz displays");
      }

      [Fact]
      public void IsEnabled_BothSettingsAreOnUnlessTheFileSaysFalse() {
         Assert.True(GraphicsSettings.IsEnabled(null, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.True(GraphicsSettings.IsEnabled(new string[0], GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.True(GraphicsSettings.IsEnabled(new[] { "[GeneralSettings]", "AnimateScroll = False" }, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.True(GraphicsSettings.IsEnabled(new[] { "UseDedicatedGraphics = True" }, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.True(GraphicsSettings.IsEnabled(new[] { "UseDedicatedGraphics = banana" }, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.True(GraphicsSettings.IsEnabled(new[] { "UseDedicatedGraphics = " }, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.False(GraphicsSettings.IsEnabled(new[] { "UseDedicatedGraphics = False" }, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.False(GraphicsSettings.IsEnabled(new[] { "[GeneralSettings]", "  UseHardwareAcceleration = False  " }, GraphicsSettings.UseHardwareAccelerationSetting));
      }

      [Fact]
      public void IsEnabled_TheTwoSettingsDoNotAffectEachOther() {
         var lines = new[] { "UseDedicatedGraphics = False" };
         Assert.False(GraphicsSettings.IsEnabled(lines, GraphicsSettings.UseDedicatedGraphicsSetting));
         Assert.True(GraphicsSettings.IsEnabled(lines, GraphicsSettings.UseHardwareAccelerationSetting));
      }

      #endregion

      #region The registry text

      [Theory]
      [InlineData(GpuPreference.WindowsDecides, "GpuPreference=0;")]
      [InlineData(GpuPreference.PowerSaving, "GpuPreference=1;")]
      [InlineData(GpuPreference.HighPerformance, "GpuPreference=2;")]
      public void GpuPreferenceData_IsWhatWindowsWritesItself(GpuPreference preference, string expected) {
         Assert.Equal(expected, GraphicsSettings.GpuPreferenceData(preference));
      }

      [Theory]
      [InlineData("", "GpuPreference=2;")]
      [InlineData(null, "GpuPreference=2;")]
      [InlineData("GpuPreference=2;", "GpuPreference=2;")]
      [InlineData("GpuPreference=1;", "GpuPreference=2;")]
      [InlineData("GpuPreference=0;", "GpuPreference=2;")]
      [InlineData("GpuPreference=2", "GpuPreference=2;")]
      [InlineData("SwapEffectUpgradeEnable=1;", "SwapEffectUpgradeEnable=1;GpuPreference=2;")]
      [InlineData("GpuPreference=1;SwapEffectUpgradeEnable=1;", "GpuPreference=2;SwapEffectUpgradeEnable=1;")]
      [InlineData("SwapEffectUpgradeEnable=1;GpuPreference=1;AutoHDREnable=0;", "SwapEffectUpgradeEnable=1;GpuPreference=2;AutoHDREnable=0;")]
      [InlineData("gpupreference=1;", "GpuPreference=2;")]
      [InlineData(" GpuPreference = 1 ; ", "GpuPreference=2;")]
      [InlineData("GpuPreference=1;GpuPreference=0;", "GpuPreference=2;")]
      public void WithGpuPreference_HighPerformance_ChangesOnlyTheChoice(string existing, string expected) {
         Assert.Equal(expected, GraphicsSettings.WithGpuPreference(existing, GpuPreference.HighPerformance));
      }

      [Theory]
      [InlineData("", "")]
      [InlineData(null, "")]
      [InlineData("GpuPreference=2;", "")]
      [InlineData("GpuPreference=2;SwapEffectUpgradeEnable=1;", "SwapEffectUpgradeEnable=1;")]
      [InlineData("SwapEffectUpgradeEnable=1;GpuPreference=2;AutoHDREnable=0;", "SwapEffectUpgradeEnable=1;AutoHDREnable=0;")]
      [InlineData("SwapEffectUpgradeEnable=1;", "SwapEffectUpgradeEnable=1;")]
      [InlineData("GpuPreference=1;GpuPreference=2;", "")]
      public void WithGpuPreference_Null_RemovesTheChoiceAndKeepsTheRest(string existing, string expected) {
         Assert.Equal(expected, GraphicsSettings.WithGpuPreference(existing, null));
      }

      [Theory]
      [InlineData(null, null)]
      [InlineData("", null)]
      [InlineData("SwapEffectUpgradeEnable=1;", null)]
      [InlineData("GpuPreference=2;", GpuPreference.HighPerformance)]
      [InlineData("GpuPreference=1;", GpuPreference.PowerSaving)]
      [InlineData("GpuPreference=0;", GpuPreference.WindowsDecides)]
      [InlineData("SwapEffectUpgradeEnable=1;GpuPreference=2;", GpuPreference.HighPerformance)]
      [InlineData("GpuPreference=7;", null)]
      [InlineData("GpuPreference=banana;", null)]
      [InlineData("GpuPreference=;", null)]
      public void GpuPreferenceOf_ReadsTheChoice(string data, GpuPreference? expected) {
         Assert.Equal(expected, GraphicsSettings.GpuPreferenceOf(data));
      }

      #endregion

      #region What to do with the registry

      private static void AssertPlan(GpuRegistryPlan plan, GpuRegistryAction action, string data = "") {
         Assert.Equal(action, plan.Action);
         Assert.Equal(data, plan.Data);
      }

      [Fact]
      public void On_WithNothingInTheRegistry_WritesHighPerformance() {
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, null), GpuRegistryAction.Write, "GpuPreference=2;");
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, ""), GpuRegistryAction.Write, "GpuPreference=2;");
      }

      [Fact]
      public void On_WhenTheRegistryAlreadySaysSo_LeavesItAlone() {
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, "GpuPreference=2;"), GpuRegistryAction.LeaveAlone);
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, "SwapEffectUpgradeEnable=1;GpuPreference=2;"), GpuRegistryAction.LeaveAlone);
      }

      [Fact]
      public void On_WhenTheRegistryHasAnotherChoice_ReplacesItAndKeepsTheOtherEntries() {
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, "GpuPreference=1;"), GpuRegistryAction.Write, "GpuPreference=2;");
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, "GpuPreference=0;"), GpuRegistryAction.Write, "GpuPreference=2;");
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, "SwapEffectUpgradeEnable=1;"), GpuRegistryAction.Write, "SwapEffectUpgradeEnable=1;GpuPreference=2;");
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, false, "GpuPreference=1;SwapEffectUpgradeEnable=1;"), GpuRegistryAction.Write, "GpuPreference=2;SwapEffectUpgradeEnable=1;");
      }

      [Fact]
      public void On_DoesNotCareWhetherItWasJustSwitchedOn() {
         AssertPlan(GraphicsSettings.PlanGpuPreference(true, true, null), GpuRegistryAction.Write, "GpuPreference=2;");
      }

      [Fact]
      public void Off_AsItWasAtStartup_NeverTouchesTheRegistry() {
         foreach (var existing in new[] { null, "", "GpuPreference=2;", "GpuPreference=1;", "SwapEffectUpgradeEnable=1;GpuPreference=2;", "GpuPreference=0;" }) {
            AssertPlan(GraphicsSettings.PlanGpuPreference(false, false, existing), GpuRegistryAction.LeaveAlone);
         }
      }

      [Fact]
      public void Off_JustNow_TakesBackTheHighPerformanceChoice() {
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, "GpuPreference=2;"), GpuRegistryAction.Delete);
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, "SwapEffectUpgradeEnable=1;GpuPreference=2;"), GpuRegistryAction.Write, "SwapEffectUpgradeEnable=1;");
      }

      [Fact]
      public void Off_JustNow_LeavesAnotherChoiceAlone() {
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, "GpuPreference=1;"), GpuRegistryAction.LeaveAlone);
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, "GpuPreference=0;"), GpuRegistryAction.LeaveAlone);
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, "SwapEffectUpgradeEnable=1;"), GpuRegistryAction.LeaveAlone);
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, null), GpuRegistryAction.LeaveAlone);
         AssertPlan(GraphicsSettings.PlanGpuPreference(false, true, ""), GpuRegistryAction.LeaveAlone);
      }

      // Plays a plan into a pretend registry value (null = no value), the way the WPF layer does.
      private static string Apply(GpuRegistryPlan plan, string value) {
         if (plan.Action == GpuRegistryAction.Write) return plan.Data;
         if (plan.Action == GpuRegistryAction.Delete) return null;
         return value;
      }

      [Fact]
      public void ASequenceOfStartsAndToggles_EndsWhereItShould() {
         string registry = null;

         // first start: on by default
         registry = Apply(GraphicsSettings.PlanGpuPreference(true, false, registry), registry);
         Assert.Equal("GpuPreference=2;", registry);

         // second start: nothing to do
         Assert.Equal(GpuRegistryAction.LeaveAlone, GraphicsSettings.PlanGpuPreference(true, false, registry).Action);

         // the user switches it off: the value goes away again
         registry = Apply(GraphicsSettings.PlanGpuPreference(false, true, registry), registry);
         Assert.Null(registry);

         // next starts, still off: the registry is not touched (not even to make a value)
         Assert.Equal(GpuRegistryAction.LeaveAlone, GraphicsSettings.PlanGpuPreference(false, false, registry).Action);

         // the user switches it on again
         registry = Apply(GraphicsSettings.PlanGpuPreference(true, true, registry), registry);
         Assert.Equal("GpuPreference=2;", registry);
      }

      [Fact]
      public void ASwitchOff_KeepsWhatWindowsWroteForOtherReasons() {
         string registry = "SwapEffectUpgradeEnable=1;";
         registry = Apply(GraphicsSettings.PlanGpuPreference(true, false, registry), registry);
         Assert.Equal("SwapEffectUpgradeEnable=1;GpuPreference=2;", registry);
         registry = Apply(GraphicsSettings.PlanGpuPreference(false, true, registry), registry);
         Assert.Equal("SwapEffectUpgradeEnable=1;", registry);
      }

      #endregion

      #region Which program

      private static string Program(params string[] parts) => Path.Combine(new[] { Path.GetTempPath() }.Concat(parts).ToArray());

      [Fact]
      public void RegistryValueName_IsTheFullPathOfTheProgram() {
         var path = Program("Apps", "HexManiacAdvance.exe");
         Assert.Equal(Path.GetFullPath(path), GraphicsSettings.RegistryValueNameFor(path));
      }

      [Fact]
      public void RegistryValueName_MakesRelativePathsFull() {
         var name = GraphicsSettings.RegistryValueNameFor("HexManiacAdvance.exe");
         Assert.True(Path.IsPathRooted(name));
         Assert.EndsWith("HexManiacAdvance.exe", name);
      }

      [Fact]
      public void RegistryValueName_NoProgram_IsNull() {
         Assert.Null(GraphicsSettings.RegistryValueNameFor(null));
         Assert.Null(GraphicsSettings.RegistryValueNameFor(""));
         Assert.Null(GraphicsSettings.RegistryValueNameFor("   "));
      }

      [Fact]
      public void RegistryValueName_NotAnExe_IsNull() {
         Assert.Null(GraphicsSettings.RegistryValueNameFor(Program("Apps", "HexManiacAdvance.dll")));
         Assert.Null(GraphicsSettings.RegistryValueNameFor(Program("Apps", "HexManiacAdvance")));
      }

      [Fact]
      public void RegistryValueName_TheDotnetLauncher_IsNull_SoNoOtherProgramGetsTheSetting() {
         // when HexManiacAdvance is started as 'dotnet HexManiacAdvance.dll', the running program is dotnet.exe
         Assert.Null(GraphicsSettings.RegistryValueNameFor(Program("dotnet", "dotnet.exe")));
         Assert.Null(GraphicsSettings.RegistryValueNameFor(Program("dotnet", "DOTNET.EXE")));
         Assert.NotNull(GraphicsSettings.RegistryValueNameFor(Program("dotnet", "notdotnet.exe")));
      }

      #endregion

      #region What the user is told

      [Fact]
      public void DescribeRendering_SaysWhatWillHappen() {
         Assert.Contains("tier 2", GraphicsSettings.DescribeRendering(2, true));
         Assert.Contains("tier 1", GraphicsSettings.DescribeRendering(1, true));
         Assert.Contains("tier 0", GraphicsSettings.DescribeRendering(0, true));
         Assert.Contains("processor", GraphicsSettings.DescribeRendering(0, true));
         Assert.Contains("driver", GraphicsSettings.DescribeRendering(0, true));
         Assert.Contains("switched off", GraphicsSettings.DescribeRendering(2, false));
         Assert.Contains("switched off", GraphicsSettings.DescribeRendering(0, false));
      }

      [Fact]
      public void DescribeRendering_AllFourAnswersAreDifferent() {
         var answers = new[] { GraphicsSettings.DescribeRendering(2, true), GraphicsSettings.DescribeRendering(1, true), GraphicsSettings.DescribeRendering(0, true), GraphicsSettings.DescribeRendering(2, false) };
         Assert.Equal(4, answers.Distinct().Count());
         Assert.All(answers, answer => Assert.DoesNotContain(Environment.NewLine, answer));
      }

      [Theory]
      [InlineData(2, true, false)]
      [InlineData(1, true, false)]
      [InlineData(0, true, true)]
      [InlineData(2, false, true)]
      [InlineData(0, false, true)]
      public void IsSoftwareOnly(int tier, bool hardwareRequested, bool expected) {
         Assert.Equal(expected, GraphicsSettings.IsSoftwareOnly(tier, hardwareRequested));
      }

      #endregion

      #region The editor

      [Fact]
      public void Editor_BothSettingsStartOn() {
         var editor = CreateEditor();
         Assert.True(editor.UseDedicatedGraphics);
         Assert.True(editor.UseHardwareAcceleration);
         Assert.Equal(string.Empty, editor.GraphicsStatus);
      }

      [Fact]
      public void Editor_LoadsBothSettings() {
         var editor = CreateEditor("[GeneralSettings]", "UseDedicatedGraphics = False", "UseHardwareAcceleration = True");
         Assert.False(editor.UseDedicatedGraphics);
         Assert.True(editor.UseHardwareAcceleration);

         editor = CreateEditor("[GeneralSettings]", "UseDedicatedGraphics = True", "UseHardwareAcceleration = False");
         Assert.True(editor.UseDedicatedGraphics);
         Assert.False(editor.UseHardwareAcceleration);
      }

      [Fact]
      public void Editor_LoadingASetting_SaysNothingToTheUser() {
         var editor = CreateEditor("[GeneralSettings]", "UseDedicatedGraphics = False", "UseHardwareAcceleration = False");
         Assert.False(editor.ShowMessage);
         Assert.True(string.IsNullOrEmpty(editor.InformationMessage));
      }

      [Fact]
      public void Editor_SettingsFromAnOlderVersion_KeepTheDefaults() {
         var editor = CreateEditor("[GeneralSettings]", "AnimateScroll = False", "CollisionGridOpacity = 25");
         Assert.True(editor.UseDedicatedGraphics);
         Assert.True(editor.UseHardwareAcceleration);
      }

      [Fact]
      public void Editor_SwitchingOffTheGraphicsCard_NotifiesOnceAndSaysARestartIsNeeded() {
         var editor = CreateEditor();
         var notifications = new List<string>();
         editor.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         editor.UseDedicatedGraphics = false;

         Assert.False(editor.UseDedicatedGraphics);
         Assert.Equal(1, notifications.Count(name => name == nameof(EditorViewModel.UseDedicatedGraphics)));
         Assert.True(editor.ShowMessage);
         Assert.Equal(GraphicsSettings.RestartNoteForGraphicsCard, editor.InformationMessage);
         Assert.Contains("CUBHMA", editor.InformationMessage);
         Assert.Contains("again", editor.InformationMessage);
         Assert.True(editor.UseHardwareAcceleration, "the other setting is not touched");
      }

      [Fact]
      public void Editor_SwitchingOffHardwareAcceleration_NotifiesOnceAndSaysARestartIsNeeded() {
         var editor = CreateEditor();
         var notifications = new List<string>();
         editor.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         editor.UseHardwareAcceleration = false;

         Assert.False(editor.UseHardwareAcceleration);
         Assert.Equal(1, notifications.Count(name => name == nameof(EditorViewModel.UseHardwareAcceleration)));
         Assert.True(editor.ShowMessage);
         Assert.Equal(GraphicsSettings.RestartNoteForRendering, editor.InformationMessage);
         Assert.True(editor.UseDedicatedGraphics, "the other setting is not touched");
      }

      [Fact]
      public void Editor_SettingTheSameValue_DoesNothing() {
         var editor = CreateEditor();
         var notifications = new List<string>();
         editor.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         editor.UseDedicatedGraphics = true;
         editor.UseHardwareAcceleration = true;

         Assert.DoesNotContain(nameof(EditorViewModel.UseDedicatedGraphics), notifications);
         Assert.DoesNotContain(nameof(EditorViewModel.UseHardwareAcceleration), notifications);
         Assert.False(editor.ShowMessage);
      }

      [Fact]
      public void Editor_GraphicsStatus_NotifiesAndNeverIsNull() {
         var editor = CreateEditor();
         var notifications = new List<string>();
         editor.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         editor.GraphicsStatus = "Drawing with the graphics card.";
         Assert.Equal("Drawing with the graphics card.", editor.GraphicsStatus);
         Assert.Contains(nameof(EditorViewModel.GraphicsStatus), notifications);

         editor.GraphicsStatus = null;
         Assert.Equal(string.Empty, editor.GraphicsStatus);
      }

      [Fact]
      public void Editor_SavesBothSettings_NextToTheOtherViewOptions() {
         string[] saved = null;
         var editor = new EditorViewModel(new StubFileSystem { SaveMetadata = (name, lines) => { saved = lines; return true; } });

         editor.WriteAppLevelMetadata();

         Assert.Contains("UseDedicatedGraphics = True", saved);
         Assert.Contains("UseHardwareAcceleration = True", saved);
         var header = Array.IndexOf(saved, "[GeneralSettings]");
         Assert.True(Array.IndexOf(saved, "UseDedicatedGraphics = True") > header);
         Assert.True(Array.IndexOf(saved, "UseHardwareAcceleration = True") > header);
      }

      [Fact]
      public void Editor_SavedSettings_SurviveARestart() {
         string[] saved = null;
         var firstRun = new EditorViewModel(new StubFileSystem { SaveMetadata = (name, lines) => { saved = lines; return true; } });
         firstRun.UseDedicatedGraphics = false;
         firstRun.UseHardwareAcceleration = false;
         firstRun.WriteAppLevelMetadata();
         Assert.Contains("UseDedicatedGraphics = False", saved);
         Assert.Contains("UseHardwareAcceleration = False", saved);

         var secondRun = CreateEditor(saved);

         Assert.False(secondRun.UseDedicatedGraphics);
         Assert.False(secondRun.UseHardwareAcceleration);

         // and back on again
         firstRun.UseDedicatedGraphics = true;
         firstRun.UseHardwareAcceleration = true;
         firstRun.WriteAppLevelMetadata();
         var thirdRun = CreateEditor(saved);
         Assert.True(thirdRun.UseDedicatedGraphics);
         Assert.True(thirdRun.UseHardwareAcceleration);
      }

      #endregion
   }
}
