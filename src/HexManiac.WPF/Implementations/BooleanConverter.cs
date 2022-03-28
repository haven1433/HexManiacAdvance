using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Implementations {
   public class BooleanConverter : IValueConverter {
      public object True { get; set; }

      public object False { get; set; }

      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? True : False;

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value?.Equals(True) ?? false;
   }
}
