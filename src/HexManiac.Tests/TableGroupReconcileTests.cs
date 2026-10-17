using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Walking from one table element to the next rebuilds the list of members of every group of the table tool.
   /// The members that can show the new element are updated (the view keeps the controls it has built for them);
   /// only a member that appeared, disappeared or became another kind of member costs the view a new control.
   /// A member in the middle of a long list that appears or disappears used to make every member after it a different kind of member: a control built again for each of them.
   /// </summary>
   public class TableGroupReconcileTests : BaseViewModelTestClass {
      private IArrayElementViewModel Hex(string name) => new FieldArrayElementViewModel(ViewPort, name, 0, 1, HexFieldStrategy.Instance);
      private IArrayElementViewModel Number(string name) => new FieldArrayElementViewModel(ViewPort, name, 0, 1, NumericFieldStrategy.Instance);
      private IArrayElementViewModel Text(string name) => new FieldArrayElementViewModel(ViewPort, name, 0, 1, new TextFieldStrategy());
      private IArrayElementViewModel Button(string name) => new ButtonArrayElementViewModel(name, () => { });

      private TableGroupViewModel NewGroup(params IArrayElementViewModel[] members) {
         var group = new TableGroupViewModel(ViewPort);
         Refresh(group, members);
         return group;
      }

      private static void Refresh(TableGroupViewModel group, params IArrayElementViewModel[] members) {
         group.Open();
         foreach (var member in members) group.Add(member);
         group.Close();
      }

      private static string Label(IArrayElementViewModel member) => member switch {
         IMultiEnabledArrayElementViewModel field => field.Name,
         ButtonArrayElementViewModel button => button.Text,
         _ => member.GetType().Name,
      };

      private static List<string> Labels(TableGroupViewModel group) => group.Members.Select(Label).ToList();

      private static List<NotifyCollectionChangedAction> Watch(TableGroupViewModel group) {
         var actions = new List<NotifyCollectionChangedAction>();
         group.Members.CollectionChanged += (sender, e) => actions.Add(e.Action);
         return actions;
      }

      [Fact]
      public void RefreshWithTheSameKinds_KeepsEveryMember_AndShowsTheNewOnes() {
         var group = NewGroup(Hex("a"), Number("b"), Text("c"));
         var before = group.Members.ToList();
         var actions = Watch(group);

         Refresh(group, Hex("a2"), Number("b2"), Text("c2"));

         Assert.Equal(before, group.Members.ToList());
         Assert.Empty(actions);
         Assert.Equal(new[] { "a2", "b2", "c2" }, Labels(group));
      }

      [Fact]
      public void ANewMemberInTheMiddle_IsOneInsert_AndTheRestKeepsItsViews() {
         var group = NewGroup(Hex("a"), Number("b"), Text("c"));
         var before = group.Members.ToList();
         var actions = Watch(group);

         Refresh(group, Hex("a2"), Button("extra"), Number("b2"), Text("c2"));

         Assert.Equal(new[] { NotifyCollectionChangedAction.Add }, actions);
         Assert.Equal(new[] { "a2", "extra", "b2", "c2" }, Labels(group));
         Assert.Same(before[0], group.Members[0]);
         Assert.Same(before[1], group.Members[2]);
         Assert.Same(before[2], group.Members[3]);
      }

      [Fact]
      public void AMemberMissingInTheMiddle_IsOneRemove_AndTheRestKeepsItsViews() {
         var group = NewGroup(Hex("a"), Button("extra"), Number("b"), Text("c"));
         var before = group.Members.ToList();
         var actions = Watch(group);

         Refresh(group, Hex("a2"), Number("b2"), Text("c2"));

         Assert.Equal(new[] { NotifyCollectionChangedAction.Remove }, actions);
         Assert.Equal(new[] { "a2", "b2", "c2" }, Labels(group));
         Assert.Same(before[0], group.Members[0]);
         Assert.Same(before[2], group.Members[1]);
         Assert.Same(before[3], group.Members[2]);
      }

      [Fact]
      public void ANewMemberAtTheFront_OfAlternatingKinds_IsOneInsert_NotAReplacementOfEveryMember() {
         // hex, number, hex, number, ...: a member in front of them makes every one of them another kind than it was at its place
         var group = NewGroup(Hex("a"), Number("b"), Hex("c"), Number("d"), Hex("e"), Number("f"));
         var before = group.Members.ToList();
         var actions = Watch(group);

         Refresh(group, Number("new"), Hex("a2"), Number("b2"), Hex("c2"), Number("d2"), Hex("e2"), Number("f2"));

         Assert.Equal(new[] { NotifyCollectionChangedAction.Add }, actions);
         Assert.Equal(new[] { "new", "a2", "b2", "c2", "d2", "e2", "f2" }, Labels(group));
         for (int i = 0; i < before.Count; i++) Assert.Same(before[i], group.Members[i + 1]);
      }

      [Fact]
      public void TheFirstMemberGone_OfAlternatingKinds_IsOneRemove() {
         var group = NewGroup(Text("lonely"), Hex("a"), Number("b"), Hex("c"), Number("d"));
         var before = group.Members.ToList();
         var actions = Watch(group);

         Refresh(group, Hex("a2"), Number("b2"), Hex("c2"), Number("d2"));

         Assert.Equal(new[] { NotifyCollectionChangedAction.Remove }, actions);
         for (int i = 1; i < before.Count; i++) Assert.Same(before[i], group.Members[i - 1]);
      }

      [Fact]
      public void AMemberThatBecomesAnotherKind_IsReplacedInPlace() {
         var group = NewGroup(Hex("a"), Number("b"), Text("c"));
         var before = group.Members.ToList();
         var actions = Watch(group);

         Refresh(group, Hex("a2"), Button("b2"), Text("c2"));

         Assert.Equal(new[] { NotifyCollectionChangedAction.Replace }, actions);
         Assert.Same(before[0], group.Members[0]);
         Assert.Same(before[2], group.Members[2]);
         Assert.IsType<ButtonArrayElementViewModel>(group.Members[1]);
      }

      [Fact]
      public void FieldsOfAnotherKind_AreNotCopiedIntoEachOther() {
         // a hex field cannot show a number: TryCopy would refuse, so the group must not pair them up
         var group = NewGroup(Hex("a"));
         var before = group.Members.ToList();

         Refresh(group, Number("b"));

         Assert.NotSame(before[0], group.Members[0]);
         Assert.Equal(new[] { "b" }, Labels(group));
      }

      [Fact]
      public void SeveralChangesAtOnce_EndWithExactlyTheNewList() {
         var group = NewGroup(Hex("a"), Number("b"), Button("x"), Text("c"), Hex("d"), Number("e"));
         var expected = new List<string> { "a2", "new1", "c2", "d2", "new2", "new3", "e2" };

         Refresh(group, Hex("a2"), Button("new1"), Text("c2"), Hex("d2"), Button("new2"), Button("new3"), Number("e2"));

         Assert.Equal(expected, Labels(group));
      }

      [Fact]
      public void TheSameNameIsPreferred_WhenThereIsAChoiceOfPartners() {
         var group = NewGroup(Number("a"), Button("x"), Number("b"));
         var before = group.Members.ToList();

         // the button and the "b" number have partners in the new list, "a" does not
         Refresh(group, Button("x"), Number("b"), Text("t"));

         Assert.Same(before[1], group.Members[0]);
         Assert.Same(before[2], group.Members[1]);
         Assert.DoesNotContain(before[0], group.Members);
         Assert.Equal(new[] { "x", "b", "t" }, Labels(group));
      }

      [Fact]
      public void RefreshWithFewerMembers_RemovesTheRest() {
         var group = NewGroup(Hex("a"), Number("b"), Text("c"));

         Refresh(group, Hex("a2"));

         Assert.Equal(new[] { "a2" }, Labels(group));
      }

      [Fact]
      public void RefreshWithNothing_EmptiesTheGroup() {
         var group = NewGroup(Hex("a"), Number("b"));

         Refresh(group);

         Assert.Empty(group.Members);
      }

      [Fact]
      public void ANewGroup_GetsAllItsMembers_InOrder() {
         var group = NewGroup(Hex("a"), Number("b"), Text("c"));

         Assert.Equal(new[] { "a", "b", "c" }, Labels(group));
      }

      [Fact]
      public void MembersAddedToAGroupNobodyOpened_AreAppendedAtOnce() {
         // the table tool makes a group when the first element of a table arrives, and fills it without opening it
         var group = new TableGroupViewModel(ViewPort);

         group.Add(Hex("a"));
         group.Add(Number("b"));

         Assert.Equal(new[] { "a", "b" }, Labels(group));
      }

      [Fact]
      public void WhileTheGroupIsOpen_TheViewIsNotToldAnythingUntilItIsClosed() {
         var group = NewGroup(Hex("a"), Number("b"));
         var actions = Watch(group);

         group.Open();
         group.Add(Number("x"));
         group.Add(Hex("y"));
         var duringTheRefresh = actions.ToList();
         group.Close();

         Assert.Empty(duringTheRefresh);
         Assert.Equal(new[] { "x", "y" }, Labels(group));
      }

      [Fact]
      public void OpeningAnOpenGroup_KeepsWhatWasAddedSoFar() {
         // the table tool opens a group that is already open when its members are shared with the group of the streams
         var group = NewGroup(Hex("a"));
         group.Open();
         group.Add(Number("x"));

         group.Open();
         group.Add(Hex("y"));
         group.Close();

         Assert.Equal(new[] { "x", "y" }, Labels(group));
      }

      [Fact]
      public void FieldsInAMultiFieldGroup_ComeOutAsOneMember() {
         var group = new TableGroupViewModel(ViewPort) { UseMultiFieldFeature = true };

         Refresh(group, Hex("a"), Number("b"), Button("x"));

         Assert.Equal(2, group.Members.Count);
         Assert.IsType<MultiFieldArrayElementViewModel>(group.Members[0]);
         Assert.IsType<ButtonArrayElementViewModel>(group.Members[1]);
      }

      [Fact]
      public void ThousandsOfMembers_AreStillReconciled() {
         // more members than the pairing is willing to compare with each other: they are paired by place, and the result is still the new list
         var first = Enumerable.Range(0, 1100).Select(i => i % 2 == 0 ? Hex("h" + i) : Number("n" + i)).ToArray();
         var second = Enumerable.Range(0, 1100).Select(i => i % 2 == 0 ? Number("n" + i) : Hex("h" + i)).ToArray();
         var group = NewGroup(first);

         Refresh(group, second);

         Assert.Equal(second.Select(Label).ToList(), Labels(group));
      }

      [Fact]
      public void PairUp_SameKinds_PairsByPlace() {
         var existing = new[] { Hex("a"), Number("b"), Text("c") };
         var replacements = new[] { Hex("x"), Number("y"), Text("z") };

         Assert.Equal(new[] { 0, 1, 2 }, TableGroupViewModel.PairUp(existing, replacements));
      }

      [Fact]
      public void PairUp_InsertedAndRemoved_PairsTheOthers() {
         var existing = new[] { Hex("a"), Button("gone"), Number("b"), Text("c") };
         var replacements = new[] { Hex("a"), Number("b"), Button("new"), Text("c") };

         // "b" keeps its partner, the button that disappeared has none, the new button has none
         Assert.Equal(new[] { 0, 2, -1, 3 }, TableGroupViewModel.PairUp(existing, replacements));
      }

      [Fact]
      public void PairUp_NothingInCommon_PairsNothing() {
         var existing = new[] { Hex("a"), Hex("b") };
         var replacements = new[] { Button("x"), Text("y"), Number("z") };

         Assert.Equal(new[] { -1, -1, -1 }, TableGroupViewModel.PairUp(existing, replacements));
      }

      [Fact]
      public void PairUp_PartnersAreInOrder() {
         var existing = new[] { Hex("a"), Number("b"), Hex("c"), Number("d"), Hex("e") };
         var replacements = new[] { Number("b"), Hex("c"), Hex("e"), Number("f") };

         var pairs = TableGroupViewModel.PairUp(existing, replacements);

         var paired = pairs.Where(partner => partner >= 0).ToList();
         Assert.Equal(paired.OrderBy(partner => partner).ToList(), paired);
         Assert.Equal(paired.Distinct().Count(), paired.Count);
      }
   }
}
