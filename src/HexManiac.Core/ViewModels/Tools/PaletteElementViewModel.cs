using HavenSoft.HexManiac.Core.Models.Runs.Compressed;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel, IPagedViewModel {
      private PaletteFormat format;

      public string TableName { get; private set; }

      public RangedObservableCollection<short> Colors { get; } = new RangedObservableCollection<short>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);

      public PaletteElementViewModel(ViewPort viewPort, PaletteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;
         TableName = viewPort.Model.GetAnchorFromAddress(-1, viewPort.Model.GetNextRun(itemAddress).Start);
         var destination = Model.ReadPointer(Start);
         var run = viewPort.Model.GetNextRun(destination) as IPaletteRun;
         Pages = run.Pages;
         UpdateColors(Start, 0);
      }

      /// <summary>
      /// Note that this method runs _before_ changes are copied from the baseclass
      /// So if we want to update colors based on the new start point,
      /// Then UpdateColors can't rely on our internal start point
      /// </summary>
      protected override bool TryCopy(PagedElementViewModel other) {
         if (!(other is PaletteElementViewModel that)) return false;
         format = that.format;
         UpdateColors(other.Start, other.CurrentPage);
         NotifyPropertyChanged(nameof(ColorWidth));
         NotifyPropertyChanged(nameof(ColorHeight));
         return true;
      }

      protected override void PageChanged() => UpdateColors(Start, CurrentPage);

      public void Activate() => UpdateSprites(TableName);

      private void UpdateSprites(string hint = null) {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (child == this) break;
            if (!(child is SpriteElementViewModel sevm)) continue;
            sevm.UpdateTiles(hint: hint);
         }
      }

      private void UpdateColors(int start, int page) {
         page %= Pages;
         var destination = Model.ReadPointer(start);
         var run = ViewPort.Model.GetNextRun(destination) as IPaletteRun;
         Colors.SetContents(run.GetPalette(Model, page));
      }
   }

   public class RangedObservableCollection<T> : List<T>, INotifyCollectionChanged {
      private readonly NotifyCollectionChangedEventArgs Reset = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public void SetContents(IEnumerable<T> elements) {
         Clear();
         AddRange(elements);
         CollectionChanged?.Invoke(this, Reset);
      }
   }
}
