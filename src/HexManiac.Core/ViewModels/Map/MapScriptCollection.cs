using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Data;
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
            AddDeleteHandler(Scripts.Count - 1);
            address += 5;
         }
         NotifyPropertyChanged(nameof(CollectionExists));
      }

      public bool CanCreateCollection => address < 0;
      public void CreateCollection() {
         throw new NotImplementedException();
      }

      public void AddScript() {
         var token = viewPort.ChangeHistory.CurrentChange;
         var model = viewPort.Model;
         var run = model.GetNextRun(address) as ITableRun;
         if (run == null) return;
         run = model.RelocateForExpansion(token, run, run.Length + 5);
         run = run.Append(token, 1);
         address = run.Start;
         var newScript = model.FindFreeSpace(model.FreeSpaceStart, 1);
         token.ChangeData(model, newScript, 0x02); // `end`
         token.ChangeData(model, run.Start + run.Length - 6, 1); // script-type 1
         model.UpdateArrayPointer(token, default, default, default, run.Start + run.Length - 5, newScript);
         model.ObserveRunWritten(token, run);
         Scripts.Add(new(viewPort, address + Scripts.Count * 5));
         AddDeleteHandler(Scripts.Count - 1);
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

      public ObservableCollection<string> ScriptOptions { get; } = new();
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
         ScriptOptions.Add("Load");
         ScriptOptions.Add("Per-Frame (Table)");
         ScriptOptions.Add("Transition");
         ScriptOptions.Add("Warp into Map (Table)");
         ScriptOptions.Add("Resume");
         ScriptOptions.Add("Dive Warp");
         ScriptOptions.Add("Return to Field");
      }

      public int ScriptTypeIndex {
         get => scriptType - 1; set => Set(ref scriptType, value + 1, arg => {
            NotifyPropertyChanged(nameof(HasSubScripts));
            // TODO update the model
            if ((arg == 2 || arg == 4) && (scriptType != 2 && scriptType != 4)) {
               // if the old type is 2 or 4 and the new type is not, delete the content and replace it with a new 1-byte script `end` (02)
               throw new NotImplementedException();
            } else if ((scriptType == 2 || scriptType == 4) && (arg != 2 && arg != 4)) {
               // if the new type is 2 or 4 and the old type is not, move the current script to be the first SubScript of the new table
               throw new NotImplementedException();
            }
            viewPort.ChangeHistory.CurrentChange.ChangeData(viewPort.Model, start, (byte)scriptType);
         });
      }

      public string Address { get => displayAddress; set => Set(ref displayAddress, value, arg => {
         if (displayAddress.TryParseHex(out var result)) {
            address = result;
            viewPort.Model.UpdateArrayPointer(viewPort.ChangeHistory.CurrentChange, default, default, default, start + 1, address);
         }
      }); }

      public void AddSubScript() {
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         // no table. Clear format.
         var start = model.ReadPointer(address);
         model.ClearFormat(token, start, SubScripts.Count * 8);
         var data = model.Cut(token, start, SubScripts.Count * 8);
         var run = model.RelocateForExpansion(token, model.GetNextRun(address), data.Length + 8);
         model.Paste(token, run.Start, data, data.Length + 8);
         // add the new element
         var newScriptStart = model.FindFreeSpace(model.FreeSpaceStart, 1);
         token.ChangeData(model, newScriptStart, 0x02);
         model.WriteMultiByteValue(run.Start + data.Length, 4, token, 0);
         model.WritePointer(token, run.Start + data.Length + 4, newScriptStart);

         // TODO add all the formatting
         for (int i = 0; i < SubScripts.Count + 1; i++) {
            throw new NotImplementedException();
         }

         address = run.Start;
         displayAddress = $"<{address:X6}>";
         NotifyPropertyChanged(nameof(Address));
         NotifyPropertyChanged(nameof(Address));
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
         (this.viewPort, this.start) = (viewPort, start);
         this.variable = viewPort.Model.ReadMultiByteValue(start, 2);
         this.val = viewPort.Model.ReadMultiByteValue(start + 2, 2);
         this.address = viewPort.Model.ReadPointer(start + 4);

         variableText = variable.ToString("X4");
         valueText = val.ToString();
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
