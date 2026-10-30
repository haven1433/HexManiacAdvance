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
         if (input.StartsWith("<")) input = input.Substring(1);
         if (input.EndsWith(">")) input = input.Substring(0, input.Length - 1);
         if (int.TryParse(input, NumberStyles.HexNumber, null, out int result)) {
            return result;
         } else {
            return Pointer.NULL;
         }
      }
   }
}
