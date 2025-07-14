using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableTool : ViewModelCore, IToolViewModel {
      public readonly IDataModel model;
      public readonly Selection selection;
      public readonly ChangeHistory<ModelDelta> history;
      public readonly IToolTrayViewModel toolTray;
      public readonly IWorkDispatcher dispatcher;
      public readonly IDelayWorkTimer loadMapUsageTimer, dataChangedTimer;

      public string Name => "Table";

      public IReadOnlyList<string> TableSections {
         get {
            var sections = UnmatchedArrays.Select(array => {
               var parts = model.GetAnchorFromAddress(-1, array.Start).Split('.');
               if (parts.Length > 2) return string.Join(".", parts.Take(2));
               return parts[0];
            }).Distinct().ToList();
            sections.Sort();
            return sections;
         }
      }

      public int selectedTableSection;

      public IReadOnlyList<string> TableList {
         get {
            if (selectedTableSection == -1 || selectedTableSection >= TableSections.Count) return new string[0];
            var selectedSection = TableSections[selectedTableSection];
            var tableList = UnmatchedArrays
               .Select(array => model.GetAnchorFromAddress(-1, array.Start))
               .Where(name => name.StartsWith(selectedSection + "."))
               .Select(name => name.Substring(selectedSection.Length + 1))
               .ToList();
            tableList.Sort();
            return tableList;
         }
      }

      public int selectedTableIndex;

      public IReadOnlyList<ITableRun> UnmatchedArrays {
         get {
            var list = new List<ITableRun>();
            foreach (var anchor in model.Anchors) {
               var table = model.GetTable(anchor);
               if (table is null) continue; // skip anything that's not a table
               if (table is ArrayRun array && !string.IsNullOrEmpty(array.LengthFromAnchor)) continue; // skip 'matched-length' arrays
               list.Add(table);
            }
            return list;
         }
      }

      public string currentElementName;
      public string CurrentElementName {
         get => currentElementName;
         set => TryUpdate(ref currentElementName, value);
      }

      public FilteringComboOptions CurrentElementSelector { get; }

      public int addCount = 1;

      public bool useMultiFieldFeature = false;

      public ObservableCollection<IArrayElementViewModel> UsageChildren { get; }
      public ObservableCollection<TableGroupViewModel> Groups { get; }

      // the address is the address not of the entire array, but of the current index of the array
      public int address = Pointer.NULL;

      public bool enabled;
      public bool Enabled {
         get => enabled;
         set => TryUpdate(ref enabled, value);
      }

      public bool usageOptionsOpen;
      public bool UsageOptionsOpen { get => usageOptionsOpen; set => Set(ref usageOptionsOpen, value); }

      public string fieldFilter = string.Empty;

#pragma warning disable 0067 // it's ok if events are never used after implementing an interface
      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler RequestMenuClose;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved; // invoke when a new item gets added and the table has to move
#pragma warning restore 0067

      // properties that exist solely so the UI can remember things when the tab switches
      public double VerticalOffset { get; set; }

      public bool ignoreFurtherCommands = false;

      public IList<IArrayElementViewModel> Children => Groups.SelectMany(group => group.Members).ToList();

      public int childIndexGroup = 0;

      public bool HasUsageOptions {
         get {
            foreach(var child in UsageChildren) {
               if (child is not MapOptionsArrayElementViewModel mapUsage) return true;
               return mapUsage.MapPreviews.Count > 0;
            }
            return false;
         }
      }

      public int usageChildInsertionIndex = 0;

      public bool dataForCurrentRunChangeUpdate;

      public void UpdateCurrentElementSelector(ITableRun array, int index) {
         SetupFromModel(array.Start + array.ElementLength * index);
      }

      public bool selfChange = false;
      public void SetupFromModel(int address) {
         if (!(model.GetNextRun(address) is ITableRun tableRun)) return;
         var offset = tableRun.ConvertByteOffsetToArrayOffset(address);
         var allOptions = tableRun.ElementNames?.ToList();
         if (allOptions == null) allOptions = tableRun.ElementCount.Range().Select(i => i.ToString()).ToList();
         while (allOptions.Count < tableRun.ElementCount) {
            allOptions.Add(allOptions.Count.ToString());
         }
         var selectedIndex = offset.ElementIndex;
         var options = new List<ComboOption>();
         for (int i = 0; i < allOptions.Count; i++) {
            // var image = ToolTipContentVisitor.GetEnumImage(model, i, tableRun as ArrayRun);
            // if (image != null) options.Add(VisualComboOption.CreateFromSprite(allOptions[i], image.PixelData, image.PixelWidth, i, 1, true));
            // else
            options.Add(new ComboOption(allOptions[i], i));
         }
         using (Scope(ref selfChange, true, old => selfChange = old)) {
            CurrentElementSelector.Update(options, selectedIndex);
         }
      }

      public void ForwardModelDataMoved(object sender, (int originalStart, int newStart) e) => ModelDataMoved?.Invoke(this, e);
   }
}
