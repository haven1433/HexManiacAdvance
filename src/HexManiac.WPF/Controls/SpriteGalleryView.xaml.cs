using HavenSoft.HexManiac.Core.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class SpriteGalleryView {
      private SpriteGalleryTab ViewModel => DataContext as SpriteGalleryTab;

      public double ItemWidth => 48 * (ViewModel?.SpriteScale ?? 2) + 16;
      public double ItemHeight => 64 * (ViewModel?.SpriteScale ?? 2) + 4;

      public SpriteGalleryView() {
         InitializeComponent();
         DataContextChanged += (sender, e) => {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= ViewModelPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged vm) vm.PropertyChanged += ViewModelPropertyChanged;
            Container.Items.Refresh();
            if (Container.ActualWidth > 0) ViewModel?.SetAvailableWidth(Container.ActualWidth);
         };
      }

      /// <summary>The sprites are laid out in rows: tell the view model how many fit.</summary>
      private void ContainerSizeChanged(object sender, SizeChangedEventArgs e) {
         if (e.WidthChanged) ViewModel?.SetAvailableWidth(e.NewSize.Width);
      }

      private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(SpriteGalleryTab.SpriteScale)) Container.Items.Refresh();
      }

      private void ItemClicked(object sender, MouseButtonEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is SpriteGalleryItem item) ViewModel?.Select(item);
      }
   }
}
