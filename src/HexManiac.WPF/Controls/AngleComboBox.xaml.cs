using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class AngleComboBox {
      #region AngleDirection

      public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(AngleDirection), typeof(AngleComboBox), new PropertyMetadata(AngleDirection.None));

      public AngleDirection Direction {
         get => (AngleDirection)GetValue(DirectionProperty);
         set => SetValue(DirectionProperty, value);
      }

      #endregion

      #region LeftTop

      public static readonly DependencyProperty LeftTopProperty = DependencyProperty.Register(nameof(LeftTop), typeof(Point), typeof(AngleComboBox), new PropertyMetadata(new Point(0, 0)));

      public Point LeftTop {
         get => (Point)GetValue(LeftTopProperty);
         set => SetValue(LeftTopProperty, value);
      }

      public static Point GetLeftTop(DependencyObject obj) => (Point)obj.GetValue(LeftTopProperty);
      public static void SetLeftTop(DependencyObject obj, Point value) => obj.SetValue(LeftTopProperty, value);

      #endregion

      #region LeftMiddle

      public static readonly DependencyProperty LeftMiddleProperty = DependencyProperty.Register(nameof(LeftMiddle), typeof(Point), typeof(AngleComboBox), new PropertyMetadata(new Point(0, 5)));

      public Point LeftMiddle {
         get => (Point)GetValue(LeftMiddleProperty);
         set => SetValue(LeftMiddleProperty, value);
      }

      public static Point GetLeftMiddle(DependencyObject obj) => (Point)obj.GetValue(LeftMiddleProperty);
      public static void SetLeftMiddle(DependencyObject obj, Point value) => obj.SetValue(LeftMiddleProperty, value);

      #endregion

      #region LeftBottom

      public static readonly DependencyProperty LeftBottomProperty = DependencyProperty.Register(nameof(LeftBottom), typeof(Point), typeof(AngleComboBox), new PropertyMetadata(new Point(0, 10)));

      public Point LeftBottom {
         get => (Point)GetValue(LeftBottomProperty);
         set => SetValue(LeftBottomProperty, value);
      }

      public static Point GetLeftBottom(DependencyObject obj) => (Point)obj.GetValue(LeftBottomProperty);
      public static void SetLeftBottom(DependencyObject obj, Point value) => obj.SetValue(LeftBottomProperty, value);

      #endregion

      #region RightTop

      public static readonly DependencyProperty RightTopProperty = DependencyProperty.Register(nameof(RightTop), typeof(Point), typeof(AngleComboBox), new PropertyMetadata(new Point(0, 0)));

      public Point RightTop {
         get => (Point)GetValue(RightTopProperty);
         set => SetValue(RightTopProperty, value);
      }

      public static Point GetRightTop(DependencyObject obj) => (Point)obj.GetValue(RightTopProperty);
      public static void SetRightTop(DependencyObject obj, Point value) => obj.SetValue(RightTopProperty, value);

      #endregion

      #region RightMiddle

      public static readonly DependencyProperty RightMiddleProperty = DependencyProperty.Register(nameof(RightMiddle), typeof(Point), typeof(AngleComboBox), new PropertyMetadata(new Point(0, 5)));

      public Point RightMiddle {
         get => (Point)GetValue(RightMiddleProperty);
         set => SetValue(RightMiddleProperty, value);
      }

      public static Point GetRightMiddle(DependencyObject obj) => (Point)obj.GetValue(RightMiddleProperty);
      public static void SetRightMiddle(DependencyObject obj, Point value) => obj.SetValue(RightMiddleProperty, value);

      #endregion

      #region RightBottom

      public static readonly DependencyProperty RightBottomProperty = DependencyProperty.Register(nameof(RightBottom), typeof(Point), typeof(AngleComboBox), new PropertyMetadata(new Point(0, 10)));

      public Point RightBottom {
         get => (Point)GetValue(RightBottomProperty);
         set => SetValue(RightBottomProperty, value);
      }

      public static Point GetRightBottom(DependencyObject obj) => (Point)obj.GetValue(RightBottomProperty);
      public static void SetRightBottom(DependencyObject obj, Point value) => obj.SetValue(RightBottomProperty, value);

      #endregion

      #region HasOverflow

      public static readonly DependencyProperty HasOverflowProperty = DependencyProperty.Register(nameof(HasOverflow), typeof(bool), typeof(AngleComboBox), new FrameworkPropertyMetadata(false, HasOverflowChanged));

      public bool HasOverflow {
         get => (bool)GetValue(HasOverflowProperty);
         set => SetValue(HasOverflowProperty, value);
      }

      private static void HasOverflowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (AngleComboBox)d;
         self.OnHasOverflowChanged(e);
      }

      protected virtual void OnHasOverflowChanged(DependencyPropertyChangedEventArgs e) { }

      #endregion

      public AngleComboBox() {
         DataContextChanged += (sender, e) => {
            if (e.OldValue is FilteringComboOptions oldVM) {
               oldVM.PropertyChanged -= HandleVMTextChanged;
            }
            if (e.NewValue is FilteringComboOptions newVM) {
               newVM.PropertyChanged += HandleVMTextChanged;
               if (IsTextSearchEnabled) {
                  IsTextSearchEnabled = false;
                  SetBinding(ItemsSourceProperty, nameof(newVM.FilteredOptions));
                  SetBinding(SelectedIndexProperty, nameof(newVM.SelectedIndex));
                  SetBinding(IsEditableProperty, nameof(newVM.CanFilter));
                  SetBinding(TextProperty, nameof(newVM.DisplayText));
                  SetBinding(IsDropDownOpenProperty, nameof(newVM.DropDownIsOpen));
               }
            }
         };
         InitializeComponent();
      }

      protected override void OnPreviewKeyDown(KeyEventArgs e) {
         KeyDownToViewModel(this, e);
         if (!e.Handled) base.OnPreviewKeyDown(e);
      }

      protected override void OnDropDownOpened(EventArgs e) {
         base.OnDropDownOpened(e);
         ClearSelection();
      }

      private void HandleVMTextChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName != nameof(FilteringComboOptions.DisplayText)) return;
         ClearSelection();
      }

      private void ClearSelection() {
         if (Template.FindName("PART_EditableTextBox", this) is not TextBox tb) return;
         tb.SelectionStart = tb.SelectionStart + tb.SelectionLength;
      }

      protected override void OnMouseEnter(MouseEventArgs e) {
         base.OnMouseEnter(e);
         if (Template.FindName("PART_EditableTextBox", this) is not TextBox tb) return;
         HasOverflow = tb.ExtentWidth > tb.ViewportWidth;
      }

      private void KeyDownToViewModel(object sender, KeyEventArgs e) {
         var element = (FrameworkElement)sender;
         if (element.DataContext is FilteringComboOptions vm) {
            if (e.Key == Key.Up) vm.SelectUp();
            if (e.Key == Key.Down) vm.SelectDown();
            if (e.Key == Key.Enter) vm.SelectConfirm();
            if (e.Key.IsAny(Key.Up, Key.Down, Key.Enter)) e.Handled = true;
         }
         if (element.DataContext is not IndexComboBoxViewModel viewModel) return;
         if (e.Key == Key.Enter) viewModel.CompleteFilterInteraction();
      }
   }
}
