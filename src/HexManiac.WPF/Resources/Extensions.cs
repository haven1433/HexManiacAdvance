using System;
using System.Windows.Controls.Primitives;

namespace HavenSoft.HexManiac.WPF.Resources {
   public static class Extensions {
      public static void Reposition(this Popup popup) {
         var updateMethod = typeof(Popup).GetMethod("Reposition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
         updateMethod.Invoke(popup, Array.Empty<object>());
      }
   }
}
