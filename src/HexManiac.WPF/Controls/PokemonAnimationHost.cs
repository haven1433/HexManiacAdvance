using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HavenSoft.HexManiac.WPF.Controls {
   /// <summary>
   /// Hosts the "In-game animation" preview of the table tool and owns its timer.
   /// The timer exists only while the preview is on screen (loaded, visible, in the active window) and playing:
   /// it is created when that becomes true and thrown away when it stops being true (tab switch, collapsed panel, window deactivated,
   /// view removed). The view model does all the work in <see cref="PokemonAnimationPreviewViewModel.Advance"/>.
   /// </summary>
   public class PokemonAnimationHost : Decorator {
      private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000.0 / 60);

      private PokemonAnimationPreviewViewModel viewModel;
      private Window window;
      private bool attached;
      private DispatcherTimer timer;
      private readonly Stopwatch clock = new Stopwatch();
      private long lastTick;

      public PokemonAnimationHost() {
         Loaded += (sender, e) => Attach();
         Unloaded += (sender, e) => Detach();
         IsVisibleChanged += (sender, e) => Update();
         DataContextChanged += (sender, e) => ChangeViewModel(e.NewValue as PokemonAnimationPreviewViewModel);
      }

      private void Attach() {
         attached = true;
         if (window == null) {
            window = Window.GetWindow(this);
            if (window != null) {
               window.Activated += WindowStateChanged;
               window.Deactivated += WindowStateChanged;
               window.StateChanged += WindowStateChanged;
            }
         }
         ChangeViewModel(DataContext as PokemonAnimationPreviewViewModel);
         Update();
      }

      private void Detach() {
         attached = false;
         if (window != null) {
            window.Activated -= WindowStateChanged;
            window.Deactivated -= WindowStateChanged;
            window.StateChanged -= WindowStateChanged;
            window = null;
         }
         if (viewModel != null) {
            viewModel.PropertyChanged -= ViewModelChanged;
            viewModel.IsDisplayed = false;
            viewModel = null;
         }
         StopTimer();
      }

      private void ChangeViewModel(PokemonAnimationPreviewViewModel newViewModel) {
         if (ReferenceEquals(viewModel, newViewModel)) return;
         if (viewModel != null) {
            viewModel.PropertyChanged -= ViewModelChanged;
            viewModel.IsDisplayed = false;
         }
         viewModel = attached ? newViewModel : null;
         if (viewModel != null) viewModel.PropertyChanged += ViewModelChanged;
         Update();
      }

      private void WindowStateChanged(object sender, EventArgs e) => Update();

      private void ViewModelChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName != nameof(PokemonAnimationPreviewViewModel.WantsTicks)) return;
         if (!Dispatcher.CheckAccess()) Dispatcher.BeginInvoke(new Action(Update)); else Update();
      }

      private bool IsShown => IsLoaded && IsVisible && window != null && window.IsActive && window.WindowState != WindowState.Minimized;

      private void Update() {
         var shown = IsShown;
         if (viewModel != null) viewModel.IsDisplayed = shown;
         if (shown && viewModel != null && viewModel.WantsTicks) StartTimer(); else StopTimer();
      }

      private void StartTimer() {
         if (timer != null) return;
         timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TickInterval };
         timer.Tick += Tick;
         clock.Restart();
         lastTick = 0;
         timer.Start();
      }

      private void StopTimer() {
         if (timer == null) return;
         timer.Stop();
         timer.Tick -= Tick;
         timer = null;
         clock.Stop();
      }

      private void Tick(object sender, EventArgs e) {
         var now = clock.ElapsedMilliseconds;
         var elapsed = (int)Math.Min(now - lastTick, 1000);
         lastTick = now;
         // a bad frame must never take the whole program down: stop the preview instead
         try { viewModel?.Advance(elapsed); } catch (Exception) { StopTimer(); }
      }
   }
}
