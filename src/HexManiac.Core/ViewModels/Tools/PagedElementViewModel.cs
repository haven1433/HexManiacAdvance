using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class PagedElementViewModel : StreamElementViewModel, IPagedViewModel {

      public bool HasMultiplePages => Pages > 1;

      private int currentPage;
      public int CurrentPage {
         get => currentPage;
         set => TryUpdate(ref currentPage, value);
      }

      public int Pages { get; protected set; }

      private readonly StubCommand previousPage = new StubCommand();
      public ICommand PreviousPage => previousPage;

      private readonly StubCommand nextPage = new StubCommand();
      public ICommand NextPage => nextPage;

      public PagedElementViewModel(ViewPort viewPort, int start) : base(viewPort, start) { }

      public void UpdateOtherPagedViewModels() {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is IPagedViewModel pvm)) continue;
            pvm.CurrentPage = pvm.Pages > CurrentPage ? CurrentPage : 0;
         }
      }

      protected override bool TryCopy(StreamElementViewModel other) {
         if (!(other is PagedElementViewModel that)) return false;
         if (!TryCopy(that)) return false;

         currentPage = that.currentPage;
         Pages = that.Pages;
         NotifyPropertyChanged(nameof(Pages));
         NotifyPropertyChanged(nameof(HasMultiplePages));

         return true;
      }

      protected abstract bool TryCopy(PagedElementViewModel other);
   }
}
