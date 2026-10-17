using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ButtonArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      public string Text { get; private set; }
      public string ToolTipText { get; private set; }
      public ICommand Command { get; private set; }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public ButtonArrayElementViewModel(string text, Action action) {
         Text = text;
         ToolTipText = text;
         Command = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => action(),
         };
      }

      public ButtonArrayElementViewModel(string text, string toolTip, Action action) {
         Text = text;
         ToolTipText = toolTip;
         Command = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => action(),
         };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is ButtonArrayElementViewModel button)) return false;
         if (Text != button.Text) return false;
         if (ToolTipText != button.ToolTipText) return false;
         Command = button.Command;
         Visible = other.Visible;
         NotifyPropertyChanged(nameof(Command));
         return true;
      }
   }

   public class MapOptionsArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly IWorkDispatcher dispatcher;
      private readonly MapEditorViewModel mapEditor;
      private readonly string tableName;
      private readonly int index;
      private bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;

      public string ErrorText => string.Empty;

      private int zIndex;
      public int ZIndex { get => zIndex; set => Set(ref zIndex, value); }

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      // if we're trying to copy data from another element,
      // cancel any remaining work on this one
      private bool cancel;
      public bool TryCopy(IArrayElementViewModel other) {
         cancel = true;
         return false;
      }

      private bool showPreviews;
      public bool ShowPreviews {
         get => showPreviews;
         set => Set(ref showPreviews, value);
      }

      public ObservableCollection<GotoMapButton> MapPreviews { get; } = new();

      public MapOptionsArrayElementViewModel(IWorkDispatcher dispatcher, IDelayWorkTimer timer, MapEditorViewModel mapEditor, string tableName, int index) {
         this.dispatcher = dispatcher;
         (this.mapEditor, this.tableName, this.index) = (mapEditor, tableName, index);
         timer.DelayCall(TimeSpan.FromSeconds(.5), Load);
      }

      private void Load() {
         // The delayed call lands on the UI thread. Searching every map for uses of this element is slow,
         // so do the search on a background thread and only touch the UI to add the results.
         dispatcher.RunBackgroundWork(() => {
            try {
               var buttons = tableName == HardcodeTablesModel.OverworldSprites ? FindOverworldUses() : FindObjectUses();
               foreach (var button in buttons) {
                  if (cancel) return;
                  dispatcher.BlockOnUIWork(() => {
                     if (cancel) return;
                     Visible = true;
                     MapPreviews.Add(button);
                  });
               }
            } catch (Exception ex) {
               // an unexpected problem with one map's data shouldn't crash the editor: it just means the 'maps' button won't show
               System.Diagnostics.Debug.WriteLine($"Could not find map uses for {tableName}/{index}: {ex}");
            }
         });
      }

      private IEnumerable<GotoMapButton> FindOverworldUses() {
         // look for any map with an event using this sprite
         var model = mapEditor.ViewPort.Model;
         var uses = model.CurrentCacheScope.GetOrAdd("map-usage-overworld", () => BuildOverworldUseIndex(model));
         if (!uses.TryGetValue(index, out var matches)) yield break;
         foreach (var use in matches) {
            if (cancel) yield break;
            yield return new GotoMapButton(mapEditor, this, use.Bank, use.Map, use.Event);
         }
      }

      private IEnumerable<GotoMapButton> FindObjectUses() {
         var model = mapEditor.ViewPort.Model;
         var parser = mapEditor.ViewPort.Tools.CodeTool.ScriptParser;
         var uses = model.CurrentCacheScope.GetOrAdd("map-usage:" + tableName, () => BuildObjectUseIndex(model, parser, tableName));
         if (!uses.TryGetValue(index, out var matches)) yield break;
         foreach (var use in matches) {
            if (cancel) yield break;
            yield return new GotoMapButton(mapEditor, this, use.Bank, use.Map, use.Event);
         }
      }

      private record MapUse(int Bank, int Map, IEventModel Event);

      /// <summary>
      /// Searching every event script in every map is expensive.
      /// Instead of searching again for each table element, find the uses of every element in one pass.
      /// The results are cached until the model data changes, so stepping through a table doesn't repeat the search.
      /// </summary>
      private static IReadOnlyDictionary<int, List<MapUse>> BuildObjectUseIndex(IDataModel model, ScriptParser parser, string tableName) {
         var results = new Dictionary<int, List<MapUse>>();
         void AddUse(int value, MapUse use) {
            if (!results.TryGetValue(value, out var list)) results[value] = list = new();
            list.Add(use);
         }

         var lines = parser.DependsOn(tableName).ToList();
         var filter = new List<byte>();
         foreach (var line in lines) {
            if (line is MacroScriptLine macro && macro.Args[0] is SilentMatchArg silent) filter.Add(silent.ExpectedValue);
            if (line is ScriptLine sl) filter.Add(line.LineCode[0]);
         }
         var filterArray = filter.ToArray();

         var isItemTable = tableName == HardcodeTablesModel.ItemsTableName;
         var isMapNameTable = tableName == HardcodeTablesModel.MapNameTable;
         var checkScripts = lines.Count > 0; // if no script command uses this table, no script can match it
         var allMaps = AllMapsModel.Create(model);
         for (int bankIndex = 0; bankIndex < allMaps.Count; bankIndex++) {
            var bank = allMaps[bankIndex];
            if (bank == null) continue;
            for (int mapIndex = 0; mapIndex < bank.Count; mapIndex++) {
               var map = bank[mapIndex];
               if (map == null) continue;
               var validPreview = map.Layout.Width.InRange(1, 0x100) && map.Layout.Height.InRange(1, 0x100);
               if (checkScripts && validPreview && map.Events is { } events) {
                  foreach (var ev in events.Objects.Concat<IScriptEventModel>(events.Scripts)) {
                     if (ev is SignpostEventModel sp && !sp.HasScript) continue;
                     var valuesForEvent = new List<int>();
                     foreach (var spot in Flags.GetAllScriptSpots(model, parser, new[] { ev.ScriptAddress }, filterArray)) {
                        int check = spot.Address + spot.Line.LineCode.Count;
                        foreach (var arg in spot.Line.Args) {
                           var length = arg.Length(model, check);
                           if (arg.EnumTableName == tableName) {
                              var value = model.ReadMultiByteValue(check, length);
                              if (!valuesForEvent.Contains(value)) valuesForEvent.Add(value);
                           }
                           check += length;
                        }
                     }
                     foreach (var value in valuesForEvent) AddUse(value, new(bankIndex, mapIndex, ev));
                  }
               }
               if (isItemTable && validPreview && map.Events is { } signEvents) {
                  foreach (var ev in signEvents.Signposts) {
                     if (ev.IsHiddenItem) AddUse(ev.ItemValue, new(bankIndex, mapIndex, ev));
                  }
               } else if (isMapNameTable) {
                  AddUse(map.NameIndex, new(bankIndex, mapIndex, null));
               }
            }
         }
         return results;
      }

      private static IReadOnlyDictionary<int, List<MapUse>> BuildOverworldUseIndex(IDataModel model) {
         var results = new Dictionary<int, List<MapUse>>();
         var allMaps = AllMapsModel.Create(model);
         for (int bankIndex = 0; bankIndex < allMaps.Count; bankIndex++) {
            var bank = allMaps[bankIndex];
            if (bank == null) continue;
            for (int mapIndex = 0; mapIndex < bank.Count; mapIndex++) {
               var map = bank[mapIndex];
               if (map is null) continue;
               if (map.Events is not Models.Map.EventGroupModel events) continue;
               if (!map.Layout.Width.InRange(1, 0x100) || !map.Layout.Height.InRange(1, 0x100)) continue;
               foreach (var ev in events.Objects) {
                  if (!results.TryGetValue(ev.Graphics, out var list)) results[ev.Graphics] = list = new();
                  list.Add(new(bankIndex, mapIndex, ev));
               }
            }
         }
         return results;
      }
   }

   public class GotoMapButton : ViewModelCore {
      private readonly MapEditorViewModel mapEditor;
      private readonly MapOptionsArrayElementViewModel owner;
      private readonly int bank, map;
      private IEventModel eventModel;

      // Rendering a preview means rendering the whole map.
      // Previews are only visible once the 'maps' popup is opened, so render them on demand.
      private IPixelViewModel image;
      private bool imageLoaded;
      public IPixelViewModel Image {
         get {
            if (!imageLoaded) {
               imageLoaded = true;
               image = eventModel == null ? mapEditor.GetMapPreview(bank, map, 7) : mapEditor.GetMapPreview(bank, map, eventModel.X, eventModel.Y);
            }
            return image;
         }
         init {
            image = value;
            imageLoaded = true;
         }
      }

      public GotoMapButton(MapEditorViewModel mapEditor, MapOptionsArrayElementViewModel owner, int bank, int map, IEventModel eventViewModel) {
         (this.mapEditor, this.owner) = (mapEditor, owner);
         (this.bank, this.map) = (bank, map);
         this.eventModel = eventViewModel;
      }
      public void Goto() {
         owner.ShowPreviews = false;
         mapEditor.ViewPort.Tools.TableTool.UsageOptionsOpen = false;
         var blockmap = new BlockMapViewModel(mapEditor.FileSystem, mapEditor.Tutorials, mapEditor.ViewPort, mapEditor.Format, mapEditor.Templates, bank, map) { AllOverworldSprites = mapEditor.PrimaryMap.AllOverworldSprites, IncludeBorders = false };
         var (x, y) = eventModel != null ? (eventModel.X, eventModel.Y) : (blockmap.PixelWidth / 32, blockmap.PixelHeight / 32);
         mapEditor.NavigateTo(bank, map, x, y);
         if (eventModel != null) mapEditor.SelectedEvent = mapEditor.PrimaryMap.EventGroup.All.FirstOrDefault(ev => ev.Element.Start == eventModel.Element.Start);
         mapEditor.ViewPort.RaiseRequestTabChange(mapEditor);
      }
   }
}
