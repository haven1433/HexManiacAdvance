using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public enum ElementContentType {
      Unknown,
      PCS,
      Integer,
      Pointer,
      BitArray,
      Splitter,
   }

   public interface IHasOptions {
      IEnumerable<string> GetOptions(IDataModel model);
   }

   public class ArrayRunElementSegment {
      public string Name { get; }
      public ElementContentType Type { get; }
      public int Length { get; }
      public virtual string SerializeFormat {
         get {
            if (Type == ElementContentType.PCS) return $"{Name}\"\"{Length}";
            if (Type == ElementContentType.Pointer) return $"{Name}<>";
            if (Type == ElementContentType.Integer && Length == 1) return $"{Name}.";
            if (Type == ElementContentType.Integer && Length == 2) return $"{Name}:";
            if (Type == ElementContentType.Integer && Length == 3) return $"{Name}:.";
            if (Type == ElementContentType.Integer && Length == 4) return $"{Name}::";
            throw new NotImplementedException();
         }
      }

      public ArrayRunElementSegment(string name, ElementContentType type, int length) => (Name, Type, Length) = (name, type, length);

      private bool recursionStopper;
      public virtual string ToText(IDataModel rawData, int offset, bool deep = false) {
         switch (Type) {
            case ElementContentType.PCS:
               return PCSString.Convert(rawData, offset, Length);
            case ElementContentType.Integer:
               return ToInteger(rawData, offset, Length).ToString();
            case ElementContentType.Pointer:
               var address = rawData.ReadPointer(offset);
               var anchor = rawData.GetAnchorFromAddress(-1, address);
               if (string.IsNullOrEmpty(anchor)) anchor = address.ToString("X6");
               var run = rawData.GetNextRun(address) as IAppendToBuilderRun;
               if (!deep || recursionStopper || run == null) return $"{PointerRun.PointerStart}{anchor}{PointerRun.PointerEnd}";

               var builder = new StringBuilder("@{ ");
               recursionStopper = true;
               if (run is ArrayRun arrayRun && arrayRun.SupportsInnerPointers && arrayRun.ElementContent.Count == 1 && arrayRun.ElementContent[0].Type == ElementContentType.PCS) {
                  // special case: if the pointer in this arrya is to a specific element of a text array, only copy that one element rather than the whole array.
                  run.AppendTo(rawData, builder, address, arrayRun.ElementLength, deep: false);
               } else {
                  run.AppendTo(rawData, builder, run.Start, run.Length, deep);
               }
               recursionStopper = false;
               if (run is TableStreamRun tsr && tsr.AllowsZeroElements) {
                  if (builder.Length >= 2 && builder.ToString(builder.Length - 2, 2) != "[]") {
                     builder.Append(" []"); // Include the end token, allowing streams to be made shorter.
                  }
               }
               builder.Append(" @} ");

               return builder.ToString();
            default:
               throw new NotImplementedException();
         }
      }

      /// <summary>
      /// Writes data to the model.
      /// If data gets parsed into further tokens,
      ///   the remaining work is returned in data.
      /// If data gets parsed and submitted fully,
      ///   data is returned as an empty string.
      /// </summary>
      /// <returns>
      /// true if the data was changed.
      /// </returns>
      public virtual bool Write(IDataModel model, ModelDelta token, int start, ref string data) {
         if (data.StartsWith("(") && data.EndsWith(")")) data = data.Substring(1, data.Length - 2);
         var remainder = string.Empty;
         if (Type != ElementContentType.PCS) {
            var tokens = TableStreamRun.Tokenize(data);
            remainder = ", ".Join(tokens.Skip(1));
            data = tokens.Count > 0 ? tokens[0] : string.Empty;
         }

         switch (Type) {
            case ElementContentType.PCS:
               var bytes = PCSString.Convert(data);
               while (bytes.Count > Length) bytes.RemoveAt(bytes.Count - 1);
               if (!bytes.Contains(0xFF)) bytes[bytes.Count - 1] = 0xFF;
               while (bytes.Count < Length) bytes.Add(0);
               data = remainder;
               return token.ChangeData(model, start, bytes);
            case ElementContentType.Integer:
               int intValue;
               if (data.StartsWith("0x")) {
                  if (!int.TryParse(data.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out intValue)) intValue = 0;
               } else {
                  if (!int.TryParse(data, out intValue)) intValue = 0;
               }
               data = remainder;
               return model.WriteMultiByteValue(start, Length, token, intValue);
            case ElementContentType.Pointer:
               data = data.Trim();
               if (data.StartsWith("<")) data = data.Substring(1);
               if (data.EndsWith(">")) data = data.Substring(0, data.Length - 1);
               if (!int.TryParse(data, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var address)) {
                  address = model.GetAddressFromAnchor(token, -1, data);
               }
               data = remainder;

               if (this is not ArrayRunPointerSegment pointerSegment || pointerSegment.DestinationDataMatchesPointerFormat(model, token, start, address, null, -1)) {
                  var currentDestination = model.ReadPointer(start);
                  model.UpdateArrayPointer(token, this, null, default, start, address);
                  return currentDestination != address;
               } else {
                  var oldDestination = model.ReadPointer(start);
                  var oldRun = model.GetNextRun(oldDestination);
                  if (oldRun.Start == oldDestination) {
                     oldRun = oldRun.RemoveSource(start);
                     if (oldRun.PointerSources.Count == 0 && model.GetAnchorFromAddress(-1, oldRun.Start) == string.Empty) model.ClearFormat(token, oldDestination, 1);
                  }
                  var changed = model.WritePointer(token, start, address);
                  model.ObserveRunWritten(token, new NoInfoRun(address, new SortedSpan<int>(start)));
                  return changed;
               }
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

   public class ArrayRunSignedSegment : ArrayRunElementSegment {
      public override string SerializeFormat => base.SerializeFormat + ArrayRun.SignedFormatString;
      public ArrayRunSignedSegment(string name, int length) : base(name, ElementContentType.Integer, length) { }
      public override string ToText(IDataModel rawData, int offset, bool deep = false) {
         return ReadValue(rawData, offset).ToString();
      }

      public int ReadValue(IDataModel model, int offset) {
         var value = model.ReadMultiByteValue(offset, Length);
         if (Length == 1) return (sbyte)value;
         if (Length == 2) return (short)value;
         return value;
      }
   }

   public class ArrayRunEnumSegment : ArrayRunElementSegment, IHasOptions {
      public string EnumName { get; }

      public int ValueOffset { get; }

      public override string SerializeFormat {
         get {
            var result = base.SerializeFormat + EnumName;
            if (ValueOffset > 0) result += "+" + ValueOffset;
            if (ValueOffset < 0) result += ValueOffset;
            return result;
         }
      }

      public ArrayRunEnumSegment(string name, int length, string enumName) : base(name, ElementContentType.Integer, length) {
         EnumName = enumName;
         var parts = EnumName.Split("+");
         if (parts.Length == 2 && int.TryParse(parts[1], out int valueOffset)) {
            EnumName = parts[0];
            ValueOffset = valueOffset;
         }
      }

      public override string ToText(IDataModel model, int address, bool deep) {
         using (ModelCacheScope.CreateScope(model)) {
            var options = GetOptions(model).ToList();
            if (options == null) return base.ToText(model, address, deep);

            var resultAsInteger = ToInteger(model, address, Length) - ValueOffset;
            if (resultAsInteger >= options.Count || resultAsInteger < 0) return resultAsInteger.ToString();
            var value = options[resultAsInteger] ?? resultAsInteger.ToString();

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

      // TODO do some sort of caching: rendering these images every time probably sucks for performance.
      public IEnumerable<ComboOption> GetComboOptions(IDataModel model) {
         var defaultOptions = GetOptions(model)
            .Select((option, i) => new ComboOption(option, i))
            .Where(combo => combo.Text != null)
            .ToList();
         if (!(model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, EnumName)) is ITableRun tableRun)) return defaultOptions;
         if (!(tableRun.ElementContent[0] is ArrayRunPointerSegment pointerSegment)) return defaultOptions;
         if (!LzSpriteRun.TryParseSpriteFormat(pointerSegment.InnerFormat, out var _) && !SpriteRun.TryParseSpriteFormat(pointerSegment.InnerFormat, out var _)) return defaultOptions;

         var imageOptions = new List<ComboOption>();
         for (int i = 0; i < tableRun.ElementCount; i++) {
            var destination = model.ReadPointer(tableRun.Start + tableRun.ElementLength * i);
            if (!(model.GetNextRun(destination) is ISpriteRun run)) return defaultOptions;
            var sprite = run.GetPixels(model, 0);
            var paletteAddress = SpriteTool.FindMatchingPalette(model, run, 0);
            var paletteRun = model.GetNextRun(paletteAddress) as IPaletteRun;
            var palette = paletteRun?.GetPalette(model, paletteRun.PaletteFormat.InitialBlankPages) ?? TileViewModel.CreateDefaultPalette(16);
            var image = SpriteTool.Render(sprite, palette, paletteRun?.PaletteFormat.InitialBlankPages ?? default, 0);
            var option = VisualComboOption.CreateFromSprite(defaultOptions[i].Text, image, sprite.GetLength(0), i);
            imageOptions.Add(option);
         }

         return imageOptions;
      }

      public override bool Write(IDataModel model, ModelDelta token, int start, ref string data) {
         if (data.StartsWith("(") && data.EndsWith(")")) data = data.Substring(1, data.Length - 2);
         var tokens = TableStreamRun.Tokenize(data);
         var remainder = ", ".Join(tokens.Skip(1));
         data = tokens[0];
         bool result;

         if (!TryParse(model, data, out int value)) {
            result = base.Write(model, token, start, ref data);
         } else {
            data = value.ToString();
            result = base.Write(model, token, start, ref data);
         }

         data = remainder;
         return result;
      }

      public bool TryParse(IDataModel model, string text, out int value) {
         var result = TryParse(EnumName, model, text, out value);
         value += ValueOffset;
         return result;
      }

      public static bool TryParse(string enumName, IDataModel model, string text, out int value) {
         var options = model.GetOptions(enumName);
         return TryMatch(text, options, out value);
      }

      public static bool TryMatch(string text, IReadOnlyList<string> list, out int value) {
         text = text.Trim();
         if (int.TryParse(text, out value)) return true;
         if (text.StartsWith("\"")) text = text.Substring(1);
         if (text.EndsWith("\"")) text = text.Substring(0, text.Length - 1);
         if (text.Length == 0) return false;
         var partialMatches = new List<string>();
         var matches = new List<string>();

         // if the ~ character is used, expect that it's saying which match we want
         var desiredMatch = 1;
         var splitIndex = text.IndexOf('~');
         if (splitIndex != -1 && !int.TryParse(text.Substring(splitIndex + 1), out desiredMatch)) return false;
         if (splitIndex != -1) text = text.Substring(0, splitIndex);

         // ok, so everything lines up... check the array to see if any values match the string entered
         text = text.ToLower();
         var options = list;
         for (int i = 0; i < options.Count; i++) {
            var option = (options[i] ?? i.ToString()).ToLower().Split("~")[0];
            if (option.StartsWith("\"")) option = option.Substring(1);
            if (option.EndsWith("\"")) option = option.Substring(0, option.Length - 1);
            if (option == text) matches.Add(option);
            if (matches.Count == desiredMatch) { value = i; return true; }
            if (option.MatchesPartial(text, onlyCheckLettersAndDigits: true)) {
               partialMatches.Add(option);
               if (partialMatches.Count == desiredMatch && matches.Count == 0) value = i;
            }
         }
         if (matches.Count == 0 && partialMatches.Count >= desiredMatch) return true; // no full matches, use the partial match

         // we went through the whole array and didn't find it :(
         return false;
      }

      public IEnumerable<string> GetOptions(IDataModel model) => GetOptions(model, EnumName);

      public static IEnumerable<string> GetOptions(IDataModel model, string enumName) {
         if (int.TryParse(enumName, out var result)) return result.Range().Select(i => i.ToString());
         IEnumerable<string> options = model.GetOptions(enumName);

         // we _need_ options for the table tool
         // if we have none, just create "0", "1", ..., "n-1" based on the length of the EnumName table.
         if (!options.Any()) {
            if (model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, enumName)) is ITableRun tableRun) {
               options = tableRun.ElementCount.Range().Select(i => i.ToString());
            }
         }

         return options;
      }
   }

   // 'switch' -> choose a format based on a value
   public class ArrayRunRecordSegment : ArrayRunElementSegment {
      public string MatchField { get; }
      public IReadOnlyDictionary<int, string> EnumForValue { get; }

      public override string SerializeFormat {
         get {
            var records = "|".Join(EnumForValue.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
            return $"{base.SerializeFormat}|s={MatchField}({records})";
         }
      }

      public ArrayRunRecordSegment(string name, int length, string recordSwitch) : base(name, ElementContentType.Integer, length) {
         Debug.Assert(recordSwitch.StartsWith("|s="));
         Debug.Assert(recordSwitch.Contains("(") && recordSwitch.Contains(")"));
         recordSwitch = recordSwitch.Substring(3);
         MatchField = recordSwitch.Split("(")[0];
         if (string.IsNullOrEmpty(MatchField)) throw new ArrayRunParseException("Record format is s={name}({number}={enum}|...).");
         recordSwitch = recordSwitch.Substring(MatchField.Length + 1).Replace(")", string.Empty);

         var enumForValue = new Dictionary<int, string>();
         foreach (var pair in recordSwitch.Split("|")) {
            var keyAndValue = pair.Split("=");
            if (keyAndValue.Length != 2) continue;
            if (!int.TryParse(keyAndValue[0], out int key)) continue;
            enumForValue[key] = keyAndValue[1];
         }
         EnumForValue = enumForValue;
      }

      public ArrayRunElementSegment CreateConcrete(IDataModel model, int offset) {
         var defaultConcrete = new ArrayRunElementSegment(Name, ElementContentType.Integer, Length);
         var table = (ITableRun)model.GetNextRun(offset);
         int matchFieldOffset = 0;
         int matchFieldIndex = 0;
         for (int i = 0; i < table.ElementContent.Count; i++) {
            if (table.ElementContent[i].Name == MatchField) break;
            matchFieldOffset += table.ElementContent[i].Length;
            matchFieldIndex += 1;
         }
         if (matchFieldOffset == table.ElementLength) return defaultConcrete;
         var offsets = table.ConvertByteOffsetToArrayOffset(offset);
         var matchFieldValue = model.ReadMultiByteValue(table.Start + offsets.ElementIndex * table.ElementLength + matchFieldOffset, table.ElementContent[matchFieldIndex].Length);
         if (!EnumForValue.TryGetValue(matchFieldValue, out var enumName)) return defaultConcrete;
         return new ArrayRunEnumSegment(Name, Length, enumName);
      }
   }

   public class ArrayRunHexSegment : ArrayRunElementSegment {
      public override string SerializeFormat => base.SerializeFormat + ArrayRun.HexFormatString;

      public ArrayRunHexSegment(string name, int length) : base(name, ElementContentType.Integer, length) {
      }

      public override string ToText(IDataModel rawData, int offset, bool deep = false) {
         var hexLength = "X" + (Length * 2);
         var hex = rawData.ReadMultiByteValue(offset, Length).ToString(hexLength);
         return hex;
      }

      public override bool Write(IDataModel model, ModelDelta token, int start, ref string data) {
         if (data.StartsWith("(") && data.EndsWith(")")) data = data.Substring(1, data.Length - 2);
         var remainder = string.Empty;
         if (Type != ElementContentType.PCS) {
            var tokens = TableStreamRun.Tokenize(data);
            remainder = ", ".Join(tokens.Skip(1));
            data = tokens[0];
         }

         data = data.Trim();
         if (data.StartsWith("0x")) data = data.Substring(2);
         if (!int.TryParse(data, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var intValue)) intValue = 0;
         data = remainder;
         return model.WriteMultiByteValue(start, Length, token, intValue);
      }
   }

   public class ArrayRunTupleSegment : ArrayRunHexSegment {
      public IReadOnlyList<TupleSegment> Elements { get; }
      public int VisibleElementCount => Elements.Count - Elements.Count(e => string.IsNullOrEmpty(e.Name));
      public ArrayRunTupleSegment(string name, string contract, int length) : base(name, length) {
         var content = new List<TupleSegment>();
         foreach (var part in contract.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)) {
            var elementName = part.Split('.', ':')[0];
            var elementEnumName = ".".Join(part.Split(new[] { '.', ':' }, StringSplitOptions.RemoveEmptyEntries).Skip(1));
            var elementFormat = part.Substring(elementName.Length, part.Length - elementName.Length - elementEnumName.Length);
            var elementSize = elementFormat.Sum(c => c == '.' ? 1 : c == ':' ? 2 : 0);
            content.Add(new TupleSegment(elementName, elementSize, elementEnumName));
         }
         Elements = content;
         if (content.Sum(seg => seg.BitWidth) > length * 8) throw new ArrayRunParseException($"{name}: tuple too long to fit in field!");
      }

      public override string ToText(IDataModel rawData, int offset, bool deep = false) {
         var result = "(";
         var bitOffset = 0;
         foreach (var segment in Elements) {
            if (string.IsNullOrEmpty(segment.Name)) {
               // don't append unnamed segments
            } else if (!string.IsNullOrEmpty(segment.SourceName)) {
               var options = rawData.GetOptions(segment.SourceName);
               var value = segment.Read(rawData, offset, bitOffset);
               var text = options.Count > value ? options[value] : value.ToString();
               result += text.Contains(" ") && !text.Contains("\"") ? '"' + text + '"' : text;
               result += " ";
            } else if (segment.BitWidth == 1) {
               result += segment.Read(rawData, offset, bitOffset) == 1 ? "true " : "false ";
            } else {
               result += segment.Read(rawData, offset, bitOffset) + " ";
            }
            bitOffset += segment.BitWidth;
         }
         return result.Trim() + ")";
      }

      public override bool Write(IDataModel model, ModelDelta token, int start, ref string data) {
         var parts = data.Split(new[] { "(", ")", " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
         TableStreamRun.Recombine(parts, "\"", "\"");
         data = string.Empty;
         if (parts.Count != VisibleElementCount) return false;
         int bitOffset = 0;
         int partIndex = 0;
         bool anyChanges = false;
         for (int i = 0; i < Elements.Count; i++) {
            if (string.IsNullOrEmpty(Elements[i].Name)) {
               // Unnamed segments. I should increment, and  bitOffset should increase, but the value should change to zero.
               anyChanges |= Elements[i].Write(model, token, start, bitOffset, 0);
               partIndex -= 1;
            } else if (!string.IsNullOrEmpty(Elements[i].SourceName)) {
               if (ArrayRunEnumSegment.TryParse(Elements[i].SourceName, model, parts[partIndex], out int value))
                  anyChanges |= Elements[i].Write(model, token, start, bitOffset, value);
            } else if (Elements[i].BitWidth == 1) {
               if (bool.TryParse(parts[partIndex], out bool value))
                  anyChanges |= Elements[i].Write(model, token, start, bitOffset, value ? 1 : 0);
            } else {
               if (int.TryParse(parts[partIndex], out int value))
                  anyChanges |= Elements[i].Write(model, token, start, bitOffset, value);
            }

            partIndex += 1;
            bitOffset += Elements[i].BitWidth;
         }

         var remainingBits = Length * 8 - bitOffset;
         if (remainingBits > 0) {
            anyChanges |= new TupleSegment(string.Empty, remainingBits).Write(model, token, start, bitOffset, 0);
         }

         return anyChanges;
      }

      public IReadOnlyList<AutocompleteItem> GetAutocomplete(IDataModel model, string text) {
         var tupleTokens = text.Split(" ").ToList();
         TableStreamRun.Recombine(tupleTokens, "\"", "\"");
         if (tupleTokens[0].StartsWith("(")) tupleTokens[0] = tupleTokens[0].Substring(1);
         var visibleTupleElements = Elements.Where(element => !string.IsNullOrEmpty(element.Name)).ToList();
         var optionToken = tupleTokens.Last();
         tupleTokens = tupleTokens.Take(tupleTokens.Count - 1).ToList();
         if (visibleTupleElements.Count > tupleTokens.Count) {
            var tupleToken = visibleTupleElements[tupleTokens.Count];
            if (optionToken.StartsWith("\"")) optionToken = optionToken.Substring(1);
            if (optionToken.EndsWith("\"")) optionToken = optionToken.Substring(0, optionToken.Length - 1);
            if (!string.IsNullOrEmpty(tupleToken.SourceName)) {
               var optionText = ArrayRunEnumSegment.GetOptions(model, tupleToken.SourceName).Where(option => option.MatchesPartial(optionToken));
               return CreateTupleEnumAutocompleteOptions(tupleTokens, optionText);
            } else if (tupleToken.BitWidth == 1) {
               var optionText = new[] { "false", "true" }.Where(option => option.MatchesPartial(optionToken));
               return CreateTupleEnumAutocompleteOptions(tupleTokens, optionText);
            }
         }

         return null;
      }

      public bool DependsOn(string anchorName) => Elements.Any(element => element.SourceName == anchorName);

      public string ConstructAutocompleteLine(IReadOnlyList<string> previousTokens, string option) {
         var newLine = "(";
         newLine += " ".Join(previousTokens);
         if (previousTokens.Count > 0) newLine += " ";
         newLine += option;
         if (previousTokens.Count + 1 < VisibleElementCount) newLine += " ";
         if (previousTokens.Count + 1 == VisibleElementCount) newLine += ")";
         return newLine;
      }

      private IReadOnlyList<AutocompleteItem> CreateTupleEnumAutocompleteOptions(IReadOnlyList<string> previousTokens, IEnumerable<string> newToken) {
         var results = new List<AutocompleteItem>();

         foreach (var option in newToken) {
            var newLine = ConstructAutocompleteLine(previousTokens, option);
            results.Add(new AutocompleteItem(option, newLine));
         }

         return results;
      }
   }

   public class TupleSegment {
      public string Name { get; }
      public string SourceName { get; }
      public int BitWidth { get; }
      public TupleSegment(string name, int width, string sourceName = null) => (Name, BitWidth, SourceName) = (name, width, sourceName);
      public int Read(IDataModel model, int start, int bitOffset) {
         var requiredByteLength = (bitOffset + BitWidth + 7) / 8;
         if (requiredByteLength > 4) return 0;
         var bitArray = model.ReadMultiByteValue(start, requiredByteLength);
         bitArray >>= bitOffset;
         bitArray &= (1 << BitWidth) - 1;
         return bitArray;
      }
      public bool Write(IDataModel model, ModelDelta token, int start, int bitOffset, int value) {
         var requiredByteLength = (bitOffset + BitWidth + 7) / 8;
         if (requiredByteLength > 4) return false;
         var bitArray = model.ReadMultiByteValue(start, requiredByteLength);
         var mask = (1 << BitWidth) - 1;
         value &= mask;
         bitArray &= ~(mask << bitOffset);
         bitArray |= value << bitOffset;
         return model.WriteMultiByteValue(start, requiredByteLength, token, bitArray);
      }
   }

   public class ArrayRunColorSegment : ArrayRunElementSegment {
      public override string SerializeFormat => base.SerializeFormat + ArrayRun.ColorFormatString;

      public ArrayRunColorSegment(string name) : base(name, ElementContentType.Integer, 2) { }

      public override string ToText(IDataModel rawData, int offset, bool deep = false) {
         var color = (short)rawData.ReadMultiByteValue(offset, Length);
         var colorText = UncompressedPaletteColor.Convert(color);
         return colorText;
      }
   }

   public class ArrayRunBitArraySegment : ArrayRunElementSegment, IHasOptions {
      public string SourceArrayName { get; }

      public override string SerializeFormat => $"{Name}{BitArray.SharedFormatString}{SourceArrayName}";

      public ArrayRunBitArraySegment(string name, int length, string bitSourceName) : base(name, ElementContentType.BitArray, length) {
         SourceArrayName = bitSourceName;
      }

      public override string ToText(IDataModel rawData, int offset, bool deep) {
         var result = new StringBuilder("-");
         var options = rawData.GetBitOptions(SourceArrayName);

         // handle edge cases: no options, or no options for some set bits
         var highestByteSet = Length - 1;
         while (rawData[offset + highestByteSet] == 0 && highestByteSet > 0) {
            highestByteSet -= 1;
         }
         var highestBitSet = (int)Math.Log(rawData[offset + highestByteSet], 2);
         var unknownBitsAreSet = options == null || (highestByteSet * 8) + highestBitSet >= options.Count;

         for (int i = 0; i < Length; i++) {
            var bits = rawData[offset + i];
            if (unknownBitsAreSet) {
               result.Append(bits.ToString("X2"));
            } else {
               var optionOffset = i << 3;
               for (int j = 0; j < 8; j++) {
                  if ((bits & (1 << j)) == 0) continue;
                  result.Append(" ");
                  result.Append(options[optionOffset + j]);
               }
            }
         }

         result.Append(" ");
         if (options != null) result.Append("/");
         return result.ToString();
      }

      public override bool Write(IDataModel model, ModelDelta token, int start, ref string data) {
         var anyChanges = false;
         for (int i = 0; i < Length && i * 2 + 1 < data.Length; i++) {
            if (!byte.TryParse(data.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var value)) value = 0;
            anyChanges |= token.ChangeData(model, start + i, value);
         }

         data = string.Empty;
         return anyChanges;
      }

      public IEnumerable<string> GetOptions(IDataModel model) {
         var cache = ModelCacheScope.GetCache(model);
         return cache.GetBitOptions(SourceArrayName);
      }
   }

   /// <summary>
   /// For pointers that contain nested formatting instructions.
   /// For example, pointing to a text stream or a plm (pokemon learnable moves) stream
   /// </summary>
   public class ArrayRunPointerSegment : ArrayRunElementSegment {
      public IFormatRunFactory Factory { get; }
      public string InnerFormat { get; }

      public bool IsInnerFormatValid => Factory.GetStrategy(InnerFormat) != null;

      public override string SerializeFormat => $"{Name}<{InnerFormat}>";

      public ArrayRunPointerSegment(IFormatRunFactory factory, string name, string innerFormat) : base(name, ElementContentType.Pointer, 4) {
         Factory = factory;
         InnerFormat = innerFormat;
      }

      public bool DestinationDataMatchesPointerFormat(IDataModel owner, ModelDelta token, int source, int destination, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         if (destination == Pointer.NULL) return true;
         var run = owner.GetNextAnchor(destination);
         if (run.Start < destination) return false;
         if (run.Start > destination || (run.Start == destination && (run is NoInfoRun || run is PointerRun))) {
            // hard case: no format found, so check the data
            if (destination < 0 || destination >= owner.Count) return false;
            return Factory.GetStrategy(InnerFormat)?.TryAddFormatAtDestination(owner, token, source, destination, Name, sourceSegments, parentIndex) ?? false;
         } else {
            // easy case: already have a format, just see if it matches
            var strategy = Factory.GetStrategy(InnerFormat);
            if (strategy == null) return false;
            if (strategy.Matches(run)) return true;

            // special test: the format of the data is wrong?
            return strategy.TryAddFormatAtDestination(owner, token, source, destination, Name, sourceSegments, parentIndex);
         }
      }

      public void WriteNewFormat(IDataModel owner, ModelDelta token, int source, int destination, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         owner.WritePointer(token, source, destination);
         var newRun = Factory.GetStrategy(InnerFormat).WriteNewRun(owner, token, source, destination, Name, sourceSegments);
         owner.ObserveRunWritten(token, newRun.MergeAnchor(new SortedSpan<int>(source)));
      }
   }

   public class ArrayRunCalculatedSegment : ArrayRunElementSegment {
      public IDataModel Model { get; }
      public IReadOnlyList<string> Operands { get; }
      public string Operator { get; }
      public bool HasOperator => !string.IsNullOrEmpty(Operator);

      public override string SerializeFormat => Name + "|=" + Operator.Join(Operands);

      public ArrayRunCalculatedSegment(IDataModel model, string name, string contract) : base(name, ElementContentType.Integer, 0) {
         Model = model;
         if (contract.Contains("*")) {
            var parts = contract.Split('*');
            Operands = parts;
            Operator = "*";
         } else if (contract.Contains("+")) {
            var parts = contract.Split('+');
            Operands = parts;
            Operator = "+";
         } else {
            Operands = new[] { contract };
            Operator = string.Empty;
         }
      }

      public int CalculatedValue(int index) {
         if (string.IsNullOrEmpty(Operands[0])) return 0;
         var table = (ITableRun)Model.GetNextRun(index);
         var offset = table.ConvertByteOffsetToArrayOffset(index);

         var values = Operands.Select(operand => ParseValue(Model, table, offset.ElementIndex, operand));
         switch (Operator) {
            case "+": return values.Aggregate((a, b) => a + b);
            case "*": return values.Aggregate((a, b) => a * b);
            default:  return values.First();
         }
      }

      public static int CalculateSource(IDataModel model, ITableRun table, int elementIndex, string content) {
         if (string.IsNullOrEmpty(content)) return Pointer.NULL;
         if (int.TryParse(content, out var _)) return Pointer.NULL;

         var tableSegment = table?.ElementContent.FirstOrDefault(seg => seg.Name == content);
         if (tableSegment != null) {
            return table.Start + table.ElementLength * elementIndex +
               table.ElementContent.Until(seg => seg == tableSegment).Sum(seg => seg.Length);
         }

         if (content.MatchesPartial("(/=)/")) {
            var parts = content.Split("(/=)".ToCharArray());
            var message = $"Expected {content} to fit the form (table/field=local)/field. But it didn't.";
            if (parts.Length != 6) throw new NotImplementedException(message);
            if (!string.IsNullOrEmpty(parts[0]) || !string.IsNullOrEmpty(parts[4])) throw new NotImplementedException(message);
            var matchTableName = parts[1];
            var matchTableField = parts[2];
            var matchLocalField = parts[3];
            var localFieldValue = ParseValue(model, table, elementIndex, matchLocalField);
            var valueField = parts[5];
            var matchTable = model.GetTable(matchTableName);
            if (matchTable == null) return Pointer.NULL;
            for (int i = 0; i < matchTable.ElementCount; i++) {
               if (matchTable.ReadValue(model, i, matchTableField) != localFieldValue) continue;
               return CalculateSource(model, matchTable, i, valueField);
            }
            return CalculateSource(model, matchTable, matchTable.ElementCount, valueField);
         }
         if (content.MatchesPartial("//")) {
            var parts = content.Split("/");
            var message = $"Expected {content} to fit the form field/index/field. But it didn't.";
            if (parts.Length != 3) throw new NotImplementedException(message);
            var destination = table.ReadPointer(model, elementIndex, parts[0]);
            var childTable = model.GetNextRun(destination) as ITableRun;
            if (childTable == null) return Pointer.NULL;
            var childTableIndex = ParseValue(model, childTable, elementIndex, parts[1]);
            return CalculateSource(model, childTable, childTableIndex, parts[2]);
         }

         throw new NotImplementedException();
      }

      public static int ParseValue(IDataModel model, ITableRun table, int elementIndex, string content) {
         if (string.IsNullOrEmpty(content)) return 0;
         if (int.TryParse(content, out int simpleValue)) return simpleValue;
         if (content == "last") return table.ElementCount - 1;

         if (table != null && table.ElementContent.Any(seg => seg.Name == content)) {
            return table.ReadValue(model, elementIndex, content);
         }

         if (content.MatchesPartial("(/=)/")) {
            var parts = content.Split("(/=)".ToCharArray());
            var message = $"Expected {content} to fit the form (table/field=local)/field. But it didn't.";
            if (parts.Length != 6) throw new NotImplementedException(message);
            if (!string.IsNullOrEmpty(parts[0]) || !string.IsNullOrEmpty(parts[4])) throw new NotImplementedException(message);
            var matchTableName = parts[1];
            var matchTableField = parts[2];
            var matchLocalField = parts[3];
            var localFieldValue = ParseValue(model, table, elementIndex, matchLocalField);
            var valueField = parts[5];
            var matchTable = model.GetTable(matchTableName);
            if (matchTable == null) return 0;
            for (int i = 0; i < matchTable.ElementCount; i++) {
               if (matchTable.ReadValue(model, i, matchTableField) != localFieldValue) continue;
               return ParseValue(model, matchTable, i, valueField);
            }
            return ParseValue(model, matchTable, matchTable.ElementCount - 1, valueField);
         }
         if (content.MatchesPartial("//")) {
            var parts = content.Split("/");
            var message = $"Expected {content} to fit the form field/index/field. But it didn't.";
            if (parts.Length != 3) throw new NotImplementedException(message);
            var destination = table.ReadPointer(model, elementIndex, parts[0]);
            if (destination == Pointer.NULL) return 0;
            var childTable = model.GetNextRun(destination) as ITableRun;
            if (childTable == null) throw new NotImplementedException(message);
            var childTableIndex = ParseValue(model, childTable, elementIndex, parts[1]);
            return ParseValue(model, childTable, childTableIndex, parts[2]);
         }
         
         throw new NotImplementedException();
      }
   }

   public class ArrayRunOffsetRenderSegment : ArrayRunElementSegment {
      public string Background { get; }
      public int BackgroundX { get; }
      public int BackgroundY { get; }
      public int BackgroundWidth { get; }
      public int BackgroundHeight { get; }

      public string Foreground { get; }
      public int X { get; }
      public int Y { get; }
      public string TargetFieldX { get; }
      public string TargetFieldY { get; }

      public override string SerializeFormat => $"{Name}|render={Background}|{BackgroundX}|{BackgroundY}|{BackgroundWidth}|{BackgroundHeight}|{Foreground}|{X}|{Y}|{TargetFieldX}|{TargetFieldY}";

      public ArrayRunOffsetRenderSegment(string name, string contract) : base(name, ElementContentType.Integer, 0) {
         var parts = contract.Split("|");
         Background = Get(parts, 0);
         BackgroundX = GetInt(parts, 1);
         BackgroundY = GetInt(parts, 2);
         BackgroundWidth = GetInt(parts, 3);
         BackgroundHeight = GetInt(parts, 4);

         Foreground = Get(parts, 5);
         X = GetInt(parts, 6);
         Y = GetInt(parts, 7);
         TargetFieldX = Get(parts, 8);
         TargetFieldY = Get(parts, 9);
      }

      private static string Get(string[] array, int index) => array.Length > index ? array[index] : string.Empty;

      private static int GetInt(string[] array, int index) => array.Length > index && int.TryParse(array[index], out var result) ? result : 0;
   }

   public class ArrayRunSplitterSegment : ArrayRunElementSegment {
      public const string Separator = "|";
      public ArrayRunSplitterSegment() : base(string.Empty, ElementContentType.Integer, 0) { }
      public override string SerializeFormat => Separator;
   }
}
