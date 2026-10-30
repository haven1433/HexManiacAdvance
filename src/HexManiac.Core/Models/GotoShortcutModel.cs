namespace HavenSoft.HexManiac.Core.Models {
   public class GotoShortcutModel {
      public string ImageAnchor { get; }
      public string GotoAnchor { get; }
      public string DisplayText { get; }

      public GotoShortcutModel(string imageAnchor, string gotoAnchor, string displayText) {
         ImageAnchor = imageAnchor;
         GotoAnchor = gotoAnchor;
         DisplayText = displayText;
      }
   }
}
