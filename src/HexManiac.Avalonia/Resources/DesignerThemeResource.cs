using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Avalonia.Views;
using Avalonia.Controls;

namespace HavenSoft.HexManiac.Avalonia.Resources {
   public class DesignerThemeResource : ResourceDictionary {
      public DesignerThemeResource() {
         if (Design.IsDesignMode) {
            var theme = new Theme([]);
            App.UpdateThemeDictionary(this, theme);
         }
      }
   }
}
