using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public interface ISearchByte {
      bool Match(byte value);
   }
   public class SearchByte : ISearchByte {
      private readonly byte value;
      public static ISearchByte Wild { get; } = new WildSearchByte();
      public SearchByte(int value) => this.value = (byte)value;
      public static explicit operator SearchByte(byte value) => new SearchByte(value);
      public bool Match(byte value) => value == this.value;
   }

   public class WildSearchByte : ISearchByte {
      public bool Match(byte value) => true;
   }

   public class PCSSearchByte : ISearchByte {
      private static readonly int CapitalEWithAccent = PCSString.PCS.IndexOf("É");
      private static readonly int LowerEWithAccent = PCSString.PCS.IndexOf("é");
      private static readonly int CapitalE = PCSString.PCS.IndexOf("E");
      private static readonly int LowerE = PCSString.PCS.IndexOf("e");
      private readonly byte match1, match2;
      public static ISearchByte Create(byte value, bool matchExactCase) {
         if (matchExactCase) {
            return new ExactMatchSearchByte(value);
         } else if (value == CapitalE || value == CapitalEWithAccent || value == LowerE || value == LowerEWithAccent) {
            return MatchESearchByte.Instance;
         } else {
            return new PCSSearchByte(value);
         }
      }

      public PCSSearchByte(int value) {
         match1 = (byte)value;
         match2 = match1;
         if (PCSString.PCS[match1] == null) return;
         var valueAsChar = PCSString.PCS[match1][0];
         if (char.IsUpper(valueAsChar)) {
            Debug.Assert(IndexOf(PCSString.PCS, "a") - IndexOf(PCSString.PCS, "A") == 0x1A);
            match2 += 0x1A;
         }
      }

      public bool Match(byte value) => value == match1 || value == match2;
      private static int IndexOf(IReadOnlyList<string> pcs, string value) => 0x100.Range().Single(i => pcs[i] == value);

      private class MatchESearchByte : ISearchByte {
         public static ISearchByte Instance { get; } = new MatchESearchByte();
         private MatchESearchByte() { }
         public bool Match(byte value) => value == CapitalE || value == CapitalEWithAccent || value == LowerE || value == LowerEWithAccent;
      }

      private class ExactMatchSearchByte : ISearchByte {
         private byte value;
         public ExactMatchSearchByte(byte value) => this.value = value;
         public bool Match(byte value) => value == this.value;
      }
   }
}
