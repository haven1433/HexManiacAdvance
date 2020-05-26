using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableTool : ViewModelCore, IToolViewModel {
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ViewPort viewPort;
      private readonly IToolTrayViewModel toolTray;

      public string Name => "Table";

      public IEnumerable<string> TableList => model.Arrays.Select(array => model.GetAnchorFromAddress(-1, array.Start));

      private int selectedTableIndex;
      public int SelectedTableIndex {
         get => selectedTableIndex;
         set {
            TryUpdate(ref selectedTableIndex, value);
            if (selectedTableIndex == -1) return;
            var array = model.Arrays[selectedTableIndex];
            selection.GotoAddress(array.Start);
            Address = array.Start;
         }
      }

      private string currentElementName;
      public string CurrentElementName {
         get => currentElementName;
         set => TryUpdate(ref currentElementName, value);
      }

      private readonly StubCommand previous, next, append;
      public ICommand Previous => previous;
      public ICommand Next => next;
      public ICommand Append => append;
      private void CommandCanExecuteChanged() {
         previous.CanExecuteChanged.Invoke(previous, EventArgs.Empty);
         next.CanExecuteChanged.Invoke(next, EventArgs.Empty);
         append.CanExecuteChanged.Invoke(append, EventArgs.Empty);
      }

      public ObservableCollection<IArrayElementViewModel> Children { get; }

      // the address is the address not of the entire array, but of the current index of the array
      private int address = Pointer.NULL;
      public int Address {
         get => address;
         set {
            if (TryUpdate(ref address, value)) {
               var run = model.GetNextRun(value);
               if (run.Start > value || !(run is ITableRun)) {
                  Enabled = false;
                  CommandCanExecuteChanged();
                  return;
               }

               CommandCanExecuteChanged();
               Enabled = true;
               toolTray.Schedule(DataForCurrentRunChanged);
            }
         }
      }

      private bool enabled;
      public bool Enabled {
         get => enabled;
         private set => TryUpdate(ref enabled, value);
      }

#pragma warning disable 0067 // it's ok if events are never used after implementing an interface
      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler RequestMenuClose;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved; // invoke when a new item gets added and the table has to move
#pragma warning restore 0067

      public TableTool(IDataModel model, Selection selection, ChangeHistory<ModelDelta> history, ViewPort viewPort, IToolTrayViewModel toolTray) {
         this.model = model;
         this.selection = selection;
         this.history = history;
         this.viewPort = viewPort;
         this.toolTray = toolTray;
         Children = new ObservableCollection<IArrayElementViewModel>();

         previous = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ITableRun;
               return array != null && array.Start < address;
            },
            Execute = parameter => {
               var array = (ITableRun)model.GetNextRun(address);
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(Address - array.ElementLength);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
            }
         };

         next = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ITableRun;
               return array != null && array.Start + array.Length > address + array.ElementLength;
            },
            Execute = parameter => {
               var array = (ITableRun)model.GetNextRun(address);
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(Address + array.ElementLength);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
            }
         };

         append = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ITableRun;
               return array != null && array.Start + array.Length == address + array.ElementLength;
            },
            Execute = parameter => {
               using (ModelCacheScope.CreateScope(model)) {
                  var array = (ITableRun)model.GetNextRun(address);
                  var originalArray = array;
                  var error = model.CompleteArrayExtension(viewPort.CurrentChange, ref array);
                  if (array != null) {
                     if (array.Start != originalArray.Start) {
                        ModelDataMoved?.Invoke(this, (originalArray.Start, array.Start));
                        selection.GotoAddress(array.Start + array.Length - array.ElementLength);
                     }
                     if (error.HasError && !error.IsWarning) {
                        OnError?.Invoke(this, error.ErrorMessage);
                     } else {
                        if (error.IsWarning) OnMessage?.Invoke(this, error.ErrorMessage);
                        ModelDataChanged?.Invoke(this, array);
                        selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(array.Start + array.Length - array.ElementLength);
                        selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
                     }
                  }
                  RequestMenuClose?.Invoke(this, EventArgs.Empty);
               }
            }
         };

         CurrentElementName = "The Table tool only works if your cursor is on table data.";
      }

      private int childInsertionIndex = 0;
      private void AddChild(IArrayElementViewModel child) {
         if (childInsertionIndex == Children.Count) {
            Children.Add(child);
         } else if (!Children[childInsertionIndex].TryCopy(child)) {
            Children[childInsertionIndex] = child;
         }
         childInsertionIndex++;
      }

      public void DataForCurrentRunChanged() {
         foreach (var child in Children) child.DataChanged -= ForwardModelChanged;
         childInsertionIndex = 0;

         var array = model.GetNextRun(Address) as ITableRun;
         if (array == null || array.Start > Address) {
            CurrentElementName = "The Table tool only works if your cursor is on table data.";
            Children.Clear();
            return;
         }

         NotifyPropertyChanged(nameof(TableList));
         TryUpdate(ref selectedTableIndex, model.Arrays.IndexOf(array), nameof(SelectedTableIndex));

         var basename = model.GetAnchorFromAddress(-1, array.Start);
         if (string.IsNullOrEmpty(basename)) basename = array.Start.ToString("X6");
         var index = (Address - array.Start) / array.ElementLength;

         if (0 <= index && index < array.ElementCount) {
            if (array.ElementNames.Count > index) {
               CurrentElementName = $"{basename}/{index}" + Environment.NewLine + $"{basename}/{array.ElementNames[index]}";
            } else {
               CurrentElementName = $"{basename}/{index}";
            }

            AddChildrenFromTable(array, index);

            if (array is ArrayRun arrayRun) {
               int negParentOffset = Math.Min(arrayRun.ParentOffset, 0);
               index -= negParentOffset;
               if (!string.IsNullOrEmpty(arrayRun.LengthFromAnchor)) basename = arrayRun.LengthFromAnchor; // basename is now a 'parent table' name, if there is one

               foreach (var currentArray in model.GetRelatedArrays(arrayRun)) {
                  var currentArrayName = model.GetAnchorFromAddress(-1, currentArray.Start);
                  var negChildOffset = Math.Min(currentArray.ParentOffset, 0);
                  var currentIndex = index + negChildOffset;
                  if (currentIndex >= 0 && currentIndex < currentArray.ElementCount) {
                     AddChild(new SplitterArrayElementViewModel(currentArrayName));
                     AddChildrenFromTable(currentArray, currentIndex);
                  }
               }
            }

            AddChildrenFromStreams(array, basename, index);
         }

         while (Children.Count > childInsertionIndex) Children.RemoveAt(Children.Count - 1);
         foreach (var child in Children) child.DataChanged += ForwardModelChanged;

         // update sprites now that all the associated palettes have been loaded.
         foreach (var child in Children) {
            if (child is SpriteElementViewModel sevm) sevm.UpdateTiles();
            if (child is PaletteElementViewModel pevm) pevm.UpdateAssociatedPointers();
         }
      }

      private void AddChildrenFromStreams(ITableRun array, string basename, int index) {
         var plmResults = new List<(int, int)>();
         var eggResults = new List<(int, int)>();
         var trainerResults = new List<int>();
         var streamResults = new List<(int, int)>();
         foreach (var child in model.Streams) {
            if (!child.DependsOn(basename)) continue;
            if (child is PLMRun plmRun) plmResults.AddRange(plmRun.Search(basename, index));
            if (child is EggMoveRun eggRun) eggResults.AddRange(eggRun.Search(basename, index));
            if (child is TrainerPokemonTeamRun trainerRun) trainerResults.AddRange(trainerRun.Search(basename, index));
            if (child is TableStreamRun streamRun) streamResults.AddRange(streamRun.Search(basename, index));
         }
         if (eggResults.Count > 0) {
            AddChild(new ButtonArrayElementViewModel("Show uses in egg moves.", () => {
               using (ModelCacheScope.CreateScope(model)) {
                  viewPort.OpenSearchResultsTab($"{array.ElementNames[index]} within {HardcodeTablesModel.EggMovesTableName}", eggResults);
               }
            }));
         }
         if (plmResults.Count > 0) {
            AddChild(new ButtonArrayElementViewModel("Show uses in level-up moves.", () => {
               using (ModelCacheScope.CreateScope(model)) {
                  viewPort.OpenSearchResultsTab($"{array.ElementNames[index]} within {HardcodeTablesModel.LevelMovesTableName}", plmResults);
               }
            }));
         }
         if (trainerResults.Count > 0) {
            var selections = trainerResults.Select(result => (result, result + 1)).ToList();
            AddChild(new ButtonArrayElementViewModel("Show uses in trainer teams.", () => {
               using (ModelCacheScope.CreateScope(model)) {
                  viewPort.OpenSearchResultsTab($"{array.ElementNames[index]} within {HardcodeTablesModel.TrainerTableName}", selections);
               }
            }));
         }
         if (streamResults.Count > 0) {
            AddChild(new ButtonArrayElementViewModel("Show uses in other streams.", () => {
               using (ModelCacheScope.CreateScope(model)) {
                  viewPort.OpenSearchResultsTab($"{array.ElementNames[index]} within streams", streamResults);
               }
            }));
         }
      }

      private void AddChildrenFromTable(ITableRun table, int index) {
         var itemAddress = table.Start + table.ElementLength * index;
         foreach (var item in table.ElementContent) {
            IArrayElementViewModel viewModel = null;
            if (item.Type == ElementContentType.Unknown) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new HexFieldStratgy());
            else if (item.Type == ElementContentType.PCS) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new TextFieldStratgy());
            else if (item.Type == ElementContentType.Pointer) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new AddressFieldStratgy());
            else if (item.Type == ElementContentType.BitArray) viewModel = new BitListArrayElementViewModel(selection, history, model, item.Name, itemAddress);
            else if (item.Type == ElementContentType.Integer) {
               if (item is ArrayRunEnumSegment enumSegment) {
                  viewModel = new ComboBoxArrayElementViewModel(selection, history, model, item.Name, itemAddress, item.Length);
                  var anchor = model.GetAnchorFromAddress(-1, table.Start);
                  if (!string.IsNullOrEmpty(anchor) && model.GetDependantArrays(anchor).Count() == 1) {
                     AddChild(viewModel);
                     viewModel = new BitListArrayElementViewModel(selection, history, model, item.Name, itemAddress);
                  }
               } else {
                  viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new NumericFieldStrategy());
               }
            } else {
               throw new NotImplementedException();
            }
            AddChild(viewModel);
            AddChildrenFromPointerSegment(itemAddress, item, childInsertionIndex - 1);
            itemAddress += item.Length;
         }
      }

      private void AddChildrenFromPointerSegment(int itemAddress, ArrayRunElementSegment item, int parentIndex) {
         if (!(item is ArrayRunPointerSegment pointerSegment)) return;
         if (pointerSegment.InnerFormat == string.Empty) return;
         var destination = model.ReadPointer(itemAddress);
         IFormattedRun streamRun = null;
         if (destination != Pointer.NULL) {
            streamRun = model.GetNextRun(destination);
            if (!pointerSegment.DestinationDataMatchesPointerFormat(model, new NoDataChangeDeltaModel(), itemAddress, destination, null)) return;
         }

         IStreamArrayElementViewModel streamElement = null;
         if (streamRun == null || streamRun is IStreamRun) streamElement = new TextStreamElementViewModel(viewPort, model, itemAddress);
         if (streamRun is ISpriteRun spriteRun) streamElement = new SpriteElementViewModel(viewPort, spriteRun.SpriteFormat, itemAddress);
         if (streamRun is IPaletteRun paletteRun) streamElement = new PaletteElementViewModel(viewPort, history, paletteRun.PaletteFormat, itemAddress);
         if (streamElement == null) return;

         var streamAddress = itemAddress;
         var myIndex = childInsertionIndex;
         Children[parentIndex].DataChanged += (sender, e) => {
            var closure_destination = model.ReadPointer(streamAddress);
            var run = model.GetNextRun(destination) as IStreamRun;
            IStreamArrayElementViewModel newStream = null;

            if (run == null || run is IStreamRun) newStream = new TextStreamElementViewModel(viewPort, model, streamAddress);
            if (run is ISpriteRun spriteRun1) newStream = new SpriteElementViewModel(viewPort, spriteRun1.SpriteFormat, streamAddress);
            if (run is IPaletteRun paletteRun1) newStream = new PaletteElementViewModel(viewPort, history, paletteRun1.PaletteFormat, streamAddress);

            newStream.DataChanged += ForwardModelChanged;
            newStream.DataMoved += ForwardModelDataMoved;
            if (!Children[myIndex].TryCopy(newStream)) Children[myIndex] = newStream;
         };
         streamElement.DataMoved += ForwardModelDataMoved;
         AddChild(streamElement);

         parentIndex = childInsertionIndex - 1;
         if (streamRun is ITableRun tableRun) {
            int segmentOffset = 0;
            for (int i = 0; i < tableRun.ElementContent.Count; i++) {
               if (!(tableRun.ElementContent[i] is ArrayRunPointerSegment)) { segmentOffset += tableRun.ElementContent[i].Length; continue; }
               for (int j = 0; j < tableRun.ElementCount; j++) {
                  itemAddress = tableRun.Start + segmentOffset + j * tableRun.ElementLength;
                  AddChildrenFromPointerSegment(itemAddress, tableRun.ElementContent[i], parentIndex);
               }
               segmentOffset += tableRun.ElementContent[i].Length;
            }
         }
      }

      private void ForwardModelChanged(object sender, EventArgs e) => ModelDataChanged?.Invoke(this, model.GetNextRun(Address));
      private void ForwardModelDataMoved(object sender, (int originalStart, int newStart) e) => ModelDataMoved?.Invoke(this, e);
   }
}
