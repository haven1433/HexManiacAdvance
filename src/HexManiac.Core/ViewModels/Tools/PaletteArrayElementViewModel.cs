using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteArrayElementViewModel : ViewModelCore, IPagedViewModel {
      private readonly ViewPort viewPort;
      private PaletteFormat format;

      public event EventHandler<(int originalStart, int newStart)> DataMoved;
      public event EventHandler DataChanged;

      public string QualifiedName { get; private set; }
      public string Name { get; private set; }
      public int Start { get; private set; } // a pointer to the sprite's compressed data

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      public ObservableCollection<short> Colors { get; } = new ObservableCollection<short>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);
      public int PixelWidth => 64;
      public int PixelHeight => 64;

      #region Pages

      public bool HasMultiplePages => Pages > 1;

      private int currentPage;
      public int CurrentPage {
         get => currentPage;
         set {
            if (!TryUpdate(ref currentPage, value)) return;
            UpdateColors();
         }
      }

      public int Pages { get; private set; }

      private readonly StubCommand previousPage = new StubCommand();
      public ICommand PreviousPage => previousPage;

      private readonly StubCommand nextPage = new StubCommand();
      public ICommand NextPage => nextPage;

      public void UpdateOtherPagedViewModels() {
         foreach (var child in viewPort.Tools.TableTool.Children) {
            if (!(child is IPagedViewModel pvm)) continue;
            pvm.CurrentPage = pvm.Pages > CurrentPage ? CurrentPage : 0;
         }
      }

      #endregion

      private string errorText;
      public string ErrorText {
         get => errorText;
         private set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public PaletteArrayElementViewModel(ViewPort viewPort, PaletteFormat format, string name, int itemAddress) {
         this.viewPort = viewPort;
         this.format = format;
         Name = name;
         Start = itemAddress;
         var parentName = viewPort.Model.GetAnchorFromAddress(-1, viewPort.Model.GetNextRun(itemAddress).Start);
         QualifiedName = $"{parentName}/{name}";
         UpdateColors();

         previousPage.CanExecute = arg => currentPage > 0;
         previousPage.Execute = arg => { CurrentPage -= 1; UpdateOtherPagedViewModels(); Activate(); };
         nextPage.CanExecute = arg => currentPage < Pages - 1;
         nextPage.Execute = arg => { CurrentPage += 1; UpdateOtherPagedViewModels(); Activate(); };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is PaletteArrayElementViewModel that)) return false;
         Name = that.Name;
         Start = that.Start;
         format = that.format;
         ErrorText = that.ErrorText;
         NotifyPropertyChanged(nameof(Name));
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(ErrorText));
         UpdateColors();
         return true;
      }

      public void Activate() {
         foreach (var child in viewPort.Tools.TableTool.Children) {
            if (!(child is SpriteArrayElementViewModel saevm)) continue;
            saevm.UpdateTiles(QualifiedName);
         }
      }

      private void UpdateColors() {
         var destination = viewPort.Model.ReadPointer(Start);
         var data = LZRun.Decompress(viewPort.Model, destination);
         int colorCount = (int)Math.Pow(2, format.Bits);
         int pageLength = colorCount * 2;
         Debug.Assert(data.Length % colorCount * 2 == 0);
         Colors.Clear();
         Pages = data.Length / pageLength;
         if (currentPage >= Pages) currentPage = 0;
         int pageStart = CurrentPage * pageLength;
         for (int i = 0; i < colorCount; i++) {
            var color = (short)data.ReadMultiByteValue(pageStart + i * 2, 2);
            Colors.Add(color);
         }

         NotifyPropertyChanged(nameof(CurrentPage));
         NotifyPropertyChanged(nameof(Pages));
         previousPage.CanExecuteChanged.Invoke(previousPage, EventArgs.Empty);
         nextPage.CanExecuteChanged.Invoke(nextPage, EventArgs.Empty);
      }
   }
}
