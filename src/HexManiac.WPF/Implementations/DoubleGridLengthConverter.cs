using System;
using System.Windows;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Implementations {
   // from https://stackoverflow.com/questions/5259729/wpf-gridsplitter-replaces-binding-on-row-height-property
   public class DoubleGridLengthConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
         return new GridLength((double)value);
      }
      public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
         GridLength gridLength = (GridLength)value;
         return gridLength.Value;
      }
   }
}
