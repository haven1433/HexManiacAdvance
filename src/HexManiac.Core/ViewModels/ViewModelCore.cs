using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class ViewModelCore : INotifyPropertyChanged {
      public event PropertyChangedEventHandler PropertyChanged;

      protected void NotifyPropertyChanged([CallerMemberName]string propertyName = null) {
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }

      protected void NotifyPropertyChanged(object oldValue, [CallerMemberName]string propertyName = null) {
         PropertyChanged?.Invoke(this, new ExtendedPropertyChangedEventArgs(oldValue, propertyName));
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
      protected bool TryUpdate<T>(ref T backingField, T newValue, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         if (backingField == null && newValue == null) return false;
         if (backingField != null && backingField.Equals(newValue)) return false;
         var oldValue = backingField;
         backingField = newValue;
         NotifyPropertyChanged(oldValue, propertyName);
         return true;
      }

      protected bool TryUpdateEnum<T>(ref T backingField, T newValue, [CallerMemberName]string propertyName = null) where T : Enum {
         if (backingField.Equals(newValue)) return false;
         var oldValue = backingField;
         backingField = newValue;
         NotifyPropertyChanged(oldValue, propertyName);
         return true;
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
   }

   public class ExtendedPropertyChangedEventArgs : PropertyChangedEventArgs {
      public object OldValue { get; }
      public ExtendedPropertyChangedEventArgs(object oldValue, string propertyName) : base(propertyName) => OldValue = oldValue;
   }
}
