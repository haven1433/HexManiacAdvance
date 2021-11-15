using System;
using System.Collections;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models {
   public interface ISearchTreePayload {
      int Start { get; }
   }

   public enum TreeColor { Red, Black }
   public enum AddType { Left, Right, Balanced, Insert }
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
   public class SearchTree {
   }

   public static class TreeNode {
      public static TreeNode<T> From<T>(T element) where T : ISearchTreePayload => new TreeNode<T>(element);
      public static void Add<T>(ref TreeNode<T> node, T element) where T : ISearchTreePayload => TreeNode<T>.Add(ref node, element);
      public static void Remove<T>(ref TreeNode<T> node, int key) where T : ISearchTreePayload => TreeNode<T>.Remove(ref node, key);
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
      public static void Rotate<T>(ref TreeNode<T> node, int direction)where T : ISearchTreePayload {
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

      public T Payload { get; }
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
      /// Only do tree-balancing if the current node is red.
      /// Returns true if the higher level should perform balancing.
      /// </summary>
      public static AddType Add(ref TreeNode<T> node, T element) {
         var insert = TreeNode.From(element);
         return Add(ref node, insert);
      }

      public static AddType Add(ref TreeNode<T> node, TreeNode<T> insert) {
         var start = insert.Payload.Start;
         if (node == null || node.Payload.Start == start) {
            insert.Color = node?.Color ?? TreeColor.Red;
            node = insert;
            return AddType.Insert;
         } else if (start < node.Payload.Start) {
            return AddChild(ref node, insert, LEFT);
         } else if (node.Payload.Start < start) {
            return AddChild(ref node, insert, RIGHT);
         } else {
            throw new NotImplementedException();
         }
      }

      private static AddType AddChild(ref TreeNode<T> parent, TreeNode<T> insert, int direction) {
         var other = 1 - direction;
         var addMethod = Add(ref parent.children[direction], insert);
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
            var color = node.Color;
            node = null;
            return color == TreeColor.Red ? RemoveType.Balanced : RemoveType.DecreaseBlackCount; ;
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
         } else {
            return RemoveType.Balanced;
         }
      }

      private static void IncreaseBlackCount(ref TreeNode<T> node, int direction) {
         if (node.Color == TreeColor.Red && node.Left.IsBlack() && node.Right.IsBlack()) {
            node.Recolor();
            node.children[1 - direction].Recolor();
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
            this.Recolor();
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
