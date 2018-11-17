namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   ///  exactly replaces a ViewPort, except that it also has a known parent view
   /// </summary>
   public class ChildViewPort : ViewPort {
      public IViewPort Parent { get; }

      public ChildViewPort(IViewPort viewPort, byte[] data) : base(new Model.LoadedFile(string.Empty, data)) {
         Parent = viewPort;
         Width = Parent.Width;
      }
   }
}
