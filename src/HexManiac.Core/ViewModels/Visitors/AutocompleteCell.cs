using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   public class AutocompleteCell : IDataFormatVisitor {
      public IDataModel Model { get; }
      public string InputText { get; }
      public int SelectionIndex { get; }

      public IEnumerable<AutoCompleteSelectionItem> Result { get; private set; }

      public AutocompleteCell(IDataModel model, string input, int index) => (Model, InputText, SelectionIndex) = (model, input, index);

      public void Visit(Undefined dataFormat, byte data) { }

      public void Visit(UnderEdit dataFormat, byte data) => throw new NotImplementedException();

      public void Visit(PCS pcs, byte data) { }

      public void Visit(EscapedPCS pcs, byte data) { }

      public void Visit(ErrorPCS pcs, byte data) { }

      public void Visit(Ascii ascii, byte data) { }

      public void Visit(Braille braille, byte data) { }

      public void Visit(Integer integer, byte data) { }

      public void Visit(IntegerEnum intEnum, byte data) => GenerateOptions(intEnum);

      public void Visit(IntegerHex integer, byte data) { }

      public void Visit(EggSection section, byte data) => VisitEgg(section);

      public void Visit(EggItem item, byte data) => VisitEgg(item);

      private void VisitEgg(IDataFormatInstance eggFormat) {
         var eggRun = (EggMoveRun)Model.GetNextRun((eggFormat).Source);
         var allOptions = eggRun.GetAutoCompleteOptions();
         Result = AutoCompleteSelectionItem.Generate(allOptions.Where(option => option.MatchesPartial(InputText)), SelectionIndex);
      }

      public void Visit(PlmItem item, byte data) {
         if (!InputText.Contains(" ")) {
            Result = AutoCompleteSelectionItem.Generate(Enumerable.Empty<string>(), -1);
            return;
         }

         var moveName = InputText.Substring(InputText.IndexOf(' ')).Trim();
         if (moveName.Length == 0) {
            Result = AutoCompleteSelectionItem.Generate(Enumerable.Empty<string>(), -1);
            return;
         }

         var plmRun = (PLMRun)Model.GetNextRun(item.Source);
         var allOptions = plmRun.GetAutoCompleteOptions(InputText.Split(' ')[0]);
         Result = AutoCompleteSelectionItem.Generate(allOptions.Where(option => option.MatchesPartial(moveName)), SelectionIndex);
      }

      public void Visit(BitArray array, byte data) => GenerateOptions(array);

      public void Visit(EndStream stream, byte data) { }

      public void Visit(LzMagicIdentifier lz, byte data) { }

      public void Visit(LzGroupHeader lz, byte data) { }

      public void Visit(LzCompressed lz, byte data) { }

      public void Visit(LzUncompressed lz, byte data) { }

      public void Visit(UncompressedPaletteColor color, byte data) { }

      public void Visit(DataFormats.Tuple tuple, byte data) {
         var options = tuple.Model.GetAutocomplete(Model, InputText);

         if (options == null || options.Count == 0) {
            Result = new AutoCompleteSelectionItem[0];
         } else {
            Result = AutoCompleteSelectionItem.Generate(options, SelectionIndex);
         }

         foreach (var vm in Result) vm.IsFormatComplete = vm.CompletionText.EndsWith(")");
      }

      private void GenerateOptions(IDataFormatInstance format) {
         var arrayRun = (ITableRun)Model.GetNextRun(format.Source);
         var offsets = arrayRun.ConvertByteOffsetToArrayOffset(format.Source);
         var contentSegment = arrayRun.ElementContent[offsets.SegmentIndex];
         if (contentSegment is ArrayRunRecordSegment record) contentSegment = record.CreateConcrete(Model, format.Source);
         var segment = (IHasOptions)contentSegment;
         var allOptions = segment.GetOptions(Model).Where(option => option != null).Select(option => option + " ");
         Result = AutoCompleteSelectionItem.Generate(ArrayRunEnumSegment.GetBestOptions(allOptions, InputText, true), -1);
      }
   }
}
