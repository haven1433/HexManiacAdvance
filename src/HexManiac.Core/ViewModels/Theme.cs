using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class Theme : ViewModelCore {
      private string primaryColor, backgroundColor;
      private double hueOffset, accentSaturation, accentValue, highlightBrightness;

      public string PrimaryColor { get => primaryColor; set { if (TryUpdate(ref primaryColor, value)) UpdateTheme(); } }
      public string BackgroundColor { get => backgroundColor; set { if (TryUpdate(ref backgroundColor, value)) UpdateTheme(); } }
      public double HueOffset { get => hueOffset; set { if (TryUpdate(ref hueOffset, value)) UpdateTheme(); } }
      public double AccentSaturation { get => accentSaturation; set { if (TryUpdate(ref accentSaturation, value)) UpdateTheme(); } }
      public double AccentValue { get => accentValue; set { if (TryUpdate(ref accentValue, value)) UpdateTheme(); } }
      public double HighlightBrightness { get => highlightBrightness; set { if (TryUpdate(ref highlightBrightness, value)) UpdateTheme(); } }

      public Theme(string[] file) {
         Reset();
         bool acceptingEntries = false;
         foreach (var entry in file) {
            var line = entry.ToLower();
            if (line.Contains("[")) acceptingEntries = false;
            if (line.Contains("[theme]")) acceptingEntries = true;
            if (!acceptingEntries) continue;

            if (line.StartsWith("primarycolor")) primaryColor = line.Split("\"".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last();
            if (line.StartsWith("backgroundcolor")) backgroundColor = line.Split("\"".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last();
            if (line.StartsWith("hueoffset")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out hueOffset);
            if (line.StartsWith("accentsaturation")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out accentSaturation);
            if (line.StartsWith("accentvalue")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out accentValue);
            if (line.StartsWith("highlightbrightness")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out highlightBrightness);
         }
         UpdateTheme();
      }

      public string[] Serialize() {
         return new[] {
            $"[Theme]",
            $"PrimaryColor = \"{PrimaryColor}\"",
            $"BackgroundColor = \"{BackgroundColor}\"",
            $"HueOffset= {HueOffset}",
            $"AccentSaturation = {AccentSaturation}",
            $"AccentValue = {AccentValue}",
            $"HighlightBrightness = {HighlightBrightness}",
         };
      }

      public void Reset() {
         TryUpdate(ref primaryColor, "#DDDDDD", nameof(PrimaryColor));
         TryUpdate(ref backgroundColor, "#222222", nameof(BackgroundColor));
         TryUpdate(ref hueOffset, 0.1, nameof(HueOffset));
         TryUpdate(ref accentSaturation, 0.7, nameof(AccentSaturation));
         TryUpdate(ref accentValue, 0.7, nameof(AccentValue));
         TryUpdate(ref highlightBrightness, 0.6, nameof(HighlightBrightness));
         UpdateTheme();
      }

      public static bool TryConvertColor(string text, out (byte r, byte g, byte b) color) {
         const string hex = "0123456789ABCDEF";
         text = text.ToUpper();
         if (text.StartsWith("#")) text = text.Substring(1);
         try {
            if (text.Length == 3) text =
               text.Substring(0, 1) + text.Substring(0, 1) +
               text.Substring(0, 2) + text.Substring(0, 2) +
               text.Substring(0, 3) + text.Substring(0, 3);
            if (text.Length == 8) text = text.Substring(2);
            if (text.Length == 6) {
               byte r = (byte)(hex.IndexOf(text[0]) * 16 + hex.IndexOf(text[1]));
               byte g = (byte)(hex.IndexOf(text[2]) * 16 + hex.IndexOf(text[3]));
               byte b = (byte)(hex.IndexOf(text[4]) * 16 + hex.IndexOf(text[5]));
               color = (r, g, b);
               return true;
            } else {
               color = default;
               return false;
            }
         } catch {
            color = default;
            return false;
         }
      }

      private void UpdateTheme() {
         if (!TryConvertColor(primaryColor, out var uiPrimary)) return;
         if (!TryConvertColor(backgroundColor, out var uiBackground)) return;
         var hsbPrimary = ToHSB(uiPrimary.r, uiPrimary.g, uiPrimary.b);
         var hsbBackground = ToHSB(uiBackground.r, uiBackground.g, uiBackground.b);

         var hsbHighlightLight = hsbPrimary;
         var hsbHighlightDark = hsbBackground;

         var brightnessTravel = .6 + .3 * highlightBrightness;
         hsbHighlightDark.sat *= .8;
         hsbHighlightDark.bright = 1 - brightnessTravel;
         if (hsbPrimary.bright < hsbBackground.bright) hsbHighlightDark.bright = brightnessTravel;
         Backlight = hsbHighlightDark.ToRgb().ToHexString();

         hsbHighlightLight.sat = 0;
         hsbHighlightLight.bright = (hsbBackground.bright + hsbPrimary.bright) / 2;
         Secondary = hsbHighlightLight.ToRgb().ToHexString();

         var accent = new List<(double hue, double sat, double bright)>();
         var saturation = accentSaturation * .8 + .2;
         var accentBrightness = accentValue * .6 + .4;
         var prototype = (hue: (hueOffset - .5) / 12, sat: saturation, bright: accentBrightness);
         for (int i = 0; i < 8; i++) {
            accent.Add(prototype);
            prototype.hue += 1 / 8.0;
         }

         Error = accent[0].ToRgb().ToHexString();
         Text1 = accent[1].ToRgb().ToHexString();
         Data1 = accent[2].ToRgb().ToHexString();
         Stream2 = accent[3].ToRgb().ToHexString();
         Data2 = accent[4].ToRgb().ToHexString();
         Accent = accent[5].ToRgb().ToHexString();
         Text2 = accent[6].ToRgb().ToHexString();
         Stream1 = accent[7].ToRgb().ToHexString();
         EditBackground = Splice(hsbHighlightDark, accent[2], .1);

         NotifyPropertyChanged(nameof(Primary));
         NotifyPropertyChanged(nameof(Background));
      }

      private string secondary, backlight;
      public string Secondary { get => secondary; set => TryUpdate(ref secondary, value); }
      public string Backlight { get => backlight; set => TryUpdate(ref backlight, value); }

      private string error, text1, text2, data1, data2, accent, stream1, stream2, editBackground;
      public string Error { get => error; set => TryUpdate(ref error, value); }
      public string Text1 { get => text1; set => TryUpdate(ref text1, value); }
      public string Text2 { get => text2; set => TryUpdate(ref text2, value); }
      public string Data1 { get => data1; set => TryUpdate(ref data1, value); }
      public string Data2 { get => data2; set => TryUpdate(ref data2, value); }
      public string Accent { get => accent; set => TryUpdate(ref accent, value); }
      public string Stream1 { get => stream1; set => TryUpdate(ref stream1, value); }
      public string Stream2 { get => stream2; set => TryUpdate(ref stream2, value); }
      public string EditBackground { get => editBackground; set => TryUpdate(ref editBackground, value); }

      public string Primary => PrimaryColor;
      public string Background => BackgroundColor;

      public static string Splice((double hue, double sat, double bright) color1, (double hue, double sat, double bright) color2, double ratio) {
         var initial = 1 - ratio;
         var hue = color2.hue;
         var sat = color2.sat * initial + color1.sat * ratio;
         var bright = color1.bright * initial + color2.bright * ratio;
         return (hue, sat, bright).ToRgb().ToHexString();
      }

      public static (byte red, byte green, byte blue) FromHSB(double hue, double sat, double bright) {
         sat = sat.LimitToRange(0, 1);
         bright = bright.LimitToRange(0, 1);
         while (hue < 0) hue += 1;
         while (hue >= 1) hue -= 1;
         var c = bright * sat;
         var hue2 = hue * 6;
         while (hue2 > 2) hue2 -= 2;
         var x = c * (1 - Math.Abs(hue2 - 1));
         var m = bright - c;

         var (r, g, b) = (0.0, 0.0, 0.0);
         if (hue < 1 / 6.0) (r, g, b) = (c, x, 0);
         else if (hue < 2 / 6.0) (r, g, b) = (x, c, 0);
         else if (hue < 3 / 6.0) (r, g, b) = (0, c, x);
         else if (hue < 4 / 6.0) (r, g, b) = (0, x, c);
         else if (hue < 5 / 6.0) (r, g, b) = (x, 0, c);
         else if (hue < 6 / 6.0) (r, g, b) = (c, 0, x);

         return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
      }

      public static (double hue, double sat, double bright) ToHSB(byte red, byte green, byte blue) {
         double r = red / 255.0;
         double g = green / 255.0;
         double b = blue / 255.0;
         var set = new[] { r, g, b };
         double max = set.Max();
         double min = set.Min();
         var delta = max - min;
         double bright = max;
         double sat = delta == 0.0 ? 0 : delta / max;
         double hue = 0;
         if (delta != 0) {
            if (max == r) hue = (g - b) / delta;
            if (max == g) hue = (b - r) / delta + 2;
            if (max == b) hue = (r - g) / delta + 4;
            while (hue > 6) hue -= 6;
            while (hue < 0) hue += 6;
            hue /= 6;
         }
         return (hue, sat, bright);
      }

      public static (byte red, byte green, byte blue) FromOklab(double lightness, double a, double b) {
         // From the creator of the color space
         // https://bottosson.github.io/posts/oklab/#converting-from-linear-srgb-to-oklab
         double lPrime = lightness + 0.3963377774 * a + 0.2158037573 * b;
         double mPrime = lightness - 0.1055613458 * a - 0.0638541728 * b;
         double sPrime = lightness - 0.0894841775 * a - 1.2914855480 * b;

         double l = lPrime * lPrime * lPrime;
         double m = mPrime * mPrime * mPrime;
         double s = sPrime * sPrime * sPrime;

         double rLinear = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
         double gLinear = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
         double bLinear = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

         // Assume the output is sRGB, meaning gamma = 2.4
         // Real original-model GBA screens have higher gamma, but most people making and playing romhacks aren't going to be playing with a GBA-like screen
         var toSRGB = (double c) => (c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055)).LimitToRange(0.0, 1.0);

         byte red = (byte)Math.Round(toSRGB(rLinear) * 255.0);
         byte green = (byte)Math.Round(toSRGB(gLinear) * 255.0);
         byte blue = (byte)Math.Round(toSRGB(bLinear) * 255.0);
         return (red, green, blue);
      }

      public static (double lightness, double a, double b) ToOklab(byte red, byte green, byte blue) {
         double r_ = red / 255.0;
         double g_ = green / 255.0;
         double b_ = blue / 255.0;

         // Assume the input is sRGB, meaning gamma = 2.4
         // Real original-model GBA screens have higher gamma, but most people making and playing romhacks aren't going to be playing with a GBA-like screen
         var toLinear = (double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
         double rLinear = toLinear(r_);
         double gLinear = toLinear(g_);
         double bLinear = toLinear(b_);

         // From the creator of the color space
         // https://bottosson.github.io/posts/oklab/#converting-from-linear-srgb-to-oklab
         double lPrime = Math.Cbrt(0.4122214708 * rLinear + 0.5363325363 * gLinear + 0.0514459929 * bLinear);
         double mPrime = Math.Cbrt(0.2119034982 * rLinear + 0.6806995451 * gLinear + 0.1073969566 * bLinear);
         double sPrime = Math.Cbrt(0.0883024619 * rLinear + 0.2817188376 * gLinear + 0.6299787005 * bLinear);

         double lightness = 0.2104542553 * lPrime + 0.7936177850 * mPrime - 0.0040720468 * sPrime;
         double a = 1.9779984951 * lPrime - 2.4285922050 * mPrime + 0.4505937099 * sPrime;
         double b = 0.0259040371 * lPrime + 0.7827717662 * mPrime - 0.8086757660 * sPrime;
         return (lightness, a, b);
      }
   }

   // failed experiment: trying to get themes to be closer aligned to Solarized Dark/Light as a baseline.
   //                    didn't work because Solarized doesn't provide enough contrast between red/orange. The accent colors in general aren't spaced evenly as hues.
   public class Theme1 : ViewModelCore {
      private string primaryColor, backgroundColor;
      private double hueOffset, accentSaturation, accentValue;

      public string PrimaryColor { get => primaryColor; set { if (TryUpdate(ref primaryColor, value)) UpdateTheme(); } }
      public string BackgroundColor { get => backgroundColor; set { if (TryUpdate(ref backgroundColor, value)) UpdateTheme(); } }
      public double HueOffset { get => hueOffset; set { if (TryUpdate(ref hueOffset, value)) UpdateTheme(); } }
      public double AccentSaturation { get => accentSaturation; set { if (TryUpdate(ref accentSaturation, value)) UpdateTheme(); } }
      public double AccentValue { get => accentValue; set { if (TryUpdate(ref accentValue, value)) UpdateTheme(); } }

      public Theme1(string[] file) {
         ResetDark();
         bool acceptingEntries = false;
         foreach (var entry in file) {
            var line = entry.ToLower();
            if (line.Contains('[')) acceptingEntries = false;
            if (line.Contains("[theme]")) acceptingEntries = true;
            if (!acceptingEntries) continue;

            if (line.StartsWith("primarycolor")) primaryColor = line.Split("\"".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last();
            if (line.StartsWith("backgroundcolor")) backgroundColor = line.Split("\"".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Last();
            if (line.StartsWith("hueoffset")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out hueOffset);
            if (line.StartsWith("accentsaturation")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out accentSaturation);
            if (line.StartsWith("accentvalue")) double.TryParse(line.Substring(line.IndexOf('=') + 1).Trim(), out accentValue);
         }
         UpdateTheme();
      }

      public string[] Serialize() {
         return new[] {
            $"[Theme]",
            $"PrimaryColor = \"{PrimaryColor}\"",
            $"BackgroundColor = \"{BackgroundColor}\"",
            $"HueOffset= {HueOffset}",
            $"AccentSaturation = {AccentSaturation}",
            $"AccentValue = {AccentValue}",
         };
      }

      public void ResetDark() {
         TryUpdate(ref primaryColor, "#DDDDDD", nameof(PrimaryColor));
         TryUpdate(ref backgroundColor, "#222222", nameof(BackgroundColor));
         ResetCore();
      }

      public void ResetSolarizedDark() {
         TryUpdate(ref primaryColor, "#839496", nameof(PrimaryColor));
         TryUpdate(ref backgroundColor, "#002b36", nameof(BackgroundColor));
         ResetCore();
      }

      public void ResetLight() {
         TryUpdate(ref primaryColor, "#222222", nameof(PrimaryColor));
         TryUpdate(ref backgroundColor, "#DDDDDD", nameof(BackgroundColor));
         ResetCore();
      }

      public void ResetSolarizedLight() {
         TryUpdate(ref primaryColor, "#657b83", nameof(PrimaryColor));
         TryUpdate(ref backgroundColor, "#fdf6e3", nameof(BackgroundColor));
         ResetCore();
      }

      private void ResetCore() {
         TryUpdate(ref hueOffset, 0.5, nameof(HueOffset));
         TryUpdate(ref accentSaturation, 0.5, nameof(AccentSaturation));
         TryUpdate(ref accentValue, 0.5, nameof(AccentValue));
         UpdateTheme();
      }

      public static bool TryConvertColor(string text, out (byte r, byte g, byte b) color) {
         const string hex = "0123456789ABCDEF";
         text = text.ToUpper();
         if (text.StartsWith("#")) text = text[1..];
         try {
            if (text.Length == 3) text =
               text[..1] + text[..1] +
               text[1..2] + text[1..2] +
               text[2..3] + text[2..3];
            if (text.Length == 8) text = text[2..];
            if (text.Length == 6) {
               byte r = (byte)(hex.IndexOf(text[0]) * 16 + hex.IndexOf(text[1]));
               byte g = (byte)(hex.IndexOf(text[2]) * 16 + hex.IndexOf(text[3]));
               byte b = (byte)(hex.IndexOf(text[4]) * 16 + hex.IndexOf(text[5]));
               color = (r, g, b);
               return true;
            } else {
               color = default;
               return false;
            }
         } catch {
            color = default;
            return false;
         }
      }

      #region Solarized Interpolation

      /*
         $base03:    #002b36;
         $base02:    #073642;
         $base01:    #586e75;
         $base00:    #657b83;
         $base0:     #839496;
         $base1:     #93a1a1;
         $base2:     #eee8d5;
         $base3:     #fdf6e3;
         $red:       #dc322f;
         $orange:    #cb4b16;
         $yellow:    #b58900;
         $magenta:   #d33682;
         $violet:    #6c71c4;
         $blue:      #268bd2;
         $cyan:      #2aa198;
         $green:     #859900;
       * Solarized Goal:
       *           Background Backlight Secondary Primary
       * Light     fdf6e3     eee8d5    93a1a1    657b83     base3  base2  base1  base00
       * Dark      002b36     073642    586e75    839496     base03 base02 base01 base0
       * 
       * Inputs:  Primary/Background
       * Outputs: Backlight/Secondary
       * 
       * Light: (65,7b,83)/(fd,f6,e3) -> (ee,e8,d5)/(93,a1,a1)
       * Dark:  (83,94,96)/(00,2b,36) -> (07,36,42)/(58,6e,75)
       * 
       */

      private static (double[] x1, double[] x2, double[] y1, double[] y2) CalculateMatrix() {
         TryConvertColor("fdf6e3", out var lightBackground);
         TryConvertColor("eee8d5", out var lightBacklight);
         TryConvertColor("93a1a1", out var lightSecondary);
         TryConvertColor("657b83", out var lightPrimary);
         TryConvertColor("002b36", out var darkBackground);
         TryConvertColor("073642", out var darkBacklight);
         TryConvertColor("586e75", out var darkSecondary);
         TryConvertColor("839496", out var darkPrimary);

         var matrixR = Solve(new[] { darkBackground.r, lightBackground.r }, new[] { darkPrimary.r, lightPrimary.r }, new[] { darkBacklight.r, lightBacklight.r }, new[] { darkSecondary.r, lightSecondary.r });
         var matrixG = Solve(new[] { darkBackground.g, lightBackground.g }, new[] { darkPrimary.g, lightPrimary.g }, new[] { darkBacklight.g, lightBacklight.g }, new[] { darkSecondary.g, lightSecondary.g });
         var matrixB = Solve(new[] { darkBackground.b, lightBackground.b }, new[] { darkPrimary.b, lightPrimary.b }, new[] { darkBacklight.b, lightBacklight.b }, new[] { darkSecondary.b, lightSecondary.b });

         var x1 = new[] { matrixR.x1, matrixG.x1, matrixB.x1 };
         var x2 = new[] { matrixR.x2, matrixG.x2, matrixB.x2 };
         var y1 = new[] { matrixR.y1, matrixG.y1, matrixB.y1 };
         var y2 = new[] { matrixR.y2, matrixG.y2, matrixB.y2 };

         return (x1, x2, y1, y2);
      }

      private static (double x1, double x2, double y1, double y2) Solve(byte[] background, byte[] foreground, byte[] highlight, byte[] secondary) {
         int dark = 0, light = 1;

         // use the seeds to find x1/x2/y1/y2
         var x1 = (foreground[dark] * highlight[light] - highlight[dark] * foreground[light]) / (double)(foreground[dark] * background[light] - background[dark] * foreground[light]);
         var x2 = (foreground[dark] * secondary[light] - secondary[dark] * foreground[light]) / (double)(foreground[dark] * background[light] - background[dark] * foreground[light]);
         var y1 = (background[dark] * highlight[light] - highlight[dark] * background[light]) / (double)(background[dark] * foreground[light] - foreground[dark] * background[light]);
         var y2 = (background[dark] * secondary[light] - secondary[dark] * background[light]) / (double)(background[dark] * foreground[light] - foreground[dark] * background[light]);

         return (x1, x2, y1, y2);
      }

      public static (byte highlight, byte secondary) CalculateDependentColors(byte background, byte foreground, (double x1, double x2, double y1, double y2) matrix) {
         var highlight = background * matrix.x1 + foreground * matrix.y1;
         var secondary = background * matrix.x2 + foreground * matrix.y2;
         return ((byte)Math.Round(highlight).LimitToRange(0, 255), (byte)Math.Round(secondary).LimitToRange(0, 255));
      }

      private static (double[] x1, double[] x2, double[] y1, double[] y2) matrix;
      public static (string highlight, string secondary) CalculateDependentColors((byte r, byte g, byte b) background, (byte r, byte g, byte b) foreground) {
         int r = 0, g = 1, b = 2;

         var resultR = CalculateDependentColors(background.r, foreground.r, (matrix.x1[r], matrix.x2[r], matrix.y1[r], matrix.y2[r]));
         var resultG = CalculateDependentColors(background.g, foreground.g, (matrix.x1[g], matrix.x2[g], matrix.y1[g], matrix.y2[g]));
         var resultB = CalculateDependentColors(background.b, foreground.b, (matrix.x1[b], matrix.x2[b], matrix.y1[b], matrix.y2[b]));
         var highlight = "#" + resultR.highlight.ToString("X2") + resultG.highlight.ToString("X2") + resultB.highlight.ToString("X2");
         var secondary = "#" + resultR.secondary.ToString("X2") + resultG.secondary.ToString("X2") + resultB.secondary.ToString("X2");
         return (highlight, secondary);
      }

      static Theme1() {
         matrix = CalculateMatrix();
      }

      #endregion

      private void UpdateTheme() {
         if (!TryConvertColor(primaryColor, out var uiPrimary)) return;
         if (!TryConvertColor(backgroundColor, out var uiBackground)) return;

         var colors = CalculateDependentColors(uiBackground, uiPrimary);
         Backlight = colors.highlight;
         Secondary = colors.secondary;

         Error = ToHSB("#dc322f").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Text1 = ToHSB("#cb4b16").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Data1 = ToHSB("#b58900").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Stream2 = ToHSB("#859900").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Data2 = ToHSB("#2aa198").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Accent = ToHSB("#268bd2").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Text2 = ToHSB("#6c71c4").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();
         Stream1 = ToHSB("#d33682").Adjust((hueOffset - .5) / 12, (accentSaturation - .5) * .6, (accentValue - .5) * .6).ToRgb().ToHexString();

         NotifyPropertyChanged(nameof(Primary));
         NotifyPropertyChanged(nameof(Background));
      }

      private string secondary, backlight;
      public string Secondary { get => secondary; set => TryUpdate(ref secondary, value); }
      public string Backlight { get => backlight; set => TryUpdate(ref backlight, value); }

      private string error, text1, text2, data1, data2, accent, stream1, stream2; //, editBackground;
      public string Error { get => error; set => TryUpdate(ref error, value); }
      public string Text1 { get => text1; set => TryUpdate(ref text1, value); }
      public string Text2 { get => text2; set => TryUpdate(ref text2, value); }
      public string Data1 { get => data1; set => TryUpdate(ref data1, value); }
      public string Data2 { get => data2; set => TryUpdate(ref data2, value); }
      public string Accent { get => accent; set => TryUpdate(ref accent, value); }
      public string Stream1 { get => stream1; set => TryUpdate(ref stream1, value); }
      public string Stream2 { get => stream2; set => TryUpdate(ref stream2, value); }


      public string Primary => PrimaryColor;
      public string Background => BackgroundColor;

      public static string Splice((double hue, double sat, double bright) color1, (double hue, double sat, double bright) color2, double ratio) {
         var initial = 1 - ratio;
         var hue = color2.hue;
         var sat = color2.sat * initial + color1.sat * ratio;
         var bright = color1.bright * initial + color2.bright * ratio;
         return (hue, sat, bright).ToRgb().ToHexString();
      }

      public static (double hue, double sat, double bright) ToHSB(string content) {
         TryConvertColor(content, out var rgb);
         return ToHSB(rgb.r, rgb.g, rgb.b);
      }

      public static (byte red, byte green, byte blue) FromHSB(double hue, double sat, double bright) {
         sat = sat.LimitToRange(0, 1);
         bright = bright.LimitToRange(0, 1);
         while (hue < 0) hue += 1;
         while (hue >= 1) hue -= 1;
         var c = bright * sat;
         var hue2 = hue * 6;
         while (hue2 > 2) hue2 -= 2;
         var x = c * (1 - Math.Abs(hue2 - 1));
         var m = bright - c;

         var (r, g, b) = (0.0, 0.0, 0.0);
         if (hue < 1 / 6.0) (r, g, b) = (c, x, 0);
         else if (hue < 2 / 6.0) (r, g, b) = (x, c, 0);
         else if (hue < 3 / 6.0) (r, g, b) = (0, c, x);
         else if (hue < 4 / 6.0) (r, g, b) = (0, x, c);
         else if (hue < 5 / 6.0) (r, g, b) = (x, 0, c);
         else if (hue < 6 / 6.0) (r, g, b) = (c, 0, x);

         return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
      }

      public static (double hue, double sat, double bright) ToHSB(byte red, byte green, byte blue) {
         double r = red / 255.0;
         double g = green / 255.0;
         double b = blue / 255.0;
         var set = new[] { r, g, b };
         double max = set.Max();
         double min = set.Min();
         var delta = max - min;
         double bright = max;
         double sat = delta == 0.0 ? 0 : delta / max;
         double hue = 0;
         if (delta != 0) {
            if (max == r) hue = (g - b) / delta;
            if (max == g) hue = (b - r) / delta + 2;
            if (max == b) hue = (r - g) / delta + 4;
            while (hue > 6) hue -= 6;
            while (hue < 0) hue += 6;
            hue /= 6;
         }
         return (hue, sat, bright);
      }
   }

   public static class Extensions {
      public static string ToHexString(this (byte red, byte green, byte blue) rgb) {
         var (red, green, blue) = rgb;
         return $"#{red:X2}{green:X2}{blue:X2}";
      }

      public static bool TryParseHex(this string hex, out int result) {
         hex = hex.Trim();
         if (hex.StartsWith("0x")) hex = hex.Substring(2);
         return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result);
      }

      public static (byte, byte, byte) ToRgb(this (double hue, double sat, double bright) hsb) => Theme.FromHSB(hsb.hue, hsb.sat, hsb.bright);

      public static (double hue, double sat, double bright) Adjust(this (double hue, double sat, double bright) hsb, double h, double s, double b) {
         var hue = hsb.hue + h;
         var sat = (hsb.sat + s).LimitToRange(0, 1);
         var bright = (hsb.bright + b).LimitToRange(0, 1);
         return (hue, sat, bright);
      }
   }
}
