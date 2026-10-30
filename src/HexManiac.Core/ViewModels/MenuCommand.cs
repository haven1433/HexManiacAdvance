using System;
using System.ComponentModel;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {

   public interface IMenuCommand : ICommand { string Text { get; } }

   public record MenuCommand(string Text, Action Execute, Func<bool> CanExecute = null) : INotifyPropertyChanged, IMenuCommand {
      public event EventHandler? CanExecuteChanged;
      public event PropertyChangedEventHandler? PropertyChanged;
      bool ICommand.CanExecute(object? parameter) => CanExecute?.Invoke() ?? true;
      void ICommand.Execute(object? parameter) => Execute?.Invoke();
   }

   public record MenuCommand<T>(string Text, Action<T> Execute, Func<T, bool> CanExecute = null) : INotifyPropertyChanged, IMenuCommand {
      public event EventHandler? CanExecuteChanged;
      public event PropertyChangedEventHandler? PropertyChanged;

      public T Parameter { get; init; }

      bool ICommand.CanExecute(object? parameter) => (parameter ?? Parameter) is T arg ? (CanExecute?.Invoke(arg) ?? true) : false;

      void ICommand.Execute(object? parameter) { if ((parameter ?? Parameter) is T arg) Execute?.Invoke(arg); }
   }
}
