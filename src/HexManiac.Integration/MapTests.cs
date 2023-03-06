using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Integration {
   public class MapTests : IntegrationTests {
      private const string StartTown = "maps.3-0 Pallet Town";

      [SkippableFact]
      public void NoConnections_CreateNewMap_AddsConnectionTable() {
         var firered = LoadFireRed();
         firered.Goto.Execute("maps.1-0 Viridian Forest");
         var leftEdgeButton = firered.MapEditor.MapButtons.Single(button => button.Icon == MapSliderIcons.ExtendLeft);

         var newMapButton = leftEdgeButton.ContextItems.Single(item => item.Text == "Create New Map");
         newMapButton.Execute();

         var forest = firered.Model.GetTableModel("data.maps.banks/1/maps/0/map/");
         Assert.Equal(1, forest[0].GetSubTable("connections")[0].GetValue("count"));
      }

      [SkippableFact]
      public void MapRepointer_ExpandPrimaryTilesetText_HasMaxTiles() {
         var firered = LoadReadOnlyFireRed();
         firered.Goto.Execute(StartTown);
         var repointer = firered.MapEditor.PrimaryMap.MapRepointer;

         var text = repointer.ExpandPrimaryTilesetText;

         Assert.Equal("This primary tileset contains 640 of 640 tiles.", text);
      }

      [SkippableFact]
      public void FlowerBlock_EditBlockLayer_OneByteChangeInFile() {
         var firered = LoadFireRed();
         firered.Goto.Execute(StartTown);

         firered.MapEditor.SelectBlock(0, 4);
         firered.MapEditor.ReleaseBlock(0, 4);
         firered.MapEditor.PrimaryMap.BlockEditor.Layer = 2;

         firered.Diff.Execute();
         Assert.Equal("1 changes found.", Messages.Single());
      }

      [SkippableFact]
      public void EditBorderBlock_SelectBlock_UpdateSelectedBlock() {
         var firered = LoadReadOnlyFireRed();
         firered.Goto.Execute(StartTown);
         var view = new StubView(firered.MapEditor);

         firered.MapEditor.ReadBorderBlock(4, 3);

         Assert.Contains(nameof(MapEditorViewModel.BlockSelectionToggle), view.PropertyNotifications);
         Assert.True(firered.MapEditor.BlockEditorVisible);
         Assert.Contains(nameof(firered.MapEditor.AutoscrollBlocks), view.EventNotifications);
      }

      [SkippableFact]
      public void CleanFile_OpenMapEditor_VisibleMapsContainsPrimaryMap() {
         var firered = LoadReadOnlyFireRed();

         firered.Goto.Execute(StartTown);

         var editor = firered.MapEditor;
         Assert.Contains(editor.PrimaryMap, editor.VisibleMaps);
      }

      [SkippableFact]
      public void OakLabSignpost_RepointScript_UpdateSignpostScriptField() {
         var firered = LoadFireRed();
         var address1 = firered.Maps[3][0].Events.Signposts[0].Arg;

         firered.Goto.Execute(StartTown);
         firered.MapEditor.PrimaryMap.EventGroup.Signposts[0].GotoScript();
         var script = firered.Tools.CodeTool.Contents[0];
         script.Content = "nop" + Environment.NewLine + script.Content; // causes repoint

         var address2 = firered.Maps[3][0].Events.Signposts[0].Arg;
         Assert.NotEqual(address1, address2);
      }
   }
}
