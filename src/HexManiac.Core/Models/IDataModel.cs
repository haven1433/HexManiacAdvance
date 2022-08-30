using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.Models {
   public interface IDataModel : IReadOnlyList<byte>, IEquatable<IDataModel> {
      /// <summary>
      /// Represents any background work being done by the model during startup or after calling Load()
      /// </summary>
      Task InitializationWorkload { get; }

      byte[] RawData { get; }
      ModelCacheScope CurrentCacheScope { get; }
      bool HasChanged(int index);
      int ChangeCount { get; }
      void ResetChanges();

      /// <summary>
      /// Used during repointing
      /// </summary>
      int FreeSpaceStart { get; set; }
      int FreeSpaceBuffer { get; set; }

      /// <summary>
      /// Used when exporting to the backup folder
      /// </summary>
      int NextExportID { get; set; }

      IFormatRunFactory FormatRunFactory { get; }
      ITextConverter TextConverter { get; }

      new byte this[int index] { get; set; }
      IReadOnlyList<string> ListNames { get; }
      IReadOnlyList<ArrayRun> Arrays { get; }
      IEnumerable<T> All<T>() where T : IFormattedRun;
      IReadOnlyList<IStreamRun> Streams { get; }
      IReadOnlyList<string> Anchors { get; }

      IReadOnlyList<GotoShortcutModel> GotoShortcuts { get; }

      /// <summary>
      /// If dataIndex is in the middle of a run, returns that run.
      /// If dataIndex is between runs, returns the next available run.
      /// If dataIndex is before the first run, return the first run.
      /// If dataIndex is after the last run, return a run that starts at int.MaxValue.
      /// </summary>
      IFormattedRun GetNextRun(int dataIndex);

      /// <summary>
      /// If dataIndex is exactly at the start of an anchor, return that run.
      /// If dataIndex is between two anchors, return the next anchor.
      /// If dataIndex is after the last anchor, return an anchor at int.MaxValue.
      /// </summary>
      IFormattedRun GetNextAnchor(int dataIndex);

      bool TryGetUsefulHeader(int address, out string header);
      bool TryGetList(string name, out ValidationList nameArray);

      bool IsAtEndOfArray(int dataIndex, out ITableRun tableRun); // is this byte the first one after the end of a table run? (also return true if the table is length 0 and starts right here)

      void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run);
      void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run);
      void MassUpdateFromDelta(
         IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd,
         IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd,
         IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd,
         IReadOnlyDictionary<int, string> matchedWordsToRemove, IReadOnlyDictionary<int, string> matchedWordsToAdd,
         IReadOnlyDictionary<int, int> offsetPointersToRemove, IReadOnlyDictionary<int, int> offsetPointersToAdd,
         IReadOnlyDictionary<string, int> unmappedConstantsToRemove, IReadOnlyDictionary<string, int> unmappedConstantsToAdd,
         IReadOnlyDictionary<string, ValidationList> listsToRemove, IReadOnlyDictionary<string, ValidationList> listsToAdd);
      T RelocateForExpansion<T>(ModelDelta changeToken, T run, int minimumLength) where T : IFormattedRun;
      int FindFreeSpace(int start, int length);
      void ClearAnchor(ModelDelta changeToken, int start, int length);
      void ClearFormat(ModelDelta changeToken, int start, int length);
      void ClearData(ModelDelta changeToken, int start, int length);
      void ClearFormatAndData(ModelDelta changeToken, int start, int length);
      void SetList(ModelDelta changeToken, string name, IReadOnlyList<string> list, string hash);
      void ClearPointer(ModelDelta currentChange, int source, int destination);
      string Copy(Func<ModelDelta> changeToken, int start, int length, bool deep = false);

      void Load(byte[] newData, StoredMetadata metadata);
      void ExpandData(ModelDelta changeToken, int minimumLength);
      void ContractData(ModelDelta changeToken, int maximumLength);

      SortedSpan<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses);
      bool WritePointer(ModelDelta changeToken, int address, int pointerDestination);
      bool WriteValue(ModelDelta changeToken, int address, int value);
      int ReadPointer(int address);
      int ReadValue(int address);

      SortedSpan<int> GetUnmappedSourcesToAnchor(string anchor);
      void SetUnmappedConstant(ModelDelta changeToken, string name, int value);
      bool TryGetUnmappedConstant(string name, out int value);
      int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor);
      string GetAnchorFromAddress(int requestSource, int destination);
      IEnumerable<string> GetAutoCompleteAnchorNameOptions(string partial, int maxResults = 30);
      StoredMetadata ExportMetadata(IMetadataInfo metadataInfo);
      void UpdateArrayPointer(ModelDelta changeToken, ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, int address, int destination);
      int ConsiderResultsAsTextRuns(Func<ModelDelta> futureChange, IReadOnlyList<int> startLocations);

      IEnumerable<string> GetAutoCompleteByteNameOptions(string text);
      IReadOnlyList<int> GetMatchedWords(string name);
      IReadOnlyList<TableGroup> GetTableGroups(string tableName);
      void AppendTableGroup(ModelDelta token, string groupName, IReadOnlyList<string> tableNames, string hash);
   }

   public class LimitSet<T> : HashSet<T>, ISet<T> {
      private readonly int limit;
      public LimitSet(int limit) => this.limit = limit;
      bool ISet<T>.Add(T element) {
         if (Count >= limit) return false;
         return Add(element);
      }
   }

   public abstract class BaseModel : IDataModel {
      public const int PointerOffset = 0x08000000;

      private readonly ISet<int> changes = new LimitSet<int>(1000);

      public Task InitializationWorkload { get; protected set; }

      public byte[] RawData { get; private set; }

      private ModelCacheScope currentCacheScope;
      protected void ClearCacheScope() => currentCacheScope = null;
      public ModelCacheScope CurrentCacheScope {
         get {
            if (currentCacheScope == null) currentCacheScope = new ModelCacheScope(this);
            return currentCacheScope;
         }
      }

      public BaseModel(byte[] data) {
         RawData = data;
         var code = data.GetGameCode();
         if (code.Length > 4) code = code.Substring(0, 4);
         TextConverter = new PCSConverter(code);
         InitializationWorkload = Task.CompletedTask;
      }

      public int FreeSpaceStart { get; set; }

      public int FreeSpaceBuffer { get; set; } = 0x100;

      public int NextExportID { get; set; }

      public IFormatRunFactory FormatRunFactory { get; protected set; } = new FormatRunFactory(false);
      public ITextConverter TextConverter { get; private set; }

      public virtual IReadOnlyList<string> ListNames { get; } = new List<string>();
      public virtual IReadOnlyList<ArrayRun> Arrays { get; } = new List<ArrayRun>();
      public virtual IEnumerable<T> All<T>() where T : IFormattedRun { yield break; }
      public virtual IReadOnlyList<IStreamRun> Streams { get; } = new List<IStreamRun>();
      public virtual IReadOnlyList<string> Anchors { get; } = new List<string>();
      public IReadOnlyList<GotoShortcutModel> GotoShortcuts { get; } = new List<GotoShortcutModel>();

      public virtual byte this[int index] {
         get => RawData[index];
         set {
            RawData[index] = value;
            ClearCacheScope();
            changes.Add(index);
         }
      }

      public bool HasChanged(int index) => changes.Contains(index);
      public int ChangeCount => changes.Count;
      public void ResetChanges() => changes.Clear();

      byte IReadOnlyList<byte>.this[int index] => RawData[index];

      public int Count => RawData.Length;

      public static IEnumerable<StoredMetadata> GetDefaultMetadatas(params string[] codes) {
         if (File.Exists("resources/default.toml")) {
            var lines = File.ReadAllLines("resources/default.toml");
            var metadata = new StoredMetadata(lines);
            yield return metadata;
         }

         var files = Directory.GetFiles("resources", "default.*.toml");
         foreach (var fileName in files) {
            foreach (var code in codes) {
               if (!fileName.ToLower().Contains($".{code.ToLower()}.")) continue;
               var lines = File.ReadAllLines(fileName);
               var metadata = new StoredMetadata(lines);
               yield return metadata;
            }
         }
      }

      public abstract void ClearAnchor(ModelDelta changeToken, int start, int length);

      public abstract void ClearFormat(ModelDelta changeToken, int start, int length);

      public virtual void ClearData(ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(this, start + i, 0xFF);
      }

      public abstract void ClearFormatAndData(ModelDelta changeToken, int originalStart, int length);

      public virtual void SetList(ModelDelta changeToken, string name, IReadOnlyList<string> list, string hash) => throw new NotImplementedException();

      public abstract string Copy(Func<ModelDelta> changeToken, int start, int length, bool deep = false);

      public void ExpandData(ModelDelta changeToken, int minimumIndex) {
         if (Count > minimumIndex) return;
         if (minimumIndex > 0x2000000) throw new NotSupportedException($"Unable to expand to 0x{minimumIndex:X6} bytes.");

         var newData = new byte[minimumIndex + 1];
         Array.Copy(RawData, newData, RawData.Length);
         for (int i = RawData.Length; i < newData.Length; i++) newData[i] = 0xFF;
         changeToken.SetDataLength(this, newData.Length);
         RawData = newData;
      }

      public void ContractData(ModelDelta changeToken, int maximumIndex) {
         if (Count <= maximumIndex + 1) return;

         var newData = new byte[maximumIndex + 1];
         Array.Copy(RawData, newData, newData.Length);
         changeToken.SetDataLength(this, newData.Length);
         RawData = newData;
      }

      public virtual IReadOnlyList<int> GetMatchedWords(string name) => new int[0];

      public virtual SortedSpan<int> GetUnmappedSourcesToAnchor(string anchor) => SortedSpan<int>.None;

      public virtual void SetUnmappedConstant(ModelDelta changeToken, string name, int value) { }

      public virtual bool TryGetUnmappedConstant(string name, out int value) { value = default; return false; }

      public abstract int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor);

      public abstract string GetAnchorFromAddress(int requestSource, int destination);

      public IEnumerator<byte> GetEnumerator() => ((IList<byte>)RawData).GetEnumerator();

      public abstract IFormattedRun GetNextRun(int dataIndex);

      public abstract IFormattedRun GetNextAnchor(int dataIndex);

      public virtual bool TryGetUsefulHeader(int address, out string header) { header = null; return false; }

      public virtual bool TryGetList(string name, out ValidationList list) { list = null; return false; }

      public abstract bool IsAtEndOfArray(int dataIndex, out ITableRun tableRun);

      public virtual void Load(byte[] newData, StoredMetadata metadata) {
         InitializationWorkload.Wait();
         RawData = newData;
      }

      public abstract void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run);

      public abstract void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run);

      public abstract void MassUpdateFromDelta(
         IReadOnlyDictionary<int, IFormattedRun> runsToRemove,
         IReadOnlyDictionary<int, IFormattedRun> runsToAdd,
         IReadOnlyDictionary<int, string> namesToRemove,
         IReadOnlyDictionary<int, string> namesToAdd,
         IReadOnlyDictionary<int, string> unmappedPointersToRemove,
         IReadOnlyDictionary<int, string> unmappedPointersToAdd,
         IReadOnlyDictionary<int, string> matchedWordsToRemove,
         IReadOnlyDictionary<int, string> matchedWordsToAdd,
         IReadOnlyDictionary<int, int> offsetPointersToRemove,
         IReadOnlyDictionary<int, int> offsetPointersToAdd,
         IReadOnlyDictionary<string, int> unmappedConstantsToRemove,
         IReadOnlyDictionary<string, int> unmappedConstantsToAdd,
         IReadOnlyDictionary<string, ValidationList> listsToRemove,
         IReadOnlyDictionary<string, ValidationList> listsToAdd);

      public abstract T RelocateForExpansion<T>(ModelDelta changeToken, T run, int minimumLength) where T : IFormattedRun;

      public abstract int FindFreeSpace(int start, int length);

      public abstract SortedSpan<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses);

      public abstract void UpdateArrayPointer(ModelDelta currentChange, ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, int index, int fullValue);

      public abstract void ClearPointer(ModelDelta currentChange, int source, int destination);

      public int ReadValue(int index) => BitConverter.ToInt32(RawData, index);

      public bool WriteValue(ModelDelta changeToken, int index, int word) {
         var anyChange = false;
         anyChange |= changeToken.ChangeData(this, index + 0, (byte)(word >> 0));
         anyChange |= changeToken.ChangeData(this, index + 1, (byte)(word >> 8));
         anyChange |= changeToken.ChangeData(this, index + 2, (byte)(word >> 16));
         anyChange |= changeToken.ChangeData(this, index + 3, (byte)(word >> 24));
         return anyChange;
      }

      public virtual int ReadPointer(int index) => ReadValue(index) - PointerOffset;

      public virtual bool WritePointer(ModelDelta changeToken, int address, int pointerDestination) => WriteValue(changeToken, address, pointerDestination + PointerOffset);

      /// <summary>
      /// Returns the number of new runs found.
      /// </summary>
      public virtual int ConsiderResultsAsTextRuns(Func<ModelDelta> futureChange, IReadOnlyList<int> startLocations) => 0;

      public virtual IEnumerable<string> GetAutoCompleteAnchorNameOptions(string partial, int maxResults = 30) => new string[0];

      public virtual IEnumerable<string> GetAutoCompleteByteNameOptions(string text) => new string[0];

      public virtual StoredMetadata ExportMetadata(IMetadataInfo metadataInfo) => null;

      public virtual IReadOnlyList<TableGroup> GetTableGroups(string tableName) => null;

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public bool Equals(IDataModel other) => other == this;

      public virtual void AppendTableGroup(ModelDelta token, string groupName, IReadOnlyList<string> tableNames, string hash) { }
   }

   public static class IDataModelExtensions {
      public const int GameCodeStart = 0xAC;
      public const int GameVersionStart = 0xBC;
      public static string GetGameCode(this IReadOnlyList<byte> model) {
         if (model.Count <= GameVersionStart) return string.Empty;
         var code = new string(Enumerable.Range(GameCodeStart, 4).Select(i => (char)model[i]).ToArray());
         code += model[GameVersionStart]; // should be "0" or "1"
         return code;
      }

      public static int ReadMultiByteValue(this IReadOnlyList<byte> model, int index, int length) {
         int word = 0;
         while (length > 0) {
            word <<= 8;
            word += model[index + length - 1];
            length--;
         }
         return word;
      }

      public static bool WriteMultiByteValue(this IDataModel model, int index, int length, ModelDelta changeToken, int value) {
         Debug.Assert(length > 0, "Trying to write a value with no length!");
         var anyChange = false;
         for (int i = 0; i < length; i++) {
            anyChange |= changeToken.ChangeData(model, index + i, (byte)value);
            value >>= 8;
         }
         return anyChange;
      }

      /// <summary>
      /// Returns the array by the given name, if it exists
      /// </summary>
      public static bool TryGetNameArray(this IDataModel model, string anchorName, out ArrayRun array) {
         array = null;

         // anchorName must name an array              enum must be the name of an array that starts with a string
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchorName);
         if (address == Pointer.NULL) return false;
         array = model.GetNextRun(address) as ArrayRun;
         if (array == null) return false;

         // the array must contain a text or pointer-to-text element
         return array.ElementContent.Any(segment =>
            segment.Type == ElementContentType.PCS ||
            (segment is ArrayRunPointerSegment pSegment && pSegment.InnerFormat == PCSRun.SharedFormatString));
      }

      public static bool TryGetIndexNames(this IDataModel model, string anchorName, out IReadOnlyList<string> names) {
         names = null;

         // verify that this anchor is an index-array
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchorName);
         var array = model.GetNextRun(address) as ArrayRun;
         if (array == null) return false;
         if (array.ElementContent.Count != 1) return false;
         if (array.ElementContent[0].Type != ElementContentType.Integer) return false;

         // verify that the parent is a name-array
         var parentName = array.LengthFromAnchor;
         if (parentName == string.Empty) return false;
         address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, parentName);
         var parent = model.GetNextRun(address) as ArrayRun;
         if (parent == null) return false;
         if (parent.ElementContent.Count != 1) return false;
         if (parent.ElementContent[0].Type != ElementContentType.PCS) return false;

         var offset = array.FormatString.EndsWith("-1") ? 1 : 0;
         var rawOptions = model.GetOptions(parentName);

         // create a name array, where the names are rearranged based on the index array
         var result = new string[rawOptions.Count];
         for (int i = 0; i < result.Length - offset; i++) {
            var index = model.ReadMultiByteValue(array.Start + array.ElementLength * i, array.ElementLength);
            if (index < offset) return false;
            if (index - offset >= result.Length) return false;
            if (i + offset >= rawOptions.Count || index >= result.Length) return false;
            result[index] = rawOptions[i + offset];
            if (result[i] == null) result[i] = "?unused?";
         }

         names = result;
         return true;
      }

      /// <summary>
      /// If anchorName is a table with enums based on another enum, return the appropriate names taken from the original enum list.
      /// </summary>
      public static bool TryGetDerivedEnumNames(this IDataModel model, string anchorName, out IReadOnlyList<string> names) {
         names = null;
         var mainEnumAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchorName);
         var enumArray = model.GetNextRun(mainEnumAddress) as ArrayRun;
         if (enumArray == null) return false;
         if (enumArray.ElementCount < 2) return false;
         if (enumArray.ElementContent.Count != 1) return false;
         if (!(enumArray.ElementContent[0] is ArrayRunEnumSegment mainEnumSegment)) return false;
         var enumSourceAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, mainEnumSegment.EnumName);
         var sourceArray = model.GetNextRun(enumSourceAddress) as ArrayRun;
         if (sourceArray == null) return false;
         using (ModelCacheScope.CreateScope(model)) {
            var allNames = sourceArray.ElementNames;
            if (allNames.Count == 0) return false;
            var list = new List<string>();

            for (int i = 0; i < enumArray.ElementCount; i++) {
               var enumValue = model.ReadMultiByteValue(enumArray.Start + enumArray.ElementLength * i, mainEnumSegment.Length);
               if (enumValue >= allNames.Count || enumValue < 0) return false;
               list.Add(allNames[enumValue]);
            }

            names = list;
            return true;
         }
      }

      /// <summary>
      /// If anchorName points to a table that's matched to a list, return the list elements
      /// </summary>
      public static bool TryGetListEnumNames(this IDataModel model, string anchorName, out IReadOnlyList<string> names) {
         names = null;
         if (model.GetTable(anchorName) is not ArrayRun array) return false;
         if (!model.TryGetList(array.LengthFromAnchor, out var list)) return false;
         names = list;
         return true;
      }

      public static List<int> FindPossibleTextStartingPlaces(this IDataModel model, int left, int length) {
         // part 1: find a previous FF, which is possibly the end of another text
         var startPlaces = new List<int>();
         if (left < 0 || left >= model.Count) return startPlaces;
         startPlaces.Add(left);
         while (left >= 0 && model[left] != 0xFF && PCSString.PCS[model[left]] != null) { left--; length++; }
         left++; length--;

         // part 2: jump forward past any known runs that we're interupting
         while (true) {
            var run = model.GetNextRun(left);
            if (run.Start >= left) break;
            length -= left - run.Start;
            left = run.Start + run.Length;
         }

         // part 3: look for possible starting locations:
         // (1) places that start directly after FF
         // (2) places that start with a NoInfoRun
         while (length > 0) {
            startPlaces.Add(left);
            var run = model.GetNextRun(left);
            if (run is NoInfoRun && run.Start < left + length) startPlaces.Add(run.Start);
            if (!(run is NoInfoRun) && run.Start < left + length) break;
            while (model.Count > left && model[left] != 0xFF) { left++; length--; }
            left++; length--;
         }

         // remove duplicates and make sure everything is in order
         startPlaces.Sort();
         startPlaces = startPlaces.Distinct().ToList();
         return startPlaces;
      }

      public static IEnumerable<AutoCompleteSelectionItem> GetNewPointerAutocompleteOptions(this IDataModel model, string text, int selectedIndex) {
         var options = model.GetAutoCompleteAnchorNameOptions(text.Substring(1));
         if (text.StartsWith(PointerRun.PointerStart.ToString())) options = options.Select(option => $"{PointerRun.PointerStart}{option}{PointerRun.PointerEnd}");
         if (text.StartsWith(ViewPort.GotoMarker.ToString())) options = options.Select(option => $"{ViewPort.GotoMarker}{option} ");
         return AutoCompleteSelectionItem.Generate(options, selectedIndex);
      }

      public static IEnumerable<AutoCompleteSelectionItem> GetNewWordAutocompleteOptions(this IDataModel model, string text, int selectedIndex) {
         IEnumerable<string> options;

         if (text.StartsWith(".")) {
            text = text.Substring(1);
            options = model.GetAutoCompleteByteNameOptions(text);
            options = options.Select(option => $".{option} ");
            return AutoCompleteSelectionItem.Generate(options, selectedIndex);
         }

         if (text.Length >= 2) text = text.Substring(2);
         else return null;
         options = model.GetAutoCompleteAnchorNameOptions(text);
         options = options.Select(option => $"::{option} ");
         return AutoCompleteSelectionItem.Generate(options, selectedIndex);
      }

      // wraps an IDataFormat in an anchor format, if it makes sense
      public static IDataFormat WrapFormat(this IDataModel model, IFormattedRun run, IDataFormat format, int dataIndex) {
         if (run.PointerSources != null && run.Start == dataIndex) {
            var name = model.GetAnchorFromAddress(-1, run.Start);
            return new Anchor(format, name, run.FormatString, run.PointerSources);
         }

         if (run is ArrayRun array && array.SupportsInnerPointers && (dataIndex - run.Start) % array.ElementLength == 0) {
            var arrayIndex = (dataIndex - run.Start) / array.ElementLength;
            var pointerSources = array.PointerSourcesForInnerElements[arrayIndex];
            if (pointerSources == null || pointerSources.Count == 0) return format;
            var name = model.GetAnchorFromAddress(-1, dataIndex);
            return new Anchor(format, name, string.Empty, pointerSources);
         }

         return format;
      }

      public static void LoadMetadata(this IDataModel model, StoredMetadata metadata) {
         var noChange = new NoDataChangeDeltaModel();
         foreach (var list in metadata.Lists) {
            if (model.TryGetList(list.Name, out var existingList) && !existingList.StoredHashMatches) {
               // the list has been manually tampered with by the user
               // do not update it
            } else {
               model.SetList(noChange, list.Name, list.Contents, list.Hash);
            }
         }
         foreach (var anchor in metadata.NamedAnchors) PokemonModel.ApplyAnchor(model, noChange, anchor.Address, BaseRun.AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
         foreach (var match in metadata.MatchedWords) {
            model.ClearFormat(noChange, match.Address, match.Length);
            model.ObserveRunWritten(noChange, new WordRun(match.Address, match.Name, match.Length, match.AddOffset, match.MultOffset, match.Note));
         }
         foreach (var anchor in model.Anchors) {
            if (!(model.GetNextRun(model.GetAddressFromAnchor(noChange, -1, anchor)) is ArrayRun table)) continue;
            // the length may have changed: rewrite the run
            if (metadata.Lists.Select(list => list.Name).Contains(table.LengthFromAnchor)) {
               if (!ArrayRun.TryParse(model, table.FormatString, table.Start, table.PointerSources, out var newTable).HasError) {
                  model.ObserveAnchorWritten(noChange, anchor, newTable);
               }
            }
         }
         foreach (var pointer in metadata.OffsetPointers) {
            if (pointer.Address >= 0 && pointer.Address < model.Count) {
               if (model.GetNextRun(pointer.Address) is OffsetPointerRun existingPointerRun && existingPointerRun.Start == pointer.Address) {
                  // we already have an offset pointer run at this location: don't overwrite it.
               } else {
                  model.ObserveRunWritten(noChange, new OffsetPointerRun(pointer.Address, pointer.Offset));
               }
            }
         }
         var shortcuts = (IList<GotoShortcutModel>)model.GotoShortcuts;
         foreach (var gotoShortcut in metadata.GotoShortcuts) {
            if (shortcuts.Any(shortcut => shortcut.DisplayText == gotoShortcut.Display)) continue;
            var tableName = gotoShortcut.Anchor.Split("/")[0];
            if (shortcuts.Any(shortcut => shortcut.GotoAnchor.Split("/")[0] == tableName)) continue;
            shortcuts.Add(new GotoShortcutModel(gotoShortcut.Image, gotoShortcut.Anchor, gotoShortcut.Display));
         }

         foreach (var group in metadata.TableGroups) {
            model.AppendTableGroup(default, group.GroupName, group.Tables, group.Hash);
         }
         model.LoadMetadataProperties(metadata);
      }

      public static void LoadMetadataProperties(this IDataModel model, StoredMetadata metadata){
         if (metadata.NextExportID > 0) model.NextExportID = metadata.NextExportID;
         if (metadata.FreeSpaceSearch >= 0) model.FreeSpaceStart = Math.Min(model.Count - 1, metadata.FreeSpaceSearch);
         if (metadata.FreeSpaceBuffer >= 0) model.FreeSpaceBuffer = metadata.FreeSpaceBuffer;
      }

      public static ErrorInfo CompleteArrayExtension(this IDataModel model, ModelDelta changeToken, int count, ref ITableRun table) {
         var currentArrayName = model.GetAnchorFromAddress(-1, table.Start);
         if (!table.CanAppend) {
            return new ErrorInfo($"Cannot extend {currentArrayName}.");
         }

         var initialTableName = model.GetAnchorFromAddress(-1, table.Start);
         if (initialTableName == string.Empty) initialTableName = model.GetNameFromParent(table);
         var visitedNames = new List<string>() { initialTableName };
         var visitedAddress = new List<int>() { table.Start };

         if (table is ArrayRun arrayRun) {
            while (arrayRun.LengthFromAnchor != string.Empty) {
               if (visitedNames.Contains(arrayRun.LengthFromAnchor)) {
                  // We kept going up the chain of tables but didn't find a top table. table length definitions are circular.
                  return new ErrorInfo($"Could not extend table safely. Table length has a circular dependency involving {arrayRun.LengthFromAnchor}.");
               }

               // jump up to the parent table. If the LengthFromAnchor indicates a non-table (such as a named constant), bail
               var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, arrayRun.LengthFromAnchor);
               if (address != Pointer.NULL) {
                  visitedNames.Add(arrayRun.LengthFromAnchor);
                  visitedAddress.Add(address);
                  arrayRun = (ArrayRun)model.GetNextRun(address);
               } else {
                  break; // this is a top-level table, with length depending on a named constant or list
               }
            }
            table = arrayRun;
         }

         var newTable = ExtendTableAndChildren(model, changeToken, table, count);
         foreach (var otherTable in model.TablesWithSameConstantForLength(newTable)) {
            ExtendTableAndChildren(model, changeToken, otherTable, count);
         }

         if (newTable.Start != table.Start && string.IsNullOrEmpty(currentArrayName)) {
            table = newTable;
            return new ErrorInfo($"Stream was moved. Pointers have been updated.", isWarningLevel: true);
         }

         table = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, currentArrayName)) as ITableRun;
         if (table == null) return ErrorInfo.NoError;

         var changedNames = new List<string>();
         for (int i = 0; i < visitedNames.Count; i++) {
            var newAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, visitedNames[i]);
            if (newAddress != visitedAddress[i]) changedNames.Add(visitedNames[i]);
         }
         if (changedNames.Count == 0) return ErrorInfo.NoError;
         if (changedNames.Count == 1) return new ErrorInfo($"{changedNames[0]} was moved. Pointers have been updated.", isWarningLevel: true);
         var all = changedNames.Aggregate((a, b) => a + ", " + b);
         return new ErrorInfo($"Tables {all} were moved. Pointers have been updated.", isWarningLevel: true);
      }

      private static string GetNameFromParent(this IDataModel model, ITableRun table) {
         foreach (var source in table.PointerSources) {
            var parent = model.GetNextRun(source) as ITableRun;
            if (parent == null) continue;
            var offsets = parent.ConvertByteOffsetToArrayOffset(source);
            var segmentName = parent.ElementContent[offsets.SegmentIndex].Name;
            var parentIndex = offsets.ElementIndex;
            var parentName = model.GetAnchorFromAddress(-1, parent.Start);
            if (parentName != null) {
               return $"{parentName}/{parentIndex}/{segmentName}/";
            } else {
               return model.GetNameFromParent(parent) + $"{parentIndex}/{segmentName}/";
            }
         }

         return table.Start.ToAddress();
      }

      private static ITableRun ExtendTableAndChildren(IDataModel model, ModelDelta changeToken, ITableRun array, int count) {
         var additionalLength = Math.Max(0, array.ElementLength * count);
         var newRun = model.RelocateForExpansion(changeToken, array, array.Length + additionalLength);
         newRun = newRun.Append(changeToken, count);
         model.ObserveRunWritten(changeToken, newRun);
         return newRun;
      }

      public static IEnumerable<ITableRun> TablesWithSameConstantForLength(this IDataModel model, ITableRun tableRun) {
         if (tableRun is not ArrayRun arrayRun) yield break;
         var constants = model.GetMatchedWords(arrayRun.LengthFromAnchor);
         if (constants == null || constants.Count == 0) yield break;
         foreach (var array in model.Arrays) {
            if (tableRun == array) continue;
            if (array.LengthFromAnchor != arrayRun.LengthFromAnchor) continue;
            yield return array;
         }
      }

      public static IFormattedRun GetNextAnchor(this IDataModel model, string name) => model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, name));

      /// <summary>
      /// Returns all arrays from the model with a length that depends on the parent array.
      /// Also returns any array with a BitArraySegment that depends on the parent array.
      /// </summary>
      public static IEnumerable<ArrayRun> GetDependantArrays(this IDataModel model, string anchor) {
         foreach (var array in model.Arrays) {
            if (array.LengthFromAnchor == anchor) yield return array;
            foreach (var segment in array.ElementContent) {
               if (segment is ArrayRunBitArraySegment bitSegment) {
                  if (bitSegment.SourceArrayName == anchor) yield return array;
               }
            }
         }
      }

      /// <summary>
      /// Returns a list of runs of the expected type that are children of given index in the table.
      /// </summary>
      public static IEnumerable<T> GetPointedChildren<T>(this IDataModel model, ITableRun table, int elementIndex) where T : IFormattedRun {
         int segmentOffset = 0;
         int elementOffset = table.ElementLength * elementIndex;
         foreach (var segment in table.ElementContent) {
            if (segment is ArrayRunPointerSegment pSegment) {
               var destination = model.ReadPointer(table.Start + elementOffset + segmentOffset);
               if (model.GetNextRun(destination) is T result && result.Start == destination) yield return result;
            }
            segmentOffset += segment.Length;
         }
      }

      /// <summary>
      /// Returns a list of arrays that use the enumName
      /// </summary>
      public static IEnumerable<ArrayRun> GetEnumArrays(this IDataModel model, string enumName) {
         foreach (var array in model.Arrays) {
            foreach (var segment in array.ElementContent) {
               if (!(segment is ArrayRunEnumSegment enumSegment)) continue;
               if (enumSegment.EnumName != enumName) continue;
               yield return array;
               break;
            }
         }
      }

      public static IEnumerable<int> FindPointer(this IDataModel model, int address) {
         var low = (byte)address;
         var mid = (byte)(address >> 8);
         var high = (byte)(address >> 16);
         return model.Find(low, mid, high, 0x08);
      }

      public static IEnumerable<int> Find(this IDataModel model, params byte[] search) {
         for (int i = 0; i < model.Count - search.Length; i++) {
            for (int j = 0; j < search.Length; j++) {
               if (model[i + j] != search[j]) break;
               if (j == search.Length - 1) yield return i;
            }
         }
      }

      /// <summary>
      /// We can search faster if we're looking for thumb code, because we know the code will be 2-byte aligned.
      /// </summary>
      public static IEnumerable<int> ThumbFind(this IDataModel model, byte[] search) {
         for (int i = 0; i < model.Count - search.Length; i += 2) {
            for (int j = 0; j < search.Length; j++) {
               if (model[i + j] != search[j]) break;
               if (j == search.Length - 1) yield return i;
            }
         }
      }

      public static IEnumerable<int> Search(this IDataModel model, IList<ISearchByte> searchBytes) {
         for (int i = 0; i < model.Count - searchBytes.Count; i++) {
            for (int j = 0; j < searchBytes.Count; j++) {
               if (!searchBytes[j].Match(model[i + j])) break;
               if (j == searchBytes.Count - 1) yield return i;
            }
         }
         searchBytes.Clear();
      }

      public static IEnumerable<(int start, int end)> FindListUsages(this IDataModel model, string searchstring) {
         searchstring = searchstring.ToLower();
         foreach (var listName in model.ListNames) {
            if (!model.TryGetList(listName, out var elementNames)) continue;
            for (int i = 0; i < elementNames.Count; i++) {
               if (elementNames[i] == null) continue;
               if (elementNames[i].ToLower() != searchstring) continue;
               foreach (var table in model.Arrays) {
                  foreach (var field in table.ElementContent) {
                     if (!(field is ArrayRunEnumSegment enumSegment)) continue;
                     if (enumSegment.EnumName.ToLower() != listName) continue;
                     var fieldOffset = table.ElementContent.Until(seg => seg == enumSegment).Sum(seg => seg.Length);
                     for (int j = 0; j < table.ElementCount; j++) {
                        var start = table.Start + j * table.ElementLength + fieldOffset;
                        var modelValue = model.ReadMultiByteValue(start, enumSegment.Length);
                        if (modelValue != i) continue;
                        yield return (start, start + enumSegment.Length - 1);
                     }
                  }
               }
            }
         }
      }

      public static IReadOnlyList<string> GetOptions(this IDataModel model, string tableName) => ModelCacheScope.GetCache(model).GetOptions(tableName);

      public static IReadOnlyList<string> GetBitOptions(this IDataModel model, string tableName) => ModelCacheScope.GetCache(model).GetBitOptions(tableName);

      public static IEnumerable<ArrayRun> GetRelatedArrays(this IDataModel model, ArrayRun table) {
         yield return table; // a table is related to itself
         var basename = model.GetAnchorFromAddress(-1, table.Start);
         var nameOptions = new HashSet<string> { basename };
         if (!string.IsNullOrEmpty(table.LengthFromAnchor)) {
            basename = table.LengthFromAnchor;
            nameOptions.Add(basename);
         }
         foreach (var array in model.Arrays) {
            if (array == table) continue;
            var currentArrayName = model.GetAnchorFromAddress(-1, array.Start);
            var options = new List<string> { currentArrayName };
            if (!string.IsNullOrEmpty(array.LengthFromAnchor)) options.Add(array.LengthFromAnchor);
            if (!options.Any(nameOptions.Contains)) continue;
            yield return array;
         }
      }

      public static void InsertPointersToRun(this IDataModel model, ModelDelta token, IFormattedRun run) {
         foreach (var source in run.PointerSources) {
            var existingRun = model.GetNextRun(source);
            if (existingRun.Start <= source) continue;
            model.ObserveRunWritten(token, new PointerRun(source));
         }

         if (run is ArrayRun newArray && newArray.SupportsInnerPointers) {
            for (int i = 0; i < newArray.ElementCount; i++) {
               foreach (var source in newArray.PointerSourcesForInnerElements[i]) {
                  var existingRun = model.GetNextRun(source);
                  if (existingRun.Start <= source) continue;
                  model.ObserveRunWritten(token, new PointerRun(source));
               }
            }
         }
      }

      public static void SetList(this IDataModel model, ModelDelta token, string name, params string[] items) => model.SetList(token, name, (IReadOnlyList<string>)items, null);
   }

   public class BasicModel : BaseModel {

      public BasicModel(byte[] data) : base(data) { }

      public override int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor) => Pointer.NULL;
      public override string GetAnchorFromAddress(int requestSource, int destination) => string.Empty;
      public override IFormattedRun GetNextRun(int dataIndex) => NoInfoRun.NullRun;
      public override IFormattedRun GetNextAnchor(int dataIndex) => NoInfoRun.NullRun;
      public override bool IsAtEndOfArray(int dataIndex, out ITableRun tableRun) { tableRun = null; return false; }
      public override void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run) { }
      public override void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run) { }
      public override void MassUpdateFromDelta(
         IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd,
         IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd,
         IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd,
         IReadOnlyDictionary<int, string> matchedWordsToRemove, IReadOnlyDictionary<int, string> matchedWordsToAdd,
         IReadOnlyDictionary<int, int> offsetPointersToRemove, IReadOnlyDictionary<int, int> offsetPointersToAdd,
         IReadOnlyDictionary<string, int> unmappedConstantsToRemove, IReadOnlyDictionary<string, int> unmappedConstantsToAdd,
         IReadOnlyDictionary<string, ValidationList> listsToRemove, IReadOnlyDictionary<string, ValidationList> listsToAdd) { }
      public override T RelocateForExpansion<T>(ModelDelta changeToken, T run, int minimumLength) => throw new NotImplementedException();
      public override int FindFreeSpace(int start, int length) => throw new NotImplementedException();
      public override void ClearAnchor(ModelDelta changeToken, int start, int length) { }
      public override void ClearFormat(ModelDelta changeToken, int start, int length) { }
      public override void ClearFormatAndData(ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(this, start + i, 0xFF);
      }

      public override void ClearPointer(ModelDelta currentChange, int source, int destination) => throw new NotImplementedException();

      public override SortedSpan<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses) => throw new NotImplementedException();

      public override void UpdateArrayPointer(ModelDelta changeToken, ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, int address, int destination) {
         WritePointer(changeToken, address, destination);
      }

      public override string Copy(Func<ModelDelta> changeToken, int start, int length, bool deep = false) {
         var bytes = Enumerable.Range(start, length).Select(i => RawData[i]);
         return string.Join(" ", bytes.Select(value => value.ToString("X2")));
      }
   }
}
