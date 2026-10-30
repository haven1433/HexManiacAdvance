using HexManiac.Core.Models.Runs.Sprites;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// The movement permission (the six collision bits of a map cell: elevation * 4 + collision) that the map editor gives a block when it is chosen for painting.
   /// A block that maps already use gets the permission that most of its uses have. A block that no map uses has nothing to follow:
   /// it gets 0, except the sideways stairs of the Emerald expansion, which only work with "elevation 3, pass".
   /// </summary>
   public static class BlockCollisionDefaults {
      /// <summary>Elevation 3, pass: the movement permission a block with a sideways-stairs behavior needs.</summary>
      public const int SidewaysStairsCollision = 3 << 2 | 0;

      /// <summary>Is this one of the six sideways-stairs metatile behaviors (SIDEWAYS_STAIRS_RIGHT_SIDE ... SIDEWAYS_STAIRS_LEFT_SIDE_BOTTOM)?
      /// It goes by the behavior's name in the ROM's list, not by its number (other games and hacks use the same numbers for something else), so it is false for a ROM whose list doesn't have these names.</summary>
      public static bool IsSidewaysStairsBehavior(IDataModel model, int behavior) {
         if (behavior < 0 || !model.TryGetList("MapAttributeBehaviors", out var list) || behavior >= list.Count) return false;
         var name = list[behavior]; // (a behavior without a name: the list can have holes)
         if (name == null) return false;
         name = name.ToUpperInvariant().Replace(" ", "_");
         if (name.StartsWith("MB_")) name = name.Substring(3);
         return name.StartsWith("SIDEWAYS_STAIRS");
      }

      /// <summary>
      /// The movement permission for a block that no map uses: the block of the blockset at 'blocksetAddress' (its header, the one the layouts point to) that has the number 'blockIndex' inside that blockset.
      /// Read from the ROM when it is asked, so it follows the behavior the person gives the block in the block editor.
      /// </summary>
      public static int ForUnusedBlock(IDataModel model, int blocksetAddress, int blockIndex) {
         if (blocksetAddress < 0 || blocksetAddress >= model.Count) return 0;
         var behavior = new BlocksetModel(model, blocksetAddress).ReadBlockBehavior(blockIndex);
         return IsSidewaysStairsBehavior(model, behavior) ? SidewaysStairsCollision : 0;
      }
   }
}
