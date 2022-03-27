using System.Windows;
using System.Windows.Controls;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class AngleButton {

      #region AngleDirection

      public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(AngleDirection), typeof(AngleButton), new PropertyMetadata(AngleDirection.None));

      public AngleDirection Direction {
         get => (AngleDirection)GetValue(DirectionProperty);
         set => SetValue(DirectionProperty, value);
      }

      #endregion

      #region LeftTop

      public static readonly DependencyProperty LeftTopProperty = DependencyProperty.Register(nameof(LeftTop), typeof(Point), typeof(AngleButton), new PropertyMetadata(new Point(0, 0)));

      public Point LeftTop {
         get => (Point)GetValue(LeftTopProperty);
         set => SetValue(LeftTopProperty, value);
      }

      public static Point GetLeftTop(DependencyObject obj) => (Point)obj.GetValue(LeftTopProperty);
      public static void SetLeftTop(DependencyObject obj, Point value) => obj.SetValue(LeftTopProperty, value);

      #endregion

      #region LeftMiddle

      public static readonly DependencyProperty LeftMiddleProperty = DependencyProperty.Register(nameof(LeftMiddle), typeof(Point), typeof(AngleButton), new PropertyMetadata(new Point(0, 5)));

      public Point LeftMiddle {
         get => (Point)GetValue(LeftMiddleProperty);
         set => SetValue(LeftMiddleProperty, value);
      }

      public static Point GetLeftMiddle(DependencyObject obj) => (Point)obj.GetValue(LeftMiddleProperty);
      public static void SetLeftMiddle(DependencyObject obj, Point value) => obj.SetValue(LeftMiddleProperty, value);

      #endregion

      #region LeftBottom

      public static readonly DependencyProperty LeftBottomProperty = DependencyProperty.Register(nameof(LeftBottom), typeof(Point), typeof(AngleButton), new PropertyMetadata(new Point(0, 10)));

      public Point LeftBottom {
         get => (Point)GetValue(LeftBottomProperty);
         set => SetValue(LeftBottomProperty, value);
      }

      public static Point GetLeftBottom(DependencyObject obj) => (Point)obj.GetValue(LeftBottomProperty);
      public static void SetLeftBottom(DependencyObject obj, Point value) => obj.SetValue(LeftBottomProperty, value);

      #endregion

      #region RightTop

      public static readonly DependencyProperty RightTopProperty = DependencyProperty.Register(nameof(RightTop), typeof(Point), typeof(AngleButton), new PropertyMetadata(new Point(0, 0)));

      public Point RightTop {
         get => (Point)GetValue(RightTopProperty);
         set => SetValue(RightTopProperty, value);
      }

      public static Point GetRightTop(DependencyObject obj) => (Point)obj.GetValue(RightTopProperty);
      public static void SetRightTop(DependencyObject obj, Point value) => obj.SetValue(RightTopProperty, value);

      #endregion

      #region RightMiddle

      public static readonly DependencyProperty RightMiddleProperty = DependencyProperty.Register(nameof(RightMiddle), typeof(Point), typeof(AngleButton), new PropertyMetadata(new Point(0, 5)));

      public Point RightMiddle {
         get => (Point)GetValue(RightMiddleProperty);
         set => SetValue(RightMiddleProperty, value);
      }

      public static Point GetRightMiddle(DependencyObject obj) => (Point)obj.GetValue(RightMiddleProperty);
      public static void SetRightMiddle(DependencyObject obj, Point value) => obj.SetValue(RightMiddleProperty, value);

      #endregion

      #region RightBottom

      public static readonly DependencyProperty RightBottomProperty = DependencyProperty.Register(nameof(RightBottom), typeof(Point), typeof(AngleButton), new PropertyMetadata(new Point(0, 10)));

      public Point RightBottom {
         get => (Point)GetValue(RightBottomProperty);
         set => SetValue(RightBottomProperty, value);
      }

      public static Point GetRightBottom(DependencyObject obj) => (Point)obj.GetValue(RightBottomProperty);
      public static void SetRightBottom(DependencyObject obj, Point value) => obj.SetValue(RightBottomProperty, value);

      #endregion

      public AngleButton() => InitializeComponent();
   }
}
