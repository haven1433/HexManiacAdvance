using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.AsciiRun;
using static HavenSoft.HexManiac.Core.Models.Runs.BaseRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;

namespace HavenSoft.HexManiac.Core.Models {
   public class PokemonModel : BaseModel {
      // list of runs, in sorted address order. Includes no names
      private readonly List<IFormattedRun> runs = new List<IFormattedRun>();

      // for a name, where is it?
      // for a location, what is its name?
      private readonly Dictionary<string, int> addressForAnchor = new Dictionary<string, int>();
      private readonly Dictionary<int, string> anchorForAddress = new Dictionary<int, string>();

      // for a name not actually in the file, what pointers point to it?
      // for a pointer pointing to something not actually in the file, what name is it pointing to?
      private readonly Dictionary<string, SortedSpan<int>> unmappedNameToSources = new Dictionary<string, SortedSpan<int>>();
      private readonly Dictionary<int, string> sourceToUnmappedName = new Dictionary<int, string>();

      // for a name of a table (which may not actually be in the file),
      // get the list of addresses in the file that want to store a number that matches the length of the table.
      private readonly Dictionary<string, List<int>> matchedWords = new Dictionary<string, List<int>>();

      // a list of all the offsets for all known offset pointers. This information is duplicated in the OffsetPointerRun.
      private readonly Dictionary<int, int> pointerOffsets = new Dictionary<int, int>();

      private readonly Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>();

      private readonly Singletons singletons;

      public virtual int EarliestAllowedAnchor => 0;

      public override IReadOnlyList<string> ListNames => lists.Keys.ToList();
      public override IReadOnlyList<ArrayRun> Arrays {
         get {
            var results = new List<ArrayRun>();
            foreach (var address in anchorForAddress.Keys) {
               var index = BinarySearch(address);
               if (index < 0) continue;
               if (runs[index] is ArrayRun arrayRun) results.Add(arrayRun);
            }
            return results;
         }
      }
      public override IEnumerable<T> All<T>() {
         foreach (var run in runs) {
            if (run is T t) yield return t;
         }
      }
      public override IReadOnlyList<IStreamRun> Streams => runs.Where(run => run is IStreamRun).Select(run => (IStreamRun)run).ToList();
      public override IReadOnlyList<string> Anchors => addressForAnchor.Keys.ToList();

      #region Constructor

      public PokemonModel(byte[] data, StoredMetadata metadata = null, Singletons singletons = null) : base(data) {
         this.singletons = singletons;
         Initialize(metadata);
      }

