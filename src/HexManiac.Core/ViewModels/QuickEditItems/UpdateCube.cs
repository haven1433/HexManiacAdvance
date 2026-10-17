using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.CubeUpdate;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Utilities > Misc > "Update CUBE".
   /// Brings a hacked ROM that was made from an older CUBE up to the current CUBE: the tab becomes the current CUBE (every new feature) with what the author changed carried over.
   /// It needs the original Pokemon Emerald (USA, Europe) ROM once, to build the current CUBE from the patch that ships with the program.
   /// Like "Apply CUBE" it does not save by itself: the tab is marked as changed, File > Save replaces the ROM, File > Save As keeps the old one.
   /// </summary>
   public class UpdateCube : IQuickEditItem {
      private readonly EditorViewModel editor;
      private readonly ICubeResources resources;
      private readonly IFileSystem fileSystem;
      private readonly VanillaRomSpec vanilla;
      private bool running, statusShown;

      public string Name => "Update CUBE";

      public string Description => @"Moves a ROM that you already hacked onto the newest CUBE.

CUBE changes with every release. If you started hacking an older CUBE, this brings the ROM in this tab up to the current CUBE: you get all of the new features, and the changes you made (stats, names, text, maps, trainers, scripts, sprites, new table rows and so on) are carried over. New data of yours is moved to free space and everything that points to it is repointed.

You need the original Pokemon Emerald (USA, Europe) ROM once: Update CUBE asks for it and builds the new CUBE from it. The .toml of your ROM (also the one of an old version) is used to find what you changed.

Nothing is written to disk yet. The tab becomes the updated ROM and is marked as changed: File > Save replaces your ROM (and its .toml), File > Save As keeps your old ROM. To go back, close the tab without saving.

What can not be carried over: changes made to the game's code (assembly or C hacks) and data that your .toml does not describe. A report lists what was not carried over.";

      public string WikiLink => string.Empty;

      public event EventHandler CanRunChanged;

      public UpdateCube(EditorViewModel editor, IFileSystem fileSystem, ICubeResources resources = null, VanillaRomSpec vanilla = null) {
         this.editor = editor;
         this.fileSystem = fileSystem;
         this.resources = resources ?? new ShippedCubeResources();
         this.vanilla = vanilla ?? VanillaRomSpec.EmeraldUsV10;
      }

      /// <summary>A CUBE ROM of the full size: the title, the game code and the expansion header are CUBE's.</summary>
      public bool CanRun(IViewPort viewPort) {
         if (viewPort is not ViewPort tab) return false;
         var data = tab.Model?.RawData;
         return data != null && data.Length == 0x2000000 && CubeOptimization.IsCube(data);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public async Task<ErrorInfo> Run(IViewPort viewPort) {
         if (running) return new ErrorInfo("CUBE is already being updated.", isWarningLevel: true);
         running = true;
         statusShown = false;
         ErrorInfo result;
         try {
            result = await RunInternal(viewPort);
         } catch (CubeUpdateException ex) {
            result = new ErrorInfo(ex.Message);
         } catch (Exception ex) {
            result = new ErrorInfo("Could not update CUBE: " + ex.Message);
         } finally {
            running = false;
         }
         if (statusShown && !(result.HasError && result.IsWarning)) editor.ShowStatus(string.Empty);
         return result;
      }

      private async Task<ErrorInfo> RunInternal(IViewPort viewPort) {
         var dispatcher = editor.Singletons.WorkDispatcher;
         if (viewPort is not ViewPort tab || tab.Model == null) return new ErrorInfo("Update CUBE works on the CUBE ROM that is open in the current tab.");
         await dispatcher.WaitForRenderingAsync();

         var patchFile = resources.ReadPatch();
         var tomlLines = resources.ReadToml();
         var versionsFile = resources.ReadVersions();
         if (patchFile == null || tomlLines == null || versionsFile == null) {
            return new ErrorInfo(
               $"The CUBE files are missing: '{ShippedCubeResources.PatchFileName}', '{ShippedCubeResources.TomlFileName}' and '{ShippedCubeResources.VersionsFileName}' must be in{Environment.NewLine}" +
               $"{resources.Location}{Environment.NewLine}They come with the release version of this program (in its 'resources' folder). Development builds do not include them.");
         }
         var patch = BpsPatch.DecompressIfGzip(patchFile);
         var info = BpsPatch.ReadInfo(patch);
         if (info == null) return new ErrorInfo("The CUBE patch that comes with this program is damaged (it is not a BPS patch).");
         var versions = CubeFingerprint.Read(versionsFile);

         // the original Emerald
         var file = fileSystem.OpenFile("Pokemon Emerald (USA, Europe) ROM", "gba");
         if (file == null) return new ErrorInfo("Update CUBE was cancelled: it needs the original Pokemon Emerald (USA, Europe) ROM to build the new CUBE.", isWarningLevel: true);
         var check = EmeraldRomCheck.Check(file.Contents, vanilla, info);
         if (!check.IsVanilla) return new ErrorInfo(check.Message);

         // the author's ROM as it is in the tab (also what is not saved yet), with its metadata
         var model = tab.Model;
         var userRom = (byte[])model.RawData.Clone();
         var references = editor.Singletons.GameReferenceTables.TryGetValue(model.GetGameCode(), out var table) ? table : null;
         var userToml = model.ExportMetadata(references, editor.Singletons.MetadataInfo).Serialize();
         var emerald = (byte[])file.Contents.Clone();

         statusShown = true;
         editor.ShowStatus("Updating CUBE, this takes about a minute...");
         BpsApplyResult applied = null;
         await dispatcher.RunBackgroundWork(() => applied = BpsPatch.Apply(patch, emerald));
         if (!applied.Success) return new ErrorInfo("Could not build the new CUBE: " + applied.Message);

         CubeUpdateResult updated = null;
         await dispatcher.RunBackgroundWork(() => updated = CubeUpdateService.Update(editor.Singletons, userRom, userToml, applied.Target, tomlLines, versions, text => _ = dispatcher.DispatchWork(() => editor.ShowStatus(text))));

         if (!CubeOptimization.SameBytes(tab.Model.RawData, userRom)) {
            return new ErrorInfo("The ROM in this tab was changed while it was being updated, so nothing was applied. Run Update CUBE again.");
         }
         var fileName = Path.GetFileName(tab.FileName);
         var newTab = editor.ReplaceRomTab(tab, updated.Rom, updated.Toml);
         if (newTab == null) return new ErrorInfo("The tab was closed while CUBE was being updated, so nothing was applied.");

         var report = updated.Report.ToString().TrimEnd();
         return new ErrorInfo(
            $"Updated {fileName} from {updated.Report.DetectedVersion} to the current CUBE. It is not saved yet: File > Save replaces {fileName}, File > Save As keeps the old ROM. Close the tab without saving to go back." +
            Environment.NewLine + report, isWarningLevel: true);
      }
   }
}
