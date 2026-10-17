using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Sound {
   public enum AllInstrumentsState {
      /// <summary>The ROM only has its own voicegroups.</summary>
      NotApplied,
      /// <summary>The ROM has the All Instruments voicegroup.</summary>
      Applied,
      /// <summary>A voicegroup is named all_instruments, but the data there does not look like instruments (the name is wrong, or the data was damaged).</summary>
      Unreadable,
   }

   /// <summary>What the Sound tab knows about the All Instruments patch of a ROM, and how it says it.</summary>
   public class AllInstrumentsStatus {
      public AllInstrumentsState State { get; init; }
      public bool IsApplied => State == AllInstrumentsState.Applied;

      /// <summary>Where the All Instruments voicegroup starts, or -1.</summary>
      public int Address { get; init; } = -1;

      /// <summary>The name of the anchor of the voicegroup (sound.voicegroups.all_instruments), or null when the ROM does not name it.</summary>
      public string AnchorName { get; init; }

      /// <summary>How many instruments the voicegroup has, counted from the first (a complete one has 128, one for each General MIDI program).</summary>
      public int InstrumentCount { get; init; }

      /// <summary>How many of those instruments are key splits (a different instrument for each part of the keyboard) and how many are drum kits.</summary>
      public int KeySplitCount { get; init; }
      public int DrumKitCount { get; init; }

      /// <summary>How many voicegroups were looked at, to say what was not found.</summary>
      public int VoicegroupsChecked { get; init; }

      /// <summary>The line the Sound tab shows under its description.</summary>
      public string Summary {
         get {
            switch (State) {
               case AllInstrumentsState.Applied:
                  var where = AnchorName != null ? "voicegroup " + AllInstrumentsPatch.VoicegroupName : $"voicegroup at {Address:X6}";
                  return $"All Instruments patch: applied ({InstrumentCount} {(InstrumentCount == 1 ? "instrument" : "instruments")} available, {where}) - MIDI and .s songs made for it will sound right.";
               case AllInstrumentsState.Unreadable:
                  return $"All Instruments patch: not usable - the voicegroup {AllInstrumentsPatch.VoicegroupName} at {Address:X6} does not look like a list of instruments, so it is not used.";
               default:
                  return "All Instruments patch: not applied (standard voicegroups only) - " + AllInstrumentsPatch.NoBankNote + ". MIDI songs are made with the voicegroup you choose below and may not sound as intended.";
            }
         }
      }

      /// <summary>The longer explanation, for a tooltip.</summary>
      public string Details {
         get {
            const string what = "The All Instruments patch adds a voicegroup with the 128 General MIDI instruments (most of them key splits that pick a different sample for each part of the keyboard) and two drum kits. ";
            switch (State) {
               case AllInstrumentsState.Applied:
                  return what + $"It starts at {Address:X6}{(AnchorName != null ? " (" + AnchorName + ")" : ", which this ROM does not give a name")}: {KeySplitCount} of its instruments are key splits and {DrumKitCount} are drum kits.";
               case AllInstrumentsState.Unreadable:
                  return what + $"This ROM has a voicegroup named {AnchorName} at {Address:X6}, but the data there does not start with valid instruments.";
               default:
                  return what + $"This ROM has none: no voicegroup is named {AllInstrumentsPatch.AnchorName}, and none of the {VoicegroupsChecked} voicegroups that were looked at has that layout.";
            }
         }
      }
   }

   /// <summary>
   /// Finds out whether a ROM has the All Instruments patch: the voicegroup that holds the 128 General MIDI instruments (anchored as sound.voicegroups.all_instruments, the way the CUBE metadata names it),
   /// which songs made with mid2agb -G_all_instruments and MIDI files are meant for.
   /// </summary>
   public static class AllInstrumentsPatch {
      public const string NoBankNote = "This ROM has no General MIDI instrument bank; pick one of the voicegroups of the game";

      public const string VoicegroupName = "all_instruments";

      /// <summary>The anchor the CUBE metadata gives the voicegroup.</summary>
      public const string AnchorName = VoicegroupCatalog.AnchorPrefix + VoicegroupName;

      /// <summary>
      /// A ROM that has the patch without names for it is still recognized: its main voicegroup has the 128 General MIDI instruments of which most are key splits, and a drum kit.
      /// An ordinary game has a handful of key splits in a voicegroup.
      /// </summary>
      public const int MinimumKeySplits = 64;

      /// <summary>
      /// Looks for the All Instruments voicegroup: by its name first, then (for ROMs that do not name their voicegroups) by what it looks like among the voicegroups that are known.
      /// </summary>
      /// <param name="model">The ROM.</param>
      /// <param name="knownVoicegroups">The voicegroups the Sound tab found (the ones songs use and the named ones). Optional: without them only the name is looked for.</param>
      public static AllInstrumentsStatus Inspect(IDataModel model, IEnumerable<VoicegroupInfo> knownVoicegroups = null) {
         var known = knownVoicegroups?.ToList() ?? new List<VoicegroupInfo>();
         var noChange = new NoDataChangeDeltaModel();
         string unreadableName = null;
         int unreadableAddress = -1;

         foreach (var anchor in model.Anchors) {
            if (!VoicegroupCatalog.IsVoicegroupAnchor(anchor, out var shortName)) continue;
            if (!string.Equals(shortName, VoicegroupName, StringComparison.OrdinalIgnoreCase)) continue;
            var address = model.GetAddressFromAnchor(noChange, -1, anchor);
            if (address < 0 || address >= model.Count) continue;
            var status = Read(model, address, anchor, known.Count);
            if (status.IsApplied) return status;
            (unreadableName, unreadableAddress) = (anchor, address);
         }

         foreach (var group in known) {
            if (group.IsCustom || group.EntryCount < VoicegroupCatalog.MaxEntries) continue;
            var status = Read(model, group.Address, null, known.Count);
            if (status.KeySplitCount >= MinimumKeySplits && status.DrumKitCount >= 1) return status;
         }

         if (unreadableName != null) return new AllInstrumentsStatus { State = AllInstrumentsState.Unreadable, Address = unreadableAddress, AnchorName = unreadableName, VoicegroupsChecked = known.Count };
         return new AllInstrumentsStatus { State = AllInstrumentsState.NotApplied, VoicegroupsChecked = known.Count };
      }

      private static AllInstrumentsStatus Read(IDataModel model, int address, string anchorName, int voicegroupsChecked) {
         int count = VoicegroupCatalog.CountEntries(model, address), keySplits = 0, drums = 0;
         for (int i = 0; i < count; i++) {
            var kind = ToneData.Read(model, address + i * VoicegroupCatalog.EntrySize).Kind;
            if (kind == ToneKind.Keysplit) keySplits++;
            if (kind == ToneKind.DrumKit) drums++;
         }
         return new AllInstrumentsStatus {
            State = count > 0 ? AllInstrumentsState.Applied : AllInstrumentsState.Unreadable,
            Address = address, AnchorName = anchorName, InstrumentCount = count, KeySplitCount = keySplits, DrumKitCount = drums, VoicegroupsChecked = voicegroupsChecked,
         };
      }
   }
}
