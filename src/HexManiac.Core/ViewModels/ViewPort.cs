using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.BaseRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PointerRun;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : ViewModelCore, IEditableViewPort, IRaiseMessageTab {
      public const string AllHexCharacters = "0123456789ABCDEFabcdef";
      public const char GotoMarker = '@';
      public const char DirectiveMarker = '.'; // for things like .thumb, .align, etc. Directives always start with a single dot and contain no further dots until they contain a space.
      public const char CommandMarker = '!'; // commands are meta, so they also start with the goto marker.
      public const char CommentStart = '#';

      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      private readonly StubCommand
         undoWrapper = new StubCommand(),
         redoWrapper = new StubCommand(),
         clear = new StubCommand(),
         selectAll = new StubCommand(),
         copy = new StubCommand(),
         copyAddress = new StubCommand(),
         copyBytes = new StubCommand(),
         deepCopy = new StubCommand(),
         isText = new StubCommand();

      private readonly object threadlock = new();

      public Singletons Singletons { get; }

      public PythonTool PythonTool { get; }

      private HexElement[,] currentView;
      private bool exitEditEarly, withinComment, skipToNextGameCode;

      public string Name {
         get {
            var name = Path.GetFileNameWithoutExtension(FileName);
            if (string.IsNullOrEmpty(name)) name = "Untitled";
            if (history.HasDataChange) name += "*";
            return name;
         }
      }

      public double ToolPanelWidth { get; set; } = 500;

      private string fileName;
      public string FileName {
         get => fileName;
         private set {
            if (TryUpdate(ref fileName, value) && !string.IsNullOrEmpty(fileName)) {
               FullFileName = Path.GetFullPath(fileName);
               NotifyPropertyChanged(nameof(Name));
            }
         }
      }

      private string fullFileName;
      public string FullFileName { get => fullFileName; private set => TryUpdate(ref fullFileName, value); }

      #region Scrolling Properties

      private readonly ScrollRegion scroll;

      public event EventHandler PreviewScrollChanged;

      public int Width {
         get => scroll.Width;
         set {
            using (ModelCacheScope.CreateScope(Model)) selection.ChangeWidth(value);
         }
      }

      public int Height {
         get => scroll.Height;
         set => scroll.Height = value;
      }

      public int MinimumScroll => scroll.MinimumScroll;

      public int ScrollValue {
         get => scroll.ScrollValue;
         set {
            PreviewScrollChanged?.Invoke(this, EventArgs.Empty);
            using (ModelCacheScope.CreateScope(Model)) scroll.ScrollValue = value;
         }
      }

      public int MaximumScroll => scroll.MaximumScroll;

      public ObservableCollection<string> Headers => scroll.Headers;
      public ObservableCollection<HeaderRow> ColumnHeaders { get; }
      public int DataOffset => scroll.DataIndex;
      public ICommand Scroll => scroll.Scroll;

      public bool UseCustomHeaders {
         get => scroll.UseCustomHeaders;
         set { using (ModelCacheScope.CreateScope(Model)) scroll.UseCustomHeaders = value; }
      }

      public bool AllowSingleTableMode {
         get => scroll.AllowSingleTableMode;
         set => scroll.AllowSingleTableMode = value;
      }

      private void ScrollPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(scroll.DataIndex)) {
            ClearActiveEditBeforeSelectionChanges(selection, selection.SelectionStart);
            RefreshBackingData();
            if (e is ExtendedPropertyChangedEventArgs<int> ex) {
               var previous = ex.OldValue;
               if (Math.Abs(scroll.DataIndex - previous) % Width != 0) UpdateColumnHeaders();
            }
         } else if (e.PropertyName != nameof(scroll.DataLength)) {
            NotifyPropertyChanged(e);
         }

         if (e.PropertyName == nameof(Width) || e.PropertyName == nameof(Height) || e.PropertyName == nameof(scroll.AllowSingleTableMode)) {
            dispatcher.DispatchWork(RefreshBackingData);
         }

         if (e.PropertyName == nameof(Width)) {
            UpdateColumnHeaders();
            NotifyPropertyChanged(nameof(ScrollValue)); // changing the Scroll's Width can mess with the ScrollValue: go ahead and notify
         }
         if (e.PropertyName == nameof(scroll.MinimumScroll)) NotifyPropertyChanged(nameof(MinimumScroll));
         if (e.PropertyName == nameof(scroll.MaximumScroll)) NotifyPropertyChanged(nameof(MaximumScroll));
         if (e.PropertyName == nameof(scroll.AllowSingleTableMode)) NotifyPropertyChanged(nameof(AllowSingleTableMode));
         if (e.PropertyName == nameof(scroll.DataLength)) dispatcher.DispatchWork(RefreshBackingData);
      }

      #endregion

      #region Selection Properties

      private readonly Selection selection;

      private bool stretchData;
      public bool StretchData { get => stretchData; set => Set(ref stretchData, value); }
      public bool AutoAdjustDataWidth { get => selection.AutoAdjustDataWidth; set => selection.AutoAdjustDataWidth = value; }
      public bool AllowMultipleElementsPerLine { get => selection.AllowMultipleElementsPerLine; set => selection.AllowMultipleElementsPerLine = value; }

      public Point SelectionStart {
         get => selection.SelectionStart;
         set => selection.SelectionStart = value;
      }

      public Point SelectionEnd {
         get => selection.SelectionEnd;
         set => selection.SelectionEnd = value;
      }

      public int PreferredWidth {
         get => selection.PreferredWidth;
         set => selection.PreferredWidth = value;
      }
      private readonly StubCommand moveSelectionStart = new StubCommand(),
         moveSelectionEnd = new StubCommand();
      public ICommand MoveSelectionStart => moveSelectionStart;
      public ICommand MoveSelectionEnd => moveSelectionEnd;
      public ICommand Goto => StubCommand<object>(ref gotoCommand, ExecuteGoto, selection.Goto.CanExecute);
      public ICommand Back => selection.Back;
      public ICommand Forward => selection.Forward;
      public ICommand ResetAlignment => selection.ResetAlignment;
      public ICommand SelectAll => selectAll;

      private StubCommand gotoCommand;
      private void ExecuteGoto(object arg) {
         Model.InitializationWorkload.ContinueWith(task => {
            // This needs to be synchronous to make it deterministic,
            // but needs to happen on the UI thread since it can update bound properties.
            dispatcher.BlockOnUIWork(() => {
               if (arg is string str) {
                  var possibleMatches = Model.GetExtendedAutocompleteOptions(str);
                  if (possibleMatches.Count == 1) str = possibleMatches[0];
                  else if (possibleMatches.Count > 1 && possibleMatches.All(match => Model.GetMatchedWords(match).Any())) str = possibleMatches[0];
                  var words = Model.GetMatchedWords(str).Where(word => Model.GetNextRun(word).Length < 3).ToList();
                  if (words.Count == 1) {
                     selection.Goto.Execute(words[0]);
                     return;
                  } else if (words.Count > 1) {
                     OpenSearchResultsTab(str, words.Select(word => (word, word)).ToList());
                     return;
                  }
               }

               selection.Goto.Execute(arg);
            });
         }, TaskContinuationOptions.ExecuteSynchronously);
      }

      private void ClearActiveEditBeforeSelectionChanges(object sender, Point location) {
         if (location.X >= 0 && location.X < scroll.Width && location.Y >= 0 && location.Y < scroll.Height) {
            var element = this[location.X, location.Y];
            var underEdit = element.Format as UnderEdit;
            if (underEdit != null) {
               using (ModelCacheScope.CreateScope(Model)) {
                  if (underEdit.CurrentText == string.Empty) {
                     var index = scroll.ViewPointToDataIndex(location);
                     var operation = new DataClear(Model, history.CurrentChange, index);
                     underEdit.OriginalFormat.Visit(operation, Model[index]);
                     ClearEdits(location);
                  } else {
                     var endEdit = " ";
                     if (underEdit.CurrentText.Count(c => c == StringDelimeter) % 2 == 1) endEdit = StringDelimeter.ToString();
                     var originalFormat = underEdit.OriginalFormat;
                     while (originalFormat is IDataFormatDecorator decorator) originalFormat = decorator.OriginalFormat;
                     if (underEdit.CurrentText.StartsWith(EggMoveRun.GroupStart) && (originalFormat is EggSection || originalFormat is EggItem)) endEdit = EggMoveRun.GroupEnd;
                     currentView[location.X, location.Y] = new HexElement(element.Value, element.Edited, underEdit.Edit(endEdit));
                     if (!TryCompleteEdit(location)) ClearEdits(location);
                  }
               }
            }
         }
      }

      private void SelectionPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(SelectionEnd)) history.ChangeCompleted();
         NotifyPropertyChanged(e.PropertyName);
         var dataIndex = scroll.ViewPointToDataIndex(SelectionStart);
         using (ModelCacheScope.CreateScope(Model)) {
            UpdateToolsFromSelection(dataIndex);
            UpdateSelectedAddress();
            UpdateSelectedBytes();
         }
      }

      private void UpdateToolsFromSelection() => UpdateToolsFromSelection(ConvertViewPointToAddress(SelectionStart));
      public void UpdateToolsFromSelection(int dataIndex) {
         var run = Model.GetNextRun(dataIndex);
         if (run.Start > dataIndex) {
            AnchorTextVisible = false;
            return;
         }

         if (run is PointerRun && string.IsNullOrEmpty(Model.GetAnchorFromAddress(-1, run.Start))) {
            var destination = Model.ReadPointer(run.Start);
            run = Model.GetNextRun(destination);
            dataIndex = destination;
         }

         // if the user explicitly closed the tools, don't auto-open them.
         if (tools != null && tools.SelectedIndex != -1) {
            // if the 'Raw' tool is selected, don't auto-update tool selection.
            if (!(tools.SelectedTool == tools.CodeTool && tools.CodeTool.Mode == CodeMode.Raw)) {
               using (ModelCacheScope.CreateScope(Model)) {
                  // update the tool from pointers too
                  if (run is ISpriteRun spriteRun) {
                     var tool = tools.SpriteTool;
                     if (tool.SpriteAddress != run.Start) {
                        tool.SpriteAddress = run.Start;
                     } else {
                        tool.UpdateSpriteProperties();
                        tool.PaletteAddress = SpriteTool.FindMatchingPalette(Model, spriteRun, tool.PaletteAddress);
                     }
                     tools.SelectedIndex = tools.IndexOf(tools.SpriteTool);
                  } else if (run is IPaletteRun) {
                     tools.SpriteTool.PaletteAddress = run.Start;
                     tools.SelectedIndex = tools.IndexOf(tools.SpriteTool);
                  } else if (run is ITableRun array) {
                     var offsets = array.ConvertByteOffsetToArrayOffset(dataIndex);
                     Tools.StringTool.Address = offsets.SegmentStart - offsets.ElementIndex * array.ElementLength;
                     Tools.TableTool.Address = array.Start + array.ElementLength * offsets.ElementIndex;
                     if (!(run is IStreamRun || array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) || tools.SelectedTool != tools.StringTool) {
                        tools.SelectedIndex = tools.IndexOf(tools.TableTool);
                     }
                  } else if (run is IStreamRun) {
                     Tools.StringTool.Address = run.Start;
                     tools.SelectedIndex = tools.IndexOf(tools.StringTool);
                  } else if (run is IScriptStartRun) {
                     tools.SelectedIndex = tools.IndexOf(tools.CodeTool);
                     if (run is XSERun) tools.CodeTool.Mode = CodeMode.Script;
                     if (run is BSERun) tools.CodeTool.Mode = CodeMode.BattleScript;
                     if (run is ASERun) tools.CodeTool.Mode = CodeMode.AnimationScript;
                  } else {
                     // not a special run, so don't update tools
                  }
               }
            }
         }

         UpdateAnchorText(dataIndex);

         RequestMenuClose?.Invoke(this, EventArgs.Empty);
      }

      private void UpdateAnchorText(int dataIndex) {
         var run = Model.GetNextRun(dataIndex);

         if (this[SelectionStart].Format is Anchor anchor) {
            // there is an anchor exactly here: show that format
            TryUpdate(ref anchorText, AnchorStart + anchor.Name + anchor.Format, nameof(AnchorText));
            AnchorTextVisible = true;
         } else if (run.Start <= dataIndex && run.PointerSources != null) {
            // there is an anchor attached to this run: show that format
            var name = Model.GetAnchorFromAddress(-1, run.Start);
            TryUpdate(ref anchorText, AnchorStart + name + run.FormatString, nameof(AnchorText));
            AnchorTextVisible = true;
         } else {
            AnchorTextVisible = false;
         }
      }

      #region Selected Address/ElementName/Length, bottom row

      private string selectedAddress, selectedLength, selectedElementName;
      public string SelectedAddress {
         get => selectedAddress;
         set => Set(ref selectedAddress, value, SelectedAddressChanged);
      }
      public string SelectedLength {
         get => selectedLength;
         set => Set(ref selectedLength, value, SelectedLengthChanged);
      }

      private bool base10SelectionLength;
      public bool Base10SelectionLength {
         get => base10SelectionLength;
         set => Set(ref base10SelectionLength, value, arg => UpdateSelectedAddress());
      }

      public string SelectedElementName => selectedElementName;

      private void UpdateSelectedAddress() {
         var dataIndex1 = scroll.ViewPointToDataIndex(SelectionStart);
         var dataIndex2 = scroll.ViewPointToDataIndex(SelectionEnd);
         var left = Math.Min(dataIndex1, dataIndex2);
         Set(ref selectedAddress, left.ToAddress(), nameof(SelectedAddress));

         var elementName = BuildElementName(Model, left);
         Set(ref selectedElementName, elementName ?? string.Empty, nameof(SelectedElementName));

         int length = Math.Abs(dataIndex1 - dataIndex2) + 1;
         var lengthText = base10SelectionLength ? length.ToString() : length.ToString("X1");
         Set(ref selectedLength, lengthText, nameof(SelectedLength));
      }

      private void SelectedAddressChanged(string old) {
         if (!selectedAddress.TryParseHex(out int address)) return;
         SelectionStart = ConvertAddressToViewPoint(address);
      }

      private void SelectedLengthChanged(string old) {
         int length;
         if (base10SelectionLength) {
            if (!int.TryParse(selectedLength, out length)) return;
         } else {
            if (!selectedLength.TryParseHex(out length)) return;
         }
         var left = Math.Min(ConvertViewPointToAddress(SelectionStart), ConvertViewPointToAddress(SelectionEnd));
         SelectionStart = ConvertAddressToViewPoint(left);
         SelectionEnd = ConvertAddressToViewPoint(left + length - 1);
      }

      #endregion

      public static string BuildElementName(IDataModel model, int address) {
         var run = model.GetNextRun(address);
         if (run is ITableRun array1 && array1.Start <= address) {
            var index = array1.ConvertByteOffsetToArrayOffset(address).ElementIndex;
            var basename = model.GetAnchorFromAddress(-1, array1.Start);
            if (array1.ElementNames.Count > index) {
               return $"{basename}/{array1.ElementNames[index]}";
            } else {
               return $"{basename}/{index}";
            }
         } else if (run.PointerSources != null && run.PointerSources.Count > 0 && string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, run.Start))) {
            var sourceRun = model.GetNextRun(run.PointerSources[0]);
            if (sourceRun is ITableRun array2) {
               // we are an anchor that's pointed to from an array
               var offset = array2.ConvertByteOffsetToArrayOffset(run.PointerSources[0]);
               var index = offset.ElementIndex;
               if (index >= 0) {
                  var segment = array2.ElementContent[offset.SegmentIndex];
                  var basename = model.GetAnchorFromAddress(-1, array2.Start);
                  if (array2.ElementNames.Count > index) {
                     return $"{basename}/{array2.ElementNames[index]}/{segment.Name}";
                  } else {
                     return $"{basename}/{index}/{segment.Name}";
                  }
               }
            }
         }

         return string.Empty;
      }

      private string selectedBytes;
      public string SelectedBytes {
         get {
            if (selectedBytes != null) return selectedBytes;

            var bytes = GetSelectedByteContents(0x10);
            selectedBytes = "Selected Bytes: " + bytes;
            return selectedBytes;
         }
         private set => TryUpdate(ref selectedBytes, value);
      }

      // update the selected bytes lazily. Most of the time we don't really care about the new value.
      private void UpdateSelectedBytes() => SelectedBytes = null;

      private string GetSelectedByteContents(int maxByteCount = int.MaxValue) {
         var dataIndex1 = scroll.ViewPointToDataIndex(SelectionStart);
         var dataIndex2 = scroll.ViewPointToDataIndex(SelectionEnd);
         var left = Math.Min(dataIndex1, dataIndex2);
         var length = Math.Abs(dataIndex1 - dataIndex2) + 1;
         if (left < 0) { length += left; left = 0; }
         if (left + length > Model.Count) length = Model.Count - left;
         var result = new StringBuilder();
         for (int i = 0; i < length && i < maxByteCount; i++) {
            var token = Model[left + i].ToHexString();
            result.Append(token);
            result.Append(" ");
         }
         if (maxByteCount < length) result.Append("...");
         return result.ToString();
      }

      private void SelectAllExecuted() {
         Goto.Execute(0);
         SelectionStart = new Point(0, 0);
         SelectionEnd = scroll.DataIndexToViewPoint(Model.Count - 1);
      }

      #endregion

      #region Undo / Redo

      private readonly ChangeHistory<ModelDelta> history;

      public ChangeHistory<ModelDelta> ChangeHistory => history;

      public ModelDelta CurrentChange => history.CurrentChange;

      public ICommand Undo => undoWrapper;

      public ICommand Redo => redoWrapper;

      private ModelDelta RevertChanges(ModelDelta changes) {
         var reverse = changes.Revert(Model);
         RefreshBackingData();
         scroll.UpdateHeaders();
         return reverse;
      }

      private void HistoryPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(history.IsSaved)) {
            save.RaiseCanExecuteChanged();
            exportBackup.RaiseCanExecuteChanged();
            if (history.IsSaved) { Model.ResetChanges(); RefreshBackingData(); }
            NotifyPropertyChanged(nameof(IsMetadataOnlyChange));
         }

         if (e.PropertyName == nameof(history.HasDataChange)) {
            NotifyPropertyChanged(nameof(IsMetadataOnlyChange));
            NotifyPropertyChanged(nameof(Name));
         }
      }

      #endregion

      #region Saving

      private readonly StubCommand
         save = new StubCommand(),
         saveAs = new StubCommand(),
         exportBackup = new StubCommand(),
         close = new StubCommand();

      public bool IsMetadataOnlyChange => !history.IsSaved && !ChangeHistory.HasDataChange;
      public ICommand Save => save;

      public ICommand SaveAs => saveAs;

      public ICommand ExportBackup => exportBackup;

      public ICommand Close => close;

      public event EventHandler Closed;

      private void SaveExecuted(IFileSystem fileSystem) {
         if (history.IsSaved) return;

         if (string.IsNullOrEmpty(FileName)) {
            SaveAsExecuted(fileSystem);
            return;
         }

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);
         if (fileSystem.Save(new LoadedFile(FileName, Model.RawData))) {
            fileSystem.SaveMetadata(FileName, metadata?.Serialize());
            history.TagAsSaved();
            Model.ResetChanges();
         }
      }

      private void SaveAsExecuted(IFileSystem fileSystem) {
         var newName = fileSystem.RequestNewName(FileName, "Game Boy Advance", "gba");
         if (newName == null) return;

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);
         if (fileSystem.Save(new LoadedFile(newName, Model.RawData))) {
            FileName = newName; // don't bother notifying, because tagging the history will cause a notify;
            fileSystem.SaveMetadata(FileName, metadata?.Serialize());
            history.TagAsSaved();
            Model.ResetChanges();
            RequestCloseOtherViewports?.Invoke(this, Model);
         }
      }

      private void ExportBackupExecuted(IFileSystem fileSystem) {
         var changeDescription = fileSystem.RequestText("Export Summary", "What was your most recent change?");
         if (changeDescription == null) return;
         changeDescription = new string(changeDescription.Select(letter => char.IsLetterOrDigit(letter) ? letter : '_').ToArray());

         var exportID = Model.NextExportID;
         Model.NextExportID += 1;
         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);
         var fileName = Path.GetFileNameWithoutExtension(FullFileName);
         fileName = fileName.Split("_backup")[0];
         var extension = Path.GetExtension(FullFileName);
         var directory = Path.GetDirectoryName(FullFileName);
         if (!string.IsNullOrEmpty(directory)) directory = directory + Path.DirectorySeparatorChar;

         var exportName = $"{directory}backups{Path.DirectorySeparatorChar}{fileName}_backup{exportID}__{changeDescription}{extension}";
         if (fileSystem.Save(new LoadedFile(exportName, Model.RawData))) {
            fileSystem.SaveMetadata(exportName, metadata?.Serialize());
            fileSystem.SaveMetadata(FileName, metadata?.Serialize());
         }
      }

      private void CloseExecuted(IFileSystem fileSystem) {
         if (!history.IsSaved) {
            var metadata = Model.ExportMetadata(Singletons.MetadataInfo);
            var result = fileSystem.TrySavePrompt(new LoadedFile(FileName, Model.RawData));
            if (result == null) return;
            if (result == true) {
               fileSystem.SaveMetadata(FileName, metadata?.Serialize());
            }
         }
         Closed?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      #region Progress

      private readonly IWorkDispatcher dispatcher;

      private double progress;
      public double Progress { get => progress; set => Set(ref progress, value); }

      private bool updateInProgress;
      public bool UpdateInProgress { get => updateInProgress; set => Set(ref updateInProgress, value); }

      private int initialWorkLoad, postEditWork; // describes the amount of work to complete, measured characters. Allows for a fairly accurate loading bar.
      private readonly List<IDisposable> CurrentProgressScopes = new List<IDisposable>();

      public InlineDispatch UpdateProgress(double value) {
         UpdateInProgress = true;
         Progress = value;
         return new InlineDispatch(dispatcher);
      }

      public void ClearProgress() { UpdateInProgress = false; }

      #endregion

      #region Diff

      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      public event EventHandler<Direction> RequestDiff;

      public int MaxDiffSegmentCount { get; set; }
      public bool HideDiffPointerChanges { get; set; }

      private StubCommand diff, diffLeft, diffRight;
      public ICommand Diff => StubCommand<object>(ref diff, ExecuteDiff);
      public ICommand DiffLeft => StubCommand(ref diffLeft, ExecuteDiffLeft, CanExecuteDiffLeft);
      public ICommand DiffRight => StubCommand(ref diffRight, ExecuteDiffRight, CanExecuteDiffRight);
      private void ExecuteDiff(object data) {
         ViewPort otherTab = data as ViewPort;
         int maxSegments = MaxDiffSegmentCount;
         if (otherTab == null) {
            var resultsTab = new SearchResultsViewPort("Changes");
            int firstResultStart = 0;
            int firstResultLength = 0;
            for (int i = 0; i < Model.Count; i++) {
               if (!Model.HasChanged(i)) continue;
               var length = 1;
               for (int j = i + 1; j < Model.Count; j++) {
                  if (Model.HasChanged(j)) length++;
                  else break;
               }
               resultsTab.Add(CreateChildView(i, i + length - 1), i, i + length - 1);
               if (resultsTab.ResultCount >= maxSegments) break;
               if (firstResultLength == 0) (firstResultStart, firstResultLength) = (i, length);
               i += length;
            }

            var changeCount = resultsTab.ResultCount;
            var changeCountText = changeCount.ToString();
            if (changeCount >= maxSegments) changeCountText += "+";
            RaiseMessage($"{changeCountText} changes found.");
            if (changeCount == 1) {
               Goto.Execute(firstResultStart);
               SelectionEnd = ConvertAddressToViewPoint(firstResultStart + firstResultLength - 1);
            } else if (changeCount > 1) {
               RequestTabChange?.Invoke(this, resultsTab);
            }
         } else if (otherTab is IEditableViewPort otherViewPort) {
            IDataModel modelA = Model, modelB = otherViewPort.Model;
            if (modelA.Count != modelB.Count) {
               RaiseError("Cannot diff files of different length.");
               return;
            }
            var resultsTabA = new List<IChildViewPort>();
            var resultsTabB = new List<IChildViewPort>();
            for (int i = 0; i < modelA.Count && i < modelB.Count; i++) {
               if (modelA[i] == modelB[i]) continue;
               var lastDiff = i;
               for (int j = i + 1; j < Model.Count && j < otherViewPort.Model.Count; j++) {
                  if (modelA[j] != modelB[j]) lastDiff = j;
                  if (lastDiff == j - 4) break;
               }
               if (HideDiffPointerChanges && IsDiffPointerChange(i, lastDiff, modelB)) continue;
               resultsTabA.Add(CreateChildView(i, lastDiff));
               resultsTabB.Add(otherViewPort.CreateChildView(i, lastDiff));
               i = lastDiff + 1;
               if (resultsTabA.Count >= maxSegments) break;
            }
            var diffTab = new DiffViewPort(resultsTabA, resultsTabB) { Height = Height };
            var changeCount = resultsTabA.Count;
            var changeCountText = changeCount.ToString();
            if (changeCount >= maxSegments) changeCountText += "+";
            RaiseMessage($"{changeCountText} changes found.");
            if (changeCount > 0) {
               RequestTabChange?.Invoke(this, diffTab);
            }
         } else {
            throw new NotImplementedException();
         }
      }

      private bool IsDiffPointerChange(int start, int end, IDataModel other) {
         while (start % 4 != 0) start--;
         while (end % 4 != 3) end++;
         if (end - start != 3) return false;
         if (Model.GetNextRun(start) is PointerRun pRun1 && pRun1.Start == start) return true;
         if (other.GetNextRun(start) is PointerRun pRun2 && pRun2.Start == start) return true;
         return false;
      }

      private void ExecuteDiffLeft() => RequestDiff?.Invoke(this, Direction.Left);
      private bool CanExecuteDiffLeft() {
         var args = new CanDiffEventArgs(Direction.Left);
         RequestCanDiff?.Invoke(this, args);
         return args.Result;
      }

      private void ExecuteDiffRight() => RequestDiff?.Invoke(this, Direction.Right);
      private bool CanExecuteDiffRight() {
         var args = new CanDiffEventArgs(Direction.Right);
         RequestCanDiff?.Invoke(this, args);
         return args.Result;
      }

      public void RefreshTabCommands() {
         diffLeft?.RaiseCanExecuteChanged();
         diffRight?.RaiseCanExecuteChanged();
      }

      #endregion

      #region Duplicate

      public bool CanDuplicate => true;
      public void Duplicate() => OpenInNewTab(scroll.DataIndex);

      #endregion

      #region CreatePatch

      public event EventHandler<CanPatchEventArgs> RequestCanCreatePatch;
      public event EventHandler<CanPatchEventArgs> RequestCreatePatch;

      public bool CanIpsPatchRight {
         get {
            var args = new CanPatchEventArgs(Direction.Right, PatchType.Ips);
            RequestCanCreatePatch?.Invoke(this, args);
            return args.Result;
         }
      }
      public bool CanUpsPatchRight {
         get {
            var args = new CanPatchEventArgs(Direction.Right, PatchType.Ups);
            RequestCanCreatePatch?.Invoke(this, args);
            return args.Result;
         }
      }

      public void IpsPatchRight() => RequestCreatePatch?.Invoke(this, new(Direction.Right, PatchType.Ips));

      public void UpsPatchRight() => RequestCreatePatch?.Invoke(this, new(Direction.Right, PatchType.Ups));

      #endregion

      public int FreeSpaceStart { get => Model.FreeSpaceStart; set {
            if (Model.FreeSpaceStart != value) {
               Model.FreeSpaceStart = value;
               NotifyPropertyChanged();
            }
         }
      }
      private StubCommand gotoFreeSpaceStart;
      public ICommand GotoFreeSpaceStart => StubCommand(ref gotoFreeSpaceStart, () => Goto.Execute(Model.FreeSpaceStart));
      public bool CanFindFreeSpace => true;
      public void FindFreeSpace(IFileSystem fileSystem) {
         var sizeText = fileSystem.RequestText("Free Space Finder", "How many bytes of freespace do you want to find?");
         if (sizeText == null) return;
         if (!int.TryParse(sizeText, out int size)) {
            RaiseError($"Could not parse {sizeText} as a number");
            return;
         }
         if (size < 1) {
            RaiseError("Try a number bigger than zero.");
            return;
         }

         var start = Model.FindFreeSpace(FreeSpaceStart, size);
         Goto.Execute(start);
         SelectionEnd = ConvertAddressToViewPoint(start + size - 1);
      }

      private readonly ToolTray tools;
      public bool HasTools => tools != null;
      public IToolTrayViewModel Tools => tools;

      private bool anchorTextVisible;
      public bool AnchorTextVisible {
         get => anchorTextVisible;
         set => Set(ref anchorTextVisible, value);
      }

      private string anchorText;
      public string AnchorText {
         get => anchorText;
         set {
            if (value == null) value = string.Empty;
            if (!value.StartsWith(AnchorStart.ToString())) value = AnchorStart + value;
            if (TryUpdate(ref anchorText, value)) {
               var index = scroll.ViewPointToDataIndex(SelectionStart);
               var run = Model.GetNextRun(index);
               var hasAnchor = (run.PointerSources != null && run.PointerSources.Count > 0) || !string.IsNullOrEmpty(Model.GetAnchorFromAddress(-1, run.Start));
               if (run is PointerRun pRun && !hasAnchor) {
                  index = Model.ReadPointer(run.Start);
                  run = Model.GetNextRun(index);
               }
               if (run.Start <= index) {
                  var token = new NoDataChangeDeltaModel();

                  // During edits, typing `^` is allowed, and is used as a way to remove an anchor.
                  // When doing AnchorText edits, clearing the name/format when there are no pointers is an error.
                  ErrorInfo errorInfo = ErrorInfo.NoError;
                  if (AnchorText == AnchorStart.ToString() && run.PointerSources.Count == 0) {
                     errorInfo = new ErrorInfo("An anchor with nothing pointing to it must have a name.");
                  }

                  if (errorInfo == ErrorInfo.NoError) {
                     errorInfo = PokemonModel.ApplyAnchor(Model, token, run.Start, AnchorText);
                  }

                  if (errorInfo == ErrorInfo.NoError) {
                     OnError?.Invoke(this, string.Empty);
                     var newRun = Model.GetNextRun(index);
                     if (AnchorText == AnchorStart.ToString()) Model.ClearFormat(token, run.Start, 1);
                     if (newRun is ArrayRun array) {
                        // if the format changed (ignoring length), run a goto to update the display width
                        if (run is ArrayRun array2 && !array.HasSameSegments(array2)) {
                           selection.PropertyChanged -= SelectionPropertyChanged; // to keep from double-updating the AnchorText
                           Goto.Execute(index);
                           selection.PropertyChanged += SelectionPropertyChanged;
                        }
                        UpdateColumnHeaders();
                     }
                     Tools.RefreshContent();
                     RefreshBackingData();
                  } else {
                     OnError?.Invoke(this, errorInfo.ErrorMessage);
                  }
                  if (token.HasAnyChange) history.InsertCustomChange(token);
               }
            }
         }
      }

      private int anchorTextSelectionStart;
      public int AnchorTextSelectionStart { get => anchorTextSelectionStart; set => Set(ref anchorTextSelectionStart, value); }

      private int anchorTextSelectionLength;
      public int AnchorTextSelectionLength { get => anchorTextSelectionLength; set => Set(ref anchorTextSelectionLength, value); }

      private bool isFocused;
      public bool IsFocused { get => isFocused; set => Set(ref isFocused, value); }

      public ICommand Copy => copy;
      public ICommand CopyAddress => copyAddress;
      public ICommand CopyBytes => copyBytes;
      public ICommand DeepCopy => deepCopy;
      public ICommand Clear => clear;
      public ICommand IsText => isText;

      public HexElement this[Point p] => this[p.X, p.Y];

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || x >= Width || x >= currentView.GetLength(0)) return HexElement.Undefined;
            if (y < 0 || y >= Height || y >= currentView.GetLength(1)) return HexElement.Undefined;
            if (currentView[x, y] is object) return currentView[x, y];

            if (x == 0 && y == 0) {
               RefreshBackingDataFull();
               return currentView[x, y];
            }

            using (ModelCacheScope.CreateScope(Model)) {
               RefreshBackingData(new Point(x, y));
            }

            return currentView[x, y];
         }
      }

      public IDataModel Model { get; }

      public bool FormattedDataIsSelected {
         get {
            var (left, right) = (scroll.ViewPointToDataIndex(SelectionStart), scroll.ViewPointToDataIndex(SelectionEnd));
            if (left > right) (left, right) = (right, left);
            var nextRun = Model.GetNextRun(left);
            return nextRun.Start <= right;
         }
      }

