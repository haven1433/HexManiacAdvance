using HavenSoft.HexManiac.Core.ViewModels;
using System;

namespace HavenSoft.HexManiac.Core.Models {
   public interface IFileSystem {
      string CopyText { get; set; }
      (short[] image, int width) CopyImage { get; set; }

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
      /// <returns>
      /// The new name if provided, or null if the operation was canceled by the user.
      /// </returns>
      string RequestNewName(string currentName, string extensionDescription = null, params string[] extensionOptions);

      /// <summary>
      /// Have the filesystem ask the user for an existing file.
      /// If no extensionOptions are provided, the user may specify any existing file.
      /// </summary>
      /// <param name="extensionOptions">
      /// A set of extensions, such as 'txt', 'gba', or 'png'. Should not start with a dot.
      /// The set may be empty, but not null.
      /// </param>
      /// <returns>
      /// If the user chooses a file, that file is loaded and returned.
      /// If the user cancels or selects an unreadable file, returns null.
      /// </returns>
      LoadedFile OpenFile(string extensionDescription = null, params string[] extensionOptions);

      /// <returns>true if the file can be loaded</returns>
      bool Exists(string file);

      /// <summary>
      /// Have the filesystem open a specific file.
      /// </summary>
      /// <returns>
      /// If the file exists, it is loaded and returned.
      /// If it doesn't exist, returns null.
      /// </returns>
      LoadedFile LoadFile(string fileName);

      /// <summary>
      /// When a file changes, the filesystem will call all listeners for that file.
      /// </summary>
      void AddListenerToFile(string fileName, Action<IFileSystem> listener);

      void RemoveListenerForFile(string fileName, Action<IFileSystem> listener);

      /// <summary>
      /// Saves the file without prompting the user for permission.
      /// </summary>
      /// <returns>
      /// true if it was possible to save, false if there was an error.
      /// In the case of false, the file system would have already dealt with the error.
      /// The return value is just so the program can know if it was written or not.
      /// </returns>
      bool Save(LoadedFile file);

      bool SaveMetadata(string originalFileName, string[] metadata);

      /// <summary>
      /// should prompt the user if they want to save and then save
      /// </summary>
      /// <returns>
      /// true if the user wanted to save the file and it was written successfully
      /// false if the user decided not to save the file
      /// null if there was any error or if the operation was canceled by the user.
      /// the FileSystem object will handle the error and potentially notify the user,
      /// the result is only so the program can know if it was written or not.
      /// </returns>
      bool? TrySavePrompt(LoadedFile file);

      /// <summary>
      /// Look for a metadata object that matches a filename.
      /// Return null if no metadata is found.
      /// </summary>
      string[] MetadataFor(string fileName);

      /// <summary>
      /// Show the user a dialog so they can select an image.
      /// Load the image into a 16bit (5r5g5b) array.
      /// </summary>
      (short[] image, int width) LoadImage();

      /// <summary>
      /// Show the user a dialog so they can create a file.
      /// Save this 16bit (5r5g5b) array into that file.
      /// </summary>
      void SaveImage(short[] image, int width);

      int ShowOptions(string title, string prompt, object[] additionalDetails, params VisualOption[] options);

      string RequestText(string title, string prompt);
   }

   public interface IWorkDispatcher {
      /// <summary>
      /// If there's a long-running task, you can use this to break it up into chunks.
      /// </summary>
      void DispatchWork(Action action);
   }

   public class InstantDispatch : IWorkDispatcher {
      public static IWorkDispatcher Instance { get; } = new InstantDispatch();
      public void DispatchWork(Action action) => action?.Invoke();
   }

   public class VisualOption : ViewModelCore {
      private int index;
      private string option, shortDescription, description;

      public int Index { get => index; set => Set(ref index, value); }
      public string Option { get => option; set => Set(ref option, value); }
      public string ShortDescription { get => shortDescription; set => Set(ref shortDescription, value); }
      public string Description { get => description; set => Set(ref description, value); }
   }
}
