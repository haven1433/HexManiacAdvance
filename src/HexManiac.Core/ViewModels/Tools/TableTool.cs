using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableTool : ViewModelCore, IToolViewModel {
      private readonly IDataModel model;
      private readonly Selection selection;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IToolTrayViewModel toolTray;

      public string Name => "Table";

      private string currentElementName;
      public string CurrentElementName {
         get => currentElementName;
         set => TryUpdate(ref currentElementName, value);
      }

      private readonly StubCommand previous, next, append;
      public ICommand Previous => previous;
      public ICommand Next => next;
      public ICommand Append => append;
      private void CommandCanExecuteChanged() {
         previous.CanExecuteChanged.Invoke(previous, EventArgs.Empty);
         next.CanExecuteChanged.Invoke(next, EventArgs.Empty);
         append.CanExecuteChanged.Invoke(append, EventArgs.Empty);
      }

      public ObservableCollection<IArrayElementViewModel> Children { get; }

      // the address is the address not of the entire array, but of the current index of the array
      private int address = Pointer.NULL;
      public int Address {
         get => address;
         set {
            if (TryUpdate(ref address, value)) {
               var run = model.GetNextRun(value);
               if (run.Start > value || !(run is ArrayRun array)) {
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

      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<string> OnError;

#pragma warning disable 0067 // it's ok if events are never used after implementing an interface
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved; // invoke when a new item gets added and the table has to move
#pragma warning restore 0067

      public TableTool(IDataModel model, Selection selection, ChangeHistory<ModelDelta> history, IToolTrayViewModel toolTray) {
         this.model = model;
         this.selection = selection;
         this.history = history;
         this.toolTray = toolTray;
         Children = new ObservableCollection<IArrayElementViewModel>();

         previous = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ArrayRun;
               return array != null && array.Start < address;
            },
            Execute = parameter => {
               var array = (ArrayRun)model.GetNextRun(address);
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(Address - array.ElementLength);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
            }
         };

         next = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ArrayRun;
               return array != null && array.Start + array.Length > address + array.ElementLength;
            },
            Execute = parameter => {
               var array = (ArrayRun)model.GetNextRun(address);
               selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(Address + array.ElementLength);
               selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
            }
         };

         append = new StubCommand {
            CanExecute = parameter => {
               var array = model.GetNextRun(address) as ArrayRun;
               return array != null && array.Start + array.Length == address + array.ElementLength;
            },
            Execute = parameter => {
               var array = (ArrayRun)model.GetNextRun(address);
               var originalArray = array;
               var error = model.CompleteArrayExtension(history.CurrentChange, ref array);
               if (array.Start != originalArray.Start) {
                  ModelDataMoved?.Invoke(this, (originalArray.Start, array.Start));
                  selection.GotoAddress(array.Start + array.Length - array.ElementLength);
               }
               if (error.HasError) {
                  OnError?.Invoke(this, error.ErrorMessage);
               } else {
                  ModelDataChanged?.Invoke(this, array);
                  selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(array.Start + array.Length - array.ElementLength);
                  selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selection.Scroll.ViewPointToDataIndex(selection.SelectionStart) + array.ElementLength - 1);
               }
            }
         };

         CurrentElementName = "The Table tool only works if your cursor is on table data.";
      }

      public void DataForCurrentRunChanged() {
         foreach (var child in Children) child.DataChanged -= ForwardModelChanged;
         Children.Clear();

         var array = model.GetNextRun(Address) as ArrayRun;
         if (array == null) {
            CurrentElementName = "The Table tool only works if your cursor is on table data.";
            return;
         }

         var basename = model.GetAnchorFromAddress(-1, array.Start);
         var index = (Address - array.Start) / array.ElementLength;
         if (array.ElementNames.Count > index) {
            CurrentElementName = $"{basename}/{index}" + Environment.NewLine + $"{basename}/{array.ElementNames[index]}";
         } else {
            CurrentElementName = $"{basename}/{index}";
         }

         int itemAddress = Address;
         foreach (var item in array.ElementContent) {
            IArrayElementViewModel viewModel = null;
            if (item.Type == ElementContentType.Unknown) viewModel = new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new HexFieldStratgy());
            else if (item.Type == ElementContentType.PCS) viewModel = new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new TextFieldStratgy());
            else if (item.Type == ElementContentType.Pointer) viewModel = new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new AddressFieldStratgy());
            else if (item.Type == ElementContentType.Integer) {
               if (item is ArrayRunEnumSegment enumSegment) {
                  viewModel = new ComboBoxArrayElementViewModel(history, model, item.Name, itemAddress, item.Length);
               } else {
                  viewModel = new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new NumericFieldStrategy());
               }
            } else {
               throw new NotImplementedException();
            }
            Children.Add(viewModel);
            viewModel.DataChanged += ForwardModelChanged;
            itemAddress += item.Length;
         }
      }

      private void ForwardModelChanged(object sender, EventArgs e) => ModelDataChanged?.Invoke(this, model.GetNextRun(Address));
   }
}
