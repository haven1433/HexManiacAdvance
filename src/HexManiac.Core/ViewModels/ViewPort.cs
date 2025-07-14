using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
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
using static HavenSoft.HexManiac.Core.ICommandExtensions;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.BaseRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PointerRun;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : ViewModelCore, IEditableViewPort {
      public const string AllHexCharacters = "0123456789ABCDEFabcdef";
      public const char GotoMarker = '@';
      public const char DirectiveMarker = '.'; // for things like .thumb, .align, etc. Directives always start with a single dot and contain no further dots until they contain a space.
      public const char CommandMarker = '!'; // commands are meta, so they also start with the goto marker.
      public const char CommentStart = '#';

      public static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

      public readonly object threadlock = new();
      public readonly IFileSystem fs;

      public MapEditorViewModel mapper;
      public bool HasValidMapper => mapper?.IsValidState ?? false;
      public MapEditorViewModel MapEditor => HasValidMapper ? mapper : null;

      public Singletons Singletons { get; }

      public PythonTool PythonTool { get; }

      public Task InitializationWorkload { get; set; }

      public bool distractionFreeMode;
      public bool DistractionFreeMode { get => distractionFreeMode; set => Set(ref distractionFreeMode, value); }

      public HexElement[,] currentView;
      public bool exitEditEarly, withinComment, skipToNextGameCode;

      public string Name {
         get {
            var name = Path.GetFileNameWithoutExtension(FileName);
            if (string.IsNullOrEmpty(name)) name = "Untitled";
            if (history.HasDataChange) name += "*";
            return name;
         }
      }

      public double ToolPanelWidth { get; set; } = 500;

      public string fileName;
      public string FileName {
         get => fileName;
         set {
            if (TryUpdate(ref fileName, value) && !string.IsNullOrEmpty(fileName)) {
               FullFileName = Path.GetFullPath(fileName);
               NotifyPropertyChanged(nameof(Name));
            }
         }
      }

      public string fullFileName;
      public string FullFileName { get => fullFileName; set => TryUpdate(ref fullFileName, value); }

      public bool spartanMode;
      public bool SpartanMode {
         get => spartanMode;
         set {
            Set(ref spartanMode, value, arg => {
               Model.SpartanMode = spartanMode;
            });
         }
      }

      #region Scrolling Properties

      public readonly ScrollRegion scroll;
      protected ScrollRegion ScrollRegion => scroll;

      public event EventHandler PreviewScrollChanged;

      public int DataStart => scroll.DataStart;

      public int DataLength => scroll.DataLength - scroll.DataStart;

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

      public ObservableCollection<RowHeader> Headers => scroll.Headers; // the headers for each row
      public ObservableCollection<ColumnHeaderRow> ColumnHeaders { get; } // the collection of all rows of column headers (for narrow dislpays, the column headers can be broken into multiple rows)
      public int DataOffset => scroll.DataIndex;

      public bool UseCustomHeaders {
         get => scroll.UseCustomHeaders;
         set { using (ModelCacheScope.CreateScope(Model)) scroll.UseCustomHeaders = value; }
      }

      public bool AllowSingleTableMode {
         get => scroll.AllowSingleTableMode;
         set => scroll.AllowSingleTableMode = value;
      }

      #endregion

      #region Selection Properties

      public readonly Selection selection;

      public bool stretchData;
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

      #region Selected Address/ElementName/Length, bottom row

      public string selectedAddress, selectedLength, selectedElementName;
      public string SelectedAddress {
         get => selectedAddress;
         set => Set(ref selectedAddress, value, SelectedAddressChanged);
      }
      public string SelectedLength {
         get => selectedLength;
         set => Set(ref selectedLength, value, SelectedLengthChanged);
      }

      public bool base10SelectionLength;
      public bool Base10SelectionLength {
         get => base10SelectionLength;
         set => Set(ref base10SelectionLength, value, arg => {
            UpdateSelectedAddress();
            NotifyPropertyChanged(nameof(Base10Length));
         });
      }
      public bool Base10Length {
         get => base10SelectionLength;
         set => Base10SelectionLength = value;
      }

      public string SelectedElementName => selectedElementName;

      public void UpdateSelectedAddress() {
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

      public void SelectedAddressChanged(string old) {
         if (!selectedAddress.TryParseHex(out int address)) return;
         SelectionStart = ConvertAddressToViewPoint(address);
      }

      public void SelectedLengthChanged(string old) {
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

      public string selectedBytes;
      public string SelectedBytes {
         get {
            if (selectedBytes != null) return selectedBytes;

            var bytes = GetSelectedByteContents(0x10);
            selectedBytes = "Selected Bytes: " + bytes;
            return selectedBytes;
         }
         set => TryUpdate(ref selectedBytes, value);
      }

      // update the selected bytes lazily. Most of the time we don't really care about the new value.
      public void UpdateSelectedBytes() => SelectedBytes = null;

      public string GetSelectedByteContents(int maxByteCount = int.MaxValue) {
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

      #endregion

      #region Undo / Redo

      public readonly ChangeHistory<ModelDelta> history;

      public ChangeHistory<ModelDelta> ChangeHistory => history;

      public ModelDelta RevertChanges(ModelDelta changes) {
         var reverse = changes.Revert(Model);
         RefreshBackingData();
         scroll.UpdateHeaders();
         return reverse;
      }

      #endregion

      #region Saving

      public bool IsMetadataOnlyChange => !history.IsSaved && !ChangeHistory.HasDataChange;

      public event EventHandler Closed;

      public GameReferenceTables RefTable => Singletons.GameReferenceTables.TryGetValue(Model.GetGameCode(), out var refTable) ? refTable : null;

      public void ExportBackupExecuted(IFileSystem fileSystem) {
         var changeDescription = fileSystem.RequestText("Export Summary", "What was your most recent change?");
         if (changeDescription == null) return;
         changeDescription = new string(changeDescription.Select(letter => char.IsLetterOrDigit(letter) ? letter : '_').ToArray());

         var exportID = Model.NextExportID;
         Model.NextExportID += 1;
         var metadata = Model.ExportMetadata(RefTable, Singletons.MetadataInfo);
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

      #endregion

      #region Progress

      public readonly IWorkDispatcher dispatcher;

      public double progress;
      public double Progress { get => progress; set => Set(ref progress, value); }

      public IDisposable holdWorkHistory;
      public bool updateInProgress;
      public bool UpdateInProgress { get => updateInProgress; set => Set(ref updateInProgress, value); }

      public int initialWorkLoad, postEditWork; // describes the amount of work to complete, measured characters. Allows for a fairly accurate loading bar.
      public readonly List<IDisposable> CurrentProgressScopes = new List<IDisposable>();

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

      public bool IsDiffPointerChange(int start, int end, IDataModel other) {
         while (start % 4 != 0) start--;
         while (end % 4 != 3) end++;
         if (end - start != 3) return false;
         if (Model.GetNextRun(start) is PointerRun pRun1 && pRun1.Start == start) return true;
         if (other.GetNextRun(start) is PointerRun pRun2 && pRun2.Start == start) return true;
         return false;
      }

      public void ExecuteDiffLeft() => RequestDiff?.Invoke(this, Direction.Left);
      public bool CanExecuteDiffLeft() {
         var args = new CanDiffEventArgs(Direction.Left);
         RequestCanDiff?.Invoke(this, args);
         return args.Result;
      }

      public void ExecuteDiffRight() => RequestDiff?.Invoke(this, Direction.Right);
      public bool CanExecuteDiffRight() {
         var args = new CanDiffEventArgs(Direction.Right);
         RequestCanDiff?.Invoke(this, args);
         return args.Result;
      }

      #endregion

      #region LaunchFileLocation

      public void LaunchFileLocation(IFileSystem fileSystem) => fileSystem.LaunchProcess("explorer.exe", $"/select,\"{FullFileName}\"");

      #endregion

      #region Duplicate

      public bool CanDuplicate => true;

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
      public bool CanFindFreeSpace => true;

      public IReadOnlyList<DocLabel> docs;
      public readonly ToolTray tools;
      public bool HasTools => tools != null;
      public IToolTrayViewModel Tools => tools;

      public bool anchorTextVisible;
      public bool AnchorTextVisible {
         get => anchorTextVisible;
         set => Set(ref anchorTextVisible, value);
      }

      public string anchorText;

      public int anchorTextSelectionStart;
      public int AnchorTextSelectionStart { get => anchorTextSelectionStart; set => Set(ref anchorTextSelectionStart, value); }

      public int anchorTextSelectionLength;
      public int AnchorTextSelectionLength { get => anchorTextSelectionLength; set => Set(ref anchorTextSelectionLength, value); }

      public bool isFocused;
      public bool IsFocused { get => isFocused; set => Set(ref isFocused, value); }

      public bool ownsHistory; // true if this tab is responsible for history ownership. False if this is a 'duplicate' tab, and another tab owns the history.

      public IDataModel Model { get; }
      public IDataModel ModelFor(Point p) => Model;
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
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler FocusToolPanel;
      public event EventHandler RequestRefreshGotoShortcuts;
#pragma warning restore 0067

      public Shortcuts Shortcuts { get; }

      #region Constructors

      public bool ignoreFurtherCommands = false;

      /// <summary>
      /// Top-level scripts may be available through metadata.
      /// Find scripts called by those scripts, and add runs for those too.
      /// </summary>
      public void CascadeScripts() {
         var noChange = new NoDataChangeDeltaModel { DoNotClearConstants = true };
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

      public static IEnumerable<IFormattedRun> Runs(IDataModel model) {
         for (var run = model.GetNextRun(0); run.Start < model.Count; run = model.GetNextRun(run.Start + Math.Max(1, run.Length))) {
            yield return run;
         }
      }

      public void CopyAddressExecute(IFileSystem fileSystem) {
         var copyText = scroll.ViewPointToDataIndex(selection.SelectionStart).ToString("X6");
         fileSystem.CopyText = copyText;
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
         OnMessage?.Invoke(this, $"'{copyText}' copied to clipboard.");
      }

      public void CopyBytesExecute(IFileSystem fileSystem) {
         var copyText = GetSelectedByteContents();
         fileSystem.CopyText = copyText;
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
         OnMessage?.Invoke(this, $"'{copyText}' copied to clipboard.");
      }

      #endregion

      public Point ConvertAddressToViewPoint(int address) => scroll.DataIndexToViewPoint(address);
      public int ConvertViewPointToAddress(Point p) => scroll.ViewPointToDataIndex(p);

      public bool IsSelected(Point point) => selection.IsSelected(point);

      public bool IsTable(Point point) {
         var search = scroll.ViewPointToDataIndex(point);
         var run = Model.GetNextRun(search);
         return run.Start <= search && run is ITableRun;
      }

      public string pathContext;

      public void RaiseError(string text) => OnError?.Invoke(this, text);

      public string deferredMessage;
      public void RaiseMessage(string text) {
         // TODO queue multiple messages.
         deferredMessage = text;
         tools.Schedule(RaiseMessage);
      }
      public void RaiseMessage() => OnMessage?.Invoke(this, deferredMessage);

      public void RaiseRequestTabChange(ITabContent tab) => RequestTabChange?.Invoke(this, new(tab));
      public void RaiseRequestTabChange(TabChangeRequestedEventArgs args) => RequestTabChange?.Invoke(this, args);

      public bool inPythonScript = false;

      public const int DefaultChunkSize = 200;
      public void ClearEditWork() {
         CurrentProgressScopes.ForEach(scope => scope.Dispose());
         CurrentProgressScopes.Clear();
         UpdateInProgress = false;
         skipToNextGameCode = false;
         pathContext = null;
         holdWorkHistory?.Dispose();
         holdWorkHistory = null;
      }

      #region Find

      public byte[] findBytes;

      public IEnumerable<(int start, int end)> FindUnquotedText(string cleanedSearchString, List<ISearchByte> searchBytes, bool matchExactCase) {
         var pcsBytes = Model.TextConverter.Convert(cleanedSearchString, out bool containsBadCharacters);
         pcsBytes.RemoveAt(pcsBytes.Count - 1); // remove the 0xFF that was added, since we're searching for a string segment instead of a whole string.

         // only search for the string if every character in the search string is allowed
         if (containsBadCharacters) yield break;

         searchBytes.AddRange(pcsBytes.Select(b => PCSSearchByte.Create(b, matchExactCase)));
         var textResults = Model.Search(searchBytes).ToList();
         Model.ConsiderResultsAsTextRuns(() => new NoTrackChange(), textResults); // don't add auto-recognized text to undo/redo
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
      public IEnumerable<(int start, int end)> FindMatchingDataResultsFromArrayElement(ArrayRun parentArray, int parentIndex) {
         var offsets = parentArray.ConvertByteOffsetToArrayOffset(parentIndex);
         var parentArrayName = Model.GetAnchorFromAddress(-1, parentArray.Start);
         if (offsets.SegmentIndex == 0 && parentArray.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) {
            var arrayUses = FindTableUsages(offsets, parentArrayName);
            var streamUses = FindStreamUsages(offsets, parentArrayName);
            return arrayUses.Concat(streamUses);
         }
         return Enumerable.Empty<(int, int)>();
      }

      public IEnumerable<(int start, int end)> FindTableUsages(ArrayOffset offsets, string parentArrayName) {
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

      public IEnumerable<(int start, int end)> FindStreamUsages(ArrayOffset offsets, string parentArrayName) {
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

      public void NotifyNumberOfResults(string rawSearch, int results) {
         if (results == 1) {
            OnMessage?.Invoke(this, $"Found only 1 match for '{rawSearch}'.");
         } else if (results > 1) {
            OnMessage?.Invoke(this, $"Found {results} matches for '{rawSearch}'.");
         }
      }

      public byte[] Parse(string content) {
         var result = new byte[content.Length / 2];
         for (int i = 0; i < result.Length; i++) {
            var thisByte = content.Substring(i * 2, 2);
            result[i] += (byte)(AllHexCharacters.IndexOf(thisByte[0]) * 0x10);
            result[i] += (byte)AllHexCharacters.IndexOf(thisByte[1]);
         }
         return result;
      }

      public bool TryParseStringSearchSegment(List<ISearchByte> searchBytes, string cleanedSearchString, ref int i) {
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

      public void ConsiderReload(IFileSystem fileSystem) {
         if (!history.IsSaved) return; // don't overwrite local changes

         void action() {
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
         };

         if (fileSystem is IWorkDispatcher dispatcher) {
            dispatcher.BlockOnUIWork(action);
         } else {
            action();
         }
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

      public List<ViewPort> RecentDuplicates = new();

      public Point GetEditPoint() {
         var selectionStart = scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(SelectionEnd);
         var leftEdge = Math.Min(selectionStart, selectionEnd);

         var point = scroll.DataIndexToViewPoint(leftEdge);
         scroll.ScrollToPoint(ref point);

         return point;
      }

      public void AbortScript() => exitEditEarly |= UpdateInProgress;

      public void HandleErrorInfo(ErrorInfo info) {
         if (!info.HasError) return;
         if (info.IsWarning) OnMessage?.Invoke(this, info.ErrorMessage);
         else OnError?.Invoke(this, info.ErrorMessage);
      }

      public void ModelChangedByCodeTool(object sender, ErrorInfo e) {
         RefreshBackingData();
         HandleErrorInfo(e);
      }

      public void RefreshBackingData() {
         lock (threadlock) {
            currentView = new HexElement[Width, Height];

            RequestMenuClose?.Invoke(this, EventArgs.Empty);
            NotifyCollectionChanged(ResetArgs);
            NotifyPropertyChanged(nameof(FreeSpaceStart));
         }
      }

      public void UpdateColumnHeaders() {
         var index = scroll.ViewPointToDataIndex(new Point(0, 0));
         var run = Model.GetNextRun(index) as ArrayRun;
         if (run != null && run.Start > index) run = null; // only use the run if it starts _before_ the screen
         var headers = run?.GetColumnHeaders(Width, index) ?? ColumnHeaderRow.GetDefaultColumnHeaders(Width, index);

         for (int i = 0; i < headers.Count; i++) {
            if (i < ColumnHeaders.Count) ColumnHeaders[i] = headers[i];
            else ColumnHeaders.Add(headers[i]);
         }

         while (ColumnHeaders.Count > headers.Count) ColumnHeaders.RemoveAt(ColumnHeaders.Count - 1);

         UpdateColumnHeaderSelection();
      }

      public void UpdateColumnHeaderSelection() {
         if (ColumnHeaders.Count == 1 && SelectionStart.Y == SelectionEnd.Y) {
            var (left, right) = (SelectionStart.X, SelectionEnd.X);
            if (left > right) (left, right) = (right, left);
            int offset = 0;
            for (int i = 0; i < ColumnHeaders[0].ColumnHeaders.Count; i++) {
               ColumnHeaders[0].ColumnHeaders[i].IsSelected = left <= offset && offset <= right;
               offset += ColumnHeaders[0].ColumnHeaders[i].ByteWidth;
            }
         } else {
            for (int i = 0; i < ColumnHeaders.Count; i++) {
               for (int j = 0; j < ColumnHeaders[i].ColumnHeaders.Count; j++) {
                  ColumnHeaders[i].ColumnHeaders[j].IsSelected = false;
               }
            }
         }
      }

      public void OpenLink(string link) => NativeProcess.Start(link);

      public void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
   }
}
