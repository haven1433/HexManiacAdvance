namespace HavenSoft.Gen3Hex.Core.Models {
   public class LoadedFile {
      public string Name { get; }

      public byte[] Contents { get; }

      public LoadedFile(string name, byte[] contents) {
         Name = name;
         Contents = contents;
      }
   }
}
