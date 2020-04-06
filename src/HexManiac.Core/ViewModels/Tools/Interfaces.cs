using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IToolViewModel : INotifyPropertyChanged {
      string Name { get; }
   }

   public class StubTool : IToolViewModel {
      public string Name { get; }
      public StubTool(string name) { Name = name; }

      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
   }

   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, INotifyPropertyChanged {
      int SelectedIndex { get; set; }
      IToolViewModel SelectedTool { get; }

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
      bool IsInError { get; }
      string ErrorText { get; }
      bool TryCopy(IArrayElementViewModel other);
   }

   public class SplitterArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }

      private string sectionName;

      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public string SectionName { get => sectionName; set => TryUpdate(ref sectionName, value); }
      public SplitterArrayElementViewModel(string sectionName) => SectionName = sectionName;

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is SplitterArrayElementViewModel splitter)) return false;
         SectionName = splitter.SectionName;
         return true;
      }
   }

   public interface IStreamArrayElementViewModel : IArrayElementViewModel {
      event EventHandler<(int originalStart, int newStart)> DataMoved;

      bool ShowContent { get; }
      int UsageCount { get; }

      bool CanRepoint { get; }
      ICommand Repoint { get; }

      bool CanCreateNew { get; }
      ICommand CreateNew { get; }
   }

   public interface IPagedViewModel : IStreamArrayElementViewModel {
      bool HasMultiplePages { get; }
      int Pages { get; }
      int CurrentPage { get; set; }
      ICommand PreviousPage { get; }
      ICommand NextPage { get; }
   }
}
