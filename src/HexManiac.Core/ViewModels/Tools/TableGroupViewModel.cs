using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   ///// <summary>
   ///// Sometimes notifying after every change is too noisy.
   ///// Custom <see cref="INotifyCollectionChanged"/> implementation that allows delayed notifications.
   ///// </summary>
   public class ObservableList<T> : List<T>, INotifyCollectionChanged {
      public event NotifyCollectionChangedEventHandler? CollectionChanged;

      public ObservableList() : base() { }
      public ObservableList(IEnumerable<T> items) : base(items) { }

      public void RaiseRefresh() => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
   }

   public class TableGroupViewModel : ViewModelCore {
      public const string DefaultName = "Other";

      private bool isOpen;
      private int currentMember; // used with open/close when refreshing the collection

      private string groupName;
      public bool DisplayHeader => GroupName != DefaultName;
      public string GroupName { get => groupName; set => Set(ref groupName, value, old => NotifyPropertyChanged(nameof(DisplayHeader))); }

      private readonly ViewPort viewPort;

      public ObservableCollection<IArrayElementViewModel> Members { get; } = new();

      public Action<IStreamArrayElementViewModel> ForwardModelChanged { get; init; }
      public Action<IStreamArrayElementViewModel> ForwardModelDataMoved { get; init; }

      public TableGroupViewModel(ViewPort viewPort) { GroupName = DefaultName; this.viewPort = viewPort; }

      public bool IsOpen => isOpen;

      public void Open() {
         if (isOpen) return;
         currentMember = 0;
         pending.Clear();
         isOpen = true;
      }

      private bool useMultiFieldFeature = false;
      public bool UseMultiFieldFeature { get => useMultiFieldFeature; set => Set(ref useMultiFieldFeature, value); }

      private MultiFieldArrayElementViewModel multiInProgress;

      // While the group is open (a refresh is going on), the new members are collected here: Close() then brings Members in line with them in one go.
      // (Knowing the whole new list is what lets a member that appeared or disappeared in the middle cost one change, instead of changing every member after it.)
      private readonly List<IArrayElementViewModel> pending = new();

      public void Add(IArrayElementViewModel child) {
         if (UseMultiFieldFeature && child is IMultiEnabledArrayElementViewModel newField) {
            if (multiInProgress == null) multiInProgress = new(viewPort);
            multiInProgress.Add(newField);
            return;
         }
         if (multiInProgress != null) {
            Commit(multiInProgress);
            multiInProgress = null;
         }
         Commit(child);
      }

      private void Commit(IArrayElementViewModel member) {
         if (isOpen) pending.Add(member);
         else Members.Add(member); // a group nobody opened yet (a brand new one) simply grows
         currentMember++;
      }

      public void Close() {
         if (!isOpen) return;
         if (multiInProgress != null) {
            Commit(multiInProgress);
            multiInProgress = null;
         }
         try {
            Reconcile(pending);
         } finally {
            pending.Clear();
            isOpen = false;
         }
      }

      #region Reconciling Members with the new list

      /// <summary>Two members are of the same kind if a member can be updated to show what the other one shows (see <see cref="IArrayElementViewModel.TryCopy"/>) instead of being thrown away.</summary>
      private static bool SameKind(IArrayElementViewModel existing, IArrayElementViewModel replacement) {
         if (existing.GetType() != replacement.GetType()) return false;
         if (existing is FieldArrayElementViewModel field && replacement is FieldArrayElementViewModel other) return field.Type == other.Type;
         return true;
      }

      /// <summary>The name a member goes by, to prefer pairing members that have the same name when a choice has to be made. Null if it has none.</summary>
      private static string NameOf(IArrayElementViewModel member) => member switch {
         IMultiEnabledArrayElementViewModel field => field.Name,
         StreamElementViewModel stream => stream.ParentName,
         BitListArrayElementViewModel bits => bits.Name,
         TupleArrayElementViewModel tuple => tuple.Name,
         ButtonArrayElementViewModel button => button.Text,
         SplitterArrayElementViewModel splitter => splitter.SectionName,
         _ => null,
      };

      // finding the best pairing takes (old members) x (new members) steps: a refresh never gets near this, but a limit keeps a freak table from stalling the program
      private const int MaxPairingWork = 1_000_000;

      /// <summary>
      /// For every new member, the index of the existing member that will be updated to become it, or -1 if there is none (a new view model is needed).
      /// The pairs are in order: the existing members that are not paired are removed, the new members that are not paired are inserted.
      /// </summary>
      public static int[] PairUp(IReadOnlyList<IArrayElementViewModel> existing, IReadOnlyList<IArrayElementViewModel> replacements) {
         var pairs = new int[replacements.Count];
         Array.Fill(pairs, -1);

         // switching to the same kind of element (the usual refresh): everything stays where it is
         int head = 0;
         while (head < existing.Count && head < replacements.Count && SameKind(existing[head], replacements[head])) { pairs[head] = head; head++; }
         if (head == existing.Count && head == replacements.Count) return pairs;

         // the members before and after the part that changed keep their partner
         int oldEnd = existing.Count, newEnd = replacements.Count;
         while (oldEnd > head && newEnd > head && SameKind(existing[oldEnd - 1], replacements[newEnd - 1])) { oldEnd--; newEnd--; pairs[newEnd] = oldEnd; }

         // what is left in the middle: pair as many as possible, in order (the longest common subsequence). A pair with the same name counts more than one without.
         int oldCount = oldEnd - head, newCount = newEnd - head;
         if (oldCount == 0 || newCount == 0) return pairs;
         if ((long)oldCount * newCount > MaxPairingWork) {
            for (int i = 0; i < Math.Min(oldCount, newCount); i++) if (SameKind(existing[head + i], replacements[head + i])) pairs[head + i] = head + i;
            return pairs;
         }

         var oldNames = new string[oldCount];
         var newNames = new string[newCount];
         for (int i = 0; i < oldCount; i++) oldNames[i] = NameOf(existing[head + i]);
         for (int j = 0; j < newCount; j++) newNames[j] = NameOf(replacements[head + j]);
         int Weight(int i, int j) {
            if (!SameKind(existing[head + i], replacements[head + j])) return 0;
            return oldNames[i] != null && oldNames[i] == newNames[j] ? 3 : 2;
         }

         // best[i, j]: the best total for the first i existing and the first j new members of the middle
         var best = new int[oldCount + 1, newCount + 1];
         for (int i = 1; i <= oldCount; i++) {
            for (int j = 1; j <= newCount; j++) {
               var value = Math.Max(best[i - 1, j], best[i, j - 1]);
               var weight = Weight(i - 1, j - 1);
               if (weight > 0) value = Math.Max(value, best[i - 1, j - 1] + weight);
               best[i, j] = value;
            }
         }
         for (int i = oldCount, j = newCount; i > 0 && j > 0;) {
            var weight = Weight(i - 1, j - 1);
            if (weight > 0 && best[i, j] == best[i - 1, j - 1] + weight) {
               pairs[head + j - 1] = head + i - 1;
               i--; j--;
            } else if (best[i - 1, j] >= best[i, j - 1]) {
               i--;
            } else {
               j--;
            }
         }
         return pairs;
      }

      /// <summary>
      /// Makes <see cref="Members"/> show the new members. An existing member that is paired with a new one is updated with it (so the view keeps the controls it has built),
      /// a new one without a partner is inserted, and an existing one without a partner is removed.
      /// Next to each other, an unpaired existing and an unpaired new member are swapped in one step.
      /// </summary>
      private void Reconcile(List<IArrayElementViewModel> replacements) {
         var pairs = PairUp(Members, replacements);
         // for each new member: the existing member its next partner is (everything before that, that was not paired, is not needed any more)
         var nextPartner = new int[replacements.Count];
         for (int j = replacements.Count - 1, next = Members.Count; j >= 0; j--) {
            nextPartner[j] = next;
            if (pairs[j] >= 0) next = pairs[j];
         }

         int existingIndex = 0; // the existing member (as numbered before any change) that Members[position] is
         int position = 0;
         for (int j = 0; j < replacements.Count; j++) {
            var replacement = replacements[j];
            if (pairs[j] >= 0) {
               while (existingIndex < pairs[j]) { Members.RemoveAt(position); existingIndex++; }
               if (!Members[position].TryCopy(replacement)) Members[position] = replacement;
               existingIndex++;
            } else if (existingIndex < nextPartner[j]) {
               Members[position] = replacement;
               existingIndex++;
            } else {
               Members.Insert(position, replacement);
            }
            position++;
         }
         while (Members.Count > position) Members.RemoveAt(Members.Count - 1);
      }

      #endregion

      /// <param name="splitPortion">Only add the fields of this partition (partitions are separated by '|' in the table's format). -1 adds every field.</param>
      /// <param name="fields">If not empty, add just these fields (names, or partition numbers) in this order, instead of the partition: this is how a group re-orders a table.</param>
      public void AddChildrenFromTable(ViewPort viewPort, Selection selection, ITableRun table, int index, SplitterArrayElementViewModel header, TableGroupViewModel helperGroup, int splitPortion = -1, IReadOnlyList<string> fields = null) {
         var itemAddress = table.Start + table.ElementLength * index;
         var originalItemAddress = itemAddress;

         foreach (var (item, address) in SelectFields(viewPort, table, itemAddress, splitPortion, fields)) {
            itemAddress = address;
            IArrayElementViewModel viewModel = null;
            if (item.Type == ElementContentType.Unknown) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, HexFieldStrategy.Instance);
            else if (item.Type == ElementContentType.PCS) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new TextFieldStrategy());
            else if (item.Type == ElementContentType.Pointer) viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new AddressFieldStrategy());
            else if (item.Type == ElementContentType.BitArray) viewModel = new BitListArrayElementViewModel(viewPort, item.Name, itemAddress);
            else if (item.Type == ElementContentType.Integer) {
               if (item is ArrayRunEnumSegment enumSegment) {
                  viewModel = new ComboBoxArrayElementViewModel(viewPort, selection, item.Name, itemAddress, item.Length);
                  var anchor = viewPort.Model.GetAnchorFromAddress(-1, table.Start);
                  var enumSourceTableStart = viewPort.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, enumSegment.EnumName);
                  if (!string.IsNullOrEmpty(anchor)) {
                     var dependentArrays = viewPort.Model.GetDependantArrays(anchor).ToList();
                     if (dependentArrays.Count == 1 && enumSourceTableStart >= 0 && dependentArrays[0].ElementContent[0] is ArrayRunBitArraySegment) {
                        Add(viewModel);
                        viewModel = new BitListArrayElementViewModel(viewPort, item.Name, itemAddress);
                     }
                  }
               } else if (item is ArrayRunTupleSegment tupleItem) {
                  viewModel = new TupleArrayElementViewModel(viewPort, tupleItem, itemAddress);
               } else if (item is ArrayRunHexSegment) {
                  viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, HexFieldStrategy.Instance);
               } else if (item is ArrayRunColorSegment) {
                  viewModel = new ColorFieldArrayElementViewModel(viewPort, item.Name, itemAddress);
               } else if (item is ArrayRunCalculatedSegment calcSeg) {
                  viewModel = new CalculatedElementViewModel(viewPort, calcSeg, originalItemAddress);
               } else if (item is ArrayRunPythonButtonSegment pythonButton) {
                  viewModel = new PythonButtonElementViewModel(viewPort, pythonButton, originalItemAddress);
               } else if (item is ArrayRunOffsetRenderSegment renderSeg) {
                  viewModel = new OffsetRenderViewModel(viewPort, renderSeg, itemAddress);
               } else if (item is ArrayRunSignedSegment signedSegment) {
                  viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, SignedFieldStrategy.Instance);
               } else {
                  viewModel = new FieldArrayElementViewModel(viewPort, item.Name, itemAddress, item.Length, new NumericFieldStrategy());
               }
            } else {
               throw new NotImplementedException();
            }
            if (!item.IsUnused() && viewModel is not null) {
               Add(viewModel);
               helperGroup.AddChildrenFromPointerSegment(viewPort, itemAddress, item, viewModel, header, recursionLevel: 0);
               if (PokemonAnimationPreviewViewModel.IsFramesField(viewPort.Model, table, item)) Add(PokemonAnimationPreviewViewModel.Create(viewPort, table, index, itemAddress));
            }
         }
         AddAdhocSpecialElementsToGroup(viewPort, table);
      }

      /// <summary>
      /// Works out which fields of one table element to show, with the address of each, in the order to show them.
      /// By default that is memory order, limited to one partition of the table. With a field list, it is the order of the list.
      /// </summary>
      public static IReadOnlyList<(ArrayRunElementSegment item, int address)> SelectFields(ViewPort viewPort, ITableRun table, int elementAddress, int splitPortion, IReadOnlyList<string> fields) {
         // first, where does every field live? ('|' splitters take no space: they only start the next partition)
         var layout = new List<(ArrayRunElementSegment item, int address, int partition)>();
         var itemAddress = elementAddress;
         var currentPartition = 0;
         foreach (var itemSegment in table.ElementContent) {
            var item = itemSegment;
            if (item is ArrayRunRecordSegment recordItem) item = recordItem.CreateConcrete(viewPort.Model, table, itemAddress);
            if (itemSegment is ArrayRunSplitterSegment) {
               currentPartition += 1;
               continue;
            }
            layout.Add((item, itemAddress, currentPartition));
            itemAddress += item.Length;
         }

         var result = new List<(ArrayRunElementSegment item, int address)>();
         if (fields == null || fields.Count == 0) {
            foreach (var entry in layout) {
               if (splitPortion != -1 && splitPortion != entry.partition) continue;
               result.Add((entry.item, entry.address));
            }
            return result;
         }

         var used = new HashSet<int>(); // indexes into layout: a field is only shown once, even if the list mentions it twice
         foreach (var field in fields) {
            var isPartition = int.TryParse(field, out var partition);
            for (int i = 0; i < layout.Count; i++) {
               var matches = isPartition ? layout[i].partition == partition : layout[i].item.Name.Equals(field, StringComparison.OrdinalIgnoreCase);
               if (matches && used.Add(i)) result.Add((layout[i].item, layout[i].address));
            }
         }
         return result;
      }

      private void AddAdhocSpecialElementsToGroup(IViewPort viewPort, ITableRun table) {
         if (!viewPort.Model.IsEmerald() && table.ElementContent.Count == 14 && table.ElementCount == 1 && table.ElementContent.Select(seg => seg.Type).ToArray().SequenceEqual(new[] {
            ElementContentType.Pointer, ElementContentType.Pointer, ElementContentType.Pointer, ElementContentType.Pointer,
            ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer,
            ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer,
            ElementContentType.Integer, ElementContentType.Integer,
         })) {
            AddAdhocMapElementsToGroup(viewPort, table);
         }

         if (viewPort.Model.IsEmerald() && table.ElementContent.Count == 13 && table.ElementCount == 1 && table.ElementContent.Select(seg => seg.Type).ToArray().SequenceEqual(new[] {
            ElementContentType.Pointer, ElementContentType.Pointer, ElementContentType.Pointer, ElementContentType.Pointer,
            ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer,
            ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer, ElementContentType.Integer,
            ElementContentType.Integer,
         })) {
            AddAdhocMapElementsToGroup(viewPort, table);
         }
      }

      private void AddAdhocMapElementsToGroup(IViewPort viewPort, ITableRun table) {
         var model = viewPort.Model;
         if (table.PointerSources == null || table.PointerSources.Count != 1) return;
         var addressInBankTable = table.PointerSources[0];
         var bankTable = model.GetNextRun(addressInBankTable) as ITableRun;
         if (bankTable == null || bankTable.PointerSources == null || bankTable.PointerSources.Count != 1) return;
         var topTable = model.GetNextRun(bankTable.PointerSources[0]) as ITableRun;
         if (topTable == null) return;
         var bankOffset = topTable.ConvertByteOffsetToArrayOffset(bankTable.PointerSources[0]);
         var mapOffset = bankTable.ConvertByteOffsetToArrayOffset(addressInBankTable);
         var (bank, map) = (bankOffset.ElementIndex, mapOffset.ElementIndex);
         var name = "maps.bank" + bank + BlockMapViewModel.MapIDToText(model, bank, map);
         var matches = model.GetMatchingMaps(name);
         if (matches.Count != 1) return;
         var mapModel = new MapModel(new ModelArrayElement(model, table.Start, 0, () => viewPort.ChangeHistory.CurrentChange, table));
         if (model.GetNextRun(mapModel.Layout.BlockMap.Start) is BlockmapRun blockmapRun) {
            var image = (CanvasPixelViewModel)model.CurrentCacheScope.GetImage(blockmapRun);
            image.SpriteScale = 128.0 / Math.Max(image.PixelWidth, image.PixelHeight);
            Add(new SpriteIndicatorElementViewModel(image));
         }
         Add(new ButtonArrayElementViewModel("Edit Map", () => viewPort.Goto.Execute(name)));
      }

      /// <summary>
      /// Most streams are edited as text. A list of encounters (min level, max level, species) gets a row of boxes per element instead.
      /// </summary>
      private static TextStreamElementViewModel CreateTextStream(ViewPort viewPort, IFormattedRun streamRun, string name, int address, string format, string areaName = null) {
         if (streamRun is ITableRun table && TableRowsStreamElementViewModel.Supports(table)) return new TableRowsStreamElementViewModel(viewPort, name, address, format, areaName);
         return new TextStreamElementViewModel(viewPort, name, address, format);
      }

      /// <summary>
      /// A list of encounters is reached through a field of the map's table ("morningGrass", "dayFish", ...): that name says what kind of encounters the list holds.
      /// </summary>
      private static string GetAreaName(IArrayElementViewModel parent) {
         if (parent is StreamElementViewModel stream) return stream.ParentName;
         if (parent is IMultiEnabledArrayElementViewModel field) return field.Name;
         return string.Empty;
      }

      private void AddChildrenFromPointerSegment(ViewPort viewPort, int itemAddress, ArrayRunElementSegment item, IArrayElementViewModel parent, SplitterArrayElementViewModel header, int recursionLevel) {
         if (!(item is ArrayRunPointerSegment pointerSegment)) return;
         if (pointerSegment.InnerFormat == string.Empty) return;
         var destination = viewPort.Model.ReadPointer(itemAddress);
         IFormattedRun streamRun = null;
         if (destination != Pointer.NULL) {
            streamRun = viewPort.Model.GetNextRun(destination);
            if (!pointerSegment.DestinationDataMatchesPointerFormat(viewPort.Model, new NoDataChangeDeltaModel(), itemAddress, destination, null, -1)) streamRun = null;
            if (streamRun != null && streamRun.Start != destination) {
               // For some reason (possibly because of a run length conflict),
               //    the destination data appears to match the expected type,
               //    but there is no run for it.
               // Go ahead and generate a new temporary run for the data.
               var strategy = viewPort.Model.FormatRunFactory.GetStrategy(pointerSegment.InnerFormat);
               strategy.TryParseData(viewPort.Model, string.Empty, destination, ref streamRun);
            }
         }

         // Pick the most specific view model first. LZ sprites are also IStreamRuns, and building a text view model for one
         // serializes the whole compressed image as text before it gets thrown away: with a dozen sprites per table element
         // (decomp-style species structs) that was most of the time spent switching between elements.
         IStreamArrayElementViewModel streamElement = null;
         var parentStart = parent is StreamElementViewModel streamParent ? streamParent.Start : -1;
         if (streamRun is ITrainerTeamRun tptRun) streamElement = new TrainerPokemonTeamElementViewModel(viewPort, tptRun, item.Name, itemAddress);
         else if (streamRun is IPaletteRun paletteRun) streamElement = new PaletteElementViewModel(viewPort, viewPort.ChangeHistory, item.Name, paletteRun.FormatString, paletteRun.PaletteFormat, itemAddress);
         else if (streamRun is ISpriteRun spriteRun) streamElement = new SpriteElementViewModel(viewPort, item.Name, spriteRun.FormatString, spriteRun.SpriteFormat, itemAddress);
         else if (streamRun == null || streamRun is IStreamRun || streamRun is ITableRun) streamElement = CreateTextStream(viewPort, streamRun, item.Name, itemAddress, pointerSegment.InnerFormat, GetAreaName(parent));
         if (streamElement == null) return;
         streamElement.Parent = header;

         var streamAddress = itemAddress;
         var myIndex = currentMember;
         parent.DataChanged += (sender, e) => {
            var closure_destination = viewPort.Model.ReadPointer(streamAddress);
            var run = viewPort.Model.GetNextRun(closure_destination) as IStreamRun;
            IStreamArrayElementViewModel newStream = null;

            var parentStart = parent is StreamElementViewModel streamParent ? streamParent.Start : -1;
            if (run is IPaletteRun paletteRun1) newStream = new PaletteElementViewModel(viewPort, viewPort.ChangeHistory, item.Name, paletteRun1.FormatString, paletteRun1.PaletteFormat, streamAddress);
            else if (run is ISpriteRun spriteRun1) newStream = new SpriteElementViewModel(viewPort, item.Name, spriteRun1.FormatString, spriteRun1.SpriteFormat, streamAddress);
            else if (run == null || run is IStreamRun) newStream = CreateTextStream(viewPort, run, item.Name, streamAddress, pointerSegment.InnerFormat, GetAreaName(parent));

            ForwardModelChanged(newStream);
            ForwardModelDataMoved(newStream);
            // using var scope = Members[myIndex].SilencePropertyNotifications();
            if (myIndex >= Members.Count) return; // the group has been rebuilt with fewer members since
            if (!Members[myIndex].TryCopy(newStream)) Members[myIndex] = newStream;
         };
         ForwardModelDataMoved(streamElement);
         Add(streamElement);

         if (streamRun is ITableRun tableRun && recursionLevel < 1) {
            int segmentOffset = 0;
            for (int i = 0; i < tableRun.ElementContent.Count; i++) {
               if (!(tableRun.ElementContent[i] is ArrayRunPointerSegment)) { segmentOffset += tableRun.ElementContent[i].Length; continue; }
               for (int j = 0; j < tableRun.ElementCount; j++) {
                  itemAddress = tableRun.Start + segmentOffset + j * tableRun.ElementLength;
                  AddChildrenFromPointerSegment(viewPort, itemAddress, tableRun.ElementContent[i], streamElement, header, recursionLevel + 1);
               }
               segmentOffset += tableRun.ElementContent[i].Length;
            }
         }
      }
   }
}
