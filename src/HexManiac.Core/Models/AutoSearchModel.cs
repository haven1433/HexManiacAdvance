using HavenSoft.HexManiac.Core.Models.Runs;
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
         if (metadata != null) return;

         gameCode = string.Concat(Enumerable.Range(0xAC, 4).Select(i => ((char)data[i]).ToString()));

         // in vanilla emerald, this pointer isn't four-byte aligned
         // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
         if (gameCode == Emerald && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

         var gamesToDecode = new[] { Ruby, Sapphire, Emerald, FireRed, LeafGreen };
         if (gamesToDecode.Contains(gameCode)) {
            DecodeHeader();
            DecodeNameArrays();
            DecodeDataArrays();
         }
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
         // pokenames
         if (TrySearch(this, "[name\"\"11]", out var pokenames)) {
            ObserveAnchorWritten(noChangeDelta, "pokenames", pokenames);
         }

         // movenames
         if (TrySearch(this, "[name\"\"13]", out var movenames)) {
            ObserveAnchorWritten(noChangeDelta, "movenames", movenames);
         }

         // abilitynames / trainer names
         if (gameCode == Ruby || gameCode == Sapphire || gameCode == Emerald) {
            if (TrySearch(this, "[name\"\"13]", out var abilitynames)) {
               ObserveAnchorWritten(noChangeDelta, "abilitynames", abilitynames);
            }
            if (TrySearch(this, "[name\"\"13]", out var trainerclassnames)) {
               ObserveAnchorWritten(noChangeDelta, "trainerclassnames", trainerclassnames);
            }
         } else {
            if (TrySearch(this, "[name\"\"13]", out var trainerclassnames)) {
               ObserveAnchorWritten(noChangeDelta, "trainerclassnames", trainerclassnames);
            }
            if (TrySearch(this, "[name\"\"13]", out var abilitynames)) {
               ObserveAnchorWritten(noChangeDelta, "abilitynames", abilitynames);
               if (gameCode == FireRed) Debug.Assert(abilitynames.ElementCount == 78);
            }
         }

         // types[name""7]18
         // this one is weird, because things actually point to each individual name, as well as being in an array
      }

      private void DecodeDataArrays() {
         // abilitydescriptions[description<"">]abilitynames

         if (TrySearch(this, "[name\"\"14 a: b: c: d<> e: f: g<> h: i: j<> k: l:]", out var itemdata)) {
            ObserveAnchorWritten(noChangeDelta, "items", itemdata);
         }
      }
   }
}
