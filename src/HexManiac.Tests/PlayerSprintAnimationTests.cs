using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The player's sprinting (running) pictures: the player's overworld sheet holds 18 pictures (9 walking, then 9 running), but only the walking half was ever shown or editable
   /// in the OW Sprites editor, because the animation table was only read up to its 16th animation (the running animations are number 20 to 23).
   /// </summary>
   public class PlayerSprintAnimationTests {
      [Theory]
      [InlineData(0x0000000C | (5 << 16), OverworldAnimCommandKind.Frame, 12, 5, false, false)]
      [InlineData(0x00000010 | (5 << 16) | (1 << 22), OverworldAnimCommandKind.Frame, 16, 5, true, false)]
      [InlineData(0x00000002 | (16 << 16) | (1 << 23), OverworldAnimCommandKind.Frame, 2, 16, false, true)]
      public void FrameCommands_DecodeAndEncode(int word, OverworldAnimCommandKind kind, int frame, int duration, bool hFlip, bool vFlip) {
         var command = OverworldAnimCommand.Decode(word);
         Assert.Equal(kind, command.Kind);
         Assert.Equal((frame, duration, hFlip, vFlip), (command.Frame, command.Duration, command.HFlip, command.VFlip));
         Assert.Equal(word, command.Encode());
      }

      [Fact]
      public void JumpLoopAndEnd_DecodeAndEncode() {
         var jump = OverworldAnimCommand.Decode(0x0003FFFE);
         Assert.Equal((OverworldAnimCommandKind.Jump, 3), (jump.Kind, jump.Argument));
         Assert.Equal(0x0003FFFE, jump.Encode());
         var loop = OverworldAnimCommand.Decode(0x0002FFFD);
         Assert.Equal((OverworldAnimCommandKind.Loop, 2), (loop.Kind, loop.Argument));
         Assert.Equal(0x0002FFFD, loop.Encode());
         Assert.Equal(OverworldAnimCommandKind.End, OverworldAnimCommand.Decode(0x0000FFFF).Kind);
         Assert.Equal(0xFFFF, new OverworldAnimCommand(OverworldAnimCommandKind.End).Encode());
      }

      [Fact]
      public void Names_FollowTheGamesConstants() {
         Assert.Equal("stand facing south", OverworldAnimations.Name(0, true));
         Assert.Equal("walk west", OverworldAnimations.Name(6, true));
         Assert.Equal("walk fastest east", OverworldAnimations.Name(19, false));
         Assert.Equal("run south", OverworldAnimations.Name(20, true));
         Assert.Equal("run east", OverworldAnimations.Name(23, true));
         Assert.Equal("spin north", OverworldAnimations.Name(25, true));
         Assert.Equal("animation 20", OverworldAnimations.Name(20, false)); // other sprites use those numbers for other things (bunny hops...)
      }

      [Fact]
      public void ReadStopsAtTheLimit_SoANeighbouringTableIsNotMistakenForTheSameOne() {
         var model = new PokemonModel(new byte[0x400], null, BaseViewModelTestClass.Singletons);
         // two tables of 4 pointers each, back to back: the first shows pictures up to 2, the second up to 30
         for (int i = 0; i < 8; i++) {
            model.WritePointer(new ModelDelta(), 0x100 + 4 * i, 0x200 + 8 * i);
            model.WriteMultiByteValue(0x200 + 8 * i, 4, new ModelDelta(), i < 4 ? 2 : 30);
            model.WriteMultiByteValue(0x204 + 8 * i, 4, new ModelDelta(), 0xFFFF);
         }
         Assert.Equal(2, OverworldAnimations.BiggestFrame(OverworldAnimations.Read(model, 0x100, 16)));
         Assert.Equal(30, OverworldAnimations.BiggestFrame(OverworldAnimations.Read(model, 0x100)));
         Assert.Empty(OverworldAnimations.Read(model, -1));
         Assert.Empty(OverworldAnimations.Read(model, 0x3FF));
      }

      #region the real CUBE ROM

      private static HardcodeTablesModel LoadCube(bool metadataRoundTrip = false, string tomlOverride = null) {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = tomlOverride ?? Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(toml)));
         model.InitializationWorkload.Wait();
         if (!metadataRoundTrip) return model;
         // what a user does: save the ROM with its toml, open both again
         var saved = model.ExportMetadata(BaseViewModelTestClass.Singletons.MetadataInfo).Serialize();
         var again = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, model.RawData, new StoredMetadata(saved));
         again.InitializationWorkload.Wait();
         return again;
      }

      private static int IndexOf(IDataModel model, string name) => model.GetOptions(HardcodeTablesModel.OverworldSprites).ToList().IndexOf(name);

      private static readonly string[] PlayerSheets = { "BRENDAN_NORMAL", "MAY_NORMAL", "RIVAL_BRENDAN_NORMAL", "RIVAL_MAY_NORMAL" };

      private static void AssertSheetHasAllEighteenPictures(IDataModel model) {
         foreach (var name in PlayerSheets) {
            var index = IndexOf(model, name);
            Assert.True(index >= 0, name + " index");
            var list = CharacterSprites.FindOverworldSpriteList(model, index);
            Assert.True(list != null, name + " list");
            Assert.True(CharacterSprites.IsRelativeSheet(model, list), name + " relative");
            var sheet = model.GetNextRun(model.ReadPointer(list.Run.Start)) as SpriteRun;
            Assert.NotNull(sheet);
            Assert.Equal(18 * 256, sheet.Length);
            Assert.Equal(2, sheet.SpriteFormat.TileWidth);
            Assert.Equal(4 * 18, sheet.SpriteFormat.TileHeight);
            var pixels = sheet.GetPixels(model, 0, -1);
            Assert.Equal((16, 18 * 32), (pixels.GetLength(0), pixels.GetLength(1)));
         }
      }

      [SkippableFact]
      public void Cube_PlayerSheets_ShowTheRunningPicturesToo() => AssertSheetHasAllEighteenPictures(LoadCube());

      [SkippableFact]
      public void Cube_PlayerSheets_ShowTheRunningPicturesToo_AfterASaveAndReload() => AssertSheetHasAllEighteenPictures(LoadCube(metadataRoundTrip: true));

      /// <summary>An older toml (from before the round-14 script tables, saved by an older CUBHMA) next to the new ROM: the sheets must not depend on anything newer.</summary>
      [SkippableFact]
      public void Cube_PlayerSheets_ShowTheRunningPicturesToo_WithAnOlderToml() {
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE_TOML to run this test");
         var blocks = File.ReadAllText(toml).Split(new[] { "[[NamedAnchors]]" }, StringSplitOptions.None);
         var older = string.Join("[[NamedAnchors]]", blocks.Where(block => !block.TrimStart().StartsWith("Name = \'\'\'cube.scripts")));
         var temp = Path.GetTempFileName();
         try {
            File.WriteAllText(temp, older);
            Assert.True(older.Length < File.ReadAllText(toml).Length);
            AssertSheetHasAllEighteenPictures(LoadCube(tomlOverride: temp));
         } finally { File.Delete(temp); }
      }

      [SkippableFact]
      public void Cube_RunningAnimations_AreRunSouthNorthWestEast_OfTheSecondHalfOfTheSheet() {
         var model = LoadCube();
         var sequences = CharacterSprites.ReadOverworldAnimations(model, IndexOf(model, "BRENDAN_NORMAL"), true);
         Assert.Equal(28, sequences.Count);
         Assert.Equal(17, OverworldAnimations.BiggestFrame(sequences));
         string Frames(int i) => string.Join(",", sequences[i].Commands.Where(c => c.Kind == OverworldAnimCommandKind.Frame).Select(c => $"{c.Frame}x{c.Duration}{(c.HFlip ? "f" : "")}"));
         Assert.Equal("run south", sequences[20].Name);
         Assert.Equal("12x5,9x3,13x5,9x3", Frames(20));
         Assert.Equal("14x5,10x3,15x5,10x3", Frames(21));
         Assert.Equal("16x5,11x3,17x5,11x3", Frames(22));
         Assert.Equal("16x5f,11x3f,17x5f,11x3f", Frames(23));
         Assert.Equal(OverworldAnimCommandKind.Jump, sequences[20].Commands.Last().Kind);
         Assert.Equal("3x8,0x8,4x8,0x8", Frames(4)); // walking south
         Assert.Contains("run south (5 ticks)", OverworldAnimations.DescribeFrameUse(sequences, 12));
         Assert.Equal(sequences.Select(s => s.Address).ToArray(), CharacterSprites.ReadOverworldAnimations(model, IndexOf(model, "MAY_NORMAL"), true).Select(s => s.Address).ToArray()); // both genders share the table
      }

      [SkippableFact]
      public void Cube_EditingARunningPicture_WritesBackAndRereads_ForBothGenders() {
         var model = LoadCube();
         foreach (var name in new[] { "BRENDAN_NORMAL", "MAY_NORMAL" }) {
            var index = IndexOf(model, name);
            var source = new OverworldSheetFrameSource(model, index, name);
            Assert.True(source.Prepare(), name);
            Assert.Equal(18, source.FrameCount);
            foreach (var frame in new[] { 9, 12, 17 }) {
               var picture = source.ReadFrame(frame, 2);
               Assert.Equal((16, 32), (picture.GetLength(0), picture.GetLength(1)));
               var edited = (int[,])picture.Clone();
               edited[3, 7] = (edited[3, 7] + 5) % 16;
               edited[15, 31] = (edited[15, 31] + 9) % 16;
               var token = new ModelDelta();
               source.WriteFrame(token, frame, 2, edited);
               var again = source.ReadFrame(frame, 2);
               Assert.Equal(edited[3, 7], again[3, 7]);
               Assert.Equal(edited[15, 31], again[15, 31]);
               // the whole sheet, read as the one tall picture the OW Sprites editor shows, has the same pixels in the same place
               var list = CharacterSprites.FindOverworldSpriteList(model, index);
               var sheet = (SpriteRun)model.GetNextRun(model.ReadPointer(list.Run.Start));
               var tall = sheet.GetPixels(model, 0, -1);
               Assert.Equal(edited[3, 7], tall[3, frame * 32 + 7]);
               Assert.Equal(edited[15, 31], tall[15, frame * 32 + 31]);
               // and the neighbours were not touched
               Assert.Equal(source.ReadFrame(frame - 1, 2)[0, 0], tall[0, (frame - 1) * 32]);
               token.Revert(model);
            }
         }
      }

      [SkippableFact]
      public void Cube_EditingARunningPictureInTheTallSheet_ChangesThatPictureOnly() {
         var model = LoadCube();
         var index = IndexOf(model, "BRENDAN_NORMAL");
         var list = CharacterSprites.FindOverworldSpriteList(model, index);
         var sheet = (SpriteRun)model.GetNextRun(model.ReadPointer(list.Run.Start));
         var tall = sheet.GetPixels(model, 0, -1);
         tall[5, 12 * 32 + 20] = (tall[5, 12 * 32 + 20] + 3) % 16;
         var token = new ModelDelta();
         var updated = sheet.SetPixels(model, token, 0, tall);
         Assert.Equal(sheet.Length, updated.Length);
         var source = new OverworldSheetFrameSource(model, index, "x");
         Assert.Equal(tall[5, 12 * 32 + 20], source.ReadFrame(12, 2)[5, 20]);
      }

      [SkippableFact]
      public void Cube_RunningAnimationTiming_CanBeEdited_AndStaysInPlace() {
         var model = LoadCube();
         var index = IndexOf(model, "BRENDAN_NORMAL");
         var sequences = CharacterSprites.ReadOverworldAnimations(model, index, true);
         var runSouth = sequences[20];
         var before = Enumerable.Range(0, 4 * runSouth.Commands.Count).Select(i => model[runSouth.Address + i]).ToArray();
         var token = new ModelDelta();
         // the first running frame faces south with picture 12 for 5 ticks: make it picture 13 for 7 ticks, flipped
         Assert.True(OverworldAnimations.TrySetCommand(model, token, runSouth, 0, new OverworldAnimCommand(OverworldAnimCommandKind.Frame, 13, 7, hFlip: true)));
         var again = CharacterSprites.ReadOverworldAnimations(model, index, true)[20];
         Assert.Equal((13, 7, true, false), (again.Commands[0].Frame, again.Commands[0].Duration, again.Commands[0].HFlip, again.Commands[0].VFlip));
         Assert.Equal(runSouth.Commands.Count, again.Commands.Count);
         // the rest of the sequence and the other animations did not change
         for (int i = 4; i < before.Length; i++) Assert.Equal(before[i], model[runSouth.Address + i]);
         Assert.Equal("14x5", string.Join(",", CharacterSprites.ReadOverworldAnimations(model, index, true)[21].Commands.Take(1).Select(c => $"{c.Frame}x{c.Duration}")));
         // wrong kind, too long a duration, a picture that does not exist: refused, nothing written
         var snapshot = again.Commands.Select(c => c.Encode()).ToArray();
         Assert.False(OverworldAnimations.TrySetCommand(model, token, again, 0, new OverworldAnimCommand(OverworldAnimCommandKind.End)));
         Assert.False(OverworldAnimations.TrySetCommand(model, token, again, 0, new OverworldAnimCommand(OverworldAnimCommandKind.Frame, 1, 64)));
         Assert.False(OverworldAnimations.TrySetCommand(model, token, again, 0, new OverworldAnimCommand(OverworldAnimCommandKind.Frame, -1, 1)));
         Assert.False(OverworldAnimations.TrySetCommand(model, token, again, 99, new OverworldAnimCommand(OverworldAnimCommandKind.Frame, 1, 1)));
         Assert.Equal(snapshot, CharacterSprites.ReadOverworldAnimations(model, index, true)[20].Commands.Select(c => c.Encode()).ToArray());
         // undo
         token.Revert(model);
         Assert.Equal(before, Enumerable.Range(0, before.Length).Select(i => model[runSouth.Address + i]).ToArray());
      }

      [SkippableFact]
      public void Cube_FrameNotes_NameTheRunningAnimationsThatUseEachPicture() {
         var model = LoadCube();
         var source = new OverworldSheetFrameSource(model, IndexOf(model, "MAY_NORMAL"), "May");
         Assert.Contains("run south", source.FrameNote(12));
         Assert.Contains("run west", source.FrameNote(16));
         Assert.Contains("run east", source.FrameNote(17));
         Assert.Contains("walk south", source.FrameNote(3));
      }

      [SkippableFact]
      public void Cube_TheOtherPlayerSheets_HaveAllTheirPictures_AsTheGameDefinesThem() {
         // pictures per sheet in pokeemerald-expansion's object_event_pic_tables.h
         var expected = new (string name, int frames, int width, int height)[] {
            ("BRENDAN_MACH_BIKE", 9, 32, 32), ("MAY_MACH_BIKE", 9, 32, 32), ("BRENDAN_ACRO_BIKE", 27, 32, 32), ("MAY_ACRO_BIKE", 27, 32, 32),
            ("BRENDAN_SURFING", 12, 32, 32), ("MAY_SURFING", 12, 32, 32), ("BRENDAN_FIELD_MOVE", 5, 32, 32), ("MAY_FIELD_MOVE", 5, 32, 32),
            ("BRENDAN_FISHING", 12, 32, 32), ("MAY_FISHING", 12, 32, 32), ("BRENDAN_WATERING", 9, 32, 32), ("MAY_WATERING", 9, 32, 32),
            ("BRENDAN_UNDERWATER", 9, 32, 32), ("MAY_UNDERWATER", 9, 32, 32),
         };
         var model = LoadCube();
         foreach (var (name, frames, width, height) in expected) {
            var index = IndexOf(model, name);
            Assert.True(index >= 0, name);
            var list = CharacterSprites.FindOverworldSpriteList(model, index);
            Assert.False(CharacterSprites.IsRelativeSheet(model, list), name);
            Assert.Equal(frames, list.Count);
            for (int frame = 0; frame < frames; frame++) {
               var pixels = CharacterSprites.ReadOverworldFrame(model, list, frame);
               Assert.True(pixels != null, $"{name} frame {frame}");
               Assert.Equal((width, height), (pixels.GetLength(0), pixels.GetLength(1)));
            }
            // every picture the animations show exists
            var sequences = CharacterSprites.ReadOverworldAnimations(model, index, false);
            Assert.True(OverworldAnimations.BiggestFrame(sequences) < frames, $"{name}: animations show picture {OverworldAnimations.BiggestFrame(sequences)} of {frames}");
         }
      }

      #endregion
   }
}
