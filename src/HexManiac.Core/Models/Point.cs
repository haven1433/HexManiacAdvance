using System;

namespace HavenSoft.HexManiac.Core.Models {
   public struct Point : IEquatable<Point> {
      public int X { get; }
      public int Y { get; }
      public Point(int x, int y) => (X, Y) = (x, y);
      public void Deconstruct(out int x, out int y) => (x, y) = (X, Y);
      public bool Equals(Point that) => this.X == that.X && this.Y == that.Y;
      public override bool Equals(object obj) => obj is Point that && Equals(that);
      public override int GetHashCode() => X * 101 + Y * 37;
      public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
      public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);
      public static bool operator ==(Point a, Point b) => a.Equals(b);
      public static bool operator !=(Point a, Point b) => !a.Equals(b);
      public override string ToString() => $"({X}, {Y})";
   }
}
