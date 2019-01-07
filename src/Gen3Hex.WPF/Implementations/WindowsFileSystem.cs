using HavenSoft.Gen3Hex.Core.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace HavenSoft.Gen3Hex.WPF.Implementations {
   public class WindowsFileSystem : IFileSystem {
      private readonly Dictionary<string, List<FileSystemWatcher>> watchers = new Dictionary<string, List<FileSystemWatcher>>();
      private readonly Dictionary<string, List<Action<IFileSystem>>> listeners = new Dictionary<string, List<Action<IFileSystem>>>();

      private readonly Dispatcher dispatcher;

      public string CopyText {
         get => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
         set => Clipboard.SetText(value);
      }

      public WindowsFileSystem(Dispatcher uiDispatcher) => dispatcher = uiDispatcher;

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
            if (e.FullPath.EndsWith(fileName)) dispatcher.BeginInvoke(listener, this);
         };
         watcher.EnableRaisingEvents = true;

         if (!watchers.ContainsKey(fileName)) {
            watchers[fileName] = new List<FileSystemWatcher>();
            listeners[fileName] = new List<Action<IFileSystem>>();
         }

         watchers[fileName].Add(watcher);
         listeners[fileName].Add(listener);
      }

      public void RemoveListenerForFile(string fileName, Action<IFileSystem> listener) {
         if (!watchers.ContainsKey(fileName)) return;

         var index = listeners[fileName].IndexOf(listener);
         if (index == -1) return;

         var watcher = watchers[fileName][index];
         watcher.EnableRaisingEvents = false;
         watcher.Dispose();

         listeners[fileName].RemoveAt(index);
         watchers[fileName].RemoveAt(index);
      }

      public string RequestNewName(string currentName, params string[] extensionOptions) {
         var dialog = new SaveFileDialog { FileName = currentName, Filter = CreateFilterFromOptions(extensionOptions) };
         var result = dialog.ShowDialog();
         if (result != true) return null;
         return dialog.FileName;
      }

      public bool Save(LoadedFile file, StoredMetadata metadata) {
         // make sure the required directory exists
         var path = Path.GetDirectoryName(file.Name);
         Directory.CreateDirectory(path);
         File.WriteAllBytes(file.Name, file.Contents);
         var metadataName = Path.ChangeExtension(file.Name, ".toml");
         File.WriteAllLines(metadataName, metadata.Serialize());
         return true;
      }

      public bool? TrySavePrompt(LoadedFile file, StoredMetadata metadata) {
         var result = MessageBox.Show($"Would you like to save {file.Name}?", Application.Current.MainWindow.Title, MessageBoxButton.YesNoCancel);
         if (result == MessageBoxResult.Cancel) return null;
         if (result == MessageBoxResult.No) return false;
         return Save(file, metadata);
      }

      public StoredMetadata MetadataFor(string fileName) {
         var metadataName = Path.ChangeExtension(fileName, ".toml");
         if (!File.Exists(metadataName)) return null;
         var lines = File.ReadAllLines(metadataName);
         return new StoredMetadata(lines);
      }

      private static string CreateFilterFromOptions(string[] extensionOptions) {
         return string.Join(",", extensionOptions.Select(option => $"*.{option}"));
      }
   }
}
