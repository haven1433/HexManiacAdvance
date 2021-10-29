using System;
using System.Collections;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models {
   public interface ISearchTreePayload {
      int Start { get; }
   }

   public enum TreeColor { Red, Black }
   public enum AddType { Left, Balanced, Right, Insert }

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
      public static bool IsBlack<T>(this TreeNode<T> node) where T : ISearchTreePayload => (node?.Color ?? TreeColor.Black) == TreeColor.Black;
   }

   public class TreeNode<T> : IEnumerable<TreeNode<T>> where T : ISearchTreePayload {
      public T Payload { get; }
      public TreeColor Color { get; private set; }
      public int BlackCount => ((Left == null) ? 0 : Left.BlackCount) + (Color == TreeColor.Black ? 1 : 0);
      private TreeNode<T> left, right;
      public TreeNode<T> Left { get => left; set => left = value; }
      public TreeNode<T> Right { get => right; set => right = value; }
      public TreeNode(T value) => Payload = value;

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
         } else if (node.Payload.Start > start) {
            var addMethod = Add(ref node.left, insert);
            if (addMethod == AddType.Insert) return AddType.Left;
            if (addMethod != AddType.Balanced) {
               node.left.RecolorIfSelfAndSiblingAreRed(node.right, node);
               if (addMethod == AddType.Right && !node.left.IsBlack() && node.right.IsBlack()) TreeNode.RotateLeft(ref node.left);
               node.left.RotateRightIfSelfRedWithBlackSibling(node.right, ref node);
            }
         } else if (node.Payload.Start < start) {
            var addMethod = Add(ref node.right, insert);
            if (addMethod == AddType.Insert) return AddType.Right;
            if (addMethod != AddType.Balanced) {
               if (node.right.RecolorIfSelfAndSiblingAreRed(node.left, node)) return AddType.Insert;
               if (addMethod == AddType.Left && !node.right.IsBlack() && node.left.IsBlack()) TreeNode.RotateRight(ref node.right);
               node.right.RotateLeftIfSelfRedWithBlackSibling(node.left, ref node);
            }
         } else {
            throw new NotImplementedException();
         }

         return AddType.Balanced;
      }

      public void Recolor() => Color = Color == TreeColor.Red ? TreeColor.Black : TreeColor.Red;

      public IEnumerator<TreeNode<T>> GetEnumerator() {
         if (left != null) {
            foreach (var node in left) {
               yield return node;
            }
         }
         yield return this;
         if (right != null) {
            foreach (var node in right) {
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

      private void RotateLeftIfSelfRedWithBlackSibling(TreeNode<T> sibling, ref TreeNode<T> parent) {
         if (parent == null) return; // root is always black
         if (!sibling.IsBlack()) return;
         if (this.IsBlack()) return;
         TreeNode.RotateLeft(ref parent);
         parent.left.Recolor();
         parent.Recolor();
      }

      private void RotateRightIfSelfRedWithBlackSibling(TreeNode<T> sibling, ref TreeNode<T> parent) {
         if (parent == null) return; // root is always black
         if (!sibling.IsBlack()) return;
         if (this.IsBlack()) return;
         TreeNode.RotateRight(ref parent);
         parent.right.Recolor();
         parent.Recolor();
      }
   }
}
