using HavenSoft.HexManiac.Core.Models;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ExpandRom : IQuickEditItem {
      public string Name => "Expand Rom";

      public string Description => "Change the size of the gba file (make it bigger or smaller)." + Environment.NewLine +
         "Warning: Decreasing the size of your ROM will permanently delete some data." + Environment.NewLine +
         "Edit your ROM size carefully!";

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
            Environment.NewLine.Join(new[] {
            "What length would you like to expand your file to?",
            "If you choose a length shorter than the current length, the file will be truncated.",
            "Enter a number of bytes (hexadecimal) or megabytes (decimal)."
         }));
         if (text == null) return Task.FromResult(ErrorInfo.NoError);

         if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var length)) {
            return Task.FromResult(new ErrorInfo($"{text} is not a valid hex length"));
         }
         if (length < 1) length = 1;
         if (length < 0x100000) {
            if (!uint.TryParse(text, out length)) Task.FromResult(new ErrorInfo($"The file must be at least 0x100_000 bytes long."));
            length *= 1024 * 1024; // assume the number is in decimal MBs
         }
         if (length > 0x2000000) {
            return Task.FromResult(new ErrorInfo($"GBA games can only be 0x2_000_000 bytes (32 MB) long."));
         }

         viewPort.Model.ExpandData(editableViewPort.ChangeHistory.CurrentChange, (int)(length - 1));
         viewPort.Model.ContractData(editableViewPort.ChangeHistory.CurrentChange, (int)(length - 1));
         viewPort.Refresh();

         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() {
         CanRunChanged?.Invoke(this, EventArgs.Empty);
      }
   }
}
