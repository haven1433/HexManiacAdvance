using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   /// <summary>
   /// Attached behaviour: when the text box gets the keyboard focus (Tab, Shift+Tab or a click), all of its text is selected,
   /// so typing replaces the value. Used for the level boxes of the wild encounter rows: Tab, type, Tab, type...
   /// Works on a TextBox, and on an AngleTextBox (which only creates its real TextBox when it becomes active: it passes the setting on).
   /// </summary>
   public static class SelectAllOnFocus {
      public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
         "IsEnabled", typeof(bool), typeof(SelectAllOnFocus), new PropertyMetadata(false, IsEnabledChanged));

      public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
      public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

      private static void IsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         if (d is not TextBox textBox) return; // an AngleTextBox reads the setting itself when it creates its TextBox
         textBox.GotKeyboardFocus -= SelectAllText;
         textBox.PreviewMouseLeftButtonDown -= FocusWithoutMovingTheCaret;
         if (e.NewValue is not true) return;
         textBox.GotKeyboardFocus += SelectAllText;
         textBox.PreviewMouseLeftButtonDown += FocusWithoutMovingTheCaret;
      }

      private static void SelectAllText(object sender, KeyboardFocusChangedEventArgs e) {
         if (sender is TextBox textBox) textBox.SelectAll();
      }

      // a click on a text box that does not have the focus yet would move the caret to the click (and so undo the select-all):
      // take the focus here instead, which selects everything. A click in a box that already has the focus places the caret as usual.
      private static void FocusWithoutMovingTheCaret(object sender, MouseButtonEventArgs e) {
         if (sender is not TextBox textBox || textBox.IsKeyboardFocusWithin) return;
         e.Handled = true;
         textBox.Focus();
      }
   }
}
