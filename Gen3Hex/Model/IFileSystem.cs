namespace HavenSoft.Gen3Hex.Model {
   public interface IFileSystem {
      /// <summary>
      /// Have the filesystem ask the user for a new name for a file.
      /// If no extensionOptions are provided, the user may specify any extension.
      /// Otherwise, the returned name will end with one of the extensionOptions.
      /// </summary>
      /// <param name="currentName">
      /// The current name of the file, which may be either relative or absolute, including extension.
      /// This parameter may be empty, but not null.
      /// </param>
      /// <param name="extensionOptions">
      /// A set of extensions, such as 'txt', 'gba', or 'png'. Should not start with a dot.
      /// The set may be empty, but not null.
      /// </param>
      string RequestNewName(string currentName, params string[] extensionOptions);

      /// <returns>
      /// true if the file was written successfully, false if there was any error.
      /// null if the operation was canceled.
      /// the FileSystem object will handle the error and potentially notify the user,
      /// the result is only so the program can know if it was written or not.
      /// </returns>
      bool? TrySave(LoadedFile file);

   }
}
