using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel {
      public PaletteFormat format;

      public string TableName { get; private set; }

      public PaletteCollection Colors { get; }

      public void Activate() { }

   }
}
