using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class SoundView {
      private SoundPlayer player;
      private MemoryStream playerStream;

      private SoundTab ViewModel => DataContext as SoundTab;

      public SoundView() {
         InitializeComponent();
         DataContextChanged += (sender, e) => {
            if (e.OldValue is SoundTab old) { old.RequestPlayWav -= PlayWav; old.RequestStopPlayback -= StopRequested; }
            if (e.NewValue is SoundTab vm) { vm.RequestPlayWav += PlayWav; vm.RequestStopPlayback += StopRequested; }
         };
         Unloaded += (sender, e) => StopPlayback();
      }

      private void PlayWav(object sender, byte[] wav) {
         try {
            StopPlayback();
            playerStream = new MemoryStream(wav);
            player = new SoundPlayer(playerStream);
            player.Load();
            player.Play();
         } catch (Exception ex) {
            MessageBox.Show("Could not play the sound: " + ex.Message, "CUBHMA", MessageBoxButton.OK, MessageBoxImage.Warning);
         }
      }

      private void StopRequested(object sender, EventArgs e) => StopPlayback();

      private void StopPlayback() {
         try {
            player?.Stop();
            player?.Dispose();
            playerStream?.Dispose();
         } catch (Exception) {
            // nothing to clean up
         }
         player = null;
         playerStream = null;
      }

      private void SongNameKeyDown(object sender, KeyEventArgs e) {
         if (e.Key != Key.Enter || sender is not System.Windows.Controls.TextBox box) return;
         // commit the name now: the binding updates when the box loses focus
         box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
         e.Handled = true;
      }

      // a song the view model selects (the one 'Add new song' just made, at the end of a long list) has to be brought into view
      private void SongSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
         if (sender is System.Windows.Controls.ListBox list && list.SelectedItem != null) list.ScrollIntoView(list.SelectedItem);
      }

      private void CryDoubleClick(object sender, MouseButtonEventArgs e) {
         if (ViewModel?.PlayCry.CanExecute(null) == true) ViewModel.PlayCry.Execute(null);
      }
   }
}
