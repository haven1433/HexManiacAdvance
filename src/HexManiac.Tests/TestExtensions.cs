using HavenSoft.HexManiac.Core.Models;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Tests {
   public static class TestExtensions {
      public static void SetList(this IDataModel model, string name, IReadOnlyList<string> list) => model.SetList(new NoDataChangeDeltaModel(), name, list);
   }
}
