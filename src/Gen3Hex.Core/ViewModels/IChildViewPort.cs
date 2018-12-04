namespace HavenSoft.Gen3Hex.Core.ViewModels {
   public interface IChildViewPort : IViewPort {
      IViewPort Parent { get; }
   }
}
