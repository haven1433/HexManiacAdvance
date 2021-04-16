using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class CalculatedElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly IViewPort viewPort;
      private ArrayRunCalculatedSegment segment;
      private int dataStart;

      public int CalculatedValue {
         get {
            var table = segment.Model.GetNextRun(dataStart) as ITableRun;
            if (table == null) return 0;
            var offset = table.ConvertByteOffsetToArrayOffset(dataStart);
            return segment.CalculatedValue(offset.ElementIndex);
         }
      }
      public string LeftOperand => segment.Left;
      public string RightOperand => segment.Right;
      public string Operator => segment.Operator;
      public bool HasOperator => segment.HasOperator;

      private StubCommand jumpToLeft, jumpToRight;
      public ICommand JumpToLeft => StubCommand(ref jumpToLeft, () => {
         var table = segment.Model.GetNextRun(dataStart) as ITableRun;
         if (table == null) return;
         var offset = table.ConvertByteOffsetToArrayOffset(dataStart);
         viewPort.Goto.Execute(ArrayRunCalculatedSegment.CalculateSource(segment.Model, table, offset.ElementIndex, segment.Left));
      });
      public ICommand JumpToRight => StubCommand(ref jumpToRight, () => {
         var table = segment.Model.GetNextRun(dataStart) as ITableRun;
         if (table == null) return;
         var offset = table.ConvertByteOffsetToArrayOffset(dataStart);
         viewPort.Goto.Execute(ArrayRunCalculatedSegment.CalculateSource(segment.Model, table, offset.ElementIndex, segment.Right));
      }, () => HasOperator);

      public CalculatedElementViewModel(IViewPort viewPort, ArrayRunCalculatedSegment segment, int start) {
         this.viewPort = viewPort;
         (this.segment, dataStart) = (segment, start);
      }

      #region IArrayElementViewModel

      private bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public event EventHandler DataChanged;

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is CalculatedElementViewModel that)) return false;
         (segment, dataStart) = (that.segment, that.dataStart);
         return true;
      }

      #endregion
   }
}
