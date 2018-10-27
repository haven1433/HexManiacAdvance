using System;
using System.Windows.Markup;
using System.Windows.Media;

using static Solarized.Brushes;
using static Solarized.Colors;

// based on Ethan Schoonover's precision colors for machines and people. http://ethanschoonover.com/solarized
namespace Solarized {
   /// <summary>
   /// At the simplest level, Solarized is a set of colors.
   /// </summary>
   public static class Colors {
      static Color Color(int value) {
         var bytes = BitConverter.GetBytes(value);
         return System.Windows.Media.Color.FromArgb(0xFF, bytes[2], bytes[1], bytes[0]);
      }

      public static readonly Color
         Base03 = Color(0x002b36),
         Base02 = Color(0x073642),
         Base01 = Color(0x586e75),
         Base00 = Color(0x657b83),
         Base0 = Color(0x839496),
         Base1 = Color(0x93a1a1),
         Base2 = Color(0xeee8d5),
         Base3 = Color(0xfdf6e3),

         Yellow = Color(0x5b8900),
         Orange = Color(0xcb4b16),
         Red = Color(0xdc322f),
         Magenta = Color(0xd33682),
         Violet = Color(0x6c71c4),
         Blue = Color(0x268bd2),
         Cyan = Color(0x2aa198),
         Green = Color(0x859900);
   }

   /// <summary>
   /// The solarized colors should also be easily available as brushes.
   /// </summary>
   public static class Brushes {
      internal static SolidColorBrush Brush(Color color) {
         var brush = new SolidColorBrush(color);
         brush.Freeze();
         return brush;
      }

      public static readonly Brush
         Yellow = Brush(Colors.Yellow),
         Orange = Brush(Colors.Orange),
         Red = Brush(Colors.Red),
         Magenta = Brush(Colors.Magenta),
         Violet = Brush(Colors.Violet),
         Blue = Brush(Colors.Blue),
         Cyan = Brush(Colors.Cyan),
         Green = Brush(Colors.Green);
   }

   /// <summary>
   /// Solarized declares two variants, Light and Dark.
   /// 5 additional brushes switch colors based on which variant you're currently using.
   /// </summary>
   public sealed class Theme {
      #region Constants

      public static readonly string Info = "http://ethanschoonover.com/solarized";

      #endregion

      static Theme() => CurrentVariant = DefaultVariant;

      #region Main Brushes

      // ideally we would use editable brushes here, so that the
      // theme could change from dark to light and everyone would
      // just update automatically. However, freezing the brush
      // gives us significant performance gains.
      public static SolidColorBrush Emphasis { get; private set; }
      public static SolidColorBrush Primary { get; private set; }
      public static SolidColorBrush Secondary { get; private set; }
      public static SolidColorBrush Backlight { get; private set; }
      public static SolidColorBrush Background { get; private set; }

      #endregion

      #region Variant Info

      public enum Variant { Dark, Light }

      static Variant variant;

      public static Variant DefaultVariant => 6 < DateTime.Now.Hour || DateTime.Now.Hour < 19 ? Variant.Light : Variant.Dark;

      public static Variant CurrentVariant {
         get => variant;
         set {
            Color current(Color a, Color b) => variant == Variant.Light ? a : b;
            variant = value;
            Emphasis = Brush(current(Base01, Base1));
            Primary = Brush(current(Base00, Base0));
            Secondary = Brush(current(Base1, Base01));
            Backlight = Brush(current(Base2, Base02));
            Background = Brush(current(Base3, Base03));
            VariantChanged?.Invoke(null, EventArgs.Empty);
         }
      }

      public static event EventHandler VariantChanged;

      #endregion
   }

   /// <summary>
   /// ThemeProperties and ThemeExtensions exist to make it easy to use solarized colors from Xaml.
   /// </summary>
   public enum ThemeProperties {
      Emphasis, Primary, Secondary, Backlight, Background,
      Yellow, Orange, Red, Magenta, Violet, Blue, Cyan, Green,
   }

   /// <example>
   /// Usage: <Border Background="{solarized:Theme Backlight}" />
   /// </example>
   public sealed class ThemeExtension : MarkupExtension {
      public ThemeProperties Target { get; set; }
      public ThemeExtension() { }
      public ThemeExtension(ThemeProperties target) => Target = target;
      public override object ProvideValue(IServiceProvider serviceProvider) {
         switch (Target) {
            case ThemeProperties.Emphasis: return Theme.Emphasis;
            case ThemeProperties.Primary: return Theme.Primary;
            case ThemeProperties.Secondary: return Theme.Secondary;
            case ThemeProperties.Backlight: return Theme.Backlight;
            case ThemeProperties.Background: return Theme.Background;

            case ThemeProperties.Yellow: return Brushes.Yellow;
            case ThemeProperties.Orange: return Brushes.Orange;
            case ThemeProperties.Red: return Brushes.Red;
            case ThemeProperties.Magenta: return Brushes.Magenta;
            case ThemeProperties.Violet: return Brushes.Violet;
            case ThemeProperties.Blue: return Brushes.Blue;
            case ThemeProperties.Cyan: return Brushes.Cyan;
            case ThemeProperties.Green: return Brushes.Green;
         }
         throw new NotImplementedException();
      }
   }
}
