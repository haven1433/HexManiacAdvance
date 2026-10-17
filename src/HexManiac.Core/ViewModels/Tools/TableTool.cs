using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableTool : ViewModelCore, IToolViewModel {
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ViewPort viewPort;
      private readonly IToolTrayViewModel toolTray;
      private readonly IWorkDispatcher dispatcher;
      private readonly IDelayWorkTimer loadMapUsageTimer, dataChangedTimer;

      public string Name => "Table";

      public IReadOnlyList<string> TableSections => GetTableNames().Sections;
      private int selectedTableSection;
      public int SelectedTableSection {
         get => selectedTableSection;
         set {
            if (dataForCurrentRunChangeUpdate) return; // recursion guard: ignore this update if we're about to change it manually
            Set(ref selectedTableSection, value, UpdateTableList);
         }
      }

      public IReadOnlyList<string> TableList {
         get {
            var names = GetTableNames();
            if (selectedTableSection == -1 || selectedTableSection >= names.Sections.Count) return new string[0];
            return names.TablesIn(names.Sections[selectedTableSection]);
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

      private IReadOnlyList<ITableRun> UnmatchedArrays {
         get {
            var list = new List<ITableRun>();
            foreach (var anchor in model.Anchors) {
               var table = model.GetTable(anchor);
               if (table is null) continue; // skip anything that's not a table
               if (table is ArrayRun array && !string.IsNullOrEmpty(array.LengthFromAnchor)) continue; // skip 'matched-length' arrays
               list.Add(table);
            }
            return list;
         }
      }

      /// <summary>
      /// The names behind the two dropdowns at the top of the tool (section, table).
      /// Working them out means looking at every table in the ROM, and every refresh asks for them several times,
      /// so they are remembered until the data changes. That also means the dropdowns get the same list back
      /// and don't have to throw away and rebuild their items each time the cursor moves.
      /// </summary>
      private sealed class TableNames {
         public IReadOnlyList<string> Sections { get; }
         private readonly List<string> names;
         private readonly Dictionary<string, IReadOnlyList<string>> tablesInSection = new();

         public TableNames(List<string> names) {
            this.names = names;
            var sections = names.Select(name => {
               var parts = name.Split('.');
               if (parts.Length > 2) return string.Join(".", parts.Take(2));
               return parts[0];
            }).Distinct().ToList();
            sections.Sort();
            Sections = sections;
         }

         public IReadOnlyList<string> TablesIn(string section) {
            lock (tablesInSection) {
               if (tablesInSection.TryGetValue(section, out var existing)) return existing;
               var tableList = names
                  .Where(name => name.StartsWith(section + "."))
                  .Select(name => name.Substring(section.Length + 1))
                  .ToList();
               tableList.Sort();
               return tablesInSection[section] = tableList;
            }
         }
      }

      private TableNames GetTableNames() {
         return model.CurrentCacheScope.GetOrAdd("table-tool-names", () => new TableNames(UnmatchedArrays.Select(array => model.GetAnchorFromAddress(-1, array.Start)).ToList()));
      }

      private string currentElementName;
      public string CurrentElementName {
         get => currentElementName;
         private set => TryUpdate(ref currentElementName, value);
      }

      public FilteringComboOptions CurrentElementSelector { get; }

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

      private bool useMultiFieldFeature = false;
      public bool UseMultiFieldFeature {
         get => useMultiFieldFeature;
         set => Set(ref useMultiFieldFeature, value, old => {
            foreach (var group in Groups) group.UseMultiFieldFeature = useMultiFieldFeature;
            DataForCurrentRunChanged();
         });
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

      private bool usageOptionsOpen;
      public bool UsageOptionsOpen { get => usageOptionsOpen; set => Set(ref usageOptionsOpen, value); }

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

      private bool ignoreFurtherCommands = false;
      public TableTool(IDataModel model, Selection selection, ChangeHistory<ModelDelta> history, ViewPort viewPort, IToolTrayViewModel toolTray, IWorkDispatcher dispatcher) {
         this.model = model;
         this.selection = selection;
         this.history = history;
         this.viewPort = viewPort;
         this.toolTray = toolTray;
         this.dispatcher = dispatcher;
         loadMapUsageTimer = dispatcher.CreateDelayTimer();
         dataChangedTimer =  /* */ new ImmediateWorkTimer(); //*/ dispatcher.CreateDelayTimer();
         CurrentElementSelector = new FilteringComboOptions();
         CurrentElementSelector.Bind(nameof(FilteringComboOptions.ModelValue), UpdateViewPortSelectionFromTableComboBoxIndex);
         Groups = new();
         UsageChildren = new();

         previous = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ITableRun;
               return array != null && array.Start < address;
            },
            Execute = async parameter => {
               if (ignoreFurtherCommands) return;
               using var _ = Scope(ref ignoreFurtherCommands, true, value => ignoreFurtherCommands = value);
               await dispatcher.WaitForRenderingAsync();

               var (start, end) = (selection.Scroll.ViewPointToDataIndex(selection.SelectionStart), selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
               var array = (ITableRun)model.GetNextRun(address);
               if (array.Start <= start && start < array.Start + array.Length && array.Start <= end && end < array.Start + array.Length) {
                  start -= array.ElementLength;
                  end -= array.ElementLength;
               } else {
                  start = address - array.ElementLength;
                  end = address - 1;
               }
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(start);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(end);
            }
         };

         next = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ITableRun;
               return array != null && array.Start + array.Length > address + array.ElementLength;
            },
            Execute = async parameter => {
               if (ignoreFurtherCommands) return;
               using var _ = Scope(ref ignoreFurtherCommands, true, value => ignoreFurtherCommands = value);
               await dispatcher.WaitForRenderingAsync();

               var (start, end) = (selection.Scroll.ViewPointToDataIndex(selection.SelectionStart), selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));
               var array = (ITableRun)model.GetNextRun(address);
               if (array.Start <= start && start < array.Start + array.Length && array.Start <= end && end < array.Start + array.Length) {
                  start += array.ElementLength;
                  end += array.ElementLength;
               } else {
                  start = address + array.ElementLength;
                  end = address + array.ElementLength * 2 - 1;
               }
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(start);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(end);
            }
         };

         append = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ITableRun;
               if (array == null || !array.CanAppend) return false;
               if (array is TableStreamRun stream && stream.AllowsZeroElements && address == array.Start + array.ElementLength * array.ElementCount) return true;
               return address == array.Start + array.ElementLength * (array.ElementCount - 1);
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
                  }
                  viewPort.Refresh();
                  RequestMenuClose?.Invoke(this, EventArgs.Empty);
                  if (model is PokemonModel pModel) pModel.ResolveConflicts();
               }
               AddCount = 1;
            }
         };

         CurrentElementName = "The Table tool only works if your cursor is on table data.";
      }

      public const string TrainerSlidesTable = "data.trainers.slides";

      public IList<IArrayElementViewModel> Children => Groups.SelectMany(group => group.Members).ToList();

      private bool widenedForSecondaryColumn;

      /// <summary>
      /// The overworld sprites get one more column to the right of their fields: the field effects and the reflection palettes (see <see cref="OverworldSecondaryElementViewModel"/>).
      /// The tool panel grows to make room for it the first time, if it is narrower than three columns need; the panel can be made narrower again by hand, and then it stays that way.
      /// </summary>
      private void AddOverworldSecondaryColumn() {
         while (Groups.Count <= childIndexGroup) AddGroup();
         AddChild(new OverworldSecondaryElementViewModel(viewPort));
         Groups[childIndexGroup].Close();
         childIndexGroup++;
         if (!widenedForSecondaryColumn) {
            widenedForSecondaryColumn = true;
            viewPort.EnsureToolPanelWidth(OverworldSecondaryElementViewModel.WantedToolPanelWidth);
         }
      }

      /// <summary>Switches the secondary overworld panel next to the overworld sprites to another section.</summary>
      public void ShowSecondarySection(OverworldSecondarySection section) {
         foreach (var panel in Children.OfType<OverworldSecondaryElementViewModel>()) panel.Show(section);
      }

      private void AddGroup() {
         Groups.Add(new TableGroupViewModel(viewPort) {
            ForwardModelChanged = element => element.DataChanged += ForwardModelChanged,
            ForwardModelDataMoved = element => element.DataMoved += ForwardModelDataMoved,
         });
      }

      private int childIndexGroup = 0;
      private void AddChild(IArrayElementViewModel child) {
         if (child == null) return;
         while (Groups.Count <= childIndexGroup) AddGroup();
         Groups[childIndexGroup].Add(child);
      }

      private void MoveToNextGroup() {
         Groups[childIndexGroup].Close();
         childIndexGroup += 1;
         if (Groups.Count > childIndexGroup) Groups[childIndexGroup].Open();
      }

      public bool HasUsageOptions {
         get {
            foreach (var child in UsageChildren) {
               if (child is not MapOptionsArrayElementViewModel mapUsage) return true;
               return mapUsage.MapPreviews.Count > 0;
            }
            return false;
         }
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
         // ignore callbacks while any held comboboxes are open
         foreach (var group in Groups) {
            foreach (var member in group.Members) {
               if (member is ComboBoxArrayElementViewModel box && box.FilteringComboOptions.DropDownIsOpen) return;
               if (member is TableRowsStreamElementViewModel rows && rows.IsDropDownOpen) return;
            }
         }

         // must be longer than initial key-hold delay or app will studder
         dataChangedTimer.DelayCall(TimeSpan.FromSeconds(.6), DataForCurrentRunChangedCore);
      }

      /// <summary>
      /// What the content of the tool was built from. Building the content for a table element is the expensive part of selecting a tab (sprites, palettes, long lists of choices),
      /// so when a tab is selected again and nothing it was built from has changed, the content can stay as it is: see <see cref="IsUpToDate"/>.
      /// </summary>
      private (long edits, int address, bool spartanMode, string fieldFilter, bool useMultiFieldFeature)? builtFrom;
      private (long edits, int address, bool spartanMode, string fieldFilter, bool useMultiFieldFeature) CurrentSignature => (ModelEditStamp.Current, address, viewPort.SpartanMode, fieldFilter, useMultiFieldFeature);

      /// <summary>
      /// True if the content is exactly what <see cref="DataForCurrentRunChanged"/> would build right now:
      /// the last build finished, and since then no model has been edited and the cursor, the filter and the modes are the same.
      /// A model that is still loading is never up to date, because it finds its tables in the background without going through the edit counter.
      /// </summary>
      public bool IsUpToDate => builtFrom.HasValue && model.InitializationWorkload.IsCompleted && builtFrom.Value == CurrentSignature;

      /// <summary>The same as <see cref="DataForCurrentRunChanged"/>, but does nothing if the content is already <see cref="IsUpToDate"/>.</summary>
      public void DataForCurrentRunChangedIfNeeded() {
         if (!IsUpToDate) DataForCurrentRunChanged();
      }

      private void DataForCurrentRunChangedCore() {
         builtFrom = null; // until this build is over, the content is not trustworthy
         DataForCurrentRunChangedBuild();
         if (model.InitializationWorkload.IsCompleted) builtFrom = CurrentSignature;
      }

      private void DataForCurrentRunChangedBuild() {
         foreach (var group in Groups) {
            foreach (var member in group.Members) ClearHandlers(member);
         }
         foreach (var child in UsageChildren) ClearHandlers(child);
         foreach (var group in Groups) group.Open();
         childIndexGroup = 0;
         usageChildInsertionIndex = 0;

         var array = model.GetNextRun(Address) as ITableRun;
         if (array == null || array.Start > Address) {
            CurrentElementName = "The Table tool only works if your cursor is on table data.";
            Groups.Clear();
            UsageChildren.Clear();
            using (Scope(ref dataForCurrentRunChangeUpdate, true, val => dataForCurrentRunChangeUpdate = val)) {
               NotifyPropertyChanged(nameof(TableSections));
            }
            NotifyPropertyChanged(nameof(HasUsageOptions));
            return;
         }

         dataForCurrentRunChangeUpdate = true;
         var basename = model.GetAnchorFromAddress(-1, array.Start);
         var hasAnchorName = !string.IsNullOrEmpty(basename);
         var anchorParts = basename.Split('.');
         NotifyPropertyChanged(nameof(TableSections));
         if (anchorParts.Length == 1) {
            TryUpdate(ref selectedTableSection, TableSections.IndexOf(anchorParts[0]), nameof(SelectedTableSection));
            NotifyPropertyChanged(nameof(TableList));
         } else if (anchorParts.Length == 2) {
            TryUpdate(ref selectedTableSection, TableSections.IndexOf(anchorParts[0]), nameof(SelectedTableSection));
            NotifyPropertyChanged(nameof(TableList));
            TryUpdate(ref selectedTableIndex, TableList.IndexOf(anchorParts[1]), nameof(SelectedTableSection));
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

            TableGroupViewModel streamGroup = null;
            var showsOverworldSprites = false;
            var elementOffset = array.Start + array.ElementLength * index;
            if (array is not ArrayRun arrayRun) {
               if (Groups.Count == 2) {
                  streamGroup = Groups[1];
                  streamGroup.GroupName = TableGroupViewModel.DefaultName; // don't keep the title this group had for the previous table (a stray "Icon" bar above a wild encounter list)
                  streamGroup.Open();
               } else {
                  streamGroup = new TableGroupViewModel(viewPort) {
                     ForwardModelChanged = element => element.DataChanged += ForwardModelChanged,
                     ForwardModelDataMoved = element => element.DataMoved += ForwardModelDataMoved,
                  };
                  streamGroup.Open();
               }

               var header = new SplitterArrayElementViewModel(viewPort, basename, elementOffset);
               AddChild(header);
               Groups[childIndexGroup].AddChildrenFromTable(viewPort, selection, array, index, header, streamGroup);
               MoveToNextGroup();
               Groups[0].GroupName = basename;
            } else {
               index -= arrayRun.ParentOffset.BeginningMargin;
               var originalTableName = basename;
               if (!string.IsNullOrEmpty(arrayRun.LengthFromAnchor) && model.GetMatchedWords(arrayRun.LengthFromAnchor).Count == 0) basename = arrayRun.LengthFromAnchor; // basename is now a 'parent table' name, if there is one

               IReadOnlyList<TableGroup> groups = new[] { new TableGroup(TableGroupViewModel.DefaultName, new[] { originalTableName }) };
               if (!viewPort.SpartanMode) groups = model.GetTableGroups(basename) ?? groups;
               if (groups.Count == 1) {
                  if (Groups.Count == 2) {
                     streamGroup = Groups[1];
                     streamGroup.GroupName = TableGroupViewModel.DefaultName;
                     streamGroup.Open();
                  } else {
                     streamGroup = new TableGroupViewModel(viewPort) {
                        ForwardModelChanged = element => element.DataChanged += ForwardModelChanged,
                        ForwardModelDataMoved = element => element.DataMoved += ForwardModelDataMoved,
                     };
                     streamGroup.Open();
                  }
               }
               foreach (var group in groups) {
                  while (Groups.Count <= childIndexGroup) AddGroup();
                  var helperGroup = streamGroup ?? Groups[childIndexGroup];
                  foreach (var table in group.Tables) {
                     var member = TableGroupMember.Parse(table);
                     var (tableName, partition) = (member.TableName, member.Partition);

                     var currentArrayStart = model.GetAddressFromAnchor(new(), -1, tableName);
                     if (model.GetNextRun(currentArrayStart) is not ArrayRun currentArray) continue;
                     var currentIndex = index + currentArray.ParentOffset.BeginningMargin;
                     if (currentIndex >= 0 && currentIndex < currentArray.ElementCount) {
                        elementOffset = currentArray.Start + currentArray.ElementLength * currentIndex;
                        var header = new SplitterArrayElementViewModel(viewPort, tableName, elementOffset);
                        AddChild(header);
                        Groups[childIndexGroup].AddChildrenFromTable(viewPort, selection, currentArray, currentIndex, header, helperGroup, partition, member.Fields);
                        // a picture is worth a thousand clicks: offer the gallery of every overworld sprite
                        if (tableName == HardcodeTablesModel.OverworldSprites) {
                           AddChild(new SpriteGalleryElementViewModel(viewPort, currentIndex));
                           showsOverworldSprites = true;
                        }
                        // the field effects (grass, ripples, dust, ...): their frames with the palette the game uses, and the palette one click away
                        if (FieldEffects.IsFieldEffectTable(tableName)) AddChild(new FieldEffectElementViewModel(viewPort, currentIndex));
                        // expansion trainers: the battle messages (trainer slides) live in their own table, one click away
                        if (tableName == HardcodeTablesModel.TrainerTableName && (member.HasFieldList ? member.ShowsField("name") : partition == 2) && model.GetTable(TrainerSlidesTable) is ITableRun slides && currentIndex < slides.ElementCount) {
                           var slideIndex = currentIndex;
                           AddChild(new ButtonArrayElementViewModel("Battle messages…", "Edit what this trainer says during battle (first turn, last Pokémon, low HP, ...)", () => viewPort.Goto.Execute($"{TrainerSlidesTable}/{slideIndex}")));
                        }
                     }
                  }
                  while (Groups.Count <= childIndexGroup) AddGroup();
                  Groups[childIndexGroup].GroupName = group.GroupName;
                  MoveToNextGroup();
               }
            }

            if (Groups.Count == 1) {
               streamGroup.Close(); // before the view hears about the group, so it gets all of the group at once
               Groups.Add(streamGroup);
               childIndexGroup++;
            } else if (streamGroup != null) {
               while (Groups.Count <= childIndexGroup) AddGroup();
               streamGroup.Close();
               // putting the very same group back would still tell the view that the group was replaced, and it would rebuild everything in it
               if (!ReferenceEquals(Groups[childIndexGroup], streamGroup)) Groups[childIndexGroup] = streamGroup;
               childIndexGroup++;
            }
            if (showsOverworldSprites && OverworldSecondaryElementViewModel.IsSupported(model)) AddOverworldSecondaryColumn();
            if (array is not ArrayRun && streamGroup != null) streamGroup.GroupName = NameForStreamGroup(streamGroup);
            if (hasAnchorName || basename != array.Start.ToString("X6")) AddChildrenFromStreams(array, basename, index); // no stream, table, script or map can use a table that has no name (like an encounter list): skip the search through the whole ROM
         }

         while (Groups.Count > childIndexGroup) Groups.RemoveAt(Groups.Count - 1);
         while (UsageChildren.Count > usageChildInsertionIndex) UsageChildren.RemoveAt(UsageChildren.Count - 1);
         foreach (var group in Groups) {
            foreach (var member in group.Members) AddHandlers(member);
         }
         foreach (var child in UsageChildren) AddHandlers(child);
         AddDummyGroup();

         var paletteIndex = Children.Where(child => child is SpriteElementViewModel).Select(c => {
            var spriteElement = (SpriteElementViewModel)c;
            if (spriteElement.CurrentPalette > spriteElement.MaxPalette) return 0;
            return spriteElement.CurrentPalette;
         }).Concat(1.Range()).Max();
         foreach (var child in Children) {
            // update sprites now that all the associated palettes have been loaded.
            if (child is SpriteElementViewModel sevm) {
               // using var scope = sevm.SilencePropertyNotifications();
               sevm.CurrentPalette = paletteIndex;
               sevm.UpdateTiles();
            }
            // update 'visible' for children based on their parents.
            if (child is SplitterArrayElementViewModel splitter) splitter.UpdateCollapsed(fieldFilter);
         }

         // an animation preview shows what the sprites next to it show: now that they are done, it can catch up, so a new pokemon's animation
         // replaces the old one in the same step as the new sprites (without this, it would catch up after its debounce, a visible moment later)
         foreach (var child in Children) {
            if (child is PokemonAnimationPreviewViewModel preview) preview.UpdateNow();
         }

         foreach (var group in Groups) Debug.Assert(!group.IsOpen, "You forgot to close a group! " + group.GroupName);
         NotifyPropertyChanged(nameof(HasUsageOptions)); // once, when all the usage buttons are in place (not after each one)
         NotifyPropertyChanged(nameof(Children));
      }

      /// <summary>
      /// An embedded table (one found through a pointer, like a wild encounter list) is titled with its address on the left,
      /// so the group of linked data on the right gets a title as well: the name of the field it came from, if there is just one.
      /// </summary>
      private static string NameForStreamGroup(TableGroupViewModel group) {
         var streams = group.Members.OfType<StreamElementViewModel>().Where(stream => stream.Parent != null).ToList();
         return streams.Count == 1 ? streams[0].ParentName : TableGroupViewModel.DefaultName;
      }

      private void AddHandlers(IArrayElementViewModel member) {
         member.DataChanged += ForwardModelChanged;
         member.DataSelected += HandleDataSelected;
      }

      private void ClearHandlers(IArrayElementViewModel member) {
         member.DataChanged -= ForwardModelChanged;
         member.DataSelected -= HandleDataSelected;
      }

      private void UpdateCurrentElementSelector(ITableRun array, int index) {
         SetupFromModel(array.Start + array.ElementLength * index);
      }

      private bool selfChange = false;
      private void SetupFromModel(int address) {
         if (!(model.GetNextRun(address) is ITableRun tableRun)) return;
         var offset = tableRun.ConvertByteOffsetToArrayOffset(address);
         var allOptions = tableRun.ElementNames?.ToList();
         if (allOptions == null) allOptions = tableRun.ElementCount.Range().Select(i => i.ToString()).ToList();
         while (allOptions.Count < tableRun.ElementCount) {
            allOptions.Add(allOptions.Count.ToString());
         }
         var selectedIndex = offset.ElementIndex;
         var options = new List<ComboOption>();
         for (int i = 0; i < allOptions.Count; i++) {
            // var image = ToolTipContentVisitor.GetEnumImage(model, i, tableRun as ArrayRun);
            // if (image != null) options.Add(VisualComboOption.CreateFromSprite(allOptions[i], image.PixelData, image.PixelWidth, i, 1, true));
            // else
            options.Add(new ComboOption(allOptions[i], i));
         }
         using (Scope(ref selfChange, true, old => selfChange = old)) {
            CurrentElementSelector.Update(options, selectedIndex);
         }
      }


      /// <summary>
      /// This extra group is added just to make the single tables look right in the table tool.
      /// </summary>
      private void AddDummyGroup() {
         if (Groups.Count > 1) return;
         AddGroup();
      }

      private void UpdateViewPortSelectionFromTableComboBoxIndex(object sender = null, EventArgs e = null) {
         if (selfChange) return;
         if (CurrentElementSelector.DropDownIsOpen) return;
         var array = (ITableRun)model.GetNextRun(Address);
         var address = array.Start + array.ElementLength * CurrentElementSelector.SelectedIndex;
         selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(address);
         selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(address + array.ElementLength - 1);
      }

      // There are tens of thousands of streams and thousands of tables in a ROM, and which of them refer to a table
      // only changes when the data does: look once per table instead of on every refresh.
      // Which streams and tables use a table depends on the formats of the whole ROM (model.Streams alone goes through every run: 4 ms in CUBE),
      // and it can only change when a run (a format) of a model changed: typing a number into the table changes none. Keep the answer until then.
      private long usingCacheStamp = -1;
      private readonly Dictionary<string, IReadOnlyList<IStreamRun>> streamsUsingCache = new();
      private readonly Dictionary<string, IReadOnlyList<ArrayRun>> arraysUsingCache = new();

      private bool UsingCacheIsValid() {
         var stamp = ModelEditStamp.RunsCurrent;
         if (!model.InitializationWorkload.IsCompleted) return false; // a model that is still loading finds its tables without going through the edit counter
         if (usingCacheStamp != stamp) {
            streamsUsingCache.Clear();
            arraysUsingCache.Clear();
            usingCacheStamp = stamp;
         }
         return true;
      }

      private IReadOnlyList<IStreamRun> StreamsUsing(string basename) {
         var useCache = UsingCacheIsValid();
         if (useCache && streamsUsingCache.TryGetValue(basename, out var cached)) return cached;
         var result = model.CurrentCacheScope.GetOrAdd<IReadOnlyList<IStreamRun>>("table-tool-streams-using:" + basename, () => model.Streams.Where(stream => stream.DependsOn(basename)).ToList());
         if (useCache) streamsUsingCache[basename] = result;
         return result;
      }

      private IReadOnlyList<ArrayRun> ArraysUsing(string basename) {
         var useCache = UsingCacheIsValid();
         if (useCache && arraysUsingCache.TryGetValue(basename, out var cached)) return cached;
         var result = model.CurrentCacheScope.GetOrAdd<IReadOnlyList<ArrayRun>>("table-tool-arrays-using:" + basename, () => model.Arrays.Where(table => table.DependsOn(basename)).ToList());
         if (useCache) arraysUsingCache[basename] = result;
         return result;
      }

      private void AddChildrenFromStreams(ITableRun array, string basename, int index) {
         var plmResults = new List<(int, int)>();
         var eggResults = new List<(int, int)>();
         var trainerResults = new List<int>();
         var streamResults = new List<(int, int)>();
         foreach (var child in StreamsUsing(basename)) {
            if (child is PLMRun plmRun) plmResults.AddRange(plmRun.Search(index));
            if (child is EggMoveRun eggRun) eggResults.AddRange(eggRun.Search(basename, index));
            if (child is ITrainerTeamRun trainerRun) trainerResults.AddRange(trainerRun.Search(basename, index));
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
               UsageOptionsOpen = false;
               viewPort.OpenSearchResultsTab($"{elementName} within {HardcodeTablesModel.TrainerTableName}", selections, trainerAddresses);
            }));
         }

         if (plmResults.Count > 0) {
            AddUsageChild(new ButtonArrayElementViewModel("level-up moves", () => {
               UsageOptionsOpen = false;
               viewPort.OpenSearchResultsTab($"{elementName} within {HardcodeTablesModel.LevelMovesTableName}", plmResults);
            }));
         }

         if (eggResults.Count > 0) {
            AddUsageChild(new ButtonArrayElementViewModel("egg moves", () => {
               UsageOptionsOpen = false;
               viewPort.OpenSearchResultsTab($"{elementName} within {HardcodeTablesModel.EggMovesTableName}", eggResults);
            }));
         }

         foreach (var table in ArraysUsing(basename)) {
            var results = new List<(int, int)>(table.Search(model, basename, index));
            if (results.Count == 0) continue;
            var name = model.GetAnchorFromAddress(-1, table.Start);
            AddUsageChild(new ButtonArrayElementViewModel(name, name, () => {
               UsageOptionsOpen = false;
               viewPort.OpenSearchResultsTab($"{elementName} within {name}", results);
            }));
         }

         if (streamResults.Count > 0) {
            AddUsageChild(new ButtonArrayElementViewModel("other streams", () => {
               UsageOptionsOpen = false;
               viewPort.OpenSearchResultsTab($"{elementName} within streams", streamResults);
            }));
         }

         // only check XSE scripts for now
         if (viewPort.Tools.CodeTool.ScriptParser.DependsOn(basename).Any()) {
            AddUsageChild(new ButtonArrayElementViewModel("scripts", () => {
               var results = new List<(int, int)>(FindXseScriptUses(basename, index));
               if (results.Count == 0) {
                  OnMessage?.Invoke(this, "No matches were found.");
               } else {
                  UsageOptionsOpen = false;
                  viewPort.Tools.SelectedTool = viewPort.Tools.CodeTool;
                  viewPort.Tools.CodeTool.Mode = CodeMode.Script;
                  viewPort.OpenSearchResultsTab($"{elementName} within scripts", results);
                  viewPort.Tools.SelectedTool = viewPort.Tools.CodeTool;
                  viewPort.Tools.CodeTool.Mode = CodeMode.Script;
               }
            }));
         }

         // maps
         // Only tables that can be referenced from map events can have map uses.
         // Skip the (expensive, delayed) map search entirely for every other table.
         var canHaveMapUses =
            basename == HardcodeTablesModel.OverworldSprites ||
            basename == HardcodeTablesModel.ItemsTableName ||
            basename == HardcodeTablesModel.MapNameTable ||
            viewPort.Tools.CodeTool.ScriptParser.DependsOn(basename).Any();
         if (canHaveMapUses && viewPort.MapEditor != null && viewPort.MapEditor.IsValidState && !viewPort.SpartanMode) {
            var mapOptions = new MapOptionsArrayElementViewModel(dispatcher, loadMapUsageTimer, viewPort.MapEditor, basename, index);
            mapOptions.MapPreviews.CollectionChanged += (sender, e) => NotifyPropertyChanged(nameof(HasUsageOptions));
            AddUsageChild(mapOptions); // always add, but invisible when empty
         }
      }

      public IEnumerable<(int, int)> FindXseScriptUses(string basename, int index) {
         var parser = viewPort.Tools.CodeTool.ScriptParser;
         var lines = parser.DependsOn(basename).ToList();
         var filter = new List<byte>();
         foreach (var line in lines) {
            if (line is MacroScriptLine macro && macro.Args[0] is SilentMatchArg silent) filter.Add(silent.ExpectedValue);
            if (line is ScriptLine sl) filter.Add(line.LineCode[0]);
         }
         foreach (var spot in Flags.GetAllScriptSpots(model, parser, Flags.GetAllTopLevelScripts(model), filter.ToArray())) {
            int check = spot.Address + spot.Line.LineCode.Count;
            foreach (var arg in spot.Line.Args) {
               var length = arg.Length(model, check);
               if (arg.EnumTableName == basename) {
                  if (model.ReadMultiByteValue(check, length) == index) {
                     yield return (spot.Address, check + length - 1);
                  }
               }
               check += length;
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

      private void HandleDataSelected(object sender, EventArgs e) {
         if (sender is not IArrayElementViewModel vm) return;
         if (vm is FieldArrayElementViewModel field) viewPort.Goto.Execute(field.Start);
         if (vm is ComboBoxArrayElementViewModel combo) viewPort.Goto.Execute(combo.Start);
         if (vm is TupleArrayElementViewModel tuple) viewPort.Goto.Execute(tuple.Children[0].Start);
      }
   }
}
