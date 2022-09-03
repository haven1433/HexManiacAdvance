using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class PCSRun : BaseRun, IStreamRun, IAppendToBuilderRun, IEquatable<IFormattedRun> {
      public const char StringDelimeter = '"';
      public static readonly string SharedFormatString = StringDelimeter + string.Empty + StringDelimeter;

      private readonly IDataModel model;
      private int cachedIndex = int.MaxValue;
      private string cachedFullString;

      public override int Length { get; }
      public override string FormatString => SharedFormatString;

      public PCSRun(IDataModel model, int start, int length, SortedSpan<int> sources = null) : base(start, sources) => (this.model, Length) = (model, length);

      public bool Equals(IFormattedRun run) {
         if (!(run is PCSRun other)) return false;
         return Start == other.Start && Length == other.Length && model == other.model;
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         Debug.Assert(data == model);

         // only read the full string from the data once per pass.
         // This assumes that we read data starting at the lowest index and working our way up.
         if (index < cachedIndex) cachedFullString = data.TextConverter.Convert(data, Start, Length);
         cachedIndex = index;

         return CreatePCSFormat(data, Start, index, cachedFullString);
      }

      public static IDataFormat CreatePCSFormat(IDataModel model, int start, int index, string fullString) {
         bool isEscaped = PCSString.IsEscaped(model, index);
         if (isEscaped) {
            return new EscapedPCS(start, index - start, fullString, model[index]);
         } else {
            var pcsCharacters = PCSString.Convert(model, index, 1);
            if (pcsCharacters == null) {
               return new ErrorPCS(start, index - start, fullString, model[index]);
            }
            var character = pcsCharacters.Substring(1); // trim leading "
            if (index == start) character = StringDelimeter + character; // include the opening quotation mark, only for the first character
            var pcs = new PCS(start, index - start, fullString, character);
            return pcs;
         }
      }

      public string SerializeRun() {
         var newContent = model.TextConverter.Convert(model, Start, Length) ?? "\"\"";
         newContent = newContent.Substring(1, newContent.Length - 2); // remove quotes
         return newContent;
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         var bytes = model.TextConverter.Convert(content, out var _);
         var changedAddresses = new List<int>();
         var newRun = model.RelocateForExpansion(token, this, bytes.Count);

         // clear out excess bytes that are no longer in use
         if (Start == newRun.Start) {
            for (int i = bytes.Count; i < Length; i++) token.ChangeData(model, Start + i, 0xFF);
         }

         if (token.ChangeData(model, newRun.Start, bytes)) changedAddresses.Add(newRun.Start);
         changedOffsets = changedAddresses;
         return new PCSRun(model, newRun.Start, bytes.Count, newRun.PointerSources);
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var result = new List<AutocompleteItem>();
         return result;
      }

      public IReadOnlyList<IPixelViewModel> Visualizations => new List<IPixelViewModel>();
      public bool DependsOn(string anchorName) => false;

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new PCSRun(model, Start, Length, newPointerSources);

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => builder.Append(model.TextConverter.Convert(model, Start, Length));

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) {
            changeToken.ChangeData(model, start + i, 0xFF);
         }
      }
   }
}