      private void Initialize(StoredMetadata metadata) {
         var pointersForDestination = new Dictionary<int, SortedSpan<int>>();
         var destinationForSource = new SortedList<int, int>();
         SearchForPointers(pointersForDestination, destinationForSource);
         WritePointerRuns(pointersForDestination, destinationForSource);
         WriteSpriteRuns(pointersForDestination);
         WriteStringRuns(pointersForDestination);
         ResolveConflicts();
         FreeSpaceStart = EarliestAllowedAnchor;

         if (metadata == null) return;

         // metadata is more important than anything already found
         foreach (var list in metadata.Lists) {
            lists[list.Name] = list.ToList();
         }
         foreach (var anchor in metadata.NamedAnchors) {
            // since we're loading metadata, we're pretty sure that the anchors in the metadata are right.
            // therefore, allow those anchors to overwrite anything we found during the initial quick-search phase.
            using (ModelCacheScope.CreateScope(this)) {
               ApplyAnchor(this, new NoDataChangeDeltaModel(), anchor.Address, AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
            }
         }
         foreach (var unmappedPointer in metadata.UnmappedPointers) {
            sourceToUnmappedName[unmappedPointer.Address] = unmappedPointer.Name;
            if (!unmappedNameToSources.ContainsKey(unmappedPointer.Name)) unmappedNameToSources[unmappedPointer.Name] = SortedSpan<int>.None;
            unmappedNameToSources[unmappedPointer.Name] = unmappedNameToSources[unmappedPointer.Name].Add1(unmappedPointer.Address);
         }
         foreach (var word in metadata.MatchedWords) {
            if (!matchedWords.ContainsKey(word.Name)) matchedWords.Add(word.Name, new List<int>());
            matchedWords[word.Name].Add(word.Address);
            var index = BinarySearch(word.Address);
            if (index > 0) {
               runs[index] = new WordRun(word.Address, word.Name, runs[index].PointerSources);
            } else {
               runs.Insert(~index, new WordRun(word.Address, word.Name));
            }
         }

         if (metadata.FreeSpaceSearch >= 0) FreeSpaceStart = metadata.FreeSpaceSearch;

         if (!metadata.IsEmpty && StoredMetadata.NeedVersionUpdate(metadata.Version, singletons?.MetadataInfo.VersionNumber ?? "0")) {
            if (singletons.GameReferenceTables.TryGetValue(this.GetGameCode(), out var tables)) {
               UpdateRuns(tables);
            }
         }

         ResolveConflicts();
      }

      private void UpdateRuns(GameReferenceTables referenceTables) {
         var noChange = new NoDataChangeDeltaModel();
         foreach (var reference in referenceTables) {
            var destination = ReadPointer(reference.Address);
            if (!anchorForAddress.ContainsKey(destination) && !addressForAnchor.ContainsKey(reference.Name)) {
               using (ModelCacheScope.CreateScope(this)) {
                  ApplyAnchor(this, noChange, destination, "^" + reference.Name + reference.Format, allowAnchorOverwrite: true);
               }
               continue;
            }

            if (!anchorForAddress.TryGetValue(destination, out var anchor)) continue;
            if (anchor == reference.Name) continue;
            using (ModelCacheScope.CreateScope(this)) {
               if (TryParseFormat(this, reference.Name, reference.Format, destination, out var run).HasError) continue;
            }

            // update this anchor
            anchorForAddress[destination] = reference.Name;
            addressForAnchor.Remove(anchor);
            addressForAnchor[reference.Name] = destination;

            // update runs
            for (int i = 0; i < runs.Count; i++) {
               // update matched-length lengths
               if (runs[i] is ArrayRun array) {
                  var parentName = array.LengthFromAnchor;
                  if (parentName == anchor) {
                     var lengthModifier = array.FormatString.Split(parentName).Last();
                     var newLengthToken = reference.Name + array.LengthFromAnchor.Substring(parentName.Length);
                     var arrayClose = array.FormatString.LastIndexOf(']');
                     var newFormat = array.FormatString.Substring(0, arrayClose + 1);
                     TryParse(this, newFormat + newLengthToken + lengthModifier, array.Start, array.PointerSources, out var newRun);
                     runs[i] = newRun;
                  }
               }

               // update enum names / bitarray names
               if (runs[i] is ITableRun table) {
                  for (int j = 0; j < table.ElementContent.Count; j++) {
                     if (table.ElementContent[j] is ArrayRunEnumSegment enumSegment && enumSegment.EnumName == anchor) {
                        var segments = table.ElementContent.ToList();
                        segments[j] = new ArrayRunEnumSegment(enumSegment.Name, enumSegment.Length, reference.Name);
                        table = table.Duplicate(table.Start, table.PointerSources, segments);
                        runs[i] = table;
                     } else if (table.ElementContent[j] is ArrayRunBitArraySegment bitSegment && bitSegment.SourceArrayName == anchor) {
                        var segments = table.ElementContent.ToList();
                        segments[j] = new ArrayRunBitArraySegment(bitSegment.Name, bitSegment.Length, reference.Name);
                        table = table.Duplicate(table.Start, table.PointerSources, segments);
                        runs[i] = table;
                     }
                  }
               }

               // update tileset hints
               if (runs[i] is LzTilemapRun tilemap) {
                  var format = tilemap.Format;
                  if (format.MatchingTileset != anchor) continue;
                  tilemap = tilemap.Duplicate(new TilemapFormat(format.BitsPerPixel, format.TileWidth, format.TileHeight, reference.Name, format.TilesetTableMember));
                  runs[i] = tilemap;
               }

               // update palette hints
               if (runs[i] is ISpriteRun sprite) {
                  if (sprite is LzTilemapRun) continue;
                  var format = sprite.SpriteFormat;
                  if (format.PaletteHint != anchor) continue;
                  sprite = sprite.Duplicate(new SpriteFormat(format.BitsPerPixel, format.TileWidth, format.TileHeight, reference.Name));
                  runs[i] = sprite;
               }
            }
         }
      }

      /// <summary>
      /// Finds pointers based on Heuristics.
      /// This is definitely wrong, but it's pretty good.
      /// </summary>
      private void SearchForPointers(Dictionary<int, SortedSpan<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         // pointers must be 4-byte aligned
         for (int i = 0; i < RawData.Length - 3; i += 4) {

            // pointers must end in 08 or 09
            if (RawData[i + 3] != 0x08 && RawData[i + 3] != 0x09) continue;

            // pointers must point to locations that are 4-byte aligned
            if (RawData[i] % 4 != 0) continue;
            var source = i;
            var destination = ReadPointer(i);

            // pointers must point into the data
            if (destination >= RawData.Length) continue;

            // pointers must not point at the header
            if (destination < EarliestAllowedAnchor) continue;

            // pointers must point at something useful, not just a bunch of FF
            bool pointsToManyFF = true;
            for (int j = 0; j < 4 && pointsToManyFF && destination + j < RawData.Length; j++) pointsToManyFF = RawData[destination + j] == 0xFF;
            if (pointsToManyFF) continue;

            // we found a pointer!
            if (!pointersForDestination.ContainsKey(destination)) pointersForDestination[destination] = SortedSpan<int>.None;
            pointersForDestination[destination] = pointersForDestination[destination].Add1(source);
            destinationForSource.Add(source, destination);
         }
      }

      private void WritePointerRuns(Dictionary<int, SortedSpan<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         var destinations = pointersForDestination.Keys.OrderBy(i => i).GetEnumerator();
         var sources = destinationForSource.Keys.GetEnumerator();

         var moreDestinations = destinations.MoveNext();
         var moreSources = sources.MoveNext();

         while (moreDestinations && moreSources) {
            if (destinations.Current < sources.Current) {
               runs.Add(new NoInfoRun(destinations.Current, pointersForDestination[destinations.Current]));
               moreDestinations = destinations.MoveNext();
            } else if (sources.Current < destinations.Current) {
               runs.Add(new PointerRun(sources.Current));
               moreSources = sources.MoveNext();
            } else {
               runs.Add(new PointerRun(sources.Current, pointersForDestination[destinations.Current]));
               moreDestinations = destinations.MoveNext();
               moreSources = sources.MoveNext();
            }
         }

         while (moreDestinations) {
            runs.Add(new NoInfoRun(destinations.Current, pointersForDestination[destinations.Current]));
            moreDestinations = destinations.MoveNext();
         }

         while (moreSources) {
            runs.Add(new PointerRun(sources.Current));
            moreSources = sources.MoveNext();
         }
      }

      private void WriteSpriteRuns(Dictionary<int, SortedSpan<int>> pointersForDestination) {
         var noDataChange = new NoDataChangeDeltaModel();
         foreach (var destination in pointersForDestination.Keys.OrderBy(i => i)) {
            var existingRun = GetNextRun(destination);
            if (!(existingRun is NoInfoRun)) continue;
            var protoRun = new LZRun(this, destination);
            if (protoRun.Length < 5) continue;
            if (protoRun.DecompressedLength < 32 || protoRun.DecompressedLength % 32 != 0) continue;
            if (GetNextRun(destination + 1).Start < destination + protoRun.Length) continue;
            if (protoRun.DecompressedLength == 32) {
               ObserveRunWritten(noDataChange, new LzPaletteRun(new PaletteFormat(4, 1), this, destination, pointersForDestination[destination]));
            } else {
               var tiles = protoRun.DecompressedLength / 32;
               var sqrt = (int)Math.Sqrt(tiles);
               var spriteFormat = new SpriteFormat(4, sqrt, sqrt, null);
               ObserveRunWritten(noDataChange, new LzSpriteRun(spriteFormat, this, destination, pointersForDestination[destination]));
            }
         }
      }

      private void WriteStringRuns(Dictionary<int, SortedSpan<int>> pointersForDestination) {
         var noDataChange = new NoDataChangeDeltaModel();
         foreach (var destination in pointersForDestination.Keys.OrderBy(i => i)) {
            var length = PCSString.ReadString(RawData, destination, false);
            if (length < 2) continue;
            if (GetNextRun(destination + 1).Start < destination + length) continue;
            ObserveRunWritten(noDataChange, new PCSRun(this, destination, length, pointersForDestination[destination]));
         }
      }

      [Conditional("DEBUG")]
      public void ResolveConflicts() {
         return;
         for (int i = 0; i < runs.Count; i++) {
            // for every pointer run, make sure that the thing it points to knows about it
            if (runs[i] is PointerRun pointerRun) {
               var destination = ReadPointer(pointerRun.Start);
               var run = GetNextRun(destination);
               if (destination < 0 || destination >= Count) {
                  // pointer points outside scope. Such a pointer is an error, but is not a metadata inconsistency.
               } else if (run is ArrayRun arrayRun1 && arrayRun1.SupportsPointersToElements) {
                  var offsets = arrayRun1.ConvertByteOffsetToArrayOffset(destination);
                  Debug.Assert(arrayRun1.PointerSourcesForInnerElements[offsets.ElementIndex].Contains(pointerRun.Start));
                  if (offsets.ElementIndex == 0) Debug.Assert(run.PointerSources.Contains(pointerRun.Start));
               } else if (run != NoInfoRun.NullRun) {
                  Debug.Assert(run.PointerSources != null && run.PointerSources.Contains(pointerRun.Start));
               }
            }

            // for every TPTRun, make sure something points to it
            if (runs[i] is TrainerPokemonTeamRun) Debug.Assert(runs[i].PointerSources.Count > 0, "TPTRuns must not exist with no content long-term.");

            // for ever NoInfoRun, something points to it
            if ((runs[i] is NoInfoRun || runs[i] is PointerRun) && !anchorForAddress.ContainsKey(runs[i].Start)) {
               Debug.Assert(runs[i].PointerSources == null || runs[i].PointerSources.Count > 0, "Unnamed NoInfoRuns must have something pointing to them!");
            }

            // for every run with sources, make sure the pointer at that source actually points to it
            if (runs[i].PointerSources != null) {
               foreach (var source in runs[i].PointerSources) {
                  var run = GetNextRun(source);
                  if (run is PointerRun) {
                     Debug.Assert(run.Start == source);
                     Debug.Assert(ReadPointer(source) == runs[i].Start, $"Expected {source:X6} to point to {runs[i].Start:X6}");
                  } else if (run is ITableRun) {
                     Debug.Assert(run.Start <= source);
                     Debug.Assert(ReadPointer(source) == runs[i].Start);
                  } else {
                     Debug.Fail($"Pointer must be a {nameof(PointerRun)} or live within an {nameof(ITableRun)}");
                  }
               }
            }
            if (runs[i] is ArrayRun arrayRun2 && arrayRun2.SupportsPointersToElements) {
               for (int j = 0; j < arrayRun2.ElementCount; j++) {
                  foreach (var source in arrayRun2.PointerSourcesForInnerElements[j]) {
                     Debug.Assert(ReadPointer(source) == arrayRun2.Start + arrayRun2.ElementLength * j);
                  }
               }
            }

            // for every table, make sure the things it points to know about the table
            if (runs[i] is ITableRun tableRun) {
               int elementOffset = 0;
               foreach (var segment in tableRun.ElementContent) {
                  if (segment.Type != ElementContentType.Pointer) { elementOffset += segment.Length; continue; }
                  for (int j = 0; j < tableRun.ElementCount; j++) {
                     var start = tableRun.Start + elementOffset + tableRun.ElementLength * j;
                     var destination = ReadPointer(start);
                     var run = GetNextRun(destination);
                     if (destination < 0 || destination >= Count) {
                        // pointer points outside scope. Such a pointer is an error, but is not a metadata inconsistency.
                     } else if (run is ArrayRun arrayRun1 && arrayRun1.SupportsPointersToElements) {
                        var offsets = arrayRun1.ConvertByteOffsetToArrayOffset(destination);
                        Debug.Assert(arrayRun1.PointerSourcesForInnerElements[offsets.ElementIndex].Contains(start));
                        if (offsets.ElementIndex == 0) Debug.Assert(run.PointerSources.Contains(start));
                     } else if (run is ITableRun && run.Start < destination) {
                        // exception: tables are allowed to have pointers that point randomly into other runs.
                        // such a thing is a data error in the ROM, but is not a metadata inconsistency.
                     } else if (run.Start != destination) {
                        // for tables, the invalidly point into a run. Such is an error in the data, but is allowed for the metadata.
                     } else {
                        if (run.PointerSources != null) {
                           Debug.Assert(run.PointerSources.Contains(start));
                        } else {
                           Debug.Fail("This run is referenced by a table, but doesn't know about the table that points to it.");
                        }
                     }
                  }
                  elementOffset += segment.Length;
               }
            }

            if (i == runs.Count - 1 || runs[i].Start + runs[i].Length <= runs[i + 1].Start) continue;
            var debugRunStart1 = runs[i].Start.ToString("X6");
            var debugRunStart2 = runs[i + 1].Start.ToString("X6");
            Debug.Fail($"Conflict: there's a run that ends after the next run starts! {debugRunStart1} and {debugRunStart2}");
         }

         // for every table with a matched-length, verify that the length is as expected.
         var token = new NoDataChangeDeltaModel();
         foreach (var array in Arrays) {
            if (string.IsNullOrEmpty(array.LengthFromAnchor)) continue;
            var parentName = array.LengthFromAnchor;
            var childName = GetAnchorFromAddress(-1, array.Start);
            if (!(GetNextRun(GetAddressFromAnchor(token, -1, array.LengthFromAnchor)) is ITableRun parent)) continue;
            Debug.Assert(parent.ElementCount + array.ParentOffset == array.ElementCount);
         }
      }

      #endregion

      public static ErrorInfo ApplyAnchor(IDataModel model, ModelDelta changeToken, int dataIndex, string text) {
         var errorInfo = ApplyAnchor(model, changeToken, dataIndex, text, allowAnchorOverwrite: false);
         (model as PokemonModel)?.ResolveConflicts();
         return errorInfo;
      }

      public static ErrorInfo ApplyAnchor(IDataModel model, ModelDelta changeToken, int dataIndex, string text, bool allowAnchorOverwrite) {
         var (name, format) = SplitNameAndFormat(text);

         var errorInfo = TryParseFormat(model, name, format, dataIndex, out var runToWrite);
         if (errorInfo.HasError) return errorInfo;

         errorInfo = ValidateAnchorNameAndFormat(model, runToWrite, name, format, dataIndex, allowAnchorOverwrite);
         if (!errorInfo.HasError) {
            errorInfo = UniquifyName(model, changeToken, dataIndex, ref name);
            model.ObserveAnchorWritten(changeToken, name, runToWrite);
         }

         return errorInfo;
      }

      private static ErrorInfo UniquifyName(IDataModel model, ModelDelta changeToken, int desiredAddressForName, ref string name) {
         var address = model.GetAddressFromAnchor(changeToken, -1, name);
         if (address == Pointer.NULL || address == desiredAddressForName) return ErrorInfo.NoError;

         var info = new ErrorInfo("Chosen name was in use. The new anchor has been renamed to avoid collisions.", isWarningLevel: true);

         // so once we've verified that the new name doesn't match the name from the current address,
         // we'll need to check again for the newly created name.
         // so do some recursion in each of these return cases.

         // Append _copy to the end to avoid the collision.
         if (!name.Contains("_copy")) {
            name += "_copy";
            UniquifyName(model, changeToken, desiredAddressForName, ref name);
            return info;
         }

         // It already had _copy on the end... fine, append the number '2'.
         var number = name.Split("_copy").Last();
         if (number.Length == 0) {
            name += "2";
            UniquifyName(model, changeToken, desiredAddressForName, ref name);
            return info;
         }

         // It already had a number on the end of the _copy... ok, just increment it by 1.
         if (int.TryParse(number, out var result)) {
            name += result;
            UniquifyName(model, changeToken, desiredAddressForName, ref name);
            return info;
         }

         // It wasn't a number? Eh, just throw _copy on the end again, it'll be fine.
         name += "_copy";
         UniquifyName(model, changeToken, desiredAddressForName, ref name);
         return info;
      }

      public static bool SpanContainsAnchor(IDataModel model, int start, int length) {
         var run = model.GetNextRun(start + 1);

         // if we're starting in the middle of a run, get the next one
         if (run.Start <= start) {
            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
            run = model.GetNextRun(start);
         }

         // move start forward to the start of the run
         length -= run.Start - start;
         start = run.Start;

         // check all the runs in the range for pointer sources / destination names
         while (length > 0) {
            if (run.PointerSources.Count > 0) return true;
            if (!string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, run.Start))) return true;
            run = model.GetNextRun(run.Start + run.Length);
            length -= run.Start - start;
            start = run.Start;
         }

