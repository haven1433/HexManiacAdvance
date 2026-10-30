using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Sound {
   public enum ToneKind { Invalid, DirectSound, Square1, Square2, ProgrammableWave, Noise, Keysplit, DrumKit }

   /// <summary>
   /// One instrument of a voicegroup: the 12 byte ToneData entry of the m4a engine.
   /// { u8 type, u8 key, u8 length, u8 pan/sweep, u32 wave pointer | duty | noise period | voicegroup pointer, u8 attack, u8 decay, u8 sustain, u8 release | u32 key split table }
   /// </summary>
   public class ToneData {
      public const int Size = 12;

      public int Address { get; init; }
      public int Type { get; init; }
      public int Key { get; init; }
      public int Length { get; init; }
      public int PanSweep { get; init; }
      /// <summary>Bytes 4-7: the sample (DirectSound, programmable wave), the duty cycle / noise period, or the voicegroup of a key split / drum kit.</summary>
      public int Word1 { get; init; }
      /// <summary>Bytes 8-11: attack, decay, sustain, release - or for a key split the pointer to the key table.</summary>
      public int Word2 { get; init; }
      public ToneKind Kind { get; init; }

      public bool IsValid => Kind != ToneKind.Invalid;
      public int Attack => Word2 & 0xFF;
      public int Decay => (Word2 >> 8) & 0xFF;
      public int Sustain => (Word2 >> 16) & 0xFF;
      public int Release => (Word2 >> 24) & 0xFF;
      /// <summary>Type bit 3: DirectSound samples that play at a fixed rate instead of following the note's pitch.</summary>
      public bool IsFixedFrequency => Kind == ToneKind.DirectSound && (Type & 0x08) != 0;
      /// <summary>The ROM offset the first word points to (meaningful for samples, waves and voicegroups).</summary>
      public int Word1Address => Word1 - BaseModel.PointerOffset;

      /// <summary>Reads the 12 bytes at an address. Returns null only when they don't fit in the model; check IsValid to see whether they look like an instrument.</summary>
      public static ToneData Read(IDataModel model, int address) {
         if (address < 0 || address + Size > model.Count) return null;
         int type = model[address];
         int word1 = model.ReadMultiByteValue(address + 4, 4);
         int word2 = model.ReadMultiByteValue(address + 8, 4);
         return new ToneData {
            Address = address, Type = type, Key = model[address + 1], Length = model[address + 2], PanSweep = model[address + 3],
            Word1 = word1, Word2 = word2, Kind = Classify(model, type, word1, word2),
         };
      }

      public static bool TryRead(IDataModel model, int address, out ToneData tone) {
         tone = Read(model, address);
         return tone != null && tone.IsValid;
      }

      private static bool IsRomPointer(IDataModel model, int value) => value >= BaseModel.PointerOffset && value - BaseModel.PointerOffset < model.Count;

      /// <summary>
      /// Decides whether 12 bytes can be an instrument. The rules follow what the pokeemerald voice_* macros emit
      /// (and what the engine reads), strict enough to tell the end of a voicegroup from the data after it.
      /// </summary>
      public static ToneKind Classify(IDataModel model, int type, int word1, int word2) {
         if ((type & 0xC0) == 0x40) return type == 0x40 && IsRomPointer(model, word1) && IsRomPointer(model, word2) ? ToneKind.Keysplit : ToneKind.Invalid;
         if ((type & 0xC0) == 0x80) return type == 0x80 && IsRomPointer(model, word1) ? ToneKind.DrumKit : ToneKind.Invalid;
         if ((type & 0xC0) != 0) return ToneKind.Invalid;
         switch (type & 0x07) {
            case 0: return IsRomPointer(model, word1) ? ToneKind.DirectSound : ToneKind.Invalid;
            case 1: return (uint)word1 <= 0xFF && type <= 0x0F ? ToneKind.Square1 : ToneKind.Invalid;
            case 2: return (uint)word1 <= 0xFF && type <= 0x0F ? ToneKind.Square2 : ToneKind.Invalid;
            case 3: return IsRomPointer(model, word1) && type <= 0x0F ? ToneKind.ProgrammableWave : ToneKind.Invalid;
            case 4: return (uint)word1 <= 0xFF && type <= 0x0F ? ToneKind.Noise : ToneKind.Invalid;
            default: return ToneKind.Invalid;
         }
      }

      public static string KindName(ToneKind kind) => kind switch {
         ToneKind.DirectSound => "Sample",
         ToneKind.Square1 => "Square wave 1",
         ToneKind.Square2 => "Square wave 2",
         ToneKind.ProgrammableWave => "Programmable wave",
         ToneKind.Noise => "Noise",
         ToneKind.Keysplit => "Key split",
         ToneKind.DrumKit => "Drum kit",
         _ => "Empty / unknown",
      };

      public static string FormatSize(int bytes) => bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:0.#} KB";

      /// <summary>A short description for lists: the kind and, for samples, the size and rate.</summary>
      public string Describe(IDataModel model) {
         switch (Kind) {
            case ToneKind.DirectSound:
               if (!GbaSample.TryRead(model, Word1Address, out var sample)) return "Sample (unreadable)";
               return $"Sample {FormatSize(sample.TotalLength)}, {sample.SampleRate:0} Hz, {sample.DurationSeconds:0.##} s{(sample.IsLooped ? ", loops" : "")}{(IsFixedFrequency ? ", fixed pitch" : "")}";
            case ToneKind.Square1:
            case ToneKind.Square2:
               var duty = new[] { "12.5%", "25%", "50%", "75%" }[Word1 & 3];
               return $"{KindName(Kind)}, {duty} duty";
            case ToneKind.Noise:
               return (Word1 & 1) != 0 ? "Noise (metallic)" : "Noise";
            case ToneKind.Keysplit:
               return "Key split (different instruments across the keyboard)";
            case ToneKind.DrumKit:
               return "Drum kit (a different sound on every key)";
            default:
               return KindName(Kind);
         }
      }
   }

   /// <summary>A voicegroup found in the ROM: where it is, how many instruments it holds and what it is called.</summary>
   public class VoicegroupInfo {
      public int Address { get; init; }
      /// <summary>How many instruments from the start look valid (0: this address isn't recognizable as a voicegroup).</summary>
      public int EntryCount { get; init; }
      /// <summary>How many entries a copy of this voicegroup carries over (at least EntryCount, so odd instruments in the middle aren't lost).</summary>
      public int CopyCount { get; init; }
      /// <summary>The name of the anchor (from the metadata) that starts here, or null.</summary>
      public string AnchorName { get; init; }
      /// <summary>The anchor's name without the 'sound.voicegroups.' style prefix, or null when it has no name.</summary>
      public string Name { get; init; }
      public IReadOnlyList<string> UsedBy { get; init; } = Array.Empty<string>();
      public bool IsCustom => EntryCount == 0;
   }

   /// <summary>Finds and names the voicegroups of a ROM.</summary>
   public static class VoicegroupCatalog {
      public const int MaxEntries = 128;
      public const int EntrySize = ToneData.Size;
      public const int Length = MaxEntries * EntrySize;
      /// <summary>How many instruments in a row can fail to look like an instrument before the data is taken to be over.</summary>
      private const int MaxUnreadableGap = 4;

      /// <summary>The "unused instrument" the games put in the slots a voicegroup doesn't use: voice_square_1 60, 0, 0, 2, 0, 0, 15, 0</summary>
      public static byte[] FillerTone() => new byte[] { 0x01, 0x3C, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x00 };

      /// <summary>The format given to voicegroups created by the Sound tab. The pointer/duty word and the key table are plain hex so the table tool never misreads a square wave's duty as a pointer.</summary>
      public const string TableFormat = "[type.|h key. length. pan_sweep.|h wave_or_group::|h attack. decay. sustain. release.]128";
      public const string AnchorPrefix = "sound.voicegroups.";

      /// <summary>How many of the first 'count' instruments of the voicegroup at 'address' are real ones, that is, not the games' unused-instrument filler.</summary>
      public static int CountRealInstruments(IDataModel model, int address, int count) {
         if (address < 0) return 0;
         var filler = FillerTone();
         int real = 0;
         for (int slot = 0; slot < count && slot < MaxEntries; slot++) {
            var start = address + slot * EntrySize;
            if (start + EntrySize > model.Count) break;
            bool same = true;
            for (int i = 0; i < EntrySize && same; i++) same = model[start + i] == filler[i];
            if (!same) real++;
         }
         return real;
      }

      /// <summary>True when the address starts a table of 12-byte elements (a voicegroup the metadata declares) whose first instrument is not an instrument: the declaration does not fit the data.</summary>
      public static bool IsDeclaredVoicegroupWithoutInstruments(IDataModel model, int address)
         => address >= 0 && address < model.Count && model.GetNextRun(address) is ITableRun table && table.Start == address && table.ElementLength == EntrySize && table.ElementCount > 0
         && !ToneData.TryRead(model, address, out _);

      /// <summary>The number of instruments in a row, from the start, that look valid.</summary>
      public static int CountEntries(IDataModel model, int address, int maxEntries = MaxEntries) {
         if (address < 0) return 0;
         int count = 0;
         while (count < maxEntries && count < MaxEntries && ToneData.TryRead(model, address + count * EntrySize, out _)) count++;
         return count;
      }

      /// <summary>
      /// Anchors that hold a voicegroup are called something like 'sound.voicegroups.route110' or 'data.sound.voicegroup.mus_foo':
      /// the name has a 'voicegroup' or 'voicegroups' part followed by the voicegroup's own name.
      /// </summary>
      public static bool IsVoicegroupAnchor(string anchorName, out string shortName) {
         shortName = null;
         if (string.IsNullOrEmpty(anchorName)) return false;
         var parts = anchorName.Split('.');
         for (int i = 0; i < parts.Length - 1; i++) {
            if (!parts[i].Equals("voicegroups", StringComparison.OrdinalIgnoreCase) && !parts[i].Equals("voicegroup", StringComparison.OrdinalIgnoreCase)) continue;
            shortName = string.Join(".", parts.Skip(i + 1));
            return shortName.Length > 0;
         }
         return false;
      }

      /// <summary>
      /// Collects the voicegroups: every address a song uses, plus every anchor that is named like a voicegroup.
      /// A voicegroup ends where the next known one starts, or where the data stops looking like instruments.
      /// </summary>
      /// <param name="usages">(voicegroup address, name of the song using it) for each song</param>
      public static List<VoicegroupInfo> Scan(IDataModel model, IEnumerable<(int voicegroup, string song)> usages) {
         var users = new Dictionary<int, List<string>>();
         foreach (var (voicegroup, song) in usages) {
            if (voicegroup < 0 || voicegroup >= model.Count) continue;
            if (!users.TryGetValue(voicegroup, out var list)) users[voicegroup] = list = new List<string>();
            list.Add(song);
         }

         var names = new Dictionary<int, (string anchor, string name)>();
         var noChange = new NoDataChangeDeltaModel();
         foreach (var anchor in model.Anchors) {
            if (!IsVoicegroupAnchor(anchor, out var shortName)) continue;
            var address = model.GetAddressFromAnchor(noChange, -1, anchor);
            if (address < 0 || address >= model.Count || names.ContainsKey(address)) continue;
            names[address] = (anchor, shortName);
         }

         var starts = users.Keys.Concat(names.Keys).Distinct().OrderBy(address => address).ToList();
         var result = new List<VoicegroupInfo>();
         for (int i = 0; i < starts.Count; i++) {
            var address = starts[i];
            int bound = MaxEntries;
            if (i + 1 < starts.Count) bound = Math.Min(bound, (starts[i + 1] - address) / EntrySize);
            names.TryGetValue(address, out var name);
            users.TryGetValue(address, out var usedBy);
            result.Add(Build(model, address, bound, name.anchor, name.name, usedBy));
         }

         // named ones first (alphabetical), then the unnamed ones in ROM order, then whatever isn't a voicegroup
         return result
            .OrderBy(info => info.IsCustom ? 2 : info.Name != null ? 0 : 1)
            .ThenBy(info => info.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(info => info.Address)
            .ToList();
      }

      /// <summary>
      /// Looks at one address on its own (nothing known to follow it): what it is called and how many instruments it holds.
      /// Gives a custom voicegroup (EntryCount 0) when the data there doesn't look like instruments.
      /// </summary>
      public static VoicegroupInfo Inspect(IDataModel model, int address) {
         string anchor = null, shortName = null;
         if (address >= 0 && address < model.Count) {
            var anchorName = model.GetAnchorFromAddress(-1, address);
            if (IsVoicegroupAnchor(anchorName, out var parsed)) (anchor, shortName) = (anchorName, parsed);
         }
         return Build(model, address, MaxEntries, anchor, shortName, null);
      }

      private static VoicegroupInfo Build(IDataModel model, int address, int bound, string anchor, string name, IReadOnlyList<string> usedBy) {
         int entries, copy;
         bound = Math.Max(0, Math.Min(bound, MaxEntries));
         if (address >= 0 && name != null && model.GetNextRun(address) is ITableRun table && table.Start == address && table.ElementLength == EntrySize && table.ElementCount > 0 && ToneData.TryRead(model, address, out _)) {
            // a table the metadata declares: trust its length (when its first instrument is an instrument: an old toml whose anchor moved to other data is not trusted, the data is read as it is)
            entries = copy = Math.Min(MaxEntries, table.ElementCount);
         } else {
            entries = CountEntries(model, address, bound);
            int lastValid = entries - 1;
            // a voicegroup somebody edited may have a few instruments that don't look like instruments in the middle: a copy keeps what comes after them
            for (int j = entries; j < bound && j - lastValid <= MaxUnreadableGap; j++) if (ToneData.TryRead(model, address + j * EntrySize, out _)) lastValid = j;
            copy = entries == 0 ? Math.Min(bound, Math.Max(0, (model.Count - address) / EntrySize)) : lastValid + 1;
         }
         return new VoicegroupInfo {
            Address = address, EntryCount = entries, CopyCount = Math.Max(entries, copy), AnchorName = anchor, Name = name,
            UsedBy = usedBy ?? (IReadOnlyList<string>)Array.Empty<string>(),
         };
      }

      /// <summary>"3 samples, 12 square waves..." - the first instruments of a voicegroup, to help tell voicegroups apart.</summary>
      public static string DescribeStart(IDataModel model, int address, int entryCount, int maxShown = 6) {
         var kinds = new List<string>();
         for (int i = 0; i < Math.Min(entryCount, maxShown); i++) {
            var tone = ToneData.Read(model, address + i * EntrySize);
            kinds.Add(tone == null ? "?" : ToneData.KindName(tone.Kind));
         }
         if (entryCount > maxShown) kinds.Add("...");
         return string.Join(", ", kinds);
      }
   }

   /// <summary>
   /// The edits of the Sound tab that involve voicegroups. They all write to the change token they are given, so a caller decides what one undo step covers.
   /// </summary>
   public static class VoicegroupEditor {
      /// <summary>No GBA cartridge is larger than this: pointers are 0x08xxxxxx and 0x09xxxxxx.</summary>
      public const int MaxRomLength = 0x2000000;

      /// <summary>
      /// Finds room for new data: free space first, then the end of the ROM (as long as the ROM stays within the 32MB cartridge limit).
      /// </summary>
      /// <param name="maxRomLength">The size the ROM may grow to (the cartridge limit unless a caller wants a smaller one).</param>
      public static bool TryAllocate(IDataModel model, ModelDelta token, int length, out int address, out string error, int maxRomLength = MaxRomLength) {
         error = null;
         address = model.FindFreeSpace(model.FreeSpaceStart, length);
         if (address >= 0) return true;
         var end = (model.Count + 3) / 4 * 4;
         if (end + length > maxRomLength) {
            error = $"There is no free space left for {length} bytes, and the ROM can't grow past {(maxRomLength % 0x100000 == 0 ? (maxRomLength / 0x100000) + "MB" : maxRomLength + " bytes")}.";
            return false;
         }
         address = end;
         model.ExpandData(token, end + length);
         return true;
      }

      /// <summary>An anchor name for a song's own voicegroup that isn't taken yet: 'sound.voicegroups.mus_foo', 'sound.voicegroups.mus_foo_2', ...</summary>
      public static string UniqueAnchorName(IDataModel model, string songName) {
         var sb = new StringBuilder();
         foreach (var c in (songName ?? string.Empty).Trim().ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) && c < 128 || c == '_' ? c : '_');
         var cleaned = sb.ToString().Trim('_');
         if (cleaned.Length == 0) cleaned = "song";
         if (char.IsDigit(cleaned[0])) cleaned = "song_" + cleaned;
         var baseName = VoicegroupCatalog.AnchorPrefix + cleaned;
         var name = baseName;
         var noChange = new NoDataChangeDeltaModel();
         for (int i = 2; model.GetAddressFromAnchor(noChange, -1, name) >= 0; i++) name = baseName + "_" + i;
         return name;
      }

      /// <summary>The 128 instruments of a new voicegroup: the first 'copyCount' entries of the source, then the games' unused-instrument filler.</summary>
      public static byte[] BuildCopy(IDataModel model, int sourceAddress, int copyCount) {
         var bytes = new byte[VoicegroupCatalog.Length];
         var filler = VoicegroupCatalog.FillerTone();
         copyCount = Math.Max(0, Math.Min(copyCount, VoicegroupCatalog.MaxEntries));
         for (int slot = 0; slot < VoicegroupCatalog.MaxEntries; slot++) {
            var sourceSlot = sourceAddress + slot * VoicegroupCatalog.EntrySize;
            bool copy = slot < copyCount && sourceAddress >= 0 && sourceSlot + VoicegroupCatalog.EntrySize <= model.Count;
            for (int i = 0; i < VoicegroupCatalog.EntrySize; i++) bytes[slot * VoicegroupCatalog.EntrySize + i] = copy ? model[sourceSlot + i] : filler[i];
         }
         return bytes;
      }

      /// <summary>
      /// Puts a new 128-instrument voicegroup, initialised as a copy of 'source', into free space and gives it a named anchor.
      /// Nothing is changed when this returns false.
      /// </summary>
      public static bool TryCreateCopy(IDataModel model, ModelDelta token, VoicegroupInfo source, string songName, out int address, out string anchorName, out string error) {
         anchorName = null;
         if (!TryAllocate(model, token, VoicegroupCatalog.Length, out address, out error)) return false;
         var bytes = BuildCopy(model, source?.Address ?? -1, source?.CopyCount ?? 0);
         WriteNewVoicegroup(model, token, address, bytes, songName, out anchorName);
         return true;
      }

      /// <summary>
      /// The voicegroup that a song file means by a symbol: the .s files of pokeemerald call it voicegroup_littleroot, and this ROM may name the voicegroup sound.voicegroups.littleroot.
      /// The symbol, or the symbol without its 'voicegroup_' / 'voicegroup' start, is compared with the names of the voicegroups (ignoring case). Null when none has that name.
      /// </summary>
      public static VoicegroupInfo FindByName(IEnumerable<VoicegroupInfo> voicegroups, string symbol) {
         if (string.IsNullOrWhiteSpace(symbol)) return null;
         var candidates = new List<string> { symbol };
         foreach (var prefix in new[] { "voicegroup_", "voicegroup" }) {
            if (symbol.Length > prefix.Length && symbol.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { candidates.Add(symbol.Substring(prefix.Length)); break; }
         }
         foreach (var candidate in candidates) {
            var found = voicegroups.FirstOrDefault(group => !group.IsCustom && group.Name != null && string.Equals(group.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
         }
         return null;
      }

      /// <summary>
      /// The 128 instruments of a new voicegroup that holds only the instruments a song uses: slot n is a copy of slot n of the source for every voice n in 'voices',
      /// and every other slot is the games' unused-instrument filler. So the song sounds exactly as it does with the source voicegroup, but it can be changed without touching anything else.
      /// A voice that the source does not have (past its end, or no source) stays filler and is added to 'missing'.
      /// </summary>
      public static byte[] BuildForVoices(IDataModel model, int sourceAddress, int sourceCount, IEnumerable<int> voices, List<int> missing = null) {
         var bytes = new byte[VoicegroupCatalog.Length];
         var filler = VoicegroupCatalog.FillerTone();
         for (int slot = 0; slot < VoicegroupCatalog.MaxEntries; slot++) Array.Copy(filler, 0, bytes, slot * VoicegroupCatalog.EntrySize, filler.Length);
         sourceCount = Math.Max(0, Math.Min(sourceCount, VoicegroupCatalog.MaxEntries));
         foreach (var voice in voices.Distinct().OrderBy(voice => voice)) {
            if (voice < 0 || voice >= VoicegroupCatalog.MaxEntries) continue;
            var sourceSlot = sourceAddress + voice * VoicegroupCatalog.EntrySize;
            if (sourceAddress < 0 || sourceSlot + VoicegroupCatalog.EntrySize > model.Count) { missing?.Add(voice); continue; }
            // a voice past the end of the voicegroup is read from whatever follows it, by the engine as well: it only counts as missing when that isn't an instrument either
            if (voice >= sourceCount && !ToneData.TryRead(model, sourceSlot, out _)) { missing?.Add(voice); continue; }
            for (int i = 0; i < VoicegroupCatalog.EntrySize; i++) bytes[voice * VoicegroupCatalog.EntrySize + i] = model[sourceSlot + i];
         }
         return bytes;
      }

      /// <summary>
      /// Puts a new 128-instrument voicegroup that holds the instruments 'voices' of 'source' (see BuildForVoices) into free space and gives it a named anchor.
      /// Nothing is changed when this returns false.
      /// </summary>
      public static bool TryCreateForVoices(IDataModel model, ModelDelta token, VoicegroupInfo source, IEnumerable<int> voices, string songName, out int address, out string anchorName, out IReadOnlyList<int> missingVoices, out string error) {
         anchorName = null;
         address = -1;
         missingVoices = Array.Empty<int>();
         voices = voices.Where(voice => voice >= 0 && voice < VoicegroupCatalog.MaxEntries).Distinct().ToList();
         var missing = new List<int>();
         var sourceCount = source?.IsCustom == true ? 0 : source?.CopyCount ?? 0;
         var bytes = BuildForVoices(model, source?.Address ?? -1, sourceCount, voices, missing);
         if (missing.Count > 0 && missing.Count == voices.Count()) {
            // nothing to carry over: a voicegroup of fillers would only make the song silent
            missingVoices = missing;
            error = "the voicegroup it was written for has none of the instruments it asks for";
            return false;
         }
         if (!TryAllocate(model, token, VoicegroupCatalog.Length, out address, out error)) return false;
         WriteNewVoicegroup(model, token, address, bytes, songName, out anchorName);
         missingVoices = missing;
         return true;
      }

      private static void WriteNewVoicegroup(IDataModel model, ModelDelta token, int address, byte[] bytes, string songName, out string anchorName) {
         token.ChangeData(model, address, bytes);
         anchorName = UniqueAnchorName(model, songName);
         var parse = ArrayRun.TryParse(model, VoicegroupCatalog.TableFormat, address, SortedSpan<int>.None, out var table);
         if (parse.HasError) {
            anchorName = null; // can't happen with a fixed format, but an unnamed voicegroup is still a working voicegroup
         } else {
            model.ObserveAnchorWritten(token, anchorName, table);
         }
      }

      /// <summary>Gives the voicegroup at an address a name in the metadata (without giving it a table format: its length isn't known for sure).</summary>
      public static void NameVoicegroup(IDataModel model, ModelDelta token, int address, string anchorName) {
         var existing = model.GetNextRun(address);
         IFormattedRun run = existing.Start == address ? existing : new NoInfoRun(address);
         model.ObserveAnchorWritten(token, anchorName, run);
      }

      /// <summary>
      /// Makes a song use another voicegroup: rewrites the pointer in its header (bytes 4-7) and keeps the editor's pointer bookkeeping in step.
      /// </summary>
      public static void AssignToSong(IDataModel model, ModelDelta token, int headerAddress, int voicegroupAddress) {
         var pointerAddress = headerAddress + 4;
         var existing = model.GetNextRun(pointerAddress);
         bool covered = existing.Start <= pointerAddress && pointerAddress < existing.Start + existing.Length;
         if (covered && existing is PointerRun && existing.Start == pointerAddress) {
            model.ClearFormat(token, pointerAddress, 4); // lets the old voicegroup forget this pointer
            covered = false;
         }
         model.WritePointer(token, pointerAddress, voicegroupAddress);
         if (!covered) model.ObserveRunWritten(token, new PointerRun(pointerAddress));
      }

      /// <summary>Copies one instrument (the 12 bytes, pointers included: samples stay shared) into a slot of a voicegroup.</summary>
      public static bool TryCopyTone(IDataModel model, ModelDelta token, int voicegroupAddress, int slot, int sourceToneAddress, out string error) {
         error = null;
         if (slot < 0 || slot >= VoicegroupCatalog.MaxEntries) { error = $"Slot {slot} is out of range (0-{VoicegroupCatalog.MaxEntries - 1})."; return false; }
         var destination = voicegroupAddress + slot * VoicegroupCatalog.EntrySize;
         if (voicegroupAddress < 0 || destination + VoicegroupCatalog.EntrySize > model.Count) { error = "The voicegroup is outside the ROM."; return false; }
         if (sourceToneAddress < 0 || sourceToneAddress + VoicegroupCatalog.EntrySize > model.Count) { error = "The instrument is outside the ROM."; return false; }
         var bytes = new byte[VoicegroupCatalog.EntrySize];
         for (int i = 0; i < bytes.Length; i++) bytes[i] = model[sourceToneAddress + i];
         token.ChangeData(model, destination, bytes);
         return true;
      }
   }
}
