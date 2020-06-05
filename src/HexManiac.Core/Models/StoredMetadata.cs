using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace HavenSoft.HexManiac.Core.Models {
   public class StoredMetadata {
      public IReadOnlyList<StoredAnchor> NamedAnchors { get; }
      public IReadOnlyList<StoredUnmappedPointer> UnmappedPointers { get; }
      public IReadOnlyList<StoredMatchedWord> MatchedWords { get; }
      public IReadOnlyList<StoredList> Lists { get; }
      public string Version { get; }
      public int FreeSpaceSearch { get; } = -1;

      public bool IsEmpty => NamedAnchors.Count == 0 && UnmappedPointers.Count == 0;

      public StoredMetadata(IReadOnlyList<StoredAnchor> anchors, IReadOnlyList<StoredUnmappedPointer> unmappedPointers, IReadOnlyList<StoredMatchedWord> matchedWords, IReadOnlyList<StoredList> lists, IMetadataInfo generalInfo, int freeSpaceSearch) {
         NamedAnchors = anchors ?? new List<StoredAnchor>();
         UnmappedPointers = unmappedPointers ?? new List<StoredUnmappedPointer>();
         MatchedWords = matchedWords ?? new List<StoredMatchedWord>();
         Lists = lists ?? new List<StoredList>();
         Version = generalInfo.VersionNumber;
         FreeSpaceSearch = freeSpaceSearch;
      }

      public StoredMetadata(string[] lines) {
         var anchors = new List<StoredAnchor>();
         var pointers = new List<StoredUnmappedPointer>();
         var matchedWords = new List<StoredMatchedWord>();
         var lists = new List<StoredList>();

         foreach (var line in lines) {
            var cleanLine = line.Split('#').First().Trim();
            if (cleanLine == string.Empty) continue;
            if (cleanLine.StartsWith("[")) {
               CloseCurrentItem(anchors, pointers, matchedWords, lists);
               currentItem = cleanLine;
               continue;
            }

            if (cleanLine.StartsWith("]")) {
               continueCurrentItemIndex = false;
               continue;
            }

            if (continueCurrentItemIndex) {
               if (cleanLine.EndsWith(",")) cleanLine = cleanLine.Substring(0, cleanLine.Length - 1);
               currentItemChildren.Add(cleanLine.Replace("'''", string.Empty));
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

            if (cleanLine.StartsWith("ApplicationVersion = '''")) {
               Version = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("FreeSpaceSearch = '''")) {
               if (int.TryParse(cleanLine.Split("'''")[1], out var fss)) FreeSpaceSearch = fss;
            }

            if (cleanLine.Contains('=') && int.TryParse(cleanLine.Split('=')[0].Trim(), out int currentItemIndex)) {
               if (currentItemChildren == null) currentItemChildren = new List<string>();
               while (currentItemChildren.Count < currentItemIndex) currentItemChildren.Add(null);
               if (!cleanLine.Split("'''")[0].Contains("[")) {
                  // this is the only element, easy peasy
                  var elementStart = cleanLine.IndexOf("'''") + 3;
                  var elementLength = cleanLine.Substring(elementStart).IndexOf("'''");
                  if (elementStart > 3 && elementLength > 0) currentItemChildren.Add(cleanLine.Substring(elementStart, elementLength));
               } else if (cleanLine.EndsWith("]")) {
                  // this is a one line list
                  var lineList = cleanLine.Substring(cleanLine.IndexOf("[") + 1);
                  lineList = lineList.Substring(0, lineList.LastIndexOf("]"));
                  foreach (var element in lineList.Split(',')) currentItemChildren.Add(element.Replace("'''", string.Empty));
               } else if (cleanLine.EndsWith("[")) {
                  // this is a multiline list
                  continueCurrentItemIndex = true;
               }
            }
         }

         CloseCurrentItem(anchors, pointers, matchedWords, lists);

         NamedAnchors = anchors;
         UnmappedPointers = pointers;
         MatchedWords = matchedWords;
         Lists = lists;
      }

      public string[] Serialize() {
         var lines = new List<string> {
            "[General]"
         };
         if (Version != null) lines.Add($"ApplicationVersion = '''{Version}'''");
         lines.Add($"FreeSpaceSearch = '''{FreeSpaceSearch}'''");
         lines.Add(string.Empty);
         lines.Add("#################################");

         foreach (var anchor in NamedAnchors) {
            lines.Add("[[NamedAnchors]]");
            lines.Add($"Name = '''{anchor.Name}'''");
            lines.Add($"Address = 0x{anchor.Address:X6}");
            lines.Add($"Format = '''{anchor.Format}'''");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var pointer in UnmappedPointers) {
            lines.Add("[[UnmappedPointers]]");
            lines.Add($"Name = '''{pointer.Name}'''");
            lines.Add($"Address = 0x{pointer.Address:X6}");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var word in MatchedWords) {
            lines.Add("[[MatchedWords]]");
            lines.Add($"Name = '''{word.Name}'''");
            lines.Add($"Address = 0x{word.Address:X6}");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var list in Lists) {
            list.AppendContents(lines);
         }

         return lines.ToArray();
      }

      string currentItem, currentItemName, currentItemFormat;
      List<string> currentItemChildren;
      bool continueCurrentItemIndex;
      int currentItemAddress = -1;

      private void CloseCurrentItem(IList<StoredAnchor> anchors, IList<StoredUnmappedPointer> pointers, IList<StoredMatchedWord> matchedWords, IList<StoredList> lists) {
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
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a MatchedWords that didn't specify a name!");
            if (currentItemAddress == -1) throw new ArgumentOutOfRangeException("The Metadata file has an UnmappedPointer that didn't specify an Address!");
            matchedWords.Add(new StoredMatchedWord(currentItemAddress, currentItemName));
         }

         if (currentItem == "[[List]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a list that didn't specify a name!");
            if (currentItemChildren == null) throw new ArgumentNullException("The Metadata file has a list that didn't specify any children!");
            lists.Add(new StoredList(currentItemName, currentItemChildren));
         }

         currentItem = null;
         currentItemName = null;
         currentItemFormat = null;
         currentItemAddress = -1;
         continueCurrentItemIndex = false;
         currentItemChildren = null;
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

   /// <summary>
   /// An index of Contents being 'null' means sticking with the default value
   /// </summary>
   public class StoredList : IReadOnlyList<string> {
      public string Name { get; }
      public IReadOnlyList<string> Contents { get; }

      public StoredList(string name, IReadOnlyList<string> contents) => (Name, Contents) = (name, contents);

      public void AppendContents(IList<string> builder) {
         builder.Add("[[List]]");
         builder.Add($"Name = '''{Name}'''");
         for (int i = 0; i < Contents.Count; i++) {
            var prevNull = i == 0 || Contents[i - 1] == null;
            var nextNull = i == Contents.Count - 1 || Contents[i + 1] == null;

            if (Contents[i] != null) {
               if (prevNull && nextNull) {
                  builder.Add($"{i} = '''{Contents[i]}'''");
               } else if (prevNull && !nextNull) {
                  builder.Add($"{i} = [");
                  builder.Add($"   '''{Contents[i]}''',");
               } else if (!prevNull && !nextNull) {
                  builder.Add($"   '''{Contents[i]}''',");
               } else {
                  builder.Add($"   '''{Contents[i]}''',");
                  builder.Add($"]");
               }
            }
         }
         builder.Add(string.Empty);
      }

      #region IReadOnlyList members

      public int Count => Contents.Count;
      public string this[int index] => Contents[index] ?? index.ToString();
      public IEnumerator<string> GetEnumerator() => Contents.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => Contents.GetEnumerator();

      #endregion
   }
}
