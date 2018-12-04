using System;

namespace HavenSoft.Gen3Hex.Core.Models {
   public struct Point : IEquatable<Point> {
      public int X { get; }
      public int Y { get; }
      public Point(int x, int y) => (X, Y) = (x, y);
      public bool Equals(Point that) => this.X == that.X && this.Y == that.Y;
      public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
      public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);
      public override string ToString() => $"({X}, {Y})";
   }
}
