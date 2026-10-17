using HavenSoft.HexManiac.Core.ViewModels;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The text boxes of the app (code tool, trainer team, ...) paint a small square over the place where the two scroll bars of the box meet.
   /// That square exists only while both bars are showing. It used to be decided when the text changed, from the sizes of the previous text,
   /// and then stayed: a dark 16x17 square appeared in the corner of the trainer team box (no scroll bar at all).
   /// </summary>
   public class TextEditorScrollCornerTests {
      // what the old code decided at the moment the text changed: the new text's width against the OLD viewport and extent (these are the values WPF still reports then)
      private static bool OldDecisionWithStaleSizes(double newTextWidth, double oldViewportWidth, double oldViewportHeight, double oldExtentHeight) =>
         newTextWidth > oldViewportWidth && oldViewportHeight > oldExtentHeight;

      [Fact]
      public void TextThatFitsInTheBox_NoCover() {
         // the trainer team of the screenshot: two short lines in a 158x140 box, no scroll bar anywhere
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 158, viewportWidth: 158, extentHeight: 60, viewportHeight: 140));
      }

      [Fact]
      public void OnlyTheHorizontalBarShowing_NoCover() {
         // there is no corner when the vertical bar is missing: painting there would hide the end of the horizontal bar
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 400, viewportWidth: 158, extentHeight: 60, viewportHeight: 123));
      }

      [Fact]
      public void OnlyTheVerticalBarShowing_NoCover() {
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 142, viewportWidth: 142, extentHeight: 600, viewportHeight: 140));
      }

      [Fact]
      public void BothBarsShowing_Cover() {
         Assert.True(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 400, viewportWidth: 142, extentHeight: 600, viewportHeight: 123));
      }

      [Fact]
      public void FloatingPointNoise_IsNotABar() {
         // WPF does not show a bar when the extent is larger than the viewport only by rounding noise (one step of the floating point number), so there is no corner either
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: System.Math.BitIncrement(158.0), viewportWidth: 158, extentHeight: 600, viewportHeight: 140));
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 400, viewportWidth: 142, extentHeight: System.Math.BitIncrement(140.0), viewportHeight: 140));
      }

      [Fact]
      public void ExtentJustOverTheViewport_IsABar() {
         Assert.True(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 158.5, viewportWidth: 158, extentHeight: 141, viewportHeight: 140));
      }

      [Fact]
      public void NotYetMeasuredBox_NoCover() {
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(0, 0, 0, 0));
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(double.NaN, 158, 600, 140));
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(400, double.NaN, 600, 140));
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(400, 158, double.NaN, 140));
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(400, 158, 600, double.NaN));
      }

      [Fact]
      public void StaleSizesOfThePreviousText_ShowTheOldBug_SettledSizesDoNot() {
         // Trainer 1 has one short line (box 100 wide, text 40 high, the box is 140 high). Switching to trainer 2 (two longer lines, 130 wide):
         // at the moment of the change the box still has the old size, so the old rule saw 'wider than the viewport, and the text fits vertically' and painted the square.
         Assert.True(OldDecisionWithStaleSizes(newTextWidth: 130, oldViewportWidth: 100, oldViewportHeight: 140, oldExtentHeight: 40));
         // WPF then widens the box to fit the new text: the settled sizes have no scroll bar, so there is nothing to paint over.
         Assert.False(TextEditorViewModel.NeedsScrollCornerCover(extentWidth: 130, viewportWidth: 130, extentHeight: 60, viewportHeight: 140));
      }
   }
}
