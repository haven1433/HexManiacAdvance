using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Implementations;
using System.IO;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("HexManiacAdvance")]

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class App {
      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);
         UpdateThemeDictionary();
         Solarized.Theme.VariantChanged += (sender, args) => UpdateThemeDictionary();

         var fileName = e.Args?.Length == 1 ? e.Args[0] : string.Empty;
         var fileSystem = new WindowsFileSystem(Dispatcher);
         var viewModel = GetViewModel(fileName, fileSystem);
         MainWindow = new MainWindow(viewModel);
         MainWindow.Resources.Add("FileSystem", fileSystem);
         MainWindow.Show();
      }

      private void UpdateThemeDictionary() {
         var dict = new ResourceDictionary {
            { "Emphasis", Solarized.Theme.Emphasis },
            { "Primary", Solarized.Theme.Primary },
            { "Secondary", Solarized.Theme.Secondary },
            { "Background", Solarized.Theme.Background },
            { "Backlight", Solarized.Theme.Backlight },
         };
         Resources.MergedDictionaries.Clear();
         Resources.MergedDictionaries.Add(dict);
         FormatDrawer.ClearVisualCaches();
      }

      private EditorViewModel GetViewModel(string fileName, IFileSystem fileSystem) {
         var editor = new EditorViewModel(fileSystem);
         if (!File.Exists(fileName)) return editor;

         var loadedFile = fileSystem.LoadFile(fileName);
         var metadata = fileSystem.MetadataFor(fileName);
         var model = new AutoSearchModel(loadedFile.Contents, metadata);
         editor.Add(new ViewPort(loadedFile.Name, model));
         return editor;
      }
   }
}
