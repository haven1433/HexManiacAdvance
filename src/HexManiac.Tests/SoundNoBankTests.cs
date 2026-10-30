using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Sound;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The Sound tab on a CUBE ROM that has no All-Instruments MIDI bank (the real round-14b ROM with the anchors of the bank removed from its toml, since the shrunk ROM is not built yet),
   /// and an old toml used with a ROM whose layout moved (not a supported pairing, but it must load and the Sound tab must open).
   /// Real-ROM tests need HMA_INTRO_CUBE / HMA_INTRO_CUBE_TOML (and HMA_INTRO_PREV_R14_TOML for the old toml).
   /// </summary>
   public class SoundNoBankTests {
      private static (byte[] rom, string[] toml)? Real(string tomlVariable = "HMA_INTRO_CUBE_TOML") {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable(tomlVariable);
         if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml)) return null;
         return (File.ReadAllBytes(path), File.ReadAllLines(toml));
      }

      /// <summary>The toml without the [[NamedAnchors]] blocks whose name contains 'all_instruments' (the bank's voicegroups and key splits).</summary>
      internal static string[] WithoutBankAnchors(string[] toml, out int removed, out int firstAddress) {
         var result = new List<string>();
         removed = 0;
         firstAddress = int.MaxValue;
         for (int i = 0; i < toml.Length; i++) {
            if (toml[i].Trim() == "[[NamedAnchors]]" && i + 2 < toml.Length && toml[i + 1].Contains("all_instruments")) {
               var addressLine = toml[i + 2];
               if (addressLine.Contains("0x")) firstAddress = Math.Min(firstAddress, System.Convert.ToInt32(addressLine.Substring(addressLine.IndexOf("0x") + 2).Trim(), 16));
               removed++;
               i += 3; // the block is [[NamedAnchors]], Name, Address, Format
               while (i < toml.Length && toml[i].Trim().Length == 0) i++;
               i--;
               continue;
            }
            result.Add(toml[i]);
         }
         return result.ToArray();
      }

      private static (ViewPort viewPort, StubFileSystem fileSystem, SoundTab tab, List<string> errors, List<string> messages) Open(byte[] rom, string[] toml) {
         var model = CubeScriptsTests.Load(rom, toml);
         var fileSystem = new StubFileSystem();
         var viewPort = new ViewPort("cube.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         var messages = new List<string>();
         var tab = new SoundTab(fileSystem, viewPort);
         tab.OnError += (sender, e) => errors.Add(e);
         tab.OnMessage += (sender, e) => messages.Add(e);
         return (viewPort, fileSystem, tab, errors, messages);
      }

      private static byte[] SingleNoteMidi() {
         const string hex = "4D546864000000060000000101E04D54726B0000001D00FF510307A12000903C648360803C000090405A836080400000FF2F00";
         return Enumerable.Range(0, hex.Length / 2).Select(i => System.Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
      }

      [SkippableFact]
      public void RealCube_WithoutTheBankAnchors_TheSoundTabPicksAnExistingVoicegroup_AndImportsAMidi() {
         var cube = Real();
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var toml = WithoutBankAnchors(cube.Value.toml, out var removed, out _);
         // the shrunk (round 14c) CUBE.toml no longer has the bank anchors at all (0 removed); an older toml still has them (112)
         Assert.True(removed == 0 || removed > 100, $"only {removed} bank anchors were removed");
         var (viewPort, fileSystem, tab, errors, messages) = Open((byte[])cube.Value.rom.Clone(), toml);

         Assert.True(tab.HasSongs);
         Assert.False(tab.HasAllInstruments);
         Assert.Contains(AllInstrumentsPatch.NoBankNote, tab.AllInstrumentsNote);
         Assert.NotNull(tab.SelectedVoicegroup);
         Assert.False(tab.SelectedVoicegroup.Info.IsCustom);
         Assert.DoesNotContain("all_instruments", tab.Voicegroups.Select(group => group.Title));
         // the default is the voicegroup that the most songs use
         var most = tab.Voicegroups.Where(group => !group.Info.IsCustom).Max(group => group.Info.UsedBy.Count);
         Assert.True(tab.SelectedVoicegroup.Info.UsedBy.Count >= Math.Min(most, 1));
         Console.WriteLine($"default voicegroup without the bank: {tab.SelectedVoicegroup.Title} (used by {tab.SelectedVoicegroup.Info.UsedBy.Count} songs, the most is {most}); {tab.Voicegroups.Count} voicegroups");

         var songs = tab.Songs.Count;
         fileSystem.OpenFile = (description, options) => new LoadedFile("test song.mid", SingleNoteMidi());
         tab.InsertAsNewSong = true;
         tab.InsertSong.Execute(null);

         Assert.Empty(errors.Where(error => error.Contains("Could not")));
         Assert.Equal(songs + 1, tab.Songs.Count);
         var header = tab.Songs[songs].Header;
         Assert.NotNull(header);
         Assert.Equal(tab.SelectedVoicegroup.Address, header.Voicegroup);
         // the voicegroup pointer of the new song points at an existing voicegroup of the ROM, never into nothing
         Assert.Contains(tab.Voicegroups, group => group.Address == header.Voicegroup);
         Assert.Contains(AllInstrumentsPatch.NoBankNote, tab.Status);
         Assert.Contains(AllInstrumentsPatch.NoBankNote, messages.Last());
      }

      [SkippableFact]
      public void RealCube_WithItsOwnToml_NoVoicegroupIsDroppedAndNothingIsLogged() {
         var cube = Real();
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var (viewPort, _, tab, _, _) = Open((byte[])cube.Value.rom.Clone(), cube.Value.toml);
         Assert.DoesNotContain(((PokemonModel)viewPort.Model).LoadingMessages, line => line.Contains("voicegroup anchor(s)"));
         var named = cube.Value.toml.Count(line => line.StartsWith("Name = '''sound.voicegroups."));
         Assert.Equal(named, tab.Voicegroups.Count(g => g.Info.AnchorName != null && !g.Info.IsCustom));
      }

      [SkippableFact]
      public void RealCube_WithTheBank_StillPrefersTheBank() {
         var cube = Real();
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var (_, _, tab, _, _) = Open((byte[])cube.Value.rom.Clone(), cube.Value.toml);
         var hasBank = cube.Value.toml.Any(line => line.Contains("sound.voicegroups.all_instruments'''"));
         Assert.Equal(hasBank, tab.HasAllInstruments);
         if (hasBank) Assert.Equal("all_instruments", tab.SelectedVoicegroup.Info.Name);
      }

      /// <summary>A ROM where the bank is gone and everything behind it moved up (what the shrunk CUBE looks like to an old toml).</summary>
      private static byte[] ShrinkTheBank(byte[] rom, int firstBankAddress, int cut) {
         var result = new byte[rom.Length];
         Array.Fill(result, (byte)0xFF);
         Array.Copy(rom, 0, result, 0, firstBankAddress);
         Array.Copy(rom, firstBankAddress + cut, result, firstBankAddress, rom.Length - firstBankAddress - cut);
         return result;
      }

      [SkippableTheory]
      [InlineData("HMA_INTRO_CUBE_TOML")]
      [InlineData("HMA_INTRO_PREV_R14_TOML")]
      public void RealCube_OldTomlWithAShrunkRom_LoadsAndTheSoundTabOpens(string tomlVariable) {
         var cube = Real(tomlVariable);
         Skip.If(cube == null, $"set HMA_INTRO_CUBE and {tomlVariable} to run this test");
         WithoutBankAnchors(cube.Value.toml, out _, out var first);
         Skip.If(first == int.MaxValue, "the toml has no bank anchors");
         var shrunk = ShrinkTheBank(cube.Value.rom, first, 1151872);
         var (viewPort, fileSystem, tab, errors, messages) = Open(shrunk, cube.Value.toml);

         // nothing threw; the tab opens; and no bank is claimed from data that is not the bank
         Assert.NotNull(tab.Songs);
         foreach (var group in tab.Voicegroups.Where(g => g.Info.Name != null && g.Info.Name.StartsWith("all_instruments")).Take(3))
            Console.WriteLine($"stale bank anchor {group.Info.Name}: {group.Info.EntryCount} entries at {group.Address:X6}, custom={group.Info.IsCustom}");
         Console.WriteLine($"old toml + shrunk rom: HasSongs={tab.HasSongs}, songs={tab.Songs.Count}, voicegroups={tab.Voicegroups.Count}, HasAllInstruments={tab.HasAllInstruments}, selected={tab.SelectedVoicegroup?.Title}");
         // the stale anchors are not voicegroups any more (the data behind them is not instruments), and the log says so
         Assert.DoesNotContain(tab.Voicegroups.Where(g => g.Info.Name != null && g.Info.Name.StartsWith("all_instruments")), g => !g.Info.IsCustom);
         Assert.False(tab.SelectedVoicegroup.Info.IsCustom);
         Assert.Contains(((PokemonModel)viewPort.Model).LoadingMessages, line => line.Contains("voicegroup anchor(s) of the TOML hold data that is not instruments"));
         Console.WriteLine(((PokemonModel)viewPort.Model).LoadingMessages.First(line => line.Contains("voicegroup anchor(s)")));
         Console.WriteLine("note: " + tab.AllInstrumentsNote + " | " + tab.AllInstrumentsDetails);
         Assert.Empty(errors);

         // and importing a song still works and points at an existing voicegroup
         if (tab.HasSongs && tab.SelectedVoicegroup != null) {
            var songs = tab.Songs.Count;
            fileSystem.OpenFile = (description, options) => new LoadedFile("x.mid", SingleNoteMidi());
            tab.InsertAsNewSong = true;
            tab.InsertSong.Execute(null);
            Assert.DoesNotContain(errors, error => error.Contains("Could not convert"));
         }
      }
   }
}
