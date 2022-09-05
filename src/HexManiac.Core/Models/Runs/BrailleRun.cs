using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class BrailleRun : BaseRun, IStreamRun, IAppendToBuilderRun {
      public static readonly IReadOnlyDictionary<byte, char> Encoding = new Dictionary<byte, char>()
      {
         { 0x01, 'A' },
         { 0x05, 'B' },
         { 0x03, 'C' },
         { 0x0B, 'D' },
         { 0x09, 'E' },
         { 0x07, 'F' },
         { 0x0F, 'G' },
         { 0x0D, 'H' },
         { 0x06, 'I' },
         { 0x0E, 'J' },
         { 0x11, 'K' },
         { 0x15, 'L' },
         { 0x13, 'M' },
         { 0x1B, 'N' },
         { 0x19, 'O' },
         { 0x17, 'P' },
         { 0x1F, 'Q' },
         { 0x1D, 'R' },
         { 0x16, 'S' },
         { 0x1E, 'T' },
         { 0x31, 'U' },
         { 0x35, 'V' },
         { 0x2E, 'W' },
         { 0x33, 'X' },
         { 0x3B, 'Y' },
         { 0x39, 'Z' },
         { 0x00, ' ' },
         { 0x04, ',' },
         { 0x2C, '.' },
         { 0xFF, '$' },
      };

      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "brl" + AsciiRun.StreamDelimeter; // braille

      private readonly IDataModel model;

      public BrailleRun(IDataModel model, int start, SortedSpan<int> sources = null) : base(start, sources) {
         this.model = model;
         while (model.Count > start + Length && Length < 100 && model[start + Length] != 0xFF) Length++;
         Length++;
      }

      public override int Length { get; }

      public override string FormatString => SharedFormatString;

      public IReadOnlyList<IPixelViewModel> Visualizations => null;

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         for (int i = 0; i < length - 1; i++) {
            if (model[start] != 0xFF && Encoding.TryGetValue(model[start + i], out var c)) builder.Append(c);
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(model, start + i, 0xFF);
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         return new Braille(Start, index - Start, data[index] == 0xFF ? '"' : Encoding.TryGetValue(data[index], out var value) ? value : ' ');
      }

      public bool DependsOn(string anchorName) => false;

      public static byte DeserializeCharacter(char content) {
         var match = Encoding.Keys.FirstOrDefault(key => Encoding[key] == content);
         return match;
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         var changes = new List<int>();
         content = content.ToUpper();

         var bytes = new List<byte>();
         foreach (var c in content) bytes.Add(DeserializeCharacter(c));
         bytes.Add(0xFF);

         var run = model.RelocateForExpansion(token, this, bytes.Count);
         if (run.Start != Start) {
            changes.AddRange((Length - 1).Range().Select(i => Start + i));
            token.ChangeData(model, run.Start, bytes);
            changes.AddRange((Length - 1).Range().Select(i => run.Start + i));
         } else {
            for (int i = 0; i < bytes.Count; i++) {
               if (bytes[i] != model[Start + i]) {
                  changes.Add(Start + i);
                  token.ChangeData(model, Start + i, bytes[i]);
               }
            }
            for (int i = bytes.Count; i < Length - 1; i++) {
               changes.Add(Start + i);
               token.ChangeData(model, Start + i, 0xFF);
            }
         }

         changedOffsets = changes;
         return new BrailleRun(model, run.Start, PointerSources);
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) => null;

      public string SerializeRun() {
         var builder = new StringBuilder();
         AppendTo(model, builder, Start, Length - 1, false);
         return builder.ToString();
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new BrailleRun(model, Start, newPointerSources);
   }
}
