using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Windows;
using System.ComponentModel;
using System.Windows;

namespace HavenSoft.HexManiac.WPF.Resources {
   public class DesignerThemeResource : ResourceDictionary {
      public DesignerThemeResource() {
         if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) {
            var theme = new Theme(new string[0]);
            App.UpdateThemeDictionary(this, theme);
         }
      }
   }
}
