using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class IntegerToHexConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         if (!(value is int number)) return "000000";
         if (number == Pointer.NULL) return "null";
         return number.ToString("X6");
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         var input = value.ToString();
         if (int.TryParse(value.ToString(), NumberStyles.HexNumber, null, out int result)) {
            return result;
         } else if (input.ToLower() == "null") {
            return Pointer.NULL;
         } else {
            return -1;
         }
      }
   }
}
