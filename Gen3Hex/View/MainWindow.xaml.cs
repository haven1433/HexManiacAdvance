using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace HavenSoft.Gen3Hex.View {
   public partial class MainWindow {
      public EditorViewModel ViewModel { get; }

      public MainWindow(EditorViewModel viewModel) {
         InitializeComponent();
         ViewModel = viewModel;
         DataContext = viewModel;
      }

      protected override void OnDrop(DragEventArgs e) {
         base.OnDrop(e);

         if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var fileName in files) {
               var data = File.ReadAllBytes(fileName);
               ViewModel.Open.Execute(new LoadedFile(fileName, data));
            }
         }
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
