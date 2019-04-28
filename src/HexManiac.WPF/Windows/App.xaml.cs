using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Implementations;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

[assembly: AssemblyTitle("HexManiacAdvance")]

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class App {
      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);

         var fileName = e.Args?.Length == 1 ? e.Args[0] : string.Empty;
         var fileSystem = new WindowsFileSystem(Dispatcher);
         var viewModel = GetViewModel(fileName, fileSystem);
         UpdateThemeDictionary(viewModel);
         viewModel.Theme.PropertyChanged += (sender, args) => UpdateThemeDictionary(viewModel);
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
         };
         Resources.MergedDictionaries.Clear();
         Resources.MergedDictionaries.Add(dict);
         FormatDrawer.ClearVisualCaches();
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

      private EditorViewModel GetViewModel(string fileName, IFileSystem fileSystem) {
         var editor = new EditorViewModel(fileSystem);
         if (!File.Exists(fileName)) return editor;

         var loadedFile = fileSystem.LoadFile(fileName);
         var metadataLines = fileSystem.MetadataFor(fileName);
         var metadata = metadataLines != null ? new StoredMetadata(metadataLines) : null;
         var model = new AutoSearchModel(loadedFile.Contents, metadata);
         editor.Add(new ViewPort(loadedFile.Name, model));
         return editor;
      }
   }
}
