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
      private readonly TextBox[] decompTextBoxes = new[] {
         new TextBox { ToolTip = "Red (0 to 255)" },
         new TextBox { ToolTip = "Green (0 to 255)" },
         new TextBox { ToolTip = "Blue (0 to 255)" },
         new TextBox { ToolTip = "Color Code (000000-FFFFFF or a color word)" },
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
                        Rows = 2,
                        Children = {
                           swatchTextBoxes[3],
                           swatchTextBoxes[0],
                           swatchTextBoxes[1],
                           swatchTextBoxes[2],
                           decompTextBoxes[3],
                           decompTextBoxes[0],
                           decompTextBoxes[1],
                           decompTextBoxes[2],
                        },
                     },
                  },
               },
            },
         };
         LoseKeyboardFocusCausesLoseMultiSelect = true;
         Unloaded += (sender, e) => ClosePopup();

         swatchTextBoxes[0].TextChanged += UpdateSwatchColorFromTextBoxes;
         swatchTextBoxes[1].TextChanged += UpdateSwatchColorFromTextBoxes;
         swatchTextBoxes[2].TextChanged += UpdateSwatchColorFromTextBoxes;
         swatchTextBoxes[3].TextChanged += UpdateSwatchColorFromBytesBox;
         decompTextBoxes[0].TextChanged += UpdateSwatchColorFromDecompTextBoxes;
         decompTextBoxes[1].TextChanged += UpdateSwatchColorFromDecompTextBoxes;
         decompTextBoxes[2].TextChanged += UpdateSwatchColorFromDecompTextBoxes;
         decompTextBoxes[3].TextChanged += UpdateSwatchColorFromDecompBytesBox;
      }

      private void AppClosePopup(object sender, EventArgs e) => ClosePopup();
      public void ClosePopup() {
         swatchPopup.IsOpen = false;
         swatch.ResultChanged -= SwatchResultChanged;
         if (Application.Current.MainWindow is Window window) {
            window.Deactivated -= AppClosePopup;
            window.LocationChanged -= AppClosePopup;
         }
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
                  Application.Current.MainWindow.LocationChanged += AppClosePopup;
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
         commitTextboxChanges = false;

         var color32 = (Color)ColorConverter.ConvertFromString(swatch.Result);
         UpdateTextBoxes(color32, ignoreSwatch: true);

         commitTextboxChanges = true;
      }

      private void UpdateSwatchColorFromTextBoxes(object sender, TextChangedEventArgs e) {
         if (!commitTextboxChanges) return;
         commitTextboxChanges = false;

         var color16 = ChannelStringsToColor16(swatchTextBoxes.Select(box => box.Text).ToArray());
         var color32 = TileImage.Convert16BitColor(color16);
         UpdateTextBoxes(color32, ignore5bitChannels: true);

         commitTextboxChanges = true;
      }

      private void UpdateSwatchColorFromBytesBox(object sender, TextChangedEventArgs e) {
         if (!commitTextboxChanges) return;
         commitTextboxChanges = false;

         if (short.TryParse(swatchTextBoxes[3].Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var color16)) {
            color16 = PaletteRun.FlipColorChannels(color16);
            var color32 = TileImage.Convert16BitColor(color16);
            UpdateTextBoxes(color32, ignore16bitColor: true);
         }

         commitTextboxChanges = true;
      }

      private void UpdateSwatchColorFromDecompTextBoxes(object sender, TextChangedEventArgs e) {
         if (!commitTextboxChanges) return;
         commitTextboxChanges = false;

         if (
            byte.TryParse(decompTextBoxes[0].Text, out var red) &&
            byte.TryParse(decompTextBoxes[1].Text, out var green) &&
            byte.TryParse(decompTextBoxes[2].Text, out var blue)
         ) {
            var color32 = Color.FromRgb(red, green, blue);
            UpdateTextBoxes(color32, ignore8bitChannels: true);
         }

         commitTextboxChanges = true;
      }

      private void UpdateSwatchColorFromDecompBytesBox(object sender, TextChangedEventArgs e) {
         if (!commitTextboxChanges) return;
         commitTextboxChanges = false;

         try {
            var color32 = (Color)ColorConverter.ConvertFromString(decompTextBoxes[3].Text);
            UpdateTextBoxes(color32, ignore32bitColor: true);
         } catch (FormatException) { // ConvertFromString can fail
            try {
               var color32 = (Color)ColorConverter.ConvertFromString("#" + decompTextBoxes[3].Text); // maybe its 6 hex characters?
               UpdateTextBoxes(color32, ignore32bitColor: true);
            } catch (FormatException) { }
         }

         commitTextboxChanges = true;
      }

      private void UpdateTextBoxes(
         Color color32,
         bool ignore5bitChannels = false,
         bool ignore8bitChannels = false,
         bool ignore16bitColor = false,
         bool ignore32bitColor = false,
         bool ignoreSwatch = false
      ) {
         var color16 = TileImage.Convert16BitColor(color32);
         var channels = Color16ToChannelStrings(color16);
         color16 = PaletteRun.FlipColorChannels(color16);

         if (!ignore5bitChannels) {
            for (int i = 0; i < channels.Length; i++) {
               swatchTextBoxes[i].Text = channels[i];
            }
         }

         if (!ignore16bitColor) {
            swatchTextBoxes[3].Text = color16.ToString("X4");
         }

         if (!ignore8bitChannels) {
            for (int i = 0; i < channels.Length; i++) {
               decompTextBoxes[i].Text = (int.Parse(channels[i]) * 255 / 31).ToString();
            }
         }

         if (!ignore32bitColor) {
            decompTextBoxes[3].Text = color32.ToString().Substring(3); // cut off the #FF at the beginning
         }

         if (!ignoreSwatch) {
            swatch.Result = color32.ToString();
         }
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
