using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Resources {
   partial class Icons {
      public Icons() {
         InitializeComponent();
         foreach (Geometry geo in Items) geo.Freeze();
      }
   }
}
