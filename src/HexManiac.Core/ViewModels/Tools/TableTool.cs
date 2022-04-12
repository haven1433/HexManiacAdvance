using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableTool : ViewModelCore, IToolViewModel {
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ViewPort viewPort;
      private readonly IToolTrayViewModel toolTray;

      public string Name => "Table";

      public IReadOnlyList<string> TableSections {
         get {
            var sections = UnmatchedArrays.Select(array => {
               var parts = model.GetAnchorFromAddress(-1, array.Start).Split('.');
               if (parts.Length > 2) return string.Join(".", parts.Take(2));
               return parts[0];
            }).Distinct().ToList();
            sections.Sort();
            return sections;
         }
      }

      private int selectedTableSection;
      public int SelectedTableSection {
         get => selectedTableSection;
         set => Set(ref selectedTableSection, value, UpdateTableList);
      }

      public IReadOnlyList<string> TableList {
         get {
            if (selectedTableSection == -1 || selectedTableSection >= TableSections.Count) return new string[0];
            var selectedSection = TableSections[selectedTableSection];
            var tableList = UnmatchedArrays
               .Select(array => model.GetAnchorFromAddress(-1, array.Start))
               .Where(name => name.StartsWith(selectedSection + "."))
               .Select(name => name.Substring(selectedSection.Length + 1))
               .ToList();
            tableList.Sort();
            return tableList;
         }
      }

      private int selectedTableIndex;
      public int SelectedTableIndex {
         get => selectedTableIndex;
         set {
            if (!TryUpdate(ref selectedTableIndex, value)) return;
            if (selectedTableIndex == -1 || dataForCurrentRunChangeUpdate) return;
            UpdateAddressFromSectionAndSelection();
         }
      }
      private void UpdateTableList(int oldValue = default) {
         NotifyPropertyChanged(nameof(TableList));
         UpdateAddressFromSectionAndSelection();
      }
      private void UpdateAddressFromSectionAndSelection(int oldValue = default) {
         if (selectedTableSection == -1 || selectedTableIndex == -1) return;
         var arrayName = TableSections[selectedTableSection];
         var tableList = TableList;
         if (selectedTableIndex >= tableList.Count) TryUpdate(ref selectedTableIndex, 0, nameof(SelectedTableIndex));
         arrayName += '.' + tableList[selectedTableIndex];
         var start = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, arrayName);
         selection.GotoAddress(start);
         Address = start;
      }

      private IReadOnlyList<ArrayRun> UnmatchedArrays => model.Arrays.Where(a => string.IsNullOrEmpty(a.LengthFromAnchor)).ToList();

      private string currentElementName;
      public string CurrentElementName {
         get => currentElementName;
         private set => TryUpdate(ref currentElementName, value);
      }

      public IndexComboBoxViewModel CurrentElementSelector { get; }

      private readonly StubCommand previous, next, append;
      private StubCommand incrementAdd, decrementAdd;
      public ICommand Previous => previous;
      public ICommand Next => next;
      public ICommand Append => append;
      public ICommand IncrementAdd => StubCommand(ref incrementAdd, IncrementAddExecute, IncrementAddCanExecute);
      public ICommand DecrementAdd => StubCommand(ref decrementAdd, DecrementAddExecute, DecrementAddCanExecute);
      private void CommandCanExecuteChanged() {
         previous.RaiseCanExecuteChanged();
         next.RaiseCanExecuteChanged();
         append.RaiseCanExecuteChanged();
         incrementAdd.RaiseCanExecuteChanged();
         decrementAdd.RaiseCanExecuteChanged();
      }
      private void IncrementAddExecute() { AddCount += 1; CommandCanExecuteChanged(); }
      private void DecrementAddExecute() { AddCount -= 1; CommandCanExecuteChanged(); }
      private bool IncrementAddCanExecute() => append.CanExecute(null) && addCount < 500;
      private bool DecrementAddCanExecute() => append.CanExecute(null) && addCount > 1;

      private int addCount = 1;
      public int AddCount {
         get => addCount;
         set {
            value = Math.Min(Math.Max(1, value), 500);
            Set(ref addCount, value, arg => CommandCanExecuteChanged());
         }
      }

      public ObservableCollection<IArrayElementViewModel> UsageChildren { get; }
      public ObservableCollection<TableGroupViewModel> Groups { get; }

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

      private string fieldFilter = string.Empty;
      public string FieldFilter {
         get => fieldFilter;
         set => Set(ref fieldFilter, value, oldVal => ApplyFieldFilter());
      }

