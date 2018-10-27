using HavenSoft.Gen3Hex.Model;
using HavenSoft.ViewModel.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : ViewModelCore, INotifyCollectionChanged {
      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

      private readonly ScrollRegion scroll;
      private readonly Selection selection;
      private readonly ChangeHistory<Dictionary<int, HexElement>> history;

      private byte[] data;
      private HexElement[,] currentView;

      #region Name

      private string name;

      public string Name {
         get => name;
         private set => TryUpdate(ref name, value);
      }

      #endregion

      #region Scrolling Properties

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

      public ICommand Scroll => scroll.Scroll;

      #endregion

      #region Selection Properties

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

      private void ClearActiveEditBeforeSelectionChanges(object sender, Point location) {
         if (location.X >= 0 && location.X < scroll.Width && location.Y >= 0 && location.Y < scroll.Height) {
            var element = currentView[location.X, location.Y];
            if (element.Format is UnderEdit underEdit) {
               currentView[location.X, location.Y] = new HexElement(element.Value, underEdit.OriginalFormat);
               NotifyCollectionChanged(ResetArgs);
            }
         }
      }

      #endregion

      #region Undo / Redo

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

      #endregion

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || x >= Width) return HexElement.Undefined;
            if (y < 0 || y >= Height) return HexElement.Undefined;

            return currentView[x, y];
         }
      }

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(LoadedFile file) {
         name = file.Name;
         data = file.Contents;

         scroll = new ScrollRegion { DataLength = data.Length };
         scroll.PropertyChanged += ScrollPropertyChanged;

         selection = new Selection(scroll);
         selection.PropertyChanged += SelectionPropertyChanged;
         selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges;

         history = new ChangeHistory<Dictionary<int, HexElement>>(RevertChanges);
      }

      public bool IsSelected(Point point) => selection.IsSelected(point);

      public void Edit(string input) {
         for (int i = 0; i < input.Length; i++) Edit(input[i]);
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

      private void SelectionPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(SelectionEnd)) history.ChangeCompleted();
         NotifyPropertyChanged(e.PropertyName);
      }

      private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
   }
}
