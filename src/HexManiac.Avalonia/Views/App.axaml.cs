using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HavenSoft.HexManiac.Avalonia.ViewModels;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Color = Avalonia.Media.Color;

[assembly: AssemblyTitle("HexManiacAdvance")]

namespace HavenSoft.HexManiac.Avalonia.Views;

public partial class App : Application {
   public const string ReleaseUrl = "https://github.com/haven1433/HexManiacAdvance/releases";
   public const string
      Arg_Skip_Splash_Screen = "--skip-splash",
      Arg_No_Metadata = "--no-metadata",
      Arg_Developer_Menu = "--dev-menu";

   private readonly string appInstanceIdentifier;
   private static string[] mainArgs;
   private MainWindow MainWindow;
   private IClassicDesktopStyleApplicationLifetime Desktop;
   private static readonly ColorConverter ColorConverter = new();


   [STAThread]
   public static void Main(string[] args) {
      mainArgs = args;
      _ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
   }

   public App() {
      // name mutex and pipes based on the file location.
      // This allows us to have debug and release running at the same time,
      // or 0.3 and 0.4 running at the same time, etc.
      // Replace slashes in the path with _, since slash is a reserved character in mutex.
      appInstanceIdentifier = "{HexManiacAdvance} : " + typeof(App).Assembly.Location.Replace("\\", "_");
   }

   private static AppBuilder BuildAvaloniaApp() {
      return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
   }

   public override void Initialize() {
      AvaloniaXamlLoader.Load(this);
   }

   public override void OnFrameworkInitializationCompleted() {
      if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
         Desktop = desktop;
         if (!Arg_Skip_Splash_Screen.IsAny(mainArgs)) {
            var splashScreen = new SplashScreen("Assets/Splash.png");
            splashScreen.mainAction += OnStartup;
            splashScreen.Show();
         }
         _ = new App();
      }

      base.OnFrameworkInitializationCompleted();
   }

   protected void OnStartup() {
      MainWindow = new MainWindow()
      {
         DataContext = new MainWindowViewModel()
      };
      MainWindow.Show();
      MainWindow.Focus();
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
         var c = (System.Drawing.Color)ColorConverter.ConvertFromString(text);
         var color = new Color(c.A, c.R, c.G, c.B);
         var brush = new SolidColorBrush(color);
         brush.ToImmutable();
         return brush;
      }
      catch {
         return null;
      }
   }

}
