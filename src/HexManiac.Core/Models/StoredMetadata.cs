using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public class StoredMetadataFields {
      public int FreeSpaceSearch { get; set; } = -1;
      public int FreeSpaceBuffer { get; set; } = -1;
      public int NextExportID { get; set; } = 0;
      public bool ShowRawIVByteForTrainer { get; set; } = false;
   }

   public class StoredMetadata {
      public IReadOnlyList<StoredAnchor> NamedAnchors { get; }
      public IReadOnlyList<StoredUnmappedPointer> UnmappedPointers { get; }
      public IReadOnlyList<StoredMatchedWord> MatchedWords { get; }
      public IReadOnlyList<StoredOffsetPointer> OffsetPointers { get; }
      public IReadOnlyList<StoredList> Lists { get; }
      public IReadOnlyList<StoredUnmappedConstant> UnmappedConstants { get; }
      public IReadOnlyList<StoredGotoShortcut> GotoShortcuts { get; }
      public IReadOnlyList<TableGroup> TableGroups { get; }
      public string Version { get; }
      public int NextExportID { get; }
      public int FreeSpaceSearch { get; } = -1;
      public int FreeSpaceBuffer { get; } = -1;
      public bool ShowRawIVByteForTrainer { get; } = false;

      public bool IsEmpty => NamedAnchors.Count == 0 && UnmappedPointers.Count == 0 && Lists.Count == 0 && TableGroups.Count == 0;

      // for backwards compatibility for tests
      public StoredMetadata(
         IReadOnlyList<StoredAnchor> anchors,
         IReadOnlyList<StoredUnmappedPointer> unmappedPointers,
         IReadOnlyList<StoredMatchedWord> matchedWords,
         IReadOnlyList<StoredOffsetPointer> offsetPointers,
         IReadOnlyList<StoredList> lists,
         IReadOnlyList<StoredUnmappedConstant> unmappedConstants,
         IMetadataInfo generalInfo,
         int freeSpaceSearch,
         int freeSpaceBuffer,
         int nextExportID
      ) : this(anchors, unmappedPointers, matchedWords, offsetPointers, lists, unmappedConstants, null, null, generalInfo, new StoredMetadataFields {
         FreeSpaceSearch = freeSpaceSearch,
         FreeSpaceBuffer = freeSpaceBuffer,
         NextExportID = nextExportID
      }) { }

      public StoredMetadata(
         IReadOnlyList<StoredAnchor> anchors,
         IReadOnlyList<StoredUnmappedPointer> unmappedPointers,
         IReadOnlyList<StoredMatchedWord> matchedWords,
         IReadOnlyList<StoredOffsetPointer> offsetPointers,
         IReadOnlyList<StoredList> lists,
         IReadOnlyList<StoredUnmappedConstant> unmappedConstants,
         IReadOnlyList<StoredGotoShortcut> gotoShortcuts,
         IMetadataInfo generalInfo,
         StoredMetadataFields fields
      ) : this(anchors, unmappedPointers, matchedWords, offsetPointers, lists, unmappedConstants, gotoShortcuts, null, generalInfo, fields) { }

      public StoredMetadata(
         IReadOnlyList<StoredAnchor> anchors = null,
         IReadOnlyList<StoredUnmappedPointer> unmappedPointers = null,
         IReadOnlyList<StoredMatchedWord> matchedWords = null,
         IReadOnlyList<StoredOffsetPointer> offsetPointers = null,
         IReadOnlyList<StoredList> lists = null,
         IReadOnlyList<StoredUnmappedConstant> unmappedConstants = null,
         IReadOnlyList<StoredGotoShortcut> gotoShortcuts = null,
         IReadOnlyList<TableGroup> tableGroups = null,
         IMetadataInfo generalInfo = null,
         StoredMetadataFields fields = null
      ) {
         NamedAnchors = anchors ?? new List<StoredAnchor>();
         UnmappedPointers = unmappedPointers ?? new List<StoredUnmappedPointer>();
         MatchedWords = matchedWords ?? new List<StoredMatchedWord>();
         OffsetPointers = offsetPointers ?? new List<StoredOffsetPointer>();
         Lists = lists ?? new List<StoredList>();
         UnmappedConstants = unmappedConstants ?? new List<StoredUnmappedConstant>();
         GotoShortcuts = gotoShortcuts ?? new List<StoredGotoShortcut>();
         TableGroups = tableGroups ?? new List<TableGroup>();
         Version = generalInfo?.VersionNumber;
         if (fields == null) fields = new StoredMetadataFields();
         FreeSpaceSearch = fields.FreeSpaceSearch;
         FreeSpaceBuffer = fields.FreeSpaceBuffer;
         NextExportID = fields.NextExportID;
         ShowRawIVByteForTrainer = fields.ShowRawIVByteForTrainer;
      }

      public StoredMetadata(string[] lines) {
         var anchors = new List<StoredAnchor>();
         var pointers = new List<StoredUnmappedPointer>();
         var matchedWords = new List<StoredMatchedWord>();
         var offsetPointers = new List<StoredOffsetPointer>();
         var lists = new List<StoredList>();
         var unmappedConstants = new List<StoredUnmappedConstant>();
         var gotoShortcuts = new List<StoredGotoShortcut>();
         var tableGroups = new List<TableGroup>();

         foreach (var line in lines) {
            var cleanLine = line.Split('#').First().Trim();
            if (cleanLine == string.Empty) continue;
            if (cleanLine.StartsWith("[")) {
               CloseCurrentItem(anchors, pointers, matchedWords, offsetPointers, lists, unmappedConstants, gotoShortcuts, tableGroups);
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

            if (cleanLine.StartsWith("Offset = 0x")) {
               var start = cleanLine.IndexOf("x") + 1;
               currentItemOffset = int.Parse(cleanLine.Substring(start), NumberStyles.HexNumber);
            } else if (cleanLine.StartsWith("Offset = ")) {
               var start = cleanLine.IndexOf(" = ") + 3;
               currentItemOffset = int.Parse(cleanLine.Substring(start));
            } else if (cleanLine.StartsWith("MultOffset = ")) {
               var start = cleanLine.IndexOf(" = ") + 3;
               currentItemMultOffset = int.Parse(cleanLine.Substring(start));
            }

            if (cleanLine.StartsWith("Length = ")) {
               var start = cleanLine.IndexOf(" = ") + 3;
               currentItemLength = int.Parse(cleanLine.Substring(start));
            }

            if (cleanLine.StartsWith("Name = '''")) {
               currentItemName = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("Value = 0x")) {
               var start = cleanLine.IndexOf("x") + 1;
               currentItemValue = int.Parse(cleanLine.Substring(start), NumberStyles.HexNumber);
            } else if (cleanLine.StartsWith("Value = ")) {
               var start = cleanLine.IndexOf(" = ") + 3;
               currentItemValue = int.Parse(cleanLine.Substring(start));
            }

            if (cleanLine.StartsWith("Format = '''")) {
               currentItemFormat = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("DefaultHash = '''")) {
               currentItemHash = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("Note = '''")) {
               currentItemNote = cleanLine.Split("'''")[1];
            } else if (cleanLine.StartsWith("Image = '''")) {
               currentItemImage = cleanLine.Split("'''")[1];
            } else if (cleanLine.StartsWith("Destination = '''")) {
               currentItemDestination = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("ApplicationVersion = '''")) {
               Version = cleanLine.Split("'''")[1];
            }

            if (cleanLine.StartsWith("NextExportID = '''")) {
               if (int.TryParse(cleanLine.Split("'''")[1], out var neid)) NextExportID = neid;
            }

            if (cleanLine.StartsWith("FreeSpaceSearch = '''")) {
               if (int.TryParse(cleanLine.Split("'''")[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var fss)) FreeSpaceSearch = fss;
            }

            if (cleanLine.StartsWith("FreeSpaceBuffer = '''")) {
               if (int.TryParse(cleanLine.Split("'''")[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var fsb)) FreeSpaceBuffer = fsb;
            }

            if (cleanLine.StartsWith("ShowRawIVByteForTrainer =")) {
               if (bool.TryParse(cleanLine.Split("=")[1].Trim(), out var showRawIV)) ShowRawIVByteForTrainer = showRawIV;
            }

            if (cleanLine.Contains('=') && cleanLine.Split('=')[0].Trim().TryParseInt(out int currentItemIndex)) {
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

         CloseCurrentItem(anchors, pointers, matchedWords, offsetPointers, lists, unmappedConstants, gotoShortcuts, tableGroups);

         NamedAnchors = anchors;
         UnmappedPointers = pointers;
         MatchedWords = matchedWords;
         OffsetPointers = offsetPointers;
         UnmappedConstants = unmappedConstants;
         Lists = lists;
         GotoShortcuts = gotoShortcuts;
         TableGroups = tableGroups;
      }

      /// <summary>
      /// Compares to version strings, in the format major.minor.update.release
      /// </summary>
      /// <returns>True if the previous verison is less than the current version.</returns>
      public static bool NeedVersionUpdate(string previousVersion, string currentVersion) {
         if (previousVersion == null && currentVersion != null) return true;

         if (previousVersion.StartsWith("v")) previousVersion = previousVersion.Substring(1);
         while (currentVersion.Contains("v")) currentVersion = currentVersion.Substring(1);

         while (previousVersion.Count(c => c == '.') < currentVersion.Count(c => c == '.')) previousVersion += ".0";
         while (currentVersion.Count(c => c == '.') < previousVersion.Count(c => c == '.')) currentVersion += ".0";

         var previousParts = previousVersion.Split('.');
         var currentParts = currentVersion.Split('.');

         for (int i = 0; i < previousParts.Length; i++) {
            if (!int.TryParse(previousParts[i], out int previous)) return false;
            if (!int.TryParse(currentParts[i], out int current)) return false;
            if (previous < current) return true;
            if (previous > current) return false;
         }

         return false;
      }

      public string[] Serialize() {
         var lines = new List<string> {
            "[General]"
         };
         if (Version != null) lines.Add($"ApplicationVersion = '''{Version}'''");
         lines.Add($"FreeSpaceSearch = '''{FreeSpaceSearch:X6}'''");
         lines.Add($"FreeSpaceBuffer = '''{FreeSpaceBuffer:X3}'''");
         lines.Add($"NextExportID = '''{NextExportID}'''");
         lines.Add($"ShowRawIVByteForTrainer = {ShowRawIVByteForTrainer}");
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
            lines.Add($"Length = {word.Length}");
            if (word.AddOffset != 0) lines.Add($"Offset = {word.AddOffset}");
            if (word.MultOffset != 1) lines.Add($"MultOffset = {word.MultOffset}");
            if (word.Note != null) lines.Add($"Note = '''{word.Note}'''");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var offsetPointer in OffsetPointers) {
            lines.Add("[[OffsetPointer]]");
            lines.Add($"Address = 0x{offsetPointer.Address:X6}");
            lines.Add($"Offset = 0x{offsetPointer.Offset:X6}");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var list in Lists) {
            list.AppendContents(lines);
         }

         lines.Add("#################################");

         foreach (var constant in UnmappedConstants) {
            lines.Add("[[UnmappedConstant]]");
            lines.Add($"Name = '''{constant.Name}'''");
            lines.Add($"Value = 0x{constant.Value:X8}");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var shortcut in GotoShortcuts) {
            lines.Add("[[GotoShortcut]]");
            lines.Add($"Name = '''{shortcut.Display}'''");
            lines.Add($"Image = '''{shortcut.Image}'''");
            lines.Add($"Destination = '''{shortcut.Anchor}'''");
            lines.Add(string.Empty);
         }

         lines.Add("#################################");

         foreach (var group in TableGroups) {
            lines.Add("[[TableGroup]]");
            lines.Add($"Name = '''{group.GroupName}'''");
            lines.Add($"DefaultHash = '''{group.Hash:X8}'''");
            lines.Add($"0 = [");
            foreach (var table in group.Tables) {
               lines.Add($"   '''{table}''',");
            }
            lines.Add($"]");
            lines.Add(string.Empty);
         }

         return lines.ToArray();
      }

      string currentItem, currentItemName, currentItemFormat, currentItemImage, currentItemDestination, currentItemHash;
      List<string> currentItemChildren;
      bool continueCurrentItemIndex;
      int currentItemLength = -1;
      int currentItemAddress = -1;
      int currentItemValue = -1;
      int currentItemOffset = int.MinValue;
      int currentItemMultOffset = 1;
      string currentItemNote = null;

      private void CloseCurrentItem(IList<StoredAnchor> anchors, IList<StoredUnmappedPointer> pointers, IList<StoredMatchedWord> matchedWords, IList<StoredOffsetPointer> offsetPointers, IList<StoredList> lists, IList<StoredUnmappedConstant> unmappedConstants, IList<StoredGotoShortcut> gotoShortcuts, IList<TableGroup> tableGroups) {
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
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a MatchedWords that didn't specify a Name!");
            if (currentItemAddress == -1) throw new ArgumentOutOfRangeException("The Metadata file has a MatchedWord that didn't specify an Address!");
            if (currentItemLength == -1) currentItemLength = 4;
            if (currentItemOffset == int.MinValue) currentItemOffset = 0;
            matchedWords.Add(new StoredMatchedWord(currentItemAddress, currentItemName, currentItemLength, currentItemOffset, currentItemMultOffset, currentItemNote));
         }

         if (currentItem == "[[OffsetPointer]]") {
            if (currentItemAddress == -1) throw new ArgumentOutOfRangeException("The Metadata file has an OffsetPointer that didn't specify an Address!");
            if (currentItemOffset == int.MinValue) throw new ArgumentOutOfRangeException("The Metadata file has an OffsetPointer that didn't specify an Offset!");
            offsetPointers.Add(new StoredOffsetPointer(currentItemAddress, currentItemOffset));
         }

         if (currentItem == "[[List]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a list that didn't specify a name!");
            if (currentItemChildren == null) throw new ArgumentNullException("The Metadata file has a list that didn't specify any children!");
            lists.Add(new StoredList(currentItemName, currentItemChildren, currentItemHash));
         }

         if (currentItem == "[[UnmappedConstant]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has an unmapped constant that didn't specify a name!");
            if (currentItemValue == -1) throw new ArgumentNullException("The Metadata file has an unmapped constant that didn't specify a value!");
            unmappedConstants.Add(new StoredUnmappedConstant(currentItemName, currentItemValue));
         }

         if (currentItem == "[[GotoShortcut]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a Goto Shortcut that didn't specify a Name!");
            if (currentItemImage == null) throw new ArgumentNullException("The Metadata file has a Goto Shortcut that didn't specify an Image!");
            if (currentItemDestination == null) throw new ArgumentNullException("The Metadata file has a Goto Shortcut that didn't specify a Destination!");
            gotoShortcuts.Add(new StoredGotoShortcut(currentItemName, currentItemImage, currentItemDestination));
         }

         if (currentItem == "[[TableGroup]]") {
            if (currentItemName == null) throw new ArgumentNullException("The Metadata file has a TableGroup that didn't specify a name!");
            if (currentItemChildren == null) throw new ArgumentNullException("The Metadat file has a TableGroup that didn't specify any children!");
            tableGroups.Add(new(currentItemName, currentItemChildren, currentItemHash));
         }

         currentItem = null;
         currentItemName = null;
         currentItemFormat = null;
         currentItemAddress = -1;
         currentItemValue = -1;
         currentItemOffset = int.MinValue;
         currentItemMultOffset = 1;
         currentItemLength = -1;
         continueCurrentItemIndex = false;
         currentItemChildren = null;
         currentItemNote = null;
         currentItemImage = null;
         currentItemDestination = null;
         currentItemHash = null;
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

   public class StoredUnmappedConstant {
      public string Name { get; }
      public int Value { get; }

      public StoredUnmappedConstant(string name, int value) => (Name, Value) = (name, value);
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
      public int Length { get; }
      public int AddOffset { get; }
      public int MultOffset { get; }
      public string Note { get; }
      public StoredMatchedWord(int address, string name, int length, int offset, int multOffset, string note) {
         Debug.Assert(multOffset > 0, "MultOffset must be positive.");
         (Address, Name, Length, AddOffset, MultOffset, Note) = (address, name, length, offset, multOffset, note);
      }
   }

   public class StoredOffsetPointer {
      public int Address { get; }
      public int Offset { get; }
      public StoredOffsetPointer(int address, int offset) => (Address, Offset) = (address, offset);
   }

   /// <summary>
   /// An index of Contents being 'null' means sticking with the default value
   /// </summary>
   public class StoredList : IReadOnlyList<string> {
      public string Name { get; }
      public IReadOnlyList<string> Contents { get; }
      public string Hash { get; }
      public bool HashMatches => Hash == GenerateHash(Contents);

      public StoredList(string name, IReadOnlyList<string> contents, string hash = null) {
         if (hash == null) hash = GenerateHash(contents);
         (Name, Hash, Contents) = (name, hash, contents);
      }

      public void AppendContents(IList<string> builder) {
         builder.Add("[[List]]");
         builder.Add($"Name = '''{Name}'''");
         if (Hash != null) builder.Add($"DefaultHash = '''{Hash}'''");
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

      public static string GenerateHash(IEnumerable<string> items, uint seed = 0xCafeBabe) {
         var hash = seed;
         var sha = System.Security.Cryptography.SHA256.Create();
         foreach (var item in items) {
            hash = (hash << 3) | (hash >> 29);
            if (item == null) continue;
            var encoded = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(item));
            hash ^= BitConverter.ToUInt32(encoded);
         }
         return hash.ToString("X8");
      }
   }

   public class StoredGotoShortcut {
      public string Display { get; } // Name
      public string Image { get; }   // Image
      public string Anchor { get; }  // Destination
      public StoredGotoShortcut(string name, string image, string destination) => (Display, Image, Anchor) = (name, image, destination);
   }

   public class ValidationList : List<string> {
      public string StoredHash { get; }
      public ValidationList(string hash) => StoredHash = hash;
      public ValidationList(string hash, IEnumerable<string> content) : base(content) => StoredHash = hash;
      public bool StoredHashMatches => StoredHash == StoredList.GenerateHash(this);
   }
}
