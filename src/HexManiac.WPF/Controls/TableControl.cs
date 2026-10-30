using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TableControl : Panel {

      #region Groups

      public static readonly DependencyProperty GroupsProperty = DependencyProperty.Register("Groups", typeof(ObservableCollection<TableGroupViewModel>), typeof(TableControl), new FrameworkPropertyMetadata(null, GroupsChanged));

      public ObservableCollection<TableGroupViewModel> Groups {
         get => (ObservableCollection<TableGroupViewModel>)GetValue(GroupsProperty);
         set => SetValue(GroupsProperty, value);
      }

      private static void GroupsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (TableControl)d;
         self.OnGroupsChanged(e);
      }

      protected virtual void OnGroupsChanged(DependencyPropertyChangedEventArgs e) {
         var oldGroups = (ObservableCollection<TableGroupViewModel>)e.OldValue;
         if (oldGroups != null) {
            oldGroups.CollectionChanged -= CollectionChanged;
         }
         var newGroups = (ObservableCollection<TableGroupViewModel>)e.NewValue;
         if (newGroups != null) {
            newGroups.CollectionChanged += CollectionChanged;
         }
         InvalidateVisual();
      }

      private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
         InvalidateVisual();
      }

      #endregion

      protected override void OnRender(DrawingContext dc) {
         base.OnRender(dc);
         if (Groups == null) return;
         int count = 0;
         var primary = Brush(nameof(Theme.Primary));
         foreach (var group in Groups) {
            foreach (var member in group.Members) {
               dc.DrawEllipse(primary, null, new(30, 30 + count * 60), 30, 30);
               count++;
            }
         }
      }

      protected override Size MeasureOverride(Size availableSize) {
         return new Size(60, 600);
      }

      protected override Size ArrangeOverride(Size finalSize) {
         var size = base.ArrangeOverride(finalSize);
         return new Size(size.Width, 600);
      }

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }
   }
}
