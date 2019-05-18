using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public interface IDataModel : IReadOnlyList<byte> {
      byte[] RawData { get; }
      new byte this[int index] { get; set; }
      IReadOnlyList<ArrayRun> Arrays { get; }
      IReadOnlyList<IFormattedRun> Streams { get; }

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

      bool IsAtEndOfArray(int dataIndex, out ArrayRun arrayRun); // is this byte the first one after the end of an array run? (also return true if the array is length 0 and starts right here)

      void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run);
      void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run);
      void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd);
      IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength);
      void ClearFormat(ModelDelta changeToken, int start, int length);
      void ClearFormatAndData(ModelDelta changeToken, int start, int length);
      string Copy(Func<ModelDelta> changeToken, int start, int length);

      void Load(byte[] newData, StoredMetadata metadata);
      void ExpandData(ModelDelta changeToken, int minimumLength);

      IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses);
      void WritePointer(ModelDelta changeToken, int address, int pointerDestination);
      void WriteValue(ModelDelta changeToken, int address, int value);
      int ReadPointer(int address);
      int ReadValue(int address);

      int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor);
      string GetAnchorFromAddress(int requestSource, int destination);
      IReadOnlyList<string> GetAutoCompleteAnchorNameOptions(string partial);
      StoredMetadata ExportMetadata();
      void UpdateArrayPointer(ModelDelta changeToken, int address, int destination);
      int ConsiderResultsAsTextRuns(ModelDelta changeToken, IReadOnlyList<int> startLocations);
   }

   public abstract class BaseModel : IDataModel {
      public const int PointerOffset = 0x08000000;

      public byte[] RawData { get; private set; }

      public BaseModel(byte[] data) => RawData = data;

      public virtual IReadOnlyList<ArrayRun> Arrays { get; } = new List<ArrayRun>();
      public virtual IReadOnlyList<IFormattedRun> Streams { get; } = new List<IFormattedRun>();

      public byte this[int index] { get => RawData[index]; set => RawData[index] = value; }

      byte IReadOnlyList<byte>.this[int index] => RawData[index];

      public int Count => RawData.Length;

      public abstract void ClearFormat(ModelDelta changeToken, int start, int length);

      public abstract void ClearFormatAndData(ModelDelta changeToken, int originalStart, int length);

      public abstract string Copy(Func<ModelDelta> changeToken, int start, int length);

      public void ExpandData(ModelDelta changeToken, int minimumIndex) {
         if (Count > minimumIndex) return;

         var newData = new byte[minimumIndex + 1];
         Array.Copy(RawData, newData, RawData.Length);
         RawData = newData;
      }

      public abstract int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor);

      public abstract string GetAnchorFromAddress(int requestSource, int destination);

      public IEnumerator<byte> GetEnumerator() => ((IList<byte>)RawData).GetEnumerator();

      public abstract IFormattedRun GetNextRun(int dataIndex);

      public abstract IFormattedRun GetNextAnchor(int dataIndex);

      public virtual bool TryGetUsefulHeader(int address, out string header) { header = null; return false; }

      public abstract bool IsAtEndOfArray(int dataIndex, out ArrayRun arrayRun);

      public virtual void Load(byte[] newData, StoredMetadata metadata) => RawData = newData;

      public abstract void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run);

      public abstract void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run);

      public abstract void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd);

      public abstract IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength);

      public abstract IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses);

      public abstract void UpdateArrayPointer(ModelDelta currentChange, int index, int fullValue);

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

      public virtual StoredMetadata ExportMetadata() => null;

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }

   public static class IDataModelExtensions {
      public static int ReadMultiByteValue(this IDataModel model, int index, int length) {
         int word = 0;
         while (length > 0) {
            word <<= 8;
            word += model.RawData[index + length - 1];
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

         // the array must start with a text element
         if (array.ElementContent.Count == 0) return false;
         var firstContent = array.ElementContent[0];
         if (firstContent.Type != ElementContentType.PCS) return false;

         return true;
      }

      public static List<int> FindPossibleTextStartingPlaces(this IDataModel model, int left, int length) {
         // part 1: find a previous FF, which is possibly the end of another text
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
         var startPlaces = new List<int>();
         while (length > 0) {
            startPlaces.Add(left);
            var run = model.GetNextRun(left);
            if (run is NoInfoRun) startPlaces.Add(run.Start);
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

      public static ErrorInfo CompleteArrayExtension(this IDataModel model, ModelDelta changeToken, ref ArrayRun arrayRun) {
         var currentArrayName = model.GetAnchorFromAddress(-1, arrayRun.Start);

         var visitedNames = new List<string>() { model.GetAnchorFromAddress(-1, arrayRun.Start) };
         var visitedAddress = new List<int>() { arrayRun.Start };

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

         ExtendArrayAndChildren(model, changeToken, arrayRun);

         arrayRun = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, currentArrayName));

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

      private static void ExtendArrayAndChildren(IDataModel model, ModelDelta changeToken, ArrayRun array) {
         var newRun = (ArrayRun)model.RelocateForExpansion(changeToken, array, array.Length + array.ElementLength);
         newRun = newRun.Append(1);
         model.ObserveRunWritten(changeToken, newRun);
      }

      /// <summary>
      /// Returns all arrays from the model with a length that depends on the parent array.
      /// </summary>
      public static IEnumerable<ArrayRun> GetDependantArrays(this IDataModel model, ArrayRun parent) {
         var anchor = model.GetAnchorFromAddress(-1, parent.Start);
         foreach (var array in model.Arrays) {
            if (array.LengthFromAnchor == anchor) yield return array;
         }
      }
   }

   public class BasicModel : BaseModel {

      public BasicModel(byte[] data) : base(data) { }

      public override int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor) => Pointer.NULL;
      public override string GetAnchorFromAddress(int requestSource, int destination) => string.Empty;
      public override IFormattedRun GetNextRun(int dataIndex) => NoInfoRun.NullRun;
      public override IFormattedRun GetNextAnchor(int dataIndex) => NoInfoRun.NullRun;
      public override bool IsAtEndOfArray(int dataIndex, out ArrayRun arrayRun) { arrayRun = null; return false; }
      public override void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run) { }
      public override void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run) { }
      public override void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd) { }
      public override IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength) => throw new NotImplementedException();
      public override void ClearFormat(ModelDelta changeToken, int start, int length) { }
      public override void ClearFormatAndData(ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(this, start + i, 0xFF);
      }

      public override IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses) => throw new NotImplementedException();

      public override void UpdateArrayPointer(ModelDelta changeToken, int address, int destination) {
         WritePointer(changeToken, address, destination);
      }

      public override string Copy(Func<ModelDelta> changeToken, int start, int length) {
         var bytes = Enumerable.Range(start, length).Select(i => RawData[i]);
         return string.Join(" ", bytes.Select(value => value.ToString("X2")));
      }
   }
}
