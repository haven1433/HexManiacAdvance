using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class PagedElementViewModel : StreamElementViewModel, IPagedViewModel {

      public virtual bool ShowPageControls => Pages > 1 || CanExecuteAddPage();

      private int currentPage;
      public int CurrentPage {
         get => currentPage;
         set {
            if (!TryUpdate(ref currentPage, value % Pages)) return;
            previousPage.RaiseCanExecuteChanged();
            nextPage.RaiseCanExecuteChanged();
            UpdateOtherPagedViewModels();
            addPage.RaiseCanExecuteChanged();
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
         var destination = ViewPort.Model.ReadPointer(Start);
         if (!(ViewPort.Model.GetNextRun(destination) is PagedLZRun run)) return;
         var newRun = run.AppendPage(ViewPort.CurrentChange);
         if (newRun.Start != run.Start) {
            ViewPort.RaiseMessage($"Data moved from {run.Start:X6} to {newRun.Start:X6}. Pointers were updated.");
         }
         Pages = newRun.Pages;
         currentPage = newRun.Pages - 1;

         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is PagedElementViewModel pvm)) continue;
            if (pvm.Pages == Pages - 1) pvm.ExecuteAddPage();
         }
         deletePage.RaiseCanExecuteChanged();
         previousPage.RaiseCanExecuteChanged();

         PageChanged();
      }
      protected virtual void ExecuteDeletePage() {
         var destination = ViewPort.Model.ReadPointer(Start);
         if (!(ViewPort.Model.GetNextRun(destination) is PagedLZRun run)) return;
         var newRun = run.DeletePage(CurrentPage, ViewPort.CurrentChange);
         Pages = newRun.Pages;
         if (CurrentPage >= Pages) currentPage = Pages - 1;

         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is PagedElementViewModel pvm)) continue;
            if (pvm.Pages == Pages + 1) pvm.ExecuteDeletePage();
         }
         deletePage.RaiseCanExecuteChanged();
         nextPage.RaiseCanExecuteChanged();

         PageChanged();
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
         Start = that.Start; // required in order to update the ShowPageControls value
         NotifyPropertyChanged(nameof(ShowPageControls));
         addPage.RaiseCanExecuteChanged();
         deletePage?.RaiseCanExecuteChanged();
         NotifyPropertyChanged(nameof(CurrentPage));
         previousPage.RaiseCanExecuteChanged();
         nextPage.RaiseCanExecuteChanged();

         return true;
      }

      protected abstract bool TryCopy(PagedElementViewModel other);

      protected abstract void PageChanged();
   }
}
