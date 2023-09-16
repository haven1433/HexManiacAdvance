using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public static class TestExtensions {
      public static void SetList(this IDataModel model, string name, IReadOnlyList<string> list) => model.SetList(new NoDataChangeDeltaModel(), name, list, null);
      public static void SetList(this IDataModel model, ModelDelta token, string name, IReadOnlyList<string> list, string hash) => model.SetList(token, name, list, new Dictionary<int, string>(), hash);
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

      public static IStreamRun DeserializeRun(this IStreamRun streamRun, string content, ModelDelta token) => streamRun.DeserializeRun(content, token, out var _, out _);
      public static TrainerPokemonTeamRun DeserializeRun(this TrainerPokemonTeamRun streamRun, string content, ModelDelta token, bool setDefaultMoves, bool setDefaultItems) => streamRun.DeserializeRun(content, token, setDefaultMoves, setDefaultItems, out var _);
      public static TableStreamRun DeserializeRun(this TableStreamRun streamRun, string content, ModelDelta token) => streamRun.DeserializeRun(content, token, out var _, out _);

      public static void ChangeList(this ModelDelta token, string name, string[] oldValues, string[] newValues) {
         token.ChangeList(name, New.ValidationList(null, oldValues), New.ValidationList(null, newValues));
      }

      public static T Single<T>(this ObservableCollection<IArrayElementViewModel> self) => (T)self.Single(item => item is T);

      public static StoredMetadata ExportMetadata(this IDataModel model, IMetadataInfo info) => model.ExportMetadata(null, info);

      public static TRun Get<TRun>(this IDataModel model, string name) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         var run = model.GetNextRun(address);
         Assert.Equal(run.Start, address);
         return (TRun)run;
      }

      public static byte[] Compile(this ScriptParser parser, ModelDelta token, IDataModel model, int start, ref string script, out IReadOnlyList<(int originalLocation, int newLocation)> movedData) {
         return parser.Compile(token, model, start, ref script, out movedData, out var _);
      }

      /// <summary>
      /// Force the evaluation of all properties to check for exceptions that would normally occur during binding.
      /// </summary>
      public static void ReadAllProperties(this INotifyPropertyChanged viewModel) {
         var type = viewModel.GetType();
         foreach (var prop in type.GetProperties()) {
            if (prop.GetMethod == null) continue;
            prop.GetMethod.Invoke(viewModel, null);
         }
      }

      public static string Parse(this ScriptParser self, IDataModel data, int start, int length, CodeBody updateBody = null) {
         int count = 0;
         return self.Parse(data, start, length, ref count, updateBody);
      }
   }
}
