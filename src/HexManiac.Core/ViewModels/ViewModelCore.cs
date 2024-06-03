using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public static class PropertyChangedEventHandlerExtensions {
      public static void Notify(this PropertyChangedEventHandler handler, INotifyPropertyChanged sender, [CallerMemberName]string propertyName = null) {
         handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
      }

      public static void Notify<T>(this PropertyChangedEventHandler handler, INotifyPropertyChanged sender, T oldValue, [CallerMemberName]string propertyName = null) {
         handler?.Invoke(sender, new ExtendedPropertyChangedEventArgs<T>(oldValue, propertyName));
      }

      public static void Notify(this PropertyChangedEventHandler handler, INotifyPropertyChanged sender, PropertyChangedEventArgs args) => handler?.Invoke(sender, args);

      public static bool TryUpdate<T>(this PropertyChangedEventHandler handler, INotifyPropertyChanged sender, ref T field, T value, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         if (field == null && value == null) return false;
         if (field != null && field.Equals(value)) return false;
         var oldValue = field;
         field = value;
         handler.Notify(sender, oldValue, propertyName);
         return true;
      }

      public static bool TryUpdateEnum<T>(this PropertyChangedEventHandler handler, INotifyPropertyChanged sender, ref T field, T value, [CallerMemberName]string propertyName = null) where T : Enum {
         if (field.Equals(value)) return false;
         var oldValue = field;
         field = value;
         handler.Notify(sender, oldValue, propertyName);
         return true;
      }

      public static void Bind<T>(this T viewModel, string propertyName, Action<T, PropertyChangedEventArgs> handler) where T : INotifyPropertyChanged {
         viewModel.PropertyChanged += (sender, e) => {
            if (propertyName == e.PropertyName) {
               var instance = (T)sender;
               handler(instance, e);
            }
         };
      }
   }

   /// <summary>
   /// Represents a superset of property notifiers
   /// with functionality to temporarily turn notifications off.
   /// This makes them less noisy in situations where a massive number of notifications are happening.
   /// </summary>
   public interface ICanSilencePropertyNotifications : INotifyPropertyChanged {
      IDisposable SilencePropertyNotifications();
   }

   /// <summary>
   /// Utility base-class that adds the PropertyChanged event and adds protected methods to simplify calling it.
   /// It also adds protected methods to simplify the creation of commands.
   /// </summary>
   public class ViewModelCore : ICanSilencePropertyNotifications {
#if DEBUG
      private static int IDGenerator = 0;
      public int ID { get; } = ++IDGenerator;
#endif

      public event PropertyChangedEventHandler PropertyChanged;

      private int deferCount = 0;
      private ISet<string> deferredPropertyNotifications;

      private readonly Stack<List<IDisposable>> silenceScopes = new();
      private readonly List<ICanSilencePropertyNotifications> silentChildren = new();
      protected void AddSilentChild(ICanSilencePropertyNotifications child) {
         silentChildren.Add(child);
         if (silenceScopes.Count > 0) {
            silenceScopes.Peek().Add(child.SilencePropertyNotifications());
         }
      }
      protected void RemoveSilentChild(ICanSilencePropertyNotifications child) => silentChildren.Remove(child);
      protected void ClearSilentChildren() => silentChildren.Clear();
  
      public IDisposable SilencePropertyNotifications() {
         deferCount += 1;
         silenceScopes.Push(silentChildren.Select(child => child.SilencePropertyNotifications()).ToList());
         deferredPropertyNotifications ??= new HashSet<string>();
         return new StubDisposable { Dispose = () => {
            deferCount -= 1;
            silenceScopes.Pop().ForEach(scope => scope.Dispose());
            if (deferCount != 0) {
               deferredPropertyNotifications.Clear();
            } else {
               deferredPropertyNotifications = null;
            }
         } };
      }

      public IDisposable DeferPropertyNotifications() {
         deferCount += 1;
         deferredPropertyNotifications ??= new HashSet<string>();
         return new StubDisposable { Dispose = () => {
            deferCount -= 1;
            if (deferCount != 0) return;
            var properties = deferredPropertyNotifications.ToArray();
            deferredPropertyNotifications = null;
            NotifyPropertiesChanged(properties);
         } };
      }

      protected void NotifyPropertyChanged([CallerMemberName]string propertyName = null) {
         Debug.Assert(GetType().GetProperty(propertyName) != null, $"Expected {propertyName} to be a property on type {GetType().Name}!");
         if (deferredPropertyNotifications != null) {
            deferredPropertyNotifications.Add(propertyName);
         } else {
            PropertyChanged?.Notify(this, propertyName);
         }
      }

      /// <summary>
      /// The oldValue will not be stored / notified if we're deferring property notifications
      /// </summary>
      protected void NotifyPropertyChanged<T>(T oldValue, [CallerMemberName]string propertyName = null) {
         Debug.Assert(GetType().GetProperty(propertyName) != null, $"Expected {propertyName} to be a property on type {GetType().Name}!");
         if (deferredPropertyNotifications != null) {
            deferredPropertyNotifications.Add(propertyName);
         } else {
            PropertyChanged?.Notify(this, oldValue, propertyName);
         }
      }

      protected void NotifyPropertiesChanged(params string[] propertyNames) {
         foreach (var name in propertyNames) NotifyPropertyChanged(name);
      }

      protected void NotifyPropertyChanged(PropertyChangedEventArgs args) {
         Debug.Assert(GetType().GetProperty(args.PropertyName) != null, $"Expected {args.PropertyName} to be a property on type {GetType().Name}!");
         PropertyChanged?.Notify(this, args);
      }

      /// <summary>
      /// Utility function to make writing property updates easier.
      /// If the backing field's value does not match the new value, the backing field is updated and PropertyChanged gets called.
      /// </summary>
      /// <typeparam name="T">The type of the property being updated.</typeparam>
      /// <param name="backingField">A reference to the backing field of the property being changed.</param>
      /// <param name="newValue">The new value for the property.</param>
      /// <param name="propertyName">The name of the property to notify on. If the property is the caller, the compiler will figure this parameter out automatically.</param>
      /// <returns>false if the data did not need to be updated, true if it did.</returns>
      protected bool TryUpdate<T>(ref T field, T value, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         if (field == null && value == null) return false;
         if (field != null && field.Equals(value)) return false;
         var oldValue = field;
         field = value;
         NotifyPropertyChanged(oldValue, propertyName);
         return true;
      }

      protected void Set<T>(ref T field, T value, Action<T> changeHandler, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         var oldValue = field;
         if (TryUpdate(ref field, value, propertyName)) {
            changeHandler?.Invoke(oldValue);
         }
      }

      protected void SetEnum<T>(ref T field, T value, Action<T> changeHandler, [CallerMemberName]string propertyName = null) where T : Enum {
         var oldValue = field;
         if (PropertyChanged.TryUpdateEnum(this, ref field, value, propertyName)) {
            changeHandler?.Invoke(oldValue);
         }
      }

      protected void Set<T>(ref T field, T value, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         Set(ref field, value, null, propertyName);
      }

      protected void SetEnum<T>(ref T field, T value, [CallerMemberName]string propertyName = null)where T : Enum {
         SetEnum(ref field, value, null, propertyName);
      }

      protected bool TryUpdateEnum<T>(ref T backingField, T newValue, [CallerMemberName]string propertyName = null) where T : Enum {
         return PropertyChanged.TryUpdateEnum(this, ref backingField, newValue, propertyName);
      }

      protected bool TryUpdateSequence<T, U>(ref T backingField, T newValue, [CallerMemberName]string propertyName = null) where T : IEnumerable<U> where U : IEquatable<U> {
         if (backingField == null && newValue == null) return false;
         if (backingField != null && backingField.Count() == newValue.Count()) {
            bool allMatch = true;
            foreach (var pair in backingField.Zip(newValue, (a, b) => (a, b))) {
               if (pair.a.Equals(pair.b)) continue;
               allMatch = false;
               break;
            }
            if (allMatch) return false;
         }
         var oldValue = backingField;
         backingField = newValue;
         NotifyPropertyChanged(oldValue, propertyName);
         return true;
      }

      protected StubCommand StubCommand<T>(ref StubCommand field, Action<T> execute, Func<T, bool> canExecute = null) {
         if (field != null) return field;
         field = new StubCommand {
            Execute = arg => execute(arg is T t ? t : default(T)),
            CanExecute = arg => canExecute?.Invoke((T)arg) ?? true,
         };
         return field;
      }

      protected StubCommand StubCommand(ref StubCommand field, Action execute, Func<bool> canExecute = null) {
         if (field != null) return field;
         field = new StubCommand {
            Execute = arg => execute(),
            CanExecute = arg => canExecute?.Invoke() ?? true,
         };
         return field;
      }

      protected static IDisposable Scope<T>(ref T field, T value, Action<T> lambda) {
         var originalValue = field;
         var disposable = new StubDisposable { Dispose = () => lambda(originalValue) };
         field = value;
         return disposable;
      }
   }

   public class ExtendedPropertyChangedEventArgs<T> : PropertyChangedEventArgs {
      public T OldValue { get; }
      public ExtendedPropertyChangedEventArgs(T oldValue, string propertyName) : base(propertyName) => OldValue = oldValue;
   }
}
