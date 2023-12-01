using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public class LabelLibrary {
      private readonly IDataModel model;
      private readonly IDictionary<string, int> labels;
      private readonly IDictionary<string, List<int>> unresolvedLabels;
      public bool RequireCompleteAddresses { get; init; } = true;
      public ITableRun Table(string table) => model.GetTable(table);
      public LabelLibrary(IDataModel data, IDictionary<string, int> additionalLabels) {
         (model, labels) = (data, additionalLabels);
         unresolvedLabels = new Dictionary<string, List<int>>();
      }

      public int ResolveLabel(string label) {
         var offset = 0;
         if (label == "null") return Pointer.NULL;
         if (label.Split("+") is string[] parts && parts.Length == 2) {
            label = parts[0];
            int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
         }
         if (labels != null && labels.TryGetValue(label, out int result)) return result + offset;
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, label);
         if (address == Pointer.NULL) return address;
         return address + offset;
      }

      public bool TryResolveLabel(string label, out int address) {
         address = ResolveLabel(label);
         return address >= 0;
      }

      public bool TryResolveValue(string title, out int value) {
         return model.TryGetUnmappedConstant(title, out value);
      }

      public void AddUnresolvedLabel(string label, int source) {
         if (!unresolvedLabels.TryGetValue(label, out var list)) {
            list = new();
            unresolvedLabels.Add(label, list);
         }
         if (!list.Contains(source)) list.Add(source);
      }

      public void ResolveUnresolvedLabels(int scriptStart, List<byte> script, byte endCommand) {
         foreach (var label in unresolvedLabels.Keys) {
            var destination = scriptStart + script.Count - Pointer.NULL;
            foreach (var address in unresolvedLabels[label]) {
               script[address - scriptStart + 0] = (byte)(destination >> 0);
               script[address - scriptStart + 1] = (byte)(destination >> 8);
               script[address - scriptStart + 2] = (byte)(destination >> 16);
               script[address - scriptStart + 3] = (byte)(destination >> 24);
            }
            labels[label] = scriptStart + script.Count;
            script.Add(endCommand);
         }
         unresolvedLabels.Clear();
         if (labels.Values.Any(address => address == scriptStart + script.Count)) script.Add(endCommand);
      }
   }

   public record DecompileLabelLibrary(IDataModel Model, int Start, int Length) {
      private const string SENTINEL = ".sentinel.";
      private readonly Dictionary<int, string> labels = new();
      private readonly HashSet<int> rawLabels = new();

      /// <param name="isScriptAddress">
      /// If this is false, the address is not the start of a script, but some other kind of data.
      /// Only turn script addresses into section headers.
      /// </param>
      public string AddressToLabel(int address, bool isScriptAddress) {
         if (address == Pointer.NULL) return "null";
         if (address < 0) address -= Pointer.NULL;
         if (labels.TryGetValue(address, out var label)) return label;
         if (isScriptAddress && Model.GetAnchorFromAddress(-1, address) is string anchor && anchor.Length > 4) {
            labels[address] = anchor;
            return anchor;
         } else if (isScriptAddress && address.InRange(Start, Start + Length)) {
            label = SENTINEL + labels.Count;
            labels[address] = label;
            return label;
         }
         rawLabels.Add(address);
         return address.ToAddress();
      }

      public IReadOnlyDictionary<string, string> FinalizeLabels(ref int existingSectionCount) {
         var usedLabels = new HashSet<int>();
         var matches = labels.Keys.Where(key => labels[key].StartsWith(SENTINEL)).ToList();
         matches.Sort();
         var results = new Dictionary<string, string>();

         for (int i = 0; i < matches.Count; i++) {
            results[labels[matches[i]]] = "section" + (existingSectionCount + i);
         }

         existingSectionCount += matches.Count;
         return results;
      }

      public string FinalizeLine(IReadOnlyDictionary<string, string> sections, string line) {
         var start = line.IndexOf(SENTINEL);
         if (start == -1) return line;
         var index = int.Parse(new string(line[(start + SENTINEL.Length)..].TakeWhile(char.IsDigit).ToArray()));
         line = line.Replace(SENTINEL + index, sections[SENTINEL + index]);
         return FinalizeLine(sections, line);
      }

      public IEnumerable<int> AutoLabels => rawLabels.Where(key => key.InRange(Start, Start + Length));
   }
}
