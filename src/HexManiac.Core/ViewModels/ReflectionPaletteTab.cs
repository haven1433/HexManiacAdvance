using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>Draws sprites the way the game draws their reflections: upside down, in the colours of the reflection palette.</summary>
   public static class ReflectionPreview {
      /// <summary>The frames of the overworld sheet that are shown for the player: standing facing down, then the two walking frames facing down.</summary>
      public static readonly IReadOnlyList<int> PlayerFrames = new[] { 0, 3, 4 };

      /// <summary>The pictures (palette indices, [x, y]) of the frames of a sprite of the overworld sprite table. Frames that can't be read are left out; a sprite that can't be read at all gives an empty list.</summary>
      public static IReadOnlyList<int[,]> ReadFrames(IDataModel model, int spriteIndex, IReadOnlyList<int> frames) {
         var result = new List<int[,]>();
         try {
            var list = CharacterSprites.FindOverworldSpriteList(model, spriteIndex);
            if (list == null) return result;
            foreach (var frame in frames) {
               var pixels = CharacterSprites.ReadOverworldFrame(model, list, frame);
               if (pixels == null) { if (result.Count == 0) continue; break; }
               result.Add(pixels);
            }
         } catch (Exception) {
            // a sprite that can't be read has no preview
         }
         return result;
      }

      /// <summary>Turns a picture upside down (the first row becomes the last).</summary>
      public static int[,] FlipVertical(int[,] pixels) {
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         var result = new int[width, height];
         for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) result[x, y] = pixels[x, height - 1 - y];
         return result;
      }

      /// <summary>The picture of a frame in a palette (null if either is missing).</summary>
      public static IPixelViewModel DrawNormal(int[,] pixels, IReadOnlyList<short> palette) => pixels == null || palette == null ? null : CharacterSprites.Draw(pixels, palette);

      /// <summary>The picture of a frame upside down in a palette: what the game draws under the sprite.</summary>
      public static IPixelViewModel DrawReflection(int[,] pixels, IReadOnlyList<short> palette) => pixels == null || palette == null ? null : CharacterSprites.Draw(FlipVertical(pixels), palette);

      /// <summary>The colours the game makes from a sprite's palette (as pictures draw them). For a stored palette this returns null: nothing is made.</summary>
      public static IReadOnlyList<short> Filter(ReflectionPaletteKind kind, IReadOnlyList<short> normal) {
         if (kind == ReflectionPaletteKind.Bridge) return ReflectionFilters.ToDisplay(ReflectionFilters.Bridge());
         if (normal == null) return null;
         var raw = normal.Select(GbaColor.FromDisplay).ToList();
         if (kind == ReflectionPaletteKind.Water) return ReflectionFilters.ToDisplay(ReflectionFilters.Water(raw));
         if (kind == ReflectionPaletteKind.Ice) return ReflectionFilters.ToDisplay(ReflectionFilters.Ice(raw));
         return null;
      }
   }

   /// <summary>One frame of a sprite: the normal picture, its reflection, and (for a palette the game doesn't read) how the game really draws the reflection.</summary>
   public class ReflectionFrameItem : ViewModelCore {
      public string Label { get; }
      public int[,] Pixels { get; }
      public PixelPreview Normal { get; } = new PixelPreview();
      public PixelPreview Reflection { get; } = new PixelPreview();
      public PixelPreview InGame { get; } = new PixelPreview();

      public ReflectionFrameItem(string label, int[,] pixels) => (Label, Pixels) = (label, pixels);

      public void SetScale(double scale) {
         foreach (var preview in new[] { Normal, Reflection, InGame }) preview.SpriteScale = scale;
      }

      public void Redraw(IReadOnlyList<short> normal, IReadOnlyList<short> reflection, IReadOnlyList<short> inGame) {
         Normal.Replace(ReflectionPreview.DrawNormal(Pixels, normal));
         Reflection.Replace(ReflectionPreview.DrawReflection(Pixels, reflection));
         InGame.Replace(ReflectionPreview.DrawReflection(Pixels, inGame));
      }
   }

   /// <summary>A sprite in the preview: its name, why it is there, and its frames.</summary>
   public class ReflectionSpriteItem : ViewModelCore {
      public ReflectionSprite Sprite { get; }
      public string Name { get; }
      public string Note { get; }
      public bool HasNote => Note.Length > 0;
      public IReadOnlyList<ReflectionFrameItem> Frames { get; }
      public ICommand OpenPalette { get; }

      /// <summary>The sprite's own palette tag.</summary>
      public int Tag => Sprite.Tag;

      public ReflectionSpriteItem(ReflectionSprite sprite, string note, IReadOnlyList<ReflectionFrameItem> frames, Action<ReflectionSpriteItem> openPalette) {
         Sprite = sprite;
         Name = ObjectPaletteTags.Humanize(sprite.Name);
         Note = note ?? string.Empty;
         Frames = frames;
         OpenPalette = new StubCommand { CanExecute = arg => true, Execute = arg => openPalette(this) };
      }

      public void SetScale(double scale) {
         foreach (var frame in Frames) frame.SetScale(scale);
      }
   }

   /// <summary>One line of the list of reflection palettes.</summary>
   public class ReflectionPaletteRowItem : ViewModelCore {
      private readonly ReflectionPaletteTab tab;

      public ReflectionPalette Palette { get; }
      public string Key => Palette.Key;
      public string Name { get; }

      private string status = string.Empty;
      public string Status { get => status; set => Set(ref status, value); }

      private IReadOnlyList<short> colors = Array.Empty<short>();
      /// <summary>The 16 colours (as pictures draw them) for the strip next to the name. Empty for a palette the game works out.</summary>
      public IReadOnlyList<short> Colors { get => colors; set { colors = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(HasColors)); } }
      public bool HasColors => colors.Count > 0;

      private bool selected;
      public bool Selected { get => selected; set => Set(ref selected, value); }

      public ICommand Select { get; }

      public ReflectionPaletteRowItem(ReflectionPaletteTab tab, ReflectionPalette palette, string name) {
         this.tab = tab;
         Palette = palette;
         Name = name;
         Select = new StubCommand { CanExecute = arg => true, Execute = arg => tab.SelectedKey = Key };
      }
   }

   /// <summary>
   /// The Reflection Palettes editor (the second section of the secondary overworld panel in the overworld sprites view, see OverworldSecondaryElementViewModel): lists every palette the game uses for reflections (in water, on ice and under a high bridge), lets the person recolour it with the normal palette editor,
   /// and shows the sprites it affects next to their reflections. A game that works its reflections out while it runs (pokeemerald-expansion) is described instead: its three colour rules are shown read-only
   /// on the sprites, and the palettes it still carries but does not read are listed as such.
   /// </summary>
   public class ReflectionPaletteTab : ViewModelCore, ITabContent, IRaiseMessageTab {
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly StubCommand close = new();
      private bool refreshing;
      private ReflectionCatalog catalog;

      #region ITabContent

      public string Name => "Reflection Palettes";
      public bool SpartanMode { get; set; }
      public IDataModel Model => model;
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

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange { add { } remove { } } // this editor is part of the main tab, it never opens another one
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
      void ITabContent.Refresh() => Reload();
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      public void RaiseMessage(string message) => OnMessage?.Invoke(this, message);

      #endregion

      public static bool IsSupported(IDataModel model) => ReflectionCatalog.IsSupported(model);

      public const string UnsupportedMessage = "This ROM has no overworld sprite table and overworld palette table that the Reflection Palettes editor can read, so there is nothing to show here.";

      public ReflectionCatalog Catalog => catalog;

      public ObservableCollection<ReflectionPaletteRowItem> Entries { get; } = new();

      /// <summary>The sprites of the selected palette with their reflections.</summary>
      public ObservableCollection<ReflectionSpriteItem> Sprites { get; } = new();

      /// <summary>The colours of the selected palette, edited with the normal palette editor. Only a stored palette can be edited.</summary>
      public PaletteCollection Palette { get; }

      public bool ComputesReflections => catalog?.Mode == ReflectionMode.Computed;

      /// <summary>The sentence at the top of the tab.</summary>
      public string Introduction => ComputesReflections
         ? "This game works the colours of a reflection out while it runs, from the colours of the sprite itself. The first three entries are the rules it uses (nothing in the ROM to edit: change the sprite's own palette to change its reflection). "
            + "The palettes below them are still in the ROM, but this game never reads them."
         : "Reflections (the upside-down copy of a sprite in water, on ice and under a bridge) are drawn with palettes that are stored in the ROM. Pick one to recolour it and see the sprites it is used for, each next to its reflection.";

      private string selectedKey;
      /// <summary>The <see cref="ReflectionPalette.Key"/> of the selected entry.</summary>
      public string SelectedKey { get => selectedKey; set => Select(value); }

      public ReflectionPaletteRowItem SelectedEntry => Entries.FirstOrDefault(entry => entry.Key == selectedKey);
      public bool HasSelection => SelectedEntry != null;
      public bool IsEditable => SelectedEntry?.Palette.IsStored == true;

      private string title = string.Empty;
      public string Title { get => title; private set => Set(ref title, value); }

      private string description = string.Empty;
      /// <summary>What the selected entry is and how the game uses it.</summary>
      public string Description { get => description; private set => Set(ref description, value); }

      private string where = string.Empty;
      /// <summary>Where the selected palette is stored (empty for a rule the game applies by itself).</summary>
      public string Where { get => where; private set { if (TryUpdate(ref where, value)) NotifyPropertyChanged(nameof(HasWhere)); } }
      public bool HasWhere => where.Length > 0;

      private string colorSummary = string.Empty;
      public string ColorSummary { get => colorSummary; private set => Set(ref colorSummary, value); }

      private string playerNote = string.Empty;
      /// <summary>Said when the selected palette is used by the player, whose colours Character Customization changes while the game runs.</summary>
      public string PlayerNote { get => playerNote; private set { if (TryUpdate(ref playerNote, value)) NotifyPropertyChanged(nameof(HasPlayerNote)); } }
      public bool HasPlayerNote => playerNote.Length > 0;

      private string spriteSummary = string.Empty;
      public string SpriteSummary { get => spriteSummary; private set => Set(ref spriteSummary, value); }

      private bool showAllSprites;
      /// <summary>Show every sprite that uses the palette, not just the first few.</summary>
      public bool ShowAllSprites {
         get => showAllSprites;
         set { if (TryUpdate(ref showAllSprites, value)) BuildSprites(); }
      }

      /// <summary>True if there are more sprites than are shown.</summary>
      public bool CanShowMore => totalSprites > MaxShown;
      private int totalSprites;
      public const int MaxShown = 24;

      private double zoom = 2;
      public double Zoom {
         get => zoom;
         set {
            value = Math.Max(1, Math.Min(5, Math.Round(value)));
            if (!TryUpdate(ref zoom, value)) return;
            foreach (var sprite in Sprites) sprite.SetScale(zoom);
         }
      }

      /// <summary>True when the preview shows a third picture: how the game really draws a reflection that this ROM never reads a stored palette for.</summary>
      public bool ShowsInGame => ComputesReflections && SelectedEntry?.Palette.IsStored == true;

      public ReflectionPaletteTab(ViewPort viewPort) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         Save = viewPort.Save;
         SaveAs = viewPort.SaveAs;
         Undo = ReloadAfter(viewPort.Undo);
         Redo = ReloadAfter(viewPort.Redo);
         close.CanExecute = arg => true;
         close.Execute = arg => Closed?.Invoke(this, EventArgs.Empty);
         Palette = new PaletteCollection(this, null, viewPort.ChangeHistory) { PreferredColumns = 8 };
         Palette.SetContents(new short[16]);
         Palette.ColorsChanged += (sender, e) => PaletteEdited();
         Palette.Elements.CollectionChanged += (sender, e) => PaletteEdited();
         Reload();
      }

      private ICommand ReloadAfter(ICommand command) {
         var wrapper = new StubCommand {
            CanExecute = arg => command.CanExecute(arg),
            Execute = arg => { command.Execute(arg); Reload(); },
         };
         command.CanExecuteChanged += (sender, e) => wrapper.RaiseCanExecuteChanged();
         return wrapper;
      }

      private StubCommand refresh, openPalettes;
      public ICommand Refresh => StubCommand(ref refresh, Reload, () => true);
      public ICommand OpenPaletteTable => StubCommand(ref openPalettes, () => ShowInMainTab(model.GetTable(HardcodeTablesModel.OverworldPalettes)?.Start ?? -1), () => model.GetTable(HardcodeTablesModel.OverworldPalettes) != null);

      private long readAtEdit = -1;

      /// <summary>Reads everything again, but only if something in a ROM changed since this editor last looked (the table tool asks whenever its cursor moves or anything is edited).</summary>
      public void ReloadIfEdited() {
         if (readAtEdit == ModelEditStamp.Current) return;
         Reload();
      }

      #region Reading

      /// <summary>Reads everything from the ROM again and keeps the selection if the entry is still there.</summary>
      public void Reload() {
         readAtEdit = ModelEditStamp.Current;
         refreshing = true;
         try {
            catalog = ReflectionCatalog.Build(model);
            var keep = selectedKey;
            Entries.Clear();
            // a game that computes its reflections: the rules first, then the palettes it doesn't read
            // (the palettes of the player first, they are what people look for)
            var stored = catalog.Stored.OrderBy(entry => catalog.SpritesFor(entry).Any(sprite => sprite.Slot == ReflectionCatalog.PlayerSlot) ? 0 : 1).ToList();
            foreach (var entry in catalog.Computed.Concat(stored)) Entries.Add(MakeRow(entry));
            selectedKey = Entries.Any(entry => entry.Key == keep) ? keep : Entries.FirstOrDefault()?.Key;
            foreach (var entry in Entries) entry.Selected = entry.Key == selectedKey;
         } finally {
            refreshing = false;
         }
         NotifyPropertyChanged(nameof(Introduction));
         NotifyPropertyChanged(nameof(ComputesReflections));
         ShowSelection();
      }

      private ReflectionPaletteRowItem MakeRow(ReflectionPalette entry) {
         var row = new ReflectionPaletteRowItem(this, entry, entry.Name);
         if (entry.IsStored) {
            var count = catalog.SpritesFor(entry).Count;
            row.Status = ComputesReflections ? "stored, but this game doesn't read it" : $"used by {count} {(count == 1 ? "sprite" : "sprites")}";
            row.Colors = catalog.ColorsOf(entry) ?? Array.Empty<short>();
         } else {
            row.Status = "worked out by the game";
         }
         return row;
      }

      #endregion

      #region Selecting

      public void Select(string key) {
         if (refreshing || key == selectedKey || !Entries.Any(entry => entry.Key == key)) return;
         CompleteEdit(); // colours changed in the palette that was selected are their own undo step
         selectedKey = key;
         foreach (var entry in Entries) entry.Selected = entry.Key == key;
         ShowSelection();
      }

      private void ShowSelection() {
         var row = SelectedEntry;
         var wasRefreshing = refreshing;
         refreshing = true; // putting the colours of the palette into the palette editor is not an edit of the palette
         try {
            var colors = row?.Palette.IsStored == true ? catalog.ColorsOf(row.Palette) : null;
            Palette.SetContents(colors ?? new short[16]);
         } finally {
            refreshing = wasRefreshing;
         }
         Describe(row);
         BuildSprites();
         NotifyPropertyChanged(nameof(SelectedKey));
         NotifyPropertyChanged(nameof(SelectedEntry));
         NotifyPropertyChanged(nameof(HasSelection));
         NotifyPropertyChanged(nameof(IsEditable));
         NotifyPropertyChanged(nameof(ShowsInGame));
      }

      private void Describe(ReflectionPaletteRowItem row) {
         if (row == null) {
            Title = "Nothing to show";
            Description = catalog != null && catalog.PaletteRowCount == 0 ? "The overworld palette table could not be read." : "This ROM has no reflection palettes.";
            Where = string.Empty;
            ColorSummary = string.Empty;
            return;
         }
         var entry = row.Palette;
         Title = entry.Name;
         ColorSummary = string.Empty;
         switch (entry.Kind) {
            case ReflectionPaletteKind.Water:
               Description = "Reflections in water and puddles. The game copies the sprite's palette and adds 10 to the blue of every colour (to at most 31); the see-through colour 0 stays. "
                  + "Nothing is stored for this, so there is nothing to edit here: recolour the sprite's own palette (the button under each sprite) and its reflection follows.";
               Where = string.Empty;
               break;
            case ReflectionPaletteKind.Ice:
               Description = "Reflections on ice. The game copies the sprite's palette, takes 5 off the red of every colour and adds 3 to the green and 16 to the blue (all limited to 0 to 31); colour 0 stays. "
                  + "Nothing is stored for this: recolour the sprite's own palette to change its reflection.";
               Where = string.Empty;
               break;
            case ReflectionPaletteKind.Bridge:
               Description = "Reflections under a high bridge (Route 120). The game fills all 16 colours with one flat dark blue, whatever the sprite looks like, so it blends into the water underneath. Nothing is stored for this.";
               Where = string.Empty;
               break;
            default:
               Where = $"Tag {entry.Tag:X4}, row {entry.RowIndex} of the overworld palette table, colours at {entry.Address:X6}";
               if (ComputesReflections) {
                  Description = "This palette is part of the overworld palette table, but this game never loads it: it makes every reflection from the sprite's own colours (see the first three entries). "
                     + "You can still edit it (the third picture shows what it would look like), but the game will not show the change. To change a reflection in the game, edit the sprite's own palette.";
               } else {
                  var bridge = catalog.SpritesFor(entry).Any(sprite => sprite.BridgeTag == entry.Tag);
                  Description = "The game loads this palette for the reflection of the sprites below (in water, on ice and in puddles)"
                     + (bridge ? ", and it is also what these sprites use on a bridge" : string.Empty)
                     + ". Click a colour to change it; colours are saved the way the game stores them, 0 to 31 per channel. Colour 0 is the see-through colour.";
               }
               var summaryColors = catalog.ColorsOf(entry);
               ColorSummary = summaryColors == null ? string.Empty : "Colours: " + string.Join("  ", summaryColors.Select((c, i) => $"{i}={GbaColor.Describe(GbaColor.FromDisplay(c))}"));
               break;
         }
      }

      #endregion

      #region Previews

      private IReadOnlyList<ReflectionSprite> SpritesOfSelection(ReflectionPaletteRowItem row) {
         if (row == null) return Array.Empty<ReflectionSprite>();
         var list = row.Palette.IsStored ? catalog.SpritesFor(row.Palette) : catalog.Examples(showAllSprites ? 400 : MaxShown + 1);
         return list.OrderBy(sprite => sprite.Slot == ReflectionCatalog.PlayerSlot ? 0 : 1).ThenBy(sprite => sprite.Index).ToList();
      }

      private void BuildSprites() {
         Sprites.Clear();
         var row = SelectedEntry;
         var all = SpritesOfSelection(row);
         totalSprites = all.Count;
         NotifyPropertyChanged(nameof(CanShowMore));
         var shown = showAllSprites ? all : all.Take(MaxShown).ToList();
         PlayerNote = string.Empty;
         foreach (var sprite in shown) {
            var frames = new List<ReflectionFrameItem>();
            var wanted = sprite.Slot == ReflectionCatalog.PlayerSlot ? ReflectionPreview.PlayerFrames : new[] { 0 };
            var labels = new[] { "standing", "walking", "walking" };
            var pixels = ReflectionPreview.ReadFrames(model, sprite.Index, wanted);
            for (int i = 0; i < pixels.Count; i++) frames.Add(new ReflectionFrameItem(wanted.Count > 1 && i < labels.Length ? labels[i] : string.Empty, pixels[i]));
            string note = string.Empty;
            if (row != null && row.Palette.IsStored && !ComputesReflections && sprite.BridgeTag == row.Palette.Tag && sprite.ReflectionTag != row.Palette.Tag) note = "on a bridge";
            var item = new ReflectionSpriteItem(sprite, note, frames, OpenSpritePalette);
            item.SetScale(zoom);
            Sprites.Add(item);
         }
         if (shown.Any(sprite => sprite.Slot == ReflectionCatalog.PlayerSlot) && CharacterCustomization.IsSupported(model)) {
            PlayerNote = ComputesReflections
               ? "The player's colours are changed by Character Customization while the game runs. The game makes the reflection from those colours, so in game the player's reflection follows the chosen skin and clothes. This preview shows the unedited sprite palette."
               : "Character Customization changes the player's own colours while the game runs. This preview shows the unedited palettes.";
         }
         RedrawSprites();
         SpriteSummary = row == null ? string.Empty
            : totalSprites == 0 ? "No sprite uses this palette."
            : showAllSprites || totalSprites <= MaxShown ? $"{totalSprites} {(totalSprites == 1 ? "sprite" : "sprites")}"
            : $"the first {MaxShown} of {(row.Palette.IsStored ? totalSprites.ToString() : "many")} sprites";
      }

      private IReadOnlyList<short> CurrentStoredColors() => Palette.Elements.Select(element => element.Color).ToList();

      /// <summary>Draws every picture again with the colours as they are now (the palette editor may have changed them).</summary>
      public void RedrawSprites() {
         var row = SelectedEntry;
         if (row == null) return;
         var stored = row.Palette.IsStored ? CurrentStoredColors() : null;
         foreach (var item in Sprites) {
            var normal = catalog.ColorsOfTag(item.Tag);
            IReadOnlyList<short> reflection, inGame = null;
            if (stored != null) {
               reflection = stored;
               if (ComputesReflections) inGame = ReflectionPreview.Filter(ReflectionPaletteKind.Water, normal);
            } else {
               reflection = ReflectionPreview.Filter(row.Palette.Kind, normal);
            }
            foreach (var frame in item.Frames) frame.Redraw(normal, reflection, inGame);
         }
      }

      private void OpenSpritePalette(ReflectionSpriteItem item) {
         var pointer = catalog.PointerOfTag(item.Tag);
         if (pointer < 0) { OnError?.Invoke(this, $"The palette of {item.Name} (tag {item.Tag:X4}) was not found in the overworld palette table."); return; }
         CompleteEdit();
         ShowInMainTab(pointer);
      }

      #endregion

      #region Editing

      internal void CompleteEdit() => viewPort.ChangeHistory.ChangeCompleted();

      /// <summary>The palette editor changed a colour (or moved one): write what it shows into the ROM.</summary>
      private void PaletteEdited() {
         if (refreshing) return;
         var row = SelectedEntry;
         if (row == null || !row.Palette.IsStored || Palette.Elements.Count != 16) return;
         var colors = CurrentStoredColors();
         var existing = catalog.ColorsOf(row.Palette);
         if (existing != null && existing.SequenceEqual(colors)) return;
         if (!WriteColors(row.Palette, colors)) return;
         readAtEdit = ModelEditStamp.Current; // this editor made the change itself: no need to read it back
         row.Colors = colors;
         var summary = colors.Select((c, i) => $"{i}={GbaColor.Describe(GbaColor.FromDisplay(c))}");
         ColorSummary = "Colours: " + string.Join("  ", summary);
         RedrawSprites();
      }

      /// <summary>Stores 16 colours (as pictures draw them) in the palette of an entry. Returns false after telling the person why if it can't be done.</summary>
      internal bool WriteColors(ReflectionPalette entry, IReadOnlyList<short> colors) {
         try {
            var address = catalog.AddressOfTag(entry.Tag);
            if (address < 0 || address + 32 > model.Count) { OnError?.Invoke(this, "The palette is not inside the ROM."); return false; }
            var token = viewPort.CurrentChange;
            if (model.GetNextRun(address) is IPaletteRun run && run.Start == address && run.PaletteFormat.Bits == 4) {
               run.SetPalette(model, token, 0, colors);
            } else {
               for (int i = 0; i < 16; i++) {
                  var color = GbaColor.FromDisplay(colors[i]);
                  var top = model[address + i * 2 + 1] & 0x80; // the unused top bit of each colour stays as it was
                  token.ChangeData(model, address + i * 2, (byte)(color & 0xFF));
                  token.ChangeData(model, address + i * 2 + 1, (byte)(((color >> 8) & 0x7F) | top));
               }
            }
            return true;
         } catch (Exception e) {
            OnError?.Invoke(this, "Could not change the palette: " + e.Message);
            return false;
         }
      }

      #endregion

      #region Opening the other editors

      private void ShowInMainTab(int address) {
         if (address < 0) return;
         viewPort.Goto.Execute(address); // this editor lives inside the main tab, which is where the table is shown
      }

      #endregion
   }
}
