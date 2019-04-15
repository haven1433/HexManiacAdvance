using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public enum ElementContentType {
      Unknown,
      PCS,
      Integer,
      Pointer,
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
               return $"<{anchor}>";
            default:
               throw new NotImplementedException();
         }
      }

      public static int ToInteger(IReadOnlyList<byte> data, int offset, int length) {
         int result = 0;
         int multiplier = 1;
         for (int i = 0; i < length; i++) {
            result += data[offset + i] * multiplier;
            multiplier *= 0x100;
         }
         return result;
      }
   }

   public class ArrayRunEnumSegment : ArrayRunElementSegment {
      public string EnumName { get; }

      public ArrayRunEnumSegment(string name, int length, string enumName) : base(name, ElementContentType.Integer, length) => EnumName = enumName;

      public override string ToText(IDataModel model, int offset) {
         var noChange = new NoDataChangeDeltaModel();
         var options = GetOptions(model);
         if (options == null) return base.ToText(model, offset);

         var resultAsInteger = ToInteger(model, offset, Length);
         if (resultAsInteger >= options.Count) return base.ToText(model, offset);
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

      public bool TryParse(IDataModel model, string text, out int value) {
         text = text.Trim();
         if (int.TryParse(text, out value)) return true;
         if (text.StartsWith("\"") && text.EndsWith("\"")) text = text.Substring(1, text.Length - 2);
         var partialMatches = new List<string>();
         var matches = new List<string>();
         if (!model.TryGetNameArray(EnumName, out var enumArray)) return false;

         // if the ~ character is used, expect that it's saying which match we want
         var desiredMatch = 1;
         var splitIndex = text.IndexOf('~');
         if (splitIndex != -1 && !int.TryParse(text.Substring(splitIndex + 1), out desiredMatch)) return false;
         if (splitIndex != -1) text = text.Substring(0, splitIndex);

         // ok, so everything lines up... check the array to see if any values match the string entered
         text = text.ToLower();
         for (int i = 0; i < enumArray.ElementCount; i++) {
            var option = PCSString.Convert(model, enumArray.Start + enumArray.ElementLength * i, enumArray.ElementContent[0].Length).ToLower();
            option = option.Substring(1, option.Length - 2);
            // check for exact matches
            if (option == text) {
               matches.Add(option);
               if (matches.Count == desiredMatch) {
                  value = i;
                  return true;
               }
            }
            // check for start-of-string matches (for autocomplete)
            if (option.StartsWith(text)) {
               partialMatches.Add(option);
               if (partialMatches.Count == desiredMatch && matches.Count == 0) {
                  value = i;
                  return true;
               }
            }
         }

         // we went through the whole array and didn't find it :(
         return false;
      }

      private IReadOnlyList<string> cachedOptions;
      public IReadOnlyList<string> GetOptions(IDataModel model) {
         if (cachedOptions != null) return cachedOptions;

         if (!model.TryGetNameArray(EnumName, out var enumArray)) return null;

         // array must be at least as long as than the current value
         var optionCount = enumArray.ElementCount;

         // sweet, we can convert from the integer value to the enum value
         var results = new List<string>();
         for (int i = 0; i < optionCount; i++) {
            var elementStart = enumArray.Start + enumArray.ElementLength * i;
            var valueWithQuotes = PCSString.Convert(model, elementStart, enumArray.ElementContent[0].Length).Trim();
            var value = valueWithQuotes.Substring(1, valueWithQuotes.Length - 2);
            if (value.Contains(' ')) value = $"\"{value}\"";
            results.Add(value);
         }

         cachedOptions = results;
         return results;
      }

      public void ClearCache() { cachedOptions = null; }
   }
}
