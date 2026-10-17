using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.CubeUpdate {
   public class CubeUpdateReport {
      public string DetectedVersion { get; set; }
      public int VersionMatches { get; set; }
      public int VersionTotal { get; set; }
      public int AnchorsMerged { get; set; }
      public int NodesEdited { get; set; }
      public int Conflicts { get; set; }
      public int TablesGrown { get; set; }
      public int Copies { get; set; }
      public int CopiedBytes { get; set; }
      public List<string> Skipped { get; } = new();
      public List<string> Notes { get; } = new();
      public Dictionary<string, int> EditsPerAnchor { get; } = new();

      public override string ToString() {
         var text = new StringBuilder();
         text.AppendLine($"Detected version: {DetectedVersion} ({VersionMatches} of {VersionTotal} data nodes match).");
         text.AppendLine($"Carried over: {NodesEdited} edited entries in {EditsPerAnchor.Count} tables, {TablesGrown} tables that you made longer, {Copies} pieces of new data ({CopiedBytes:N0} bytes) moved to free space.");
         if (Conflicts > 0) text.AppendLine($"{Conflicts} entries were changed by you AND by the new CUBE; your version was kept.");
         foreach (var line in Skipped) text.AppendLine("Not carried over: " + line);
         foreach (var line in Notes) text.AppendLine(line);
         return text.ToString();
      }
   }

   public class CubeUpdateException : Exception {
      public CubeUpdateException(string message) : base(message) { }
   }

   /// <summary>
   /// "Update CUBE": takes what the author of a hacked CUBE ROM changed and puts it into a ROM of the current CUBE.
   ///
   /// Both ROMs are looked at as graphs of nodes (RomGraph). The author's edits are the nodes of the hacked ROM that differ from the CUBE version it started from
   /// (CubeFingerprint knows that version). Every such edit is written into the same node of the new ROM; data that does not fit
   /// (a longer text, a new table row, new sprites...) is copied to free space and the pointers are redirected, tables that were made longer grow with the model's own table growth,
   /// and every node that the author did not touch stays as the new CUBE has it, so all new features arrive.
   /// What can not be carried over is listed in the report.
   /// </summary>
   public class CubeUpdater {
      private readonly IDataModel user, target;
      private readonly RomGraph gu, gn;
      private readonly IReadOnlyDictionary<ulong, ulong> old;
      private readonly ModelDelta token = new ModelDelta();
      private readonly Dictionary<int, int> copied = new();                 // start of a run of the user's ROM -> where its copy lives in the new ROM
      private readonly Dictionary<ulong, IFormattedRun> stockLeaves = new(); // subtree hash -> a run of the new ROM that can be shared
      private readonly HashSet<int> noted = new(), editedNodes = new();
      private string currentAnchor;

      public CubeUpdateReport Report { get; } = new();

      /// <param name="user">The hacked ROM, with the metadata the author has for it.</param>
      /// <param name="target">The new CUBE (a model of its own: it is changed by the update).</param>
      /// <param name="oldFingerprint">The fingerprint of the CUBE version that the hacked ROM started from.</param>
      public CubeUpdater(IDataModel user, IDataModel target, IReadOnlyDictionary<ulong, ulong> oldFingerprint) {
         this.user = user;
         this.target = target;
         old = oldFingerprint;
         gu = new RomGraph(user);
         gn = new RomGraph(target);
      }

      public IDataModel Result => target;

      public void Run() {
         // runs that may be shared: plain data without a table in it (text, sprites, palettes)
         foreach (var run in gn.Runs) {
            if (run is ArrayRun || run is IScriptStartRun) continue;
            stockLeaves.TryAdd(gn.SubtreeHash(run), run);
         }

         var roots = gn.NamedRuns().ToList();
         // tables that take their length from another table come after it: a longer parent has to grow first
         var firstPass = roots.Where(root => !(root.run is ArrayRun a && !string.IsNullOrEmpty(a.LengthFromAnchor))).ToList();
         var secondPass = roots.Except(firstPass).ToList();
         foreach (var (name, run) in firstPass.Concat(secondPass)) MergeRoot(name, run);
      }

      #region roots

      private void MergeRoot(string name, IFormattedRun nOrig) {
         var uAddress = user.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         if (uAddress < 0 || uAddress >= gu.Data.Length) return;
         var uRun = gu.RunAt(uAddress);
         if (uRun == null) return;
         var nLive = target.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         if (nLive < 0) return;
         if (!SameKind(uRun, nOrig, out var why)) { Report.Skipped.Add($"{name}: {why}"); return; }
         currentAnchor = name;
         var before = Report.NodesEdited;
         if (!(uRun is ArrayRun) && uRun.Length != nOrig.Length) ReplaceRoot(name, uRun, nLive, RomGraph.PathOf(name));
         else MergeTarget(uRun, nOrig, nLive, RomGraph.PathOf(name));
         Report.AnchorsMerged++;
         if (Report.NodesEdited > before) Report.EditsPerAnchor[name] = Report.NodesEdited - before;
      }

      private static bool SameKind(IFormattedRun u, IFormattedRun n, out string why) {
         why = null;
         if (u is ArrayRun ua && n is ArrayRun na) {
            if (ua.ElementLength != na.ElementLength) { why = $"the table has a different layout (rows of {ua.ElementLength} bytes in your ROM, {na.ElementLength} in the new CUBE)"; return false; }
            return true;
         }
         if (u is ArrayRun != n is ArrayRun) { why = "the table has a different kind of data in your ROM"; return false; }
         return true;
      }

      /// <summary>The run can be changed where it is: it has the same shape, and the new CUBE does not share it more than your ROM does.</summary>
      private static bool CanMergeInPlace(IFormattedRun u, IFormattedRun n) {
         if (u is ArrayRun ua && n is ArrayRun na) {
            if (ua.ElementLength != na.ElementLength) return false;
         } else {
            if (u.Length != n.Length || u.GetType() != n.GetType()) return false;
         }
         var sharedMore = (n.PointerSources?.Count ?? 0) > (u.PointerSources?.Count ?? 0);
         return !sharedMore;
      }

      #endregion

      #region merging

      private void MergeTarget(IFormattedRun uRun, IFormattedRun nOrig, int nLive, ulong path) {
         if (uRun is ArrayRun ua && nOrig is ArrayRun na) {
            MergeArray(ua, na, nLive, path);
         } else {
            MergeStruct(uRun.Start, nOrig.Start, nLive, Math.Min(uRun.Length, nOrig.Length), path, false);
         }
      }

      private void MergeArray(ArrayRun ua, ArrayRun na, int nLive, ulong path) {
         var length = na.ElementLength;
         var uCount = ua.ElementCount;
         var nCount = na.ElementCount;

         // rows that the author added at the end (a row that is just the leftover of an older CUBE version is not theirs)
         var lastAdded = -1;
         for (int i = nCount; i < uCount; i++) {
            var elementPath = RomGraph.ElementPath(path, i);
            var hash = gu.ScalarHash(ua.Start + i * length, length) | 1;
            if (old.TryGetValue(elementPath, out var stock) && stock == hash) continue;
            lastAdded = i;
         }
         if (lastAdded >= 0) {
            var live = target.GetNextRun(nLive) as ArrayRun;
            if (live == null || live.Start != nLive) { Report.Skipped.Add($"{currentAnchor}: could not make the table longer"); lastAdded = -1; }
            else {
               var wanted = lastAdded + 1;
               var grown = target.RelocateForExpansion(token, live, wanted * length);
               grown = grown.Append(token, wanted - grown.ElementCount);
               target.ObserveRunWritten(token, grown);
               nLive = grown.Start;
               Report.TablesGrown++;
               if (grown.ElementCount < wanted) {
                  Report.Skipped.Add($"{currentAnchor}: the table could only grow to {grown.ElementCount} rows");
                  lastAdded = grown.ElementCount - 1;
               }
            }
         }

         for (int i = 0; i < Math.Min(uCount, nCount); i++) {
            MergeStruct(ua.Start + i * length, na.Start + i * length, nLive + i * length, length, RomGraph.ElementPath(path, i), false);
         }
         for (int i = nCount; i <= lastAdded; i++) {
            MergeStruct(ua.Start + i * length, -1, nLive + i * length, length, RomGraph.ElementPath(path, i), true);
         }
      }

      private void MergeStruct(int uStart, int nOrigStart, int nLive, int length, ulong path, bool userAdded) {
         var uHash = gu.ScalarHash(uStart, length) | 1;
         var changed = userAdded;
         if (!userAdded && old.TryGetValue(path, out var stock)) {
            changed = uHash != stock;
            if (changed && nOrigStart >= 0 && (gn.ScalarHash(nOrigStart, length) | 1) != stock) Report.Conflicts++;
         }
         var offsets = new SortedSet<int>(gu.PointersIn(uStart, length).Select(p => p.source - uStart));
         if (nOrigStart >= 0) foreach (var p in gn.PointersIn(nOrigStart, length)) offsets.Add(p.source - nOrigStart);

         if (changed) {
            CopyScalars(uStart, nLive, length, offsets);
            if (editedNodes.Add(uStart)) Report.NodesEdited++;
         }
         foreach (var offset in offsets) MergeChild(uStart, nOrigStart, nLive, offset, path, userAdded);
      }

      /// <summary>Writes the bytes of the author's node over the new node, except the pointers (they are handled one by one).</summary>
      private void CopyScalars(int uStart, int nLive, int length, SortedSet<int> pointerOffsets) {
         var skip = new bool[length];
         foreach (var offset in pointerOffsets) for (int i = 0; i < 4 && offset + i < length; i++) skip[offset + i] = true;
         for (int i = 0; i + 4 <= length; i++) {
            var address = uStart + i;
            if (!skip[i] && gu.IsPointerWord(address)) for (int j = 0; j < 4; j++) skip[i + j] = true;
         }
         for (int i = 0; i < length; i++) {
            if (!skip[i]) token.ChangeData(target, nLive + i, gu.Data[uStart + i]);
         }
      }

      private void MergeChild(int uStart, int nOrigStart, int nLive, int offset, ulong path, bool userAdded) {
         var childPath = RomGraph.ChildPath(path, offset);
         var uDestination = gu.PointerAt(uStart + offset);
         var nDestination = nOrigStart >= 0 ? gn.PointerAt(nOrigStart + offset) : -1;
         var hadOld = old.ContainsKey(childPath) || old.ContainsKey(RomGraph.ElementPath(childPath, 0));
         var source = nLive + offset;

         if (uDestination < 0) {
            // the author cleared a pointer that the old CUBE had
            if (nDestination >= 0 && hadOld && !userAdded) WritePointerValue(source, 0);
            return;
         }
         var uTarget = gu.RunAt(uDestination);
         if (uTarget == null) {
            var inside = gu.RunContaining(uDestination);
            if (inside != null) { var copy = CopyTree(inside); if (copy >= 0) WritePointerValue(source, copy + (uDestination - inside.Start)); return; }
            Note($"{currentAnchor}: a pointer to data that your ROM's metadata does not describe was left as the new CUBE has it");
            return;
         }

         if (nDestination < 0) {
            // the new CUBE has no pointer here: the author added one (an old CUBE leftover that the new CUBE dropped stays dropped)
            if (!hadOld || userAdded) {
               var copy = CopyTree(uTarget);
               if (copy >= 0) WritePointerValue(source, copy);
            }
            return;
         }

         var nTarget = gn.RunAt(nDestination);
         if (nTarget == null) return;
         if (!CanMergeInPlace(uTarget, nTarget)) {
            if (!(old.TryGetValue(childPath, out var stock) || old.TryGetValue(RomGraph.ElementPath(childPath, 0), out stock))) return; // not enough is known to say it is the author's
            var uHash = gu.ScalarHash(uTarget.Start, uTarget is ArrayRun a && a.ElementLength > 0 ? a.ElementLength : uTarget.Length) | 1;
            if (uHash == stock) return;                      // the author did not touch it: the new CUBE's version stays
            var copy = CopyTree(uTarget);
            if (copy >= 0) WritePointerValue(source, copy);
            return;
         }
         var live = ReadPointerValue(source);
         if (live < 0) return;
         MergeTarget(uTarget, nTarget, live, childPath);
      }

      #endregion

      #region writing

      private int ReadPointerValue(int address) {
         var value = target[address] | (target[address + 1] << 8) | (target[address + 2] << 16) | (target[address + 3] << 24);
         var top = (value >> 24) & 0xFF;
         if (top != 0x08 && top != 0x09) return -1;
         return value & 0x1FFFFFF;
      }

      private void WritePointerValue(int address, int destination) {
         var value = destination == 0 ? 0 : destination + 0x08000000;
         for (int i = 0; i < 4; i++) token.ChangeData(target, address + i, (byte)(value >> (8 * i)));
      }

      /// <summary>Copies a run of the author's ROM (and everything it points to) into free space of the new ROM, and returns where the copy lives. Plain data that the new ROM has already is shared instead.</summary>
      private int CopyTree(IFormattedRun uRun) {
         if (copied.TryGetValue(uRun.Start, out var known)) return known;
         if (!(uRun is ArrayRun) && !(uRun is IScriptStartRun) && stockLeaves.TryGetValue(gu.SubtreeHash(uRun), out var shared)) {
            copied[uRun.Start] = shared.Start;
            return shared.Start;
         }
         var length = uRun.Length;
         if (uRun is IScriptStartRun scriptStart) length = Math.Max(length, user.GetScriptLength(scriptStart, new Dictionary<int, int>()));
         var address = target.FindFreeSpace(0x100, length);
         if (address < 0) throw new CubeUpdateException("There is not enough free space left in the new CUBE for the data that you added. Remove unused data from your ROM first, or free up space, and run Update CUBE again.");
         copied[uRun.Start] = address;
         Report.Copies++;
         Report.CopiedBytes += length;
         WriteRunData(uRun, address, length);
         return address;
      }

      /// <summary>Writes the bytes of the author's run at the address, and makes everything that the run points to exist in the new ROM too.</summary>
      private void WriteRunData(IFormattedRun uRun, int address, int length) {
         for (int i = 0; i < length; i++) token.ChangeData(target, address + i, gu.Data[uRun.Start + i]);
         foreach (var (source, destination) in gu.PointersIn(uRun.Start, length).ToList()) {
            var inner = gu.RunAt(destination);
            var offsetInside = 0;
            if (inner == null) { inner = gu.RunContaining(destination); if (inner != null) offsetInside = destination - inner.Start; }
            if (inner == null) continue; // a pointer to something that is not described: it keeps pointing where it pointed (nowhere useful), like in the original
            var copy = CopyTree(inner);
            WritePointerValue(address + (source - uRun.Start), copy + offsetInside);
         }
      }

      /// <summary>
      /// A named piece of data (a compressed sprite, a text...) that has another length in the author's ROM than in the new CUBE.
      /// If the author edited it, theirs replaces the new one under the same name (the new data moves when it needs more room; the pointers to it follow).
      /// If not, the new CUBE's data stays, which is the usual case: the new CUBE may compress the same picture differently.
      /// </summary>
      private void ReplaceRoot(string name, IFormattedRun uRun, int nLive, ulong path) {
         var length = uRun is IScriptStartRun scriptStart ? Math.Max(uRun.Length, user.GetScriptLength(scriptStart, new Dictionary<int, int>())) : uRun.Length;
         if (!old.TryGetValue(path, out var stock)) return;
         if ((gu.ScalarHash(uRun.Start, uRun.Length) | 1) == stock) return;
         var live = target.GetNextRun(nLive);
         if (live == null || live.Start != nLive) return;
         var liveLength = live.Length;
         if (length > liveLength) { live = target.RelocateForExpansion(token, live, length); nLive = live.Start; }
         WriteRunData(uRun, nLive, length);
         for (int i = length; i < liveLength; i++) token.ChangeData(target, nLive + i, 0xFF);
         if (editedNodes.Add(uRun.Start)) Report.NodesEdited++;
         Report.EditsPerAnchor[name] = 1;
      }

      private void Note(string text) {
         if (noted.Add(text.GetHashCode())) Report.Notes.Add(text);
      }

      #endregion
   }
}
