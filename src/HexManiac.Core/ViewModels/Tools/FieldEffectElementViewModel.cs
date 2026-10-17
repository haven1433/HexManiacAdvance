using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// Shown in the table tool under a field effect's fields (grass rustle, shadows, ripples, dust, sparkles...): the effect's name, every frame of its picture
   /// drawn with the palette the game uses for it, which palette that is (with a button that jumps to it, to edit its colours), and a gallery
   /// of all the field effects to jump to another one with a click.
   /// It is also the Field Effects half of the secondary overworld panel next to the overworld sprites (<see cref="OverworldSecondaryElementViewModel"/>): there it is
   /// <see cref="IsEmbedded"/>, picks effects without leaving the sprite table and lets the tags of the effect be typed in.
   /// </summary>
   public class FieldEffectElementViewModel : ViewModelCore, IArrayElementViewModel {
      /// <summary>The room a picture gets in the gallery, in screen pixels (the pictures are drawn as big as fit, 1x to 3x).</summary>
      public const double CellWidth = 64, CellHeight = 40;

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

      public int CurrentIndex { get; private set; }

      private string title = string.Empty;
      /// <summary>"4: TALL_GRASS"</summary>
      public string Title { get => title; private set => Set(ref title, value); }

      private string info = string.Empty;
      /// <summary>Size, number of frames and the tags, in a few lines.</summary>
      public string Info { get => info; private set => Set(ref info, value); }

      private string paletteTitle = string.Empty;
      /// <summary>Which palette the effect is drawn with and where it is stored.</summary>
      public string PaletteTitle { get => paletteTitle; private set => Set(ref paletteTitle, value); }

      private IPixelViewModel paletteImage;
      /// <summary>The palette's sixteen colours as a strip, or null if the effect has no palette to show.</summary>
      public IPixelViewModel PaletteImage { get => paletteImage; private set { paletteImage = value; NotifyPropertyChanged(); } }

      public bool HasPalette => palette != null;

      private FieldEffectPalette palette;

      /// <summary>Every frame of the picture, as the game draws it.</summary>
      public ObservableCollection<IPixelViewModel> Frames { get; } = new();

      public bool HasFrames => Frames.Count > 0;

      private readonly ObservableCollection<SpriteGalleryItem> elements = new();
      private bool loaded;
      /// <summary>Listed on first use: the table tool creates and discards these as the cursor moves.</summary>
      public ObservableCollection<SpriteGalleryItem> Elements { get { if (!loaded) Load(); return elements; } }

      private string filter = string.Empty;
      public string Filter {
         get => filter;
         set {
            if (!TryUpdate(ref filter, value ?? string.Empty)) return;
            foreach (var item in elements) item.MatchToFilter(filter);
         }
      }

      private string status = string.Empty;
      public string Status { get => status; private set => Set(ref status, value); }

      /// <summary>True when this is shown inside the overworld sprites view: picking an effect there stays in that view, and the tags can be edited.</summary>
      public bool IsEmbedded { get; }

      /// <summary>The tile tag and the palette tag of the effect, to type in (only for an embedded view: the table tool has the real fields).</summary>
      public ObservableCollection<FieldEffectTagItem> Fields { get; } = new();

      public ICommand OpenPalette { get; }
      public ICommand Refresh { get; }
      /// <summary>Shows the effect in the table tool, with all of its fields.</summary>
      public ICommand OpenTable { get; }
      /// <summary>Shows the Reflection Palettes section of the overworld sprites view (the table tool of the field effects links to it).</summary>
      public ICommand OpenReflectionPalettes { get; }
      /// <summary>Parameter: the SpriteGalleryItem that was clicked.</summary>
      public ICommand SelectCommand { get; }

      public FieldEffectElementViewModel(ViewPort viewPort, int index, bool embedded = false) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         IsEmbedded = embedded;
         OpenPalette = new StubCommand { CanExecute = arg => palette != null, Execute = arg => { if (palette != null) viewPort.Goto.Execute(palette.GotoDestination); } };
         OpenTable = new StubCommand { CanExecute = arg => FieldEffects.IsSupported(model), Execute = arg => { if (FieldEffects.FindTableName(model) is string table) viewPort.Goto.Execute($"{table}/{CurrentIndex}"); } };
         OpenReflectionPalettes = new StubCommand { CanExecute = arg => !IsEmbedded && ReflectionPaletteTab.IsSupported(model), Execute = arg => viewPort.OpenOverworldSpritesEditor(OverworldSecondarySection.Reflections) };
         Refresh = new StubCommand { CanExecute = arg => true, Execute = arg => { model.ClearCacheScope(); Load(); ShowEffect(CurrentIndex); } };
         SelectCommand = new StubCommand { CanExecute = arg => true, Execute = arg => Select(arg as SpriteGalleryItem) };
         ShowEffect(index);
      }

      /// <summary>Fills in the name, the info, the palette and the frames of the effect.</summary>
      private void ShowEffect(int index) {
         CurrentIndex = index;
         NotifyPropertyChanged(nameof(CurrentIndex));
         var effect = FieldEffects.Describe(model, index);
         Title = effect == null || string.IsNullOrEmpty(effect.Name) ? $"Field effect {index}" : $"{index}: {effect.Name}";
         Info = DescribeInfo(effect);
         palette = effect?.Palette;
         PaletteTitle = DescribePalette(effect);
         PaletteImage = palette == null ? null : FieldEffects.CreatePaletteStrip(palette.Colors);
         NotifyPropertyChanged(nameof(HasPalette));
         Frames.Clear();
         if (effect != null) {
            var scale = effect.Width > 0 ? Math.Max(1, Math.Min(3, (int)(CellWidth * 3 / effect.Width))) : 2;
            foreach (var frame in FieldEffects.RenderFrames(model, effect, scale)) Frames.Add(frame);
         }
         NotifyPropertyChanged(nameof(HasFrames));
         (OpenPalette as StubCommand)?.RaiseCanExecuteChanged();
         if (IsEmbedded && effect != null && effect.Template >= 0) {
            if (Fields.Count != 2) {
               Fields.Clear();
               Fields.Add(new FieldEffectTagItem(this, "Tile tag", 0, -1));
               Fields.Add(new FieldEffectTagItem(this, "Palette tag", 0, -1));
            }
            Fields[0].Reset(effect.Template, effect.TileTag); // the same two boxes keep showing: one of them may be being typed in right now
            Fields[1].Reset(effect.Template + 2, effect.PaletteTag);
         } else {
            Fields.Clear();
         }
      }

      /// <summary>Writes a tag the person typed into the effect's template and draws the effect again. Returns false after saying why if it can't be done.</summary>
      internal bool WriteTag(int address, int value) {
         if (address < 0 || address + 2 > model.Count) { Status = "That field is not inside the ROM."; return false; }
         model.WriteMultiByteValue(address, 2, viewPort.CurrentChange, value);
         viewPort.ChangeHistory.ChangeCompleted();
         Resync(CurrentIndex);
         return true;
      }

      private static string DescribeInfo(FieldEffectInfo effect) {
         if (effect == null) return string.Empty;
         if (!effect.HasPicture) return "This effect has no picture (an empty template).";
         var frames = effect.FrameCount == 1 ? "1 frame" : $"{effect.FrameCount} frames";
         return $"{effect.Width}x{effect.Height} pixels, {frames}\nTile tag {(effect.TileTag < 0 ? "?" : effect.TileTag.ToString("X4"))}, palette tag {effect.PaletteTagText}";
      }

      private static string DescribePalette(FieldEffectInfo effect) {
         if (effect == null || !effect.HasPicture) return string.Empty;
         var palette = effect.Palette;
         if (palette == null) return effect.PaletteTag == FieldEffects.NoTag ? "No palette of its own: the game draws it with the palette of the object it belongs to." : $"Palette {effect.PaletteTagText} is not in any palette table.";
         var place = $"{palette.TableName} #{palette.Index}";
         if (palette.IsPreviewOnly) return $"No palette of its own (the game uses the palette of the object it belongs to). Drawn here with the {palette.Title}, {place}.";
         return $"Palette {palette.Tag:X4}: the {palette.Title}, {place}.";
      }

      /// <summary>
      /// List every effect. Nothing is drawn here: each picture is drawn when the view first asks for it (see <see cref="FieldEffectRenderer"/>).
      /// </summary>
      private void Load() {
         loaded = true;
         elements.Clear();
         selectedItem = null;
         var tableName = FieldEffects.FindTableName(model);
         var count = FieldEffects.Count(model);
         if (tableName == null) { Status = string.Empty; return; }
         var pictures = renderer = FieldEffectRenderer.Get(model);
         var table = model.GetTable(tableName);
         for (int i = 0; i < count; i++) {
            var index = i;
            var (width, height) = FieldEffects.FrameSize(model, index);
            var scale = width > 0 && height > 0 ? Math.Max(1, Math.Min(3, (int)Math.Min(CellWidth / width, CellHeight / height))) : 2;
            var item = new SpriteGalleryItem(index, table.Start + table.ElementLength * index, FieldEffects.NameOf(model, index), () => pictures.Get(index)) { SpriteScale = scale, SelectCommand = SelectCommand };
            item.MatchToFilter(filter);
            elements.Add(item);
         }
         if (CurrentIndex >= 0 && CurrentIndex < elements.Count) { selectedItem = elements[CurrentIndex]; selectedItem.Selected = true; }
         Status = $"{count} field effects";
      }

      private SpriteGalleryItem selectedItem;
      private FieldEffectRenderer renderer;

      /// <summary>Jump the table tool to another effect.</summary>
      public void Select(SpriteGalleryItem item) {
         if (item == null) return;
         if (IsEmbedded) { Resync(item.Index); return; } // stay in this view
         var tableName = FieldEffects.FindTableName(model);
         if (tableName == null) return;
         viewPort.Goto.Execute($"{tableName}/{item.Index}");
      }

      /// <summary>Draws the effect again if the ROM has been edited since (and only then): the table tool refreshes its elements whenever anything changes.</summary>
      public void UpdateIfEdited() {
         if (shownAtEdit == ModelEditStamp.Current) return;
         Resync(CurrentIndex);
      }

      private long shownAtEdit = -1;

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not FieldEffectElementViewModel next) return false;
         if (next.model != model) return false;
         Resync(next.CurrentIndex);
         Visible = other.Visible;
         return true;
      }

      /// <summary>Shows this effect. It (and its palette, and its frames) may have been edited, so it is drawn again, but only this one.</summary>
      private void Resync(int index) {
         shownAtEdit = ModelEditStamp.Current;
         ShowEffect(index);
         if (loaded) {
            var current = CurrentIndex >= 0 && CurrentIndex < elements.Count ? elements[CurrentIndex] : null;
            if (!ReferenceEquals(selectedItem, current)) {
               if (selectedItem != null) selectedItem.Selected = false;
               selectedItem = current;
               if (current != null) current.Selected = true;
            }
            if (selectedItem != null && selectedItem.IsRendered) {
               var redrawn = renderer?.Redraw(CurrentIndex);
               if (redrawn != null) selectedItem.Replace(redrawn.Value.image, redrawn.Value.isPlaceholder);
            }
         }
      }
   }

   /// <summary>A two-byte tag of a field effect's template (a hex number), as a box to type in.</summary>
   public class FieldEffectTagItem : ViewModelCore {
      private readonly FieldEffectElementViewModel owner;
      private int address;
      public string Label { get; }

      private string text;
      public string Text {
         get => text;
         set {
            value = (value ?? string.Empty).Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value.Substring(2);
            if (!int.TryParse(value, System.Globalization.NumberStyles.AllowHexSpecifier, null, out var number) || number < 0 || number > 0xFFFF) {
               NotifyPropertyChanged(); // put back what the ROM says
               return;
            }
            var formatted = number.ToString("X4");
            if (text == formatted) return;
            text = formatted;
            NotifyPropertyChanged();
            owner.WriteTag(address, number);
         }
      }

      public FieldEffectTagItem(FieldEffectElementViewModel owner, string label, int address, int value) {
         this.owner = owner;
         Label = label;
         Reset(address, value);
      }

      /// <summary>Shows what the ROM says (a different effect, or a change from somewhere else) without writing anything.</summary>
      public void Reset(int newAddress, int value) {
         address = newAddress;
         var newText = value < 0 ? string.Empty : value.ToString("X4");
         if (text == newText) return;
         text = newText;
         NotifyPropertyChanged(nameof(Text));
      }
   }
}
