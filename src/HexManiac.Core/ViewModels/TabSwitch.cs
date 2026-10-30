namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A tab that knows when selecting it again needs no work.
   /// <see cref="ITabContent.Refresh"/> means 'something may have changed behind your back, rebuild everything', and that is what a tab gets when its model was edited from somewhere else.
   /// When the tab is only selected again with nothing edited in between, rebuilding is wasted time, and with a lot of open tabs it is most of the time spent switching between them.
   /// </summary>
   public interface IRefreshOnSelect {
      /// <summary>The tab was selected: bring it up to date, but only do the work that its model's edit counter (see <see cref="Models.ModelEditStamp"/>) says is needed.</summary>
      void RefreshOnSelect();
   }

   public static class TabSwitch {
      /// <summary>What the editor does to a tab that was just selected.</summary>
      public static void Refresh(ITabContent tab) {
         if (tab is IRefreshOnSelect cheap) cheap.RefreshOnSelect();
         else tab.Refresh();
      }
   }
}
