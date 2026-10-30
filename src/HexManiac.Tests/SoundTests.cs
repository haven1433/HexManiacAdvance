using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Sound;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class SoundTests : BaseViewModelTestClass {
      public SoundTests() : base(0x4000) { }

      [Fact]
      public void Dpcm_EncodeDecode_StaysClose() {
         var samples = new sbyte[300];
         for (int i = 0; i < samples.Length; i++) samples[i] = (sbyte)(Math.Sin(i / 7.0) * 100);

         var encoded = GbaSample.EncodeCompressed(samples);
         var decoded = GbaSample.Decode(encoded, 0, samples.Length, true);

         Assert.Equal(GbaSample.CompressedLength(samples.Length), encoded.Length);
         Assert.Equal(samples.Length, decoded.Length);
         Assert.Equal(samples[0], decoded[0]);
         Assert.Equal(samples[64], decoded[64]); // each 64-sample block restarts from an exact sample
         var error = Math.Sqrt(samples.Select((s, i) => (double)(s - decoded[i]) * (s - decoded[i])).Average());
         Assert.True(error < 20, $"rms error {error}");
      }

      [Fact]
      public void CompressedLength_MatchesWav2agbNoPad() {
         // 8184 samples: 127 full blocks of 33 bytes + a 56 sample block of 29 bytes = 4220
         Assert.Equal(4220, GbaSample.CompressedLength(8184));
         Assert.Equal(68, GbaSample.CompressedLength(128)); // 2 blocks of 33, aligned to 4
         Assert.Equal(4, GbaSample.CompressedLength(1));
      }

      [Fact]
      public void Sample_BuildAndRead_RoundTrip() {
         var samples = new sbyte[100];
         for (int i = 0; i < samples.Length; i++) samples[i] = (sbyte)(i - 50);
         var bytes = GbaSample.Build(samples, 10512, compressed: false);
         for (int i = 0; i < bytes.Length; i++) Model[0x40 + i] = bytes[i];

         Assert.True(GbaSample.TryRead(Model, 0x40, out var sample));
         Assert.False(sample.IsCompressed);
         Assert.Equal(100, sample.SampleCount);
         Assert.Equal(10512, sample.SampleRate, 3);
         Assert.Equal(samples, sample.Decode(Model));
      }

      [Fact]
      public void Wav_WriteAndRead_RoundTrip() {
         var samples = new sbyte[50];
         for (int i = 0; i < samples.Length; i++) samples[i] = (sbyte)(i * 2 - 50);
         var wav = GbaSample.ToWav(samples, 13379);

         Assert.True(GbaSample.TryReadWav(wav, out var read, out var rate, out var error), error);
         Assert.Equal(13379, rate);
         Assert.Equal(samples, GbaSample.Quantize(read));
      }

      #region WAV files from other programs

      private static byte[] Le(long value, int bytes) => Enumerable.Range(0, bytes).Select(i => (byte)(value >> (8 * i))).ToArray();

      private static byte[] Chunk(string id, byte[] body, long declaredSize = -1)
         => System.Text.Encoding.ASCII.GetBytes(id).Concat(Le(declaredSize >= 0 ? declaredSize : body.Length, 4)).Concat(body).Concat(body.Length % 2 == 1 && declaredSize < 0 ? new byte[1] : new byte[0]).ToArray();

      private static byte[] Riff(params byte[][] chunks) {
         var content = chunks.SelectMany(chunk => chunk).ToArray();
         return System.Text.Encoding.ASCII.GetBytes("RIFF").Concat(Le(content.Length + 4, 4)).Concat(System.Text.Encoding.ASCII.GetBytes("WAVE")).Concat(content).ToArray();
      }

      private static byte[] Fmt(int tag, int channels, int rate, int bits)
         => Le(tag, 2).Concat(Le(channels, 2)).Concat(Le(rate, 4)).Concat(Le((long)rate * channels * bits / 8, 4)).Concat(Le(channels * bits / 8, 2)).Concat(Le(bits, 2)).ToArray();

      /// <summary>The 40 byte format chunk of WAVE_FORMAT_EXTENSIBLE: Audacity, Reaper and Windows write 24 bit and 32 bit float files this way. 'subTag' is the first two bytes of the sub format GUID.</summary>
      private static byte[] ExtensibleFmt(int subTag, int channels, int rate, int bits)
         => Fmt(0xFFFE, channels, rate, bits).Concat(Le(22, 2)).Concat(Le(bits, 2)).Concat(Le(0, 4))
            .Concat(Le(subTag, 2)).Concat(new byte[] { 0, 0, 0, 0, 0x10, 0, 0x80, 0, 0, 0xAA, 0, 0x38, 0x9B, 0x71 }).ToArray();

      [Fact]
      public void Wav_Extensible24Bit_IsRead() {
         var data = Le(0x400000, 3).Concat(Le(-0x400000, 3)).Concat(Le(0, 3)).ToArray();
         var wav = Riff(Chunk("fmt ", ExtensibleFmt(1, 1, 22050, 24)), Chunk("data", data));

         Assert.True(GbaSample.TryReadWav(wav, out var samples, out var rate, out var error), error);

         Assert.Equal(22050, rate);
         Assert.Equal(new[] { 0.5, -0.5, 0.0 }, samples);
      }

      [Fact]
      public void Wav_ExtensibleFloat_IsRead() {
         var data = BitConverter.GetBytes(0.25f).Concat(BitConverter.GetBytes(-0.5f)).ToArray();
         var wav = Riff(Chunk("fmt ", ExtensibleFmt(3, 1, 32000, 32)), Chunk("data", data));

         Assert.True(GbaSample.TryReadWav(wav, out var samples, out var rate, out var error), error);

         Assert.Equal(new[] { 0.25, -0.5 }, samples);
      }

      [Fact]
      public void Wav_CutOffInsideTheFormatChunk_IsRefused_NotAnException() {
         var wav = Riff(Chunk("fmt ", Fmt(1, 1, 13379, 16))).Take(30).ToArray();

         Assert.False(GbaSample.TryReadWav(wav, out _, out _, out var error));
         Assert.False(string.IsNullOrEmpty(error));
      }

      [Fact]
      public void Wav_ChunkSizeThatPointsBackwards_DoesNotHang() {
         // a chunk size is unsigned: 0xFFFFFFF8 read as a signed number moved the reader back to the start of the same chunk, forever
         var wav = Riff(Chunk("fmt ", Fmt(1, 1, 13379, 16)), Chunk("junk", new byte[4], 0xFFFFFFF8L), Chunk("data", new byte[8]));

         var task = System.Threading.Tasks.Task.Run(() => GbaSample.TryReadWav(wav, out _, out _, out _));

         Assert.True(task.Wait(5000), "TryReadWav does not return");
      }

      [Fact]
      public void Wav_DataSizeOfAStreamingWriter_ReadsToTheEndOfTheFile() {
         // ffmpeg writing to a pipe cannot know the length, and leaves 0xFFFFFFFF in the data chunk
         var data = Le(1000, 2).Concat(Le(2000, 2)).Concat(Le(-3000, 2)).Concat(Le(4000, 2)).ToArray();
         var wav = Riff(Chunk("fmt ", Fmt(1, 1, 13379, 16)), Chunk("data", data, 0xFFFFFFFFL));

         Assert.True(GbaSample.TryReadWav(wav, out var samples, out var rate, out var error), error);

         Assert.Equal(4, samples.Length);
         Assert.Equal(-3000 / 32768.0, samples[2], 6);
      }

      #endregion

      [Fact]
      public void Resample_HalvesLength() {
         var samples = Enumerable.Range(0, 100).Select(i => i / 100.0).ToArray();
         var result = GbaSample.Resample(samples, 44100, 22050);
         Assert.Equal(50, result.Length);
         Assert.Equal(0, result[0], 6);
         Assert.Equal(0.5, result[25], 6);
      }

      private const string Song = @"
	.include ""MPlayDef.s""

	.equ	test_grp, voicegroup000
	.equ	test_pri, 0
	.equ	test_rev, reverb_set+50
	.equ	test_mvl, 100
	.equ	test_key, 0
	.equ	test_tbs, 1

	.section .rodata
	.global	test
	.align	2

test_1:
	.byte	KEYSH , test_key+0
	.byte	TEMPO , 120*test_tbs/2
	.byte		VOICE , 48
	.byte		PAN   , c_v-16
	.byte		VOL   , 47*test_mvl/mxv
test_1_B1:
	.byte		N12   , Cn4 , v112
	.byte	W12
	.byte		        En4 , v096
	.byte	W12
	.byte	GOTO
	 .word	test_1_B1
	.byte	FINE

	.align	2
test:
	.byte	1	@ NumTrks
	.byte	0	@ NumBlks
	.byte	test_pri	@ Priority
	.byte	test_rev	@ Reverb.

	.word	test_grp

	.word	test_1

	.end
";

      [Fact]
      public void Assembler_ReportsUndefinedVoicegroup() {
         var result = SongAssembler.Assemble(Song, 0x08100000);
         Assert.False(result.Success);
         Assert.Equal(new[] { "voicegroup000" }, result.UndefinedSymbols);
      }

      [Fact]
      public void Assembler_BuildsHeaderAndTrack() {
         var result = SongAssembler.Assemble(Song, 0x08100000, new System.Collections.Generic.Dictionary<string, int> { ["voicegroup000"] = 0x08200000 });
         Assert.True(result.Success, result.Error);
         Assert.Equal("test", result.SongName);
         var bytes = result.Bytes;
         Assert.Equal(new byte[] { 0xBC, 0, 0xBB, 60, 0xBD, 48, 0xBF, 0x30, 0xBE, 37 }, bytes.Take(10));
         Assert.Equal(new byte[] { 0xDB, 72, 112, 0x8C, 76, 96, 0x8C, 0xB2 }, bytes.Skip(10).Take(8));
         Assert.Equal(0x08100000 + 10, BitConverter.ToInt32(bytes, 18)); // GOTO test_1_B1
         Assert.Equal(0xB1, bytes[22]);
         Assert.Equal(24, result.HeaderOffset); // aligned to 4
         Assert.Equal(new byte[] { 1, 0, 0, 0x80 + 50 }, bytes.Skip(24).Take(4));
         Assert.Equal(0x08200000, BitConverter.ToInt32(bytes, 28));
         Assert.Equal(0x08100000, BitConverter.ToInt32(bytes, 32));
      }

      [Fact]
      public void Disassembler_RoundTrips() {
         var result = SongAssembler.Assemble(Song, 0x08000100, new System.Collections.Generic.Dictionary<string, int> { ["voicegroup000"] = 0x08000180 });
         for (int i = 0; i < result.Bytes.Length; i++) Model[0x100 + i] = result.Bytes[i];
         Assert.True(SongHeader.TryRead(Model, 0x100 + result.HeaderOffset, out var header));
         Assert.Equal(1, header.TrackCount);
         Assert.Equal(0x180, header.Voicegroup);

         var text = new SongDisassembler(Model).Disassemble(header, "again");
         Assert.Contains("KEYSH", text);
         Assert.Contains("GOTO", text);
         var again = SongAssembler.Assemble(text, 0x08000100);
         Assert.True(again.Success, again.Error);
         Assert.Equal(result.Bytes, again.Bytes);
      }

      [Fact]
      public void Renderer_PlaysSquareAndSampleNotes_ThenGoesQuiet() {
         // voicegroup at 0x100: voice 0 = square 1 (50% duty, instant attack, no decay), voice 1 = a direct-sound sine sample at 0x200
         int voicegroup = 0x100;
         Model[voicegroup] = 1; Model[voicegroup + 1] = 60; Model[voicegroup + 4] = 2; Model[voicegroup + 8] = 0; Model[voicegroup + 9] = 0; Model[voicegroup + 10] = 15; Model[voicegroup + 11] = 0;
         var sine = new sbyte[2000];
         for (int i = 0; i < sine.Length; i++) sine[i] = (sbyte)(Math.Sin(i * 2 * Math.PI * 220 / 13379) * 100);
         var sample = GbaSample.Build(sine, 13379, compressed: false, loopStart: 0);
         for (int i = 0; i < sample.Length; i++) Model[0x200 + i] = sample[i];
         Model[voicegroup + 12] = 0; Model[voicegroup + 13] = 60; Model.WritePointer(Token, voicegroup + 16, 0x200);
         Model[voicegroup + 20] = 255; Model[voicegroup + 21] = 0; Model[voicegroup + 22] = 255; Model[voicegroup + 23] = 0;

         // the song: tempo 120, voice 0 Cn3 for a quarter note, then voice 1 En3 for a quarter note, then FINE
         var song = SongAssembler.Assemble(@"
	.include ""MPlayDef.s""
	.equ	t_grp, voicegroup000
t_1:
	.byte	TEMPO , 120*1/2
	.byte	VOICE , 0
	.byte	VOL , 127
	.byte	N24 , Cn3 , v127
	.byte	W24
	.byte	VOICE , 1
	.byte	N24 , En3 , v127
	.byte	W24
	.byte	W24
	.byte	FINE
t:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	t_grp
	.word	t_1
	.end
", 0x08000400, new System.Collections.Generic.Dictionary<string, int> { ["voicegroup000"] = 0x08000000 + voicegroup });
         Assert.True(song.Success, song.Error);
         for (int i = 0; i < song.Bytes.Length; i++) Model[0x400 + i] = song.Bytes[i];
         Assert.True(SongHeader.TryRead(Model, 0x400 + song.HeaderOffset, out var header));

         var (left, right) = new M4aRenderer(Model).Render(header);

         // 120 bpm: a quarter note is half a second. The song is 1.5 seconds long, then everything stops.
         double Rms(int fromSecondsTenths, int toSecondsTenths) {
            int from = fromSecondsTenths * M4aRenderer.OutputRate / 10, to = Math.Min(left.Length, toSecondsTenths * M4aRenderer.OutputRate / 10);
            if (to <= from) return 0;
            double sum = 0;
            for (int i = from; i < to; i++) sum += left[i] * left[i] + right[i] * right[i];
            return Math.Sqrt(sum / (to - from) / 2);
         }
         Assert.True(left.Length > 1.4 * M4aRenderer.OutputRate, $"only {left.Length} samples");
         Assert.True(Rms(1, 4) > 0.05, "the square wave note should be audible");
         Assert.True(Rms(6, 9) > 0.05, "the sampled note should be audible");
         Assert.True(Rms(12, 15) < 0.01, "nothing should play after the notes end");
         Assert.True(left.Length < 4 * M4aRenderer.OutputRate, "the render should stop soon after FINE");
      }

      [Fact]
      public void Assembler_NamesTheSymbolOfTheHeadersVoicegroup_NotTheOtherUnknownSymbols() {
         // the usual voicegroup symbol, used through an .equ
         Assert.Equal("voicegroup000", SongAssembler.Assemble(Song, 0x08100000).VoicegroupSymbol);
         // a name nobody would guess
         Assert.Equal("my_instruments", SongAssembler.Assemble(Song.Replace("voicegroup000", "my_instruments"), 0x08100000).VoicegroupSymbol);
         // the voicegroup is a number and a GOTO goes to a label that does not exist: the label is not the voicegroup
         var jump = Song.Replace("test_grp, voicegroup000", "test_grp, 0x08000500").Replace("\t.byte\tFINE", "\t.byte\tGOTO\n\t .word\tnowhere\n\t.byte\tFINE");
         var result = SongAssembler.Assemble(jump, 0x08100000);
         Assert.False(result.Success);
         Assert.Equal(new[] { "nowhere" }, result.UndefinedSymbols);
         Assert.Null(result.VoicegroupSymbol);
         // a song that assembles has no unknown symbol to name
         Assert.Null(SongAssembler.Assemble(Song, 0x08100000, new System.Collections.Generic.Dictionary<string, int> { ["voicegroup000"] = 0x08200000 }).VoicegroupSymbol);
      }

      /// <summary>Puts a song made from text in the small test ROM, reads it back as text, assembles that, and returns both byte arrays.</summary>
      private (byte[] original, byte[] again, string text) RoundTripAt(string source) {
         var result = SongAssembler.Assemble(source, 0x08000100, new System.Collections.Generic.Dictionary<string, int> { ["voicegroup000"] = 0x08000300 });
         Assert.True(result.Success, result.Error);
         for (int i = 0; i < result.Bytes.Length; i++) Model[0x100 + i] = result.Bytes[i];
         Assert.True(SongHeader.TryRead(Model, 0x100 + result.HeaderOffset, out var header));
         var text = new SongDisassembler(Model).Disassemble(header, "again");
         var again = SongAssembler.Assemble(text, 0x08000100);
         Assert.True(again.Success, again.Error + Environment.NewLine + text);
         return (result.Bytes, again.Bytes, text);
      }

      private static string TrackSource(string commands) => @"
	.include ""MPlayDef.s""
	.equ	t_grp, voicegroup000
t_1:
" + commands + @"
	.align	2
t:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	t_grp
	.word	t_1
	.end
";

      [Fact]
      public void Disassembler_KeepsTheAddressOfARepeatThatCountsZero() {
         // REPT 0 is an endless jump, but the four bytes of the address are still there: the song has to read back as it was
         var (original, again, text) = RoundTripAt(TrackSource(@"
	.byte	VOL , 100
t_1_B1:
	.byte	N12 , Cn3 , v100
	.byte	W12
	.byte	REPT , 3
	 .word	t_1_B1
	.byte	REPT , 0
	 .word	t_1_B2
t_1_B2:
	.byte	W12
	.byte	FINE"));
         Assert.Equal(original, again);
         Assert.Contains("REPT , 3", text);
         Assert.Contains("REPT , 0", text);
      }

      [Fact]
      public void Disassembler_WritesAGateTimeThatMid2AgbHasNoNameForAsANumber() {
         // mid2agb only uses gtp1 to gtp3, other programs write any number from 0 to 127 after the velocity
         var (original, again, text) = RoundTripAt(TrackSource(@"
	.byte	N04 , Cn3 , v100 , gtp2
	.byte	W12
	.byte	N04 , Dn3 , v100 , 9
	.byte	W12
	.byte	N04 , En3 , v100 , 0
	.byte	W12
	.byte	N04 , Fn3 , v100 , 127
	.byte	W12
	.byte	FINE"));
         Assert.Equal(original, again);
         Assert.Contains("gtp2", text);
         Assert.DoesNotContain("gtp9", text);
         Assert.DoesNotContain("gtp0", text);
         Assert.DoesNotContain("gtp127", text);
      }

      [Fact]
      public void Disassembler_RoundTripsEveryCommandTheEngineHas() {
         var (original, again, text) = RoundTripAt(TrackSource(@"
	.byte	VOL , 100
	.byte	KEYSH , t_key + 3
	.byte	PRIO , 7
	.byte	TEMPO , 90
	.byte	VOICE , 5
	.byte	PAN , c_v+20
	.byte	BEND , c_v-10
	.byte	BENDR , 12
	.byte	LFOS , 20
	.byte	LFODL , 10
	.byte	MOD , 30
	.byte	MODT , mod_tre
	.byte	TUNE , c_v+5
	.byte	XCMD , xIECV , 40
	.byte	XCMD , xIECL , 10
	.byte	MEMACC , mem_set , 1 , 5
	.byte	TIE , En3 , v090
	.byte	W48
	.byte	EOT , En3
	.byte	PATT
	 .word	t_1_000
	.byte	FINE
t_1_000:
	.byte	N06 , Gn3 , v127 , gtp2
	.byte	W24
	.byte	PEND").Replace("t_key + 3", "3"));
         Assert.Equal(original, again);
      }
   }
}