#pragma warning disable 0067 // it's ok if events are never used
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<IDataModel> RequestCloseOtherViewports;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
#pragma warning restore 0067

      public Shortcuts Shortcuts { get; }

      #region Constructors

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(string fileName, IDataModel model, IWorkDispatcher dispatcher, Singletons singletons = null, PythonTool pythonTool = null, ChangeHistory<ModelDelta> changeHistory = null) {
         Singletons = singletons ?? new Singletons();
         PythonTool = pythonTool;
         history = changeHistory ?? new ChangeHistory<ModelDelta>(RevertChanges);
         history.PropertyChanged += HistoryPropertyChanged;
         this.dispatcher = dispatcher ?? InstantDispatch.Instance;

         Model = model;
         FileName = fileName;
         ColumnHeaders = new ObservableCollection<HeaderRow>();

         scroll = new ScrollRegion(model.TryGetUsefulHeader) { DataLength = Model.Count };
         scroll.PropertyChanged += ScrollPropertyChanged;

         selection = new Selection(scroll, Model, history, GetSelectionSpan);
         selection.PropertyChanged += SelectionPropertyChanged;
         selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges;
         selection.OnError += (sender, e) => OnError?.Invoke(this, e);

         if (this is not ChildViewPort) { // child viewports don't need tools
            tools = new ToolTray(Singletons, Model, selection, history, this);
            Tools.OnError += (sender, e) => OnError?.Invoke(this, e);
            Tools.OnMessage += (sender, e) => RaiseMessage(e);
            tools.RequestMenuClose += (sender, e) => RequestMenuClose?.Invoke(this, e);
            Tools.StringTool.ModelDataChanged += ModelChangedByTool;
            Tools.StringTool.ModelDataMoved += ModelDataMovedByTool;
            Tools.TableTool.ModelDataChanged += ModelChangedByTool;
            Tools.TableTool.ModelDataMoved += ModelDataMovedByTool;
            Tools.CodeTool.ModelDataChanged += ModelChangedByCodeTool;
            Tools.CodeTool.ModelDataMoved += ModelDataMovedByTool;
            scroll.Scheduler = tools;
         }

         ImplementCommands();
         RefreshBackingData();
         Shortcuts = new Shortcuts(this);

         Model.InitializationWorkload.ContinueWith(task => {
            // if we're sharing history with another viewmodel, our model has already been updated like this.
            if (changeHistory == null) CascadeScripts();
            dispatcher.DispatchWork(() => {
               RefreshBackingData();
               ValidateMatchedWords();
            });
         }, TaskContinuationOptions.ExecuteSynchronously);
      }

      public ViewPort(LoadedFile file) : this(file.Name, new BasicModel(file.Contents), InstantDispatch.Instance) { }

      private void ImplementCommands() {
         undoWrapper.CanExecute = history.Undo.CanExecute;
         undoWrapper.Execute = arg => { history.Undo.Execute(arg); tools.RefreshContent(); };
         history.Undo.CanExecuteChanged += (sender, e) => undoWrapper.CanExecuteChanged.Invoke(undoWrapper, e);

         redoWrapper.CanExecute = history.Redo.CanExecute;
         redoWrapper.Execute = arg => { history.Redo.Execute(arg); tools.RefreshContent(); };
         history.Redo.CanExecuteChanged += (sender, e) => redoWrapper.CanExecuteChanged.Invoke(redoWrapper, e);

         clear.CanExecute = CanAlwaysExecute;
         clear.Execute = arg => {
            var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
            var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
            var left = Math.Min(selectionStart, selectionEnd);
            var right = Math.Max(selectionStart, selectionEnd);
            var startRun = Model.GetNextRun(left);
            var endRun = Model.GetNextRun(right);
            if (startRun == endRun && startRun.Start <= left && (startRun.Start < left || startRun.Start + startRun.Length - 1 > right) && startRun is ITableRun arrayRun) {
               for (int i = 0; i < arrayRun.ElementCount; i++) {
                  var start = arrayRun.Start + arrayRun.ElementLength * i;
                  if (start + arrayRun.ElementLength <= left) continue;
                  if (start > right) break;
                  for (int j = 0; j < arrayRun.ElementContent.Count; j++) {
                     start = arrayRun.Start + arrayRun.ElementLength * i + arrayRun.ElementContent.Take(j).Sum(seg => seg.Length);
                     if (start + arrayRun.ElementContent[j].Length <= left) continue;
                     if (start > right) break;
                     for (int k = 0; k < arrayRun.ElementContent[j].Length; k++) {
                        if (arrayRun.ElementContent[j].Type == ElementContentType.Pointer) {
                           history.CurrentChange.ChangeData(Model, start + k, 0x00);
                        } else {
                           history.CurrentChange.ChangeData(Model, start + k, 0xFF);
                        }
                     }
                  }
               }
            } else if (startRun == endRun && (startRun.Start < left || startRun.Start + startRun.Length > right + 1)) {
               // clearing _within_ a single run
               Model.ClearData(history.CurrentChange, left, right - left + 1);
            } else if (left >= Model.Count) {
               // don't do any clearing, we are past the last byte
            } else {
               Model.ClearFormatAndData(history.CurrentChange, left, right - left + 1);
            }
            tools?.StringTool.DataForCurrentRunChanged();
            RefreshBackingData();
            scroll.UpdateHeaders();
         };

         copy.CanExecute = CanAlwaysExecute;
         copy.Execute = arg => {
            var filesystem = (IFileSystem)arg;
            CopyExecute(filesystem, allowModelChanges: false);
         };

         copyAddress.CanExecute = CanAlwaysExecute;
         copyAddress.Execute = arg => {
            var fileSystem = (IFileSystem)arg;
            CopyAddressExecute(fileSystem);
         };

         copyBytes.CanExecute = CanAlwaysExecute;
         copyBytes.Execute = arg => {
            var fileSystem = (IFileSystem)arg;
            CopyBytesExecute(fileSystem);
         };

         deepCopy.CanExecute = CanAlwaysExecute;
         deepCopy.Execute = arg => {
            var fileSystem = (IFileSystem)arg;
            DeepCopyExecute(fileSystem);
         };

         moveSelectionStart.CanExecute = selection.MoveSelectionStart.CanExecute;
         moveSelectionStart.Execute = arg => {
            var direction = (Direction)arg;
            using (ModelCacheScope.CreateScope(Model)) {
               MoveSelectionStartExecuted(arg, direction);
            }
         };
         selection.MoveSelectionStart.CanExecuteChanged += (sender, e) => moveSelectionStart.CanExecuteChanged.Invoke(this, e);
         moveSelectionEnd.CanExecute = selection.MoveSelectionEnd.CanExecute;
         moveSelectionEnd.Execute = arg => {
            using (ModelCacheScope.CreateScope(Model)) {
               selection.MoveSelectionEnd.Execute(arg);
            }
         };
         selection.MoveSelectionEnd.CanExecuteChanged += (sender, e) => moveSelectionEnd.CanExecuteChanged.Invoke(this, e);

         isText.CanExecute = CanAlwaysExecute;
         isText.Execute = IsTextExecuted;

         save.CanExecute = arg => !history.IsSaved;
         save.Execute = arg => SaveExecuted((IFileSystem)arg);

         saveAs.CanExecute = CanAlwaysExecute;
         saveAs.Execute = arg => SaveAsExecuted((IFileSystem)arg);

         exportBackup.CanExecute = arg => !history.HasDataChange;
         exportBackup.Execute = arg => ExportBackupExecuted((IFileSystem)arg);

         close.CanExecute = CanAlwaysExecute;
         close.Execute = arg => CloseExecuted((IFileSystem)arg);

         selectAll.CanExecute = CanAlwaysExecute;
         selectAll.Execute = arg => SelectAllExecuted();
      }

      /// <summary>
      /// Top-level scripts may be available through metadata.
      /// Find scripts called by those scripts, and add runs for those too.
      /// </summary>
      private void CascadeScripts() {
         var noChange = new NoDataChangeDeltaModel();
         using (ModelCacheScope.CreateScope(Model)) {
            foreach (var run in Runs(Model).OfType<IScriptStartRun>().ToList()) {
               if (run is XSERun) {
                  tools.CodeTool.ScriptParser.FormatScript<XSERun>(noChange, Model, run.Start);
               } else if (run is BSERun) {
                  tools.CodeTool.BattleScriptParser.FormatScript<BSERun>(noChange, Model, run.Start);
               } else if (run is ASERun) {
                  tools.CodeTool.AnimationScriptParser.FormatScript<ASERun>(noChange, Model, run.Start);
               }
            }
         }
      }

      private static IEnumerable<IFormattedRun> Runs(IDataModel model) {
         for (var run = model.GetNextRun(0); run.Start < model.Count; run = model.GetNextRun(run.Start + Math.Max(1, run.Length))) {
            yield return run;
         }
      }

      private void CopyAddressExecute(IFileSystem fileSystem) {
         var copyText = scroll.ViewPointToDataIndex(selection.SelectionStart).ToString("X6");
         fileSystem.CopyText = copyText;
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
         OnMessage?.Invoke(this, $"'{copyText}' copied to clipboard.");
      }

      private void CopyBytesExecute(IFileSystem fileSystem) {
         var copyText = GetSelectedByteContents();
         fileSystem.CopyText = copyText;
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
         OnMessage?.Invoke(this, $"'{copyText}' copied to clipboard.");
      }

      private void CopyExecute(IFileSystem filesystem, bool allowModelChanges) {
         var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
         var left = Math.Min(selectionStart, selectionEnd);
         var length = Math.Abs(selectionEnd - selectionStart) + 1;
         if (length > Singletons.CopyLimit) {
            OnError?.Invoke(this, $"Cannot copy more than {Singletons.CopyLimit} bytes at once!");
         } else {
            bool usedHistory = false;
            if (left + length > Model.Count) {
               OnError?.Invoke(this, $"Cannot copy beyond the end of the data.");
            } else if (left < 0) {
               OnError?.Invoke(this, $"Cannot copy before the start of the data.");
            } else {
               if (allowModelChanges) {
                  filesystem.CopyText = Model.Copy(() => { usedHistory = true; return history.CurrentChange; }, left, length);
               } else {
                  filesystem.CopyText = Model.Copy(() => null, left, length);
               }
               RefreshBackingData();
               if (usedHistory) UpdateToolsFromSelection(left);
            }
         }
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
      }

      private void DeepCopyExecute(IFileSystem fileSystem) {
         var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
         var left = Math.Min(selectionStart, selectionEnd);
         var length = Math.Abs(selectionEnd - selectionStart) + 1;
         if (length > Singletons.CopyLimit) {
            OnError?.Invoke(this, $"Cannot copy more than {Singletons.CopyLimit} bytes at once!");
         } else {
            bool usedHistory = false;
            fileSystem.CopyText = Model.Copy(() => { usedHistory = true; return history.CurrentChange; }, left, length, deep: true);
            RefreshBackingData();
            if (usedHistory) UpdateToolsFromSelection(left);
         }
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      private void MoveSelectionStartExecuted(object arg, Direction direction) {
         var format = this[SelectionStart.X, SelectionStart.Y].Format;
         if (format is UnderEdit underEdit && underEdit.AutocompleteOptions != null && underEdit.AutocompleteOptions.Count > 0) {
            int index = -1;
            for (int i = 0; i < underEdit.AutocompleteOptions.Count; i++) if (underEdit.AutocompleteOptions[i].IsSelected) index = i;
            var options = default(IEnumerable<AutoCompleteSelectionItem>);
            if (direction == Direction.Up) {
               index -= 1;
               if (index < -1) index = underEdit.AutocompleteOptions.Count - 1;
               options = AutoCompleteSelectionItem.Generate(underEdit.AutocompleteOptions.Select(option => option.CompletionText), index);
            } else if (direction == Direction.Down) {
               index += 1;
               if (index == underEdit.AutocompleteOptions.Count) index = -1;
               options = AutoCompleteSelectionItem.Generate(underEdit.AutocompleteOptions.Select(option => option.CompletionText), index);
            }
            if (options != null) {
               var edit = new UnderEdit(underEdit.OriginalFormat, underEdit.CurrentText, underEdit.EditWidth, options);
               currentView[SelectionStart.X, SelectionStart.Y] = new HexElement(this[SelectionStart.X, SelectionStart.Y], edit);
               NotifyCollectionChanged(ResetArgs);
               return;
            }
         }
         PreviewScrollChanged?.Invoke(this, EventArgs.Empty);
         selection.MoveSelectionStart.Execute(arg);
      }

      public Point ConvertAddressToViewPoint(int address) => scroll.DataIndexToViewPoint(address);
      public int ConvertViewPointToAddress(Point p) => scroll.ViewPointToDataIndex(p);

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point selectionPoint, IFileSystem fileSystem = null) {
         // don't show the context menu if the clicked box isn't actually selected.
         // Example: selection is outside the range of selectable data (maybe past the end of the data).
         if (!IsSelected(selectionPoint)) return new IContextItem[0];
         var factory = new ContextItemFactory(this);
         var cell = this[SelectionStart.X, SelectionStart.Y];
         (cell?.Format ?? None.Instance).Visit(factory, cell.Value);
         var results = factory.Results.ToList();
         if (!SelectionStart.Equals(SelectionEnd)) {
            results.Add(new ContextItem("Copy", Copy.Execute) { ShortcutText = "Ctrl+C" });
            results.Add(new ContextItem("Deep Copy", DeepCopy.Execute) { ShortcutText = "Ctrl+Shift+C" });
         }
         results.Add(new ContextItem("Paste", arg => Edit(((IFileSystem)arg).CopyText)) { ShortcutText = "Ctrl+V" });
         if (fileSystem != null && fileSystem.CopyText.All(c => AllHexCharacters.Contains(c) || char.IsWhiteSpace(c))) {
            results.Add(new ContextItem("Paste Raw Bytes", arg => PasteRawBytes(((IFileSystem)arg).CopyText)));
         }
         results.Add(new ContextItem("Copy Address", arg => CopyAddressExecute((IFileSystem)arg)));
         return results;
      }

      private void PasteRawBytes(string text) {
         text = text.Replace(" ", "").Replace("\n", "").Replace("\r", "");
         var index = Math.Min(ConvertViewPointToAddress(SelectionStart), ConvertViewPointToAddress(SelectionEnd));
         for (int i = 0; i < text.Length / 2; i++) {
            var high = AllHexCharacters.IndexOf(text[i * 2]);
            var low = AllHexCharacters.IndexOf(text[i * 2 + 1]);
            var value = (byte)((high << 4) + low);
            var run = Model.GetNextRun(index + i);
            if (run.Start <= index + i) {
               // remove pointers, since randomly changing pointer bytes can lead to metadata issues
               if (run is ITableRun tableRun) {
                  var contentIndex = tableRun.ConvertByteOffsetToArrayOffset(index + i).SegmentIndex;
                  if (tableRun.ElementContent[contentIndex].Type == ElementContentType.Pointer) {
                     Model.ClearFormat(CurrentChange, index + i, 1);
                  }
               } 
               if (run is PointerRun) Model.ClearFormat(CurrentChange, index + i, 1);
            }
            CurrentChange.ChangeData(Model, index + i, value);
         }
         SelectionStart = ConvertAddressToViewPoint(index + text.Length / 2);
         RefreshBackingDataFull();
      }

      public bool IsSelected(Point point) => selection.IsSelected(point);

      public bool IsTable(Point point) {
         var search = scroll.ViewPointToDataIndex(point);
         var run = Model.GetNextRun(search);
         return run.Start <= search && run is ITableRun;
      }

      public void Refresh() {
         scroll.DataLength = Model.Count;
         var selectionStart = ConvertViewPointToAddress(SelectionStart);
         if (selectionStart > Model.Count + 1) SelectionStart = ConvertAddressToViewPoint(Model.Count + 1);
         RefreshBackingData();
         scroll.UpdateHeaders();
         Tools?.TableTool.DataForCurrentRunChanged();
         Tools?.SpriteTool.DataForCurrentRunChanged();
         UpdateAnchorText(ConvertViewPointToAddress(SelectionStart));
      }

      public void Cut(IFileSystem filesystem) {
         CopyExecute(filesystem, allowModelChanges: true);
         Clear.Execute();
      }

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) {
         if (file.Name.ToLower().EndsWith(".hma")) {
            var edit = Encoding.Default.GetString(file.Contents);
            Edit(edit);
            return true;
         } else if (file.Name.ToLower().EndsWith(".ips")) {
            history.ChangeCompleted();
            history.ClearHistory();
            var destination = Patcher.ApplyIPSPatch(Model, file.Contents, new NoTrackChange());
            if (destination >= 0) ReloadMetadata(Model.RawData, Model.ExportMetadata(Singletons.MetadataInfo).Serialize());
            Goto.Execute(destination);
            return true;
         } else if (file.Name.ToLower().EndsWith(".ups")) {
            history.ChangeCompleted();
            history.ClearHistory();
            var destination = Patcher.ApplyUPSPatch(Model, file.Contents, () => new NoTrackChange(), ignoreChecksums: false, out var direction);
            scroll.DataLength = Model.Count;
            switch (destination) {
               case -1: RaiseError("UPS Header didn't match!"); break;
               case -2:
                  var choice = fileSystem.ShowOptions("UPS Patch Error", "The UPS source-file check failed: it isn't meant to be run on this file.", null,
                     new VisualOption { Option = "Apply It Anyway", Index = 0, ShortDescription = "This is UNsafe", Description = $"Applying a UPS when the checksum doesn't match can break your file.{Environment.NewLine}But if you're sure that the patch doesn't interfere with any changes to your rom, you may choose to run it anyway." },
                     new VisualOption { Option = "Cancel", Index = 1, ShortDescription = "This is SAFE", Description = "The UPS will instead be opened as a hex file in a separate tab." }
                     );
                  if (choice == 0) {
                     destination = Patcher.ApplyUPSPatch(Model, file.Contents, () => new NoTrackChange(), ignoreChecksums: true, out direction);
                     scroll.DataLength = Model.Count;
                  } else {
                     return false;
                  }
                  if (destination == -3) RaiseError("Patch file is corrupt! (CRC doesn't match!)");
                  break;
               case -3: RaiseError("Patch file is corrupt! (CRC doesn't match!)"); break;
               case -4: RaiseError("Source file size doesn't match!"); break;
               case -5: RaiseError("Result file CRC doesn't match! Patch was still applied."); break;
               case -6: RaiseError("UPS chunks are corrupted! Patch was still applied."); break;
               case -7: RaiseError("Tried to write past the end of the result file! Patch was still applied."); break;
            }

            if (destination >= 0) {
               ReloadMetadata(Model.RawData, Model.ExportMetadata(Singletons.MetadataInfo).Serialize());
               Goto.Execute(destination);
               if (direction == Patcher.UpsPatchDirection.SourceToDestination) {
                  RaiseMessage("Applied UPS: source->destination patch.");
               } else if (direction == Patcher.UpsPatchDirection.DestinationToSource) {
                  RaiseMessage("Reverted UPS: destination->source patch.");
               }
            }

            return true;
         }

         return false;
      }

      public void RaiseError(string text) => OnError?.Invoke(this, text);

      private string deferredMessage;
      public void RaiseMessage(string text) {
         // TODO queue multiple messages.
         deferredMessage = text;
         tools.Schedule(RaiseMessage);
      }
      private void RaiseMessage() => OnMessage?.Invoke(this, deferredMessage);

      public void ClearAnchor() {
         var startDataIndex = scroll.ViewPointToDataIndex(SelectionStart);
         var endDataIndex = scroll.ViewPointToDataIndex(SelectionEnd);
         if (startDataIndex > endDataIndex) (startDataIndex, endDataIndex) = (endDataIndex, startDataIndex);

         // do the clear with a custom token that can't change data.
         // This anchor-clear is a formatting-only change.
         scroll.ClearTableMode();
         Model.ClearAnchor(history.InsertCustomChange(new NoDataChangeDeltaModel()), startDataIndex, endDataIndex - startDataIndex + 1);
         Refresh();
      }

      /// <summary>
      /// The primary Edit method.
      /// If the edit is large, this will create a loading bar that runs from 0 to 100%,
      /// with parts of the edit split off to happen over time.
      /// </summary>
      public void Edit(string input) {
         if (UpdateInProgress) return;
         lock (threadlock) {
            UpdateInProgress = true;
            CurrentProgressScopes.Insert(0, tools.DeferUpdates);
            initialWorkLoad = input.Length;
            postEditWork = 0;
            EditCore(input);
         }
      }

      /// <summary>
      /// A separate Edit method that assumes that the edit is part of a larger operation.
      /// The loading bar will still be cleared after the edit, just in case.
      /// But this messes with how the loading bar will fill.
      /// </summary>
      public async Task Edit(string input, double startPercent, double endPercent) {
         CurrentProgressScopes.Insert(0, tools.DeferUpdates);
         initialWorkLoad = (int)(input.Length / (endPercent - startPercent));
         postEditWork = (int)((1 - endPercent) * initialWorkLoad);
         initialWorkLoad -= postEditWork;
         await EditCoreAsync(input);
      }

      private const int DefaultChunkSize = 200;
      private void EditCore(string input) {
         // allow chunking at newline boundaries only
         int chunkSize = Math.Max(DefaultChunkSize, initialWorkLoad / 100);
         var maxSize = input.Length;

         if (dispatcher != null && input.Length > chunkSize) {
            var nextNewline = input.Substring(chunkSize).IndexOf('\n');
            if (nextNewline != -1) maxSize = chunkSize + nextNewline + 1;
         }

         exitEditEarly = false;
         int i = EditHelper(input, maxSize);

         if (exitEditEarly) {
            ClearEditWork();
            Refresh();
         } else if (input.Length > i) {
            Progress = (double)(initialWorkLoad - input.Length) / (initialWorkLoad + postEditWork);
            dispatcher.DispatchWork(() => EditCore(input.Substring(i)));
         } else {
            ClearEditWork();
         }
      }

      private async Task EditCoreAsync(string input) {
         int chunkSize = Math.Max(DefaultChunkSize, initialWorkLoad / 100);
         var maxSize = input.Length;

         if (dispatcher != null && input.Length > chunkSize) {
            var nextNewline = input.Substring(chunkSize).IndexOf('\n');
            if (nextNewline != -1) maxSize = chunkSize + nextNewline + 1;
         }

         exitEditEarly = false;
         int i;
         lock (threadlock) {
            i = EditHelper(input, maxSize);
         }

         if (exitEditEarly) {
            lock (threadlock) {
               ClearEditWork();
               Refresh();
            }
         } else if (input.Length > i) {
            await UpdateProgress((double)(initialWorkLoad - input.Length) / (initialWorkLoad + postEditWork));
            await EditCoreAsync(input.Substring(i));
         } else {
            lock (threadlock) {
               ClearEditWork();
            }
         }
      }

      private int EditHelper(string input, int maxSize) {
         int i = 0;
         try {
            for (i = 0; i < input.Length && i < maxSize && !exitEditEarly; i++) {
               var precededByWhitespace = i == 0 || input[i - 1] == ' ' || input[i - 1] == '\n';
               if (input[i] == '@' && input.Substring(i).StartsWith("@!game")) skipToNextGameCode = false;
               if (skipToNextGameCode) {
                  // skip this input
               } else if (input[i] == '.' && input.Length > i + 6 && input.Substring(i + 1, 5).ToLower() == "thumb" && precededByWhitespace) {
                  var lines = input.Substring(i).Split('\n', '\r');
                  var endLine = lines.Length.Range().FirstOrDefault(j => (lines[j] + " ").ToLower().StartsWith(".end "));
                  if (endLine == 0) endLine = lines.Length - 1;
                  lines = lines.Take(endLine + 1).ToArray();
                  var thumbLength = (lines.Length - 1) + lines.Sum(line => line.Length);
                  i += thumbLength - 1;
                  InsertThumbCode(lines);
               } else if (input[i] == '.' && input.Length > i + 7 && input.Substring(i + 1, 6).ToLower() == "python" && precededByWhitespace) {
                  var lines = input.Substring(i).Split('\n', '\r');
                  var endLine = lines.Length.Range().FirstOrDefault(j => (lines[j] + " ").ToLower().StartsWith(".end "));
                  if (endLine == 0) endLine = lines.Length - 1;
                  lines = lines.Take(endLine + 1).ToArray();
                  var pythonLength = (lines.Length - 1) + lines.Sum(line => line.Length);
                  i += pythonLength - 1;
                  // note that we're ignoring any non-error result here
                  var pythonContent = Environment.NewLine.Join(lines.Skip(1).Take(lines.Length - 2));
                  var result = PythonTool.RunPythonScript(pythonContent);
                  if (result.HasError && !result.IsWarning) {
                     RaiseError(result.ErrorMessage);
                     exitEditEarly = true;
                  }
               } else {
                  Edit(input[i]);
               }
            }
         } catch {
            ClearEditWork();
            throw;
         }

         return i;
      }

      public void InsertThumbCode(string[] lines) {
         var start = ConvertViewPointToAddress(SelectionStart);
         var result = tools.CodeTool.Parser.Compile(CurrentChange, Model, start, lines);
         SelectionStart = ConvertAddressToViewPoint(start + result.Count);
         RefreshBackingData();
      }

      private void ClearEditWork() {
         CurrentProgressScopes.ForEach(scope => scope.Dispose());
         CurrentProgressScopes.Clear();
         UpdateInProgress = false;
         skipToNextGameCode = false;
      }

      public void Edit(ConsoleKey key) {
         lock (threadlock) {
            using (ModelCacheScope.CreateScope(Model)) {
               var point = GetEditPoint();
               var offset = scroll.ViewPointToDataIndex(point);
               var run = Model.GetNextRun(offset);
               var element = this[point.X, point.Y];
               var underEdit = element.Format as UnderEdit;
               if (key == ConsoleKey.Enter && underEdit != null) {
                  if (underEdit.AutocompleteOptions != null && underEdit.AutocompleteOptions.Any(option => option.IsSelected)) {
                     var selectedIndex = AutoCompleteSelectionItem.SelectedIndex(underEdit.AutocompleteOptions);
                     underEdit = new UnderEdit(underEdit.OriginalFormat, underEdit.AutocompleteOptions[selectedIndex].CompletionText, underEdit.EditWidth);
                     currentView[point.X, point.Y] = new HexElement(element.Value, element.Edited, underEdit);
                     RequestMenuClose?.Invoke(this, EventArgs.Empty);
                     TryCompleteEdit(point);
                  } else {
                     Edit(Environment.NewLine);
                  }
                  return;
               }
               if (key == ConsoleKey.Enter && run is ITableRun arrayRun1) {
                  var offsets = arrayRun1.ConvertByteOffsetToArrayOffset(offset);
                  SilentScroll(offsets.SegmentStart + arrayRun1.ElementLength);
               }
               if (key == ConsoleKey.Tab && run is ITableRun arrayRun2) {
                  var offsets = arrayRun2.ConvertByteOffsetToArrayOffset(offset);
                  SilentScroll(offsets.SegmentStart + arrayRun2.ElementContent[offsets.SegmentIndex].Length);
               }
               if (key == ConsoleKey.Escape) {
                  ClearEdits(SelectionStart);
                  ClearMessage?.Invoke(this, EventArgs.Empty);
                  RequestMenuClose?.Invoke(this, EventArgs.Empty);
               }

               if (key != ConsoleKey.Backspace) return;

               // special case: when an entire run is selected, tread backspace like delete
               //   (special case doesn't apply to short runs like IScriptStartRun, NoInfoRun, or PointerRun)
               if (run.Length > 4 && scroll.ViewPointToDataIndex(SelectionStart) == run.Start && scroll.ViewPointToDataIndex(SelectionEnd) == run.Start + run.Length - 1) {
                  Clear.Execute();
                  return;
               }

               AcceptBackspace(underEdit, element.Value, point);
            }
         }
      }

      public void Autocomplete(string input) {
         var point = SelectionStart;
         var element = this[point.X, point.Y];
         var underEdit = element.Format as UnderEdit;
         if (underEdit == null) return;
         bool tryComplete = true;
         if (underEdit.AutocompleteOptions != null) {
            var options = underEdit.AutocompleteOptions;
            var index = options.Select(option => option.CompletionText).ToList().IndexOf(input);
            underEdit = new UnderEdit(underEdit.OriginalFormat, options[index].CompletionText, underEdit.EditWidth);
            tryComplete = options[index].IsFormatComplete;
         } else {
            underEdit = new UnderEdit(underEdit.OriginalFormat, input, underEdit.EditWidth);
         }
         currentView[point.X, point.Y] = new HexElement(element.Value, element.Edited, underEdit);
         if (tryComplete) {
            TryCompleteEdit(point);
         } else {
            NotifyCollectionChanged(ResetArgs); // refresh the view
         }
      }

      public void RepointToNewCopy(int pointer) {
         // if the pointer points to nothing
         var destinationAddress = Model.ReadPointer(pointer);
         if (destinationAddress == Pointer.NULL) {
            CreateNewData(pointer);
            return;
         }

         // if the pointer is expected to point to a type of data, but doesn't
         var destination = Model.GetNextRun(destinationAddress);
         var parentRun = Model.GetNextRun(pointer);
         if (parentRun is ITableRun tableRun) {
            var offset = tableRun.ConvertByteOffsetToArrayOffset(pointer);
            if (tableRun.ElementContent[offset.SegmentIndex] is ArrayRunPointerSegment pSegment) {
               var run = destination;
               var error = Model.FormatRunFactory.GetStrategy(pSegment.InnerFormat, allowStreamCompressionErrors: true).TryParseData(Model, string.Empty, destinationAddress, ref run);
               if (error.HasError) {
                  CreateNewData(pointer);
                  return;
               }
            }
         }

         // if the pointer points to the right type of data, but with no run
         if (destination.Start != destinationAddress) {
            RepointWithoutRun(pointer, destinationAddress);
            return;
         }

         if (destination is ArrayRun) {
            OnError?.Invoke(this, "Cannot automatically duplicate a table. This operation is unsafe.");
            return;
         }

         int newDestination;
         if (destination is LZRun lz && lz.HasLengthErrors) {
            // we can repoint this
            var uncompressed = LZRun.Decompress(Model, lz.Start, true);
            var newCompressed = LZRun.Compress(uncompressed);
            newDestination = Model.FindFreeSpace(destination.Start, newCompressed.Count);
            if (newDestination == -1) {
               newDestination = Model.Count;
               Model.ExpandData(history.CurrentChange, Model.Count + newCompressed.Count);
            }

            history.CurrentChange.ChangeData(Model, newDestination, newCompressed);
         } else if (destination.PointerSources.Count < 2) {
            OnError?.Invoke(this, "This is the only pointer, no need to make a new copy.");
            return;
         } else {
            newDestination = Model.FindFreeSpace(destination.Start, destination.Length);
            if (newDestination == -1) {
               newDestination = Model.Count;
               Model.ExpandData(history.CurrentChange, Model.Count + destination.Length);
            }

            for (int i = 0; i < destination.Length; i++) {
               history.CurrentChange.ChangeData(Model, newDestination + i, Model[destination.Start + i]);
            }
         }

         Model.ClearPointer(CurrentChange, pointer, destination.Start);
         Model.WritePointer(CurrentChange, pointer, newDestination); // point to the new destination
         var destination2 = Model.GetNextRun(destination.Start);
         Model.ObserveRunWritten(CurrentChange, destination2.Duplicate(newDestination, new SortedSpan<int>(pointer))); // create a new run at the new destination
         OnMessage?.Invoke(this, "New Copy added at " + newDestination.ToString("X6"));
      }

      public void OpenInNewTab(int destination) {
         var child = new ViewPort(FileName, Model, dispatcher, Singletons, PythonTool, history);
         child.selection.GotoAddress(destination);
         RequestTabChange?.Invoke(this, child);
      }

      private bool CreateNewData(int pointer) {
         var errorText = "Can only create new data for a pointer with a format within a table.";
         if (!(Model.GetNextRun(pointer) is ITableRun tableRun)) {
            OnError?.Invoke(this, errorText);
            return false;
         }
         var offsets = tableRun.ConvertByteOffsetToArrayOffset(pointer);
         if (!(tableRun.ElementContent[offsets.SegmentIndex] is ArrayRunPointerSegment pointerSegment) || !pointerSegment.IsInnerFormatValid) {
            OnError?.Invoke(this, errorText);
            return false;
         }

         var length = Model.FormatRunFactory.GetStrategy(pointerSegment.InnerFormat).LengthForNewRun(Model, pointer);

         var insert = Model.FindFreeSpace(0, length);
         if (insert < 0) {
            insert = Model.Count;
            Model.ExpandData(CurrentChange, Model.Count + length);
            scroll.DataLength = Model.Count;
         }
         pointerSegment.WriteNewFormat(Model, CurrentChange, pointer, insert, tableRun.ElementContent);
         RaiseMessage($"New data added at {insert:X6}");
         RefreshBackingData();
         return true;
      }

      /// <summary>
      /// Sometimes, valid data exists in the game but no run could be added.
      /// In such cases, it could be because a conflict was detected between 2 runs in the data.
      /// Make a new copy of data, only if we can prove that the data is valid and only if no run is found.
      /// Leave the original data (and other pointers to it) untouched.
      /// </summary>
      private void RepointWithoutRun(int source, int destination) {
         if (!(Model.GetNextRun(source) is ITableRun table)) {
            RaiseError("Could not parse a data format for that pointer.");
            return;
         }
         var offset = table.ConvertByteOffsetToArrayOffset(source);
         var segment = table.ElementContent[offset.SegmentIndex] as ArrayRunPointerSegment;
         if (segment == null) {
            RaiseError("Could not parse a data format for that pointer.");
            return;
         }
         var strategy = Model.FormatRunFactory.GetStrategy(segment.InnerFormat);
         if (strategy == null) {
            RaiseError("Could not parse a data format for that pointer.");
            return;
         }
         IFormattedRun run = new NoInfoRun(destination, new SortedSpan<int>(source));
         if (strategy.TryParseData(Model, string.Empty, destination, ref run).HasError) {
            RaiseError("Could not parse a data format for that pointer.");
            return;
         }

         var newDestination = Model.FindFreeSpace(destination, run.Length);
         if (newDestination == -1) {
            newDestination = Model.Count;
            Model.ExpandData(history.CurrentChange, Model.Count + run.Length);
         }

         for (int i = 0; i < run.Length; i++) {
            history.CurrentChange.ChangeData(Model, newDestination + i, Model[destination + i]);
         }

         Model.WritePointer(CurrentChange, source, newDestination); // point to the new destination
         var newRun = run.Duplicate(newDestination, new SortedSpan<int>(source));
         Model.ObserveRunWritten(CurrentChange, newRun); // create a new run at the new destination
         OnMessage?.Invoke(this, $"Run moved to {newDestination:X6}. This pointer was updated, original data was not modified.");
         Refresh();
      }

      private void AcceptBackspace(UnderEdit underEdit, byte cellValue, Point point) {
         // backspace in progress with characters left: just clear a character
         if (underEdit != null && underEdit.CurrentText.Length > 0) {
            var newText = underEdit.CurrentText.Substring(0, underEdit.CurrentText.Length - 1);
            IEnumerable<AutoCompleteSelectionItem> options = underEdit.AutocompleteOptions;
            if (options != null) {
               var selectedIndex = AutoCompleteSelectionItem.SelectedIndex(underEdit.AutocompleteOptions);
               options = GetAutocompleteOptions(underEdit.OriginalFormat, cellValue, newText, selectedIndex);
            }
            var newFormat = new UnderEdit(underEdit.OriginalFormat, newText, underEdit.EditWidth, options);
            currentView[point.X, point.Y] = new HexElement(this[point.X, point.Y], newFormat);
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         var index = scroll.ViewPointToDataIndex(point);

         // backspace on an empty element: clear the data from those cells
         if (underEdit != null) {
            var operation = new DataClear(Model, history.CurrentChange, index);
            var currentValue = index < Model.Count ? Model[index] : (byte)0;
            underEdit.OriginalFormat.Visit(operation, currentValue);
            RefreshBackingData();
            SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            point = GetEditPoint();
            index = scroll.ViewPointToDataIndex(point);
         }

         var run = Model.GetNextRun(index);
         var cell = this[point];
         var format = cell.Format;
         while (format is IDataFormatDecorator decorator) format = decorator.OriginalFormat;
         var cellToText = new ConvertCellToText(Model, index);
         format.Visit(cellToText, cell.Value);
         if (format is IDataFormatInstance instance) {
            SelectionStart = scroll.DataIndexToViewPoint(instance.Source);
            var editText = cellToText.Result.Substring(0, cellToText.Result.Length - 1);
            currentView[SelectionStart.X, SelectionStart.Y] = new HexElement(cell.Value, cell.Edited, cell.Format.Edit(editText));
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         if (run.Start > index) {
            // no run: doing a raw edit.
            SelectionStart = scroll.DataIndexToViewPoint(index);
            var element = this[SelectionStart.X, SelectionStart.Y];
            var text = element.Value.ToString("X2");
            currentView[SelectionStart.X, SelectionStart.Y] = new HexElement(element, element.Format.Edit(text.Substring(0, text.Length - 1)));
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         if (run is PCSRun || run is AsciiRun) {
            for (int i = index; i < run.Start + run.Length; i++) history.CurrentChange.ChangeData(Model, i, 0xFF);
            var length = PCSString.ReadString(Model, run.Start, true);
            if (run is PCSRun) Model.ObserveRunWritten(history.CurrentChange, new PCSRun(Model, run.Start, length, run.PointerSources));
            RefreshBackingData();
            SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            return;
         }

         void TableBackspace(int length) {
            PrepareForMultiSpaceEdit(point, length);
            cell.Format.Visit(cellToText, cell.Value);
            var text = cellToText.Result;
            if (format is BitArray) {
               for (int i = 1; i < length; i++) {
                  var extraData = Model[scroll.ViewPointToDataIndex(point) + i];
                  cell.Format.Visit(cellToText, extraData);
                  text += cellToText.Result;
               }
            }
            text = text.Substring(0, text.Length - 1);
            currentView[point.X, point.Y] = new HexElement(cell, new UnderEdit(cell.Format, text, length));
         }

         if (run is ITableRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(index);
            if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) {
               for (int i = index + 1; i < offsets.SegmentStart + array.ElementContent[offsets.SegmentIndex].Length; i++) history.CurrentChange.ChangeData(Model, i, 0x00);
               history.CurrentChange.ChangeData(Model, index, 0xFF);
               RefreshBackingData();
               scroll.UpdateHeaders();
               SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            } else if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer) {
               TableBackspace(4);
            } else if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Integer) {
               TableBackspace(((Integer)format).Length);
            } else if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.BitArray) {
               TableBackspace(((BitArray)format).Length);
            } else {
               throw new NotImplementedException();
            }
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         if (run is EggMoveRun || run is PLMRun) {
            PrepareForMultiSpaceEdit(point, 2);
            cell.Format.Visit(cellToText, cell.Value);
            var text = cellToText.Result;
            text = text.Substring(0, text.Length - 1);
            currentView[point.X, point.Y] = new HexElement(cell, new UnderEdit(cell.Format, text, 2));
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         if (run.Start <= index && run.Start + run.Length > index) {
            // I want to do a backspace at the end of this run
            SelectionStart = scroll.DataIndexToViewPoint(run.Start);
            var element = this[SelectionStart.X, SelectionStart.Y];
            element.Format.Visit(cellToText, element.Value);
            var text = cellToText.Result;

            var editLength = 1;
            if (element.Format is Pointer) editLength = 4;

            for (int i = 0; i < run.Length; i++) {
               var p = scroll.DataIndexToViewPoint(run.Start + i);
               string editString = i == 0 ? text.Substring(0, text.Length - 1) : string.Empty;
               if (i > 0) editLength = 1;
               var newFormat = new UnderEdit(this[p.X, p.Y].Format, editString, editLength);
               currentView[p.X, p.Y] = new HexElement(this[p.X, p.Y], newFormat);
            }
         }

         NotifyCollectionChanged(ResetArgs);
      }

      #region Find

      private byte[] findBytes;
      public byte[] FindBytes {
         get => findBytes;
         set {
            findBytes = value;
            NotifyPropertyChanged();
            RefreshBackingDataFull();
         }
      }

      public IReadOnlyList<(int start, int end)> Find(string rawSearch, bool matchExactCase = false) {
         var results = new List<(int start, int end)>();
         var cleanedSearchString = rawSearch;
         if (!matchExactCase) cleanedSearchString = rawSearch.ToUpper().Trim();
         var searchBytes = new List<ISearchByte>();

         // it might be a string with no quotes, we should check for matches for that.
         if (cleanedSearchString.Length > 3 && !cleanedSearchString.Contains(StringDelimeter) && !cleanedSearchString.All(AllHexCharacters.Contains)) {
            results.AddRange(FindUnquotedText(cleanedSearchString, searchBytes, matchExactCase));
         }

         // it might be a matched-word
         var matchedWords = Model.GetMatchedWords(rawSearch);
         if (matchedWords.Count > 0) {
            results.AddRange(matchedWords.Select(word => (word, word)));
         }

         // it might be a pointer without angle braces
         if (cleanedSearchString.Length == 6 && cleanedSearchString.All(AllHexCharacters.Contains)) {
            searchBytes.AddRange(Parse(cleanedSearchString).Reverse().Append((byte)0x08).Select(b => (SearchByte)b));
            results.AddRange(Model.Search(searchBytes).Select(result => (result, result + 3)));
         }

         // it might be a word
         if (cleanedSearchString.Length == 8 && cleanedSearchString.All(AllHexCharacters.Contains)) {
            searchBytes.AddRange(Parse(cleanedSearchString).Reverse().Select(b => (SearchByte)b));
            results.AddRange(Model.Search(searchBytes).Select(result => (result, result + 3)));
         }

         // it might be a bl command
         if (cleanedSearchString.StartsWith("BL ") && cleanedSearchString.Contains("<") && cleanedSearchString.EndsWith(">")) {
            results.AddRange(FindBranchLink(cleanedSearchString));
         }

         // attempt to parse the search string fully
         if (TryParseSearchString(searchBytes, cleanedSearchString, errorOnParseError: results.Count == 0)) {
            // find matches
            var textResults = Model.Search(searchBytes).Select(result => (result, result + searchBytes.Count - 1)).ToList();
            results.AddRange(textResults);
            // find data matches for the results that are in tables
            foreach (var result in textResults) {
               if (Model.GetNextRun(result.result) is ArrayRun parentArray && parentArray.LengthFromAnchor == string.Empty) {
                  results.AddRange(FindMatchingDataResultsFromArrayElement(parentArray, result.result));
               }
            }
         }

         // reorder the list to start at the current cursor position
         results.Sort((a, b) => a.start.CompareTo(b.start));
         var offset = scroll.ViewPointToDataIndex(SelectionStart);
         var left = results.Where(result => result.start < offset);
         var right = results.Where(result => result.start >= offset);
         results = right.Concat(left).ToList();
         NotifyNumberOfResults(rawSearch, results.Count);
         return results;
      }

      private IEnumerable<(int start, int end)> FindBranchLink(string command) {
         var addressStart = command.IndexOf(" <") + 2;
         var addressEnd = command.LastIndexOf(">");
         if (addressEnd < addressStart) yield break;
         var addressText = command.Substring(addressStart, addressEnd - addressStart);
         if (!int.TryParse(addressText, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int address)) {
            address = Model.GetAddressFromAnchor(CurrentChange, -1, addressText);
            if (address < 0 || address >= Model.Count) yield break;
         }

         // I want to know, for any given point in the raw data, if it's possible a branch-link command pointing to `address`
         // branch link commands are always 4 bytes and have the following format:
         // 11111 #11 11110 #11, where #=pc+#*2+4
         // note that this command is 4 bytes long, stored byte reversed. So in the data, it's:
         // 8 bits: bits 11-18 of a 22 bit signed offset
         // 8 bits:
         //         the low 3 bits are bits 19-21 of a 22 bit signed offset
         //         the high 5 bits are always 11110
         // 8 bits: bits 0-7 of a 22 bit signed offset
         // 8 bits:
         //         the low 3 bits are bits 8-10 of a 22 bit signed offset
         //         the high 5 bits are always 11111
         // the command is always 2-byte aligned
         //
         // bit order is really weird (11-18, 19-21, 0-7, 8-10) because BL is made of **2** instructions,
         // and each instruction is stored little-endian

         // start as early as possible in the file: maximum offset, or offset for source=0
         int offset = Math.Min(0b0111111111111111111111, (address - 4) / 2);
         for (; true; offset--) { // traveling down the offsets means traveling up the source options
            int source = address - 4 - offset * 2;
            if (source + 4 > Model.RawData.Length) break;
            if (Model.RawData[source + 2] != (byte)offset) continue; // check source+2 first because it's the simplest, and thus fastest
            if (Model.RawData[source + 0] != (byte)(offset >> 11)) continue;
            if (Model.RawData[source + 3] != (0b11111000 | (0b111 & offset >> 8))) continue;
            if (Model.RawData[source + 1] != (0b11110000 | (0b111 & offset >> 19))) continue;
            yield return (source, source + 3);
         }
      }

      private IEnumerable<(int start, int end)> FindUnquotedText(string cleanedSearchString, List<ISearchByte> searchBytes, bool matchExactCase) {
         var pcsBytes = Model.TextConverter.Convert(cleanedSearchString, out bool containsBadCharacters);
         pcsBytes.RemoveAt(pcsBytes.Count - 1); // remove the 0xFF that was added, since we're searching for a string segment instead of a whole string.

         // only search for the string if every character in the search string is allowed
         if (containsBadCharacters) yield break;

         searchBytes.AddRange(pcsBytes.Select(b => PCSSearchByte.Create(b, matchExactCase)));
         var textResults = Model.Search(searchBytes).ToList();
         Model.ConsiderResultsAsTextRuns(() => history.CurrentChange, textResults);
         foreach (var result in textResults) {
            // also look for elements that use that text as a name or value
            // (if matching exact case, we only want to find text: skip this step)
            if (!matchExactCase && Model.GetNextRun(result) is ArrayRun parentArray && parentArray.LengthFromAnchor == string.Empty) {
               foreach (var dataResult in FindMatchingDataResultsFromArrayElement(parentArray, result)) yield return dataResult;
            }

            yield return (result, result + pcsBytes.Count - 1);
         }

         // it could also be a list token. Look for matches among list enums.
         foreach (var dataResult in Model.FindListUsages(cleanedSearchString)) yield return dataResult;
      }

      /// <summary>
      /// When performing a search, sometimes one of the search results is text from a table.
      /// If so, then we also care about places where that table value is used.
      /// This function finds uses of an element in a table.
      /// </summary>
      private IEnumerable<(int start, int end)> FindMatchingDataResultsFromArrayElement(ArrayRun parentArray, int parentIndex) {
         var offsets = parentArray.ConvertByteOffsetToArrayOffset(parentIndex);
         var parentArrayName = Model.GetAnchorFromAddress(-1, parentArray.Start);
         if (offsets.SegmentIndex == 0 && parentArray.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) {
            var arrayUses = FindTableUsages(offsets, parentArrayName);
            var streamUses = FindStreamUsages(offsets, parentArrayName);
            return arrayUses.Concat(streamUses);
         }
         return Enumerable.Empty<(int, int)>();
      }

      private IEnumerable<(int start, int end)> FindTableUsages(ArrayOffset offsets, string parentArrayName) {
         foreach (var child in Model.All<ITableRun>()) {
            // option 1: another table has a row named after this element
            if (child is ArrayRun arrayRun && arrayRun.LengthFromAnchor == parentArrayName) {
               var address = child.Start + child.ElementLength * offsets.ElementIndex;
               yield return (address, address + child.ElementLength - 1);
            }

            // option 2: another table has an enum named after this element
            var segmentOffset = 0;
            foreach (var segment in child.ElementContent) {
               if (!(segment is ArrayRunEnumSegment enumSegment) || enumSegment.EnumName != parentArrayName) {
                  segmentOffset += segment.Length;
                  continue;
               }
               for (int i = 0; i < child.ElementCount; i++) {
                  var address = child.Start + child.ElementLength * i + segmentOffset;
                  var enumValue = Model.ReadMultiByteValue(address, segment.Length);
                  if (enumValue != offsets.ElementIndex) continue;
                  yield return (address, address + segment.Length - 1);
               }
               segmentOffset += segment.Length;
            }
         }
      }

      private IEnumerable<(int start, int end)> FindStreamUsages(ArrayOffset offsets, string parentArrayName) {
         foreach (var child in Model.Streams) {
            // option 1: the value is used by egg moves
            if (child is EggMoveRun eggRun) {
               foreach (var result in eggRun.Search(parentArrayName, offsets.ElementIndex)) yield return result;
            }
            // option 2: the value is used by learnable moves
            if (child is PLMRun plmRun && parentArrayName == HardcodeTablesModel.MoveNamesTable) {
               foreach (var result in plmRun.Search(offsets.ElementIndex)) yield return result;
            }
            // option 3: the value is a move used by trainer teams
            if (child is TrainerPokemonTeamRun team) {
               foreach (var result in team.Search(parentArrayName, offsets.ElementIndex)) {
                  yield return (result, result + 1);
               }
            }
            // option 3: the value is in an enum used by a custom table stream
            if (child is TableStreamRun table) {
               foreach (var result in table.Search(parentArrayName, offsets.ElementIndex)) yield return result;
            }
         }
      }

      private void NotifyNumberOfResults(string rawSearch, int results) {
         if (results == 1) {
            OnMessage?.Invoke(this, $"Found only 1 match for '{rawSearch}'.");
         } else if (results > 1) {
            OnMessage?.Invoke(this, $"Found {results} matches for '{rawSearch}'.");
         }
      }

      private byte[] Parse(string content) {
         var result = new byte[content.Length / 2];
         for (int i = 0; i < result.Length; i++) {
            var thisByte = content.Substring(i * 2, 2);
            result[i] += (byte)(AllHexCharacters.IndexOf(thisByte[0]) * 0x10);
            result[i] += (byte)AllHexCharacters.IndexOf(thisByte[1]);
         }
         return result;
      }

      private bool TryParseSearchString(List<ISearchByte> searchBytes, string cleanedSearchString, bool errorOnParseError) {
         for (int i = 0; i < cleanedSearchString.Length;) {
            if (cleanedSearchString[i] == ' ') {
               i++;
               continue;
            }

            if (cleanedSearchString[i] == PointerStart) {
               if (!TryParsePointerSearchSegment(searchBytes, cleanedSearchString, ref i)) return false;
               continue;
            }

            if (cleanedSearchString[i] == StringDelimeter) {
               if (TryParseStringSearchSegment(searchBytes, cleanedSearchString, ref i)) continue;
            }

            if (cleanedSearchString.Length >= i + 2 && cleanedSearchString.Substring(i, 2) == "XX") {
               searchBytes.Add(SearchByte.Wild);
               i += 2;
               continue;
            }

            if (cleanedSearchString.Length >= i + 2 && cleanedSearchString.Substring(i, 2).All(AllHexCharacters.Contains)) {
               searchBytes.AddRange(Parse(cleanedSearchString.Substring(i, 2)).Select(b => (SearchByte)b));
               i += 2;
               continue;
            }

            if (errorOnParseError) OnError?.Invoke(this, $"Could not parse search term {cleanedSearchString.Substring(i)}");
            return false;
         }

         return true;
      }

      private bool TryParsePointerSearchSegment(List<ISearchByte> searchBytes, string cleanedSearchString, ref int i) {
         var pointerEnd = cleanedSearchString.IndexOf(PointerEnd, i);
         if (pointerEnd == -1) { OnError(this, "Search mismatch: no closing >"); return false; }
         var pointerContents = cleanedSearchString.Substring(i + 1, pointerEnd - i - 1);
         var address = Model.GetAddressFromAnchor(history.CurrentChange, -1, pointerContents);
         if (address != Pointer.NULL) {
            searchBytes.Add((SearchByte)(address >> 0));
            searchBytes.Add((SearchByte)(address >> 8));
            searchBytes.Add((SearchByte)(address >> 16));
            searchBytes.Add((SearchByte)0x08);
         } else if (pointerContents.All(AllHexCharacters.Contains) && pointerContents.Length <= 6) {
            searchBytes.AddRange(Parse(pointerContents).Reverse().Append((byte)0x08).Select(b => (SearchByte)b));
         } else {
            OnError(this, $"Could not parse pointer <{pointerContents}>");
            return false;
         }
         i = pointerEnd + 1;
         return true;
      }

      private bool TryParseStringSearchSegment(List<ISearchByte> searchBytes, string cleanedSearchString, ref int i) {
         var endIndex = cleanedSearchString.IndexOf(StringDelimeter, i + 1);
         while (endIndex > i && cleanedSearchString[endIndex - 1] == '\\') endIndex = cleanedSearchString.IndexOf(StringDelimeter, endIndex + 1);
         if (endIndex > i) {
            var pcsBytes = Model.TextConverter.Convert(cleanedSearchString.Substring(i, endIndex + 1 - i), out var _);
            i = endIndex + 1;
            if (i == cleanedSearchString.Length) pcsBytes.RemoveAt(pcsBytes.Count - 1);
            searchBytes.AddRange(pcsBytes.Select(b => new PCSSearchByte(b)));
            return true;
         } else {
            return false;
         }
      }

      #endregion

      public IChildViewPort CreateChildView(int startAddress, int endAddress) {
         var child = new ChildViewPort(this, dispatcher, Singletons) { PreferredWidth = 16, Width = Math.Min(24, Width) };

         var run = Model.GetNextRun(startAddress);
         if (run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(startAddress);
            var lineStart = array.Start + array.ElementLength * offsets.ElementIndex;
            child.Goto.Execute(lineStart.ToString("X2"));
            child.SelectionStart = child.ConvertAddressToViewPoint(startAddress);
            var endPoint = child.ConvertAddressToViewPoint(endAddress);
            if (endPoint.Y - child.SelectionStart.Y > 3) child.Height = endPoint.Y - child.SelectionStart.Y + 1;
            child.SelectionEnd = endPoint;
         } else if (run is ITableRun tableRun) {
            child.Goto.Execute(tableRun.Start);
            child.Width = tableRun.Length <= 32 ? tableRun.Length : tableRun.ElementLength;
            var endPoint = child.ConvertAddressToViewPoint(endAddress);
            child.Height = endPoint.Y + 1;
            child.SelectionStart = child.ConvertAddressToViewPoint(startAddress);
            child.SelectionEnd = endPoint;
         } else {
            child.Goto.Execute(startAddress.ToString("X2"));
            var endPoint = child.ConvertAddressToViewPoint(endAddress);
            if (endPoint.Y > 3) child.Height = endPoint.Y + 1;
            child.SelectionEnd = endPoint;
         }

         return child;
      }

      public void FollowLink(int x, int y) {
         var format = this[x, y].Format;
         while (format is IDataFormatDecorator decorator) format = decorator.OriginalFormat;

         using (ModelCacheScope.CreateScope(Model)) {
            // follow pointer
            if (format is Pointer pointer) {
               if (pointer.Destination != Pointer.NULL) {
                  selection.GotoAddress(pointer.Destination);
               } else if (string.IsNullOrEmpty(pointer.DestinationName)) {
                  OnError(this, $"null pointers point to nothing, so going to their source isn't possible.");
               } else {
                  OnError(this, $"Pointer destination {pointer.DestinationName} not found.");
               }
               return;
            }

            // follow word value source
            if (format is MatchedWord word) {
               var address = Model.GetAddressFromAnchor(history.CurrentChange, -1, word.Name.Substring(2));
               if (address == Pointer.NULL) {
                  OnError(this, $"No table with name '{word.Name.Substring(2)}' was found.");
               } else {
                  selection.GotoAddress(address);
               }
               return;
            }

            // open tool
            var byteOffset = scroll.ViewPointToDataIndex(new Point(x, y));
            var currentRun = Model.GetNextRun(byteOffset);
            if (currentRun is ISpriteRun) {
               tools.SpriteTool.SpriteAddress = currentRun.Start;
               tools.SelectedIndex = Tools.IndexOf(Tools.SpriteTool);
            } else if (currentRun is IPaletteRun) {
               tools.SpriteTool.PaletteAddress = currentRun.Start;
               tools.SelectedIndex = Tools.IndexOf(Tools.SpriteTool);
            } else if (currentRun is IStreamRun) {
               Tools.StringTool.Address = currentRun.Start;
               Tools.SelectedIndex = Tools.IndexOf(Tools.StringTool);
            } else if (currentRun is ITableRun array) {
               var offsets = array.ConvertByteOffsetToArrayOffset(byteOffset);
               if (format is PCS) {
                  Tools.StringTool.Address = offsets.SegmentStart - offsets.ElementIndex * array.ElementLength;
                  Tools.SelectedIndex = Tools.IndexOf(Tools.StringTool);
               } else {
                  Tools.TableTool.Address = array.Start + offsets.ElementIndex * array.ElementLength;
                  Tools.SelectedIndex = Tools.IndexOf(Tools.TableTool);
               }
            }
         }
      }

      public void ExpandSelection(int x, int y) {
         var index = scroll.ViewPointToDataIndex(SelectionStart);
         var run = Model.GetNextRun(index);
         if (run.Start > index) return;
         if (run is ITableRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(index);
            if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer) {
               FollowLink(x, y);
            } else {
               SelectionStart = scroll.DataIndexToViewPoint(offsets.SegmentStart);
               SelectionEnd = scroll.DataIndexToViewPoint(offsets.SegmentStart + array.ElementContent[offsets.SegmentIndex].Length - 1);
            }
         } else if (run is PointerRun) {
            FollowLink(x, y);
         } else if (run is IScriptStartRun xse) {
            var length = tools.CodeTool.ScriptParser.GetScriptSegmentLength(Model, run.Start);
            if (xse is BSERun) length = tools.CodeTool.BattleScriptParser.GetScriptSegmentLength(Model, run.Start);
            if (xse is ASERun) length = tools.CodeTool.AnimationScriptParser.GetScriptSegmentLength(Model, run.Start);
            SelectionStart = scroll.DataIndexToViewPoint(run.Start);
            SelectionEnd = scroll.DataIndexToViewPoint(run.Start + length - 1);
            tools.CodeTool.Mode = CodeMode.Script;
            if (xse is BSERun) tools.CodeTool.Mode = CodeMode.BattleScript;
            if (xse is ASERun) tools.CodeTool.Mode = CodeMode.AnimationScript;
            tools.SelectedIndex = tools.IndexOf(tools.CodeTool);
         } else {
            SelectionStart = scroll.DataIndexToViewPoint(run.Start);
            SelectionEnd = scroll.DataIndexToViewPoint(run.Start + run.Length - 1);
         }
         if (SelectionStart == SelectionEnd && scroll.ViewPointToDataIndex(SelectionStart) >= run.Start && scroll.ViewPointToDataIndex(SelectionEnd) < run.Start + run.Length) {
            SelectionStart = scroll.DataIndexToViewPoint(run.Start);
            SelectionEnd = scroll.DataIndexToViewPoint(run.Start + run.Length - 1);
         }
      }

      public void ConsiderReload(IFileSystem fileSystem) {
         if (!history.IsSaved) return; // don't overwrite local changes

         try {
            var file = fileSystem.LoadFile(FileName);
            if (file == null) return; // asked to load the file, but the file wasn't found... carry on
            var metadata = fileSystem.MetadataFor(FileName);
            ReloadMetadata(file.Contents, metadata);
         } catch (IOException) {
            // something happened when we tried to load the file
            // try again soon.
            RequestDelayedWork?.Invoke(this, () => ConsiderReload(fileSystem));
         } catch (InvalidOperationException) {
            // failed to compare runs
            // try again soon.
            RequestDelayedWork?.Invoke(this, () => ConsiderReload(fileSystem));
         }

         return;
      }

      public void ReloadMetadata(byte[] data, string[] metadata) {
         Model.Load(data, metadata != null ? new StoredMetadata(metadata) : null);
         scroll.DataLength = Model.Count;
         RefreshBackingData();
         Model.InitializationWorkload.ContinueWith(task => {
            CascadeScripts();
            dispatcher.DispatchWork(RefreshBackingData);
         }, TaskContinuationOptions.ExecuteSynchronously);

         // if the new file is shorter, selection might need to be updated
         // this forces it to be re-evaluated.
         SelectionStart = SelectionStart;
      }

      public virtual void FindAllSources(int x, int y) {
         var anchor = this[x, y].Format as Anchor;
         if (anchor == null) return;
         FindAllSources(ConvertViewPointToAddress(new(x, y)));
      }

      public void FindAllSources(int address) {
         var name = Model.GetAnchorFromAddress(-1, address);
         var run = Model.GetNextRun(address);
         var title = string.IsNullOrEmpty(name) ? address.ToAddress() : name;
         title = "Sources of " + title;
         var newTab = new SearchResultsViewPort(title);

         foreach (var source in run.PointerSources) newTab.Add(CreateChildView(source, source), source, source);

         RequestTabChange(this, newTab);
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
      }

      public void OpenSearchResultsTab(string title, IReadOnlyList<(int start, int end)> showSelection, IReadOnlyList<(int start, int end)> gotoSelection = null) {
         gotoSelection ??= showSelection;
         if (showSelection.Count == 1) {
            var (start, end) = showSelection[0];
            selection.GotoAddress(start);
            SelectionStart = scroll.DataIndexToViewPoint(start);
            SelectionEnd = scroll.DataIndexToViewPoint(end);
            return;
         }

         var newTab = new SearchResultsViewPort(title);
         for (int i = 0; i < showSelection.Count; i++) {
            var (showStart, showEnd) = showSelection[i];
            var (gotoStart, gotoEnd) = gotoSelection[i];
            newTab.Add(CreateChildView(showStart, showEnd), gotoStart, gotoEnd);
         }
         RequestTabChange(this, newTab);
      }

      public void OpenDexReorderTab(string dexTableName) {
         var newTab = new DexReorderTab(Name, history, Model, dexTableName, HardcodeTablesModel.DexInfoTableName, dexTableName == HardcodeTablesModel.NationalDexTableName);
         RequestTabChange(this, newTab);
      }

      public void OpenImageEditorTab(int address, int spritePage, int palettePage) {
         try {
            var newTab = new ImageEditorViewModel(history, Model, address, Save, tools.SpriteTool.PaletteAddress) {
               SpritePage = spritePage,
               PalettePage = palettePage,
            };
            RequestTabChange(this, newTab);
         } catch (ImageEditorViewModelCreationException e) {
            RaiseError(e.Message);
         }
      }

      private void ValidateMatchedWords() {
         // TODO if this is too slow, add a method to the model to get the set of only MatchedWordRuns.
         for (var run = Model.GetNextRun(0); run != NoInfoRun.NullRun; run = Model.GetNextRun(run.Start + Math.Max(1, run.Length))) {
            if (!(run is WordRun wordRun)) continue;
            var address = Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, wordRun.SourceArrayName);
            if (address == Pointer.NULL) continue;
            var array = Model.GetNextRun(address) as ArrayRun;
            if (array == null) continue;
            var actualValue = Model.ReadValue(wordRun.Start);
            if (array.ElementCount == actualValue) continue;
            OnMessage?.Invoke(this, $"MatchedWord at {wordRun.Start:X6} was expected to have value {array.ElementCount}, but was {actualValue}.");
            Goto.Execute(wordRun.Start.ToString("X6"));
            break;
         }
      }

      public void InsertPalette16() {
         var selectionPoint = scroll.ViewPointToDataIndex(SelectionStart);
         var run1 = Model.GetNextRun(selectionPoint);
         var run2 = Model.GetNextRun(selectionPoint + 1);
         if (run1.Start + 32 != run2.Start || !(run1 is NoInfoRun)) {
            OnError?.Invoke(this, "Palettes insertion requires a no-format anchor with exactly 32 bytes of space.");
            return;
         }
         for (int i = 0; i < 16; i++) {
            if (Model.ReadMultiByteValue(run1.Start + i * 2, 2) >= 0x8000) {
               OnError?.Invoke(this, $"Palette colors only use 15 bits, but the high bit it set at {run1.Start + i * 2 + 1:X6}.");
               return;
            }
         }
         var currentName = Model.GetAnchorFromAddress(-1, run1.Start);
         if (string.IsNullOrEmpty(currentName)) currentName = $"{HardcodeTablesModel.DefaultPaletteNamespace}.{run1.Start:X6}";
         Model.ObserveAnchorWritten(CurrentChange, currentName, new PaletteRun(run1.Start, new PaletteFormat(4, 1)));
         Refresh();
         UpdateToolsFromSelection(run1.Start);
      }

      public void CascadeScript(int address) {
         Width = Math.Max(Width, 16);   // hack to make the width right on initial load
         Height = Math.Max(Height, 16); // hack to make the height right on initial load
         var addressText = address.ToString("X6");
         Goto.Execute(addressText);
         Debug.Assert(scroll.DataIndex == address - address % 16);
         var length = tools.CodeTool.ScriptParser.GetScriptSegmentLength(Model, address);
         Model.ClearFormat(CurrentChange, address, length - 1);

         using (ModelCacheScope.CreateScope(Model)) {
            tools.CodeTool.ScriptParser.FormatScript<XSERun>(CurrentChange, Model, address);
         }

         SelectionStart = scroll.DataIndexToViewPoint(address);
         SelectionEnd = scroll.DataIndexToViewPoint(address + length - 1);
         tools.CodeTool.Mode = CodeMode.Script;
         tools.SelectedIndex = tools.IndexOf(tools.CodeTool);
      }

      private void Edit(char input) {
         var point = GetEditPoint();
         var element = this[point.X, point.Y];

         if (input.IsAny('\r', '\n')) {
            input = ' '; // handle multiline pasting by just treating the newlines as standard whitespace.
            withinComment = false;
            if (element.Format is PCS || element.Format is ErrorPCS) return; // exit early: newlines within strings are ignored, because they're escaped.
         }

         if (element.Format is UnderEdit && input == ',') input = ' ';

         if (!ShouldAcceptInput(point, element, input)) {
            ClearEdits(point);
            return;
         }

         SelectionStart = point;

         if (element == this[point.X, point.Y]) {
            UnderEdit newFormat;
            if (element.Format is UnderEdit underEdit && underEdit.AutocompleteOptions != null) {
               var newText = underEdit.CurrentText + input;
               var autoCompleteOptions = GetAutocompleteOptions(underEdit.OriginalFormat, element.Value, newText);
               newFormat = new UnderEdit(underEdit.OriginalFormat, newText, underEdit.EditWidth, autoCompleteOptions);
            } else {
               newFormat = element.Format.Edit(input.ToString());
            }
            currentView[point.X, point.Y] = new HexElement(element, newFormat);
         } else {
            // ShouldAcceptInput already did the work: nothing to change
         }

         if (!TryCompleteEdit(point)) {
            // only need to notify collection changes if we didn't complete an edit
            NotifyCollectionChanged(ResetArgs);
         }
      }

      private IEnumerable<AutoCompleteSelectionItem> GetAutocompleteOptions(IDataFormat originalFormat, byte value, string newText, int selectedIndex = -1) {
         using (ModelCacheScope.CreateScope(Model)) {
            var visitor = new AutocompleteCell(Model, newText, selectedIndex);
            (originalFormat ?? Undefined.Instance).Visit(visitor, value);
            return visitor.Result;
         }
      }

      private void ClearEdits(Point point) {
         if (this[point.X, point.Y].Format is UnderEdit) RefreshBackingData();
         withinComment = false;
      }

      private Point GetEditPoint() {
         var selectionStart = scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(SelectionEnd);
         var leftEdge = Math.Min(selectionStart, selectionEnd);

         var point = scroll.DataIndexToViewPoint(leftEdge);
         scroll.ScrollToPoint(ref point);

         return point;
      }

      private void IsTextExecuted(object notUsed) {
         var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
         var left = Math.Min(selectionStart, selectionEnd);
         var length = Math.Abs(selectionEnd - selectionStart) + 1;
         var startPlaces = Model.FindPossibleTextStartingPlaces(left, length);

         // do the actual search now that we know places to start
         var foundCount = Model.ConsiderResultsAsTextRuns(() => history.CurrentChange, startPlaces);
         if (foundCount == 0) {
            OnError?.Invoke(this, "Failed to automatically find text at that location.");
         } else {
            RefreshBackingData();
         }

         RequestMenuClose?.Invoke(this, EventArgs.Empty);
      }

      private bool ShouldAcceptInput(Point point, HexElement element, char input) {
         var memoryLocation = scroll.ViewPointToDataIndex(point);

         // special cases: if there's no edit unde way, there's a few formats that can be added anywhere. Handle those first.
         if (!(element.Format is UnderEdit)) {
            if (input == ExtendArray) {
               var index = scroll.ViewPointToDataIndex(point);
               if (Model.IsAtEndOfArray(index, out var _)) return true;
            }

            if (input == AnchorStart || input == GotoMarker) {
               // anchor edits are actually 0 length
               // but lets give them 4 spaces to work with
               PrepareForMultiSpaceEdit(point, 4);
               var autoCompleteOptions = input == GotoMarker ? new AutoCompleteSelectionItem[0] : null;
               var underEdit = new UnderEdit(element.Format, input.ToString(), 4, autoCompleteOptions);
               currentView[point.X, point.Y] = new HexElement(element, underEdit);
               return true;
            }

            if (input == CommentStart) {
               var underEdit = new UnderEdit(element.Format, input.ToString());
               currentView[point.X, point.Y] = new HexElement(element, underEdit);
               withinComment = true;
               return true;
            }
         }

         // normal case: the logic for how to handle this edit depends on what format is in this cell.
         var startCellEdit = new StartCellEdit(Model, memoryLocation, input);
         element.Format.Visit(startCellEdit, element.Value);
         if (startCellEdit.NewFormat != null) {
            // if the edit provided a new format, go ahead and build a new element based on that format.
            // if no new format was provided, then the default logic in the method above will make a new UnderEdit cell if the Result is true.
            currentView[point.X, point.Y] = new HexElement(element, startCellEdit.NewFormat);
            if (startCellEdit.NewFormat.EditWidth > 1) PrepareForMultiSpaceEdit(point, startCellEdit.NewFormat.EditWidth);
         }
         return startCellEdit.Result;
      }

      private (Point start, Point end) GetSelectionSpan(Point p) {
         var index = scroll.ViewPointToDataIndex(p);
         var run = Model.GetNextRun(index);
         if (run.Start > index) return (p, p);

         (Point, Point) pair(int start, int end) => (scroll.DataIndexToViewPoint(start), scroll.DataIndexToViewPoint(end));

         var format = run.CreateDataFormat(Model, index);
         while (format is IDataFormatDecorator decorator) format = decorator.OriginalFormat;
         if (format is IDataFormatInstance instance) {
            return pair(instance.Source, instance.Source + instance.Length - 1);
         }

         if (!(run is ITableRun array)) return (p, p);

         var naturalEnd = array.Start + array.ElementCount * array.ElementLength;
         if (naturalEnd <= index) {
            return pair(naturalEnd, array.Start + array.Length - 1);
         }

         var offset = array.ConvertByteOffsetToArrayOffset(index);
         var type = array.ElementContent[offset.SegmentIndex].Type;
         if (type == ElementContentType.Pointer || type == ElementContentType.Integer || type == ElementContentType.BitArray) {
            return pair(offset.SegmentStart, offset.SegmentStart + array.ElementContent[offset.SegmentIndex].Length - 1);
         }

         return (p, p);
      }

      private void PrepareForMultiSpaceEdit(Point point, int length) {
         var index = scroll.ViewPointToDataIndex(point);
         var endIndex = index + length - 1;
         for (int i = 1; i < length; i++) {
            point = scroll.DataIndexToViewPoint(index + i);
            if (point.Y >= Height) return;
            var element = this[point.X, point.Y];
            var newFormat = element.Format.Edit(string.Empty);
            currentView[point.X, point.Y] = new HexElement(element, newFormat);
         }
         selection.PropertyChanged -= SelectionPropertyChanged; // don't notify on multi-space edit: it breaks up the undo history
         SelectionEnd = scroll.DataIndexToViewPoint(endIndex);
         selection.PropertyChanged += SelectionPropertyChanged;
      }

      private bool TryCompleteEdit(Point point) {
         // wrap this whole method in an anti-recursion clause
         selection.PreviewSelectionStartChanged -= ClearActiveEditBeforeSelectionChanges;
         using (new StubDisposable { Dispose = () => selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges }) {

            var element = this[point.X, point.Y];
            var underEdit = element.Format as UnderEdit;
            if (underEdit == null) return false; // no edit to complete

            if (TryGeneralCompleteEdit(underEdit.CurrentText, point, out bool result)) {
               return result;
            }

            // normal case: whether or not to accept the edit depends on the existing cell format
            var dataIndex = scroll.ViewPointToDataIndex(point);
            var completeEditOperation = new CompleteCellEdit(Model, scroll, dataIndex, underEdit.CurrentText, history.CurrentChange);
            using (ModelCacheScope.CreateScope(Model)) {
               (underEdit.OriginalFormat ?? Undefined.Instance).Visit(completeEditOperation, element.Value);

               if (completeEditOperation.Result) {
                  // if the data we just changed was in a table, notify children of that table about the change
                  var previousRun = Model.GetNextRun(dataIndex);
                  if (previousRun is ITableRun tableRun) {
                     var offsets = tableRun.ConvertByteOffsetToArrayOffset(dataIndex);
                     var errorInfo = tableRun.NotifyChildren(Model, history.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
                     HandleErrorInfo(errorInfo);
                  }

                  // update the cell / selection
                  if (completeEditOperation.NewCell != null) {
                     currentView[point.X, point.Y] = completeEditOperation.NewCell;
                  }
                  if (completeEditOperation.DataMoved || completeEditOperation.NewDataIndex > scroll.DataLength) {
                     scroll.DataLength = Model.Count;
                     scroll.ClearTableMode();
                  }

                  // update tools from the new moved selection
                  var run = Model.GetNextRun(completeEditOperation.NewDataIndex);
                  if (run.Start > completeEditOperation.NewDataIndex) run = new NoInfoRun(Model.Count);
                  if (completeEditOperation.DataMoved) UpdateToolsFromSelection(run.Start);
                  if (run is ITableRun || previousRun is ITableRun) {
                     Tools.Schedule(Tools.TableTool.DataForCurrentRunChanged);
                  }
                  if (run is ITableRun || run is IStreamRun || previousRun is ITableRun || previousRun is IStreamRun) Tools.Schedule(Tools.StringTool.DataForCurrentRunChanged);
                  if (run is ISpriteRun || run is IPaletteRun || previousRun is ISpriteRun || previousRun is IPaletteRun) {
                     tools.Schedule(tools.SpriteTool.DataForCurrentRunChanged);
                     tools.Schedule(tools.TableTool.DataForCurrentRunChanged);
                  }
                  if (completeEditOperation.MessageText != null) OnMessage?.Invoke(this, completeEditOperation.MessageText);
                  if (completeEditOperation.ErrorText != null) OnError?.Invoke(this, completeEditOperation.ErrorText);

                  // refresh the screen
                  RefreshBackingData(point);
                  if (!SilentScroll(completeEditOperation.NewDataIndex) && completeEditOperation.NewCell == null) {
                     RefreshBackingData();
                  }

                  tools.Schedule(UpdateToolsFromSelection);
               }
            }

            return completeEditOperation.Result;
         }
      }

      /// <summary>
      /// Some edits are valid no matter where you are in the data.
      /// Try to complete one of those edits here.
      /// Return true if it's a special edit. Result is true if the edit was completed.
      /// </summary>
      private bool TryGeneralCompleteEdit(string currentText, Point point, out bool result) {
         result = false;

         // goto marker
         if (currentText.StartsWith(GotoMarker.ToString())) {
            if (currentText.Length == 2 && currentText[1] == '{') {
               var currentAddress = scroll.ViewPointToDataIndex(point);
               var destination = Model.ReadPointer(currentAddress);
               if (destination == Pointer.NULL) {
                  if (CreateNewData(currentAddress)) {
                     destination = Model.ReadPointer(currentAddress);
                  } else {
                     OnError?.Invoke(this, $"Could not jump using pointer at {currentAddress:X6}");
                  }
               }
               ClearEdits(point);
               if (destination >= 0 && destination < Model.Count) {
                  using (ChangeHistory.ContinueCurrentTransaction()) {
                     Goto.Execute(destination);
                     selection.SetJumpBackPoint(currentAddress + 4);
                  }
               } else if (destination != Pointer.NULL) {
                  OnError?.Invoke(this, $"Could not jump using pointer at {currentAddress:X6}");
               }
               RequestMenuClose?.Invoke(this, EventArgs.Empty);
               result = true;
            } else if (currentText.Length == 2 && currentText[1] == '}') {
               ClearEdits(point);
               using (ChangeHistory.ContinueCurrentTransaction()) selection.Back.Execute();
               RequestMenuClose?.Invoke(this, EventArgs.Empty);
               result = true;
            }
            if (char.IsWhiteSpace(currentText[currentText.Length - 1])) {
               var destination = currentText.Substring(1).Trim();
               ClearEdits(point);
               if (currentText.Contains("=")) {
                  UpdateConstant(destination);
               } else {
                  var parts = destination.Split(CommandMarker);
                  if (!string.IsNullOrWhiteSpace(parts[0])) Goto.Execute(parts[0]);
                  for (int i = 1; i < parts.Length; i++) ExecuteMetacommand(parts[i]);
                  UpdateSelectedBytes();
               }
               RequestMenuClose?.Invoke(this, EventArgs.Empty);
               result = true;
            }

            return true;
         }

         // comment
         if (currentText.StartsWith(CommentStart.ToString())) {
            if (currentText.Length > 1 && currentText.EndsWith(CommentStart.ToString())) withinComment = false;
            result = (currentText.EndsWith(" ") || currentText.EndsWith("#")) && !withinComment;
            if (result) ClearEdits(point);
            return true;
         }

         // anchor start
         if (currentText.StartsWith(AnchorStart.ToString())) {
            TryUpdate(ref anchorText, currentText, nameof(AnchorText));
            var endingCharacter = currentText[currentText.Length - 1];
            // anchor format will only end once the user
            // -> types a whitespace character,
            // -> types a closing quote for the text format ""
            // -> types a closing ` for a `` format
            if (!char.IsWhiteSpace(endingCharacter) && !currentText.EndsWith(AsciiRun.StreamDelimeter.ToString()) && !currentText.EndsWith(PCSRun.StringDelimeter.ToString())) {
               AnchorTextVisible = true;
               return true;
            }

            // special case: `asc` has a length token outside the ``, so the anchor isn't completed if it ends with `asc`
            if (currentText.EndsWith("`asc`")) {
               AnchorTextVisible = true;
               return true;
            }

            // only end the anchor edit if the [] brace count matches
            if (currentText.Sum(c => c == '[' ? 1 : c == ']' ? -1 : 0) != 0) {
               AnchorTextVisible = true;
               return true;
            }

            // only end the anchor if the "" and `` quote count are even
            if (currentText.Count(AsciiRun.StreamDelimeter) % 2 != 0 || currentText.Count(StringDelimeter) % 2 != 0) {
               AnchorTextVisible = true;
               return true;
            }

            if (!CompleteAnchorEdit(point)) exitEditEarly = true;
            result = true;
            return true;
         }

         // directive marker
         var element = this[point.X, point.Y];
         var underEdit = element.Format as UnderEdit;
         if (currentText.StartsWith(DirectiveMarker.ToString()) && currentText.Count(c => c == DirectiveMarker) == 1) {
            if (underEdit.OriginalFormat is PCS || underEdit.OriginalFormat is Ascii) {
               // if we're in a text cell, don't allow directives.
            } else {
               result = CompleteDirectiveEdit(point, currentText);
               return true;
            }
         }

         // table extension
         var dataIndex = scroll.ViewPointToDataIndex(point);
         if (currentText == ExtendArray.ToString() && Model.IsAtEndOfArray(dataIndex, out var arrayRun)) {
            var originalArray = arrayRun;
            var errorInfo = Model.CompleteArrayExtension(history.CurrentChange, 1, ref arrayRun);
            if (!errorInfo.HasError || errorInfo.IsWarning) {
               if (arrayRun != null && arrayRun.Start != originalArray.Start) {
                  // refresh first to clear any active edit cells
                  RefreshBackingData(point);
                  ScrollFromTableMove(dataIndex, originalArray, arrayRun);
               }
               RefreshBackingData();
               SelectionEnd = GetSelectionSpan(SelectionStart).end;
            }
            HandleErrorInfo(errorInfo);
            result = true;
            return true;
         }

         return false;
      }

      private void UpdateConstant(string expression) {
         var parts = expression.Split('=');
         if (parts.Length != 2) {
            RaiseError("Could not parse constant assignment expression.");
            return;
         }
         if (!int.TryParse(parts[1], out var value)) {
            if (!parts[1].StartsWith("0x") || !int.TryParse(parts[1].Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value)) {
               RaiseError("Could not parse constant assignment expression.");
               return;
            }
         }

         var locations = Model.GetMatchedWords(parts[0]);
         if (locations == null || locations.Count == 0) {
            Model.SetUnmappedConstant(CurrentChange, parts[0], value);
         }

         foreach (var address in locations) {
            if (!(Model.GetNextRun(address) is WordRun currentRun)) continue;
            var writeValue = value + currentRun.ValueOffset;
            if (writeValue < 0 || writeValue > 255) {
               RaiseError($"{currentRun.Start:X6}: value out of range!");
            }
            Model.WriteMultiByteValue(address, currentRun.Length, CurrentChange, writeValue);
         }
      }

      /// <summary>
      /// Current Available metacommands:
      /// lz(1024) -> write 1024 compressed bytes. Error if we're not in freespace, do nothing if we're at lz data of the expected length already.
      /// 00(32)   -> Write 32 bytes of zero. Error if we're not in freespace (FF).
      ///             * Does not error if clearing a subset (or entire) table, so long as the clear matches a multiple of a row length. Still clears that data though.
      ///             * Does not error if clearing exactly the length of a non-table run. Don't clear the data either: all 0's may not be valid (such as with strings)
      /// put(1234)-> put the bytes 12, then 34, at the current location, but don't change the current selection.
      ///             works no matter what the current data is.
      /// </summary>
      private void ExecuteMetacommand(string command) {
         command = command.ToLower();
         var index = scroll.ViewPointToDataIndex(SelectionStart);
         var paramsStart = command.IndexOf("(");
         var paramsEnd = command.IndexOf(")");
         var length = 0;
         if (command.StartsWith("lz(") && paramsEnd > 3 && int.TryParse(command.Substring(3, paramsEnd - 3), out length)) {
            // only do the write if the current data isn't compressed data of the right length
            var existingCompressedData = LZRun.Decompress(Model, index);
            var newCompressed = LZRun.Compress(new byte[length], 0, length);

            if (existingCompressedData != null && existingCompressedData.Length == length) {
               // do nothing, it's already the right data type
            } else if (newCompressed.Count.Range().All(i => Model[index + i] == 0xFF)) {
               // data is all FF, go ahead and write
               for (int i = 0; i < newCompressed.Count; i++) CurrentChange.ChangeData(Model, index + i, newCompressed[i]);
            } else {
               // data is not freespace and is not the correct data: error
               RaiseError($"Writing {length} compressed bytes would overwrite existing data.");
               exitEditEarly = true;
            }
         } else if (command.StartsWith("00(") && paramsEnd > 3 && int.TryParse(command.Substring(3, paramsEnd - 3), out length)) {
            var currentRun = Model.GetNextRun(index);
            var tableRun = currentRun as ITableRun;
            if (tableRun != null && currentRun.Start == index && length % tableRun.ElementLength == 0 && length <= tableRun.Length) {
               // we're trying to clear out table data.
               // assume that the user wanted us to clear it.
               // do NOT do the clear if the current clear is bigger than the current table: that could wipe existing data.
               ClearPointersFromTable(tableRun, index, length);
               for (int i = 0; i < length; i++) CurrentChange.ChangeData(Model, index + i, 0);
            } else if (tableRun == null && currentRun.Start == index && currentRun.Length == length) {
               // we're trying to clear out a non-table
               // the length matches exactly, so we shouldn't error. But also, don't actually clear.
            } else if (length.Range().All(i => Model[index + i] == 0xFF)) {
               for (int i = 0; i < length; i++) CurrentChange.ChangeData(Model, index + i, 0);
            } else {
               RaiseError($"Writing {length} 00 bytes would overwrite existing data.");
               exitEditEarly = true;
            }
         } else if (command.StartsWith("game(") && paramsEnd > 5) {
            var content = command.Substring(5, paramsEnd - 5).ToLower();
            var gameCode = Model.GetGameCode().ToLower();
            if (content == "all") {
               // all good
            } else if (!content.Contains(gameCode)) {
               skipToNextGameCode = true;
            }
         } else if (command.StartsWith("put(") && paramsEnd > 4) {
            var content = command.Substring(4, paramsEnd - 4);
            if (content.Length % 2 != 0 || !content.All(AllHexCharacters.Contains)) {
               RaiseError("'put' expects hex bytes as an argument. ");
               exitEditEarly = true;
               return;
            }
            for (int i = 0; i < content.Length / 2; i++) {
               var data = byte.Parse(content.Substring(i * 2, 2), NumberStyles.HexNumber);
               CurrentChange.ChangeData(Model, index + i, data);
            }
         } else {
            RaiseError($"Could not parse metacommand {command}.");
            exitEditEarly = true;
         }
      }

      private void ClearPointersFromTable(ITableRun tableRun, int index, int length) {
         foreach (var segment in tableRun.ElementContent) {
            if (segment.Type != ElementContentType.Pointer) continue;
            var offset = tableRun.ElementContent.Until(seg => seg == segment).Sum(seg => seg.Length);
            for (int i = offset; i < length; i += tableRun.ElementLength) {
               var destination = Model.ReadPointer(tableRun.Start + i);
               Model.ClearPointer(CurrentChange, tableRun.Start + i, destination);
            }
         }
      }

      /// <returns>True if it was completed successfully, false if some sort of error occurred and we should abort the remainder of the edit.</returns>
      private bool CompleteAnchorEdit(Point point) {
         var underEdit = (UnderEdit)this[point.X, point.Y].Format;
         var index = scroll.ViewPointToDataIndex(point);
         ErrorInfo errorInfo;

         // if it's an unnamed text/stream anchor, we have special logic for that
         using (ModelCacheScope.CreateScope(Model)) {
            if (underEdit.CurrentText.Trim() == AnchorStart + PCSRun.SharedFormatString) {
               int count = Model.ConsiderResultsAsTextRuns(() => history.CurrentChange, new[] { index });
               if (count == 0) {
                  errorInfo = new ErrorInfo("An anchor with nothing pointing to it must have a name.");
               } else {
                  errorInfo = ErrorInfo.NoError;
               }
            } else if (underEdit.CurrentText == AnchorStart + PLMRun.SharedFormatString) {
               if (!PokemonModel.ConsiderAsPlmStream(Model, index, history.CurrentChange)) {
                  errorInfo = new ErrorInfo("An anchor with nothing pointing to it must have a name.");
               } else {
                  errorInfo = ErrorInfo.NoError;
                  Tools.StringTool.RefreshContentAtAddress();
               }
            } else if (underEdit.CurrentText == AnchorStart + XSERun.SharedFormatString) {
               // TODO
               CascadeScript(index);
               errorInfo = ErrorInfo.NoError;
            } else {
               errorInfo = PokemonModel.ApplyAnchor(Model, history.CurrentChange, index, underEdit.CurrentText);
               Tools.StringTool.RefreshContentAtAddress();
            }
         }

         ClearEdits(point);
         UpdateToolsFromSelection(index);

         if (errorInfo == ErrorInfo.NoError) {
            if (Model.GetNextRun(index) is ArrayRun array && array.Start == index) Goto.Execute(index.ToString("X2"));
            return true;
         }

         HandleErrorInfo(errorInfo);

         return errorInfo.IsWarning;
      }

      private bool CompleteDirectiveEdit(Point point, string currentText) {
         currentText = currentText.ToLower();

         if (!currentText.EndsWith(" ")) return false;

         if (currentText.StartsWith(".align ") && currentText.Length > 8) {
            if (!int.TryParse(currentText.Substring(7), out int value)) value = 4;
            ClearEdits(point);
            var index = scroll.ViewPointToDataIndex(point);
            if (value == 2) SelectionStart = scroll.DataIndexToViewPoint(index + point.X % 2);
            if (value == 4 && point.X % 4 != 0) SelectionStart = scroll.DataIndexToViewPoint(index + 4 - (point.X % 4));
         } else if (currentText.StartsWith(".text")) {
            ClearEdits(point);
         } else if (currentText.StartsWith(".thumb")) {
            ClearEdits(point);
         } else if (currentText.StartsWith(".python")) {
            ClearEdits(point);
         } else {
            RaiseError($"'{currentText.Substring(1).Trim()}' is not a valid directive.");
         }

         return false;
      }

      private void ScrollFromTableMove(int initialSelection, ITableRun oldRun, ITableRun newRun) {
         scroll.DataLength = Model.Count; // possible length change
         var tableOffset = scroll.DataIndex - oldRun.Start;
         var relativeSelection = initialSelection - oldRun.Start;
         selection.PropertyChanged -= SelectionPropertyChanged;
         selection.GotoAddress(newRun.Start + tableOffset);
         selection.SelectionStart = scroll.DataIndexToViewPoint(newRun.Start + relativeSelection);
         selection.PropertyChanged += SelectionPropertyChanged;
      }

      private bool SilentScroll(int memoryLocation) {
         var nextPoint = scroll.DataIndexToViewPoint(memoryLocation);
         var didScroll = true;
         if (!scroll.ScrollToPoint(ref nextPoint)) {
            // only need to notify collection change if we didn't auto-scroll after changing cells
            NotifyCollectionChanged(ResetArgs);
            didScroll = false;
         }

         UpdateSelectionWithoutNotify(nextPoint);
         return didScroll;
      }

      public void HandleErrorInfo(ErrorInfo info) {
         if (!info.HasError) return;
         if (info.IsWarning) OnMessage?.Invoke(this, info.ErrorMessage);
         else OnError?.Invoke(this, info.ErrorMessage);
      }

      /// <summary>
      /// When automatically updating the selection,
      /// update it without notifying ourselves.
      /// This lets us tell the difference between a manual cell change and an auto-cell change,
      /// which is useful for deciding change history boundaries.
      /// </summary>
      private void UpdateSelectionWithoutNotify(Point nextPoint) {
         selection.PropertyChanged -= SelectionPropertyChanged;

         SelectionStart = nextPoint;
         NotifyPropertyChanged(nameof(SelectionStart));
         NotifyPropertyChanged(nameof(SelectionEnd));

         selection.PropertyChanged += SelectionPropertyChanged;
      }

      private void ModelChangedByTool(object sender, IFormattedRun run) {
         if (run.Start < scroll.ViewPointToDataIndex(new Point(Width - 1, Height - 1)) || run.Start + run.Length > scroll.DataIndex) {
            // there's some visible data that changed
            RefreshBackingData();
            scroll.UpdateHeaders();
         }

         if (run is ITableRun && sender != Tools.StringTool && Model.GetNextRun(Tools.StringTool.Address).Start == run.Start) Tools.StringTool.DataForCurrentRunChanged();
         if (run is ITableRun && Model.GetNextRun(Tools.TableTool.Address).Start == run.Start) Tools.TableTool.DataForCurrentRunChanged();
      }

      private void ModelDataMovedByTool(object sender, (int originalLocation, int newLocation) locations) {
         scroll.DataLength = Model.Count;
         if (scroll.DataIndex <= locations.originalLocation && locations.originalLocation < scroll.ViewPointToDataIndex(new Point(Width - 1, Height - 1))) {
            // data was moved from onscreen: follow it
            int offset = locations.originalLocation - scroll.DataIndex;
            selection.GotoAddress(locations.newLocation - offset);
         }
         RaiseMessage($"Data was automatically moved to {locations.newLocation:X6}. Pointers were updated.");
      }

      private void ModelChangedByCodeTool(object sender, ErrorInfo e) {
         RefreshBackingData();
         HandleErrorInfo(e);
      }

      private void RefreshBackingData(Point p) {
         lock (threadlock) {
            var index = scroll.ViewPointToDataIndex(p);
            var edited = Model.HasChanged(index);
            if (index < 0 | index >= Model.Count) { currentView[p.X, p.Y] = HexElement.Undefined; return; }
            var run = Model.GetNextRun(index);
            if (index < run.Start) { currentView[p.X, p.Y] = new HexElement(Model[index], edited, None.Instance); return; }
            var format = run.CreateDataFormat(Model, index);
            format = Model.WrapFormat(run, format, index);
            currentView[p.X, p.Y] = new HexElement(Model[index], edited, format);
         }
      }

      private void RefreshBackingData() {
         lock (threadlock) {
            currentView = new HexElement[Width, Height];

            RequestMenuClose?.Invoke(this, EventArgs.Empty);
            NotifyCollectionChanged(ResetArgs);
            NotifyPropertyChanged(nameof(FreeSpaceStart));
         }
      }

      private void RefreshBackingDataFull() {
         lock (threadlock) {
            currentView = new HexElement[Width, Height];
            IFormattedRun run = null;
            using (ModelCacheScope.CreateScope(Model)) {
               for (int y = 0; y < Height; y++) {
                  for (int x = 0; x < Width; x++) {
                     var index = scroll.ViewPointToDataIndex(new Point(x, y));
                     var edited = Model.HasChanged(index);
                     if (run == null || index >= run.Start + run.Length) {
                        run = Model.GetNextRun(index) ?? new NoInfoRun(Model.Count);
                     }
                     if (index < scroll.DataStart || index >= scroll.DataLength) {
                        currentView[x, y] = HexElement.Undefined;
                     } else if (index >= run.Start) {
                        var format = run is BaseRun baseRun ? baseRun.CreateDataFormat(Model, index, x == 0, Width) : run.CreateDataFormat(Model, index);
                        format = Model.WrapFormat(run, format, index);
                        currentView[x, y] = new HexElement(Model[index], edited, format);
                     } else {
                        currentView[x, y] = new HexElement(Model[index], edited, None.Instance);
                     }
                  }
               }
            }
            if (FindBytes != null) {
               var fullLength = Width * Height;
               for (int i = 0; i < fullLength - FindBytes.Length - 1; i++) {
                  bool possibleMatch = FindBytes.Length > 0;
                  for (int j = 0; j < FindBytes.Length; j++) {
                     var (x, y) = ((i + j) % Width, (i + j) / Width);
                     if (currentView[x, y].Value != FindBytes[j]) {
                        possibleMatch = false;
                        break;
                     }
                  }
                  if (!possibleMatch) continue;
                  for (int j = 0; j < FindBytes.Length; j++) {
                     var (x, y) = ((i + j) % Width, (i + j) / Width);
                     if (currentView[x, y].Format is None) {
                        currentView[x, y] = new HexElement(currentView[x, y].Value, currentView[x, y].Edited, None.ResultInstance);
                     } else if (currentView[x, y].Format is Anchor anchor && anchor.OriginalFormat is None) {
                        var newWrapper = new Anchor(None.ResultInstance, anchor.Name, anchor.Format, anchor.Sources);
                        currentView[x, y] = new HexElement(currentView[x, y].Value, currentView[x, y].Edited, newWrapper);
                     }
                  }
                  i += FindBytes.Length - 1;
               }
            }
         }
      }

      private void UpdateColumnHeaders() {
         var index = scroll.ViewPointToDataIndex(new Point(0, 0));
         var run = Model.GetNextRun(index) as ArrayRun;
         if (run != null && run.Start > index) run = null; // only use the run if it starts _before_ the screen
         var headers = run?.GetColumnHeaders(Width, index) ?? HeaderRow.GetDefaultColumnHeaders(Width, index);

         for (int i = 0; i < headers.Count; i++) {
            if (i < ColumnHeaders.Count) ColumnHeaders[i] = headers[i];
            else ColumnHeaders.Add(headers[i]);
         }

         while (ColumnHeaders.Count > headers.Count) ColumnHeaders.RemoveAt(ColumnHeaders.Count - 1);
      }

      private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
   }
}
