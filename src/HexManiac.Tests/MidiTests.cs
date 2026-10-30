using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Sound;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// MidiToAgb is a port of the pokeemerald tool mid2agb. Its text is checked against what the real tool made (compiled from the same source)
   /// for the same MIDI bytes and options. The expected lines below were produced by running that tool.
   /// </summary>
   public class MidiToAgbTests {
      private static byte[] FromHex(string hex) => Enumerable.Range(0, hex.Length / 2).Select(i => System.Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();

      /// <summary>The expected text and the converted text, compared line by line without the spaces at the end of lines.</summary>
      private static void AssertSameText(IEnumerable<string> expected, string actual) {
         var expectedLines = expected.Select(line => line.TrimEnd()).ToList();
         var actualLines = actual.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd()).ToList();
         if (actualLines.Count > 0 && actualLines[actualLines.Count - 1] == string.Empty) actualLines.RemoveAt(actualLines.Count - 1);
         for (int i = 0; i < Math.Min(expectedLines.Count, actualLines.Count); i++) Assert.Equal(expectedLines[i], actualLines[i]);
         Assert.Equal(expectedLines.Count, actualLines.Count);
      }

      private static readonly Dictionary<string, (string midi, string options, string[] expected)> Vectors = new Dictionary<string, (string, string, string[])> {
         // one channel, a tempo, two notes
         ["single_note"] = (
            "4D546864000000060000000101E04D54726B0000001D00FF510307A12000903C648360803C000090405A836080400000FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\tTEMPO , 120*test_tbs/2",
               "\t.byte\t\tN24   , Cn3 , v100",
               "\t.byte\tW24",
               "\t.byte\t\t        En3 , v092",
               "\t.byte\tW24",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "",
               "\t.end",
            }),
         // two channels with a program change, volume and pan
         ["two_channels"] = (
            "4D546864000000060000000101E04D54726B0000003300FF51030927C000C00500B0076400B00A2000903064874080300000C11400913C50009140508360813C000081400000FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\tTEMPO , 100*test_tbs/2",
               "\t.byte\t\tVOICE , 5",
               "\t.byte\t\tVOL   , 100*test_mvl/mxv",
               "\t.byte\t\tPAN   , c_v-32",
               "\t.byte\t\tN48   , Cn2 , v100",
               "\t.byte\tW72",
               "\t.byte\tFINE",
               "",
               "@**************** Track 2 (Midi-Chn.2) ****************@",
               "",
               "test_2:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\tW48",
               "\t.byte\t\tVOICE , 20",
               "\t.byte\t\tN24   , Cn3 , v080",
               "\t.byte\t\tN24   , En3 ",
               "\t.byte\tW24",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t2\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "\t.word\ttest_2",
               "",
               "\t.end",
            }),
         // loop markers and a note longer than a whole note (a tie)
         ["loop_and_tie"] = (
            "4D546864000000060000000101E04D54726B0000002000FF01015B0090396E926080390000903C3C8170803C0000FF01015D00FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "test_1_B1:",
               "@ 000   ----------------------------------------",
               "\t.byte\t\tTIE   , An2 , v112",
               "\t.byte\tW96",
               "@ 001   ----------------------------------------",
               "\t.byte\tW24",
               "\t.byte\t\tEOT   ",
               "\t.byte\t\tN12   , Cn3 , v060",
               "\t.byte\tW12",
               "\t.byte\tGOTO",
               "\t .word\ttest_1_B1",
               "test_1_B2:",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "",
               "\t.end",
            }),
         // identical bars become a pattern (PATT / PEND)
         ["repeated_bars"] = (
            "4D546864000000060000000101E04D54726B0000010400903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "test_1_000:",
               "\t.byte\t\tN06   , Cn3 , v096",
               "\t.byte\tW06",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        En3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Fs3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Cn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        En3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Fs3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Cn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        En3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Fs3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Cn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        En3 ",
               "\t.byte\tW06",
               "\t.byte\t\t        Fs3 ",
               "\t.byte\tW06",
               "\t.byte\tPEND",
               "@ 001   ----------------------------------------",
               "\t.byte\tPATT",
               "\t .word\ttest_1_000",
               "@ 002   ----------------------------------------",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "",
               "\t.end",
            }),
         // notes between the engine lengths, exact gate time
         ["gate_time_exact"] = (
            "4D546864000000060000000100184D54726B0000001C00903C6419803C0000903E641B803E00009040641E80400000FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\t\tN24   , Cn3 , v100, gtp1",
               "\t.byte\tW24",
               "\t.byte\tW01",
               "\t.byte\t\t        Dn3 , v100, gtp3",
               "\t.byte\tW24",
               "\t.byte\tW03",
               "\t.byte\t\tN30   , En3 ",
               "\t.byte\tW30",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "",
               "\t.end",
            }),
         // the same notes without the exact gate time
         ["gate_time_rounded"] = (
            "4D546864000000060000000100184D54726B0000001C00903C6419803C0000903E641B803E00009040641E80400000FF2F00",
            "-R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 0",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\t\tN24   , Cn3 , v100",
               "\t.byte\tW24",
               "\t.byte\tW01",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW24",
               "\t.byte\tW03",
               "\t.byte\t\tN30   , En3 ",
               "\t.byte\tW30",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "",
               "\t.end",
            }),
         // running status, and note-on with velocity 0 as the note-off
         ["running_status"] = (
            "4D546864000000060000000101E04D54726B0000001E00903C6483603C00003E6483603E0000B0075A0040648170400000FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\t\tN24   , Cn3 , v100",
               "\t.byte\tW24",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW24",
               "\t.byte\t\tVOL   , 90*test_mvl/mxv",
               "\t.byte\tW12",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "",
               "\t.end",
            }),
         // format 1: a conductor track with tempo and a 3/4 time signature
         ["format1_conductor"] = (
            "4D546864000000060001000201E04D54726B0000001300FF510307A12000FF58040302180800FF2F004D54726B0000001F00903C648360803C0000914364874081430000903E648360803E0000FF2F00",
            "-E -R50 -G_all_instruments -V080 -Ltest",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\ttest_grp, voicegroup_all_instruments",
               "\t.equ\ttest_pri, 0",
               "\t.equ\ttest_rev, reverb_set+50",
               "\t.equ\ttest_mvl, 80",
               "\t.equ\ttest_key, 0",
               "\t.equ\ttest_tbs, 1",
               "\t.equ\ttest_exg, 1",
               "\t.equ\ttest_cmp, 1",
               "",
               "\t.section .rodata",
               "\t.global\ttest",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "test_1:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "@ 001   ----------------------------------------",
               "\t.byte\tTEMPO , 120*test_tbs/2",
               "\t.byte\t\tN24   , Cn3 , v100",
               "\t.byte\tW72",
               "@ 002   ----------------------------------------",
               "\t.byte\t\t        Dn3 ",
               "\t.byte\tW24",
               "\t.byte\tFINE",
               "",
               "@**************** Track 2 (Midi-Chn.2) ****************@",
               "",
               "test_2:",
               "\t.byte\t\tVOL   , 127*test_mvl/mxv",
               "\t.byte\tKEYSH , test_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\tW24",
               "\t.byte\t\tN48   , Gn3 , v100",
               "\t.byte\tW48",
               "@ 001   ----------------------------------------",
               "\t.byte\tW24",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "test:",
               "\t.byte\t2\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\ttest_pri\t@ Priority",
               "\t.byte\ttest_rev\t@ Reverb.",
               "",
               "\t.word\ttest_grp",
               "",
               "\t.word\ttest_1",
               "\t.word\ttest_2",
               "",
               "\t.end",
            }),
         // -N (no compression), -X (48 clocks per beat), master volume 127, priority 4, no reverb
         ["no_compression_double_clocks"] = (
            "4D546864000000060000000101E04D54726B0000004400903C6078803C0000903E6078803E000090406078804000009042607880420000903C6078803C0000903E6078803E000090406078804000009042607880420000FF2F00",
            "-N -X -V127 -P4 -Lsong",
            new[] {
               "\t.include \"MPlayDef.s\"",
               "",
               "\t.equ\tsong_grp, voicegroup_dummy",
               "\t.equ\tsong_pri, 4",
               "\t.equ\tsong_rev, 0",
               "\t.equ\tsong_mvl, 127",
               "\t.equ\tsong_key, 0",
               "\t.equ\tsong_tbs, 2",
               "\t.equ\tsong_exg, 0",
               "\t.equ\tsong_cmp, 0",
               "",
               "\t.section .rodata",
               "\t.global\tsong",
               "\t.align\t2",
               "",
               "@**************** Track 1 (Midi-Chn.1) ****************@",
               "",
               "song_1:",
               "\t.byte\t\tVOL   , 127*song_mvl/mxv",
               "\t.byte\tKEYSH , song_key+0",
               "@ 000   ----------------------------------------",
               "\t.byte\t\tN12   , Cn3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , Dn3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , En3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , Fs3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , Cn3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , Dn3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , En3 , v096",
               "\t.byte\tW12",
               "\t.byte\t\tN12   , Fs3 , v096",
               "\t.byte\tW12",
               "\t.byte\tFINE",
               "",
               "@******************************************************@",
               "\t.align\t2",
               "",
               "song:",
               "\t.byte\t1\t@ NumTrks",
               "\t.byte\t0\t@ NumBlks",
               "\t.byte\tsong_pri\t@ Priority",
               "\t.byte\tsong_rev\t@ Reverb.",
               "",
               "\t.word\tsong_grp",
               "",
               "\t.word\tsong_1",
               "",
               "\t.end",
            }),
      };

      [Theory]
      [InlineData("single_note")]
      [InlineData("two_channels")]
      [InlineData("loop_and_tie")]
      [InlineData("repeated_bars")]
      [InlineData("gate_time_exact")]
      [InlineData("gate_time_rounded")]
      [InlineData("running_status")]
      [InlineData("format1_conductor")]
      [InlineData("no_compression_double_clocks")]
      public void Convert_MakesTheTextOfMid2Agb(string name) {
         var vector = Vectors[name];
         var result = MidiToAgb.Convert(FromHex(vector.midi), MidiToAgbOptions.Parse(vector.options));
         AssertSameText(vector.expected, result.Text);
      }

      [Fact]
      public void Convert_ReportsWhatItFoundInTheFile() {
         var two = MidiToAgb.Convert(FromHex(Vectors["two_channels"].midi), new MidiToAgbOptions { Label = "x" });
         Assert.Equal(2, two.TrackCount);
         Assert.Equal(0, two.MidiFormat);
         Assert.Equal(1, two.MidiTrackCount);
         Assert.Equal(new[] { (1, 1), (1, 2) }, two.Channels.ToArray());

         var conductor = MidiToAgb.Convert(FromHex(Vectors["format1_conductor"].midi), new MidiToAgbOptions { Label = "x" });
         Assert.Equal(1, conductor.MidiFormat);
         Assert.Equal(2, conductor.MidiTrackCount);
         Assert.Equal(new[] { (2, 1), (2, 2) }, conductor.Channels.ToArray()); // the notes are in the second MIDI track; the first only has the tempo
      }

      [Fact]
      public void Convert_UsesTheLabelEverywhere() {
         var text = MidiToAgb.Convert(FromHex(Vectors["single_note"].midi), new MidiToAgbOptions { Label = "my_song", VoiceGroup = "000" }).Text;
         Assert.Contains("\t.equ\tmy_song_grp, voicegroup000", text);
         Assert.Contains("\t.global\tmy_song", text);
         Assert.Contains("my_song_1:", text);
         Assert.Contains("\t.word\tmy_song_1", text);
         Assert.DoesNotContain("test_", text);
      }

      [Fact]
      public void Convert_AGBTracksFollowTheChannelsThatPlayNotes() {
         // 17 MIDI tracks with a note each make 17 tracks: the converter keeps them all, the Sound tab refuses songs with more than 16
         var result = MidiToAgb.Convert(Build(17), new MidiToAgbOptions { Label = "many" });
         Assert.Equal(17, result.TrackCount);
         Assert.Contains("\t.byte\t17\t@ NumTrks", result.Text);
      }

      /// <summary>A format 1 file with this many tracks, each one playing a note on its own channel.</summary>
      public static byte[] Build(int tracks) {
         var file = new List<byte>();
         file.AddRange(new byte[] { 0x4D, 0x54, 0x68, 0x64, 0, 0, 0, 6, 0, 1, 0, (byte)tracks, 0x01, 0xE0 });
         for (int i = 0; i < tracks; i++) {
            var body = new byte[] { 0x00, (byte)(0x90 | i % 16), (byte)(48 + i % 12), 100, 0x83, 0x60, (byte)(0x80 | i % 16), (byte)(48 + i % 12), 0, 0x00, 0xFF, 0x2F, 0x00 };
            file.AddRange(new byte[] { 0x4D, 0x54, 0x72, 0x6B, 0, 0, 0, (byte)body.Length });
            file.AddRange(body);
         }
         return file.ToArray();
      }

      /// <summary>A format 0 file: one channel picks a program (instrument) and plays Cn3 for two beats at 120 bpm.</summary>
      public static byte[] WithProgram(int program) {
         var body = new byte[] { 0x00, 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20, 0x00, 0xC0, (byte)program, 0x00, 0x90, 60, 100, 0x87, 0x40, 0x80, 60, 0, 0x00, 0xFF, 0x2F, 0x00 };
         var file = new List<byte>(new byte[] { 0x4D, 0x54, 0x68, 0x64, 0, 0, 0, 6, 0, 0, 0, 1, 0x01, 0xE0, 0x4D, 0x54, 0x72, 0x6B, 0, 0, 0, (byte)body.Length });
         file.AddRange(body);
         return file.ToArray();
      }

      [Fact]
      public void Options_AreReadLikeTheCommandLine() {
         var options = MidiToAgbOptions.Parse("-E -R50 -G_all_instruments -V080 -P5 -Lsong_name");
         Assert.True(options.ExactGateTime);
         Assert.Equal(50, options.Reverb);
         Assert.Equal("_all_instruments", options.VoiceGroup);
         Assert.Equal(80, options.MasterVolume);
         Assert.Equal(5, options.Priority);
         Assert.Equal("song_name", options.Label);
         Assert.True(options.Compression);
         Assert.False(options.DoubleClocks);

         var separate = MidiToAgbOptions.Parse("-V 90 -R 7 -N -X");
         Assert.Equal(90, separate.MasterVolume);
         Assert.Equal(7, separate.Reverb);
         Assert.False(separate.Compression);
         Assert.True(separate.DoubleClocks);
         Assert.Equal(-1, MidiToAgbOptions.Parse("-E").Reverb); // reverb is off unless asked for
         Assert.Equal(127, MidiToAgbOptions.Parse("").MasterVolume);
         Assert.Throws<ArgumentException>(() => MidiToAgbOptions.Parse("-Z"));
         Assert.Throws<ArgumentException>(() => MidiToAgbOptions.Parse("-V"));
      }

      [Theory]
      [InlineData("", "chunk header")] // empty file
      [InlineData("524946460000000000000000000000000000000000000000", "MThd")] // not midi
      [InlineData("4D546864000000060002000101E04D54726B0000000D00903C648360803C0000FF2F00", "format 2")] // format 2
      [InlineData("4D5468640000000600000001E7284D54726B0000000D00903C648360803C0000FF2F00", "SMPTE")] // smpte
      [InlineData("4D546864000000060000000100004D54726B0000000D00903C648360803C0000FF2F00", "time division")] // division 0
      [InlineData("4D546864000000060000000101E04D54726B0000000D00903C648360B0075A00FF2F00", "never ends")] // note never ends
      [InlineData("4D546864000000060000000101E04D54726B0000000900903C648360803C00", "ends too early")] // no end of track
      [InlineData("4D546864000000060000000101E04D54726B0000001100FF010000903C648360803C0000FF2F00", "text event")] // empty text event
      [InlineData("4D546864000000060000000101E0", "chunk header")] // missing track
      [InlineData("4D546864000000060000000101E058585858000000000000000000000000000000000000000000000000000000000000", "MTrk")] // bad track signature
      [InlineData("4D546864000000060000000101E04D54726B0000001300FF510207A100903C648360803C0000FF2F00", "tempo")] // bad tempo size
      [InlineData("4D546864000000060000000101E04D54726B0000001500FF58040410180800903C648360803C0000FF2F00", "time signature")] // bad time signature
      public void Convert_ExplainsWhatIsWrongWithAFile(string hex, string messagePart) {
         var exception = Assert.Throws<MidiConversionException>(() => MidiToAgb.Convert(FromHex(hex), new MidiToAgbOptions { Label = "bad" }));
         Assert.Contains(messagePart, exception.Message);
      }

      [Fact]
      public void Convert_NeverFailsInAnyOtherWay_ForCutOffAndDamagedFiles() {
         foreach (var name in new[] { "two_channels", "loop_and_tie", "format1_conductor", "repeated_bars" }) {
            var original = FromHex(Vectors[name].midi);
            for (int length = 0; length < original.Length; length++) {
               try { MidiToAgb.Convert(original.Take(length).ToArray(), new MidiToAgbOptions { Label = "cut" }); } catch (MidiConversionException) { }
            }
            foreach (var value in new byte[] { 0x00, 0x7F, 0x80, 0xFF }) {
               for (int position = 0; position < original.Length; position++) {
                  var damaged = (byte[])original.Clone();
                  damaged[position] = value;
                  try { MidiToAgb.Convert(damaged, new MidiToAgbOptions { Label = "damaged" }); } catch (MidiConversionException) { }
               }
            }
         }
      }

      [Fact]
      public void Convert_RefusesMissingData() {
         Assert.Throws<MidiConversionException>(() => MidiToAgb.Convert(null));
         Assert.Throws<MidiConversionException>(() => MidiToAgb.Convert(new byte[0]));
         Assert.Throws<MidiConversionException>(() => MidiToAgb.Convert(FromHex(Vectors["single_note"].midi), new MidiToAgbOptions { Label = string.Empty }));
      }

      [Fact]
      public void Labels_AreMadeSafeForTheAssembler() {
         Assert.Equal("BW_Accumula_Town", MidiToAgb.LabelFromFileName("BW Accumula Town.mid"));
         Assert.Equal("_1_Route", MidiToAgb.LabelFromFileName("1 Route.mid"));
         Assert.Equal("Route_29_Remix", MidiToAgb.LabelFromFileName("C:\\music\\Route 29 (Remix).midi"));
         Assert.Equal("song", MidiToAgb.LabelFromFileName(".mid"));
         Assert.Equal("BW_Accumula_Town", SoundTab.SongNameFromFileName("/home/me/BW Accumula Town.mid"));
         Assert.Equal("Route_29_VGMusic_Remix", SoundTab.SongNameFromFileName("Route 29 -- VGMusic  Remix.MID"));
         Assert.Equal("a_b", SoundTab.SongNameFromFileName("a.b.s"));
      }
   }


   /// <summary>
   /// Inserting MIDI files in the Sound tab: they are converted like mid2agb does and then go in the ROM the same way .s files do
   /// (as a new song or over the selected one, with the selected voicegroup, in one undo step), one at a time or many at once.
   /// </summary>
   public class MidiInsertTests : BaseViewModelTestClass {
      // a small ROM: free space everywhere (0xFF), with a song table, a sample, two voicegroups and two songs put in by hand
      private const int TableAddress = 0x100;
      private const int SampleAddress = 0x200;
      private const int AllInstruments = 0x400; // 4 instruments, named 'sound.voicegroups.all_instruments' (when the test asks for it)
      private const int Other = 0x500; // 2 instruments, named 'sound.voicegroups.other'. Used by the two songs.
      private const int ToneSize = 12;
      // the tests below search and replace whole lines with \n, so the text must not depend on the line endings of the checkout (a Windows checkout turns this file into \r\n)
      private static readonly string SongSource = @"
	.include ""MPlayDef.s""
	.equ	t_grp, voicegroup000
t_1:
	.byte	VOICE , 0
	.byte	VOL , 127
	.byte	N24 , Cn3 , v127
	.byte	W24
	.byte	FINE
	.align	2
t:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	t_grp
	.word	t_1
	.end
".Replace("\r\n", "\n");

      private readonly List<string> tabErrors = new List<string>();
      private readonly List<string> tabMessages = new List<string>();
      private readonly int[] headers = new int[2];

      public MidiInsertTests() : base(0x10000) { SetFullModel(0xFF); }

      #region Setup

      /// <summary>A format 0 file with one channel: a tempo, then Cn3 and En3 as quarter notes (the 'single_note' vector of MidiToAgbTests).</summary>
      private static byte[] Midi(string name) {
         if (name == "single") return FromHex("4D546864000000060000000101E04D54726B0000001D00FF510307A12000903C648360803C000090405A836080400000FF2F00");
         throw new ArgumentException(name);
      }

      private static byte[] FromHex(string hex) => Enumerable.Range(0, hex.Length / 2).Select(i => System.Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();

      private void WriteSquare(int address, int type, int duty) {
         Model[address] = (byte)type;
         Model[address + 1] = 60;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WriteMultiByteValue(address + 4, 4, Token, duty);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0x000F0000);
      }

      private void WriteSample(int address) {
         Model[address] = 0;
         Model[address + 1] = 60;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WritePointer(Token, address + 4, SampleAddress);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0x00FF00FF);
      }

      private int PlaceSong(int address, int voicegroup) {
         var song = SongAssembler.Assemble(SongSource, 0x08000000 + address, new Dictionary<string, int> { ["voicegroup000"] = 0x08000000 + voicegroup });
         Assert.True(song.Success, song.Error);
         for (int i = 0; i < song.Bytes.Length; i++) Model[address + i] = song.Bytes[i];
         var header = address + song.HeaderOffset;
         Model.ObserveRunWritten(Token, new PointerRun(header + 4));
         return header;
      }

      /// <summary>Puts the songs, voicegroups and the song table in the ROM. The All Instruments voicegroup is only named when asked for.</summary>
      private void Build(bool named = true, bool withNames = true, bool nameOther = true) {
         var sine = new sbyte[400];
         for (int i = 0; i < sine.Length; i++) sine[i] = (sbyte)(Math.Sin(i * 2 * Math.PI * 220 / 13379) * 100);
         var sample = GbaSample.Build(sine, 13379, compressed: false, loopStart: 0);
         for (int i = 0; i < sample.Length; i++) Model[SampleAddress + i] = sample[i];

         WriteSquare(AllInstruments + 0 * ToneSize, 1, 2);
         WriteSample(AllInstruments + 1 * ToneSize);
         WriteSquare(AllInstruments + 2 * ToneSize, 2, 1);
         WriteSquare(AllInstruments + 3 * ToneSize, 4, 0);
         WriteSample(Other + 0 * ToneSize);
         WriteSquare(Other + 1 * ToneSize, 1, 0);
         if (nameOther) Model.ObserveAnchorWritten(Token, "sound.voicegroups.other", new NoInfoRun(Other));
         if (named) Model.ObserveAnchorWritten(Token, SoundTab.AllInstrumentsAnchor, new NoInfoRun(AllInstruments));

         headers[0] = PlaceSong(0x600, Other);
         headers[1] = PlaceSong(0x700, Other);
         for (int i = 0; i < headers.Length; i++) {
            Model.WritePointer(Token, TableAddress + i * 8, headers[i]);
            Model.WriteMultiByteValue(TableAddress + i * 8 + 4, 2, Token, 0);
            Model.WriteMultiByteValue(TableAddress + i * 8 + 6, 2, Token, 0);
         }
         var error = ArrayRun.TryParse(Model, "[pointer<> musicplayer: unknown:]" + headers.Length, TableAddress, SortedSpan<int>.None, out var table);
         Assert.False(error.HasError, error.ErrorMessage);
         Model.ObserveAnchorWritten(Token, SoundTab.SongTable, table);
         if (withNames) Model.SetList(Token, SoundTab.SongNamesList, "MUS_ONE", "MUS_TWO");
      }

      private SoundTab CreateTab() {
         var tab = new SoundTab(FileSystem, ViewPort);
         tab.OnError += (sender, e) => tabErrors.Add(e);
         tab.OnMessage += (sender, e) => tabMessages.Add(e);
         return tab;
      }

      private int SongCount => Model.GetTable(SoundTab.SongTable).ElementCount;

      private static LoadedFile Loaded(string name, byte[] data) => new LoadedFile(name, data);

      #endregion

      [Fact]
      public void InsertDialog_ListsSongsAndMidiFilesTogether_SFirst() {
         Assert.Equal("s", SoundTab.SongFileExtensions[0]);
         Assert.Contains("mid", SoundTab.SongFileExtensions);
         Assert.Contains("midi", SoundTab.SongFileExtensions);
         var filter = FileDialogFilter.Create(SoundTab.SongInsertDescription, SoundTab.SongFileExtensions);
         Assert.StartsWith("Songs (*.s, *.mid, ...)|*.s;*.mid;*.midi;", filter);
         Assert.EndsWith("|All Files|*.*", filter);
         Assert.True(SoundTab.IsMidiFile("a.MID"));
         Assert.True(SoundTab.IsMidiFile("a.midi"));
         Assert.False(SoundTab.IsMidiFile("a.s"));
      }

      [Fact]
      public void Tab_ChoosesTheAllInstrumentsVoicegroupFirst() {
         Build();
         var tab = CreateTab();
         Assert.Equal(AllInstruments, tab.SelectedVoicegroup.Address);
         Assert.Equal(SoundTab.AllInstrumentsAnchor, tab.SelectedVoicegroup.Info.AnchorName);
      }

      [Fact]
      public void Tab_WithoutAllInstruments_StartsWithTheFirstVoicegroup() {
         Build(named: false);
         var tab = CreateTab();
         Assert.Equal(tab.Voicegroups.First().Address, tab.SelectedVoicegroup.Address);
         Assert.NotEqual(AllInstruments, tab.SelectedVoicegroup.Address);
      }

      [Fact]
      public void Tab_InsertMidi_ReplacesTheSelectedSong() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("My Song.mid", Midi("single"));
         tab.SelectedSong = tab.Songs[0];

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(2, SongCount);
         tab.SelectedSong = tab.Songs[0];
         var header = tab.SelectedSong.Header;
         Assert.Equal(1, header.TrackCount);
         Assert.Equal(AllInstruments, header.Voicegroup);
         Assert.Equal(0x80 + 50, header.Reverb);
         Assert.Equal(0, header.Priority);
         Assert.Equal("MUS_ONE", tab.SelectedSong.Name); // a replaced song keeps its name
         Assert.Equal(headers[1], tab.Songs[1].HeaderAddress); // the other song is untouched
         Assert.StartsWith("Converted My Song.mid: 1 track, ", tab.Status);
         Assert.Contains("inserted as song 0", tab.Status);
         Assert.Contains("instruments from all_instruments", tab.Status);
         Assert.Contains("General MIDI programs", tab.Status); // and the way out for a MIDI file from a game's own folder
      }

      [Fact]
      public void Tab_InsertMidi_UsesAllInstruments_UnlessAnotherVoicegroupWasPicked() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;

         // selecting a song makes its own voicegroup the selected one, but that is not a choice of the user: a MIDI file still gets All Instruments
         tab.SelectedSong = tab.Songs[1];
         Assert.Equal(Other, tab.SelectedVoicegroup.Address);
         tab.InsertSong.Execute(null);
         Assert.Equal(AllInstruments, tab.Songs[2].Header.Voicegroup);

         // 'always use this one' is a choice too
         tab.SelectedSong = tab.Songs[1];
         tab.OverrideVoicegroup = true;
         tab.InsertSong.Execute(null);
         Assert.Equal(Other, tab.Songs[3].Header.Voicegroup);
         Assert.DoesNotContain("General MIDI programs", tab.Status); // the user already chose
         Assert.Empty(tabErrors);
      }

      /// <summary>Two voicegroups of 16 real instruments each: Unused (listed first) and Popular (the one both songs use). No All Instruments voicegroup.</summary>
      private const int UnusedGroup = 0x800, PopularGroup = 0x900;
      private void BuildWithoutBankButTwoBigVoicegroups() {
         Build(named: false);
         for (int i = 0; i < 16; i++) { WriteSquare(UnusedGroup + i * ToneSize, 1, i % 4); WriteSquare(PopularGroup + i * ToneSize, 2, i % 3); }
         Model.ObserveAnchorWritten(Token, "sound.voicegroups.unused_group", new NoInfoRun(UnusedGroup));
         Model.ObserveAnchorWritten(Token, "sound.voicegroups.popular_group", new NoInfoRun(PopularGroup));
         for (int i = 0; i < headers.Length; i++) Model.WritePointer(Token, headers[i] + 4, PopularGroup);
      }

      [Fact]
      public void Tab_WithoutBank_DefaultsToTheVoicegroupMostSongsUse_AndSaysThereIsNoBank() {
         BuildWithoutBankButTwoBigVoicegroups();
         var tab = CreateTab();
         Assert.False(tab.HasAllInstruments);
         Assert.Equal(PopularGroup, tab.SelectedVoicegroup.Address); // not the first one of the list
         Assert.Contains(AllInstrumentsPatch.NoBankNote, tab.AllInstrumentsNote);
         Assert.Contains("popular_group", tab.SelectedVoicegroup.Title);
      }

      [Fact]
      public void Tab_InsertMidi_WithoutBank_UsesAnExistingVoicegroup_NeverDangles_AndSaysSo() {
         BuildWithoutBankButTwoBigVoicegroups();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];

         tab.InsertSong.Execute(null);

         Assert.Equal(3, SongCount);
         Assert.Equal(PopularGroup, tab.Songs[2].Header.Voicegroup);
         Assert.True(tab.Songs[2].Header.Voicegroup + 12 <= Model.Count);
         Assert.Contains(AllInstrumentsPatch.NoBankNote, tab.Status);
         Assert.Contains(AllInstrumentsPatch.NoBankNote, tabMessages.Last());
         Assert.DoesNotContain(tabErrors, error => error.Contains("Could not"));
      }

      [Fact]
      public void Tab_InsertMidi_WithoutBank_WhenTheUserPickedAVoicegroup_UsesThatOne_WithoutTheNote() {
         BuildWithoutBankButTwoBigVoicegroups();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];
         tab.SelectedVoicegroup = tab.Voicegroups.Single(group => group.Address == UnusedGroup);

         tab.InsertSong.Execute(null);

         Assert.Equal(UnusedGroup, tab.Songs[2].Header.Voicegroup);
         Assert.DoesNotContain(AllInstrumentsPatch.NoBankNote, tab.Status);
      }

      [Fact]
      public void Tab_InsertMidi_WithoutAnyVoicegroupAtAll_SaysSoAndWritesNothing() {
         SetFullModel(0xFF);
         var tab = CreateTab(); // no song table: nothing to insert into
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         var before = Model.RawData.ToArray();
         tab.InsertSong.Execute(null);
         Assert.Equal(before, Model.RawData);
      }

      [Fact]
      public void Tab_InsertMidi_WithoutAllInstruments_UsesTheSelectedVoicegroup() {
         Build(named: false);
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(Other, tab.Songs[2].Header.Voicegroup);
         Assert.Contains("instruments from other", tab.Status);
      }

      [Fact]
      public void Tab_InsertMidi_AsNewSong_NamesItAfterTheFile_AndOneUndoTakesItBack() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("My Song.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.InsertMusicPlayer = 1;

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(3, SongCount);
         Assert.Equal(3, tab.Songs.Count);
         var song = tab.Songs[2];
         Assert.Equal("My_Song", song.Name);
         Assert.Same(song, tab.SelectedSong);
         Assert.Equal(AllInstruments, song.Header.Voicegroup);
         var table = Model.GetTable(SoundTab.SongTable);
         Assert.Equal(1, Model.ReadMultiByteValue(table.Start + 2 * table.ElementLength + 4, 2)); // the music player
         Assert.Contains("inserted as song 2 (My_Song)", tab.Status);
         Assert.True(Model.TryGetList(SoundTab.SongNamesList, out var names));
         Assert.Equal("My_Song", names[2]);

         // inserting the same file again: the name stays unique
         tab.InsertSong.Execute(null);
         Assert.Equal("My_Song_2", tab.Songs[3].Name);

         // the song, its table entry and its name are one undo step
         tab.Undo.Execute(null);
         Assert.Equal(3, SongCount);
         Assert.Equal(3, tab.Songs.Count);
         Assert.True(Model.TryGetList(SoundTab.SongNamesList, out names));
         Assert.Equal("My_Song", names[2]);
         Assert.False(names.Count > 3 && names[3] == "My_Song_2");
         tab.Undo.Execute(null);
         Assert.Equal(2, SongCount);
         Assert.Equal(2, tab.Songs.Count);
         Assert.True(Model.TryGetList(SoundTab.SongNamesList, out names));
         Assert.False(names.Count > 2 && names[2] == "My_Song");
      }

      [Fact]
      public void Tab_InsertMidi_MovesTheSongTableWhenItHasNoRoom_AndSaysSoOnce() {
         Build();
         for (int i = 0; i < 16; i++) Model[TableAddress + 16 + i] = 0x42; // something right behind the table
         var tab = CreateTab();
         FileSystem.OpenFiles = (description, options) => new[] { Loaded("a.mid", Midi("single")), Loaded("b.mid", Midi("single")), Loaded("c.mid", Midi("single")) };

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(5, SongCount);
         Assert.NotEqual(TableAddress, Model.GetTable(SoundTab.SongTable).Start);
         Assert.Single(tabMessages.Where(message => message.StartsWith("The song table was moved to ")));
         Assert.Equal(headers[0], tab.Songs[0].HeaderAddress); // the old entries came along
         Assert.Equal(headers[1], tab.Songs[1].HeaderAddress);
         Assert.Equal(new[] { "a", "b", "c" }, tab.Songs.Skip(2).Select(song => song.Name).ToArray());
      }

      [Fact]
      public void Tab_InsertSong_RemindsWhenTheMusicPlayerMayNotPlayAllTracks() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         FileSystem.OpenFile = (description, options) => Loaded("ten.mid", MidiToAgbTests.Build(10));
         tab.InsertSong.Execute(null);
         Assert.DoesNotContain(tabMessages, message => message.Contains("music player"));

         FileSystem.OpenFile = (description, options) => Loaded("twelve.mid", MidiToAgbTests.Build(12));
         tab.InsertSong.Execute(null);
         Assert.Single(tabMessages, message => message.Contains("it has 12 tracks, but the music player of the original game plays only 10"));

         // the sound effect players have their own track counts: the note is only about the music player
         tabMessages.Clear();
         tab.InsertMusicPlayer = 1;
         tab.InsertSong.Execute(null);
         Assert.DoesNotContain(tabMessages, message => message.Contains("music player"));
         Assert.Empty(tabErrors);

         // a batch says it once
         tabMessages.Clear();
         tab.InsertMusicPlayer = 0;
         FileSystem.OpenFiles = (description, options) => new[] { Loaded("a.mid", MidiToAgbTests.Build(11)), Loaded("b.mid", MidiToAgbTests.Build(3)), Loaded("c.mid", MidiToAgbTests.Build(14)) };
         tab.InsertSong.Execute(null);
         Assert.Single(tabMessages, message => message.Contains("2 of them have more than 10 tracks"));
      }

      [Fact]
      public void Tab_InsertMidi_PlaysWithTheChosenVoicegroup() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.SelectedVoicegroup = tab.Voicegroups.First(group => group.Address == Other);

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(Other, tab.Songs[2].Header.Voicegroup);
      }

      [Fact]
      public void Tab_InsertMidi_WorksWithAVoicegroupThatHasNoName() {
         Build(named: false, nameOther: false);
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];
         Assert.Null(tab.SelectedVoicegroup.Info.Name);

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(Other, tab.Songs[2].Header.Voicegroup);
         Assert.Contains("instruments from Voicegroup 000500", tab.Status);
      }

      [Fact]
      public void Tab_InsertMidi_UsesTheOptionsOfTheTab() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         Assert.Equal(80, tab.MidiVolume);
         Assert.Equal(50, tab.MidiReverb);
         Assert.True(tab.MidiReverbOn);
         Assert.True(tab.MidiExactGateTime);
         tab.MidiVolume = 100;
         tab.MidiPriority = 7;
         tab.MidiReverbOn = false;

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         var header = tab.Songs[2].Header;
         Assert.Equal(0, header.Reverb);
         Assert.Equal(7, header.Priority);
         Assert.Equal(0xBE, Model[header.Tracks[0]]); // VOL
         Assert.Equal(100, Model[header.Tracks[0] + 1]); // 127 * master volume / 127

         tab.MidiReverbOn = true;
         tab.MidiReverb = 90;
         tab.MidiVolume = 300; // out of range: the limit is used
         Assert.Equal(127, tab.MidiVolume);
         tab.MidiPriority = -4;
         Assert.Equal(0, tab.MidiPriority);
         tab.InsertSong.Execute(null);
         header = tab.Songs[3].Header;
         Assert.Equal(0x80 + 90, header.Reverb);
         Assert.Equal(127, Model[header.Tracks[0] + 1]);
      }

      [Fact]
      public void Tab_InsertMidi_CanGiveTheSongAVoicegroupOfItsOwn() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertAsNewSong = true;
         tab.InsertWithNewVoicegroup = true;

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         var own = tab.Songs[2].Header.Voicegroup;
         Assert.NotEqual(AllInstruments, own);
         Assert.NotEqual(Other, own);
         Assert.Equal(Enumerable.Range(0, 4 * ToneSize).Select(i => Model[AllInstruments + i]).ToArray(), Enumerable.Range(0, 4 * ToneSize).Select(i => Model[own + i]).ToArray());
      }

      [Fact]
      public void Tab_InsertMidi_ExplainsAFileThatIsNotAMidi_AndChangesNothing() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("notes.mid", Encoding.ASCII.GetBytes("this is not a midi file"));
         tab.InsertAsNewSong = true;

         tab.InsertSong.Execute(null);

         Assert.Single(tabErrors);
         Assert.StartsWith("Could not convert notes.mid: ", tabErrors[0]);
         Assert.Contains("MThd", tabErrors[0]);
         Assert.Equal(2, SongCount);
         Assert.Equal(2, tab.Songs.Count);
      }

      [Fact]
      public void Tab_InsertMidi_RefusesMoreThanSixteenTracks_AndSongsWithoutNotes() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         FileSystem.OpenFile = (description, options) => Loaded("big.mid", MidiToAgbTests.Build(17));
         tab.InsertSong.Execute(null);
         Assert.Single(tabErrors);
         Assert.Contains("17 channels", tabErrors[0]);
         Assert.Contains("at most 16 tracks", tabErrors[0]);

         tabErrors.Clear();
         FileSystem.OpenFile = (description, options) => Loaded("sixteen.mid", MidiToAgbTests.Build(16));
         tab.InsertSong.Execute(null);
         Assert.Empty(tabErrors);
         Assert.Equal(3, SongCount);
         Assert.Equal(16, tab.Songs[2].Header.TrackCount);

         FileSystem.OpenFile = (description, options) => Loaded("empty.mid", FromHex("4D546864000000060000000101E04D54726B0000000400FF2F00"));
         tab.InsertSong.Execute(null);
         Assert.Single(tabErrors);
         Assert.Contains("no notes", tabErrors[0]);
         Assert.Equal(3, SongCount);
      }

      [Fact]
      public void Tab_InsertMidi_NeedsASongToReplace_OrTheNewSongOption() {
         Build();
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => Loaded("a.mid", Midi("single"));
         tab.InsertSong.Execute(null);
         Assert.Single(tabErrors);
         Assert.Contains("Select a song to replace", tabErrors[0]);
         Assert.Equal(2, SongCount);
      }

      [Fact]
      public void Tab_InsertSongs_AddsEveryFileAsANewSong_InOneUndoStep() {
         Build();
         var tab = CreateTab();
         var asm = Encoding.UTF8.GetBytes(@"
	.include ""MPlayDef.s""
	.equ	ins_grp, voicegroup000
	.global	inserted
	.align	2
inserted_1:
	.byte	VOICE , 1
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	FINE
	.align	2
inserted:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	ins_grp
	.word	inserted_1
	.end
");
         string[] extensions = null;
         FileSystem.OpenFiles = (description, options) => {
            extensions = options;
            return new[] { Loaded("First.mid", Midi("single")), Loaded("broken.mid", new byte[] { 1, 2, 3 }), Loaded("plain.s", asm), Loaded("Third Song.midi", MidiToAgbTests.Build(3)) };
         };
         // 'replace the selected song' is the default: a batch always adds new songs
         tab.SelectedSong = tab.Songs[0];

         tab.InsertSong.Execute(null);

         Assert.Equal("s", extensions[0]);
         Assert.Equal(5, SongCount);
         Assert.Equal(5, tab.Songs.Count);
         Assert.Equal(new[] { "MUS_ONE", "MUS_TWO", "First", "plain", "Third_Song" }, tab.Songs.Select(song => song.Name).ToArray());
         Assert.Equal(headers[0], tab.Songs[0].HeaderAddress); // nothing was replaced
         Assert.Equal(3, tab.Songs[4].Header.TrackCount);
         Assert.Equal(AllInstruments, tab.Songs[2].Header.Voicegroup); // MIDI files play All Instruments
         Assert.Equal(Other, tab.Songs[3].Header.Voicegroup); // the .s file uses the voicegroup selected with the song, as before
         Assert.Contains("Inserted 3 of 4 songs as songs 2 to 4", tab.Status);
         Assert.Single(tabErrors); // the broken file is named, the others went in
         Assert.Contains("broken.mid", tabErrors[0]);

         tab.Undo.Execute(null);
         Assert.Equal(2, SongCount);
         Assert.Equal(2, tab.Songs.Count);
      }

      [Fact]
      public void Tab_TryImport_TakesDroppedMidiFiles() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;

         Assert.True(tab.TryImport(Loaded("dropped song.mid", Midi("single")), FileSystem));
         Assert.True(tab.TryImport(Loaded("other.MIDI", Midi("single")), FileSystem));
         Assert.False(tab.TryImport(Loaded("picture.png", new byte[4]), FileSystem));
         Assert.False(tab.TryImport(Loaded("notes.txt", Encoding.ASCII.GetBytes("not a song")), FileSystem)); // only the file types that are songs
         Assert.True(tab.TryImport(Loaded("plain.s", Encoding.UTF8.GetBytes(SongSource)), FileSystem)); // .s files too, as before

         Assert.Empty(tabErrors);
         Assert.Equal(5, SongCount);
         Assert.Equal("dropped_song", tab.Songs[2].Name);
         Assert.Equal("other", tab.Songs[3].Name);
         Assert.Equal("plain", tab.Songs[4].Name);
      }

      [Fact]
      public void Tab_InsertedMidiSong_CanBeShownAsTextAndRendered() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         // the instruments of All Instruments in this ROM: 0 = square 1, 1 = a sampled sine, 2 = square 2
         for (int program = 0; program <= 2; program++) {
            var data = MidiToAgbTests.WithProgram(program);
            FileSystem.OpenFile = (description, options) => Loaded("note" + program + ".mid", data);
            tab.InsertSong.Execute(null);
            Assert.Empty(tabErrors);

            // the song shows as text that assembles again
            Assert.Contains("note" + program + "_1:", tab.SongText);
            Assert.Contains("VOICE , " + program, tab.SongText);
            var assembled = SongAssembler.Assemble(tab.SongText, 0x08004000, new Dictionary<string, int> { ["voicegroup000"] = 0x08000000 + AllInstruments });
            Assert.True(assembled.Success, assembled.Error);

            // and plays: about a second of sound, not silent, not clipping
            var (left, right) = new M4aRenderer(Model).Render(tab.SelectedSong.Header);
            double peak = 0;
            for (int i = 0; i < left.Length; i++) peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
            Assert.True(left.Length > 0.9 * M4aRenderer.OutputRate, $"program {program}: only {left.Length} samples");
            Assert.True(peak > 0.02, $"program {program} renders as silence (peak {peak})");
            Assert.True(peak <= 1.0, $"program {program} is too loud (peak {peak})");
         }
      }

      /// <summary>A format 0 file with a tempo, one quarter note and a marker with the given text one beat later.</summary>
      private static byte[] MidiWithMarker(string marker)
         => FromHex("4D546864000000060000000101E0" + "4D54726B00000019" + "00FF510307A120" + "00903C64" + "8360803C00" + "00FF0601" + ((int)marker[0]).ToString("X2") + "00FF2F00");

      [Fact]
      public void Tab_InsertMidi_WithALoopEndBeforeAnyLoopStart_IsRefused_NotJumpingIntoTheVoicegroup() {
         // mid2agb writes "GOTO song_1_B0" for a ']' marker with no '[' before it, and nothing defines that label.
         // The label must not be taken for the voicegroup: the song would jump into the instruments and play garbage.
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         var before = Model.RawData.ToArray();
         FileSystem.OpenFile = (description, options) => Loaded("loop.mid", MidiWithMarker("]"));

         tab.InsertSong.Execute(null);

         Assert.Single(tabErrors);
         Assert.Contains("loop_1_B0", tabErrors[0]);
         Assert.Contains("']'", tabErrors[0]);
         Assert.Equal(2, SongCount);
         Assert.Equal(before, Model.RawData); // nothing was written

         // with a start marker first it is a loop like any other
         FileSystem.OpenFile = (description, options) => Loaded("loop.mid", MidiWithMarker("["));
         tab.InsertSong.Execute(null);
         Assert.Single(tabErrors); // no new error
         Assert.Equal(3, SongCount);
      }

      [Fact]
      public void Tab_InsertSong_RefusesAJumpToALabelThatDoesNotExist_EvenIfItIsTheOnlyUnknownSymbol() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         var before = Model.RawData.ToArray();
         // the voicegroup is a number here, so "nowhere" is the only symbol the file leaves open
         var source = SongSource.Replace("t_grp, voicegroup000", "t_grp, 0x08000400").Replace("\t.byte\tFINE", "\t.byte\tGOTO\n\t .word\tnowhere\n\t.byte\tFINE");
         FileSystem.OpenFile = (description, options) => Loaded("broken.s", Encoding.UTF8.GetBytes(source));

         tab.InsertSong.Execute(null);

         Assert.Single(tabErrors);
         Assert.Contains("nowhere", tabErrors[0]);
         Assert.Equal(2, SongCount);
         Assert.Equal(before, Model.RawData);
      }

      [Fact]
      public void Tab_InsertSong_StillTakesAVoicegroupSymbolWithAnUnusualName() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];
         FileSystem.OpenFile = (description, options) => Loaded("odd.s", Encoding.UTF8.GetBytes(SongSource.Replace("voicegroup000", "my_instruments")));

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(3, SongCount);
         Assert.Equal(Other, tab.Songs[2].Header.Voicegroup); // the voicegroup selected with the song
      }

      [Fact]
      public void Tab_InsertSong_ReadsFilesSavedWithAByteOrderMark_OrAsUtf16()
         // Notepad saves "UTF-8" files with a byte order mark and "Unicode" files as UTF-16; Windows PowerShell's > writes UTF-16
         => InsertEncodedSongs(new[] {
            new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(SongSource)).ToArray(),
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(SongSource)).ToArray(),
            Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(SongSource)).ToArray(),
            Encoding.Unicode.GetBytes(SongSource), // UTF-16 without the mark
            Encoding.UTF8.GetBytes(SongSource.Replace("\n", "\r\n")), // and plain text with Windows line ends
         });

      [Fact]
      public void Tab_ExportSong_OfASongWithoutTracks_SavesNoFile_AndSaysWhy() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];
         var empty = SongSource.Replace("\t.byte\t1\n", "\t.byte\t0\n").Replace("\t.word\tt_1\n", "");
         FileSystem.OpenFile = (description, options) => Loaded("empty.s", Encoding.UTF8.GetBytes(empty));
         tab.InsertSong.Execute(null);
         Assert.Empty(tabErrors);
         Assert.Equal(0, tab.Songs[2].Header.TrackCount);

         var saved = new List<LoadedFile>();
         FileSystem.RequestNewName = (name, description, extensions) => "empty_export.s";
         FileSystem.Save = file => { saved.Add(file); return true; };
         tab.SelectedSong = tab.Songs[2];
         tab.ExportSong.Execute(null);

         // a file that only says '(no tracks)' could not be put back in a ROM
         Assert.Empty(saved);
         Assert.Single(tabErrors);
         Assert.Contains("no tracks", tabErrors[0]);
      }

      /// <summary>The 'single' file with a program change (instrument 5) before the first note.</summary>
      private static byte[] MidiWithProgram(int program)
         => FromHex("4D546864000000060000000101E04D54726B0000002000FF510307A12000C0" + program.ToString("X2") + "00903C648360803C000090405A836080400000FF2F00");

      [Fact]
      public void Tab_InsertMidi_WithoutAProgramChange_PlaysInstrumentZero_NotASilentTrack() {
         // MIDI plays program 0 on a channel that never chooses one. In the game a track that has not chosen an instrument starts on an empty square wave: it is silent.
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         FileSystem.OpenFile = (description, options) => Loaded("plain.mid", Midi("single"));

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         var text = tab.SongText;
         var voice = text.IndexOf("VOICE", StringComparison.Ordinal);
         Assert.True(voice >= 0, "no VOICE command in " + text);
         Assert.True(voice < text.IndexOf("Cn3", StringComparison.Ordinal), "the instrument is chosen after the first note");
         Assert.Equal(voice, text.LastIndexOf("VOICE", StringComparison.Ordinal));
      }

      [Fact]
      public void Tab_InsertMidi_WithAProgramChange_KeepsIt_AndAddsNoOther() {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         FileSystem.OpenFile = (description, options) => Loaded("piano.mid", MidiWithProgram(5));

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         var text = tab.SongText;
         var voice = text.IndexOf("VOICE", StringComparison.Ordinal);
         Assert.True(voice >= 0);
         Assert.Equal(voice, text.LastIndexOf("VOICE", StringComparison.Ordinal));
         Assert.Contains("VOICE , 5", text.Replace("\t", " ").Replace("  ", " "));
      }

      [Fact]
      public void MidiToAgb_AddMissingVoice_IsOffUnlessAsked_LikeMid2Agb() {
         var plain = MidiToAgb.Convert(Midi("single"), new MidiToAgbOptions());
         Assert.DoesNotContain("VOICE", plain.Text);
         var added = MidiToAgb.Convert(Midi("single"), new MidiToAgbOptions { AddMissingVoice = true });
         Assert.Contains("VOICE", added.Text);
         var withProgram = MidiToAgb.Convert(MidiWithProgram(0), new MidiToAgbOptions { AddMissingVoice = true });
         Assert.Equal(1, withProgram.Text.Split("VOICE").Length - 1);
      }

      [Fact]
      public void Tab_InsertSong_WhenTheSongFitsButTheBiggerSongTableDoesNot_IsRefused_NotAGrownRom() {
         // a nearly full ROM: room for the song at the very end, and something right behind the song table so that the table has to move
         Build();
         for (int i = 0; i < Model.Count; i++) if (Model[i] == 0xFF) Model[i] = 0; // every free byte is used...
         for (int i = Model.Count - 0x80; i < Model.Count; i++) Model[i] = 0xFF;     // ...but the last 128
         Model[TableAddress + 2 * 8] = 0x12;
         var tab = CreateTab();
         tab.MaxRomLength = Model.Count;
         tab.InsertAsNewSong = true;
         var before = Model.RawData.ToArray();
         FileSystem.OpenFile = (description, options) => Loaded("a.s", Encoding.UTF8.GetBytes(SongSource));

         tab.InsertSong.Execute(null);

         Assert.Single(tabErrors);
         Assert.Contains("song table", tabErrors[0]);
         Assert.Equal(before.Length, Model.Count); // the ROM did not grow past the size of a cartridge
         Assert.Equal(before, Model.RawData);      // and nothing was written
         Assert.Equal(2, SongCount);
      }

      [Fact]
      public void Tab_RenameSong_NamesThatTheMetadataFileCouldNotHold_AreCleanedUp_AndTheOtherNamesSurviveASave() {
         Build();
         var tab = CreateTab();
         var awkward = new[] { "Route #1", "it'''s", "two\nlines", "tab\tbed", "  spaced name  ", "\u00fcn\u00ef \u65e5\u672c" };
         var expected = new[] { "Route__1", "it''s", "two_lines", "tab_bed", "spaced_name", "\u00fcn\u00ef_\u65e5\u672c" };
         for (int i = 0; i < awkward.Length; i++) {
            tab.SelectedSong = tab.Songs[1];
            tab.SelectedSongName = awkward[i];
            Assert.Empty(tabErrors);
            Assert.Equal(expected[i], tab.Songs[1].Name);

            // what is saved is what comes back: no other name is lost or moved by the one that was typed
            var saved = new StoredMetadata(Model.ExportMetadata(Singletons.MetadataInfo).Serialize());
            var list = saved.Lists.Single(l => l.Name == SoundTab.SongNamesList);
            Assert.Equal(expected[i], list[1]);
            Assert.Equal("MUS_ONE", list[0]);
            Assert.Equal(2, list.Count);
         }
      }

      [Fact]
      public void Tab_InsertSong_WithAHeaderPointerThatPointsOutsideTheRom_IsRefused_NotInsertedAsASongThatCannotPlay() {
         // a track pointer or voicegroup that is a number, not a label, is written as it is: -1, 0 and 0xFFFFFFFF are no address of this ROM
         foreach (var (find, replace, expectedWord) in new[] {
            ("\t.word\tt_1\n", "\t.word\t-1\n", "track 1"),
            ("\t.word\tt_1\n", "\t.word\t0xFFFFFFFF\n", "track 1"),
            ("\t.word\tt_1\n", "\t.word\t0\n", "track 1"),
            ("t_grp, voicegroup000", "t_grp, 0", "voicegroup"),
            ("t_grp, voicegroup000", "t_grp, 0x7FFFFFFF", "voicegroup"),
         }) {
            Build();
            var tab = CreateTab();
            tab.InsertAsNewSong = true;
            var before = Model.RawData.ToArray();
            tabErrors.Clear();
            var source = SongSource.Replace(find, replace);
            Assert.NotEqual(SongSource, source);
            FileSystem.OpenFile = (description, options) => Loaded("broken.s", Encoding.UTF8.GetBytes(source));

            tab.InsertSong.Execute(null);

            Assert.Single(tabErrors);
            Assert.Contains(expectedWord, tabErrors[0]);
            Assert.Equal(2, SongCount);
            Assert.Equal(before, Model.RawData);
         }
      }

      [Fact]
      public void Tab_InsertSong_WithHeaderPointersIntoTheRom_IsInserted() {
         // a number is fine when it is an address of the ROM
         foreach (var (find, replace) in new[] {
            ("t_grp, voicegroup000", "t_grp, 0x08000400"),
            ("\t.word\tt_1\n", "\t.word\t0x08000800\n"),
         }) {
            Build();
            var tab = CreateTab();
            tab.InsertAsNewSong = true;
            tabErrors.Clear();
            FileSystem.OpenFile = (description, options) => Loaded("fine.s", Encoding.UTF8.GetBytes(SongSource.Replace(find, replace)));

            tab.InsertSong.Execute(null);

            Assert.Empty(tabErrors);
            Assert.Equal(3, SongCount);
         }
      }

      private void InsertEncodedSongs(byte[][] files) {
         Build();
         var tab = CreateTab();
         tab.InsertAsNewSong = true;
         tab.SelectedSong = tab.Songs[0];
         for (int i = 0; i < files.Length; i++) {
            var data = files[i];
            FileSystem.OpenFile = (description, options) => Loaded("encoded" + i + ".s", data);
            tab.InsertSong.Execute(null);
            Assert.Empty(tabErrors);
            Assert.Equal(3 + i, SongCount);
            Assert.Equal(1, tab.Songs[2 + i].Header.TrackCount);
         }
         // the same files in one batch
         FileSystem.OpenFiles = (description, options) => files.Select((data, i) => Loaded("batch" + i + ".s", data)).ToArray();
         tab.InsertSong.Execute(null);
         Assert.Empty(tabErrors);
         Assert.Equal(2 + 2 * files.Length, SongCount);
      }
   }

}
