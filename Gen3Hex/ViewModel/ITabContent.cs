using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public interface ITabContent {
      string Name { get; }
      ICommand Save { get; }
      ICommand SaveAs { get; }
      ICommand Undo { get; }
      ICommand Redo { get; }
   }
}
