using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
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

//*
using RunPath = System.Int32;
/*/
using RunPath = HavenSoft.HexManiac.Core.Models.SearchTree<HavenSoft.HexManiac.Core.Models.Runs.IFormattedRun>.SearchPath;
//*/

namespace HavenSoft.HexManiac.Core.Models {
   public class PokemonModel : BaseModel {

      #region Toggles for while we're working on the SearchTree
      //*

      // list of runs, in sorted address order. Includes no names
      private readonly IList<IFormattedRun> runs = new List<IFormattedRun>();

      private void SetIndex(RunPath index, IFormattedRun run) => runs[index] = run;

      private void InsertIndex(RunPath index, IFormattedRun existingRun) => runs.Insert(index, existingRun);

      private void RemoveIndex(RunPath index) => runs.RemoveAt(index);

      // if an existing run starts exactly at start, return that index
      // otherwise, return a number such that ~index would be inserted into the list at the correct index
      // so ~index - 1 is the previous run, and ~index is the next run
      private RunPath BinarySearch(int start) {
         var index = ((List<IFormattedRun>)runs).BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         return index;
      }

      private RunPath BinarySearchNext(int start) {
         var index = ((List<IFormattedRun>)runs).BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         if (index < 0) index = ~index;
         return index;
      }

      private IEnumerable<IFormattedRun> RunsStartingFrom(int dataIndex) {
         var index = BinarySearchNext(dataIndex);
         for (; index < runs.Count; index++) {
            yield return runs[index];
         }
      }

      /*/

      // list of runs, in sorted address order. Includes no names
      private readonly SearchTree<IFormattedRun> runs = new SearchTree<IFormattedRun>();

      private void SetIndex(RunPath index, IFormattedRun run) => runs.Add(run);

      private void InsertIndex(RunPath index, IFormattedRun existingRun) => runs.Add(existingRun);

      private void RemoveIndex(RunPath index) => runs.Remove(index.Element.Start);

      // if an existing run starts exactly at start, return that index
      // otherwise, return a number such that ~index would be inserted into the list at the correct index
      // so ~index - 1 is the previous run, and ~index is the next run
      private RunPath BinarySearch(int start) => runs[start];

      private RunPath BinarySearchNext(int start) => runs[start];

      private IEnumerable<IFormattedRun> RunsStartingFrom(int dataIndex) => runs.StartingFrom(dataIndex);

      //*/
      #endregion

      // for a name, where is it?
      // for a location, what is its name?
      private readonly IDictionary<string, int> addressForAnchor = new ThreadSafeDictionary<string, int>();
      private readonly Dictionary<int, string> anchorForAddress = new Dictionary<int, string>();

      // for a name not actually in the file, what pointers point to it?
      // for a pointer pointing to something not actually in the file, what name is it pointing to?
      private readonly Dictionary<string, SortedSpan<int>> unmappedNameToSources = new Dictionary<string, SortedSpan<int>>();
      private readonly Dictionary<int, string> sourceToUnmappedName = new Dictionary<int, string>();

      private readonly Dictionary<string, int> unmappedConstants = new Dictionary<string, int>();

      // for a name of a table (which may not actually be in the file),
      // get the list of addresses in the file that want to store a number that matches the length of the table.
      private readonly Dictionary<string, ISet<int>> matchedWords = new Dictionary<string, ISet<int>>();

      // a list of all the offsets for all known offset pointers. This information is duplicated in the OffsetPointerRun.
      private readonly Dictionary<int, int> pointerOffsets = new Dictionary<int, int>();

      private readonly Dictionary<string, ValidationList> lists = new Dictionary<string, ValidationList>();

      private readonly Singletons singletons;
      private readonly bool showRawIVByteForTrainer;

      #region Pointer destination-to-source caching, for faster pointer search during initial load

      private IDictionary<int, SortedSpan<int>> sourcesForDestinations;

      /// <summary>
      /// setup a cache to make loading faster
      /// </summary>
      private void BuildDestinationToSourceCache(byte[] data) {
         sourcesForDestinations = new Dictionary<int, SortedSpan<int>>();
         for (int i = 3; i < data.Length; i++) {
            if (data[i] != 0x08 && data[i] != 0x09) continue;
            var source = i - 3;
            var destination = ReadPointer(source);
            if (destination < 0 || destination >= data.Length) continue;
            if (!sourcesForDestinations.ContainsKey(destination)) sourcesForDestinations.Add(destination, SortedSpan<int>.None);
            sourcesForDestinations[destination] = sourcesForDestinations[destination].Add1(source);
         }
      }

      public override byte this[int index] {
         get => base[index];
         set {
            base[index] = value;
            ClearPointerCache();
         }
      }

      private void ClearPointerCache() => sourcesForDestinations = null;

      #endregion

      public virtual int EarliestAllowedAnchor => 0;

