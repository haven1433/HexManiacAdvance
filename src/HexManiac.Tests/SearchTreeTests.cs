using HavenSoft.HexManiac.Core.Models;
using System.Linq;
using Xunit;
using static HavenSoft.HexManiac.Core.Models.TreeColor;

namespace HavenSoft.HexManiac.Tests {
   public class TestPayload : ISearchTreePayload {
      public int Start { get; }
      public int ID { get; }
      public TestPayload(int start, int id) => (Start, ID) = (start, id);
   }

   public class SearchTreeTests {
      private static TestPayload New(int value, int id = 0) => new TestPayload(value, id);
      private static TreeNode<TestPayload> Node(int value, int id = 0) => new TreeNode<TestPayload>(New(value, id));
      private static TreeNode<TestPayload> BlackNode(int value, int id = 0, TreeNode<TestPayload> left = null, TreeNode<TestPayload> right = null) {
         var node = Node(value, id);
         node.Recolor();
         (node.Left, node.Right) = (left, right);
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

         Assert.Equal(node, x);
         Assert.Equal(x.Left, a);
         Assert.Equal(x.Right, y);
         Assert.Equal(y.Left, b);
         Assert.Equal(y.Right, c);
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

         Assert.Equal(node, y);
         Assert.Equal(x.Left, a);
         Assert.Equal(x.Right, b);
         Assert.Equal(y.Left, x);
         Assert.Equal(y.Right, c);
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
         Assert.Equal(node, parent);
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
         Assert.Equal(node, parent);
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
         Assert.Equal(node, parent.Right);
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
         Assert.Equal(node, parent.Left);
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

      // TODO remove node tests

      // TODO tests on the SearchTree itself, not just nodes
   }
}
