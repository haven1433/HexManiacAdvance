using HavenSoft.ViewModel.DataFormats;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class HexElement {
      public byte Value { get; set; }
      public IDataFormat Format { get; set; }
   }
}