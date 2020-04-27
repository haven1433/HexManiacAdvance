using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public interface IDataModel : IReadOnlyList<byte>, IEquatable<IDataModel> {
      byte[] RawData { get; }
      bool HasChanged(int index);
      void ResetChanges();

      new byte this[int index] { get; set; }
      IReadOnlyList<ArrayRun> Arrays { get; }
      IReadOnlyList<IStreamRun> Streams { get; }
      IReadOnlyList<string> Anchors { get; }

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
      bool TryGetList(string name, out IReadOnlyList<string> nameArray);

      bool IsAtEndOfArray(int dataIndex, out ITableRun tableRun); // is this byte the first one after the end of a table run? (also return true if the table is length 0 and starts right here)

      void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run);
      void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run);
      void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd, IReadOnlyDictionary<int, string> matchedWordsToRemove, IReadOnlyDictionary<int, string> matchedWordsToAdd);
      IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength);
      int FindFreeSpace(int start, int length);
      void ClearAnchor(ModelDelta changeToken, int start, int length);
      void ClearFormat(ModelDelta changeToken, int start, int length);
      void ClearFormatAndData(ModelDelta changeToken, int start, int length);
      void ClearPointer(ModelDelta currentChange, int source, int destination);
      string Copy(Func<ModelDelta> changeToken, int start, int length);

      void Load(byte[] newData, StoredMetadata metadata);
      void ExpandData(ModelDelta changeToken, int minimumLength);

      IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses);
      void WritePointer(ModelDelta changeToken, int address, int pointerDestination);
      void WriteValue(ModelDelta changeToken, int address, int value);
      int ReadPointer(int address);
      int ReadValue(int address);

      int[] GetUnmappedSourcesToAnchor(string anchor);
      int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor);
      string GetAnchorFromAddress(int requestSource, int destination);
      IReadOnlyList<string> GetAutoCompleteAnchorNameOptions(string partial);
      StoredMetadata ExportMetadata(IMetadataInfo metadataInfo);
      void UpdateArrayPointer(ModelDelta changeToken, ArrayRunElementSegment segment, int address, int destination);
      int ConsiderResultsAsTextRuns(ModelDelta changeToken, IReadOnlyList<int> startLocations);
   }

   public abstract class BaseModel : IDataModel {
      public const int PointerOffset = 0x08000000;

      private readonly ISet<int> changes = new HashSet<int>();

      public byte[] RawData { get; private set; }

      public BaseModel(byte[] data) => RawData = data;

      public virtual IReadOnlyList<ArrayRun> Arrays { get; } = new List<ArrayRun>();
      public virtual IReadOnlyList<IStreamRun> Streams { get; } = new List<IStreamRun>();
      public virtual IReadOnlyList<string> Anchors { get; } = new List<string>();

      public byte this[int index] {
         get => RawData[index];
         set {
            RawData[index] = value;
            changes.Add(index);
         }
      }

      public bool HasChanged(int index) => changes.Contains(index);
      public void ResetChanges() => changes.Clear();

      byte IReadOnlyList<byte>.this[int index] => RawData[index];

      public int Count => RawData.Length;

      public abstract void ClearAnchor(ModelDelta changeToken, int start, int length);

      public abstract void ClearFormat(ModelDelta changeToken, int start, int length);

      public abstract void ClearFormatAndData(ModelDelta changeToken, int originalStart, int length);

      public abstract string Copy(Func<ModelDelta> changeToken, int start, int length);

      public void ExpandData(ModelDelta changeToken, int minimumIndex) {
         if (Count > minimumIndex) return;

         var newData = new byte[minimumIndex + 1];
         Array.Copy(RawData, newData, RawData.Length);
         RawData = newData;
      }

      public virtual int[] GetUnmappedSourcesToAnchor(string anchor) => new int[0];

      public abstract int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor);

      public abstract string GetAnchorFromAddress(int requestSource, int destination);

      public IEnumerator<byte> GetEnumerator() => ((IList<byte>)RawData).GetEnumerator();

      public abstract IFormattedRun GetNextRun(int dataIndex);

      public abstract IFormattedRun GetNextAnchor(int dataIndex);

      public virtual bool TryGetUsefulHeader(int address, out string header) { header = null; return false; }

      public virtual bool TryGetList(string name, out IReadOnlyList<string> list) { list = null; return false; }

      public abstract bool IsAtEndOfArray(int dataIndex, out ITableRun tableRun);

      public virtual void Load(byte[] newData, StoredMetadata metadata) => RawData = newData;

      public abstract void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run);

      public abstract void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run);

      public abstract void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd, IReadOnlyDictionary<int, string> matchedWordsToRemove, IReadOnlyDictionary<int, string> matchedWordsToAdd);

      public abstract IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength);

      public abstract int FindFreeSpace(int start, int length);

      public abstract IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses);

      public abstract void UpdateArrayPointer(ModelDelta currentChange, ArrayRunElementSegment segment, int index, int fullValue);

      public abstract void ClearPointer(ModelDelta currentChange, int source, int destination);

      public int ReadValue(int index) => BitConverter.ToInt32(RawData, index);

      public void WriteValue(ModelDelta changeToken, int index, int word) {
         changeToken.ChangeData(this, index + 0, (byte)(word >> 0));
         changeToken.ChangeData(this, index + 1, (byte)(word >> 8));
         changeToken.ChangeData(this, index + 2, (byte)(word >> 16));
         changeToken.ChangeData(this, index + 3, (byte)(word >> 24));
      }

      public int ReadPointer(int index) => ReadValue(index) - PointerOffset;

      public void WritePointer(ModelDelta changeToken, int address, int pointerDestination) => WriteValue(changeToken, address, pointerDestination + PointerOffset);

      /// <summary>
      /// Returns the number of new runs found.
      /// </summary>
      public virtual int ConsiderResultsAsTextRuns(ModelDelta changeToken, IReadOnlyList<int> startLocations) => 0;

      public virtual IReadOnlyList<string> GetAutoCompleteAnchorNameOptions(string partial) => new string[0];

      public virtual StoredMetadata ExportMetadata(IMetadataInfo metadataInfo) => null;

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public bool Equals(IDataModel other) => other == this;
   }

   public static class IDataModelExtensions {
      public static string GetGameCode(this IDataModel model) {
         var code = new string(Enumerable.Range(0xAC, 4).Select(i => (char)model[i]).ToArray());
         code += model[0xBC]; // should be "0" or "1"
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

      public static void WriteMultiByteValue(this IDataModel model, int index, int length, ModelDelta changeToken, int value) {
         for (int i = 0; i < length; i++) {
            changeToken.ChangeData(model, index + i, (byte)value);
            value >>= 8;
         }
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
            if (i + offset >= rawOptions.Count) return false;
            result[index] = rawOptions[i + offset];
            if (result[i] == null) result[i] = "?unused?";
         }

         names = result;
         return true;
      }

      public static List<int> FindPossibleTextStartingPlaces(this IDataModel model, int left, int length) {
         // part 1: find a previous FF, which is possibly the end of another text
         var startPlaces = new List<int>();
         if (left < 0 || left >= model.Count) return startPlaces;
         while (model[left] != 0xFF && PCSString.PCS[model[left]] != null) { left--; length++; }
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
            if (!(run is NoInfoRun)) break;
            while (model[left] != 0xFF) { left++; length--; }
            left++; length--;
         }

         // remove duplicates and make sure everything is in order
         startPlaces.Sort();
         startPlaces = startPlaces.Distinct().ToList();
         return startPlaces;
      }

      public static IReadOnlyList<AutoCompleteSelectionItem> GetNewPointerAutocompleteOptions(this IDataModel model, string text, int selectedIndex) {
         var options = model.GetAutoCompleteAnchorNameOptions(text.Substring(1));
         if (text.StartsWith(PointerRun.PointerStart.ToString())) options = options.Select(option => $"{PointerRun.PointerStart}{option}{PointerRun.PointerEnd}").ToList();
         if (text.StartsWith(ViewPort.GotoMarker.ToString())) options = options.Select(option => $"{ViewPort.GotoMarker}{option} ").ToList();
         return AutoCompleteSelectionItem.Generate(options, selectedIndex);
      }

      public static IReadOnlyList<AutoCompleteSelectionItem> GetNewWordAutocompleteOptions(this IDataModel model, string text, int selectedIndex) {
         if (text.Length >= 2) text = text.Substring(2);
         else return null;
         var options = model.GetAutoCompleteAnchorNameOptions(text);
         options = options.Select(option => $"::{option} ").ToList();
         return AutoCompleteSelectionItem.Generate(options, selectedIndex);
      }

      // wraps an IDataFormat in an anchor format, if it makes sense
      public static IDataFormat WrapFormat(this IDataModel model, IFormattedRun run, IDataFormat format, int dataIndex) {
         if (run.PointerSources != null && run.Start == dataIndex) {
            var name = model.GetAnchorFromAddress(-1, run.Start);
            return new Anchor(format, name, run.FormatString, run.PointerSources);
         }

         if (run is ArrayRun array && array.SupportsPointersToElements && (dataIndex - run.Start) % array.ElementLength == 0) {
            var arrayIndex = (dataIndex - run.Start) / array.ElementLength;
            var pointerSources = array.PointerSourcesForInnerElements[arrayIndex];
            if (pointerSources == null || pointerSources.Count == 0) return format;
            var name = model.GetAnchorFromAddress(-1, dataIndex);
            return new Anchor(format, name, string.Empty, pointerSources);
         }

         return format;
      }

      public static ErrorInfo CompleteArrayExtension(this IDataModel model, ModelDelta changeToken, ref ITableRun table) {
         var currentArrayName = model.GetAnchorFromAddress(-1, table.Start);

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

               var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, arrayRun.LengthFromAnchor);
               visitedNames.Add(arrayRun.LengthFromAnchor);
               visitedAddress.Add(address);
               arrayRun = (ArrayRun)model.GetNextRun(address);
            }
            table = arrayRun;
         }

         var newTable = ExtendTableAndChildren(model, changeToken, table);

         if (newTable.Start != table.Start && string.IsNullOrEmpty(currentArrayName)) {
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

         Debug.Fail("Could not find parent of nameless table. A table may have been moved, but we don't know which one.");
         return string.Empty;
      }

      private static ITableRun ExtendTableAndChildren(IDataModel model, ModelDelta changeToken, ITableRun array) {
         var newRun = (ITableRun)model.RelocateForExpansion(changeToken, array, array.Length + array.ElementLength);
         newRun = newRun.Append(changeToken, 1);
         model.ObserveRunWritten(changeToken, newRun);
         return newRun;
      }

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

      public static IReadOnlyList<string> GetOptions(this IDataModel model, string tableName) => ModelCacheScope.GetCache(model).GetOptions(tableName);

      public static IReadOnlyList<string> GetBitOptions(this IDataModel model, string tableName) => ModelCacheScope.GetCache(model).GetBitOptions(tableName);

      public static IEnumerable<ArrayRun> GetRelatedArrays(this IDataModel model, ArrayRun table) {
         var basename = model.GetAnchorFromAddress(-1, table.Start);
         if (!string.IsNullOrEmpty(table.LengthFromAnchor)) basename = table.LengthFromAnchor;
         foreach (var array in model.Arrays) {
            if (array == table) continue;
            var currentArrayName = model.GetAnchorFromAddress(-1, array.Start);
            if (!basename.IsAny(array.LengthFromAnchor, currentArrayName)) continue;
            yield return array;
         }
      }
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
      public override void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd, IReadOnlyDictionary<int, string> matchedWordsToRemove, IReadOnlyDictionary<int, string> matchedWordsToAdd) { }
      public override IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength) => throw new NotImplementedException();
      public override int FindFreeSpace(int start, int length) => throw new NotImplementedException();
      public override void ClearAnchor(ModelDelta changeToken, int start, int length) { }
      public override void ClearFormat(ModelDelta changeToken, int start, int length) { }
      public override void ClearFormatAndData(ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(this, start + i, 0xFF);
      }

      public override void ClearPointer(ModelDelta currentChange, int source, int destination) => throw new NotImplementedException();

      public override IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses) => throw new NotImplementedException();

      public override void UpdateArrayPointer(ModelDelta changeToken, ArrayRunElementSegment segment, int address, int destination) {
         WritePointer(changeToken, address, destination);
      }

      public override string Copy(Func<ModelDelta> changeToken, int start, int length) {
         var bytes = Enumerable.Range(start, length).Select(i => RawData[i]);
         return string.Join(" ", bytes.Select(value => value.ToString("X2")));
      }
   }
}
