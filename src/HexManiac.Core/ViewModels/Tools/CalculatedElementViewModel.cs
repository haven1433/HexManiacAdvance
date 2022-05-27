using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class CalculatedElementViewModel : ViewModelCore, IArrayElementViewModel {
      private ArrayRunCalculatedSegment segment;
      private int dataStart;

      public string Name => segment.Name;
      public int CalculatedValue {
         get {
            var table = segment.Model.GetNextRun(dataStart) as ITableRun;
            if (table == null) return 0;
            var offset = table.ConvertByteOffsetToArrayOffset(dataStart);
            var value = segment.CalculatedValue(table.Start + table.ElementLength * offset.ElementIndex);
            return value;
         }
      }
      public ObservableCollection<CalculatedElementViewModelOperand> Operands { get; private set; }
      public string Operator => segment.Operator;
      public bool HasOperator => segment.HasOperator;

      public CalculatedElementViewModel(IViewPort viewPort, ArrayRunCalculatedSegment segment, int start) {
         (this.segment, dataStart) = (segment, start);
         Operands = new ObservableCollection<CalculatedElementViewModelOperand>();
         foreach (var operand in segment.Operands) Operands.Add(new CalculatedElementViewModelOperand(operand, viewPort, start));
      }

      #region IArrayElementViewModel

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
         Operands.Clear();
         foreach (var operand in that.Operands) Operands.Add(operand);
         NotifyPropertyChanged(nameof(CalculatedValue));
         return true;
      }

      #endregion
   }

   public class CalculatedElementViewModelOperand : ViewModelCore {
      public string Text { get; private set; }
      public ICommand JumpTo { get; private set; }

      public CalculatedElementViewModelOperand(string text, IViewPort viewPort, int sourceAddress) {
         Text = text;
         JumpTo = new StubCommand();

         var table = viewPort.Model.GetNextRun(sourceAddress) as ITableRun;
         if (table != null && table.Start <= sourceAddress) {
            var offset = table.ConvertByteOffsetToArrayOffset(sourceAddress);
            var destination = ArrayRunCalculatedSegment.CalculateSource(viewPort.Model, table, offset.ElementIndex, Text);
            if (destination != Pointer.NULL) {
               JumpTo = new StubCommand { Execute = arg => viewPort.Goto.Execute(destination), CanExecute = arg => true };
            }
         }
      }
   }
}
