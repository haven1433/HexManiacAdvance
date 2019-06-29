using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public class StoredMetadata {
      public IReadOnlyList<StoredAnchor> NamedAnchors { get; }
      public IReadOnlyList<StoredUnmappedPointer> UnmappedPointers { get; }
      public IReadOnlyList<StoredMatchedWord> MatchedWords { get; }

      public bool IsEmpty => NamedAnchors.Count == 0 && UnmappedPointers.Count == 0;

      public StoredMetadata(IReadOnlyList<StoredAnchor> anchors, IReadOnlyList<StoredUnmappedPointer> unmappedPointers, IReadOnlyList<StoredMatchedWord> matchedWords) {
         NamedAnchors = anchors;
         UnmappedPointers = unmappedPointers;
         MatchedWords = matchedWords;
      }

      public StoredMetadata(string[] lines) {
         var anchors = new List<StoredAnchor>();
         var pointers = new List<StoredUnmappedPointer>();
         var matchedWords = new List<StoredMatchedWord>();

         foreach (var line in lines) {
            var cleanLine = line.Split('#').First().Trim();
            if (cleanLine == string.Empty) continue;
            if (cleanLine.StartsWith("[")) {
               CloseCurrentItem(anchors, pointers, matchedWords);
               currentItem = cleanLine;
               continue;
            }

            if (cleanLine.StartsWith("Address = 0x")) {
               var start = cleanLine.IndexOf("x") + 1;
               currentItemAddress = int.Parse(cleanLine.Substring(start), NumberStyles.HexNumber);
            }

            if (cleanLine.StartsWith("Name = '''")) {
               currentItemName = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("Format = '''")) {
               currentItemFormat = cleanLine.Split("'''")[1];
            }
         }

         CloseCurrentItem(anchors, pointers, matchedWords);

         NamedAnchors = anchors;
         UnmappedPointers = pointers;
         MatchedWords = matchedWords;
      }

      public string[] Serialize() {
         var lines = new List<string>();

         foreach (var anchor in NamedAnchors) {
            lines.Add("[[NamedAnchors]]");
            lines.Add($"Name = '''{anchor.Name}'''");
            lines.Add($"Address = 0x{anchor.Address.ToString("X6")}");
            lines.Add($"Format = '''{anchor.Format}'''");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var pointer in UnmappedPointers) {
            lines.Add("[[UnmappedPointers]]");
            lines.Add($"Name = '''{pointer.Name}'''");
            lines.Add($"Address = 0x{pointer.Address.ToString("X6")}");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var word in MatchedWords) {
            lines.Add("[[MatchedWords]]");
            lines.Add($"Name = '''{word.Name}'''");
            lines.Add($"Address = 0x{word.Address.ToString("X6")}");
            lines.Add(string.Empty);
         }

         return lines.ToArray();
      }

      string currentItem, currentItemName, currentItemFormat;
      int currentItemAddress = -1;

      private void CloseCurrentItem(IList<StoredAnchor> anchors, IList<StoredUnmappedPointer> pointers, IList<StoredMatchedWord> matchedWords) {
         if (currentItem == "[[UnmappedPointers]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has an UnmappedPointer that didn't specify a name!");
            if (currentItemAddress == -1) throw new ArgumentOutOfRangeException("The Metadata file has an UnmappedPointer that didn't specify an Address!");
            pointers.Add(new StoredUnmappedPointer(currentItemAddress, currentItemName));
         }

         if (currentItem == "[[NamedAnchors]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a NamedAnchor that didn't specify a name!");
            if (currentItemAddress == -1) throw new ArgumentOutOfRangeException("The Metadata file has a NamedAnchor that didn't specify an Address!");
            anchors.Add(new StoredAnchor(currentItemAddress, currentItemName, currentItemFormat));
         }

         if (currentItem == "[[MatchedWords]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has an UnmappedPointer that didn't specify a name!");
            if (currentItemAddress == -1) throw new ArgumentOutOfRangeException("The Metadata file has an UnmappedPointer that didn't specify an Address!");
            matchedWords.Add(new StoredMatchedWord(currentItemAddress, currentItemName));
         }

         currentItem = null;
         currentItemName = null;
         currentItemFormat = null;
         currentItemAddress = -1;
      }
   }

   public class StoredUnmappedPointer {
      public int Address { get; }
      public string Name { get; }

      public StoredUnmappedPointer(int address, string name) {
         Address = address;
         Name = name;
      }
   }

   public class StoredAnchor {
      public int Address { get; }
      public string Name { get; }
      public string Format { get; }

      public StoredAnchor(int address, string name, string format) {
         Address = address;
         Name = name;
         Format = format;
      }
   }

   public class StoredMatchedWord {
      public int Address { get; }
      public string Name { get; }
      public StoredMatchedWord(int address, string name) => (Address, Name) = (address, name);
   }
}
