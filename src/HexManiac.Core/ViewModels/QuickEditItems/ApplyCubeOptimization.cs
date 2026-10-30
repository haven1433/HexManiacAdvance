using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public enum CubeRomState { NotCube, PreviousCube, OptimizedCube }

   /// <summary>
   /// What makes a ROM "CUBE" and "optimized CUBE".
   ///
   /// CUBE: the game code is BPEE, the internal title (0xA0) is "POKEMON CUBE" and pokeemerald-expansion's "RHHEXP" header is in the first 0x400 bytes.
   /// Every CUBE build so far (perf29 and later) has all three.
   ///
   /// Optimized: the ROM holds the 16 bytes "CUBE_OPTIMIZED_1". A CUBE built with the optimization has them in its header, right after the RHHEXP header
   /// (src/rom_header_cube.c). A CUBE that was optimized in place by <see cref="ApplyCubeOptimization"/> has them in free space under the anchor
   /// <see cref="MarkerAnchor"/>. Both are found by a search of the whole ROM (a few milliseconds), so they also survive repointing.
   /// </summary>
   public static class CubeOptimization {
      public const string MarkerText = "CUBE_OPTIMIZED_1";
      public const string MarkerAnchor = "cube.optimized";
      public const string CubeTitle = "POKEMON CUBE";
      public const int TitleAddress = 0xA0, GameCodeAddress = 0xAC, HeaderSearchLength = 0x400;
      private static readonly byte[] markerBytes = Encoding.ASCII.GetBytes(MarkerText);
      private static readonly byte[] expansionMagic = Encoding.ASCII.GetBytes("RHHEXP");

      public static IReadOnlyList<byte> Marker => markerBytes;

      public static bool IsCube(byte[] data) {
         if (data == null || data.Length < HeaderSearchLength) return false;
         if (Encoding.ASCII.GetString(data, TitleAddress, CubeTitle.Length) != CubeTitle) return false;
         if (Encoding.ASCII.GetString(data, GameCodeAddress, 4) != "BPEE") return false;
         return new ReadOnlySpan<byte>(data, 0, HeaderSearchLength).IndexOf(new ReadOnlySpan<byte>(expansionMagic)) >= 0;
      }

      /// <returns>The address of the marker, or -1.</returns>
      public static int FindMarker(byte[] data) {
         if (data == null) return -1;
         return new ReadOnlySpan<byte>(data).IndexOf(new ReadOnlySpan<byte>(markerBytes));
      }

      public static bool SameBytes(byte[] a, byte[] b) => a != null && b != null && new ReadOnlySpan<byte>(a).SequenceEqual(new ReadOnlySpan<byte>(b));

      public static CubeRomState GetState(byte[] data) {
         if (!IsCube(data)) return CubeRomState.NotCube;
         return FindMarker(data) >= 0 ? CubeRomState.OptimizedCube : CubeRomState.PreviousCube;
      }

      /// <summary>One LZ77 stream that gets a smaller encoding of exactly the same data.</summary>
      public record Recode(int Start, int OldLength, byte[] NewData);

      /// <summary>
      /// For each LZ77 stream (start, length as the model sees it), re-encode it with <see cref="LzOptimalCompressor"/>.
      /// A stream is only kept when the new encoding is shorter and decompresses to exactly the same bytes as the old one.
      /// Streams that do not decompress cleanly (length errors, damaged data) are left alone. Works on a copy of the data, so it can run in the background.
      /// </summary>
      public static List<Recode> PlanRecompression(byte[] data, IReadOnlyList<(int start, int length)> streams) {
         var results = new Recode[streams.Count];
         Parallel.For(0, streams.Count, i => {
            var (start, length) = streams[i];
            if (start < 0 || length < 5 || start + length > data.Length) return;
            if (SmolDecoder.LooksLikeSmol(data, start)) return; // smol assets are not LZ77 streams: they stay as they are
            if (LZRun.IsCompressedLzData(data, start) != length) return;
            var original = LZRun.Decompress(data, start);
            if (original == null || original.Length == 0) return;
            var recoded = LzOptimalCompressor.Compress(original);
            if (recoded.Length >= length) return;
            var check = LZRun.Decompress(recoded, 0);
            if (check == null || !SameBytes(check, original)) return;
            if (LZRun.IsCompressedLzData(recoded, 0) != recoded.Length) return;
            results[i] = new Recode(start, length, recoded);
         });
         return results.Where(result => result != null).ToList();
      }
   }

   /// <summary>A quick-edit whose menu item is hidden for some ROMs, and that explains in a tooltip why it is grayed out.</summary>
   public interface IQuickEditMenuState {
      bool IsVisible(IViewPort viewPort);
      string ToolTip(IViewPort viewPort);
   }

   /// <summary>
   /// Utilities > Misc > "Apply CUBE optimization", right under "Apply CUBE".
   /// Shown only for CUBE ROMs; available for a CUBE from before the optimization, grayed out ("already optimized") for an optimized one.
   ///
   /// It works on the ROM in the tab, edited or not, through HexManiac's own knowledge of the ROM (its .toml):
   /// every LZ77-compressed asset the metadata knows (Pokemon/trainer pics, OW sprites, tilesets, tilemaps, item icons, ...) is re-encoded with the
   /// optimal LZ77 parse that the optimized CUBE build uses. The pictures stay exactly the same (each new stream is checked to decompress to the very same bytes
   /// before it is written), the pointers stay the same (each stream keeps its address), and the bytes that are no longer needed become free space (0xFF).
   /// Then it writes the optimized marker. It needs no patch file, so it also works on ROMs the user has edited.
   ///
   /// Safety: the ROM file on disk must match the tab (save first); a backup copy of the ROM and its .toml is written next to it before anything changes;
   /// every change is one undo step; nothing is saved to disk (File > Save does that).
   /// </summary>
   public class ApplyCubeOptimization : IQuickEditItem, IQuickEditMenuState {
      public const string BackupSuffix = ".before-cube-optimization";
      private readonly EditorViewModel editor;
      private readonly IFileSystem fileSystem;
      private bool running;

      private WeakReference<byte[]> checkedData;
      private long checkedStamp;
      private CubeRomState checkedState;

      public ApplyCubeOptimization(EditorViewModel editor, IFileSystem fileSystem) => (this.editor, this.fileSystem) = (editor, fileSystem);

      public string Name => "Apply CUBE optimization";

      public string Description => @"Makes a CUBE ROM from an earlier release smaller, without changing anything in the game.

Every compressed graphic that HexManiac knows in this ROM (Pokemon and trainer pictures, overworld sprites, tilesets, tilemaps, item icons, ...) is compressed again with the better LZ77 encoder that the optimized CUBE build uses. The pictures stay exactly the same (each one is checked byte for byte before it is written) and stay where they are, so no pointer changes. The bytes that are no longer needed become free space (FF) that you can use for your own additions.

It works on edited ROMs too. Before it changes anything it saves a backup copy of your ROM and its .toml next to it (name" + BackupSuffix + @".gba). The change is one undo step and is not saved until you use File > Save.

Only available for CUBE ROMs from before the optimization. It is grayed out when the ROM is already optimized.";

      public string WikiLink => string.Empty;

      public event EventHandler CanRunChanged;

      public CubeRomState GetState(IViewPort viewPort) {
         if (viewPort is not ViewPort tab || tab.Model?.RawData is not byte[] data) return CubeRomState.NotCube;
         var stamp = ModelEditStamp.Current;
         if (stamp == checkedStamp && checkedData != null && checkedData.TryGetTarget(out var known) && ReferenceEquals(known, data)) return checkedState;
         var state = CubeOptimization.GetState(data);
         (checkedData, checkedStamp, checkedState) = (new WeakReference<byte[]>(data), stamp, state);
         return state;
      }

      public bool CanRun(IViewPort viewPort) => !running && GetState(viewPort) == CubeRomState.PreviousCube;

      public bool IsVisible(IViewPort viewPort) => GetState(viewPort) != CubeRomState.NotCube;

      public string ToolTip(IViewPort viewPort) => GetState(viewPort) switch {
         CubeRomState.OptimizedCube => "This ROM is already optimized.",
         CubeRomState.PreviousCube => "Re-compresses this CUBE ROM's graphics with the optimized encoder (pictures stay identical) and frees the saved space.",
         _ => "Only for CUBE ROMs.",
      };

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public async Task<ErrorInfo> Run(IViewPort viewPort) {
         if (running) return new ErrorInfo("The CUBE optimization is already running.", isWarningLevel: true);
         running = true;
         try {
            return await RunInternal(viewPort);
         } catch (Exception ex) {
            return new ErrorInfo("Could not apply the CUBE optimization: " + ex.Message + Environment.NewLine + "Nothing was changed.");
         } finally {
            running = false;
            TabChanged();
         }
      }

      private async Task<ErrorInfo> RunInternal(IViewPort viewPort) {
         if (viewPort is not ViewPort tab || tab.Model is not IDataModel model || model.RawData == null) {
            return new ErrorInfo("Apply CUBE optimization works on the CUBE ROM that is open in the current tab.");
         }
         var data = model.RawData;
         switch (CubeOptimization.GetState(data)) {
            case CubeRomState.NotCube: return new ErrorInfo("This ROM is not CUBE (its header is not 'POKEMON CUBE' / BPEE with the pokeemerald-expansion header), so nothing was changed.");
            case CubeRomState.OptimizedCube: return new ErrorInfo("This ROM is already optimized, so nothing was changed.", isWarningLevel: true);
         }
         if (!tab.ChangeHistory.IsSaved) {
            return new ErrorInfo("This tab has changes that are not saved yet. Save them first (File > Save), then run Apply CUBE optimization, so that the backup it makes is your current ROM.");
         }

         // 1. The file on disk must be what the tab shows: it becomes the backup.
         var fileName = tab.FileName;
         if (string.IsNullOrEmpty(fileName) || !fileSystem.Exists(fileName)) {
            return new ErrorInfo("Save the ROM to a file first (File > Save As): the optimization makes a backup of the file next to it before it changes anything.");
         }
         var onDisk = fileSystem.LoadFile(fileName)?.Contents;
         if (onDisk == null || !CubeOptimization.SameBytes(onDisk, data)) {
            return new ErrorInfo($"{Path.GetFileName(fileName)} on disk is not the same as the ROM in this tab (was it changed by another program?). Close the tab and open the ROM again, then run Apply CUBE optimization. Nothing was changed.");
         }

         // 2. Plan in the background, on a copy: re-encode every LZ77 stream the metadata knows.
         var streams = model.All<LZRun>().Where(run => run.Length > 0 && !run.HasLengthErrors).Select(run => (run.Start, run.Length)).ToList();
         var snapshot = (byte[])data.Clone();
         var stamp = ModelEditStamp.Current;
         editor.ShowStatus($"Optimizing {Path.GetFileName(fileName)}: re-compressing {streams.Count:N0} graphics...");
         List<CubeOptimization.Recode> plan = null;
         await editor.Singletons.WorkDispatcher.RunBackgroundWork(() => plan = CubeOptimization.PlanRecompression(snapshot, streams));
         editor.ShowStatus(string.Empty);
         if (!ReferenceEquals(model.RawData, data) || !CubeOptimization.SameBytes(model.RawData, snapshot) || !tab.ChangeHistory.IsSaved) {
            return new ErrorInfo("The ROM in this tab was changed while the optimization was being prepared, so nothing was changed. Run it again.");
         }

         // 3. Backup of the ROM (and its .toml) next to it. If that fails, stop.
         var backupName = BackupName(fileName);
         if (!fileSystem.Save(new LoadedFile(backupName, onDisk)) || !fileSystem.Exists(backupName)) {
            return new ErrorInfo($"Could not write the backup {Path.GetFileName(backupName)}, so nothing was changed.");
         }
         var metadata = fileSystem.MetadataFor(fileName);
         if (metadata != null) fileSystem.SaveMetadata(backupName, metadata);

         // 4. Write: each stream keeps its address; the rest of its old space becomes FF. One undo step.
         var token = tab.ChangeHistory.CurrentChange;
         int written = 0, freed = 0;
         foreach (var recode in plan) {
            if (model.GetNextRun(recode.Start) is not LZRun run || run.Start != recode.Start || run.Length != recode.OldLength) continue;
            for (int i = 0; i < recode.NewData.Length; i++) token.ChangeData(model, recode.Start + i, recode.NewData[i]);
            for (int i = recode.NewData.Length; i < recode.OldLength; i++) token.ChangeData(model, recode.Start + i, 0xFF);
            model.ObserveRunWritten(token, run.Duplicate(recode.Start, run.PointerSources));
            written++;
            freed += recode.OldLength - recode.NewData.Length;
         }

         // 5. The marker, in free space, under an anchor so that it is visible (and kept by the .toml).
         var markerAddress = model.FindFreeSpace(model.FreeSpaceStart, CubeOptimization.Marker.Count);
         if (markerAddress < 0 || markerAddress + CubeOptimization.Marker.Count > model.Count) {
            Revert(tab);
            return new ErrorInfo("There is no free space left for the 16 bytes of the optimized marker, so nothing was changed.");
         }
         for (int i = 0; i < CubeOptimization.Marker.Count; i++) token.ChangeData(model, markerAddress + i, CubeOptimization.Marker[i]);
         model.ObserveAnchorWritten(token, CubeOptimization.MarkerAnchor, new AsciiRun(model, markerAddress, CubeOptimization.Marker.Count));

         // 6. Check what was written: every stream must still read as the same LZ77 run with the planned bytes. Otherwise take everything back.
         foreach (var recode in plan) {
            if (model.GetNextRun(recode.Start) is not LZRun run || run.Start != recode.Start) continue; // skipped above
            if (run.Length != recode.NewData.Length || !new ReadOnlySpan<byte>(model.RawData, recode.Start, recode.NewData.Length).SequenceEqual(new ReadOnlySpan<byte>(recode.NewData))) {
               Revert(tab);
               return new ErrorInfo($"The re-compressed data at {recode.Start:X6} did not read back correctly, so the optimization was taken back. Nothing was changed.");
            }
         }
         if (CubeOptimization.GetState(model.RawData) != CubeRomState.OptimizedCube) {
            Revert(tab);
            return new ErrorInfo("The optimized marker could not be written, so the optimization was taken back. Nothing was changed.");
         }

         tab.ChangeHistory.ChangeCompleted();
         tab.Refresh();
         TabChanged();
         return new ErrorInfo(
            $"Optimized {Path.GetFileName(fileName)}: {written:N0} of {streams.Count:N0} compressed graphics re-compressed, {freed:N0} bytes freed (now FF). The pictures are unchanged. " +
            $"A backup is in {Path.GetFileName(backupName)}. It is not saved yet: File > Save keeps it, Undo takes it back.", isWarningLevel: true);
      }

      private static void Revert(ViewPort tab) {
         tab.ChangeHistory.ChangeCompleted();
         if (tab.ChangeHistory.Undo.CanExecute(null)) tab.ChangeHistory.Undo.Execute();
      }

      /// <summary>MyRom.gba -> MyRom.before-cube-optimization.gba, then MyRom.before-cube-optimization (2).gba, ... if that exists.</summary>
      public string BackupName(string fileName) {
         var directory = Path.GetDirectoryName(fileName) ?? string.Empty;
         var name = Path.GetFileNameWithoutExtension(fileName);
         var extension = Path.GetExtension(fileName);
         if (string.IsNullOrEmpty(extension)) extension = ".gba";
         var candidate = Path.Combine(directory, name + BackupSuffix + extension);
         for (int i = 2; fileSystem.Exists(candidate) && i < 1000; i++) candidate = Path.Combine(directory, $"{name}{BackupSuffix} ({i}){extension}");
         return candidate;
      }
   }
}
