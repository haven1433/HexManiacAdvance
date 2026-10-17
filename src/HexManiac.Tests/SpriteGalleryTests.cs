using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class SpriteGalleryTests : BaseViewModelTestClass {
      public SpriteGalleryTests() : base(0x1000) {
         SetFullModel(0xFF);
         for (int i = 0x100; i < 0x110; i++) Model[i] = 0;
         Model.SetList(new ModelDelta(), SpriteGalleryElementViewModel.NameListName, "HERO", "RIVAL", "MOM");
         ViewPort.Edit($"@100 ^{HardcodeTablesModel.OverworldSprites}[data::]{SpriteGalleryElementViewModel.NameListName} ");
      }

      [Fact]
      public void Gallery_LabelsShowNumberThenName() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 1);

         Assert.Equal(3, gallery.Elements.Count);
         Assert.Equal("1 (RIVAL)", gallery.Elements[1].Label);
         Assert.Equal("RIVAL", gallery.CurrentName);
         Assert.True(gallery.Elements[1].Selected);
         Assert.False(gallery.Elements[0].Selected);
      }

      [Fact]
      public void Gallery_RenamingUpdatesTheListAndTheDropdownNames() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 1);

         gallery.CurrentName = "Best Friend";

         Assert.True(Model.TryGetList(SpriteGalleryElementViewModel.NameListName, out var list));
         Assert.Equal(new List<string> { "HERO", "Best_Friend", "MOM" }, list.ToList());
         Assert.True(list.StoredHashMatches);
         Assert.Equal("Best_Friend", Model.GetOptions(HardcodeTablesModel.OverworldSprites)[1]);
         Assert.Equal("1 (Best_Friend)", gallery.Elements[1].Label);
         Assert.Equal("Best_Friend", gallery.CurrentName);
      }

      [Fact]
      public void Gallery_SelectingAnItemMovesTheTableTool() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 0);

         gallery.SelectCommand.Execute(gallery.Elements[2]);

         Assert.Equal(0x100 + 8, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void TableTool_OnOverworldSprite_IncludesTheGallery() {
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/2");

         var gallery = ViewPort.Tools.TableTool.Children.OfType<SpriteGalleryElementViewModel>().Single();

         Assert.Equal(2, gallery.CurrentIndex);
         Assert.Equal("MOM", gallery.CurrentName);
      }

      [Fact]
      public void EnumIntoTableOfSpriteStructs_ComboOptionsHavePictures() {
         // like data.trainers.sprites: each element points at a struct whose first field is the sprite
         for (int i = 0x200; i < 0x400; i++) Model[i] = 0;
         ViewPort.Edit("@200 ^pics[front<[sprite<`ucs4x1x1`> palette<`ucp4`>]1>]2 ");
         Model.WritePointer(Token, 0x200, 0x300);
         Model.WritePointer(Token, 0x204, 0x300);
         ViewPort.Edit("@300 ^pic0[sprite<`ucs4x1x1`> palette<`ucp4`>]1 ");
         Model.WritePointer(Token, 0x300, 0x340);
         Model.WritePointer(Token, 0x304, 0x360);
         ViewPort.Edit("@340 ^pic0img`ucs4x1x1` @360 ^pic0pal`ucp4` ");
         ViewPort.Edit("@380 ^users[pic.pics]1 ");

         var segment = (ArrayRunEnumSegment)Model.GetTable("users").ElementContent[0];
         var options = segment.GetComboOptions(Model).ToList();

         Assert.Equal(2, options.Count);
         Assert.All(options, option => Assert.IsType<VisualComboOption>(option));
      }
   }
}
