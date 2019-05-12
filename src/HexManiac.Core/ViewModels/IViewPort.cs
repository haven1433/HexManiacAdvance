using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IViewPort : ITabContent, INotifyCollectionChanged {
      string FileName { get; } // Name is dispayed in a tab. FileName lets us know when to call 'ConsiderReload'

      int Width { get; set; }
      int Height { get; set; }

      bool UseCustomHeaders { get; set; }
      int MinimumScroll { get; }
      int ScrollValue { get; set; }
      int MaximumScroll { get; }
      ObservableCollection<string> Headers { get; }
      ObservableCollection<HeaderRow> ColumnHeaders { get; }
      int DataOffset { get; }
      ICommand Scroll { get; } // parameter: Direction to scroll

      string SelectedAddress { get; }
      string AnchorText { get; }
      bool AnchorTextVisible { get; }

      HexElement this[int x, int y] { get; }
      IDataModel Model { get; }
      bool IsSelected(Point point);
      bool IsTable(Point point);

      IReadOnlyList<(int start, int end)> Find(string search);
      IChildViewPort CreateChildView(int startAddress, int endAddress);
      void FollowLink(int x, int y);
      void ExpandSelection(int x, int y);
      void ConsiderReload(IFileSystem fileSystem);
      void FindAllSources(int x, int y);

      bool HasTools { get; }
      IToolTrayViewModel Tools { get; }

      IReadOnlyList<IContextItem> GetContextMenuItems(Point point);
   }
}
