using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Resources;
using HavenSoft.HexManiac.WPF.Windows;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class PaletteControl : UserControl {
      private const int ExpectedElementWidth = 16, ExpectedElementHeight = 16;
      private static readonly Duration span = new Duration(TimeSpan.FromMilliseconds(100));

      private readonly Popup swatchPopup = new Popup { Placement = PlacementMode.Right, VerticalOffset = -15, PopupAnimation = PopupAnimation.Fade, AllowsTransparency = true };
      private readonly Swatch swatch = new Swatch { Width = 230, Height = 200 };
      private readonly TextBox[] swatchTextBoxes = new[] {
         new TextBox { ToolTip = "Red (0 to 31)" },
         new TextBox { ToolTip = "Green (0 to 31)" },
         new TextBox { ToolTip = "Blue (0 to 31)" },
         new TextBox { ToolTip = "Color Code (0000 to 7FFF)" },
      };

      private Point interactionPoint;
      private short[] initialColors;
      private int activeSelection;

      private PaletteCollection ViewModel => (PaletteCollection)DataContext;

      private int InteractionTileIndex {
         get {
            var elementWidth = Math.Max(1, ItemsControl.ActualWidth / ViewModel.ColorWidth);
            var elementHeight = Math.Max(1, ItemsControl.ActualHeight / ViewModel.ColorHeight);
            var x = (int)(interactionPoint.X / elementWidth);
            var y = (int)(interactionPoint.Y / elementHeight);
            var index = y * ViewModel.ColorWidth + x;
            index = Math.Min(Math.Max(0, index), ViewModel.Elements.Count - 1);
            return index;
         }
      }

      public bool LoseKeyboardFocusCausesLoseMultiSelect { get; set; }

      public PaletteControl() {
         InitializeComponent();
         swatchPopup.PlacementTarget = ItemsControl;
         swatchPopup.Child = new StackPanel {
            Children = {
               new Grid {
                  HorizontalAlignment = HorizontalAlignment.Center,
                  Children = {
                     new Rectangle { Fill = (Brush)FindResource("Background"), Opacity = .5 },
                     new TextBlock { Text = "Shift/Ctrl+Click to edit multiple colors.", FontStyle = FontStyles.Italic, Foreground = (Brush)FindResource("Secondary") },
                  },
               },
               swatch,
               new DockPanel {
                  Children = {
                     new Button {
                        Padding = new Thickness(0),
                        Content = new Path {
                           Data = IconExtension.GetIcon("EyeDropper"),
                           Fill = (Brush)FindResource("Primary"),
                           Stretch = Stretch.Uniform,
                           Width = 16,
                           Height = 16,
                        },
                        ToolTip = new ToolTip { Content = EyeDropperToolTip.Content },
                     }.Fluent(button => {
                        DockPanel.SetDock(button, Dock.Left);
                        button.Click += GrabScreenColor;
                     }),
                     new UniformGrid {
                        Columns = 4,
                        Children = {
                           swatchTextBoxes[3],
                           swatchTextBoxes[0],
                           swatchTextBoxes[1],
                           swatchTextBoxes[2],
                        },
                     },
                  },
               },
            },
         };
         LoseKeyboardFocusCausesLoseMultiSelect = true;
         Unloaded += (sender, e) => ClosePopup();
      }

      private void AppClosePopup(object sender, EventArgs e) => ClosePopup();
      public void ClosePopup() {
         swatchPopup.IsOpen = false;
         swatch.ResultChanged -= SwatchResultChanged;
         Application.Current.MainWindow.Deactivated -= AppClosePopup;
      }
      public void SingleSelect() => ViewModel?.SingleSelect();

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
         if (ViewModel == null) return;
         ViewModel.HoverIndex = -1;
      }

      private void StartPaletteColorMove(object sender, MouseButtonEventArgs e) {
         swatch.ResultChanged -= SwatchResultChanged;
         Focus();

         interactionPoint = e.GetPosition(ItemsControl);
         var elementWidth = Math.Max(1, ItemsControl.ActualWidth / ViewModel.ColorWidth);
         if (interactionPoint.X > elementWidth * ViewModel.ColorWidth || interactionPoint.X < 0) {
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
               swatchPopup.IsOpen = ViewModel.CanEditColors;
               if (swatchPopup.IsOpen) {
                  swatch.ResultChanged += SwatchResultChanged;
                  Application.Current.MainWindow.Deactivated += AppClosePopup;
                  commitTextboxChanges = true;
               }
            }
         } else {
            ClosePopup();
         }
      }

      private bool commitTextboxChanges = true;

      private void UpdateSwatchTextBoxContentFromSwatch() {
         if (!commitTextboxChanges) return;
         var color32 = (Color)ColorConverter.ConvertFromString(swatch.Result);
         var color16 = TileImage.Convert16BitColor(color32);
         var channels = Color16ToChannelStrings(color16);
         color16 = PaletteRun.FlipColorChannels(color16);
         for (int i = 0; i < channels.Length; i++) {
            swatchTextBoxes[i].TextChanged -= UpdateSwatchColorFromTextBoxes;
            swatchTextBoxes[i].Text = channels[i];
            swatchTextBoxes[i].TextChanged += UpdateSwatchColorFromTextBoxes;
         }
         swatchTextBoxes[3].TextChanged -= UpdateSwatchColorFromBytesBox;
         swatchTextBoxes[3].Text = color16.ToString("X4");
         swatchTextBoxes[3].TextChanged += UpdateSwatchColorFromBytesBox;
      }

      private void UpdateSwatchColorFromTextBoxes(object sender, TextChangedEventArgs e) {
         if (!commitTextboxChanges) return;
         commitTextboxChanges = false;
         for (int i = 0; i < 3; i++) swatchTextBoxes[i].TextChanged -= UpdateSwatchColorFromTextBoxes;

         var color16 = ChannelStringsToColor16(swatchTextBoxes.Select(box => box.Text).ToArray());
         var color32 = TileImage.Convert16BitColor(color16);
         color16 = PaletteRun.FlipColorChannels(color16);
         swatch.Result = color32.ToString();
         swatchTextBoxes[3].Text = color16.ToString("X4");

         for (int i = 0; i < 3; i++) swatchTextBoxes[i].TextChanged += UpdateSwatchColorFromTextBoxes;
         commitTextboxChanges = true;
      }

      private void UpdateSwatchColorFromBytesBox(object sender, TextChangedEventArgs e) {
         if (!commitTextboxChanges) return;
         commitTextboxChanges = false;
         swatchTextBoxes[3].TextChanged -= UpdateSwatchColorFromBytesBox;

         if (short.TryParse(swatchTextBoxes[3].Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var color16)) {
            color16 = PaletteRun.FlipColorChannels(color16);
            var color32 = TileImage.Convert16BitColor(color16);
            swatch.Result = color32.ToString();

            var channels = Color16ToChannelStrings(color16);
            for (int i = 0; i < channels.Length; i++) {
               swatchTextBoxes[i].TextChanged -= UpdateSwatchColorFromTextBoxes;
               swatchTextBoxes[i].Text = channels[i];
               swatchTextBoxes[i].TextChanged += UpdateSwatchColorFromTextBoxes;
            }
         }

         swatchTextBoxes[3].TextChanged += UpdateSwatchColorFromBytesBox;
         commitTextboxChanges = true;
      }

      private void PaletteColorMove(object sender, MouseEventArgs e) {
         if (!ViewModel.CanEditColors || isInScreenGrabMode) return;
         var oldTileIndex = InteractionTileIndex;
         interactionPoint = e.GetPosition(ItemsControl);
         var newTileIndex = InteractionTileIndex;
         var elementWidth = Math.Max(1, ItemsControl.ActualWidth / ViewModel.ColorWidth);

         if (!IsMouseCaptured) {
            ViewModel.HoverIndex = newTileIndex;
            if (interactionPoint.X < 0 || interactionPoint.X > elementWidth * ViewModel.ColorWidth) ViewModel.HoverIndex = -1;
            return;
         }

         var tilesToAnimate = ViewModel.HandleMove(oldTileIndex, newTileIndex);
         if (oldTileIndex != newTileIndex) {
            ClosePopup();
         }

         foreach (var (index, direction) in tilesToAnimate) {
            var tile = MainWindow.GetChild(ItemsControl, "PaletteColor", ViewModel.Elements[index]);
            if (!(tile.RenderTransform is TranslateTransform)) tile.RenderTransform = new TranslateTransform();
            var transform = (TranslateTransform)tile.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(elementWidth * direction, 0, span));
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

      private static string[] Color16ToChannelStrings(short color16) {
         var r = color16 >> 10;
         var g = (color16 >> 5) & 0x1F;
         var b = color16 & 0x1F;
         return new[] { r.ToString(), g.ToString(), b.ToString() };
      }

      private bool isInScreenGrabMode = false;
      private int colorIndexForScreenGrab;
      private void GrabScreenColor(object sender, EventArgs e) {
         ViewModel.SingleSelect();
         colorIndexForScreenGrab = ViewModel.SelectionStart;
         isInScreenGrabMode = true;
         CaptureMouse();
         PreviewMouseDown += GrabColorFromScreen;
      }

      private void GrabColorFromScreen(object sender, MouseButtonEventArgs e) {
         var color = DesktopColorPicker.GrabMousePixelColorFromScreen();
         var color16 = TileImage.Convert16BitColor(color);

         ViewModel.Elements[colorIndexForScreenGrab].Color = color16;
         ViewModel.PushColorsToModel();

         ReleaseMouseCapture();
         PreviewMouseDown -= GrabColorFromScreen;
         e.Handled = true;
         isInScreenGrabMode = false;
         ClosePopup();
      }

      private void ControlRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) => e.Handled = true;

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
