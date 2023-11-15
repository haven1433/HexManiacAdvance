using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   ///// <summary>
   ///// Sometimes notifying after every change is too noisy.
   ///// Custom <see cref="INotifyCollectionChanged"/> implementation that allows delayed notifications.
   ///// </summary>
   //public class ObservableList<T> : List<T>, INotifyCollectionChanged {
   //   public event NotifyCollectionChangedEventHandler? CollectionChanged;

   //   public void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
   //   public void RaiseRefresh() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
   //}

   public class TableGroupViewModel : ViewModelCore {
      public const string DefaultName = "Other";

      private bool isOpen;
      private int currentMember; // used with open/close when refreshing the collection

      private string groupName;
      public bool DisplayHeader => GroupName != DefaultName;
      public string GroupName { get => groupName; set => Set(ref groupName, value, old => NotifyPropertyChanged(nameof(DisplayHeader))); }

      public ObservableCollection<IArrayElementViewModel> Members { get; } = new();

      public Action<IStreamArrayElementViewModel> ForwardModelChanged { get; init; }
      public Action<IStreamArrayElementViewModel> ForwardModelDataMoved { get; init; }

      public TableGroupViewModel() { GroupName = DefaultName; }

      public bool IsOpen => isOpen;

      public void Open() {
         if (isOpen) return;
         currentMember = 0;
         isOpen = true;
      }

      public void Add(IArrayElementViewModel child, string theme = null) {
         child.Theme = theme;
         if (currentMember == Members.Count) {
            Members.Add(child);
         } else if (!Members[currentMember].TryCopy(child)) {
            Members[currentMember] = child;
         } else {
            Members[currentMember].Theme = child.Theme;
         }
         currentMember += 1;
      }

      public void Close() {
         if (!isOpen) return;
         while (Members.Count > currentMember) Members.RemoveAt(Members.Count - 1);
         isOpen = false;
         // Members.RaiseRefresh();
      }

      public void AddChildrenFromTable(ViewPort viewPort, Selection selection, ITableRun table, int index, string theme, SplitterArrayElementViewModel header, TableGroupViewModel helperGroup, int splitPortion = -1) {
         var itemAddress = table.Start + table.ElementLength * index;
         var originalItemAddress = itemAddress;
         var currentPartition = 0;
         foreach (var itemSegment in table.ElementContent) {
            var item = itemSegment;
            if (item is ArrayRunRecordSegment recordItem) item = recordItem.CreateConcrete(viewPort.Model, table, itemAddress);

            if (itemSegment is ArrayRunSplitterSegment) {
               currentPartition += 1;
               continue;
            } else if (splitPortion != -1 && splitPortion != currentPartition) {
               itemAddress += item.Length;
               continue;
            }

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
            if (!item.IsUnused()) {
               Add(viewModel, theme);
               helperGroup.AddChildrenFromPointerSegment(viewPort, theme, itemAddress, item, viewModel, header, recursionLevel: 0);
            }
            itemAddress += item.Length;
         }
         AddAdhocSpecialElementsToGroup(viewPort, table);
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

      private void AddChildrenFromPointerSegment(ViewPort viewPort, string theme, int itemAddress, ArrayRunElementSegment item, IArrayElementViewModel parent, SplitterArrayElementViewModel header, int recursionLevel) {
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

         IStreamArrayElementViewModel streamElement = null;
         if (streamRun == null || streamRun is IStreamRun || streamRun is ITableRun) streamElement = new TextStreamElementViewModel(viewPort, item.Name, itemAddress, pointerSegment.InnerFormat);
         var parentStart = parent is StreamElementViewModel streamParent ? streamParent.Start : -1;
         if (streamRun is ISpriteRun spriteRun) streamElement = new SpriteElementViewModel(viewPort, item.Name, spriteRun.FormatString, spriteRun.SpriteFormat, itemAddress);
         if (streamRun is IPaletteRun paletteRun) streamElement = new PaletteElementViewModel(viewPort, viewPort.ChangeHistory, item.Name, paletteRun.FormatString, paletteRun.PaletteFormat, itemAddress);
         if (streamRun is TrainerPokemonTeamRun tptRun) streamElement = new TrainerPokemonTeamElementViewModel(viewPort, tptRun, item.Name, itemAddress);
         if (streamElement == null) return;
         streamElement.Parent = header;

         var streamAddress = itemAddress;
         var myIndex = currentMember;
         parent.DataChanged += (sender, e) => {
            var closure_destination = viewPort.Model.ReadPointer(streamAddress);
            var run = viewPort.Model.GetNextRun(closure_destination) as IStreamRun;
            IStreamArrayElementViewModel newStream = null;

            if (run == null || run is IStreamRun) newStream = new TextStreamElementViewModel(viewPort, item.Name, streamAddress, pointerSegment.InnerFormat);
            var parentStart = parent is StreamElementViewModel streamParent ? streamParent.Start : -1;
            if (run is ISpriteRun spriteRun1) newStream = new SpriteElementViewModel(viewPort, item.Name, spriteRun1.FormatString, spriteRun1.SpriteFormat, streamAddress);
            if (run is IPaletteRun paletteRun1) newStream = new PaletteElementViewModel(viewPort, viewPort.ChangeHistory, item.Name, paletteRun1.FormatString, paletteRun1.PaletteFormat, streamAddress);

            ForwardModelChanged(newStream);
            ForwardModelDataMoved(newStream);
            if (!Members[myIndex].TryCopy(newStream)) Members[myIndex] = newStream;
         };
         ForwardModelDataMoved(streamElement);
         Add(streamElement, theme);

         if (streamRun is ITableRun tableRun && recursionLevel < 1) {
            int segmentOffset = 0;
            for (int i = 0; i < tableRun.ElementContent.Count; i++) {
               if (!(tableRun.ElementContent[i] is ArrayRunPointerSegment)) { segmentOffset += tableRun.ElementContent[i].Length; continue; }
               for (int j = 0; j < tableRun.ElementCount; j++) {
                  itemAddress = tableRun.Start + segmentOffset + j * tableRun.ElementLength;
                  AddChildrenFromPointerSegment(viewPort, theme, itemAddress, tableRun.ElementContent[i], streamElement, header, recursionLevel + 1);
               }
               segmentOffset += tableRun.ElementContent[i].Length;
            }
         }
      }
   }
}
