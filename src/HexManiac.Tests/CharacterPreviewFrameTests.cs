using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Which overworld frames the Character Customization previews show (standing still facing down, up and left: frames 0, 1 and 2 of the player's sheet,
   /// as pokeemerald's sAnim_FaceSouth / sAnim_FaceNorth / sAnim_FaceWest), how big one frame of a sheet is, and when the tab shows its two columns.
   /// </summary>
   public class CharacterPreviewFrameTests {
      [Fact]
      public void PreviewFrames_AreStandingFacingDownUpAndLeft_WithNoWalkingFrames() {
         Assert.Equal(new[] { 0, 1, 2 }, CharacterSprites.OverworldFrameNumbers);
         Assert.Equal(new[] { "facing down", "facing up", "facing left" }, CharacterSprites.OverworldFrameLabels);
         Assert.DoesNotContain(CharacterSprites.OverworldFrameNumbers, frame => frame >= 3); // 3 to 8 are the walking frames
      }

      [Fact]
      public void StandingFrame_MatchesTheGamesFacingAnimations() {
         Assert.Equal(0, CharacterSprites.StandingFrame(OverworldFacing.Down));
         Assert.Equal(1, CharacterSprites.StandingFrame(OverworldFacing.Up));
         Assert.Equal(2, CharacterSprites.StandingFrame(OverworldFacing.Left));
         Assert.Equal(2, CharacterSprites.StandingFrame(OverworldFacing.Right)); // right is left, flipped
         Assert.Equal(CharacterSprites.OverworldFrameNumbers, new[] { OverworldFacing.Down, OverworldFacing.Up, OverworldFacing.Left }.Select(CharacterSprites.StandingFrame));
      }

      [Theory]
      [InlineData(256, 2, 36, 4, 2, 4)]   // the player's sheet as CUBE formats it: 9 frames of 16x32 stacked
      [InlineData(256, 2, 4, 4, 2, 4)]    // a picture that is one frame already
      [InlineData(512, 4, 4, 4, 4, 4)]    // 32x32 frames (the bikes)
      [InlineData(0, 2, 36, 4, 2, 36)]    // size unknown: the picture's own size
      [InlineData(100, 2, 36, 4, 2, 36)]  // not whole tiles: the picture's own size
      [InlineData(96, 2, 36, 4, 2, 36)]   // 3 tiles can't be 2 tiles wide: the picture's own size
      [InlineData(512, 2, 36, 8, 2, 4)]   // 8 bits per pixel: 16x32 is 512 bytes
      public void FrameTileSize_IsTheListedSizeAtThePicturesWidth(int frameBytes, int tileWidth, int tileHeight, int bitsPerPixel, int expectedWidth, int expectedHeight) {
         Assert.Equal((expectedWidth, expectedHeight), CharacterSprites.FrameTileSize(frameBytes, tileWidth, tileHeight, bitsPerPixel));
      }

      [Fact]
      public void PreviewPanel_HasOneLabelledPicturePerShownFrame() {
         var panel = new CharacterPreviewPanel(null, CharacterRole.Male);
         Assert.Equal(CharacterSprites.OverworldFrameLabels, panel.OverworldFrames.Select(frame => frame.Label));
         Assert.Same(panel.OverworldFrames[0].Image, panel.Standing);
         panel.SetScale(5);
         Assert.All(panel.OverworldFrames, frame => Assert.Equal(5, frame.Image.SpriteScale));
         Assert.Equal(5, panel.Front.SpriteScale);
         Assert.Equal(5, panel.Back.SpriteScale);
      }

      [Theory]
      [InlineData(2544, true)]
      [InlineData(1200, true)]
      [InlineData(CharacterCustomizationTab.TwoColumnMinWidth, true)]
      [InlineData(CharacterCustomizationTab.TwoColumnMinWidth - 1, false)]
      [InlineData(500, false)]
      [InlineData(0, false)]
      [InlineData(double.NaN, false)]
      public void TwoColumns_OnlyWhenTheListsAndThePreviewsBothFit(double width, bool expected) {
         Assert.Equal(expected, CharacterCustomizationTab.UseTwoColumns(width));
         Assert.True(CharacterCustomizationTab.TwoColumnMinWidth >= 440 + 400); // a list card and the previews
      }

      /// <summary>The real CUBE ROM: the player's sheet is one 16x288 picture, the previews still show single 16x32 frames (they used to show the whole sheet).</summary>
      [SkippableFact]
      public void Cube_PreviewFramesAreSixteenByThirtyTwo_AndAreTheFirstThreeFramesOfTheSheet() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(toml)));
         model.InitializationWorkload.Wait();

         foreach (var gender in new[] { CharacterRole.Male, CharacterRole.Female }) {
            var sprites = CharacterSprites.Load(model, gender);
            Assert.True(sprites.HasOverworld, string.Join(" ", sprites.Problems));
            Assert.Equal(3, sprites.OverworldFrames.Count);
            var list = CharacterSprites.FindOverworldSpriteList(model, sprites.OverworldIndex);
            Assert.True(CharacterSprites.IsRelativeSheet(model, list));
            var sheet = model.ReadPointer(list.Run.Start);
            for (int i = 0; i < 3; i++) {
               var frame = sprites.OverworldFrames[i];
               Assert.Equal(16, frame.GetLength(0));
               Assert.Equal(32, frame.GetLength(1));
               // the first byte of frame i holds its first two pixels
               var first = model[sheet + 256 * i];
               Assert.Equal(first & 0xF, frame[0, 0]);
               Assert.Equal(first >> 4, frame[1, 0]);
            }
            Assert.NotNull(sprites.DrawOverworld(2, null, null, null));
            Assert.Null(sprites.DrawOverworld(3, null, null, null));
         }
      }
   }
}
