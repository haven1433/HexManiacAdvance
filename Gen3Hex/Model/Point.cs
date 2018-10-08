using System;

namespace HavenSoft.Gen3Hex.Model {
   public class Point : IEquatable<Point> {
      public int X { get; }
      public int Y { get; }
      public Point(int x, int y) => (X, Y) = (x, y);
      public bool Equals(Point that) => this.X == that.X && this.Y == that.Y;
   }
}
