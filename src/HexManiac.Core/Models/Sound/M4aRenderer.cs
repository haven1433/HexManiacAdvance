using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Sound {
   /// <summary>
   /// A software version of the GBA's m4a ("Sappy") music engine: it runs a song's tracks the way MPlayMain does,
   /// plays DirectSound samples and the four CGB channels (square 1/2, programmable wave, noise), and writes the mix to a WAV.
   /// It's meant for previewing songs in the editor, so it favours clarity over cycle accuracy.
   /// </summary>
   public class M4aRenderer {
      public const int OutputRate = 32768;
      private const double FramesPerSecond = 59.7275;
      private const int EngineRate = 13379;           // the games' DirectSound rate: fixed-frequency samples play at this rate
      private const int MaxDirectSoundChannels = 12;
      private static readonly int[] ClockTable = MPlayDef.ClockTable;

      private readonly IDataModel model;
      private readonly Dictionary<int, GbaSample> sampleHeaders = new();
      private readonly Dictionary<int, sbyte[]> sampleData = new();

      public M4aRenderer(IDataModel model) => this.model = model;

      /// <summary>How long the rendered song may be, including one repeat of its loop.</summary>
      public double MaxSeconds { get; set; } = 150;
      /// <summary>Seconds of fade-out added once every track has looped.</summary>
      public double FadeSeconds { get; set; } = 4;

      public byte[] RenderWav(SongHeader header) {
         var (left, right) = Render(header);
         return ToWav(left, right, OutputRate);
      }

      /// <summary>
      /// Plays a single note of one instrument (the 12 byte ToneData at 'toneAddress') so it can be auditioned without a song:
      /// held for 'holdSeconds', then released, for at most 'maxSeconds'. Key splits and drum kits pick their sub-instrument from 'key', like in a song.
      /// </summary>
      public byte[] RenderPreviewNote(int toneAddress, int key = 60, int velocity = 100, double holdSeconds = 0.9, double maxSeconds = 3) {
         var (left, right) = RenderPreview(toneAddress, key, velocity, holdSeconds, maxSeconds);
         return ToWav(left, right, OutputRate);
      }

      public (float[] left, float[] right) RenderPreview(int toneAddress, int key = 60, int velocity = 100, double holdSeconds = 0.9, double maxSeconds = 3) {
         var tone = ReadTone(toneAddress);
         var player = new Player { Priority = 0 };
         for (int i = 1; i <= 4; i++) player.Cgb[i] = new Channel { CgbNumber = i };
         // a track that never runs a command: it only exists to own the note (the long wait keeps MPlayMain from reading the ROM)
         var track = new Track { Tone = tone, Wait = int.MaxValue / 2, Vol = 100, Key = key, Velocity = velocity, GateTime = (int)Math.Round(holdSeconds * FramesPerSecond) };
         track.VolChanged = track.PitChanged = true;
         TrkVolPitSet(track);
         player.Tracks.Add(track);
         StartNote(player, track);

         var left = new List<float>();
         var right = new List<float>();
         double sampleCarry = 0;
         int maxFrames = (int)(maxSeconds * FramesPerSecond);
         for (int frame = 0; frame < maxFrames; frame++) {
            MPlayMain(player);
            sampleCarry += OutputRate / FramesPerSecond;
            int samples = (int)sampleCarry;
            sampleCarry -= samples;
            var (l, r) = MixFrame(player, samples);
            for (int i = 0; i < samples; i++) { left.Add((float)l[i]); right.Add((float)r[i]); }
            if (!player.DirectSound.Any(c => c.On) && !player.Cgb.Skip(1).Any(c => c.On)) break;
         }
         var leftArray = left.ToArray();
         var rightArray = right.ToArray();
         float peak = 0;
         for (int i = 0; i < leftArray.Length; i++) { peak = Math.Max(peak, Math.Abs(leftArray[i])); peak = Math.Max(peak, Math.Abs(rightArray[i])); }
         float scale = peak > 0.95f ? 0.95f / peak : 1f;
         if (scale != 1f) {
            for (int i = 0; i < leftArray.Length; i++) { leftArray[i] *= scale; rightArray[i] *= scale; }
         }
         return (leftArray, rightArray);
      }

      #region Data structures

      private class Tone {
         public int Address;
         public int Type, Key, Length, PanSweep, Wav, Attack, Decay, Sustain, Release;
         public bool IsCgb => (Type & 7) != 0 && (Type & 0xC0) == 0;
         public int CgbChannel => Type & 7;
      }

      private class Track {
         public bool Exists = true;
         public int Pc, Wait, RunningStatus;
         public int Key, Velocity, GateTime;
         public Tone Tone;
         public int Vol, Pan, Bend, BendRange = 2, Tune, KeyShift, Priority;
         public int LfoSpeed = 22, LfoDelay, LfoDelayC, LfoSpeedC, Mod, ModT, ModM;
         public int VolX = 64;
         public int KeyM, PitM, VolML, VolMR;
         public bool VolChanged, PitChanged;
         public readonly int[] PatternStack = new int[4];
         public int PatternLevel, RepN;
         public int Loops;      // how many times a backwards GOTO has been taken
         public int Start;
         public readonly List<Channel> Channels = new();
         public int EchoVolume, EchoLength;
      }

      private enum Envelope { Release = 0, Sustain = 1, Decay = 2, Attack = 3 }

      private class Channel {
         public Track Track;
         public bool On, Starting, Stopping, Echo;
         public Envelope Phase;
         public int Type;
         public int Key, MidiKey, Priority, RhythmPan, GateTime, VelocityAtStart;
         public int Attack, Decay, Sustain, Release;
         public int EnvVolume, RightVolume, LeftVolume;
         public int EchoVolume, EchoLength;
         public double Age;
         // direct sound
         public sbyte[] Data;
         public double Position, Step, SampleRate;
         public bool Loop;
         public int LoopStart;
         // cgb
         public int CgbNumber;            // 1..4, 0 for direct sound
         public int EnvGoal, SustainGoal, EnvCounter;
         public int Length, LengthCounter;
         public int FrequencyRegister;
         public int Duty;
         public byte[] Wave;
         public int NoiseMode, Lfsr = 0x7FFF;
         public double Oscillator, NoiseCounter;
      }

      private class Player {
         public readonly List<Track> Tracks = new();
         public int TempoD = 150, TempoI = 150, TempoU = 0x100, TempoC;
         public int Voicegroup, Priority;
         public readonly byte[] MemAcc = new byte[16];
         public readonly List<Channel> DirectSound = new();
         public readonly Channel[] Cgb = new Channel[5];
         public int C15;
      }

      #endregion

      #region Reading the ROM

      private Tone ReadTone(int address) {
         if (address < 0 || address + 12 > model.Count) return null;
         return new Tone {
            Address = address,
            Type = model[address], Key = model[address + 1], Length = model[address + 2], PanSweep = model[address + 3],
            Wav = model.ReadMultiByteValue(address + 4, 4),
            Attack = model[address + 8], Decay = model[address + 9], Sustain = model[address + 10], Release = model[address + 11],
         };
      }

      private int ToRomAddress(int value) => value - BaseModel.PointerOffset;

      private (GbaSample header, sbyte[] data) ReadSample(int pointerValue) {
         var address = ToRomAddress(pointerValue);
         if (!sampleHeaders.TryGetValue(address, out var header)) {
            header = GbaSample.TryRead(model, address, out var sample) ? sample : null;
            sampleHeaders[address] = header;
            if (header != null) {
               try { sampleData[address] = header.Decode(model); } catch { sampleData[address] = null; sampleHeaders[address] = null; header = null; }
            }
         }
         return (header, header == null ? null : sampleData[address]);
      }

      private byte[] ReadWave(int pointerValue) {
         var address = ToRomAddress(pointerValue);
         if (address < 0 || address + 16 > model.Count) return null;
         var wave = new byte[16];
         for (int i = 0; i < 16; i++) wave[i] = model[address + i];
         return wave;
      }

      #endregion

      #region Sequencer

      public (float[] left, float[] right) Render(SongHeader header) {
         var player = new Player { Voicegroup = header.Voicegroup, Priority = header.Priority };
         for (int i = 0; i < header.TrackCount; i++) player.Tracks.Add(new Track { Pc = header.Tracks[i], Start = header.Tracks[i] });
         for (int i = 1; i <= 4; i++) player.Cgb[i] = new Channel { CgbNumber = i };

         var left = new List<float>();
         var right = new List<float>();
         double sampleCarry = 0;
         int maxFrames = (int)(MaxSeconds * FramesPerSecond);
         int fadeStart = -1;
         for (int frame = 0; frame < maxFrames; frame++) {
            MPlayMain(player);
            sampleCarry += OutputRate / FramesPerSecond;
            int samples = (int)sampleCarry;
            sampleCarry -= samples;
            var (l, r) = MixFrame(player, samples);
            double fade = 1;
            if (fadeStart >= 0) fade = Math.Max(0, 1 - (frame - fadeStart) / (FadeSeconds * FramesPerSecond));
            for (int i = 0; i < samples; i++) { left.Add((float)(l[i] * fade)); right.Add((float)(r[i] * fade)); }

            bool anyTrack = player.Tracks.Any(t => t.Exists);
            bool anySound = player.DirectSound.Any(c => c.On) || player.Cgb.Skip(1).Any(c => c.On);
            if (!anyTrack && !anySound) break;
            if (fadeStart < 0 && player.Tracks.Where(t => t.Exists).All(t => t.Loops > 0) && player.Tracks.Any(t => t.Exists)) fadeStart = frame;
            if (fadeStart >= 0 && fade <= 0) break;
         }

         // normalize so the loudest point sits just under full scale
         float peak = 0;
         for (int i = 0; i < left.Count; i++) { peak = Math.Max(peak, Math.Abs(left[i])); peak = Math.Max(peak, Math.Abs(right[i])); }
         float scale = peak > 0.95f ? 0.95f / peak : 1f;
         var leftArray = left.ToArray();
         var rightArray = right.ToArray();
         if (scale != 1f) {
            for (int i = 0; i < leftArray.Length; i++) { leftArray[i] *= scale; rightArray[i] *= scale; }
         }
         return (leftArray, rightArray);
      }

      private void MPlayMain(Player player) {
         player.TempoC += player.TempoI;
         while (player.TempoC >= 150) {
            player.TempoC -= 150;
            foreach (var track in player.Tracks) {
               if (!track.Exists) continue;
               // notes whose gate time ran out get released
               foreach (var chan in track.Channels.ToList()) {
                  if (!chan.On) { track.Channels.Remove(chan); continue; }
                  if (chan.GateTime > 0) {
                     chan.GateTime--;
                     if (chan.GateTime == 0) chan.Stopping = true;
                  }
               }
               if (track.Wait > 0) track.Wait--;
               int guard = 10000;
               while (track.Wait == 0 && track.Exists && guard-- > 0) RunCommand(player, track);
               // LFO
               if (track.LfoSpeed != 0 && track.Mod != 0) {
                  if (track.LfoDelayC != 0) {
                     track.LfoDelayC--;
                  } else {
                     track.LfoSpeedC = (track.LfoSpeedC + track.LfoSpeed) & 0xFF;
                     int c = track.LfoSpeedC;
                     int r = ((c - 0x40) & 0x80) != 0 ? (sbyte)c : 0x80 - c;
                     int modM = (sbyte)((track.Mod * r) >> 6);
                     if (modM != track.ModM) {
                        track.ModM = modM;
                        if (track.ModT == 0) track.PitChanged = true; else track.VolChanged = true;
                     }
                  }
               }
            }
         }
         // apply volume/pitch changes to the channels
         foreach (var track in player.Tracks) {
            if (!track.Exists) continue;
            if (!track.VolChanged && !track.PitChanged) continue;
            bool vol = track.VolChanged, pit = track.PitChanged;
            TrkVolPitSet(track);
            foreach (var chan in track.Channels) {
               if (!chan.On) continue;
               if (vol) ChnVolSet(chan, track);
               if (pit) SetFrequency(chan, track);
            }
         }
      }

      private void TrkVolPitSet(Track track) {
         if (track.VolChanged) {
            int x = (track.Vol * track.VolX) >> 5;
            if (track.ModT == 1) x = (x * (track.ModM + 128)) >> 7;
            int y = 2 * track.Pan;
            if (track.ModT == 2) y += track.ModM;
            y = Math.Clamp(y, -128, 127);
            track.VolMR = ((y + 128) * x) >> 8;
            track.VolML = ((127 - y) * x) >> 8;
         }
         if (track.PitChanged) {
            int bend = track.Bend * track.BendRange;
            int x = (track.Tune + bend) * 4 + (track.KeyShift << 8);
            if (track.ModT == 0) x += 16 * track.ModM;
            track.KeyM = x >> 8;
            track.PitM = x & 0xFF;
         }
         track.VolChanged = track.PitChanged = false;
      }

      private static void ChnVolSet(Channel chan, Track track) {
         int velocity = chan.VelocityAtStart;
         int right = ((128 + chan.RhythmPan) * velocity * track.VolMR) >> 14;
         int left = ((127 - chan.RhythmPan) * velocity * track.VolML) >> 14;
         chan.RightVolume = Math.Min(255, right);
         chan.LeftVolume = Math.Min(255, left);
         if (chan.CgbNumber != 0) CgbModVol(chan);
      }

      private int ReadArg(Track track) {
         if (track.Pc < 0 || track.Pc >= model.Count) { track.Exists = false; return 0; }
         return model[track.Pc++];
      }

      private int ReadPointerArg(Track track) {
         if (track.Pc < 0 || track.Pc + 4 > model.Count) { track.Exists = false; return -1; }
         var target = model.ReadPointer(track.Pc);
         track.Pc += 4;
         return target;
      }

      private void RunCommand(Player player, Track track) {
         if (track.Pc < 0 || track.Pc >= model.Count) { StopTrack(track); return; }
         int cmd = model[track.Pc];
         if (cmd < 0x80) {
            cmd = track.RunningStatus;
            if (cmd < 0x80) { track.Pc++; return; } // garbage before any running status: skip it
         } else {
            track.Pc++;
            if (cmd >= 0xBD) track.RunningStatus = cmd;
         }

         if (cmd >= 0xCF) { PlayNote(player, track, cmd - 0xCF); return; }
         if (cmd <= 0xB0) { track.Wait = ClockTable[cmd - 0x80]; return; }
         switch (cmd) {
            case 0xB1: // FINE
               StopTrack(track);
               break;
            case 0xB2: { // GOTO
               var target = ReadPointerArg(track);
               if (target < 0 || target >= model.Count) { StopTrack(track); break; }
               if (target <= track.Pc - 5) track.Loops++;
               track.Pc = target;
               break;
            }
            case 0xB3: { // PATT
               var target = ReadPointerArg(track);
               if (target < 0 || target >= model.Count) { StopTrack(track); break; }
               if (track.PatternLevel < 3) { track.PatternStack[track.PatternLevel++] = track.Pc; track.Pc = target; }
               break;
            }
            case 0xB4: // PEND
               if (track.PatternLevel > 0) track.Pc = track.PatternStack[--track.PatternLevel];
               break;
            case 0xB5: { // REPT
               int count = ReadArg(track);
               var target = ReadPointerArg(track);
               if (target < 0 || target >= model.Count) { StopTrack(track); break; }
               if (count == 0) { if (target <= track.Pc - 6) track.Loops++; track.Pc = target; break; }
               track.RepN++;
               if (track.RepN < count) track.Pc = target; else track.RepN = 0;
               break;
            }
            case 0xB9: { // MEMACC
               int op = ReadArg(track), index = ReadArg(track) & 15, data = ReadArg(track);
               var mem = player.MemAcc;
               bool jump = false;
               switch (op) {
                  case 0: mem[index] = (byte)data; break;
                  case 1: mem[index] += (byte)data; break;
                  case 2: mem[index] -= (byte)data; break;
                  case 3: mem[index] = mem[data & 15]; break;
                  case 4: mem[index] += mem[data & 15]; break;
                  case 5: mem[index] -= mem[data & 15]; break;
                  case 6: jump = mem[index] == data; goto condition;
                  case 7: jump = mem[index] != data; goto condition;
                  case 8: jump = mem[index] > data; goto condition;
                  case 9: jump = mem[index] >= data; goto condition;
                  case 10: jump = mem[index] <= data; goto condition;
                  case 11: jump = mem[index] < data; goto condition;
                  case 12: jump = mem[index] == mem[data & 15]; goto condition;
                  case 13: jump = mem[index] != mem[data & 15]; goto condition;
                  case 14: jump = mem[index] > mem[data & 15]; goto condition;
                  case 15: jump = mem[index] >= mem[data & 15]; goto condition;
                  case 16: jump = mem[index] <= mem[data & 15]; goto condition;
                  case 17: jump = mem[index] < mem[data & 15]; goto condition;
                  condition:
                     if (jump) {
                        var target = ReadPointerArg(track);
                        if (target >= 0 && target < model.Count) { if (target <= track.Pc - 4) track.Loops++; track.Pc = target; }
                     } else {
                        track.Pc += 4;
                     }
                     break;
               }
               break;
            }
            case 0xBA: track.Priority = ReadArg(track); break;
            case 0xBB: { // TEMPO
               player.TempoD = ReadArg(track) * 2;
               player.TempoI = (player.TempoD * player.TempoU) >> 8;
               break;
            }
            case 0xBC: track.KeyShift = (sbyte)ReadArg(track); track.PitChanged = true; break;
            case 0xBD: { // VOICE
               int voice = ReadArg(track);
               track.Tone = ReadTone(player.Voicegroup + voice * 12);
               break;
            }
            case 0xBE: track.Vol = ReadArg(track); track.VolChanged = true; break;
            case 0xBF: track.Pan = ReadArg(track) - 0x40; track.VolChanged = true; break;
            case 0xC0: track.Bend = ReadArg(track) - 0x40; track.PitChanged = true; break;
            case 0xC1: track.BendRange = ReadArg(track); track.PitChanged = true; break;
            case 0xC2: track.LfoSpeed = ReadArg(track); track.LfoSpeedC = 0; ClearModM(track); break;
            case 0xC3: track.LfoDelay = ReadArg(track); break;
            case 0xC4: track.Mod = ReadArg(track); if (track.Mod == 0) ClearModM(track); break;
            case 0xC5: { int t = ReadArg(track); if (t != track.ModT) { track.ModT = t; track.VolChanged = true; track.PitChanged = true; } break; }
            case 0xC8: track.Tune = ReadArg(track) - 0x40; track.PitChanged = true; break;
            case 0xCD: { // XCMD
               int sub = ReadArg(track), value = ReadArg(track);
               if (sub == 8) track.EchoVolume = value;
               else if (sub == 9) track.EchoLength = value;
               break;
            }
            case 0xCE: EndTie(track); break;
            case 0xB6: case 0xB7: case 0xB8: case 0xC6: case 0xC7: case 0xC9: case 0xCA: case 0xCB: case 0xCC:
               ReadArg(track); // unknown one-argument commands
               break;
            default:
               break;
         }
      }

      private static void ClearModM(Track track) {
         track.ModM = 0;
         if (track.ModT == 0) track.PitChanged = true; else track.VolChanged = true;
      }

      private void StopTrack(Track track) {
         track.Exists = false;
         foreach (var chan in track.Channels) if (chan.On) chan.Stopping = true;
      }

      private void EndTie(Track track) {
         int key = track.Pc < model.Count && model[track.Pc] < 0x80 ? model[track.Pc++] : track.Key;
         foreach (var chan in track.Channels) {
            if (chan.On && !chan.Stopping && chan.MidiKey == key) { chan.Stopping = true; break; }
         }
      }

      private void PlayNote(Player player, Track track, int clockIndex) {
         track.GateTime = clockIndex < ClockTable.Length ? ClockTable[clockIndex] : 0;
         if (track.Pc < model.Count && model[track.Pc] < 0x80) {
            track.Key = model[track.Pc++];
            if (track.Pc < model.Count && model[track.Pc] < 0x80) {
               track.Velocity = model[track.Pc++];
               if (track.Pc < model.Count && model[track.Pc] < 0x80) track.GateTime += model[track.Pc++];
            }
         }
         StartNote(player, track);
      }

      /// <summary>Starts the track's current key/velocity/gate time on a channel, using the track's current tone.</summary>
      private void StartNote(Player player, Track track) {
         var tone = track.Tone;
         if (tone == null) return;
         int key = track.Key;
         int rhythmPan = 0;
         if ((tone.Type & 0xC0) != 0) {
            int index = key;
            if ((tone.Type & 0x40) != 0) {
               var table = ToRomAddress(tone.Attack | (tone.Decay << 8) | (tone.Sustain << 16) | (tone.Release << 24));
               if (table < 0 || table + key >= model.Count) return;
               index = model[table + key];
            }
            var sub = ReadTone(ToRomAddress(tone.Wav) + index * 12);
            if (sub == null || (sub.Type & 0xC0) != 0) return;
            if ((tone.Type & 0x80) != 0) {
               if ((sub.PanSweep & 0x80) != 0) rhythmPan = (sub.PanSweep - 0xC0) * 2;
               key = sub.Key;
            }
            tone = sub;
         }
         int priority = Math.Min(255, track.Priority + player.Priority);

         Channel chan;
         if ((tone.Type & 7) != 0) {
            // CGB channel: one of each; a newer or higher-priority note takes it over
            chan = player.Cgb[tone.Type & 7];
            if (chan.On && !chan.Stopping && chan.Priority > priority) return;
            chan.Track?.Channels.Remove(chan);
         } else {
            chan = AllocateDirectSound(player, priority);
            if (chan == null) return;
         }
         if (chan.Track != null) chan.Track.Channels.Remove(chan);
         track.Channels.Add(chan);
         chan.Track = track;
         track.LfoDelayC = track.LfoDelay;
         if (track.LfoDelay != 0) ClearModM(track);
         track.VolChanged = track.PitChanged = true;
         TrkVolPitSet(track);

         chan.On = true;
         chan.Starting = true;
         chan.Stopping = false;
         chan.Echo = false;
         chan.Age = 0;
         chan.GateTime = track.GateTime;
         chan.Priority = priority;
         chan.Key = key;
         chan.MidiKey = track.Key;
         chan.RhythmPan = rhythmPan;
         chan.Type = tone.Type;
         chan.VelocityAtStart = track.Velocity;
         chan.Attack = tone.Attack; chan.Decay = tone.Decay; chan.Sustain = tone.Sustain; chan.Release = tone.Release;
         chan.EchoVolume = track.EchoVolume; chan.EchoLength = track.EchoLength;
         ChnVolSet(chan, track);

         if (chan.CgbNumber != 0) {
            chan.Length = tone.Length;
            chan.Duty = tone.Wav & 3;
            if (chan.CgbNumber == 3) chan.Wave = ReadWave(tone.Wav);
            if (chan.CgbNumber == 4) chan.NoiseMode = tone.Wav & 1;
            chan.Oscillator = 0;
            chan.Lfsr = 0x7FFF;
         } else {
            var (header, data) = ReadSample(tone.Wav);
            if (header == null || data == null || data.Length == 0) { chan.On = false; track.Channels.Remove(chan); return; }
            chan.Data = data;
            chan.SampleRate = header.SampleRate;
            chan.Loop = header.IsLooped;
            chan.LoopStart = Math.Clamp(header.LoopStart, 0, data.Length);
            chan.Position = 0;
         }
         SetFrequency(chan, track);
      }

      private Channel AllocateDirectSound(Player player, int priority) {
         var free = player.DirectSound.FirstOrDefault(c => !c.On);
         if (free != null) return free;
         if (player.DirectSound.Count < MaxDirectSoundChannels) { var chan = new Channel(); player.DirectSound.Add(chan); return chan; }
         // steal: prefer a releasing note, then the lowest priority, then the oldest
         var victim = player.DirectSound.Where(c => c.Stopping).OrderByDescending(c => c.Age).FirstOrDefault();
         if (victim == null) victim = player.DirectSound.OrderBy(c => c.Priority).ThenByDescending(c => c.Age).First();
         if (victim.Priority > priority) return null;
         victim.On = false;
         return victim;
      }

      private void SetFrequency(Channel chan, Track track) {
         int key = Math.Max(0, chan.Key + track.KeyM);
         double fine = track.PitM / 256.0;
         if (chan.CgbNumber != 0) {
            chan.FrequencyRegister = MidiKeyToCgbFreq(chan.CgbNumber, key, fine);
         } else if ((chan.Type & 0x08) != 0) {
            chan.Step = (double)EngineRate / OutputRate;
         } else {
            if (key > 178) { key = 178; fine = 1; }
            double rate = chan.SampleRate * Math.Pow(2, (key + fine - 60) / 12.0);
            chan.Step = rate / OutputRate;
         }
      }

      private static int MidiKeyToCgbFreq(int channel, int key, double fine) {
         if (channel == 4) {
            key = key <= 20 ? 0 : Math.Min(59, key - 21);
            return NoiseTable[key];
         }
         // register value: 2048 - 131072 / f, where key 69 is 440 Hz
         double f = 440 * Math.Pow(2, (key + fine - 69) / 12.0);
         int value = (int)Math.Round(2048 - 131072 / f);
         return Math.Clamp(value, 0, 2047);
      }

      private static readonly int[] NoiseTable = {
         0xD7, 0xD6, 0xD5, 0xD4, 0xC7, 0xC6, 0xC5, 0xC4, 0xB7, 0xB6, 0xB5, 0xB4, 0xA7, 0xA6, 0xA5, 0xA4,
         0x97, 0x96, 0x95, 0x94, 0x87, 0x86, 0x85, 0x84, 0x77, 0x76, 0x75, 0x74, 0x67, 0x66, 0x65, 0x64,
         0x57, 0x56, 0x55, 0x54, 0x47, 0x46, 0x45, 0x44, 0x37, 0x36, 0x35, 0x34, 0x27, 0x26, 0x25, 0x24,
         0x17, 0x16, 0x15, 0x14, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00,
      };

      #endregion

      #region Mixing

      private (double[] left, double[] right) MixFrame(Player player, int samples) {
         var left = new double[samples];
         var right = new double[samples];
         foreach (var chan in player.DirectSound) {
            if (!chan.On) continue;
            UpdateDirectSoundEnvelope(chan);
            if (!chan.On) continue;
            MixDirectSound(chan, left, right);
            chan.Age++;
         }
         // the CGB envelope steps 64 times a second: every 15th frame it runs twice
         bool twice = player.C15 == 0;
         player.C15 = player.C15 == 0 ? 14 : player.C15 - 1;
         for (int i = 1; i <= 4; i++) {
            var chan = player.Cgb[i];
            if (!chan.On) continue;
            UpdateCgbEnvelope(chan);
            if (chan.On && twice) UpdateCgbEnvelope(chan);
            if (!chan.On) continue;
            MixCgb(chan, left, right);
            chan.Age++;
         }
         return (left, right);
      }

      private static void UpdateDirectSoundEnvelope(Channel chan) {
         if (chan.Starting) {
            chan.Starting = false;
            if (chan.Stopping) { chan.On = false; return; }
            chan.Phase = Envelope.Attack;
            chan.EnvVolume = 0;
            chan.EnvVolume += chan.Attack;
            if (chan.EnvVolume >= 255) { chan.EnvVolume = 255; chan.Phase = Envelope.Decay; }
         } else if (chan.Echo) {
            chan.EchoLength--;
            if (chan.EchoLength <= 0) { chan.On = false; return; }
         } else if (chan.Stopping) {
            chan.EnvVolume = (chan.EnvVolume * chan.Release) >> 8;
            if (chan.EnvVolume <= chan.EchoVolume) {
               chan.EnvVolume = chan.EchoVolume;
               if (chan.EnvVolume == 0) { chan.On = false; return; }
               chan.Echo = true;
            }
         } else if (chan.Phase == Envelope.Decay) {
            chan.EnvVolume = (chan.EnvVolume * chan.Decay) >> 8;
            if (chan.EnvVolume <= chan.Sustain) {
               chan.EnvVolume = chan.Sustain;
               if (chan.EnvVolume == 0) {
                  chan.EnvVolume = chan.EchoVolume;
                  if (chan.EnvVolume == 0) { chan.On = false; return; }
                  chan.Echo = true;
               } else {
                  chan.Phase = Envelope.Sustain;
               }
            }
         } else if (chan.Phase == Envelope.Attack) {
            chan.EnvVolume += chan.Attack;
            if (chan.EnvVolume >= 255) { chan.EnvVolume = 255; chan.Phase = Envelope.Decay; }
         }
      }

      private static void MixDirectSound(Channel chan, double[] left, double[] right) {
         var data = chan.Data;
         double gainRight = chan.EnvVolume * chan.RightVolume / (255.0 * 256.0) / 128.0;
         double gainLeft = chan.EnvVolume * chan.LeftVolume / (255.0 * 256.0) / 128.0;
         double position = chan.Position, step = chan.Step;
         int length = data.Length;
         int loopLength = length - chan.LoopStart;
         for (int i = 0; i < left.Length; i++) {
            if (position >= length) {
               if (chan.Loop && loopLength > 0) { position -= loopLength; if (position >= length) position = chan.LoopStart; } else { chan.On = false; break; }
            }
            int index = (int)position;
            double frac = position - index;
            double a = data[index];
            double b = index + 1 < length ? data[index + 1] : (chan.Loop && loopLength > 0 ? data[chan.LoopStart] : a);
            double sample = a + (b - a) * frac;
            left[i] += sample * gainLeft;
            right[i] += sample * gainRight;
            position += step;
         }
         chan.Position = position;
      }

      private static void CgbModVol(Channel chan) {
         chan.EnvGoal = Math.Min(15, (chan.LeftVolume + chan.RightVolume) / 16);
         chan.SustainGoal = (chan.EnvGoal * chan.Sustain + 15) >> 4;
      }

      private static void UpdateCgbEnvelope(Channel chan) {
         if (chan.Starting) {
            chan.Starting = false;
            if (chan.Stopping) { chan.On = false; return; }
            chan.Phase = Envelope.Attack;
            CgbModVol(chan);
            chan.LengthCounter = chan.Length == 0 ? -1 : (chan.CgbNumber == 3 ? 256 - chan.Length : 64 - chan.Length);
            chan.EnvCounter = chan.Attack;
            if (chan.Attack != 0) { chan.EnvVolume = 0; chan.EnvCounter--; return; }
            StartCgbDecay(chan);
            return;
         }
         if (chan.Echo) {
            chan.EchoLength--;
            if (chan.EchoLength <= 0) chan.On = false;
            return;
         }
         if (chan.Stopping && chan.Phase != Envelope.Release) {
            chan.Phase = Envelope.Release;
            chan.EnvCounter = chan.Release;
            if (chan.Release == 0) { CgbEcho(chan); return; }
            chan.EnvCounter--;
            return;
         }
         if (chan.LengthCounter > 0) {
            // the hardware length counter runs at 256 Hz: 4 steps per frame
            chan.LengthCounter -= 4;
            if (chan.LengthCounter <= 0) { chan.On = false; return; }
         }
         if (chan.EnvCounter == 0) {
            CgbModVol(chan);
            switch (chan.Phase) {
               case Envelope.Release:
                  chan.EnvVolume--;
                  if (chan.EnvVolume <= 0) { CgbEcho(chan); return; }
                  chan.EnvCounter = chan.Release;
                  break;
               case Envelope.Sustain:
                  chan.EnvVolume = chan.SustainGoal;
                  chan.EnvCounter = 7;
                  break;
               case Envelope.Decay:
                  chan.EnvVolume--;
                  if (chan.EnvVolume <= chan.SustainGoal) {
                     if (chan.Sustain == 0) { CgbEcho(chan); return; }
                     chan.Phase = Envelope.Sustain;
                     chan.EnvVolume = chan.SustainGoal;
                     chan.EnvCounter = 7;
                  } else {
                     chan.EnvCounter = chan.Decay;
                  }
                  break;
               case Envelope.Attack:
                  chan.EnvVolume++;
                  if (chan.EnvVolume >= chan.EnvGoal) { StartCgbDecay(chan); return; }
                  chan.EnvCounter = chan.Attack;
                  break;
            }
         }
         chan.EnvCounter--;
      }

      private static void StartCgbDecay(Channel chan) {
         chan.Phase = Envelope.Decay;
         chan.EnvCounter = chan.Decay;
         chan.EnvVolume = chan.EnvGoal;
         if (chan.Decay == 0) {
            if (chan.Sustain == 0) { CgbEcho(chan); return; }
            chan.Phase = Envelope.Sustain;
            chan.EnvVolume = chan.SustainGoal;
            chan.EnvCounter = 7;
         }
         chan.EnvCounter--;
      }

      private static void CgbEcho(Channel chan) {
         chan.EnvVolume = ((chan.EnvGoal * chan.EchoVolume) + 0xFF) >> 8;
         if (chan.EnvVolume > 0 && chan.EchoLength > 0) { chan.Echo = true; } else { chan.On = false; }
      }

      private static readonly double[][] DutyCycles = {
         new[] { 0.125 }, new[] { 0.25 }, new[] { 0.5 }, new[] { 0.75 },
      };

      private static void MixCgb(Channel chan, double[] left, double[] right) {
         // a PSG channel at full envelope is mixed a little quieter than a full DirectSound channel
         double amplitude = chan.EnvVolume / 15.0 * 0.4;
         bool panLeft = true, panRight = true;
         if (chan.RightVolume >= chan.LeftVolume) { if (chan.RightVolume / 2 >= chan.LeftVolume) panLeft = false; } else if (chan.LeftVolume / 2 >= chan.RightVolume) panRight = false;
         double gainLeft = panLeft ? amplitude : 0, gainRight = panRight ? amplitude : 0;
         int register = chan.FrequencyRegister;
         switch (chan.CgbNumber) {
            case 1:
            case 2: {
               double frequency = 131072.0 / (2048 - register);
               double increment = frequency / OutputRate;
               double duty = DutyCycles[chan.Duty & 3][0];
               double phase = chan.Oscillator;
               for (int i = 0; i < left.Length; i++) {
                  double sample = phase < duty ? 1 : -1;
                  left[i] += sample * gainLeft;
                  right[i] += sample * gainRight;
                  phase += increment;
                  if (phase >= 1) phase -= Math.Floor(phase);
               }
               chan.Oscillator = phase;
               break;
            }
            case 3: {
               if (chan.Wave == null) break;
               double frequency = 2097152.0 / (2048 - register); // samples per second through the 32-entry wave
               double increment = frequency / OutputRate;
               double phase = chan.Oscillator;
               for (int i = 0; i < left.Length; i++) {
                  int index = (int)phase & 31;
                  int nibble = (index & 1) == 0 ? chan.Wave[index >> 1] >> 4 : chan.Wave[index >> 1] & 15;
                  double sample = (nibble - 7.5) / 7.5;
                  left[i] += sample * gainLeft;
                  right[i] += sample * gainRight;
                  phase += increment;
                  if (phase >= 32) phase -= 32 * Math.Floor(phase / 32);
               }
               chan.Oscillator = phase;
               break;
            }
            case 4: {
               int shift = (register >> 4) & 15, divisor = register & 7;
               double r = divisor == 0 ? 0.5 : divisor;
               double frequency = 524288.0 / r / Math.Pow(2, shift + 1);
               double increment = frequency / OutputRate;
               bool narrow = chan.NoiseMode == 1;
               double counter = chan.NoiseCounter;
               int lfsr = chan.Lfsr;
               for (int i = 0; i < left.Length; i++) {
                  counter += increment;
                  while (counter >= 1) {
                     counter -= 1;
                     int bit = (lfsr ^ (lfsr >> 1)) & 1;
                     lfsr = (lfsr >> 1) | (bit << 14);
                     if (narrow) lfsr = (lfsr & ~0x40) | (bit << 6);
                  }
                  double sample = (lfsr & 1) == 0 ? 1 : -1;
                  left[i] += sample * gainLeft;
                  right[i] += sample * gainRight;
               }
               chan.NoiseCounter = counter;
               chan.Lfsr = lfsr;
               break;
            }
         }
      }

      #endregion

      public static byte[] ToWav(float[] left, float[] right, int sampleRate) {
         int frames = Math.Min(left.Length, right.Length);
         int dataLength = frames * 4;
         var wav = new byte[44 + dataLength];
         void Write(int offset, string text) { for (int i = 0; i < text.Length; i++) wav[offset + i] = (byte)text[i]; }
         void Write32(int offset, int value) { wav[offset] = (byte)value; wav[offset + 1] = (byte)(value >> 8); wav[offset + 2] = (byte)(value >> 16); wav[offset + 3] = (byte)(value >> 24); }
         void Write16(int offset, int value) { wav[offset] = (byte)value; wav[offset + 1] = (byte)(value >> 8); }
         Write(0, "RIFF"); Write32(4, 36 + dataLength); Write(8, "WAVE");
         Write(12, "fmt "); Write32(16, 16); Write16(20, 1); Write16(22, 2); Write32(24, sampleRate); Write32(28, sampleRate * 4); Write16(32, 4); Write16(34, 16);
         Write(36, "data"); Write32(40, dataLength);
         int o = 44;
         for (int i = 0; i < frames; i++) {
            short l = (short)Math.Clamp(Math.Round(left[i] * 32767), -32768, 32767);
            short r = (short)Math.Clamp(Math.Round(right[i] * 32767), -32768, 32767);
            wav[o++] = (byte)l; wav[o++] = (byte)(l >> 8);
            wav[o++] = (byte)r; wav[o++] = (byte)(r >> 8);
         }
         return wav;
      }
   }
}
