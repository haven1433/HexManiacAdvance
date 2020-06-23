using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class PagedElementViewModel : StreamElementViewModel, IPagedViewModel {

      public bool ShowPageControls => Pages > 1 || CanExecuteAddPage();

      private int currentPage;
      public int CurrentPage {
         get => currentPage;
         set {
            if (!TryUpdate(ref currentPage, value % Pages)) return;
            previousPage.CanExecuteChanged.Invoke(previousPage, EventArgs.Empty);
            nextPage.CanExecuteChanged.Invoke(nextPage, EventArgs.Empty);
            UpdateOtherPagedViewModels();
            addPage?.CanExecuteChanged.Invoke(addPage, EventArgs.Empty);
            PageChanged();
         }
      }

      public int Pages { get; protected set; }

      #region Commands

      private readonly StubCommand previousPage = new StubCommand();
      public ICommand PreviousPage => previousPage;

      private readonly StubCommand nextPage = new StubCommand();
      public ICommand NextPage => nextPage;

      private StubCommand addPage, deletePage;
      public ICommand AddPage => StubCommand(ref addPage, ExecuteAddPage, CanExecuteAddPage);
      public ICommand DeletePage => StubCommand(ref deletePage, ExecuteDeletePage, CanExecuteDeletePage);
      protected abstract bool CanExecuteAddPage();
      protected abstract bool CanExecuteDeletePage();
      protected virtual void ExecuteAddPage() {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is PagedElementViewModel pvm)) continue;
            if (pvm.Pages == Pages - 1) pvm.ExecuteAddPage();
         }
         deletePage?.CanExecuteChanged.Invoke(deletePage, EventArgs.Empty);
      }
      protected virtual void ExecuteDeletePage() {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is PagedElementViewModel pvm)) continue;
            if (pvm.Pages == Pages + 1) pvm.ExecuteDeletePage();
         }
         deletePage?.CanExecuteChanged.Invoke(deletePage, EventArgs.Empty);
      }

      #endregion

      public PagedElementViewModel(ViewPort viewPort, int start) : base(viewPort, start) {
         Pages = 1;

         // update usage count, assuming that our run is paged.
         var run = Model.GetNextRun(Model.ReadPointer(Start)) as IPagedRun;
         UsageCount = run?.PointerSources?.Count ?? 0;

         previousPage.CanExecute = arg => currentPage > 0;
         previousPage.Execute = arg => CurrentPage -= 1;
         nextPage.CanExecute = arg => currentPage < Pages - 1;
         nextPage.Execute = arg => CurrentPage += 1;
      }

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
         NotifyPropertyChanged(nameof(ShowPageControls));
         addPage?.CanExecuteChanged.Invoke(addPage, EventArgs.Empty);
         deletePage?.CanExecuteChanged.Invoke(deletePage, EventArgs.Empty);
         NotifyPropertyChanged(nameof(CurrentPage));
         previousPage.CanExecuteChanged.Invoke(previousPage, EventArgs.Empty);
         nextPage.CanExecuteChanged.Invoke(nextPage, EventArgs.Empty);

         return true;
      }

      protected abstract bool TryCopy(PagedElementViewModel other);

      protected abstract void PageChanged();
   }
}
