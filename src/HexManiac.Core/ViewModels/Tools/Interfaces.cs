using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IToolViewModel : INotifyPropertyChanged {
      string Name { get; }
      void DataForCurrentRunChanged();
   }

   public class StubTool : IToolViewModel {
      public string Name { get; }
      public StubTool(string name) { Name = name; }

      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
      public void DataForCurrentRunChanged() { }
   }

   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, INotifyPropertyChanged {
      int SelectedIndex { get; set; }
      IToolViewModel SelectedTool { get; set; }

      ICommand HideCommand { get; }
      ICommand StringToolCommand { get; }
      ICommand TableToolCommand { get; }
      ICommand CodeToolCommand { get; }
      ICommand SpriteToolCommand { get; }

      PCSTool StringTool { get; }

      TableTool TableTool { get; }

      CodeTool CodeTool { get; }

      SpriteTool SpriteTool { get; }

      IDisposable DeferUpdates { get; }

      event EventHandler<string> OnError;
      event EventHandler<string> OnMessage;

      void Schedule(Action action);
      void RefreshContent();
   }

   public interface IArrayElementViewModel : INotifyPropertyChanged {
      event EventHandler DataChanged;
      event EventHandler DataSelected;
      bool Visible { get; set; }
      bool IsInError { get; }
      string ErrorText { get; }
      int ZIndex { get; }
      bool TryCopy(IArrayElementViewModel other);
   }

   public class SplitterArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      private readonly IViewPort viewPort;
      private string sectionName;
      private int sectionLink;
      private bool showSection;
      private string lastFilter = string.Empty;
      private StubCommand followLink, collapseAll, expandAll, toggleVisibility;

      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public bool ShowSection { get => showSection; private set => Set(ref showSection, value); }
      public string SectionName { get => sectionName; set => Set(ref sectionName, value); }
      public int SectionLink { get => sectionLink; set => Set(ref sectionLink, value); }
      public ICommand FollowLink => StubCommand(ref followLink, () => viewPort.Goto.Execute(SectionLink));
      public ICommand CollapseAll => StubCommand(ref collapseAll, () => UpdateAllVisibility(false));
      public ICommand ExpandAll => StubCommand(ref expandAll, () => UpdateAllVisibility(true));
      public ICommand ToggleVisibility => StubCommand(ref toggleVisibility, () => Visible = !Visible);

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value, arg => UpdateCollapsed(lastFilter)); }
      public void UpdateCollapsed(string filter) {
         var start = false;
         var filterMatchesGroup = filter.Length == 0 || sectionName.MatchesPartial(filter);
         bool lastFieldVisible = filterMatchesGroup;
         bool anyChildrenVisible = false;
         foreach (var child in viewPort.Tools.TableTool.Children) {
            if (child == this) start = true;
            if (!start) continue;
            if (child is SplitterArrayElementViewModel && child != this) break;
            if (child is SplitterArrayElementViewModel) continue;
            if (child is ButtonArrayElementViewModel) break;
            var childVisible = filterMatchesGroup;

            if (child is FieldArrayElementViewModel faevm) childVisible = filterMatchesGroup || faevm.Name.MatchesPartial(filter);
            if (child is ComboBoxArrayElementViewModel cbaevm) childVisible = filterMatchesGroup || cbaevm.Name.MatchesPartial(filter);
            if (child is CalculatedElementViewModel cevm) childVisible = filterMatchesGroup || cevm.Name.MatchesPartial(filter);
            if (child is IStreamArrayElementViewModel saevm) childVisible = lastFieldVisible;
            if (child is BitListArrayElementViewModel blaevm) {
               childVisible = filterMatchesGroup ||
                  blaevm.Name.MatchesPartial(filter) ||
                  blaevm.Any(bitChild => bitChild.BitLabel.MatchesPartial(filter));
            } else if (child is TupleArrayElementViewModel taevm) {
               childVisible = filterMatchesGroup ||
                  taevm.Name.MatchesPartial(filter) ||
                  taevm.Children.Any(tupleChild => tupleChild.Name.MatchesPartial(filter));
            }

            child.Visible = childVisible && visible;
            lastFieldVisible = childVisible;
            anyChildrenVisible = anyChildrenVisible || childVisible;
         }
         ShowSection = anyChildrenVisible || filterMatchesGroup;
         lastFilter = filter;
      }

      private void UpdateAllVisibility(bool newValue) {
         foreach (var child in viewPort.Tools.TableTool.Children) {
            if (child is SplitterArrayElementViewModel) child.Visible = newValue;
         }
      }

      public SplitterArrayElementViewModel(IViewPort viewPort, string sectionName, int sectionLink) => (this.viewPort, SectionName, SectionLink) = (viewPort, sectionName, sectionLink);

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is SplitterArrayElementViewModel splitter)) return false;
         SectionName = splitter.SectionName;
         SectionLink = splitter.SectionLink;
         return true;
      }
   }

   public interface IStreamArrayElementViewModel : IArrayElementViewModel {
      event EventHandler<(int originalStart, int newStart)> DataMoved;

      bool ShowContent { get; }
      int UsageCount { get; }

      bool CanRepoint { get; }
      ICommand Repoint { get; }

      bool CanRepointAll { get; }
      ICommand RepointAll { get; }

      bool CanCreateNew { get; }
      ICommand CreateNew { get; }
   }

   public interface IPagedViewModel : IStreamArrayElementViewModel {
      bool ShowPageControls { get; }
      int Pages { get; }
      int CurrentPage { get; set; }

      ICommand AddPage { get; }
      ICommand DeletePage { get; }

      bool CanMovePrevious { get; }
      bool CanMoveNext { get; }
      void MovePrevious();
      void MoveNext();
   }
}
