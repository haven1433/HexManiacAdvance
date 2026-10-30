using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A picture the view shows that can be swapped for another one without the view noticing (the previews change as the colours are edited).
   /// </summary>
   public class PixelPreview : ViewModelCore, IPixelViewModel {
      private IPixelViewModel image;

      public short Transparent => image?.Transparent ?? -1;
      public int PixelWidth => image?.PixelWidth ?? 0;
      public int PixelHeight => image?.PixelHeight ?? 0;
      public short[] PixelData => image?.PixelData ?? Array.Empty<short>();
      public bool HasImage => image != null && image.PixelWidth > 0 && image.PixelHeight > 0;

      private double spriteScale = 2;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

      public PixelPreview(IPixelViewModel image = null, double scale = 2) {
         this.image = image;
         spriteScale = scale;
      }

      /// <summary>Swap the picture. The size is only announced if it changed: every announcement makes the view build the picture again.</summary>
      public void Replace(IPixelViewModel newImage) {
         var sizeChanged = PixelWidth != (newImage?.PixelWidth ?? 0) || PixelHeight != (newImage?.PixelHeight ?? 0);
         image = newImage;
         if (sizeChanged) {
            NotifyPropertyChanged(nameof(PixelWidth));
            NotifyPropertyChanged(nameof(PixelHeight));
         }
         NotifyPropertyChanged(nameof(Transparent));
         NotifyPropertyChanged(nameof(HasImage));
         NotifyPropertyChanged(nameof(PixelData));
      }
   }

   /// <summary>
   /// The graphics of one of the two player characters (the boy, Brendan, or the girl, May), read through the ROM's metadata:
   /// the standing and walking pictures of the overworld sprite, the trainer front picture and the back picture, each with its palette.
   /// The pixels are kept as palette indices, so drawing them again with other colours is just a lookup.
   /// </summary>
   public class CharacterSprites {
      public const string TrainerSpritesTable = "data.trainers.sprites";
      public const string OverworldPicName = "objecteventgfx";

      /// <summary>
      /// The frames of the overworld sheet that the previews show: standing still facing down, facing up and facing left, with no walking frames.
      /// The player's walking sheet (and every standard sheet) starts with those three, in that order: pokeemerald's sAnim_FaceSouth, sAnim_FaceNorth and
      /// sAnim_FaceWest show frames 0, 1 and 2 (facing right is frame 2 flipped). Frames 3 to 8 are walking down, up and left, two of each.
      /// </summary>
      public static readonly IReadOnlyList<int> OverworldFrameNumbers = new[] { 0, 1, 2 };
      public static readonly IReadOnlyList<string> OverworldFrameLabels = new[] { "facing down", "facing up", "facing left" };

      /// <summary>The frame of the sheet that shows the character standing still facing that way (right is not a frame of its own: the game flips left).</summary>
      public static int StandingFrame(OverworldFacing facing) => facing switch {
         OverworldFacing.Down => 0,
         OverworldFacing.Up => 1,
         OverworldFacing.Left => 2,
         OverworldFacing.Right => 2,
         _ => 0,
      };

      /// <summary>
      /// The size, in tiles, of one frame of a sheet that stores its frames one after the other: frameBytes long, as wide as the sheet's picture.
      /// The sheet's picture can hold every frame stacked (the player's sheet is 2 tiles wide and 36 tiles tall: 9 frames of 2x4),
      /// so its own height is not the height of a frame. Returns the picture's own size when the frame size is unknown or doesn't fit the width.
      /// </summary>
      public static (int tileWidth, int tileHeight) FrameTileSize(int frameBytes, int sheetTileWidth, int sheetTileHeight, int bitsPerPixel) {
         if (frameBytes <= 0 || sheetTileWidth <= 0 || bitsPerPixel <= 0) return (sheetTileWidth, sheetTileHeight);
         var tileBytes = 8 * bitsPerPixel;
         if (frameBytes % tileBytes != 0) return (sheetTileWidth, sheetTileHeight);
         var tiles = frameBytes / tileBytes;
         if (tiles % sheetTileWidth != 0) return (sheetTileWidth, sheetTileHeight);
         return (sheetTileWidth, tiles / sheetTileWidth);
      }

      public int Gender { get; }
      public string Title => Gender == CharacterRole.Male ? "Boy (Brendan)" : "Girl (May)";
      public string OverworldName => overworldNameOverride ?? (Gender == CharacterRole.Male ? "BRENDAN_NORMAL" : "MAY_NORMAL");
      public string TrainerPicName => Gender == CharacterRole.Male ? "BRENDAN" : "MAY";

      public int OverworldIndex { get; private set; } = -1;
      public int TrainerPicIndex { get; private set; } = -1;

      /// <summary>Palette indices of the overworld frames (standing facing down, up and left: see <see cref="OverworldFrameNumbers"/>), [x, y].</summary>
      public IReadOnlyList<int[,]> OverworldFrames { get; private set; } = Array.Empty<int[,]>();
      public IReadOnlyList<short> OverworldPalette { get; private set; }
      public int[,] Front { get; private set; }
      public IReadOnlyList<short> FrontPalette { get; private set; }
      public int[,] Back { get; private set; }
      public IReadOnlyList<short> BackPalette { get; private set; }

      /// <summary>Where each picture lives, for the buttons that open it in the image editor (-1 if it wasn't found).</summary>
      public int OverworldAddress { get; private set; } = -1;
      public int FrontAddress { get; private set; } = -1;
      public int BackAddress { get; private set; } = -1;
      /// <summary>The overworld sprite's own entry in the overworld sprite table, for the 'open the table' buttons.</summary>
      public int OverworldEntryAddress { get; private set; } = -1;
      public int TrainerEntryAddress { get; private set; } = -1;

      /// <summary>What went wrong, if some of the graphics could not be found.</summary>
      public IReadOnlyList<string> Problems { get; private set; } = Array.Empty<string>();

      private readonly string overworldNameOverride;

      private CharacterSprites(int gender, string overworldName = null) => (Gender, overworldNameOverride) = (gender, overworldName);

      public bool HasOverworld => OverworldFrames.Count > 0 && OverworldPalette != null;
      public bool HasFront => Front != null && FrontPalette != null;
      public bool HasBack => Back != null && BackPalette != null;

      /// <param name="overworldName">The name of the entry of the overworld sprite table to show, when the ROM doesn't call the player's sprite BRENDAN_NORMAL / MAY_NORMAL (the default).</param>
      public static CharacterSprites Load(IDataModel model, int gender, string overworldName = null) {
         var sprites = new CharacterSprites(gender, overworldName);
         var problems = new List<string>();
         try { sprites.LoadOverworld(model, problems); } catch (Exception e) { problems.Add($"The overworld sprite could not be read ({e.Message})."); }
         try { sprites.LoadTrainerPics(model, problems); } catch (Exception e) { problems.Add($"The trainer pictures could not be read ({e.Message})."); }
         sprites.Problems = problems;
         return sprites;
      }

      private static int IndexOf(IReadOnlyList<string> names, string name) {
         if (names == null) return -1;
         for (int i = 0; i < names.Count; i++) if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
         return -1;
      }

      #region Overworld

      private void LoadOverworld(IDataModel model, List<string> problems) {
         var table = model.GetTableModel(HardcodeTablesModel.OverworldSprites);
         if (table == null) { problems.Add("This ROM has no overworld sprite table."); return; }
         OverworldIndex = IndexOf(model.GetOptions(HardcodeTablesModel.OverworldSprites), OverworldName);
         if (OverworldIndex < 0 || OverworldIndex >= table.Count) { problems.Add($"The overworld sprite {OverworldName} was not found."); return; }
         var element = table[OverworldIndex];
         OverworldEntryAddress = element.Start;
         var data = element.GetSubTable("data")?[0];
         var spriteList = data?.GetSubTable("sprites");
         if (data == null || spriteList == null || spriteList.Count == 0) { problems.Add($"The overworld sprite {OverworldName} has no pictures."); return; }

         var paletteId = data.GetValue("paletteid");
         OverworldPalette = FindOverworldPalette(model, paletteId);
         if (OverworldPalette == null) problems.Add($"The palette of {OverworldName} (number {paletteId:X4}) was not found in the overworld palette table.");

         var firstFrame = spriteList[0];
         OverworldAddress = firstFrame.GetAddress("sprite");
         var frames = new List<int[,]>();
         foreach (var frame in OverworldFrameNumbers) {
            var pixels = ReadOverworldFrame(model, spriteList, frame);
            if (pixels == null) { problems.Add($"Frame {frame} of {OverworldName} could not be read."); break; }
            frames.Add(pixels);
         }
         OverworldFrames = frames;
      }

      private static IReadOnlyList<short> FindOverworldPalette(IDataModel model, int paletteId) {
         var palettes = model.GetTableModel(HardcodeTablesModel.OverworldPalettes);
         if (palettes == null) return null;
         for (int i = 0; i < palettes.Count; i++) {
            var entry = palettes[i];
            if (entry.GetValue("id") != paletteId) continue;
            var run = model.GetNextRun(entry.GetAddress("pal")) as IPaletteRun;
            if (run != null) return run.GetPalette(model, 0).ToArray();
         }
         return null;
      }

      /// <summary>
      /// The picture of one frame of an overworld sheet. Most sheets list a picture per frame. Sheets that store every frame one after the other
      /// (the player's walking sheet does) list only the first and give the size of one frame; the later frames follow it.
      /// </summary>
      public static int[,] ReadOverworldFrame(IDataModel model, ModelTable spriteList, int frame) {
         var list = spriteList.Run;
         if (list == null || spriteList.Count == 0) return null;
         var listStart = list.Start;
         var relativeFrames = model[listStart + 6] != 0;
         if (!relativeFrames && frame < spriteList.Count) {
            var run = model.GetNextRun(spriteList[frame].GetAddress("sprite")) as ISpriteRun;
            return run?.GetPixels(model, 0, -1);
         }

         var firstAddress = model.ReadPointer(listStart);
         if (firstAddress < 0 || firstAddress >= model.Count) return null;
         var first = model.GetNextRun(firstAddress) as ISpriteRun;
         if (first == null) return null;
         var frameBytes = model.ReadMultiByteValue(listStart + 4, 2);
         if (first is SpriteRun) {
            // uncompressed: the frames follow each other in the ROM
            // the picture can be formatted as the whole sheet (all frames stacked): one frame is frameBytes long, not the whole picture
            var format = first.SpriteFormat;
            var (tileWidth, tileHeight) = FrameTileSize(frameBytes, format.TileWidth, format.TileHeight, format.BitsPerPixel);
            var length = tileWidth * tileHeight * 8 * format.BitsPerPixel;
            if (frameBytes <= 0) frameBytes = length;
            var start = firstAddress + frameBytes * frame;
            if (length <= 0 || start + length > model.Count) return null;
            return SpriteRun.GetPixels(model, start, tileWidth, tileHeight, format.BitsPerPixel);
         }

         // compressed: one tall picture with every frame in it (stacked top to bottom)
         var all = first.GetPixels(model, 0, -1);
         if (all == null) return null;
         var width = all.GetLength(0);
         var frames = Math.Max(1, spriteList.Count);
         var height = all.GetLength(1);
         var frameHeight = frames > 1 ? height / frames : first.SpriteFormat.TileHeight * 8;
         if (frameHeight <= 0 || (frame + 1) * frameHeight > height) return null;
         var result = new int[width, frameHeight];
         for (int y = 0; y < frameHeight; y++) for (int x = 0; x < width; x++) result[x, y] = all[x, frame * frameHeight + y];
         return result;
      }

      /// <summary>The list of pictures of an entry of the overworld sprite table (null if the entry or its list is gone).</summary>
      public static ModelTable FindOverworldSpriteList(IDataModel model, int overworldIndex) {
         var table = model.GetTableModel(HardcodeTablesModel.OverworldSprites);
         if (table == null || overworldIndex < 0 || overworldIndex >= table.Count) return null;
         var list = table[overworldIndex].GetSubTable("data")?[0]?.GetSubTable("sprites");
         return list == null || list.Count == 0 || list.Run == null ? null : list;
      }

      /// <summary>The animations (frame order and timing) of an entry of the overworld sprite table, read from its info's anims pointer. Empty if the entry has none.</summary>
      public static IReadOnlyList<OverworldAnimSequence> ReadOverworldAnimations(IDataModel model, int overworldIndex, bool playerSheet) {
         var table = model.GetTableModel(HardcodeTablesModel.OverworldSprites);
         if (table == null || overworldIndex < 0 || overworldIndex >= table.Count) return new OverworldAnimSequence[0];
         var info = table[overworldIndex].GetSubTable("data")?[0];
         if (info == null) return new OverworldAnimSequence[0];
         var start = info.GetAddress("anims"); // the address the pointer leads to: the animation table
         if (start < 0 || start + 4 > model.Count) return new OverworldAnimSequence[0];
         var next = model.GetNextAnchor(start + 1).Start;
         return OverworldAnimations.Read(model, start, next > start && next < model.Count ? next - start : 0, playerSheet);
      }

      /// <summary>True for a sheet that lists only its first picture and stores the others right after it (the player's walking sheet).</summary>
      public static bool IsRelativeSheet(IDataModel model, ModelTable spriteList) => model[spriteList.Run.Start + 6] != 0;

      /// <summary>Where a frame of an uncompressed sheet is stored: its first byte, its length, and how many bits one pixel takes. False if the frame is not plain uncompressed pictures.</summary>
      public static bool TryLocateOverworldFrame(IDataModel model, ModelTable spriteList, int frame, out int start, out int length, out int bitsPerPixel) {
         (start, length, bitsPerPixel) = (-1, 0, 4);
         if (spriteList == null || frame < 0) return false;
         var listStart = spriteList.Run.Start;
         if (!IsRelativeSheet(model, spriteList)) {
            if (frame >= spriteList.Count) return false;
            if (model.GetNextRun(spriteList[frame].GetAddress("sprite")) is not SpriteRun run) return false;
            return TryLocate(run, run.Start, 0, out start, out length, out bitsPerPixel);
         }
         var firstAddress = model.ReadPointer(listStart);
         if (firstAddress < 0 || firstAddress >= model.Count) return false;
         if (model.GetNextRun(firstAddress) is not SpriteRun first) return false;
         var frameBytes = model.ReadMultiByteValue(listStart + 4, 2);
         return TryLocate(first, firstAddress, frame, out start, out length, out bitsPerPixel, frameBytes) && start + length <= model.Count;
      }

      private static bool TryLocate(SpriteRun run, int firstAddress, int frame, out int start, out int length, out int bitsPerPixel, int frameBytes = 0) {
         var format = run.SpriteFormat;
         bitsPerPixel = format.BitsPerPixel;
         length = format.TileWidth * format.TileHeight * 8 * format.BitsPerPixel;
         if (frameBytes > 0) length = frameBytes;
         start = firstAddress + length * frame;
         return length > 0;
      }

      /// <summary>
      /// Stores the picture of one frame of an uncompressed sheet (the shape ReadOverworldFrame returns). The bytes of a sheet can have been taken for a pointer:
      /// a pointer HexManiac saw in the bytes that change is cleared first, so that nothing keeps pointing from the middle of the picture.
      /// Returns false if the frame can't be stored this way.
      /// </summary>
      public static bool WriteOverworldFrame(IDataModel model, ModelDelta token, ModelTable spriteList, int frame, int[,] pixels) {
         if (pixels == null || !TryLocateOverworldFrame(model, spriteList, frame, out var start, out var length, out var bitsPerPixel)) return false;
         var data = new byte[length];
         SpriteRun.SetPixels(data, 0, pixels, bitsPerPixel);
         var clearedUntil = -1;
         for (int i = 0; i < data.Length; i++) {
            var address = start + i;
            if (model[address] == data[i]) continue;
            if (address < clearedUntil) continue;
            var run = model.GetNextRun(address);
            if (run is PointerRun && run.Start <= address && address < run.Start + run.Length) {
               model.ClearFormat(token, run.Start, run.Length);
               clearedUntil = run.Start + run.Length;
            }
         }
         for (int i = 0; i < data.Length; i++) {
            if (model[start + i] != data[i]) token.ChangeData(model, start + i, data[i]);
         }
         return true;
      }

      #endregion

      #region Trainer front and back pictures

      private void LoadTrainerPics(IDataModel model, List<string> problems) {
         var table = model.GetTableModel(TrainerSpritesTable);
         if (table == null) { problems.Add("This ROM has no trainer picture table."); return; }
         TrainerPicIndex = IndexOf(model.GetOptions(TrainerSpritesTable), TrainerPicName);
         if (TrainerPicIndex < 0 || TrainerPicIndex >= table.Count) { problems.Add($"The trainer picture {TrainerPicName} was not found."); return; }
         var element = table[TrainerPicIndex];
         TrainerEntryAddress = element.Start;

         var front = element.GetSubTable("front")?[0];
         if (front != null) {
            FrontAddress = front.GetAddress("sprite");
            Front = (model.GetNextRun(FrontAddress) as ISpriteRun)?.GetPixels(model, 0, -1);
            FrontPalette = (model.GetNextRun(front.GetAddress("palette")) as IPaletteRun)?.GetPalette(model, 0).ToArray();
         }
         if (!HasFront) problems.Add($"The front picture of {TrainerPicName} could not be read.");

         var back = element.GetSubTable("back")?[0];
         if (back != null) {
            BackAddress = back.GetAddress("image");
            Back = FirstFrame((model.GetNextRun(BackAddress) as ISpriteRun)?.GetPixels(model, 0, -1));
            BackPalette = (model.GetNextRun(back.GetAddress("palette")) as IPaletteRun)?.GetPalette(model, 0).ToArray();
         }
         if (!HasBack) problems.Add($"The back picture of {TrainerPicName} could not be read.");
      }

      /// <summary>The back picture holds several poses stacked on each other (the first is the one shown): keep the top square.</summary>
      public static int[,] FirstFrame(int[,] pixels) {
         if (pixels == null) return null;
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         if (width <= 0 || height <= width) return pixels;
         var result = new int[width, width];
         for (int y = 0; y < width; y++) for (int x = 0; x < width; x++) result[x, y] = pixels[x, y];
         return result;
      }

      #endregion

      #region Drawing

      /// <summary>Draws palette indices with a palette. Colour 0 is the see-through colour: it never matches another colour, so it can stay transparent.</summary>
      public static IPixelViewModel Draw(int[,] pixels, IReadOnlyList<short> palette) {
         if (pixels == null || palette == null || palette.Count == 0) return null;
         var colors = SpriteTool.CreatePaletteWithUniqueTransparentColor(palette);
         var data = SpriteTool.Render(pixels, colors, 0, 0);
         return new ReadonlyPixelViewModel(pixels.GetLength(0), pixels.GetLength(1), data, colors[0]);
      }

      private static IReadOnlyList<short> Painted(IReadOnlyList<short> palette, CharacterRole role, CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary) {
         if (palette == null || role == null) return palette;
         return role.Apply(palette, skin, clothes, secondary);
      }

      /// <summary>One of the shown overworld frames (0 = facing down, 1 = facing up, 2 = facing left; see <see cref="OverworldFrameNumbers"/>) with the skin tone, clothes colour and secondary colour painted on, as the game does when it loads the palette.</summary>
      public IPixelViewModel DrawOverworld(int frame, CharacterRole role, CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary = null) {
         if (!HasOverworld || frame < 0 || frame >= OverworldFrames.Count) return null;
         return Draw(OverworldFrames[frame], Painted(OverworldPalette, role, skin, clothes, secondary));
      }

      public IPixelViewModel DrawFront(CharacterRole role, CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary = null) => HasFront ? Draw(Front, Painted(FrontPalette, role, skin, clothes, secondary)) : null;

      public IPixelViewModel DrawBack(CharacterRole role, CharacterColorRow skin, CharacterColorRow clothes, CharacterColorRow secondary = null) => HasBack ? Draw(Back, Painted(BackPalette, role, skin, clothes, secondary)) : null;

      #endregion
   }

   /// <summary>Which way an overworld character looks.</summary>
   public enum OverworldFacing { Down, Up, Left, Right }

   /// <summary>One overworld frame of a preview: the picture and what it shows ("facing down").</summary>
   public class CharacterFramePreview {
      public string Label { get; }
      public PixelPreview Image { get; }
      public CharacterFramePreview(string label, double scale = 2) => (Label, Image) = (label, new PixelPreview(null, scale));
   }

   /// <summary>
   /// Every picture of the boy's or the girl's overworld sprite sheet (standing, walking, running...) for one image editor tab: the dots on the left of the editor choose the picture.
   /// The sheet is found again by its place in the overworld sprite table every time, so the source keeps working while tables move. Only plain (uncompressed) sheets are handled this way.
   /// </summary>
   public class OverworldSheetFrameSource : IImageFrameSource {
      /// <summary>A sheet that stores its pictures one after the other lists only the first: the game's player sheets have 18 pictures (the walking set and the running set).</summary>
      public const int PlayerSheetFrames = 18;
      private static readonly string[] PlayerFrameNotes = {
         "standing, facing down", "standing, facing up", "standing, facing left (flipped for right)",
         "walking down", "walking down", "walking up", "walking up", "walking left (flipped for right)", "walking left (flipped for right)",
      };

      private readonly IDataModel model;
      private readonly int overworldIndex;
      private readonly int firstAddress;
      private readonly int frameCount;

      public string Title { get; }

      public OverworldSheetFrameSource(IDataModel model, int overworldIndex, string title) {
         (this.model, this.overworldIndex, Title) = (model, overworldIndex, title);
         var list = List;
         firstAddress = list == null ? -1 : model.ReadPointer(list.Run.Start);
         frameCount = CountFrames(list);
      }

      private ModelTable List => CharacterSprites.FindOverworldSpriteList(model, overworldIndex);

      private int CountFrames(ModelTable list) {
         if (list == null) return 0;
         return CharacterSprites.IsRelativeSheet(model, list) ? PlayerSheetFrames : list.Count;
      }

      public bool IsValid {
         get {
            var list = List;
            return list != null && model.ReadPointer(list.Run.Start) == firstAddress && CountFrames(list) == frameCount;
         }
      }

      public int FrameCount => frameCount;

      /// <summary>True if every picture of the sheet is plain uncompressed pictures this source can read and store (compressed sheets are edited one picture at a time instead).</summary>
      public bool Prepare() {
         var list = List;
         if (list == null || frameCount < 1) return false;
         for (int frame = 0; frame < frameCount; frame++) {
            if (!CharacterSprites.TryLocateOverworldFrame(model, list, frame, out _, out _, out _)) return false;
         }
         return model.GetNextRun(firstAddress) is ISpriteRun;
      }

      public string FrameNote(int frame) {
         var list = List;
         if (list == null || !CharacterSprites.IsRelativeSheet(model, list)) return string.Empty;
         var note = frame >= 0 && frame < PlayerFrameNotes.Length ? PlayerFrameNotes[frame] : frame < PlayerSheetFrames ? "running" : string.Empty;
         // the animation table says exactly where each picture is used (and for how long): show it so the running pictures can be told apart
         var use = OverworldAnimations.DescribeFrameUse(CharacterSprites.ReadOverworldAnimations(model, overworldIndex, true), frame);
         return use.Count == 0 ? note : $"{note} - {string.Join(", ", use)}";
      }

      /// <summary>The sheet's own list points at its first picture (which knows the palette); a list with a picture for each frame points at each.</summary>
      public int SpritePointer(int frame) {
         var list = List;
         if (list == null) return -1;
         if (CharacterSprites.IsRelativeSheet(model, list) || frame < 0) return list.Run.Start;
         return list[Math.Min(frame, list.Count - 1)].Start;
      }

      public bool CanChooseWidth => false;
      public int DefaultWidthTiles => 2;
      public int MaxWidthTiles => 2;

      public int[,] ReadFrame(int frame, int widthTiles) {
         var list = List;
         return (list == null ? null : CharacterSprites.ReadOverworldFrame(model, list, frame)) ?? new int[16, 32];
      }

      public void WriteFrame(ModelDelta token, int frame, int widthTiles, int[,] pixels) {
         var list = List;
         if (list != null) CharacterSprites.WriteOverworldFrame(model, token, list, frame, pixels);
      }
   }
}
