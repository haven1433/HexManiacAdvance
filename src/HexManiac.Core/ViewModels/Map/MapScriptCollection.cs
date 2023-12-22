using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public record NewMapScriptsCreatedEventArgs(int Address);

   public class MapScriptCollection : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private ModelArrayElement owner;
      private int address;

      public int Address => address;
      public bool Unloaded => address == 0;
      public ObservableCollection<MapScriptViewModel> Scripts { get; } = new();
      public bool CollectionExists => address > 0;

      public event EventHandler<NewMapScriptsCreatedEventArgs> NewMapScriptsCreated;

      public MapScriptCollection(IEditableViewPort viewPort) => this.viewPort = viewPort;

      public void Load(ModelArrayElement owner) {
         Scripts.Clear();
         this.owner = owner;
         if (owner == null) return;
         address = owner.GetAddress("mapscripts");
         if (address == Pointer.NULL) return;
         var model = viewPort.Model;
         var scriptStart = address;
         while (model[scriptStart] != 0) {
            Scripts.Add(new(viewPort, scriptStart));
            AddDeleteHandler(Scripts.Count - 1);
            scriptStart += 5;
         }
         NotifyPropertiesChanged(nameof(CollectionExists), nameof(Unloaded), nameof(Address));
      }

      public bool CanCreateCollection => address < 0;
      public void CreateCollection() {
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var start = model.FindFreeSpace(model.FreeSpaceStart, 1);
         token.ChangeData(model, start, 0x00);
         address = start;
         owner.SetAddress("mapscripts", address);
         Scripts.Clear();
         NotifyPropertiesChanged(nameof(Unloaded), nameof(CollectionExists), nameof(Address));
      }

      public IReadOnlyList<int> GetScripts() {
         var results = new List<int>();
         foreach (var child in Scripts) results.AddRange(child.GetScripts());
         return results;
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
         model.ObserveRunWritten(token, new XSERun(newScript));
         Scripts.Add(new(viewPort, address + Scripts.Count * 5));
         AddDeleteHandler(Scripts.Count - 1);
         viewPort.ChangeHistory.ChangeCompleted();
      }

      private void AddDeleteHandler(int index) {
         Scripts[index].DeleteMe += HandleDelete;
      }

      private void HandleDelete(object sender, MapScriptDeleteEventArgs e) {
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var script = (MapScriptViewModel)sender;
         var index = Scripts.IndexOf(script);
         var tableRun = model.GetNextRun(address) as ITableRun;
         if (tableRun == null) return;
         var table = new ModelTable(model, tableRun, () => token); // type. pointer<>
         for (int i = index; i < Scripts.Count - 1; i++) {
            table[i].SetValue(0, table[i + 1].GetValue(0));
            table[i].SetAddress("pointer", table[i + 1].GetAddress("pointer"));
         }
         tableRun = tableRun.Append(token, -1);
         model.ObserveRunWritten(token, tableRun);
         Scripts[index].DeleteMe -= HandleDelete;
         Scripts.RemoveAt(index);
         e.Success = true;
         viewPort.ChangeHistory.ChangeCompleted();
      }
   }

   public class MapScriptDeleteEventArgs : EventArgs {
      public bool Success { get; set; }
   }

   public class MapScriptViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private readonly int start;
      private int scriptType, address;
      private string displayAddress;

      public event EventHandler<MapScriptDeleteEventArgs> DeleteMe;

      public bool HasSubScripts => scriptType == 2 || scriptType == 4;

      public ObservableCollection<VisualOption> ScriptOptions { get; } = new();
      public ObservableCollection<MapSubScriptViewModel> SubScripts { get; } = new();

      public MapScriptViewModel(IEditableViewPort viewPort, int start) {
         this.viewPort = viewPort;
         var model = viewPort.Model;
         this.start = start;
         this.scriptType = model[start];
         this.address = model.ReadPointer(start + 1);
         this.displayAddress = $"<{address:X6}>";
         Load();
         ScriptOptions.Add(new VisualOption {
            Option = "Load",
            ShortDescription = "Before layout is drawn",
            Description = "Almost exclusively used to set metatiles on the map before it's first drawn",
         });
         ScriptOptions.Add(new VisualOption {
            Option = "Per-Frame (Table)",
            ShortDescription = "Run every frame",
            Description = "Only the first script whose condition is satisfied is run. Used to trigger events.",
         });
         ScriptOptions.Add(new VisualOption {
            Option = "Transition",
            ShortDescription = "Run when switching maps",
            Description = "Used to set map-specific flags/vars, update object positions/movement types, set weather, etc",
         });
         ScriptOptions.Add(new VisualOption {
            Option = "Warp into Map (Table)",
            ShortDescription = "Run after objects are loaded",
            Description = "Only the first script whose condition is satisfied is run. Used to update facing / visibility or to add objects to the scene.",
         });
         ScriptOptions.Add(new VisualOption {
            Option = "Resume",
            ShortDescription = "Run after loading the map, or exiting the bag, or finishing a battle, etc",
            Description = "Used to hide defeated static pokemon, or maintain some map state",
         });
         ScriptOptions.Add(new VisualOption {
            Option = "Dive Warp",
            ShortDescription = "Run after the player dives or emerges",
            Description = "Only used to determine whether the player should emerge in the sealed chamber.",
         });
         ScriptOptions.Add(new VisualOption {
            Option = "Return to Field",
            ShortDescription = "Run after exiting bag or finishing battle, but not when entering a map",
            Description = "Used rarely, when something must only happen on reload.",
         });
      }

      // create one-use NoDataChangeDeltaModel tokens rather than using tokens from CurrentChange
      // we don't really care for these to be part of undo/redo
      private void Load() {
         var model = viewPort.Model;
         SubScripts.Clear();
         var token = new NoDataChangeDeltaModel();
         if (scriptType != 2 && scriptType != 4) {
            // we're pointing at an XSERun
            if (model.GetNextRun(address).Start >= address) {
               model.ObserveRunWritten(token, new XSERun(address));
            }
         } else {
            var destination = address;
            while (true) {
               var currentValue = model.ReadMultiByteValue(destination, 2);
               if (currentValue == 0 || currentValue == 0xFFFF) break;
               var child = new MapSubScriptViewModel(viewPort, destination);
               child.DeleteMe += HandleDelete;
               SubScripts.Add(child);
               destination += 8;
            }
            if (!ArrayRun.TryParse(model, "[variable:|h value: pointer<`xse`>]!0000", address, SortedSpan.One(start + 1), out var run).HasError) {
               model.ClearFormat(token, run.Start, run.Length);
               model.ObserveRunWritten(token, run);
            }
         }
      }

      public int ScriptTypeIndex {
         get => scriptType - 1;
         set => Set(ref scriptType, value + 1, arg => {
            NotifyPropertyChanged(nameof(HasSubScripts));
            var model = viewPort.Model;
            var token = viewPort.ChangeHistory.CurrentChange;
            if ((arg == 2 || arg == 4) && (scriptType != 2 && scriptType != 4)) {
               // if the old type is 2 or 4 and the new type is not, delete the content and replace it with a new 1-byte script `end` (02)
               int destination;
               if (SubScripts.Count == 0) {
                  destination = model.FindFreeSpace(model.FreeSpaceStart, 2);
                  token.ChangeData(model, destination, 0x02);
                  model.ObserveRunWritten(token, new XSERun(destination));
               } else {
                  destination = model.ReadPointer(SubScripts[0].Start + 4);
               }
               model.UpdateArrayPointer(token, default, default, -1, this.start + 1, destination);
               address = destination;
               this.displayAddress = $"<{address:X6}>";
               NotifyPropertyChanged(nameof(Address));
               Load();
            } else if ((scriptType == 2 || scriptType == 4) && (arg != 2 && arg != 4)) {
               // if the new type is 2 or 4 and the old type is not, move the current script to be the first SubScript of the new table
               var destination = model.FindFreeSpace(model.FreeSpaceStart, 10);
               model.WriteMultiByteValue(destination + 0, 2, token, 0x4000); // temp0
               model.WriteMultiByteValue(destination + 2, 2, token, 0);      // check = 0
                                                                             // by default, the script will now always run, until you make it stop!
               model.WritePointer(token, destination + 4, address);
               model.WriteMultiByteValue(destination + 8, 2, token, 0);
               model.UpdateArrayPointer(token, default, default, -1, this.start + 1, destination);
               address = destination;
               this.displayAddress = $"<{address:X6}>";
               NotifyPropertyChanged(nameof(Address));
               Load();
            }
            viewPort.ChangeHistory.CurrentChange.ChangeData(viewPort.Model, start, (byte)scriptType);
            NotifyPropertyChanged(nameof(HasSubScripts));
            viewPort.ChangeHistory.ChangeCompleted();
         });
      }

      public string Address {
         get => displayAddress;
         set => Set(ref displayAddress, value, arg => {
            var text = displayAddress.Trim('<', '>');
            if (text.TryParseHex(out var result)) {
               address = result;
               viewPort.Model.UpdateArrayPointer(viewPort.ChangeHistory.CurrentChange, default, default, default, start + 1, address);
               Load();
            }
         });
      }

      public IReadOnlyCollection<int> GetScripts() {
         var results = new List<int>();
         foreach (var script in SubScripts) {
            results.Add(viewPort.Model.ReadPointer(script.Start + 4));
         }
         if (scriptType != 2 && scriptType != 4) {
            results.Add(address);
         }
         return results;
      }

      public void AddSubScript() {
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;

         var run = model.GetNextRun(address);
         if (run is not ITableRun tableRun) return;
         tableRun = tableRun.Append(token, 1);
         model.ObserveRunWritten(token, tableRun);

         // add new element data
         var newScriptStart = model.FindFreeSpace(model.FreeSpaceStart, 1);
         token.ChangeData(model, newScriptStart, 0x02);
         model.UpdateArrayPointer(token, default, default, -1, tableRun.Start + (tableRun.ElementCount - 1) * tableRun.ElementLength + 4, newScriptStart);
         model.ObserveRunWritten(token, new XSERun(newScriptStart));

         address = tableRun.Start;
         displayAddress = $"<{address:X6}>";
         NotifyPropertyChanged(nameof(Address));
         if (run.Start != tableRun.Start) {
            Load();
         } else {
            SubScripts.Append(new MapSubScriptViewModel(viewPort, tableRun.Start + tableRun.ElementCount * tableRun.ElementLength - 4));
         }
      }

      public void Delete() {
         var args = new MapScriptDeleteEventArgs();
         DeleteMe.Raise(this, args);
         if (!args.Success) return;
         // NOTE maybe we should clear all the data in this script right here
      }

      public void Goto() => BlockMapViewModel.GotoAddress(viewPort, address);

      private void HandleDelete(object sender, MapScriptDeleteEventArgs e) {
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var script = (MapSubScriptViewModel)sender;
         var index = SubScripts.IndexOf(script);
         if (index == -1) return;

         var tableRun = model.GetNextRun(address) as ITableRun;
         if (tableRun == null) return;
         var table = new ModelTable(model, tableRun, () => token); // type. pointer<>
         for (int i = index; i < SubScripts.Count - 1; i++) {
            table[i].SetValue(0, table[i + 1].GetValue(0));
            table[i].SetValue(1, table[i + 1].GetValue(1));
            table[i].SetAddress("pointer", table[i + 1].GetAddress("pointer"));
         }
         tableRun = tableRun.Append(token, -1);
         model.ObserveRunWritten(token, tableRun);
         SubScripts[index].DeleteMe -= HandleDelete;
         SubScripts.RemoveAt(index);
         e.Success = true;
      }
   }

   /// <summary>
   /// Represents an indivdual map script from a map script table (type 2 or type 4).
   /// </summary>
   public class MapSubScriptViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private int start, variable, val, address;
      private string variableText, valueText, addressText;

      public int Start => start;

      public event EventHandler<MapScriptDeleteEventArgs> DeleteMe;

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
         viewPort.Model.UpdateArrayPointer(viewPort.ChangeHistory.CurrentChange, default, default, -1, start + 4, address);
      }); }

      public void Delete() {
         var args = new MapScriptDeleteEventArgs();
         DeleteMe.Raise(this, args);
         if (!args.Success) return;
         // leaving an orphan behind on purpose
      }

      public void Goto() => BlockMapViewModel.GotoAddress(viewPort, address);
   }

}
