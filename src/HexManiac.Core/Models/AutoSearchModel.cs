using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;

namespace HavenSoft.HexManiac.Core.Models {
   public class AutoSearchModel : PokemonModel {
      public const string
         Ruby = "AXVE",
         Sapphire = "AXPE",
         Emerald = "BPEE",
         FireRed = "BPRE",
         LeafGreen = "BPGE";

      public const string
         TmMoves = "tmmoves",
         TmCompatibility = "tmcompatibility",
         MoveTutors = "tutormoves",
         TutorCompatibility = "tutorcompatibility";

      private readonly string gameCode;
      private readonly ModelDelta noChangeDelta = new NoDataChangeDeltaModel();

      /// <summary>
      /// The first 0x100 bytes of the GBA rom is always the header.
      /// The next 0x100 bytes contains some tables and some startup code, but nothing interesting to point to.
      /// Choosing 0x200 might prevent us from seeing an actual anchor, but it will also remove a bunch
      ///      of false positives and keep us from getting conflicts with the RomName (see DecodeHeader).
      /// </summary>
      public override int EarliestAllowedAnchor => 0x200;

      public AutoSearchModel(byte[] data, StoredMetadata metadata = null) : base(data, metadata) {
         if (metadata != null && !metadata.IsEmpty) return;

         gameCode = string.Concat(Enumerable.Range(0xAC, 4).Select(i => ((char)data[i]).ToString()));

         // in vanilla emerald, this pointer isn't four-byte aligned
         // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
         if (gameCode == Emerald && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

         var gamesToDecode = new[] { Ruby, Sapphire, Emerald, FireRed, LeafGreen };
         if (gamesToDecode.Contains(gameCode)) {
            DecodeHeader();
            DecodeNameArrays();
            DecodeDataArrays();
            DecodeStreams();
         }

         ResolveConflicts();
      }

      private void DecodeHeader() {
         ObserveAnchorWritten(noChangeDelta, "GameTitle", new AsciiRun(0xA0, 12));
         ObserveAnchorWritten(noChangeDelta, "GameCode", new AsciiRun(0xAC, 4));
         ObserveAnchorWritten(noChangeDelta, "MakerCode", new AsciiRun(0xB0, 2));

         if (gameCode != Ruby && gameCode != Sapphire) {
            ObserveAnchorWritten(noChangeDelta, "RomName", new AsciiRun(0x108, 0x20));
         }
      }

      private void DecodeNameArrays() {
         // movenames
         if (TrySearch(this, noChangeDelta, "[name\"\"13]", out var movenames)) {
            ObserveAnchorWritten(noChangeDelta, EggMoveRun.MoveNamesTable, movenames);
         }

         // pokenames
         if (TrySearch(this, noChangeDelta, "[name\"\"11]", out var pokenames)) {
            ObserveAnchorWritten(noChangeDelta, EggMoveRun.PokemonNameTable, pokenames);
         }

         // abilitynames / trainer names
         if (gameCode == Ruby || gameCode == Sapphire || gameCode == Emerald) {
            if (TrySearch(this, noChangeDelta, "[name\"\"13]", out var abilitynames, run => run.PointerSources.FirstOrDefault() < 0x100000)) {
               ObserveAnchorWritten(noChangeDelta, "abilitynames", abilitynames);
            }
            if (TrySearch(this, noChangeDelta, "[name\"\"13]", out var trainerclassnames)) {
               ObserveAnchorWritten(noChangeDelta, "trainerclassnames", trainerclassnames);
            }
         } else {
            if (TrySearch(this, noChangeDelta, "[name\"\"13]", out var trainerclassnames, run => run.PointerSources.Count > 1 && run.PointerSources.Count < 4)) {
               ObserveAnchorWritten(noChangeDelta, "trainerclassnames", trainerclassnames);
            }
            if (TrySearch(this, noChangeDelta, "[name\"\"13]", out var abilitynames, run => run.PointerSources.FirstOrDefault() < 0x100000)) {
               ObserveAnchorWritten(noChangeDelta, "abilitynames", abilitynames);
            }
         }

         // types
         if (TrySearch(this, noChangeDelta, "^[name\"\"7]", out var typenames)) { // the type names are sometimes pointed to directly, instead of in the array
            ObserveAnchorWritten(noChangeDelta, "types", typenames);
         }
      }

      private void DecodeDataArrays() {
         if (TrySearch(this, noChangeDelta, $"[name\"\"14 index: price: holdeffect: description<{PCSRun.SharedFormatString}> keyitemvalue. bagkeyitem. pocket. type. fieldeffect<> battleusage:: battleeffect<> battleextra::]", out var itemdata)) {
            ObserveAnchorWritten(noChangeDelta, "items", itemdata);
            FindItemImages(itemdata);
         }

         // if the stat data doesn't match the pokenames length, use whichever is shorter.
         var format = "[hp. attack. def. speed. spatk. spdef. type1.types type2.types catchRate. baseExp. evs: item1:items item2:items genderratio. steps2hatch. basehappiness. growthrate. egg1. egg2. ability1.abilitynames ability2.abilitynames runrate. unknown. padding:]pokenames";
         var pokenames = GetNextRun(GetAddressFromAnchor(noChangeDelta, -1, EggMoveRun.PokemonNameTable)) as ArrayRun;
         if (pokenames != null && TrySearch(this, noChangeDelta, format, out var pokestatdata, run => run.PointerSources.Count > 5)) {
            if (pokestatdata.ElementCount < pokenames.ElementCount) {
               pokenames = pokenames.Append(pokestatdata.ElementCount - pokenames.ElementCount);
               ObserveAnchorWritten(noChangeDelta, EggMoveRun.PokemonNameTable, pokenames);
            } else if (pokestatdata.ElementCount > pokenames.ElementCount) {
               pokestatdata = pokestatdata.Append(pokenames.ElementCount - pokestatdata.ElementCount);
            }

            ObserveAnchorWritten(noChangeDelta, "pokestats", pokestatdata);
         }

         // the first abilityDescriptions pointer is directly after the first abilityNames pointer
         var abilityNamesAddress = GetAddressFromAnchor(noChangeDelta, -1, "abilitynames");
         if (abilityNamesAddress != Pointer.NULL) {
            var firstPointerToAbilityNames = GetNextAnchor(abilityNamesAddress).PointerSources?.FirstOrDefault() ?? Pointer.NULL;
            if (firstPointerToAbilityNames != Pointer.NULL) {
               var firstPointerToAbilityDescriptions = firstPointerToAbilityNames + 4;
               var abilityDescriptionsAddress = ReadPointer(firstPointerToAbilityDescriptions);
               var existingRun = GetNextAnchor(abilityDescriptionsAddress);
               if (!(existingRun is ArrayRun) && existingRun.Start == abilityDescriptionsAddress) {
                  var error = TryParse(this, $"[description<{PCSRun.SharedFormatString}>]abilitynames", existingRun.Start, existingRun.PointerSources, out var abilityDescriptions);
                  if (!error.HasError) ObserveAnchorWritten(noChangeDelta, "abilitydescriptions", abilityDescriptions);
               }
            }
         }

         if (TrySearch(this, noChangeDelta, "[effect. power. type.types accuracy. pp. effectAccuracy. target. priority. more::]" + EggMoveRun.MoveNamesTable, out var movedata, run => run.PointerSources.Count > 100)) {
            ObserveAnchorWritten(noChangeDelta, "movedata", movedata);
         }

         if (TrySearch(this, noChangeDelta, $"[moves<{PLMRun.SharedFormatString}>]" + EggMoveRun.PokemonNameTable, out var lvlMoveData)) {
            ObserveAnchorWritten(noChangeDelta, "lvlmoves", lvlMoveData);
         }

         FindTutorMoveAnchors();
         FindTmMoveAnchors();

         // @3D4294 ^itemicons[image<> palette<>]items
         // @4886E8 ^movedescriptions[description<>]354 <- note that there is no description for move 0
      }

      private void FindTutorMoveAnchors() {
         int tutorMoves, tutorCompatibility;
         if (gameCode == FireRed || gameCode == LeafGreen) {
            var originalCode = new byte[] {
               0x00, 0xB5, 0x00, 0x04, 0x00, 0x0C, 0x09, 0x06,
               0x0A, 0x0E, 0x10, 0x2A, 0x0A, 0xD0, 0x10, 0x2A,  // This is the thumb code for the tutor compatibility check.
               0x02, 0xDC, 0x0F, 0x2A, 0x03, 0xD0, 0x0B, 0xE0,  // It's the exact same in both FireRed and LeafGreen, but in different places.
               0x11, 0x2A, 0x06, 0xD0, 0x08, 0xE0, 0x03, 0x28,  // If I find this not tampered with, then it's probably the original.
               0x14, 0xD0, 0x0E, 0xE0, 0x06, 0x28, 0x11, 0xD0,  // More so, it's bookended by two pointers.
               0x0B, 0xE0, 0x09, 0x28, 0x0E, 0xD0, 0x08, 0xE0,  // Directly before this is tutormoves.
               0x05, 0x49, 0x40, 0x00, 0x40, 0x18, 0x00, 0x88,  // Directly after this is tutorcompatibility.
               0x10, 0x41, 0x01, 0x21, 0x08, 0x40, 0x00, 0x28,
               0x04, 0xD1, 0x00, 0x20, 0x03, 0xE0, 0x00, 0x00,
            };

            var list = Find(originalCode, 0x120B00, 0x120C00);
            if (list.Count == 0) return;
            tutorMoves = ReadPointer(list[0] - 4);
            tutorCompatibility = ReadPointer(list[0] + originalCode.Length);
            if (tutorMoves < 0 || tutorMoves > Count || tutorCompatibility < 0 || tutorCompatibility > Count) return;
            if (list.Count != 1) return;
            if (!TryParse(this, $"[move:{EggMoveRun.MoveNamesTable}]15", tutorMoves, null, out var tutorMovesRun).HasError) {
               ObserveAnchorWritten(noChangeDelta, MoveTutors, tutorMovesRun);
               if (!TryParse(this, $"[pokemon|b[]{MoveTutors}]{EggMoveRun.PokemonNameTable}", tutorCompatibility, null, out var tutorCompatibilityRun).HasError) {
                  ObserveAnchorWritten(noChangeDelta, TutorCompatibility, tutorCompatibilityRun);
               }
            }
         } else if (gameCode == Emerald) {
            // these two addresses are supposed to have pointers to tutormoves / tutorcompatibility
            tutorMoves = ReadPointer(0x1B236C);
            tutorCompatibility = ReadPointer(0x1B2390);
            if (tutorMoves < 0 || tutorMoves > Count || tutorCompatibility < 0 || tutorCompatibility > Count) return;
            if (!TryParse(this, $"[move:{EggMoveRun.MoveNamesTable}]30", tutorMoves, null, out var tutorMovesRun).HasError) {
               ObserveAnchorWritten(noChangeDelta, MoveTutors, tutorMovesRun);
               if (!TryParse(this, $"[pokemon|b[]{MoveTutors}]{EggMoveRun.PokemonNameTable}", tutorCompatibility, null, out var tutorCompatibilityRun).HasError) {
                  ObserveAnchorWritten(noChangeDelta, TutorCompatibility, tutorCompatibilityRun);
               }
            }
         }
      }

      private void FindTmMoveAnchors() {
         // get tmCompatibility location
         var originalCode = new byte[] {
            0x00, 0x04, 0x01, 0x0C, 0x0B, 0x1C, 0xCE, 0x20,
            0x40, 0x00, 0x81, 0x42, 0x01, 0xD1, 0x00, 0x20,
            0x15, 0xE0, 0x1F, 0x2C, 0x0C, 0xD9, 0x20, 0x1C,
            0x20, 0x38, 0x01, 0x22, 0x82, 0x40, 0x03, 0x48,
            0xC9, 0x00, 0x04, 0x30, 0x09, 0x18, 0x08, 0x68,
            0x10, 0x40, 0x08, 0xE0,
         };
         var list = Find(originalCode, 0x40000, 0x70000);
         if (list.Count != 1) return;
         var tmCompatibility = ReadPointer(list[0] + originalCode.Length);
         if (tmCompatibility < 0 || tmCompatibility > Count) return;

         // get tmMoves location
         originalCode = new byte[] {
            0x00, 0xB5, 0x00, 0x04, 0x02, 0x0C, 0x00, 0x21,
            0x04, 0x4B, 0x08, 0x1C, 0x32, 0x30, 0x40, 0x00,
            0xC0, 0x18, 0x00, 0x88, 0x90, 0x42, 0x03, 0xD1,
            0x01, 0x20, 0x07, 0xE0,
         };
         list = Find(originalCode, 0x06F700, 0x1B7000);
         if (list.Count != 1) return;
         var tmMoves = ReadPointer(list[0] + originalCode.Length);
         if (tmMoves< 0 || tmMoves > Count) return;

         // add tm locations into the metadata
         if (!TryParse(this, $"[move:{EggMoveRun.MoveNamesTable}]58", tmMoves, null, out var tmMovesRun).HasError) {
            ObserveAnchorWritten(noChangeDelta, TmMoves, tmMovesRun);
            if (!TryParse(this, $"[pokemon|b[]{TmMoves}]{EggMoveRun.PokemonNameTable}", tmCompatibility, null, out var tmCompatibilityRun).HasError) {
               ObserveAnchorWritten(noChangeDelta, TmCompatibility, tmCompatibilityRun);
            }
         }
      }

      private void FindItemImages(ArrayRun itemsTable) {
         var originalCode = new byte[] {
            0x88, 0x00, 0xD9, 0x00, 0x40, 0x18, 0x80, 0x18,
            0x00, 0x68, 0x02, 0xBC, 0x08, 0x47, 0x00, 0x00,
         };
         var list = Find(originalCode, 0x98950, 0x1B0050);
         if (list.Count != 1) return;

         var lengthOffset = gameCode == Emerald ? -0x10 : originalCode.Length;
         var pointerOffset = gameCode == Emerald ? originalCode.Length : originalCode.Length + 4;

         var imagesStart = ReadPointer(list[0] + pointerOffset);
         if (!TryParse(this, $"[image<> palette<>]items", imagesStart, null, out var itemImages).HasError) {
            ObserveAnchorWritten(noChangeDelta, "itemimages", itemImages);
         }

         // TODO @{lengthOffset:X6} ::itemimages-1
      }

      private IList<int> Find(byte[] subset, int start, int end) {
         var results = new List<int>();
         for (int i = start; i < end; i++) {
            bool match = Enumerable.Range(0, subset.Length).All(j => this[i + j] == subset[j]);
            if (match) results.Add(i);
         }
         return results;
      }

      private void DecodeStreams() {
         if (EggMoveRun.TrySearch(this, noChangeDelta, out var eggmoves)) {
            ObserveAnchorWritten(noChangeDelta, "eggmoves", eggmoves);
         }
      }
   }
}
