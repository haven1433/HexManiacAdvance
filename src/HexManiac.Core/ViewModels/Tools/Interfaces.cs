using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IToolViewModel : INotifyPropertyChanged {
      string Name { get; }
   }

   public class StubTool : IToolViewModel {
      public string Name { get; }
      public StubTool(string name) { Name = name; }

      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
      public void DataForCurrentRunChanged() { }
   }

   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, INotifyPropertyChanged {
      PCSTool StringTool { get; }

      TableTool TableTool { get; }

      CodeTool CodeTool { get; }

      SpriteTool SpriteTool { get; }

      IDisposable DeferUpdates { get; }

      event EventHandler<string> OnError;
      event EventHandler<string> OnMessage;

      void Schedule(Action action);
   }

   public interface IArrayElementViewModel : ICanSilencePropertyNotifications {
      event EventHandler DataChanged;
      event EventHandler DataSelected;
      string Theme { get; set; }
      bool IsInError { get; }
      string ErrorText { get; }
      int ZIndex { get; }
   }

   // shared interface for fields/comboboxes to appear in multi-boxes
   public interface IMultiEnabledArrayElementViewModel : IArrayElementViewModel {
      string Name { get; set; }
   } 

   public class SplitterArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      public readonly IViewPort viewPort;
      public string sectionName;
      public int sectionLink;
      public bool showSection;
      public string lastFilter = string.Empty, theme;

      public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public bool ShowSection { get => showSection; set => Set(ref showSection, value); }
      public string SectionName { get => sectionName; set => Set(ref sectionName, value); }
      public int SectionLink { get => sectionLink; set => Set(ref sectionLink, value); }

      public bool visible = true;

      public SplitterArrayElementViewModel(IViewPort viewPort, string sectionName, int sectionLink) => (this.viewPort, SectionName, SectionLink) = (viewPort, sectionName, sectionLink);

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is SplitterArrayElementViewModel splitter)) return false;
         SectionName = splitter.SectionName;
         SectionLink = splitter.SectionLink;
         Theme = splitter.Theme;
         return true;
      }
   }

   public interface IStreamArrayElementViewModel : IArrayElementViewModel {
      event EventHandler<(int originalStart, int newStart)> DataMoved;

      SplitterArrayElementViewModel Parent { get; set; }
   }

   public interface IPagedViewModel : IStreamArrayElementViewModel {
      int Pages { get; }

      bool CanMovePrevious { get; }
      bool CanMoveNext { get; }
   }
}
