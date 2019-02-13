using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.AsciiRun;
using static HavenSoft.HexManiac.Core.Models.Runs.BaseRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PointerRun;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : ViewModelCore, IViewPort {
      private const char GotoMarker = '@';
      private const string AllHexCharacters = "0123456789ABCDEFabcdef";
      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      private readonly StubCommand
         clear = new StubCommand(),
         copy = new StubCommand();

      private HexElement[,] currentView;
      private bool exitEditEarly;

      public string Name {
         get {
            var name = Path.GetFileNameWithoutExtension(FileName);
            if (string.IsNullOrEmpty(name)) name = "Untitled";
            if (!history.IsSaved) name += "*";
            return name;
         }
      }

      private string fileName;
      public string FileName { get => fileName; private set => TryUpdate(ref fileName, value); }

      #region Scrolling Properties

      private readonly ScrollRegion scroll;

      public int Width {
         get => scroll.Width;
         set => selection.ChangeWidth(value);
      }

      public int Height {
         get => scroll.Height;
         set => scroll.Height = value;
      }

      public int MinimumScroll => scroll.MinimumScroll;

      public int ScrollValue {
         get => scroll.ScrollValue;
         set => scroll.ScrollValue = value;
      }

      public int MaximumScroll => scroll.MaximumScroll;

      public ObservableCollection<string> Headers => scroll.Headers;
      public int DataOffset => scroll.DataIndex;
      public ICommand Scroll => scroll.Scroll;

      private void ScrollPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(scroll.DataIndex)) {
            RefreshBackingData();
         } else if (e.PropertyName != nameof(scroll.DataLength)) {
            NotifyPropertyChanged(e.PropertyName);
         }

         if (e.PropertyName == nameof(Width) || e.PropertyName == nameof(Height)) {
            RefreshBackingData();
         }
      }

      #endregion

      #region Selection Properties

      private readonly Selection selection;

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

      public ICommand MoveSelectionStart => selection.MoveSelectionStart;
      public ICommand MoveSelectionEnd => selection.MoveSelectionEnd;
      public ICommand Goto => selection.Goto;
      public ICommand Back => selection.Back;
      public ICommand Forward => selection.Forward;

      private void ClearActiveEditBeforeSelectionChanges(object sender, Point location) {
         if (location.X >= 0 && location.X < scroll.Width && location.Y >= 0 && location.Y < scroll.Height) {
            ClearEdits(location);
         }
      }

      private void SelectionPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(SelectionEnd)) history.ChangeCompleted();
         NotifyPropertyChanged(e.PropertyName);
         var dataIndex = scroll.ViewPointToDataIndex(SelectionStart);
         UpdateToolsFromSelection(dataIndex);
      }

      private void UpdateToolsFromSelection(int dataIndex) {
         var run = Model.GetNextRun(dataIndex);

         if (run.Start <= dataIndex && run is PCSRun) Tools.StringTool.Address = run.Start;

         if (run.Start <= dataIndex && run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(dataIndex);
            Tools.StringTool.Address = offsets.SegmentStart - offsets.ElementIndex * array.ElementLength;
         }

         if (this[SelectionStart].Format is Anchor anchor) {
            TryUpdate(ref anchorText, AnchorStart + anchor.Name + anchor.Format, nameof(AnchorText));
            AnchorTextVisible = true;
         } else {
            AnchorTextVisible = false;
         }
      }

      #endregion

      #region Undo / Redo

      private readonly ChangeHistory<ModelDelta> history;

      public ICommand Undo => history.Undo;

      public ICommand Redo => history.Redo;

      private ModelDelta RevertChanges(ModelDelta changes) {
         var reverse = changes.Revert(Model);
         var point = scroll.DataIndexToViewPoint(reverse.EarliestChange);
         if (!scroll.ScrollToPoint(ref point)) RefreshBackingData();
         return reverse;
      }

      private void HistoryPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName != nameof(history.IsSaved)) return;
         save.CanExecuteChanged.Invoke(save, EventArgs.Empty);
         NotifyPropertyChanged(nameof(Name));
      }

      #endregion

      #region Saving

      private readonly StubCommand
         save = new StubCommand(),
         saveAs = new StubCommand(),
         close = new StubCommand();

      public ICommand Save => save;

      public ICommand SaveAs => saveAs;

      public ICommand Close => close;

      public event EventHandler Closed;

      private void SaveExecuted(IFileSystem fileSystem) {
         if (history.IsSaved) return;

         if (string.IsNullOrEmpty(FileName)) {
            SaveAsExecuted(fileSystem);
            return;
         }

         var metadata = Model.ExportMetadata();
         if (fileSystem.Save(new LoadedFile(FileName, Model.RawData), metadata)) history.TagAsSaved();
      }

      private void SaveAsExecuted(IFileSystem fileSystem) {
         var newName = fileSystem.RequestNewName(FileName, "GameBoy Advanced", "gba");
         if (newName == null) return;

         var metadata = Model.ExportMetadata();
         if (fileSystem.Save(new LoadedFile(newName, Model.RawData), metadata)) {
            FileName = newName; // don't bother notifying, because tagging the history will cause a notify;
            history.TagAsSaved();
         }
      }

      private void CloseExecuted(IFileSystem fileSystem) {
         if (!history.IsSaved) {
            var metadata = Model.ExportMetadata();
            var result = fileSystem.TrySavePrompt(new LoadedFile(FileName, Model.RawData), metadata);
            if (result == null) return;
         }
         Closed?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      public bool HasTools => true;
      public IToolTrayViewModel Tools { get; }

      private bool anchorTextVisible;
      public bool AnchorTextVisible {
         get => anchorTextVisible;
         set => TryUpdate(ref anchorTextVisible, value);
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
               if (run.Start == index) {
                  var errorInfo = PokemonModel.ApplyAnchor(Model, history.CurrentChange, index, AnchorText);
                  if (errorInfo == ErrorInfo.NoError) {
                     OnError?.Invoke(this, string.Empty);
                     RefreshBackingData();
                  } else {
                     OnError?.Invoke(this, errorInfo.ErrorMessage);
                  }
               }
            }
         }
      }

      public ICommand Copy => copy;
      public ICommand Clear => clear;

      public HexElement this[Point p] => this[p.X, p.Y];

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || x >= Width) return HexElement.Undefined;
            if (y < 0 || y >= Height) return HexElement.Undefined;
            return currentView[x, y];
         }
      }

      public IDataModel Model { get; private set; }

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
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
#pragma warning restore 0067

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(string fileName, IDataModel model) {
         history = new ChangeHistory<ModelDelta>(RevertChanges);
         history.PropertyChanged += HistoryPropertyChanged;

         Model = model;
         FileName = fileName;

         scroll = new ScrollRegion { DataLength = Model.Count };
         scroll.PropertyChanged += ScrollPropertyChanged;

         selection = new Selection(scroll, Model);
         selection.PropertyChanged += SelectionPropertyChanged;
         selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges;
         selection.OnError += (sender, e) => OnError?.Invoke(this, e);

         Tools = new ToolTray(Model, selection, history);
         Tools.StringTool.ModelDataChanged += ModelChangedByTool;
         Tools.StringTool.ModelDataMoved += ModelDataMovedByTool;

         ImplementCommands();
         RefreshBackingData();
      }

      public ViewPort(LoadedFile file) : this(file.Name, new BasicModel(file.Contents)) { }

      private void ImplementCommands() {
         clear.CanExecute = CanAlwaysExecute;
         clear.Execute = arg => {
            var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
            var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
            var left = Math.Min(selectionStart, selectionEnd);
            var right = Math.Max(selectionStart, selectionEnd);
            Model.ClearFormatAndData(history.CurrentChange, left, right - left + 1);
            RefreshBackingData();
         };

         copy.CanExecute = CanAlwaysExecute;
         copy.Execute = arg => {
            var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
            var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
            var left = Math.Min(selectionStart, selectionEnd);
            var length = Math.Abs(selectionEnd - selectionStart) + 1;
            ((IFileSystem)arg).CopyText = Model.Copy(left, length);
         };

         save.CanExecute = arg => !history.IsSaved;
         save.Execute = arg => SaveExecuted((IFileSystem)arg);

         saveAs.CanExecute = CanAlwaysExecute;
         saveAs.Execute = arg => SaveAsExecuted((IFileSystem)arg);

         close.CanExecute = CanAlwaysExecute;
         close.Execute = arg => CloseExecuted((IFileSystem)arg);
      }

      public Point ConvertAddressToViewPoint(int address) => scroll.DataIndexToViewPoint(address);

      public bool IsSelected(Point point) => selection.IsSelected(point);

      public void ClearFormat() {
         var startDataIndex = scroll.ViewPointToDataIndex(SelectionStart);
         var endDataIndex = scroll.ViewPointToDataIndex(SelectionEnd);
         if (startDataIndex > endDataIndex) (startDataIndex, endDataIndex) = (endDataIndex, startDataIndex);

         Model.ClearFormat(history.CurrentChange, startDataIndex, endDataIndex - startDataIndex + 1);
         RefreshBackingData();
      }

      public void Edit(string input) {
         exitEditEarly = false;
         using (Tools.DeferUpdates) {
            for (int i = 0; i < input.Length && !exitEditEarly; i++) Edit(input[i]);
         }
      }

      public void Edit(ConsoleKey key) {
         var offset = scroll.ViewPointToDataIndex(GetEditPoint());
         var run = Model.GetNextRun(offset);
         if (key == ConsoleKey.Enter && run is ArrayRun arrayRun1) {
            var offsets = arrayRun1.ConvertByteOffsetToArrayOffset(offset);
            SilentScroll(offsets.SegmentStart + arrayRun1.ElementLength);
         }
         if (key == ConsoleKey.Tab && run is ArrayRun arrayRun2) {
            var offsets = arrayRun2.ConvertByteOffsetToArrayOffset(offset);
            SilentScroll(offsets.SegmentStart + arrayRun2.ElementContent[offsets.SegmentIndex].Length);
         }
         if (key == ConsoleKey.Escape) {
            ClearEdits(SelectionStart);
            ClearMessage?.Invoke(this, EventArgs.Empty);
            RequestMenuClose?.Invoke(this, EventArgs.Empty);
         }
         if (key != ConsoleKey.Backspace) return;

         var point = GetEditPoint();
         var underEdit = currentView[point.X, point.Y].Format as UnderEdit;

         if (underEdit != null && underEdit.CurrentText.Length > 0) {
            var newFormat = new UnderEdit(underEdit.OriginalFormat, underEdit.CurrentText.Substring(0, underEdit.CurrentText.Length - 1));
            currentView[point.X, point.Y] = new HexElement(currentView[point.X, point.Y].Value, newFormat);
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         var index = scroll.ViewPointToDataIndex(point);

         // if there's an open edit, clear the data from those cells
         if (underEdit != null) {
            var operation = new DataClear(Model, history.CurrentChange, index);
            underEdit.OriginalFormat.Visit(operation, Model[index]);
            RefreshBackingData();
         }

         run = Model.GetNextRun(index - 1);
         if (run is PCSRun pcs) {
            for (int i = index - 1; i < run.Start + run.Length; i++) history.CurrentChange.ChangeData(Model, i, 0xFF);
            var length = PCSString.ReadString(Model, run.Start, true);
            Model.ObserveRunWritten(history.CurrentChange, new PCSRun(run.Start, length, run.PointerSources));
            RefreshBackingData();
            SilentScroll(index - 1);
         } else if (run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(index - 1);
            if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) {
               for (int i = index - 1; i < offsets.SegmentStart + array.ElementContent[offsets.SegmentIndex].Length; i++) history.CurrentChange.ChangeData(Model, i, 0x00);
               history.CurrentChange.ChangeData(Model, index - 1, 0xFF);
               RefreshBackingData();
               SilentScroll(index - 1);
            } else {
               throw new NotImplementedException();
            }
         } else if (run.Start <= index - 1 && run.Start + run.Length > index - 1) {
            // I want to do a backspace at the end of this run
            SelectionStart = scroll.DataIndexToViewPoint(run.Start);
            var cellToText = new ConvertCellToText(Model, run.Start);
            var element = currentView[SelectionStart.X, SelectionStart.Y];
            element.Format.Visit(cellToText, element.Value);
            var text = cellToText.Result;
            for (int i = 0; i < run.Length; i++) {
               var p = scroll.DataIndexToViewPoint(run.Start + i);
               string editString = i == 0 ? text.Substring(0, text.Length - 1) : string.Empty;
               currentView[p.X, p.Y] = new HexElement(currentView[p.X, p.Y].Value, currentView[p.X, p.Y].Format.Edit(editString));
            }
         } else {
            SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            var element = currentView[SelectionStart.X, SelectionStart.Y];
            var text = element.Value.ToString("X2");
            currentView[SelectionStart.X, SelectionStart.Y] = new HexElement(element.Value, element.Format.Edit(text.Substring(0, text.Length - 1)));
         }
      }

      #region Find

      public IReadOnlyList<(int start, int end)> Find(string rawSearch) {
         var results = new List<(int start, int end)>();
         var cleanedSearchString = rawSearch.ToUpper();
         var searchBytes = new List<ISearchByte>();

         // it might be a string with no quotes, we should check for matches for that.
         if (cleanedSearchString.Length > 3 && !cleanedSearchString.Contains(StringDelimeter) && !cleanedSearchString.All(AllHexCharacters.Contains)) {
            var pcsBytes = PCSString.Convert(cleanedSearchString);
            pcsBytes.RemoveAt(pcsBytes.Count - 1); // remove the 0xFF that was added, since we're searching for a string segment instead of a whole string.
            searchBytes.AddRange(pcsBytes.Select(b => new PCSSearchByte(b)));
            var textResults = Search(searchBytes).ToList();
            ConsiderResultsAsTextRuns(textResults);
            results.AddRange(textResults.Select(result => (result, result + pcsBytes.Count - 1)));
         }

         // it might be a pointer without angle braces
         if (cleanedSearchString.Length == 6 && cleanedSearchString.All(AllHexCharacters.Contains)) {
            searchBytes.AddRange(Parse(cleanedSearchString).Reverse().Append((byte)0x08).Select(b => (SearchByte)b));
            results.AddRange(Search(searchBytes).Select(result => (result, result + 3)));
         }

         // attempt to parse the search string fully
         if (!TryParseSearchString(searchBytes, cleanedSearchString, errorOnParseError: results.Count == 0)) return results;

         // find matches
         results.AddRange(Search(searchBytes).Select(result => (result, result + searchBytes.Count - 1)));

         // reorder the list to start at the current cursor position
         results.Sort();
         var offset = scroll.ViewPointToDataIndex(SelectionStart);
         var left = results.Where(result => result.start < offset);
         var right = results.Where(result => result.start >= offset);
         results = right.Concat(left).ToList();
         if (results.Count == 1) {
            OnMessage?.Invoke(this, $"Found only 1 match for '{rawSearch}'.");
         } else {
            OnMessage?.Invoke(this, $"Found {results.Count} matches for '{rawSearch}'.");
         }
         return results;
      }

      // for each of the results, we recognized it as text: see if we need to add a matching string run / pointers
      private int ConsiderResultsAsTextRuns(IEnumerable<int> searchResults) {
         int resultsRecognizedAsTextRuns = 0;

         foreach (var result in searchResults) {
            var nextRun = Model.GetNextRun(result);
            if (nextRun.Start <= result) continue;
            var pointers = Model.SearchForPointersToAnchor(history.CurrentChange, result);
            if (pointers.Count == 0) continue;
            var newRun = new PCSRun(result, PCSString.ReadString(Model, result, true), pointers);
            if (newRun.Length < 1) continue;
            if (newRun.Start + newRun.Length > nextRun.Start) continue;
            Model.ObserveAnchorWritten(history.CurrentChange, string.Empty, newRun);
            resultsRecognizedAsTextRuns++;
         }

         return resultsRecognizedAsTextRuns;
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

      private IEnumerable<int> Search(IList<ISearchByte> searchBytes) {
         for (int i = 0; i < Model.Count - searchBytes.Count; i++) {
            for (int j = 0; j < searchBytes.Count; j++) {
               if (!searchBytes[j].Match(Model[i + j])) break;
               if (j == searchBytes.Count - 1) yield return i;
            }
         }
         searchBytes.Clear();
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

            if (cleanedSearchString.Length >= i + 2 && cleanedSearchString.Substring(i, 2).All(AllHexCharacters.Contains)) {
               searchBytes.AddRange(Parse(cleanedSearchString.Substring(i, 2)).Select(b => (SearchByte)b));
               i += 2;
               continue;
            }

            if (errorOnParseError) OnError(this, $"Could not parse search term {cleanedSearchString.Substring(i)}");
            return false;
         }

         return true;
      }

      private bool TryParsePointerSearchSegment(List<ISearchByte> searchBytes, string cleanedSearchString, ref int i) {
         var pointerEnd = cleanedSearchString.IndexOf(PointerEnd, i);
         if (pointerEnd == -1) { OnError(this, "Search mismatch: no closing >"); return false; }
         var pointerContents = cleanedSearchString.Substring(i + 1, pointerEnd - i - 2);
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
            var pcsBytes = PCSString.Convert(cleanedSearchString.Substring(i, endIndex + 1 - i));
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
         var child = new ChildViewPort(this);
         child.Goto.Execute(startAddress.ToString("X2"));
         child.SelectionEnd = child.ConvertAddressToViewPoint(endAddress);
         return child;
      }

      public void FollowLink(int x, int y) {
         var format = currentView[x, y].Format;
         if (format is Anchor anchor) format = anchor.OriginalFormat;
         if (format is Pointer pointer) {
            if (pointer.Destination != Pointer.NULL) {
               selection.GotoAddress(pointer.Destination);
            } else if (string.IsNullOrEmpty(pointer.DestinationName)) {
               OnError(this, $"null pointers point to nothing, so going to their source isn't possible.");
            } else {
               OnError(this, $"Pointer destination {pointer.DestinationName} not found.");
            }
         }
         if (format is PCS pcs) {
            var byteOffset = scroll.ViewPointToDataIndex(new Point(x, y));
            var currentRun = Model.GetNextRun(byteOffset);
            if (currentRun is PCSRun) {
               Tools.StringTool.Address = currentRun.Start;
            } else if (currentRun is ArrayRun array) {
               var offsets = array.ConvertByteOffsetToArrayOffset(byteOffset);
               Tools.StringTool.Address = offsets.SegmentStart - offsets.ElementIndex * array.ElementLength;
            } else {
               throw new NotImplementedException();
            }
            Tools.SelectedIndex = Enumerable.Range(0, Tools.Count).First(i => Tools[i] is PCSTool);
         }
      }

      public void ExpandSelection(int x, int y) {
         var index = scroll.ViewPointToDataIndex(SelectionStart);
         var run = Model.GetNextRun(index);
         if (run.Start > index) return;
         SelectionStart = scroll.DataIndexToViewPoint(run.Start);
         SelectionEnd = scroll.DataIndexToViewPoint(run.Start + run.Length - 1);
      }

      public void ConsiderReload(IFileSystem fileSystem) {
         if (!history.IsSaved) return; // don't overwrite local changes

         try {
            var file = fileSystem.LoadFile(FileName);
            if (file == null) return; // asked to load the file, but the file wasn't found... carry on
            Model.Load(file.Contents, fileSystem.MetadataFor(FileName));
            scroll.DataLength = Model.Count;
            RefreshBackingData();

            // if the new file is shorter, selection might need to be updated
            // this forces it to be re-evaluated.
            SelectionStart = SelectionStart;
         } catch (IOException) {
            // something happened when we tried to load the file
            // try again soon.
            RequestDelayedWork?.Invoke(this, () => ConsiderReload(fileSystem));
         }
      }

      public virtual void FindAllSources(int x, int y) {
         var anchor = currentView[x, y].Format as DataFormats.Anchor;
         if (anchor == null) return;
         var title = string.IsNullOrEmpty(anchor.Name) ? (y * Width + x + scroll.DataIndex).ToString("X6") : anchor.Name;
         title = "Sources of " + title;
         var newTab = new SearchResultsViewPort(title);

         foreach (var source in anchor.Sources) newTab.Add(CreateChildView(source, source), source, source);

         RequestTabChange(this, newTab);
      }

      private void Edit(char input) {
         var point = GetEditPoint();
         var element = currentView[point.X, point.Y];

         if (!ShouldAcceptInput(ref point, ref element, input)) {
            ClearEdits(point);
            return;
         }

         SelectionStart = point;

         if (element == currentView[point.X, point.Y]) {
            var newFormat = element.Format.Edit(input.ToString());
            currentView[point.X, point.Y] = new HexElement(element.Value, newFormat);
         } else {
            // ShouldAcceptInput already did the work: nothing to change
         }

         if (!TryCompleteEdit(point)) {
            // only need to notify collection changes if we didn't complete an edit
            NotifyCollectionChanged(ResetArgs);
         }
      }

      private void ClearEdits(Point point) {
         if (currentView[point.X, point.Y].Format is UnderEdit) RefreshBackingData();
      }

      private Point GetEditPoint() {
         var selectionStart = scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(SelectionEnd);
         var leftEdge = Math.Min(selectionStart, selectionEnd);

         var point = scroll.DataIndexToViewPoint(leftEdge);
         scroll.ScrollToPoint(ref point);

         return point;
      }

      private bool ShouldAcceptInput(ref Point point, ref HexElement element, char input) {
         var underEdit = element.Format as UnderEdit;
         var innerFormat = underEdit?.OriginalFormat ?? element.Format;
         if (innerFormat is Anchor) innerFormat = ((Anchor)innerFormat).OriginalFormat;

         if (underEdit == null) {
            if (input == ExtendArray) {
               var index = scroll.ViewPointToDataIndex(point);
               return Model.IsAtEndOfArray(index, out var _);
            }

            if (input == AnchorStart) {
               // anchor edits are actually 0 length
               // but lets give them 4 spaces to work with
               PrepareForMultiSpaceEdit(point, 4);
               if (element.Format is Anchor anchor) {
                  underEdit = new UnderEdit(anchor, AnchorStart.ToString());
                  currentView[point.X, point.Y] = new HexElement(element.Value, underEdit);
               }
               return true;
            }

            if (input == GotoMarker) {
               PrepareForMultiSpaceEdit(point, 4);
               underEdit = new UnderEdit(element.Format, GotoMarker.ToString());
               currentView[point.X, point.Y] = new HexElement(element.Value, underEdit);
               return true;
            }

            if (innerFormat is PCS) {
               return input == StringDelimeter || PCSString.PCS.Any(str => str != null && str.StartsWith(input.ToString()));
            }

            if (innerFormat is EscapedPCS) return AllHexCharacters.Contains(input);

            if (innerFormat is Ascii) return true;

            if (innerFormat is Integer) return char.IsNumber(input) || char.IsWhiteSpace(input);

            // for pointers in array, don't accept anything but a pointer start
            if (innerFormat is Pointer) {
               var index = scroll.ViewPointToDataIndex(point);
               var run = Model.GetNextRun(index);
               if (run.Start <= index && run is ArrayRun array) {
                  if (input != PointerStart) return false;
               }
            }

            if (input == PointerStart) {
               // pointer edits are 4 bytes long
               if (!TryCoerceSelectionToStartOfPointer(ref point, ref element)) PrepareForMultiSpaceEdit(point, 4);
               return true;
            }
         } else if (underEdit.CurrentText.StartsWith(PointerStart.ToString())) {
            return char.IsLetterOrDigit(input) || input == PointerEnd;
         } else if (underEdit.CurrentText.StartsWith(GotoMarker.ToString())) {
            return char.IsLetterOrDigit(input) || char.IsWhiteSpace(input);
         } else if (underEdit.CurrentText.StartsWith(AnchorStart.ToString())) {
            return
               char.IsLetterOrDigit(input) ||
               char.IsWhiteSpace(input) ||
               input == ArrayStart ||
               input == ArrayEnd ||
               input == StringDelimeter ||
               input == StreamDelimeter ||
               input == PointerStart ||
               input == PointerEnd ||
               input == SingleByteIntegerFormat ||
               input == DoubleByteIntegerFormat;
         } else if (underEdit.OriginalFormat is Anchor && innerFormat is PCS) {
            if (input == StringDelimeter) return true;
            // if this is the start of a string (as noted by the anchor), crop off the leading " before trying to convert to a byte
            var currentText = underEdit.CurrentText;
            if (currentText.StartsWith(StringDelimeter.ToString())) currentText = currentText.Substring(1);
            return PCSString.PCS.Any(str => str != null && str.StartsWith(currentText + input));
         } else if (innerFormat is PCS) {
            if (input == StringDelimeter) return true;
            var memoryLocation = scroll.ViewPointToDataIndex(point);
            var currentText = underEdit.CurrentText;
            // if this is the start of an array string segment, crop off the leading " before trying to convert to a byte
            if (Model.GetNextRun(memoryLocation) is ArrayRun array) {
               var offsets = array.ConvertByteOffsetToArrayOffset(memoryLocation);
               if (offsets.SegmentStart == memoryLocation) {
                  if (currentText.StartsWith(StringDelimeter.ToString())) currentText = currentText.Substring(1);
               }
            }
            return PCSString.PCS.Any(str => str != null && str.StartsWith(currentText + input));
         } else if (innerFormat is Integer) {
            return char.IsNumber(input) || char.IsWhiteSpace(input);
         }

         if (AllHexCharacters.Contains(input)) {
            // if we're trying to write standard data over a pointer, allow that, but you must start at the first byte
            TryCoerceSelectionToStartOfPointer(ref point, ref element);
            return true;
         }

         return false;
      }

      private bool TryCoerceSelectionToStartOfPointer(ref Point point, ref HexElement element) {
         if (element.Format is Pointer pointer) {
            point = scroll.DataIndexToViewPoint(scroll.ViewPointToDataIndex(point) - pointer.Position);
            element = this[point];
            UpdateSelectionWithoutNotify(point);
            PrepareForMultiSpaceEdit(point, 4);
            return true;
         }

         return false;
      }

      private void PrepareForMultiSpaceEdit(Point point, int length) {
         var index = scroll.ViewPointToDataIndex(point);
         var endIndex = index + length - 1;
         for (int i = 1; i < length; i++) {
            point = scroll.DataIndexToViewPoint(index + i);
            if (point.Y >= Height) return;
            var element = currentView[point.X, point.Y];
            var newFormat = element.Format.Edit(string.Empty);
            currentView[point.X, point.Y] = new HexElement(element.Value, newFormat);
         }
         selection.PropertyChanged -= SelectionPropertyChanged; // don't notify on multi-space edit: it breaks up the undo history
         SelectionEnd = scroll.DataIndexToViewPoint(endIndex);
         selection.PropertyChanged += SelectionPropertyChanged;
      }

      private bool TryCompleteEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         if (underEdit.CurrentText.StartsWith(PointerStart.ToString())) {
            if (!underEdit.CurrentText.EndsWith(PointerEnd.ToString())) return false;
            CompletePointerEdit(point);
            return true;
         }
         if (underEdit.CurrentText.StartsWith(GotoMarker.ToString())) {
            if (char.IsWhiteSpace(underEdit.CurrentText[underEdit.CurrentText.Length - 1])) {
               var destination = underEdit.CurrentText.Substring(1);
               ClearEdits(point);
               Goto.Execute(destination);
               return true;
            } else {
               return false;
            }
         }
         if (underEdit.CurrentText.StartsWith(AnchorStart.ToString())) {
            TryUpdate(ref anchorText, underEdit.CurrentText, nameof(AnchorText));
            if (!char.IsWhiteSpace(underEdit.CurrentText[underEdit.CurrentText.Length - 1])) {
               AnchorTextVisible = true;
               return false;
            }

            // only end the anchor edit if the [] brace count matches
            if (underEdit.CurrentText.Sum(c => c == '[' ? 1 : c == ']' ? -1 : 0) != 0) {
               AnchorTextVisible = true;
               return false;
            }

            if (!CompleteAnchorEdit(point)) exitEditEarly = true;
            return true;
         }
         var dataIndex = scroll.ViewPointToDataIndex(point);
         if (underEdit.CurrentText == ExtendArray.ToString() && Model.IsAtEndOfArray(dataIndex, out var arrayRun)) {
            CompleteArrayExtension(arrayRun);
            return true;
         }

         var originalFormat = underEdit.OriginalFormat;
         if (originalFormat is Anchor) originalFormat = ((Anchor)originalFormat).OriginalFormat;
         if (originalFormat is Ascii) {
            CompleteAsciiEdit(point, underEdit.CurrentText);
            return true;
         } else if (originalFormat is PCS stringFormat) {
            var currentText = underEdit.CurrentText;
            if (currentText.StartsWith(StringDelimeter.ToString())) currentText = currentText.Substring(1);
            if (stringFormat.Position != 0 && underEdit.CurrentText == StringDelimeter.ToString()) {
               CompleteStringEdit(point);
               return true;
            } else if (PCSString.PCS.Any(str => str == currentText)) {
               CompleteCharacterEdit(point);
               return true;
            }

            return false;
         } else if (originalFormat is EscapedPCS escaped) {
            if (underEdit.CurrentText.Length < 2) return false;
            CompleteCharacterEdit(point);
            return true;
         } else if (originalFormat is Integer integer) {
            var currentText = underEdit.CurrentText;
            if (char.IsWhiteSpace(currentText.Last())) {
               CompleteIntegerEdit(point, currentText);
               return true;
            }
            return false;
         }

         if (underEdit.CurrentText.Length < 2) return false;
         CompleteHexEdit(point);
         return true;
      }

      private void CompleteIntegerEdit(Point point, string currentText) {
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         var editFormat = (UnderEdit)currentView[point.X, point.Y].Format;

         var integer = (Integer)(editFormat.OriginalFormat is Anchor anchor ? anchor.OriginalFormat : editFormat.OriginalFormat);
         if (!int.TryParse(currentText, out var result)) {
            OnError?.Invoke(this, $"Could not parse {integer} as a number");
            return;
         }

         var run = (ArrayRun)Model.GetNextRun(memoryLocation);
         var offsets = run.ConvertByteOffsetToArrayOffset(memoryLocation);
         int length = run.ElementContent[offsets.SegmentIndex].Length;
         for (int i = 0; i < length; i++) {
            history.CurrentChange.ChangeData(Model, offsets.SegmentStart + i, (byte)result);
            result /= 0x100;
         }
         if (result != 0) OnError?.Invoke(this, $"Warning: number was too big to fit in the available space.");
         if (!SilentScroll(offsets.SegmentStart + length)) ClearEdits(point);
      }

      private void CompleteAsciiEdit(Point point, string currentText) {
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         var editFormat = (UnderEdit)currentView[point.X, point.Y].Format;
         var asciiFormat = (Ascii)editFormat.OriginalFormat;
         var content = (byte)currentText[0];

         history.CurrentChange.ChangeData(Model, memoryLocation, content);
         currentView[point.X, point.Y] = new HexElement(content, new Ascii(asciiFormat.Source, asciiFormat.Position, currentText[0]));
         SilentScroll(memoryLocation + 1);
      }

      private void CompleteArrayExtension(ArrayRun arrayRun) {
         var newRun = (ArrayRun)Model.RelocateForExpansion(history.CurrentChange, arrayRun, arrayRun.Length + arrayRun.ElementLength);
         if (newRun != arrayRun) {
            ScrollFromRunMove(arrayRun.Start + arrayRun.Length, arrayRun.Length, newRun);
         }
         Model.ObserveRunWritten(history.CurrentChange, arrayRun.Append(1));
         RefreshBackingData();
      }

      private void CompletePointerEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         var index = scroll.ViewPointToDataIndex(point);
         var destination = underEdit.CurrentText.Substring(1, underEdit.CurrentText.Length - 2);

         Model.ExpandData(history.CurrentChange, index + 3);
         scroll.DataLength = Model.Count;

         var currentRun = Model.GetNextRun(index);
         bool inArray = currentRun.Start <= index && currentRun is ArrayRun;

         if (!inArray) {
            if (destination != string.Empty) {
               Model.ClearFormatAndData(history.CurrentChange, index, 4);
            } else {
               Model.ClearFormat(history.CurrentChange, index, 4);
            }
         }

         int fullValue;
         if (destination == string.Empty) {
            fullValue = Model.ReadPointer(index);
         } else if (destination.All(AllHexCharacters.Contains) && destination.Length <= 7) {
            while (destination.Length < 6) destination = "0" + destination;
            fullValue = int.Parse(destination, NumberStyles.HexNumber);
         } else {
            fullValue = Model.GetAddressFromAnchor(history.CurrentChange, index, destination);
         }

         if (fullValue == Pointer.NULL || (0 <= fullValue && fullValue < Model.Count)) {
            if (inArray) {
               Model.UpdateArrayPointer(history.CurrentChange, index, fullValue);
            } else {
               Model.WritePointer(history.CurrentChange, index, fullValue);
               Model.ObserveRunWritten(history.CurrentChange, new PointerRun(index));
            }

            ClearEdits(point);
            SilentScroll(index + 4);
         } else {
            OnError?.Invoke(this, $"Address {fullValue.ToString("X2")} is not within the data.");
            ClearEdits(point);
         }
      }

      private bool CompleteAnchorEdit(Point point) {
         var underEdit = (UnderEdit)currentView[point.X, point.Y].Format;
         var index = scroll.ViewPointToDataIndex(point);
         ErrorInfo errorInfo;

         // if it's an unnamed text anchor, we have special logic for that
         if (underEdit.CurrentText == "^\"\" ") {
            int count = ConsiderResultsAsTextRuns(new[] { index });
            if (count == 0) {
               errorInfo = new ErrorInfo("An anchor with nothing pointing to it must have a name.");
            } else {
               errorInfo = ErrorInfo.NoError;
            }
         } else {
            errorInfo = PokemonModel.ApplyAnchor(Model, history.CurrentChange, index, underEdit.CurrentText);
         }

         ClearEdits(point);
         UpdateToolsFromSelection(index);

         if (errorInfo == ErrorInfo.NoError) return true;

         OnError?.Invoke(this, errorInfo.ErrorMessage);
         return false;
      }

      private void CompleteStringEdit(Point point) {
         // all the bytes are already correct, just move to the next space
         ClearEdits(point);
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         var run = Model.GetNextRun(memoryLocation);
         if (run is PCSRun pcsRun) {
            while (run.Start + run.Length > memoryLocation) {
               history.CurrentChange.ChangeData(Model, memoryLocation, 0xFF);
               memoryLocation++;
               SilentScroll(memoryLocation);
               var newRunLength = PCSString.ReadString(Model, run.Start, true);
               Model.ObserveRunWritten(history.CurrentChange, new PCSRun(run.Start, newRunLength, run.PointerSources));
            }
         } else if (run is ArrayRun arrayRun) {
            var offsets = arrayRun.ConvertByteOffsetToArrayOffset(memoryLocation);
            history.CurrentChange.ChangeData(Model, memoryLocation, 0xFF);
            memoryLocation++;
            SilentScroll(memoryLocation);
            while (offsets.SegmentStart + arrayRun.ElementContent[offsets.SegmentIndex].Length > memoryLocation) {
               history.CurrentChange.ChangeData(Model, memoryLocation, 0x00);
               memoryLocation++;
               SilentScroll(memoryLocation);
            }
         }
         RefreshBackingData();
      }

      private void CompleteCharacterEdit(Point point) {
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         var editText = underEdit.CurrentText;
         if (editText.StartsWith("\"")) editText = editText.Substring(1);
         var pcs = underEdit.OriginalFormat as PCS;
         pcs = pcs ?? (underEdit.OriginalFormat as Anchor)?.OriginalFormat as PCS;
         var escaped = underEdit.OriginalFormat as EscapedPCS;
         escaped = escaped ?? (underEdit.OriginalFormat as Anchor)?.OriginalFormat as EscapedPCS;
         var run = Model.GetNextRun(memoryLocation);
         var byteValue = escaped != null ?
            byte.Parse(underEdit.CurrentText, NumberStyles.HexNumber) :
            (byte)Enumerable.Range(0, 0x100).First(i => PCSString.PCS[i] == editText);

         var position = pcs != null ? pcs.Position : escaped.Position;
         HandleLastCharacterChange(ref memoryLocation, editText, pcs, ref run, position);

         history.CurrentChange.ChangeData(Model, memoryLocation, byteValue);
         Tools.Schedule(Tools.StringTool.DataForCurrentRunChanged);
         if (!SilentScroll(memoryLocation + 1)) {
            RefreshBackingData(point);
            if (point.X + 1 < Width) {
               RefreshBackingData(new Point(point.X + 1, point.Y));
            } else {
               RefreshBackingData(new Point(0, point.Y + 1));
            }
         }
      }

      private void HandleLastCharacterChange(ref int memoryLocation, string editText, PCS pcs, ref IFormattedRun run, int position) {
         if (run is PCSRun) {
            // if its the last character being edited on a normal string, try to expand
            if (run.Length == position + 1) {
               int extraBytesNeeded = editText == "\\\\" ? 2 : 1;
               // last character edit: might require relocation
               var newRun = Model.RelocateForExpansion(history.CurrentChange, run, run.Length + extraBytesNeeded);
               if (newRun != run) {
                  ScrollFromRunMove(memoryLocation, pcs.Position, newRun);
                  memoryLocation += newRun.Start - run.Start;
                  run = newRun;
                  UpdateToolsFromSelection(run.Start);
               }

               history.CurrentChange.ChangeData(Model, memoryLocation + 1, 0xFF);
               if (editText == "\\\\") history.CurrentChange.ChangeData(Model, memoryLocation + 2, 0xFF);
               run = new PCSRun(run.Start, run.Length + extraBytesNeeded, run.PointerSources);
               Model.ObserveRunWritten(history.CurrentChange, run);
            }
         } else if (run is ArrayRun arrayRun) {
            // if the last characet is being edited for an array, truncate
            var offsets = arrayRun.ConvertByteOffsetToArrayOffset(memoryLocation);
            if (arrayRun.ElementContent[offsets.SegmentIndex].Length == position + 1) {
               memoryLocation--; // move back one byte and edit that one instead
            }
         } else {
            Debug.Fail("Why are we completing a character edit on something other than a PCSRun or an Array?");
         }
      }

      private void ScrollFromRunMove(int originalIndexInData, int indexInOldRun, IFormattedRun newRun) {
         scroll.DataLength = Model.Count; // possible length change
         var offset = originalIndexInData - scroll.DataIndex;
         selection.PropertyChanged -= SelectionPropertyChanged;
         selection.GotoAddress(newRun.Start + indexInOldRun - offset);
         selection.PropertyChanged += SelectionPropertyChanged;
      }

      private void CompleteHexEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         var byteValue = byte.Parse(underEdit.CurrentText, NumberStyles.HexNumber);
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         Model.ClearFormat(history.CurrentChange, memoryLocation, 1);
         history.CurrentChange.ChangeData(Model, memoryLocation, byteValue);
         scroll.DataLength = Model.Count;
         ClearEdits(point);
         SilentScroll(memoryLocation + 1);
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
         }
      }

      private void ModelDataMovedByTool(object sender, (int originalLocation, int newLocation) locations) {
         scroll.DataLength = Model.Count;
         if (scroll.DataIndex <= locations.originalLocation && locations.originalLocation < scroll.ViewPointToDataIndex(new Point(Width - 1, Height - 1))) {
            // data was moved from onscreen: follow it
            int offset = locations.originalLocation - scroll.DataIndex;
            selection.GotoAddress(locations.newLocation - offset);
         }
      }

      private void RefreshBackingData(Point p) {
         var index = scroll.ViewPointToDataIndex(p);
         if (index < 0 | index >= Model.Count) { currentView[p.X, p.Y] = HexElement.Undefined; return; }
         var run = Model.GetNextRun(index);
         if (index < run.Start) { currentView[p.X, p.Y] = new HexElement(Model[index], None.Instance); return; }
         var format = run.CreateDataFormat(Model, index);
         if (run.PointerSources != null && run.Start == index) {
            var name = Model.GetAnchorFromAddress(-1, run.Start);
            format = new Anchor(format, name, run.FormatString, run.PointerSources);
         }
         currentView[p.X, p.Y] = new HexElement(Model[index], format);
      }

      private void RefreshBackingData() {
         currentView = new HexElement[Width, Height];
         IFormattedRun run = null;
         for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
               var index = scroll.ViewPointToDataIndex(new Point(x, y));
               if (run == null || index >= run.Start + run.Length) run = Model.GetNextRun(index) ?? new NoInfoRun(Model.Count);
               if (index < 0 || index >= Model.Count) {
                  currentView[x, y] = HexElement.Undefined;
               } else if (index >= run.Start) {
                  var format = run.CreateDataFormat(Model, index);
                  if (run.PointerSources != null && run.Start == index) {
                     var name = Model.GetAnchorFromAddress(-1, run.Start);
                     format = new Anchor(format, name, run.FormatString, run.PointerSources);
                  }
                  currentView[x, y] = new HexElement(Model[index], format);
               } else {
                  currentView[x, y] = new HexElement(Model[index], None.Instance);
               }
            }
         }

         NotifyCollectionChanged(ResetArgs);
      }

      private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);

      /// <summary>
      /// Given a data format, decide how to best display that as text
      /// </summary>
      private class ConvertCellToText : IDataFormatVisitor {
         private readonly IDataModel buffer;
         private readonly int index;

         public string Result { get; private set; }

         public ConvertCellToText(IDataModel buffer, int index) {
            this.buffer = buffer;
            this.index = index;
         }

         public void Visit(Undefined dataFormat, byte data) { }

         public void Visit(None dataFormat, byte data) => Result = data.ToString("X2");

         public void Visit(UnderEdit dataFormat, byte data) {
            throw new NotImplementedException();
         }

         public void Visit(Pointer pointer, byte data) {
            var destination = pointer.Destination.ToString("X6");
            Result = $"<{destination}>";
            if (!string.IsNullOrEmpty(pointer.DestinationName)) Result = $"<{pointer.DestinationName}>";
         }

         public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

         public void Visit(PCS pcs, byte data) {
            Result = pcs.ThisCharacter;
         }

         public void Visit(EscapedPCS pcs, byte data) => Visit((None)null, data);

         public void Visit(ErrorPCS pcs, byte data) => Visit((None)null, data);

         public void Visit(Ascii ascii, byte data) => Result = ((char)data).ToString();

         public void Visit(Integer integer, byte data) => Result = integer.Value.ToString();
      }

      /// <summary>
      /// How we clear data depends on what type of data we're clearing.
      /// For example, cleared pointers get replaced with NULL (0x00000000).
      /// For example, cleared data with no known format gets 0xFF.
      /// </summary>
      private class DataClear : IDataFormatVisitor {
         private readonly IDataModel buffer;
         private readonly ModelDelta currentChange;
         private readonly int index;

         public DataClear(IDataModel data, ModelDelta delta, int index) {
            buffer = data;
            currentChange = delta;
            this.index = index;
         }

         public void Visit(Undefined dataFormat, byte data) { }

         public void Visit(None dataFormat, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

         public void Visit(UnderEdit dataFormat, byte data) => throw new NotImplementedException();

         public void Visit(Pointer pointer, byte data) {
            int start = index - pointer.Position;
            buffer.WriteValue(currentChange, start, 0);
         }

         public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

         public void Visit(PCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

         public void Visit(EscapedPCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

         public void Visit(ErrorPCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

         public void Visit(Ascii ascii, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

         public void Visit(Integer integer, byte data) => buffer.WriteValue(currentChange, index, 0);
      }
   }
}
