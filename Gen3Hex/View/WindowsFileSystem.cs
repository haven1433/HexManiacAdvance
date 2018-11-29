using HavenSoft.Gen3Hex.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace HavenSoft.Gen3Hex.View {
   public class WindowsFileSystem : IFileSystem {
      private readonly Dictionary<string, List<FileSystemWatcher>> watchers = new Dictionary<string, List<FileSystemWatcher>>();

      public string CopyText {
         get => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
         set => Clipboard.SetText(value);
      }

      public LoadedFile OpenFile(params string[] extensionOptions) {
         var dialog = new OpenFileDialog { Filter = CreateFilterFromOptions(extensionOptions) };
         var result = dialog.ShowDialog();
         if (result != true) return null;
         return LoadFile(dialog.FileName);
      }

      public LoadedFile LoadFile(string fileName) {
         if (!File.Exists(fileName)) return null;
         var data = File.ReadAllBytes(fileName);
         return new LoadedFile(fileName, data);
      }

      public void AddListenerToFile(string fileName, Action<IFileSystem> listener) {
         var watcher = new FileSystemWatcher(Path.GetDirectoryName(fileName)) {
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName,
         };
         watcher.Changed += (sender, e) => {
            if (e.FullPath.EndsWith(fileName)) listener(this);
         };
         watcher.EnableRaisingEvents = true;

         if (!watchers.ContainsKey(fileName)) watchers[fileName] = new List<FileSystemWatcher>();
         watchers[fileName].Add(watcher);
      }

      public void RemoveAllListenersForFile(string fileName) {
         if (!watchers.ContainsKey(fileName)) return;
         watchers[fileName].Clear();
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
