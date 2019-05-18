using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
   public class ViewPort : ViewModelCore, IViewPort {
      public const string AllHexCharacters = "0123456789ABCDEFabcdef";
      public const char GotoMarker = '@';

      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      private readonly StubCommand
         clear = new StubCommand(),
         copy = new StubCommand(),
         copyAddress = new StubCommand(),
         isText = new StubCommand();

      private HexElement[,] currentView;
      private bool exitEditEarly;

      public string Name {
         get {
            var name = Path.GetFileNameWithoutExtension(FileName);
            if (string.IsNullOrEmpty(name)) name = "Untitled";
            if (history.HasDataChange) name += "*";
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
      public ObservableCollection<HeaderRow> ColumnHeaders { get; }
      public int DataOffset => scroll.DataIndex;
      public ICommand Scroll => scroll.Scroll;

      public bool UseCustomHeaders {
         get => scroll.UseCustomHeaders;
         set => scroll.UseCustomHeaders = value;
      }

      private void ScrollPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(scroll.DataIndex)) {
            RefreshBackingData();
            if (e is ExtendedPropertyChangedEventArgs ex) {
               var previous = (int)ex.OldValue;
               if (Math.Abs(scroll.DataIndex - previous) % Width != 0) UpdateColumnHeaders();
            }
         } else if (e.PropertyName != nameof(scroll.DataLength)) {
            NotifyPropertyChanged(((ExtendedPropertyChangedEventArgs)e).OldValue, e.PropertyName);
         }

         if (e.PropertyName == nameof(Width) || e.PropertyName == nameof(Height)) {
            RefreshBackingData();
         }

         if (e.PropertyName == nameof(Width)) {
            UpdateColumnHeaders();
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
      private readonly StubCommand moveSelectionStart = new StubCommand(),
         moveSelectionEnd = new StubCommand();
      public ICommand MoveSelectionStart => moveSelectionStart;
      public ICommand MoveSelectionEnd => moveSelectionEnd;
      public ICommand Goto => selection.Goto;
      public ICommand Back => selection.Back;
      public ICommand Forward => selection.Forward;

      private void ClearActiveEditBeforeSelectionChanges(object sender, Point location) {
         if (location.X >= 0 && location.X < scroll.Width && location.Y >= 0 && location.Y < scroll.Height) {
            var element = currentView[location.X, location.Y];
            var underEdit = element.Format as UnderEdit;
            if (underEdit != null) {
               if (underEdit.CurrentText == string.Empty) {
                  var index = scroll.ViewPointToDataIndex(location);
                  var operation = new DataClear(Model, history.CurrentChange, index);
                  underEdit.OriginalFormat.Visit(operation, Model[index]);
                  ClearEdits(location);
               } else {
                  var endEdit = " ";
                  if (underEdit.CurrentText.Count(c => c == StringDelimeter) % 2 == 1) endEdit = StringDelimeter.ToString();
                  var originalFormat = underEdit.OriginalFormat;
                  if (originalFormat is Anchor anchor) originalFormat = anchor.OriginalFormat;
                  if (underEdit.CurrentText.StartsWith("[") && (originalFormat is EggSection || originalFormat is EggItem)) endEdit = "]";
                  currentView[location.X, location.Y] = new HexElement(element.Value, underEdit.Edit(endEdit));
                  if (!TryCompleteEdit(location)) ClearEdits(location);
               }
            }
         }
      }

      private void SelectionPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(SelectionEnd)) history.ChangeCompleted();
         NotifyPropertyChanged(e.PropertyName);
         var dataIndex = scroll.ViewPointToDataIndex(SelectionStart);
         UpdateToolsFromSelection(dataIndex);
         UpdateSelectedAddress();
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
      }

      private void UpdateToolsFromSelection(int dataIndex) {
         var run = Model.GetNextRun(dataIndex);

         if (run.Start <= dataIndex && run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(dataIndex);
            Tools.StringTool.Address = offsets.SegmentStart - offsets.ElementIndex * array.ElementLength;
            Tools.TableTool.Address = array.Start + array.ElementLength * offsets.ElementIndex;
         } else if (run.Start <= dataIndex && (run is PCSRun || run is EggMoveRun)) {
            Tools.StringTool.Address = run.Start;
         } else {
            Tools.StringTool.Address = dataIndex;
         }

         if (this[SelectionStart].Format is Anchor anchor) {
            TryUpdate(ref anchorText, AnchorStart + anchor.Name + anchor.Format, nameof(AnchorText));
            AnchorTextVisible = true;
         } else {
            AnchorTextVisible = false;
         }
      }

      private string selectedAddress;
      public string SelectedAddress {
         get => selectedAddress;
         private set => TryUpdate(ref selectedAddress, value);
      }

      private void UpdateSelectedAddress() {
         var dataIndex1 = scroll.ViewPointToDataIndex(SelectionStart);
         var dataIndex2 = scroll.ViewPointToDataIndex(SelectionEnd);
         var left = Math.Min(dataIndex1, dataIndex2);
         var result = "Address: " + left.ToString("X6");

         if (Model.GetNextRun(left) is ArrayRun array && array.Start <= left) {
            var index = array.ConvertByteOffsetToArrayOffset(left).ElementIndex;
            var basename = Model.GetAnchorFromAddress(-1, array.Start);
            if (array.ElementNames.Count > index) {
               result += $" | {basename}/{array.ElementNames[index]}";
            } else {
               result += $" | {basename}/{index}";
            }
         }

         SelectedAddress = result;
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
         if (e.PropertyName == nameof(history.IsSaved)) save.CanExecuteChanged.Invoke(save, EventArgs.Empty);
         if (e.PropertyName == nameof(history.HasDataChange)) NotifyPropertyChanged(nameof(Name));
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
         if (fileSystem.Save(new LoadedFile(FileName, Model.RawData))) {
            fileSystem.SaveMetadata(FileName, metadata?.Serialize());
            history.TagAsSaved();
         }
      }

      private void SaveAsExecuted(IFileSystem fileSystem) {
         var newName = fileSystem.RequestNewName(FileName, "GameBoy Advanced", "gba");
         if (newName == null) return;

         var metadata = Model.ExportMetadata();
         if (fileSystem.Save(new LoadedFile(newName, Model.RawData))) {
            FileName = newName; // don't bother notifying, because tagging the history will cause a notify;
            fileSystem.SaveMetadata(FileName, metadata?.Serialize());
            history.TagAsSaved();
         }
      }

      private void CloseExecuted(IFileSystem fileSystem) {
         if (!history.IsSaved) {
            var metadata = Model.ExportMetadata();
            var result = fileSystem.TrySavePrompt(new LoadedFile(FileName, Model.RawData));
            if (result == null) return;
            if (result == true) {
               fileSystem.SaveMetadata(FileName, metadata?.Serialize());
            }
         }
         Closed?.Invoke(this, EventArgs.Empty);
      }

      #endregion

      private readonly ToolTray tools;
      public bool HasTools => true;
      public IToolTrayViewModel Tools => tools;

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
                     if (run is ArrayRun array) {
                        // to keep from double-updating the AnchorText
                        selection.PropertyChanged -= SelectionPropertyChanged;
                        Goto.Execute(index.ToString("X2"));
                        selection.PropertyChanged += SelectionPropertyChanged;
                        UpdateColumnHeaders();
                        Tools.RefreshContent();
                     }
                     RefreshBackingData();
                  } else {
                     OnError?.Invoke(this, errorInfo.ErrorMessage);
                  }
               }
            }
         }
      }

      public ICommand Copy => copy;
      public ICommand CopyAddress => copyAddress;
      public ICommand Clear => clear;
      public ICommand IsText => isText;

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

      #region Constructors

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(string fileName, IDataModel model) {
         history = new ChangeHistory<ModelDelta>(RevertChanges);
         history.PropertyChanged += HistoryPropertyChanged;

         Model = model;
         FileName = fileName;
         ColumnHeaders = new ObservableCollection<HeaderRow>();

         scroll = new ScrollRegion(model.TryGetUsefulHeader) { DataLength = Model.Count };
         scroll.PropertyChanged += ScrollPropertyChanged;

         selection = new Selection(scroll, Model, GetSelectionSpan);
         selection.PropertyChanged += SelectionPropertyChanged;
         selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges;
         selection.OnError += (sender, e) => OnError?.Invoke(this, e);

         tools = new ToolTray(Model, selection, history);
         Tools.OnError += (sender, e) => OnError?.Invoke(this, e);
         Tools.OnMessage += (sender, e) => OnMessage?.Invoke(this, e);
         tools.RequestMenuClose += (sender, e) => RequestMenuClose?.Invoke(this, e);
         Tools.StringTool.ModelDataChanged += ModelChangedByTool;
         Tools.StringTool.ModelDataMoved += ModelDataMovedByTool;
         Tools.TableTool.ModelDataChanged += ModelChangedByTool;
         Tools.TableTool.ModelDataMoved += ModelDataMovedByTool;

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
            bool usedHistory = false;
            ((IFileSystem)arg).CopyText = Model.Copy(() => { usedHistory = true; return history.CurrentChange; }, left, length);
            RefreshBackingData();
            if (usedHistory) UpdateToolsFromSelection(left);
            RequestMenuClose?.Invoke(this, EventArgs.Empty);
         };

         copyAddress.CanExecute = CanAlwaysExecute;
         copyAddress.Execute = arg => {
            var fileSystem = (IFileSystem)arg;
            fileSystem.CopyText = scroll.ViewPointToDataIndex(selection.SelectionStart).ToString("X6");
         };

         moveSelectionStart.CanExecute = selection.MoveSelectionStart.CanExecute;
         moveSelectionStart.Execute = arg => {
            var direction = (Direction)arg;
            MoveSelectionStartExecuted(arg, direction);
         };
         selection.MoveSelectionStart.CanExecuteChanged += (sender, e) => moveSelectionStart.CanExecuteChanged.Invoke(this, e);
         moveSelectionEnd.CanExecute = selection.MoveSelectionEnd.CanExecute;
         moveSelectionEnd.Execute = arg => {
            selection.MoveSelectionEnd.Execute(arg);
         };
         selection.MoveSelectionEnd.CanExecuteChanged += (sender, e) => moveSelectionEnd.CanExecuteChanged.Invoke(this, e);

         isText.CanExecute = CanAlwaysExecute;
         isText.Execute = IsTextExecuted;

         save.CanExecute = arg => !history.IsSaved;
         save.Execute = arg => SaveExecuted((IFileSystem)arg);

         saveAs.CanExecute = CanAlwaysExecute;
         saveAs.Execute = arg => SaveAsExecuted((IFileSystem)arg);

         close.CanExecute = CanAlwaysExecute;
         close.Execute = arg => CloseExecuted((IFileSystem)arg);
      }

      #endregion

      private void MoveSelectionStartExecuted(object arg, Direction direction) {
         var format = this[SelectionStart.X, SelectionStart.Y].Format;
         if (format is UnderEdit underEdit && underEdit.AutocompleteOptions != null && underEdit.AutocompleteOptions.Count > 0) {
            int index = -1;
            for (int i = 0; i < underEdit.AutocompleteOptions.Count; i++) if (underEdit.AutocompleteOptions[i].IsSelected) index = i;
            var options = default(IReadOnlyList<AutoCompleteSelectionItem>);
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
               currentView[SelectionStart.X, SelectionStart.Y] = new HexElement(this[SelectionStart.X, SelectionStart.Y].Value, edit);
               NotifyCollectionChanged(ResetArgs);
               return;
            }
         }
         selection.MoveSelectionStart.Execute(arg);
      }

      public Point ConvertAddressToViewPoint(int address) => scroll.DataIndexToViewPoint(address);
      public int ConvertViewPointToAddress(Point p) => scroll.ViewPointToDataIndex(p);

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point selectionPoint) {
         Debug.Assert(IsSelected(selectionPoint));
         var factory = new ContextItemFactory(this);
         var cell = currentView[SelectionStart.X, SelectionStart.Y];
         cell.Format.Visit(factory, cell.Value);
         return factory.Results;
      }

      public bool IsSelected(Point point) => selection.IsSelected(point);

      public bool IsTable(Point point) {
         var search = scroll.ViewPointToDataIndex(point);
         var run = Model.GetNextRun(search);
         return run.Start <= search && run is ArrayRun;
      }

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
         var point = GetEditPoint();
         var element = currentView[point.X, point.Y];
         var underEdit = element.Format as UnderEdit;
         if (key == ConsoleKey.Enter && underEdit != null) {
            if (underEdit.AutocompleteOptions != null && underEdit.AutocompleteOptions.Any(option => option.IsSelected)) {
               var selectedIndex = AutoCompleteSelectionItem.SelectedIndex(underEdit.AutocompleteOptions);
               underEdit = new UnderEdit(underEdit.OriginalFormat, underEdit.AutocompleteOptions[selectedIndex].CompletionText, underEdit.EditWidth);
               currentView[point.X, point.Y] = new HexElement(element.Value, underEdit);
               RequestMenuClose?.Invoke(this, EventArgs.Empty);
            } else {
               currentView[point.X, point.Y] = new HexElement(element.Value, underEdit.Edit(" "));
            }
            TryCompleteEdit(point);
            return;
         }
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
         AcceptBackspace(underEdit, run, point);
      }

      public void Autocomplete(string input) {
         var point = SelectionStart;
         var element = currentView[point.X, point.Y];
         var underEdit = element.Format as UnderEdit;
         if (underEdit == null) return;
         var index = underEdit.AutocompleteOptions.Select(option => option.CompletionText).ToList().IndexOf(input);
         underEdit = new UnderEdit(underEdit.OriginalFormat, underEdit.AutocompleteOptions[index].CompletionText, underEdit.EditWidth);
         currentView[point.X, point.Y] = new HexElement(element.Value, underEdit);
         TryCompleteEdit(point);
      }

      private void AcceptBackspace(UnderEdit underEdit, IFormattedRun run, Point point) {
         // backspace in progress with characters left: just clear a character
         if (underEdit != null && underEdit.CurrentText.Length > 0) {
            var newText = underEdit.CurrentText.Substring(0, underEdit.CurrentText.Length - 1);
            var options = underEdit.AutocompleteOptions;
            if (options != null) {
               var selectedIndex = AutoCompleteSelectionItem.SelectedIndex(underEdit.AutocompleteOptions);
               options = GetAutocompleteOptions(underEdit.OriginalFormat, newText, selectedIndex);
            }
            var newFormat = new UnderEdit(underEdit.OriginalFormat, newText, underEdit.EditWidth, options);
            currentView[point.X, point.Y] = new HexElement(currentView[point.X, point.Y].Value, newFormat);
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         var index = scroll.ViewPointToDataIndex(point);

         // backspace on an empty element: clear the data from those cells
         if (underEdit != null) {
            var operation = new DataClear(Model, history.CurrentChange, index);
            underEdit.OriginalFormat.Visit(operation, Model[index]);
            RefreshBackingData();
            SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            point = GetEditPoint();
            index = scroll.ViewPointToDataIndex(point);
         }

         run = Model.GetNextRun(index);

         if (run is PCSRun pcs) {
            for (int i = index; i < run.Start + run.Length; i++) history.CurrentChange.ChangeData(Model, i, 0xFF);
            var length = PCSString.ReadString(Model, run.Start, true);
            Model.ObserveRunWritten(history.CurrentChange, new PCSRun(run.Start, length, run.PointerSources));
            RefreshBackingData();
            SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            return;
         }

         if (run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(index);
            if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS) {
               for (int i = index + 1; i < offsets.SegmentStart + array.ElementContent[offsets.SegmentIndex].Length; i++) history.CurrentChange.ChangeData(Model, i, 0x00);
               history.CurrentChange.ChangeData(Model, index, 0xFF);
               RefreshBackingData();
               SelectionStart = scroll.DataIndexToViewPoint(index - 1);
            } else if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer) {
               var cell = currentView[point.X, point.Y];
               PrepareForMultiSpaceEdit(point, 4);
               var destination = ((Pointer)cell.Format).DestinationAsText;
               destination = destination.Substring(0, destination.Length - 1);
               currentView[point.X, point.Y] = new HexElement(cell.Value, new UnderEdit(cell.Format, destination, 4));
            } else if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Integer) {
               var cell = currentView[point.X, point.Y];
               var format = (Integer)cell.Format;
               PrepareForMultiSpaceEdit(point, format.Length);
               var text = format.Value.ToString();
               if (format is IntegerEnum intEnum) text = intEnum.Value;
               text = text.Substring(0, text.Length - 1);
               currentView[point.X, point.Y] = new HexElement(cell.Value, new UnderEdit(format, text, format.Length));
            } else {
               throw new NotImplementedException();
            }
            NotifyCollectionChanged(ResetArgs);
            return;
         }

         if (run.Start <= index && run.Start + run.Length > index) {
            // I want to do a backspace at the end of this run
            SelectionStart = scroll.DataIndexToViewPoint(run.Start);
            var cellToText = new ConvertCellToText(Model, run.Start);
            var element = currentView[SelectionStart.X, SelectionStart.Y];
            element.Format.Visit(cellToText, element.Value);
            var text = cellToText.Result;

            var editLength = 1;
            if (element.Format is Pointer pointer) editLength = 4;
            // if (element.Format is Integer integer) editLength = integer.Length;

            for (int i = 0; i < run.Length; i++) {
               var p = scroll.DataIndexToViewPoint(run.Start + i);
               string editString = i == 0 ? text.Substring(0, text.Length - 1) : string.Empty;
               if (i > 0) editLength = 1;
               currentView[p.X, p.Y] = new HexElement(currentView[p.X, p.Y].Value, new UnderEdit(currentView[p.X, p.Y].Format, editString, editLength));
            }
         } else {
            SelectionStart = scroll.DataIndexToViewPoint(index);
            var element = currentView[SelectionStart.X, SelectionStart.Y];
            var text = element.Value.ToString("X2");
            currentView[SelectionStart.X, SelectionStart.Y] = new HexElement(element.Value, element.Format.Edit(text.Substring(0, text.Length - 1)));
         }
         NotifyCollectionChanged(ResetArgs);
      }

      #region Find

      public IReadOnlyList<(int start, int end)> Find(string rawSearch) {
         var results = new List<(int start, int end)>();
         var cleanedSearchString = rawSearch.ToUpper();
         var searchBytes = new List<ISearchByte>();

         // it might be a string with no quotes, we should check for matches for that.
         if (cleanedSearchString.Length > 3 && !cleanedSearchString.Contains(StringDelimeter) && !cleanedSearchString.All(AllHexCharacters.Contains)) {
            results.AddRange(FindUnquotedText(cleanedSearchString, searchBytes));
         }

         // it might be a pointer without angle braces
         if (cleanedSearchString.Length == 6 && cleanedSearchString.All(AllHexCharacters.Contains)) {
            searchBytes.AddRange(Parse(cleanedSearchString).Reverse().Append((byte)0x08).Select(b => (SearchByte)b));
            results.AddRange(Search(searchBytes).Select(result => (result, result + 3)));
         }

         // attempt to parse the search string fully
         if (TryParseSearchString(searchBytes, cleanedSearchString, errorOnParseError: results.Count == 0)) {
            // find matches
            results.AddRange(Search(searchBytes).Select(result => (result, result + searchBytes.Count - 1)));
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

      private IEnumerable<(int start, int end)> FindUnquotedText(string cleanedSearchString, List<ISearchByte> searchBytes) {
         var pcsBytes = PCSString.Convert(cleanedSearchString);
         pcsBytes.RemoveAt(pcsBytes.Count - 1); // remove the 0xFF that was added, since we're searching for a string segment instead of a whole string.

         // only search for the string if every character in the search string is allowed
         if (pcsBytes.Count != cleanedSearchString.Length) yield break;

         searchBytes.AddRange(pcsBytes.Select(b => new PCSSearchByte(b)));
         var textResults = Search(searchBytes).ToList();
         Model.ConsiderResultsAsTextRuns(history.CurrentChange, textResults);
         foreach (var result in textResults) {
            if (Model.GetNextRun(result) is ArrayRun parentArray && parentArray.LengthFromAnchor == string.Empty) {
               foreach (var dataResult in FindMatchingDataResultsFromArrayElement(parentArray, result)) yield return dataResult;
            }

            yield return (result, result + pcsBytes.Count - 1);
         }
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
            foreach (var child in Model.Arrays) {
               // option 1: another table has a row named after this element
               if (child.LengthFromAnchor == parentArrayName) {
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
            foreach (var child in Model.Streams) {
               // option 3: a stream uses this as a datatype
               if (child is EggMoveRun eggRun) {
                  var groupStart = 0;
                  if (parentArrayName == EggMoveRun.PokemonNameTable) groupStart = EggMoveRun.MagicNumber;
                  if (parentArrayName == EggMoveRun.PokemonNameTable || parentArrayName == EggMoveRun.MoveNamesTable) {
                     for (int i = 0; i < eggRun.Length - 2; i += 2) {
                        if (Model.ReadMultiByteValue(eggRun.Start + i, 2) == offsets.ElementIndex + groupStart) {
                           yield return (eggRun.Start + i, eggRun.Start + i + 1);
                        }
                     }
                  }
               }
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

         var run = Model.GetNextRun(startAddress);
         if (run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(startAddress);
            var lineStart = array.Start + array.ElementLength * offsets.ElementIndex;
            child.Goto.Execute(lineStart.ToString("X2"));
            child.SelectionStart = child.ConvertAddressToViewPoint(startAddress);
            child.SelectionEnd = child.ConvertAddressToViewPoint(endAddress);
         } else {
            child.Goto.Execute(startAddress.ToString("X2"));
            child.SelectionEnd = child.ConvertAddressToViewPoint(endAddress);
         }

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
         if (format is EggSection || format is EggItem) {
            var byteOffset = scroll.ViewPointToDataIndex(new Point(x, y));
            var currentRun = Model.GetNextRun(byteOffset);
            Tools.StringTool.Address = currentRun.Start;
            Tools.SelectedIndex = Enumerable.Range(0, Tools.Count).First(i => Tools[i] is PCSTool);
         }
      }

      public void ExpandSelection(int x, int y) {
         var index = scroll.ViewPointToDataIndex(SelectionStart);
         var run = Model.GetNextRun(index);
         if (run.Start > index) return;
         if (run is ArrayRun array) {
            var offsets = array.ConvertByteOffsetToArrayOffset(index);
            SelectionStart = scroll.DataIndexToViewPoint(offsets.SegmentStart);
            SelectionEnd = scroll.DataIndexToViewPoint(offsets.SegmentStart + array.ElementContent[offsets.SegmentIndex].Length - 1);
         } else {
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
            Model.Load(file.Contents, metadata != null ? new StoredMetadata(metadata) : null);
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
         RequestMenuClose?.Invoke(this, EventArgs.Empty);
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
            UnderEdit newFormat;
            if (element.Format is UnderEdit underEdit && underEdit.AutocompleteOptions != null) {
               var newText = underEdit.CurrentText + input;
               var autoCompleteOptions = GetAutocompleteOptions(underEdit.OriginalFormat, newText);
               newFormat = new UnderEdit(underEdit.OriginalFormat, newText, underEdit.EditWidth, autoCompleteOptions);
            } else {
               newFormat = element.Format.Edit(input.ToString());
            }
            currentView[point.X, point.Y] = new HexElement(element.Value, newFormat);
         } else {
            // ShouldAcceptInput already did the work: nothing to change
         }

         if (!TryCompleteEdit(point)) {
            // only need to notify collection changes if we didn't complete an edit
            NotifyCollectionChanged(ResetArgs);
         }
      }

      private IReadOnlyList<AutoCompleteSelectionItem> GetAutocompleteOptions(IDataFormat originalFormat, string newText, int selectedIndex = -1) {
         if (originalFormat is Anchor anchor) originalFormat = anchor.OriginalFormat;
         if (newText.StartsWith(PointerStart.ToString())) {
            return Model.GetNewPointerAutocompleteOptions(newText, selectedIndex);
         } else if (newText.StartsWith(GotoMarker.ToString())) {
            return Model.GetNewPointerAutocompleteOptions(newText, selectedIndex);
         } else if (originalFormat is IntegerEnum intEnum) {
            var array = (ArrayRun)Model.GetNextRun(intEnum.Source);
            var segment = (ArrayRunEnumSegment)array.ElementContent[array.ConvertByteOffsetToArrayOffset(intEnum.Source).SegmentIndex];
            var options = segment.GetOptions(Model).Select(option => option + " "); // autocomplete needs to complete after selection, so add a space
            return AutoCompleteSelectionItem.Generate(options.Where(option => option.MatchesPartial(newText)), selectedIndex);
         } else if (originalFormat is EggSection || originalFormat is EggItem) {
            var eggRun = (EggMoveRun)Model.GetNextRun(((IDataFormatInstance)originalFormat).Source);
            var allOptions = eggRun.GetAutoCompleteOptions();
            return AutoCompleteSelectionItem.Generate(allOptions.Where(option => option.MatchesPartial(newText)), selectedIndex);
         } else {
            throw new NotImplementedException();
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

      private void IsTextExecuted(object notUsed) {
         var selectionStart = scroll.ViewPointToDataIndex(selection.SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(selection.SelectionEnd);
         var left = Math.Min(selectionStart, selectionEnd);
         var length = Math.Abs(selectionEnd - selectionStart) + 1;
         var startPlaces = Model.FindPossibleTextStartingPlaces(left, length);

         // do the actual search now that we know places to start
         var foundCount = Model.ConsiderResultsAsTextRuns(history.CurrentChange, startPlaces);
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
               return Model.IsAtEndOfArray(index, out var _);
            }

            if (input == AnchorStart || input == GotoMarker) {
               // anchor edits are actually 0 length
               // but lets give them 4 spaces to work with
               PrepareForMultiSpaceEdit(point, 4);
               var autoCompleteOptions = input == GotoMarker ? new AutoCompleteSelectionItem[0] : null;
               var underEdit = new UnderEdit(element.Format, input.ToString(), 4, autoCompleteOptions);
               currentView[point.X, point.Y] = new HexElement(element.Value, underEdit);
               return true;
            }
         }

         // normal case: the logic for how to handle this edit depends on what format is in this cell.
         var startCellEdit = new StartCellEdit(Model, memoryLocation, input);
         element.Format.Visit(startCellEdit, element.Value);
         if (startCellEdit.NewFormat != null) {
            // if the edit provided a new format, go ahead and build a new element based on that format.
            // if no new format was provided, then the default logic in the method above will make a new UnderEdit cell if the Result is true.
            currentView[point.X, point.Y] = new HexElement(element.Value, startCellEdit.NewFormat);
            if (startCellEdit.NewFormat.EditWidth > 1) PrepareForMultiSpaceEdit(point, startCellEdit.NewFormat.EditWidth);
         }
         return startCellEdit.Result;
      }

      private (Point start, Point end) GetSelectionSpan(Point p) {
         var index = scroll.ViewPointToDataIndex(p);
         var run = Model.GetNextRun(index);
         if (run.Start > index) return (p, p);

         (Point, Point) pair(int start, int end) => (scroll.DataIndexToViewPoint(start), scroll.DataIndexToViewPoint(end));

         if (run is PointerRun) return pair(run.Start, run.Start + run.Length - 1);
         if (run is EggMoveRun) {
            var even = (index - run.Start) % 2 == 0;
            if (even) return pair(index, index + 1);
            return pair(index - 1, index);
         }
         if (!(run is ArrayRun array)) return (p, p);

         var offset = array.ConvertByteOffsetToArrayOffset(index);
         var type = array.ElementContent[offset.SegmentIndex].Type;
         if (type == ElementContentType.Pointer || type == ElementContentType.Integer) {
            return pair(offset.SegmentStart, offset.SegmentStart + array.ElementContent[offset.SegmentIndex].Length - 1);
         }

         return (p, p);
      }

      private bool TryCoerceSelectionToStartOfElement(ref Point point, ref HexElement element) {
         var format = element.Format;
         var (position, length) = (-1, -1);
         if (format is Pointer pointer) (position, length) = (pointer.Position, 4);
         if (format is Integer integer) (position, length) = (integer.Position, integer.Length);
         if (position == -1) return false;

         point = scroll.DataIndexToViewPoint(scroll.ViewPointToDataIndex(point) - position);
         element = this[point];
         UpdateSelectionWithoutNotify(point);
         PrepareForMultiSpaceEdit(point, length);
         return true;
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
         // wrap this whole method in an anti-recursion clause
         selection.PreviewSelectionStartChanged -= ClearActiveEditBeforeSelectionChanges;
         using (new StubDisposable { Dispose = () => selection.PreviewSelectionStartChanged += ClearActiveEditBeforeSelectionChanges }) {

            var element = currentView[point.X, point.Y];
            var underEdit = element.Format as UnderEdit;
            if (underEdit == null) return false; // no edit to complete

            if (TryGeneralCompleteEdit(underEdit.CurrentText, point, out bool result)) {
               return result;
            }

            // normal case: whether or not to accept the edit depends on the existing cell format
            var dataIndex = scroll.ViewPointToDataIndex(point);
            var completeEditOperation = new CompleteEditOperation(Model, dataIndex, underEdit.CurrentText, history.CurrentChange);
            underEdit.OriginalFormat.Visit(completeEditOperation, element.Value);
            if (completeEditOperation.Result) {
               if (completeEditOperation.NewCell != null) {
                  currentView[point.X, point.Y] = completeEditOperation.NewCell;
               }
               if (completeEditOperation.DataMoved || completeEditOperation.NewDataIndex > scroll.DataLength) scroll.DataLength = Model.Count;
               if (!SilentScroll(completeEditOperation.NewDataIndex) && completeEditOperation.NewCell == null) {
                  RefreshBackingData();
               }
               var run = Model.GetNextRun(completeEditOperation.NewDataIndex);
               if (run.Start > completeEditOperation.NewDataIndex) run = new NoInfoRun(Model.Count);
               if (completeEditOperation.DataMoved) UpdateToolsFromSelection(run.Start);
               if (run is ArrayRun) Tools.Schedule(Tools.TableTool.DataForCurrentRunChanged);
               if (run is ArrayRun || run is PCSRun) Tools.Schedule(Tools.StringTool.DataForCurrentRunChanged);
               if (completeEditOperation.MessageText != null) OnMessage?.Invoke(this, completeEditOperation.MessageText);
               if (completeEditOperation.ErrorText != null) OnError?.Invoke(this, completeEditOperation.ErrorText);
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
         // goto marker
         if (currentText.StartsWith(GotoMarker.ToString())) {
            if (char.IsWhiteSpace(currentText[currentText.Length - 1])) {
               var destination = currentText.Substring(1);
               ClearEdits(point);
               Goto.Execute(destination);
               RequestMenuClose?.Invoke(this, EventArgs.Empty);
               result = true;
            } else {
               result = false;
            }

            return true;
         }

         // anchor start
         if (currentText.StartsWith(AnchorStart.ToString())) {
            TryUpdate(ref anchorText, currentText, nameof(AnchorText));
            if (!char.IsWhiteSpace(currentText[currentText.Length - 1])) {
               AnchorTextVisible = true;
               result = false;
               return true;
            }

            // only end the anchor edit if the [] brace count matches
            if (currentText.Sum(c => c == '[' ? 1 : c == ']' ? -1 : 0) != 0) {
               AnchorTextVisible = true;
               result = false;
               return true;
            }

            if (!CompleteAnchorEdit(point)) exitEditEarly = true;
            result = true;
            return true;
         }

         // table extension
         var dataIndex = scroll.ViewPointToDataIndex(point);
         if (currentText == ExtendArray.ToString() && Model.IsAtEndOfArray(dataIndex, out var arrayRun)) {
            var originalArray = arrayRun;
            var errorInfo = Model.CompleteArrayExtension(history.CurrentChange, ref arrayRun);
            if (!errorInfo.HasError || errorInfo.IsWarning) {
               if (arrayRun.Start != originalArray.Start) {
                  ScrollFromRunMove(arrayRun.Start + arrayRun.Length, arrayRun.Length, arrayRun);
               }
               if (errorInfo.IsWarning) OnMessage?.Invoke(this, errorInfo.ErrorMessage);
               RefreshBackingData();
            } else {
               OnError?.Invoke(this, errorInfo.ErrorMessage);
            }
            result = true;
            return true;
         }

         result = default;
         return false;
      }

      /// <returns>True if it was completed successfully, false if some sort of error occurred and we should abort the remainder of the edit.</returns>
      private bool CompleteAnchorEdit(Point point) {
         var underEdit = (UnderEdit)currentView[point.X, point.Y].Format;
         var index = scroll.ViewPointToDataIndex(point);
         ErrorInfo errorInfo;

         // if it's an unnamed text anchor, we have special logic for that
         if (underEdit.CurrentText == "^\"\" ") {
            int count = Model.ConsiderResultsAsTextRuns(history.CurrentChange, new[] { index });
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

         if (errorInfo == ErrorInfo.NoError) {
            if (Model.GetNextRun(index) is ArrayRun array && array.Start == index) Goto.Execute(index.ToString("X2"));
            return true;
         }

         if (errorInfo.IsWarning) {
            OnMessage?.Invoke(this, errorInfo.ErrorMessage);
         } else {
            OnError?.Invoke(this, errorInfo.ErrorMessage);
         }

         return errorInfo.IsWarning;
      }

      private void ScrollFromRunMove(int originalIndexInData, int indexInOldRun, IFormattedRun newRun) {
         scroll.DataLength = Model.Count; // possible length change
         var offset = originalIndexInData - scroll.DataIndex;
         selection.PropertyChanged -= SelectionPropertyChanged;
         selection.GotoAddress(newRun.Start + indexInOldRun - offset);
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

         if (run is ArrayRun && sender != Tools.StringTool && Model.GetNextRun(Tools.StringTool.Address).Start == run.Start) Tools.StringTool.DataForCurrentRunChanged();
         if (run is ArrayRun && sender != Tools.TableTool && Model.GetNextRun(Tools.TableTool.Address).Start == run.Start) Tools.TableTool.DataForCurrentRunChanged();
      }

      private void ModelDataMovedByTool(object sender, (int originalLocation, int newLocation) locations) {
         scroll.DataLength = Model.Count;
         if (scroll.DataIndex <= locations.originalLocation && locations.originalLocation < scroll.ViewPointToDataIndex(new Point(Width - 1, Height - 1))) {
            // data was moved from onscreen: follow it
            int offset = locations.originalLocation - scroll.DataIndex;
            selection.GotoAddress(locations.newLocation - offset);
         }
         OnMessage?.Invoke(this, $"Data was automatically moved to {locations.newLocation.ToString("X6")}. Pointers were updated.");
      }

      private void RefreshBackingData(Point p) {
         var index = scroll.ViewPointToDataIndex(p);
         if (index < 0 | index >= Model.Count) { currentView[p.X, p.Y] = HexElement.Undefined; return; }
         var run = Model.GetNextRun(index);
         if (index < run.Start) { currentView[p.X, p.Y] = new HexElement(Model[index], None.Instance); return; }
         var format = run.CreateDataFormat(Model, index);
         format = Model.WrapFormat(run, format, index);
         currentView[p.X, p.Y] = new HexElement(Model[index], format);
      }

      private void RefreshBackingData() {
         currentView = new HexElement[Width, Height];
         IFormattedRun run = null;
         for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
               var index = scroll.ViewPointToDataIndex(new Point(x, y));
               if (run == null || index >= run.Start + run.Length) {
                  run = Model.GetNextRun(index) ?? new NoInfoRun(Model.Count);
                  if (run is ArrayRun array) Tools.Schedule(array.ClearCache);
               }
               if (index < 0 || index >= Model.Count) {
                  currentView[x, y] = HexElement.Undefined;
               } else if (index >= run.Start) {
                  var format = run.CreateDataFormat(Model, index);
                  format = Model.WrapFormat(run, format, index);
                  currentView[x, y] = new HexElement(Model[index], format);
               } else {
                  currentView[x, y] = new HexElement(Model[index], None.Instance);
               }
            }
         }

         RequestMenuClose?.Invoke(this, EventArgs.Empty);
         NotifyCollectionChanged(ResetArgs);
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
