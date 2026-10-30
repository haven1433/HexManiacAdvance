using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// Shown in the table tool under an overworld sprite's fields: the first frame of every overworld sprite, so you can
   /// see what you're editing and jump to another sprite with one click. Also lets you rename the current sprite:
   /// the names live in the 'objecteventgfx' list, which is saved with the ROM's .toml.
   /// </summary>
   public class SpriteGalleryElementViewModel : ViewModelCore, IArrayElementViewModel {
      public const string NameListName = "objecteventgfx";

      private readonly ViewPort viewPort;
      private readonly IDataModel model;

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      private readonly ObservableCollection<SpriteGalleryItem> elements = new();
      private bool loaded;
      /// <summary>Listed on first use: the table tool creates and discards these as the cursor moves.</summary>
      public ObservableCollection<SpriteGalleryItem> Elements { get { if (!loaded) Load(); return elements; } }
      public int CurrentIndex { get; private set; }

      private ObservableCollection<SpriteGalleryRow> rows = new();
      /// <summary>
      /// The sprites that pass the filter, in rows of <see cref="ColumnCount"/>. The view shows rows rather than sprites
      /// because a list of rows can be virtualized: only the rows on screen get any controls (there are over a thousand sprites).
      /// </summary>
      public ObservableCollection<SpriteGalleryRow> Rows { get { if (!loaded) Load(); return rows; } }

      private int columnCount = 6;
      public int ColumnCount => columnCount;

      /// <summary>The room one sprite takes up in a row: its 44-wide picture plus the button's margin, border and padding.</summary>
      public const double ItemOuterWidth = 54;

      /// <summary>The view tells us how wide the list is so we know how many sprites fit in a row.</summary>
      public void SetAvailableWidth(double width) {
         if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0) return;
         var columns = SpriteGalleryRow.ColumnsFor(width, ItemOuterWidth);
         if (columns == columnCount) return;
         columnCount = columns;
         if (loaded) UpdateRows();
      }

      private void UpdateRows() {
         rows = new ObservableCollection<SpriteGalleryRow>(SpriteGalleryRow.Chunk(elements, columnCount));
         NotifyPropertyChanged(nameof(Rows));
      }

      private string currentName = string.Empty;
      /// <summary>The current sprite's name. Changing it updates the name list that every dropdown and the gallery use.</summary>
      public string CurrentName {
         get => currentName;
         set {
            if (currentName == value) return;
            currentName = value ?? string.Empty;
            NotifyPropertyChanged();
            RenameCurrent(currentName);
         }
      }

      private string filter = string.Empty;
      public string Filter {
         get => filter;
         set {
            if (!TryUpdate(ref filter, value ?? string.Empty)) return;
            foreach (var item in elements) item.MatchToFilter(filter);
            if (loaded) UpdateRows();
         }
      }

      private double spriteScale = 2;
      public double SpriteScale {
         get => spriteScale;
         set => Set(ref spriteScale, value.LimitToRange(1, 3), old => { foreach (var item in elements) item.SpriteScale = spriteScale; });
      }

      private string status = string.Empty;
      public string Status { get => status; private set => Set(ref status, value); }

      public ICommand OpenGallery { get; }
      public ICommand Refresh { get; }
      /// <summary>Parameter: the SpriteGalleryItem that was clicked.</summary>
      public ICommand SelectCommand { get; }

      public SpriteGalleryElementViewModel(ViewPort viewPort, int index) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         CurrentIndex = index;
         OpenGallery = new StubCommand { CanExecute = arg => true, Execute = arg => viewPort.OpenSpriteGalleryTab() };
         Refresh = new StubCommand { CanExecute = arg => true, Execute = arg => { model.ClearCacheScope(); Load(); NotifyPropertyChanged(nameof(Rows)); } };
         SelectCommand = new StubCommand { CanExecute = arg => true, Execute = arg => Select(arg as SpriteGalleryItem) };
         var names = model.GetOptions(HardcodeTablesModel.OverworldSprites);
         currentName = names != null && index < names.Count ? names[index] : string.Empty;
      }

      /// <summary>
      /// List every sprite. Nothing is drawn here: each picture is drawn when the view first asks for it (see <see cref="OverworldSpriteRenderer"/>),
      /// and with the rows virtualized that's only the ones on screen.
      /// </summary>
      private void Load() {
         loaded = true;
         elements.Clear();
         selectedItem = null;
         var ows = model.GetTable(HardcodeTablesModel.OverworldSprites);
         if (ows == null) { rows = new(); return; }
         var names = model.GetOptions(HardcodeTablesModel.OverworldSprites);
         var sprites = renderer = OverworldSpriteRenderer.Get(model);
         for (int i = 0; i < ows.ElementCount; i++) {
            var index = i;
            var name = names != null && i < names.Count ? names[i] : string.Empty;
            var item = new SpriteGalleryItem(i, ows.Start + ows.ElementLength * i, name, () => sprites.Get(index)) { SpriteScale = spriteScale, SelectCommand = SelectCommand };
            item.MatchToFilter(filter);
            elements.Add(item);
         }
         if (CurrentIndex >= 0 && CurrentIndex < elements.Count) { selectedItem = elements[CurrentIndex]; selectedItem.Selected = true; }
         currentName = names != null && CurrentIndex < names.Count ? names[CurrentIndex] : string.Empty;
         NotifyPropertyChanged(nameof(CurrentName));
         Status = $"{ows.ElementCount} sprites";
         rows = new ObservableCollection<SpriteGalleryRow>(SpriteGalleryRow.Chunk(elements, columnCount)); // no change notification: the view is reading the list right now
      }

      private SpriteGalleryItem selectedItem;
      private OverworldSpriteRenderer renderer;

      /// <summary>Jump the table tool to another sprite.</summary>
      public void Select(SpriteGalleryItem item) {
         if (item == null) return;
         viewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/{item.Index}");
      }

      private void RenameCurrent(string name) {
         name = (name ?? string.Empty).Trim();
         if (!model.TryGetList(NameListName, out var list)) return;
         var names = list.ToList();
         while (names.Count <= CurrentIndex) names.Add(names.Count.ToString());
         if (string.IsNullOrEmpty(name)) name = CurrentIndex.ToString();
         // list entries can't have spaces: they're used as identifiers in scripts and dropdowns
         name = name.Replace(' ', '_');
         if (names[CurrentIndex] == name) return;
         names[CurrentIndex] = name;
         model.SetList(viewPort.CurrentChange, NameListName, names, list.Comments, StoredList.GenerateHash(names));
         model.ClearCacheScope();
         viewPort.ChangeHistory.ChangeCompleted();
         var item = elements.FirstOrDefault(element => element.Index == CurrentIndex);
         if (item != null) item.Name = name;
         if (currentName != name) { currentName = name; NotifyPropertyChanged(nameof(CurrentName)); }
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not SpriteGalleryElementViewModel gallery) return false;
         if (gallery.model != model) return false;
         CurrentIndex = gallery.CurrentIndex;
         // only the old and the new sprite change, there is no need to go over all of them
         if (loaded) {
            var current = CurrentIndex >= 0 && CurrentIndex < elements.Count ? elements[CurrentIndex] : null;
            if (!ReferenceEquals(selectedItem, current)) {
               if (selectedItem != null) selectedItem.Selected = false;
               selectedItem = current;
               if (current != null) current.Selected = true;
            }
         }
         var names = model.GetOptions(HardcodeTablesModel.OverworldSprites);
         currentName = names != null && CurrentIndex < names.Count ? names[CurrentIndex] : string.Empty;
         NotifyPropertyChanged(nameof(CurrentName));
         NotifyPropertyChanged(nameof(CurrentIndex));
         // the sprite being edited may have changed: draw just that one again (if it's in the list at all)
         if (selectedItem != null && selectedItem.IsRendered) {
            var redrawn = renderer?.Redraw(CurrentIndex);
            if (redrawn != null) selectedItem.Replace(redrawn.Value.image, redrawn.Value.isPlaceholder);
         }
         Visible = other.Visible;
         return true;
      }
   }
}
