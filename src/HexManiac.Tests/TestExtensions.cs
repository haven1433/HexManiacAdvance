using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Tests {
   public static class TestExtensions {
      public static void SetList(this IDataModel model, string name, IReadOnlyList<string> list) => model.SetList(new NoDataChangeDeltaModel(), name, list);
      public static ContextItemGroup GetSubmenu(this IReadOnlyList<IContextItem> menu, string content) => (ContextItemGroup)menu.Single(item => item.Text == content);
      public static byte[] BytesFrom(this IDataModel model, IFormattedRun run) {
         var data = new byte[run.Length];
         Array.Copy(model.RawData, run.Start, data, 0, run.Length);
         return data;
      }

      public static int[,] ReadSprite(this IDataModel model, int address) {
         var sprite = (ISpriteRun)model.GetNextRun(address);
         return sprite.GetPixels(model, 0);
      }
   }
}
