using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public class LabelLibrary {
      private readonly IDataModel model;
      private readonly IDictionary<string, int> labels;
      private readonly IDictionary<string, List<int>> unresolvedLabels;
      public LabelLibrary(IDataModel data, IDictionary<string, int> additionalLabels) {
         (model, labels) = (data, additionalLabels);
         unresolvedLabels = new Dictionary<string, List<int>>();
      }

      public int ResolveLabel(string label) {
         var offset = 0;
         if (label.Split("+") is string[] parts && parts.Length == 2) {
            label = parts[0];
            int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
         }
         if (labels.TryGetValue(label, out int result)) return result + offset;
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

   public record DecompileLabelLibrary(int Start, int Length) {
      private readonly Dictionary<int, string> labels = new();
      public string AddressToLabel(int address) {
         if (labels.TryGetValue(address, out var label)) return label;
         if (address.InRange(Start, Start + Length)) {
            label = "section" + labels.Count;
            labels[address] = label;
            return label;
         }
         return address.ToAddress();
      }
   }
}
