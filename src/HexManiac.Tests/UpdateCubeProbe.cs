using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.CubeUpdate;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class UpdateCubeProbe {
      static Dictionary<ulong, ulong> Fingerprint(string rom, string toml, TextWriter log) {
         var model = CubeScriptsTests.Load(File.ReadAllBytes(rom), File.ReadAllLines(toml));
         var graph = new RomGraph(model);
         log.WriteLine($"{Path.GetFileName(rom)}: runs {graph.Runs.Count}, pointers {graph.PointerCount}, anchors {model.Anchors.Count}");
         var result = new Dictionary<ulong, ulong>();
         int collisions = 0;
         var byDepth = new int[10];
         foreach (var (name, run) in graph.NamedRuns()) {
            graph.Walk(run, RomGraph.PathOf(name), name, 0, node => {
               if (!result.TryAdd(node.Path, node.ScalarHash)) collisions++;
               byDepth[Math.Min(9, node.Depth)]++;
            });
         }
         log.WriteLine($"  nodes {result.Count}, path repeats {collisions}, by depth {string.Join(",", byDepth)}");
         return result;
      }

      [SkippableFact]
      public void Probe() {
         var romA = Environment.GetEnvironmentVariable("UPD_A"); var tomlA = Environment.GetEnvironmentVariable("UPD_A_TOML");
         var romB = Environment.GetEnvironmentVariable("UPD_B"); var tomlB = Environment.GetEnvironmentVariable("UPD_B_TOML");
         Skip.If(romA == null || romB == null, "UPD_A/UPD_B");
         using var log = new StreamWriter(Environment.GetEnvironmentVariable("UPD_LOG") ?? "/tmp/upd_probe.log");
         Dictionary<ulong, ulong> a = null, b = null;
         var t = new Thread(() => { a = Fingerprint(romA, tomlA, log); b = Fingerprint(romB, tomlB, log); }, 512 * 1024 * 1024);
         t.Start(); t.Join();
         int same = 0, diff = 0, onlyA = 0;
         foreach (var kv in a) { if (!b.TryGetValue(kv.Key, out var v)) onlyA++; else if (v == kv.Value) same++; else diff++; }
         log.WriteLine($"A vs B: same {same}, different {diff}, only in A {onlyA}, only in B {b.Count - same - diff}");
      }
   }
}
