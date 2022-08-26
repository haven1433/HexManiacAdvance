using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Tests {
   public static class TestExtensions {
      public static void SetList(this IDataModel model, string name, IReadOnlyList<string> list) => model.SetList(new NoDataChangeDeltaModel(), name, list, null);
      public static ContextItemGroup GetSubmenu(this IReadOnlyList<IContextItem> menu, string content) => (ContextItemGroup)menu.Single(item => item.Text == content);
      public static byte[] BytesFrom(this IDataModel model, IFormattedRun run) {
         var data = new byte[run.Length];
         Array.Copy(model.RawData, run.Start, data, 0, run.Length);
         return data;
      }

      public static int[,] GetPixels(this ISpriteRun sprite, IDataModel model, int page) {
         return sprite.GetPixels(model, page, -1);
      }

      public static int[,] ReadSprite(this IDataModel model, int address) {
         var sprite = (ISpriteRun)model.GetNextRun(address);
         return sprite.GetPixels(model, 0);
      }

      public static IStreamRun DeserializeRun(this IStreamRun streamRun, string content, ModelDelta token) => streamRun.DeserializeRun(content, token, out var _);
      public static TrainerPokemonTeamRun DeserializeRun(this TrainerPokemonTeamRun streamRun, string content, ModelDelta token, bool setDefaultMoves, bool setDefaultItems) => streamRun.DeserializeRun(content, token, setDefaultMoves, setDefaultItems, out var _);
      public static TableStreamRun DeserializeRun(this TableStreamRun streamRun, string content, ModelDelta token) => streamRun.DeserializeRun(content, token, out var _);

      public static void ChangeList(this ModelDelta token, string name, string[] oldValues, string[] newValues) {
         token.ChangeList(name, new ValidationList(null, oldValues), new ValidationList(null, newValues));
      }

      public static T Single<T>(this ObservableCollection<IArrayElementViewModel> self) => (T)self.Single(item => item is T);
   }
}
