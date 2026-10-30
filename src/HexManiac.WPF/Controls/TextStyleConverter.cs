using System;
using System.Globalization;
using System.Windows.Data;

namespace HavenSoft.HexManiac.WPF.Controls {
   public enum TextStle { Normal, Reminder }

   /// <summary>
   /// Extracts the 'style' of the text by looking at the text.
   /// </summary>
   public class TextStyleConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         if (value is not string str) return value;
         if (str.StartsWith("(") && str.EndsWith(")")) return TextStle.Reminder;
         return TextStle.Normal;
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         throw new NotImplementedException();
      }
   }
}
