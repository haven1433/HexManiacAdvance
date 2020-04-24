using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HavenSoft.HexManiac.WPF.Implementations {
   public class WindowsFileSystem : IFileSystem {
      private readonly Dictionary<string, List<FileSystemWatcher>> watchers = new Dictionary<string, List<FileSystemWatcher>>();
      private readonly Dictionary<string, List<Action<IFileSystem>>> listeners = new Dictionary<string, List<Action<IFileSystem>>>();

      private readonly Dispatcher dispatcher;

      public string CopyText {
         get => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
         set => Clipboard.SetText(value);
      }

      public WindowsFileSystem(Dispatcher uiDispatcher) => dispatcher = uiDispatcher;

      public LoadedFile OpenFile(string extensionDescription = null, params string[] extensionOptions) {
         var dialog = new OpenFileDialog { Filter = CreateFilterFromOptions(extensionDescription, extensionOptions) };
         var result = dialog.ShowDialog();
         if (result != true) return null;
         return LoadFile(dialog.FileName);
      }

      public LoadedFile LoadFile(string fileName) {
         if (!File.Exists(fileName)) return null;
         var output = new List<byte>();

         // use a buffered read with FileShare ReadWrite so we can open the file while another program is holding it.
         using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) {
            var buffer = new byte[0x100000];
            int readCount;
            do {
               readCount = stream.Read(buffer, 0, buffer.Length);
               output.AddRange(buffer.Take(readCount));
            } while (readCount == buffer.Length);
         }

         var data = output.ToArray();
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

      public string RequestNewName(string currentName, string extensionDescription = null, params string[] extensionOptions) {
         var dialog = new SaveFileDialog { FileName = currentName, Filter = CreateFilterFromOptions(extensionDescription, extensionOptions) };
         var result = dialog.ShowDialog();
         if (result != true) return null;
         return dialog.FileName;
      }

      public bool Save(LoadedFile file) {
         // make sure the required directory exists
         var path = Path.GetDirectoryName(file.Name);
         Directory.CreateDirectory(path);
         try {
            // use FileShare ReadWrite so we can write the file while another program is holding it.
            using (var stream = new FileStream(file.Name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)) {
               stream.Write(file.Contents, 0, file.Contents.Length);
            }
         } catch (IOException) {
            ShowCustomMessageBox("Could not save. The file might be ReadOnly or in use by another application.", showYesNoCancel: false);
            return false;
         }
         return true;
      }

      public bool SaveMetadata(string originalFileName, string[] metadata) {
         if (metadata == null) return true; // nothing to save
         var metadataName = Path.ChangeExtension(originalFileName, ".toml");
         File.WriteAllLines(metadataName, metadata);
         return true;
      }

      public bool? TrySavePrompt(LoadedFile file) {
         var result = ShowCustomMessageBox($"Would you like to save{Environment.NewLine}{file.Name}?");
         if (result == MessageBoxResult.Cancel) return null;
         if (result == MessageBoxResult.No) return false;
         return Save(file);
      }

      public MessageBoxResult ShowCustomMessageBox(string message, bool showYesNoCancel = true) {
         var choices = showYesNoCancel ? new StackPanel {
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal,
            Children = {
               new Button {
                  HorizontalContentAlignment = HorizontalAlignment.Center,
                  Content = new Label { Foreground = Brush(nameof(Theme.Primary)), Content = "_Yes" },
                  MinWidth = 70,
                  Margin = new Thickness(5)
               }.SetEvent(Button.ClickEvent, MessageBoxButtonClick),
               new Button {
                  HorizontalContentAlignment = HorizontalAlignment.Center,
                  Content = new Label { Foreground = Brush(nameof(Theme.Primary)), Content = "_No" },
                  MinWidth = 70,
                  Margin = new Thickness(5)
               }.SetEvent(Button.ClickEvent, MessageBoxButtonClick),
               new Button {
                  HorizontalContentAlignment = HorizontalAlignment.Center,
                  Content = new Label { Foreground = Brush(nameof(Theme.Primary)), Content = "Cancel" },
                  MinWidth = 70,
                  Margin = new Thickness(5)
               }.SetEvent(Button.ClickEvent, MessageBoxButtonClick),
            }
         } : new StackPanel {
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal,
            Children = {
               new Button {
                  HorizontalContentAlignment = HorizontalAlignment.Center,
                  Content = new Label { Foreground = Brush(nameof(Theme.Primary)), Content = "OK" },
                  MinWidth = 70,
                  Margin = new Thickness(5)
               }.SetEvent(Button.ClickEvent, MessageBoxButtonClick),
            }
         };

         var window = new Window {
            Background = Brush(nameof(Theme.Background)),
            Foreground = Brush(nameof(Theme.Primary)),
            Title = Application.Current.MainWindow.Title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.ToolWindow,
            Content = new StackPanel {
               Orientation = Orientation.Vertical,
               Children = {
                  new TextBlock { Text = message, Margin = new Thickness(5, 10, 5, 10) },
                  choices,
               }
            },
            Owner = Application.Current.MainWindow,
            Left = Application.Current.MainWindow.Left + Application.Current.MainWindow.Width / 2,
            Top = Application.Current.MainWindow.Top + Application.Current.MainWindow.Height / 2,
         }.SetEvent(UIElement.KeyDownEvent, MessageBoxKeyDown);

         passingResult = MessageBoxResult.Cancel;
         window.ShowDialog();
         return passingResult;
      }

      private MessageBoxResult passingResult = MessageBoxResult.Cancel;
      private void MessageBoxButtonClick(object sender, EventArgs e) {
         var button = (Button)sender;
         switch (((Label)button.Content).Content.ToString()) {
            case "_Yes": passingResult = MessageBoxResult.Yes; break;
            case "_No": passingResult = MessageBoxResult.No; break;
            case "Cancel": passingResult = MessageBoxResult.Cancel; break;
            case "OK": passingResult = MessageBoxResult.OK; break;
         }
         var parent = (FrameworkElement)button.Parent;
         while (parent.Parent is FrameworkElement) parent = (FrameworkElement)parent.Parent;
         ((Window)parent).Close();
      }

      private void MessageBoxKeyDown(object sender, EventArgs e) {
         var window = (Window)sender;
         var args = (KeyEventArgs)e;
         switch (args.Key) {
            case Key.Enter: passingResult = MessageBoxResult.Yes; window.Close(); break;
            case Key.Y: passingResult = MessageBoxResult.Yes; window.Close(); break;
            case Key.N: passingResult = MessageBoxResult.No; window.Close(); break;
            case Key.Escape: passingResult = MessageBoxResult.Cancel; window.Close(); break;
         }
      }

      public string[] MetadataFor(string fileName) {
         var metadataName = Path.ChangeExtension(fileName, ".toml");
         if (!File.Exists(metadataName)) return null;
         var lines = File.ReadAllLines(metadataName);

         return lines;
      }

      public (short[] image, int width) LoadImage() {
         var dialog = new OpenFileDialog { Filter = CreateFilterFromOptions("Image Files", "png") };
         var result = dialog.ShowDialog();
         if (result != true) return default;
         var fileName = dialog.FileName;

         using (var fileStream = File.Open(fileName, FileMode.Open)) {
            var decoder = new PngBitmapDecoder(fileStream, BitmapCreateOptions.None, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            if (frame.PixelWidth % 8 != 0) return (default, frame.PixelWidth);
            if (frame.PixelHeight % 8 != 0) return (default, frame.PixelHeight);
            var format = frame.Format;
            short[] data = new short[frame.PixelWidth * frame.PixelHeight];
            if (format == PixelFormats.Bgr24) {
               byte[] raw = new byte[frame.PixelWidth * frame.PixelHeight * 3];
               frame.CopyPixels(raw, frame.PixelWidth * 3, 0);
               for (int y = 0; y < frame.PixelHeight; y++) {
                  for (int x = 0; x < frame.PixelWidth; x++) {
                     var outputPoint = y * frame.PixelWidth + x;
                     var inputPoint = outputPoint * 3;
                     var (r, g, b) = (raw[inputPoint + 2], raw[inputPoint + 1], raw[inputPoint + 0]);
                     r >>= 3; g >>= 3; b >>= 3;
                     data[outputPoint] = (short)((r << 0) | (g << 5) | (b << 10));
                  }
               }
            } else {
               throw new NotImplementedException();
            }

            return (data, frame.PixelWidth);
         }
      }

      public void SaveImage(short[] image, int width) {
         int height = image.Length / width;

         var dialog = new SaveFileDialog { Filter = CreateFilterFromOptions("Image Files", "png") };
         var result = dialog.ShowDialog();
         if (result != true) return;
         var fileName = dialog.FileName;

         var stride = width * 2;
         var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr555, null);
         var rect = new Int32Rect(0, 0, width, height);
         bitmap.WritePixels(rect, image, stride, 0);

         var encoder = new PngBitmapEncoder();
         encoder.Frames.Add(BitmapFrame.Create(bitmap));
         using (var fileStream = File.Create(fileName)) {
            encoder.Save(fileStream);
         }
      }

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }

      private static string CreateFilterFromOptions(string description, params string[] extensionOptions) {
         if (description == null) return string.Empty;
         var extensions = string.Join(",", extensionOptions.Select(option => $"*.{option}"));
         return $"{description}|{extensions}|All Files|*.*";
      }
   }
}
