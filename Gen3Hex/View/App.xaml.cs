using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("Gen3Hex")]

namespace HavenSoft.Gen3Hex.View {
   public partial class App {
      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);
         UpdateThemeDictionary();
         Solarized.Theme.VariantChanged += (sender, args) => UpdateThemeDictionary();

         var fileName = e.Args?.Length == 1 ? e.Args[0] : string.Empty;
         var viewPort = GetViewModel(fileName);
         MainWindow = new MainWindow(viewPort);
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
      }

      private EditorViewModel GetViewModel(string fileName) {
         var editor = new EditorViewModel(new WindowsFileSystem());
         if (!File.Exists(fileName)) return editor;

         var bytes = File.ReadAllBytes(fileName);
         var loadedFile = new LoadedFile(fileName, bytes);
         editor.Add(new ViewPort(loadedFile));
         return editor;
      }
   }
}
