using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class IntegerToBooleanViaMatchConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         var a = value as int?;
         var b = parameter as int?;
         if (a == null && b == null) return true;
         return a != null && b != null && a == (int)b;
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         var match = (bool)value;
         if (match) return parameter;
         return -1;
      }
   }
}
