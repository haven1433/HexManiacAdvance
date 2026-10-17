using HavenSoft.HexManiac.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Utilities > Misc > "Apply CUBE".
   /// Turns the unmodified Pokemon Emerald (USA, Europe) ROM that is open in the current tab into CUBE (pokeemerald-expansion, built with all of its features)
   /// by applying the BPS patch that ships with the program, and loads CUBE's HexManiac metadata (the .toml that ships with the program) for the result.
   ///
   /// It works like "Apply HUBOL DPE CFRU": it applies to the ROM that is open, and the menu item is grayed out unless that ROM is the one the patch was made for.
   /// Unlike HUBOL it does not save by itself: the tab shows CUBE (32 MB, with its anchors and tables) and is marked as changed,
   /// so the user decides between File > Save (replaces the ROM and its .toml) and File > Save As (keeps the original untouched).
   /// A 32 MB change can not be recorded in the undo history, so there is no undo; closing the tab without saving takes it back.
   /// </summary>
   public class ApplyCubePatch : IQuickEditItem {
      private readonly EditorViewModel editor;
      private readonly ICubeResources resources;
      private readonly VanillaRomSpec vanilla;
      private bool running, statusShown;

      // CanRun is asked every time the user changes tabs (and when the open ROM is edited or saved), so what it learned about one ROM is remembered until anything changes.
      private WeakReference<byte[]> checkedData; // weak: the item must not keep the 16 MB of a closed tab alive
      private long checkedStamp;
      private bool checkedResult;

      public string Name => "Apply CUBE";

      public string Description => @"Cradi's Ultimate Build of Emerald (CUBE).

Turns the Pokemon Emerald (USA, Europe) ROM that is open in this tab into CUBE: the pokeemerald-expansion based build, with all of its features. It also loads CUBE's HexManiac metadata (the .toml file), so its tables and names show up right away.

Nothing is written to disk yet. The tab becomes CUBE (32 MB) and is marked as changed: File > Save replaces your ROM (and its .toml) with CUBE, File > Save As keeps your original ROM. Undo can not take CUBE back out; to go back, close the tab without saving.

This is only available while the open ROM is an unmodified Pokemon Emerald (USA, Europe).";

      public string WikiLink => string.Empty;

      public event EventHandler CanRunChanged;

      public ApplyCubePatch(EditorViewModel editor, ICubeResources resources = null, VanillaRomSpec vanilla = null) {
         this.editor = editor;
         this.resources = resources ?? new ShippedCubeResources();
         this.vanilla = vanilla ?? VanillaRomSpec.EmeraldUsV10;
      }

      /// <summary>
      /// True if the tab holds exactly the ROM that the CUBE patch was made for: same length, same game code and same content.
      /// The length and the game code are looked at first, because every other file fails them without being read.
      /// An already patched CUBE ROM, an expanded or edited Emerald, FireRed, Ruby and so on are all "no".
      /// </summary>
      public bool CanRun(IViewPort viewPort) {
         if (viewPort is not ViewPort tab) return false;
         var data = tab.Model?.RawData;
         if (data == null || data.Length != vanilla.Length) return false;
         if (EmeraldRomCheck.ReadGameCode(data) != vanilla.GameCode) return false;

         var stamp = ModelEditStamp.Current; // goes up whenever a byte of any model changes
         if (stamp == checkedStamp && checkedData != null && checkedData.TryGetTarget(out var known) && ReferenceEquals(data, known)) return checkedResult;
         // The CRC32 of 16 MB takes a few milliseconds. Without a known CRC32, fall back to the SHA-1 (a tenth of a second).
         var result = vanilla.Crc32 != 0 ? (uint)Patcher.CalcCRC32(data) == vanilla.Crc32 : EmeraldRomCheck.Check(data, vanilla).IsVanilla;
         checkedData = new WeakReference<byte[]>(data);
         checkedStamp = stamp;
         checkedResult = result;
         return result;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public async Task<ErrorInfo> Run(IViewPort viewPort) {
         if (running) return new ErrorInfo("CUBE is already being applied.", isWarningLevel: true);
         running = true;
         statusShown = false;
         ErrorInfo result;
         try {
            result = await RunInternal(viewPort);
         } catch (Exception ex) {
            result = new ErrorInfo("Could not apply CUBE: " + ex.Message);
         } finally {
            running = false;
         }
         // the "Applying CUBE..." status must not stay on screen next to an error (a success message replaces it by itself)
         if (statusShown && !(result.HasError && result.IsWarning)) editor.ShowStatus(string.Empty);
         return result;
      }

      private async Task<ErrorInfo> RunInternal(IViewPort viewPort) {
         var dispatcher = editor.Singletons.WorkDispatcher;
         if (viewPort is not ViewPort tab || tab.Model == null) {
            return new ErrorInfo("Apply CUBE works on the ROM that is open in the current tab. Open a Pokemon Emerald (USA, Europe) ROM first.");
         }

         // Let the dialog that started this command close before we start working.
         await dispatcher.WaitForRenderingAsync();

         // 1. The patch and the .toml that ship with the program.
         var patchFile = resources.ReadPatch();
         var tomlLines = resources.ReadToml();
         if (patchFile == null || tomlLines == null) {
            return new ErrorInfo(
               $"The CUBE files are missing: '{ShippedCubeResources.PatchFileName}' and '{ShippedCubeResources.TomlFileName}' were not found in{Environment.NewLine}" +
               $"{resources.Location}{Environment.NewLine}" +
               "They come with the release version of this program (in its 'resources' folder). Development builds do not include them.");
         }
         var patch = BpsPatch.DecompressIfGzip(patchFile);
         var info = BpsPatch.ReadInfo(patch);
         if (info == null) return new ErrorInfo("The CUBE patch that comes with this program is damaged (it is not a BPS patch).");

         // 2. The open ROM must be the original Emerald (the menu item is grayed out otherwise, but the menu can be a moment behind).
         var data = tab.Model.RawData;
         var check = EmeraldRomCheck.Check(data, vanilla, info);
         if (!check.IsVanilla) return new ErrorInfo(check.Message);
         if (!tab.ChangeHistory.IsSaved) {
            return new ErrorInfo(
               "This tab has changes that are not saved yet (for example names or formats you added). Applying CUBE replaces everything in the tab," + Environment.NewLine +
               "so save them first (File > Save) or close the tab and open the ROM again, then run Apply CUBE.");
         }
         var sourceCrc = Patcher.CalcCRC32(data);
         var source = (byte[])data.Clone(); // the tab stays usable while we work in the background

         // 3. Apply the patch (about 32 MB of output) without freezing the window. The patch checks the checksums of the ROM it reads and of the ROM it makes.
         statusShown = true;
         editor.ShowStatus($"Applying CUBE to {Path.GetFileName(tab.FileName)} ({FormatMegabytes(info.TargetSize)}), this takes a few seconds...");
         BpsApplyResult applied = null;
         await dispatcher.RunBackgroundWork(() => applied = BpsPatch.Apply(patch, source));
         if (!applied.Success) return new ErrorInfo("Could not build CUBE: " + applied.Message);

         // 4. The tab was free while we worked: if it was edited, closed or replaced meanwhile, leave everything as it is.
         if (!ReferenceEquals(tab.Model.RawData, data) || Patcher.CalcCRC32(tab.Model.RawData) != sourceCrc || !tab.ChangeHistory.IsSaved) {
            return new ErrorInfo("The ROM in this tab was changed while CUBE was being built, so nothing was applied. Run Apply CUBE again.");
         }

         // 5. The tab shows CUBE with its metadata, in the same place and under the same file name, and is marked as changed. Nothing is written.
         var fileName = Path.GetFileName(tab.FileName);
         var cubeTab = editor.ReplaceRomTab(tab, applied.Target, tomlLines);
         if (cubeTab == null) return new ErrorInfo("The tab was closed while CUBE was being built, so nothing was applied.");

         return new ErrorInfo(
            $"Applied CUBE to {fileName} ({FormatMegabytes(applied.Target.Length)}). It is not saved yet: File > Save replaces {fileName} with CUBE, File > Save As keeps your original. " +
            "Close the tab without saving to go back.", isWarningLevel: true);
      }

      private static string FormatMegabytes(long length) => length >= (1 << 20) ? $"{length >> 20} MB" : $"{length:N0} bytes";
   }
}
