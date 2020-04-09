using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
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
            var options = GetOptions(model).ToList();
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

      // TODO do some sort of caching: rendering these images every time probably sucks for performance.
      public IEnumerable<ComboOption> GetComboOptions(IDataModel model) {
         var defaultOptions = GetOptions(model).Select(option => new ComboOption(option));
         var tableRun = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, EnumName)) as ITableRun;
         if (tableRun == null) return defaultOptions;
         if (!(tableRun.ElementContent[0] is ArrayRunPointerSegment pointerSegment)) return defaultOptions;
         if (!LzSpriteRun.TryParseSpriteFormat(pointerSegment.InnerFormat, out var _) && !Sprites.SpriteRun.TryParseSpriteFormat(pointerSegment.InnerFormat, out var _)) return defaultOptions;

         var imageOptions = new List<ComboOption>();
         for (int i = 0; i < tableRun.ElementCount; i++) {
            var destination = model.ReadPointer(tableRun.Start + tableRun.ElementLength * i);
            var run = model.GetNextRun(destination) as Sprites.ISpriteRun;
            if (run == null) return defaultOptions;
            var sprite = run.GetPixels(model, 0);
            var paletteAddress = SpriteTool.FindMatchingPalette(model, run, 0);
            var paletteRun = model.GetNextRun(paletteAddress) as Sprites.IPaletteRun;
            var palette = paletteRun?.GetPalette(model, 0) ?? TileViewModel.CreateDefaultPalette(16);
            var image = SpriteTool.Render(sprite, palette);
            var option = ComboOption.CreateFromSprite(image, sprite.GetLength(0));
            imageOptions.Add(option);
         }

         return imageOptions;
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
            if (option.MatchesPartial(text)) {
               partialMatches.Add(option);
               if (partialMatches.Count == desiredMatch && matches.Count == 0) value = i;
            }
         }
         if (matches.Count == 0 && partialMatches.Count >= desiredMatch) return true; // no full matches, use the partial match

         // we went through the whole array and didn't find it :(
         return false;
      }

      public IEnumerable<string> GetOptions(IDataModel model) {
         if (int.TryParse(EnumName, out var result)) return Enumerable.Range(0, result).Select(i => i.ToString());
         IEnumerable<string> options = model.GetOptions(EnumName);

         // we _need_ options for the table tool
         // if we have none, just create "0", "1", ..., "n-1" based on the length of the EnumName table.
         if (!options.Any()) {
            var tableRun = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, EnumName)) as ITableRun;
            if (tableRun != null) options = Enumerable.Range(0, tableRun.ElementCount).Select(i => i.ToString());
         }

         return options;
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

      public bool IsInnerFormatValid => FormatRunFactory.GetStrategy(InnerFormat) != null;

      public ArrayRunPointerSegment(string name, string innerFormat) : base(name, ElementContentType.Pointer, 4) {
         InnerFormat = innerFormat;
      }

      public bool DestinationDataMatchesPointerFormat(IDataModel owner, ModelDelta token, int source, int destination, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         if (destination == Pointer.NULL) return true;
         var run = owner.GetNextAnchor(destination);
         if (run.Start < destination) return false;
         if (run.Start > destination || (run.Start == destination && (run is NoInfoRun || run is PointerRun))) {
            // hard case: no format found, so check the data
            return FormatRunFactory.GetStrategy(InnerFormat)?.TryAddFormatAtDestination(owner, token, source, destination, Name, sourceSegments) ?? false;
         } else {
            // easy case: already have a useful format, just see if it matches
            return FormatRunFactory.GetStrategy(InnerFormat)?.Matches(run) ?? false;
         }
      }

      public void WriteNewFormat(IDataModel owner, ModelDelta token, int source, int destination, int length, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         owner.WritePointer(token, source, destination);
         IFormattedRun run;
         var newRun = FormatRunFactory.GetStrategy(InnerFormat).WriteNewRun(owner, token, source, destination, Name, sourceSegments);
         owner.ObserveRunWritten(token, newRun.MergeAnchor(new[] { source }));
      }
   }
}
