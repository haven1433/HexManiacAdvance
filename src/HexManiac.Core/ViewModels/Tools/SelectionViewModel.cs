namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SelectionViewModel : ViewModelCore {
      private bool selected;
      public bool Selected { get => selected; set => Set(ref selected, value); }

      private string name;
      public string Name { get => name; set => Set(ref name, value); }

      private int index;
      public int Index { get => index; set => Set(ref index, value); }
   }
}
