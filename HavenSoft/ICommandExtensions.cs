using System.Windows.Input;

namespace HavenSoft {
   public static class ICommandExtensions {
      /// <summary>
      /// Runs execute on the command with a null parameter.
      /// </summary>
      public static void Execute(this ICommand command) => command.Execute(null);
   }
}
