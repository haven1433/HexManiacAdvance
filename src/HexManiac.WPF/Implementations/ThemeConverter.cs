using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Implementations {
   public class ThemeConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         var text = value?.ToString();
         if (text == null) return null;
         return (Brush)Application.Current.Resources.MergedDictionaries[0][text];
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         throw new NotImplementedException();
      }
   }
}
