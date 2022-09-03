using HavenSoft.HexManiac.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Resources {
   /// <example>
   /// {hs:RotateTransform 60, 0, 0}
   /// </example>
   public class RotateTransformExtension : MarkupExtension {
      public double Angle { get; }
      public double CenterX { get; }
      public double CenterY { get; }

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
      public string Figures { get; }

      public GeometryExtension(string figures) { Figures = figures; }

      public override object ProvideValue(IServiceProvider serviceProvider) {
         return Geometry.Parse(Figures);
      }
   }

   public class TextGeometryExtension : MarkupExtension {
      public string Text { get; set; }
      public Point Location { get; set; }
      public int Size { get; set; }
      public TextGeometryExtension() { }
      public TextGeometryExtension(string text, int x, int y, int size) => (Text, Location, Size) = (text, new Point(x, y), size);
      public override object ProvideValue(IServiceProvider serviceProvider) {
         var formattedText = new FormattedText(Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Calibri"), Size, Brushes.Black, 1);
         return formattedText.BuildGeometry(Location);
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
      public string Name { get; }

      public IconExtension(string name) { Name = name; }

      public override object ProvideValue(IServiceProvider serviceProvider) => GetIcon(Name);

      public static Geometry GetIcon(string name) {
         var icons = (Icons)Application.Current.FindResource("Icons");
         return (Geometry)icons.FindName(name);
      }
   }

   /// <summary>
   /// Creates a Command from a method on the ViewModel
   /// </summary>
   public class MethodCommandExtension : MarkupExtension {
      public string CommandMethod { get; }
      public MethodCommandExtension(string methodName) => CommandMethod = methodName;
      public override object ProvideValue(IServiceProvider serviceProvider) {
         var valueProvider = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
         if (valueProvider == null) return null;
         if (valueProvider.TargetObject.GetType().FullName == "System.Windows.SharedDp") {
            return this; // we're in a template: defer execution
         }
         var element = (FrameworkElement)valueProvider.TargetObject;
         var context = element.DataContext;
         var command = new MethodCommand(context, CommandMethod);
         element.DataContextChanged += (sender, e) => command.Context = element.DataContext;
         return command;
      }
   }

   public class CommandExtension : MarkupExtension {
      private static readonly Dictionary<string, RoutedCommand> commands = new();
      public string Command { get; }
      public CommandExtension(string commandName) => Command = commandName;
      public override object ProvideValue(IServiceProvider serviceProvider) {
         if (!commands.TryGetValue(Command, out var command)) {
            command = new RoutedCommand(Command, typeof(CommandExtension));
            commands[Command] = command;
         }
         return command;
      }
   }
}
