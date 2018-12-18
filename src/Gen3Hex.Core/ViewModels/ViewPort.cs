using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.Gen3Hex.Core.ICommandExtensions;

namespace HavenSoft.Gen3Hex.Core.ViewModels {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : ViewModelCore, IViewPort {
      private const string AllHexCharacters = "0123456789ABCDEFabcdef";
      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      private readonly StubCommand
         clear = new StubCommand(),
         copy = new StubCommand();

      private HexElement[,] currentView;

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

      public ICommand MoveSelectionStart => selection.MoveSelectionStart;
      public ICommand MoveSelectionEnd => selection.MoveSelectionEnd;
      public ICommand Goto => selection.Goto;
      public ICommand Back => selection.Back;
      public ICommand Forward => selection.Forward;

      private void ClearActiveEditBeforeSelectionChanges(object sender, Point location) {
         if (location.X >= 0 && location.X < scroll.Width && location.Y >= 0 && location.Y < scroll.Height) {
            var element = currentView[location.X, location.Y];
            if (element.Format is UnderEdit underEdit) {
               if (underEdit.CurrentText.StartsWith("^")) {
                  currentView[location.X, location.Y] = new HexElement(element.Value, underEdit.Edit(" "));
                  if (!TryCompleteEdit(location)) ClearEdits(location);
               } else {
                  currentView[location.X, location.Y] = new HexElement(element.Value, underEdit.OriginalFormat);
                  NotifyCollectionChanged(ResetArgs);
               }
            }
         }
      }

      private void SelectionPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(SelectionEnd)) history.ChangeCompleted();
         NotifyPropertyChanged(e.PropertyName);
      }

      #endregion

      #region Undo / Redo

      private readonly ChangeHistory<Dictionary<int, HexElement>> history;

      public ICommand Undo => history.Undo;

      public ICommand Redo => history.Redo;

      private Dictionary<int, HexElement> RevertChanges(Dictionary<int, HexElement> changes) {
         var opposite = new Dictionary<int, HexElement>();

         foreach (var change in changes) {
            var (index, element) = (change.Key, change.Value);
            var point = scroll.DataIndexToViewPoint(index);
            scroll.ScrollToPoint(ref point);

            opposite[index] = currentView[point.X, point.Y];
            Model[index] = element.Value;
            currentView[point.X, point.Y] = element;
         }

         if (changes.Count > 0) NotifyCollectionChanged(ResetArgs);
         return opposite;
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

         if (fileSystem.Save(new LoadedFile(FileName, Model.RawData))) history.TagAsSaved();
      }

      private void SaveAsExecuted(IFileSystem fileSystem) {
         var newName = fileSystem.RequestNewName(FileName);
         if (newName == null) return;

         if (fileSystem.Save(new LoadedFile(newName, Model.RawData))) {
            FileName = newName; // don't bother notifying, because tagging the history will cause a notify;
            history.TagAsSaved();
         }
      }

      private void CloseExecuted(IFileSystem fileSystem) {
         if (!history.IsSaved) {
            var result = fileSystem.TrySavePrompt(new LoadedFile(FileName, Model.RawData));
            if (result == null) return;
         }
         Closed?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      public ICommand Copy => copy;
      public ICommand Clear => clear;

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || x >= Width) return HexElement.Undefined;
            if (y < 0 || y >= Height) return HexElement.Undefined;
            return currentView[x, y];
         }
      }

      public IModel Model { get; private set; }

#pragma warning disable 0067 // it's ok if events are never used
      public event EventHandler<string> OnError;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
