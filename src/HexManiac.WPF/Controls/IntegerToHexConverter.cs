using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class IntegerToHexConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         if (value is int number) return number.ToString("X6");
         return "000000";
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         if (int.TryParse(value.ToString(), NumberStyles.HexNumber, null, out int result)) {
            return result;
         } else {
            return -1;
         }
      }
   }
}
