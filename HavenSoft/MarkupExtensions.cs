using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace HavenSoft.View {
   /// <example>
   /// {hs:RotateTransform 60, 0, 0}
   /// </example>
   public class RotateTransformExtension : MarkupExtension {
      readonly double Angle;
      readonly double CenterX;
      readonly double CenterY;

      public RotateTransformExtension(double angle, double centerX, double centerY) {
         Angle = angle;
         CenterX = centerX;
         CenterY = centerY;
      }

      public override object ProvideValue(IServiceProvider serviceProvider) {
         return new RotateTransform(Angle, CenterX, CenterY);
      }
   }

   /// <example>
   /// {hs:Geometry 'M0,0 L0,1 1,1 1,0 Z'}
   /// </example>
   public class GeometryExtension : MarkupExtension {
      readonly string Figures;

      public GeometryExtension(string figures) { Figures = figures; }

      public override object ProvideValue(IServiceProvider serviceProvider) {
         return Geometry.Parse(Figures);
      }
   }

   /// <summary>
   /// Icons are taken from Icons.xaml
   /// Requires that there is an Icons instance stored at "Icons" in the app resources.
   /// Easiest way to ensure this is to include Resources.xaml
   /// </summary>
   /// <example>
   /// {hs:Icons Save}
   /// </example>
   public class IconExtension : MarkupExtension {
      readonly string Name;

      public IconExtension(string name) { Name = name; }

      public override object ProvideValue(IServiceProvider serviceProvider) {
         var icons = (Icons)Application.Current.FindResource("Icons");
         return icons.FindName(Name);
      }
   }
}
