using HavenSoft.Gen3Hex.ViewModel;
using System;
using System.ComponentModel;

namespace HavenSoft.Gen3Hex.View {
   public partial class MainWindow {
      public EditorViewModel ViewModel { get; }

      public MainWindow(EditorViewModel viewModel) {
         InitializeComponent();
         ViewModel = viewModel;
         DataContext = viewModel;
      }

      protected override void OnClosing(CancelEventArgs e) {
         base.OnClosing(e);
         ViewModel.CloseAll.Execute();
         if (ViewModel.Count != 0) e.Cancel = true;
      }

      private void ExitClicked(object sender, EventArgs e) {
         ViewModel.CloseAll.Execute();
         if (ViewModel.Count == 0) Close();
      }
   }
}
