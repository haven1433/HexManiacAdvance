using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A browsable grid of every overworld sprite in the ROM (first frame, facing down), with its index and name.
   /// Clicking an entry jumps the main tab to that entry of graphics.overworld.sprites.
   /// </summary>
   public class SpriteGalleryTab : ViewModelCore, ITabContent {
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly StubCommand close = new();

      public ObservableCollection<SpriteGalleryItem> Elements { get; } = new();

      private ObservableCollection<SpriteGalleryRow> rows = new();
      /// <summary>
      /// The sprites that pass the filter, in rows of <see cref="ColumnCount"/>. The view shows rows rather than sprites
      /// because a list of rows can be virtualized: only the rows on screen get any controls.
      /// </summary>
      public ObservableCollection<SpriteGalleryRow> Rows => rows;

      private int columnCount = 8;
      public int ColumnCount => columnCount;

      /// <summary>The room one sprite takes up in a row: the picture, its label's room, and the margin, border and padding around it.</summary>
      public double ItemOuterWidth => 48 * SpriteScale + 28;

      /// <summary>The view tells us how wide the list is so we know how many sprites fit in a row.</summary>
      public void SetAvailableWidth(double width) {
         if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0) return;
         availableWidth = width;
         UpdateColumns();
      }

      private double availableWidth;
      private void UpdateColumns() {
         if (availableWidth <= 0) return;
         var columns = SpriteGalleryRow.ColumnsFor(availableWidth, ItemOuterWidth);
         if (columns == columnCount) return;
         columnCount = columns;
         UpdateRows();
      }

      private void UpdateRows() {
         rows = new ObservableCollection<SpriteGalleryRow>(SpriteGalleryRow.Chunk(Elements, columnCount));
         NotifyPropertyChanged(nameof(Rows));
         NotifyPropertyChanged(nameof(ColumnCount));
      }

      public string Name => "Overworld Sprites";
      public bool SpartanMode { get; set; }
      public IDataModel Model => model;

      private string filter = string.Empty;
      public string Filter {
         get => filter;
         set {
            if (!TryUpdate(ref filter, value)) return;
            foreach (var item in Elements) item.MatchToFilter(filter);
            UpdateRows();
         }
      }

      private double spriteScale = 2;
      public double SpriteScale {
         get => spriteScale;
         set => Set(ref spriteScale, value.LimitToRange(1, 4), old => {
            foreach (var item in Elements) item.SpriteScale = spriteScale;
            NotifyPropertyChanged(nameof(ItemOuterWidth));
            UpdateColumns();
         });
      }

      private string status = string.Empty;
      public string Status { get => status; private set => Set(ref status, value); }

      public bool IsMetadataOnlyChange => false;
      public ICommand Save { get; }
      public ICommand SaveAs { get; }
      public ICommand ExportBackup { get; } = new StubCommand();
      public ICommand Undo { get; }
      public ICommand Redo { get; }
      public ICommand Copy { get; } = new StubCommand();
      public ICommand DeepCopy { get; } = new StubCommand();
      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close => close;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      private StubCommand refresh;
      public ICommand Refresh => StubCommand(ref refresh, Reload);

      public event EventHandler<string> OnError;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      event EventHandler<string> ITabContent.OnMessage { add { } remove { } }
      event EventHandler ITabContent.ClearMessage { add { } remove { } }
      event EventHandler<Action> ITabContent.RequestDelayedWork { add { } remove { } }
      event EventHandler ITabContent.RequestMenuClose { add { } remove { } }
      event EventHandler<Direction> ITabContent.RequestDiff { add { } remove { } }
      event EventHandler<CanDiffEventArgs> ITabContent.RequestCanDiff { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      public SpriteGalleryTab(ViewPort viewPort) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         Save = viewPort.Save;
         SaveAs = viewPort.SaveAs;
         Undo = viewPort.Undo;
         Redo = viewPort.Redo;
         close.CanExecute = arg => true;
         close.Execute = arg => Closed?.Invoke(this, EventArgs.Empty);
         Build();
      }

      /// <summary>Draw everything again (the Refresh button).</summary>
      public void Reload() {
         model.ClearCacheScope();
         Build();
      }

      private OverworldSpriteRenderer renderer;

      /// <summary>
      /// Lists every sprite. The pictures are drawn when the view first asks for them (see <see cref="OverworldSpriteRenderer"/>),
      /// so this is quick no matter how many sprites there are.
      /// </summary>
      private void Build() {
         Elements.Clear();
         renderer = null;
         var ows = model.GetTable(HardcodeTablesModel.OverworldSprites);
         if (ows == null) { Status = $"This ROM has no {HardcodeTablesModel.OverworldSprites} table."; UpdateRows(); return; }
         var sprites = renderer = OverworldSpriteRenderer.Get(model);
         var names = model.GetOptions(HardcodeTablesModel.OverworldSprites);
         for (int i = 0; i < ows.ElementCount; i++) {
            var index = i;
            var name = names != null && i < names.Count ? names[i] : string.Empty;
            Elements.Add(new SpriteGalleryItem(i, ows.Start + ows.ElementLength * i, name, () => sprites.Get(index)) { SpriteScale = SpriteScale });
         }
         foreach (var item in Elements) item.MatchToFilter(filter);
         Status = $"{ows.ElementCount} overworld sprites";
         UpdateRows();
      }

      /// <summary>
      /// Jump the main tab to this sprite's entry in the overworld sprite table.
      /// </summary>
      public void Select(SpriteGalleryItem item) {
         if (item == null) return;
         foreach (var element in Elements) element.Selected = element == item;
         viewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/{item.Index}");
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      /// <summary>
      /// The editor calls this every time the tab is selected. Nothing needs redoing unless the data changed in the meantime
      /// (the model hands out a new renderer then), so coming back to the gallery is instant.
      /// </summary>
      void ITabContent.Refresh() {
         if (renderer != null && Elements.Count > 0 && ReferenceEquals(renderer, OverworldSpriteRenderer.Get(model))) return;
         Build();
      }
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;
   }

   /// <summary>A line of sprites in a gallery. The galleries are lists of rows so the view can virtualize them.</summary>
   public class SpriteGalleryRow {
      public IReadOnlyList<SpriteGalleryItem> Items { get; }

      public SpriteGalleryRow(IReadOnlyList<SpriteGalleryItem> items) => Items = items;

      /// <summary>How many sprites fit side by side in <paramref name="width"/> (at least one).</summary>
      public static int ColumnsFor(double width, double itemWidth) {
         const double scrollBarRoom = 20;
         if (itemWidth <= 0) return 1;
         return Math.Max(1, (int)Math.Floor((width - scrollBarRoom) / itemWidth));
      }

      /// <summary>Lays out the sprites that aren't filtered out in rows of <paramref name="columns"/> sprites.</summary>
      public static List<SpriteGalleryRow> Chunk(IEnumerable<SpriteGalleryItem> items, int columns) {
         columns = Math.Max(1, columns);
         var rows = new List<SpriteGalleryRow>();
         var current = new List<SpriteGalleryItem>(columns);
         foreach (var item in items) {
            if (item.IsFilteredOut) continue;
            current.Add(item);
            if (current.Count < columns) continue;
            rows.Add(new SpriteGalleryRow(current));
            current = new List<SpriteGalleryItem>(columns);
         }
         if (current.Count > 0) rows.Add(new SpriteGalleryRow(current));
         return rows;
      }
   }

   public class SpriteGalleryItem : ViewModelCore, IPixelViewModel {
      private IPixelViewModel image;
      private bool isPlaceholder;
      private Func<(IPixelViewModel image, bool isPlaceholder)> render;

      public int Index { get; }
      public int Address { get; }
      private string name;
      /// <summary>The sprite's name from the objecteventgfx list (editable: see SpriteGalleryElementViewModel).</summary>
      public string Name { get => name; set { if (TryUpdate(ref name, value ?? string.Empty)) NotifyPropertyChanged(nameof(Label)); } }

      /// <summary>True if the sprite has no usable graphics and the default sprite is shown instead.</summary>
      public bool IsPlaceholder { get { EnsureImage(); return isPlaceholder; } }

      /// <summary>The original sprite number first, then the name in parentheses: "12 (MAY_NORMAL)".</summary>
      public string Label => string.IsNullOrEmpty(Name) || Name == Index.ToString() ? $"{Index}" : $"{Index} ({Name})";

      /// <summary>Clicking the sprite runs this with the sprite as the parameter.</summary>
      public ICommand SelectCommand { get; set; }

      /// <summary>False until somebody has looked at the picture: a sprite nobody scrolled to is never drawn.</summary>
      public bool IsRendered => render == null;

      /// <summary>Swap in a freshly rendered picture (after the sprite's graphics were edited).</summary>
      public void Replace(IPixelViewModel newImage, bool isPlaceholder) {
         render = null;
         image = newImage;
         this.isPlaceholder = isPlaceholder;
         NotifyPropertyChanged(nameof(PixelData));
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         NotifyPropertyChanged(nameof(IsPlaceholder));
      }

      private void EnsureImage() {
         var factory = render;
         if (factory == null) return;
         render = null;
         (image, isPlaceholder) = factory();
      }

      public short Transparent { get { EnsureImage(); return image.Transparent; } }
      public int PixelWidth { get { EnsureImage(); return image.PixelWidth; } }
      public int PixelHeight { get { EnsureImage(); return image.PixelHeight; } }
      public short[] PixelData { get { EnsureImage(); return image.PixelData; } }

      private double spriteScale = 2;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

      private bool isFilteredOut;
      public bool IsFilteredOut { get => isFilteredOut; set => TryUpdate(ref isFilteredOut, value); }

      private bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

      public SpriteGalleryItem(int index, int address, string name, IPixelViewModel image, bool isPlaceholder) {
         Index = index;
         Address = address;
         this.name = name ?? string.Empty;
         this.image = image;
         this.isPlaceholder = isPlaceholder;
      }

      /// <summary>The picture is drawn by <paramref name="render"/> the first time it's needed.</summary>
      public SpriteGalleryItem(int index, int address, string name, Func<(IPixelViewModel image, bool isPlaceholder)> render) {
         Index = index;
         Address = address;
         this.name = name ?? string.Empty;
         this.render = render;
      }

      public void MatchToFilter(string filter) {
         if (string.IsNullOrWhiteSpace(filter)) { IsFilteredOut = false; return; }
         var terms = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
         IsFilteredOut = !terms.All(term => Index.ToString() == term || Name.Contains(term, StringComparison.OrdinalIgnoreCase));
      }
   }
}
