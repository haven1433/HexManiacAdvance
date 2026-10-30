using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   /// <summary>
   /// One shared transform for all the maps while a zoom animation runs.
   /// <para>
   /// Moving every map one frame at a time (new scale, new left edge, new top edge: three notifications per map and a new layout for every picture) costs more with every map on screen,
   /// and with 20 or 30 maps at the far zoom levels that is more than a 60 Hz frame. Instead the maps stay exactly where they were when the zoom started
   /// and the view draws all of them through one transform: everything is bigger by <see cref="Scale"/> and moved by (<see cref="X"/>, <see cref="Y"/>).
   /// A frame is then three numbers, however many maps there are. When the animation is over the maps jump to their final scale and position and the transform goes back to the identity.
   /// </para>
   /// <para>
   /// A point of the maps at (px, py) is drawn at (Scale * px + X, Scale * py + Y). All the numbers are in the same screen coordinates the maps use for LeftEdge and TopEdge.
   /// The transform scales around the anchor (the point under the mouse), so that point stays where it is. Zooming again in the middle of a zoom
   /// (a quick turn of the wheel) starts from the transform on screen and scales around the new anchor: the transforms are chained, so the picture never jumps.
   /// </para>
   /// </summary>
   public class ZoomLayer {
      // the transform when the current leg of the animation started: the identity for a zoom that starts at rest, the transform on screen for one that was redirected
      private double baseScale = 1, baseX, baseY;

      /// <summary>True from <see cref="Begin"/> until <see cref="Reset"/>. While it is true the maps themselves are not moved.</summary>
      public bool IsActive { get; private set; }

      /// <summary>The scale the maps have (their SpriteScale): it does not change while the animation runs.</summary>
      public double CommittedScale { get; private set; } = 1;

      /// <summary>The scale that is on screen at the start of the current leg: CommittedScale for the first leg of an animation.</summary>
      public double LegStartScale { get; private set; } = 1;

      /// <summary>The point that stays where it is while the current leg runs (screen coordinates, relative to the center of the map view like LeftEdge and TopEdge).</summary>
      public double AnchorX { get; private set; }

      /// <summary>The point that stays where it is while the current leg runs.</summary>
      public double AnchorY { get; private set; }

      /// <summary>How much bigger than the maps themselves everything is drawn right now. 1 when nothing is animating.</summary>
      public double Scale { get; private set; } = 1;

      /// <summary>How far everything is moved to the right right now.</summary>
      public double X { get; private set; }

      /// <summary>How far everything is moved down right now.</summary>
      public double Y { get; private set; }

      /// <summary>
      /// Start a leg of the animation, around the point (anchorX, anchorY).
      /// If an animation is already running, the new leg starts from the transform that is on screen at <paramref name="shownScale"/> (so the picture doesn't jump)
      /// and <paramref name="committedScale"/> is ignored.
      /// </summary>
      /// <param name="committedScale">The scale of the maps (SpriteScale) when no animation is running.</param>
      /// <param name="shownScale">The scale that is on screen right now: the same as committedScale when no animation is running.</param>
      public void Begin(double committedScale, double shownScale, double anchorX, double anchorY) {
         if (IsActive) {
            Update(shownScale);
            (baseScale, baseX, baseY) = (Scale, X, Y);
         } else {
            CommittedScale = committedScale;
            (baseScale, baseX, baseY) = (1, 0, 0);
            IsActive = true;
         }
         LegStartScale = shownScale;
         (AnchorX, AnchorY) = (anchorX, anchorY);
         Update(shownScale);
      }

      /// <summary>Work out the transform for the scale the animation has reached. Cheap: a few multiplications, whatever the number of maps.</summary>
      public void Update(double scale) {
         var ratio = scale / LegStartScale;
         Scale = scale / CommittedScale;
         X = AnchorX * (1 - ratio) + ratio * baseX;
         Y = AnchorY * (1 - ratio) + ratio * baseY;
      }

      /// <summary>
      /// Where a point of the maps (a map's LeftEdge and TopEdge, for example) was on screen at the start of the current leg.
      /// A zoom that starts at rest gives the point back unchanged.
      /// </summary>
      public (double x, double y) LegStartPosition(double x, double y) => (baseScale * x + baseX, baseScale * y + baseY);

      /// <summary>Where a point of the screen is on the maps themselves (the opposite of what the view does while the animation runs).</summary>
      public (double x, double y) ToCommitted(double x, double y) => IsActive ? ((x - X) / Scale, (y - Y) / Scale) : (x, y);

      /// <summary>The animation is over (or abandoned): the maps are drawn as they are.</summary>
      public void Reset() {
         IsActive = false;
         (Scale, X, Y) = (1, 0, 0);
         (baseScale, baseX, baseY) = (1, 0, 0);
         (CommittedScale, LegStartScale) = (1, 1);
      }
   }
}
