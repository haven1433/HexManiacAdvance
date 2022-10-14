using HavenSoft.HexManiac.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public record NewMapScriptsCreatedEventArgs(int Address);

   public class MapScriptCollection : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private int address;

      public bool Unloaded => address == 0;

      public ObservableCollection<MapScriptViewModel> Scripts { get; } = new();

      public bool CollectionExists => address > 0;

      public event EventHandler<NewMapScriptsCreatedEventArgs> NewMapScriptsCreated;

      public MapScriptCollection(IEditableViewPort viewPort) => this.viewPort = viewPort;

      public void Load(int address) {
         Scripts.Clear();
         this.address = address;
         var model = viewPort.Model;
         while (model[address] != 0) {
            Scripts.Add(new(viewPort, address));
            address += 5;
         }
         NotifyPropertyChanged(nameof(CollectionExists));
      }

      public bool CanCreateCollection => address < 0;
      public void CreateCollection() {
         throw new NotImplementedException();
      }

      public void AddScript() {
         // TODO repointing work, possibly updating the `start` of any children
         // TODO write '1' for the new script type
         // TODO write an address for the new script
         Scripts.Add(new(viewPort, address + Scripts.Count * 5));
      }

      private void AddDeleteHandler(int index) {
         Scripts[index].DeleteMe += HandleDelete;
      }

      private void HandleDelete(object sender, EventArgs e) {
         var script = (MapScriptViewModel)sender;
         var index = Scripts.IndexOf(script);
         throw new NotImplementedException();
      }
   }

   public class MapScriptViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private readonly int start;
      private int scriptType, address;
      private string displayAddress;

      public event EventHandler DeleteMe;

      public bool HasSubScripts => scriptType == 2 || scriptType == 4;

      public ObservableCollection<MapSubScriptViewModel> SubScripts { get; } = new();

      public MapScriptViewModel(IEditableViewPort viewPort, int start) {
         this.viewPort = viewPort;
         this.start = start;
         this.scriptType = viewPort.Model[start];
         this.address = viewPort.Model.ReadPointer(start + 1);
         this.displayAddress = $"<{address:X6}>";
         if (scriptType == 2 || scriptType == 4) {
            var destination = address;
            while (viewPort.Model[destination] != 0) {
               var child = new MapSubScriptViewModel(viewPort, destination);
               child.DeleteMe += HandleDelete;
               SubScripts.Add(child);
               destination += 8;
            }
         }
         throw new NotImplementedException();
      }

      public int ScriptTypeIndex {
         get => scriptType - 1; set => Set(ref scriptType, value + 1, arg => {
            NotifyPropertyChanged(nameof(HasSubScripts));
            // TODO update the model
            if ((arg == 2 || arg == 4) && (scriptType != 2 && scriptType != 4)) {
               // if the old type is 2 or 4 and the new type is not, delete the content and replace it with a new 1-byte script `end` (02)
            } else if ((scriptType == 2 || scriptType == 4) && (arg != 2 && arg != 4)) {
               // if the new type is 2 or 4 and the old type is not, move the current script to be the first SubScript of the new table
            }
            throw new NotImplementedException();
         });
      }

      public string Address { get => displayAddress; set => Set(ref displayAddress, value, arg => {
         if (displayAddress.TryParseHex(out var result)) address = result;
         // TODO update model
         throw new NotImplementedException();
      }); }

      public void AddSubScript() {
         // TODO repointing work
         var newStart = viewPort.Model.ReadPointer(address) + 8 * SubScripts.Count;
         SubScripts.Append(new MapSubScriptViewModel(viewPort, newStart));
      }

      public void Delete() => DeleteMe.Raise(this);

      public void Goto() => viewPort.Goto.Execute(address);

      private void HandleDelete(object sender, EventArgs e) {
         var script = (MapSubScriptViewModel)sender;
         var index = SubScripts.IndexOf(script);
         if (index == -1) return;
         throw new NotImplementedException();
      }
   }

   /// <summary>
   /// Represents an indivdual map script from a map script table (type 2 or type 4).
   /// </summary>
   public class MapSubScriptViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private int start, variable, val, address;
      private string variableText, valueText, addressText;

      public event EventHandler DeleteMe;

      public MapSubScriptViewModel(IEditableViewPort viewPort, int start) {
         this.viewPort = viewPort;
         this.start = viewPort.Model.ReadMultiByteValue(start, 2);
         this.variable = viewPort.Model.ReadMultiByteValue(start + 2, 2);
         this.address = viewPort.Model.ReadPointer(start + 4);

         variableText = variable.ToString("X4");
         valueText = Value.ToString();
         addressText = $"<{address:X6}>";
      }

      public string Variable { get => variableText; set => Set(ref variableText, value, arg => {
         if (!variableText.TryParseHex(out int result)) return;
         variable = result;
         viewPort.Model.WriteMultiByteValue(start, 2, viewPort.ChangeHistory.CurrentChange, variable);
      }); }

      public string Value { get => valueText; set => Set(ref valueText, value, arg => {
         if (!int.TryParse(valueText, out int result)) return;
         val = result;
         viewPort.Model.WriteMultiByteValue(start + 2, 2, viewPort.ChangeHistory.CurrentChange, val);
      }); }

      public string Address { get => addressText; set => Set(ref addressText, value, arg => {
         var text = addressText.Trim("<> ".ToCharArray());
         if (!text.TryParseHex(out int result)) return;
         // do the same work that we do in the code tool, removing scripts that aren't needed
         address = result;
         throw new NotImplementedException();
      }); }

      public void Delete() => DeleteMe.Raise(this);

      public void Goto() => viewPort.Goto.Execute(address);
   }

}
