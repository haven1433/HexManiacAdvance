using HavenSoft.HexManiac.Core.Models;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   ///  exactly replaces a ViewPort, except that it also has a known parent view
   /// </summary>
   public class ChildViewPort : ViewPort, IChildViewPort {
      public IViewPort Parent { get; }

      public ChildViewPort(IViewPort viewPort, IWorkDispatcher dispatcher, Singletons singletons) : base(viewPort.FileName, viewPort.Model, dispatcher, singletons, viewPort.ChangeHistory) {
         Parent = viewPort;
         Width = Parent.Width;
      }
   }
}
