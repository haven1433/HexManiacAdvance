using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TabView {

      #region ZoomLevel

      public static readonly DependencyProperty ZoomLevelProperty = DependencyProperty.Register(nameof(ZoomLevel), typeof(int), typeof(TabView), new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

      public int ZoomLevel {
         get => (int)GetValue(ZoomLevelProperty);
         set => SetValue(ZoomLevelProperty, value);
      }

      #endregion

      public IFileSystem FileSystem => (IFileSystem)Application.Current.MainWindow.Resources["FileSystem"];
      public TabView() => InitializeComponent();

      private void StringToolContentSelectionChanged(object sender, RoutedEventArgs e) {
         var textbox = (TextBox)sender;
         var tools = textbox.DataContext as ToolTray;
         if (tools == null || tools.StringTool == null) return;
         tools.StringTool.ContentIndex = textbox.SelectionStart;
         tools.StringTool.ContentSelectionLength = textbox.SelectionLength;
      }

      private void HeaderMouseDown(object sender, MouseButtonEventArgs e) {
         HexContent.RaiseEvent(e);
      }

      private readonly Popup contextMenu = new Popup();
      private void AddressShowMenu(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var viewModel = element.DataContext as ViewPort;
         if (viewModel == null) return;
         contextMenu.Child = new Button {
            Content = "Copy Address"
         }.SetEvent(Button.ClickEvent, (sender2, e2) => {
            viewModel.CopyAddress.Execute(FileSystem);
            contextMenu.IsOpen = false;
         });
         contextMenu.PlacementTarget = element;
         contextMenu.StaysOpen = false;
         contextMenu.IsOpen = true;
      }
   }
}
