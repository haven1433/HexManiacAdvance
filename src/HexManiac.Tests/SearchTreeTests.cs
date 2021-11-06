using HavenSoft.HexManiac.Core.Models;
using System;
using System.Linq;
using Xunit;
using static HavenSoft.HexManiac.Core.Models.TreeColor;

using TestTreeNode = HavenSoft.HexManiac.Core.Models.TreeNode<HavenSoft.HexManiac.Tests.TestPayload>;

namespace HavenSoft.HexManiac.Tests {
   public class TestPayload : ISearchTreePayload {
      public int Start { get; }
      public int ID { get; }
      public TestPayload(int start, int id) => (Start, ID) = (start, id);
      public override string ToString() => $"{Start}.{ID}";
   }

   public class SearchTreeTests {
      private static TestPayload New(int value, int id = 0) => new TestPayload(value, id);
      private static TestTreeNode Node(int value, int id = 0, TestTreeNode left = null, TestTreeNode right = null) => new TestTreeNode(New(value, id)) { Left = left, Right = right };
      private static TestTreeNode BlackNode(int value, int id = 0, TestTreeNode left = null, TestTreeNode right = null) {
         var node = Node(value, id, left, right);
         node.Recolor();
         return node;
      }

      [Fact]
      public void Element_LessValue_InsertedLeft() {
         var node = BlackNode(5);
         TreeNode.Add(ref node, New(4));

         Assert.Equal(4, node.Left.Payload.Start);
         Assert.Null(node.Right);
         Assert.Equal(5, node.Payload.Start);
         Assert.Equal(Black, node.Color);
         Assert.Equal(Red, node.Left.Color);
      }

      [Fact]
      public void Element_MoreValue_InsertedRight() {
         var node = BlackNode(5);
         TreeNode.Add(ref node, New(6));

         Assert.Equal(6, node.Right.Payload.Start);
         Assert.Null(node.Left);
         Assert.Equal(5, node.Payload.Start);
         Assert.Equal(Black, node.Color);
         Assert.Equal(Red, node.Right.Color);
      }

      [Fact]
      public void Element_SameValue_Replaces() {
         var node = BlackNode(5);
         TreeNode.Add(ref node, New(5, 1));

         Assert.Equal(5, node.Payload.Start);
         Assert.Equal(1, node.Payload.ID);
         Assert.Null(node.Left);
         Assert.Null(node.Right);
         Assert.Equal(Black, node.Color);
      }

      [Fact]
      public void RotateRight_PreservesChildren() {
         //    y        x
         //  x   c -> a   y
         // a b          b c
         var (a, x, b, y, c) = (Node(1), Node(2), Node(3), Node(4), Node(5));
         (x.Left, x.Right) = (a, b);
         (y.Left, y.Right) = (x, c);

         var node = y;
         TreeNode.RotateRight(ref node);

         Assert.Same(node, x);
         Assert.Same(x.Left, a);
         Assert.Same(x.Right, y);
         Assert.Same(y.Left, b);
         Assert.Same(y.Right, c);
      }

      [Fact]
      public void RotateLeft_PreservesChildren() {
         //    x        y
         //  a   y -> x   c
         //     b c  a b
         var (a, x, b, y, c) = (Node(1), Node(2), Node(3), Node(4), Node(5));
         (x.Left, x.Right) = (a, y);
         (y.Left, y.Right) = (b, c);

         var node = x;
         TreeNode.RotateLeft(ref node);

         Assert.Same(node, y);
         Assert.Same(x.Left, a);
         Assert.Same(x.Right, b);
         Assert.Same(y.Left, x);
         Assert.Same(y.Right, c);
      }

      [Fact]
      public void RedNode_Recolor_Black() {
         var node = Node(1);

         node.Recolor();
         Assert.Equal(Black, node.Color);

         node.Recolor();
         Assert.Equal(Red, node.Color);
      }

      [Fact]
      public void LeftChild_AddLeft_Recursive() {
         var node = BlackNode(4);
         node.Left = BlackNode(3);

         TreeNode.Add(ref node, New(2));

         Assert.Equal(2, node.Left.Left.Payload.Start);
      }

      [Fact]
      public void LeftChild_AddRight_Recursive() {
         var node = BlackNode(4);
         node.Left = BlackNode(2);

         TreeNode.Add(ref node, New(3));

         Assert.Equal(3, node.Left.Right.Payload.Start);
      }

      [Fact]
      public void RightChild_AddRight_Recursive() {
         var node = BlackNode(4);
         node.Right = BlackNode(5);

         TreeNode.Add(ref node, New(6));

         Assert.Equal(6, node.Right.Right.Payload.Start);
      }

