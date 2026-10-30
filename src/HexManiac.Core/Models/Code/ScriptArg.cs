using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Code {

   public interface IScriptArg {
      ArgType Type { get; }
      ExpectedPointerType PointerType { get; }
      string Name { get; }
      string EnumTableName { get; }

      int Length(IDataModel model, int start);
   }

   /// <summary>How flags and variables are written in the script editor (View > Script Flags&amp;Vars).</summary>
   public enum ScriptNameDisplay {
      /// <summary>Numbers. Only the flags the ROM named on purpose (list 'scriptnamedflags': level caps, badges, start menu entries, battle options) keep their names.</summary>
      Numbers = 0,
      /// <summary>Every flag and variable by its define (the lists 'scriptflagaliases' and 'scriptvariablealiases').</summary>
      Defines = 1,
   }

   public class ScriptArg : IScriptArg {
      public const string NamedFlagList = "scriptnamedflags", AllFlagList = "scriptflagaliases", AllVariableList = "scriptvariablealiases";

      /// <summary>The View menu's choice, saved with the other settings. Numbers is the default.</summary>
      public static ScriptNameDisplay NameDisplay { get; set; } = ScriptNameDisplay.Numbers;

      /// <summary>
      /// The list that gives the names the editor shows for a list of the script reference.
      /// Defines mode shows every flag (not only the named ones). Null means: show numbers (variables in Numbers mode).
      /// </summary>
      public static string ListForDisplay(string listName) {
         if (listName == AllVariableList && NameDisplay == ScriptNameDisplay.Numbers) return null;
         if (listName == NamedFlagList && NameDisplay == ScriptNameDisplay.Defines) return AllFlagList;
         return listName;
      }

      private int length;

      public ArgType Type { get; }
      public ExpectedPointerType PointerType { get; }
      public string Name { get; }
      public string EnumTableName { get; }
      public int EnumOffset { get; }

      /// <summary>
      /// True for an argument written as 'name:list|h' (for example 'flag:scriptnamedflags|h').
      /// That is a number first: it is shown in hex and typed as a number, like every 'name:|h' argument.
      /// But the ROM's 'list' (a [[List]] in the TOML, indexed by the number) may also give some of the numbers a name.
      /// Those names are shown when decompiling, accepted when compiling, and only suggested while the user is typing a letter.
      /// A ROM without the list (or a list with gaps) simply keeps the plain numbers.
      /// </summary>
      public bool IsNumberOrName => IsNumberOrNameSpec(EnumTableName);

      /// <summary>The list or table of an EnumTableName, without a trailing '|h' or '|z'. Null if there is no EnumTableName.</summary>
      public string EnumListName => GetListName(EnumTableName);

      public static string GetListName(string enumTableName) {
         if (enumTableName == null) return null;
         var bar = enumTableName.IndexOf('|');
         return bar < 0 ? enumTableName : enumTableName.Substring(0, bar);
      }

      /// <summary>True for 'list|h': a number that can also be written as a name from the list.</summary>
      public static bool IsNumberOrNameSpec(string enumTableName) {
         if (string.IsNullOrEmpty(enumTableName)) return false;
         var bar = enumTableName.IndexOf('|');
         return bar > 0 && enumTableName.Substring(bar) == "|h";
      }

      /// <summary>
      /// The name of the table or list that holds the options for an EnumTableName.
      /// Only 'list|h' is split up, every other spelling (including '|h' on its own) is returned as it is.
      /// </summary>
      public static string GetOptionsName(string enumTableName) => IsNumberOrNameSpec(enumTableName) ? GetListName(enumTableName) : enumTableName;

      public int Length(IDataModel model, int start) => length;

      public ScriptArg(string token) {
         (Type, PointerType, Name, EnumTableName, length) = Construct(token);
         if (EnumTableName == null) return;
         if (EnumTableName.Contains("+")) {
            var parts = EnumTableName.Split(new[] { '+' }, 2);
            EnumTableName = parts[0];
            if (parts[1].TryParseInt(out var result)) EnumOffset = result;
         } else if (EnumTableName.Contains("-")) {
            var parts = EnumTableName.Split(new[] { '-' }, 2);
            EnumTableName = parts[0];
            if (parts[1].TryParseInt(out var result)) EnumOffset = -result;
         }
      }

      public static (ArgType type, ExpectedPointerType pointerType, string name, string enumTableName, int length) Construct(string token) {
         if (token.Contains("<>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Unknown, name, default, length);
         } else if (token.Contains("<\"\">")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<\"\">" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Text, name, default, length);
         } else if (token.Contains("<`mart`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`mart`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Mart, name, default, length);
         } else if (token.Contains("<`decor`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`decor`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Decor, name, default, length);
         } else if (token.Contains("<`move`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`move`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Movement, name, default, length);
         } else if (token.Contains("<`oam`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`oam`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.SpriteTemplate, name, default, length);

         } else if (token.Contains("<`xse`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`xse`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);
         } else if (token.Contains("<`bse`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`bse`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);
         } else if (token.Contains("<`ase`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`ase`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);
         } else if (token.Contains("<`tse`>")) {
            var (type, length) = (ArgType.Pointer, 4);
            var name = token.Split(new[] { "<`tse`>" }, StringSplitOptions.None).First();
            return (type, ExpectedPointerType.Script, name, default, length);

         } else if (token.Contains("::")) {
            var (type, length) = (ArgType.Word, 4);
            var name = token.Split(new[] { "::" }, StringSplitOptions.None).First();
            var enumTableName = token.Split("::").Last();
            return (type, default, name, enumTableName, length);
         } else if (token.Contains(':')) {
            var (type, length) = (ArgType.Short, 2);
            var name = token.Split(':').First();
            var enumTableName = token.Split(':').Last();
            return (type, default, name, enumTableName, length);
         } else if (token.Contains('.')) {
            var (type, length) = (ArgType.Byte, 1);
            var parts = token.Split(new[] { '.' }, 2);
            var name = parts[0];
            var enumTableName = parts[1];
            return (type, default, name, enumTableName, length);
         } else {
            // didn't find a token :(
            // I guess it's a byte?
            var (type, length) = (ArgType.Byte, 1);
            var name = token;
            return (type, default, name, default, length);
         }
      }

      public static bool IsValidToken(string token) {
         return "<> <`xse`> <`bse`> <`ase`> <`tse`> <\"\"> <`mart`> <`decor`> <`move`> <`oam`> : .".Split(' ').Any(token.Contains);
      }

      public bool FitsInRange(IDataModel model, int address) {
         if (string.IsNullOrEmpty(EnumTableName) || EnumTableName.StartsWith("|") || IsNumberOrName) return true;
         return model.ReadMultiByteValue(address, length) < model.GetOptions(EnumTableName).Count;
      }

      /// <summary>
      /// Reads the text of one argument the way the compiler does: a number, or a name from the argument's list.
      /// </summary>
      /// <returns>True if the text could be understood.</returns>
      public bool TryParseValue(IDataModel model, string text, out int value) => Convert(model, text, out value) == null;

      private static bool TryParseNumber(string text, out int result) {
         result = 0;
         return
            text.StartsWith("0x") && text.Substring(2).TryParseHex(out result) ||
            text.StartsWith("0X") && text.Substring(2).TryParseHex(out result) ||
            text.StartsWith("$") && text.Substring(1).TryParseHex(out result) ||
            int.TryParse(text, out result);
      }

      private string Convert(IDataModel model, int value, int bytes) {
         var preferHex = EnumTableName?.EndsWith("|h") ?? false;
         var preferSign = EnumTableName?.EndsWith("|z") ?? false;
         var enumName = ListForDisplay(EnumTableName?.Split('|')[0]);
         var table = string.IsNullOrEmpty(enumName) ? null : model.GetOptions(enumName);
         if (table == null || value - EnumOffset < 0 || table.Count <= value - EnumOffset || string.IsNullOrEmpty(table[value])) {
            if (value != int.MinValue) preferHex |= Math.Abs(value).InRange(0x4000, 20000) || Math.Abs(value) > 20100;
            if (preferHex || value == int.MinValue) {
               return "0x" + ((uint)(value - EnumOffset)).ToString($"X{length * 2}");
            } else {
               if (bytes == 1 && preferSign) value = (sbyte)value;
               if (bytes == 2 && preferSign) value = (short)value;
               return (value - EnumOffset).ToString();
            }
         }
         return table[value - EnumOffset];
      }

      private string Convert(IDataModel model, string value, out int result) {
         result = 0;
         var parseType = "as a number";
         if (IsNumberOrName) {
            // numbers always win: only text that is not a number is looked up in the list
            if (TryParseNumber(value, out result)) {
               result += EnumOffset;
               return null;
            }
            var listName = ListForDisplay(EnumListName) ?? EnumListName;
            var options = model.GetOptions(listName);
            if (options.Count == 0) return $"Could not parse '{value}' {parseType}."; // the ROM has no names for this argument
            if (ArrayRunEnumSegment.TryParse(listName, model, value, out result)) {
               result += EnumOffset;
               return null;
            }
            return $"Could not parse '{value}' as a number or from {EnumListName}.";
         }
         if (!string.IsNullOrEmpty(EnumTableName)) {
            if (!EnumTableName.StartsWith("|")) parseType = "from " + EnumTableName;
            if (ArrayRunEnumSegment.TryParse(EnumTableName, model, value, out result)) {
               result += EnumOffset;
               return null;
            }
         }
         if (
            value.StartsWith("0x") && value.Substring(2).TryParseHex(out result) ||
            value.StartsWith("0X") && value.Substring(2).TryParseHex(out result) ||
            value.StartsWith("$") && value.Substring(1).TryParseHex(out result) ||
            int.TryParse(value, out result)
         ) {
            result += EnumOffset;
            return null;
         }
         return $"Could not parse '{value}' {parseType}.";
      }

      /// <summary>
      /// Build from compiled bytes to text.
      /// </summary>
      public bool Build(bool allFillerIsZero, IDataModel data, int start, StringBuilder builder, List<string> streamContent, DecompileLabelLibrary labels, IList<ExpectedPointerType> streamTypes)
         => Build(allFillerIsZero, data, start, builder, streamContent, 0, 0, labels, streamTypes);

      public bool Build(bool allFillerIsZero, IDataModel data, int start, StringBuilder builder, List<string> streamContent, int shift, int carry, DecompileLabelLibrary labels, IList<ExpectedPointerType> streamTypes) {
         if (allFillerIsZero && Name == "filler") return true;
         if (Type == ArgType.Byte) builder.Append(Convert(data, (data[start] << shift) + carry, 1));
         if (Type == ArgType.Short) builder.Append(Convert(data, (data.ReadMultiByteValue(start, 2) << shift) + carry, 2));
         if (Type == ArgType.Word) builder.Append(Convert(data, data.ReadMultiByteValue(start, 4), 4)); // shift/carry not supported for 4-byte reads
         if (Type == ArgType.Pointer) {
            var address = data.ReadMultiByteValue(start, 4);
            if (address < 0x8000000) {
               builder.Append($"<{labels.AddressToLabel(address + Pointer.NULL, Type == ArgType.Pointer && PointerType == ExpectedPointerType.Script)}>");
            } else {
               address -= 0x8000000;
               builder.Append($"<{labels.AddressToLabel(address, Type == ArgType.Pointer && PointerType == ExpectedPointerType.Script)}>");
               if (PointerType != ExpectedPointerType.Unknown) {
                  if (data.GetNextRun(address) is IStreamRun stream && stream.Start == address) {
                     streamContent.Add(stream.SerializeRun());
                     streamTypes.Add(PointerType);
                  }
               }
            }
         }
         return false;
      }

      public string Build(IDataModel model, int address, string token, IList<byte> results, LabelLibrary labels)
         => Build(model, address, token, 0, results, labels);

      /// <summary>
      /// Build from text to compiled bytes.
      /// </summary>
      public string Build(IDataModel model, int address, string token, int shift, IList<byte> results, LabelLibrary labels) {
         int value;
         if (Type == ArgType.Byte) {
            var error = Convert(model, token, out value);
            if (error != null) return error;
            results.Add((byte)(value >> shift));
         } else if (Type == ArgType.Short) {
            var error = Convert(model, token, out value);
            if (error != null) return error;
            results.Add((byte)(value >> shift));
            results.Add((byte)(value >> (shift + 8)));
         } else if (Type == ArgType.Word) {
            var error = Convert(model, token, out value);
            if (error != null) return error;
            results.Add((byte)value);
            results.Add((byte)(value >> 0x8));
            results.Add((byte)(value >> 0x10));
            results.Add((byte)(value >> 0x18));
         } else if (Type == ArgType.Pointer) {
            if (token.StartsWith("<")) {
               if (!token.EndsWith(">")) return "Unmatched <>";
               token = token.Substring(1, token.Length - 2);
            }
            if (token.StartsWith("0x")) {
               token = token.Substring(2);
            }
            if (token == "auto") {
               if (PointerType == ExpectedPointerType.Script || PointerType == ExpectedPointerType.Unknown) {
                  return "<auto> only supported for text/data.";
               }
               value = Pointer.NULL + DeferredStreamToken.AutoSentinel;
            } else if (labels.TryResolveLabel(token, out value)) {
               // resolved to an address
            } else if (token == "null") {
               value = Pointer.NULL;
            } else if (token.TryParseHex(out value)) {
               // pointer *is* an address: nothing else to do
               if (value > -Pointer.NULL) value += Pointer.NULL;
               //       public bool RequireCompleteAddresses { get; set; } = true;
               if (labels.RequireCompleteAddresses && (token.Length < 6 || token.Length > 7)) {
                  return "Script addresses must be 6 or 7 characters long.";
               }
            } else if (PointerType != ExpectedPointerType.Script) {
               return $"'{token}' is not a valid pointer.";
            } else {
               labels.AddUnresolvedLabel(token, address);
               value = Pointer.NULL;
            }
            if (value < 0x02000000) value -= Pointer.NULL;
            results.Add((byte)value);
            results.Add((byte)(value >> 0x8));
            results.Add((byte)(value >> 0x10));
            results.Add((byte)(value >> 0x18));
         } else {
            throw new NotImplementedException();
         }
         return null;
      }
   }

   public class SilentMatchArg : IScriptArg {
      public ArgType Type => ArgType.Byte;
      public ExpectedPointerType PointerType => ExpectedPointerType.Unknown;
      public string Name => null;
      public string EnumTableName => null;
      public int EnumOffset => 0;

      public int Length(IDataModel model, int start) => 1;

      public byte ExpectedValue { get; }
      public SilentMatchArg(byte value) => ExpectedValue = value;
   }

   public class ArrayArg : IScriptArg {
      public ArgType Type { get; }
      public string Name { get; }
      public string EnumTableName { get; }
      public int TokenLength { get; }
      public ExpectedPointerType PointerType => ExpectedPointerType.Unknown;

      public int Length(IDataModel model, int start) {
         return model[start] * TokenLength + 1;
      }

      public ArrayArg(string token) {
         (Type, _, Name, EnumTableName, TokenLength) = ScriptArg.Construct(token);
      }

      public string ConvertMany(IDataModel model, int start) {
         var result = new StringBuilder();
         var count = model[start];
         start++;
         for (int i = 0; i < count; i++) {
            var value = model.ReadMultiByteValue(start, TokenLength);
            start += TokenLength;
            var tokenText = "0x" + value.ToString($"X{TokenLength * 2}");
            if (!string.IsNullOrEmpty(EnumTableName)) {
               var table = model.GetOptions(EnumTableName);
               if ((table?.Count ?? 0) > value) {
                  tokenText = table[value];
               }
            }
            result.Append(tokenText);
            if (i < count - 1) result.Append(' ');
         }
         return result.ToString();
      }

      public IEnumerable<int> ConvertMany(IDataModel model, IEnumerable<string> info) {
         foreach (var token in info) {
            if (string.IsNullOrEmpty(EnumTableName)) {
               if (token.StartsWith("0x") && token.Substring(2).TryParseHex(out var result)) yield return result;
               else if (token.StartsWith("0X") && token.Substring(2).TryParseHex(out result)) yield return result;
               else if (token.StartsWith("$") && token.Substring(1).TryParseHex(out result)) yield return result;
               else if (int.TryParse(token, out result)) yield return result;
               else yield return 0;
            } else if (ArrayRunEnumSegment.TryParse(EnumTableName, model, token, out var enumValue)) {
               yield return enumValue;
            } else {
               yield return 0;
            }
         }
      }
   }

   public enum ArgType {
      Byte,
      Short,
      Word,
      Pointer,
   }

}
