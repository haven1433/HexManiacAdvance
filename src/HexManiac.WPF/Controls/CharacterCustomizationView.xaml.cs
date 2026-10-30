using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   /// <summary>The view of the Character Customization tab. All the logic is in the view model: this only forwards clicks and name edits.</summary>
   public partial class CharacterCustomizationView {
      public CharacterCustomizationView() {
         InitializeComponent();
      }

      private bool? twoColumns;

      private void ControlSizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout(CharacterCustomizationTab.UseTwoColumns(ActualWidth));

      /// <summary>
      /// Wide: the lists and the previews side by side, each in its own scroll viewer. Narrow: both moved into one scroll viewer, the previews under the lists.
      /// The columns are moved, not copied, so their bindings and state stay as they are.
      /// </summary>
      private void ApplyLayout(bool wide) {
         if (twoColumns == wide) return;
         twoColumns = wide;
         if (wide) {
            PageStack.Children.Clear();
            LeftColumn.Width = 452;
            RightColumn.Margin = new Thickness(2, 8, 2, 8);
            LeftScroll.Content = LeftColumn;
            RightScroll.Content = RightColumn;
            PageScroll.Visibility = Visibility.Collapsed;
            Columns.Visibility = Visibility.Visible;
         } else {
            LeftScroll.Content = null;
            RightScroll.Content = null;
            LeftColumn.Width = double.NaN; // the list cards shrink to the window (they are at most 440 wide)
            RightColumn.Margin = new Thickness(12, 0, 2, 8);
            PageStack.Children.Add(LeftColumn);
            PageStack.Children.Add(RightColumn);
            Columns.Visibility = Visibility.Collapsed;
            PageScroll.Visibility = Visibility.Visible;
         }
      }

      /// <summary>A click anywhere on a row (even in its name box) selects the row. The click still goes on to the name box.</summary>
      private void RowPressed(object sender, MouseButtonEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is CharacterColorRowItem item) item.Select.Execute(null);
      }

      /// <summary>A row that was just added (and selected) is scrolled into view.</summary>
      private void RowLoaded(object sender, RoutedEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is CharacterColorRowItem item && item.Selected) element.BringIntoView();
      }

      private void NameKeyDown(object sender, KeyEventArgs e) {
         if (sender is not TextBox box || box.DataContext is not CharacterColorRowItem item) return;
         if (e.Key == Key.Enter) {
            CommitName(box, item);
            Keyboard.ClearFocus();
            e.Handled = true;
         } else if (e.Key == Key.Escape) {
            box.Text = item.Name;
            Keyboard.ClearFocus();
            e.Handled = true;
         }
      }

      private void NameLostFocus(object sender, KeyboardFocusChangedEventArgs e) {
         if (sender is TextBox box && box.DataContext is CharacterColorRowItem item) CommitName(box, item);
      }

      /// <summary>Keeps the new name if the view model accepts it. Otherwise (it said why) the box goes back to the name in the ROM.</summary>
      private static void CommitName(TextBox box, CharacterColorRowItem item) {
         if (box.Text.Trim() == item.Name) { box.Text = item.Name; return; }
         if (!item.Rename(box.Text)) box.Text = item.Name;
      }
   }
}
