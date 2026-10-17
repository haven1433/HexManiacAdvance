using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Stopwatch tests of the wild encounter row editor, run on the real CUBE rom (they skip without HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML).
   /// The numbers are printed so that a before/after comparison is possible; the asserts only guard against a big regression.
   /// WPF rendering is not part of these numbers: they measure the view models (what the UI thread runs on every refresh).
   /// </summary>
   public class WildEditorPerformanceTests {
      private static ViewPort OpenCube(out IDataModel model) {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var metadata = new StoredMetadata(File.ReadAllLines(toml));
         var hardcode = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path), metadata);
         hardcode.InitializationWorkload.Wait();
         model = hardcode;
         return new ViewPort("CUBE.gba", hardcode, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
      }

      /// <summary>The addresses of the encounter lists (list of rows) of the first maps that have grass encounters.</summary>
      private static List<int> FindLists(IDataModel model, string field, int count) {
         var result = new List<int>();
         var wild = model.GetTableModel(HardcodeTablesModel.WildTableName, default);
         for (int i = 0; i < wild.Count && result.Count < count; i++) {
            if (!wild[i].HasField(field)) continue;
            var address = wild[i].GetAddress(field);
            if (address == Pointer.NULL) continue;
            result.Add(address);
         }
         return result;
      }

      private static double Time(int repeat, Action action) {
         action(); // warm up (JIT, caches)
         var watch = Stopwatch.StartNew();
         for (int i = 0; i < repeat; i++) action();
         return watch.Elapsed.TotalMilliseconds / repeat;
      }

      private static TableRowsStreamElementViewModel Rows(ViewPort viewPort) => viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().FirstOrDefault();

      [SkippableFact]
      public void Profile_RealCube() {
         var viewPort = OpenCube(out var model);
         var table = model.GetTable(HardcodeTablesModel.WildTableName);
         Assert.NotNull(table);
         var field = new[] { "morningGrass", "grass", "dayGrass" }.First(name => table.ElementContent.Any(segment => segment.Name == name));
         var lists = FindLists(model, field, 12);
         Console.WriteLine($"PERF wildTableCount={model.GetTableModel(HardcodeTablesModel.WildTableName, default)?.Count} field={field} lists={lists.Count}");
         Assert.True(lists.Count >= 4, "the CUBE rom should have encounter lists");
         Console.WriteLine($"PERF wild field={field} maps={lists.Count} species={model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(model))?.Count}");

         // 1. open a wild table: goto the list, the table tool builds the row editor
         var next = 0;
         var open = Time(10, () => {
            viewPort.Goto.Execute(lists[next++ % lists.Count]);
            viewPort.Tools.SelectedTool = viewPort.Tools.TableTool;
            viewPort.Tools.TableTool.DataForCurrentRunChanged();
         });
         Console.WriteLine($"PERF open-table-ms={open:F1}");
         Assert.True(open < 250, $"opening a wild list took {open:F0} ms");
         var rows = Rows(viewPort);
         Assert.NotNull(rows);
         Console.WriteLine($"PERF rows={rows.Rows.Count} rowEditors={viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Count()} totalRows={viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Sum(r => r.Rows.Count)} children={viewPort.Tools.TableTool.Children.Count()} groups={viewPort.Tools.TableTool.Groups.Count}");

         var gotoOnly = Time(10, () => viewPort.Goto.Execute(lists[next++ % lists.Count]));
         Console.WriteLine($"PERF goto-only-ms={gotoOnly:F1}");
         var toolOnly = Time(10, () => { viewPort.Goto.Execute(lists[next++ % lists.Count]); viewPort.Tools.TableTool.DataForCurrentRunChanged(); });
         Console.WriteLine($"PERF goto+refresh-ms={toolOnly:F1}");
         var cell1 = (NumericTableCellViewModel)Rows(viewPort).Rows[1].Cells[0];
         var lookup = Time(10, () => Rows(viewPort));
         Console.WriteLine($"PERF rows-lookup-ms={lookup:F2}");
         var lvl = 5;
         var writeOnly = Time(30, () => {
            cell1.Content = (lvl = lvl % 50 + 1).ToString();
         });
         Console.WriteLine($"PERF write-level-only-ms={writeOnly:F1}");
         var cellStart = ((NumericTableCellViewModel)Rows(viewPort).Rows[1].Cells[0]).Start;
         var rawWrite = Time(30, () => { model.WriteMultiByteValue(cellStart, 1, viewPort.CurrentChange, 5); });
         var notify = Time(30, () => {
            var t = (ITableRun)model.GetNextRun(cellStart);
            var o = t.ConvertByteOffsetToArrayOffset(cellStart);
            t.NotifyChildren(model, viewPort.CurrentChange, o.ElementIndex, o.SegmentIndex);
         });
         Console.WriteLine($"PERF notify-children-ms={notify:F2}");
         var v = 5;
         var changingNotify = Time(30, () => {
            model.WriteMultiByteValue(cellStart, 1, viewPort.CurrentChange, v = v % 50 + 1);
            var t = (ITableRun)model.GetNextRun(cellStart);
            var o = t.ConvertByteOffsetToArrayOffset(cellStart);
            t.NotifyChildren(model, viewPort.CurrentChange, o.ElementIndex, o.SegmentIndex);
         });
         Console.WriteLine($"PERF write+notify-changing-ms={changingNotify:F2}");

         var refreshMethod = typeof(TextStreamElementViewModel).GetMethod("RefreshContentFromData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
         var refreshContent = Time(30, () => refreshMethod.Invoke(Rows(viewPort), null));
         Console.WriteLine($"PERF refresh-content-ms={refreshContent:F2}");
         var textSet = Time(30, () => { var r = Rows(viewPort); r.ElementContent.Content = r.ElementContent.Content + " "; });
         Console.WriteLine($"PERF element-content-set-ms={textSet:F2}");
         var proxyBuild = Time(30, () => {
            var t = (ITableRun)model.GetNextRun(model.ReadPointer(Rows(viewPort).Start));
            var proxy = new TableStreamRun(model, t.Start, t.PointerSources, t.FormatString, t.ElementContent, new FixedLengthStreamStrategy(t.ElementCount));
            proxy.SerializeRun();
         });
         Console.WriteLine($"PERF proxy-serialize-ms={proxyBuild:F2}");
         Console.WriteLine($"PERF raw-write-ms={rawWrite:F2}");

         var dataChangedField = typeof(StreamElementViewModel).GetField("dataChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
         var raiseOnly = Time(30, () => { var d = (EventHandler)dataChangedField.GetValue(Rows(viewPort)); d?.Invoke(Rows(viewPort), EventArgs.Empty); });
         Console.WriteLine($"PERF raise-datachanged-ms={raiseOnly:F2}");
         var refreshBacking = typeof(ViewPort).GetMethod("RefreshBackingData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
         var rb = Time(30, () => refreshBacking.Invoke(viewPort, null));
         Console.WriteLine($"PERF refresh-backing-data-ms={rb:F2}");

         // 2. type a level (every keystroke writes, notifies the table and refreshes the table tool)
         var level = 5; Console.WriteLine("PERF ---");
         var type = Time(30, () => {
            cell1.Content = (level = level % 50 + 1).ToString();
            viewPort.Tools.TableTool.DataForCurrentRunChanged();
         });
         Console.WriteLine($"PERF type-level-ms={type:F1}");
         Assert.True(type < 150, $"typing a level took {type:F0} ms per keystroke (it was 20-30 ms before the usage search was skipped for unnamed tables)");

         // 3. tab through the rows: moving the keyboard focus changes nothing in the view model, but every row/cell is built once per refresh
         var rebuild = Time(10, () => viewPort.Tools.TableTool.DataForCurrentRunChanged());
         Console.WriteLine($"PERF table-refresh-ms={rebuild:F1}");

         // 3b. how many PropertyChanged notifications (each one is binding work for WPF) a refresh and a keystroke cause in the row editor
         {
            var counts = new Dictionary<string, int>();
            void Watch(System.ComponentModel.INotifyPropertyChanged source, string kind) => source.PropertyChanged += (sender, e) => counts[kind + "." + e.PropertyName] = counts.GetValueOrDefault(kind + "." + e.PropertyName) + 1;
            var editor = Rows(viewPort);
            Watch(editor, "rows");
            foreach (var row in editor.Rows) {
               Watch(row, "row");
               foreach (var cell in row.Cells) {
                  Watch(cell, "cell");
                  if (cell is EnumTableCellViewModel enumCell) Watch(enumCell.FilteringComboOptions, "combo");
               }
            }
            viewPort.Tools.TableTool.DataForCurrentRunChanged();
            Console.WriteLine($"PERF notifications-on-plain-refresh={counts.Values.Sum()} [{string.Join(", ", counts.Select(kv => kv.Key + "x" + kv.Value))}]");
            counts.Clear();
            var c = (NumericTableCellViewModel)editor.Rows[2].Cells[0];
            c.Content = (c.Content == "7" ? 8 : 7).ToString();
            viewPort.Tools.TableTool.DataForCurrentRunChanged();
            Console.WriteLine($"PERF notifications-on-level-edit={counts.Values.Sum()} [{string.Join(", ", counts.Select(kv => kv.Key + "x" + kv.Value))}]");
         }

         // 4. build the row editor alone
         var build = Time(10, () => { var r = Rows(viewPort); var fresh = new TableRowsStreamElementViewModel(viewPort, "x", r.Start, r.RunFormat); });
         Console.WriteLine($"PERF build-rows-ms={build:F1}");

         // 4b. the whole wild entry of a map (every terrain and time of day is a row editor): goto the table row
         var entryStart = table.Start + table.ElementLength * 3;
         var openEntry = Time(5, () => {
            viewPort.Goto.Execute(entryStart + (next++ % 3) * table.ElementLength);
            viewPort.Tools.TableTool.DataForCurrentRunChanged();
         });
         Console.WriteLine($"PERF open-wild-entry-ms={openEntry:F1} rowEditors={viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Count()} totalRows={viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Sum(r => r.Rows.Count)}");
         var entryRows = viewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().First();
         var entryCell = (NumericTableCellViewModel)entryRows.Rows[0].Cells[0];
         var entryLevel = 5;
         var typeEntry = Time(20, () => {
            entryCell.Content = (entryLevel = entryLevel % 50 + 1).ToString();
            viewPort.Tools.TableTool.DataForCurrentRunChanged();
         });
         Console.WriteLine($"PERF type-level-in-entry-ms={typeEntry:F1}");

         var streamsScan = Time(10, () => { var l = model.Streams; });
         var arraysScan = Time(10, () => { var l = model.Arrays; });
         Console.WriteLine($"PERF model.Streams-ms={streamsScan:F2} model.Arrays-ms={arraysScan:F2} streams={model.Streams.Count} arrays={model.Arrays.Count}");

         // 4c. type a species name into the box of a row (a filter over all species per keystroke), then pick it
         var box = ((EnumTableCellViewModel)Rows(viewPort).Rows[0].Cells[2]).FilteringComboOptions;
         var typeSpecies = Time(10, () => {
            box.DropDownIsOpen = true;
            foreach (var text in new[] { "p", "pi", "pik", "pika" }) box.DisplayText = text;
            box.SelectConfirm();
            box.DropDownIsOpen = false;
         });
         Console.WriteLine($"PERF type-species-4-keys-ms={typeSpecies:F2}");
         var pick = Time(5, () => { box.DropDownIsOpen = true; box.SelectedIndex = (box.SelectedIndex + 1) % box.FilteredOptions.Count; box.DropDownIsOpen = false; viewPort.Tools.TableTool.DataForCurrentRunChanged(); });
         Console.WriteLine($"PERF pick-species-ms={pick:F2}");

         // 5. the map's popup: summary text and overworld checkbox for several maps
         var wildModel = model.GetTableModel(HardcodeTablesModel.WildTableName, default);
         var summary = Time(5, () => {
            for (int i = 0; i < 10; i++) {
               var vm = new WildPokemonViewModel(viewPort, new MapTutorialsViewModel(), wildModel[i].GetValue("bank"), wildModel[i].GetValue("map"));
               var text = vm.WildSummary;
               var a = vm.GrassExists; var b = vm.HasOverworldWildPokemonOption; var c = vm.HasHidden;
            }
         });
         Console.WriteLine($"PERF summary-10-maps-ms={summary:F1}");
         // the map editor clears the caches of every visible map whenever the map changes: the popup button then reads every property again
         var vms = Enumerable.Range(0, 10).Select(i => new WildPokemonViewModel(viewPort, new MapTutorialsViewModel(), wildModel[i].GetValue("bank"), wildModel[i].GetValue("map"))).ToList();
         var reread = Time(10, () => {
            foreach (var vm in vms) {
               vm.ClearCache();
               var a = vm.HasWildData; var b = vm.WildSummary; var c = vm.HasTimesOfDay; var d = vm.GrassExists; var e = vm.SurfExists; var f = vm.TreeExists;
               var g = vm.FishingExists; var h = vm.HiddenExists; var i = vm.HasHidden; var j = vm.HasOverworldWildPokemonOption; var k = vm.OverworldWildPokemon;
            }
         });
         Console.WriteLine($"PERF clearcache+reread-10-maps-ms={reread:F1}");
      }
   }
}