         return false;
      }

      public override SortedSpan<int> GetUnmappedSourcesToAnchor(string anchor) {
         if (!unmappedNameToSources.TryGetValue(anchor, out var list)) return SortedSpan<int>.None;
         return list;
      }

      public override int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor) {

         var nameparts = anchor.Split('/');
         anchor = nameparts.First();

         if (addressForAnchor.TryGetValueCaseInsensitive(anchor, out int address)) {
            nameparts = nameparts.Skip(1).ToArray();
            if (nameparts.Length > 0) address = GetAddressFromAnchor(address, nameparts);
            return address;
         }

         if (requestSource < 0) return Pointer.NULL;
         if (anchor.ToLower() == "null") return Pointer.NULL;

         // the named anchor does not exist! Add it to the list of desired anchors
         if (!unmappedNameToSources.ContainsKey(anchor)) {
            unmappedNameToSources[anchor] = SortedSpan<int>.None;
         }
         unmappedNameToSources[anchor] = unmappedNameToSources[anchor].Add1(requestSource);
         sourceToUnmappedName[requestSource] = anchor;
         changeToken.AddUnmappedPointer(requestSource, anchor);

         return Pointer.NULL;
      }

      private int GetAddressFromAnchor(int startingAddress, string[] nameparts) {
         var run = GetNextRun(startingAddress);

         // support empty string as element 0
         if (nameparts[0] == string.Empty) return run.Start;

         // only support indexing into an anchor if the anchor points to an array
         if (!(run is ITableRun array)) return Pointer.NULL;

         if (nameparts.Length < 1) return Pointer.NULL;

         // support things like .../4
         if (!int.TryParse(nameparts[0], out var index)) {
            // support things like .../BULBASAUR
            if (!ArrayRunEnumSegment.TryMatch(nameparts[0], array.ElementNames, out index)) return Pointer.NULL;
         }

         var elementStart = array.Start + array.ElementLength * index;
         if (nameparts.Length == 1) return elementStart;

         // support things like .../4/name
         var segmentOffset = 0;
         foreach (var segment in array.ElementContent) {
            if (segment.Name.ToLower() == nameparts[1].ToLower()) {
               var segmentStart = elementStart + segmentOffset;
               if (nameparts.Length > 2 && segment.Type == ElementContentType.Pointer) {
                  return GetAddressFromAnchor(ReadPointer(segmentStart), nameparts.Skip(2).ToArray());
               }
               return segmentStart;
            }
            segmentOffset += segment.Length;
         }

         return Pointer.NULL;
      }

      public override string GetAnchorFromAddress(int requestSource, int address) {
         // option 1: a known name exists for this address
         if (anchorForAddress.TryGetValue(address, out string anchor)) return anchor;

         // option 2: a known name exists for this source, but the name doesn't actually exist in the file
         if (sourceToUnmappedName.TryGetValue(requestSource, out anchor)) return anchor;

         // option 3: pointing to nothing
         if (address == -0x08000000) return "null";

         // option 4: pointing within an array that supports inner element anchors
         var containingRun = GetNextRun(address);
         if (containingRun.Start < address && containingRun is ArrayRun array) {
            var arrayName = GetAnchorFromAddress(-1, array.Start);
            var arrayIndex = (address - array.Start) / array.ElementLength;
            var indexMod = (address - array.Start) % array.ElementLength;
            if (indexMod == 0) return $"{arrayName}{ArrayAnchorSeparator}{arrayIndex}";
         }

         return string.Empty;
      }

      public override IFormattedRun GetNextRun(int dataIndex) {
         if (dataIndex == Pointer.NULL) return NoInfoRun.NullRun;
         var index = BinarySearch(dataIndex);
         if (index < 0) {
            index = ~index;
            if (index > 0) {
               var previous = runs[index - 1];
               if (previous.Start + previous.Length > dataIndex) index -= 1;
            }
         }
         if (index >= runs.Count) return NoInfoRun.NullRun;
         return runs[index];
      }

      public override IFormattedRun GetNextAnchor(int dataIndex) {
         var index = BinarySearch(dataIndex);
         if (index < 0) index = ~index;
         for (; index < runs.Count; index++) {
            if (runs[index].Start < dataIndex) continue;
            if (runs[index].PointerSources == null) continue;
            return runs[index];
         }
         return NoInfoRun.NullRun;
      }

      public override bool TryGetUsefulHeader(int address, out string header) {
         header = null;
         // only produce headers for arrays with length based on other arrays that start with a text member.
         var run = GetNextRun(address);
         if (run.Start > address) return false;
         if (!(run is ArrayRun array)) return false;
         if ((address - array.Start) % array.ElementLength != 0) return false;

         using (ModelCacheScope.CreateScope(this)) {
            var index = (address - array.Start) / array.ElementLength;
            if (array.ElementNames.Count <= index) return false;
            header = array.ElementNames[index];
         }

         return true;
      }

      public override bool IsAtEndOfArray(int dataIndex, out ITableRun arrayRun) {
         var index = BinarySearch(dataIndex);
         if (index >= 0 && runs[index].Length == 0) {
            arrayRun = runs[index] as ITableRun;
            return arrayRun != null;
         }

         if (index < 0) index = ~index;
         index -= 1;

         if (index < 0) {
            arrayRun = null;
            return false;
         }

         arrayRun = runs[index] as ITableRun;
         return arrayRun != null && runs[index].Start + runs[index].Length == dataIndex;
      }

      public override void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run) {
         Debug.Assert(run.Length > 0); // writing a run of length zero is stupid.
         if (run is ArrayRun array) {
            // update any words who's length matches this array's name
            if (anchorForAddress.TryGetValue(run.Start, out var anchorName)) {
               if (matchedWords.TryGetValue(anchorName, out var words)) {
                  foreach (var address in words) WriteValue(changeToken, address, array.ElementCount);
               }
            }
         }

         var index = BinarySearch(run.Start);
         IFormattedRun existingRun = null;
         if (index < 0) {
            index = ~index;
            if (runs.Count == index || (runs[index].Start >= run.Start + run.Length && (index == 0 || runs[index - 1].Start + runs[index - 1].Length <= run.Start))) {
               runs.Insert(index, run);
               changeToken.AddRun(run);
            } else {
               // there's a conflict: the new run was written in a space already being used, but not where another run starts
               // I'll need to do something here eventually... but for now, just error
               // the right thing to do is probably to erase the existing format in favor of the new thing the user just tried to add.
               // if the existing format was an anchor, clear all the pointers that pointed to it, since the writer is declaring that that address is not a valid anchor.
               Debug.Fail($"Trying to add a run at {run.Start:X6} which overlaps a run at {runs[index].Start:X6}");
            }
         } else {
            // replace / merge with existing
            // if the only thing changed was the anchor, then don't change the format, just merge the anchor
            existingRun = runs[index];
            changeToken.RemoveRun(existingRun);
            if (existingRun is PointerRun) {
               bool needClearPointerRun = !(run is NoInfoRun) && !(run is PointerRun);
               if (((run as OffsetPointerRun)?.Offset ?? 0) != ((existingRun as OffsetPointerRun)?.Offset ?? 0)) needClearPointerRun = true;
               if (needClearPointerRun) {
                  var destination = ReadPointer(existingRun.Start);
                  ClearPointer(changeToken, existingRun.Start, destination);
                  index = BinarySearch(run.Start); // have to recalculate index, because ClearPointer can removed runs.
               }
            }
            run = run.MergeAnchor(existingRun.PointerSources);
            if (run is NoInfoRun) run = existingRun.MergeAnchor(run.PointerSources); // when writing an anchor with no format, keep the existing format.
            if (existingRun is ITableRun arrayRun1) {
               ModifyAnchorsFromPointerArray(changeToken, run as ITableRun, arrayRun1, arrayRun1.ElementCount, ClearPointerFormat);
               index = BinarySearch(run.Start); // have to recalculate index, because ClearPointerFormat can removed runs.
            }
            runs[index] = run;
            changeToken.AddRun(run);
         }

         if (run is WordRun word) {
            if (!matchedWords.ContainsKey(word.SourceArrayName)) matchedWords[word.SourceArrayName] = new List<int>();
            matchedWords[word.SourceArrayName].Add(word.Start);
            changeToken.AddMatchedWord(this, word.Start, word.SourceArrayName);
         } else if (run is OffsetPointerRun offsetPointer) {
            pointerOffsets[offsetPointer.Start] = offsetPointer.Offset;
            changeToken.AddOffsetPointer(offsetPointer.Start, offsetPointer.Offset);
         }

         if (run is PointerRun) AddPointerToAnchor(null, null, changeToken, run.Start);
         if (run is ITableRun tableRun) ModifyAnchorsFromPointerArray(changeToken, tableRun, existingRun as ITableRun, tableRun.ElementCount, AddPointerToAnchor);
         if (run is ArrayRun arrayRun) UpdateDependantArrayLengths(changeToken, arrayRun);

         if (run is NoInfoRun && run.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(run.Start)) {
            // this run has no useful information. Remove it.
            changeToken.RemoveRun(runs[index]);
            runs.RemoveAt(index);
         }
      }

      public override int ReadPointer(int index) {
         var destination = base.ReadPointer(index);
         if (pointerOffsets.TryGetValue(index, out int offset)) destination += offset;
         return destination;
      }

      public override void WritePointer(ModelDelta changeToken, int address, int pointerDestination) {
         if (pointerOffsets.TryGetValue(address, out int offset)) pointerDestination -= offset;
         base.WritePointer(changeToken, address, pointerDestination);
      }

      /// <summary>
      /// A new array just came in. It might have pointers.
      /// When we make a new pointer, we need to update anchors to include the new pointer.
      /// So update all the anchors based on any new pointers in this newly added array.
      /// </summary>
      private void ModifyAnchorsFromPointerArray(ModelDelta changeToken, ITableRun arrayRun, ITableRun previousTable, int elementCount, Action<ArrayRunElementSegment, IReadOnlyList<ArrayRunElementSegment>, ModelDelta, int> changeAnchors) {
         int segmentOffset = arrayRun.Start;
         var formatMatches = previousTable != null && arrayRun.DataFormatMatches(previousTable);
         var parentOffset = 0;
         if (arrayRun is ArrayRun arrayRun1) parentOffset = Math.Max(arrayRun1.ParentOffset, 0);
         var shorterTable = Math.Min(arrayRun.ElementCount, previousTable?.ElementCount ?? arrayRun.ElementCount);
         // i loops over the different segments in the array
         for (int i = 0; i < arrayRun.ElementContent.Count; i++) {
            if (arrayRun.ElementContent[i].Type != ElementContentType.Pointer) { segmentOffset += arrayRun.ElementContent[i].Length; continue; }
            // for a pointer segment, j loops over all the elements in the array
            for (int j = 0; j < elementCount; j++) {
               if (formatMatches && shorterTable - parentOffset > j) continue; // we can skip this one
               var start = segmentOffset + arrayRun.ElementLength * j;
               changeAnchors(arrayRun.ElementContent[i], arrayRun.ElementContent, changeToken, start);
            }
            segmentOffset += arrayRun.ElementContent[i].Length;
         }
      }

      /// <summary>
      /// An array was moved.
      /// If that array pointed to stuff, that stuff needs to know that its sources moved.
      /// Remove the sources that match the array's original location.
      /// Add new sources corresponding to the array's new location.
      /// </summary>
      private void UpdateAnchorsFromArrayMove(ModelDelta changeToken, ITableRun original, ITableRun moved) {
         int originalOffset = original.Start;
         int segmentOffset = moved.Start;
         if (original.ElementContent.Count != moved.ElementContent.Count) return; // if the number of elements changed during the move, nop out
         // i loops over the different segments in the array
         for (int i = 0; i < moved.ElementContent.Count; i++) {
            if (moved.ElementContent[i].Type != ElementContentType.Pointer) {
               originalOffset += original.ElementContent[i].Length;
               segmentOffset += moved.ElementContent[i].Length;
               continue;
            }
            // for a pointer segment, j loops over all the elements in the array
            for (int j = 0; j < moved.ElementCount; j++) {
               var originalStart = originalOffset + original.ElementLength * j;
               var movedStart = segmentOffset + moved.ElementLength * j;
               var destination = ReadPointer(movedStart);
               if (destination < 0 || destination >= RawData.Length) continue;
               var destinationRun = GetNextRun(destination);
               changeToken.RemoveRun(destinationRun);
               destinationRun = destinationRun.RemoveSource(originalStart);
               destinationRun = destinationRun.MergeAnchor(new SortedSpan<int>(movedStart));
               changeToken.AddRun(destinationRun);
               var runIndex = BinarySearch(destinationRun.Start);
               runs[runIndex] = destinationRun;
            }
            originalOffset += original.ElementContent[i].Length;
            segmentOffset += moved.ElementContent[i].Length;
         }
      }

      /// <summary>
      /// This new array may have other arrays who's length depend on it.
      /// Update those arrays based on this new length.
      /// (Recursively, since other arrays might depend on those ones).
      /// </summary>
      private void UpdateDependantArrayLengths(ModelDelta changeToken, ArrayRun arrayRun) {
         if (!anchorForAddress.TryGetValue(arrayRun.Start, out string anchor)) return;
         foreach (var table in this.GetDependantArrays(anchor)) {
            var newTable = table;
            // option 1: this table's length is based on the given table
            if (anchor.Equals(table.LengthFromAnchor)) {
               int targetCount = arrayRun.ElementCount + table.ParentOffset;
               if (table.ElementCount == targetCount) continue;
               // only relocate if we're not in a loading situation
               if (!(changeToken is NoDataChangeDeltaModel)) {
                  newTable = RelocateForExpansion(changeToken, table, targetCount * table.ElementLength);
               }
               int originalLength = newTable.Length;
               newTable = newTable.Append(changeToken, targetCount - table.ElementCount);
               var tableAnchor = GetAnchorFromAddress(-1, newTable.Start);

               if (newTable.Length < originalLength) ClearFormat(changeToken, newTable.Start, originalLength);
               if (string.IsNullOrEmpty(tableAnchor)) {
                  ObserveRunWritten(changeToken, newTable);
               } else {
                  ObserveAnchorWritten(changeToken, tableAnchor, newTable);
               }
            }
            // option 2: this table includes a bit-array based on the given table
            var requiredByteLength = (int)Math.Ceiling(arrayRun.ElementCount / 8.0);
            for (int segmentIndex = 0; segmentIndex < newTable.ElementContent.Count; segmentIndex++) {
               if (!(newTable.ElementContent[segmentIndex] is ArrayRunBitArraySegment bitSegment)) continue;
               if (bitSegment.SourceArrayName != anchor) continue;
               if (bitSegment.Length == requiredByteLength) continue;

               // if the changeToken is a NoChange, we're still in the middle of loading
               // in that case, don't try to relocate/shift anything, just grow the proper segment based on the length of the newly loaded table
               var newElementWidth = newTable.ElementLength - bitSegment.Length + requiredByteLength;
               if (!(changeToken is NoDataChangeDeltaModel)) {
                  newTable = (ArrayRun)RelocateForExpansion(changeToken, table, newTable.ElementCount * newElementWidth);
                  // within the new table, shift all the data to fit the new data width
                  ShiftTableBytesForGrowingSegment(changeToken, newTable, requiredByteLength, segmentIndex);
               } else {
                  // we didn't relocate/shift, but we still need to clear the area before growing the table
                  ClearFormat(changeToken, newTable.Start + newTable.Length, newTable.ElementCount * (newElementWidth - newTable.ElementLength));
               }
               newTable = newTable.GrowBitArraySegment(segmentIndex, requiredByteLength - bitSegment.Length);
               ObserveRunWritten(changeToken, newTable);
            }
         }
      }

      /// <summary>
      /// A segment within a table is growing to include an extra byte.
      /// Shift all the bytes within the table to make room within each element for the new byte at the end of the chosen segment.
      /// </summary>
      private void ShiftTableBytesForGrowingSegment(ModelDelta changeToken, ArrayRun table, int newLength, int segmentIndex) {
         var segment = table.ElementContent[segmentIndex];
         // since we're moving data in-place, start at the end and work our way to the front to avoid overwriting anything we haven't read yet.
         var (oldElementWidth, newElementWidth) = (table.ElementLength, table.ElementLength - segment.Length + newLength);
         for (int elementIndex = table.ElementCount - 1; elementIndex >= 0; elementIndex--) {
            var sourceIndex = table.Start + oldElementWidth * (elementIndex + 1) - 1;
            var destinationIndex = table.Start + newElementWidth * (elementIndex + 1) - 1;
            foreach (var movingSegment in table.ElementContent.Reverse()) {
               // if we're at the segment that's expanding, expand it by filling with 0's
               if (movingSegment == segment) {
                  foreach (var _ in Enumerable.Range(0, newLength - segment.Length)) {
                     changeToken.ChangeData(this, destinationIndex, 0);
                     destinationIndex--;
                  }
               }
               // move the source data to the destination point
               foreach (var _ in Enumerable.Range(0, movingSegment.Length)) {
                  changeToken.ChangeData(this, destinationIndex, RawData[sourceIndex]);
                  sourceIndex--;
                  destinationIndex--;
               }
            }
         }
      }

      /// <summary>
      /// There is a pointer at 'start' that was just added.
      /// Update anchor at destination to include that pointer.
      /// </summary>
      /// <param name="changeToken"></param>
      /// <param name="start"></param>
      private void AddPointerToAnchor(ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, ModelDelta changeToken, int start) {
         var destination = ReadPointer(start);
         if (destination < 0 || destination >= Count) return;
         int index = BinarySearch(destination);
         if (index < 0 && ~index > 0 && runs[~index - 1] is ArrayRun array &&
            array.SupportsPointersToElements &&
            array.Start + array.Length > destination &&
            (destination - array.Start) % array.ElementLength == 0) {
            // the pointer points into an array that supports inner anchors
            index = ~index - 1;
            changeToken.RemoveRun(array);
            runs[index] = array.AddSourcePointingWithinArray(start);
            changeToken.AddRun(runs[index]);
         } else if (index < 0) {
            // the pointer points to a location between existing runs
            IFormattedRun newRun = new NoInfoRun(destination, new SortedSpan<int>(start));
            UpdateNewRunFromPointerFormat(ref newRun, segment as ArrayRunPointerSegment, segments, changeToken);
            if (newRun != null) {
               ClearFormat(changeToken, newRun.Start, newRun.Length); // adding a new destination, so clear anything in the way.
               ObserveRunWritten(changeToken, newRun);
            }
         } else {
            // the pointer points to a known normal anchor
            var existingRun = runs[index];
            changeToken.RemoveRun(existingRun);
            var previousRun = existingRun;
            existingRun = existingRun.MergeAnchor(new SortedSpan<int>(start));
            UpdateNewRunFromPointerFormat(ref existingRun, segment as ArrayRunPointerSegment, segments, changeToken);
            if (existingRun != null) {
               if (segment == null) {
                  // it's just a naked pointer, so we have no knowledge about the thing it points to.
                  index = BinarySearch(destination); // runs could've been removed during UpdateNewRunFromPointerFormat: search for the index again.
                  if (index < 0) {
                     runs.Insert(~index, existingRun);
                  } else {
                     runs[index] = existingRun;
                  }
                  changeToken.AddRun(existingRun);
               } else {
                  if (previousRun.FormatString != existingRun.FormatString) {
                     // it could point to something interesting. Do a full observe. Start by clearing out any existing formats in that area.
                     ClearFormat(changeToken, existingRun.Start, existingRun.Length);
                  }
                  ObserveRunWritten(changeToken, existingRun);
               }
            }
         }
      }

      /// <summary>
      /// If this new FormattedRun is a pointer to a known stream format,
      /// Update the model so the data we're pointing to is actually that format.
      /// </summary>
      private void UpdateNewRunFromPointerFormat(ref IFormattedRun run, ArrayRunPointerSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, ModelDelta token) {
         var nextRun = GetNextRun(run.Start);
         if (nextRun == run && nextRun is ITableRun) {
            // the parent table points into a table. The existing table format wins: just keep it the same.
            return;
         }
         if (nextRun.Start <= run.Start && nextRun is ITableRun && run.GetType() != nextRun.GetType()) {
            // we're trying to point into a table. The table format wins: don't add any anchor.
            // this pointer is a 'bad' pointer: its pointing somewhere we KNOW doesn't contain the right data.
            run = null;
            return;
         }
         if (nextRun.Start < run.Start && nextRun.PointerSources != null && nextRun.PointerSources.Any(source => GetNextRun(source) is ITableRun)) {
            // we're trying to point into something that is owned by a table. The table format wins: don't add any anchor.
            // this pointer is a 'bad' pointer: its pointing somewhere we KNOW doesn't contain the right data.
            run = null;
            return;
         }

         if (segment == null) {
            // we don't know anything about the format, but we know a pointer starts here.
            // clear any existing formats that conflict with this pointer.
            if (run.Start != nextRun.Start) ClearFormat(token, run.Start, run.Length);
            return;
         }

         FormatRunFactory.GetStrategy(segment.InnerFormat).UpdateNewRunFromPointerFormat(this, token, segment.Name, segments, ref run);
      }

      public override void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run) {
         Debug.Assert(run.Length > 0); // writing an anchor of length zero is stupid.
         int location = run.Start;
         int index = BinarySearch(location);

         var existingRun = (index >= 0 && index < runs.Count) ? runs[index] : null;

         using (ModelCacheScope.CreateScope(this)) {
            if (existingRun == null || existingRun.Start != run.Start) {
               // no format starts exactly at this anchor, so clear any format that goes over this anchor.
               ClearFormat(changeToken, location, run.Length);
            } else if (!(run is NoInfoRun)) {
               // a format starts exactly at this anchor.
               // but the new format may extend further. If so, clear the excess space.
               if (existingRun.Length < run.Length) {
                  ClearFormat(changeToken, existingRun.Start + existingRun.Length, run.Length - existingRun.Length);
               }
            }

            if (anchorForAddress.TryGetValue(location, out string oldAnchorName)) {
               anchorForAddress.Remove(location);
               addressForAnchor.Remove(oldAnchorName);
               changeToken.RemoveName(location, oldAnchorName);
            }

            if (addressForAnchor.ContainsKey(anchorName)) {
               RemoveAnchorByName(changeToken, anchorName);
            }

            // if this anchor was given a name, add it
            if (anchorName != string.Empty) {
               anchorForAddress.Add(location, anchorName);
               addressForAnchor.Add(anchorName, location);
               changeToken.AddName(location, anchorName);
            }

            var seekPointers = existingRun?.PointerSources == null || existingRun?.Start != location;
            var noKnownPointers = run.PointerSources == null || run.PointerSources.Count == 0;
            seekPointers = seekPointers && noKnownPointers;
            var sources = GetSourcesPointingToNewAnchor(changeToken, anchorName, seekPointers);

            // if we're adding an array, a few extra updates
            if (run is ArrayRun array) {
               // update inner pointers and dependent arrays
               if (array.SupportsPointersToElements) run = array.AddSourcesPointingWithinArray(changeToken);
            }

            var newRun = run.MergeAnchor(sources);
            ObserveRunWritten(changeToken, newRun);
         }
      }

      public override void MassUpdateFromDelta(
         IReadOnlyDictionary<int, IFormattedRun> runsToRemove,
         IReadOnlyDictionary<int, IFormattedRun> runsToAdd,
         IReadOnlyDictionary<int, string> namesToRemove,
         IReadOnlyDictionary<int, string> namesToAdd,
         IReadOnlyDictionary<int, string> unmappedPointersToRemove,
         IReadOnlyDictionary<int, string> unmappedPointersToAdd,
         IReadOnlyDictionary<int, string> matchedWordsToRemove,
         IReadOnlyDictionary<int, string> matchedWordsToAdd,
         IReadOnlyDictionary<int,int> offsetPointersToRemove,
         IReadOnlyDictionary<int,int> offsetPointersToAdd
      ) {
         foreach (var kvp in namesToRemove) {
            var (address, name) = (kvp.Key, kvp.Value);
            addressForAnchor.Remove(name);
            anchorForAddress.Remove(address);
         }

         foreach (var kvp in namesToAdd) {
            var (address, name) = (kvp.Key, kvp.Value);
            addressForAnchor[name] = address;
            anchorForAddress[address] = name;
         }

         foreach (var kvp in unmappedPointersToRemove) {
            var (address, name) = (kvp.Key, kvp.Value);
            unmappedNameToSources[name] = unmappedNameToSources[name].Remove1(address);
            if (unmappedNameToSources[name].Count == 0) unmappedNameToSources.Remove(name);
            sourceToUnmappedName.Remove(address);
         }

         foreach (var kvp in unmappedPointersToAdd) {
            var (address, name) = (kvp.Key, kvp.Value);
            if (!unmappedNameToSources.ContainsKey(name)) unmappedNameToSources[name] = SortedSpan<int>.None;
            unmappedNameToSources[name] = unmappedNameToSources[name].Add1(address);
            sourceToUnmappedName[address] = name;
         }

         foreach (var kvp in matchedWordsToRemove) {
            var (address, name) = (kvp.Key, kvp.Value);
            matchedWords[name].Remove(address);
            if (matchedWords[name].Count == 0) matchedWords.Remove(name);
         }

         foreach (var kvp in matchedWordsToAdd) {
            var (address, name) = (kvp.Key, kvp.Value);
            if (!matchedWords.ContainsKey(name)) matchedWords[name] = new List<int>();
            matchedWords[name].Add(address);
         }

         foreach (var kvp in offsetPointersToRemove) {
            if (pointerOffsets.ContainsKey(kvp.Key)) pointerOffsets.Remove(kvp.Key);
         }

         foreach (var kvp in offsetPointersToAdd) pointerOffsets[kvp.Key] = kvp.Value;

         foreach (var kvp in runsToRemove) {
            var index = BinarySearch(kvp.Key);
            if (index >= 0) runs.RemoveAt(index);
         }

         foreach (var kvp in runsToAdd) {
            var index = BinarySearch(kvp.Key);
            if (index >= 0) {
               runs[index] = kvp.Value;
            } else {
               index = ~index;
               if (index < runs.Count) {
                  runs.Insert(index, kvp.Value);
               } else {
                  runs.Add(kvp.Value);
               }
            }
         }
      }

      public override T RelocateForExpansion<T>(ModelDelta changeToken, T run, int minimumLength) {
         int currentLength = run.Length;
         if (run is IScriptStartRun) {
            IReadOnlyList<ScriptLine> lines = null;
            if (run is XSERun) lines = singletons.ScriptLines;
            if (run is BSERun) lines = singletons.BattleScriptLines;
            if (run is ASERun) lines = singletons.AnimationScriptLines;
            currentLength = Math.Max(currentLength, lines.GetScriptSegmentLength(this, run.Start));
         }
         if (minimumLength <= currentLength) return run;
         if (CanSafelyUse(run.Start + currentLength, run.Start + minimumLength)) return run;

         var freeSpace = FindFreeSpace(0x100, minimumLength);
         if (freeSpace >= 0) {
            return MoveRun(changeToken, run, currentLength, freeSpace);
         } else {
            ExpandData(changeToken, RawData.Length + minimumLength);
            return MoveRun(changeToken, run, currentLength, RawData.Length - minimumLength - 1);
         }
      }

      public override int FindFreeSpace(int start, int minimumLength) {
         if (FreeSpaceStart != 0) start = FreeSpaceStart;
         if (start < EarliestAllowedAnchor) start = EarliestAllowedAnchor;
         const int SpacerLength = 0x100;
         minimumLength += 0x140; // make sure there's plenty of room after, so that we're not in the middle of some other data set
         var runIndex = 0;
         while (start < RawData.Length - minimumLength) {
            // catch the currentRun up to where we are
            while (runIndex < runs.Count && runs[runIndex].Start < start) runIndex++;
            var currentRun = runIndex < runs.Count ? runs[runIndex] : NoInfoRun.NullRun;

            // if the space we want intersects the current run, then skip past the current run
            if (start + minimumLength > currentRun.Start) {
               start = currentRun.Start + currentRun.Length + SpacerLength;
               start -= start % 4;
               continue;
            }

            // if the space we want already has some data in it that we don't have a run for, skip it
            var lastConflictingData = -1;
            for (int i = start; i < start + minimumLength; i++) if (RawData[i] != 0xFF) lastConflictingData = i;
            if (lastConflictingData != -1) {
               start = lastConflictingData + SpacerLength;
               start -= start % 4;
               continue;
            }

            // found a good spot!
            // move the run
            FreeSpaceStart = start;
            return start;
         }

         return -1;
      }

      public override void ClearAnchor(ModelDelta changeToken, int start, int length) {
         ClearFormat(changeToken, start, length, keepInitialAnchorPointers: false, alsoClearData: false);
      }

      public override void ClearFormat(ModelDelta changeToken, int originalStart, int length) {
         var run = GetNextRun(originalStart);
         ClearFormat(changeToken, originalStart, length, keepInitialAnchorPointers: run.Start == originalStart, alsoClearData: false);
      }

      public override void ClearFormatAndData(ModelDelta changeToken, int originalStart, int length) {
         ClearFormat(changeToken, originalStart, length, keepInitialAnchorPointers: false, alsoClearData: true);
      }

      public void SetList(string name, IReadOnlyList<string> list) {
         if (list == null && lists.ContainsKey(name)) lists.Remove(name);
         else lists[name] = list.ToList();
      }

      public override bool TryGetList(string name, out IReadOnlyList<string> list) {
         var result = lists.TryGetValueCaseInsensitive(name, out var value);
         list = value;
         return result;
      }

      // for each of the results, we recognized it as text: see if we need to add a matching string run / pointers
      public override int ConsiderResultsAsTextRuns(ModelDelta currentChange, IReadOnlyList<int> searchResults) {
         int resultsRecognizedAsTextRuns = 0;
         var parallelLock = new object();
         Parallel.ForEach(searchResults, result => {
            var run = ConsiderAsTextStream(this, result, currentChange);
            if (run != null) {
               lock (parallelLock) {
                  ObserveAnchorWritten(currentChange, string.Empty, run);
                  resultsRecognizedAsTextRuns++;
               }
            }
         });

         return resultsRecognizedAsTextRuns;
      }

      // if the destination seems to be a PlmStream, adds the anchor and return true.
      public static bool ConsiderAsPlmStream(IDataModel model, int address, ModelDelta currentChange) {
         var nextRun = model.GetNextRun(address);
         if (nextRun.Start < address) return false;
         if (nextRun.Start == address && !(nextRun is NoInfoRun)) return false;
         var run = new PLMRun(model, address);
         if (run.Length < 2) return false;
         if (address + run.Length > nextRun.Start && nextRun.Start != address) return false;
         var pointers = model.SearchForPointersToAnchor(currentChange, address);  // this is slow and change the metadata. Only do it if we're sure we want the new PLMRun
         if (pointers.Count == 0) return false;
         model.ObserveAnchorWritten(currentChange, string.Empty, run.MergeAnchor(pointers));
         return true;
      }

      public static PCSRun ConsiderAsTextStream(IDataModel model, int address, ModelDelta currentChange) {
         var nextRun = model.GetNextRun(address);
         if (nextRun.Start < address) return null;
         if (nextRun.Start == address && !(nextRun is NoInfoRun)) return null;
         var length = PCSString.ReadString(model, address, true);
         if (length < 1) return null;
         if (address + length > nextRun.Start && nextRun.Start != address) return null;
         var pointers = model.SearchForPointersToAnchor(currentChange, address); // this is slow and change the metadata. Only do it if we're sure we want the new PCSRun
         if (pointers.Count == 0) return null;
         return new PCSRun(model, address, length, pointers);
      }

      /// <summary>
      /// Removes a pointer from the list of sources
      /// </summary>
      public override void ClearPointer(ModelDelta currentChange, int source, int destination) {
         var index = BinarySearch(destination);
         currentChange.RemoveRun(runs[index]);

         var newRun = runs[index].RemoveSource(source);
         if (newRun is NoInfoRun nir && nir.PointerSources.Count == 0) {
            runs.RemoveAt(index);
         } else {
            runs[index] = newRun;
            currentChange.AddRun(newRun);
         }
      }

      private void ClearFormat(ModelDelta changeToken, int start, int length, bool keepInitialAnchorPointers, bool alsoClearData) {
         for (var run = GetNextRun(start); length > 0 && run != null; run = GetNextRun(start)) {

            if (alsoClearData && start < run.Start) {
               for (int i = 0; i < length && i < run.Start - start; i++) changeToken.ChangeData(this, start + i, 0xFF);
            }

            if (run.Start >= start + length) return;
            if (run is PointerRun) ClearPointerFormat(null, null, changeToken, run.Start);
            if (run is ITableRun arrayRun) ModifyAnchorsFromPointerArray(changeToken, arrayRun, null, arrayRun.ElementCount, ClearPointerFormat);
            if (run is WordRun wordRun) {
               changeToken.RemoveMatchedWord(wordRun.Start, wordRun.SourceArrayName);
               matchedWords[wordRun.SourceArrayName].Remove(wordRun.Start);
            } else if (run is OffsetPointerRun offsetPointer) {
               changeToken.RemoveOffsetPointer(offsetPointer.Start, offsetPointer.Offset);
               pointerOffsets.Remove(offsetPointer.Start);
            }

            if (GetNextRun(run.Start).Start == run.Start) {
               ClearAnchorFormat(changeToken, keepInitialAnchorPointers, run);
            }

            if (alsoClearData) {
               for (int i = 0; i < run.Length; i++) changeToken.ChangeData(this, run.Start + i, 0xFF);
            }

            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
            keepInitialAnchorPointers = false;
         }
      }

      private void ClearAnchorFormat(ModelDelta changeToken, bool keepPointers, IFormattedRun run) {
         int runIndex;

         // case 1: anchor is named
         // delete the anchor.
         if (anchorForAddress.TryGetValue(run.Start, out string name)) {
            if (!(changeToken is NoDataChangeDeltaModel)) {
               // Clear pointers to it, but keep the names. They're pointers, just not to here anymore.
               foreach (var source in run.PointerSources) {
                  WriteValue(changeToken, source, 0);
                  changeToken.AddUnmappedPointer(source, name);
                  sourceToUnmappedName[source] = name;
               }
               unmappedNameToSources[name] = run.PointerSources;
            } else if (!keepPointers) {
               // Clear pointer formats to it. They're not actually pointers.
               foreach (var source in run.PointerSources) ClearFormat(changeToken, source, 4);
            }
            changeToken.RemoveName(run.Start, name);
            addressForAnchor.Remove(name);
            anchorForAddress.Remove(run.Start);
            runIndex = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            runs.RemoveAt(runIndex);
            return;
         }

         // case 2: unnamed anchor is not really an anchor, so don't keep the pointers
         // this anchor shouldn't exist. The things that point to it aren't real pointers.
         if (!keepPointers) {
            // by removing the unnamed anchor here, we're claiming that these were never really pointers to begin with.
            // as such, we should not change their data, just remove their pointer format
            foreach (var source in run.PointerSources ?? SortedSpan<int>.None) {
               var sourceRunIndex = BinarySearch(source);
               if (sourceRunIndex >= 0 && runs[sourceRunIndex] is PointerRun) {
                  var pointerRun = runs[sourceRunIndex];
                  changeToken.RemoveRun(pointerRun);
                  if (pointerRun.PointerSources == null) {
                     runs.RemoveAt(sourceRunIndex);
                  } else {
                     // remove the pointer, but keep any anchors to that location.
                     var newRun = new NoInfoRun(pointerRun.Start, pointerRun.PointerSources);
                     changeToken.AddRun(newRun);
                     runs[sourceRunIndex] = newRun;
                  }
               } else {
                  // this source is in a table: the source is in error, but we have to leave it anyway.
               }
            }
            runIndex = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            runs.RemoveAt(runIndex);
            return;
         }

         // case 3: unnamed anchor and we want to keep the pointers
         // delete the content, but leave the anchor and pointers to it: we don't want to lose track of the pointers that point here.
         runIndex = BinarySearch(run.Start);
         changeToken.RemoveRun(run);
         if (run.PointerSources != null) {
            runs[runIndex] = new NoInfoRun(run.Start, run.PointerSources);
            changeToken.AddRun(runs[runIndex]);
         } else {
            runs.RemoveAt(runIndex);
         }
      }

      private void ClearPointerFormat(ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, ModelDelta changeToken, int start) {
         // remove the reference from the anchor we're pointing to as well
         var destination = ReadPointer(start);
         if (destination >= 0 && destination < Count) {
            var index = BinarySearch(destination);
            if (index >= 0) {
               ClearPointerFromAnchor(changeToken, start, index);
            } else if (index != -1 && runs[~index - 1] is ArrayRun array) { // if index is -1, we are before the first run, so we're not within an array run
               ClearPointerWithinArray(changeToken, start, ~index - 1, array);
            } else {
               // pointers in tables are allowed to point at junk
            }
         } else if (sourceToUnmappedName.TryGetValue(start, out var name)) {
            changeToken.RemoveUnmappedPointer(start, name);
            sourceToUnmappedName.Remove(start);
            if (unmappedNameToSources[name].Count == 1) {
               unmappedNameToSources.Remove(name);
            } else {
               unmappedNameToSources[name] = unmappedNameToSources[name].Remove1(start);
            }
         }
      }

      private void ClearPointerFromAnchor(ModelDelta changeToken, int start, int index) {
         var anchorRun = runs[index];
         var newAnchorRun = anchorRun.RemoveSource(start);
         changeToken.RemoveRun(anchorRun);

         // the only run that is allowed to exist with nothing pointing to it and no name is a pointer run.
         // if it's any other kind of run with no name and no pointers to it, remove it.
         if (newAnchorRun.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(newAnchorRun.Start) && !(newAnchorRun is PointerRun)) {
            ClearFormat(changeToken, anchorRun.Start, anchorRun.Length, false, false);
         } else if (newAnchorRun.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(newAnchorRun.Start) && newAnchorRun is PointerRun) {
            // if it IS a pointer run, we still need to remove the anchor by setting the pointerSources to null.
            runs[index] = new PointerRun(newAnchorRun.Start);
         } else {
            runs[index] = newAnchorRun;
            changeToken.AddRun(newAnchorRun);
         }
      }

      private void ClearPointerWithinArray(ModelDelta changeToken, int start, int index, ArrayRun array) {
         changeToken.RemoveRun(array);
         var newArray = array.RemoveSource(start);
         runs[index] = newArray;
         changeToken.AddRun(newArray);
      }

      public override void UpdateArrayPointer(ModelDelta changeToken, ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int source, int destination) {
         ClearPointerFormat(segment, null, changeToken, source);
         if (ReadPointer(source) != destination) WritePointer(changeToken, source, destination);
         AddPointerToAnchor(segment, segments, changeToken, source);
      }

      public override string Copy(Func<ModelDelta> changeToken, int start, int length, bool deep = false) {
         var text = new StringBuilder();
         var run = GetNextRun(start);

         using (ModelCacheScope.CreateScope(this)) {
            while (length > 0) {
               run = GetNextRun(start);
               if (run.Start > start) {
                  var len = Math.Min(length, run.Start - start);
                  var bytes = Enumerable.Range(start, len).Select(i => RawData[i].ToHexString());
                  text.Append(string.Join(" ", bytes) + " ");
                  length -= len;
                  start += len;
                  continue;
               }
               if (run.Start == start) {
                  if (!anchorForAddress.TryGetValue(start, out string anchor)) {
                     if ((run.PointerSources?.Count ?? 0) > 0) {
                        anchor = GenerateDefaultAnchorName(run);
                        ObserveAnchorWritten(changeToken(), anchor, run);
                        text.Append($"^{anchor}{run.FormatString} ");
                     }
                  } else {
                     text.Append($"^{anchor}{run.FormatString} ");
                  }
               }
               if (run is PointerRun pointerRun) {
                  var destination = ReadPointer(pointerRun.Start);
                  var anchorName = GetAnchorFromAddress(run.Start, destination);
                  if (string.IsNullOrEmpty(anchorName)) anchorName = destination.ToString("X6");
                  text.Append($"<{anchorName}> ");
                  start += 4;
                  length -= 4;
               } else if (run is NoInfoRun || run is IScriptStartRun) {
                  text.Append(RawData[run.Start].ToHexString() + " ");
                  start += 1;
                  length -= 1;
               } else if (run is IAppendToBuilderRun atbRun) {
                  atbRun.AppendTo(this, text, start, length, deep);
                  text.Append(" ");
                  length -= run.Start + run.Length - start;
                  start = run.Start + run.Length;
               } else {
                  throw new NotImplementedException();
               }
            }
         }

         text.Remove(text.Length - 1, 1); // remove the trailing space
         return text.ToString();
      }

      private string GenerateDefaultAnchorName(IFormattedRun run) {
         var gameCodeText = ReadGameCode(this);
         var textSample = GetSampleText(run);
         var initialAddress = run.Start.ToString("X6");

         return $"misc.{gameCodeText}_{initialAddress}{textSample}";
      }

      /// <summary>
      /// If this model recognizes a GameCode AsciiRun, return that code formatted as a name.
      /// </summary>
      public static string ReadGameCode(IDataModel model) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "GameCode");
         if (address == Pointer.NULL) return string.Empty;
         if (!(model.GetNextRun(address) is AsciiRun gameCode) || gameCode.Start != address) return string.Empty;
         return new string(Enumerable.Range(0, gameCode.Length).Select(i => (char)model[gameCode.Start + i]).ToArray());
      }

      /// <summary>
      /// If the run is text, grab the first 3 words and return it formatted as a name.
      /// </summary>
      private string GetSampleText(IFormattedRun run) {
         if (!(run is PCSRun)) return string.Empty;
         var text = PCSString.Convert(this, run.Start, run.Length);
         var words = text.Split(' ');
         if (words.Length > 3) words = words.Take(3).ToArray();
         text = string.Concat(words);
         return "." + new string(text.Where(char.IsLetterOrDigit).ToArray());
      }

      public override void Load(byte[] newData, StoredMetadata metadata) {
         base.Load(newData, metadata);
         unmappedNameToSources.Clear();
         sourceToUnmappedName.Clear();
         addressForAnchor.Clear();
         anchorForAddress.Clear();
         runs.Clear();
         Initialize(metadata);
      }

      public override IReadOnlyList<string> GetAutoCompleteAnchorNameOptions(string partial) {
         const int MaxResults = 50;
         partial = partial.ToLower();
         var mappedNames = addressForAnchor.Keys.ToList();

         var results = new List<string>();

         if (!partial.Contains(ArrayAnchorSeparator)) {
            foreach (var index in SystemExtensions.FindMatches(partial, mappedNames)) {
               results.Add(mappedNames[index]);
               if (results.Count == MaxResults) break;
            }
            return results;
         }

         foreach (var name in mappedNames) {
            var address = addressForAnchor[name];
            if (GetNextRun(address) is ArrayRun run) {
               var nameParts = partial.Split(ArrayAnchorSeparator);

               if (!name.MatchesPartialWithReordering(nameParts[0])) continue;
               results.AddRange(GetAutoCompleteOptions(name + ArrayAnchorSeparator, run, nameParts.Skip(1).ToArray()));
            }
         }

         // limit it to the first MaxResults options for performance
         if (results.Count > MaxResults) results.RemoveRange(MaxResults, results.Count - MaxResults);
         return results;
      }

      /// <summary>
      /// This recursively looks through parts[], alternating looking for two things:
      /// (1) Find which index of an array we're looking at.
      /// (2) Find which segment of that element we're looking at.
      /// (3) Follow a pointer and go back to (1).
      ///
      /// Since there are multiple cases for what an 'index' can look like, we have to check each case for each index. 2 loops.
      /// Since there can be multiple returns from a pointer, we have to return each recursive result for each segment name. 2 loops.
      ///
      /// ... So this function has 4 nested for-loops, each with multiple conditionals.
      /// </summary>
      private IEnumerable<string> GetAutoCompleteOptions(string prefix, ITableRun run, string[] parts) {
         var childNames = run.ElementNames;
         for (int i = 0; i < run.ElementCount; i++) {
            var options = new List<string> { i.ToString() };
            if (childNames != null && childNames.Count > i && !string.IsNullOrEmpty(childNames[i])) options.Add(childNames[i]);
            foreach (var option in options) {
               if (!option.MatchesPartial(parts[0])) continue;
               if (parts.Length == 1) {
                  yield return prefix + option;
                  continue;
               }
               // looking for a field name
               int segmentOffset = 0;
               foreach (var segment in run.ElementContent) {
                  if (!segment.Name.MatchesPartial(parts[1])) {
                     segmentOffset += segment.Length;
                     continue;
                  }
                  if (parts.Length == 2) {
                     yield return prefix + option + ArrayAnchorSeparator + segment.Name;
                  } else {
                     var childRunStart = ReadPointer(run.Start + run.ElementLength * i + segmentOffset);
                     if (segment.Type != ElementContentType.Pointer) {
                        segmentOffset += segment.Length;
                        continue; // oops, can't follow into a non-pointer segment
                     }
                     var childRun = GetNextRun(childRunStart);
                     if (parts[2] == string.Empty) {
                        yield return prefix + option + ArrayAnchorSeparator + segment.Name + ArrayAnchorSeparator;
                     } else if (childRun is ITableRun tableRun) {
                        foreach (var result in GetAutoCompleteOptions(prefix + option + ArrayAnchorSeparator + segment.Name + ArrayAnchorSeparator, tableRun, parts.Skip(2).ToArray())) {
                           yield return result;
                        }
                     }
                  }
                  segmentOffset += segment.Length;
               }
            }
         }
      }

      public override StoredMetadata ExportMetadata(IMetadataInfo metadataInfo) {
         var anchors = new List<StoredAnchor>();
         foreach (var kvp in anchorForAddress) {
            var (address, name) = (kvp.Key, kvp.Value);
            var index = BinarySearch(address);
            if (index < 0) continue;
            var format = runs[index].FormatString;
            anchors.Add(new StoredAnchor(address, name, format));
         }

         var unmappedPointers = new List<StoredUnmappedPointer>();
         foreach (var kvp in sourceToUnmappedName) {
            var (address, name) = (kvp.Key, kvp.Value);
            unmappedPointers.Add(new StoredUnmappedPointer(address, name));
         }

         var matchedWords = new List<StoredMatchedWord>();
         foreach (var kvp in this.matchedWords) {
            var name = kvp.Key;
            foreach (var address in kvp.Value) {
               matchedWords.Add(new StoredMatchedWord(address, name));
            }
         }

         var offsetPointers = pointerOffsets.Select(kvp => new StoredOffsetPointer(kvp.Key, kvp.Value)).ToList();

         var lists = new List<StoredList>();
         foreach (var kvp in this.lists) {
            var name = kvp.Key;
            var members = kvp.Value.Select((text, i) => i.ToString() == text ? null : text);
            lists.Add(new StoredList(name, members.ToList()));
         }

         return new StoredMetadata(anchors, unmappedPointers, matchedWords, offsetPointers, lists, metadataInfo, FreeSpaceStart);
      }

      /// <summary>
      /// This method might be called in parallel with the same changeToken
      /// </summary>
      public override SortedSpan<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses) {
         var lockObj = new object();
         var results = SortedSpan<int>.None;

         var chunkLength = 0x10000;
         var groups = (int)Math.Ceiling((double)RawData.Length / chunkLength);
         Parallel.For(0, groups, group => {
            var data = RawData;
            var chunkEnd = chunkLength * (group + 1);
            chunkEnd = Math.Min(chunkEnd, data.Length);
            for (int i = chunkLength * group + 3; i < chunkEnd; i++) {
               if (data[i] != 0x08 && data[i] != 0x09) continue;
               var destination = ReadPointer(i - 3);
               if (!addresses.Contains(destination)) continue;
               if (IsValidResult(changeToken, i)) {
                  lock (lockObj) results = results.Add1(i - 3);
               }
            }
         });

         return results;
      }

      private bool IsValidResult(ModelDelta changeToken, int i) {
         // I have to lock this whole block, because I need to know that 'index' remains consistent until I can call runs.Insert
         lock (runs) {
            var index = BinarySearch(i - 3);
            if (index >= 0) {
               if (runs[index] is PointerRun) return true;
               if (runs[index] is ArrayRun arrayRun && arrayRun.ElementContent[0].Type == ElementContentType.Pointer) return true;
               if (runs[index] is NoInfoRun) {
                  var pointerRun = new PointerRun(i - 3, runs[index].PointerSources);
                  changeToken.RemoveRun(runs[index]);
                  changeToken.AddRun(pointerRun);
                  runs[index] = pointerRun;
                  return true;
               }
               return false;
            }
            index = ~index;
            if (index < runs.Count && runs[index].Start <= i) return false; // can't add a pointer run if an existing run starts during the new one

            // can't add a pointer run if the new one starts during an existing one
            if (index > 0 && runs[index - 1].Start + runs[index - 1].Length > i - 3) {
               // ah, but if that run is an array and there's already a pointer here...
               if (runs[index - 1] is ArrayRun array) {
                  var offsets = array.ConvertByteOffsetToArrayOffset(i);
                  if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer) {
                     return true;
                  }
               }
               return false;
            }
            var newRun = new PointerRun(i - 3);
            runs.Insert(index, newRun);
            changeToken.AddRun(newRun);
         }

         return true;
      }

      private static (string, string) SplitNameAndFormat(string text) {
         var name = text.Substring(1).Trim(); // lop off leading ^
         string format = string.Empty;
         int split = -1;

         if (name.Contains(AnchorStart.ToString() + ArrayStart)) {
            split = name.IndexOf(AnchorStart);
         } else if (name.Contains(ArrayStart)) {
            split = name.IndexOf(ArrayStart);
         } else if (name.Contains(StringDelimeter)) {
            split = name.IndexOf(StringDelimeter);
         } else if (name.Contains(StreamDelimeter)) {
            split = name.IndexOf(StreamDelimeter);
         }

         if (split != -1) {
            format = name.Substring(split);
            name = name.Substring(0, split);
         }

         return (name, format);
      }

      private static ErrorInfo TryParseFormat(IDataModel model, string name, string format, int dataIndex, out IFormattedRun run) {
         run = new NoInfoRun(dataIndex);
         var existingRun = model.GetNextRun(dataIndex);
         if (existingRun.Start == run.Start) run = run.MergeAnchor(existingRun.PointerSources);

         // special case: empty format, stick with the no-info run
         if (format == string.Empty) return ErrorInfo.NoError;

         return FormatRunFactory.GetStrategy(format)?.TryParseData(model, name, dataIndex, ref run) ?? new ErrorInfo($"Format {format} was not understood."); ;
      }

      private static ErrorInfo ValidateAnchorNameAndFormat(IDataModel model, IFormattedRun runToWrite, string name, string format, int dataIndex, bool allowAnchorOverwrite = false) {
         var existingRun = model.GetNextRun(dataIndex);
         var nextAnchor = model.GetNextAnchor(dataIndex + 1);

         if (name.ToLower() == "null") {
            return new ErrorInfo("'null' is a reserved word and cannot be used as an anchor name.");
         } else if (name == string.Empty && existingRun.Start != dataIndex) {
            // if there isn't already a run here, then clearly there's nothing pointing here
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (name == string.Empty && existingRun.PointerSources.Count == 0 && format != string.Empty) {
            // the next run DOES start here, but nothing points to it
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (!allowAnchorOverwrite && nextAnchor.Start < runToWrite.Start + runToWrite.Length) {
            return new ErrorInfo("An existing anchor starts before the new one ends.");
         } else if (!name.All(c => char.IsLetterOrDigit(c) || "-._".Contains(c))) { // at this point, the name might have a "-1" on the end, so still allow the dash
            return new ErrorInfo("Anchor names must contain only letters, numbers, dots, and underscores.");
         } else {
            return ErrorInfo.NoError;
         }
      }

      private void RemoveAnchorByName(ModelDelta changeToken, string anchorName) {
         var index = BinarySearch(addressForAnchor[anchorName]);
         var oldAnchor = runs[index];
         changeToken.RemoveRun(oldAnchor);
         runs.RemoveAt(index);
         var oldAnchorName = anchorForAddress[oldAnchor.Start];

         foreach (var source in oldAnchor.PointerSources ?? SortedSpan<int>.None) {
            WriteValue(changeToken, source, 0);
            sourceToUnmappedName[source] = oldAnchorName;
            changeToken.AddUnmappedPointer(source, oldAnchorName);
         }

         unmappedNameToSources[oldAnchorName] = oldAnchor.PointerSources;
         var nameToRemove = anchorForAddress[oldAnchor.Start];
         addressForAnchor.Remove(nameToRemove);
         anchorForAddress.Remove(oldAnchor.Start);
         changeToken.RemoveName(oldAnchor.Start, nameToRemove);
      }

      /// <summary>
      /// if there are unmapped sources trying to point to this name, point them at the new anchor
      /// </summary>
      /// <returns>
      /// The list of sources that point at the new anchor
      /// </returns>
      private SortedSpan<int> GetSourcesPointingToNewAnchor(ModelDelta changeToken, string anchorName, bool seakPointers) {
         if (!addressForAnchor.TryGetValue(anchorName, out int location)) return SortedSpan<int>.None;     // new anchor is unnamed, so nothing points to it yet

         if (!unmappedNameToSources.TryGetValue(anchorName, out var sources)) {
            // no pointer was waiting for this anchor to be created
            // but the user thinks there's something pointing here
            if (seakPointers) return SearchForPointersToAnchor(changeToken, location);
            return SortedSpan<int>.None;
         }

         foreach (var source in sources) {
            var index = BinarySearch(source);
            if (index >= 0 && runs[index] is ITableRun array1) {
               Debug.Assert(array1.ElementContent[0].Type == ElementContentType.Pointer);
            } else if (index < 0 && runs[~index - 1] is ITableRun array2) {
               var offsets = array2.ConvertByteOffsetToArrayOffset(source);
               Debug.Assert(array2.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer);
            } else {
               Debug.Assert(index >= 0 && runs[index] is PointerRun);
            }
            changeToken.RemoveUnmappedPointer(source, anchorName);
            sourceToUnmappedName.Remove(source);
            WritePointer(changeToken, source, location);
         }
         unmappedNameToSources.Remove(anchorName);

         return sources;
      }

      private T MoveRun<T>(ModelDelta changeToken, T run, int length, int newStart) where T : IFormattedRun {
         // repoint
         foreach (var source in run.PointerSources) {
            WritePointer(changeToken, source, newStart);
         }

         // move data
         for (int i = 0; i < length; i++) {
            changeToken.ChangeData(this, newStart + i, RawData[run.Start + i]);
            changeToken.ChangeData(this, run.Start + i, 0xFF);
         }

         // move run
         var newRun = (T)run.Duplicate(newStart, run.PointerSources);
         if (newRun is ITableRun array) {
            UpdateAnchorsFromArrayMove(changeToken, (ITableRun)run, array);
         }

         int index = BinarySearch(run.Start);
         changeToken.RemoveRun(runs[index]);
         runs.RemoveAt(index);
         int newIndex = BinarySearch(newStart);
         runs.Insert(~newIndex, newRun);
         changeToken.AddRun(newRun);

         // move anchor
         if (anchorForAddress.TryGetValue(run.Start, out var name)) {
            addressForAnchor[name] = newRun.Start;
            anchorForAddress.Remove(run.Start);
            anchorForAddress[newRun.Start] = name;
            changeToken.RemoveName(run.Start, name);
            changeToken.AddName(newRun.Start, name);
         }

         return newRun;
      }

      private bool CanSafelyUse(int rangeStart, int rangeEnd) {
         // only safe to use if there is no run in that range
         var nextRun = GetNextRun(rangeStart);

         // ignore a runs of length zero that begin at the requested rangeStart
         // because space after a run of length zero is obviously safe to use when extending that run.
         // in this case, we actually care about accidentally butting up against the _next_ run.
         if (nextRun.Start == rangeStart && nextRun.Length == 0) nextRun = GetNextRun(rangeStart + 1);

         if (nextRun.Start < rangeEnd) return false;
         if (rangeEnd >= RawData.Length) return false;

         // make sure the data is clear
         for (int i = rangeStart; i < rangeEnd; i++) if (RawData[i] != 0xFF && RawData[i] != 0x00) return false;

         return true;
      }

      // if an existing run starts exactly at start, return that index
      // otherwise, return a number such that ~index would be inserted into the list at the correct index
      // so ~index - 1 is the previous run, and ~index is the next run
      private int BinarySearch(int start) {
         var index = runs.BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         return index;
      }
   }

   public static class StringDictionaryExtensions {
      public static bool TryGetValueCaseInsensitive<T>(this IDictionary<string, T> self, string key, out T value) {
         foreach (var option in self.Keys) {
            if (key.Equals(option, StringComparison.CurrentCultureIgnoreCase)) {
               value = self[option];
               return true;
            }
         }

         value = default;
         return false;
      }
   }
}
