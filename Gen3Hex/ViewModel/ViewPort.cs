using HavenSoft.Gen3Hex.Model;
using HavenSoft.ViewModel;
using HavenSoft.ViewModel.DataFormats;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : INotifyPropertyChanged, INotifyCollectionChanged {
      private readonly byte[] data;

      private readonly NotifyCollectionChangedEventArgs resetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

      private int dataIndex;

      #region Name

      private string name;

      public string Name {
         get => name;
         set => Update(ref name, value);
      }

      #endregion

      #region Width

      private int width;

      public int Width {
         get => width;
         set {
            Update(ref width, value);
            UpdateScrollRange();
         }
      }

      #endregion

      #region Height

      private int height;

      public int Height {
         get => height;
         set {
            Update(ref height, value);
            UpdateScrollRange();
         }
      }

      #endregion

      #region MinimumScroll

      private int minimumScroll;

      public int MinimumScroll {
         get => minimumScroll;
         set => Update(ref minimumScroll, value);
      }

      #endregion

      #region ScrollValue

      private int scrollValue;

      public int ScrollValue {
         get => scrollValue;
         set {
            value = Math.Min(Math.Max(minimumScroll, value), maximumScroll);
            var dif = value - scrollValue;
            dataIndex += dif * width;
            Update(ref scrollValue, value);
            NotifyCollectionChanged(resetArgs);
         }
      }

      private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);

      #endregion

      #region MaximumScroll

      private int maximumScroll;

      public int MaximumScroll {
         get => maximumScroll;
         set => Update(ref maximumScroll, value);
      }

      #endregion

      public HexElement this[int x, int y] {
         get {
            var index = y * Width + x + dataIndex;
            if (index < 0 || index >= data.Length) {
               return new HexElement { Format = Undefined.Instance };
            }

            return new HexElement {
               Format = new None(data[index]),
               Value = data[index],
            };
         }
      }

      public event PropertyChangedEventHandler PropertyChanged;

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ViewPort() { data = new byte[0]; }

      public ViewPort(LoadedFile file) {
         name = file.Name;
         data = file.Contents;
      }

      /// <summary>
      /// Utility function to make writing property updates easier.
      /// </summary>
      /// <typeparam name="T">The type of the property being updated.</typeparam>
      /// <param name="backingField">A reference to the backing field of the property being changed.</param>
      /// <param name="newValue">The new value for the property.</param>
      /// <param name="propertyName">The name of the property to notify on. If the property is the caller, the compiler will figure this parameter out automatically.</param>
      private void Update<T>(ref T backingField, T newValue, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         if (backingField.Equals(newValue)) return;
         backingField = newValue;
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }

      private void UpdateScrollRange() {
         var lineCount = (int)Math.Ceiling((double)data.Length / width);

         MinimumScroll = 1 - height;
         MaximumScroll = lineCount - 1;
         var newCurrentScroll = (int)Math.Ceiling((double)dataIndex / width);
         Update(ref scrollValue, newCurrentScroll, nameof(ScrollValue));
         NotifyCollectionChanged(resetArgs);
      }
   }
}
