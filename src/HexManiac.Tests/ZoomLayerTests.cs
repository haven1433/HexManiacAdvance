using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// While a zoom animates, the maps stay where they are and the view draws all of them through one transform (ZoomLayer).
   /// These tests check the transform against the formula that moves every map one frame at a time (BlockMapViewModel.ZoomFrame), which defines where a map has to be on screen.
   /// </summary>
   public class ZoomLayerTests {
      // where ZoomFrame puts a map whose edge was at 'start' (at scale startScale) when the screen point 'anchor' stays where it is and the zoom has reached 'scale'
      private static double Classic(double start, double startScale, double anchor, double scale) => start + (anchor - start) * (1 - scale / startScale);

      private static double Drawn(ZoomLayer layer, double position) => layer.Scale * position + layer.X;
      private static double DrawnY(ZoomLayer layer, double position) => layer.Scale * position + layer.Y;

      [Fact]
      public void NewLayer_IsTheIdentity_AndNotActive() {
         var layer = new ZoomLayer();
         Assert.False(layer.IsActive);
         Assert.Equal(1, layer.Scale);
         Assert.Equal(0, layer.X);
         Assert.Equal(0, layer.Y);
         Assert.Equal((12.0, -7.0), layer.ToCommitted(12, -7));
      }

      [Fact]
      public void Begin_AtRest_StartsAsTheIdentity() {
         var layer = new ZoomLayer();
         layer.Begin(2, 2, 100, 50);
         Assert.True(layer.IsActive);
         Assert.Equal(1, layer.Scale);
         Assert.Equal(0, layer.X);
         Assert.Equal(0, layer.Y);
         Assert.Equal(2, layer.CommittedScale);
         Assert.Equal(2, layer.LegStartScale);
         Assert.Equal((300.0, -40.0), layer.LegStartPosition(300, -40));
      }

      [Theory]
      [InlineData(1, 2)]
      [InlineData(1, 0.5)]
      [InlineData(3, 4)]
      [InlineData(0.25, 0.125)]
      [InlineData(0.0625, 0.5)]
      [InlineData(9, 10)]
      public void Scale_IsTheScaleOnScreenDividedByTheScaleOfTheMaps(double from, double to) {
         var layer = new ZoomLayer();
         layer.Begin(from, from, 10, 20);
         layer.Update(to);
         Assert.Equal(to / from, layer.Scale, 12);
      }

      [Theory]
      [InlineData(1, 2, 300, 200)]
      [InlineData(1, 0.5, -50, 80)]
      [InlineData(4, 3, 0, 0)]
      [InlineData(0.25, 0.125, 123.5, -7.25)]
      public void TheAnchor_StaysWhereItIs(double from, double to, double anchorX, double anchorY) {
         var layer = new ZoomLayer();
         layer.Begin(from, from, anchorX, anchorY);
         for (double scale = from; Math.Abs(scale - to) > 1e-9; scale += (to - from) / 10) {
            layer.Update(scale);
            Assert.Equal(anchorX, Drawn(layer, anchorX), 9);
            Assert.Equal(anchorY, DrawnY(layer, anchorY), 9);
         }
      }

      [Fact]
      public void EveryPoint_IsDrawnWhereTheOneByOneFormulaPutsIt() {
         var layer = new ZoomLayer();
         var (startScale, anchorX, anchorY) = (1.0, 300.0, 200.0);
         layer.Begin(startScale, startScale, anchorX, anchorY);
         foreach (var scale in new[] { 1.0, 0.9, 0.7, 0.55, 0.5 }) {
            layer.Update(scale);
            foreach (var left in new[] { -400.0, -16.0, 0.0, 17.0, 250.0 }) {
               Assert.Equal(Classic(left, startScale, anchorX, scale), Drawn(layer, left), 9);
            }
            foreach (var top in new[] { -300.0, 0.0, 33.0, 600.0 }) {
               Assert.Equal(Classic(top, startScale, anchorY, scale), DrawnY(layer, top), 9);
            }
         }
      }

      [Fact]
      public void ZoomingOutToHalf_HalvesTheDistanceToTheAnchor() {
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 100, 100);
         layer.Update(0.5);
         Assert.Equal(0.5, layer.Scale);
         Assert.Equal(100 + (300 - 100) * 0.5, Drawn(layer, 300));
         Assert.Equal(100 + (-60 - 100) * 0.5, Drawn(layer, -60));
      }

      [Fact]
      public void Redirecting_StartsFromWhatIsOnScreen_SoNothingJumps() {
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 300, 200);
         layer.Update(1.6);
         var before = (Drawn(layer, 40), DrawnY(layer, -90), layer.Scale);

         layer.Begin(1, 1.6, -50, 70); // the wheel turned again, with the cursor somewhere else

         Assert.True(layer.IsActive);
         Assert.Equal(1, layer.CommittedScale); // the maps themselves still have the scale they had when the first zoom began
         Assert.Equal(1.6, layer.LegStartScale);
         Assert.Equal(before.Item1, Drawn(layer, 40), 9);
         Assert.Equal(before.Item2, DrawnY(layer, -90), 9);
         Assert.Equal(before.Item3, layer.Scale, 9);
      }

      [Fact]
      public void Redirecting_ScalesAroundTheNewAnchor() {
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 300, 200);
         layer.Update(1.6);
         layer.Begin(1, 1.6, -50, 70);
         var (anchorX, anchorY) = (-50.0, 70.0);
         var (onScreenX, onScreenY) = (Drawn(layer, 0), DrawnY(layer, 0));
         // the point of the map that is under the new anchor stays there while the zoom goes on
         var (mapX, mapY) = layer.ToCommitted(anchorX, anchorY);
         foreach (var scale in new[] { 1.8, 2.0, 2.5, 3.0 }) {
            layer.Update(scale);
            Assert.Equal(anchorX, Drawn(layer, mapX), 9);
            Assert.Equal(anchorY, DrawnY(layer, mapY), 9);
         }
         Assert.NotEqual(onScreenX, Drawn(layer, 0));
      }

      [Fact]
      public void Redirecting_GivesTheSamePositionsAsMovingTheMapsOneAtATime() {
         // the one-by-one way: every frame the map is moved from where it was when the current leg of the zoom started
         double left = 120, top = -35;                 // where a map is when everything starts
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 300, 200);
         layer.Update(1.5);
         var (x1, y1) = (Classic(left, 1, 300, 1.5), Classic(top, 1, 200, 1.5));
         Assert.Equal(x1, Drawn(layer, left), 9);

         layer.Begin(1, 1.5, 40, -20);
         layer.Update(3);
         var (x2, y2) = (Classic(x1, 1.5, 40, 3), Classic(y1, 1.5, -20, 3));
         Assert.Equal(x2, Drawn(layer, left), 9);
         Assert.Equal(y2, DrawnY(layer, top), 9);

         layer.Begin(1, 3, -200, 100);
         layer.Update(2);
         var (x3, y3) = (Classic(x2, 3, -200, 2), Classic(y2, 3, 100, 2));
         Assert.Equal(x3, Drawn(layer, left), 9);
         Assert.Equal(y3, DrawnY(layer, top), 9);
         Assert.Equal(2.0, layer.Scale, 9);
      }

      [Fact]
      public void LegStartPosition_IsWhereTheMapWasWhenTheLegStarted() {
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 300, 200);
         layer.Update(1.5);
         var onScreen = (Drawn(layer, 120), DrawnY(layer, -35));
         layer.Begin(1, 1.5, 40, -20);
         var (x, y) = layer.LegStartPosition(120, -35);
         Assert.Equal(onScreen.Item1, x, 9);
         Assert.Equal(onScreen.Item2, y, 9);
         // and the one-by-one formula started from there gets to the same place as the layer
         layer.Update(2.25);
         Assert.Equal(Classic(x, layer.LegStartScale, 40, 2.25), Drawn(layer, 120), 9);
         Assert.Equal(Classic(y, layer.LegStartScale, -20, 2.25), DrawnY(layer, -35), 9);
      }

      [Fact]
      public void ManyRandomRedirects_AlwaysAgreeWithTheOneByOneFormula() {
         var random = new Random(12345);
         var scales = new[] { 0.0625, 0.125, 0.25, 0.5, 1, 2, 3, 4, 6, 10 };
         for (int round = 0; round < 50; round++) {
            var committed = scales[random.Next(scales.Length)];
            double left = random.Next(-2000, 2000), top = random.Next(-2000, 2000);
            var layer = new ZoomLayer();
            var (classicX, classicY, classicScale) = (left, top, committed);
            layer.Begin(committed, committed, random.Next(-400, 400), random.Next(-300, 300));
            for (int leg = 0; leg < 6; leg++) {
               var shown = classicScale * (0.6 + random.NextDouble() * 1.2);
               // run the current leg to 'shown'
               var (ax, ay) = (layer.AnchorX, layer.AnchorY);
               layer.Update(shown);
               (classicX, classicY) = (Classic(classicX, classicScale, ax, shown), Classic(classicY, classicScale, ay, shown));
               classicScale = shown;
               Assert.Equal(classicX, Drawn(layer, left), 6);
               Assert.Equal(classicY, DrawnY(layer, top), 6);
               Assert.Equal(classicScale / committed, layer.Scale, 9);
               // redirect somewhere else
               layer.Begin(committed, shown, random.Next(-400, 400), random.Next(-300, 300));
               Assert.Equal(classicX, Drawn(layer, left), 6);
            }
         }
      }

      [Fact]
      public void ToCommitted_IsTheOppositeOfWhatTheViewDoes() {
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 300, 200);
         layer.Update(0.7);
         layer.Begin(1, 0.7, -50, 70);
         layer.Update(0.3);
         foreach (var (x, y) in new[] { (0.0, 0.0), (100.0, -40.0), (-250.5, 310.25) }) {
            var (onScreenX, onScreenY) = (Drawn(layer, x), DrawnY(layer, y));
            var (backX, backY) = layer.ToCommitted(onScreenX, onScreenY);
            Assert.Equal(x, backX, 9);
            Assert.Equal(y, backY, 9);
         }
      }

      [Fact]
      public void Reset_GoesBackToTheIdentity_AndCanBeginAgainFromRest() {
         var layer = new ZoomLayer();
         layer.Begin(1, 1, 300, 200);
         layer.Update(0.5);
         layer.Reset();
         Assert.False(layer.IsActive);
         Assert.Equal(1, layer.Scale);
         Assert.Equal(0, layer.X);
         Assert.Equal(0, layer.Y);
         Assert.Equal((5.0, 6.0), layer.ToCommitted(5, 6));

         layer.Begin(0.5, 0.5, 10, 10); // a new zoom does not inherit anything from the last one
         Assert.Equal(1, layer.Scale);
         Assert.Equal(0, layer.X);
         Assert.Equal(0.5, layer.CommittedScale);
         Assert.Equal((44.0, 55.0), layer.LegStartPosition(44, 55));
      }

      [Fact]
      public void ADriver_FollowingTheAnimator_EndsExactlyWhereTheMapsWillBe() {
         // what MapEditorViewModel does: Begin with the animator's scales, Update for every frame, and in the last frame move the maps with the one-by-one formula
         var now = TimeSpan.FromSeconds(10);
         var animator = new ZoomAnimator(() => now, 0.25);
         var layer = new ZoomLayer();
         double mapLeft = 77;
         layer.Begin(1, 1, 120, 80);
         animator.Start(1, 2);
         double scale = 1;
         var drawn = new List<double>();
         while (animator.IsAnimating) {
            now += TimeSpan.FromSeconds(1.0 / 60);
            if (!animator.IsFrameDue) continue;
            scale = animator.Update();
            layer.Update(scale);
            drawn.Add(Drawn(layer, mapLeft));
         }
         Assert.Equal(2, scale);
         Assert.Equal(Classic(mapLeft, 1, 120, 2), drawn.Last(), 9);
         Assert.Equal(drawn.OrderByDescending(position => position), drawn); // the map moves steadily away from the anchor (to the left of it)
         Assert.InRange(drawn.Count, 14, 17);
      }
   }
}
