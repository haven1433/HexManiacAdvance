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
            NotifyPropertyChanged(nameof(CanMovePrevious));
            NotifyPropertyChanged(nameof(CanMoveNext));
            UpdateOtherPagedViewModels();
            addPage.RaiseCanExecuteChanged();
            PageChanged();
         }
      }

      public int Pages { get; protected set; }

      #region Commands

      public bool CanMovePrevious => currentPage > 0;
      public bool CanMoveNext => currentPage < Pages - 1;
      public void MovePrevious() => CurrentPage -= 1;
      public void MoveNext() => CurrentPage += 1;

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
         NotifyPropertyChanged(nameof(CanMovePrevious));

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
         NotifyPropertyChanged(nameof(CanMoveNext));

         PageChanged();
      }

      #endregion

      public PagedElementViewModel(ViewPort viewPort, string parentName, string runFormat, int start) : base(viewPort, parentName, runFormat, start) {
         Pages = 1;

         // update usage count, assuming that our run is paged.
         var run = Model.GetNextRun(Model.ReadPointer(Start)) as IPagedRun;
         UsageCount = run?.PointerSources?.Count ?? 0;
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
         NotifyPropertyChanged(nameof(CanMovePrevious));
         NotifyPropertyChanged(nameof(CanMoveNext));

         return true;
      }

      protected abstract bool TryCopy(PagedElementViewModel other);

      protected abstract void PageChanged();
   }
}
