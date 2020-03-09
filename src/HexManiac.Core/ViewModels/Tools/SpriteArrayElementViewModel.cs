namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   internal class SpriteArrayElementViewModel : StreamArrayElementViewModel {
      // TODO add stuff to let the view display the sprite
      public SpriteArrayElementViewModel(ViewPort viewPort, string name, int itemAddress)
         : base(viewPort, viewPort.Model, name, itemAddress) {
      }
   }
}
