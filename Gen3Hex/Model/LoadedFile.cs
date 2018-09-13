namespace HavenSoft.Gen3Hex.Model {
   public class LoadedFile {
      public string Name { get; }

      public byte[] Contents { get; }

      public LoadedFile(string name, byte[] contents) => (Name, Contents) = (name, contents);
   }
}
