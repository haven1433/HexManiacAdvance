using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>One row of the skin tone list, the clothes colour list or the secondary colour list: its number, its name and its three colours.</summary>
   public class CharacterColorRowItem : ViewModelCore {
      private readonly CharacterColorList list;

      /// <summary>The row number in the table (row 0 is the original look).</summary>
      public int Index { get; }

      private string name = string.Empty;
      public string Name { get => name; private set => Set(ref name, value); }

      private short main, shadow, highlight;
      /// <summary>The three colours the way a picture draws them (see <see cref="GbaColor.ToDisplay"/>).</summary>
      public short Main { get => main; private set => Set(ref main, value); }
      public short Shadow { get => shadow; private set => Set(ref shadow, value); }
      public short Highlight { get => highlight; private set => Set(ref highlight, value); }

      public bool IsOriginal => Index == 0;
      public bool IsEditable => Index != 0;

      /// <summary>What is written in the row when its name can't be edited.</summary>
      public string Caption => IsOriginal ? $"{Name} (the sprite's own colours)" : Name;

      private bool selected;
      public bool Selected { get => selected; set => Set(ref selected, value); }

      public ICommand Select { get; }

      public CharacterColorRowItem(CharacterColorList list, CharacterColorRow row) {
         this.list = list;
         Index = row.Index;
         Select = new StubCommand { CanExecute = arg => true, Execute = arg => list.SelectedIndex = Index };
         Update(row);
      }

      public void Update(CharacterColorRow row) {
         Name = row.Name;
         Main = GbaColor.ToDisplay(row.Colors[0]);
         Shadow = GbaColor.ToDisplay(row.Colors[1]);
         Highlight = GbaColor.ToDisplay(row.Colors[2]);
         NotifyPropertyChanged(nameof(Caption));
      }

      /// <summary>Gives the row a new name. Returns false (after telling the person why) if the name can't be used: the view then shows the old name again.</summary>
      public bool Rename(string text) => list.Rename(this, text);
   }

   /// <summary>One tile of the "everything at a glance" grids: the boy and the girl wearing one skin tone, clothes colour or secondary colour.</summary>
   public class CharacterGridItem : ViewModelCore {
      public int Index { get; }

      private string name = string.Empty;
      public string Name { get => name; set => Set(ref name, value); }

      public PixelPreview Boy { get; } = new PixelPreview(null, 2);
      public PixelPreview Girl { get; } = new PixelPreview(null, 2);

      private bool selected;
      public bool Selected { get => selected; set => Set(ref selected, value); }

      public ICommand Select { get; }

      public CharacterGridItem(int index, Action<int> select) {
         Index = index;
         Select = new StubCommand { CanExecute = arg => true, Execute = arg => select(index) };
      }
   }

   /// <summary>The boy or the girl: standing in the overworld (facing down, up and left, no walking frames), the trainer front picture and the back picture, all wearing the chosen colours.</summary>
   public class CharacterPreviewPanel : ViewModelCore {
      private readonly CharacterCustomizationTab tab;
      private CharacterSprites sprites;

      public int Gender { get; }
      public string Title => Gender == CharacterRole.Male ? "Boy (Brendan)" : "Girl (May)";

      /// <summary>The overworld frames the preview shows, in order: standing facing down, facing up, facing left (<see cref="CharacterSprites.OverworldFrameNumbers"/>).</summary>
      public IReadOnlyList<CharacterFramePreview> OverworldFrames { get; } = CharacterSprites.OverworldFrameLabels.Select(label => new CharacterFramePreview(label)).ToArray();
      /// <summary>Standing still, facing down (the first of <see cref="OverworldFrames"/>).</summary>
      public PixelPreview Standing => OverworldFrames[0].Image;
      public PixelPreview Front { get; } = new PixelPreview();
      public PixelPreview Back { get; } = new PixelPreview();

      public bool HasOverworld => sprites?.HasOverworld ?? false;
      public bool HasFront => sprites?.HasFront ?? false;
      public bool HasBack => sprites?.HasBack ?? false;

      private string note = string.Empty;
      /// <summary>Empty, or what could not be shown (a picture the ROM doesn't have).</summary>
      public string Note { get => note; private set { if (TryUpdate(ref note, value)) NotifyPropertyChanged(nameof(HasNote)); } }
      public bool HasNote => note.Length > 0;

      public ICommand EditOverworld { get; }
      public ICommand EditFront { get; }
      public ICommand EditBack { get; }

      public string EditOverworldText => $"Edit {(Gender == CharacterRole.Male ? "boy's" : "girl's")} overworld sprites";
      public string EditFrontText => $"Edit {(Gender == CharacterRole.Male ? "boy's" : "girl's")} front picture";
      public string EditBackText => $"Edit {(Gender == CharacterRole.Male ? "boy's" : "girl's")} back picture";

      public CharacterPreviewPanel(CharacterCustomizationTab tab, int gender) {
         this.tab = tab;
         Gender = gender;
         EditOverworld = new StubCommand { CanExecute = arg => HasOverworld, Execute = arg => tab.EditOverworldSprites(this) };
         EditFront = new StubCommand { CanExecute = arg => HasFront, Execute = arg => tab.EditPicture(sprites.FrontAddress, $"{Title} front picture") };
         EditBack = new StubCommand { CanExecute = arg => HasBack, Execute = arg => tab.EditPicture(sprites.BackAddress, $"{Title} back picture") };
      }

      public CharacterSprites Sprites => sprites;

      /// <summary>Reads the graphics again (they can have been edited in the image editor).</summary>
      public void LoadSprites(IDataModel model) {
         sprites = CharacterSprites.Load(model, Gender);
         Note = string.Join(" ", sprites.Problems);
         NotifyPropertyChanged(nameof(HasOverworld));
         NotifyPropertyChanged(nameof(HasFront));
         NotifyPropertyChanged(nameof(HasBack));
         (EditOverworld as StubCommand)?.RaiseCanExecuteChanged();
         (EditFront as StubCommand)?.RaiseCanExecuteChanged();
         (EditBack as StubCommand)?.RaiseCanExecuteChanged();
      }

      public void SetScale(double scale) {
         foreach (var preview in OverworldFrames.Select(frame => frame.Image).Concat(new[] { Front, Back })) preview.SpriteScale = scale;
      }

      public void Redraw(CharacterRole overworld, CharacterRole trainer, CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary = null) {
         if (sprites == null) return;
         for (int i = 0; i < OverworldFrames.Count; i++) OverworldFrames[i].Image.Replace(sprites.DrawOverworld(i, overworld, skin, clothes, secondary));
         Front.Replace(sprites.DrawFront(trainer, skin, clothes, secondary));
         Back.Replace(sprites.DrawBack(trainer, skin, clothes, secondary));
      }

      /// <summary>The standing picture (facing down) for the grids, or null if the ROM doesn't have the sprite.</summary>
      public IPixelViewModel DrawStanding(CharacterRole overworld, CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary = null) => sprites?.DrawOverworld(0, overworld, skin, clothes, secondary);
   }

   /// <summary>
   /// One of the lists, skin tones, clothes colours or secondary colours: the rows, the colours of the selected row (edited with the same colour picker as any palette),
   /// and the buttons that add, copy, remove and move rows. Everything is written to the ROM the moment it changes, and can be undone.
   /// </summary>
   public class CharacterColorList : ViewModelCore {
      private readonly CharacterCustomizationTab tab;
      private bool refreshing;

      public CharacterColorTable Table { get; }
      public CharacterColorKind Kind => Table.Kind;
      public string Title => Kind switch { CharacterColorKind.SkinTone => "Skin tones", CharacterColorKind.Clothes => "Clothes colours", _ => "Secondary colours (the red parts)" };

      public ObservableCollection<CharacterColorRowItem> Rows { get; } = new();

      /// <summary>What is in the ROM, row by row (kept in step with <see cref="Rows"/>).</summary>
      public List<CharacterColorRow> Data { get; } = new();

      /// <summary>The colours of the selected row, edited with the palette editor (the skin, clothes or secondary colours are its three colours).</summary>
      public PaletteCollection Palette { get; }

      private int selectedIndex = -1;
      public int SelectedIndex { get => selectedIndex; set => Select(value); }

      public CharacterColorRow SelectedData => selectedIndex >= 0 && selectedIndex < Data.Count ? Data[selectedIndex] : null;

      public bool HasSelection => selectedIndex >= 0 && selectedIndex < Rows.Count;
      public bool IsOriginalSelected => selectedIndex == 0;
      public bool CanEditSelected => selectedIndex > 0 && selectedIndex < Rows.Count;

      public string SelectedTitle {
         get {
            if (!HasSelection) return string.Empty;
            return selectedIndex == 0
               ? $"Row 0 is the original look: the game doesn't recolour it, so there is nothing to edit. Pick another row to change its colours."
               : $"Row {selectedIndex}: {Data[selectedIndex].Name}";
         }
      }

      private string colorSummary = string.Empty;
      public string ColorSummary { get => colorSummary; private set => Set(ref colorSummary, value); }

      private string problem;
      public string Problem { get => problem; private set { if (TryUpdate(ref problem, value)) NotifyPropertyChanged(nameof(HasProblem)); } }
      public bool HasProblem => !string.IsNullOrEmpty(problem);

      public string CountText {
         get {
            var count = Rows.Count;
            return $"{count} {(count == 1 ? Table.Noun : Table.NounPlural)} (the game can use up to {CharacterColorTable.MaxRows})";
         }
      }

      public CharacterColorList(CharacterCustomizationTab tab, CharacterColorTable table, ChangeHistory<ModelDelta> history) {
         this.tab = tab;
         Table = table;
         Palette = new PaletteCollection(tab, null, history) { PreferredColumns = CharacterColorTable.ColorsPerRow };
         Palette.SetContents(new short[CharacterColorTable.ColorsPerRow]);
         Palette.ColorsChanged += (sender, e) => PaletteEdited();
         Palette.Elements.CollectionChanged += (sender, e) => PaletteEdited(); // dragging a colour to another place in the palette
      }

      #region Reading

      /// <summary>Reads the table again and updates the rows in place (so the list keeps its scroll position). Selects the given row, or keeps the selected one.</summary>
      public void Reload(int select = -1) {
         refreshing = true;
         try {
            var rows = Table.ReadRows();
            Problem = Table.Problem;
            Data.Clear();
            Data.AddRange(rows);
            for (int i = 0; i < rows.Count; i++) {
               if (i < Rows.Count) Rows[i].Update(rows[i]); else Rows.Add(new CharacterColorRowItem(this, rows[i]));
            }
            while (Rows.Count > rows.Count) Rows.RemoveAt(Rows.Count - 1);
            var index = select >= 0 ? select : selectedIndex;
            SetSelection(Rows.Count == 0 ? -1 : Math.Max(0, Math.Min(index, Rows.Count - 1)));
         } finally {
            refreshing = false;
         }
         NotifyPropertyChanged(nameof(CountText));
         NotifyPropertyChanged(nameof(HasSelection));
         RaiseCommandStates();
      }

      private void SetSelection(int index) {
         selectedIndex = index;
         for (int i = 0; i < Rows.Count; i++) Rows[i].Selected = i == index;
         var row = SelectedData;
         var wasRefreshing = refreshing;
         refreshing = true; // putting the colours of the row into the palette editor is not an edit of the row
         try {
            Palette.SetContents(row == null ? new short[CharacterColorTable.ColorsPerRow] : row.Colors.Select(GbaColor.ToDisplay).ToArray());
         } finally {
            refreshing = wasRefreshing;
         }
         UpdateSummary();
         NotifyPropertyChanged(nameof(SelectedIndex));
         NotifyPropertyChanged(nameof(SelectedData));
         NotifyPropertyChanged(nameof(HasSelection));
         NotifyPropertyChanged(nameof(IsOriginalSelected));
         NotifyPropertyChanged(nameof(CanEditSelected));
         NotifyPropertyChanged(nameof(SelectedTitle));
      }

      private void UpdateSummary() {
         var row = SelectedData;
         if (row == null || row.Index == 0) { ColorSummary = string.Empty; return; }
         string Part(string label, ushort color) => $"{label} {GbaColor.Describe(color)}";
         ColorSummary = $"{Part("Main", row.Colors[0])}   {Part("Shadow", row.Colors[1])}   {Part("Highlight", row.Colors[2])}   (red:green:blue, each 0 to 31)";
      }

      #endregion

      #region Selecting and editing colours

      public void Select(int index) {
         if (index == selectedIndex || index < 0 || index >= Rows.Count) return;
         tab.CompleteEdit(); // the colours changed in the row that was selected are their own undo step
         SetSelection(index);
         RaiseCommandStates();
         tab.SelectionChanged(this);
      }

      /// <summary>The palette editor changed one of the three colours (or moved one): write what it shows into the selected row.</summary>
      private void PaletteEdited() {
         if (refreshing || selectedIndex <= 0 || selectedIndex >= Data.Count || Palette.Elements.Count != CharacterColorTable.ColorsPerRow) return;
         var colors = Palette.Elements.Select(element => GbaColor.FromDisplay(element.Color)).ToArray();
         if (Data[selectedIndex].Colors.SequenceEqual(colors)) return;
         var result = Table.SetColors(selectedIndex, colors);
         if (!result.Success) { tab.ReportError(result.Message); return; }
         Data[selectedIndex] = Table.ReadRow(selectedIndex);
         Rows[selectedIndex].Update(Data[selectedIndex]);
         UpdateSummary();
         tab.ColorsEdited(this, selectedIndex);
      }

      /// <summary>Makes the shadow and the highlight from the main colour of the selected row.</summary>
      public void DeriveShades() {
         if (!CanEditSelected) return;
         tab.RunEdit(this, () => Table.DeriveShades(selectedIndex));
      }

      public bool Rename(CharacterColorRowItem item, string text) {
         if (item == null || item.Index <= 0 || item.Index >= Data.Count) return false;
         if ((text ?? string.Empty).Trim() == item.Name) return true;
         return tab.RunEdit(this, () => Table.SetName(item.Index, text), item.Index);
      }

      #endregion

      #region Commands

      private StubCommand add, duplicate, remove, moveUp, moveDown, derive, fix, open;

      public ICommand Add => StubCommand(ref add, AddRow, () => Table.Exists && !HasProblem && Rows.Count < CharacterColorTable.MaxRows);
      public ICommand Duplicate => StubCommand(ref duplicate, DuplicateRow, () => Table.Exists && !HasProblem && HasSelection && Rows.Count < CharacterColorTable.MaxRows);
      public ICommand Remove => StubCommand(ref remove, RemoveRow, () => !HasProblem && CanEditSelected);
      public ICommand MoveUp => StubCommand(ref moveUp, () => MoveRow(-1), () => !HasProblem && selectedIndex > 1);
      public ICommand MoveDown => StubCommand(ref moveDown, () => MoveRow(1), () => !HasProblem && selectedIndex > 0 && selectedIndex < Rows.Count - 1);
      public ICommand DeriveShadeCommand => StubCommand(ref derive, DeriveShades, () => CanEditSelected);
      public ICommand FixCount => StubCommand(ref fix, FixTable, () => Table.Run != null && HasProblem);
      public ICommand OpenTable => StubCommand(ref open, () => tab.OpenTable(Table), () => Table.Run != null);

      private void RaiseCommandStates() {
         add?.RaiseCanExecuteChanged();
         duplicate?.RaiseCanExecuteChanged();
         remove?.RaiseCanExecuteChanged();
         moveUp?.RaiseCanExecuteChanged();
         moveDown?.RaiseCanExecuteChanged();
         derive?.RaiseCanExecuteChanged();
         fix?.RaiseCanExecuteChanged();
         open?.RaiseCanExecuteChanged();
      }

      private void AddRow() {
         // a new row starts as a copy of the selected colours (or a neutral colour next to the original look) so there is something to see and edit
         var colors = CanEditSelected ? SelectedData.Colors : DefaultColors();
         tab.RunEdit(this, () => Table.Add(Table.UniqueName(Table.DefaultName), colors));
      }

      private IReadOnlyList<ushort> DefaultColors() {
         var main = Kind == CharacterColorKind.SkinTone ? GbaColor.Pack(25, 18, 13) : GbaColor.Pack(26, 6, 6);
         var (shadow, highlight) = Kind == CharacterColorKind.Secondary ? GbaColor.DeriveSecondaryShades(main) : GbaColor.DeriveShades(main);
         return new[] { main, shadow, highlight };
      }

      private void DuplicateRow() {
         if (!HasSelection) return;
         var index = selectedIndex;
         tab.RunEdit(this, () => Table.Duplicate(index));
      }

      private void RemoveRow() {
         if (!CanEditSelected) return;
         var index = selectedIndex;
         tab.RunEdit(this, () => Table.Remove(index));
      }

      private void MoveRow(int direction) {
         var index = selectedIndex;
         tab.RunEdit(this, () => Table.Move(index, direction));
      }

      private void FixTable() => tab.RunEdit(this, () => Table.FixCount());

      #endregion
   }

   /// <summary>
   /// The "Character Customization" editor. After choosing the boy or the girl the player picks a skin tone, a clothes colour and a secondary colour (the red parts); the game paints those colours over
   /// chosen slots of the character's palettes wherever the character is drawn. This tab edits the lists of colours (rename, recolour, add, copy, remove, move),
   /// shows the boy and the girl wearing the selected colours, and opens the character's pictures in the image editor.
   /// A ROM from before the secondary colour has only two lists: the secondary list, its grid and its buttons are hidden then.
   /// </summary>
   public class CharacterCustomizationTab : ViewModelCore, ITabContent, IRaiseMessageTab {
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly CharacterCustomization customization;
      private readonly StubCommand close = new();
      private IReadOnlyList<CharacterRole> roles = Array.Empty<CharacterRole>();

      #region ITabContent

      public string Name => "Character Customization";
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
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
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

      /// <summary>True if the ROM's metadata has the skin tone and clothes colour tables, their counters and the roles table (the secondary colour table is optional).</summary>
      public static bool IsSupported(IDataModel model) => CharacterCustomization.IsSupported(model);

      public const string UnsupportedMessage = "This ROM has no character customization tables (the graphics.characterCustomization.* anchors in its metadata), so there is nothing to edit here. The CUBE ROM has them.";

      public const string Intro = "Choose what the player can look like. After picking the boy or the girl in the new game intro, the player picks a skin tone, a clothes colour and a secondary colour (the red parts of the clothes) from these three lists, "
         + "and the game paints them over the character's sprites everywhere: walking around, in battles, on the trainer card. "
         + "Click a row to edit it, press Add for a new one, and watch the previews. Row 0 of each list is the original look and always stays.";

      /// <summary>What the top of the tab says for a ROM that has no secondary colour (it was made before that existed).</summary>
      public const string IntroWithoutSecondary = "Choose what the player can look like. After picking the boy or the girl in the new game intro, the player picks a skin tone and a clothes colour from these two lists, "
         + "and the game paints them over the character's sprites everywhere: walking around, in battles, on the trainer card. "
         + "Click a row to edit it, press Add for a new one, and watch the previews. Row 0 of each list is the original look and always stays.";

      /// <summary>The sentence at the top of the tab (a property, so the view can bind to it).</summary>
      public string Introduction => HasSecondary ? Intro : IntroWithoutSecondary;

      /// <summary>The sentence above the previews.</summary>
      public string PreviewHint => HasSecondary
         ? "The selected skin tone, clothes colour and secondary colour on the boy and the girl, painted the way the game paints them: in the overworld (facing down, up and left), on the trainer picture and from behind."
         : "The selected skin tone and clothes colour on the boy and the girl, painted the way the game paints them: in the overworld (facing down, up and left), on the trainer picture and from behind.";

      /// <summary>
      /// The tab shows the colour lists on the left and the previews on the right when it is at least this wide (in device-independent pixels):
      /// a list card is 440 wide plus margins, and the previews need about 400 to show the overworld frames and a trainer picture side by side.
      /// Narrower, the previews go below the lists and the whole tab scrolls as one.
      /// </summary>
      public const double TwoColumnMinWidth = 880;

      /// <summary>True if a tab this wide shows the lists and the previews side by side (each column scrolling on its own).</summary>
      public static bool UseTwoColumns(double width) => !double.IsNaN(width) && width >= TwoColumnMinWidth;

      /// <summary>The sentence above the 'everything at a glance' grids.</summary>
      public string GlanceHint => HasSecondary
         ? "Every skin tone, every clothes colour and every secondary colour, each one wearing the selected other two (the boy, then the girl). Click one to select it."
         : "Every skin tone with the selected clothes colour, and every clothes colour with the selected skin tone (the boy, then the girl). Click one to select it.";

      public CharacterColorList SkinTones { get; }
      public CharacterColorList Clothes { get; }
      /// <summary>The secondary colours (the red parts of the clothes). Only part of <see cref="Lists"/> if <see cref="HasSecondary"/>.</summary>
      public CharacterColorList Secondary { get; }
      /// <summary>The lists the view shows: skin tones, clothes colours and, if the ROM has them, secondary colours.</summary>
      public IReadOnlyList<CharacterColorList> Lists { get; }

      /// <summary>True if the ROM has the secondary colour table. If not, nothing about it is shown.</summary>
      public bool HasSecondary => customization.HasSecondary;

      public CharacterPreviewPanel Boy { get; }
      public CharacterPreviewPanel Girl { get; }
      public IReadOnlyList<CharacterPreviewPanel> Panels { get; }

      /// <summary>Every skin tone with the selected clothes colour (the boy and the girl standing).</summary>
      public ObservableCollection<CharacterGridItem> SkinGrid { get; } = new();

      /// <summary>Every clothes colour with the selected skin tone and secondary colour.</summary>
      public ObservableCollection<CharacterGridItem> ClothesGrid { get; } = new();

      /// <summary>Every secondary colour with the selected skin tone and clothes colour (empty if the ROM has no secondary colours).</summary>
      public ObservableCollection<CharacterGridItem> SecondaryGrid { get; } = new();

      private IReadOnlyList<string> roleLines = Array.Empty<string>();
      /// <summary>One line per kind of picture the game paints: which of the palette's 16 slots get the skin, clothes and secondary colours. Read from the ROM's roles table.</summary>
      public IReadOnlyList<string> RoleLines { get => roleLines; private set { roleLines = value; NotifyPropertyChanged(); } }

      private string pictureHint = string.Empty;
      /// <summary>A sentence for the image editor buttons: which palette slots to draw skin, clothes and the red parts with.</summary>
      public string PictureHint { get => pictureHint; private set => Set(ref pictureHint, value); }

      private string highlightNote = string.Empty;
      /// <summary>Says when no picture uses the highlight colour of the clothes or the secondary colour (the sprites only have two shades of each).</summary>
      public string HighlightNote { get => highlightNote; private set => Set(ref highlightNote, value); }

      private double zoom = 3;
      public double Zoom {
         get => zoom;
         set {
            value = Math.Max(1, Math.Min(6, Math.Round(value)));
            if (!TryUpdate(ref zoom, value)) return;
            foreach (var panel in Panels) panel.SetScale(zoom);
         }
      }

      private bool showAllPreviews = true;
      /// <summary>Show the grids with every skin tone and every clothes colour.</summary>
      public bool ShowAllPreviews {
         get => showAllPreviews;
         set { if (TryUpdate(ref showAllPreviews, value)) RedrawGrids(); }
      }

      public CharacterCustomizationTab(ViewPort viewPort) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         Save = viewPort.Save;
         SaveAs = viewPort.SaveAs;
         Undo = ReloadAfter(viewPort.Undo);
         Redo = ReloadAfter(viewPort.Redo);
         close.CanExecute = arg => true;
         close.Execute = arg => Closed?.Invoke(this, EventArgs.Empty);
         customization = new CharacterCustomization(model, () => viewPort.CurrentChange);
         SkinTones = new CharacterColorList(this, customization.SkinTones, viewPort.ChangeHistory);
         Clothes = new CharacterColorList(this, customization.Clothes, viewPort.ChangeHistory);
         Secondary = new CharacterColorList(this, customization.Secondary, viewPort.ChangeHistory);
         Lists = customization.HasSecondary ? new[] { SkinTones, Clothes, Secondary } : new[] { SkinTones, Clothes };
         Boy = new CharacterPreviewPanel(this, CharacterRole.Male);
         Girl = new CharacterPreviewPanel(this, CharacterRole.Female);
         Panels = new[] { Boy, Girl };
         foreach (var panel in Panels) panel.SetScale(zoom);
         Reload();
      }

      /// <summary>
      /// Undo and redo change the ROM behind this tab's back: everything is read again afterwards.
      /// </summary>
      private ICommand ReloadAfter(ICommand command) {
         var wrapper = new StubCommand {
            CanExecute = arg => command.CanExecute(arg),
            Execute = arg => { command.Execute(arg); Reload(); },
         };
         command.CanExecuteChanged += (sender, e) => wrapper.RaiseCanExecuteChanged();
         return wrapper;
      }

      private StubCommand refresh, openSkin, openClothes, openSecondary, openRoles;
      public ICommand Refresh => StubCommand(ref refresh, Reload, () => true);
      public ICommand OpenSkinTable => StubCommand(ref openSkin, () => OpenTable(SkinTones.Table), () => SkinTones.Table.Run != null);
      public ICommand OpenClothesTable => StubCommand(ref openClothes, () => OpenTable(Clothes.Table), () => Clothes.Table.Run != null);
      public ICommand OpenSecondaryTable => StubCommand(ref openSecondary, () => OpenTable(Secondary.Table), () => HasSecondary && Secondary.Table.Run != null);
      public ICommand OpenRolesTable => StubCommand(ref openRoles, OpenRoles, () => customization.RoleRun != null);

      #region Reading everything

      public void Reload() {
         roles = customization.ReadRoles();
         SkinTones.Reload();
         Clothes.Reload();
         if (HasSecondary) Secondary.Reload();
         foreach (var panel in Panels) panel.LoadSprites(model);
         DescribeRoles();
         RedrawPreviews();
      }

      private void DescribeRoles() {
         // group the roles that paint the same slots: "boy and girl, overworld sprites and trainer pictures: ..."
         var lines = new List<string>();
         foreach (var group in roles.GroupBy(role => role.Describe())) {
            var genders = group.Select(role => role.GenderName.ToLower()).Distinct().ToList();
            var contexts = group.Select(role => CharacterRole.ContextName(role.Context)).Distinct().ToList();
            lines.Add($"{Capitalize(JoinWords(contexts))} of the {JoinWords(genders)}: {group.Key}.");
         }
         RoleLines = lines;
         var overworld = roles.Where(role => role.Context == CharacterRole.OverworldContext).ToList();
         var sample = overworld.Count > 0 ? overworld[0] : roles.FirstOrDefault();
         PictureHint = sample == null
            ? "This ROM's roles table has no rows, so the game doesn't paint any slot."
            : $"The game paints these slots of the palette: {sample.DescribeSlots()} Draw {(sample.Secondary.Any(slot => slot >= 0) ? "skin, clothes and the red parts" : "skin and clothes")} with those slots; the other slots (hair, eyes, outlines...) keep their own colours.";
         var clothesHighlightUnused = roles.Count > 0 && roles.All(role => role.Clothes[2] < 0);
         var secondaryHighlightUnused = HasSecondary && roles.Count > 0 && roles.All(role => role.Secondary[2] < 0);
         HighlightNote = clothesHighlightUnused && secondaryHighlightUnused ? "The sprites only have two shades of clothes and of the red parts, so the highlight colours of the clothes and of the secondary colours are saved but not used yet."
            : clothesHighlightUnused ? "The sprites only have two shades of clothes, so the clothes highlight is saved but not used yet."
            : secondaryHighlightUnused ? "The sprites only have two shades of the red parts, so the secondary highlight is saved but not used yet."
            : string.Empty;
      }

      private static string JoinWords(IReadOnlyList<string> words) {
         if (words.Count <= 1) return string.Concat(words);
         return string.Join(", ", words.Take(words.Count - 1)) + " and " + words[words.Count - 1];
      }

      private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text.Substring(1);

      public CharacterRole FindRole(int gender, int context) => roles.FirstOrDefault(role => role.Gender == gender && role.Context == context);

      #endregion

      #region Previews

      /// <summary>The selected secondary colour, or null if the ROM has no secondary colours (the pictures then keep the sprites' own red).</summary>
      private CharacterColorRow SelectedSecondary => HasSecondary ? Secondary.SelectedData : null;

      public void RedrawPreviews() {
         var skin = SkinTones.SelectedData;
         var clothes = Clothes.SelectedData;
         var secondary = SelectedSecondary;
         foreach (var panel in Panels) panel.Redraw(FindRole(panel.Gender, CharacterRole.OverworldContext), FindRole(panel.Gender, CharacterRole.TrainerPicContext), skin, clothes, secondary);
         RedrawGrids();
      }

      private void RedrawGrids() {
         if (!showAllPreviews) { SkinGrid.Clear(); ClothesGrid.Clear(); SecondaryGrid.Clear(); return; }
         var skin = SkinTones.SelectedData;
         var clothes = Clothes.SelectedData;
         var secondary = SelectedSecondary;
         SyncGrid(SkinGrid, SkinTones, row => (row, clothes, secondary), SkinTones.SelectedIndex);
         SyncGrid(ClothesGrid, Clothes, row => (skin, row, secondary), Clothes.SelectedIndex);
         if (HasSecondary) SyncGrid(SecondaryGrid, Secondary, row => (skin, clothes, row), Secondary.SelectedIndex);
         else SecondaryGrid.Clear();
      }

      private void SyncGrid(ObservableCollection<CharacterGridItem> grid, CharacterColorList list, Func<CharacterColorRow, (CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary)> look, int selected) {
         for (int i = 0; i < list.Data.Count; i++) {
            if (i >= grid.Count) grid.Add(new CharacterGridItem(i, index => list.Select(index)));
            DrawGridItem(grid[i], list.Data[i], look(list.Data[i]), i == selected);
         }
         while (grid.Count > list.Data.Count) grid.RemoveAt(grid.Count - 1);
      }

      private void DrawGridItem(CharacterGridItem item, CharacterColorRow row, (CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary) look, bool selected) {
         item.Name = row.Name;
         item.Selected = selected;
         item.Boy.Replace(Boy.DrawStanding(FindRole(CharacterRole.Male, CharacterRole.OverworldContext), look.skin, look.clothes, look.secondary));
         item.Girl.Replace(Girl.DrawStanding(FindRole(CharacterRole.Female, CharacterRole.OverworldContext), look.skin, look.clothes, look.secondary));
      }

      /// <summary>A colour of a row was edited: show it in the previews and in its tile.</summary>
      internal void ColorsEdited(CharacterColorList list, int index) {
         RedrawPreviews();
      }

      internal void SelectionChanged(CharacterColorList list) {
         viewPort.Refresh();
         RedrawPreviews();
      }

      #endregion

      #region Editing

      /// <summary>Closes the current undo step: whatever is changed next can be undone on its own.</summary>
      internal void CompleteEdit() => viewPort.ChangeHistory.ChangeCompleted();

      internal void ReportError(string message) => OnError?.Invoke(this, message);

      /// <summary>
      /// Runs one change of a table (add, remove, rename...) as a single undo step, then reads everything again.
      /// Returns false after telling the person why if the change was refused.
      /// </summary>
      internal bool RunEdit(CharacterColorList list, Func<CharacterEditResult> edit, int keepSelection = -1) {
         CompleteEdit();
         CharacterEditResult result;
         try {
            result = edit();
         } catch (Exception e) {
            CompleteEdit();
            ReportError("Could not change the table: " + e.Message);
            Reload();
            return false;
         }
         CompleteEdit();
         if (!result.Success) { ReportError(result.Message); return false; }
         viewPort.Refresh();
         list.Reload(result.Index >= 0 ? result.Index : keepSelection);
         DescribeRoles();
         RedrawPreviews();
         if (!string.IsNullOrEmpty(result.Message)) OnMessage?.Invoke(this, result.Message);
         return true;
      }

      #endregion

      #region Opening the other editors

      private void ShowInMainTab(int address) {
         if (address < 0) return;
         viewPort.Goto.Execute(address);
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      internal void OpenTable(CharacterColorTable table) {
         var run = table.Run;
         if (run == null) { OnError?.Invoke(this, $"This ROM has no {table.Noun} table."); return; }
         ShowInMainTab(run.Start);
      }

      private void OpenRoles() {
         var run = customization.RoleRun;
         if (run == null) { OnError?.Invoke(this, "This ROM has no roles table."); return; }
         ShowInMainTab(run.Start);
      }

      /// <summary>Opens a picture of the character in the image editor (a new tab). Its palette shows which slots the game paints over.</summary>
      internal void EditPicture(int address, string description) {
         if (address < 0) { OnError?.Invoke(this, $"The {description} was not found in this ROM."); return; }
         CompleteEdit();
         viewPort.OpenImageEditorTab(address, 0, 0);
      }

      // one image editor tab per sheet: its dots choose the picture (standing, walking, running...)
      private readonly Dictionary<int, ImageEditorViewModel> sheetEditors = new();

      internal void EditOverworldSprites(CharacterPreviewPanel panel) {
         var sprites = panel.Sprites;
         if (sprites == null || sprites.OverworldAddress < 0) { OnError?.Invoke(this, $"The {panel.Title} overworld sprites were not found in this ROM."); return; }
         CompleteEdit();
         var editor = GetSheetEditor(sprites);
         if (editor != null) {
            var args = new TabChangeRequestedEventArgs(editor);
            RequestTabChange?.Invoke(this, args);
            if (args.RequestAccepted) return;
         }
         // the pictures can't be shown together (the sheet is compressed or in an unusual shape): edit the first picture on its own
         EditPicture(sprites.OverworldAddress, $"{panel.Title} overworld sprites");
      }

      /// <summary>The image editor with every picture of the sheet: the one already open for this character if there is one, otherwise a new one (null if the sheet can't be shown that way).</summary>
      private ImageEditorViewModel GetSheetEditor(CharacterSprites sprites) {
         var key = sprites.Gender;
         if (sheetEditors.TryGetValue(key, out var existing)) {
            existing.Frame = 0; // an editor whose sheet is gone closes itself here
            if (sheetEditors.TryGetValue(key, out existing)) return existing;
         }
         var source = new OverworldSheetFrameSource(model, sprites.OverworldIndex, $"{sprites.Title} overworld sprites");
         if (!source.Prepare()) return null;
         // registering the sheet's formats is not something undo should be able to take back from under the editor
         viewPort.ChangeHistory.ChangeCompleted();
         ImageEditorViewModel editor;
         try {
            editor = viewPort.CreateImageEditor(model.ReadPointer(source.SpritePointer(0)), 0, 0);
         } catch (ImageEditorViewModelCreationException) {
            return null;
         }
         editor.SetFrameSource(source, 0);
         sheetEditors[key] = editor;
         editor.Closed += (sender, e) => { if (sheetEditors.TryGetValue(key, out var current) && current == editor) sheetEditors.Remove(key); };
         return editor;
      }

      #endregion
   }
}
