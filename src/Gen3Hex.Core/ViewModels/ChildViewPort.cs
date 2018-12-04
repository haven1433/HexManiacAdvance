namespace HavenSoft.Gen3Hex.Core.ViewModels {
   /// <summary>
   ///  exactly replaces a ViewPort, except that it also has a known parent view
   /// </summary>
   public class ChildViewPort : ViewPort, IChildViewPort {
      public IViewPort Parent { get; }

      public ChildViewPort(IViewPort viewPort, byte[] data) : base(new Models.LoadedFile(string.Empty, data)) {
         Parent = viewPort;
         Width = Parent.Width;
      }
   }
}
