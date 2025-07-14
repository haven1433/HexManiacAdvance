using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ButtonArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      public string Text { get; set; }
      public string ToolTipText { get; set; }

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }
   }

   public class MapOptionsArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public readonly IWorkDispatcher dispatcher;
      public readonly MapEditorViewModel mapEditor;
      public readonly string tableName;
      public readonly int index;
      public bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;

      public string ErrorText => string.Empty;

      public int zIndex;
      public int ZIndex { get => zIndex; set => Set(ref zIndex, value); }

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      // if we're trying to copy data from another element,
      // cancel any remaining work on this one
      public bool cancel;
      public bool TryCopy(IArrayElementViewModel other) {
         cancel = true;
         return false;
      }

      public bool showPreviews;
      public bool ShowPreviews {
         get => showPreviews;
         set => Set(ref showPreviews, value);
      }

      public ObservableCollection<GotoMapButton> MapPreviews { get; } = new();
   }

   public class GotoMapButton : ViewModelCore {
      public readonly MapEditorViewModel mapEditor;
      public readonly MapOptionsArrayElementViewModel owner;
      public readonly int bank, map;
      public IEventModel eventModel;
      public IPixelViewModel Image { get; init; }
   }
}
