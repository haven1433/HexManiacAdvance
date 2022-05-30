using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core {
   /// <summary>
   /// Conventions-based ICommand implementation.
   /// Given a name of an {Execute} method,
   /// it searches for a Can{Execute} method and a Can{Execute}Changed event.
   /// The Can{Execute} method and Can{Execute}Changed event are optional.
   /// </summary>
   public class MethodCommand : ICommand {
      private object context;
      public object Context {
         get => context;
         set {
            var oldContext = context;
            context = value;
            SetupFromContext(oldContext, context);
            RaiseCanExecuteChanged(this, EventArgs.Empty);
         }
      }

      public string ExecuteName { get; }
      public string CanExecuteName { get; }
      public string CanExecuteChangedName { get; }

      /// <param name="context">The object that has the members.</param>
      /// <param name="executeMethodName">The name to use for the Execute method.</param>
      public MethodCommand(object context, string executeMethodName, string canExecuteMethodName = null, string canExecuteChangedEventName = null) {
         ExecuteName = executeMethodName;
         CanExecuteName = canExecuteMethodName ?? $"Can{ExecuteName}";
         CanExecuteChangedName = canExecuteChangedEventName ?? $"Can{ExecuteName}Changed";
         Context = context;
      }

      private void SetupFromContext(object oldContext, object newContext) {
         if (oldContext != null) {
            var contextEvent = oldContext.GetType().GetEvent(CanExecuteChangedName);
            if (contextEvent != null) {
               contextEvent.RemoveEventHandler(oldContext, (EventHandler)RaiseCanExecuteChanged);
            }
            if (oldContext is INotifyPropertyChanged vm) vm.PropertyChanged -= OnContextPropertyChanged;
         }

         if (newContext != null) {
            var contextEvent = newContext.GetType().GetEvent(CanExecuteChangedName);
            if (contextEvent != null) {
               contextEvent.AddEventHandler(newContext, (EventHandler)RaiseCanExecuteChanged);
            }
            if (newContext is INotifyPropertyChanged vm) vm.PropertyChanged += OnContextPropertyChanged;
         }
      }

      private void OnContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName != CanExecuteName) return;
         RaiseCanExecuteChanged(this, e);
      }

      private void RaiseCanExecuteChanged(object sender, EventArgs e) => CanExecuteChanged?.Invoke(this, e);
      public event EventHandler CanExecuteChanged;

      /// <summary>
      /// Defaults to true if no Can{Execute} method is found.
      /// </summary>
      public bool CanExecute(object parameter) {
         if (Context == null) return false;
         var canExecuteMethod = Context.GetType().GetMethod(CanExecuteName);
         if (canExecuteMethod == null) {
            // property case
            var canExecuteProperty = Context.GetType().GetProperty(CanExecuteName);
            if (canExecuteProperty == null || canExecuteProperty.PropertyType != typeof(bool)) return true;
            return ((bool?)canExecuteProperty.GetValue(Context)) ?? true;
         }

         // method case
         if (canExecuteMethod.ReturnType != typeof(bool)) return true;
         switch( canExecuteMethod.GetParameters().Length) {
            case 0: return canExecuteMethod.Invoke(Context, Array.Empty<object>()) as bool? ?? true;
            case 1: return canExecuteMethod.Invoke(Context, new[] { parameter }) as bool? ?? true;
            default: return true;
         };
      }

      public void Execute(object parameter) {
         var execute = Context.GetType().GetMethod(ExecuteName);
         if (execute == null) return;
         object result;
         switch (execute.GetParameters().Length) {
            case 0: result = execute.Invoke(Context, Array.Empty<object>()); break;
            case 1: result = execute.Invoke(Context, new object[] { parameter }); break;
            default: throw new InvalidOperationException($"{ExecuteName}: too many parameters!");
         }
         if (result is Task task) {
            task.ContinueWith(t => {
               if (t.Exception == null) return;
               if (t.Exception.InnerExceptions.Count == 1) throw t.Exception.InnerExceptions[0];
               throw t.Exception;
            }, TaskContinuationOptions.OnlyOnFaulted);
         }
      }
   }
}
