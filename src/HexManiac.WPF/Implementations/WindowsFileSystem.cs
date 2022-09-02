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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace HavenSoft.HexManiac.WPF.Implementations {
   public class WindowsFileSystem : IFileSystem, IWorkDispatcher {
      private const string QueryPalette = "/Text/HexManiacAdvance_Palette";

      private readonly Dictionary<string, List<FileSystemWatcher>> watchers = new();
      private readonly Dictionary<string, List<Action<IFileSystem>>> listeners = new();

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
         set {
            try {
               Clipboard.SetDataObject(value, true);
            } catch (COMException) {
               try {
                  // try again, but don't try to persist the value after app exit
                  Clipboard.SetDataObject(value);
                  ShowCustomMessageBox("Copied text to clipboard.", false);
               } catch (COMException) {
                  // something went wrong... we couldn't copy
                  var window = (MainWindow)Application.Current.MainWindow;
                  window.ViewModel.ErrorMessage = "Could not copy";
               }
            }
         }
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

      public string OpenFolder() {
         using (var dialog = new FolderBrowserDialog()) {
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK) return null;
            return dialog.SelectedPath;
         }
      }

      public bool Exists(string fileName) => File.Exists(fileName);

      public void LaunchProcess(string file) {
         try {
            file = Path.GetFullPath(file);
            NativeProcess.Start(file);
         } catch (System.ComponentModel.Win32Exception) {
            var nl = Environment.NewLine;
            var path = Path.GetFileName(file);
            ShowCustomMessageBox(
               $"{EditorViewModel.ApplicationName} tried to run{nl}{path}{nl}but there is no application associated with its file type.",
               showYesNoCancel: false, new ProcessModel("Change 'Opens with:'", "!" + file));
         }
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
               dispatcher.BeginInvoke(
                  () => {
                     listener(this);
                     scheduled = false;
                  }, DispatcherPriority.ApplicationIdle);
            }
         };
         watcher.EnableRaisingEvents = true;

         if (!watchers.ContainsKey(fileName)) {
            watchers[fileName] = new();
            listeners[fileName] = new();
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

      public void BlockOnUIWork(Action action) {
         dispatcher.Invoke(action, DispatcherPriority.Normal);
      }

      public Task DispatchWork(Action action) {
         return Task.Run(() => {
            try {
               dispatcher.Invoke(action, DispatcherPriority.Input);
            } catch (TaskCanceledException) {
               // that's ok
            }
         });
      }

      public Task RunBackgroundWork(Action action) => Task.Run(action);

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

         // disable watchers for this file since we're about to save it ourselves
         if (watchers.TryGetValue(file.Name, out var watcherList) && listeners.TryGetValue(file.Name, out var listenerList)) {
            foreach (var watcher in watcherList) watcher.EnableRaisingEvents = false;
         } else {
            watcherList = null;
            listenerList = null;
         }

         bool result = true;
         try {
            // use FileShare ReadWrite so we can write the file while another program is holding it.
            using (var stream = new FileStream(file.Name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)) {
               stream.Write(file.Contents, 0, file.Contents.Length);
            }
         } catch (Exception) {
            ShowCustomMessageBox("Could not save. The file might be ReadOnly or in use by another application.", showYesNoCancel: false);
            result = false;
         }

         // re-enable watchers for this file
         if (watcherList != null && listenerList != null) {
            foreach (var watcher in watcherList) watcher.EnableRaisingEvents = true;
         }

         return result;
      }

      public bool SaveMetadata(string originalFileName, string[] metadata) {
         if (metadata == null) return true; // nothing to save
         var metadataName = originalFileName + ".toml";
         if (originalFileName.ToLower().EndsWith(".gba")) {
            metadataName = Path.ChangeExtension(originalFileName, ".toml");
         }
         int tryCount = 0;
         while (tryCount < 5) {
            try {
               File.WriteAllLines(metadataName, metadata);
               break;
            } catch (Exception ex) {
               tryCount += 1;
               if (tryCount < 5) {
                  Thread.Sleep(100);
               } else {
                  ShowCustomMessageBox($"Failed to write {metadataName}:{Environment.NewLine}{ex.Message}.", showYesNoCancel: false);
                  return false;
               }
            }
         }
         return true;
      }

      public bool? TrySavePrompt(LoadedFile file) {
         var displayName = string.Empty;
         if (!string.IsNullOrEmpty(file.Name)) displayName = Environment.NewLine + file.Name;
         var result = ShowCustomMessageBox($"Would you like to save{displayName}?");
         if (result != true) return result;
         if (displayName == string.Empty) displayName = RequestNewName(displayName);
         if (string.IsNullOrEmpty(displayName)) return null;
         return Save(new LoadedFile(displayName.Trim(), file.Contents));
      }

      public bool? ShowCustomMessageBox(string message, bool showYesNoCancel = true, params ProcessModel[] links) {
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
                  new TextBox {
                     BorderThickness = new Thickness(0),
                     Background = Brushes.Transparent,
                     IsReadOnly = true,
                     Text = message,
                     Margin = new Thickness(15, 20, 15, 5),
                     TextWrapping = TextWrapping.Wrap,
                  },
                  choices,
               }
            }.Fluent(sp => {
               foreach (var link in links) {
                  sp.Children.Insert(1, new TextBlock {
                     Margin = new Thickness(20, 2, 20, 2),
                     Inlines = {
                        new Hyperlink {
                           Foreground = Brush(nameof(Theme.Accent)),
                           Inlines = { new Run(link.DisplayText) },
                        }.Fluent(hyperlink => hyperlink.Click += (sender, e) => {
                           try {
                              if (link.Content.StartsWith("~")) {
                                 CopyText = link.Content.Substring(1);
                              } else if (!link.Content.StartsWith("!")) {
                                 NativeProcess.Start(link.Content);
                              } else {
                                 ShowFileProperties(link.Content.Substring(1));
                              }
                           } catch {
                              ShowCustomMessageBox($"Could not start '{link.Content}'.", showYesNoCancel: false);
                           }
                        }),
                     }
                  });
               }
            }),
            Owner = Application.Current.MainWindow,
            Left = Application.Current.MainWindow.Left + Application.Current.MainWindow.Width / 2,
            Top = Application.Current.MainWindow.Top + Application.Current.MainWindow.Height / 2,
         }.SetEvent(UIElement.KeyDownEvent, MessageBoxKeyDown);

         passingResult = MessageBoxResult.Cancel;
         window.ShowDialog();

         return passingResult switch {
            MessageBoxResult.No => false,
            MessageBoxResult.OK => true,
            MessageBoxResult.Yes => true,
            MessageBoxResult.Cancel => null,
            _ => throw new NotSupportedException()
         };
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
         if (!fileName.ToLower().EndsWith(".gba")) metadataName = fileName + ".toml";
         if (!File.Exists(metadataName)) return null;
         var lines = File.ReadAllLines(metadataName);

         return lines;
      }

      public (short[] image, int width) LoadImage(string fileName) {
         if (fileName == null) {
            var dialog = new OpenFileDialog { Filter = CreateFilterFromOptions("Image Files", "png") };
            var result = dialog.ShowDialog();
            if (result != true) return default;
            fileName = dialog.FileName;
         }

         try {
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

               var (data, width) = DecodeImage(frame);

               return (data, frame.PixelWidth);
            }
         } catch(UnauthorizedAccessException) {
            var nl = Environment.NewLine;
            MessageBox.Show($"Access Denied.{nl}Do you have read access to the file?{nl}Your anti-virus may be blocking {EditorViewModel.ApplicationName}.");
            return default;
         } catch (IOException io) {
            MessageBox.Show($"Error: {io.Message}.");
            return default;
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
            var stride = (int)Math.Ceiling(frame.PixelWidth / 2.0);
            byte[] raw = new byte[stride * frame.PixelHeight];
            frame.CopyPixels(raw, stride, 0);
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

      public void SaveImage(short[] image, int width, string fileName = null) {
         if (fileName == null) {
            var dialog = new SaveFileDialog { Filter = CreateFilterFromOptions("Image Files", "png") };
            var result = dialog.ShowDialog();
            if (result != true) return;
            fileName = dialog.FileName;
         }

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

      public int ShowOptions(string title, string prompt, IReadOnlyList<IReadOnlyList<object>> additionalDetails, VisualOption[] options) {
         var collection = new ObservableCollection<VisualOption>();
         foreach (var option in options) collection.Add(option);

         var optionDialog = new OptionDialog { Title = title, Prompt = { Text = prompt }, AdditionalDetails = { ItemsSource = additionalDetails }, Options = { ItemsSource = collection } };
         optionDialog.ShowDialog();
         return optionDialog.Result;
      }

      public string RequestText(string title, string prompt) {
         var dialog = new RequestTextDialog { Title = title, Prompt = { Text = prompt } };
         dialog.ShowDialog();
         return dialog.Result;
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

      #region StackOverflow: how to open a file's properties dialog (https://stackoverflow.com/questions/1936682/how-do-i-display-a-files-properties-dialog-from-c)

      [DllImport("shell32.dll", CharSet = CharSet.Auto)]
      static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

      [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
      public struct SHELLEXECUTEINFO {
         public int cbSize;
         public uint fMask;
         public IntPtr hwnd;
         [MarshalAs(UnmanagedType.LPTStr)]
         public string lpVerb;
         [MarshalAs(UnmanagedType.LPTStr)]
         public string lpFile;
         [MarshalAs(UnmanagedType.LPTStr)]
         public string lpParameters;
         [MarshalAs(UnmanagedType.LPTStr)]
         public string lpDirectory;
         public int nShow;
         public IntPtr hInstApp;
         public IntPtr lpIDList;
         [MarshalAs(UnmanagedType.LPTStr)]
         public string lpClass;
         public IntPtr hkeyClass;
         public uint dwHotKey;
         public IntPtr hIcon;
         public IntPtr hProcess;
      }

      private const int SW_SHOW = 5;
      private const uint SEE_MASK_INVOKEIDLIST = 12;
      public static bool ShowFileProperties(string filename) {
         SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
         info.cbSize = Marshal.SizeOf(info);
         info.lpVerb = "properties";
         info.lpFile = filename;
         info.nShow = SW_SHOW;
         info.fMask = SEE_MASK_INVOKEIDLIST;
         return ShellExecuteEx(ref info);
      }
      #endregion
   }
}
