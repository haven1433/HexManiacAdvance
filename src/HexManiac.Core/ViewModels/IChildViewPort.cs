namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IChildViewPort : IViewPort {
      int PreferredWidth { get; set; }
      IViewPort Parent { get; }
   }
}
