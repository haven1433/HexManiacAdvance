using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
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
      private readonly string dexOrder, dexInfo;
      private readonly bool isNational;

      public ObservableCollection<SortablePokemon> Elements { get; } = new ObservableCollection<SortablePokemon>();

      public string Name => "Adjust Dex Order";

      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs { get; } = new StubCommand();
      public ICommand Undo { get; }
      public ICommand Redo { get; }
      public ICommand Copy { get; } = new StubCommand();
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close { get; }

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event PropertyChangedEventHandler PropertyChanged;

      public DexReorderTab(ChangeHistory<ModelDelta> history, IDataModel model, string dexOrder, string dexInfo, bool isNational) {
         this.history = history;
         this.model = model;
         this.dexOrder = dexOrder;
         this.dexInfo = dexInfo;
         this.isNational = isNational;

         Close = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => Closed?.Invoke(this, EventArgs.Empty),
         };
         Undo = history.Undo;
         Redo = history.Redo;
      }

      public void Refresh() {
         history.ChangeCompleted();
         Elements.Clear();

         var dexOrder = (ITableRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, this.dexOrder));
         var dexInfo = (ITableRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, this.dexInfo));

         var elementCount = dexOrder.ElementCount;
         var dexOrderTable = new ModelTable(model, dexOrder.Start);
         var indexName = dexOrder.ElementContent[0].Name;

         var elements = new SortablePokemon[dexInfo.ElementCount - 1];
         for (int i = 0; i < elementCount; i++) {
            var dexIndex = dexOrderTable[i].GetValue(indexName);
            if (dexIndex >= dexInfo.ElementCount) continue;
            elements[dexIndex - 1] = new SortablePokemon(model, i + 1);
         }
         for (int i = 0; i < elements.Length; i++) Elements.Add(elements[i]);
         Debug.Assert(Elements.All(element => element != null), "Dex Reorder only works if there are no empty pokedex slots!");
      }

      public void CompleteCurrentInteraction() {
         try {
            UpdateDexFromSortOrder();
         } catch (Exception e) {
            OnError?.Invoke(this, e.Message);
         }
         history.ChangeCompleted();
      }

      public void HandleMove(int originalIndex, int newIndex) {
         if (originalIndex == newIndex || originalIndex >= Elements.Count || newIndex >= Elements.Count) return;
         var element = Elements[originalIndex];
         Elements.RemoveAt(originalIndex);
         Elements.Insert(newIndex, element);
         Debug.Assert(Elements.All(item => item != null), "Dex Reorder only works if there are no empty pokedex slots!");
      }

      private void UpdateDexFromSortOrder() {
         var token = history.CurrentChange;
         // Elements is in the new desired pokedex order
         // update dexOrder / dexInfo to match

         var dexOrder = (ITableRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, this.dexOrder));
         var dexInfo = (ITableRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, this.dexInfo));

         // maps from canonical order to previous desired dex order
         var oldOrder = new List<int>();
         for (int i = dexOrder.Start; i < dexOrder.Start + dexOrder.Length; i += dexOrder.ElementLength) {
            oldOrder.Add(model.ReadMultiByteValue(i, dexOrder.ElementContent[0].Length));
         }

         // maps from canonical order to new desired dex order
         Debug.Assert(Elements.All(item => item != null), "Dex Reorder only works if there are no empty pokedex slots!");
         var newOrder = Invert(Elements.Select(element => element.CanonicalIndex).ToList(), oldOrder);

         var oldDexInfo = new byte[dexInfo.Length];
         Array.Copy(model.RawData, dexInfo.Start, oldDexInfo, 0, dexInfo.Length);

         // clear dexInfo format
         if (isNational) model.ClearFormat(token, dexInfo.Start, dexInfo.Length);

         // move each dex info / dex order
         for (int i = 1; i < dexInfo.ElementCount; i++) {
            if (isNational) {
               // we only have to update the dex info if this tab is editing the nationaldex.
               var originalIndex = i;
               var newIndex = newOrder[oldOrder.IndexOf(i)];
               if (newIndex != originalIndex) {
                  // update data
                  for (int j = 0; j < dexInfo.ElementLength; j++) {
                     token.ChangeData(model, dexInfo.Start + dexInfo.ElementLength * newIndex + j, oldDexInfo[dexInfo.ElementLength * originalIndex + j]);
                  }
               }
            }
            model.WriteMultiByteValue(dexOrder.Start + dexOrder.ElementLength * (i - 1), 2, token, newOrder[i - 1]);
         }

         // restore dexInfo format
         if (isNational) model.ObserveAnchorWritten(token, HardcodeTablesModel.DexInfoTableName, dexInfo);

         UpdateDexConversionTable.Run(model, token);
      }

      private IList<int> Invert(IList<int> input, IList<int> filler) {
         var result = new int[filler.Count];
         for (int i = 0; i < filler.Count; i++) result[i] = filler[i];
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
