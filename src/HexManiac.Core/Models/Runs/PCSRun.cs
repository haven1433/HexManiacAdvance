using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class PCSRun : BaseRun {
      public const char StringDelimeter = '"';

      private int cachedIndex = int.MaxValue;
      private string cachedFullString;

      public override int Length { get; }
      public override string FormatString => StringDelimeter.ToString() + StringDelimeter;

      public PCSRun(int start, int length, IReadOnlyList<int> sources = null) : base(start, sources) => Length = length;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         // only read the full string from the data once per pass.
         // This assumes that we read data starting at the lowest index and working our way up.
         if (index < cachedIndex) cachedFullString = PCSString.Convert(data, Start, Length);
         cachedIndex = index;

         return CreatePCSFormat(data, Start, index, cachedFullString);
      }

      public static IDataFormat CreatePCSFormat(IDataModel model, int start, int index, string fullString) {
         bool isEscaped = index > start && model[index - 1] == PCSString.Escape;
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

      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         return new PCSRun(Start, Length, newPointerSources);
      }
   }
}
