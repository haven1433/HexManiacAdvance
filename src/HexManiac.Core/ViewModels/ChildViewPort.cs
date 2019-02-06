using HavenSoft.HexManiac.Core.Models;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   ///  exactly replaces a ViewPort, except that it also has a known parent view
   /// </summary>
   public class ChildViewPort : ViewPort, IChildViewPort {
      public IViewPort Parent { get; }

      public ChildViewPort(IViewPort viewPort) : base(new Models.LoadedFile(string.Empty, viewPort.Model.RawData), viewPort.Model) {
         Parent = viewPort;
         Width = Parent.Width;
      }
   }
}
