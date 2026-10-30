using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.PokemonAnimations {
   /// <summary>
   /// One entry of a species' frontAnimFrames command list (the union AnimCmd of the game).
   /// A frame command shows an image for a number of game frames, jump and loop move the command index, end stops the list.
   /// </summary>
   public readonly struct FrameCommand {
      public const int KindFrame = 0, KindLoop = 1, KindJump = 2, KindEnd = 3;

      public int Kind { get; }

      /// <summary>Frame: the image index (0 or 1). Loop: the repeat count. Jump: the target command index.</summary>
      public int Value { get; }

      /// <summary>Frame only: the raw duration in game frames (0 and 1 both show the image for one frame).</summary>
      public int Duration { get; }
      public bool HFlip { get; }
      public bool VFlip { get; }

      public FrameCommand(int kind, int value, int duration = 0, bool hFlip = false, bool vFlip = false) {
         (Kind, Value, Duration, HFlip, VFlip) = (kind, value, duration, hFlip, vFlip);
      }

      public static FrameCommand Frame(int image, int duration, bool hFlip = false, bool vFlip = false) => new FrameCommand(KindFrame, image, duration, hFlip, vFlip);
      public static FrameCommand End => new FrameCommand(KindEnd, 0);

      /// <summary>Decodes the 4 bytes the game stores for one AnimCmd.</summary>
      public static FrameCommand Parse(uint word) {
         var type = (short)(word & 0xFFFF);
         if (type == -1) return End;
         if (type == -2) return new FrameCommand(KindJump, (int)((word >> 16) & 0x3F));
         if (type == -3) return new FrameCommand(KindLoop, (int)((word >> 16) & 0x3F));
         return new FrameCommand(KindFrame, type, (int)((word >> 16) & 0x3F), ((word >> 22) & 1) != 0, ((word >> 23) & 1) != 0);
      }
   }

   /// <summary>What the game does with a sprite on one game frame (1/60 s), after its movement and frame animation ran.</summary>
   public readonly struct AnimFrameState : IEquatable<AnimFrameState> {
      /// <summary>Which of the sprite's pictures is shown (0 = first, 1 = second).</summary>
      public int Image { get; }
      public int X2 { get; }
      public int Y2 { get; }
      /// <summary>The OAM offset from the sprite's position to the corner of its box (-32 normally, -64 in double-size mode).</summary>
      public int CornerX { get; }
      public int CornerY { get; }
      /// <summary>0 = off, 1 = affine, 3 = affine with a box twice the size of the picture.</summary>
      public int AffineMode { get; }
      public short A { get; }
      public short B { get; }
      public short C { get; }
      public short D { get; }
      public bool Invisible { get; }
      public bool HFlip { get; }
      public bool VFlip { get; }
      /// <summary>An index into <see cref="PokemonAnimationSimulation.Palettes"/>.</summary>
      public int Palette { get; }

      public AnimFrameState(int image, int x2, int y2, int cornerX, int cornerY, int affineMode, short a, short b, short c, short d, bool invisible, bool hFlip, bool vFlip, int palette) {
         (Image, X2, Y2, CornerX, CornerY, AffineMode, A, B, C, D, Invisible, HFlip, VFlip, Palette) = (image, x2, y2, cornerX, cornerY, affineMode, a, b, c, d, invisible, hFlip, vFlip, palette);
      }

      public bool Equals(AnimFrameState other) =>
         Image == other.Image && X2 == other.X2 && Y2 == other.Y2 && CornerX == other.CornerX && CornerY == other.CornerY &&
         AffineMode == other.AffineMode && A == other.A && B == other.B && C == other.C && D == other.D &&
         Invisible == other.Invisible && HFlip == other.HFlip && VFlip == other.VFlip && Palette == other.Palette;

      public override bool Equals(object obj) => obj is AnimFrameState other && Equals(other);

      public override int GetHashCode() {
         unchecked {
            int hash = Image;
            hash = hash * 31 + X2;
            hash = hash * 31 + Y2;
            hash = hash * 31 + AffineMode;
            hash = hash * 31 + A;
            hash = hash * 31 + B;
            hash = hash * 31 + C;
            hash = hash * 31 + D;
            hash = hash * 31 + (Invisible ? 1 : 0);
            hash = hash * 31 + Palette;
            return hash;
         }
      }
   }

   /// <summary>The inputs of one simulated animation.</summary>
   public class PokemonAnimationRequest {
      /// <summary>frontAnimId: which movement the game runs (an ANIM_* id).</summary>
      public int AnimId { get; set; }

      /// <summary>frontAnimDelay: game frames the game waits before it starts the movement.</summary>
      public int Delay { get; set; }

      /// <summary>
      /// The command list of frontAnimFrames. Null or empty means the sprite only has its first picture
      /// (the placeholder list of species that do not use the second picture).
      /// </summary>
      public IReadOnlyList<FrameCommand> FrameCommands { get; set; }

      /// <summary>The sprite palette, 16 colours in the GBA's own order (bit 0-4 red, 5-9 green, 10-14 blue).</summary>
      public ushort[] Palette { get; set; }

      /// <summary>The OAM palette slot of the sprite. Only changes which palette entries the glow code touches.</summary>
      public int PaletteNum { get; set; }

      /// <summary>Safety cap for movements that never finish.</summary>
      public int MaxGameFrames { get; set; } = 1200;

      /// <summary>
      /// The order the game runs its tasks and its sprites in. In the battle the sprites go first (BattleMainCB2), so the movement starts on the later
      /// of the delay and the second game frame. The Pokemon sprite visualizer of the game (CB2_PokemonSpriteVisualizerRunner) runs the tasks first
      /// and is one game frame earlier: true reproduces that. Only used to compare with the game, the editor shows the battle (false).
      /// </summary>
      public bool TasksRunBeforeSprites { get; set; }

      /// <summary>Also keep the sprite's variables of every game frame (<see cref="PokemonAnimationSimulation.SpriteData"/>). For tests and debugging.</summary>
      public bool RecordSpriteData { get; set; }
   }

   /// <summary>The result of running the game's code for one animation: one state per game frame.</summary>
   public class PokemonAnimationSimulation {
      /// <summary>One entry per game frame, from the frame the animation starts to the frame the game considers it finished.</summary>
      public AnimFrameState[] Frames { get; set; }

      /// <summary>The distinct palettes the sprite showed (16 GBA colours each). Frame states point to them by index.</summary>
      public ushort[][] Palettes { get; set; }

      /// <summary>True if the animation was cut off at <see cref="PokemonAnimationRequest.MaxGameFrames"/>.</summary>
      public bool HitFrameLimit { get; set; }

      /// <summary>True if the movement id is not one the game knows. Only the frame list animates then.</summary>
      public bool UnknownAnimId { get; set; }

      /// <summary>
      /// How often the game's sine table was read outside of its 320 entries.
      /// The game reads whatever is stored next to the table then, which an editor cannot know: this counts the guesses made.
      /// </summary>
      public int OutOfRangeSineReads { get; set; }

      /// <summary>True if the movement code failed (division by zero, bad index). The animation is cut off where it failed.</summary>
      public bool Faulted { get; set; }

      /// <summary>
      /// Only if requested: per game frame the sprite's data[0..7], then animNum, animCmdIndex, animDelayCounter, animEnded.
      /// </summary>
      public short[][] SpriteData { get; set; }
   }

   /// <summary>Entry of the array-of-structs tables of the game (struct YellowFlashData).</summary>
   public sealed class YellowFlashData {
      public readonly bool isYellow;
      public readonly byte time;
      public YellowFlashData(bool isYellow, byte time) => (this.isYellow, this.time) = (isYellow, time);
   }

   /// <summary>
   /// A port of the movement code the pokeemerald-expansion runs on front sprites (src/pokemon_animation.c) together with the little of
   /// src/sprite.c it depends on (frame command sequencing, affine matrices). The movement functions themselves are generated from the C source
   /// (see PokemonAnimationEngine.Generated.cs); this file is the hand written part: the C integer semantics, the sprite, the palette blending,
   /// BIOS ObjAffineSet, and the loop that runs one game frame after the other.
   /// One instance simulates one animation, it is not thread safe and not meant to be reused.
   /// </summary>
   public sealed partial class PokemonAnimationEngine {
      public const int GameFramesPerSecond = 60;
      private const int TRUE = 1, FALSE = 0;
      private const int MAX_BATTLERS_COUNT = 4;

      // constants/rgb.h: RGB(r, g, b) with 5 bits per channel
      private const int RGB_BLACK = 0;
      private const int RGB_WHITE = 31 | (31 << 5) | (31 << 10);
      private const int RGB_RED = 31;
      private const int RGB_GREEN = 31 << 5;
      private const int RGB_BLUE = 31 << 10;
      private const int RGB_PURPLE = 24 | (24 << 10);
      private const int RGB_YELLOW = 31 | (31 << 5);

      private const int SHAKEGLOW_RED = 0, SHAKEGLOW_GREEN = 1, SHAKEGLOW_BLUE = 2, SHAKEGLOW_BLACK = 3, SHAKEGLOW_WHITE = 4, SHAKEGLOW_PURPLE = 5;
      private static readonly ushort[] sColors = { RGB_RED, RGB_GREEN, RGB_BLUE, RGB_BLACK, RGB_WHITE, RGB_PURPLE };

      private const int ST_OAM_AFFINE_OFF = 0, ST_OAM_AFFINE_NORMAL = 1, ST_OAM_AFFINE_DOUBLE = 3;

      #region C-compatible types

      /// <summary>sprite->data[]: eight s16 values. Writes truncate to 16 bits like the game's variables do.</summary>
      public sealed class SpriteData {
         private readonly short[] values = new short[8];
         public int this[int index] {
            get => values[index];
            set => values[index] = unchecked((short)value);
         }
      }

      public sealed class OamData {
         public int paletteNum;
         public int affineMode = ST_OAM_AFFINE_NORMAL;
         public int matrixNum;
      }

      public sealed class PokemonAnimData {
         private ushort _delay;
         private short _speed, _runs, _rotation, _data;
         public int delay { get => _delay; set => _delay = unchecked((ushort)value); }
         public int speed { get => _speed; set => _speed = unchecked((short)value); }
         public int runs { get => _runs; set => _runs = unchecked((short)value); }
         public int rotation { get => _rotation; set => _rotation = unchecked((short)value); }
         public int data { get => _data; set => _data = unchecked((short)value); }
      }

      public sealed class Sprite {
         public readonly SpriteData data = new SpriteData();
         public readonly OamData oam = new OamData();
         public Action<Sprite> callback;

         private short _x2, _y2;
         private sbyte _cx, _cy;
         private bool _invisible;
         public int x2 { get => _x2; set => _x2 = unchecked((short)value); }
         public int y2 { get => _y2; set => _y2 = unchecked((short)value); }
         public int centerToCornerVecX { get => _cx; set => _cx = unchecked((sbyte)value); }
         public int centerToCornerVecY { get => _cy; set => _cy = unchecked((sbyte)value); }
         public int invisible { get => _invisible ? 1 : 0; set => _invisible = (value & 1) != 0; }

         /// <summary>#define sDontFlip data[1]: TRUE for a normal animation, FALSE for the summary screen.</summary>
         public int sDontFlip { get => data[1]; set => data[1] = value; }

         // frame command sequencing (sprite.c)
         public bool animBeginning, animEnded, animPaused;
         public int animNum;
         public byte animCmdIndex;
         public int animDelayCounter;
         public int animLoopCounter;
         public int image;
         public bool hFlip, vFlip;

         // affine animation (sprite.c): the mon animations only ever use sMonAffineAnims, which hold a single frame
         public bool affineAnimBeginning, affineAnimPaused;
         public int affineAnimNum;
      }

      #endregion

      private readonly Sprite sprite = new Sprite();
      private readonly PokemonAnimData[] sAnims = new PokemonAnimData[MAX_BATTLERS_COUNT];
      private byte sAnimIdx;
      private int sIsSummaryAnim;

      private readonly ushort[] gPlttBufferUnfaded = new ushort[512];
      private readonly ushort[] gPlttBufferFaded = new ushort[512];
      private readonly short[] gOamMatrices = new short[32 * 4];
      private int outOfRangeSineReads;

      private PokemonAnimationEngine() {
         for (int i = 0; i < sAnims.Length; i++) sAnims[i] = new PokemonAnimData();
         dummyCallback = SpriteCallbackDummy;
      }

      #region trig.c

      private int SineAt(int index) {
         if (index < 0 || index >= gSineTable.Length) {
            // the game reads whatever is stored around the table: it cannot be known here
            outOfRangeSineReads++;
            index &= 0xFF;
         }
         return gSineTable[index];
      }

      /// <summary>s16 Sin(s16 index, s16 amplitude): amplitude * sin(index*(pi/128))</summary>
      private int Sin(int index, int amplitude) {
         int i = unchecked((short)index);
         int a = unchecked((short)amplitude);
         return unchecked((short)((a * SineAt(i)) >> 8));
      }

      /// <summary>s16 Cos(s16 index, s16 amplitude): amplitude * cos(index*(pi/128))</summary>
      private int Cos(int index, int amplitude) {
         int i = unchecked((short)index);
         int a = unchecked((short)amplitude);
         return unchecked((short)((a * SineAt(i + 64)) >> 8));
      }

      private static int ToU16(int value) => unchecked((ushort)value);

      /// <summary>Integer division like the game's runtime does it: dividing by zero gives 0 instead of failing.</summary>
      private static int SafeDiv(int numerator, int denominator) => denominator == 0 ? 0 : numerator / denominator;

      #endregion

      #region palette (util.c, constants/rgb.h)

      private static int OBJ_PLTT_ID(int n) => 256 + n * 16;

      private static int RGB(int r, int g, int b) => r | (g << 5) | (b << 10);

      /// <summary>
      /// void BlendPalette(u16 palOffset, u16 numEntries, u8 coeff, u32 blendColor):
      /// every colour moves coeff/16 of the way from its original to the blend colour, one channel at a time.
      /// </summary>
      private void BlendPalette(int palOffset, int numEntries, int coeff, int blendColor) {
         int offset = ToU16(palOffset), count = ToU16(numEntries);
         int c = coeff & 0xFF;
         int tr = blendColor & 31, tg = (blendColor >> 5) & 31, tb = (blendColor >> 10) & 31;
         if (offset + count > gPlttBufferFaded.Length) return;
         for (int i = 0; i < count; i++) {
            int index = i + offset;
            int color = gPlttBufferUnfaded[index];
            int r = color & 31, g = (color >> 5) & 31, b = (color >> 10) & 31;
            gPlttBufferFaded[index] = unchecked((ushort)RGB(
               r + (((tr - r) * c) >> 4),
               g + (((tg - g) * c) >> 4),
               b + (((tb - b) * c) >> 4)));
         }
      }

      /// <summary>The GlowColor macro of pokemon_animation.c.</summary>
      private void GlowColor(Sprite sprite, int color, int colorIncrement, int speed) {
         if (sprite.data[2] == 0)
            sprite.data[7] = OBJ_PLTT_ID(sprite.oam.paletteNum);

         if (sprite.data[2] > 128) {
            BlendPalette(sprite.data[7], 16, 0, color);
            sprite.callback = WaitAnimEnd;
         } else {
            sprite.data[6] = Sin(sprite.data[2], colorIncrement);
            BlendPalette(sprite.data[7], 16, sprite.data[6], color);
         }
         sprite.data[2] += speed;
      }

      #endregion

      #region affine

      /// <summary>
      /// BIOS ObjAffineSet for one entry, written the way the emulator used for the comparison with the game (mGBA) does it:
      /// 256 steps of rotation, float maths, results truncated toward zero. A real BIOS can differ from it by 1 in a matrix entry.
      /// </summary>
      public static void ObjAffineSet(short xScale, short yScale, ushort rotation, out short a, out short b, out short c, out short d) {
         float sx = xScale / 256f;
         float sy = yScale / 256f;
         float theta = (float)((float)((rotation >> 8) / 128f) * Math.PI);
         float cos = MathF.Cos(theta);
         float sin = MathF.Sin(theta);
         float fa = cos, fd = cos, fb = sin, fc = sin;
         fa *= sx;
         fb *= -sx;
         fc *= sy;
         fd *= sy;
         a = unchecked((short)(int)(fa * 256));
         b = unchecked((short)(int)(fb * 256));
         c = unchecked((short)(int)(fc * 256));
         d = unchecked((short)(int)(fd * 256));
      }

      private void SetOamMatrix(int matrixNum, short a, short b, short c, short d) {
         gOamMatrices[matrixNum * 4] = a;
         gOamMatrices[matrixNum * 4 + 1] = b;
         gOamMatrices[matrixNum * 4 + 2] = c;
         gOamMatrices[matrixNum * 4 + 3] = d;
      }

      private static int ConvertScaleParam(int scale) {
         short s = unchecked((short)scale);
         return s == 0 ? 0 : unchecked((short)(0x10000 / s));
      }

      private void SetAffineData(Sprite sprite, int xScale, int yScale, int rotation) {
         ObjAffineSet(unchecked((short)xScale), unchecked((short)yScale), unchecked((ushort)rotation), out var a, out var b, out var c, out var d);
         SetOamMatrix(sprite.oam.matrixNum, a, b, c, d);
      }

      private void HandleSetAffineData(Sprite sprite, int xScale, int yScale, int rotation) {
         short xs = unchecked((short)xScale);
         ushort rot = unchecked((ushort)rotation);
         if (sprite.sDontFlip == 0) {
            xs = unchecked((short)(xs * -1));
            rot = unchecked((ushort)(rot * -1));
         }
         SetAffineData(sprite, xs, yScale, rot);
      }

      private void CalcCenterToCornerVec(Sprite sprite, int affineMode) {
         // sCenterToCornerVecTable for a 64x64 square: -32 stored as u8, doubled (as u8) in double-size mode, read back as s8
         byte x = unchecked((byte)-32), y = unchecked((byte)-32);
         if ((affineMode & 2) != 0) {
            x = unchecked((byte)(x * 2));
            y = unchecked((byte)(y * 2));
         }
         sprite.centerToCornerVecX = unchecked((sbyte)x);
         sprite.centerToCornerVecY = unchecked((sbyte)y);
      }

      private void StartSpriteAffineAnim(Sprite sprite, int animNum) {
         sprite.affineAnimNum = animNum;
         sprite.affineAnimBeginning = true;
      }

      private void InitSpriteAffineAnim(Sprite sprite) {
         CalcCenterToCornerVec(sprite, sprite.oam.affineMode);
         sprite.oam.matrixNum = 0;
         sprite.affineAnimBeginning = true;
      }

      private void FreeOamMatrix(int matrixNum) => SetOamMatrix(matrixNum, 0x100, 0, 0, 0x100);

      private void HandleStartAffineAnim(Sprite sprite) {
         sprite.oam.affineMode = ST_OAM_AFFINE_DOUBLE;

         if (sIsSummaryAnim == TRUE)
            InitSpriteAffineAnim(sprite);

         if (sprite.sDontFlip == 0)
            StartSpriteAffineAnim(sprite, 1);
         else
            StartSpriteAffineAnim(sprite, 0);

         CalcCenterToCornerVec(sprite, sprite.oam.affineMode);
         sprite.affineAnimPaused = true;
      }

      private void TryFlipX(Sprite sprite) {
         if (sprite.sDontFlip == 0)
            sprite.x2 *= -1;
      }

      private void ResetSpriteAfterAnim(Sprite sprite) {
         sprite.oam.affineMode = ST_OAM_AFFINE_NORMAL;
         CalcCenterToCornerVec(sprite, sprite.oam.affineMode);

         if (sIsSummaryAnim == TRUE) {
            sprite.hFlip = sprite.sDontFlip == 0;
            FreeOamMatrix(sprite.oam.matrixNum);
            sprite.oam.affineMode = ST_OAM_AFFINE_OFF;
         }
      }

      /// <summary>BeginAffineAnim: the first frame of sMonAffineAnim_0/1 (scale 256 or -256, no rotation) replaces the matrix.</summary>
      private void BeginAffineAnim(Sprite sprite) {
         if ((sprite.oam.affineMode & 1) == 0) return;
         sprite.affineAnimBeginning = false;
         int xScale = sprite.affineAnimNum == 1 ? -256 : 256;
         ObjAffineSet(unchecked((short)ConvertScaleParam(xScale)), unchecked((short)ConvertScaleParam(256)), 0, out var a, out var b, out var c, out var d);
         SetOamMatrix(sprite.oam.matrixNum, a, b, c, d);
      }

      #endregion

      #region movement helpers (the hand written part of pokemon_animation.c)

      private void SpriteCallbackDummy(Sprite sprite) { }
      private Action<Sprite> dummyCallback;

      private void SetPosForRotation(Sprite sprite, int index, int amplitudeX, int amplitudeY) {
         int ax = unchecked((short)amplitudeX), ay = unchecked((short)amplitudeY);
         int idx = unchecked((ushort)index);

         ax = unchecked((short)(ax * -1));
         ay = unchecked((short)(ay * -1));

         int xAdder = unchecked((short)(Cos(idx, ax) - Sin(idx, ay)));
         int yAdder = unchecked((short)(Cos(idx, ay) + Sin(idx, ax)));

         ax = unchecked((short)(ax * -1));
         ay = unchecked((short)(ay * -1));

         sprite.x2 = xAdder + ax;
         sprite.y2 = yAdder + ay;
      }

      private bool InitAnimData(int id) {
         if (id >= MAX_BATTLERS_COUNT)
            return false;
         sAnims[id].rotation = 0;
         sAnims[id].delay = 0;
         sAnims[id].runs = 1;
         sAnims[id].speed = 0;
         sAnims[id].data = 0;
         return true;
      }

      private byte AddNewAnim() {
         sAnimIdx = unchecked((byte)((sAnimIdx + 1) % MAX_BATTLERS_COUNT));
         InitAnimData(sAnimIdx);
         return sAnimIdx;
      }

      #endregion

      #region frame command sequencing (sprite.c)

      private FrameCommand[][] anims;

      private FrameCommand CurrentCommand(Sprite sprite) {
         var list = anims[sprite.animNum];
         if (sprite.animCmdIndex >= list.Length) return FrameCommand.End;
         return list[sprite.animCmdIndex];
      }

      private void SetImage(Sprite sprite, FrameCommand cmd) {
         int duration = cmd.Duration;
         if (duration != 0) duration--;
         sprite.animDelayCounter = duration;
         sprite.image = cmd.Value;
         if ((sprite.oam.affineMode & 1) == 0) {
            sprite.hFlip = cmd.HFlip;
            sprite.vFlip = cmd.VFlip;
         }
      }

      private void BeginAnim(Sprite sprite) {
         sprite.animCmdIndex = 0;
         sprite.animEnded = false;
         sprite.animLoopCounter = 0;
         var cmd = CurrentCommand(sprite);
         if (cmd.Kind == FrameCommand.KindFrame) {
            sprite.animBeginning = false;
            SetImage(sprite, cmd);
         }
      }

      private void ContinueAnim(Sprite sprite) {
         if (sprite.animDelayCounter != 0) {
            if (!sprite.animPaused) sprite.animDelayCounter--;
            var cmd = CurrentCommand(sprite);
            if ((sprite.oam.affineMode & 1) == 0) {
               sprite.hFlip = cmd.HFlip;
               sprite.vFlip = cmd.VFlip;
            }
         } else if (!sprite.animPaused) {
            sprite.animCmdIndex = unchecked((byte)(sprite.animCmdIndex + 1));
            var cmd = CurrentCommand(sprite);
            switch (cmd.Kind) {
               case FrameCommand.KindLoop:
                  if (sprite.animLoopCounter != 0) {
                     sprite.animLoopCounter--;
                  } else {
                     sprite.animLoopCounter = cmd.Value;
                  }
                  JumpToTopOfAnimLoop(sprite);
                  ContinueAnim(sprite);
                  break;
               case FrameCommand.KindJump:
                  sprite.animCmdIndex = unchecked((byte)cmd.Value);
                  SetImage(sprite, CurrentCommand(sprite));
                  break;
               case FrameCommand.KindEnd:
                  sprite.animCmdIndex = unchecked((byte)(sprite.animCmdIndex - 1));
                  sprite.animEnded = true;
                  break;
               default:
                  SetImage(sprite, cmd);
                  break;
            }
         }
      }

      private void JumpToTopOfAnimLoop(Sprite sprite) {
         if (sprite.animLoopCounter != 0) {
            sprite.animCmdIndex = unchecked((byte)(sprite.animCmdIndex - 1));
            while (IsLoopEnd(sprite, sprite.animCmdIndex - 1) == false) {
               if (sprite.animCmdIndex == 0) break;
               sprite.animCmdIndex = unchecked((byte)(sprite.animCmdIndex - 1));
            }
            sprite.animCmdIndex = unchecked((byte)(sprite.animCmdIndex - 1));
         }
      }

      private bool IsLoopEnd(Sprite sprite, int index) {
         var list = anims[sprite.animNum];
         return index >= 0 && index < list.Length && list[index].Kind == FrameCommand.KindLoop;
      }

      /// <summary>void AnimateSprite(struct Sprite *sprite)</summary>
      private void AnimateSprite(Sprite sprite) {
         if (sprite.animBeginning) BeginAnim(sprite); else ContinueAnim(sprite);
         if (sprite.affineAnimBeginning) BeginAffineAnim(sprite);
      }

      private void StartSpriteAnim(Sprite sprite, int animNum) {
         sprite.animNum = animNum;
         sprite.animBeginning = true;
         sprite.animEnded = false;
      }

      #endregion

      #region running

      /// <summary>
      /// Runs the game's code for the front animation of a species and records the sprite state of every game frame.
      /// The frame commands start on the first game frame and the movement on the later of the delay and the second game frame, like the battle's
      /// DoMonFrontSpriteAnimation does. The simulation ends when the game's task for the animation is done.
      /// </summary>
      public static PokemonAnimationSimulation Simulate(PokemonAnimationRequest request) {
         return new PokemonAnimationEngine().Run(request);
      }

      private PokemonAnimationSimulation Run(PokemonAnimationRequest request) {
         var result = new PokemonAnimationSimulation();
         var states = new List<AnimFrameState>();
         var palettes = new List<ushort[]>();

         // palette
         int paletteNum = Math.Max(0, Math.Min(15, request.PaletteNum));
         sprite.oam.paletteNum = paletteNum;
         int paletteBase = OBJ_PLTT_ID(paletteNum);
         for (int i = 0; i < 16; i++) {
            var color = request.Palette != null && i < request.Palette.Length ? request.Palette[i] : (ushort)0;
            gPlttBufferUnfaded[paletteBase + i] = color;
            gPlttBufferFaded[paletteBase + i] = color;
         }

         // the sprite's frame commands: anim 0 is sAnim_GeneralFrame0, anim 1 is the species' list (with the END the macro appends)
         var list = new List<FrameCommand>();
         if (request.FrameCommands != null && request.FrameCommands.Count > 0) {
            foreach (var cmd in request.FrameCommands) {
               list.Add(cmd);
               if (cmd.Kind == FrameCommand.KindEnd) break;
            }
            if (list.Count == 0 || list[list.Count - 1].Kind != FrameCommand.KindEnd) list.Add(FrameCommand.End);
         }
         anims = new[] {
            new[] { FrameCommand.Frame(0, 0), FrameCommand.End },
            list.Count > 0 ? list.ToArray() : new[] { FrameCommand.Frame(0, 0), FrameCommand.End },
         };

         // a battle sprite starts as an affine sprite with an identity matrix
         sprite.oam.affineMode = ST_OAM_AFFINE_NORMAL;
         sprite.oam.matrixNum = 0;
         CalcCenterToCornerVec(sprite, sprite.oam.affineMode);
         SetOamMatrix(0, 0x100, 0, 0, 0x100);
         sprite.data[0] = 0;
         sprite.callback = dummyCallback;
         // the sprite's own picture animation (a single frame) has long finished when the front animation starts
         sprite.animBeginning = false;
         sprite.animEnded = true;

         var movement = GetMonAnimFunction(request.AnimId);
         result.UnknownAnimId = movement == null;
         if (movement == null) movement = WaitAnimEnd;
         bool hasFrameList = list.Count > 0;
         bool tasksFirst = request.TasksRunBeforeSprites;
         var spriteData = request.RecordSpriteData ? new List<short[]>() : null;
         int max = Math.Max(1, request.MaxGameFrames);

         // DoMonFrontSpriteAnimation: with a delay the game creates Task_AnimateAfterDelay, which counts the delay down and then creates
         // Task_HandleMonAnimation (priority 128, so it runs after the delay task). Without a delay it creates Task_HandleMonAnimation at once.
         int delayLeft = request.Delay;
         bool delayTask = false, handleTask = false, handleStarted = false;
         int savedPaletteNum = 0, savedSpeciesId = 0;

         void RunTasks() {
            if (delayTask && --delayLeft == 0) {
               delayTask = false;
               handleTask = true;
            }
            if (handleTask) {
               if (!handleStarted) {
                  // Task_HandleMonAnimation, state 0
                  savedPaletteNum = sprite.oam.paletteNum;
                  savedSpeciesId = sprite.data[2];
                  sprite.sDontFlip = TRUE;
                  sprite.data[0] = 0;
                  for (int i = 2; i < 8; i++) sprite.data[i] = 0;
                  sprite.callback = movement;
                  sIsSummaryAnim = FALSE;
                  handleStarted = true;
               }
               if (sprite.callback == dummyCallback) {
                  sprite.data[0] = savedPaletteNum;
                  sprite.data[2] = savedSpeciesId;
                  sprite.data[1] = 0;
                  handleTask = false;
               }
            }
         }

         int lastPalette = -1;
         try {
            for (int frame = 0; frame < max; frame++) {
               if (frame == 0) {
                  // DoMonFrontSpriteAnimation
                  if (hasFrameList) StartSpriteAnim(sprite, 1);
                  if (request.Delay != 0) delayTask = true;
                  else handleTask = true;
               }

               if (tasksFirst) RunTasks();
               sprite.callback(sprite);
               AnimateSprite(sprite);
               if (!tasksFirst) RunTasks();

               // record
               if (lastPalette < 0 || !SamePalette(palettes[lastPalette], paletteBase)) {
                  var snapshot = new ushort[16];
                  Array.Copy(gPlttBufferFaded, paletteBase, snapshot, 0, 16);
                  palettes.Add(snapshot);
                  lastPalette = palettes.Count - 1;
               }
               int m = sprite.oam.matrixNum * 4;
               states.Add(new AnimFrameState(
                  sprite.image, sprite.x2, sprite.y2, sprite.centerToCornerVecX, sprite.centerToCornerVecY, sprite.oam.affineMode,
                  gOamMatrices[m], gOamMatrices[m + 1], gOamMatrices[m + 2], gOamMatrices[m + 3],
                  sprite.invisible != 0, sprite.hFlip, sprite.vFlip, lastPalette));

               if (spriteData != null) {
                  var record = new short[12];
                  for (int i = 0; i < 8; i++) record[i] = (short)sprite.data[i];
                  record[8] = (short)sprite.animNum;
                  record[9] = sprite.animCmdIndex;
                  record[10] = (short)sprite.animDelayCounter;
                  record[11] = (short)(sprite.animEnded ? 1 : 0);
                  spriteData.Add(record);
               }

               if (handleStarted && sprite.callback == dummyCallback) break;
               if (frame == max - 1) result.HitFrameLimit = true;
            }
         } catch (Exception ex) when (ex is DivideByZeroException || ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException) {
            result.Faulted = true;
         }

         result.Frames = states.ToArray();
         result.SpriteData = spriteData?.ToArray();
         result.Palettes = palettes.ToArray();
         result.OutOfRangeSineReads = outOfRangeSineReads;
         return result;
      }

      private bool SamePalette(ushort[] snapshot, int paletteBase) {
         for (int i = 0; i < 16; i++) if (snapshot[i] != gPlttBufferFaded[paletteBase + i]) return false;
         return true;
      }

      #endregion
   }
}
