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

      public MapOptionsArrayElementViewModel(IWorkDispatcher dispatcher, MapEditorViewModel mapEditor, string tableName, int index) {
         this.dispatcher = dispatcher;
         (this.mapEditor, this.tableName, this.index) = (mapEditor, tableName, index);
         dispatcher.RunBackgroundWork(Load);
      }

      private void Load() {
         void Add(GotoMapButton button) {
            Visible = true;
            dispatcher.BlockOnUIWork(() => MapPreviews.Add(button));
         }
         if (tableName == HardcodeTablesModel.OverworldSprites) {
            foreach (var button in FindOverworldUses()) Add(button);
         } else {
            foreach (var button in FindObjectUses()) Add(button);
         }
      }

      private IEnumerable<GotoMapButton> FindOverworldUses() {
         // look for any map with an event using this sprite
         var allMaps = AllMapsModel.Create(mapEditor.ViewPort.Model);
         for (int bankIndex = 0; bankIndex < allMaps.Count; bankIndex++) {
            var bank = allMaps[bankIndex];
            for (int mapIndex = 0; mapIndex < bank.Count; mapIndex++) {
               var map = bank[mapIndex];
               foreach (var ev in map.Events.Objects) {
                  if (cancel) yield break;
                  if (ev.Graphics != index) continue;
                  var button = new GotoMapButton(mapEditor, this, bankIndex, mapIndex, ev);
                  if (button.Image == null) continue;
                  yield return button;
               }
            }
         }
      }

      private IEnumerable<GotoMapButton> FindObjectUses() {
         var model = mapEditor.ViewPort.Model;
         var parser = mapEditor.ViewPort.Tools.CodeTool.ScriptParser;
         var lines = parser.DependsOn(tableName).ToList();
         var filter = new List<byte>();
         foreach (var line in lines) {
            if (line is MacroScriptLine macro && macro.Args[0] is SilentMatchArg silent) filter.Add(silent.ExpectedValue);
            if (line is ScriptLine sl) filter.Add(line.LineCode[0]);
         }

         var allMaps = AllMapsModel.Create(model);
         var isItemTable = tableName == HardcodeTablesModel.ItemsTableName;
         var isMapNameTable = tableName == HardcodeTablesModel.MapNameTable;
         for (int bankIndex = 0; bankIndex < allMaps.Count; bankIndex++) {
            var bank = allMaps[bankIndex];
            for (int mapIndex = 0; mapIndex < bank.Count; mapIndex++) {
               var map = bank[mapIndex];
               foreach (var ev in map.Events.Objects.Concat<IScriptEventModel>(map.Events.Scripts)) {
                  if (ev is SignpostEventModel sp && !sp.HasScript) continue;
                  if (cancel) yield break;
                  var spots = Flags.GetAllScriptSpots(model, parser, new[] { ev.ScriptAddress }, filter.ToArray());

                  // if any of these spots match, then this object's script refers to this enum
                  foreach (var spot in spots) {
                     int check = spot.Address + spot.Line.LineCode.Count;
                     bool match = false;
                     foreach (var arg in spot.Line.Args) {
                        if (cancel) yield break;
                        var length = arg.Length(model, check);
                        if (arg.EnumTableName == tableName) {
                           if (model.ReadMultiByteValue(check, length) == index) {
                              var button = new GotoMapButton(mapEditor, this, bankIndex, mapIndex, ev);
                              if (button.Image == null) continue;
                              yield return button;
                              match = true;
                              break;
                           }
                        }
                        check += length;
                     }
                     if (match) break;
                  }
               }
               if (isItemTable) {
                  foreach (var ev in map.Events.Signposts) {
                     if (cancel) yield break;
                     if (ev.IsHiddenItem && ev.ItemValue == index) {
                        var button = new GotoMapButton(mapEditor, this, bankIndex, mapIndex, ev);
                        if (button.Image == null) continue;
                        yield return button;
                     }
                  }
               } else if (isMapNameTable) {
                  if (map.NameIndex != index) continue;
                  var button = new GotoMapButton(mapEditor, this, bankIndex, mapIndex, null);
                  if (button.Image == null) continue;
                  yield return button;
               }
            }
         }
      }
   }

   public class GotoMapButton : ViewModelCore {
      private readonly MapEditorViewModel mapEditor;
      private readonly MapOptionsArrayElementViewModel owner;
      private readonly int bank, map;
      private IEventModel eventModel;
      public IPixelViewModel Image { get; init; }
      public GotoMapButton(MapEditorViewModel mapEditor, MapOptionsArrayElementViewModel owner, int bank, int map, IEventModel eventViewModel) {
         (this.mapEditor, this.owner) = (mapEditor, owner);
         (this.bank, this.map) = (bank, map);
         this.eventModel = eventViewModel;
         if (eventViewModel == null) {
            Image = mapEditor.GetMapPreview(bank, map, 7);
         } else {
            Image = mapEditor.GetMapPreview(bank, map, eventViewModel.X, eventViewModel.Y);
         }
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
