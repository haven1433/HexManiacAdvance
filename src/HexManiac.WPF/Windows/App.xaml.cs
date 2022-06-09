using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Implementations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

[assembly: AssemblyTitle("HexManiacAdvance")]

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class App {
      public const string ReleaseUrl = "https://github.com/haven1433/HexManiacAdvance/releases";
      public const string
         Arg_No_Metadata = "--no-metadata",
         Arg_Developer_Menu = "--dev-menu";

      private Mutex singleInstanceApplicationMutex;
      private readonly string appInstanceIdentifier;

      [STAThread]
      public static void Main(string[] args) => new App().Run();

      public App() {
         this.InitializeComponent();
         // name mutex and pipes based on the file location.
         // This allows us to have debug and release running at the same time,
         // or 0.3 and 0.4 running at the same time, etc.
         // Replace slashes in the path with _, since slash is a reserved character in mutex.
         appInstanceIdentifier = "{HexManiacAdvance} : " + typeof(App).Assembly.Location.Replace("\\", "_");
      }

      protected override void OnStartup(StartupEventArgs e) {
         base.OnStartup(e);

         var (path, address, options) = ParseArgs(e.Args);

         singleInstanceApplicationMutex = new Mutex(true, appInstanceIdentifier, out var mutexIsNew);
         if (!mutexIsNew) SendParams(path, address);

         var fileSystem = new WindowsFileSystem(Dispatcher);
         var viewModel = GetViewModel(path, address, fileSystem, options);
         SetupServer(viewModel);

         DebugLog(viewModel, e.Args);
         DebugLog(viewModel, "Have Editor");
         UpdateThemeDictionary(Resources, viewModel.Theme);
         viewModel.Theme.PropertyChanged += (sender, _) => UpdateThemeDictionary(Resources, viewModel.Theme);
         MainWindow = new MainWindow(viewModel);
         MainWindow.Resources.Add("FileSystem", fileSystem);
         MainWindow.Resources.Add("PaletteMixer", new PaletteCollection().Fluent(mixer => mixer.SetContents(new short[16])));
         MainWindow.Resources.Add("IsPaletteMixerExpanded", new EditableValue<bool>());
         MainWindow.Show();
         DebugLog(viewModel, "All Started!");
      }

      private void SetupServer(EditorViewModel viewModel) {
         Task.Factory.StartNew(() => {
            while (true) {
               using (var singleInstanceServer = new NamedPipeServerStream(appInstanceIdentifier)) {
                  singleInstanceServer.WaitForConnection();
                  using (var reader = new StreamReader(singleInstanceServer)) {
                     var line = reader.ReadLine();
                     Dispatcher.Invoke(() => {
                        AcceptParams(viewModel, line);
                        MainWindow.Activate();
                        if (MainWindow.WindowState == WindowState.Minimized) {
                           MainWindow.WindowState = WindowState.Normal;
                        }
                     });
                  }
               }
            }
         });
      }

      private void SendParams(string path, int address) {
         if (path != string.Empty) path = Path.GetFullPath(path);
         using (var client = new NamedPipeClientStream(appInstanceIdentifier)) {
            client.Connect();
            using (var writer = new StreamWriter(client)) {
               writer.WriteLine($"{path}(){address}");
               writer.Flush();
            }
         }
         Shutdown();
      }
      private void AcceptParams(EditorViewModel editor, string line) {
         var parts = line.Split("()");
         if (!File.Exists(parts[0])) return;
         var fileSystem = ((MainWindow)MainWindow).FileSystem;
         int.TryParse(parts[1], out int address);
         TryOpenFile(editor, fileSystem, parts[0], address);
      }

      private static (string path, int address, bool[] options) ParseArgs(string[] args) {
         var useMetadata = true;
         var showDevMenu = false;
         if (args.Any(arg => arg == Arg_No_Metadata)) {
            useMetadata = false;
            args = args.Where(arg => arg != Arg_No_Metadata).ToArray();
         }
         if (args.Any(arg => arg == Arg_Developer_Menu)) {
            showDevMenu = true;
            args = args.Where(arg => arg != Arg_Developer_Menu).ToArray();
         }

         var allArgs = args.Aggregate(string.Empty, (a, b) => a + ' ' + b).Trim();
         var loadAddress = -1;
         if (allArgs.Contains(":") && allArgs.LastIndexOf(":") > 4) {
            var parts = allArgs.Split(':');
            if (!int.TryParse(parts.Last(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out loadAddress)) loadAddress = -1;
            allArgs = parts.Take(parts.Length - 1).Aggregate((a, b) => a + ":" + b).Trim();
         } else if (allArgs.ToLower().Contains(".gba ")) {
            var parts = allArgs.Split(" ");
            if (!int.TryParse(parts.Last(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out loadAddress)) loadAddress = -1;
            allArgs = parts.Take(parts.Length - 1).Aggregate(string.Empty, (a, b) => a + " " + b).Trim();
         }

         return (allArgs, loadAddress, new[] { useMetadata, showDevMenu });
      }

      /// <summary>
      /// Generally, the initial working directory is set to wherever the program was launched from.
      /// In the case of command line usage or dropping a file onto the EXE, that's not the EXE's location.
      /// We want the initial loading directory to match the EXE's path so we can find the reference files.
      /// Example: armReference.txt, scriptReference.txt
      /// </summary>
      private static void SetInitialWorkingDirectory() {
         var mainAssemblyLocation = Assembly.GetExecutingAssembly().Location;
         var workingDirectory = Path.GetDirectoryName(mainAssemblyLocation);
         Directory.SetCurrentDirectory(workingDirectory);
      }

      public static void UpdateThemeDictionary(ResourceDictionary resources, Theme theme) {
         if (resources.MergedDictionaries.Count == 0) resources.MergedDictionaries.Add(new ResourceDictionary());

         var sources = new List<string> {
            nameof(theme.Primary),
            nameof(theme.Secondary),
            nameof(theme.Background),
            nameof(theme.Backlight),
            nameof(theme.Error),
            nameof(theme.Text1),
            nameof(theme.Text2),
            nameof(theme.Data1),
            nameof(theme.Data2),
            nameof(theme.Accent),
            nameof(theme.Stream1),
            nameof(theme.Stream2),
            nameof(theme.EditBackground),
         };

         var dict = new ResourceDictionary();
         var themeType = theme.GetType();
         sources.ForEach(source => {
            var rawValue = (string)themeType.GetProperty(source).GetValue(theme);
            dict.Add(source, Brush(rawValue));
            dict.Add(source + "Color", ColorConverter.ConvertFromString(rawValue));
         });

         resources.MergedDictionaries[0] = dict;
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

      private static EditorViewModel GetViewModel(string fileName, int address, WindowsFileSystem fileSystem, bool[] options) {
         bool useMetadata = options[0];
         bool showDevMenu = options[1];
         if (fileName != string.Empty) fileName = Path.GetFullPath(fileName);
         SetInitialWorkingDirectory();
         var editor = new EditorViewModel(fileSystem, fileSystem, allowLoadingMetadata: useMetadata) { ShowDeveloperMenu = showDevMenu };
         DebugLog(editor, "------");
         DebugLog(editor, fileName);
         CheckIsNewerVersionAvailable(editor);
         if (!File.Exists(fileName)) return editor;
         DebugLog(editor, "File Exists");
         TryOpenFile(editor, fileSystem, fileName, address);
         return editor;
      }

      private static void TryOpenFile(EditorViewModel editor, WindowsFileSystem fileSystem, string fileName, int address) {
         var loadedFile = fileSystem.LoadFile(fileName);
         editor.Open.Execute(loadedFile);
         DebugLog(editor, "Tab Added");
         var tab = editor[editor.SelectedIndex] as ViewPort;
         if (tab != null && address >= 0) {
            DebugLog(editor, $"Loading at Script {address:X6}.");
            tab.Model.InitializationWorkload.ContinueWith(
               task => fileSystem.DispatchWork(() => tab.CascadeScript(address)),
               TaskContinuationOptions.ExecuteSynchronously);
            editor.GotoViewModel.ControlVisible = false;
         }
      }

      [Conditional("DEBUG")]
      private static void DebugLog(EditorViewModel editor, params string[] text) {
         if (editor.LogAppStartupProgress) {
            File.AppendAllLines("HexManiacAdvance.debug.txt", text);
         }
      }

      private static void CheckIsNewerVersionAvailable(EditorViewModel viewModel) {
         if (DateTime.Now < viewModel.LastUpdateCheck + TimeSpan.FromDays(1)) return;
         viewModel.LastUpdateCheck = DateTime.Now;
         try {
            using (var client = new HttpClient()) {
               string content = client.GetStringAsync(ReleaseUrl).Result;
               var mostRecentVersion = content
                  .Split('\n')
                  .Where(line => line.Contains("/haven1433/HexManiacAdvance/tree/"))
                  .Select(line => line.Split("title=").Last().Split('"')[1].Split('v').Last())
                  .First();
               viewModel.IsNewVersionAvailable = StoredMetadata.NeedVersionUpdate(viewModel.Singletons.MetadataInfo.VersionNumber, mostRecentVersion);
            }
         } catch {
            // Exceptions are expected on Windows 7.
            // If anything goes wrong, we probably don't care. It just means that the IsNewVersionAvailable will be false.
         }
      }
   }
}
