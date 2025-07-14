namespace HavenSoft.HexManiac.Core {
   public static class ICommandExtensions {
      /// <summary>
      /// Utility implementation of CanExecute for commands that can always execute.
      /// </summary>
      public static bool CanAlwaysExecute(object parameter) => true;
   }
}
