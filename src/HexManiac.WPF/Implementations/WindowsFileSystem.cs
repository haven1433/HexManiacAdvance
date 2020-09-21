using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Controls;
using HavenSoft.HexManiac.WPF.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HavenSoft.HexManiac.WPF.Implementations {
   public class WindowsFileSystem : IFileSystem, IWorkDispatcher {
      private const string QueryPalette = "/Text/HexManiacAdvance_Palette";

      private readonly Dictionary<string, List<FileSystemWatcher>> watchers = new Dictionary<string, List<FileSystemWatcher>>();
      private readonly Dictionary<string, List<Action<IFileSystem>>> listeners = new Dictionary<string, List<Action<IFileSystem>>>();

      private readonly Dispatcher dispatcher;

      public string CopyText {
         get {
            try {
               return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            } catch {
               // we shouldn't be able to get an exception because of the ContainsText() check.
               // but some users have still been able to get the failure.
               // so just in case it fails, count that as "no text"
               return string.Empty;
            }
         }
         set => Clipboard.SetText(value);
      }

      public (short[] image, int width) CopyImage {
         get {
            if (Clipboard.ContainsImage()) {
               var bitmapSource = Clipboard.GetImage();
               return DecodeImage(bitmapSource);
            } else {
               return (default, default);
            }
         }
         set {
            var frame = EncodeImage(value.image, value.width);
            Clipboard.SetImage(frame);
         }
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
         bool scheduled = false;
         watcher.Changed += (sender, e) => {
            if (e.FullPath.EndsWith(fileName)) {
               if (scheduled) return;  // if multiple changes come in fairly quickly, ignore
               scheduled = true;
               dispatcher.BeginInvoke((Action)(() => { listener(this); scheduled = false; }), DispatcherPriority.ApplicationIdle);
            }
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

      public void DispatchWork(Action action) => dispatcher.BeginInvoke(action, DispatcherPriority.Input);

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
            BitmapFrame frame;
            try {
               var decoder = new PngBitmapDecoder(fileStream, BitmapCreateOptions.None, BitmapCacheOption.None);
               frame = decoder.Frames[0];
            } catch (FileFormatException) {
               MessageBox.Show("Could not decode bitmap. The file may not be a valid PNG.");
               return default;
            }
            var metadata = (BitmapMetadata)frame.Metadata;
            short[] comparePalette = null;
            var comparePaletteMetadata = metadata.GetQuery(QueryPalette) as string;
            if (comparePaletteMetadata != null) {
               comparePalette = comparePaletteMetadata.Split(",")
                  .Select(hex => short.TryParse(hex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var color) ? color : default)
                  .ToArray();
            }

            if (frame.PixelWidth % 8 != 0) return (default, frame.PixelWidth);
            if (frame.PixelHeight % 8 != 0) return (default, frame.PixelHeight);

            var (data, width) = DecodeImage(frame);

            return (data, frame.PixelWidth);
         }
      }

      private static (short[] image, int width) DecodeImage(BitmapSource frame) {
         var format = frame.Format;
         short[] data = new short[frame.PixelWidth * frame.PixelHeight];

         if (format == PixelFormats.Bgr24) {
            byte[] raw = new byte[frame.PixelWidth * frame.PixelHeight * 3];
            frame.CopyPixels(raw, frame.PixelWidth * 3, 0);
            for (int y = 0; y < frame.PixelHeight; y++) {
               for (int x = 0; x < frame.PixelWidth; x++) {
                  var outputPoint = y * frame.PixelWidth + x;
                  var inputPoint = outputPoint * 3;
                  var color = Color.FromRgb(raw[inputPoint + 2], raw[inputPoint + 1], raw[inputPoint + 0]);
                  data[outputPoint] = Convert(color);
               }
            }
         } else if (format == PixelFormats.Bgra32) {
            byte[] raw = new byte[frame.PixelWidth * frame.PixelHeight * 4];
            frame.CopyPixels(raw, frame.PixelWidth * 4, 0);
            for (int y = 0; y < frame.PixelHeight; y++) {
               for (int x = 0; x < frame.PixelWidth; x++) {
                  var outputPoint = y * frame.PixelWidth + x;
                  var inputPoint = outputPoint * 4;
                  var color = Color.FromRgb(raw[inputPoint + 2], raw[inputPoint + 1], raw[inputPoint + 0]);
                  data[outputPoint] = Convert(color);
               }
            }
         } else if (format == PixelFormats.Indexed8) {
            byte[] raw = new byte[frame.PixelWidth * frame.PixelHeight];
            frame.CopyPixels(raw, frame.PixelWidth, 0);
            for (int y = 0; y < frame.PixelHeight; y++) {
               for (int x = 0; x < frame.PixelWidth; x++) {
                  var outputPoint = y * frame.PixelWidth + x;
                  var inputPoint = raw[y * frame.PixelWidth + x];
                  var color = frame.Palette.Colors[inputPoint];
                  data[outputPoint] = Convert(color);
               }
            }
         } else if (format == PixelFormats.Indexed4) {
            byte[] raw = new byte[frame.PixelWidth * frame.PixelHeight / 2];
            frame.CopyPixels(raw, frame.PixelWidth / 2, 0);
            for (int y = 0; y < frame.PixelHeight; y++) {
               for (int x = 0; x < frame.PixelWidth / 2; x++) {
                  var outputPoint1 = y * frame.PixelWidth + x * 2;
                  var outputPoint2 = y * frame.PixelWidth + x * 2 + 1;
                  var inputPoint = raw[y * frame.PixelWidth / 2 + x];
                  var color1 = frame.Palette.Colors[inputPoint >> 4];
                  var color2 = frame.Palette.Colors[inputPoint & 0xF];
                  data[outputPoint1] = Convert(color1);
                  data[outputPoint2] = Convert(color2);
               }
            }
         } else if (format == PixelFormats.Indexed2) {
            byte[] raw = new byte[frame.PixelWidth * frame.PixelHeight / 2];
            frame.CopyPixels(raw, frame.PixelWidth / 2, 0);
            for (int y = 0; y < frame.PixelHeight; y++) {
               for (int x = 0; x < frame.PixelWidth / 4; x++) {
                  var outputPoint1 = y * frame.PixelWidth + x * 4;
                  var outputPoint2 = y * frame.PixelWidth + x * 4 + 1;
                  var outputPoint3 = y * frame.PixelWidth + x * 4 + 2;
                  var outputPoint4 = y * frame.PixelWidth + x * 4 + 3;
                  var inputPoint = raw[y * frame.PixelWidth / 2 + x];
                  var color1 = frame.Palette.Colors[inputPoint >> 6];
                  var color2 = frame.Palette.Colors[(inputPoint >> 4) & 0x3];
                  var color3 = frame.Palette.Colors[(inputPoint >> 2) & 0x3];
                  var color4 = frame.Palette.Colors[inputPoint & 0x3];
                  data[outputPoint1] = Convert(color1);
                  data[outputPoint2] = Convert(color2);
                  data[outputPoint3] = Convert(color3);
                  data[outputPoint4] = Convert(color4);
               }
            }
         } else {
            MessageBox.Show($"Current version does not support converting PixelFormats.{format}");
            return (null, frame.PixelWidth);
         }

         return (data, frame.PixelWidth);
      }

      public void SaveImage(short[] image, int width) {
         var dialog = new SaveFileDialog { Filter = CreateFilterFromOptions("Image Files", "png") };
         var result = dialog.ShowDialog();
         if (result != true) return;
         var fileName = dialog.FileName;

         var frame = EncodeImage(image, width);

         var encoder = new PngBitmapEncoder();
         encoder.Frames.Add(frame);
         using (var fileStream = File.Create(fileName)) {
            encoder.Save(fileStream);
         }
      }

      private static BitmapFrame EncodeImage(short[] image, int width) {
         int height = image.Length / width;
         var stride = width * 2;
         var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr555, null);
         var rect = new Int32Rect(0, 0, width, height);
         bitmap.WritePixels(rect, image, stride, 0);
         var metadata = new BitmapMetadata("png");
         return BitmapFrame.Create(bitmap, null, metadata, null);
      }

      public int ShowOptions(string title, string prompt, VisualOption[] options) {
         var collection = new ObservableCollection<VisualOption>();
         foreach (var option in options) collection.Add(option);

         var optionDialog = new OptionDialog { Title = title, Prompt = { Text = prompt }, Options = { ItemsSource = collection } };
         optionDialog.ShowDialog();
         return optionDialog.Result;
      }

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }

      private static short Convert(Color color) {
         var (r, g, b) = (color.R, color.G, color.B);
         r >>= 3; g >>= 3; b >>= 3;
         return (short)((r << 10) | (g << 5) | (b << 0));
      }

      private static string CreateFilterFromOptions(string description, params string[] extensionOptions) {
         if (description == null) return string.Empty;
         var extensions = string.Join(",", extensionOptions.Select(option => $"*.{option}"));
         return $"{description}|{extensions}|All Files|*.*";
      }
   }
}