      [Fact]
      public void SelfAndSiblingRed_AddRed_SwapColorOfParentSelfAndSibling() {
         var sibling = Node(1);
         var node = Node(4);
         var parent = BlackNode(2, left: sibling, right: node);

         TreeNode.Add(ref parent, New(5));

         // add to the right of node
         // recolor node/sibling
         // recolor the parent
         Assert.Equal(Red, parent.Color);
         Assert.Equal(Black, sibling.Color);
         Assert.Equal(Black, node.Color);
         Assert.Equal(Red, node.Right.Color);
      }

      [Fact]
      public void SelfAndSiblingRed_AddRed_SwapColorOfParentSelfAndSibling2() {
         var sibling = Node(1);
         var node = Node(4);
         var parent = BlackNode(2, left: sibling, right: node);

         TreeNode.Add(ref parent, New(3));

         // add to the left of node
         // recolor node/sibling
         // recolor the parent
         Assert.Equal(Red, parent.Color);
         Assert.Equal(Black, sibling.Color);
         Assert.Equal(Black, node.Color);
         Assert.Equal(Red, node.Left.Color);
      }

      [Fact]
      public void RedWithLeftSiblingBlack_AddOuterRed_RotateLeft() {
         var node = Node(4);
         var parent = BlackNode(2, right: node);

         TreeNode.Add(ref parent, New(5));

         //   4B
         // 2R  5R
         Assert.Equal(Black, parent.Color);
         Assert.Equal(Red, parent.Left.Color);
         Assert.Equal(Red, parent.Right.Color);
         Assert.Equal(5, parent.Right.Payload.Start);
         Assert.Same(node, parent);
         Assert.Equal(2, parent.Left.Payload.Start);
      }

      [Fact]
      public void RedWithRightSiblingBlack_AddOuterRed_RotateRight() {
         var node = Node(2);
         var parent = BlackNode(4, left: node);

         TreeNode.Add(ref parent, New(1));

         //   2B
         // 1R  4R
         Assert.Equal(Black, parent.Color);
         Assert.Equal(Red, parent.Left.Color);
         Assert.Equal(Red, parent.Right.Color);
         Assert.Equal(1, parent.Left.Payload.Start);
         Assert.Same(node, parent);
         Assert.Equal(4, parent.Right.Payload.Start);
      }

      [Fact]
      public void RedWithLeftSiblingBlack_AddInnerRed_RotateLowerRightAndUpperLeft() {
         var node = Node(4);
         var parent = BlackNode(2, right: node);

         TreeNode.Add(ref parent, New(3));

         //   3B
         // 2R  4R
         Assert.Equal(Black, parent.Color);
         Assert.Equal(Red, parent.Left.Color);
         Assert.Equal(Red, parent.Right.Color);
         Assert.Equal(2, parent.Left.Payload.Start);
         Assert.Equal(3, parent.Payload.Start);
         Assert.Equal(4, parent.Right.Payload.Start);
         Assert.Same(node, parent.Right);
      }

      [Fact]
      public void RedWithRightSiblingBlack_AddInnerRed_RotateLowerLeftAndUpperRight() {
         var node = Node(2);
         var parent = BlackNode(4, left: node);

         TreeNode.Add(ref parent, New(3));

         //   3B
         // 2R  4R
         Assert.Equal(Black, parent.Color);
         Assert.Equal(Red, parent.Left.Color);
         Assert.Equal(Red, parent.Right.Color);
         Assert.Equal(2, parent.Left.Payload.Start);
         Assert.Equal(3, parent.Payload.Start);
         Assert.Equal(4, parent.Right.Payload.Start);
         Assert.Same(node, parent.Left);
      }

      [Fact]
      public void UnbalancedTree_AddNode_Balance() {
         // initial state
         //      2
         //   1     (4)
         //       3      6
         //          (5)   (7)
         //               add (8)
         var tree = BlackNode(2, left: BlackNode(1), right: Node(4));
         tree.Right.Left = BlackNode(3);
         tree.Right.Right = BlackNode(6, left: Node(5), right: Node(7));

         // first step: recolor to pull the black down
         //      2
         //   1     (4)
         //       3     (6)
         //           5     7 
         //                   (8)
         // but notice there's a double-red now
         TreeNode.Add(ref tree, New(8));

         // second step: rebalance, because 1/4 aren't both red
         //     4
         //  (2)  (6)
         //  1 3  5 7
         //         (8)
         Assert.Equal(1, tree.Left.Left.Payload.Start);
         Assert.Equal(2, tree.Left.Payload.Start);
         Assert.Equal(3, tree.Left.Right.Payload.Start);
         Assert.Equal(4, tree.Payload.Start);
         Assert.Equal(5, tree.Right.Left.Payload.Start);
         Assert.Equal(6, tree.Right.Payload.Start);
         Assert.Equal(7, tree.Right.Right.Payload.Start);
         Assert.Equal(8, tree.Right.Right.Right.Payload.Start);

         var colors = tree.Select(node => node.Color).ToList();
         Assert.Equal(new[] { Black, Red, Black, Black, Black, Red, Black, Red }, colors);
      }

