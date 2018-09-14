using HavenSoft.Gen3Hex.ViewModel;
using System;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.View {
   public partial class MainWindow {
      private readonly ViewPort viewPort;

      public MainWindow(ViewPort viewModel) {
         InitializeComponent();
         viewPort = viewModel;
         DataContext = viewModel;
      }

      protected override void OnMouseWheel(MouseWheelEventArgs e) {
         base.OnMouseWheel(e);
         viewPort.ScrollValue -= Math.Sign(e.Delta);
      }
   }
}
