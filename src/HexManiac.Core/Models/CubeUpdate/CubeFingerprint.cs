using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.CubeUpdate {
   /// <summary>
   /// The fingerprint of a CUBE ROM: for every node of its graph (see RomGraph), the scalar hash of the node, by path.
   /// The fingerprint of the current CUBE is computed when "Update CUBE" runs (from the CUBE it builds).
   /// What ships with the program is, for every older CUBE version, only how its fingerprint differs from the current one (a few thousand nodes):
   /// that is enough to tell which nodes of a hacked ROM were edited by its author (they differ from the version they started from).
   /// </summary>
   public static class CubeFingerprint {
      public static Dictionary<ulong, ulong> Compute(RomGraph graph) {
         var result = new Dictionary<ulong, ulong>();
         foreach (var (name, run) in graph.NamedRuns()) {
            graph.Walk(run, RomGraph.PathOf(name), name, 0, node => result.TryAdd(node.Path, node.ScalarHash | 1));
         }
         return result;
      }

      /// <summary>How one older version differs from the current one. A hash of 0 means: the version has no such node.</summary>
      public record VersionDelta(string Name, IReadOnlyList<(ulong path, ulong hash)> Changes);

      public static VersionDelta Diff(string name, IReadOnlyDictionary<ulong, ulong> version, IReadOnlyDictionary<ulong, ulong> current) {
         var changes = new List<(ulong, ulong)>();
         foreach (var (path, hash) in current) {
            if (!version.TryGetValue(path, out var other)) changes.Add((path, 0));
            else if (other != hash) changes.Add((path, other));
         }
         foreach (var (path, hash) in version) {
            if (!current.ContainsKey(path)) changes.Add((path, hash));
         }
         changes.Sort();
         return new VersionDelta(name, changes);
      }

      /// <summary>The fingerprint of the older version.</summary>
      public static Dictionary<ulong, ulong> Apply(IReadOnlyDictionary<ulong, ulong> current, VersionDelta delta) {
         var result = new Dictionary<ulong, ulong>(current);
         foreach (var (path, hash) in delta.Changes) {
            if (hash == 0) result.Remove(path); else result[path] = hash;
         }
         return result;
      }

      public record Detection(string Name, int Matches, int Total, VersionDelta Delta);

      /// <summary>
      /// Which version does a ROM come from? The one whose fingerprint agrees with the ROM's in the most nodes.
      /// 'current' counts as a version too (Delta is null for it).
      /// </summary>
      public static IReadOnlyList<Detection> Detect(IReadOnlyDictionary<ulong, ulong> rom, IReadOnlyDictionary<ulong, ulong> current, IReadOnlyList<VersionDelta> deltas, string currentName) {
         var matchesCurrent = current.Count(pair => rom.TryGetValue(pair.Key, out var hash) && hash == pair.Value);
         var results = new List<Detection> { new(currentName, matchesCurrent, current.Count, null) };
         foreach (var delta in deltas) {
            var matches = matchesCurrent;
            var total = current.Count;
            foreach (var (path, hash) in delta.Changes) {
               var currentHash = current.TryGetValue(path, out var c) ? c : 0UL;
               var romHash = rom.TryGetValue(path, out var r) ? r : 0UL;
               if (currentHash != 0 && romHash == currentHash) matches--;
               if (hash != 0 && romHash == hash) matches++;
               if (currentHash == 0 && hash != 0) total++;
               if (currentHash != 0 && hash == 0) total--;
            }
            results.Add(new(delta.Name, matches, total, delta));
         }
         return results.OrderByDescending(d => d.Matches).ToList();
      }

      #region file format: gzip( "CUBEFP1", count, then per version: name, change count, (path u64, hash u64)... )

      public static byte[] Write(IEnumerable<VersionDelta> deltas) {
         using var memory = new MemoryStream();
         using (var gzip = new GZipStream(memory, CompressionLevel.Optimal, true))
         using (var writer = new BinaryWriter(gzip)) {
            writer.Write("CUBEFP1");
            var list = deltas.ToList();
            writer.Write(list.Count);
            foreach (var delta in list) {
               writer.Write(delta.Name);
               writer.Write(delta.Changes.Count);
               foreach (var (path, hash) in delta.Changes) { writer.Write(path); writer.Write(hash); }
            }
         }
         return memory.ToArray();
      }

      public static List<VersionDelta> Read(byte[] file) {
         using var memory = new MemoryStream(file);
         using var gzip = new GZipStream(memory, CompressionMode.Decompress);
         using var reader = new BinaryReader(gzip);
         if (reader.ReadString() != "CUBEFP1") throw new InvalidDataException("not a CUBE fingerprint file");
         var result = new List<VersionDelta>();
         var count = reader.ReadInt32();
         for (int i = 0; i < count; i++) {
            var name = reader.ReadString();
            var changes = new List<(ulong, ulong)>(reader.ReadInt32());
            var n = changes.Capacity;
            for (int j = 0; j < n; j++) changes.Add((reader.ReadUInt64(), reader.ReadUInt64()));
            result.Add(new VersionDelta(name, changes));
         }
         return result;
      }

      #endregion
   }
}
