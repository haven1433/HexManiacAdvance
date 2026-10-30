using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Taking a template out of the toolbar, and dragging an event over the map, change the data of the map one tile at a time, and every change makes the map build its events again.
   /// So an object event must be cheap to build: the things only the open event panel needs are not made until it asks.
   /// </summary>
   public class MapEventDragTests : BaseViewModelTestClass {
      public MapEventDragTests() : base(0x400) { }

      private ObjectEventViewModel CreateObject(int graphics, int knownSprites = 4) {
         // with this table, the panel offers a category of dynamic graphics: filling its list would need the map the event is on (here there is none)
         ViewPort.Edit("@100 ^graphics.overworld.sprites[a:]2 ");
         ViewPort.Edit("@00 ^objects[id. graphics: unused: x: y: elevation. moveType. range. trainerType. trainerRangeOrBerryID: script<> flag: unused:]2 ");
         Model.SetList(Token, "FacingOptions", "Still", "Wander", "Look");
         var element = Model.GetTableModel("objects", () => Token)[0];
         element.SetValue("graphics", graphics);
         var sprites = Enumerable.Range(0, knownSprites).Select(_ => (IPixelViewModel)new ReadonlyPixelViewModel(16, 32)).ToList();
         return new ObjectEventViewModel(null, address => { }, element, null, sprites, new ReadonlyPixelViewModel(16, 32), null);
      }

      [Fact]
      public void ObjectEvent_WithADynamicGraphic_IsBuiltWithoutFillingTheListsOfSprites() {
         // building it used to render every sprite of the category for the panel: half a second per event with such a graphic, each time the map's data changed
         var objectEvent = CreateObject(graphics: 10, knownSprites: 4);

         Assert.True(objectEvent.ShowGraphicsAsText);
         Assert.Equal(10, objectEvent.Graphics);
      }

      [Fact]
      public void ObjectEvent_WithAKnownGraphic_ShowsNoDynamicGraphics() {
         var objectEvent = CreateObject(graphics: 2, knownSprites: 4);

         Assert.False(objectEvent.ShowGraphicsAsText);
      }
   }
}
