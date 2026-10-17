using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// There are over a thousand overworld sprites. The galleries must not draw them all, or build a control for each of them,
   /// just to show a few: they draw a sprite when somebody looks at it, and lay the sprites out in rows that can be virtualized.
   /// </summary>
   public class SpriteGalleryRowTests : BaseViewModelTestClass {
      private const int SpriteCount = 30;

      public SpriteGalleryRowTests() : base(0x1000) {
         SetFullModel(0xFF);
         var names = Enumerable.Range(0, SpriteCount).Select(i => i == 11 ? "PROFESSOR" : i == 12 ? "PROFESSOR_ASSISTANT" : $"SPRITE_{i}").ToArray();
         Model.SetList(new ModelDelta(), SpriteGalleryElementViewModel.NameListName, names);
         ViewPort.Edit($"@100 ^{HardcodeTablesModel.OverworldSprites}[data::]{SpriteGalleryElementViewModel.NameListName} ");
      }

      [Fact]
      public void Gallery_Load_DoesNotDrawAnySprite() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);

         Assert.Equal(SpriteCount, gallery.Elements.Count);

         Assert.All(gallery.Elements, item => Assert.False(item.IsRendered));
         Assert.Equal(0, OverworldSpriteRenderer.Get(Model).RenderedCount);
      }

      [Fact]
      public void Gallery_LookingAtOneSprite_DrawsOnlyThatSprite() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);
         var item = gallery.Elements[5];

         var width = item.PixelWidth;

         Assert.True(width >= 0);
         Assert.True(item.IsRendered);
         Assert.Equal(1, gallery.Elements.Count(element => element.IsRendered));
         Assert.Equal(1, OverworldSpriteRenderer.Get(Model).RenderedCount);
      }

      [Fact]
      public void Gallery_DrawnSprite_IsSharedByEveryGalleryOfTheSameData() {
         var first = new SpriteGalleryElementViewModel(ViewPort, 3);
         var second = new SpriteGalleryElementViewModel(ViewPort, 4);

         var a = first.Elements[7].PixelData;
         var b = second.Elements[7].PixelData;

         Assert.Same(a, b);
         Assert.Equal(1, OverworldSpriteRenderer.Get(Model).RenderedCount);
      }

      [Fact]
      public void Gallery_DataChanges_NextGalleryStartsOverWithoutOldPictures() {
         var first = new SpriteGalleryElementViewModel(ViewPort, 3);
         var drawn = first.Elements[7].PixelWidth;
         var renderer = OverworldSpriteRenderer.Get(Model);

         Model[0x800] = 1; // any change of the data

         Assert.NotSame(renderer, OverworldSpriteRenderer.Get(Model));
         Assert.Equal(0, OverworldSpriteRenderer.Get(Model).RenderedCount);
      }

      [Fact]
      public void Rows_HoldAsManySpritesAsFitInTheWidth() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);

         gallery.SetAvailableWidth(SpriteGalleryElementViewModel.ItemOuterWidth * 4 + 20 + 1);

         Assert.Equal(4, gallery.ColumnCount);
         Assert.Equal(8, gallery.Rows.Count); // 7 full rows and one with the last two
         Assert.Equal(new[] { 0, 1, 2, 3 }, gallery.Rows[0].Items.Select(item => item.Index));
         Assert.Equal(new[] { 28, 29 }, gallery.Rows[7].Items.Select(item => item.Index));
         Assert.Equal(SpriteCount, gallery.Rows.Sum(row => row.Items.Count));
      }

      [Fact]
      public void Rows_TooNarrow_StillHoldOneSprite() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);

         gallery.SetAvailableWidth(5);

         Assert.Equal(1, gallery.ColumnCount);
         Assert.Equal(SpriteCount, gallery.Rows.Count);
      }

      [Fact]
      public void Rows_ListsBeforeAnythingWasMeasured() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);

         Assert.Equal(SpriteCount, gallery.Rows.Sum(row => row.Items.Count));
         Assert.All(gallery.Rows.Take(gallery.Rows.Count - 1), row => Assert.Equal(gallery.ColumnCount, row.Items.Count));
      }

      [Fact]
      public void Rows_Filter_OnlyKeepsTheMatchingSprites() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);
         gallery.SetAvailableWidth(SpriteGalleryElementViewModel.ItemOuterWidth * 4 + 20 + 1);

         gallery.Filter = "PROFESSOR";

         Assert.Equal(new[] { 11, 12 }, gallery.Rows.SelectMany(row => row.Items).Select(item => item.Index));
         Assert.Single(gallery.Rows);

         gallery.Filter = string.Empty;

         Assert.Equal(SpriteCount, gallery.Rows.Sum(row => row.Items.Count));
      }

      [Fact]
      public void Rows_ChangeWidthAfterTheyWereCreated_ViewIsToldAboutTheNewRows() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 3);
         gallery.SetAvailableWidth(SpriteGalleryElementViewModel.ItemOuterWidth * 4 + 20 + 1);
         Assert.Equal(8, gallery.Rows.Count); // the view has asked for the rows
         var notified = new List<string>();
         gallery.PropertyChanged += (sender, e) => notified.Add(e.PropertyName);

         gallery.SetAvailableWidth(SpriteGalleryElementViewModel.ItemOuterWidth * 6 + 20 + 1);

         Assert.Equal(6, gallery.ColumnCount);
         Assert.Contains(nameof(SpriteGalleryElementViewModel.Rows), notified);
         Assert.Equal(5, gallery.Rows.Count);
      }

      [Fact]
      public void Gallery_EveryItemKnowsHowToSelectItself() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 0);

         var item = gallery.Elements[2];
         item.SelectCommand.Execute(item);

         Assert.Equal(0x100 + 8, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void Gallery_MovingToAnotherSprite_OnlyTheOldAndNewSpritesChangeSelection() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 2);
         var items = gallery.Elements;
         var changed = new List<int>();
         foreach (var item in items) {
            var index = item.Index;
            item.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(SpriteGalleryItem.Selected)) changed.Add(index); };
         }

         var moved = new SpriteGalleryElementViewModel(ViewPort, 7);
         Assert.True(gallery.TryCopy(moved));

         Assert.Equal(new[] { 2, 7 }, changed.OrderBy(i => i));
         Assert.Equal(7, gallery.CurrentIndex);
         Assert.True(items[7].Selected);
         Assert.False(items[2].Selected);
      }

      [Fact]
      public void Gallery_MovingToAnotherSprite_DoesNotDrawTheOtherSprites() {
         var gallery = new SpriteGalleryElementViewModel(ViewPort, 2);
         var items = gallery.Elements;

         Assert.True(gallery.TryCopy(new SpriteGalleryElementViewModel(ViewPort, 7)));

         Assert.Empty(items.Where(item => item.IsRendered));
      }

      [Fact]
      public void TableTool_MovingToTheNextSprite_KeepsTheGalleryAndDoesNotReplaceGroups() {
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/1");
         var tool = ViewPort.Tools.TableTool;
         var gallery = tool.Children.OfType<SpriteGalleryElementViewModel>().Single();
         var events = new List<NotifyCollectionChangedAction>();
         tool.Groups.CollectionChanged += (sender, e) => events.Add(e.Action);

         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/2");
         ViewPort.Goto.Execute($"{HardcodeTablesModel.OverworldSprites}/3");

         Assert.Same(gallery, tool.Children.OfType<SpriteGalleryElementViewModel>().Single());
         Assert.Equal(3, gallery.CurrentIndex);
         Assert.DoesNotContain(NotifyCollectionChangedAction.Replace, events); // a replaced group makes the view throw away and rebuild everything in it
      }

      [Fact]
      public void TableTool_TableLists_AreRememberedUntilTheDataChanges() {
         var tool = ViewPort.Tools.TableTool;
         ViewPort.Edit("@200 ^data.test.plain[value:]4 ");
         ViewPort.Goto.Execute("data.test.plain/1");

         var sections = tool.TableSections;
         var list = tool.TableList;

         Assert.Contains("data.test", sections);
         Assert.Contains("plain", list);
         Assert.Same(sections, tool.TableSections); // the dropdowns keep their items if the list is the same list
         Assert.Same(list, tool.TableList);

         Model[0x800] = 1;
         var after = tool.TableSections;

         Assert.NotSame(sections, after);
         Assert.Equal(sections, after);
      }

      [Fact]
      public void Tab_Open_DoesNotDrawAnySprite() {
         var tab = new SpriteGalleryTab(ViewPort);

         Assert.Equal(SpriteCount, tab.Elements.Count);
         Assert.All(tab.Elements, item => Assert.False(item.IsRendered));
         Assert.Equal(0, OverworldSpriteRenderer.Get(Model).RenderedCount);
      }

      [Fact]
      public void Tab_Rows_HoldAsManySpritesAsFit() {
         var tab = new SpriteGalleryTab(ViewPort);

         tab.SetAvailableWidth(tab.ItemOuterWidth * 5 + 20 + 1);

         Assert.Equal(5, tab.ColumnCount);
         Assert.Equal(6, tab.Rows.Count);
         Assert.Equal(SpriteCount, tab.Rows.Sum(row => row.Items.Count));
      }

      [Fact]
      public void Tab_Zoom_ChangesHowManySpritesFitInARow() {
         var tab = new SpriteGalleryTab(ViewPort);
         tab.SetAvailableWidth(1000);
         var before = tab.ColumnCount;

         tab.SpriteScale = 4;

         Assert.True(tab.ColumnCount < before);
         Assert.Equal(SpriteCount, tab.Rows.Sum(row => row.Items.Count));
      }

      [Fact]
      public void Tab_Selected_WithoutDataChange_KeepsTheSprites() {
         var tab = new SpriteGalleryTab(ViewPort);
         var first = tab.Elements[0];

         ((ITabContent)tab).Refresh(); // the editor does this every time the tab is selected

         Assert.Same(first, tab.Elements[0]);
      }

      [Fact]
      public void Tab_Selected_AfterDataChange_ListsTheSpritesAgain() {
         var tab = new SpriteGalleryTab(ViewPort);
         var first = tab.Elements[0];

         Model[0x800] = 1;
         ((ITabContent)tab).Refresh();

         Assert.NotSame(first, tab.Elements[0]);
         Assert.Equal(SpriteCount, tab.Elements.Count);
      }

      [Fact]
      public void Tab_RefreshButton_DrawsEverythingAgain() {
         var tab = new SpriteGalleryTab(ViewPort);
         var drawn = tab.Elements[4].PixelWidth;
         var renderer = OverworldSpriteRenderer.Get(Model);

         tab.Reload();

         Assert.NotSame(renderer, OverworldSpriteRenderer.Get(Model));
         Assert.False(tab.Elements[4].IsRendered);
      }

      [Fact]
      public void Item_GivenAPicture_NeedsNoRenderer() {
         var item = new SpriteGalleryItem(1, 0x104, "X", new ReadonlyPixelViewModel(2, 2), false);

         Assert.True(item.IsRendered);
         Assert.Equal(2, item.PixelWidth);
         Assert.False(item.IsPlaceholder);
      }
   }
}