#pragma warning restore 0067

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(LoadedFile file, IModel model = null) {
         Model = model ?? new BasicModel(file.Contents);
         FileName = file.Name;

         scroll = new ScrollRegion { DataLength = Model.Count };
         scroll.PropertyChanged += ScrollPropertyChanged;

         selection = new Selection(scroll, Model);
         selection.PropertyChanged += SelectionPropertyChanged;
         selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges;
         selection.OnError += (sender, e) => OnError?.Invoke(this, e);

         history = new ChangeHistory<Dictionary<int, HexElement>>(RevertChanges);
         history.PropertyChanged += HistoryPropertyChanged;

         ImplementCommands();
         RefreshBackingData();
      }

      private void ImplementCommands() {
         clear.CanExecute = CanAlwaysExecute;
         clear.Execute = arg => {
            var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
            var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
            var left = Math.Min(selectionStart, selectionEnd);
            var right = Math.Max(selectionStart, selectionEnd);
            for (int i = left; i <= right; i++) {
               var p = scroll.DataIndexToViewPoint(i);
               if (p.Y >= 0 && p.Y < scroll.Height) {
                  history.CurrentChange[i] = this[p.X, p.Y];
               } else {
                  history.CurrentChange[i] = new HexElement(Model[i], None.Instance);
               }
               Model[i] = 0xFF;
            }
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

      public bool IsSelected(Point point) => selection.IsSelected(point);

      public void Edit(string input) {
         for (int i = 0; i < input.Length; i++) Edit(input[i]);
      }

      public void Edit(ConsoleKey key) {
         if (key == ConsoleKey.Escape) ClearEdits(SelectionStart);
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
            var operation = new DataClear(Model, index);
            underEdit.OriginalFormat.Visit(operation, Model[index]);
            RefreshBackingData();
         }

         var run = Model.GetNextRun(index - 1) ?? new NoInfoRun(int.MaxValue);
         if (run is PCSRun pcs) {
            for (int i = index - 1; i < run.Start + run.Length; i++) Model[i] = 0xFF;
            var length = PCSString.ReadString(Model, run.Start);
            Model.ObserveRunWritten(new PCSRun(run.Start, length, run.PointerSources));
            RefreshBackingData();
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

      private byte[] Parse(string content) {
         var hex = "0123456789ABCDEF";
         var result = new byte[content.Length / 2];
         for (int i = 0; i < result.Length; i++) {
            var thisByte = content.Substring(i * 2, 2);
            result[i] += (byte)(hex.IndexOf(thisByte[0]) * 0x10);
            result[i] += (byte)hex.IndexOf(thisByte[1]);
         }
         return result;
      }

      public IReadOnlyList<int> Find(string rawSearch) {
         var results = new List<int>();
         var cleanedSearchString = rawSearch.Replace(" ", string.Empty).ToUpper();
         var searchBytes = new List<byte>();
         var hex = "0123456789ABCDEF";

         for (int i = 0; i < cleanedSearchString.Length;) {
            if (cleanedSearchString[i] == '<') {
               var pointerEnd = cleanedSearchString.IndexOf('>', i);
               if (pointerEnd == -1) { OnError(this, "Search mismatch: no closing >"); return results; }
               var pointerContents = cleanedSearchString.Substring(i + 1, pointerEnd - i - 2);
               var address = Model.GetAddressFromAnchor(-1, pointerContents);
               if (address != Pointer.NULL) {
                  searchBytes.Add((byte)(address >> 0));
                  searchBytes.Add((byte)(address >> 8));
                  searchBytes.Add((byte)(address >> 16));
                  searchBytes.Add(0x08);
               } else if (pointerContents.All(hex.Contains) && pointerContents.Length <= 6) {
                  searchBytes.AddRange(Parse(pointerContents).Reverse().Append((byte)0x08));
               } else {
                  OnError(this, $"Could not parse pointer <{pointerContents}>");
                  return results;
               }
               i = pointerEnd + 1;
               continue;
            }
            if (cleanedSearchString.Length >= i + 2 && cleanedSearchString.Substring(i, 2).All(hex.Contains)) {
               searchBytes.AddRange(Parse(cleanedSearchString.Substring(i, 2)));
               i += 2;
               continue;
            }
            OnError(this, $"Could not parse search term {cleanedSearchString.Substring(i)}");
            return results;
         }

         for (int i = 0; i < Model.Count - searchBytes.Count; i++) {
            for (int j = 0; j < searchBytes.Count; j++) {
               if (Model[i + j] != searchBytes[j]) break;
               if (j == searchBytes.Count - 1) results.Add(i);
            }
         }

         // reorder the list to start at the current cursor position
         var offset = scroll.ViewPointToDataIndex(SelectionStart);
         var left = results.Where(result => result < offset);
         var right = results.Where(result => result >= offset);
         results = right.Concat(left).ToList();
         return results;
      }

      public IChildViewPort CreateChildView(int offset) {
         var child = new ChildViewPort(this);
         child.Goto.Execute(offset.ToString("X2"));
         return child;
      }

      public void FollowLink(int x, int y) {
         var format = currentView[x, y].Format;
         if (format is Pointer pointer) {
            if (pointer.Destination != Pointer.NULL) {
               selection.GotoAddress(pointer.Destination);
            } else if (string.IsNullOrEmpty(pointer.DestinationName)) {
               OnError(this, $"null pointers point to nothing, so going to their source isn't possible.");
            } else {
               OnError(this, $"Pointer destination {pointer.DestinationName} not found.");
            }
         }
      }

      public void ConsiderReload(IFileSystem fileSystem) {
         if (!history.IsSaved) return; // don't overwrite local changes

         try {
            var file = fileSystem.LoadFile(FileName);
            if (file == null) return; // asked to load the file, but the file wasn't found... carry on
            Model.Load(file.Contents);
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

         foreach (var source in anchor.Sources) newTab.Add(CreateChildView(source));

         RequestTabChange(this, newTab);
      }

      private void Edit(char input) {
         var point = GetEditPoint();
         var element = currentView[point.X, point.Y];

         if (!ShouldAcceptInput(point, element, input)) {
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
         var point = scroll.DataIndexToViewPoint(Math.Min(selectionStart, selectionEnd));
         scroll.ScrollToPoint(ref point);

         return point;
      }

      private bool ShouldAcceptInput(Point point, HexElement element, char input) {
         var underEdit = element.Format as UnderEdit;

         if (underEdit == null) {
            var innerFormat = element.Format;
            if (innerFormat is Anchor anchorFormat) innerFormat = anchorFormat.OriginalFormat;

            if (innerFormat is PCS) {
               if (input == '"') return true;
               return PCSString.PCS.Any(str => str != null && str.StartsWith(input.ToString()));
            }

            if (innerFormat is EscapedPCS) {
               return AllHexCharacters.Contains(input);
            }

            if (input == '<') {
               // pointer edits are 4 bytes long
               PrepareForMultiSpaceEdit(point, 4);
               return true;
            }
            if (input == '^') {
               // anchor edits are actually 0 length
               // but lets give them 4 spaces to work with
               PrepareForMultiSpaceEdit(point, 4);
               if (element.Format is Anchor anchor) {
                  underEdit = new UnderEdit(anchor, "^" + anchor.Name + anchor.Format);
                  currentView[point.X, point.Y] = new HexElement(element.Value, underEdit);
               }
               return true;
            }
         } else if (underEdit.CurrentText.StartsWith("<")) {
            return char.IsLetterOrDigit(input) || input == '>';
         } else if (underEdit.CurrentText.StartsWith("^")) {
            return char.IsLetterOrDigit(input) || char.IsWhiteSpace(input) || input == '"';
         } else if (underEdit.OriginalFormat is Anchor anchorFormat && anchorFormat.OriginalFormat is PCS) {
            if (input == '"') return true;
            var currentText = underEdit.CurrentText;
            if (currentText.StartsWith("\"")) currentText = currentText.Substring(1);
            return PCSString.PCS.Any(str => str != null && str.StartsWith(currentText + input));
         } else if (underEdit.OriginalFormat is PCS) {
            if (input == '"') return true;
            return PCSString.PCS.Any(str => str != null && str.StartsWith(underEdit.CurrentText + input));
         }

         return AllHexCharacters.Contains(input);
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
         SelectionEnd = scroll.DataIndexToViewPoint(endIndex);
      }

      private bool TryCompleteEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         if (underEdit.CurrentText.StartsWith("<")) {
            if (!underEdit.CurrentText.EndsWith(">")) return false;
            CompletePointerEdit(point);
            return true;
         }
         if (underEdit.CurrentText.StartsWith("^")) {
            if (!char.IsWhiteSpace(underEdit.CurrentText[underEdit.CurrentText.Length - 1])) return false;
            CompleteAnchorEdit(point);
            return true;
         }

         PCS stringFormat = null;
         if (underEdit.OriginalFormat is Anchor anchorFormat) stringFormat = anchorFormat.OriginalFormat as PCS;
         stringFormat = stringFormat ?? underEdit.OriginalFormat as PCS;
         if (stringFormat != null) {
            var currentText = underEdit.CurrentText;
            if (currentText.StartsWith("\"")) currentText = currentText.Substring(1);
            if (stringFormat.Position != 0 && underEdit.CurrentText == "\"") {
               CompleteStringEdit(point);
               return true;
            } else if (PCSString.PCS.Any(str => str == currentText)) {
               CompleteCharacterEdit(point);
               return true;
            }

            return false;
         } else if (underEdit.OriginalFormat is EscapedPCS escaped) {
            if (underEdit.CurrentText.Length < 2) return false;
            CompleteCharacterEdit(point);
            return true;
         }

         if (underEdit.CurrentText.Length < 2) return false;
         CompleteHexEdit(point);
         return true;
      }

      private void CompletePointerEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         var index = scroll.ViewPointToDataIndex(point);
         var destination = underEdit.CurrentText.Substring(1, underEdit.CurrentText.Length - 2);

         Model.ExpandData(index + 3);
         scroll.DataLength = Model.Count;
         Model.ClearFormat(index, 4);

         int fullValue;
         if (destination.All("0123456789ABCDEFabcdef".Contains) && destination.Length <= 6) {
            while (destination.Length < 6) destination = "0" + destination;
            fullValue = int.Parse(destination, NumberStyles.HexNumber);
         } else {
            fullValue = Model.GetAddressFromAnchor(index, destination);
         }

         Model.WritePointer(index, fullValue);
         Model.ObserveRunWritten(new PointerRun(index));
         ClearEdits(point);
         SilentScroll(index + 4);
      }

      private void CompleteAnchorEdit(Point point) {
         var underEdit = (UnderEdit)currentView[point.X, point.Y].Format;
         var index = scroll.ViewPointToDataIndex(point);
         var name = underEdit.CurrentText.Substring(1).Trim();
         string format = string.Empty;

         if (name.Contains('"')) {
            var split = name.IndexOf('"');
            format = name.Substring(split);
            name = name.Substring(0, split);
         }

         if (format == "\"\"") {
            var length = PCSString.ReadString(Model, index);
            if (length < 0) {
               OnError(this, $"Format was specified as a string, but no string was recognized.");
               format = string.Empty;
            } else if (SpanContainsAnchor(index, length)) {
               OnError(this, $"Format was specified as a string, but a string would overlap the next anchor.");
               format = string.Empty;
            }
         } else if (format != string.Empty) {
            OnError(this, $"Format {format} was not understood.");
            format = string.Empty;
         }

         var nextRun = Model.GetNextRun(index);

         if (name.ToLower() == "null") {
            OnError(this, "'null' is a reserved word and cannot be used as an anchor name.");
         } else if (name == string.Empty && nextRun.Start != index) {
            OnError(this, "An anchor with nothing pointing to it must have a name.");
         } else if (name == string.Empty && nextRun.PointerSources.Count == 0 && format != string.Empty) {
            OnError(this, "An anchor with nothing pointing to it must have a name.");
         } else {
            Model.ObserveAnchorWritten(index, name, format);
         }

         ClearEdits(point);
      }

      private bool SpanContainsAnchor(int start, int length) {
         var run = Model.GetNextRun(start + 1);

         // if we're starting in the middle of a run, get the next one
         if (run.Start <= start) {
            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
            run = Model.GetNextRun(start);
         }

         // move start forward to the start of the run
         length -= run.Start - start;
         start = run.Start;

         // check all the runs in the range for pointer sources / destination names
         while (length > 0) {
            if (run.PointerSources.Count > 0) return true;
            if (!string.IsNullOrEmpty(Model.GetAnchorFromAddress(-1, run.Start))) return true;
            run = Model.GetNextRun(run.Start + run.Length);
            length -= run.Start - start;
            start = run.Start;
         }

         return false;
      }

      private void CompleteStringEdit(Point point) {
         // all the bytes are already correct, just move to the next space
         ClearEdits(point);
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         var run = (PCSRun)Model.GetNextRun(memoryLocation);
         while (run.Start + run.Length > memoryLocation) {
            Model[memoryLocation] = 0xFF;
            memoryLocation++;
            SilentScroll(memoryLocation);
            var newRunLength = PCSString.ReadString(Model, run.Start);
            Model.ObserveRunWritten(new PCSRun(run.Start, newRunLength, run.PointerSources));
         }
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

         // if its the last character being edited, do some stuff
         if (pcs != null && run.Length == pcs.Position + 1) {
            int extraBytesNeeded = editText == "\\\\" ? 2 : 1;
            // last character edit: might require relocation
            var newRun = Model.RelocateForExpansion(run, run.Length + extraBytesNeeded);
            if (newRun != run) {
               var offset = memoryLocation - scroll.ViewPointToDataIndex(new Point(0, 0));
               selection.GotoAddress(newRun.Start + pcs.Position - offset);
               memoryLocation += newRun.Start - run.Start;
               run = newRun;
            }

            Model[memoryLocation + 1] = 0xFF;
            if (editText == "\\\\") Model[memoryLocation + 2] = 0xFF;
            Model.ObserveRunWritten(new PCSRun(run.Start, run.Length + extraBytesNeeded, run.PointerSources));
         }

         Model[memoryLocation] = byteValue;
         RefreshBackingData();
         SilentScroll(memoryLocation + 1);
      }

      private void CompleteHexEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;

         var byteValue = byte.Parse(underEdit.CurrentText, NumberStyles.HexNumber);
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         history.CurrentChange[memoryLocation] = new HexElement(element.Value, underEdit.OriginalFormat);
         Model.ExpandData(memoryLocation);
         scroll.DataLength = Model.Count;
         Model.ClearFormat(memoryLocation, 1);
         Model[memoryLocation] = byteValue;
         ClearEdits(point);
         SilentScroll(memoryLocation + 1);
      }

      private void SilentScroll(int memoryLocation) {
         var nextPoint = scroll.DataIndexToViewPoint(memoryLocation);
         if (!scroll.ScrollToPoint(ref nextPoint)) {
            // only need to notify collection change if we didn't auto-scroll after changing cells
            NotifyCollectionChanged(ResetArgs);
         }

         UpdateSelectionWithoutNotify(nextPoint);
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
                     format = new Anchor(format, name, string.Empty, run.PointerSources);
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
         private readonly IModel buffer;
         private readonly int index;

         public string Result { get; private set; }

         public ConvertCellToText(IModel buffer, int index) {
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
      }

      /// <summary>
      /// How we clear data depends on what type of data we're clearing.
      /// For example, cleared pointers get replaced with NULL (0x00000000).
      /// For example, cleared data with no known format gets 0xFF.
      /// </summary>
      private class DataClear : IDataFormatVisitor {
         private readonly IModel buffer;
         private readonly int index;

         public DataClear(IModel data, int index) {
            buffer = data;
            this.index = index;
         }

         public void Visit(Undefined dataFormat, byte data) { }

         public void Visit(None dataFormat, byte data) => buffer[index] = 0xFF;

         public void Visit(UnderEdit dataFormat, byte data) => throw new NotImplementedException();

         public void Visit(Pointer pointer, byte data) {
            int start = index - pointer.Position;
            buffer.WriteValue(start, 0);
         }

         public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

         public void Visit(PCS pcs, byte data) => buffer[index] = 0xFF;

         public void Visit(EscapedPCS pcs, byte data) => buffer[index] = 0xFF;
      }
   }
}
