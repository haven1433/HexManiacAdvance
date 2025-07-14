using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class PagedElementViewModel : StreamElementViewModel, IPagedViewModel {

      public int currentPage;

      public int Pages { get; protected set; }

      #region Commands

      public bool CanMovePrevious => currentPage > 0;
      public bool CanMoveNext => currentPage < Pages - 1;

      #endregion

   }
}
