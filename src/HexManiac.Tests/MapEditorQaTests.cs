using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlockCollisionDefaults = HavenSoft.HexManiac.Core.Models.Map.BlockCollisionDefaults;
using Pointer = HavenSoft.HexManiac.Core.ViewModels.DataFormats.Pointer;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>Regression tests for problems found by driving the map editor through all of its commands (QA-MAP).</summary>
   public class MapEditorQaTests : BaseViewModelTestClass {
      private const int SignpostStart = 0x40;

      private ModelArrayElement CreateSignpostElement() {
         ViewPort.Edit($"@{SignpostStart:X2} ^signposts[x:|z y:|z elevation. kind. unused: arg::|s=kind(0=<>|1=<>|2=<>|3=<>|4=<>)]1 ");
         var table = new ModelTable(Model, (ITableRun)Model.GetNextRun(SignpostStart), () => Token);
         return table[0];
      }

      [Fact]
      public void NewSignpost_WithoutAScript_DoesNotFormatAScriptBeforeTheStartOfTheFile() {
         var element = CreateSignpostElement();

         var signpost = new SignpostEventViewModel(element, address => { }) { X = 0, Y = 0, Elevation = 0, Kind = 0, Pointer = Pointer.NULL };

         // a signpost with no script has a null pointer: there is nothing there to format as a script
         Assert.DoesNotContain(Model.All<IFormattedRun>(), run => run.Start < 0 || run.Start >= Model.Count);
         Assert.False(signpost.CanGotoScript);
      }

      #region trainer template

      // name, class, music+gender, sprite, team size, team pointer: 20 bytes per trainer
      private const string TrainerFormat = "[name\"\"12 class. introMusicAndGender. sprite. pokemonCount. pokemon<>]2 ";
      private const int OldTeam = 0x100, NewTeam = 0x140;

      /// <summary>every pointer source that a run claims has to be a pointer that really points at the run</summary>
      private IEnumerable<string> StalePointerSources() {
         foreach (var run in Model.All<IFormattedRun>()) {
            if (run.PointerSources == null) continue;
            foreach (var source in run.PointerSources) {
               if (Model.ReadPointer(source) != run.Start) yield return $"{run.Start:X}<-{source:X}";
            }
         }
      }

      [Fact]
      public void CreateExpansionTrainer_ReusingATrainerSlot_LeavesNoStalePointerSourceToTheOldTeam() {
         // the same trainer exists once per difficulty; each of them points at a team
         ViewPort.Edit($"@40 ^data.trainers.stats{TrainerFormat}");
         ViewPort.Edit($"@80 ^data.trainers.stats.easy{TrainerFormat}");
         ViewPort.Edit($"@C0 ^data.trainers.stats.hard{TrainerFormat}");
         foreach (var pointer in new[] { 0x64, 0xA4, 0xE4 }) ViewPort.Edit($"@{pointer:X2} <{OldTeam:X6}> ");
         Assert.True(!StalePointerSources().Any(), "setup: " + string.Join(" ", StalePointerSources()) + " ptr64=" + Model.ReadPointer(0x64).ToString("X"));
         Assert.Equal(3, Model.GetNextRun(OldTeam).PointerSources.Count);

         var template = new EventTemplate(InstantDispatch.Instance, Model, new ScriptParser(0, new List<IScriptLine>(), 0x02), Enumerable.Range(0, 100).Select(i => (IPixelViewModel)new ReadonlyPixelViewModel(16, 16, new short[256])).ToList());
         var write = typeof(EventTemplate).GetMethod("WriteExpansionTrainer", BindingFlags.NonPublic | BindingFlags.Instance);
         var trainer = new ModelTable(Model, Model.GetTable("data.trainers.stats"), () => Token)[1];
         ViewPort.Edit($"@{NewTeam:X2} 00 ");
         write.Invoke(template, new object[] { Token, trainer, 1, new TrainerPreference(1, 2, 3), 1, NewTeam });

         Assert.Empty(StalePointerSources());
         Assert.Equal(NewTeam, Model.ReadPointer(0x64));
         Assert.Equal(NewTeam, Model.ReadPointer(0xA4));
         Assert.Equal(NewTeam, Model.ReadPointer(0xE4));
         var newTeam = Model.GetNextRun(NewTeam);
         Assert.Equal(NewTeam, newTeam.Start);
         Assert.Equal(new[] { 0x64, 0xA4, 0xE4 }, newTeam.PointerSources.OrderBy(i => i).ToArray());
      }

      #endregion

      #region object event range

      private ObjectEventViewModel CreateObjectViewModel() {
         ViewPort.Edit("@40 ^objects[id. graphics. kind. x:|z y:|z elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<> flag: padding:]1 ");
         var element = new ModelTable(Model, (ITableRun)Model.GetNextRun(0x40), () => Token)[0];
         Model.SetList(Token, "FacingOptions", new[] { "stand", "walk" }, "0");
         return new ObjectEventViewModel(null, address => { }, element, null, new List<IPixelViewModel>(), null, null);
      }

      [Fact]
      public void ObjectRange_NumberAboveFifteen_DoesNotSpillIntoTheOtherRange() {
         var obj = CreateObjectViewModel();
         obj.RangeX = 3; obj.RangeY = 4;

         obj.RangeX = 16 + 7; // only 4 bits belong to X

         Assert.Equal(7, obj.RangeX);
         Assert.Equal(4, obj.RangeY);
      }

      [Fact]
      public void ObjectRange_YNumberAboveFifteen_DoesNotSpillIntoTheNeighboringFields() {
         var obj = CreateObjectViewModel();
         obj.RangeX = 3; obj.RangeY = 5;
         var before = Model.RawData.Clone() as byte[];

         obj.RangeY = 16 + 2;

         Assert.Equal(3, obj.RangeX);
         Assert.Equal(2, obj.RangeY);
         var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != Model.RawData[i]).ToList();
         Assert.Single(changed); // only the range byte
      }

      [Fact]
      public void ObjectRange_TypedAsText_KeepsEachNumberInItsOwnHalf() {
         var obj = CreateObjectViewModel();

         obj.RangeXY = "(20, 33)";

         Assert.Equal(4, obj.RangeX);
         Assert.Equal(1, obj.RangeY);
      }

      #endregion

      #region wave function collapse

      [Fact]
      public void WaveCollapse_EveryOptionCanBeChosen() {
         var cell = new WaveCell(new List<CollapseProbability> { new(10) { Count = 1 }, new(20) { Count = 1 }, new(30) { Count = 1 } }, block => 0);
         var rnd = new Random(1);
         var chosen = new HashSet<int>();

         for (int i = 0; i < 300; i++) chosen.Add(cell.Collapse(rnd) & 0x3FF);

         Assert.Equal(new[] { 10, 20, 30 }, chosen.OrderBy(i => i).ToArray());
      }

      [Fact]
      public void WaveCollapse_OptionsAreChosenInProportionToTheirCount() {
         var cell = new WaveCell(new List<CollapseProbability> { new(10) { Count = 3 }, new(20) { Count = 1 } }, block => 0);
         var rnd = new Random(2);
         int second = 0;

         for (int i = 0; i < 4000; i++) if ((cell.Collapse(rnd) & 0x3FF) == 20) second++;

         Assert.InRange(second, 800, 1200); // one in four
      }

      [Fact]
      public void WaveCollapse_UsesTheCollisionOfTheChosenBlock() {
         var cell = new WaveCell(new List<CollapseProbability> { new(10) { Count = 1 } }, block => 3);

         Assert.Equal((3 << 10) | 10, cell.Collapse(new Random(3)));
      }

      #endregion

      #region block collision defaults (the movement permission of a block that no map uses)

      private const int TilesetHeader = 0x40, BlockData = 0x100, AttributeData = 0x180;

      /// <summary>A primary tileset header (Emerald layout) with 4 blocks whose behaviors are the given numbers, and a behavior list in which the six sideways stairs have the expansion's names.</summary>
      private void CreateTilesetWithBehaviors(params int[] behaviors) {
         Data[TilesetHeader] = 1; // compressed
         Data[TilesetHeader + 1] = 0; // primary
         Model.WriteMultiByteValue(TilesetHeader + 12, 4, Token, 0x08000000 | BlockData);
         Model.WriteMultiByteValue(TilesetHeader + 16, 4, Token, 0x08000000 | AttributeData);
         for (int i = 0; i < behaviors.Length; i++) {
            Data[AttributeData + i * 2] = (byte)behaviors[i];
            Data[AttributeData + i * 2 + 1] = 0x10; // layer type: covered
         }
         var names = Enumerable.Range(0, 80).Select(i => $"UNUSED_{i:X2}").ToArray();
         names[0] = "NORMAL";
         names[73] = "SIDEWAYS_STAIRS_RIGHT_SIDE"; names[74] = "SIDEWAYS_STAIRS_LEFT_SIDE";
         names[75] = "SIDEWAYS_STAIRS_RIGHT_SIDE_TOP"; names[76] = "SIDEWAYS_STAIRS_LEFT_SIDE_TOP";
         names[77] = "SIDEWAYS_STAIRS_RIGHT_SIDE_BOTTOM"; names[78] = "SIDEWAYS_STAIRS_LEFT_SIDE_BOTTOM";
         names[79] = "ROCK_STAIRS";
         Model.SetList(Token, "MapAttributeBehaviors", names);
      }

      [Fact]
      public void SidewaysStairsCollision_IsElevation3Pass() {
         Assert.Equal(0x0C, BlockCollisionDefaults.SidewaysStairsCollision);
         Assert.Equal(3, BlockCollisionDefaults.SidewaysStairsCollision >> 2); // elevation
         Assert.Equal(0, BlockCollisionDefaults.SidewaysStairsCollision & 3);  // collision: pass
      }

      [Fact]
      public void IsSidewaysStairsBehavior_RecognizesTheSixStairsByTheirNames() {
         CreateTilesetWithBehaviors(0, 0, 0, 0);

         for (int behavior = 73; behavior <= 78; behavior++) Assert.True(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, behavior), $"behavior {behavior}");
         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 0));
         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 72));
         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 79)); // rock stairs are not sideways stairs
         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, -1));
         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 200)); // past the list
      }

      [Fact]
      public void IsSidewaysStairsBehavior_AcceptsTheOtherSpellingsOfTheName() {
         Model.SetList(Token, "MapAttributeBehaviors", "Normal", "MB_SIDEWAYS_STAIRS_LEFT_SIDE_TOP", "Sideways Stairs Right Side", "sideways_stairs_left_side");

         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 0));
         Assert.True(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 1));
         Assert.True(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 2));
         Assert.True(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 3));
      }

      [Fact]
      public void IsSidewaysStairsBehavior_GoesByTheNameNotTheNumber() {
         // a vanilla ROM has other behaviors under the numbers 73 to 78
         Model.SetList(Token, "MapAttributeBehaviors", Enumerable.Range(0, 80).Select(i => $"UNUSED_{i:X2}").ToArray());

         for (int behavior = 70; behavior < 80; behavior++) Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, behavior), $"behavior {behavior}");
      }

      [Fact]
      public void IsSidewaysStairsBehavior_WithoutABehaviorList_IsFalse() {
         Assert.False(BlockCollisionDefaults.IsSidewaysStairsBehavior(Model, 74));
      }

      [Fact]
      public void ForUnusedBlock_GivesTheSidewaysStairsElevation3Pass_AndEverythingElseZero() {
         CreateTilesetWithBehaviors(0, 74, 73, 78); // normal, left side, right side, left side bottom

         Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 0));
         Assert.Equal(0x0C, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 1));
         Assert.Equal(0x0C, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 2));
         Assert.Equal(0x0C, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 3));
      }

      [Fact]
      public void ForUnusedBlock_FollowsTheBehaviorTheBlockHasNow() {
         CreateTilesetWithBehaviors(0, 0, 0, 0);
         Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 2));

         Data[AttributeData + 2 * 2] = 75; // the block editor gives block 2 a stairs behavior

         Assert.Equal(0x0C, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 2));
      }

      [Fact]
      public void ForUnusedBlock_WithoutBehaviorNames_IsAlwaysZero() {
         CreateTilesetWithBehaviors(0, 74, 73, 78);
         Model.SetList(Token, "MapAttributeBehaviors", Enumerable.Range(0, 80).Select(i => $"UNUSED_{i:X2}").ToArray());

         for (int block = 0; block < 4; block++) Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, block));
      }

      [Fact]
      public void ForUnusedBlock_BlockWithoutAnAttribute_IsZero() {
         CreateTilesetWithBehaviors(0, 74, 73, 78);

         Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, -1));
         Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, TilesetHeader, 600)); // past the end of any tileset
         Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, -1, 1));             // no tileset
         Assert.Equal(0, BlockCollisionDefaults.ForUnusedBlock(Model, 0x300, 1));          // not a tileset header (no pointers)
      }

      #endregion

      #region Block editor

      [Fact]
      public void BlockEditor_PaletteSelection_IgnoresNumbersThatAreNoPalette() {
         Model.SetList(Token, "MapAttributeBehaviors", "Normal", "Tall Grass", "Water");
         var tiles = new int[1024][,];
         for (int i = 0; i < tiles.Length; i++) tiles[i] = new int[8, 8];
         var palettes = Enumerable.Range(0, 16).Select(_ => new short[16]).ToArray();
         var blocks = Enumerable.Range(0, 4).Select(_ => new byte[16]).ToArray();
         var attributes = Enumerable.Range(0, 4).Select(_ => new byte[2]).ToArray();
         var editor = new BlockEditor(ViewPort.ChangeHistory, Model, new MapTutorialsViewModel(), palettes, tiles, blocks, attributes);
         editor.BlockIndex = 1;
         editor.ShowTiles = true;
         editor.PaletteSelection = 3;

         editor.PaletteSelection = -1;  // a combo box that lost its items writes -1 back
         editor.PaletteSelection = 16;

         Assert.Equal(3, editor.PaletteSelection);
         Assert.NotNull(editor.TileRender);
      }

      #endregion

      #region delete event

      private const string SignpostFormat = "[x:|z y:|z elevation. kind. unused: arg::|s=kind(0=<>|1=<>|2=<>|3=<>|4=<>)]";

      [Fact]
      public void DeleteSignpost_ThatIsNotTheLast_MovesTheScriptPointersOfTheOthersAlong() {
         // three signposts, each shows its own script
         ViewPort.Edit($"@{SignpostStart:X2} ^signposts{SignpostFormat}3 ");
         ViewPort.Edit("@48 <100> @54 <110> @60 <120> ");
         Assert.Empty(StalePointerSources());
         var table = new ModelTable(Model, (ITableRun)Model.GetNextRun(SignpostStart), () => Token);
         var first = new SignpostEventViewModel(table[0], address => { });

         first.Delete();

         Assert.Empty(StalePointerSources());
         Assert.Equal(0x110, Model.ReadPointer(0x48));
         Assert.Equal(0x120, Model.ReadPointer(0x54));
         Assert.Equal(new[] { 0x48 }, Model.GetNextRun(0x110).PointerSources.ToArray());
         Assert.Equal(new[] { 0x54 }, Model.GetNextRun(0x120).PointerSources.ToArray());
         // the signpost that moved up is the second one, the slot that fell off the table does not count as a source anymore
         Assert.DoesNotContain(Model.All<IFormattedRun>(), run => run.PointerSources != null && run.PointerSources.Contains(0x60));
      }

      [Fact]
      public void DeleteSignpost_ThatShowsAScript_BeforeSignpostsThatHoldAnItem_LeavesNothingPointingAtTheScript() {
         // kind 0 -> arg is a script pointer, kind 5 -> arg is a hidden item (not a pointer)
         ViewPort.Edit($"@{SignpostStart:X2} ^signposts{SignpostFormat}3 ");
         ViewPort.Edit("@48 <100> @54 <110> @60 <120> ");
         var table = new ModelTable(Model, (ITableRun)Model.GetNextRun(SignpostStart), () => Token);
         new SignpostEventViewModel(table[1], address => { }).Kind = 5;
         new SignpostEventViewModel(table[2], address => { }).Kind = 5;
         Assert.Empty(StalePointerSources());
         var first = new SignpostEventViewModel(table[0], address => { });

         first.Delete();

         // the item signposts moved up and nothing points at the first script anymore
         Assert.Empty(StalePointerSources());
         Assert.Equal(5, table[0].GetValue("kind"));
         Assert.DoesNotContain(Model.All<IFormattedRun>(), run => run.PointerSources != null && run.PointerSources.Contains(0x48));
      }

      [Fact]
      public void DeleteSignpost_ThatHoldsAnItem_BeforeASignpostWithAScript_PointsTheMovedSignpostAtItsScript() {
         ViewPort.Edit($"@{SignpostStart:X2} ^signposts{SignpostFormat}2 ");
         ViewPort.Edit("@54 <110> ");
         var table = new ModelTable(Model, (ITableRun)Model.GetNextRun(SignpostStart), () => Token);
         var first = new SignpostEventViewModel(table[0], address => { });
         first.Kind = 5;
         Assert.Empty(StalePointerSources());

         first.Delete();

         Assert.Empty(StalePointerSources());
         Assert.Equal(0x110, Model.ReadPointer(0x48));
         Assert.Equal(new[] { 0x48 }, Model.GetNextRun(0x110).PointerSources.ToArray());
      }

      [Fact]
      public void SignpostKind_ChangedFromScriptToHiddenItem_LetsGoOfTheScript() {
         ViewPort.Edit($"@{SignpostStart:X2} ^signposts{SignpostFormat}1 ");
         ViewPort.Edit("@48 <100> ");
         var table = new ModelTable(Model, (ITableRun)Model.GetNextRun(SignpostStart), () => Token);
         var signpost = new SignpostEventViewModel(table[0], address => { });
         Assert.Contains(0x48, Model.GetNextRun(0x100).PointerSources);

         signpost.Kind = 5;

         // the hidden item shares the place of the pointer: moving the script must not write its address there
         Assert.Empty(StalePointerSources());
         Assert.DoesNotContain(Model.All<IFormattedRun>(), run => run.PointerSources != null && run.PointerSources.Contains(0x48));
      }

      #endregion

      /// <summary>every run of the model as text: start, type, length</summary>
      private List<string> RunSnapshot() => Model.All<IFormattedRun>().Select(run => $"{run.Start:X}:{run.GetType().Name}:{run.Length:X}").ToList();

      [Fact]
      public void SignpostCreateScript_WhenUndone_LeavesNoFormatBehindOverTheErasedScript() {
         for (int i = 0x80; i < Data.Length; i++) Data[i] = 0xFF; // free space for the script
         var element = CreateSignpostElement();
         var signpost = new SignpostEventViewModel(element, address => BlockMapViewModel.GotoAddress(ViewPort, address)) { X = 0, Y = 0, Elevation = 0, Kind = 0, Pointer = Pointer.NULL }; // (the map editor's own goto: it formats the script it goes to)
         var runsBefore = RunSnapshot();

         signpost.CreateScript();

         var start = element.GetAddress("arg");
         Assert.True(start >= 0x80, "setup: the script has to be in the free space, was " + start.ToString("X"));
         Assert.Equal(0x0F, Data[start]);
         Assert.NotEqual(runsBefore, RunSnapshot()); // the script and the text it points to are formatted now

         Token.Revert(Model);

         // the bytes are free again, so the format of the script (the script itself, its pointer, its text) has to be gone too
         Assert.Equal(0xFF, Data[start]);
         Assert.Equal(runsBefore, RunSnapshot());
         Assert.Empty(StalePointerSources());
      }

      #region map script

      [Fact]
      public void MapScript_AddressOutsideTheFile_ShowsNoSubScripts() {
         ViewPort.Edit("@40 02 <100> "); // a per-frame table of sub scripts
         var script = new MapScriptViewModel(ViewPort, null, 0x40);

         script.Address = "<1FFFFFC>"; // a place that is not in the file

         Assert.Empty(script.SubScripts);
      }

      [Fact]
      public void MapScript_TableThatRunsIntoTheEndOfTheFile_StopsAtTheEnd() {
         ViewPort.Edit("@40 02 <100> @1FC 01 ");
         var script = new MapScriptViewModel(ViewPort, null, 0x40);

         script.Address = "<1FC>"; // the first entry would need 8 bytes, only 4 are left

         Assert.Empty(script.SubScripts);
      }

      #endregion

      #region blockmap size

      [Fact]
      public void ResizeBlockmap_UsedByTwoLayouts_UpdatesTheSizeOfBothLayouts() {
         // "Repoint layout" copies the layout of a map but both copies still share the blockmap until it is repointed too
         ViewPort.Edit("@40 ^layouts[width:: height:: borderblock<> blockmap<`blm`>]2 ");
         foreach (var layout in new[] { 0x40, 0x50 }) { Model.WriteValue(Token, layout, 4); Model.WriteValue(Token, layout + 4, 4); }
         ViewPort.Edit("@4C <140> @5C <140> ");
         var run = (BlockmapRun)Model.GetNextRun(0x140);
         Assert.Equal(2, run.PointerSources.Count);

         var newRun = run.TryChangeSize(() => Token, MapDirection.Right, 1, 2, 2);

         Assert.NotNull(newRun);
         Assert.Equal(5, Model.ReadValue(0x40));
         Assert.Equal(5, Model.ReadValue(0x50)); // the other layout reads the same blockmap: it has to know about the new width
         Assert.Equal(4, Model.ReadValue(0x44));
         Assert.Equal(4, Model.ReadValue(0x54));
         Assert.Equal(5 * 4 * 2, newRun.Length);
      }

      #endregion

      #region block count of a blockset

      // 8 attributes of 2 bytes right before 8 blocks of 16 bytes (pokeemerald-expansion builds put the attributes first),
      // and four bytes in the middle of the blocks that happen to look like a pointer
      [Fact]
      public void EstimateBlockCount_AttributesBeforeTheBlocks_IsNotCutShortByAPointerLookingThingInsideTheBlocks() {
         ViewPort.Edit("@40 ^attributes 00 @50 ^blocks 00 @70 <000010> ");
         Assert.IsType<PointerRun>(Model.GetNextRun(0x70));

         int count = 512;
         BlocksetModel.EstimateBlockCount(Model, ref count, 0x50, 0x40);

         Assert.Equal(8, count); // (the attributes say 8, the pointer at 0x70 would say 2)
      }

      [Fact]
      public void EstimateBlockCount_AttributesAfterTheBlocks_StopsAtTheAttributes() {
         ViewPort.Edit("@40 ^blocks 00 @70 <000010> @C0 ^attributes 00 ");

         int count = 512;
         BlocksetModel.EstimateBlockCount(Model, ref count, 0x40, 0xC0);

         Assert.Equal(8, count); // (0x40 to 0xC0 is 8 blocks, the pointer inside them does not end them)
      }

      #endregion
   }
}
