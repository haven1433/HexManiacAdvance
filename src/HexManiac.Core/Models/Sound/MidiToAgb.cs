using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Sound {
   /// <summary>Thrown when a MIDI file cannot be converted. The message is meant to be shown to the user as it is.</summary>
   public class MidiConversionException : Exception {
      public MidiConversionException(string message) : base(message) { }
   }

   /// <summary>
   /// The options of mid2agb (the pokeemerald tool): -L label, -G voicegroup label, -V master volume, -P priority, -R reverb, -E exact gate time, -N no compression, -X 48 clocks/beat.
   /// </summary>
   public class MidiToAgbOptions {
      /// <summary>-L: the label of the song. The song header is called this, its tracks are label_1, label_2 and so on.</summary>
      public string Label { get; set; } = "song";
      /// <summary>-G: what follows "voicegroup" in the symbol the song uses for its voicegroup. "_dummy" gives the symbol voicegroup_dummy; "000" gives voicegroup000.</summary>
      public string VoiceGroup { get; set; } = "_dummy";
      /// <summary>-V: the master volume that every volume change of the song is scaled by (0-127).</summary>
      public int MasterVolume { get; set; } = 127;
      /// <summary>-P: the priority of the song.</summary>
      public int Priority { get; set; } = 0;
      /// <summary>-R: the reverb amount (0-127), or a negative number to leave reverb off.</summary>
      public int Reverb { get; set; } = -1;
      /// <summary>-X: use 48 clocks per beat instead of 24.</summary>
      public bool DoubleClocks { get; set; }
      /// <summary>-E: keep the exact length of notes (with a gate time parameter) instead of rounding them to the lengths the engine has a command for.</summary>
      public bool ExactGateTime { get; set; }
      /// <summary>Repeated bars become patterns (PATT/PEND). -N turns this off.</summary>
      public bool Compression { get; set; } = true;
      /// <summary>
      /// Not an option of mid2agb. A channel that plays notes without ever choosing a program plays program 0 in MIDI, but in the game a track that has not chosen
      /// an instrument starts on an empty square wave and is silent. When this is on, such a channel gets a "VOICE 0" at its start. Off, the output is mid2agb's.
      /// </summary>
      public bool AddMissingVoice { get; set; }

      public MidiToAgbOptions Clone() => (MidiToAgbOptions)MemberwiseClone();

      /// <summary>
      /// Reads options the way the mid2agb command line does: "-E -R50 -G_all_instruments -V080". A value may follow the letter directly or be the next word.
      /// Words that do not start with '-' are not options and are ignored.
      /// </summary>
      public static MidiToAgbOptions Parse(string arguments, MidiToAgbOptions start = null) {
         var options = start?.Clone() ?? new MidiToAgbOptions();
         var words = (arguments ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
         for (int i = 0; i < words.Length; i++) {
            var word = words[i];
            if (word.Length == 0 || word[0] != '-' || word.Length < 2) continue;
            string Argument() {
               if (word.Length >= 3) return word.Substring(2);
               if (i + 1 < words.Length) return words[++i];
               throw new ArgumentException($"The option {word} needs a value.");
            }
            int Number() {
               var text = Argument();
               var end = 0;
               if (end < text.Length && (text[end] == '-' || text[end] == '+')) end++;
               while (end < text.Length && char.IsDigit(text[end])) end++;
               if (end == 0 || !int.TryParse(text.Substring(0, end), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) throw new ArgumentException($"The option {word} needs a number, not '{text}'.");
               return value;
            }
            switch (char.ToUpperInvariant(word[1])) {
               case 'E': options.ExactGateTime = true; break;
               case 'G': options.VoiceGroup = Argument(); break;
               case 'L': options.Label = Argument(); break;
               case 'N': options.Compression = false; break;
               case 'P': options.Priority = Number(); break;
               case 'R': options.Reverb = Number(); break;
               case 'V': options.MasterVolume = Number(); break;
               case 'X': options.DoubleClocks = true; break;
               default: throw new ArgumentException($"Unknown option {word}.");
            }
         }
         return options;
      }
   }

   /// <summary>The song text of a conversion, and what was found in the MIDI file.</summary>
   public class MidiConversionResult {
      /// <summary>The song as mid2agb writes it: a .s file for the assembler.</summary>
      public string Text { get; init; }
      /// <summary>How many tracks the song has: one for every MIDI channel that plays a note.</summary>
      public int TrackCount { get; init; }
      /// <summary>The MIDI file's own format (0 or 1) and track count.</summary>
      public int MidiFormat { get; init; }
      public int MidiTrackCount { get; init; }
      /// <summary>The (MIDI track, MIDI channel) that each of the song's tracks was made from; channels are 1-16.</summary>
      public IReadOnlyList<(int midiTrack, int channel)> Channels { get; init; }
   }

   /// <summary>
   /// A port of mid2agb (pokeemerald/tools/mid2agb, by YamaArashi), the tool that turns a Standard MIDI File into a song for the GBA's m4a sound engine:
   /// the .s file that the Sound tab's insert command takes. It follows the C++ code step by step (its quirks included), so that the text it
   /// produces is the same, byte for byte, as the text of the real tool for the same file and options.
   /// </summary>
   public sealed class MidiToAgb {
      #region Tables (tables.cpp)

      private static readonly int[] DurationLut = {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            16, 17, 18, 19, 20, 21, 22, 23, 24, 24, 24, 24, 28, 28, 30, 30,
            32, 32, 32, 32, 36, 36, 36, 36, 40, 40, 42, 42, 44, 44, 44, 44,
            48, 48, 48, 48, 52, 52, 54, 54, 56, 56, 56, 56, 60, 60, 60, 60,
            64, 64, 66, 66, 68, 68, 68, 68, 72, 72, 72, 72, 76, 76, 78, 78,
            80, 80, 80, 80, 84, 84, 84, 84, 88, 88, 90, 90, 92, 92, 92, 92,
            96
      };

      private static readonly int[] VelocityLut = {
            0, 4, 4, 4, 4, 8, 8, 8, 8, 12, 12, 12, 12, 16, 16, 16,
            16, 20, 20, 20, 20, 24, 24, 24, 24, 28, 28, 28, 28, 32, 32, 32,
            32, 36, 36, 36, 36, 40, 40, 40, 40, 44, 44, 44, 44, 48, 48, 48,
            48, 52, 52, 52, 52, 56, 56, 56, 56, 60, 60, 60, 60, 64, 64, 64,
            64, 68, 68, 68, 68, 72, 72, 72, 72, 76, 76, 76, 76, 80, 80, 80,
            80, 84, 84, 84, 84, 88, 88, 88, 88, 92, 92, 92, 92, 96, 96, 96,
            96, 100, 100, 100, 100, 104, 104, 104, 104, 108, 108, 108, 108, 112, 112, 112,
            112, 116, 116, 116, 116, 120, 120, 120, 120, 124, 124, 124, 124, 127, 127, 127
      };

      private static readonly string[] NoteTable = { "Cn", "Cs", "Dn", "Ds", "En", "Fn", "Fs", "Gn", "Gs", "An", "As", "Bn" };

      /// <summary>
      /// g_noteDurationLUT[index]. The C++ array is read at index -1 for the notes that become ties, and that reads the last entry of the table in front of it
      /// (g_noteVelocityLUT[127]): the compiled tool depends on it, so this does as well.
      /// </summary>
      private static int Duration(int index) {
         if (index == -1) return VelocityLut[127];
         if (index < 0 || index >= DurationLut.Length) throw new MidiConversionException("The MIDI file has a note with a length the GBA song format cannot store.");
         return DurationLut[index];
      }

      /// <summary>g_noteVelocityLUT[index]. For an index past the end the C++ array reads the duration table that follows it.</summary>
      private static int Velocity(int index) {
         if (index >= 0 && index < VelocityLut.Length) return VelocityLut[index];
         var next = index - VelocityLut.Length;
         if (index >= 0 && next < DurationLut.Length) return DurationLut[next];
         throw new MidiConversionException("The MIDI file has a note with a velocity above 127, which is not valid MIDI.");
      }

      #endregion

      #region Types

      private enum EventType {
         EndOfTie = 0x01,
         Label = 0x11,
         LoopEnd = 0x12,
         LoopEndBegin = 0x13,
         LoopBegin = 0x14,
         OriginalTimeSignature = 0x15,
         WholeNoteMark = 0x16,
         Pattern = 0x17,
         TimeSignature = 0x18,
         Tempo = 0x19,
         InstrumentChange = 0x21,
         Controller = 0x22,
         PitchBend = 0x23,
         KeyShift = 0x31,
         Note = 0x40,
         TimeSplit = 0xFE,
         EndOfTrack = 0xFF,
      }

      private enum Category { Control, SysEx, Meta, Invalid }

      private struct Event {
         public int Time;
         public EventType Type;
         public byte Note;
         public byte Param1;
         public int Param2;

         public bool SameAs(Event other) => Time == other.Time && Type == other.Type && Note == other.Note && Param1 == other.Param1 && Param2 == other.Param2;
      }

      private static bool IsPatternBoundary(EventType type) => type == EventType.EndOfTrack || (int)type <= 0x17;

      #endregion

      #region Limits

      /// <summary>No real song comes near this (it is 100000 whole notes, a day and a half at 120 beats per minute): a MIDI file with a later time stamp is broken, and converting it would take forever.</summary>
      private const int MaxSongClocks = 96 * 100000;
      /// <summary>The most events a note search may look at in all (a MIDI file whose notes never end would otherwise take minutes).</summary>
      private const long MaxScannedEvents = 100_000_000;
      private long scannedEvents;

      #endregion

      private readonly byte[] data;
      private readonly MidiToAgbOptions options;
      private readonly StringBuilder output = new();
      private readonly int clocksPerBeat;
      private readonly string label;

      // midi.cpp
      private int midiFormat;
      private int midiTrackCount;
      private short midiTimeDiv;
      private int midiChan;
      private int initialWait;
      private long position;
      private long trackDataStart;
      private readonly List<Event> seqEvents = new();
      private List<Event> trackEvents = new();
      private int absoluteTime;
      private int blockCount;
      private int minNote;
      private int maxNote;
      private int runningStatus;

      // agb.cpp
      private int agbTrack;
      private string lastOpName = string.Empty;
      private int blockNum;
      private bool keepLastOpName;
      private int lastNote;
      private int lastVelocity;
      private bool noteChanged;
      private bool velocityChanged;
      private bool inPattern;
      private int extendedCommand;
      private int memaccOp;
      private int memaccParam1;
      private int memaccParam2;

      private MidiToAgb(byte[] data, MidiToAgbOptions options) {
         this.data = data;
         this.options = options;
         clocksPerBeat = options.DoubleClocks ? 2 : 1;
         label = options.Label;
      }

      /// <summary>Converts the bytes of a .mid file. Throws a MidiConversionException with a readable message when the file can't be converted.</summary>
      public static MidiConversionResult Convert(byte[] midi, MidiToAgbOptions options = null) {
         if (midi == null) throw new MidiConversionException("There is no MIDI data to convert.");
         options ??= new MidiToAgbOptions();
         if (string.IsNullOrEmpty(options.Label)) throw new MidiConversionException("The song needs a label.");
         var converter = new MidiToAgb(midi, options);
         try {
            return converter.Run();
         } catch (MidiConversionException) {
            throw;
         } catch (Exception e) when (e is IndexOutOfRangeException || e is ArgumentOutOfRangeException || e is InvalidOperationException || e is OverflowException || e is OutOfMemoryException || e is NullReferenceException) {
            // the C++ tool reads memory it shouldn't, or crashes, on files like this one
            throw new MidiConversionException("The MIDI file has data this converter cannot make sense of (" + e.GetType().Name + ").");
         }
      }

      /// <summary>The label mid2agb would derive from an output file name, made safe for the assembler: letters, digits and underscores only, not starting with a digit.</summary>
      public static string LabelFromFileName(string fileName) {
         var name = fileName ?? string.Empty;
         var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
         if (slash >= 0) name = name.Substring(slash + 1);
         var dot = name.LastIndexOf('.');
         if (dot >= 0) name = name.Substring(0, dot);
         var sb = new StringBuilder();
         foreach (var c in name) {
            var keep = c < 128 && (char.IsLetterOrDigit(c) || c == '_');
            if (keep) sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
         }
         var label = sb.ToString().Trim('_');
         if (label.Length == 0) label = "song";
         if (char.IsDigit(label[0])) label = "_" + label;
         return label;
      }

      private MidiConversionResult Run() {
         ReadMidiFileHeader();
         PrintAgbHeader();
         var channels = ReadMidiTracks();
         PrintAgbFooter();
         return new MidiConversionResult {
            Text = output.ToString(),
            TrackCount = agbTrack - 1,
            MidiFormat = midiFormat,
            MidiTrackCount = midiTrackCount,
            Channels = channels,
         };
      }

      #region Reading the file (midi.cpp)

      private void Seek(long offset) => position = offset;

      private void Skip(long offset) => position += offset;

      private string ReadSignature() {
         if (position < 0 || position + 4 > data.Length) throw new MidiConversionException("The file ends in the middle of a MIDI chunk header (is it cut off?).");
         var text = new string(new[] { (char)data[position], (char)data[position + 1], (char)data[position + 2], (char)data[position + 3] });
         position += 4;
         return text;
      }

      private uint ReadInt8() {
         if (position < 0 || position >= data.Length) throw new MidiConversionException("The MIDI file ends too early (is it cut off, or missing its end-of-track marker?).");
         return data[position++];
      }

      private uint ReadInt16() {
         uint value = 0;
         value |= ReadInt8() << 8;
         value |= ReadInt8();
         return value;
      }

      private uint ReadInt24() {
         uint value = 0;
         value |= ReadInt8() << 16;
         value |= ReadInt8() << 8;
         value |= ReadInt8();
         return value;
      }

      private uint ReadInt32() {
         uint value = 0;
         value |= ReadInt8() << 24;
         value |= ReadInt8() << 16;
         value |= ReadInt8() << 8;
         value |= ReadInt8();
         return value;
      }

      private uint ReadVLQ() {
         uint value = 0;
         uint c;
         do {
            c = ReadInt8();
            value <<= 7;
            value |= c & 0x7F;
         } while ((c & 0x80) != 0);
         return value;
      }

      private void ReadMidiFileHeader() {
         Seek(0);
         if (ReadSignature() != "MThd") throw new MidiConversionException("This is not a MIDI file: it does not start with the 'MThd' header.");
         var headerLength = ReadInt32();
         if (headerLength != 6) throw new MidiConversionException("The MIDI file header has an unexpected length (" + headerLength + " instead of 6).");
         var format = ReadInt16();
         if (format >= 2) throw new MidiConversionException($"MIDI format {format} is not supported. Save the file as a format 0 or format 1 MIDI (a sequencer can export it that way).");
         midiFormat = (int)format;
         midiTrackCount = (int)ReadInt16();
         midiTimeDiv = unchecked((short)ReadInt16());
         if (midiTimeDiv < 0) throw new MidiConversionException("This MIDI file measures time in SMPTE frames, which is not supported. Re-export it with a time division in ticks per beat.");
      }

      private long ReadMidiTrackHeader(long offset) {
         Seek(offset);
         if (ReadSignature() != "MTrk") throw new MidiConversionException("A MIDI track does not start with 'MTrk': the file is damaged, or has fewer tracks than its header says.");
         long size = ReadInt32();
         trackDataStart = position;
         return size + 8;
      }

      private void StartTrack() {
         Seek(trackDataStart);
         absoluteTime = 0;
         runningStatus = 0;
      }

      private void SkipEventData() => Skip(ReadVLQ());

      private void DetermineEventCategory(out Category category, out int typeChan, out int size) {
         typeChan = (int)ReadInt8();
         if (typeChan < 0x80) {
            // if a data byte was found, use the running status
            position--;
            typeChan = runningStatus;
         }
         if (typeChan == 0xFF) {
            category = Category.Meta;
            size = 0;
            runningStatus = 0;
         } else if (typeChan >= 0xF0) {
            category = Category.SysEx;
            size = 0;
            runningStatus = 0;
         } else if (typeChan >= 0x80) {
            category = Category.Control;
            switch (typeChan >> 4) {
               case 0xC:
               case 0xD:
                  size = 1;
                  break;
               default:
                  size = 2;
                  break;
            }
            runningStatus = typeChan;
         } else {
            category = Category.Invalid;
            size = 0;
         }
      }

      private void MakeBlockEvent(ref Event e, EventType type) {
         e.Type = type;
         e.Param1 = unchecked((byte)blockCount++);
         e.Param2 = 0;
      }

      private string ReadEventText() {
         var length = ReadVLQ();
         if (length <= 2) {
            // the C++ tool reads this with fread(buffer, length, 1, file), which fails for a length of 0 as well
            if (length == 0 || position + length > data.Length) throw new MidiConversionException("A text event in the MIDI file is empty or cut off.");
            var text = length == 1 ? ((char)data[position]).ToString() : new string(new[] { (char)data[position], (char)data[position + 1] });
            position += length;
            return text;
         }
         Skip(length);
         return string.Empty;
      }

      private static void InvalidEvent() => throw new MidiConversionException("The MIDI file has an event the converter does not understand (a data byte where a status byte should be).");

      private bool ReadSeqEvent(ref Event e) {
         absoluteTime = unchecked(absoluteTime + (int)ReadVLQ());
         e.Time = absoluteTime;

         DetermineEventCategory(out var category, out _, out var size);

         if (category == Category.Control) {
            Skip(size);
            return false;
         }
         if (category == Category.SysEx) {
            SkipEventData();
            return false;
         }
         if (category == Category.Invalid) InvalidEvent();

         // meta event
         var metaEventType = (int)ReadInt8();
         if (metaEventType >= 1 && metaEventType <= 7) {
            // text event
            var text = ReadEventText();
            if (text == "[") MakeBlockEvent(ref e, EventType.LoopBegin);
            else if (text == "][") MakeBlockEvent(ref e, EventType.LoopEndBegin);
            else if (text == "]") MakeBlockEvent(ref e, EventType.LoopEnd);
            else if (text == ":") MakeBlockEvent(ref e, EventType.Label);
            else return false;
         } else {
            switch (metaEventType) {
               case 0x2F: // end of track
                  SkipEventData();
                  e.Type = EventType.EndOfTrack;
                  e.Param1 = 0;
                  e.Param2 = 0;
                  break;
               case 0x51: // tempo
                  if (ReadVLQ() != 3) throw new MidiConversionException("A tempo event in the MIDI file has an invalid size.");
                  e.Type = EventType.Tempo;
                  e.Param1 = 0;
                  e.Param2 = (int)ReadInt24();
                  break;
               case 0x58: { // time signature
                  if (ReadVLQ() != 4) throw new MidiConversionException("A time signature event in the MIDI file has an invalid size.");
                  var numerator = (int)ReadInt8();
                  var denominatorExponent = (int)ReadInt8();
                  if (denominatorExponent >= 16) throw new MidiConversionException("A time signature in the MIDI file has an invalid denominator.");
                  Skip(2); // ignore other values
                  var clockTicks = 96 * numerator * clocksPerBeat;
                  var denominator = 1 << denominatorExponent;
                  var timeSig = clockTicks / denominator;
                  if (timeSig <= 0 || timeSig >= 0x10000) throw new MidiConversionException("A time signature in the MIDI file is not valid (numerator " + numerator + ", denominator " + denominator + ").");
                  e.Type = EventType.TimeSignature;
                  e.Param1 = 0;
                  e.Param2 = timeSig;
                  break;
               }
               default:
                  SkipEventData();
                  return false;
            }
         }
         return true;
      }

      private void ReadSeqEvents() {
         StartTrack();
         for (; ; ) {
            var e = new Event();
            if (ReadSeqEvent(ref e)) {
               seqEvents.Add(e);
               if (e.Type == EventType.EndOfTrack) return;
            }
         }
      }

      private bool CheckNoteEnd(ref Event e) {
         if (++scannedEvents > MaxScannedEvents) throw new MidiConversionException("The MIDI file is too complex to convert (its notes have no ends).");
         e.Param2 = unchecked(e.Param2 + (int)ReadVLQ());

         DetermineEventCategory(out var category, out var typeChan, out var size);

         if (category == Category.Control) {
            var chan = typeChan & 0xF;
            if (chan != midiChan) {
               Skip(size);
               return false;
            }
            switch (typeChan & 0xF0) {
               case 0x80: { // note off
                  var note = (int)ReadInt8();
                  ReadInt8(); // ignore velocity
                  if (note == e.Note) return true;
                  break;
               }
               case 0x90: { // note on
                  var note = (int)ReadInt8();
                  var velocity = (int)ReadInt8();
                  if (velocity == 0 && note == e.Note) return true;
                  break;
               }
               default:
                  Skip(size);
                  break;
            }
            return false;
         }
         if (category == Category.SysEx) {
            SkipEventData();
            return false;
         }
         if (category == Category.Meta) {
            var metaEventType = (int)ReadInt8();
            SkipEventData();
            if (metaEventType == 0x2F) throw new MidiConversionException("A note in the MIDI file never ends (a note-on without a note-off before the end of the track).");
            return false;
         }
         InvalidEvent();
         return false;
      }

      private void FindNoteEnd(ref Event e) {
         // the file position and running status get modified by CheckNoteEnd: save them
         var startPosition = position;
         var savedRunningStatus = runningStatus;
         e.Param2 = 0;
         while (!CheckNoteEnd(ref e)) { }
         Seek(startPosition);
         runningStatus = savedRunningStatus;
      }

      private bool ReadTrackEvent(ref Event e) {
         absoluteTime = unchecked(absoluteTime + (int)ReadVLQ());
         e.Time = absoluteTime;

         DetermineEventCategory(out var category, out var typeChan, out var size);

         if (category == Category.Control) {
            var chan = typeChan & 0xF;
            if (chan != midiChan) {
               Skip(size);
               return false;
            }
            switch (typeChan & 0xF0) {
               case 0x90: { // note on
                  var note = (int)ReadInt8();
                  var velocity = (int)ReadInt8();
                  if (velocity != 0) {
                     e.Type = EventType.Note;
                     e.Note = (byte)note;
                     e.Param1 = (byte)velocity;
                     FindNoteEnd(ref e);
                     if (e.Param2 > 0) {
                        if (note < minNote) minNote = note;
                        if (note > maxNote) maxNote = note;
                     }
                  }
                  // a note-on with velocity 0 falls out of the switch like the others: mid2agb keeps it as an event of type 0 (it only splits waits)
                  break;
               }
               case 0xB0: // controller event
                  e.Type = EventType.Controller;
                  e.Param1 = (byte)ReadInt8(); // controller index
                  e.Param2 = (int)ReadInt8(); // value
                  break;
               case 0xC0: // instrument change
                  e.Type = EventType.InstrumentChange;
                  e.Param1 = (byte)ReadInt8(); // instrument
                  e.Param2 = 0;
                  break;
               case 0xE0: // pitch bend
                  e.Type = EventType.PitchBend;
                  e.Param1 = (byte)ReadInt8();
                  e.Param2 = (int)ReadInt8();
                  break;
               default:
                  Skip(size);
                  return false;
            }
            return true;
         }
         if (category == Category.SysEx) {
            SkipEventData();
            return false;
         }
         if (category == Category.Meta) {
            var metaEventType = (int)ReadInt8();
            SkipEventData();
            if (metaEventType == 0x2F) {
               e.Type = EventType.EndOfTrack;
               e.Param1 = 0;
               e.Param2 = 0;
               return true;
            }
            return false;
         }
         InvalidEvent();
         return false;
      }

      private void ReadTrackEvents() {
         StartTrack();
         trackEvents = new List<Event>();
         minNote = 0xFF;
         maxNote = 0;
         for (; ; ) {
            var e = new Event();
            if (ReadTrackEvent(ref e)) {
               trackEvents.Add(e);
               if (e.Type == EventType.EndOfTrack) return;
            }
         }
      }

      #endregion

      #region Turning events into a track (midi.cpp)

      private static bool EventCompare(Event event1, Event event2) {
         if (event1.Time < event2.Time) return true;
         if (event1.Time > event2.Time) return false;

         uint event1Type = (uint)event1.Type;
         uint event2Type = (uint)event2.Type;
         if (event1.Type == EventType.Note) event1Type += event1.Note;
         if (event2.Type == EventType.Note) event2Type += event2.Note;

         if (event1Type < event2Type) return true;
         if (event1Type > event2Type) return false;

         if (event1.Type == EventType.EndOfTie) {
            if (event1.Note < event2.Note) return true;
            if (event1.Note > event2.Note) return false;
         }
         return false;
      }

      private List<Event> MergeEvents() {
         var events = new List<Event>();
         int trackEventPos = 0;
         int seqEventPos = 0;

         while (trackEvents[trackEventPos].Type != EventType.EndOfTrack && seqEvents[seqEventPos].Type != EventType.EndOfTrack) {
            if (EventCompare(trackEvents[trackEventPos], seqEvents[seqEventPos])) events.Add(trackEvents[trackEventPos++]);
            else events.Add(seqEvents[seqEventPos++]);
         }
         while (trackEvents[trackEventPos].Type != EventType.EndOfTrack) events.Add(trackEvents[trackEventPos++]);
         while (seqEvents[seqEventPos].Type != EventType.EndOfTrack) events.Add(seqEvents[seqEventPos++]);

         // push the EndOfTrack event with the larger time
         if (EventCompare(trackEvents[trackEventPos], seqEvents[seqEventPos])) events.Add(seqEvents[seqEventPos]);
         else events.Add(trackEvents[trackEventPos]);
         return events;
      }

      private void ConvertTimes(List<Event> events) {
         if (midiTimeDiv == 0) throw new MidiConversionException("The MIDI file's time division is 0, so its notes have no timing.");
         for (int i = 0; i < events.Count; i++) {
            var e = events[i];
            unchecked {
               e.Time = (24 * clocksPerBeat * e.Time) / midiTimeDiv;
               if (e.Time > MaxSongClocks || e.Time < 0) throw new MidiConversionException("The MIDI file is far too long to be a GBA song (its events are spread over more than 100000 whole notes).");

               if (e.Type == EventType.Note) {
                  e.Param1 = (byte)Velocity(e.Param1);
                  uint duration = (uint)((24 * clocksPerBeat * e.Param2) / midiTimeDiv);
                  if (duration == 0) duration = 1;
                  if (!options.ExactGateTime && duration < 96) duration = (uint)Duration((int)duration);
                  e.Param2 = (int)duration;
               }
            }
            events[i] = e;
         }
      }

      private List<Event> InsertTimingEvents(List<Event> inEvents) {
         var outEvents = new List<Event>();
         var timingEvent = new Event { Time = 0, Type = EventType.TimeSignature, Param2 = 96 * clocksPerBeat };

         foreach (var e in inEvents) {
            while (EventCompare(timingEvent, e)) {
               outEvents.Add(timingEvent);
               timingEvent.Time += timingEvent.Param2;
            }
            if (e.Type == EventType.TimeSignature) {
               if (agbTrack == 1 && e.Param2 != timingEvent.Param2) {
                  var originalTimingEvent = e;
                  originalTimingEvent.Type = EventType.OriginalTimeSignature;
                  outEvents.Add(originalTimingEvent);
               }
               timingEvent.Param2 = e.Param2;
               timingEvent.Time = e.Time + timingEvent.Param2;
            }
            outEvents.Add(e);
         }
         return outEvents;
      }

      private static List<Event> SplitTime(List<Event> inEvents) {
         var outEvents = new List<Event>();
         int time = 0;
         foreach (var e in inEvents) {
            int diff = e.Time - time;
            if (diff > 96) {
               int wholeNoteCount = (diff - 1) / 96;
               diff -= 96 * wholeNoteCount;
               for (int i = 0; i < wholeNoteCount; i++) {
                  time += 96;
                  outEvents.Add(new Event { Time = time, Type = EventType.TimeSplit });
               }
            }
            int lutValue = Duration(diff);
            if (lutValue != diff) outEvents.Add(new Event { Time = time + lutValue, Type = EventType.TimeSplit });
            time = e.Time;
            outEvents.Add(e);
         }
         return outEvents;
      }

      private static List<Event> CreateTies(List<Event> inEvents) {
         var outEvents = new List<Event>();
         foreach (var e in inEvents) {
            if (e.Type == EventType.Note && e.Param2 > 96) {
               var tieEvent = e;
               tieEvent.Param2 = -1;
               outEvents.Add(tieEvent);
               outEvents.Add(new Event { Time = e.Time + e.Param2, Type = EventType.EndOfTie, Note = e.Note });
            } else {
               outEvents.Add(e);
            }
         }
         return outEvents;
      }

      /// <summary>std::stable_sort(events, EventCompare): insertion by merge, so that equal events keep their order.</summary>
      private static List<Event> StableSort(List<Event> events) {
         if (events.Count < 2) return events;
         var buffer = events.ToArray();
         var scratch = new Event[buffer.Length];
         MergeSort(buffer, scratch, 0, buffer.Length);
         return new List<Event>(buffer);
      }

      private static void MergeSort(Event[] items, Event[] scratch, int start, int end) {
         if (end - start < 2) return;
         int middle = start + (end - start) / 2;
         MergeSort(items, scratch, start, middle);
         MergeSort(items, scratch, middle, end);
         int left = start, right = middle, index = start;
         while (left < middle && right < end) {
            // take from the right only when it is strictly less: equal elements keep their order
            scratch[index++] = EventCompare(items[right], items[left]) ? items[right++] : items[left++];
         }
         while (left < middle) scratch[index++] = items[left++];
         while (right < end) scratch[index++] = items[right++];
         Array.Copy(scratch, start, items, start, end - start);
      }

      private void CalculateWaits(List<Event> events) {
         initialWait = events[0].Time;
         int wholeNoteCount = 0;
         for (int i = 0; i < events.Count && events[i].Type != EventType.EndOfTrack; i++) {
            var e = events[i];
            e.Time = events[i + 1].Time - e.Time;
            if (e.Type == EventType.TimeSignature) {
               e.Type = EventType.WholeNoteMark;
               e.Param2 = wholeNoteCount++;
            }
            events[i] = e;
         }
      }

      private static int CalculateCompressionScore(List<Event> events, int index) {
         int score = 0;
         byte lastParam1 = events[index].Param1;
         byte lastVelocity = 0x80;
         EventType lastType = events[index].Type;
         int lastDuration = int.MinValue;
         byte lastNote = 0x40;

         if (events[index].Time > 0) score++;

         for (int i = index + 1; !IsPatternBoundary(events[i].Type); i++) {
            if (events[i].Type == EventType.Note) {
               int val = 0;
               if (events[i].Note != lastNote) {
                  val++;
                  lastNote = events[i].Note;
               }
               if (events[i].Param1 != lastVelocity) {
                  val++;
                  lastVelocity = events[i].Param1;
               }
               int duration = events[i].Param2;
               if (Duration(duration) != lastDuration) {
                  val++;
                  lastDuration = Duration(duration);
               }
               if (duration != lastDuration) val++;
               if (val == 0) val = 1;
               score += val;
            } else {
               lastDuration = int.MinValue;
               if (events[i].Type == lastType) {
                  if ((lastType != EventType.Controller && (int)lastType != 0x25 && lastType != EventType.EndOfTie) || events[i].Param1 == lastParam1) score++;
                  else score += 2;
               } else {
                  score += 2;
               }
            }
            lastParam1 = events[i].Param1;
            lastType = events[i].Type;
            if (events[i].Time != 0) score++;
         }
         return score;
      }

      private static bool IsCompressionMatch(List<Event> events, int index1, int index2) {
         if (events[index1].Type != events[index2].Type ||
            events[index1].Note != events[index2].Note ||
            events[index1].Param1 != events[index2].Param1 ||
            events[index1].Time != events[index2].Time) return false;

         index1++;
         index2++;
         do {
            if (!events[index1].SameAs(events[index2])) return false;
            index1++;
            index2++;
         } while (!IsPatternBoundary(events[index1].Type));
         return IsPatternBoundary(events[index2].Type);
      }

      private static void CompressWholeNote(List<Event> events, int index) {
         for (int j = index + 1; events[j].Type != EventType.EndOfTrack; j++) {
            while (events[j].Type != EventType.WholeNoteMark) {
               j++;
               if (events[j].Type == EventType.EndOfTrack) return;
            }
            if (IsCompressionMatch(events, index, j)) {
               var pattern = events[j];
               pattern.Type = EventType.Pattern;
               pattern.Param2 = events[index].Param2 & 0x7FFFFFFF;
               events[j] = pattern;
               var mark = events[index];
               mark.Param2 |= int.MinValue;
               events[index] = mark;
            }
         }
      }

      private static void Compress(List<Event> events) {
         for (int i = 0; events[i].Type != EventType.EndOfTrack; i++) {
            while (events[i].Type != EventType.WholeNoteMark) {
               i++;
               if (events[i].Type == EventType.EndOfTrack) return;
            }
            if (CalculateCompressionScore(events, i) >= 6) CompressWholeNote(events, i);
         }
      }

      /// <summary>Gives a channel whose first note comes before any program change the instrument MIDI plays then: program 0 (see MidiToAgbOptions.AddMissingVoice).</summary>
      private void AddMissingVoice() {
         var firstNote = int.MaxValue;
         foreach (var e in trackEvents) if (e.Type == EventType.Note && e.Param2 > 0 && e.Time < firstNote) firstNote = e.Time;
         if (trackEvents.Any(e => e.Type == EventType.InstrumentChange && e.Time <= firstNote)) return;
         trackEvents.Insert(0, new Event { Time = 0, Type = EventType.InstrumentChange, Param1 = 0, Param2 = 0 });
      }

      private List<(int midiTrack, int channel)> ReadMidiTracks() {
         var channels = new List<(int, int)>();
         long trackHeaderStart = 14;
         int trackLoops = 4; // B_NUM_LOW_HEALTH_BEEPS of the pokeemerald-expansion this tool was built from

         ReadMidiTrackHeader(trackHeaderStart);
         ReadSeqEvents();

         agbTrack = 1;

         for (int midiTrack = 0; midiTrack < midiTrackCount; midiTrack++) {
            trackHeaderStart += ReadMidiTrackHeader(trackHeaderStart);

            for (midiChan = 0; midiChan < 16; midiChan++) {
               ReadTrackEvents();

               if (minNote != 0xFF) {
                  if (options.AddMissingVoice) AddMissingVoice();
                  var events = MergeEvents();

                  // we don't need TEMPO in anything but track 1
                  if (agbTrack == 1) seqEvents.RemoveAll(e => e.Type == EventType.Tempo);

                  ConvertTimes(events);
                  events = InsertTimingEvents(events);
                  events = CreateTies(events);
                  events = StableSort(events);
                  events = SplitTime(events);
                  CalculateWaits(events);

                  if (options.Compression) Compress(events);

                  if (label == "se_low_health" && trackLoops >= 0) PrintAgbTrackLoop(events, trackLoops);
                  else PrintAgbTrack(events);

                  channels.Add((midiTrack + 1, midiChan + 1));
                  agbTrack++;
               }
            }
         }
         return channels;
      }

      #endregion

      #region Writing the song (agb.cpp)

      private static string Unsigned(int value) => unchecked((uint)value).ToString(CultureInfo.InvariantCulture);

      private static string Signed(int value) => (value >= 0 ? "+" : "-") + Math.Abs((long)value).ToString(CultureInfo.InvariantCulture);

      private static string Pad(int value, int width) => value.ToString("D" + width, CultureInfo.InvariantCulture);

      private static string NoteName(int note) {
         // the tables are "Cn%01u " for notes from 24 up and "CnM%01u" for the ones below
         if (note >= 24) return NoteTable[note % 12] + Unsigned(note / 12 - 2) + " ";
         return NoteTable[note % 12] + "M" + Unsigned(note / -12 + 2);
      }

      private void Line(string text) => output.Append(text).Append('\n');

      private void PrintAgbHeader() {
         Line("\t.include \"MPlayDef.s\"");
         Line("");
         Line($"\t.equ\t{label}_grp, voicegroup{options.VoiceGroup}");
         Line($"\t.equ\t{label}_pri, {Unsigned(options.Priority)}");
         if (options.Reverb >= 0) Line($"\t.equ\t{label}_rev, reverb_set+{Unsigned(options.Reverb)}");
         else Line($"\t.equ\t{label}_rev, 0");
         Line($"\t.equ\t{label}_mvl, {Unsigned(options.MasterVolume)}");
         Line($"\t.equ\t{label}_key, 0");
         Line($"\t.equ\t{label}_tbs, {clocksPerBeat}");
         Line($"\t.equ\t{label}_exg, {(options.ExactGateTime ? 1 : 0)}");
         Line($"\t.equ\t{label}_cmp, {(options.Compression ? 1 : 0)}");
         Line("");
         Line("\t.section .rodata");
         Line($"\t.global\t{label}");
         Line("\t.align\t2");
      }

      private void ResetTrackVars() {
         lastVelocity = -1;
         lastNote = -1;
         velocityChanged = false;
         noteChanged = false;
         keepLastOpName = false;
         lastOpName = string.Empty;
         inPattern = false;
      }

      private void PrintWait(int wait) {
         if (wait > 0) {
            Line("\t.byte\tW" + Pad(wait, 2));
            velocityChanged = true;
            noteChanged = true;
            keepLastOpName = true;
         }
      }

      /// <summary>PrintOp(wait, name, format, ...): the operands are already formatted, null for an op without any.</summary>
      private void PrintOp(int wait, string name, string operands) {
         output.Append("\t.byte\t\t");
         if (operands != null) {
            if (!options.Compression || lastOpName != name) {
               output.Append(name).Append(", ");
               lastOpName = name;
            } else {
               output.Append("        ");
            }
            output.Append(operands);
         } else {
            output.Append(name);
            lastOpName = name;
         }
         output.Append('\n');
         PrintWait(wait);
      }

      private void PrintByte(string text) {
         output.Append("\t.byte\t").Append(text).Append('\n');
         velocityChanged = true;
         noteChanged = true;
         keepLastOpName = true;
      }

      private void PrintWord(string text) => output.Append("\t .word\t").Append(text).Append('\n');

      private void PrintNote(Event e) {
         int note = e.Note;
         int velocity = Velocity(e.Param1);
         int duration = -1;
         if (e.Param2 != -1) duration = Duration(e.Param2);

         int gateTimeParam = 0;
         if (options.ExactGateTime && duration != -1) gateTimeParam = e.Param2 - duration;

         var gateTimeText = gateTimeParam > 0 ? ", gtp" + Unsigned(gateTimeParam) : string.Empty;
         var opName = duration == -1 ? "TIE   " : "N" + Pad(duration, 2) + "   ";

         bool noteChangedNow = true;
         bool velocityChangedNow = true;
         if (options.Compression) {
            noteChangedNow = note != lastNote;
            velocityChangedNow = velocity != lastVelocity;
         }

         if (keepLastOpName) keepLastOpName = false;
         else lastOpName = string.Empty;

         if (noteChangedNow || velocityChangedNow || gateTimeParam > 0) {
            lastNote = note;
            var noteText = NoteName(note);
            string velocityText;
            if (velocityChangedNow || gateTimeParam > 0) {
               lastVelocity = velocity;
               velocityText = ", v" + Pad(velocity, 3);
            } else {
               velocityText = string.Empty;
            }
            PrintOp(e.Time, opName, noteText + velocityText + gateTimeText);
         } else {
            PrintOp(e.Time, opName, null);
         }

         noteChanged = noteChangedNow;
         velocityChanged = velocityChangedNow;
      }

      private void PrintEndOfTieOp(Event e) {
         int note = e.Note;
         bool noteChangedNow = note != lastNote;
         if (!noteChangedNow || !noteChanged) lastOpName = string.Empty;

         if (!noteChangedNow && options.Compression) {
            PrintOp(e.Time, "EOT   ", null);
         } else {
            lastNote = note;
            PrintOp(e.Time, "EOT   ", NoteName(note));
         }
         noteChanged = noteChangedNow;
      }

      private void PrintSeqLoopLabel(Event e) {
         blockNum = e.Param1 + 1;
         Line($"{label}_{agbTrack}_B{blockNum}:");
         PrintWait(e.Time);
         ResetTrackVars();
      }

      private void PrintBranchWord() => PrintWord($"{label}_{agbTrack}_L{Unsigned(memaccParam2)}");

      private void PrintMemAcc(Event e) {
         var p1 = "0x" + memaccParam1.ToString("X2", CultureInfo.InvariantCulture);
         var p2Hex = "0x" + e.Param2.ToString("X2", CultureInfo.InvariantCulture);
         var p2 = Unsigned(e.Param2);
         switch (memaccOp) {
            case 0x00: PrintByte($"MEMACC, mem_set, {p1}, {p2}"); break;
            case 0x01: PrintByte($"MEMACC, mem_add, {p1}, {p2}"); break;
            case 0x02: PrintByte($"MEMACC, mem_sub, {p1}, {p2}"); break;
            case 0x03: PrintByte($"MEMACC, mem_mem_set, {p1}, {p2Hex}"); break;
            case 0x04: PrintByte($"MEMACC, mem_mem_add, {p1}, {p2Hex}"); break;
            case 0x05: PrintByte($"MEMACC, mem_mem_sub, {p1}, {p2Hex}"); break;
            case 0x06: PrintByte($"MEMACC, mem_beq, {p1}, {p2}"); PrintBranchWord(); break;
            case 0x07: PrintByte($"MEMACC, mem_bne, {p1}, {p2}"); PrintBranchWord(); break;
            case 0x08: PrintByte($"MEMACC, mem_bhi, {p1}, {p2}"); PrintBranchWord(); break;
            case 0x09: PrintByte($"MEMACC, mem_bhs, {p1}, {p2}"); PrintBranchWord(); break;
            case 0x0A: PrintByte($"MEMACC, mem_bls, {p1}, {p2}"); PrintBranchWord(); break;
            case 0x0B: PrintByte($"MEMACC, mem_blo, {p1}, {p2}"); PrintBranchWord(); break;
            case 0x0C: PrintByte($"MEMACC, mem_mem_beq, {p1}, {p2Hex}"); PrintBranchWord(); break;
            case 0x0D: PrintByte($"MEMACC, mem_mem_bne, {p1}, {p2Hex}"); PrintBranchWord(); break;
            case 0x0E: PrintByte($"MEMACC, mem_mem_bhi, {p1}, {p2Hex}"); PrintBranchWord(); break;
            case 0x0F: PrintByte($"MEMACC, mem_mem_bhs, {p1}, {p2Hex}"); PrintBranchWord(); break;
            case 0x10: PrintByte($"MEMACC, mem_mem_bls, {p1}, {p2Hex}"); PrintBranchWord(); break;
            case 0x11: PrintByte($"MEMACC, mem_mem_blo, {p1}, {p2Hex}"); PrintBranchWord(); break;
            default: break;
         }
         PrintWait(e.Time);
      }

      private void PrintExtendedOp(Event e) {
         switch (extendedCommand) {
            case 0x08: PrintOp(e.Time, "XCMD  ", "xIECV , " + Unsigned(e.Param2)); break;
            case 0x09: PrintOp(e.Time, "XCMD  ", "xIECL , " + Unsigned(e.Param2)); break;
            default: PrintWait(e.Time); break;
         }
      }

      private void PrintControllerOp(Event e) {
         switch (e.Param1) {
            case 0x01: PrintOp(e.Time, "MOD   ", Unsigned(e.Param2)); break;
            case 0x07: PrintOp(e.Time, "VOL   ", $"{Unsigned(e.Param2)}*{label}_mvl/mxv"); break;
            case 0x0A: PrintOp(e.Time, "PAN   ", "c_v" + Signed(e.Param2 - 64)); break;
            case 0x0C:
            case 0x10:
               PrintMemAcc(e);
               break;
            case 0x0D:
               memaccOp = e.Param2;
               PrintWait(e.Time);
               break;
            case 0x0E:
               memaccParam1 = e.Param2;
               PrintWait(e.Time);
               break;
            case 0x0F:
               memaccParam2 = e.Param2;
               PrintWait(e.Time);
               break;
            case 0x11:
               Line($"{label}_{agbTrack}_L{Unsigned(e.Param2)}:");
               PrintWait(e.Time);
               ResetTrackVars();
               break;
            case 0x14: PrintOp(e.Time, "BENDR ", Unsigned(e.Param2)); break;
            case 0x15: PrintOp(e.Time, "LFOS  ", Unsigned(e.Param2)); break;
            case 0x16: PrintOp(e.Time, "MODT  ", Unsigned(e.Param2)); break;
            case 0x18: PrintOp(e.Time, "TUNE  ", "c_v" + Signed(e.Param2 - 64)); break;
            case 0x1A: PrintOp(e.Time, "LFODL ", Unsigned(e.Param2)); break;
            case 0x1D:
            case 0x1F:
               PrintExtendedOp(e);
               break;
            case 0x1E:
               extendedCommand = e.Param2;
               // TODO in mid2agb: loop op (the wait is dropped as well)
               break;
            case 0x21:
            case 0x27:
               PrintByte("PRIO  , " + Unsigned(e.Param2));
               PrintWait(e.Time);
               break;
            default:
               PrintWait(e.Time);
               break;
         }
      }

      private string TempoText(int microsecondsPerBeat) {
         // static_cast<int>(round(60000000.0f / static_cast<float>(param2))): single precision, and the x86 result of an impossible conversion is int.MinValue
         float quotient = 60000000.0f / (float)microsecondsPerBeat;
         double rounded = Math.Round((double)quotient, MidpointRounding.AwayFromZero);
         int tempo = double.IsNaN(rounded) || rounded >= 2147483648.0 || rounded < -2147483648.0 ? int.MinValue : (int)rounded;
         return $"TEMPO , {Unsigned(tempo)}*{label}_tbs/2";
      }

      private void PrintAgbTrackHeader() {
         Line("");
         Line($"@**************** Track {agbTrack} (Midi-Chn.{midiChan + 1}) ****************@");
         Line("");
         Line($"{label}_{agbTrack}:");
      }

      private void PrintStartOfTrack(List<Event> events) {
         ResetTrackVars();
         bool foundVolBeforeNote = false;
         foreach (var e in events) {
            if (e.Type == EventType.Note) break;
            if (e.Type == EventType.Controller && e.Param1 == 0x07) {
               foundVolBeforeNote = true;
               break;
            }
         }
         if (!foundVolBeforeNote) PrintByte($"\tVOL   , 127*{label}_mvl/mxv");
         PrintWait(initialWait);
      }

      private void PrintAgbTrack(List<Event> events) {
         PrintAgbTrackHeader();
         int wholeNoteCount = 0;
         int loopEndBlockNum = 0;

         PrintStartOfTrack(events);
         PrintByte($"KEYSH , {label}_key{Signed(0)}");

         for (int i = 0; events[i].Type != EventType.EndOfTrack; i++) {
            var e = events[i];

            if (IsPatternBoundary(e.Type)) {
               if (inPattern) PrintByte("PEND");
               inPattern = false;
            }

            if (e.Type == EventType.WholeNoteMark || e.Type == EventType.Pattern) Line($"@ {Pad(wholeNoteCount++, 3)}   ----------------------------------------");

            switch (e.Type) {
               case EventType.Note:
                  PrintNote(e);
                  break;
               case EventType.EndOfTie:
                  PrintEndOfTieOp(e);
                  break;
               case EventType.Label:
                  PrintSeqLoopLabel(e);
                  break;
               case EventType.LoopEnd:
                  PrintByte("GOTO");
                  PrintWord($"{label}_{agbTrack}_B{loopEndBlockNum}");
                  PrintSeqLoopLabel(e);
                  break;
               case EventType.LoopEndBegin:
                  PrintByte("GOTO");
                  PrintWord($"{label}_{agbTrack}_B{loopEndBlockNum}");
                  PrintSeqLoopLabel(e);
                  loopEndBlockNum = blockNum;
                  break;
               case EventType.LoopBegin:
                  PrintSeqLoopLabel(e);
                  loopEndBlockNum = blockNum;
                  break;
               case EventType.WholeNoteMark:
                  if ((e.Param2 & int.MinValue) != 0) {
                     Line($"{label}_{agbTrack}_{Pad(e.Param2 & 0x7FFFFFFF, 3)}:");
                     ResetTrackVars();
                     inPattern = true;
                  }
                  PrintWait(e.Time);
                  break;
               case EventType.Pattern:
                  PrintByte("PATT");
                  PrintWord($"{label}_{agbTrack}_{Pad(e.Param2, 3)}");
                  while (!IsPatternBoundary(events[i + 1].Type)) i++;
                  ResetTrackVars();
                  break;
               case EventType.Tempo:
                  PrintByte(TempoText(e.Param2));
                  PrintWait(e.Time);
                  break;
               case EventType.InstrumentChange:
                  PrintOp(e.Time, "VOICE ", Unsigned(e.Param1));
                  break;
               case EventType.PitchBend:
                  PrintOp(e.Time, "BEND  ", "c_v" + Signed(e.Param2 - 64));
                  break;
               case EventType.Controller:
                  PrintControllerOp(e);
                  break;
               default:
                  PrintWait(e.Time);
                  break;
            }
         }
         PrintByte("FINE");
      }

      /// <summary>The same track written trackLoops times, for the song called se_low_health (the "low HP" beep that repeats a set number of times).</summary>
      private void PrintAgbTrackLoop(List<Event> events, int trackLoops) {
         PrintAgbTrackHeader();
         int wholeNoteCount = 0;

         PrintStartOfTrack(events);
         if (trackLoops > 0) PrintByte($"KEYSH , {label}_key{Signed(0)}");

         for (int k = 0; k < trackLoops; k++) {
            for (int i = 0; events[i].Type != EventType.EndOfTrack; i++) {
               var e = events[i];

               if (IsPatternBoundary(e.Type)) {
                  if (inPattern) PrintByte("PEND");
                  inPattern = false;
               }

               // mid2agb adds `&& (i % 2 == 0)` to cut down on excess comments created in the .s file
               if ((e.Type == EventType.WholeNoteMark || e.Type == EventType.Pattern) && i % 2 == 0) Line($"@ {Pad(wholeNoteCount++, 3)}   ----------------------------------------");

               switch (e.Type) {
                  case EventType.Note:
                     PrintNote(e);
                     break;
                  case EventType.EndOfTie:
                     PrintEndOfTieOp(e);
                     break;
                  case EventType.Label:
                     if (k == 0) PrintSeqLoopLabel(e);
                     break;
                  case EventType.LoopEnd:
                  case EventType.LoopEndBegin:
                     break;
                  case EventType.LoopBegin:
                     if (k == 0) PrintSeqLoopLabel(e);
                     break;
                  case EventType.WholeNoteMark:
                     if ((e.Param2 & int.MinValue) != 0) {
                        Line($"{label}_{agbTrack}_{Pad(e.Param2 & 0x7FFFFFFF, 3)}:");
                        ResetTrackVars();
                        inPattern = true;
                     }
                     PrintWait(e.Time);
                     break;
                  case EventType.Pattern:
                     PrintByte("PATT");
                     PrintWord($"{label}_{agbTrack}_{Pad(e.Param2, 3)}");
                     while (!IsPatternBoundary(events[i + 1].Type)) i++;
                     ResetTrackVars();
                     break;
                  case EventType.Tempo:
                     if (k == 0) {
                        PrintByte(TempoText(e.Param2));
                        PrintWait(e.Time);
                     }
                     break;
                  case EventType.InstrumentChange:
                     if (k == 0) PrintOp(e.Time, "VOICE ", Unsigned(e.Param1));
                     break;
                  case EventType.PitchBend:
                     PrintOp(e.Time, "BEND  ", "c_v" + Signed(e.Param2 - 64));
                     break;
                  case EventType.Controller:
                     if (k == 0) PrintControllerOp(e);
                     break;
                  default:
                     PrintWait(e.Time);
                     break;
               }
            }
         }
         PrintByte("FINE");
      }

      private void PrintAgbFooter() {
         int trackCount = agbTrack - 1;
         Line("");
         Line("@******************************************************@");
         Line("\t.align\t2");
         Line("");
         Line($"{label}:");
         Line($"\t.byte\t{trackCount}\t@ NumTrks");
         Line($"\t.byte\t{0}\t@ NumBlks");
         Line($"\t.byte\t{label}_pri\t@ Priority");
         Line($"\t.byte\t{label}_rev\t@ Reverb.");
         Line("");
         Line($"\t.word\t{label}_grp");
         Line("");
         for (int i = 1; i <= trackCount; i++) Line($"\t.word\t{label}_{i}");
         Line("");
         Line("\t.end");
      }

      #endregion
   }
}
