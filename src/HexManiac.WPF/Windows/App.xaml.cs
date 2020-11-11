using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Implementations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

[assembly: AssemblyTitle("HexManiacAdvance")]

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class App {
      public const string ReleaseUrl = "https://github.com/haven1433/HexManiacAdvance/releases";
      public const string
         Arg_No_Metadata = "--no-metadata",
         Arg_Developer_Menu = "--dev-menu";

      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);

         DebugLog("------");
         var (path, address, options) = ParseArgs(e.Args);
         DebugLog(e.Args);

         var fileSystem = new WindowsFileSystem(Dispatcher);
         var viewModel = GetViewModel(path, address, fileSystem, options);
         DebugLog("Have Editor");
         UpdateThemeDictionary(viewModel);
         viewModel.Theme.PropertyChanged += (sender, _) => UpdateThemeDictionary(viewModel);
         MainWindow = new MainWindow(viewModel);
         MainWindow.Resources.Add("FileSystem", fileSystem);
         MainWindow.Resources.Add("PaletteMixer", new PaletteCollection().Fluent(mixer => mixer.SetContents(new short[16])));
         MainWindow.Resources.Add("IsPaletteMixerExpanded", new EditableValue<bool>());
         MainWindow.Show();
         DebugLog("All Started!");
      }

      private static (string path, int address, bool[] options) ParseArgs(string[] args) {
         var useMetadata = true;
         var showDevMenu = false;
         if (args.Any(arg => arg == Arg_No_Metadata)) {
            useMetadata = false;
            args = args.Where(arg => arg != Arg_No_Metadata).ToArray();
         }
         if (args.Any(arg => arg == Arg_Developer_Menu)) {
            showDevMenu = true;
            args = args.Where(arg => arg != Arg_Developer_Menu).ToArray();
         }

         var allArgs = args.Aggregate(string.Empty, (a, b) => a + ' ' + b).Trim();
         var loadAddress = -1;
         if (allArgs.Contains(":") && allArgs.LastIndexOf(":") > 4) {
            var parts = allArgs.Split(':');
            if (!int.TryParse(parts.Last(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out loadAddress)) loadAddress = -1;
            allArgs = parts.Take(parts.Length - 1).Aggregate((a, b) => a + ":" + b).Trim();
         } else if (allArgs.ToLower().Contains(".gba ")) {
            var parts = allArgs.Split(" ");
            if (!int.TryParse(parts.Last(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out loadAddress)) loadAddress = -1;
            allArgs = parts.Take(parts.Length - 1).Aggregate(string.Empty, (a, b) => a + " " + b).Trim();
         }

         return (allArgs, loadAddress, new[] { useMetadata, showDevMenu });
      }

      /// <summary>
      /// Generally, the initial working directory is set to wherever the program was launched from.
      /// In the case of command line usage or dropping a file onto the EXE, that's not the EXE's location.
      /// We want the initial loading directory to match the EXE's path so we can find the reference files.
      /// Example: armReference.txt, scriptReference.txt
      /// </summary>
      private static void SetInitialWorkingDirectory() {
         var mainAssemblyLocation = Assembly.GetExecutingAssembly().Location;
         var workingDirectory = Path.GetDirectoryName(mainAssemblyLocation);
         Directory.SetCurrentDirectory(workingDirectory);
      }

      private void UpdateThemeDictionary(EditorViewModel viewModel) {
         if (Resources.MergedDictionaries.Count == 0) Resources.MergedDictionaries.Add(new ResourceDictionary());

         var sources = new List<string> {
            nameof(viewModel.Theme.Primary),
            nameof(viewModel.Theme.Secondary),
            nameof(viewModel.Theme.Background),
            nameof(viewModel.Theme.Backlight),
            nameof(viewModel.Theme.Error),
            nameof(viewModel.Theme.Text1),
            nameof(viewModel.Theme.Text2),
            nameof(viewModel.Theme.Data1),
            nameof(viewModel.Theme.Data2),
            nameof(viewModel.Theme.Accent),
            nameof(viewModel.Theme.Stream1),
            nameof(viewModel.Theme.Stream2),
            nameof(viewModel.Theme.EditBackground),
         };

         var dict = new ResourceDictionary();
         var theme = viewModel.Theme.GetType();
         sources.ForEach(source => {
            var rawValue = (string)theme.GetProperty(source).GetValue(viewModel.Theme);
            dict.Add(source, Brush(rawValue));
            dict.Add(source + "Color", ColorConverter.ConvertFromString(rawValue));
         });

         Resources.MergedDictionaries[0] = dict;
      }

      private static SolidColorBrush Brush(string text) {
         try {
            var color = (Color)ColorConverter.ConvertFromString(text);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
         } catch {
            return null;
         }
      }

      private static EditorViewModel GetViewModel(string fileName, int address, WindowsFileSystem fileSystem, bool[] options) {
         bool useMetadata = options[0];
         bool showDevMenu = options[1];
         if (fileName != string.Empty) fileName = Path.GetFullPath(fileName);
         SetInitialWorkingDirectory();
         DebugLog(fileName);
         var editor = new EditorViewModel(fileSystem, fileSystem, allowLoadingMetadata: useMetadata) { ShowDeveloperMenu = showDevMenu };
         CheckIsNewerVersionAvailable(editor);
         if (!File.Exists(fileName)) return editor;
         DebugLog("File Exists");
         var loadedFile = fileSystem.LoadFile(fileName);
         editor.Open.Execute(loadedFile);
         DebugLog("Tab Added");
         var tab = editor[editor.SelectedIndex] as ViewPort;
         if (tab != null && address >= 0) {
            tab.CascadeScript(address);
            editor.GotoViewModel.ControlVisible = false;
         }
         return editor;
      }

      private static void DebugLog(params string[] text) {
         // File.AppendAllLines("debug.txt", text);
      }

      private static void CheckIsNewerVersionAvailable(EditorViewModel viewModel) {
         if (DateTime.Now < viewModel.LastUpdateCheck + TimeSpan.FromDays(1)) return;
         viewModel.LastUpdateCheck = DateTime.Now;
         try {
            using (var client = new WebClient()) {
               string content = client.DownloadString(ReleaseUrl);
               var mostRecentVersion = content
                  .Split('\n')
                  .Where(line => line.Contains("/haven1433/HexManiacAdvance/tree/"))
                  .Select(line => line.Split("title=").Last().Split('"')[1])
                  .First();
               viewModel.IsNewVersionAvailable = StoredMetadata.NeedVersionUpdate(viewModel.Singletons.MetadataInfo.VersionNumber, mostRecentVersion);
            }
         } catch {
            // Exceptions are expected on Windows 7.
            // If anything goes wrong, we probably don't care. It just means that the IsNewVersionAvailable will be false.
         }
      }
   }
}
