using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.ObjectModel;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class CalculatedElementViewModel : ViewModelCore, IArrayElementViewModel {
      private IDataModel model;
      private ArrayRunCalculatedSegment segment;
      private int dataStart;

      public string Name => segment.Name;
      public string CalculatedValue {
         get {
            var table = segment.Model.GetNextRun(dataStart) as ITableRun;
            if (table == null) return string.Empty;
            var offset = table.ConvertByteOffsetToArrayOffset(dataStart);
            var value = segment.CalculatedValue(table.Start + table.ElementLength * offset.ElementIndex);

            var text = value.ToString();
            if (value == (int)value && segment.Enum != null) {
               var options = model.GetOptions(segment.Enum);
               if (value.InRange(0, options.Count)) text = options[(int)value];
            } else if (value != (int)value) {
               // rounding
               text = value.ToString("F2");
            }

            return text;
         }
      }
      public ObservableCollection<CalculatedElementViewModelOperand> Operands { get; private set; }
      public string Operator => segment.Operator;
      public bool HasOperator => segment.HasOperator;

      #region IArrayElementViewModel

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      private bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is CalculatedElementViewModel that)) return false;
         (segment, dataStart) = (that.segment, that.dataStart);
         this.model = that.model;
         Operands.Clear();
         foreach (var operand in that.Operands) Operands.Add(operand);
         NotifyPropertiesChanged(nameof(Name), nameof(CalculatedValue), nameof(Operator), nameof(HasOperator));
         return true;
      }

      #endregion
   }

   public class CalculatedElementViewModelOperand : ViewModelCore {
      public string Text { get; private set; }
   }
}
