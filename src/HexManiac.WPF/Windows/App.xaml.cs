using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Implementations;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

[assembly: AssemblyTitle("HexManiacAdvance")]

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class App {
      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);

         var (path, address, useMetadata) = ParseArgs(e.Args);

         var fileSystem = new WindowsFileSystem(Dispatcher);
         var viewModel = GetViewModel(path, address, fileSystem, useMetadata);
         UpdateThemeDictionary(viewModel);
         viewModel.Theme.PropertyChanged += (sender, _) => UpdateThemeDictionary(viewModel);
         MainWindow = new MainWindow(viewModel);
         MainWindow.Resources.Add("FileSystem", fileSystem);
         MainWindow.Show();
      }

      private static (string path, int address, bool useMetadata) ParseArgs(string[] args) {
         var useMetadata = true;
         if (args.Any(arg => arg == "--no-metadata")) {
            useMetadata = false;
            args = args.Where(arg => arg != "--no-metadata").ToArray();
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

         return (allArgs, loadAddress, useMetadata);
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

      private static EditorViewModel GetViewModel(string fileName, int address, IFileSystem fileSystem, bool useMetadata) {
         if (fileName != string.Empty) fileName = Path.GetFullPath(fileName);
         SetInitialWorkingDirectory();
         var editor = new EditorViewModel(fileSystem, useMetadata);
         if (!File.Exists(fileName)) return editor;
         var loadedFile = fileSystem.LoadFile(fileName);
         editor.Open.Execute(loadedFile);
         var tab = editor[editor.SelectedIndex] as ViewPort;
         if (tab != null && address >= 0) tab.CascadeScript(address);
         return editor;
      }
   }
}
