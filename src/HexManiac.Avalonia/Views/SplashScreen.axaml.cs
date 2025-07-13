using System;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using HavenSoft.HexManiac.Avalonia.ViewModels;

namespace HavenSoft.HexManiac.Avalonia.Views;

public partial class SplashScreen : Window
{
    public Action? mainAction;
    public SplashScreen() {
    }

    public SplashScreen(string imageResourcePath)
    {
        DataContext = new SplashScreenViewModel(imageResourcePath);
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        DummyLoad();
    }

    private async void DummyLoad() {
        // Do some background stuff here.
        await Task.Delay(3000);

        await Dispatcher.UIThread.InvokeAsync(OnInvokeAsync);
        void OnInvokeAsync() {
            mainAction?.Invoke();
            Close();
        }
    }
}
