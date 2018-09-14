using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.IO;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("Gen3Hex")]

namespace HavenSoft.Gen3Hex.View {
   public partial class App {
      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);
         var viewPort = GetViewPort(e.Args);
         MainWindow = new MainWindow(viewPort);
         MainWindow.Show();
      }

      private ViewPort GetViewPort(string[] args) {
         if (args?.Length != 1) return new ViewPort();
         var fileName = args[0];
         if (!File.Exists(fileName)) return new ViewPort();

         var bytes = File.ReadAllBytes(fileName);
         var loadedFile = new LoadedFile(fileName, bytes);
         return new ViewPort(loadedFile);
      }
   }
}