      [Fact]
      public void BalancedTree_AddNode_Recolor() {
         // before:
         //       4
         //  (2)      (6)
         // 1   3   5     8 
         //            (7) (9)
         var parent = BlackNode(4, left: Node(2), right: Node(6));
         parent.Right.Left = BlackNode(5);
         parent.Right.Right = BlackNode(8, left: Node(7), right: Node(9));

         // after inserting (X)
         TreeNode.Add(ref parent, New(10));

         //      (4)
         //   2       6
         // 1   3   5  (8)
         //            7 9
         //              (X)
         Assert.Equal(4, parent.Payload.Start);
         Assert.Equal(Red, parent.Color);
         Assert.Equal(Black, parent.Right.Color);
         Assert.Equal(Red, parent.Right.Right.Color);
         Assert.Equal(Black, parent.Right.Right.Left.Color);
         Assert.Equal(Black, parent.Right.Right.Right.Color);
      }

      [Fact]
      public void OnlyNode_Remove_NullReturn() {
         var node = Node(1);

         TreeNode.Remove(ref node, 1);

         Assert.Null(node);
      }

      [Fact]
      public void RightChildRed_RemoveRightChild_TreeStaysBalanced() {
         var node = BlackNode(1, right: Node(2));

         var result = TestTreeNode.Remove(ref node, 2);

         Assert.Equal(RemoveType.Balanced, result);
         Assert.Equal(1, node.Payload.Start);
         Assert.Equal(Black, node.Color);
         Assert.Null(node.Left);
         Assert.Null(node.Right);
      }

      [Fact]
      public void RightChildBlack_RemoveRightChild_ReportBlackDecrease() {
         var node = BlackNode(2, left: BlackNode(1), right: BlackNode(3));

         var result = TestTreeNode.Remove(ref node, 3);

         Assert.Equal(RemoveType.DecreaseBlackCount, result);
         Assert.Equal(Red, node.Left.Color);
         Assert.Equal(Black, node.Color);
         Assert.Null(node.Right);
         Assert.Equal(1, node.Left.Payload.Start);
         Assert.Equal(2, node.Payload.Start);
      }

      [Fact]
      public void LeftChildBlack_RemoveLeftChild_ReportBlackDecrease() {
         var node = BlackNode(2, left: BlackNode(1), right: BlackNode(3));

         var result = TestTreeNode.Remove(ref node, 1);

         Assert.Equal(RemoveType.DecreaseBlackCount, result);
         Assert.Equal(Red, node.Right.Color);
         Assert.Equal(Black, node.Color);
         Assert.Null(node.Left);
         Assert.Equal(3, node.Right.Payload.Start);
         Assert.Equal(2, node.Payload.Start);
      }

      [Fact]
      public void ChildrenBlackParentRed_RemoveRight_ParentBlack() {
         var node = Node(2, left: BlackNode(1), right: BlackNode(3));

         var result = TestTreeNode.Remove(ref node, 3);

         Assert.Equal(RemoveType.Balanced, result);
         Assert.Equal(Red, node.Left.Color);
         Assert.Equal(Black, node.Color);
         Assert.Equal(new[] { 1, 2 }, node.Select(n => n.Payload.Start));
      }

      [Fact]
      public void LeftNodeRed_RemoveBlackRight_TreeStaysBalanced() {
         var left = Node(2, left: BlackNode(1), right: BlackNode(3));
         var root = BlackNode(4, left: left, right: BlackNode(5));

         var result = TestTreeNode.Remove(ref root, 5);

         //    2
         //  1   4
         //    (3)
         Assert.Equal(RemoveType.Balanced, result);
         Assert.Equal(2, root.Payload.Start);
         Assert.Equal(Black, root.Color);
         Assert.Equal(Black, root.Left.Color);
         Assert.Equal(Black, root.Right.Color);
         Assert.Equal(Red, root.Right.Left.Color);
         Assert.Equal(3, root.Right.Left.Payload.Start);
      }

      [Fact]
      public void BlackTreeWithRedRightmostChild_RemoveLeft_BalanceTreeAndRedNodeBecomesBlack() {
         TestTreeNode node1 = BlackNode(1), node2 = BlackNode(2), node3 = BlackNode(3),
            node4 = Node(4);
         (node2.Left, node2.Right) = (node1, node3);
         node3.Right = node4;
         var root = node2;

         var result = TestTreeNode.Remove(ref root, 1);

         Assert.Same(node3, root);
         Assert.Same(node2, root.Left);
         Assert.Same(node4, root.Right);
         Assert.All(root, node => node.IsBlack());
      }

      // TODO more delete cases (such as deleting a non-leaf

      // TODO tests on the SearchTree itself, not just nodes
   }
}
