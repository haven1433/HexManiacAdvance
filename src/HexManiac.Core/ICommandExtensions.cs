using System.Windows.Input;

namespace HavenSoft.HexManiac.Core {
   public static class ICommandExtensions {
      /// <summary>
      /// Runs execute on the command with a null parameter.
      /// </summary>
      public static void Execute(this ICommand command) => command.Execute(null);

      /// <summary>
      /// Utility implementation of CanExecute for commands that can always execute.
      /// </summary>
      public static bool CanAlwaysExecute(object parameter) => true;
   }
}
