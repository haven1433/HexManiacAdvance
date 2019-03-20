using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class MultiplyConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         if (value is int number && parameter is double param) return number * param;
         return 0.0;
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         throw new NotImplementedException();
      }
   }
}
