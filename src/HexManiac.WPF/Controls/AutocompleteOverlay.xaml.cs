using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class AutocompleteOverlay {
      #region Target

      public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target), typeof(TextBox), typeof(AutocompleteOverlay), new FrameworkPropertyMetadata(null, TargetChanged));

      public TextBox Target {
         get => (TextBox)GetValue(TargetProperty);
         set => SetValue(TargetProperty, value);
      }

      private static void TargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (AutocompleteOverlay)d;
         self.OnTargetChanged(e);
      }

      protected virtual void OnTargetChanged(DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is TextBox oldTarget) {
            oldTarget.TextChanged -= TargetTextChanged;
            oldTarget.PreviewKeyDown -= TargetKeyDown;
            oldTarget.SelectionChanged -= TargetSelectionChanged;
            oldTarget.LostFocus -= TargetLostFocus;
         }

         if (e.NewValue is TextBox newTarget) {
            newTarget.TextChanged += TargetTextChanged;
            newTarget.PreviewKeyDown += TargetKeyDown;
            newTarget.SelectionChanged += TargetSelectionChanged;
            newTarget.LostFocus += TargetLostFocus;
         }
      }

      #endregion

      public AutocompleteOverlay() {
         InitializeComponent();
      }

      public void ClearAutocompleteOptions() {
         AutocompleteItems.ItemsSource = null;
         Visibility = Visibility.Collapsed;
         if (DataContext is StreamElementViewModel streamViewModel) streamViewModel.ClearAutocomplete();
         Application.Current.MainWindow.Deactivated -= AppClosePopup;
         Application.Current.MainWindow.LocationChanged -= AppClosePopup;
      }
      private void ShowAutocompleteOptions() {
         Visibility = Visibility.Visible;
         Application.Current.MainWindow.Deactivated += AppClosePopup;
         Application.Current.MainWindow.LocationChanged += AppClosePopup;
      }
      private void AppClosePopup(object sender, EventArgs e) => ClearAutocompleteOptions();


      private void TargetTextChanged(object sender, TextChangedEventArgs e) {
         if (e.Source != Target) return;
         if (Target.CaretIndex == 0) return;
         Func<string, int, int, IReadOnlyList<AutocompleteItem>> getAutocomplete;
         if (DataContext is ToolTray tools) {
            getAutocomplete = tools.StringTool.GetAutocomplete;
         } else if (DataContext is StreamElementViewModel streamViewModel) {
            getAutocomplete = streamViewModel.GetAutoCompleteOptions;
         } else {
            return;
         }

         var index = Target.CaretIndex;
         var lines = Target.Text.Split(Environment.NewLine);
         var lineIndex = 0;
         while (index > lines[lineIndex].Length) {
            index -= lines[lineIndex].Length + 2;
            lineIndex += 1;
         }

         var editLineIndex = Target.Text.Substring(0, Target.SelectionStart).Split(Environment.NewLine).Length;
         var totalLines = Target.Text.Split(Environment.NewLine).Length;
         var verticalOffset = Target.VerticalOffset;
         var lineHeight = Target.ExtentHeight / totalLines;
         var verticalStart = lineHeight * editLineIndex - verticalOffset + 2;

         var options = getAutocomplete(lines[lineIndex], lineIndex, index);
         if (options != null && options.Count > 0) {
            AutocompleteItems.ItemsSource = AutoCompleteSelectionItem.Generate(options, 0).ToList();
            ShowAutocompleteOptions();
         }
         var screenVertical = Target.TranslatePoint(new Point(0, verticalStart), Application.Current.MainWindow).Y;
         ScrollBorder.UpdateLayout();
         if (Application.Current.MainWindow.ActualHeight - screenVertical < 200) verticalStart -= ScrollBorder.ActualHeight + 12;
         AutocompleteTransform.Y = verticalStart;
         Popup.Reposition();

         ignoreNextSelectionChange = true;
      }

      private void TargetKeyDown(object sender, KeyEventArgs e) {
         if (!(AutocompleteItems.ItemsSource is IReadOnlyList<AutoCompleteSelectionItem> items)) return;
         if (items.Count == 0) return;
         var index = items.IndexOf(items.Single(item => item.IsSelected));
         if (e.Key == Key.Enter) {
            AutocompleteOptionChosen(items[index]);
            e.Handled = true;
         } else if (e.Key == Key.Escape) {
            ClearAutocompleteOptions();
         }

         if (e.Key == Key.Space || e.Key == Key.OemQuotes) {
            var caretIndex = Target.CaretIndex;
            var lines = Target.Text.Split(Environment.NewLine);
            var lineIndex = 0;
            while (caretIndex > lines[lineIndex].Length) {
               caretIndex -= lines[lineIndex].Length + 2;
               lineIndex += 1;
            }
            var quoteCount = lines[lineIndex].Substring(0, caretIndex).Count(c => c == '"');

            if (e.Key == Key.Space && quoteCount % 2 == 0) {
               AutocompleteOptionChosen(items[index]);
               e.Handled = true;
            } else if (e.Key == Key.OemQuotes && quoteCount % 2 == 1) {
               AutocompleteOptionChosen(items[index]);
               e.Handled = true;
            }
         }

         if (e.Key == Key.Up) {
            index -= 1;
            if (index == -1) index = items.Count - 1;
         } else if (e.Key == Key.Down) {
            index += 1;
            if (index == items.Count) index = 0;
         } else {
            return;
         }

         var models = items.Select(item => new AutocompleteItem(item.DisplayText, item.CompletionText));
         AutocompleteItems.ItemsSource = AutoCompleteSelectionItem.Generate(models, index).ToList();
         e.Handled = true;
      }

      private bool ignoreNextSelectionChange = false;
      private void TargetSelectionChanged(object sender, RoutedEventArgs e) {
         if (!ignoreNextSelectionChange) {
            ClearAutocompleteOptions();
         }
         ignoreNextSelectionChange = false;
      }

      private void TargetLostFocus(object sender, EventArgs e) => ClearAutocompleteOptions();

      private void AutocompleteOptionChosen(object sender, RoutedEventArgs e) {
         if (!(sender is FrameworkElement element)) return;
         if (!(element.DataContext is AutoCompleteSelectionItem item)) return;
         AutocompleteOptionChosen(item);
      }

      private void AutocompleteOptionChosen(AutoCompleteSelectionItem item) {
         var oldCaretIndex = Target.CaretIndex;

         var index = Target.CaretIndex;
         var lines = Target.Text.Split(Environment.NewLine);
         var lineIndex = 0;
         while (index > lines[lineIndex].Length) {
            index -= lines[lineIndex].Length + 2;
            lineIndex += 1;
         }

         var oldLineLength = lines[lineIndex].Length;
         lines[lineIndex] = item.CompletionText;
         var newLineLength = lines[lineIndex].Length;

         Target.Text = Environment.NewLine.Join(lines);
         Target.CaretIndex = oldCaretIndex + newLineLength - oldLineLength;
         ClearAutocompleteOptions();
      }
   }
}
