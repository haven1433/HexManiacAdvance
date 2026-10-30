using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// The table tool's editor for a short list made only of numbers and enums, such as the wild encounters of a map:
   /// the generic stream editor shows one text line per element ("10, 15, Geodude"),
   /// this one shows one row per element, and every value of the row (min level, max level, species) gets its own box.
   /// That way Tab walks across a row and then down to the next one, instead of retyping whole lines.
   /// Edits are written straight into the data, like every other field of the table tool.
   /// </summary>
   public class TableRowsStreamElementViewModel : TextStreamElementViewModel {
      public const double NumberColumnWidth = 56, LabelColumnWidth = 96, ChanceColumnWidth = 44;
      public const double EnumColumnWidth = double.NaN; // the last column (the species) takes whatever width is left over

      private static readonly string[] LowLevelNames = { "lowLevel", "minLevel" };
      private static readonly string[] HighLevelNames = { "highLevel", "maxLevel" };

      /// <summary>
      /// Only encounter lists get the row editor: lists of 2-4 numbers/enums that have a minimum and a maximum level.
      /// Every other embedded table keeps the generic text editor.
      /// </summary>
      public static bool Supports(ITableRun table) {
         if (table == null || table.ElementCount < 1 || table.ElementCount > 64) return false;
         if (table is TableStreamRun stream && (stream.CanAppend || stream.AllowsZeroElements)) return false; // the rows can't add or remove elements
         var fields = table.ElementContent.Where(IsField).ToList();
         if (fields.Count < 2 || fields.Count > 4) return false;
         if (!fields.All(field => field is ArrayRunEnumSegment || field.GetType() == typeof(ArrayRunElementSegment))) return false;
         if (fields.Any(field => field.Type != ElementContentType.Integer)) return false;
         return fields.Any(field => LowLevelNames.Contains(field.Name, StringComparer.OrdinalIgnoreCase)) &&
                fields.Any(field => HighLevelNames.Contains(field.Name, StringComparer.OrdinalIgnoreCase));
      }

      private static bool IsField(ArrayRunElementSegment segment) => segment.Length > 0 && !segment.IsUnused();

      /// <summary>
      /// The title of a column: the field names of an encounter list are camelCase, and the user thinks in "min level, max level, pokemon".
      /// </summary>
      public static string GetColumnTitle(string fieldName) {
         if (LowLevelNames.Contains(fieldName, StringComparer.OrdinalIgnoreCase)) return "Min level";
         if (HighLevelNames.Contains(fieldName, StringComparer.OrdinalIgnoreCase)) return "Max level";
         if (fieldName.Equals("species", StringComparison.OrdinalIgnoreCase)) return "Pokémon";
         var builder = new StringBuilder();
         for (int i = 0; i < fieldName.Length; i++) {
            if (i > 0 && char.IsUpper(fieldName[i]) && !char.IsUpper(fieldName[i - 1])) builder.Append(' ');
            builder.Append(i == 0 ? char.ToUpper(fieldName[i]) : char.ToLower(fieldName[i]));
         }
         return builder.ToString();
      }

      private IReadOnlyList<TableColumnViewModel> columns = Array.Empty<TableColumnViewModel>();
      public IReadOnlyList<TableColumnViewModel> Columns => columns;

      public ObservableCollection<TableRowViewModel> Rows { get; } = new();

      private bool hasRowLabels;
      public bool HasRowLabels { get => hasRowLabels; private set => Set(ref hasRowLabels, value); }

      private bool hasChances;
      /// <summary>
      /// True if the odds of every row are known (the game picks a wild encounter slot by its number alone, see <see cref="WildEncounterChances"/>):
      /// each row then shows its chance in percent in front of its boxes.
      /// </summary>
      public bool HasChances { get => hasChances; private set => Set(ref hasChances, value); }

      // the name of the field that points to the list ("morningGrass", "daySurf", ...): what kind of encounters it holds, which decides the odds of its rows.
      private string areaName;

      /// <summary>
      /// While one of the species boxes is open for typing, the table tool must not refresh it underneath the user.
      /// </summary>
      public bool IsDropDownOpen => Rows.Any(row => row.Cells.OfType<EnumTableCellViewModel>().Any(cell => cell.FilteringComboOptions.DropDownIsOpen));

      /// <param name="areaName">The name of the field of the parent table that points to this list (optional): "grass", "morningFish", ... It tells land encounters from fishing ones. Without it, the size of the list decides.</param>
      public TableRowsStreamElementViewModel(ViewPort viewPort, string parentName, int start, string format, string areaName = null) : base(viewPort, parentName, start, format) {
         this.areaName = areaName ?? string.Empty;
         BuildRows();
      }

      // every cell reports its edits to this object: a cell never stores a reference to a particular view model, only this callback.
      // (the table tool throws away its view models and copies the new data into the old ones: the old ones are the ones the user is typing in)
      private void RaiseCellChanged() {
         RefreshContentFromData();
         using (PreventSelfCopy()) RaiseDataChanged();
      }

      private void BuildRows() {
         var newColumns = new List<TableColumnViewModel>();
         var newRows = new List<TableRowViewModel>();
         var anyLabels = false;
         var anyChances = false;
         var destination = Model.ReadPointer(Start);
         if (Model.GetNextRun(destination) is ITableRun table && table.Start == destination && Supports(table)) {
            // the odds of each row are fixed by the game, per slot: when they are known, say them (and don't repeat a comment that only says "20%")
            anyChances = WildEncounterChances.TryGetChances(areaName, table.ElementCount, out var chances, out var chanceGroups);
            var comments = table.ElementContent.OfType<ArrayRunCommentSegment>().Where(comment => !anyChances || !WildEncounterChances.IsPercentComment(comment.Comment)).ToList();
            anyLabels = comments.Count > 0 || (anyChances && chanceGroups.Count > 0);
            // every row has the same species list: only build it once (a combo box with the names of all pokemon is not cheap)
            var sharedOptions = new Dictionary<ArrayRunEnumSegment, IReadOnlyList<ComboOption>>();
            for (int i = 0; i < table.ElementCount; i++) {
               var cells = new List<TableCellViewModel>();
               var cellStart = table.Start + table.ElementLength * i;
               foreach (var segment in table.ElementContent) {
                  if (IsField(segment)) {
                     TableCellViewModel cell;
                     if (segment is ArrayRunEnumSegment enumSegment) {
                        if (!sharedOptions.TryGetValue(enumSegment, out var options)) sharedOptions[enumSegment] = options = enumSegment.GetComboOptions(Model).ToList();
                        cell = new EnumTableCellViewModel(ViewPort, segment.Name, cellStart, segment.Length, enumSegment, options, RaiseCellChanged);
                     } else {
                        cell = new NumericTableCellViewModel(ViewPort, segment.Name, cellStart, segment.Length, RaiseCellChanged);
                     }
                     cells.Add(cell);
                     if (i == 0) newColumns.Add(new(GetColumnTitle(segment.Name), segment is ArrayRunEnumSegment ? EnumColumnWidth : NumberColumnWidth));
                  }
                  cellStart += segment.Length;
               }
               // the tables of the original games say what the rows mean, like "20% common": show that on the row that starts a new comment.
               var label = string.Join(" ", comments.Where(comment => comment.Index == i).Select(comment => comment.Comment.Replace('_', ' ')));
               // a fishing list is three lists in one (old rod, good rod, super rod): name the one that starts here
               if (label.Length == 0 && anyChances) label = chanceGroups.Where(group => group.Start == i).Select(group => group.Name).FirstOrDefault() ?? string.Empty;
               var chance = anyChances ? WildEncounterChances.Format(chances[i]) : string.Empty;
               newRows.Add(new TableRowViewModel(i, label, anyLabels, cells, chance, anyChances));
            }
         }

         columns = newColumns;
         Rows.Clear();
         foreach (var row in newRows) Rows.Add(row);
         HasRowLabels = anyLabels;
         HasChances = anyChances;
         NotifyPropertyChanged(nameof(Columns));
      }

      #region Copy / paste the whole list as text

      private bool showCopied;
      /// <summary>
      /// Becomes true each time <see cref="CopyTable"/> puts the list on the clipboard (false, then true again on a second copy),
      /// so the view can flash a short "Copied!" next to the button.
      /// </summary>
      public bool ShowCopied { get => showCopied; private set => Set(ref showCopied, value); }

      private string ListName => string.IsNullOrEmpty(areaName) ? "the list" : $"the {areaName} list";

      /// <summary>
      /// Puts the whole list on the clipboard as plain text, in the format of HMA's original text editor for these lists:
      /// one line per row ("2, 3, Zigzagoon"), with the "# comment" lines of the tables that have them.
      /// The same text pastes back here, into the text editor of upstream HMA, or anywhere else.
      /// </summary>
      public void CopyTable(IFileSystem fileSystem) {
         RefreshContentFromData();
         fileSystem.CopyText = Content ?? string.Empty;
         ShowCopied = false;
         ShowCopied = true;
         ViewPort.RaiseMessage($"Copied the {Rows.Count} rows of {ListName} as text.");
      }

      /// <summary>
      /// Reads the clipboard text (as written by <see cref="CopyTable"/> or by the text editor of upstream HMA) into the list.
      /// Nothing is written unless every line can be read; the whole paste is a single step for Undo.
      /// </summary>
      public void PasteTable(IFileSystem fileSystem) {
         ShowCopied = false;
         if (!TryPasteTable(fileSystem.CopyText, out var error)) ViewPort.RaiseError(error);
      }

      /// <summary>
      /// Checks every line of the text first, then writes all of it at once.
      /// Accepts lines like "2, 3, Zigzagoon" (also without commas, with quotes around names that have spaces, or with the '+' that a deep copy puts in front of a line).
      /// Empty lines and "# comment" lines are skipped.
      /// </summary>
      /// <returns>false (and writes nothing) if the text does not have exactly one line per row, or any value cannot be read.</returns>
      public bool TryPasteTable(string text, out string error) {
         error = null;
         var destination = Model.ReadPointer(Start);
         if (Model.GetNextRun(destination) is not ITableRun table || table.Start != destination || !Supports(table)) {
            error = "This list cannot be pasted into.";
            return false;
         }
         if (string.IsNullOrWhiteSpace(text)) {
            error = "The clipboard has no text to paste.";
            return false;
         }

         var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("#"))
            .Select(line => line[0] == ArrayRun.ExtendArray ? line.Substring(1).Trim() : line)
            .ToList();
         if (lines.Count != table.ElementCount) {
            error = $"Expected {table.ElementCount} lines of text (one per row: min level, max level, Pokémon), but the clipboard has {lines.Count}.";
            return false;
         }

         // the text has one value for each part of a row that has a length (the same values the text editor shows): an "unused" part too
         var segments = table.ElementContent.Where(segment => segment.Length > 0).ToList();
         var values = new int[table.ElementCount, segments.Count];
         for (int i = 0; i < lines.Count; i++) {
            var tokens = TableStreamRun.Tokenize(lines[i]);
            if (tokens.Count > segments.Count && segments[segments.Count - 1] is ArrayRunEnumSegment) {
               // a name with spaces that was typed without quotes: "2, 3, Mr. Mime"
               tokens[segments.Count - 1] = string.Join(" ", tokens.Skip(segments.Count - 1));
               tokens.RemoveRange(segments.Count, tokens.Count - segments.Count);
            }
            if (tokens.Count != segments.Count) {
               error = $"Line {i + 1} \"{lines[i]}\": expected {segments.Count} values ({string.Join(", ", segments.Select(segment => GetColumnTitle(segment.Name).ToLower()))}), found {tokens.Count}.";
               return false;
            }
            for (int j = 0; j < segments.Count; j++) {
               var segment = segments[j];
               var max = segment.Length >= 4 ? int.MaxValue : (1 << (8 * segment.Length)) - 1;
               var token = tokens[j].Trim();
               int value;
               if (segment is ArrayRunEnumSegment enumSegment) {
                  if (!enumSegment.TryParse(Model, token, out value)) {
                     error = $"Line {i + 1} \"{lines[i]}\": \"{token}\" is not a known {GetColumnTitle(segment.Name)}.";
                     return false;
                  }
               } else if (!token.TryParseInt(out value)) {
                  error = $"Line {i + 1} \"{lines[i]}\": \"{token}\" is not a number ({GetColumnTitle(segment.Name).ToLower()}).";
                  return false;
               }
               if (value < 0 || value > max) {
                  error = $"Line {i + 1} \"{lines[i]}\": {GetColumnTitle(segment.Name).ToLower()} {value} is out of range (0 to {max}).";
                  return false;
               }
               values[i, j] = value;
            }
         }

         // everything can be read: write it as one change (the paste is undone in one step, apart from any edit typed before it)
         ViewPort.ChangeHistory.ChangeCompleted();
         var changeToken = ViewPort.CurrentChange;
         var changed = new List<int>();
         for (int i = 0; i < table.ElementCount; i++) {
            var offset = table.Start + table.ElementLength * i;
            int j = 0;
            foreach (var segment in table.ElementContent) {
               if (segment.Length == 0) continue;
               if (Model.ReadMultiByteValue(offset, segment.Length) != values[i, j]) {
                  Model.WriteMultiByteValue(offset, segment.Length, changeToken, values[i, j]);
                  changed.Add(offset);
               }
               offset += segment.Length;
               j++;
            }
         }
         foreach (var address in changed) {
            var offsets = table.ConvertByteOffsetToArrayOffset(address);
            ViewPort.HandleErrorInfo(table.NotifyChildren(Model, changeToken, offsets.ElementIndex, offsets.SegmentIndex));
         }
         ViewPort.ChangeHistory.ChangeCompleted();

         // the boxes show the new values (the same boxes: the keyboard focus stays where it is)
         RefreshCells();
         RaiseCellChanged();
         ViewPort.RaiseMessage(changed.Count == 0 ? $"The pasted rows are the same as the rows of {ListName}." : $"Pasted {table.ElementCount} rows into {ListName}.");
         return true;
      }

      /// <summary>Reads the data again into the existing boxes (or builds new ones if the shape of the list changed).</summary>
      private void RefreshCells() {
         var fresh = new TableRowsStreamElementViewModel(ViewPort, ParentName, Start, RunFormat, areaName);
         bool sameShape = Rows.Count == fresh.Rows.Count && columns.Count == fresh.columns.Count;
         for (int i = 0; sameShape && i < Rows.Count; i++) sameShape = Rows[i].TryCopy(fresh.Rows[i]);
         if (!sameShape) BuildRows();
      }

      #endregion

      protected override bool TryCopy(StreamElementViewModel other) {
         if (other is not TableRowsStreamElementViewModel that) return false;
         if (!base.TryCopy(other)) return false;
         areaName = that.areaName;

         // keep the existing boxes (and with them, the keyboard focus) as long as the shape of the list is the same: just update their values.
         bool sameShape = Rows.Count == that.Rows.Count && columns.Count == that.columns.Count;
         for (int i = 0; sameShape && i < Rows.Count; i++) sameShape = Rows[i].TryCopy(that.Rows[i]);
         if (!sameShape) BuildRows();
         NotifyPropertyChanged(nameof(IsDropDownOpen));
         return true;
      }
   }

   public class TableColumnViewModel {
      public string Header { get; }
      public double Width { get; }
      public TableColumnViewModel(string header, double width) => (Header, Width) = (header, width);
   }

   public class TableRowViewModel : ViewModelCore {
      public int Index { get; }
      public string Label { get; private set; }
      public bool ShowLabel { get; }

      /// <summary>How often this row is the one that appears, in percent ("20%"): empty if the game's odds for this kind of list are not known.</summary>
      public string Chance { get; private set; }
      public bool ShowChance { get; }
      public IReadOnlyList<TableCellViewModel> Cells { get; }

      public TableRowViewModel(int index, string label, bool showLabel, IReadOnlyList<TableCellViewModel> cells, string chance = "", bool showChance = false) {
         (Index, Label, ShowLabel, Cells, Chance, ShowChance) = (index, label, showLabel, cells, chance ?? string.Empty, showChance);
      }

      public bool TryCopy(TableRowViewModel other) {
         if (other.Cells.Count != Cells.Count || other.ShowLabel != ShowLabel || other.ShowChance != ShowChance) return false;
         for (int i = 0; i < Cells.Count; i++) if (!Cells[i].TryCopy(other.Cells[i])) return false;
         if (Label != other.Label) {
            Label = other.Label;
            NotifyPropertyChanged(nameof(Label));
         }
         if (Chance != other.Chance) {
            Chance = other.Chance;
            NotifyPropertyChanged(nameof(Chance));
         }
         return true;
      }
   }

   public abstract class TableCellViewModel : ViewModelCore {
      protected readonly ViewPort viewPort;
      protected readonly Action raiseDataChanged;

      public string Name { get; }
      public int Start { get; protected set; }
      public int Length { get; }

      protected TableCellViewModel(ViewPort viewPort, string name, int start, int length, Action raiseDataChanged) {
         (this.viewPort, Name, Start, Length, this.raiseDataChanged) = (viewPort, name, start, length, raiseDataChanged);
      }

      /// <summary>
      /// Copies the data of another cell for the same field: the boxes themselves stay, the callbacks stay.
      /// </summary>
      public abstract bool TryCopy(TableCellViewModel other);

      /// <summary>
      /// The same follow-up every table tool field does after a write: tell the table about the change.
      /// </summary>
      protected void NotifyTable() {
         if (viewPort.Model.GetNextRun(Start) is not ITableRun table || table.Start > Start) return;
         var offsets = table.ConvertByteOffsetToArrayOffset(Start);
         var info = table.NotifyChildren(viewPort.Model, viewPort.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
         if (info.HasError && info.IsWarning) viewPort.RaiseMessage(info.ErrorMessage);
         else if (info.HasError) viewPort.RaiseError(info.ErrorMessage);
      }
   }

   public class NumericTableCellViewModel : TableCellViewModel {
      private string content;

      /// <summary>
      /// The text of the box. Anything that is not a number is kept in the box (so you can keep typing) but is not written.
      /// </summary>
      public string Content {
         get => content;
         set {
            if (!TryUpdate(ref content, value)) return;
            if (!content.TryParseInt(out var number)) return;
            viewPort.Model.WriteMultiByteValue(Start, Length, viewPort.CurrentChange, number);
            NotifyTable();
            raiseDataChanged();
         }
      }

      public NumericTableCellViewModel(ViewPort viewPort, string name, int start, int length, Action raiseDataChanged) : base(viewPort, name, start, length, raiseDataChanged) {
         content = ReadValue().ToString();
      }

      private int ReadValue() => viewPort.Model.ReadMultiByteValue(Start, Length);

      /// <summary>Up arrow, like the number fields of the table tool.</summary>
      public void Increment() => Nudge(1);

      /// <summary>Down arrow, like the number fields of the table tool.</summary>
      public void Decrement() => Nudge(-1);

      private void Nudge(int amount) {
         if (!(content ?? string.Empty).TryParseInt(out var number)) return;
         var max = Length >= 4 ? int.MaxValue : (1 << (8 * Length)) - 1;
         Content = Math.Min(max, Math.Max(0, number + amount)).ToString();
      }

      public override bool TryCopy(TableCellViewModel other) {
         if (other is not NumericTableCellViewModel that) return false;
         if (Name != that.Name || Length != that.Length) return false;
         Start = that.Start;
         // if the user typed "015" and the data says 15, leave the text alone: that is the same value
         if (!(content != null && content.TryParseInt(out var typed) && typed == ReadValue())) TryUpdate(ref content, that.content, nameof(Content));
         return true;
      }
   }

   public class EnumTableCellViewModel : TableCellViewModel {
      private readonly ArrayRunEnumSegment segment;
      private bool copying;

      public FilteringComboOptions FilteringComboOptions { get; } = new();

      public EnumTableCellViewModel(ViewPort viewPort, string name, int start, int length, ArrayRunEnumSegment segment, IReadOnlyList<ComboOption> allOptions, Action raiseDataChanged) : base(viewPort, name, start, length, raiseDataChanged) {
         this.segment = segment;
         AddSilentChild(FilteringComboOptions);

         var options = new List<ComboOption>(allOptions);
         var value = viewPort.Model.ReadMultiByteValue(start, length) - segment.ValueOffset;
         int selectedIndex = options.FindIndex(option => option.Index == value);
         if (selectedIndex < 0) {
            // a value that has no name (yet): show the number, so the box does not lie about the data
            selectedIndex = options.Count(option => option.Index < value);
            options.Insert(selectedIndex, new ComboOption(value.ToString(), value));
         }
         FilteringComboOptions.Update(options, selectedIndex);
         FilteringComboOptions.Bind(nameof(FilteringComboOptions.ModelValue), (sender, e) => {
            if (copying) return;
            viewPort.Model.WriteMultiByteValue(Start, Length, viewPort.CurrentChange, sender.ModelValue + segment.ValueOffset);
            NotifyTable();
            raiseDataChanged();
         });
      }

      public override bool TryCopy(TableCellViewModel other) {
         if (other is not EnumTableCellViewModel that) return false;
         if (Name != that.Name || Length != that.Length || segment.EnumName != that.segment.EnumName) return false;
         Start = that.Start;
         using (Scope(ref copying, true, old => copying = old)) {
            FilteringComboOptions.Update(that.FilteringComboOptions.AllOptions, that.FilteringComboOptions.SelectedIndex);
         }
         return true;
      }
   }
}
