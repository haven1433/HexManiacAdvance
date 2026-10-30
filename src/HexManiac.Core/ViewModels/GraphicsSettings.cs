using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>The choices Windows offers for which graphics card a program uses (Settings > System > Display > Graphics).</summary>
   public enum GpuPreference {
      /// <summary>Let Windows decide.</summary>
      WindowsDecides = 0,
      /// <summary>The power saving graphics card (usually the one built into the processor).</summary>
      PowerSaving = 1,
      /// <summary>The high performance graphics card (usually the dedicated one).</summary>
      HighPerformance = 2,
   }

   /// <summary>What to do with the program's entry in the Windows registry.</summary>
   public enum GpuRegistryAction {
      /// <summary>The registry already says what it should: don't touch it.</summary>
      LeaveAlone,
      /// <summary>Write <see cref="GpuRegistryPlan.Data"/> as the value.</summary>
      Write,
      /// <summary>Remove the value: nothing is left in it that is worth keeping.</summary>
      Delete,
   }

   /// <summary>The answer of <see cref="GraphicsSettings.PlanGpuPreference"/>.</summary>
   public readonly struct GpuRegistryPlan {
      public GpuRegistryAction Action { get; }

      /// <summary>The text to write for <see cref="GpuRegistryAction.Write"/>. Empty for the other actions.</summary>
      public string Data { get; }

      public GpuRegistryPlan(GpuRegistryAction action, string data = "") => (Action, Data) = (action, data ?? string.Empty);

      public static GpuRegistryPlan LeaveAlone => new GpuRegistryPlan(GpuRegistryAction.LeaveAlone);
      public static GpuRegistryPlan Write(string data) => new GpuRegistryPlan(GpuRegistryAction.Write, data);
      public static GpuRegistryPlan Delete => new GpuRegistryPlan(GpuRegistryAction.Delete);
   }

   /// <summary>
   /// The decisions behind the two graphics options of the View menu, kept free of Windows code so they can be tested.
   /// <para>
   /// "Use dedicated graphics card": on a laptop with two graphics cards, Windows decides per program which one draws its windows (usually the weak one built into the processor).
   /// Windows keeps that choice in the registry, under HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences, in a value named like the full path of the program
   /// whose data looks like "GpuPreference=2;" (2 is "high performance"). It reads the value when the program starts, so a change applies the next time the program is started.
   /// The WPF layer writes the registry. This class works out what to write.
   /// </para>
   /// <para>
   /// "Hardware accelerated rendering": when it is off, the WPF layer makes the program draw everything with the processor (RenderMode.SoftwareOnly) for computers whose graphics driver has problems.
   /// </para>
   /// </summary>
   public static class GraphicsSettings {
      /// <summary>The name of the setting in the application settings file (HexManiacAdvance.toml, section [GeneralSettings]). On unless the file says "UseDedicatedGraphics = False".</summary>
      public const string UseDedicatedGraphicsSetting = "UseDedicatedGraphics";

      /// <summary>The name of the setting in the application settings file. On unless the file says "UseHardwareAcceleration = False".</summary>
      public const string UseHardwareAccelerationSetting = "UseHardwareAcceleration";

      /// <summary>The registry key (under HKEY_CURRENT_USER) where Windows keeps the graphics card chosen for each program.</summary>
      public const string GpuPreferenceRegistryKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

      /// <summary>The name of the entry inside the registry value's data.</summary>
      public const string GpuPreferenceEntry = "GpuPreference";

      /// <summary>
      /// WPF draws animations (storyboards, anything with a clock) at 60 frames per second unless it is told otherwise. This is what it is told instead: far above any display,
      /// so the display's own refresh rate is the only limit. (WPF cannot show more frames than the display refreshes.)
      /// </summary>
      public const int UncappedFrameRate = 1000;

      public const string RestartNoteForGraphicsCard = "The graphics card setting is used the next time CUBHMA starts: close CUBHMA and open it again.";

      public const string RestartNoteForRendering = "The rendering setting is used the next time CUBHMA starts: close CUBHMA and open it again.";

      #region Reading and writing the settings file

      /// <summary>The line the settings file holds for a setting, for example "UseDedicatedGraphics = True".</summary>
      public static string SettingLine(string name, bool value) => $"{name} = {value}";

      /// <summary>
      /// Reads one of the on/off settings from the lines of the settings file (they all start on).
      /// A missing file (null), a missing line or a line that doesn't say False all mean on, like the other settings of the View menu.
      /// </summary>
      public static bool IsEnabled(IEnumerable<string> settingsLines, string name) {
         if (settingsLines == null) return true;
         var off = SettingLine(name, false);
         return !settingsLines.Any(line => line != null && string.Equals(line.Trim(), off, StringComparison.OrdinalIgnoreCase));
      }

      #endregion

      #region The graphics card entry of the registry

      /// <summary>The text Windows keeps for a graphics card choice: "GpuPreference=2;".</summary>
      public static string GpuPreferenceData(GpuPreference preference) => $"{GpuPreferenceEntry}={(int)preference};";

      /// <summary>
      /// The registry data after the graphics card choice was changed. The other entries in the data (Windows 11 keeps for example "SwapEffectUpgradeEnable=1;" in the same value) stay exactly as they were.
      /// Pass null to remove the choice.
      /// </summary>
      public static string WithGpuPreference(string existingData, GpuPreference? preference) {
         var entries = SplitEntries(existingData);
         var result = new List<string>();
         var written = false;
         foreach (var entry in entries) {
            if (!IsGpuPreferenceEntry(entry)) {
               result.Add(entry);
            } else if (preference != null && !written) {
               result.Add($"{GpuPreferenceEntry}={(int)preference.Value}");
               written = true;
            } // anything else is a repeat of the choice, or the choice we are removing
         }
         if (preference != null && !written) result.Add($"{GpuPreferenceEntry}={(int)preference.Value}");
         return string.Concat(result.Select(entry => entry + ";"));
      }

      /// <summary>The graphics card choice in the registry data, or null if the data doesn't make one (or makes one that isn't 0, 1 or 2).</summary>
      public static GpuPreference? GpuPreferenceOf(string data) {
         foreach (var entry in SplitEntries(data)) {
            if (!IsGpuPreferenceEntry(entry)) continue;
            var value = entry.Substring(entry.IndexOf('=') + 1).Trim();
            if (int.TryParse(value, out var number) && Enum.IsDefined(typeof(GpuPreference), number)) return (GpuPreference)number;
         }
         return null;
      }

      private static List<string> SplitEntries(string data) {
         if (string.IsNullOrWhiteSpace(data)) return new List<string>();
         return data.Split(';').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
      }

      private static bool IsGpuPreferenceEntry(string entry) {
         var equals = entry.IndexOf('=');
         return equals > 0 && string.Equals(entry.Substring(0, equals).Trim(), GpuPreferenceEntry, StringComparison.OrdinalIgnoreCase);
      }

      /// <summary>
      /// What to do with the registry value of this program for the "Use dedicated graphics card" setting.
      /// <list type="bullet">
      /// <item>On: make sure the value asks for the high performance card (keeping its other entries). Done every time the program starts, so a value that was lost or changed comes back.</item>
      /// <item>Off, switched off just now in the menu: take back the high performance choice (Windows decides again). A different choice (power saving) is somebody else's, and stays.</item>
      /// <item>Off, as it was when the program started: don't touch the registry. That's where somebody who picked a card for this program in the Windows settings is left alone.</item>
      /// </list>
      /// </summary>
      /// <param name="useDedicatedGraphics">The setting.</param>
      /// <param name="switchedOffJustNow">True only when this is the answer to the user switching the setting off in the menu.</param>
      /// <param name="existingData">The data of the registry value now: null or empty if there is no value.</param>
      public static GpuRegistryPlan PlanGpuPreference(bool useDedicatedGraphics, bool switchedOffJustNow, string existingData) {
         existingData ??= string.Empty;
         if (useDedicatedGraphics) {
            var wanted = WithGpuPreference(existingData, GpuPreference.HighPerformance);
            return wanted == existingData ? GpuRegistryPlan.LeaveAlone : GpuRegistryPlan.Write(wanted);
         }

         if (!switchedOffJustNow) return GpuRegistryPlan.LeaveAlone;
         if (GpuPreferenceOf(existingData) != GpuPreference.HighPerformance) return GpuRegistryPlan.LeaveAlone;
         var remaining = WithGpuPreference(existingData, null);
         return remaining.Length == 0 ? GpuRegistryPlan.Delete : GpuRegistryPlan.Write(remaining);
      }

      /// <summary>
      /// The name of the registry value for a program: its full path, which is how Windows keys the choice.
      /// Returns null if the program is not one this should be written for: not a .exe, or the generic "dotnet" launcher
      /// (then every .NET program on the computer would get the setting, because they all run as dotnet.exe).
      /// </summary>
      public static string RegistryValueNameFor(string programPath) {
         if (string.IsNullOrWhiteSpace(programPath)) return null;
         string fullPath;
         try {
            fullPath = Path.GetFullPath(programPath);
         } catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) {
            return null;
         }
         if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase)) return null;
         var name = Path.GetFileNameWithoutExtension(fullPath);
         if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase)) return null;
         return fullPath;
      }

      #endregion

      #region What the user is told

      /// <summary>
      /// The line in the View > Graphics menu that says how the program is drawing.
      /// </summary>
      /// <param name="renderTier">WPF's rendering tier: 0 nothing is accelerated by the graphics card, 1 some things are, 2 most things are (RenderCapability.Tier >> 16).</param>
      /// <param name="hardwareAccelerationRequested">The "Hardware accelerated rendering" setting as it was when the program started.</param>
      public static string DescribeRendering(int renderTier, bool hardwareAccelerationRequested) {
         if (!hardwareAccelerationRequested) {
            return "Hardware acceleration is switched off: everything is drawn by the processor, so the map editor is slower.";
         }
         if (renderTier >= 2) return "Drawing with the graphics card (render tier 2: full hardware acceleration).";
         if (renderTier == 1) return "Drawing with the graphics card (render tier 1: partial hardware acceleration).";
         return "Windows says the graphics card does not help with drawing here (render tier 0): everything is drawn by the processor, so the map editor is slower. Updating the graphics driver may help.";
      }

      /// <summary>True if the program is drawing with the processor only, whatever the reason.</summary>
      public static bool IsSoftwareOnly(int renderTier, bool hardwareAccelerationRequested) => !hardwareAccelerationRequested || renderTier <= 0;

      #endregion
   }
}
