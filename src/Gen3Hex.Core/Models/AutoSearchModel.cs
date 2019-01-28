using HavenSoft.Gen3Hex.Core.Models.Runs;
using System.Linq;
using static HavenSoft.Gen3Hex.Core.Models.Runs.ArrayRun;

namespace HavenSoft.Gen3Hex.Core.Models {
   public class AutoSearchModel : PokemonModel {
      public AutoSearchModel(byte[] data, StoredMetadata metadata = null) : base(data, metadata) {
         if (metadata != null) return;

         var noChangeDelta = new NoDataChangeDeltaModel();

         ClearFormat(noChangeDelta, -1, 0x101); // starting before the beginning to clear the anchor at the very start

         const string Ruby = "AXVE";
         const string Sapphire = "AXPE";
         const string Emerald = "BPEE";
         const string FireRed = "BPRE";
         const string LeafGreen = "BPGE";

         var gameCode = string.Concat(Enumerable.Range(0xAC, 4).Select(i => ((char)data[i]).ToString()));

         // in vanilla emerald, this pointer isn't four-byte aligned
         // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
         if (gameCode == Emerald && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

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
            }
         }
      }
   }
}
