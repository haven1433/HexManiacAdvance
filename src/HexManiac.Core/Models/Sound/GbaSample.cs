using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Sound {
   /// <summary>
   /// A DirectSound sample as stored in the ROM (the format used by Pokémon cries and most sampled instruments).
   /// Layout: 16 byte header { flags (bit0: 1=DPCM compressed, bit30: looped), pitch (sampleRate*1024), loopStart, loopEnd/sampleCount } followed by the sample data.
   /// Uncompressed data is signed 8-bit PCM. Compressed data is blocks of 64 samples: one signed 8-bit sample followed by 63 4-bit deltas (low nibble first) that index a delta table.
   /// This matches the wav2agb tool used by pokeemerald/pokefirered and the decoder in the m4a sound engine.
   /// </summary>
   public class GbaSample {
      public const int HeaderLength = 16;
      public const int BlockSamples = 64;
      public const int CompressedBlockLength = 33;
      public static readonly sbyte[] DeltaTable = { 0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1 };

      public int Address { get; init; }
      public bool IsCompressed { get; init; }
      public bool IsLooped { get; init; }
      public uint Pitch { get; init; }
      public int LoopStart { get; init; }
      public int SampleCount { get; init; }

      public double SampleRate => Pitch / 1024.0;
      public double DurationSeconds => SampleRate <= 0 ? 0 : SampleCount / SampleRate;
      public int DataLength => IsCompressed ? CompressedLength(SampleCount) : (SampleCount + 3) / 4 * 4;
      public int TotalLength => HeaderLength + DataLength;

      public static int CompressedLength(int sampleCount) {
         // each block: one byte for the first sample, one byte holding only the first delta, then two deltas per byte
         int length = 0;
         for (int i = 0; i < sampleCount; i += BlockSamples) {
            int inBlock = Math.Min(BlockSamples, sampleCount - i);
            length += inBlock == 1 ? 1 : 2 + (inBlock - 1) / 2;
         }
         return (length + 3) / 4 * 4;
      }

      public static bool TryRead(IDataModel model, int address, out GbaSample sample) {
         sample = null;
         if (address < 0 || address + HeaderLength > model.Count) return false;
         var flags = (uint)model.ReadMultiByteValue(address, 4);
         var pitch = (uint)model.ReadMultiByteValue(address + 4, 4);
         var loopStart = model.ReadMultiByteValue(address + 8, 4);
         var sampleCount = model.ReadMultiByteValue(address + 12, 4);
         if ((flags & 0x3FFFFFFE) != 0) return false; // only format (bit0) and loop (bit30) are defined
         if (pitch < 1024 || pitch > 96000u * 1024) return false;
         if (sampleCount <= 0 || sampleCount > 0x400000) return false;
         if (loopStart < 0 || loopStart > sampleCount) return false;
         sample = new GbaSample {
            Address = address,
            IsCompressed = (flags & 1) != 0,
            IsLooped = (flags & 0x40000000) != 0,
            Pitch = pitch,
            LoopStart = loopStart,
            SampleCount = sampleCount,
         };
         if (address + sample.TotalLength > model.Count) { sample = null; return false; }
         return true;
      }

      /// <summary>
      /// Decode the sample to signed 8-bit PCM.
      /// </summary>
      public sbyte[] Decode(IDataModel model) => Decode(model.RawData, Address + HeaderLength, SampleCount, IsCompressed);

      public static sbyte[] Decode(IReadOnlyList<byte> data, int start, int sampleCount, bool compressed) {
         var result = new sbyte[sampleCount];
         if (!compressed) {
            for (int i = 0; i < sampleCount && start + i < data.Count; i++) result[i] = (sbyte)data[start + i];
            return result;
         }
         int read = start;
         for (int blockStart = 0; blockStart < sampleCount; blockStart += BlockSamples) {
            if (read >= data.Count) break;
            int level = (sbyte)data[read++];
            result[blockStart] = (sbyte)level;
            int inBlock = Math.Min(BlockSamples, sampleCount - blockStart);
            for (int i = 1; i < inBlock; i += 2) {
               if (read >= data.Count) break;
               var b = data[read++];
               level += DeltaTable[b & 0xF];
               result[blockStart + i] = (sbyte)level;
               if (i + 1 < inBlock) {
                  level += DeltaTable[b >> 4];
                  result[blockStart + i + 1] = (sbyte)level;
               }
            }
         }
         return result;
      }

      /// <summary>
      /// Encode signed 8-bit PCM with the DPCM scheme understood by the m4a engine.
      /// Lookahead is the number of samples considered when choosing each delta (wav2agb uses 1 for cries, 3 by default).
      /// </summary>
      public static byte[] EncodeCompressed(sbyte[] samples, int lookahead = 1) {
         lookahead = Math.Clamp(lookahead, 1, 8);
         var output = new List<byte>(samples.Length / 2 + samples.Length / BlockSamples + 4);
         for (int blockStart = 0; blockStart < samples.Length; blockStart += BlockSamples) {
            int inBlock = Math.Min(BlockSamples, samples.Length - blockStart);
            int level = samples[blockStart];
            output.Add((byte)(sbyte)level);
            int index = 1;
            // the first byte of a block only holds one delta (in the low nibble), so that each block is exactly 33 bytes
            if (index < inBlock) {
               var delta = ChooseDelta(samples, blockStart + index, Math.Min(lookahead, inBlock - index), level);
               level += DeltaTable[delta];
               output.Add((byte)delta);
               index++;
            }
            while (index < inBlock) {
               var high = ChooseDelta(samples, blockStart + index, Math.Min(lookahead, inBlock - index), level);
               level += DeltaTable[high];
               index++;
               int low = 0;
               if (index < inBlock) {
                  low = ChooseDelta(samples, blockStart + index, Math.Min(lookahead, inBlock - index), level);
                  level += DeltaTable[low];
                  index++;
               }
               output.Add((byte)((high << 4) | low));
            }
         }
         while (output.Count % 4 != 0) output.Add(0);
         return output.ToArray();
      }

      private static int ChooseDelta(sbyte[] samples, int position, int lookahead, int level) {
         Lookahead(samples, position, lookahead, level, out _, out var index);
         return index;
      }

      private static void Lookahead(sbyte[] samples, int position, int lookahead, int level, out int minimumError, out int minimumIndex) {
         minimumError = int.MaxValue;
         minimumIndex = 0;
         if (lookahead == 0 || position >= samples.Length) { minimumError = 0; return; }
         int target = samples[position];
         for (int i = 0; i < DeltaTable.Length; i++) {
            int newLevel = level + DeltaTable[i];
            int estimate = (target - newLevel) * (target - newLevel);
            if (estimate >= minimumError) continue;
            Lookahead(samples, position + 1, lookahead - 1, newLevel, out var recursiveError, out _);
            int error = estimate + recursiveError;
            if (error < minimumError && newLevel <= 127 && newLevel >= -128) {
               minimumError = error;
               minimumIndex = i;
            }
         }
         if (minimumError == int.MaxValue) {
            // every candidate overflowed: pick the one that stays closest to the valid range
            minimumError = 0;
            minimumIndex = level > 0 ? 15 : 1;
         }
      }

      /// <summary>
      /// Build the full ROM representation (header + data) for a sample.
      /// </summary>
      public static byte[] Build(sbyte[] samples, int sampleRate, bool compressed, int loopStart = -1) {
         var data = compressed ? EncodeCompressed(samples) : BuildUncompressed(samples);
         var result = new byte[HeaderLength + data.Length];
         uint flags = compressed ? 1u : 0u;
         if (loopStart >= 0) flags |= 0x40000000;
         WriteUint(result, 0, flags);
         WriteUint(result, 4, (uint)(sampleRate * 1024L));
         WriteUint(result, 8, (uint)Math.Max(0, loopStart));
         WriteUint(result, 12, (uint)samples.Length);
         Array.Copy(data, 0, result, HeaderLength, data.Length);
         return result;
      }

      private static byte[] BuildUncompressed(sbyte[] samples) {
         var data = new byte[(samples.Length + 3) / 4 * 4];
         for (int i = 0; i < samples.Length; i++) data[i] = (byte)samples[i];
         return data;
      }

      private static void WriteUint(byte[] data, int offset, uint value) {
         data[offset] = (byte)value;
         data[offset + 1] = (byte)(value >> 8);
         data[offset + 2] = (byte)(value >> 16);
         data[offset + 3] = (byte)(value >> 24);
      }

      #region WAV conversion

      /// <summary>
      /// Convert signed 8-bit PCM into a RIFF/WAVE file (8-bit unsigned PCM, mono).
      /// </summary>
      public static byte[] ToWav(sbyte[] samples, int sampleRate) {
         using var stream = new MemoryStream();
         using var writer = new BinaryWriter(stream);
         writer.Write(Encoding.ASCII.GetBytes("RIFF"));
         writer.Write(36 + samples.Length);
         writer.Write(Encoding.ASCII.GetBytes("WAVE"));
         writer.Write(Encoding.ASCII.GetBytes("fmt "));
         writer.Write(16);
         writer.Write((short)1);          // PCM
         writer.Write((short)1);          // mono
         writer.Write(sampleRate);
         writer.Write(sampleRate);        // byte rate
         writer.Write((short)1);          // block align
         writer.Write((short)8);          // bits per sample
         writer.Write(Encoding.ASCII.GetBytes("data"));
         writer.Write(samples.Length);
         for (int i = 0; i < samples.Length; i++) writer.Write((byte)(samples[i] + 128));
         writer.Flush();
         return stream.ToArray();
      }

      /// <summary>
      /// Read a RIFF/WAVE file into mono floating point samples in the range [-1, 1).
      /// Supports 8/16/24/32-bit PCM and 32-bit float, any channel count (channels are averaged).
      /// </summary>
      public static bool TryReadWav(byte[] file, out double[] samples, out int sampleRate, out string error) {
         samples = null;
         sampleRate = 0;
         error = null;
         if (file == null || file.Length < 12 || Encoding.ASCII.GetString(file, 0, 4) != "RIFF" || Encoding.ASCII.GetString(file, 8, 4) != "WAVE") {
            error = "Not a RIFF/WAVE file.";
            return false;
         }
         int format = 0, channels = 0, bits = 0;
         int dataStart = -1, dataLength = 0;
         int pos = 12;
         while (pos + 8 <= file.Length) {
            var id = Encoding.ASCII.GetString(file, pos, 4);
            long size = BitConverter.ToUInt32(file, pos + 4); // unsigned: a streaming writer (ffmpeg to a pipe) leaves 0xFFFFFFFF in the data chunk, meaning 'to the end of the file'
            int body = pos + 8;
            if (id == "fmt " && size >= 16 && body + 16 <= file.Length) {
               format = BitConverter.ToUInt16(file, body);
               channels = BitConverter.ToInt16(file, body + 2);
               sampleRate = BitConverter.ToInt32(file, body + 4);
               bits = BitConverter.ToInt16(file, body + 14);
               if (format == 0xFFFE && size >= 26 && body + 26 <= file.Length) format = BitConverter.ToUInt16(file, body + 24); // WAVE_FORMAT_EXTENSIBLE: first two bytes of the subformat GUID
            } else if (id == "data") {
               dataStart = body;
               dataLength = (int)Math.Max(0, Math.Min(size, file.Length - body));
            }
            var next = body + size + (size & 1);
            if (next > file.Length) break;
            pos = (int)next;
         }
         if (channels <= 0 || sampleRate <= 0 || dataStart < 0) { error = "The WAV file has no fmt/data chunks."; return false; }
         bool isFloat = format == 3;
         if (format != 1 && !isFloat) { error = $"Unsupported WAV format {format}: only PCM and IEEE float are supported."; return false; }
         if (bits != 8 && bits != 16 && bits != 24 && bits != 32) { error = $"Unsupported bit depth: {bits}."; return false; }
         int bytesPerSample = bits / 8;
         int frameCount = dataLength / (bytesPerSample * channels);
         samples = new double[frameCount];
         for (int frame = 0; frame < frameCount; frame++) {
            double sum = 0;
            for (int channel = 0; channel < channels; channel++) {
               int offset = dataStart + (frame * channels + channel) * bytesPerSample;
               double value;
               if (isFloat && bits == 32) value = BitConverter.ToSingle(file, offset);
               else if (bits == 8) value = (file[offset] - 128) / 128.0;
               else if (bits == 16) value = BitConverter.ToInt16(file, offset) / 32768.0;
               else if (bits == 24) value = ((file[offset] << 8 | file[offset + 1] << 16 | file[offset + 2] << 24) >> 8) / 8388608.0;
               else value = BitConverter.ToInt32(file, offset) / 2147483648.0;
               sum += value;
            }
            samples[frame] = sum / channels;
         }
         return true;
      }

      /// <summary>
      /// Linear-interpolation resampler.
      /// </summary>
      public static double[] Resample(double[] samples, int fromRate, int toRate) {
         if (fromRate == toRate || samples.Length == 0) return samples;
         int newLength = (int)Math.Max(1, Math.Round((long)samples.Length * toRate / (double)fromRate));
         var result = new double[newLength];
         double step = fromRate / (double)toRate;
         for (int i = 0; i < newLength; i++) {
            double position = i * step;
            int index = (int)position;
            double fraction = position - index;
            double a = index < samples.Length ? samples[index] : samples[^1];
            double b = index + 1 < samples.Length ? samples[index + 1] : a;
            result[i] = a + (b - a) * fraction;
         }
         return result;
      }

      /// <summary>
      /// Convert floating point samples to the signed 8-bit range the same way wav2agb does (floor(x*128), clamped).
      /// </summary>
      public static sbyte[] Quantize(double[] samples) {
         var result = new sbyte[samples.Length];
         for (int i = 0; i < samples.Length; i++) {
            result[i] = (sbyte)Math.Clamp((int)Math.Floor(samples[i] * 128.0), -128, 127);
         }
         return result;
      }

      #endregion
   }
}
