using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HavenSoft.HexManiac.Core.Models.CubeUpdate {
   public class CubeUpdateResult {
      public byte[] Rom { get; set; }
      public string[] Toml { get; set; }
      public CubeUpdateReport Report { get; set; }
   }

   /// <summary>
   /// Everything "Update CUBE" does that does not need a window: find which CUBE version a hacked ROM comes from, merge its edits into the new CUBE, and write the metadata for the result.
   /// </summary>
   public static class CubeUpdateService {
      public const string CurrentName = "the current CUBE";
      /// <summary>Less of the ROM than this agrees with a known version: it is not a CUBE that this program knows.</summary>
      public const double MinimumAgreement = 0.80;

      /// <summary>A model of a ROM and its metadata lines, the way File > Open makes it.</summary>
      public static IDataModel Load(Singletons singletons, byte[] rom, string[] tomlLines) {
         var model = new HardcodeTablesModel(singletons, rom, tomlLines == null ? null : new StoredMetadata(tomlLines));
         model.InitializationWorkload.Wait();
         return model;
      }

      public static string[] Export(Singletons singletons, IDataModel model) {
         var references = singletons.GameReferenceTables.TryGetValue(model.GetGameCode(), out var table) ? table : null;
         return model.ExportMetadata(references, singletons.MetadataInfo).Serialize();
      }

      /// <summary>The graph walk is recursive and the ROM is deep: it runs on a thread with a big stack.</summary>
      public static void OnBigStack(Action action) {
         Exception error = null;
         var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }, 512 * 1024 * 1024);
         thread.Start();
         thread.Join();
         if (error != null) throw error is CubeUpdateException ? error : new CubeUpdateException(error.Message);
      }

      public static Dictionary<ulong, ulong> Fingerprint(IDataModel model) => CubeFingerprint.Compute(new RomGraph(model));

      /// <param name="userRom">The hacked ROM, as it is now.</param>
      /// <param name="userToml">The metadata of the hacked ROM (old versions of the .toml are fine: tables that it does not know are simply taken from the new CUBE).</param>
      /// <param name="newRom">The new CUBE, built from Emerald by the patch that ships with the program.</param>
      /// <param name="newToml">The .toml that ships with the new CUBE.</param>
      /// <param name="versions">What the older CUBE versions look like (CubeFingerprint).</param>
      public static CubeUpdateResult Update(Singletons singletons, byte[] userRom, string[] userToml, byte[] newRom, string[] newToml, IReadOnlyList<CubeFingerprint.VersionDelta> versions, Action<string> progress = null) {
         CubeUpdateResult result = null;
         OnBigStack(() => result = UpdateInternal(singletons, userRom, userToml, newRom, newToml, versions, progress ?? (_ => { })));
         return result;
      }

      private static CubeUpdateResult UpdateInternal(Singletons singletons, byte[] userRom, string[] userToml, byte[] newRom, string[] newToml, IReadOnlyList<CubeFingerprint.VersionDelta> versions, Action<string> progress) {
         progress("Reading your ROM...");
         var user = Load(singletons, userRom, userToml);
         progress("Reading the new CUBE...");
         var target = Load(singletons, (byte[])newRom.Clone(), newToml);
         var current = Fingerprint(target);
         var userFingerprint = Fingerprint(user);

         var detections = CubeFingerprint.Detect(userFingerprint, current, versions, CurrentName);
         var best = detections[0];
         var agreement = best.Total == 0 ? 0 : (double)best.Matches / best.Total;
         if (agreement < MinimumAgreement) {
            throw new CubeUpdateException($"This ROM is not one of the CUBE versions that Update CUBE knows (the closest one agrees in {agreement:P0} of the data; at least {MinimumAgreement:P0} is needed). " +
               "Only ROMs made from a CUBE release can be updated. If this ROM is a CUBE, check that the .toml next to it is the one that was saved with it.");
         }
         if (best.Delta == null) {
            throw new CubeUpdateException("This ROM already has the features of the current CUBE, so there is nothing to update.");
         }

         progress("Moving your changes into the new CUBE...");
         var updater = new CubeUpdater(user, target, CubeFingerprint.Apply(current, best.Delta));
         updater.Report.DetectedVersion = best.Name;
         updater.Report.VersionMatches = best.Matches;
         updater.Report.VersionTotal = best.Total;
         updater.Run();

         var lost = user.Anchors.Where(name => target.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name) < 0).ToList();
         if (lost.Count > 0) updater.Report.Notes.Add($"{lost.Count} names that only your ROM's metadata has (for example {string.Join(", ", lost.Take(3))}) were not added to the new metadata. The data behind them was carried over when it belongs to a table or to something a table points to.");

         progress("Writing the metadata...");
         var toml = Export(singletons, updater.Result);
         toml = AddAuthorsLists(toml, userToml);
         return new CubeUpdateResult { Rom = updater.Result.RawData, Toml = toml, Report = updater.Report };
      }

      /// <summary>The lists that the author made themselves (not in the new CUBE's metadata) stay available.</summary>
      public static string[] AddAuthorsLists(string[] newToml, string[] userToml) {
         if (userToml == null) return newToml;
         var metadata = new StoredMetadata(newToml);
         var known = new HashSet<string>(metadata.Lists.Select(list => list.Name));
         var extra = new StoredMetadata(userToml).Lists.Where(list => !known.Contains(list.Name)).ToList();
         if (extra.Count == 0 || metadata.Lists is not IList<StoredList> lists) return newToml;
         foreach (var list in extra) lists.Add(list);
         return metadata.Serialize();
      }
   }
}
