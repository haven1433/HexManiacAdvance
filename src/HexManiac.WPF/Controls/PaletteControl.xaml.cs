using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Windows;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class PaletteControl : UserControl {
      private const int ExpectedElementWidth = 16, ExpectedElementHeight = 16;
      private static readonly Duration span = new Duration(TimeSpan.FromMilliseconds(100));

      private readonly Popup swatchPopup = new Popup { Placement = PlacementMode.Bottom, PopupAnimation = PopupAnimation.Fade, AllowsTransparency = true };
      private readonly Swatch swatch = new Swatch { Width = 230, Height = 200 };

      private Point interactionPoint;
      private short[] initialColors;
      private int activeSelection;

      private PaletteCollection ViewModel => (PaletteCollection)DataContext;

      private int InteractionTileIndex {
         get {
            var x = (int)(interactionPoint.X / ExpectedElementWidth);
            var y = (int)(interactionPoint.Y / ExpectedElementHeight);
            var index = y * ViewModel.ColorWidth + x;
            index = Math.Min(Math.Max(0, index), ViewModel.Elements.Count - 1);
            return index;
         }
      }

      public PaletteControl() {
         InitializeComponent();
         swatchPopup.PlacementTarget = this;
         swatchPopup.Child = swatch;
      }

      protected override void OnLostFocus(RoutedEventArgs e) {
         swatchPopup.IsOpen = false;
         swatch.ResultChanged -= SwatchResultChanged;
         ViewModel.SelectionStart = -1;
         base.OnLostFocus(e);
      }

      protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
         swatchPopup.IsOpen = false;
         swatch.ResultChanged -= SwatchResultChanged;
         base.OnLostKeyboardFocus(e);
      }

      private void StartPaletteColorMove(object sender, MouseButtonEventArgs e) {
         swatch.ResultChanged -= SwatchResultChanged;
         Focus();

         interactionPoint = e.GetPosition(this);
         var tileIndex = InteractionTileIndex;

         if (Keyboard.Modifiers == ModifierKeys.Shift) {
            ViewModel.SelectionEnd = tileIndex;
         } else if (ViewModel.SelectionStart == tileIndex && e.LeftButton == MouseButtonState.Pressed && ViewModel.SelectionEnd == tileIndex && swatchPopup.IsOpen) {
            e.Handled = true;
            swatchPopup.IsOpen = false;
            return;
         } else {
            ViewModel.SelectionStart = tileIndex;
         }

         CaptureMouse();
         e.Handled = true;

         if (Keyboard.Modifiers != ModifierKeys.Shift) {
            swatch.Result = ColorFor(tileIndex);
            initialColors = CollectColorList();
            activeSelection = tileIndex;
            if (e.LeftButton == MouseButtonState.Pressed) {
               swatchPopup.IsOpen = true;
               swatch.ResultChanged += SwatchResultChanged;
            }
         } else {
            swatchPopup.IsOpen = false;
         }
      }

      private void PaletteColorMove(object sender, MouseEventArgs e) {
         if (!IsMouseCaptured) return;

         var oldTileIndex = InteractionTileIndex;
         interactionPoint = e.GetPosition(this);
         var newTileIndex = InteractionTileIndex;

         var tilesToAnimate = ViewModel.HandleMove(oldTileIndex, newTileIndex);
         if (oldTileIndex != newTileIndex) {
            swatch.ResultChanged -= SwatchResultChanged;
            swatchPopup.IsOpen = false;
         }

         foreach (var (index, direction) in tilesToAnimate) {
            var tile = MainWindow.GetChild(ItemsControl, "PaletteColor", ViewModel.Elements[index]);
            if (!(tile.RenderTransform is TranslateTransform)) tile.RenderTransform = new TranslateTransform();
            var transform = (TranslateTransform)tile.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ExpectedElementWidth * direction, 0, span));
         }
      }

      private void EndPaletteColorMove(object sender, MouseButtonEventArgs e) {
         if (!IsMouseCaptured) return;
         ReleaseMouseCapture();

         ViewModel.CompleteCurrentInteraction();
      }

      private short[] CollectColorList() => ViewModel.Elements.Select(element => element.Color).ToArray();

      private (double hueDif, double satDif, double brightDif) GetColorDif(Color newColor) {
         var oldColor = TileImage.Convert16BitColor(initialColors[activeSelection]);
         var newHSB = Theme.ToHSB(newColor.R, newColor.G, newColor.B);
         var oldHSB = Theme.ToHSB(oldColor.R, oldColor.G, oldColor.B);
         var hueDif = newHSB.hue - oldHSB.hue;
         var satDif = newHSB.sat - oldHSB.sat;
         var brightDif = newHSB.bright - oldHSB.bright;
         return (hueDif, satDif, brightDif);
      }

      /// <summary>
      /// Grabs the initial color at index and applies a HSB dif to it, returning the new short color
      /// </summary>
      private short ApplyDif(int index, (double hueDif, double satDif, double brightDif) colorDif) {
         var originalColor = TileImage.Convert16BitColor(initialColors[index]);
         var hsb = Theme.ToHSB(originalColor.R, originalColor.G, originalColor.B);
         hsb.hue += colorDif.hueDif;
         hsb.sat += colorDif.satDif;
         hsb.bright += colorDif.brightDif;
         var currentRGB = Theme.FromHSB(hsb.hue, hsb.sat, hsb.bright);
         var newColor = Color.FromRgb(currentRGB.red, currentRGB.green, currentRGB.blue);
         return TileImage.Convert16BitColor(newColor);
      }

      private void SwatchResultChanged(object sender, string oldValue) {
         var newColor = (Color)ColorConverter.ConvertFromString(swatch.Result);
         var dif = GetColorDif(newColor);

         // concern: this might not work well, since it's a diff of a diff of a diff.
         var left = Math.Min(ViewModel.SelectionStart, ViewModel.SelectionEnd);
         var right = Math.Max(ViewModel.SelectionStart, ViewModel.SelectionEnd);
         for (int i = left; i <= right; i++) {
            if (i == activeSelection) {
               ViewModel.Elements[i].Color = TileImage.Convert16BitColor(newColor);
               continue;
            }

            ViewModel.Elements[i].Color = ApplyDif(i, dif);
         }

         ViewModel.PushColorsToModel();
      }

      private static readonly ColorConverter colorConverter = new ColorConverter();
      private string ColorFor(int tileIndex) {
         var color = TileImage.Convert16BitColor(ViewModel.Elements[tileIndex].Color);
         var colorString = colorConverter.ConvertToString(color);
         return colorString;
      }
   }
}
