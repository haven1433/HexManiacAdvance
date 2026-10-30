using HavenSoft.HexManiac.Core.ViewModels;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HavenSoft.HexManiac.WPF.Implementations {
   /// <summary>
   /// Everything CUBHMA does about graphics before and just after its first window: the frame rate limit, the render mode, and the graphics card Windows runs the program on.
   /// What each setting means is decided in Core (<see cref="GraphicsSettings"/>, tested); this is the thin layer that talks to WPF and to the registry.
   /// Nothing here may stop the program from starting: every call is guarded, and a failure just means the setting has no effect.
   /// </summary>
   public static class GraphicsStartup {
      private static bool hardwareAccelerationAtStartup = true;

      /// <summary>
      /// First thing in Main, before WPF creates anything: takes the 60 frames per second limit off WPF's animations,
      /// and writes the graphics card choice for this program. (Windows reads the choice when a program starts, so the earlier it is written the better its chance to count for this very start.)
      /// </summary>
      public static void Prepare() {
         RemoveFrameRateLimit();
         var settings = ReadSettingsFile();
         hardwareAccelerationAtStartup = GraphicsSettings.IsEnabled(settings, GraphicsSettings.UseHardwareAccelerationSetting);
         WriteGpuPreference(GraphicsSettings.IsEnabled(settings, GraphicsSettings.UseDedicatedGraphicsSetting), switchedOffJustNow: false);
      }

      /// <summary>
      /// WPF draws animations at 60 frames per second unless told otherwise (Timeline.DesiredFrameRate has no value, and that means 60).
      /// This changes the default, for every animation made after this call, to a number far above any display.
      /// The map zoom does not use this (it moves on every frame WPF renders), but the other animations of the program do.
      /// WPF can never show more frames than the display refreshes: with this, the display is the only limit.
      /// </summary>
      public static void RemoveFrameRateLimit() {
         try {
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = GraphicsSettings.UncappedFrameRate });
         } catch (Exception ex) {
            Debug.WriteLine($"Could not remove the animation frame rate limit: {ex}");
         }
      }

      /// <summary>
      /// First thing in Application.OnStartup, before any window exists: when "Hardware accelerated rendering" is off, everything the program draws is drawn by the processor.
      /// (The setting is read from the settings file in <see cref="Prepare"/>, so the editor does not have to exist yet.)
      /// </summary>
      public static void ApplyRenderMode() {
         if (hardwareAccelerationAtStartup) return;
         try {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
         } catch (Exception ex) {
            Debug.WriteLine($"Could not switch to software rendering: {ex}");
         }
      }

      /// <summary>
      /// Called from Application.OnStartup once the editor exists: keeps the graphics line of the View > Graphics menu up to date
      /// and keeps the Windows graphics card choice up to date when the menu item is clicked.
      /// </summary>
      public static void Connect(EditorViewModel viewModel) {
         try {
            RenderCapability.TierChanged += (sender, e) => UpdateStatus(viewModel);
         } catch (Exception ex) {
            Debug.WriteLine($"Could not watch the rendering tier: {ex}");
         }

         viewModel.PropertyChanged += (sender, e) => {
            if (e.PropertyName != nameof(EditorViewModel.UseDedicatedGraphics)) return;
            // the registry follows the menu item right away. The new choice counts from the next start.
            WriteGpuPreference(viewModel.UseDedicatedGraphics, switchedOffJustNow: !viewModel.UseDedicatedGraphics);
         };
      }

      /// <summary>WPF's rendering tier: 0 nothing is accelerated by the graphics card, 1 some things are, 2 most things are. -1 if WPF can't say.</summary>
      public static int RenderTier {
         get {
            try {
               return RenderCapability.Tier >> 16;
            } catch (Exception ex) {
               Debug.WriteLine($"Could not read the rendering tier: {ex}");
               return -1;
            }
         }
      }

      /// <summary>Fills in the line of the View > Graphics menu that says how the program is drawing. Call it once the first window exists.</summary>
      public static void UpdateStatus(EditorViewModel viewModel) {
         var tier = RenderTier;
         if (tier < 0) return;
         viewModel.GraphicsStatus = GraphicsSettings.DescribeRendering(tier, hardwareAccelerationAtStartup);
      }

      private static string[] ReadSettingsFile() {
         try {
            // the same file the editor reads: HexManiacAdvance.toml, next to the program
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = string.IsNullOrEmpty(location) ? AppContext.BaseDirectory : Path.GetDirectoryName(location);
            var path = Path.Combine(directory, EditorViewModel.ApplicationName + ".toml");
            return File.Exists(path) ? File.ReadAllLines(path) : null;
         } catch (Exception ex) {
            Debug.WriteLine($"Could not read the graphics settings: {ex}");
            return null;
         }
      }

      /// <summary>
      /// Windows keeps the graphics card choice of each program in HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences:
      /// a value named like the full path of the program, with data like "GpuPreference=2;". This is what Settings > System > Display > Graphics edits.
      /// Core decides what to write (GraphicsSettings.PlanGpuPreference); this does it.
      /// </summary>
      public static void WriteGpuPreference(bool useDedicatedGraphics, bool switchedOffJustNow) {
         try {
            if (!OperatingSystem.IsWindows()) return;
            var program = GraphicsSettings.RegistryValueNameFor(Environment.ProcessPath);
            if (program == null) return; // not HexManiacAdvance.exe (for example the generic dotnet.exe): the choice would apply to every .NET program
            using (var key = Registry.CurrentUser.CreateSubKey(GraphicsSettings.GpuPreferenceRegistryKey)) {
               if (key == null) return;
               var existing = key.GetValue(program) as string;
               var plan = GraphicsSettings.PlanGpuPreference(useDedicatedGraphics, switchedOffJustNow, existing);
               if (plan.Action == GpuRegistryAction.Write) {
                  key.SetValue(program, plan.Data, RegistryValueKind.String);
               } else if (plan.Action == GpuRegistryAction.Delete) {
                  key.DeleteValue(program, throwOnMissingValue: false);
               }
            }
         } catch (Exception ex) {
            // locked down computers do not let the program write there: the setting just has no effect
            Debug.WriteLine($"Could not write the graphics card choice: {ex}");
         }
      }
   }
}
