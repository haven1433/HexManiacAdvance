using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Xml.Linq;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TutorialControl {
      private static readonly DoubleAnimation
         enterControlAnimation = new(-400, Duration(.2)),
         exitControlAnimaiton = new(0, Duration(.2));

      private const double ElementHeight = 100;

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
         }
         if (e.NewValue is MapTutorialsViewModel newVM) {
            foreach (var tut in newVM.Tutorials) {
               tut.PropertyChanged += HandleTutorialChanged;
               tut.AnimateMovement += HandleTutorialChanged;
            }
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

      private void HandleTutorialChanged(object sender, EventArgs e) {
         var tut = (MapTutorialViewModel)sender;
         var ui = (FrameworkElement)Tutorials.ItemContainerGenerator.ContainerFromItem(tut);
         if (ui == null) return;
         ui.AnimateTop(tut.TargetPosition * ElementHeight);
         ui.AnimateOpacity(tut.Incomplete ? 1 : 0);
         ui.AnimateLeft(tut.Incomplete ? 0 : 100);
      }

      private static Duration Duration(double seconds) => new(TimeSpan.FromSeconds(seconds));
   }

   public static class AnimationExtensions {
      private static readonly Duration Time = new(TimeSpan.FromSeconds(.4));

      public static void AnimateTop(this FrameworkElement element, double position) {
         var lag = Math.Min(1, position / 500);
         var rush = 1 - lag;
         element.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(position, Time) {
            DecelerationRatio = rush,
            AccelerationRatio = lag
         });
      }

      public static void AnimateOpacity(this FrameworkElement element, double opacity) {
         element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(opacity, Time));
      }

      public static void AnimateLeft(this FrameworkElement element, double position) {
         element.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(position, Time));
      }
   }
}
