using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using System.IO;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class CodeTool : ViewModelCore, IToolViewModel {
      public string Name => "Code Tool";

      private string content;
      private readonly ThumbParser parser;
      private readonly IDataModel model;
      private readonly Selection selection;

      public string Content {
         get => content;
         set => TryUpdate(ref content, value);
      }

      public CodeTool(IDataModel model, Selection selection) {
         this.parser = new ThumbParser(File.ReadAllLines("Models/Code/armReference.txt"));
         this.model = model;
         this.selection = selection;
         selection.PropertyChanged += (sender, e) => {
            if (e.PropertyName == nameof(selection.SelectionEnd)) {
               UpdateContent();
            }
         };
      }

      public void UpdateContent() {
         var start = selection.Scroll.ViewPointToDataIndex(selection.SelectionStart);
         var end = selection.Scroll.ViewPointToDataIndex(selection.SelectionEnd);
         if (start == end) { Content = string.Empty; return; }

         Content = parser.Parse(model, start, end - start + 1);
      }
   }
}
