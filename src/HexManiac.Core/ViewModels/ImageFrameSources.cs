using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A group of pictures (the frames of an animation) that one image editor tab shows one at a time:
   /// the dots on the left of the editor select the frame, and the palette has a row of its own.
   /// The editor draws on the picture the source returns and hands it back to be stored: where the pixels live is up to the source.
   /// Every frame must be backed by a registered sprite (the editor takes the palette from it), see <see cref="SpritePointer"/>.
   /// </summary>
   public interface IImageFrameSource {
      /// <summary>The name of the editor's tab.</summary>
      string Title { get; }

      /// <summary>False once the data behind the frames was removed or no longer looks the way it did when the editor opened: the editor then closes.</summary>
      bool IsValid { get; }

      int FrameCount { get; }

      /// <summary>Empty, or a short note about the frame for its dot (for example that it shares its picture with an earlier frame).</summary>
      string FrameNote(int frame);

      /// <summary>The address of a pointer to the sprite that holds this frame: the editor finds the palette through that sprite.</summary>
      int SpritePointer(int frame);

      /// <summary>True if the tiles of a frame can be shown in rows of different widths (the editor then shows its 'tiles per row' slider).</summary>
      bool CanChooseWidth { get; }
      int DefaultWidthTiles { get; }
      int MaxWidthTiles { get; }

      /// <summary>The frame as a picture of palette indices ([x, y]); widthTiles is only used if CanChooseWidth.</summary>
      int[,] ReadFrame(int frame, int widthTiles);

      /// <summary>Store an edited picture of the shape ReadFrame returned.</summary>
      void WriteFrame(ModelDelta token, int frame, int widthTiles, int[,] pixels);

      /// <summary>
      /// Called when an edit of a frame is over (the pen was lifted, something was pasted...) with the change that edit is part of, so that a source which keeps another copy
      /// of the picture can update it once per edit instead of once per pixel. The animation sources use it to show the first frame in the tileset.
      /// </summary>
      void EditCompleted(ModelDelta token, int frame) { }
   }

   /// <summary>The frames of one tileset animation (the game's own, or one added with HexManiac): each frame is a run of tiles of its own.</summary>
   public class AnimationFrameSource : IImageFrameSource {
      private readonly IDataModel model;
      private readonly TilesetAnimations animations;
      private readonly int tilesetStart;
      private readonly Func<TilesetAnimationEntry> resolve;
      private readonly int firstTile, tileCount;

      public string Title { get; }

      /// <param name="resolve">Finds the animation again every time it is needed (null if it is gone): its tables move when animations are added or removed.</param>
      public AnimationFrameSource(IDataModel model, TilesetAnimations animations, int tilesetStart, TilesetAnimationEntry entry, string title, Func<TilesetAnimationEntry> resolve) {
         (this.model, this.animations, this.tilesetStart, this.resolve) = (model, animations, tilesetStart, resolve);
         (firstTile, tileCount) = (entry.FirstTile, entry.TileCount);
         Title = title;
      }

      public TilesetAnimationEntry Entry => resolve();

      public bool IsValid {
         get {
            var entry = Entry;
            return entry != null && entry.FirstTile == firstTile && entry.TileCount == tileCount && entry.FrameCount >= 1;
         }
      }

      public int FrameCount => Entry?.FrameCount ?? 0;

      /// <summary>Register every frame as tiles that know their palette (frames can have been added since the editor opened). Returns false if a frame is unusable.</summary>
      public bool Prepare() {
         var entry = Entry;
         if (entry == null) return false;
         for (int frame = 0; frame < entry.FrameCount; frame++) {
            var address = animations.EnsureFrameFormat(tilesetStart, entry, frame);
            if (address < 0 || model.GetNextRun(address) is not ISpriteRun) return false;
         }
         return true;
      }

      public string FrameNote(int frame) {
         var entry = Entry;
         if (entry == null) return string.Empty;
         // a frame table can point at the same pictures twice (the flowers go 1, 2, 1, 3): editing one edits the other
         var address = animations.FrameAddress(entry, frame);
         for (int earlier = 0; earlier < frame && address >= 0; earlier++) {
            if (animations.FrameAddress(entry, earlier) == address) return $"the same picture as frame {earlier + 1}";
         }
         return string.Empty;
      }

      public int SpritePointer(int frame) {
         var entry = Entry;
         return entry == null ? -1 : entry.FramesAddress + 4 * frame;
      }

      public bool CanChooseWidth => tileCount > 1;
      public int DefaultWidthTiles => TilesetAnimations.DefaultPictureWidth(tileCount);
      public int MaxWidthTiles => Math.Max(1, tileCount);

      public int[,] ReadFrame(int frame, int widthTiles) {
         var entry = Entry;
         if (entry == null) return new int[8, 8];
         return animations.ReadFramePixels(entry, frame, widthTiles);
      }

      public void WriteFrame(ModelDelta token, int frame, int widthTiles, int[,] pixels) {
         var entry = Entry;
         if (entry != null) animations.WriteFramePixels(token, entry, frame, widthTiles, pixels);
      }

      /// <summary>
      /// The first frame is also what the tileset shows (and what the map editor's tile picker offers, and what the game shows before the first tick):
      /// once an edit of it is over, store it in the tileset's own graphics too, as part of the same undo step. The game's own animations keep their tiles.
      /// </summary>
      public void EditCompleted(ModelDelta token, int frame) {
         var entry = Entry;
         if (frame != 0 || entry == null || entry.IsBuiltIn) return;
         animations.ShowFirstFrameInTileset(token, tilesetStart, entry);
      }
   }

   /// <summary>The three frames of one animated door. They are stored in one block of tiles; each frame is shown as the picture the game draws.</summary>
   public class DoorFrameSource : IImageFrameSource {
      private readonly DoorAnimations doors;
      private readonly int index, tilesetAddress, tilesAddress;
      private readonly int size;

      public string Title { get; }

      public DoorFrameSource(DoorAnimations doors, DoorEntry entry, string title) {
         this.doors = doors;
         (index, tilesetAddress, tilesAddress, size) = (entry.Index, entry.TilesetAddress, entry.TilesAddress, entry.Size);
         Title = title;
      }

      public DoorEntry Entry {
         get {
            var entries = doors.ReadEntries();
            return index >= 0 && index < entries.Count ? entries[index] : null;
         }
      }

      public bool IsValid {
         get {
            var entry = Entry;
            return entry != null && entry.TilesetAddress == tilesetAddress && entry.TilesAddress == tilesAddress && entry.Size == size;
         }
      }

      public int FrameCount => DoorEntry.FrameCount;

      /// <summary>Register the door's tiles as a sprite that knows the tileset's palette. Returns false if the door's tiles are unusable.</summary>
      public bool Prepare() {
         var entry = Entry;
         if (entry == null) return false;
         doors.EnsureTilesFormat(entry);
         return true;
      }

      public string FrameNote(int frame) => string.Empty;

      public int SpritePointer(int frame) {
         var entry = Entry;
         return entry == null ? -1 : entry.EntryAddress + 12;
      }

      public bool CanChooseWidth => false;
      public int DefaultWidthTiles => DoorEntry.WidthTilesFor(size);
      public int MaxWidthTiles => DefaultWidthTiles;

      public int[,] ReadFrame(int frame, int widthTiles) {
         var entry = Entry;
         if (entry == null) return new int[DoorEntry.WidthTilesFor(size) * 8, DoorEntry.HeightTilesFor(size) * 8];
         return doors.ReadFramePixels(entry, frame);
      }

      public void WriteFrame(ModelDelta token, int frame, int widthTiles, int[,] pixels) {
         var entry = Entry;
         if (entry != null) doors.WriteFramePixels(token, entry, frame, pixels);
      }
   }
}
