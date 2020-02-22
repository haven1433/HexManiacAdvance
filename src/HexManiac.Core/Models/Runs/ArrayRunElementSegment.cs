using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public enum ElementContentType {
      Unknown,
      PCS,
      Integer,
      Pointer,
      BitArray,
   }

   public class ArrayRunElementSegment {
      public string Name { get; }
      public ElementContentType Type { get; }
      public int Length { get; }
      public ArrayRunElementSegment(string name, ElementContentType type, int length) => (Name, Type, Length) = (name, type, length);

      public virtual string ToText(IDataModel rawData, int offset) {
         switch (Type) {
            case ElementContentType.PCS:
               return PCSString.Convert(rawData, offset, Length);
            case ElementContentType.Integer:
               return ToInteger(rawData, offset, Length).ToString();
            case ElementContentType.Pointer:
               var address = rawData.ReadPointer(offset);
               var anchor = rawData.GetAnchorFromAddress(-1, address);
               if (string.IsNullOrEmpty(anchor)) anchor = address.ToString("X6");
               return $"{PointerRun.PointerStart}{anchor}{PointerRun.PointerEnd}";
            default:
               throw new NotImplementedException();
         }
      }

      public virtual void Write(IDataModel model, ModelDelta token, int start, string data) {
         switch (Type) {
            case ElementContentType.PCS:
               var bytes = PCSString.Convert(data);
               while (bytes.Count > Length) bytes.RemoveAt(bytes.Count - 1);
               if (!bytes.Contains(0xFF)) bytes[bytes.Count - 1] = 0xFF;
               while (bytes.Count < Length) bytes.Add(0);
               for (int i = 0; i < Length; i++) token.ChangeData(model, start + i, bytes[i]);
               break;
            case ElementContentType.Integer:
               if (!int.TryParse(data, out var intValue)) intValue = 0;
               if (model.ReadMultiByteValue(start, Length) != intValue) {
                  model.WriteMultiByteValue(start, Length, token, intValue);
               }
               break;
            case ElementContentType.Pointer:
               if (data.StartsWith("<")) data = data.Substring(1);
               if (data.EndsWith(">")) data = data.Substring(0, data.Length - 1);
               if (!int.TryParse(data, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var address)) {
                  address = model.GetAddressFromAnchor(token, -1, data);
               }
               if (model.ReadPointer(start) != address) {
                  model.WritePointer(token, start, address);
               }
               break;
            default:
               throw new NotImplementedException();
         }
      }

      public static int ToInteger(IReadOnlyList<byte> data, int offset, int length) {
         int result = 0;
         int shift = 0;
         for (int i = 0; i < length; i++) {
            result += data[offset + i] << shift;
            shift += 8;
         }
         return result;
      }
   }

   public class ArrayRunEnumSegment : ArrayRunElementSegment {
      public string EnumName { get; }

      public ArrayRunEnumSegment(string name, int length, string enumName) : base(name, ElementContentType.Integer, length) => EnumName = enumName;

      public override string ToText(IDataModel model, int offset) {
         var noChange = new NoDataChangeDeltaModel();
         using (ModelCacheScope.CreateScope(model)) {
            var options = GetOptions(model);
            if (options == null) return base.ToText(model, offset);

            var resultAsInteger = ToInteger(model, offset, Length);
            if (resultAsInteger >= options.Count || resultAsInteger < 0) return base.ToText(model, offset);
            var value = options[resultAsInteger];

            // use ~2 postfix for a value if an earlier entry in the array has the same string
            var elementsUpToHereWithThisName = 1;
            for (int i = resultAsInteger - 1; i >= 0; i--) {
               var previousValue = options[i];
               if (previousValue == value) elementsUpToHereWithThisName++;
            }
            if (value.StartsWith("\"") && value.EndsWith("\"")) value = value.Substring(1, value.Length - 2);
            if (elementsUpToHereWithThisName > 1) value += "~" + elementsUpToHereWithThisName;

            // add quotes around it if it contains a space
            if (value.Contains(' ')) value = $"\"{value}\"";

            return value;
         }
      }

      public override void Write(IDataModel model, ModelDelta token, int start, string data) {
         using (ModelCacheScope.CreateScope(model)) {
            if (!TryParse(model, data, out int value)) {
               base.Write(model, token, start, data);
               return;
            }

            base.Write(model, token, start, value.ToString());
         }
      }

      public bool TryParse(IDataModel model, string text, out int value) {
         text = text.Trim();
         if (int.TryParse(text, out value)) return true;
         if (text.StartsWith("\"") && text.EndsWith("\"")) text = text.Substring(1, text.Length - 2);
         var partialMatches = new List<string>();
         var matches = new List<string>();

         // if the ~ character is used, expect that it's saying which match we want
         var desiredMatch = 1;
         var splitIndex = text.IndexOf('~');
         if (splitIndex != -1 && !int.TryParse(text.Substring(splitIndex + 1), out desiredMatch)) return false;
         if (splitIndex != -1) text = text.Substring(0, splitIndex);

         // ok, so everything lines up... check the array to see if any values match the string entered
         text = text.ToLower();
         var options = model.GetOptions(EnumName);
         for (int i = 0; i < options.Count; i++) {
            var option = options[i].ToLower();
            if (option == text) matches.Add(option);
            if (matches.Count == desiredMatch) { value = i; return true; }
            if (option.MatchesPartial(text)) partialMatches.Add(option);
            if (partialMatches.Count == desiredMatch && matches.Count == 0) value = i;
         }
         if (matches.Count == 0 && partialMatches.Count >= desiredMatch) return true; // no full matches, use the partial match

         // we went through the whole array and didn't find it :(
         return false;
      }

      public IReadOnlyList<string> GetOptions(IDataModel model) {
         if (int.TryParse(EnumName, out var result)) return Enumerable.Range(0, result).Select(i => i.ToString()).ToList();
         return model.GetOptions(EnumName);
      }
   }

   public class ArrayRunBitArraySegment : ArrayRunElementSegment {
      public string SourceArrayName { get; }

      public ArrayRunBitArraySegment(string name, int length, string bitSourceName) : base(name, ElementContentType.BitArray, length) {
         SourceArrayName = bitSourceName;
      }

      public override string ToText(IDataModel rawData, int offset) {
         var result = new StringBuilder(Length * 2);
         for (int i = 0; i < Length; i++) {
            result.Append(rawData[offset + i].ToString("X2"));
         }
         return result.ToString();
      }

      public override void Write(IDataModel model, ModelDelta token, int start, string data) {
         for (int i = 0; i < Length && i * 2 + 1 < data.Length; i++) {
            if (!byte.TryParse(data.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var value)) value = 0;
            token.ChangeData(model, start + i, value);
         }
      }

      public IReadOnlyList<string> GetOptions(IDataModel model) {
         var cache = ModelCacheScope.GetCache(model);
         return cache.GetBitOptions(SourceArrayName);
      }
   }

   /// <summary>
   /// For pointers that contain nested formatting instructions.
   /// For example, pointing to a text stream or a plm (pokemon learnable moves) stream
   /// </summary>
   public class ArrayRunPointerSegment : ArrayRunElementSegment {
      public string InnerFormat { get; }

      public bool IsInnerFormatValid {
         get {
            if (InnerFormat == PCSRun.SharedFormatString) return true;
            if (InnerFormat == PLMRun.SharedFormatString) return true;
            if (InnerFormat == TrainerPokemonTeamRun.SharedFormatString) return true;
            if (InnerFormat.StartsWith("[") && InnerFormat.Contains("]")) return true;
            return false;
         }
      }

      public ArrayRunPointerSegment(string name, string innerFormat) : base(name, ElementContentType.Pointer, 4) {
         InnerFormat = innerFormat;
      }

      public bool DestinationDataMatchesPointerFormat(IDataModel owner, ModelDelta token, int source, int destination, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (destination == Pointer.NULL) return true;
         var run = owner.GetNextAnchor(destination);
         if (run.Start < destination) return false;
         if (run.Start > destination || (run.Start == destination && (run is NoInfoRun || run is PointerRun))) {
            // hard case: no format found, so check the data
            if (InnerFormat == PCSRun.SharedFormatString) {
               var length = PCSString.ReadString(owner, destination, true);

               if (length > 0) {
                  // our token will be a no-change token if we're in the middle of exploring the data.
                  // If so, don't actually add the run. It's enough to know that we _can_ add the run.
                  if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, new PCSRun(owner, destination, length));
                  return true;
               }
            } else if (InnerFormat == PLMRun.SharedFormatString) {
               var plmRun = new PLMRun(owner, destination);
               var length = plmRun.Length;
               if (length >= 2) {
                  if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, plmRun);
                  return true;
               }
            } else if (InnerFormat == TrainerPokemonTeamRun.SharedFormatString) {
               var teamRun = new TrainerPokemonTeamRun(owner, destination, new[] { source });
               var length = teamRun.Length;
               if (length >= 2) {
                  if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, teamRun);
                  return true;
               }
            } else if (InnerFormat.StartsWith("[")) {
               if (TableStreamRun.TryParseTableStream(owner, destination, new[] { source }, Name, InnerFormat, sourceSegments, out var tsRun)) {
                  if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, tsRun);
                  return true;
               }
            }
         } else {
            // easy case: already have a useful format, just see if it matches
            if (InnerFormat == PCSRun.SharedFormatString) return run is PCSRun;
            if (InnerFormat == PLMRun.SharedFormatString) return run is PLMRun;
            if (InnerFormat == TrainerPokemonTeamRun.SharedFormatString) return run is TrainerPokemonTeamRun;
            if (InnerFormat.StartsWith("[")) return run is TableStreamRun tsRun && tsRun.FormatString == InnerFormat;
         }
         return false;
      }

      public void WriteNewFormat(IDataModel owner, ModelDelta token, int source, int destination, int length, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         owner.WritePointer(token, source, destination);
         IFormattedRun run;
         if (InnerFormat == PCSRun.SharedFormatString) {
            // found freespace, so this should already be an FF. Just add the format.
            run = new PCSRun(owner, destination, length);
         } else if (InnerFormat == PLMRun.SharedFormatString) {
            // PLM ends with FFFF, and this is already freespace, so just add the format.
            run = new PLMRun(owner, destination);
         } else if (InnerFormat == TrainerPokemonTeamRun.SharedFormatString) {
            run = new TrainerPokemonTeamRun(owner, destination, new[] { source }).DeserializeRun("0 ???", token);
         } else if (InnerFormat.StartsWith("[") && InnerFormat.Contains("]")) {
            // don't bother checking the TryParse result: we very much expect that the data originally in the run won't fit the parse.
            TableStreamRun.TryParseTableStream(owner, destination, new[] { source }, Name, InnerFormat, sourceSegments, out var tableStream);
            run = tableStream.DeserializeRun("", token);
         } else {
            Debug.Fail("Not Implemented!");
            return;
         }

         owner.ObserveRunWritten(token, run.MergeAnchor(new[] { source }));
      }
   }
}
