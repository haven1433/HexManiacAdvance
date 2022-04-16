using HavenSoft.HexManiac.Core.Models;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ExpandRom : IQuickEditItem {
      public string Name => "Expand Rom";

      public string Description => "Change the size of the gba file (make it bigger or smaller).";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Rom-Expansion-Explained";

      public IFileSystem FileSystem { get; }

      public event EventHandler CanRunChanged;

      public ExpandRom(IFileSystem fs) => FileSystem = fs;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort && viewPort.FileName.EndsWith(".gba");
      }

      public Task<ErrorInfo> Run(IViewPort viewPort) {
         var editableViewPort = (IEditableViewPort)viewPort;
         var text = FileSystem.RequestText(
            "New File Length",
            $"What length (in hex) would you like to expand your file to?{Environment.NewLine}If you choose a length shorter than the current length, the file will be truncated.");
         if (text == null) return Task.FromResult(ErrorInfo.NoError);
         if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var length)) {
            return Task.FromResult(new ErrorInfo($"{text} is not a valid hex length"));
         } else if (length < 0x100000) {
            return Task.FromResult(new ErrorInfo($"The file must be at least 0x100_000 bytes long."));
         } else if (length > 0x2000000) {
            return Task.FromResult(new ErrorInfo($"GBA games can only be 0x2_000_000 bytes long."));
         }

         viewPort.Model.ExpandData(editableViewPort.ChangeHistory.CurrentChange, length - 1);
         viewPort.Model.ContractData(editableViewPort.ChangeHistory.CurrentChange, length - 1);
         viewPort.Refresh();

         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() {
         CanRunChanged?.Invoke(this, EventArgs.Empty);
      }
   }
}
