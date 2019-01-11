using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.Gen3Hex.WPF.Controls {
   public class IntegerToHexConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         if (value is int number) return number.ToString("X6");
         return "000000";
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         return int.Parse(value.ToString(), NumberStyles.HexNumber);
      }
   }
}
