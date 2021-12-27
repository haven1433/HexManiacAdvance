using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public interface ISearchTreePayload {
      int Start { get; }
   }

   public enum TreeColor { Red, Black }
   public enum AddType { Left, Right, Balanced, Insert, ReplaceExisting }
   public enum RemoveType { NoRemoval, Balanced, DecreaseBlackCount }

   // Red-Black tree rules:
   // (1) The root is black
   // (2) All red nodes have black parents
   // (3) The path to every limb has the same number of blacks.

   /// <summary>
   /// Implements a sorted collection with methods for storing and retrieving elements.
   /// Unlike a SortedDictionary<>, you can get a value using a key that's not in the dictionary.
   /// In such a case, you get the value with the _next_ start position.
   /// </summary>
   public class SearchTree<T> : IEnumerable<T> where T : class, ISearchTreePayload {
      public const int LEFT = 0, RIGHT = 1;
      private TreeNode<T> root;

      public int Count { get; private set; }

      public void Add(T element) {
         if (TreeNode.Add(ref root, element)) Count++;
      }

      public void Remove(int start) {
         if (TreeNode.Remove(ref root, start)) Count--;
      }

      public void Clear() {
         root = null;
         Count = 0;
      }

      public SearchPath this[int index] {
         get {
            var directions = new List<int>();
            for (var node = root; node != null;) {
               if (index < node.Payload.Start) {
                  directions.Add(LEFT);
                  node = node.Left;
               } else if (node.Payload.Start < index) {
                  directions.Add(RIGHT);
                  node = node.Right;
               } else {
                  return new SearchPath(this, directions, node.Payload);
               }
            }

            // not found
            return new SearchPath(this, directions);
         }
      }

      public T this[SearchPath path] => path.Element;

      private IList<TreeNode<T>> GetNodesOnPath(IEnumerable<int> path) {
         var results = new List<TreeNode<T>>();
         var node = root;
         results.Add(node);
         foreach (int direction in path) {
            if (direction == LEFT) node = node.Left;
            else node = node.Right;
            if (node == null) break;
            results.Add(node);
         }
         return results;
      }

      public IEnumerable<T> StartingFrom(int index) {
         return root.EnumerateFrom(index);
      }

      public IEnumerator<T> GetEnumerator() {
         foreach (var node in root) yield return node.Payload;
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public class SearchPath {
         private readonly SearchTree<T> tree;
         private readonly IList<int> directions;

         public T PreviousElement => GetPrevious().Payload;
         public T Element { get; }
         public T NextElement => GetNext().Payload;
         public bool HasElement => Element != null;

         public SearchPath(SearchTree<T> tree, IList<int> directions, T element = null) {
            this.tree = tree;
            Element = element;
            this.directions = directions;
         }

         #region operators

         public static bool operator >=(SearchPath path, int index) {
            if (index == 0) return path.HasElement;
            throw new NotImplementedException();
         }
         public static bool operator <=(SearchPath path, int inedx) {
            throw new NotImplementedException();
         }
         public static bool operator <(SearchPath path, int index) {
            if (index == 0) return !path.HasElement;
            throw new NotImplementedException();
         }
         public static bool operator >(SearchPath path, int index) {
            throw new NotImplementedException();
         }

         #endregion

         private TreeNode<T> GetPrevious() {
            var path = tree.GetNodesOnPath(directions);
            if (!HasElement && directions.Last() == RIGHT) return path.Last();

            int checkNodeIndex = path.Count - 1;

            var previous = path[checkNodeIndex].Left;
            if (previous == null) {
               while (checkNodeIndex > 0 && path[checkNodeIndex - 1].Left == path[checkNodeIndex]) checkNodeIndex--;
               if (checkNodeIndex == 0) return null;
               previous = path[checkNodeIndex - 1].Left;
            }

            while (previous.Right != null) previous = previous.Right;
            return previous;
         }

         private TreeNode<T> GetNext() {
            var path = tree.GetNodesOnPath(directions);
            if (!HasElement && directions.Last() == LEFT) return path.Last();

            int checkNodeIndex = path.Count - 1;

            var next = path[checkNodeIndex].Right;
            if (next == null) {
               while (checkNodeIndex > 0 && path[checkNodeIndex - 1].Right == path[checkNodeIndex]) checkNodeIndex--;
               if (checkNodeIndex == 0) return null;
               next = path[checkNodeIndex - 1].Right;
            }

            while (next.Left != null) next = next.Left;
            return next;
         }
      }
   }

   public static class TreeNode {
      public static TreeNode<T> From<T>(T element) where T : ISearchTreePayload => new TreeNode<T>(element);
      /// <returns>True if the size of the collection grew.</returns>
      public static bool Add<T>(ref TreeNode<T> node, T element) where T : ISearchTreePayload => TreeNode<T>.Add(ref node, element) != AddType.ReplaceExisting;
      /// <returns>True if a node was actually removed.</returns>
      public static bool Remove<T>(ref TreeNode<T> node, int key) where T : ISearchTreePayload => TreeNode<T>.Remove(ref node, key) != RemoveType.NoRemoval;
      public static void RotateRight<T>(ref TreeNode<T> node) where T : ISearchTreePayload {
         var newRoot = node.Left;
         node.Left = newRoot.Right;
         newRoot.Right = node;
         node = newRoot;
      }
      public static void RotateLeft<T>(ref TreeNode<T> node) where T : ISearchTreePayload {
         var newRoot = node.Right;
         node.Right= newRoot.Left;
         newRoot.Left = node;
         node = newRoot;
      }
      public static void Rotate<T>(ref TreeNode<T> node, int direction) where T : ISearchTreePayload {
         if (direction == TreeNode<T>.LEFT) RotateLeft(ref node);
         else RotateRight(ref node);
      }
      public static bool IsBlack<T>(this TreeNode<T> node) where T : ISearchTreePayload => (node?.Color ?? TreeColor.Black) == TreeColor.Black;
      public static bool BlackWithBlackChildren<T>(this TreeNode<T> node)where T : ISearchTreePayload {
         if (node == null || node.Color == TreeColor.Red) return false;
         return node.Left.IsBlack() && node.Right.IsBlack();
      }
   }

   public class TreeNode<T> : IEnumerable<TreeNode<T>> where T : ISearchTreePayload {
      public const int LEFT = 0, RIGHT = 1;

      public T Payload { get; private set; }
      public TreeColor Color { get; private set; }
      public int BlackCount => ((Left == null) ? 0 : Left.BlackCount) + (Color == TreeColor.Black ? 1 : 0);
      private TreeNode<T>[] children = new TreeNode<T>[2];
      public TreeNode<T> Left { get => children[0]; set => children[0] = value; }
      public TreeNode<T> Right { get => children[1]; set => children[1] = value; }
      public TreeNode(T value) => Payload = value;

      public override string ToString() {
         var result = Payload.ToString();
         if (Color == TreeColor.Red) return $"({result})";
         return result;
      }

      /// <summary>
      /// Checks to make sure that this red-black subtree obeys the no-double-red rule and the black-height-match rule.
      /// </summary>
      [Conditional("DEBUG")]
      public void Verify() => VerifyHelper();

      private int VerifyHelper() {
         // verify that from this node, the black length is the same in both directions
         // verify that if this node is red, it has no red children.
         var leftCount = children[LEFT] == null ? 0 : Left.VerifyHelper();
         var rightCount = Right == null ? 0 : Right.VerifyHelper();
         Debug.Assert(leftCount == rightCount, $"RedBlack Node must have matching black count on left and right branches: {leftCount} and {rightCount}");
         if (Color == TreeColor.Black) return leftCount + 1;
         Debug.Assert(Left.IsBlack() && Right.IsBlack(), "Red nodes must not have red children!");
         return leftCount;
      }

      public bool BlackWithRedChild(int direction) => this.IsBlack() && !children[direction].IsBlack();

      public IEnumerable<T> EnumerateFrom(int start) {
         if (start > Payload.Start && Left != null) {
            foreach (var node in Left.EnumerateFrom(start)) yield return node;
         }
         if (start == Payload.Start) yield return Payload;
         if (Right != null) {
            foreach (var node in Right.EnumerateFrom(start)) yield return node;
         }
      }

      /// <summary>
      /// Only do tree-balancing if the current node is red.
      /// Returns true if the higher level should perform balancing.
      /// </summary>
      public static AddType Add(ref TreeNode<T> node, T element) {
         var insert = TreeNode.From(element);
         return Add(ref node, insert);
      }

      public static AddType Add(ref TreeNode<T> node, TreeNode<T> insert, bool balance = true) {
         var start = insert.Payload.Start;
         if (node == null || node.Payload.Start == start) {
            if (balance) insert.Color = node?.Color ?? TreeColor.Red;
            var addType = balance ? AddType.Insert : AddType.Balanced;
            if (node != null) addType = AddType.ReplaceExisting;
            insert.Left = node?.Left;
            insert.Right = node?.Right;
            node = insert;
            return addType;
         } else if (start < node.Payload.Start) {
            return AddChild(ref node, insert, LEFT, balance);
         } else if (node.Payload.Start < start) {
            return AddChild(ref node, insert, RIGHT, balance);
         } else {
            throw new NotImplementedException();
         }
      }

      private static AddType AddChild(ref TreeNode<T> parent, TreeNode<T> insert, int direction, bool balance) {
         var other = 1 - direction;
         var addMethod = Add(ref parent.children[direction], insert, balance);
         if (addMethod == AddType.ReplaceExisting) return addMethod;
         if (addMethod == AddType.Insert) return (AddType)direction;
         var (node, sibling) = (parent.children[direction], parent.children[other]);
         if (addMethod != AddType.Balanced) {
            if (node.RecolorIfSelfAndSiblingAreRed(sibling, parent)) return AddType.Insert;
            if (addMethod == (AddType)other && !node.IsBlack() && sibling.IsBlack()) {
               TreeNode.Rotate(ref parent.children[direction], direction);
               node = parent.children[direction];
            }
            node.RotateIfSelfRedWithBlackSibling(sibling, ref parent, other);
         }
         return AddType.Balanced;
      }

      public static RemoveType Remove(ref TreeNode<T> node, int key) {
         if (node == null) return RemoveType.NoRemoval;

         if (key == node.Payload.Start) {
            if (node.Right != null) {
               var nextNode = node.Right.First();
               (node.Payload, nextNode.Payload) = (nextNode.Payload, node.Payload);
               return RemoveChild(ref node, key, RIGHT);
            } else if (node.Left != null) {
               var prevNode = node.Left; // if I have only one child, by the red-black rules it must be a Red leaf
               (node.Payload, prevNode.Payload) = (prevNode.Payload, node.Payload);
               return RemoveChild(ref node, key, LEFT);
            } else {
               var color = node.Color;
               node = null;
               return color == TreeColor.Red ? RemoveType.Balanced : RemoveType.DecreaseBlackCount; ;
            }
         }

         if (node.Payload.Start < key) {
            return RemoveChild(ref node, key, RIGHT);
         } else {
            return RemoveChild(ref node, key, LEFT);
         }
      }

      private static RemoveType RemoveChild(ref TreeNode<T> node, int key, int direction) {
         int other = 1 - direction;
         var result = Remove(ref node.children[direction], key);
         if (result == RemoveType.NoRemoval) return result;
         if (node.Color == TreeColor.Black && !node.children[other].IsBlack() && result == RemoveType.DecreaseBlackCount) {
            TreeNode.Rotate(ref node, direction);
            node.Recolor();
            node.children[direction].Recolor();
            IncreaseBlackCount(ref node.children[direction], direction);
            return RemoveType.Balanced;
         }
         if (node.Color == TreeColor.Black && result == RemoveType.DecreaseBlackCount && node.children[other].BlackWithBlackChildren()) {
            node.children[other].Recolor();
            return RemoveType.DecreaseBlackCount;
         } else if (node.Color == TreeColor.Red && result == RemoveType.DecreaseBlackCount && node.children[other].IsBlack() && node.children[direction].IsBlack()) {
            IncreaseBlackCount(ref node, direction);
            return RemoveType.Balanced;
         }

         if (node.Color == TreeColor.Black && result == RemoveType.DecreaseBlackCount && node.children[other].BlackWithRedChild(other)) {
            TreeNode.Rotate(ref node, direction);
            node.children[other].Recolor();
            return RemoveType.Balanced;
         }
         if (node.Color == TreeColor.Black && result == RemoveType.DecreaseBlackCount && node.children[other].BlackWithRedChild(direction)) {
            // this case just rotates to become the previous case
            TreeNode.Rotate(ref node.children[other], other);
            node.children[other].children[other].Recolor();
            node.children[other].Recolor();

            TreeNode.Rotate(ref node, direction);
            node.children[other].Recolor();
            return RemoveType.Balanced;
         }

         return RemoveType.Balanced;
      }

      private static void IncreaseBlackCount(ref TreeNode<T> node, int direction) {
         if (node.Color == TreeColor.Red && node.children[1 - direction].BlackWithRedChild(direction)) {
            TreeNode.Rotate(ref node.children[1 - direction], 1 - direction);
            node.children[1 - direction].Recolor();
            node.children[1 - direction].children[1 - direction].Recolor();
         }
         if (node.Color == TreeColor.Red && node.Left.IsBlack() && node.Right.IsBlack()) {
            node.Recolor();
            node.children[1 - direction].Recolor();
         }
         if (node.children[1 - direction].children.Any(child => !child.IsBlack())) {
            TreeNode.Rotate(ref node, direction);
            node.children[direction].Recolor();
            node.Recolor();
         }
      }

      public void Recolor() => Color = Color == TreeColor.Red ? TreeColor.Black : TreeColor.Red;

      public IEnumerator<TreeNode<T>> GetEnumerator() {
         if (Left != null) {
            foreach (var node in Left) {
               yield return node;
            }
         }
         yield return this;
         if (Right != null) {
            foreach (var node in Right) {
               yield return node;
            }
         }
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      private bool RecolorIfSelfAndSiblingAreRed(TreeNode<T> sibling, TreeNode<T> parent) {
         if (!this.IsBlack() && !sibling.IsBlack()) {
            // parent is guaranteed to be black by rule (2)
            parent.Recolor();
            Recolor();
            sibling.Recolor();
            return true;
         }
         return false;
      }

      private void RotateIfSelfRedWithBlackSibling(TreeNode<T> sibling, ref TreeNode<T> parent, int rotateDirection) {
         if (parent == null) return; // root is always black
         if (!sibling.IsBlack()) return;
         if (this.IsBlack()) return;
         TreeNode.Rotate(ref parent, rotateDirection);
         parent.children[rotateDirection].Recolor();
         parent.Recolor();
      }
   }
}
