using HavenSoft.HexManiac.Core;
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
      private readonly TextBox[] swatchTextBoxes = new[] {
         new TextBox { ToolTip = "Red (0 to 31)" },
         new TextBox { ToolTip = "Green (0 to 31)" },
         new TextBox { ToolTip = "Blue (0 to 31)" },
      };

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

      public bool LoseKeyboardFocusCausesLoseMultiSelect { get; set; }

      public PaletteControl() {
         InitializeComponent();
         swatchPopup.PlacementTarget = this;
         swatchPopup.Child = new StackPanel {
            Children = {
               swatch,
               new UniformGrid {
                  Columns = 3,
                  Children = {
                     swatchTextBoxes[0],
                     swatchTextBoxes[1],
                     swatchTextBoxes[2],
                  },
               },
            },
         };
         LoseKeyboardFocusCausesLoseMultiSelect = true;
      }

      public void ClosePopup() {
         swatchPopup.IsOpen = false;
         swatch.ResultChanged -= SwatchResultChanged;
      }
      public void SingleSelect() => ViewModel.SingleSelect();

      protected override void OnLostFocus(RoutedEventArgs e) {
         if (swatchPopup.IsKeyboardFocusWithin) return;
         ClosePopup();
         if (LoseKeyboardFocusCausesLoseMultiSelect) SingleSelect();
         base.OnLostFocus(e);
      }

      protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
         if (swatchPopup.IsKeyboardFocusWithin) return;
         ClosePopup();

         base.OnLostKeyboardFocus(e);
      }

      protected override void OnMouseLeave(MouseEventArgs e) {
         base.OnMouseLeave(e);
         ViewModel.HoverIndex = -1;
      }

      private void StartPaletteColorMove(object sender, MouseButtonEventArgs e) {
         swatch.ResultChanged -= SwatchResultChanged;
         Focus();

         interactionPoint = e.GetPosition(ItemsControl);
         if (interactionPoint.X > ExpectedElementWidth * ViewModel.ColorWidth || interactionPoint.X < 0) {
            ClosePopup();
            return;
         }
         var tileIndex = InteractionTileIndex;

         if (Keyboard.Modifiers == ModifierKeys.Shift) {
            ViewModel.SelectionEnd = tileIndex;
         } else if (Keyboard.Modifiers == ModifierKeys.Control)  {
            ViewModel.ToggleSelection(tileIndex);
         } else if (ViewModel.Elements[tileIndex].Selected && e.LeftButton == MouseButtonState.Pressed && swatchPopup.IsOpen) {
            e.Handled = true;
            ClosePopup();
            return;
         } else {
            ViewModel.SelectionStart = tileIndex;
         }

         CaptureMouse();
         e.Handled = true;

         if (Keyboard.Modifiers != ModifierKeys.Shift && Keyboard.Modifiers != ModifierKeys.Control) {
            swatch.Result = Color32For(tileIndex);
            UpdateSwatchTextBoxContentFromSwatch();
            initialColors = CollectColorList();
            activeSelection = tileIndex;
            if (e.LeftButton == MouseButtonState.Pressed) {
               swatchPopup.IsOpen = true;
               swatch.ResultChanged += SwatchResultChanged;
            }
         } else {
            ClosePopup();
         }
      }

      private void UpdateSwatchTextBoxContentFromSwatch() {
         var color32 = (Color)ColorConverter.ConvertFromString(swatch.Result);
         var color16 = TileImage.Convert16BitColor(color32);
         var channels = Color16ToChannelStrings(color16);
         for (int i = 0; i < channels.Length; i++) {
            swatchTextBoxes[i].TextChanged -= UpdateSwatchColorFromTextBoxes;
            swatchTextBoxes[i].Text = channels[i];
            swatchTextBoxes[i].TextChanged += UpdateSwatchColorFromTextBoxes;
         }
      }

      private void UpdateSwatchColorFromTextBoxes(object sender, TextChangedEventArgs e) {
         for (int i = 0; i < swatchTextBoxes.Length; i++) swatchTextBoxes[i].TextChanged -= UpdateSwatchColorFromTextBoxes;

         var color16 = ChannelStringsToColor16(swatchTextBoxes.Select(box => box.Text).ToArray());
         var color32 = TileImage.Convert16BitColor(color16);
         swatch.Result = color32.ToString();

         for (int i = 0; i < swatchTextBoxes.Length; i++) swatchTextBoxes[i].TextChanged += UpdateSwatchColorFromTextBoxes;
      }

      private void PaletteColorMove(object sender, MouseEventArgs e) {
         var oldTileIndex = InteractionTileIndex;
         interactionPoint = e.GetPosition(ItemsControl);
         var newTileIndex = InteractionTileIndex;

         if (!IsMouseCaptured) {
            ViewModel.HoverIndex = newTileIndex;
            if (interactionPoint.X < 0 || interactionPoint.X > ExpectedElementWidth * ViewModel.ColorWidth) ViewModel.HoverIndex = -1;
            return;
         }

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
         var (hue, sat, bright) = Theme.ToHSB(newColor.R, newColor.G, newColor.B);
         var (oldHue, oldSat, oldBright) = Theme.ToHSB(oldColor.R, oldColor.G, oldColor.B);
         return (hue - oldHue, sat - oldSat, bright - oldBright);
      }

      /// <summary>
      /// Grabs the initial color at index and applies a HSB dif to it, returning the new short color
      /// </summary>
      private short ApplyDif(int index, (double hueDif, double satDif, double brightDif) colorDif) {
         var originalColor = TileImage.Convert16BitColor(initialColors[index]);
         var (hue, sat, bright) = Theme.ToHSB(originalColor.R, originalColor.G, originalColor.B);
         hue += colorDif.hueDif;
         sat += colorDif.satDif;
         bright += colorDif.brightDif;
         var (red, green, blue) = Theme.FromHSB(hue, sat, bright);
         var newColor = Color.FromRgb(red, green, blue);
         return TileImage.Convert16BitColor(newColor);
      }

      private void SwatchResultChanged(object sender, string oldValue) {
         var newColor = (Color)ColorConverter.ConvertFromString(swatch.Result);
         var dif = GetColorDif(newColor);

         // concern: this might not work well, since it's a diff of a diff of a diff.
         for (int i = 0; i < ViewModel.Elements.Count; i++) {
            if (!ViewModel.Elements[i].Selected) continue;
            if (i == activeSelection) {
               ViewModel.Elements[i].Color = TileImage.Convert16BitColor(newColor);
               continue;
            }

            ViewModel.Elements[i].Color = ApplyDif(i, dif);
         }

         UpdateSwatchTextBoxContentFromSwatch();

         ViewModel.PushColorsToModel();
      }

      private static readonly ColorConverter colorConverter = new ColorConverter();

      private void ShowElementPopup(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         ((ToolTip)element.ToolTip).IsOpen = true;
      }

      private void HideElementPopup(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         ((ToolTip)element.ToolTip).IsOpen = false;
      }

      private string Color32For(int tileIndex) {
         var color = TileImage.Convert16BitColor(ViewModel.Elements[tileIndex].Color);
         var colorString = colorConverter.ConvertToString(color);
         return colorString;
      }

      private string[] Color16ToChannelStrings(short color16) {
         var r = color16 >> 10;
         var g = (color16 >> 5) & 0x1F;
         var b = color16 & 0x1F;
         return new[] { r.ToString(), g.ToString(), b.ToString() };
      }

      private short ChannelStringsToColor16(string[] channels) {
         int.TryParse(channels[0], out int r);
         int.TryParse(channels[1], out int g);
         int.TryParse(channels[2], out int b);
         r = r.LimitToRange(0, 0x1F);
         g = g.LimitToRange(0, 0x1F);
         b = b.LimitToRange(0, 0x1F);
         var color = (r << 10) + (g << 5) + b;
         return (short)color;
      }
   }
}
