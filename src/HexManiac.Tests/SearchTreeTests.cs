using HavenSoft.HexManiac.Core;
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

      private static TestTreeNode Build(string order) {
         var nodes = order.SplitLines()
            .SelectMany(line => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(text => text.StartsWith("(") ?
               Node(int.Parse(text.Substring(1, text.Length - 2))) :
               BlackNode(int.Parse(text))
            ).ToList();
         var root = nodes[0];
         for (int i = 1; i < nodes.Count; i++)
            TestTreeNode.Add(ref root, nodes[i], balance: false);
         return root;
      }

      private SearchTree<TestPayload> CreateTestTree(params int[] start) {
         var tree = new SearchTree<TestPayload>();
         for (int i = 0; i < start.Length; i++) tree.Add(new TestPayload(start[i], i + 1));
         return tree;
      }

      private static void AssertNodeEquals(TestTreeNode expected, TestTreeNode actual) {
         if (expected == null) {
            if (actual == null) return;
            throw new ArgumentException("Compared null node to non-null node!");
         }
         if (expected.Color != actual.Color) throw new ArgumentException("Colors don't match!");
         if (expected.Payload.Start != actual.Payload.Start) throw new ArgumentException("Start doesn't match!");
         if (expected.Payload.ID != actual.Payload.ID) throw new ArgumentException("ID doesn't match!");
         AssertNodeEquals(expected.Left, actual.Left);
         AssertNodeEquals(expected.Right, actual.Right);
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
         var root = Build("1 (2)");

         var result = TestTreeNode.Remove(ref root, 2);

         var expected = Build("1");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void RightChildBlack_RemoveRightChild_ReportBlackDecrease() {
         var root = Build(@"  2
                             1 3  ");

         var result = TestTreeNode.Remove(ref root, 3);

         var expected = Build(@"  2
                                (1)  ");
         Assert.Equal(RemoveType.DecreaseBlackCount, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void LeftChildBlack_RemoveLeftChild_ReportBlackDecrease() {
         var root = Build(@"  2
                             1 3  ");

         var result = TestTreeNode.Remove(ref root, 1);

         var expected = Build(@"  2
                                  (3)  ");
         Assert.Equal(RemoveType.DecreaseBlackCount, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void ChildrenBlackParentRed_RemoveRight_ParentBlack() {
         var root = Build(@" (2)
                             1 3  ");

         var result = TestTreeNode.Remove(ref root, 3);

         var expected = Build(@"  2
                                (1)  ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void LeftNodeRed_RemoveBlackRight_TreeStaysBalanced() {
         var root = Build(@"      4
                               (2)  5
                               1 3     ");

         var result = TestTreeNode.Remove(ref root, 5);

         var expected = Build(@"    2
                                  1   4
                                    (3)  ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void BlackTreeWithRedRightmostChild_RemoveLeft_BalanceTreeAndRedNodeBecomesBlack() {
         var root = Build(@"  2
                             1 3
                               (4)  ");

         var result = TestTreeNode.Remove(ref root, 1);

         var expected = Build(@"   3
                                 2   4  ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void BlackTreeWithMiddleRedInRight_RemoveLeft_BalanceTree() {
         var root = Build(@"  2
                            1  (4)
                               3 5  ");

         var result = TestTreeNode.Remove(ref root, 1);

         var expected = Build(@"  4
                                2   5
                                (3)    ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void BlackTreeWithRedInnerLeaf_RemoveLeft_BalanceTree() {
         var root = Build(@"   2
                             1   4
                               (3)       ");

         var result = TestTreeNode.Remove(ref root, 1);

         var tree = Build(@"   3
                             2   4      ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(tree, root);
      }

      [Fact]
      public void BlackTreeWithTwoRedLeafs_RemoveLeft_BalanceTree() {
         var root = Build(@"  2
                          1       4
                               (3) (5)  ");

         var result = TestTreeNode.Remove(ref root, 1);

         var tree = Build(@"  4
                           2     5
                           (3)      ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(tree, root);
      }

      [Fact]
      public void ParentRed_RemoveBlackLeaf_Balance() {
         var root = Build(@"  (3)
                           1       5
                          0 2     4 (7)
                                    6 8  ");

         var result = TestTreeNode.Remove(ref root, 0);

         var tree = Build(@"  5
                         (3)     (7)
                        1   4   6   8
                        (2)            ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(tree, root);
      }

      [Fact]
      public void ParentBlack_RemoveBlackLeaf_Balance() {
         var root = Build(@"  3
                          1       5
                         0 2     4 (7)
                                   6 8  ");

         var result = TestTreeNode.Remove(ref root, 0);

         var tree = Build(@"  5
                          3       7
                        1   4   6   8
                        (2)            ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(tree, root);
      }

      [Fact]
      public void ParentRedWithInnerRedSubTree_RemoveBlackLeaf_Balance() {
         var root = Build(@"  (3)
                           1       7
                          0 2   (5)  8
                                4 6     ");

         var result = TestTreeNode.Remove(ref root, 0);

         var tree = Build(@"  5
                         (3)     (7)
                        1   4    6 8
                        (2)              ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(tree, root);
      }

      [Fact]
      public void ParentBlackWithInnerRedSubTree_RemoveBlackLeaf_Balance() {
         var root = Build(@"  3
                          1       7
                         0 2   (5)  8
                               4 6     ");

         var result = TestTreeNode.Remove(ref root, 0);

         var tree = Build(@"  5
                          3       7
                       1    4    6 8
                       (2)            ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(tree, root);
      }

      [Fact]
      public void NodeWithChildren_RemoveRoot_ReplaceRootPayloadWithNextInOrderPosition() {
         var root = Build(@"  3
                            2   5
                              (4)  ");

         var result = TestTreeNode.Remove(ref root, 3);

         var expected = Build(@"  4
                                2   5  ");
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void NodeWithOnlyLeftChildren_RemoveRoot_ReplaceRootPayloadWithPreviousInOrderPosition() {
         var root = Build(@"  3
                            (2)  ");

         var result = TestTreeNode.Remove(ref root, 3);

         var expected = BlackNode(2);
         Assert.Equal(RemoveType.Balanced, result);
         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void NodeWithChildren_ReplaceRoot_StillHaveChilren() {
         var root = Build("3 (2) (4) 3");

         var expected = Build("3 (2) (4)");

         AssertNodeEquals(expected, root);
      }

      [Fact]
      public void SearchTree_GetNodePathToNode_HasNode() {
         var tree = CreateTestTree(50);

         var path = tree[50];

         Assert.True(path.HasElement);
         Assert.Equal(1, path.Element.ID);
      }

      [Fact]
      public void SearchTree_GetNoneRootNode_HasNode() {
         var tree = CreateTestTree(50, 51);

         var path = tree[51];

         Assert.True(path.HasElement);
         Assert.Equal(2, path.Element.ID);
      }

      [Fact]
      public void SearchTree_GetStartPastEnd_CanGetPrevious() {
         var tree = CreateTestTree(50, 51);

         var path = tree[52];

         Assert.False(path.HasElement);
         Assert.Equal(2, path.PreviousElement.ID);
      }

      [Fact]
      public void SearchTree_GetStartBeforeBeginning_CanGetNext() {
         var tree = CreateTestTree(50, 51);

         var path = tree[0];

         Assert.False(path.HasElement);
         Assert.Equal(1, path.NextElement.ID);
      }

      [Fact]
      public void SearchTree_Remove_Removed() {
         var tree = CreateTestTree(50, 51);

         tree.Remove(50);

         Assert.Equal(1, tree.Count);
      }

      // TODO tests on the SearchTree itself, not just nodes
   }
}
