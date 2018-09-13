namespace HavenSoft.ViewModel {
   /// <summary>
   /// Data Formats are simple types that provide limited meta-data that can vary based on the format.
   /// This type works sort of like an enum: the UI should iterate through each of these options and be able to handle them separately.
   /// </summary>
   public class CommonFormats {
      public class Undefined {
         public static Undefined Instance { get; } = new Undefined();
         private Undefined() { }
      }

      public class None {
         public bool IsUnused { get; }
         public None(byte value) => IsUnused = value == 0x00 || value == 0xFF;
      }
   }
}