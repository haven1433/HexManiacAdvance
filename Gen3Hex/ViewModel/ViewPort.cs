using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : INotifyPropertyChanged {

      #region Width

      private int width;

      public int Width {
         get => width;
         set => Update(ref width, value);
      }

      #endregion

      #region Height

      private int height;

      public int Height {
         get => height;
         set => Update(ref height, value);
      }

      #endregion

      public int MinimumScroll { get; set; }

      public int MaximumScroll { get; set; }

      public HexElement this[int x, int y] => new HexElement();

      public event PropertyChangedEventHandler PropertyChanged;

      public void LoadFile(LoadedFile file) {
         // TODO use the data
      }

      /// <summary>
      /// Utility function to make writing property updates easier.
      /// </summary>
      /// <typeparam name="T">The type of the property being updated.</typeparam>
      /// <param name="field">A reference to the backing field of the property being changed.</param>
      /// <param name="value">The new value for the property.</param>
      /// <param name="propertyName">The name of the property to notify on. If the property is the caller, the compiler will figure this parameter out automatically.</param>
      private void Update<T>(ref T field, T value, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         if (field.Equals(value)) return;
         field = value;
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
   }
}
