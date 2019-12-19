using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using System;
using System.IO;
using System.Text;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum CodeMode { Thumb, Script, Raw }

   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      private string content;
      private CodeMode mode;
      private readonly ThumbParser thumb;
      private readonly ScriptParser script;
      private readonly IDataModel model;
      private readonly Selection selection;

      public CodeMode Mode {
         get => mode;
         set {
            if (TryUpdateEnum(ref mode, value)) UpdateContent();
         }
      }

      public string Content {
         get => content;
         set => TryUpdate(ref content, value);
      }

      public ThumbParser Parser => thumb;

      public CodeTool(IDataModel model, Selection selection) {
         thumb = new ThumbParser(File.ReadAllLines("resources/armReference.txt"));
         script = new ScriptParser(File.ReadAllLines("resources/scriptReference.txt"));
         this.model = model;
         this.selection = selection;
         selection.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(selection.SelectionEnd)) {
               UpdateContent();
            }
         };
      }

      public void UpdateContent() {
         var start = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionStart));
         var end = Math.Min(model.Count - 1, selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd));

         if (start > end) (start, end) = (end, start);
         int length = end - start + 1;

         if (length > 0x1000) {
            Content = "Too many bytes selected.";
         } else if (mode == CodeMode.Raw) {
            Content = RawParse(model, start, end - start + 1);
         } else if (length < 2) {
            Content = string.Empty;
         } else if (mode == CodeMode.Script) {
            Content = script.Parse(model, start, end - start + 1);
         } else if (mode == CodeMode.Thumb) {
            Content = thumb.Parse(model, start, end - start + 1);
         } else {
            throw new NotImplementedException();
         }
      }

      private string RawParse(IDataModel model, int start, int length) {
         var builder = new StringBuilder();
         while (length > 0) {
            builder.Append(model[start].ToHexString());
            builder.Append(" ");
            length--;
            start++;
            if (start % 16 == 0) builder.AppendLine();
         }
         return builder.ToString();
      }
   }
}
