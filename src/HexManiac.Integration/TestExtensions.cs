using HavenSoft.HexManiac.Core.Models;
using Xunit;

namespace HavenSoft.HexManiac.Integration {
   public static class TestExtensions {
      public static TRun Get<TRun>(this IDataModel model, string name) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         var run = model.GetNextRun(address);
         Assert.Equal(run.Start, address);
         return (TRun)run;
      }
   }
}
