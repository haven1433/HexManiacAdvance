using HavenSoft.Gen3Hex.Model;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;

namespace HavenSoft.Gen3Hex.View {
   public class WindowsFileSystem : IFileSystem {
      public LoadedFile OpenFile(params string[] extensionOptions) {
         var dialog = new OpenFileDialog { Filter = CreateFilterFromOptions(extensionOptions) };
         var result = dialog.ShowDialog();
         if (result != true) return null;
         if (!File.Exists(dialog.FileName)) return null;
         var data = File.ReadAllBytes(dialog.FileName);
         return new LoadedFile(dialog.FileName, data);
      }

      public string RequestNewName(string currentName, params string[] extensionOptions) {
         var dialog = new SaveFileDialog { FileName = currentName, Filter = CreateFilterFromOptions(extensionOptions) };
         var result = dialog.ShowDialog();
         if (result != true) return null;
         return dialog.FileName;
      }

      public bool Save(LoadedFile file) {
         File.WriteAllBytes(file.Name, file.Contents);
         return true;
      }

      public bool? TrySavePrompt(LoadedFile file) {
         var result = MessageBox.Show($"Would you like to save {file.Name}?", Application.Current.MainWindow.Title, MessageBoxButton.YesNoCancel);
         if (result == MessageBoxResult.Cancel) return null;
         if (result == MessageBoxResult.No) return false;
         return Save(file);
      }

      private static string CreateFilterFromOptions(string[] extensionOptions) {
         return string.Join(",", extensionOptions.Select(option => $"*.{option}"));
      }
   }
}
