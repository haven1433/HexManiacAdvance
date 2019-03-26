using System;
using System.Collections.ObjectModel;
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

      public ObservableCollection<IArrayElementViewModel> Children { get; }

      // the address is the address not of the entire array, but of the current index of the array
      private int address = Pointer.NULL;
      public int Address {
         get => address;
         set {
            var run = model.GetNextRun(value);
            if (run.Start > value) {
               Enabled = false;
               return;
            }
            if (!(run is ArrayRun array)) {
               Enabled = false;
               return;
            }

            var offsets = array.ConvertByteOffsetToArrayOffset(value);
            value = array.Start + array.ElementLength * offsets.ElementIndex;
            if (TryUpdate(ref address, value)) {
               toolTray.Schedule(DataForCurrentRunChanged);
               Enabled = true;
            }
         }
      }

      private bool enabled;
      public bool Enabled {
         get => enabled;
         private set => TryUpdate(ref enabled, value);
      }

      public event EventHandler<IFormattedRun> ModelDataChanged;

      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved; // invoke when a new item gets added and the table has to move

      public TableTool(IDataModel model, ChangeHistory<ModelDelta> history, IToolTrayViewModel toolTray) {
         this.model = model;
         this.history = history;
         this.toolTray = toolTray;
         Children = new ObservableCollection<IArrayElementViewModel>();
      }

      private void DataForCurrentRunChanged() {
         Children.Clear();

         var array = model.GetNextRun(Address) as ArrayRun;
         if (array == null) {
            Children.Add(new TitleArrayElementViewModel("The Table tool only works if your cursor is on table data."));
            return;
         }

         Children.Add(new TitleArrayElementViewModel(model.GetAnchorFromAddress(-1, Address))); // example: pokestat/Charmander

         int itemAddress = Address;
         foreach (var item in array.ElementContent) {
            if (item.Type == ElementContentType.Unknown) Children.Add(new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new HexFieldStratgy()));
            else if (item.Type == ElementContentType.PCS) Children.Add(new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new TextFieldStratgy()));
            else if (item.Type == ElementContentType.Pointer) Children.Add(new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new AddressFieldStratgy()));
            else if (item.Type == ElementContentType.Integer) {
               if (item is ArrayRunEnumSegment enumSegment) {
                  Children.Add(new ComboBoxArrayElementViewModel(history, model, item.Name, itemAddress, item.Length));
               } else {
                  Children.Add(new FieldArrayElementViewModel(history, model, item.Name, itemAddress, item.Length, new NumericFieldStrategy()));
               }
            } else {
               throw new NotImplementedException();
            }
            itemAddress += item.Length;
         }
      }

      private void UpdateRun(ArrayRun array) {
         // TODO update the model based on changes in the configuration.
         // generally, the formatted won't change.
         // Instead, the content will change.
         ModelDataChanged?.Invoke(this, null);
      }
   }
}
