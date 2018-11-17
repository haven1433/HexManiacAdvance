using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class CompositeViewPort : ViewModelCore, IViewPort {
      public HexElement this[int x, int y] => throw new NotImplementedException();

      public int Width { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
      public int Height { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

      public int MinimumScroll => throw new NotImplementedException();

      public int ScrollValue => throw new NotImplementedException();

      public int MaximumScroll => throw new NotImplementedException();

      public ObservableCollection<string> Headers => throw new NotImplementedException();

      public ICommand Scroll => throw new NotImplementedException();

      public string Name => throw new NotImplementedException();

      public ICommand Save => throw new NotImplementedException();

      public ICommand SaveAs => throw new NotImplementedException();

      public ICommand Undo => throw new NotImplementedException();

      public ICommand Redo => throw new NotImplementedException();

      public ICommand Copy => throw new NotImplementedException();

      public ICommand Clear => throw new NotImplementedException();

      public ICommand Goto => throw new NotImplementedException();

      public ICommand Back => throw new NotImplementedException();

      public ICommand Forward => throw new NotImplementedException();

      public ICommand Close => throw new NotImplementedException();

      public event EventHandler<string> OnError;
      public event EventHandler Closed;
      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public IViewPort CreateChildView(int offset) {
         throw new NotImplementedException();
      }

      public IReadOnlyList<int> Find(string search) {
         throw new NotImplementedException();
      }

      public bool IsSelected(Point point) {
         throw new NotImplementedException();
      }
   }
}
