using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PythonButtonElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly ViewPort viewPort;
      private readonly ArrayRunPythonButtonSegment segment;
      private int elementAddress;

      private string theme;
      public string Theme { get => theme; set => Set(ref theme, value); }

      private bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      private string name;
      public string Name { get => name; set => Set(ref name, value); }

      private string tooltip;
      public string Tooltip {
         get {
            if (tooltip != null) return tooltip;
            if (viewPort.PythonTool.HasFunction(segment.FunctionName)) {
               tooltip = viewPort.PythonTool.GetComment(segment.FunctionName);
            }
            return tooltip;
         }
      }

      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public PythonButtonElementViewModel(ViewPort viewPort, ArrayRunPythonButtonSegment segment, int elementAddress) {
         this.viewPort = viewPort;
         this.segment = segment;
         this.elementAddress = elementAddress;
         this.name = segment.Name;
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is PythonButtonElementViewModel button && button.segment.Name == Name) {
            Name = button.segment.Name;
            elementAddress = button.elementAddress;
            return true;
         }
         return false;
      }

      public bool CanExecute() => viewPort.PythonTool.HasFunction(segment.FunctionName);

      public void Execute() {
         if (viewPort.Model.GetNextRun(elementAddress) is not ITableRun tableRun) return;
         var elementIndex = tableRun.ConvertByteOffsetToArrayOffset(elementAddress).ElementIndex;
         viewPort.PythonTool.AddVariable("element", new ModelTable(viewPort.Model, tableRun, () => viewPort.CurrentChange)[elementIndex]);
         var error = viewPort.PythonTool.RunPythonScript($"{segment.FunctionName}(element)");
         if (error.HasError && !error.IsWarning) {
            viewPort.RaiseError(error.ErrorMessage);
         }
         viewPort.Refresh();
      }
   }
}
