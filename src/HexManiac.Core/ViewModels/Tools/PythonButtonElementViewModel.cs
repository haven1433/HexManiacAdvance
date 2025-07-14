using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PythonButtonElementViewModel : ViewModelCore, IArrayElementViewModel {
      public readonly ArrayRunPythonButtonSegment segment;
      public int elementAddress;

      public string theme;
      public string Theme { get => theme; set => Set(ref theme, value); }

      public bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public string name;
      public string Name { get => name; set => Set(ref name, value); }

      public string tooltip;

      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is PythonButtonElementViewModel button && button.segment.Name == Name) {
            Name = button.segment.Name;
            elementAddress = button.elementAddress;
            return true;
         }
         return false;
      }
   }
}
