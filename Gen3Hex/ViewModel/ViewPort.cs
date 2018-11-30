using HavenSoft.Gen3Hex.Model;
using HavenSoft.ViewModel.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.ICommandExtensions;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : ViewModelCore, IViewPort {
      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      private readonly StubCommand
         clear = new StubCommand(),
         copy = new StubCommand();

      private byte[] data;
      private HexElement[,] currentView;

      public string Name {
         get {
            var name = Path.GetFileNameWithoutExtension(FileName);
            if (string.IsNullOrEmpty(name)) name = "Untitled";
            if (!history.IsSaved) name += "*";
            return name;
         }
      }

      public string FileName { get; private set; }

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
               currentView[location.X, location.Y] = new HexElement(element.Value, underEdit.OriginalFormat);
               NotifyCollectionChanged(ResetArgs);
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
            data[index] = element.Value;
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

         if (fileSystem.Save(new LoadedFile(FileName, data))) history.TagAsSaved();
      }

      private void SaveAsExecuted(IFileSystem fileSystem) {
         var newName = fileSystem.RequestNewName(FileName);
         if (newName == null) return;

         if (fileSystem.Save(new LoadedFile(newName, data))) {
            FileName = newName; // don't bother notifying, because tagging the history will cause a notify;
            history.TagAsSaved();
         }
      }

      private void CloseExecuted(IFileSystem fileSystem) {
         if (!history.IsSaved) {
            var result = fileSystem.TrySavePrompt(new LoadedFile(FileName, data));
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

      public event EventHandler<string> OnError;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(LoadedFile file) {
         FileName = file.Name;
         data = file.Contents;

         scroll = new ScrollRegion { DataLength = data.Length };
         scroll.PropertyChanged += ScrollPropertyChanged;

         selection = new Selection(scroll);
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
            for (int i = left; i <= right; i++) data[i] = 0xFF;
            RefreshBackingData();
         };

         copy.CanExecute = CanAlwaysExecute;
         copy.Execute = arg => {
            var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
            var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
            var left = Math.Min(selectionStart, selectionEnd);
            var length = Math.Abs(selectionEnd - selectionStart) + 1;
            var bytes = Enumerable.Range(left, length).Select(i => data[i]);
            ((IFileSystem)arg).CopyText = string.Join(" ", bytes.Select(value => value.ToString("X2")));
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

      public IReadOnlyList<int> Find(string rawSearch) {
         var results = new List<int>();

         // basic attempt: see if the search term is a string of bytes
         var cleanedSearch = rawSearch.Replace(" ", string.Empty).ToUpper();
         var hex = "0123456789ABCDEF";
         if (cleanedSearch.All(hex.Contains) && cleanedSearch.Length % 2 == 0) {
            var search = new byte[cleanedSearch.Length / 2];
            for (int i = 0; i < search.Length; i++) {
               var thisByte = cleanedSearch.Substring(i * 2, 2);
               search[i] += (byte)(hex.IndexOf(thisByte[0]) * 0x10);
               search[i] += (byte)hex.IndexOf(thisByte[1]);
            }
            for (int i = 0; i < data.Length - search.Length; i++) {
               for (int j = 0; j < search.Length; j++) {
                  if (data[i + j] != search[j]) break;
                  if (j == search.Length - 1) results.Add(i);
               }
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
         var child = new ChildViewPort(this, data);
         child.Goto.Execute(offset.ToString("X2"));
         return child;
      }

      public void FollowLink(int x, int y) { }

      public void ConsiderReload(IFileSystem fileSystem) {
         if (!history.IsSaved) return; // don't overwrite local changes

         try {
            var file = fileSystem.LoadFile(FileName);
            if (file == null) return; // asked to load the file, but the file wasn't found... carry on
            data = file.Contents;
            scroll.DataLength = data.Length;
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

      private void Edit(char input) {
         var point = GetEditPoint();
         var element = currentView[point.X, point.Y];

         if (!ShouldAcceptInput(point, element, input)) return;

         SelectionStart = point;

         var newFormat = element.Format.Edit(input.ToString());
         currentView[point.X, point.Y] = new HexElement(element.Value, newFormat);
         if (!TryCompleteEdit(point)) {
            // only need to notify collection changes if we didn't complete an edit
            NotifyCollectionChanged(ResetArgs);
         }
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

         if (!"0123456789ABCDEFabcdef".Contains(input)) {
            if (underEdit != null) {
               currentView[point.X, point.Y] = new HexElement(element.Value, underEdit.OriginalFormat);
               NotifyCollectionChanged(ResetArgs);
            }
            return false;
         }

         return true;
      }

      private bool TryCompleteEdit(Point point) {
         var element = currentView[point.X, point.Y];
         var underEdit = (UnderEdit)element.Format;
         if (underEdit.CurrentText.Length < 2) return false;

         var byteValue = byte.Parse(underEdit.CurrentText, NumberStyles.HexNumber);
         var memoryLocation = scroll.ViewPointToDataIndex(point);
         history.CurrentChange[memoryLocation] = new HexElement(element.Value, underEdit.OriginalFormat);
         ExpandData(memoryLocation);
         data[memoryLocation] = byteValue;
         currentView[point.X, point.Y] = new HexElement(byteValue, None.Instance);
         var nextPoint = scroll.DataIndexToViewPoint(memoryLocation + 1);
         if (!scroll.ScrollToPoint(ref nextPoint)) {
            // only need to notify collection change if we didn't auto-scroll after changing cells
            NotifyCollectionChanged(ResetArgs);
         }

         UpdateSelectionWithoutNotify(nextPoint);
         return true;
      }

      // Calling this method over and over
      // (for example, holding a key on the keyboard at the end of the file)
      // makes the garbage collector go crazy.
      // However, running performance is still super smooth, so don't optimize yet.
      private void ExpandData(int minimumIndex) {
         if (data.Length > minimumIndex) return;

         var newData = new byte[minimumIndex + 1];
         Array.Copy(data, newData, data.Length);
         data = newData;
         scroll.DataLength = data.Length;
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
         for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
               var index = scroll.ViewPointToDataIndex(new Point(x, y));
               if (index < 0 || index >= data.Length) {
                  currentView[x, y] = HexElement.Undefined;
               } else {
                  currentView[x, y] = new HexElement(data[index], None.Instance);
               }
            }
         }

         NotifyCollectionChanged(ResetArgs);
      }

      private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
   }
}
