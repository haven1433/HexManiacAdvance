using HavenSoft.Gen3Hex.Model;
using HavenSoft.ViewModel;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

      #region MinimumScroll

      private int minimumScroll;

      public int MinimumScroll {
         get => minimumScroll;
         set => Update(ref minimumScroll, value);
      }

      #endregion

      #region MaximumScroll

      private int maximumScroll;

      public int MaximumScroll {
         get => maximumScroll;
         set => Update(ref maximumScroll, value);
      }

      #endregion

      public HexElement this[int x, int y] => new HexElement { Format = CommonFormats.Undefined.Instance };

      public event PropertyChangedEventHandler PropertyChanged;

      public ViewPort() { }

      public ViewPort(LoadedFile file) { }

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
