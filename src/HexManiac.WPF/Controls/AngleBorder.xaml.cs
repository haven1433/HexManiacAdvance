using System.Windows;
using System.Windows.Controls;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class AngleBorder : UserControl {

      #region AngleDirection

      public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(AngleDirection), typeof(AngleBorder), new PropertyMetadata(AngleDirection.None));

      public AngleDirection Direction {
         get => (AngleDirection)GetValue(DirectionProperty);
         set => SetValue(DirectionProperty, value);
      }

      #endregion


      #region LeftTop

      public static readonly DependencyProperty LeftTopProperty = DependencyProperty.Register(nameof(LeftTop), typeof(Point), typeof(AngleBorder), new PropertyMetadata(new Point(0, 0)));

      public Point LeftTop {
         get => (Point)GetValue(LeftTopProperty);
         set => SetValue(LeftTopProperty, value);
      }

      #endregion

      #region LeftMiddle

      public static readonly DependencyProperty LeftMiddleProperty = DependencyProperty.Register(nameof(LeftMiddle), typeof(Point), typeof(AngleBorder), new PropertyMetadata(new Point(0, 5)));

      public Point LeftMiddle {
         get => (Point)GetValue(LeftMiddleProperty);
         set => SetValue(LeftMiddleProperty, value);
      }

      #endregion

      #region LeftBottom

      public static readonly DependencyProperty LeftBottomProperty = DependencyProperty.Register(nameof(LeftBottom), typeof(Point), typeof(AngleBorder), new PropertyMetadata(new Point(0, 10)));

      public Point LeftBottom {
         get => (Point)GetValue(LeftBottomProperty);
         set => SetValue(LeftBottomProperty, value);
      }

      #endregion

      #region RightTop

      public static readonly DependencyProperty RightTopProperty = DependencyProperty.Register(nameof(RightTop), typeof(Point), typeof(AngleBorder), new PropertyMetadata(new Point(0, 0)));

      public Point RightTop {
         get => (Point)GetValue(RightTopProperty);
         set => SetValue(RightTopProperty, value);
      }

      #endregion

      #region RightMiddle

      public static readonly DependencyProperty RightMiddleProperty = DependencyProperty.Register(nameof(RightMiddle), typeof(Point), typeof(AngleBorder), new PropertyMetadata(new Point(0, 5)));

      public Point RightMiddle {
         get => (Point)GetValue(RightMiddleProperty);
         set => SetValue(RightMiddleProperty, value);
      }

      #endregion

      #region RightBottom

      public static readonly DependencyProperty RightBottomProperty = DependencyProperty.Register(nameof(RightBottom), typeof(Point), typeof(AngleBorder), new PropertyMetadata(new Point(0, 10)));

      public Point RightBottom {
         get => (Point)GetValue(RightBottomProperty);
         set => SetValue(RightBottomProperty, value);
      }

      #endregion

      public AngleBorder() => InitializeComponent();
   }
}