      public override IReadOnlyList<string> ListNames => lists.Keys.ToList();
      public override IReadOnlyList<ArrayRun> Arrays {
         get {
            lock (threadlock) {
               var results = new List<ArrayRun>();
               foreach (var address in anchorForAddress.Keys) {
                  var index = BinarySearch(address);
                  if (index < 0) continue;
                  if (runs[index] is ArrayRun arrayRun) results.Add(arrayRun);
               }
               return results;
            }
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
         showRawIVByteForTrainer = metadata?.ShowRawIVByteForTrainer ?? false;
         this.FormatRunFactory = new FormatRunFactory(showRawIVByteForTrainer);
         BuildDestinationToSourceCache(data);

         // if we have a subclass, expect the subclass to do this when it's ready.
         if (GetType() == typeof(PokemonModel)) {
            InitializationWorkload = (singletons?.WorkDispatcher ?? InstantDispatch.Instance).RunBackgroundWork(() => Initialize(metadata));
         }
      }

      protected void Initialize(StoredMetadata metadata) {
         {
            var pointersForDestination = new Dictionary<int, SortedSpan<int>>();
            var destinationForSource = new SortedList<int, int>();
            SearchForPointers(pointersForDestination, destinationForSource);
            WritePointerRuns(pointersForDestination, destinationForSource);
            WriteSpriteRuns(pointersForDestination);
            WriteStringRuns(pointersForDestination);
            FreeSpaceStart = EarliestAllowedAnchor;

            if (metadata == null) return;
            var noChange = new NoDataChangeDeltaModel();

            // metadata is more important than anything already found
            foreach (var list in metadata.Lists) {
               lists[list.Name] = new ValidationList(list.Hash, list);
            }
            foreach (var anchor in metadata.NamedAnchors) {
               // since we're loading metadata, we're pretty sure that the anchors in the metadata are right.
               // therefore, allow those anchors to overwrite anything we found during the initial quick-search phase.
               using (ModelCacheScope.CreateScope(this)) {
                  ApplyAnchor(this, noChange, anchor.Address, AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
               }
            }
            foreach (var unmappedPointer in metadata.UnmappedPointers) {
               sourceToUnmappedName[unmappedPointer.Address] = unmappedPointer.Name;
               if (!unmappedNameToSources.ContainsKey(unmappedPointer.Name)) unmappedNameToSources[unmappedPointer.Name] = SortedSpan<int>.None;
               unmappedNameToSources[unmappedPointer.Name] = unmappedNameToSources[unmappedPointer.Name].Add1(unmappedPointer.Address);
               if (GetNextRun(unmappedPointer.Address).Start >= unmappedPointer.Address + 4 && ReadPointer(unmappedPointer.Address) == Pointer.NULL) {
                  ObserveRunWritten(noChange, new PointerRun(unmappedPointer.Address));
               }
            }
            foreach (var word in metadata.MatchedWords) {
               if (word.Address + word.Length >= Count) continue;
               if (!matchedWords.ContainsKey(word.Name)) matchedWords.Add(word.Name, new HashSet<int>());
               matchedWords[word.Name].Add(word.Address);
               var index = BinarySearch(word.Address);
               WordRun newRun;
               if (index > 0) {
                  newRun = new WordRun(word.Address, word.Name, word.Length, word.AddOffset, word.MultOffset, word.Note, runs[index].PointerSources);
               } else {
                  newRun = new WordRun(word.Address, word.Name, word.Length, word.AddOffset, word.MultOffset, word.Note);
               }
               ClearFormat(noChange, word.Address, word.Length);
               ObserveRunWritten(noChange, newRun);
               CompleteCellEdit.UpdateAllWords(this, newRun, noChange, this.ReadMultiByteValue(word.Address, word.Length), true);
            }
            RemoveMatchedWordsThatDoNotMatch(noChange);
            foreach (var offsetPointer in metadata.OffsetPointers) {
               if (offsetPointer.Address + 4 >= Count) continue;
               var newRun = new OffsetPointerRun(offsetPointer.Address, offsetPointer.Offset);
               ClearFormat(noChange, newRun.Start, newRun.Length);
               pointerOffsets[offsetPointer.Address] = offsetPointer.Offset;
               ObserveRunWritten(noChange, newRun);
            }
            foreach (var unmappedConstant in metadata.UnmappedConstants) {
               unmappedConstants.Add(unmappedConstant.Name, unmappedConstant.Value);
            }

            this.LoadMetadataProperties(metadata);

            FillGotoModels(metadata);

            TableGroups.AddRange(metadata.TableGroups);

            if (!metadata.IsEmpty && StoredMetadata.NeedVersionUpdate(metadata.Version, singletons?.MetadataInfo.VersionNumber ?? "0")) {
               var gameCode = this.GetGameCode();
               if (singletons.GameReferenceTables.TryGetValue(gameCode, out var tables)) {
                  var metadatas = GetDefaultMetadatas(gameCode.Substring(0, 4), gameCode);
                  UpdateRuns(tables, metadatas);
               }
            } else {
               // didn't run an update. Now that all the constants are setup correctly, do a quick second pass through the anchors to look for any that failed the first time.
               // for example, if the types were loaded before the number of types was known, then the type chart probably failed to load.
               foreach (var anchor in metadata.NamedAnchors) {
                  if (addressForAnchor.ContainsKey(anchor.Name)) continue;
                  ApplyAnchor(this, noChange, anchor.Address, AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
               }
            }

            if (GetType() == typeof(PokemonModel)) ResolveConflicts();
         }
      }

      private void RemoveMatchedWordsThatDoNotMatch(ModelDelta token) {
         foreach (var key in matchedWords.Keys.ToList()) {
            bool allMatch = true;
            var addresses = matchedWords[key].ToList();
            foreach (var address in addresses) {
               var run = GetNextRun(address) as WordRun;
               if (run == null) { allMatch = false; break; }
               if (address == addresses[0]) continue;
               var firstRun = (WordRun)GetNextRun(addresses[0]);
               var virtualTargetValue = firstRun.Read(this);
               var virtualActualValue = run.Read(this);
               if (virtualActualValue != virtualTargetValue) {
                  allMatch = false;
                  break;
               }
            }
            if (!allMatch) {
               var run = GetNextRun(addresses[0]) as WordRun;
               var name = run?.SourceArrayName ?? $"Constant at {addresses[0]:X2}";
               // {name} will be removed because not all the uses match.
               foreach (var address in addresses) {
                  ClearFormat(token, address, 1);
               }
            }
         }
      }

      /// <summary>
      /// Delete whatever TableGroups have matching Hashes: those haven't been edited by the user.
      /// </summary>
      private void ClearNoEditTableGroups() {
         var groupsToRemove = new List<TableGroup>(TableGroups.Where(group => group.HashMatches));
         groupsToRemove.ForEach(group => TableGroups.Remove(group));
      }

      private void UpdateRuns(GameReferenceTables referenceTables, IEnumerable<StoredMetadata> metadatas) {
         var noChange = new NoDataChangeDeltaModel();

         ClearNoEditTableGroups();

         if (singletons.GameReferenceConstants != null && singletons.GameReferenceConstants.TryGetValue(this.GetGameCode(), out var referenceConstants)) {
            var metadata = HardcodeTablesModel.DecodeConstantsFromReference(this, singletons.MetadataInfo, new StoredMetadata(new string[0]), referenceConstants);
            this.LoadMetadata(metadata);
         }

         // if there's any differences between the previous metadata and the new metadata, go ahead and use the new metadata.
         // this allows for default metadata updates when member names change, but may overwrite manual changes made by the user.
         // We're not currently worried about that, since we don't expect that to be a common use case.
         foreach (var metadata in metadatas) {
            this.LoadMetadata(metadata);
         }

         var changedLocations = new HashSet<int>();

         foreach (var reference in referenceTables) {
            if (reference.Address + 4 > Count) continue;
            var destination = base.ReadPointer(reference.Address) - reference.Offset;
            if (!anchorForAddress.ContainsKey(destination) && !addressForAnchor.ContainsKey(reference.Name)) {
               ApplyAnchor(this, noChange, destination, "^" + reference.Name + reference.Format, allowAnchorOverwrite: true);
               changedLocations.Add(destination);
               continue;
            }

            if (!anchorForAddress.TryGetValue(destination, out var anchor)) continue;
            var existingRun = GetNextRun(destination);
            if (anchor == reference.Name && existingRun.Start == destination && existingRun.FormatString == reference.Format) continue;
            if (TryParseFormat(this, reference.Name, reference.Format, destination, out var replacementRun).HasError) continue;
            if (DoNotChangeFormatOnUpdate(existingRun.FormatString)) continue;
            // update this anchor
            anchorForAddress[destination] = reference.Name;
            addressForAnchor.Remove(anchor);
            addressForAnchor[reference.Name] = destination;

            // update the run, if the new one can drop-in replace the old one. Used for updating field names or general format
            if (existingRun.Start == replacementRun.Start && existingRun.Length <= replacementRun.Length && existingRun.FormatString != replacementRun.FormatString) {
               ObserveAnchorWritten(noChange, reference.Name, replacementRun);
               changedLocations.Add(destination);
            }

            // update runs that care about this name
            for (int i = 0; i < runs.Count; i++) {
               // update matched-length lengths
               if (runs[i] is ArrayRun array) {
                  var parentName = array.LengthFromAnchor;
                  if (!anchorForAddress.TryGetValue(array.Start, out var childTableName)) childTableName = null;
                  if (parentName == anchor) {
                     var lengthModifier = array.FormatString.Split(parentName).Last();
                     var newLengthToken = reference.Name + array.LengthFromAnchor.Substring(parentName.Length);
                     var arrayClose = array.FormatString.LastIndexOf(']');
                     var newFormat = array.FormatString.Substring(0, arrayClose + 1);
                     TryParse(this, newFormat + newLengthToken + lengthModifier, array.Start, array.PointerSources, out var newRun);
                     ClearFormat(noChange, newRun.Start, newRun.Length);
                     if (childTableName == null) {
                        ObserveRunWritten(noChange, newRun);
                     } else {
                        ObserveAnchorWritten(noChange, childTableName, newRun);
                     }
                     i = BinarySearch(newRun.Start);
                  }
                  if (parentName == reference.Name) {
                     // there's a table that depends on the new name, which wasn't available yet.
                     // re-evaluate that table to get the right length.
                     TryParse(this, array.FormatString, array.Start, array.PointerSources, out var newRun);
                     ClearFormat(noChange, newRun.Start, newRun.Length);
                     if (childTableName == null) {
                        ObserveRunWritten(noChange, newRun);
                     } else {
                        ObserveAnchorWritten(noChange, childTableName, newRun);
                     }
                     i = BinarySearch(newRun.Start);
                  }
               }

               if (changedLocations.Contains(runs[i].Start)) continue; // we've already updated this run, no need to check it again

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
               if (runs[i] is ITilemapRun tilemap) {
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
                  sprite = sprite.Duplicate(new SpriteFormat(format.BitsPerPixel, format.TileWidth, format.TileHeight, reference.Name, format.AllowLengthErrors));
                  runs[i] = sprite;
               }

               // update dependent streams
               if (runs[i] is IStreamRun streamRun && streamRun.DependsOn(reference.Name)) {
                  // clear/observe is heavy-handed, but it clears any stray pointers
                  var newRun = streamRun.Duplicate(streamRun.Start, streamRun.PointerSources);
                  ClearFormat(noChange, newRun.Start, newRun.Length);
                  ObserveRunWritten(noChange, newRun);
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
         for (int i = 0; i < runs.Count; i++) {
            if (!anchorForAddress.TryGetValue(runs[i].Start, out var pointerSourceName)) pointerSourceName = string.Empty;
            else pointerSourceName = " (" + pointerSourceName + ")";

            // for every pointer run, make sure that the thing it points to knows about it
            if (runs[i] is PointerRun pointerRun) {
               var destination = ReadPointer(pointerRun.Start);
               var run = GetNextRun(destination);
               if (destination < 0 || destination >= Count) {
                  // pointer points outside scope. Such a pointer is an error, but is not a metadata inconsistency.
               } else if (run is ArrayRun arrayRun1 && arrayRun1.SupportsInnerPointers) {
                  var offsets = arrayRun1.ConvertByteOffsetToArrayOffset(destination);
                  Debug.Assert(arrayRun1.PointerSourcesForInnerElements[offsets.ElementIndex].Contains(pointerRun.Start));
                  if (offsets.ElementIndex == 0) Debug.Assert(run.PointerSources.Contains(pointerRun.Start));
               } else if (run.Start != destination) {
                  Debug.Fail($"Pointer at {pointerRun.Start:X6} expected a run at {destination:X6} but the next run was at {run.Start:X6}.");
               } else if (run != NoInfoRun.NullRun) {
                  Debug.Assert(run.PointerSources != null && run.PointerSources.Contains(pointerRun.Start), $"Expected run at {run.Start:X6} to know about pointer at {pointerRun.Start:X6}, but it did not.");
               }
            }

            // for every TPTRun, make sure something points to it
            if (runs[i] is TrainerPokemonTeamRun) Debug.Assert(runs[i].PointerSources.Count > 0, "TPTRuns must not exist with no content long-term.");

            // for ever NoInfoRun, something points to it
            if ((runs[i] is NoInfoRun || runs[i] is PointerRun) && !anchorForAddress.ContainsKey(runs[i].Start)) {
               Debug.Assert(runs[i].PointerSources == null || runs[i].PointerSources.Count > 0, $"{runs[i].Start:X6}: Unnamed NoInfoRuns must have something pointing to them!");
            }

            // for every run with sources, make sure the pointer at that source actually points to it
            if (runs[i].PointerSources != null) {
               foreach (var source in runs[i].PointerSources) {
                  var run = GetNextRun(source);
                  if (run is PointerRun) {
                     Debug.Assert(run.Start == source, $"{runs[i].Start:X6}{pointerSourceName} expects a pointer at {source:X6}, but the next pointer was found at {run.Start:X6}.");
                     Debug.Assert(ReadPointer(source) == runs[i].Start, $"Expected {source:X6} to point to {runs[i].Start:X6}{pointerSourceName}");
                  } else if (run is ITableRun) {
                     Debug.Assert(run.Start <= source, $"The run at {runs[i].Start:X6} expects a pointer at {source:X6}, but found a table at {run.Start:X6}.");
                     var destination = ReadPointer(source);
                     Debug.Assert(destination == runs[i].Start, $"The run at {runs[i].Start:X6} expects a pointer at {source:X6}, but that source points to {destination:X6}.");
                  } else {
                     Debug.Fail($"Pointer at {source:X6} must be a {nameof(PointerRun)} or live within an {nameof(ITableRun)} (pointing to {runs[i].Start:X6})");
                  }
               }
            }
            if (runs[i] is ArrayRun arrayRun2 && arrayRun2.SupportsInnerPointers) {
               for (int j = 0; j < arrayRun2.ElementCount; j++) {
                  foreach (var source in arrayRun2.PointerSourcesForInnerElements[j]) {
                     var run = GetNextRun(source);
                     if (run is PointerRun) {
                        Debug.Assert(run.Start == source, $"{runs[i].Start:X6}{pointerSourceName} index {j} expects a pointer at {source:X6}, but the next pointer was found at {run.Start:X6}.");
                        Debug.Assert(ReadPointer(source) == runs[i].Start + arrayRun2.ElementLength * j, $"Expected {source:X6} to point to {runs[i].Start:X6}{pointerSourceName} index {j}");
                     } else if (run is ITableRun) {
                        Debug.Assert(ReadPointer(source) == runs[i].Start + arrayRun2.ElementLength * j, $"Expected {source:X6} to point to {runs[i].Start:X6}{pointerSourceName} index {j}");
                     } else {
                        Debug.Fail($"Pointer at {source:X6} must be a {nameof(PointerRun)} or live within an {nameof(ITableRun)}");
                     }
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
                     } else if (run is ArrayRun arrayRun1 && arrayRun1.SupportsInnerPointers) {
                        var offsets = arrayRun1.ConvertByteOffsetToArrayOffset(destination);
                        if (offsets.SegmentOffset == 0) {
                           Debug.Assert(arrayRun1.PointerSourcesForInnerElements[offsets.ElementIndex].Contains(start));
                           if (offsets.ElementIndex == 0) Debug.Assert(run.PointerSources.Contains(start));
                        } else {
                           // pointer points into an element (not the beginning). This is an error, but is not a metadata inconsistency.
                        }
                     } else if (run is ITableRun && run.Start < destination) {
                        // exception: tables are allowed to have pointers that point randomly into other runs.
                        // such a thing is a data error in the ROM, but is not a metadata inconsistency.
                     } else if (run.Start != destination) {
                        // for tables, the invalidly point into a run. Such is an error in the data, but is allowed for the metadata.
                     } else {
                        if (run.PointerSources != null) {
                           Debug.Assert(run.PointerSources.Contains(start), $"Expected {run.Start:X6} to know about pointer {start:X6} (within table {tableRun.Start:X6}), but it did not.");
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
            Debug.Fail($"Conflict: there's a run that ends after the next run starts! {debugRunStart1}{pointerSourceName} and {debugRunStart2}");
         }

         // For every table with a matched-length, verify that the length is as expected.
         // (The child array length must still be at least 1.)
         var token = new NoDataChangeDeltaModel();
         foreach (var array in Arrays) {
            if (string.IsNullOrEmpty(array.LengthFromAnchor)) continue;
            var parentName = array.LengthFromAnchor;
            var childName = GetAnchorFromAddress(-1, array.Start);
            if (matchedWords.TryGetValue(parentName, out var set)) {
               foreach (var wordAddress in set) {
                  if (GetNextRun(wordAddress) is WordRun word) {
                     var expectedElementCount = (this.ReadMultiByteValue(word.Start, word.Length) - word.ValueOffset) / word.MultOffset;
                     Debug.Assert(array.ElementCount == expectedElementCount, $"Expected {childName} to have {expectedElementCount} elements because of {parentName}, but it had {array.ElementCount} elements instead!");
                  } else {
                     Debug.Fail("Expected a constant at " + wordAddress.ToAddress() + " but didn't find one!");
                  }
               }
            }
            if (!(GetNextRun(GetAddressFromAnchor(token, -1, array.LengthFromAnchor)) is ITableRun parent)) continue;
            if (array.ParentOffset.BeginningMargin + array.ParentOffset.EndMargin + parent.ElementCount > 0) {
               var expectedChildLength = parent.ElementCount + array.ParentOffset.BeginningMargin + array.ParentOffset.EndMargin;
               Debug.Assert(expectedChildLength == array.ElementCount, $"Expected table {childName} to be {expectedChildLength} elements based on {parentName}, but it was {array.ElementCount} elements instead.");
            } else {
               Debug.Assert(array.ElementCount == 1);
            }
         }
      }

      private bool DoNotChangeFormatOnUpdate(string format) {
         // move utility changes the format of moves.stats: effects is now 2 bytes and other formats have moved.
         if (format.Contains("[effect:") && format.Contains(" pp. ")) return true;

         // move utility changes the format of moves.levelup: pointer is now to a series of 4-byte tokens
         if (format.Contains(" level:]!FFFFFFFF>]")) return true;

         return false;
      }

      private void FillGotoModels(StoredMetadata metadata) {
         var shortcuts = (IList<GotoShortcutModel>)GotoShortcuts;
         foreach (var shortcut in metadata.GotoShortcuts) {
            if (shortcuts.Any(s => s.DisplayText == shortcut.Display)) continue; // don't double-add the same shortcut
            shortcuts.Add(new GotoShortcutModel(shortcut.Image, shortcut.Anchor, shortcut.Display));
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
            if (runToWrite.ContainsOnlyPointerToSelf()) {
               errorInfo = new ErrorInfo($"{name} could not be added at {dataIndex:X6} because no pointers were found.", true);
            } else {
               model.ObserveAnchorWritten(changeToken, name, runToWrite);
            }
         }

         return errorInfo;
      }

      public static ErrorInfo UniquifyName(IDataModel model, ModelDelta changeToken, int desiredAddressForName, ref string name) {
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

      public override IReadOnlyList<int> GetMatchedWords(string name) {
         if (matchedWords.TryGetValue(name, out var list)) return list.ToList();
         return new int[0];
      }

      public override SortedSpan<int> GetUnmappedSourcesToAnchor(string anchor) {
         if (!unmappedNameToSources.TryGetValue(anchor, out var list)) return SortedSpan<int>.None;
         return list;
      }

      public override void SetUnmappedConstant(ModelDelta changeToken, string name, int value) {
         unmappedConstants[name.ToLower()] = value;
         changeToken.AddUnmappedConstant(name.ToLower(), value);
      }

      public override bool TryGetUnmappedConstant(string name, out int value) => unmappedConstants.TryGetValue(name.ToLower(), out value);

      public override int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor) {

         var nameparts = anchor.Split('/');
         anchor = nameparts.First();

         if (addressForAnchor.TryGetValueCaseInsensitive(anchor, out int address)) {
            nameparts = nameparts.Skip(1).ToArray();
            if (nameparts.Length > 0) address = GetAddressFromAnchor(address, nameparts);
            return address;
         }

         // check if it's a named constant with a valid index specifier
         if (anchor.Contains("~")) {
            var constantParts = anchor.Split('~');
            if (
               constantParts.Length == 2 &&
               int.TryParse(constantParts[1], out int constantIndex) &&
               matchedWords.TryGetValue(constantParts[0], out var constantAddresses) &&
               constantAddresses.Count >= constantIndex &&
               constantIndex > 0
            ) {
               var sortedConstantAddresses = constantAddresses.OrderBy(i => i).ToList();
               return sortedConstantAddresses[constantIndex - 1];
            }
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

      /// <summary>
      /// If no anchor is found, return string.Empty.
      /// Never returns null.
      /// </summary>
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

      private readonly object threadlock = new object();
      public override IFormattedRun GetNextRun(int dataIndex) {
         if (dataIndex == Pointer.NULL) return NoInfoRun.NullRun;
         lock (threadlock) {
            var index = GetIndexForNextRun(dataIndex);
            if (index >= runs.Count) return NoInfoRun.NullRun;
            return runs[index];
         }
      }

      private int GetIndexForNextRun(int address) {
         var index = BinarySearch(address);
         if (index >= 0) return index;
         index = ~index;
         if (index > 0) {
            var previous = runs[index - 1];
            if (previous.Start + previous.Length > address) index -= 1;
         }
         return index;
      }

      public override IFormattedRun GetNextAnchor(int dataIndex) {
         foreach (var run in RunsStartingFrom(dataIndex)) {
            if (run.Start < dataIndex) continue;
            if (run.PointerSources == null) continue;
            return run;
         }
         return NoInfoRun.NullRun;
      }

      public override bool TryGetUsefulHeader(int address, out string header) {
         header = null;
         // only produce headers for arrays with length based on other arrays that start with a text member.
         var run = GetNextRun(address);
         if (run.Start > address) return false;
         if (!(run is ArrayRun array)) {
            if (run.PointerSources != null && run.PointerSources.Count > 0 && run.Start == address) {
               var parentRun = GetNextRun(run.PointerSources[0]);
               if (parentRun is ArrayRun parentArray) {
                  array = parentArray;
                  var arrayIndex = parentArray.ConvertByteOffsetToArrayOffset(run.PointerSources[0]).ElementIndex;
                  address = parentArray.Start + arrayIndex * parentArray.ElementLength;
               } else {
                  return false;
               }
            } else {
               return false;
            }
         }

         if ((address - array.Start) % array.ElementLength != 0) return false;

         var index = (address - array.Start) / array.ElementLength;
         if (array.ElementNames.Count <= index) return false;
         header = array.ElementNames[index];

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
         lock (threadlock) {
            if (run is ArrayRun array) {
               // update any words who's name matches this array's name
               if (anchorForAddress.TryGetValue(run.Start, out var anchorName)) {
                  if (matchedWords.TryGetValue(anchorName, out var words) && !(changeToken is NoDataChangeDeltaModel)) {
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
               if (existingRun is ITableRun arrayRun1 && run is ITableRun tableRun1) {
                  ModifyAnchorsFromPointerArray(changeToken, tableRun1, arrayRun1, arrayRun1.ElementCount, ClearPointerFormat);
                  index = BinarySearch(run.Start); // have to recalculate index, because ClearPointerFormat can removed runs.
               }
               SetIndex(index, run);
               changeToken.AddRun(run);
            }

            if (run is WordRun word && word.Start + word.Length <= Count) {
               if (!matchedWords.ContainsKey(word.SourceArrayName)) matchedWords[word.SourceArrayName] = new HashSet<int>();
               matchedWords[word.SourceArrayName].Add(word.Start);
               changeToken.AddMatchedWord(this, word.Start, word.SourceArrayName, word.Length);
               CompleteCellEdit.UpdateAllWords(this, word, changeToken, this.ReadMultiByteValue(word.Start, word.Length), true);
            } else if (run is OffsetPointerRun offsetPointer) {
               pointerOffsets[offsetPointer.Start] = offsetPointer.Offset;
               changeToken.AddOffsetPointer(offsetPointer.Start, offsetPointer.Offset);
            }

            if (run is PointerRun) AddPointerToAnchor(null, null, 0, changeToken, run.Start);
            if (run is ITableRun tableRun) ModifyAnchorsFromPointerArray(changeToken, tableRun, existingRun as ITableRun, tableRun.ElementCount, AddPointerToAnchor);
            if (run is ArrayRun arrayRun) UpdateDependantArrayLengths(changeToken, arrayRun);

            if (run is NoInfoRun && run.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(run.Start)) {
               // this run has no useful information. Remove it.
               changeToken.RemoveRun(runs[index]);
               RemoveIndex(index);
            }

            if (existingRun is ArrayRun arrayRun2 && arrayRun2.SupportsInnerPointers && arrayRun2.Length > run.Length) {
               for (int i = 0; i < arrayRun2.ElementCount; i++) {
                  if (arrayRun2.ElementLength * i < run.Length) continue;
                  ObserveRunWritten(changeToken, new NoInfoRun(arrayRun2.Start + arrayRun2.ElementLength * i, arrayRun2.PointerSourcesForInnerElements[i]));
               }
            }
         }
      }

      public override int ReadPointer(int index) {
         var destination = base.ReadPointer(index);
         if (pointerOffsets.TryGetValue(index, out int offset)) destination -= offset;
         return destination;
      }

      public override bool WritePointer(ModelDelta changeToken, int address, int pointerDestination) {
         if (pointerOffsets.TryGetValue(address, out int offset)) pointerDestination += offset;
         return base.WritePointer(changeToken, address, pointerDestination);
      }

      /// <summary>
      /// A new array just came in. It might have pointers.
      /// When we make a new pointer, we need to update anchors to include the new pointer.
      /// So update all the anchors based on any new pointers in this newly added array.
      /// </summary>
      private void ModifyAnchorsFromPointerArray(ModelDelta changeToken, ITableRun arrayRun, ITableRun previousTable, int elementCount, Action<ArrayRunElementSegment, IReadOnlyList<ArrayRunElementSegment>, int, ModelDelta, int> changeAnchors) {
         int segmentOffset = arrayRun.Start;
         var formatMatches = previousTable != null && arrayRun.DataFormatMatches(previousTable);
         var parentOffset = 0;
         if (arrayRun is ArrayRun arrayRun1) parentOffset = Math.Max(arrayRun1.ParentOffset.EndMargin, 0);
         var shorterTable = Math.Min(arrayRun.ElementCount, previousTable?.ElementCount ?? arrayRun.ElementCount);
         // i loops over the different segments in the array
         for (int i = 0; i < arrayRun.ElementContent.Count; i++) {
            if (arrayRun.ElementContent[i].Type != ElementContentType.Pointer) { segmentOffset += arrayRun.ElementContent[i].Length; continue; }
            // for a pointer segment, j loops over all the elements in the array
            var range = elementCount.Range();
            if (arrayRun.ElementContent[i] is ArrayRunPointerSegment pSeg && pSeg.InnerFormat.EndsWith("?")) range = range.Reverse();
            foreach (int j in range) {
               if (formatMatches && shorterTable - parentOffset > j) continue; // we can skip this one
               var start = segmentOffset + arrayRun.ElementLength * j;
               changeAnchors(arrayRun.ElementContent[i], arrayRun.ElementContent, j, changeToken, start);
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
               if (destination == moved.Start) destinationRun = moved;
               changeToken.RemoveRun(destinationRun);

               if (destinationRun is ISupportInnerPointersRun destinationTable && destinationTable.SupportsInnerPointers) {
                  // special case: use the override methods to handle inner-pointers
                  destinationTable = destinationTable.RemoveInnerSource(originalStart);
                  destinationRun = destinationTable.AddSourcePointingWithinRun(movedStart);
               } else {
                  destinationRun = destinationRun.RemoveSource(originalStart);
                  destinationRun = destinationRun.MergeAnchor(new SortedSpan<int>(movedStart));
               }

               changeToken.AddRun(destinationRun);
               var runIndex = BinarySearch(destinationRun.Start);
               if (runIndex >= 0) {
                  SetIndex(runIndex, destinationRun);
               }
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
         var dependentArrays = this.GetDependantArrays(anchor).ToList();
         foreach (var table in dependentArrays) {
            var newTable = table;

            // runs may have changed since getting the dependent arrays
            // try to update newTable to be the most recent version from the model
            newTable = GetNextRun(newTable.Start) as ArrayRun;
            if (newTable == null || newTable.Start != table.Start) newTable = table; // update failed

            // option 1: this table's length is based on the given table
            if (anchor.Equals(table.LengthFromAnchor)) {
               int targetCount = arrayRun.ElementCount + table.ParentOffset.BeginningMargin + table.ParentOffset.EndMargin;
               if (newTable.ElementCount == targetCount) continue;
               // only relocate if we're not in a loading situation
               if (!(changeToken is NoDataChangeDeltaModel)) {
                  newTable = RelocateForExpansion(changeToken, table, targetCount * table.ElementLength);
               }
               int originalLength = newTable.Length;

               // clear any possible metadata in the way of appending (only matters if we didn't relocate
               // note that we need to do this _before_ Append is called
               var lengthChange = (targetCount - table.ElementCount) * newTable.ElementLength;
               if (lengthChange > 0) ClearFormat(changeToken, newTable.Start + originalLength, lengthChange);

               var tableAnchor = GetAnchorFromAddress(-1, newTable.Start);
               newTable = newTable.Append(changeToken, targetCount - table.ElementCount);

               // clear any possible remaining metadata after contracting
               // note that we need to do this _after_ Append is called
               if (newTable.Length < originalLength) ClearFormat(changeToken, newTable.Start, originalLength);

               if (string.IsNullOrEmpty(tableAnchor)) {
                  ObserveRunWritten(changeToken, newTable);
               } else {
                  ObserveAnchorWritten(changeToken, tableAnchor, newTable);
               }

               // if this run has pointers, those may have been cleared by some earlier update
               this.InsertPointersToRun(changeToken, newTable);
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
                  foreach (var _ in (newLength - segment.Length).Range()) {
                     changeToken.ChangeData(this, destinationIndex, 0);
                     destinationIndex--;
                  }
               }
               // move the source data to the destination point
               foreach (var _ in movingSegment.Length.Range()) {
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
      private void AddPointerToAnchor(ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, ModelDelta changeToken, int start) {
         var destination = ReadPointer(start);
         if (destination < 0 || destination >= Count) return;
         var index = BinarySearch(destination);
         if (index < 0 && ~index > 0 && runs[~index - 1] is ArrayRun array &&
            array.SupportsInnerPointers &&
            array.Start + array.Length > destination &&
            (destination - array.Start) % array.ElementLength == 0) {
            // the pointer points into an array that supports inner anchors
            index = ~index - 1;
            changeToken.RemoveRun(array);
            SetIndex(index, array.AddSourcePointingWithinRun(start));
            changeToken.AddRun(runs[index]);
         } else if (index < 0) {
            // the pointer points to a location between existing runs
            IFormattedRun newRun = new NoInfoRun(destination, new SortedSpan<int>(start));
            UpdateNewRunFromPointerFormat(ref newRun, segment as ArrayRunPointerSegment, segments, parentIndex, changeToken);
            if (newRun != null) {
               ClearFormat(changeToken, newRun.Start, newRun.Length); // adding a new destination, so clear anything in the way.
               ObserveRunWritten(changeToken, newRun);
            }
         } else if (runs[index].Start <= start && start < runs[index].Start + runs[index].Length) {
            // self-referential pointer: don't write a new run, just add the pointer
            var existingRun = runs[index];
            changeToken.RemoveRun(existingRun);
            existingRun = existingRun.MergeAnchor(SortedSpan.One(start));
            SetIndex(index, existingRun);
            changeToken.AddRun(existingRun);
         } else {
            // the pointer points to a known normal anchor
            var existingRun = runs[index];
            var previousRun = existingRun;
            existingRun = existingRun.MergeAnchor(new SortedSpan<int>(start));
            UpdateNewRunFromPointerFormat(ref existingRun, segment as ArrayRunPointerSegment, segments, parentIndex, changeToken);
            if (existingRun != null) {
               if (segment == null) {
                  // it's just a naked pointer, so we have no knowledge about the thing it points to.
                  index = BinarySearch(destination); // runs could've been removed during UpdateNewRunFromPointerFormat: search for the index again.
                  if (index < 0) {
                     InsertIndex(~index, existingRun);
                  } else {
                     SetIndex(index, existingRun);
                  }
                  changeToken.RemoveRun(previousRun);
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
      private void UpdateNewRunFromPointerFormat(ref IFormattedRun run, ArrayRunPointerSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, ModelDelta token) {
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

         FormatRunFactory.GetStrategy(segment.InnerFormat).UpdateNewRunFromPointerFormat(this, token, segment.Name, segments, parentIndex, ref run);
      }

      public override void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run) {
         Debug.Assert(run.Length > 0); // writing an anchor of length zero is stupid.
         lock (threadlock) {
            int location = run.Start;
            var index = BinarySearch(location);

            var existingRun = (index >= 0 && index < runs.Count) ? runs[index] : null;

            if (existingRun == null || existingRun.Start != run.Start) {
               // no format starts exactly at this anchor, so clear any format that goes over this anchor.
               ClearFormat(changeToken, location, run.Length);
            } else if (!(run is NoInfoRun)) {
               // a format starts exactly at this anchor.
               // but the new format may extend further. If so, clear the excess space.
               if (existingRun.Length < run.Length) {
                  ClearFormatAndAnchors(changeToken, existingRun.Start + existingRun.Length, run.Length - existingRun.Length);
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
            var sources = GetSourcesPointingToNewAnchor(changeToken, anchorName, run, seekPointers).Add(run.PointerSources);
            // remove any sources that were added _within_ the existing run
            for (int i = 0; i < sources.Count; i++) {
               if (sources[i] <= run.Start || sources[i] >= run.Start + run.Length) continue;
               ClearFormat(changeToken, sources[i], 4);
               sources = sources.Remove1(sources[i]);
               i -= 1;
            }

            // if we're adding an array, a few extra updates
            IFormattedRun newRun;
            if (run is ArrayRun array) {
               // update inner pointers and dependent arrays
               if (array.SupportsInnerPointers) run = array.AddSourcesPointingWithinArray(changeToken);
               newRun = run.MergeAnchor(sources);
            } else {
               newRun = run.Duplicate(run.Start, sources);
            }

            ObserveRunWritten(changeToken, newRun);

            ClearCacheScope();
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
         IReadOnlyDictionary<int, int> offsetPointersToRemove,
         IReadOnlyDictionary<int, int> offsetPointersToAdd,
         IReadOnlyDictionary<string, int> unmappedConstantsToRemove,
         IReadOnlyDictionary<string, int> unmappedConstantsToAdd,
         IReadOnlyDictionary<string, ValidationList> listsToRemove,
         IReadOnlyDictionary<string, ValidationList> listsToAdd
      ) {
         foreach (var kvp in listsToRemove) lists.Remove(kvp.Key);
         foreach (var kvp in listsToAdd) {
            var newList = new ValidationList(kvp.Value.StoredHash);
            newList.AddRange(kvp.Value);
            lists.Add(kvp.Key, newList);
         }

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
            if (!matchedWords.ContainsKey(name)) matchedWords[name] = new HashSet<int>();
            matchedWords[name].Add(address);
         }

         foreach (var kvp in offsetPointersToRemove) {
            if (pointerOffsets.ContainsKey(kvp.Key)) pointerOffsets.Remove(kvp.Key);
         }

         foreach (var kvp in offsetPointersToAdd) pointerOffsets[kvp.Key] = kvp.Value;

         foreach (var kvp in unmappedConstantsToRemove) {
            if (unmappedConstants.ContainsKey(kvp.Key)) unmappedConstants.Remove(kvp.Key);
         }

         foreach (var kvp in unmappedConstantsToAdd) unmappedConstants[kvp.Key] = kvp.Value;

         foreach (var kvp in runsToRemove) {
            var index = BinarySearch(kvp.Key);
            if (index >= 0) runs.RemoveAt(index);
         }

         foreach (var kvp in runsToAdd) {
            var index = BinarySearch(kvp.Key);
            if (index >= 0) {
               SetIndex(index, kvp.Value);
            } else {
               index = ~index;
               Debug.Assert(kvp.Value != null);
               if (index < runs.Count) {
                  InsertIndex(index, kvp.Value);
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
         minimumLength += 0x140; // make sure there's plenty of room after, so that we're not in the middle of some other data set
         var runIndex = 0;
         while (start < RawData.Length - minimumLength) {
            // catch the currentRun up to where we are
            while (runIndex < runs.Count && runs[runIndex].Start < start) runIndex++;
            var currentRun = runIndex < runs.Count ? runs[runIndex] : NoInfoRun.NullRun;

            // if the space we want intersects the current run, then skip past the current run
            if (start + minimumLength > currentRun.Start) {
               start = currentRun.Start + currentRun.Length + FreeSpaceBuffer;
               var modulo = start % 4;
               if (modulo != 0) start += 4 - modulo;
               continue;
            }

            // if the space we want already has some data in it that we don't have a run for, skip it
            var lastConflictingData = -1;
            for (int i = start; i < start + minimumLength; i++) if (RawData[i] != 0xFF) lastConflictingData = i;
            if (lastConflictingData != -1) {
               start = lastConflictingData + Math.Max(4, FreeSpaceBuffer);
               var modulo = start % 4;
               if (modulo != 0) start += 4 - modulo;
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
         lock (threadlock) {
            var run = GetNextRun(originalStart);
            ClearFormat(changeToken, originalStart, length, keepInitialAnchorPointers: run.Start == originalStart, alsoClearData: false);
         }
      }

      private void ClearFormatAndAnchors(ModelDelta changeToken, int originalStart, int length) {
         ClearFormat(changeToken, originalStart, length, keepInitialAnchorPointers: false, alsoClearData: false);
      }

      public override void ClearData(ModelDelta changeToken, int start, int length) {
         var run = GetNextRun(start);
         if (run.Start <= start && run is IAppendToBuilderRun builder) {
            Debug.Assert(run.Start + run.Length >= start + length, "Cannot clear data (without format) across runs.");
            builder.Clear(this, changeToken, start, length);
         } else {
            base.ClearData(changeToken, start, length);
         }
      }

      public override void ClearFormatAndData(ModelDelta changeToken, int originalStart, int length) {
         using (ModelCacheScope.CreateScope(this)) {
            ClearFormat(changeToken, originalStart, length, keepInitialAnchorPointers: false, alsoClearData: true);
         }
      }

      public override void SetList(ModelDelta changeToken, string name, IReadOnlyList<string> list, string hash) {
         if (!lists.TryGetValue(name, out var oldContent)) oldContent = null;
         if (list == null && lists.ContainsKey(name)) lists.Remove(name);
         else {
            lists[name] = new ValidationList(hash, list);
         }
         changeToken.ChangeList(name, oldContent, new ValidationList(hash, list));
      }

      public override bool TryGetList(string name, out ValidationList list) {
         return lists.TryGetValueCaseInsensitive(name, out list);
      }

      // for each of the results, we recognized it as text: see if we need to add a matching string run / pointers
      public override int ConsiderResultsAsTextRuns(Func<ModelDelta> futureChange, IReadOnlyList<int> searchResults) {
         int resultsRecognizedAsTextRuns = 0;
         foreach (var result in searchResults) {
            var run = ConsiderAsTextStream(this, result, futureChange);
            if (run != null) {
               ObserveAnchorWritten(futureChange(), string.Empty, run);
               resultsRecognizedAsTextRuns++;
            }
         }

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

      public static PCSRun ConsiderAsTextStream(IDataModel model, int address, Func<ModelDelta> futureCurrentChange) {
         var nextRun = model.GetNextRun(address);
         if (nextRun.Start < address) return null;
         if (nextRun.Start == address && !(nextRun is NoInfoRun)) return null;
         var length = PCSString.ReadString(model, address, true);
         if (length < 1) return null;
         if (address + length > nextRun.Start && nextRun.Start != address) return null;
         var pointers = model.SearchForPointersToAnchor(futureCurrentChange(), address); // this is slow and change the metadata. Only do it if we're sure we want the new PCSRun
         if (pointers.Count == 0) return null;
         return new PCSRun(model, address, length, pointers);
      }

      /// <summary>
      /// Removes a pointer from the list of sources
      /// </summary>
      public override void ClearPointer(ModelDelta currentChange, int source, int destination) {
         var index = BinarySearch(destination);
         if (index < 0) return; // nothing to remove at the destination
         currentChange.RemoveRun(runs[index]);

         var newRun = runs[index].RemoveSource(source);
         if (newRun is NoInfoRun && newRun.PointerSources.Count == 0) {
            // run carries no info, just remove it
            RemoveIndex(index);
         } else if (newRun is PointerRun && newRun.PointerSources.Count == 0) {
            // run carries no pointer info: remove the anchor
            SetIndex(index, new PointerRun(newRun.Start));
            if (newRun is OffsetPointerRun opr) SetIndex(index, new OffsetPointerRun(newRun.Start, opr.Offset));
            currentChange.AddRun(runs[index]);
         } else {
            SetIndex(index, newRun);
            currentChange.AddRun(newRun);
         }
      }

      private void ClearFormat(ModelDelta changeToken, int start, int length, bool keepInitialAnchorPointers, bool alsoClearData) {
         for (var run = GetNextRun(start); length > 0 && run != null; run = GetNextRun(start)) {

            if (alsoClearData && start < run.Start) {
               for (int i = 0; i < length && i < run.Start - start; i++) {
                  if (start + i < Count) {
                     changeToken.ChangeData(this, start + i, 0xFF);
                  }
               }
            }

            if (run.Start >= start + length) return;
            if (run is PointerRun) ClearPointerFormat(null, null, 0, changeToken, run.Start);
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
               if (run is ArrayRun array && array.SupportsInnerPointers) {
                  for (int i = 0; i < array.PointerSourcesForInnerElements.Count; i++) {
                     foreach (var source in array.PointerSourcesForInnerElements[i]) {
                        WriteValue(changeToken, source, i);
                        changeToken.AddUnmappedPointer(source, name);
                        sourceToUnmappedName[source] = name;
                     }
                     unmappedNameToSources[name] = unmappedNameToSources[name].Add(array.PointerSourcesForInnerElements[i]);
                  }
               }
            } else if (!keepPointers) {
               // Clear pointer formats to it. They're not actually pointers.
               foreach (var source in run.PointerSources) {
                  if (source > run.Start && source < run.Start + run.Length) continue;
                  ClearPointerFormat(changeToken, source);
               }
               if (run is ArrayRun table && table.SupportsInnerPointers) {
                  foreach (var sources in table.PointerSourcesForInnerElements) {
                     foreach (var source in sources) {
                        if (source > run.Start && source < run.Start + run.Length) continue;
                        ClearPointerFormat(changeToken, source);
                     }
                  }
               }
            }
            changeToken.RemoveName(run.Start, name);
            addressForAnchor.Remove(name);
            anchorForAddress.Remove(run.Start);
            runIndex = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            runs.RemoveAt(runIndex);
            if (keepPointers && run is ArrayRun table1 && table1.SupportsInnerPointers) AddAnchorsForRemovedArray(table1, changeToken);
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
            if (runIndex >= 0) runs.RemoveAt(runIndex); // if the run was a pointer, it may've already been removed in the previous step
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

      private void AddAnchorsForRemovedArray(ArrayRun table, ModelDelta token) {
         for (int i = 1; i < table.ElementCount; i++) {
            var sources = table.PointerSourcesForInnerElements[i];
            if (sources == null || sources.Count == 0) continue;
            var destination = table.Start + table.ElementLength * i;
            ObserveRunWritten(token, new NoInfoRun(destination, sources));
         }
      }

      private void ClearPointerFormat(ModelDelta changeToken, int source) {
         var run = GetNextRun(source);
         if (run is PointerRun && run.Start == source) ClearFormat(changeToken, source, 4);
      }

      private void ClearPointerFormat(ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, ModelDelta changeToken, int start) {
         if (start < 0 || start > Count - 3) return; // no need to clear the format, this location isn't valid
         // remove the reference from the anchor we're pointing to as well
         var destination = ReadPointer(start);
         if (destination >= 0 && destination < Count) {
            var index = BinarySearch(destination);
            if (index >= 0) {
               ClearPointerFromAnchor(changeToken, start, index);
            } else if (index != -1 && runs[~index - 1] is ArrayRun array) { // if index is -1, we are before the first run, so we're not within an array run
               ClearPointerWithinArray(changeToken, start, array, ~index - 1);
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

      private void ClearPointerFromAnchor(ModelDelta changeToken, int start, RunPath index) {
         var anchorRun = runs[index];
         var newAnchorRun = anchorRun.RemoveSource(start);

         // the only run that is allowed to exist with nothing pointing to it and no name is a pointer run.
         // if it's any other kind of run with no name and no pointers to it, remove it.
         if (newAnchorRun.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(newAnchorRun.Start) && !(newAnchorRun is PointerRun)) {
            if (anchorRun.Start <= start && anchorRun.Start + anchorRun.Length > start) {
               // calling ClearFormat would try to clear the element we're already removing
               // no need to do that: This element should get removed higher up the callstack.
               changeToken.RemoveRun(anchorRun);
            } else {
               ClearFormat(changeToken, anchorRun.Start, anchorRun.Length, false, false);
            }
         } else if (newAnchorRun.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(newAnchorRun.Start) && newAnchorRun is PointerRun) {
            // if it IS a pointer run, we still need to remove the anchor by setting the pointerSources to null.
            changeToken.RemoveRun(anchorRun);
            SetIndex(index, new PointerRun(newAnchorRun.Start));
         } else {
            changeToken.RemoveRun(anchorRun);
            SetIndex(index, newAnchorRun);
            changeToken.AddRun(newAnchorRun);
         }
      }

      private void ClearPointerWithinArray(ModelDelta changeToken, int start, ArrayRun array, RunPath index) {
         changeToken.RemoveRun(array);
         var newArray = array.RemoveSource(start);
         SetIndex(index, newArray);
         changeToken.AddRun(newArray);
      }

      public override void UpdateArrayPointer(ModelDelta changeToken, ArrayRunElementSegment segment, IReadOnlyList<ArrayRunElementSegment> segments, int parentIndex, int source, int destination) {
         ClearPointerFormat(segment, null, 0, changeToken, source);
         if (ReadPointer(source) != destination) WritePointer(changeToken, source, destination);
         AddPointerToAnchor(segment, segments, parentIndex, changeToken, source);
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
               if (run.Start == start && run.Length <= length) {
                  if (run is LZRun lzRun) {
                     // the user is copying an lzrun. Make sure to include a metacommand to insert a new lzrun during the paste.
                     text.Append($"@!lz({lzRun.DecompressedLength}) ");
                  } else if (run is ITableRun tableRun) {
                     // the user is copying a table run. Make sure to include a metacommand to insert 00 of the appropriate length during the paste.
                     // this makes sure that we have enough freespace and makes deep copy for pointers work correctly.
                     text.Append($"@!00({tableRun.Length}) ");
                     if (tableRun is TableStreamRun tableStream) {
                        var defaultStream = tableStream.CreateDefault().Select(b => b.ToString("X2")).Aggregate(string.Empty, string.Concat);
                        if (defaultStream.Length > 0) {
                           defaultStream = new string('0', 2 * tableStream.ElementLength) + defaultStream;
                           text.Append($"@!put({defaultStream}) ");
                        }
                     }
                  }
                  if (!anchorForAddress.TryGetValue(start, out string anchor)) {
                     if ((run.PointerSources?.Count ?? 0) > 0) {
                        anchor = GenerateDefaultAnchorName(run);
                        var token = changeToken();
                        if (token != null) ObserveAnchorWritten(token, anchor, run);
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
                  var offset = string.Empty;
                  if (pointerRun is OffsetPointerRun offsetPointerRun) {
                     if (offsetPointerRun.Offset > 0) offset = "+" + offsetPointerRun.Offset.ToString("X6");
                     if (offsetPointerRun.Offset < 0) offset = "-" + (-offsetPointerRun.Offset).ToString("X6");
                  }
                  text.Append($"<{anchorName}{offset}> ");
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
               } else if (run is AsciiRun ascii) {
                  var textLength = Math.Min(ascii.Length, length);
                  for (int i = 0; i < textLength; i++) {
                     text.Append((char)RawData[start + i]);
                  }
                  start += textLength;
                  length -= textLength;
                  text.Append(" ");
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

         var defaultName = $"misc.temp.{gameCodeText}_{initialAddress}{textSample}";
         if (!addressForAnchor.ContainsKey(defaultName)) return defaultName;

         int counter = 0;
         while (true) {
            counter++;
            if (!addressForAnchor.ContainsKey(defaultName + "_" + counter)) return defaultName + "_" + counter;
         }
      }

      /// <summary>
      /// If this model recognizes a GameCode AsciiRun, return that code formatted as a name.
      /// </summary>
      public static string ReadGameCode(IDataModel model) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "data.header.gamecode");
         if (address == Pointer.NULL) return string.Empty;
         if (!(model.GetNextRun(address) is AsciiRun gameCode) || gameCode.Start != address) return string.Empty;
         return new string(gameCode.Length.Range().Select(i => (char)model[gameCode.Start + i]).ToArray());
      }

      /// <summary>
      /// If the run is text, grab the first 3 words and return it formatted as a name.
      /// </summary>
      private string GetSampleText(IFormattedRun run) {
         if (!(run is PCSRun)) return string.Empty;
         var text = TextConverter.Convert(this, run.Start, run.Length);
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
         lists.Clear();
         pointerOffsets.Clear();
         unmappedConstants.Clear();
         TableGroups.Clear();
         matchedWords.Clear();
         InitializationWorkload = (singletons?.WorkDispatcher ?? InstantDispatch.Instance).RunBackgroundWork(() => {
            BuildDestinationToSourceCache(newData);
            Initialize(metadata);
         });
      }

      public override IEnumerable<string> GetAutoCompleteAnchorNameOptions(string partial, int maxResults = 30) {
         lock (threadlock) {
            partial = partial.ToLower();
            var mappedNames = addressForAnchor.Keys.ToList();

            var resultsCount = 0;

            if (!partial.Contains(ArrayAnchorSeparator)) {
               foreach (var index in SystemExtensions.FindMatches(partial, mappedNames)) {
                  yield return mappedNames[index];
                  resultsCount += 1;
                  if (resultsCount == maxResults) break;
               }
               yield break;
            }

            var nameParts = partial.Split(ArrayAnchorSeparator);
            var seekBits = nameParts[0].BitLetters();

            foreach (var name in mappedNames) {
               var address = addressForAnchor[name];
               if (GetNextRun(address) is ArrayRun run) {

                  var sanitizedName = name.Replace("é", "e");
                  var includedBits = sanitizedName.BitLetters();
                  if ((seekBits & ~includedBits) != 0) continue;
                  if (!sanitizedName.MatchesPartialWithReordering(nameParts[0])) continue;
                  foreach (var option in GetAutoCompleteOptions(name + ArrayAnchorSeparator, run, nameParts.Skip(1).ToArray())) {
                     yield return option;
                     resultsCount += 1;
                     if (resultsCount >= maxResults) yield break;
                  }
               }
            }
         }
      }

      public override IEnumerable<string> GetAutoCompleteByteNameOptions(string text) {
         var seekBits = text.BitLetters();
         var matchedWordsCopy = matchedWords.Keys.ToList();
         foreach (var key in matchedWordsCopy) {
            var includedBits = key.BitLetters();
            if ((seekBits & ~includedBits) != 0) continue;
            if (key.MatchesPartialWithReordering(text)) {
               yield return key;
            }
         }
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
            if (name.StartsWith("misc.temp.")) continue; // don't persist miscilaneous temp anchors
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
               var run = GetNextRun(address) as WordRun;
               matchedWords.Add(new StoredMatchedWord(address, name, run?.Length ?? 4, run?.ValueOffset ?? 0, run?.MultOffset ?? 1, run?.Note));
            }
         }

         var offsetPointers = pointerOffsets.Select(kvp => new StoredOffsetPointer(kvp.Key, kvp.Value)).ToList();

         var lists = new List<StoredList>();
         foreach (var kvp in this.lists) {
            var name = kvp.Key;
            var members = kvp.Value.Select((text, i) => i.ToString() == text ? null : text);
            lists.Add(new StoredList(name, members.ToList(), kvp.Value.StoredHash));
         }

         var unmappedConstants = new List<StoredUnmappedConstant>();
         foreach (var kvp in this.unmappedConstants) {
            var name = kvp.Key;
            var value = kvp.Value;
            unmappedConstants.Add(new StoredUnmappedConstant(name, value));
         }

         var gotoShortcuts = new List<StoredGotoShortcut>();
         foreach(var shortcut in this.GotoShortcuts) {
            gotoShortcuts.Add(new StoredGotoShortcut(shortcut.DisplayText, shortcut.ImageAnchor, shortcut.GotoAnchor));
         }

         return new StoredMetadata(anchors, unmappedPointers, matchedWords, offsetPointers, lists, unmappedConstants, gotoShortcuts, TableGroups.ToList(), metadataInfo,
            new StoredMetadataFields {
               FreeSpaceSearch = FreeSpaceStart,
               FreeSpaceBuffer = FreeSpaceBuffer,
               NextExportID = NextExportID,
               ShowRawIVByteForTrainer = showRawIVByteForTrainer,
            });
      }

      /// <summary>
      /// This method might be called in parallel with the same changeToken
      /// </summary>
      public override SortedSpan<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses) {
         if (sourcesForDestinations != null) return SpanFromCache(changeToken, addresses);

         var lockObj = new object();
         var results = SortedSpan<int>.None;
         var runsToAdd = new List<PointerRun>();

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
               if (TryMakePointerAtAddress(changeToken, i - 3, out var newRun)) {
                  lock (lockObj) {
                     results = results.Add1(i - 3);
                     if (newRun != null) runsToAdd.Add(newRun);
                  }
               }
            }
         });

         foreach (var newRun in runsToAdd) {
            var index = ~BinarySearch(newRun.Start);
            runs.Insert(index, newRun);
            changeToken.AddRun(newRun);
         }
         return results;
      }

      private SortedSpan<int> SpanFromCache(ModelDelta token, int[] destinations) {
         var results = SortedSpan<int>.None;
         foreach (var destination in destinations) {
            if (sourcesForDestinations.TryGetValue(destination, out var sources)) results = results.Add(sources);
         }

         // remove sources that are already in use in other ways
         for (int i = 0; i < results.Count; i++) {
            if (!TryMakePointerAtAddress(token, results[i], out var newRun)) {
               results = results.Remove1(results[i]);
               i -= 1;
            } else if (newRun != null) {
               // NOTE don't ObserveRunWritten here! That will automatically add not only the Pointer, but also an anchor. Example: Unbound-bt-d1.3.1, it causes a conflict where an anchor is added into the type names _while_ we're adding the table that contains that inner anchor.
               var index = ~BinarySearch(newRun.Start);
               InsertIndex(index, newRun);
               token.AddRun(newRun);
            }
         }

         return results;
      }

      /// <summary>
      /// Returns true if the model is able to detect a valid pointer at that address.
      /// Returns a new pointer run to add if the valid pointer doesn't have a matching run yet.
      ///
      /// This method can be called from a parellel context, so it doesn't make any changes to the runs collection.
      /// Instead, it returns a new pointer run if one needs to be added.
      /// </summary>
      private bool TryMakePointerAtAddress(ModelDelta changeToken, int address, out PointerRun runToAdd) {
         // I have to lock this whole block, because I need to know that 'index' remains consistent until I can call runs.Insert
         runToAdd = null;
         lock (runs) {
            var index = BinarySearch(address);
            if (index >= 0) {
               if (runs[index] is PointerRun) return true;
               if (runs[index] is ArrayRun arrayRun && arrayRun.ElementContent[0].Type == ElementContentType.Pointer) return true;
               if (runs[index] is NoInfoRun) {
                  var pointerRun = new PointerRun(address, runs[index].PointerSources);
                  changeToken.RemoveRun(runs[index]);
                  changeToken.AddRun(pointerRun);
                  runs[index] = pointerRun;
                  return true;
               }
               return false;
            }
            index = ~index;
            if (index < runs.Count && runs[index].Start <= address + 3) return false; // can't add a pointer run if an existing run starts during the new one

            // can't add a pointer run if the new one starts during an existing one
            if (index > 0 && runs[index - 1].Start + runs[index - 1].Length > address) {
               // ah, but if that run is an array and there's already a pointer here...
               if (runs[index - 1] is ArrayRun array) {
                  var offsets = array.ConvertByteOffsetToArrayOffset(address + 3);
                  // should this check that offsets.SegmentOffset == 0?
                  if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer) {
                     return true;
                  }
               }
               return false;
            }
         }
         runToAdd = new PointerRun(address);

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
         if (dataIndex >= model.Count || dataIndex < 0) return new ErrorInfo($"{name} cannot start at 0x{dataIndex:X6}, which is outside the file size ({model.Count:X6}).");

         // special case: empty format, stick with the no-info run
         if (format == string.Empty) return ErrorInfo.NoError;

         return model.FormatRunFactory.GetStrategy(format)?.TryParseData(model, name, dataIndex, ref run) ?? new ErrorInfo($"Format {format} was not understood."); ;
      }

      private static ErrorInfo ValidateAnchorNameAndFormat(IDataModel model, IFormattedRun runToWrite, string name, string format, int dataIndex, bool allowAnchorOverwrite = false) {
         var existingRun = model.GetNextRun(dataIndex);
         var nextAnchor = model.GetNextAnchor(dataIndex + 1);

         if (name.ToLower() == "null") {
            return new ErrorInfo("'null' is a reserved word and cannot be used as an anchor name.");
         } else if (name == string.Empty && existingRun.Start != dataIndex) {
            // if there isn't already a run here, then clearly there's nothing pointing here
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (name == string.Empty && (existingRun.PointerSources?.Count ?? 0) == 0 && format != string.Empty) {
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
      private SortedSpan<int> GetSourcesPointingToNewAnchor(ModelDelta changeToken, string anchorName, IFormattedRun run, bool seekPointers) {
         if (!addressForAnchor.TryGetValue(anchorName, out int location)) return SortedSpan<int>.None;     // new anchor is unnamed, so nothing points to it yet

         if (!unmappedNameToSources.TryGetValue(anchorName, out var sources)) {
            // no pointer was waiting for this anchor to be created
            // but the user thinks there's something pointing here
            if (seekPointers) return SearchForPointersToAnchor(changeToken, location);
            return SortedSpan<int>.None;
         }

         var sourcesDirectlyToThis = sources;
         foreach (var source in sources) {
            var index = BinarySearch(source);
            if (index >= 0 && runs[index] is ITableRun array1) {
               Debug.Assert(array1.ElementContent[0].Type == ElementContentType.Pointer);
            } else if (index < 0 && runs[~index - 1] is ITableRun array2) {
               var offsets = array2.ConvertByteOffsetToArrayOffset(source);
               Debug.Assert(array2.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer);
            } else {
               Debug.Assert(index >= 0 && runs[index] is PointerRun, $"Expected a pointer at address {runs[index].Start:X6} but found {runs[index].GetType()} instead.");
            }
            changeToken.RemoveUnmappedPointer(source, anchorName);
            sourceToUnmappedName.Remove(source);
            int offset = 0;
            if (run is ArrayRun array && array.SupportsInnerPointers) {
               offset = (ReadValue(source) * array.ElementLength).LimitToRange(0, array.Length);
               if (offset != 0) sourcesDirectlyToThis = sourcesDirectlyToThis.Remove1(source);
            }

            if (changeToken is NoDataChangeDeltaModel) {
               // if we're doing an initial load of the model, we may load a pointer and want to update unmapped pointers to point to that location.
               // this _is_ a data edit, and we're ok with that in this case.
               // example: if the original pointer for the game name was cleared and we re-add it, we want to re-add any pointers to it.
               // note that this change isn't undo-able and isn't tracked for save purposes.
               // this is an edge case, so it's probably ok.
               WritePointer(new ModelDelta(), source, location + offset);
            } else {
               WritePointer(changeToken, source, location + offset);
            }
         }
         unmappedNameToSources.Remove(anchorName);

         return sourcesDirectlyToThis;
      }

      private T MoveRun<T>(ModelDelta changeToken, T run, int length, int newStart) where T : IFormattedRun {
         // repoint
         foreach (var source in run.PointerSources.ToList()) {
            // special update for pointers to this run that live within this run
            if (run.Start < source && source < run.Start + run.Length) {
               run = (T)run.RemoveSource(source);
               run = (T)run.Duplicate(run.Start, run.PointerSources.Add1(source + newStart - run.Start));
            }
            WritePointer(changeToken, source, newStart);
         }
         if (run is ArrayRun tableRun && tableRun.SupportsInnerPointers) {
            for (int i = 1; i < tableRun.ElementCount; i++) {
               foreach (var source in tableRun.PointerSourcesForInnerElements[i]) {
                  WritePointer(changeToken, source, newStart + i * tableRun.ElementLength);
               }
            }
         }

         // clear pointers from moved scripts
         if (run is IScriptStartRun) {
            do {
               var nextRun = GetNextRun(run.Start + 1) as PointerRun;
               if (nextRun == null || nextRun.Start >= run.Start + length) break;
               ClearFormat(changeToken, nextRun.Start, 4);
            }
            while (true);
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

         var index = BinarySearch(run.Start);
         changeToken.RemoveRun(runs[index]);
         RemoveIndex(index);
         var newIndex = BinarySearch(newStart);
         InsertIndex(~newIndex, newRun);
         changeToken.AddRun(newRun);
         if (~newIndex < index) index += 1;

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

      private readonly List<TableGroup> TableGroups = new();
      public override void AppendTableGroup(ModelDelta token, string groupName, IReadOnlyList<string> tableNames, string hash) {
         if (TableGroups.Any(group => tableNames.Any(group.Tables.Contains))) return; // don't add it if it contains the same table as one already added
         TableGroups.Add(new(groupName, tableNames, hash));
      }
      public override IReadOnlyList<TableGroup> GetTableGroups(string tableName) {
         if (!addressForAnchor.TryGetValue(tableName, out var address)) {
            if (lists.TryGetValue(tableName, out var list)) {
               var firstTable = this.Arrays.FirstOrDefault(array => array.LengthFromAnchor == tableName);
               if (firstTable == null) return null;
               address = firstTable.Start;
            } else {
               return null;
            }
         }
         if (GetNextRun(address) is not ArrayRun run || run.Start != address) return null;
         var related = this.GetRelatedArrays(run).Distinct().ToList();
         var others = new List<string>();
         var groups = new List<TableGroup>();
         foreach (var arrayRun in related) {
            if (!anchorForAddress.TryGetValue(arrayRun.Start, out var arrayName)) continue;
            var matchingGroups = TableGroups.Where(group => group.Tables.Any(table => table.Split(ArrayRunSplitterSegment.Separator)[0] == arrayName)).ToList();
            foreach (var matchingGroup in matchingGroups) {
               if (groups.Contains(matchingGroup)) continue;
               groups.Add(matchingGroup);
            }
            if (matchingGroups.Count == 0) {
               others.Add(arrayName);
            }
         }
         if (others.Count > 0) {
            others.Sort();
            groups.Add(new("Other", others));
         }
         return groups;
      }
   }

   public record TableGroup(string GroupName, IReadOnlyList<string> Tables) {
      private string hash;
      public string Hash {
         get {
            if (hash == null) hash = StoredList.GenerateHash(Tables);
            return hash;
         }
      }

      public bool HashMatches => StoredList.GenerateHash(Tables) == Hash;

      public TableGroup(string groupName, IReadOnlyList<string> tables, string hash) : this(groupName, tables) {
         if (hash == null) hash = StoredList.GenerateHash(tables);
         this.hash = hash;
      }
   }

   public static class StringDictionaryExtensions {
      public static bool TryGetValueCaseInsensitive<T>(this IDictionary<string, T> self, string key, out T value) {
         if (self.TryGetValue(key, out value)) return true;
         var keys = self.Keys.ToList();
         foreach (var option in keys) {
            if (key.Equals(option, StringComparison.CurrentCultureIgnoreCase)) {
               value = self[option];
               return true;
            }
         }

         value = default;
         return false;
      }
   }

   public class DebugList : List<IFormattedRun>, IList<IFormattedRun> {
      private static readonly int[] TargetAddresses = new int[] { };
      public int InsertCount { get; private set; }
      public int RemoveCount { get; private set; }
      public Dictionary<Type, int> RemovedRunTypes { get; } = new();
      public int ReplaceCount { get; private set; }
      void ICollection<IFormattedRun>.Add(IFormattedRun item) {
         if (TargetAddresses.Contains(item.Start)) Debugger.Break();
         InsertCount += 1;
         Add(item);
      }
      void IList<IFormattedRun>.Insert(int index, IFormattedRun item) {
         if (TargetAddresses.Contains(item.Start)) Debugger.Break();
         InsertCount += 1;
         Insert(index, item);
      }
      void IList<IFormattedRun>.RemoveAt(int index) {
         var item = this[index];
         if (TargetAddresses.Contains(item.Start)) Debugger.Break();
         RemoveCount += 1;

         var type = item.GetType();
         if (!RemovedRunTypes.ContainsKey(type)) RemovedRunTypes[type] = 0;
         RemovedRunTypes[type] += 1;

         RemoveAt(index);
      }
      IFormattedRun IList<IFormattedRun>.this[int index] {
         get => this[index];
         set {
            if (TargetAddresses.Contains(value.Start)) Debugger.Break();
            ReplaceCount += 1;
            this[index] = value;
         }
      }
   }
}
