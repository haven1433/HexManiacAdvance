using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public enum AngleDirection {
      None,
      Left,
      Out,
      In,
      Right,
   }

   public partial class AngleTextBox {

      private static readonly Thickness TextContentThickness = new(0, 1, 0, 1);
      private ContextMenu menu;

      private bool keepTextBoxForContextMenu;
      public TextBox GetTextBox() {
         keepTextBoxForContextMenu = true;
         UpdateFieldTextBox(default, default);
         return (TextBox)Content;
      }

      private Binding textBinding;
      public Binding TextBinding {
         get => textBinding;
         set {
            textBinding = value;
            RefreshProxy();
         }
      }

      #region AngleDirection

      public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(AngleDirection), typeof(AngleTextBox), new PropertyMetadata(AngleDirection.None));

      public AngleDirection Direction {
         get => (AngleDirection)GetValue(DirectionProperty);
         set => SetValue(DirectionProperty, value);
      }

      #endregion

      #region LeftTop

      public static readonly DependencyProperty LeftTopProperty = DependencyProperty.Register(nameof(LeftTop), typeof(Point), typeof(AngleTextBox), new PropertyMetadata(new Point(0, 0)));

      public Point LeftTop {
         get => (Point)GetValue(LeftTopProperty);
         set => SetValue(LeftTopProperty, value);
      }

      public static Point GetLeftTop(DependencyObject obj) => (Point)obj.GetValue(LeftTopProperty);
      public static void SetLeftTop(DependencyObject obj, Point value) => obj.SetValue(LeftTopProperty, value);

      #endregion

      #region LeftMiddle

      public static readonly DependencyProperty LeftMiddleProperty = DependencyProperty.Register(nameof(LeftMiddle), typeof(Point), typeof(AngleTextBox), new PropertyMetadata(new Point(0,5)));

      public Point LeftMiddle {
         get => (Point)GetValue(LeftMiddleProperty);
         set => SetValue(LeftMiddleProperty, value);
      }

      public static Point GetLeftMiddle(DependencyObject obj) => (Point)obj.GetValue(LeftMiddleProperty);
      public static void SetLeftMiddle(DependencyObject obj, Point value) => obj.SetValue(LeftMiddleProperty, value);

      #endregion

      #region LeftBottom

      public static readonly DependencyProperty LeftBottomProperty = DependencyProperty.Register(nameof(LeftBottom), typeof(Point), typeof(AngleTextBox), new PropertyMetadata(new Point(0, 10)));

      public Point LeftBottom {
         get => (Point)GetValue(LeftBottomProperty);
         set => SetValue(LeftBottomProperty, value);
      }

      public static Point GetLeftBottom(DependencyObject obj) => (Point)obj.GetValue(LeftBottomProperty);
      public static void SetLeftBottom(DependencyObject obj, Point value) => obj.SetValue(LeftBottomProperty, value);

      #endregion

      #region RightTop

      public static readonly DependencyProperty RightTopProperty = DependencyProperty.Register(nameof(RightTop), typeof(Point), typeof(AngleTextBox), new PropertyMetadata(new Point(0, 0)));

      public Point RightTop {
         get => (Point)GetValue(RightTopProperty);
         set => SetValue(RightTopProperty, value);
      }

      public static Point GetRightTop(DependencyObject obj) => (Point)obj.GetValue(RightTopProperty);
      public static void SetRightTop(DependencyObject obj, Point value) => obj.SetValue(RightTopProperty, value);

      #endregion

      #region RightMiddle

      public static readonly DependencyProperty RightMiddleProperty = DependencyProperty.Register(nameof(RightMiddle), typeof(Point), typeof(AngleTextBox), new PropertyMetadata(new Point(0, 5)));

      public Point RightMiddle {
         get => (Point)GetValue(RightMiddleProperty);
         set => SetValue(RightMiddleProperty, value);
      }

      public static Point GetRightMiddle(DependencyObject obj) => (Point)obj.GetValue(RightMiddleProperty);
      public static void SetRightMiddle(DependencyObject obj, Point value) => obj.SetValue(RightMiddleProperty, value);

      #endregion

      #region RightBottom

      public static readonly DependencyProperty RightBottomProperty = DependencyProperty.Register(nameof(RightBottom), typeof(Point), typeof(AngleTextBox), new PropertyMetadata(new Point(0, 10)));

      public Point RightBottom {
         get => (Point)GetValue(RightBottomProperty);
         set => SetValue(RightBottomProperty, value);
      }

      public static Point GetRightBottom(DependencyObject obj) => (Point)obj.GetValue(RightBottomProperty);
      public static void SetRightBottom(DependencyObject obj, Point value) => obj.SetValue(RightBottomProperty, value);

      #endregion

      public AngleTextBox() => InitializeComponent();

      /// <summary>
      /// TextBlock is a lot faster than TextBox.
      /// And we only ever really need to have the focus in a single textbox at a time.
      /// Therefore, for performance reasons, we'd rather have all the non-active textboxes just
      /// *look* like TextBoxes, and really be TextBlocks instead.
      /// </summary>
      private void UpdateFieldTextBox(object sender, RoutedEventArgs e) {
         var isActive = IsMouseOver || IsFocused || IsKeyboardFocusWithin || keepTextBoxForContextMenu;
         isActive |= Content is TextBox tb && tb.ContextMenu != null && tb.ContextMenu.IsOpen;
         if (isActive && Content is TextBoxLookAlike) {
            var textBox = new TextBox {
               UndoLimit = 0,
               BorderThickness = TextContentThickness,
               VerticalAlignment = VerticalAlignment.Stretch,
            };
            if (ContextMenu != null) {
               menu = ContextMenu;
               ContextMenu = null;
            }
            if (menu != null) textBox.ContextMenu = menu;

            if (DataContext is FieldArrayElementViewModel) textBox.InputBindings.Add(
               new KeyBinding {
                  Key = Key.Enter,
                  Command = new MethodCommand(DataContext, nameof(FieldArrayElementViewModel.Accept))
               });
            if (textBinding != null) {
               textBox.SetBinding(TextBox.TextProperty, textBinding);
            } else {
               textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(FieldArrayElementViewModel.Content)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            }
            Content = textBox;
            textBox.ContextMenuOpening += (sender, args) => keepTextBoxForContextMenu = true;
            textBox.ContextMenuClosing += (sender, args) => keepTextBoxForContextMenu = false;
            if (IsKeyboardFocused) {
               textBox.Loaded += HandleTextboxLoaded;
            } else {
               Focusable = false;
            }
         } else if (!isActive && Content is TextBox) {
            RefreshProxy();
         }
      }

      private void RefreshProxy() {
         var proxy = new TextBoxLookAlike { BorderThickness = TextContentThickness, VerticalAlignment = VerticalAlignment.Stretch };
         if (textBinding != null) proxy.TextBlock.SetBinding(TextBlock.TextProperty, textBinding);
         Content = proxy;
         Focusable = true;
      }

      private void HandleTextboxLoaded(object sender, RoutedEventArgs e) {
         var textBox = (TextBox)sender;
         Keyboard.Focus(textBox);
         Focusable = false;
      }
   }
}
