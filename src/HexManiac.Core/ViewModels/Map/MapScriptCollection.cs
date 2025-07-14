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
      public readonly IEditableViewPort viewPort;
      public ModelArrayElement owner;
      public int address;

      public int Address => address;
      public bool Unloaded => address == 0;
      public ObservableCollection<MapScriptViewModel> Scripts { get; } = new();
      public bool CollectionExists => address > 0;

      public event EventHandler<NewMapScriptsCreatedEventArgs> NewMapScriptsCreated;

      public MapScriptCollection(IEditableViewPort viewPort) => this.viewPort = viewPort;

      public bool CanCreateCollection => address < 0;

      public IReadOnlyList<int> GetScripts() {
         var results = new List<int>();
         foreach (var child in Scripts) results.AddRange(child.GetScripts());
         return results;
      }
   }

   public class MapScriptDeleteEventArgs : EventArgs {
      public bool Success { get; set; }
   }

   public class MapScriptViewModel : ViewModelCore {
      public readonly IEditableViewPort viewPort;
      public readonly int start;
      public int scriptType, address;
      public string displayAddress;

      public event EventHandler<MapScriptDeleteEventArgs> DeleteMe;

      public bool HasSubScripts => scriptType == 2 || scriptType == 4;

      public ObservableCollection<VisualOption> ScriptOptions { get; } = new();
      public ObservableCollection<MapSubScriptViewModel> SubScripts { get; } = new();

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

      public void Delete() {
         var args = new MapScriptDeleteEventArgs();
         DeleteMe.Raise(this, args);
         if (!args.Success) return;
         // NOTE maybe we should clear all the data in this script right here
      }
   }

   /// <summary>
   /// Represents an indivdual map script from a map script table (type 2 or type 4).
   /// </summary>
   public class MapSubScriptViewModel : ViewModelCore {
      public readonly IEditableViewPort viewPort;
      public int start, variable, val, address;
      public string variableText, valueText, addressText;

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

      public void Delete() {
         var args = new MapScriptDeleteEventArgs();
         DeleteMe.Raise(this, args);
         if (!args.Success) return;
         // leaving an orphan behind on purpose
      }
   }
}
