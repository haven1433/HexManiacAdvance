using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Implementations;
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

         var args = e.Args;
         var useMetadata = true;
         if (args.Any(arg => arg == "--no-metadata")) {
            useMetadata = false;
            args = args.Where(arg => arg != "--no-metadata").ToArray();
         }
         var fileName = args?.Length == 1 ? args[0] : string.Empty;
         var fileSystem = new WindowsFileSystem(Dispatcher);
         var viewModel = GetViewModel(fileName, fileSystem, useMetadata);
         UpdateThemeDictionary(viewModel);
         viewModel.Theme.PropertyChanged += (sender, _) => UpdateThemeDictionary(viewModel);
         MainWindow = new MainWindow(viewModel);
         MainWindow.Resources.Add("FileSystem", fileSystem);
         MainWindow.Show();
      }

      private void UpdateThemeDictionary(EditorViewModel viewModel) {
         var dict = new ResourceDictionary {
            { nameof(viewModel.Theme.Primary), Brush(viewModel.Theme.Primary) },
            { nameof(viewModel.Theme.Secondary), Brush(viewModel.Theme.Secondary) },
            { nameof(viewModel.Theme.Background), Brush(viewModel.Theme.Background) },
            { nameof(viewModel.Theme.Backlight), Brush(viewModel.Theme.Backlight) },
            { nameof(viewModel.Theme.Error), Brush(viewModel.Theme.Error) },
            { nameof(viewModel.Theme.Text1), Brush(viewModel.Theme.Text1) },
            { nameof(viewModel.Theme.Text2), Brush(viewModel.Theme.Text2) },
            { nameof(viewModel.Theme.Data1), Brush(viewModel.Theme.Data1) },
            { nameof(viewModel.Theme.Data2), Brush(viewModel.Theme.Data2) },
            { nameof(viewModel.Theme.Accent), Brush(viewModel.Theme.Accent) },
            { nameof(viewModel.Theme.Stream1), Brush(viewModel.Theme.Stream1) },
            { nameof(viewModel.Theme.Stream2), Brush(viewModel.Theme.Stream2) },
            { nameof(viewModel.Theme.ErrorBackground), Brush(viewModel.Theme.ErrorBackground) },
         };
         Resources.MergedDictionaries.Clear();
         Resources.MergedDictionaries.Add(dict);
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

      private static EditorViewModel GetViewModel(string fileName, IFileSystem fileSystem, bool useMetadata) {
         var editor = new EditorViewModel(fileSystem, useMetadata);
         if (!File.Exists(fileName)) return editor;
         var loadedFile = fileSystem.LoadFile(fileName);
         editor.Open.Execute(loadedFile);
         return editor;
      }
   }
}
