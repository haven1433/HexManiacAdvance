namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IChildViewPort : IViewPort {
      IViewPort Parent { get; }
   }
}