#pragma warning disable 0067 // it's ok if events are never used after implementing an interface
      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler RequestMenuClose;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved; // invoke when a new item gets added and the table has to move
#pragma warning restore 0067

      // properties that exist solely so the UI can remember things when the tab switches
      public double VerticalOffset { get; set; }

      public TableTool(IDataModel model, Selection selection, ChangeHistory<ModelDelta> history, ViewPort viewPort, IToolTrayViewModel toolTray) {
         this.model = model;
         this.selection = selection;
         this.history = history;
         this.viewPort = viewPort;
         this.toolTray = toolTray;
         CurrentElementSelector = new IndexComboBoxViewModel(viewPort.Model);
         CurrentElementSelector.UpdateSelection += UpdateViewPortSelectionFromTableComboBoxIndex;
         Groups = new();
         UsageChildren = new();

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
               var address = this.address;
               var array = (ITableRun)model.GetNextRun(address);
               if (selection.Scroll.DataIndex < array.Start || selection.Scroll.DataIndex > array.Start + array.Length) selection.GotoAddress(array.Start);
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(address + array.ElementLength);
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
                  var initialViewOffset = viewPort.DataOffset;
                  var error = model.CompleteArrayExtension(viewPort.CurrentChange, addCount, ref array);
                  if (array != null) {
                     if (array.Start != originalArray.Start) {
                        ModelDataMoved?.Invoke(this, (originalArray.Start, array.Start));
                        viewPort.Goto.Execute(array.Start + (initialViewOffset - originalArray.Start));
                        selection.SelectionStart = viewPort.ConvertAddressToViewPoint(array.Start + array.Length - array.ElementLength);
                     }
                     if (error.HasError && !error.IsWarning) {
                        OnError?.Invoke(this, error.ErrorMessage);
                     } else {
                        if (error.IsWarning) OnMessage?.Invoke(this, error.ErrorMessage);
                        ModelDataChanged?.Invoke(this, array);
                        selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(array.Start + array.Length - array.ElementLength);
                        selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
                     }
                  } else {
                     append.RaiseCanExecuteChanged();
                     viewPort.Refresh();
                  }
                  RequestMenuClose?.Invoke(this, EventArgs.Empty);
                  if (model is PokemonModel pModel) pModel.ResolveConflicts();
               }
               AddCount = 1;
            }
         };

         CurrentElementName = "The Table tool only works if your cursor is on table data.";
      }

      public IList<IArrayElementViewModel> Children => Groups.SelectMany(group => group.Members).ToList();
      private record InsertionIndex(int Group = 0, int Member = 0);

      private InsertionIndex childIndex = new();
      private void AddChild(IArrayElementViewModel child) {
         if (child == null) return;
         while (Groups.Count <= childIndex.Group) Groups.Add(new TableGroupViewModel());
         if (childIndex.Member == Groups[childIndex.Group].Members.Count) {
            Groups[childIndex.Group].Members.Add(child);
         } else if (!Groups[childIndex.Group].Members[childIndex.Member].TryCopy(child)) {
            Groups[childIndex.Group].Members[childIndex.Member] = child;
         }
         childIndex = childIndex with { Member = childIndex.Member + 1 };
      }
      private void MoveToNextGroup() {
         var group = Groups[childIndex.Group];
         while (group.Members.Count > childIndex.Member) group.Members.RemoveAt(group.Members.Count - 1);
         childIndex = new(childIndex.Group + 1, 0);
      }

      private int usageChildInsertionIndex = 0;
      private void AddUsageChild(IArrayElementViewModel child) {
         if (usageChildInsertionIndex == UsageChildren.Count) {
            UsageChildren.Add(child);
         } else if (!UsageChildren[usageChildInsertionIndex].TryCopy(child)) {
            UsageChildren[usageChildInsertionIndex] = child;
         }
         usageChildInsertionIndex++;
      }

      private bool dataForCurrentRunChangeUpdate;
      public void DataForCurrentRunChanged() {
         foreach (var group in Groups) {
            foreach (var member in group.Members) member.DataChanged -= ForwardModelChanged;
         }
         foreach (var child in UsageChildren) child.DataChanged -= ForwardModelChanged;
         childIndex = new();
         usageChildInsertionIndex = 0;

         var array = model.GetNextRun(Address) as ITableRun;
         if (array == null || array.Start > Address) {
            CurrentElementName = "The Table tool only works if your cursor is on table data.";
            Groups.Clear();
            UsageChildren.Clear();
            NotifyPropertyChanged(nameof(TableSections));
            return;
         }

         dataForCurrentRunChangeUpdate = true;
         var basename = model.GetAnchorFromAddress(-1, array.Start);
         var anchorParts = basename.Split('.');
         NotifyPropertyChanged(nameof(TableSections));
         if (anchorParts.Length == 1) {
            TryUpdate(ref selectedTableSection, TableSections.IndexOf(anchorParts[0]));
            NotifyPropertyChanged(nameof(TableList));
         } else if (anchorParts.Length == 2) {
            TryUpdate(ref selectedTableSection, TableSections.IndexOf(anchorParts[0]));
            NotifyPropertyChanged(nameof(TableList));
            TryUpdate(ref selectedTableIndex, TableList.IndexOf(anchorParts[1]));
         } else {
            TryUpdate(ref selectedTableSection, TableSections.IndexOf(anchorParts[0] + "." + anchorParts[1]), nameof(SelectedTableSection));
            NotifyPropertyChanged(nameof(TableList));
            TryUpdate(ref selectedTableIndex, TableList.IndexOf(string.Join(".", anchorParts.Skip(2))), nameof(SelectedTableIndex));
         }

         dataForCurrentRunChangeUpdate = false;
         if (string.IsNullOrEmpty(basename)) basename = array.Start.ToString("X6");
         var index = (Address - array.Start) / array.ElementLength;

         if (0 <= index && index < array.ElementCount) {
            CurrentElementName = $"{basename}/{index}";
            UpdateCurrentElementSelector(array, index);

            var elementOffset = array.Start + array.ElementLength * index;
            if (array is not ArrayRun arrayRun) {
               AddChild(new SplitterArrayElementViewModel(viewPort, basename, elementOffset));
               AddChildrenFromTable(array, index);
               MoveToNextGroup();
               Groups[0].GroupName = basename;
            } else {
               index -= arrayRun.ParentOffset.BeginningMargin;
               var originalTableName = basename;
               if (!string.IsNullOrEmpty(arrayRun.LengthFromAnchor) && model.GetMatchedWords(arrayRun.LengthFromAnchor).Count == 0) basename = arrayRun.LengthFromAnchor; // basename is now a 'parent table' name, if there is one

               var groups = model.GetTableGroups(basename) ?? new[] { new TableGroup("Other", new[] { originalTableName }) };
               foreach (var group in groups) {
                  foreach (var table in group.Tables) {
                     var (tableName, partition) = (table, 0);
                     var parts = table.Split(ArrayRunSplitterSegment.Separator);
                     if (parts.Length == 2) {
                        tableName = parts[0];
                        if (!int.TryParse(parts[1], out partition)) partition = 0;
                     }

                     var currentArrayStart = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, tableName);
                     if (model.GetNextRun(currentArrayStart) is not ArrayRun currentArray) continue;
                     var currentIndex = index + currentArray.ParentOffset.BeginningMargin;
                     if (currentIndex >= 0 && currentIndex < currentArray.ElementCount) {
                        elementOffset = currentArray.Start + currentArray.ElementLength * currentIndex;
                        AddChild(new SplitterArrayElementViewModel(viewPort, tableName, elementOffset));
                        AddChildrenFromTable(currentArray, currentIndex, partition);
                     }
                  }
                  while (Groups.Count <= childIndex.Group) Groups.Add(new TableGroupViewModel());
                  Groups[childIndex.Group].GroupName = group.GroupName;
                  MoveToNextGroup();
               }
            }

            AddChildrenFromStreams(array, basename, index);
         }

         while (Groups.Count > childIndex.Group) Groups.RemoveAt(Groups.Count - 1);
         while (UsageChildren.Count > usageChildInsertionIndex) UsageChildren.RemoveAt(UsageChildren.Count - 1);
         foreach (var group in Groups) {
            foreach (var member in group.Members) member.DataChanged += ForwardModelChanged;
         }
         foreach (var child in UsageChildren) child.DataChanged += ForwardModelChanged;

         var paletteIndex = Children.Where(child => child is SpriteElementViewModel).Select(c => {
            var spriteElement = (SpriteElementViewModel)c;
            if (spriteElement.CurrentPalette > spriteElement.MaxPalette) return 0;
            return spriteElement.CurrentPalette;
         }).Concat(1.Range()).Max();
         foreach (var child in Children) {
            // update sprites now that all the associated palettes have been loaded.
            if (child is SpriteElementViewModel sevm) {
               sevm.CurrentPalette = paletteIndex;
               sevm.UpdateTiles();
            }
            // update 'visible' for children based on their parents.
            if (child is SplitterArrayElementViewModel splitter) splitter.UpdateCollapsed(fieldFilter);
         }

         NotifyPropertyChanged(nameof(Children));
      }

      private void UpdateCurrentElementSelector(ITableRun array, int index) {
         CurrentElementSelector.SetupFromModel(array.Start + array.ElementLength * index);
      }

      private void UpdateViewPortSelectionFromTableComboBoxIndex(object sender = null, EventArgs e = null) {
         var array = (ITableRun)model.GetNextRun(Address);
         var address = array.Start + array.ElementLength * CurrentElementSelector.SelectedIndex;
         selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(address);
         selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(address + array.ElementLength - 1);
      }

      private void AddChildrenFromStreams(ITableRun array, string basename, int index) {
         var plmResults = new List<(int, int)>();
         var eggResults = new List<(int, int)>();
         var trainerResults = new List<int>();
         var streamResults = new List<(int, int)>();
         foreach (var child in model.Streams) {
            if (!child.DependsOn(basename)) continue;
            if (child is PLMRun plmRun) plmResults.AddRange(plmRun.Search(index));
            if (child is EggMoveRun eggRun) eggResults.AddRange(eggRun.Search(basename, index));
            if (child is TrainerPokemonTeamRun trainerRun) trainerResults.AddRange(trainerRun.Search(basename, index));
            if (child is TableStreamRun streamRun) streamResults.AddRange(streamRun.Search(basename, index));
         }
         var parentOffset = array is ArrayRun arrayRun ? arrayRun.ParentOffset.BeginningMargin : 0;
         var elementName = array.ElementNames.Count > index + parentOffset && index + parentOffset >= 0 ? array.ElementNames[index + parentOffset] : "Element " + index;

         if (trainerResults.Count > 0) {
            var selections = trainerResults.Select(result => (result, result + 1)).ToList();
            var trainerAddresses = model.GetTable(HardcodeTablesModel.TrainerTableName) is ITableRun trainerTable ? selections.Select(s => {
               var parentPointer = model.GetNextRun(s.Item1).PointerSources?.FirstOrDefault();
               if (parentPointer is int source) {
                  var index = trainerTable.ConvertByteOffsetToArrayOffset(source).ElementIndex;
                  return (trainerTable.Start + trainerTable.ElementLength * index, trainerTable.Start + trainerTable.ElementLength * (index + 1) - 1);
               }
               return s;
            }).ToList() : null;
            AddUsageChild(new ButtonArrayElementViewModel("trainer teams", () => {
               viewPort.OpenSearchResultsTab($"{elementName} within {HardcodeTablesModel.TrainerTableName}", selections, trainerAddresses);
            }));
         }

         if (plmResults.Count > 0) {
            AddUsageChild(new ButtonArrayElementViewModel("level-up moves", () => {
               viewPort.OpenSearchResultsTab($"{elementName} within {HardcodeTablesModel.LevelMovesTableName}", plmResults);
            }));
         }

         if (eggResults.Count > 0) {
            AddUsageChild(new ButtonArrayElementViewModel("egg moves", () => {
               viewPort.OpenSearchResultsTab($"{elementName} within {HardcodeTablesModel.EggMovesTableName}", eggResults);
            }));
         }

         foreach (var table in model.Arrays) {
            if (!table.DependsOn(basename)) continue;
            var results = new List<(int, int)>(table.Search(model, basename, index));
            if (results.Count == 0) continue;
            var name = model.GetAnchorFromAddress(-1, table.Start);
            var shortName = name;
            if (shortName.StartsWith("data.")) shortName = shortName.Replace("data.", "...");
            if (shortName.StartsWith("...poke")) shortName = shortName.Replace("...poke", "...");
            AddUsageChild(new ButtonArrayElementViewModel(shortName, name, () => {
               viewPort.OpenSearchResultsTab($"{elementName} within {name}", results);
            }));
         }

         if (streamResults.Count > 0) {
            AddUsageChild(new ButtonArrayElementViewModel("other streams", () => {
               viewPort.OpenSearchResultsTab($"{elementName} within streams", streamResults);
            }));
         }
      }

      private void AddChildrenFromTable(ITableRun table, int index, int splitPortion = -1) {
         var itemAddress = table.Start + table.ElementLength * index;
         var currentPartition = 0;
         foreach (var itemSegment in table.ElementContent) {
            var item = itemSegment;
            if (item is ArrayRunRecordSegment recordItem) item = recordItem.CreateConcrete(model, itemAddress);

            if (itemSegment is ArrayRunSplitterSegment) {
               currentPartition += 1;
               continue;
            } else if (splitPortion != -1 && splitPortion != currentPartition) {
               itemAddress += item.Length;
               continue;
            }

            IArrayElementViewModel viewModel = null;
            if (item.Type == ElementContentType.Unknown) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, HexFieldStratgy.Instance);
            else if (item.Type == ElementContentType.PCS) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new TextFieldStrategy());
            else if (item.Type == ElementContentType.Pointer) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new AddressFieldStratgy());
            else if (item.Type == ElementContentType.BitArray) viewModel = new BitListArrayElementViewModel(selection, history, model, item.Name, itemAddress);
            else if (item.Type == ElementContentType.Integer) {
               if (item is ArrayRunEnumSegment enumSegment) {
                  viewModel = new ComboBoxArrayElementViewModel(viewPort, selection, item.Name, itemAddress, item.Length);
                  var anchor = model.GetAnchorFromAddress(-1, table.Start);
                  var enumSourceTableStart = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, enumSegment.EnumName);
                  if (!string.IsNullOrEmpty(anchor) && model.GetDependantArrays(anchor).Count() == 1 && enumSourceTableStart >= 0) {
                     AddChild(viewModel);
                     viewModel = new BitListArrayElementViewModel(selection, history, model, item.Name, itemAddress);
                  }
               } else if (item is ArrayRunTupleSegment tupleItem) {
                  viewModel = new TupleArrayElementViewModel(viewPort, tupleItem, itemAddress);
               } else if (item is ArrayRunHexSegment) {
                  viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, HexFieldStratgy.Instance);
               } else if (item is ArrayRunColorSegment) {
                  viewModel = new ColorFieldArrayElementViewModel(viewPort, item.Name, itemAddress);
               } else if (item is ArrayRunCalculatedSegment calcSeg) {
                  viewModel = new CalculatedElementViewModel(viewPort, calcSeg, itemAddress);
               } else {
                  viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new NumericFieldStrategy());
               }
            } else {
               throw new NotImplementedException();
            }
            if (!SkipElement(item)) {
               AddChild(viewModel);
               AddChildrenFromPointerSegment(itemAddress, item, childIndex.Member - 1, recursionLevel: 0);
            }
            itemAddress += item.Length;
         }
      }

      private static bool SkipElement(ArrayRunElementSegment element) {
         return element.Name.StartsWith("unused") || element.Name.StartsWith("padding");
      }

      private void AddChildrenFromPointerSegment(int itemAddress, ArrayRunElementSegment item, int parentIndex, int recursionLevel) {
         if (!(item is ArrayRunPointerSegment pointerSegment)) return;
         if (pointerSegment.InnerFormat == string.Empty) return;
         var destination = model.ReadPointer(itemAddress);
         IFormattedRun streamRun = null;
         if (destination != Pointer.NULL) {
            streamRun = model.GetNextRun(destination);
            if (!pointerSegment.DestinationDataMatchesPointerFormat(model, new NoDataChangeDeltaModel(), itemAddress, destination, null, parentIndex)) streamRun = null;
            if (streamRun != null && streamRun.Start != destination) {
               // For some reason (possibly because of a run length conflict),
               //    the destination data appears to match the expected type,
               //    but there is no run for it.
               // Go ahead and generate a new temporary run for the data.
               var strategy = model.FormatRunFactory.GetStrategy(pointerSegment.InnerFormat);
               strategy.TryParseData(model, string.Empty, destination, ref streamRun);
            }
         }

         IStreamArrayElementViewModel streamElement = null;
         if (streamRun == null || streamRun is IStreamRun) streamElement = new TextStreamElementViewModel(viewPort, itemAddress, pointerSegment.InnerFormat);
         if (streamRun is ISpriteRun spriteRun) streamElement = new SpriteElementViewModel(viewPort, spriteRun.FormatString, spriteRun.SpriteFormat, itemAddress);
         if (streamRun is IPaletteRun paletteRun) streamElement = new PaletteElementViewModel(viewPort, history, paletteRun.FormatString, paletteRun.PaletteFormat, itemAddress);
         if (streamRun is TrainerPokemonTeamRun tptRun) streamElement = new TrainerPokemonTeamElementViewModel(viewPort, tptRun, itemAddress);
         if (streamElement == null) return;

         var streamAddress = itemAddress;
         var myIndex = childIndex.Member;
         Groups[childIndex.Group].Members[parentIndex].DataChanged += (sender, e) => {
            var closure_destination = model.ReadPointer(streamAddress);
            var run = model.GetNextRun(closure_destination) as IStreamRun;
            IStreamArrayElementViewModel newStream = null;

            if (run == null || run is IStreamRun) newStream = new TextStreamElementViewModel(viewPort, streamAddress, pointerSegment.InnerFormat);
            if (run is ISpriteRun spriteRun1) newStream = new SpriteElementViewModel(viewPort, spriteRun1.FormatString, spriteRun1.SpriteFormat, streamAddress);
            if (run is IPaletteRun paletteRun1) newStream = new PaletteElementViewModel(viewPort, history, paletteRun1.FormatString, paletteRun1.PaletteFormat, streamAddress);

            newStream.DataChanged += ForwardModelChanged;
            newStream.DataMoved += ForwardModelDataMoved;
            if (!Groups[childIndex.Group].Members[myIndex].TryCopy(newStream)) Groups[childIndex.Group].Members[myIndex] = newStream;
         };
         streamElement.DataMoved += ForwardModelDataMoved;
         AddChild(streamElement);

         parentIndex = childIndex.Member - 1;
         if (streamRun is ITableRun tableRun && recursionLevel < 1) {
            int segmentOffset = 0;
            for (int i = 0; i < tableRun.ElementContent.Count; i++) {
               if (!(tableRun.ElementContent[i] is ArrayRunPointerSegment)) { segmentOffset += tableRun.ElementContent[i].Length; continue; }
               for (int j = 0; j < tableRun.ElementCount; j++) {
                  itemAddress = tableRun.Start + segmentOffset + j * tableRun.ElementLength;
                  AddChildrenFromPointerSegment(itemAddress, tableRun.ElementContent[i], parentIndex, recursionLevel + 1);
               }
               segmentOffset += tableRun.ElementContent[i].Length;
            }
         }
      }

      private void ApplyFieldFilter() {
         foreach (var child in Children) {
            // update 'visible' for children based on their parents.
            if (child is SplitterArrayElementViewModel splitter) splitter.UpdateCollapsed(fieldFilter);
         }
      }

      private void ForwardModelChanged(object sender, EventArgs e) => ModelDataChanged?.Invoke(this, model.GetNextRun(Address));
      private void ForwardModelDataMoved(object sender, (int originalStart, int newStart) e) => ModelDataMoved?.Invoke(this, e);
   }
}
