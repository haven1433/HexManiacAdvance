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
      private readonly StubCommand undo, redo;

      public ObservableCollection<SortablePokemon> Elements { get; } = new ObservableCollection<SortablePokemon>();

      public string Name => "Adjust Dex Order";

      private string filter = string.Empty;
      public string Filter {
         get => filter;
         set {
            if (!TryUpdate(ref filter, value)) return;
            foreach (var mon in Elements) mon.MatchToFilter(filter);
         }
      }

      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs { get; } = new StubCommand();
      public ICommand Undo => undo;
      public ICommand Redo => redo;
      public ICommand Copy { get; } = new StubCommand();
      public ICommand DeepCopy { get; } = new StubCommand();
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close { get; }

      public event EventHandler<string> OnError;
      public event EventHandler Closed;
      event EventHandler<string> ITabContent.OnMessage { add { } remove { } }
      event EventHandler ITabContent.ClearMessage { add { } remove { } }
      event EventHandler<ITabContent> ITabContent.RequestTabChange { add { } remove { } }
      event EventHandler<Action> ITabContent.RequestDelayedWork { add { } remove { } }
      event EventHandler ITabContent.RequestMenuClose { add { } remove { } }

      private int selectionStart;
      public int SelectionStart {
         get => selectionStart;
         set {
            var first = Math.Min(selectionStart, selectionEnd);
            var last = Math.Max(selectionStart, selectionEnd);
            if (first <= value && value <= last) return;
            if (!TryUpdate(ref selectionStart, value)) return;
            SelectionEnd = selectionStart;
         }
      }

      private int selectionEnd;
      public int SelectionEnd {
         get => selectionEnd;
         set {
            TryUpdate(ref selectionEnd, value);
            UpdateSelection();
         }
      }

      public DexReorderTab(ChangeHistory<ModelDelta> history, IDataModel model, string dexOrder, string dexInfo, bool isNational) {
         this.history = history;
         this.model = model;
         this.dexOrder = dexOrder;
         this.dexInfo = dexInfo;
         this.isNational = isNational;

         undo = new StubCommand {
            CanExecute = history.Undo.CanExecute,
            Execute = arg => { history.Undo.Execute(arg); Refresh(); },
         };
         history.Undo.CanExecuteChanged += UndoCanExecuteWatcher;

         redo = new StubCommand {
            CanExecute = history.Redo.CanExecute,
            Execute = arg => { history.Redo.Execute(arg); Refresh(); },
         };
         history.Redo.CanExecuteChanged += RedoCanExecuteWatcher;

         Close = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => {
               Closed?.Invoke(this, EventArgs.Empty);
               history.Undo.CanExecuteChanged -= UndoCanExecuteWatcher;
               history.Redo.CanExecuteChanged -= RedoCanExecuteWatcher;
            },
         };
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
         var unusedIndeces = new Dictionary<int, int>();
         for (int i = 0; i < elementCount; i++) {
            var dexIndex = dexOrderTable[i].GetValue(indexName);
            if (dexIndex >= dexInfo.ElementCount) {
               if (dexIndex > elementCount) {
                  Debug.Fail($"Dex Reorder Warning: pokemon {i} is set to pokedex slot {dexIndex - 1} which is more than the number of pokemon!");
               }
               if (!unusedIndeces.ContainsKey(dexIndex)) {
                  unusedIndeces[dexIndex] = 0;
               }
               unusedIndeces[dexIndex] += 1;
               continue;
            }
            if (elements[dexIndex - 1] != null) Debug.Fail($"Dex Reorder Warning: pokedex slot {dexIndex - 1} is set more than once! See pokemon {i}.");
            elements[dexIndex - 1] = new SortablePokemon(model, i + 1);
         }
         for (int i = dexInfo.ElementCount; i < elementCount; i++) {
            if (!unusedIndeces.ContainsKey(i)) {
               Debug.Fail($"Dex Reorder Warning: pokedex slot {i} is not used!");
            } else if (unusedIndeces[i] > 1) {
               Debug.Fail($"Dex Reorder Warning: pokedex slot {i} is used more than once!");
            }
         }
         for (int i = 0; i < elements.Length; i++) {
            Debug.Assert(elements[i] != null, $"Dex Reorder warning: pokedex slot {i + 1} is empty!");
            Elements.Add(elements[i]);
            elements[i].MatchToFilter(filter);
         }
      }

      private void UpdateSelection() {
         var first = Math.Min(selectionStart, selectionEnd);
         var last = Math.Max(selectionStart, selectionEnd);
         for (int i = 0; i < Elements.Count; i++) Elements[i].Selected = first <= i && i <= last;
      }

      public void CompleteCurrentInteraction() {
         try {
            UpdateDexFromSortOrder();
         } catch (Exception e) {
            OnError?.Invoke(this, e.Message);
         }
         history.ChangeCompleted();
      }

      public IList<(int index, int direction)> HandleMove(int originalIndex, int newIndex) {
         var otherMovedElements = new List<(int, int)>();
         if (originalIndex == newIndex) return otherMovedElements;

         var first = Math.Min(selectionStart, selectionEnd);
         var last = Math.Max(selectionStart, selectionEnd);
         newIndex = Math.Max(newIndex, originalIndex - first);
         newIndex = Math.Min(newIndex, Elements.Count - 1 - last + originalIndex);
         var targetElements = Enumerable.Range(first, last - first + 1).ToList();

         if (originalIndex < newIndex) targetElements.Reverse();
         foreach (var index in targetElements) {
            var element = Elements[index];
            Elements.RemoveAt(index);
            Elements.Insert(newIndex - originalIndex + index, element);
         }

         Debug.Assert(Elements.All(item => item != null), "Dex Reorder only works if there are no empty pokedex slots!");

         for (int i = first; i < newIndex - (originalIndex - first); i++) {
            otherMovedElements.Add((i, last - first + 1));
         }
         for (int i = last; i > newIndex + (last - originalIndex); i--) {
            otherMovedElements.Add((i, first - last - 1));
         }

         selectionStart += newIndex - originalIndex;
         SelectionEnd += newIndex - originalIndex;
         return otherMovedElements;
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
         }

         for (int i = 0; i < newOrder.Count; i++) {
            int start = dexOrder.Start + dexOrder.ElementLength * i;
            model.WriteMultiByteValue(start, dexOrder.ElementContent[0].Length, token, newOrder[i]);
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

      private void UndoCanExecuteWatcher(object sender, EventArgs e) => undo.CanExecuteChanged.Invoke(undo, EventArgs.Empty);
      private void RedoCanExecuteWatcher(object sender, EventArgs e) => redo.CanExecuteChanged.Invoke(redo, EventArgs.Empty);
   }

   public class SortablePokemon : ViewModelCore, IPixelViewModel {
      public const string FrontSpritesTable = "frontsprites";
      public const string PokePalettesTable = "pokepalettes";

      private readonly IList<string> filterTerms;

      public int CanonicalIndex { get; }
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; }
      public double SpriteScale { get; }

      private bool isFilteredOut;
      public bool IsFilteredOut { get => isFilteredOut; set => TryUpdate(ref isFilteredOut, value); }

      private bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

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

         filterTerms = GenerateFilterTerms(model, index);
      }

      public void MatchToFilter(string filter) {
         IsFilteredOut = false;
         if (filter == string.Empty) return;

         foreach (var term in filterTerms) {
            if (term.MatchesPartial(filter)) return;
         }

         IsFilteredOut = true;
      }

      private static IList<string> GenerateFilterTerms(IDataModel model, int pokemonIndex) {
         var terms = new List<string>();

         // pokemon name
         var names = new ModelTable(model, model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.PokemonNameTable));
         var name = names[pokemonIndex].GetStringValue(names[pokemonIndex].GetFieldName(0));
         terms.Add(name);

         // pokemon types
         var statsTable = ReorderDex.GetTable(model, HardcodeTablesModel.PokemonStatsTable);
         if (statsTable != null) {
            var stats = new ModelTable(model, statsTable.Start);
            terms.Add(stats[pokemonIndex].GetEnumValue("type1"));
            terms.Add(stats[pokemonIndex].GetEnumValue("type2"));
         }

         return terms;
      }
   }
}
