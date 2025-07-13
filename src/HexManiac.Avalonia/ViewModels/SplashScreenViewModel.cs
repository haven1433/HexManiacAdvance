using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HavenSoft.HexManiac.Avalonia.ViewModels;

public partial class SplashScreenViewModel(string imageResourcePath) : ViewModelBase
{
    [ObservableProperty] public Bitmap? _imageSplash = new Bitmap(AssetLoader.Open(new Uri("avares://HexManiacAdvance/" + imageResourcePath)));
}
