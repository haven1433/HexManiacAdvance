using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DexReorderTab : ViewModelCore, ITabContent {
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;
      private readonly ITableRun dexOrder;
      private readonly ITableRun dexInfo;

      public ObservableCollection<SortablePokemon> Elements { get; } = new ObservableCollection<SortablePokemon>();

      public string Name => "Adjust Dex Order";

      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs { get; } = new StubCommand();
      public ICommand Undo { get; } = new StubCommand();
      public ICommand Redo { get; } = new StubCommand();
      public ICommand Copy { get; } = new StubCommand();
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close { get; } = new StubCommand();

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event PropertyChangedEventHandler PropertyChanged;

      public DexReorderTab(ChangeHistory<ModelDelta> history, IDataModel model, ITableRun dexOrder, ITableRun dexInfo) {
         this.history = history;
         this.model = model;
         this.dexOrder = dexOrder;
         this.dexInfo = dexInfo;
      }

      public void Refresh() {
         Elements.Clear();

         var elementCount = dexOrder.ElementCount;
         var dexOrderTable = new ModelTable(model, dexOrder.Start);
         var indexName = dexOrder.ElementContent[0].Name;

         var elements = new SortablePokemon[dexInfo.ElementCount];
         for (int i = 0; i < elementCount; i++) {
            var dexIndex = dexOrderTable[i].GetValue(indexName);
            if (dexIndex >= dexInfo.ElementCount) continue;
            elements[dexIndex - 1] = new SortablePokemon(model, i + 1);
         }
         Debug.Assert(Elements.All(element => element != null), "Dex Reorder onl works if there are no empty pokedex slots!");
         for (int i = 0; i < elements.Length; i++) Elements.Add(elements[i]);
      }

      public void UpdateDexFromSortOrder() {
         var token = history.CurrentChange;
         // Elements is in the new desired pokedex order
         // update dexOrder / dexInfo to match

         // maps from canonical order to previous desired dex order
         var oldOrder = new List<int>();
         for (int i = dexOrder.Start; i < dexOrder.Start + dexOrder.Length; i += dexOrder.ElementLength) {
            oldOrder.Add(model.ReadMultiByteValue(i, dexOrder.ElementContent[0].Length));
         }

         // maps from canonical order to new desired dex order
         var newOrder = Invert(Elements.Select(element => element.CanonicalIndex).ToList());

         var oldDexInfo = new byte[dexInfo.Length];
         Array.Copy(model.RawData, dexInfo.Start, oldDexInfo, 0, dexInfo.Length);

         // move each dex info / dex order
         for (int i = 1; i <= dexInfo.ElementCount; i++) {
            var originalIndex = oldOrder.IndexOf(i);
            var newIndex = newOrder.IndexOf(i);
            for (int j = 0; j < dexInfo.ElementLength; j++) {
               token.ChangeData(model, dexInfo.Start + dexInfo.ElementLength * newIndex + j, oldDexInfo[dexInfo.ElementLength * originalIndex + j]);
            }
            model.WriteMultiByteValue(dexOrder.Start + dexOrder.ElementLength * (i - 1), 2, token, newOrder[i - 1]);
         }
      }

      private IList<int> Invert(IList<int> input) {
         var result = new int[input.Count];
         for (int i = 0; i < input.Count; i++) {
            result[input[i] - 1] = i + 1;
         }
         return result;
      }
   }

   public class SortablePokemon : ViewModelCore, IPixelViewModel {
      public const string FrontSpritesTable = "frontsprites";
      public const string PokePalettesTable = "pokepalettes";

      public int CanonicalIndex { get; }
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; }
      public double SpriteScale { get; }
      public event PropertyChangedEventHandler PropertyChanged;

      public SortablePokemon(IDataModel model, int index) {
         var sprites = new ModelTable(model, model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, FrontSpritesTable));
         var palettes = new ModelTable(model, model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, PokePalettesTable));
         var sprite = sprites[index].GetSprite(sprites[index].GetFieldName(0));
         var palette = palettes[index].GetPalette(palettes[index].GetFieldName(0));

         CanonicalIndex = index;
         PixelWidth = sprite.GetLength(0);
         PixelHeight = sprite.GetLength(1);
         PixelData = SpriteTool.Render(sprite, palette, new PaletteFormat(4, 1));
         SpriteScale = 1;
      }
   }
}
