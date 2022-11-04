using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TutorialControl {
      private static readonly DoubleAnimation
         enterControlAnimation = new(-300, Duration(.2)),
         exitControlAnimaiton = new(0, Duration(.2));

      private const double ElementHeight = 90;

      public event EventHandler<MapTutorialViewModel> EnterTutorial;

      public MapTutorialsViewModel ViewModel => DataContext as MapTutorialsViewModel;

      #region Constructor

      public TutorialControl() {
         InitializeComponent();
         DataContextChanged += UpdateTutorialHandlers;
      }

      private void UpdateTutorialHandlers(object sender, DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is MapTutorialsViewModel oldVM) {
            foreach (var tut in oldVM.Tutorials) {
               tut.PropertyChanged -= HandleTutorialChanged;
               tut.AnimateMovement -= HandleTutorialChanged;
            }
            oldVM.CompletedTutorial -= ShowCheck;
         }
         if (e.NewValue is MapTutorialsViewModel newVM) {
            foreach (var tut in newVM.Tutorials) {
               tut.PropertyChanged += HandleTutorialChanged;
               tut.AnimateMovement += HandleTutorialChanged;
            }
            newVM.CompletedTutorial += ShowCheck;
         }
      }

      #endregion

      protected override void OnMouseEnter(MouseEventArgs e) {
         base.OnMouseEnter(e);
         Tutorials.BeginAnimation(Canvas.LeftProperty, enterControlAnimation);
      }

      protected override void OnMouseLeave(MouseEventArgs e) {
         base.OnMouseLeave(e);
         Tutorials.BeginAnimation(Canvas.LeftProperty, exitControlAnimaiton);
      }

      private void OnEnterTutorial(object sender, EventArgs e) {
         var element = (FrameworkElement)sender;
         if (element.DataContext is not MapTutorialViewModel tutorial) return;
         EnterTutorial.Raise(this, tutorial);
      }

      private void HandleTutorialChanged(object sender, EventArgs e) {
         var tut = (MapTutorialViewModel)sender;
         var ui = (FrameworkElement)Tutorials.ItemContainerGenerator.ContainerFromItem(tut);
         if (ui == null) return;
         ui.Visibility = Visibility.Visible;
         ui.AnimateTop(tut.TargetPosition * ElementHeight);
         ui.AnimateOpacity(double.NaN, tut.Incomplete ? 1 : 0, 0);
         ui.AnimateLeft(tut.Incomplete ? 0 : 100);
      }

      private void ShowCheck(object sender, EventArgs e) {
         var tut = (MapTutorialViewModel)sender;
         var ui = (FrameworkElement)Tutorials.ItemContainerGenerator.ContainerFromItem(tut);
         if (ui == null) return;
         Canvas.SetTop(Check, tut.TopEdge + 15);
         var offset = 300 + Canvas.GetLeft(Tutorials);
         Check.AnimateRight(ui.ActualWidth / 2 - offset, ui.ActualWidth * 1.2 - offset);
         Check.AnimateOpacity(1, 0, 1);
      }

      private static Duration Duration(double seconds) => new(TimeSpan.FromSeconds(seconds));
   }

   public static class AnimationExtensions {
      private static readonly Duration Time = new(TimeSpan.FromSeconds(.4));

      public static void AnimateTop(this FrameworkElement element, double position) {
         var lag = Math.Min(1, position / 450);
         var rush = 1 - lag;
         element.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(position, Time) {
            DecelerationRatio = rush,
            AccelerationRatio = lag
         });
      }

      public static void AnimateOpacity(this FrameworkElement element, double from, double to, double acceleration) {
         var animation = new DoubleAnimation(to, Time) { AccelerationRatio = acceleration };
         if (!double.IsNaN(from)) animation.From = from;
         element.BeginAnimation(UIElement.OpacityProperty, animation);
      }

      public static void AnimateLeft(this FrameworkElement element, double position) {
         element.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(position, Time));
      }

      public static void AnimateRight(this FrameworkElement element, double from, double to) {
         element.BeginAnimation(Canvas.RightProperty, new DoubleAnimation(from, to, Time) { DecelerationRatio = 1 });
      }
   }
}
