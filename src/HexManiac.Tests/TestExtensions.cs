using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Tests {
   public static class TestExtensions {
      public static void SetList(this IDataModel model, string name, IReadOnlyList<string> list) => model.SetList(new NoDataChangeDeltaModel(), name, list);
      public static ContextItemGroup GetSubmenu(this IReadOnlyList<IContextItem> menu, string content) => (ContextItemGroup)menu.Single(item => item.Text == content);
   }
}
