using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TrainerPokemonTeamRun : BaseRun, IStreamRun, ITableRun {
      public const int IV_Cap = 31;
      public const int TrainerFormat_StructTypeOffset = 0;
      public const int TrainerFormat_PokemonCountOffset = 32;
      public const int TrainerFormat_PointerOffset = 36;
      public const int TrainerFormat_Width = 40;
      public const byte INCLUDE_MOVES = 1;
      public const byte INCLUDE_ITEM = 2;

      public const int PokemonFormat_FixedIVStart = 0;
      public const int PokemonFormat_LevelStart = 2;
      public const int PokemonFormat_PokemonStart = 4;
      public const int PokemonFormat_ItemStart = 6;
      public const int PokemonFormat_MoveStart = 6;

      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "tpt" + AsciiRun.StreamDelimeter;

      private readonly IDataModel model;

      public byte StructType { get; }

      public bool CanAppend => ElementCount < 6;

      private readonly bool showFullIVByteRange = false;

      #region Constructors

      public TrainerPokemonTeamRun(IDataModel model, int start, bool showFullIVByteRange, SortedSpan<int> sources) : base(start, sources) {
         this.model = model;
         this.showFullIVByteRange = showFullIVByteRange;

         // trainer format (abbreviated):
         //     0           1       2         3        4-15     16     18      20     22       24          28       32         36          40 total
         // [structType. class. introMusic. sprite. name\"\"12 item1: item2: item3: item4: doubleBattle:: ai:: pokemonCount:: pokemon<>]
         StructType = 0;
         ElementCount = 1;
         foreach (var source in sources) {
            if (!(model.GetNextRun(source) is ITableRun)) continue;
            StructType = model[source - TrainerFormat_PointerOffset];
            ElementCount = model.ReadMultiByteValue(source - TrainerFormat_PointerOffset + TrainerFormat_PokemonCountOffset, 4);
            break;
         }

         var segments = Initialize();
         ElementContent = segments;
         ElementLength = ElementContent.Sum(segment => segment.Length);
      }

      private TrainerPokemonTeamRun(IDataModel model, int start, bool showFullIVByteRange, SortedSpan<int> sources, int primarySource) : base(start, sources) {
         this.model = model;
         this.showFullIVByteRange = showFullIVByteRange;
         StructType = model[primarySource - TrainerFormat_PointerOffset];
         ElementCount = model[primarySource - TrainerFormat_PointerOffset + TrainerFormat_PokemonCountOffset];
         var segments = Initialize();
         ElementContent = segments;
         ElementLength = ElementContent.Sum(segment => segment.Length);
      }

      private IReadOnlyList<ArrayRunElementSegment> Initialize() {
         var segments = new List<ArrayRunElementSegment> {
            showFullIVByteRange ? new ArrayRunElementSegment("ivSpread", ElementContentType.Integer, 2) : new ArrayRunTupleSegment("ivSpread", "|:.|each::.", 2),
            new ArrayRunElementSegment("level", ElementContentType.Integer, 2),
            new ArrayRunEnumSegment("mon", 2, HardcodeTablesModel.PokemonNameTable)
         };
         if ((StructType & INCLUDE_ITEM) != 0) {
            segments.Add(new ArrayRunEnumSegment("item", 2, HardcodeTablesModel.ItemsTableName));
         }
         if ((StructType & INCLUDE_MOVES) != 0) {
            segments.Add(new ArrayRunEnumSegment("move1", 2, HardcodeTablesModel.MoveNamesTable));
            segments.Add(new ArrayRunEnumSegment("move2", 2, HardcodeTablesModel.MoveNamesTable));
            segments.Add(new ArrayRunEnumSegment("move3", 2, HardcodeTablesModel.MoveNamesTable));
            segments.Add(new ArrayRunEnumSegment("move4", 2, HardcodeTablesModel.MoveNamesTable));
         }
         if ((StructType & INCLUDE_ITEM) == 0) {
            segments.Add(new ArrayRunElementSegment("padding", ElementContentType.Integer, 2));
         }

         return segments;
      }

      #endregion

      public IEnumerable<int> Search(string parentArrayName, int id) {
         for (int i = 0; i < ElementCount; i++) {
            int start = Start + i * ElementLength;
            if (parentArrayName == HardcodeTablesModel.MoveNamesTable && (StructType & INCLUDE_MOVES) != 0) {
               for (int j = 0; j < 4; j++) {
                  var index = start + PokemonFormat_MoveStart + j * 2;
                  if ((StructType & INCLUDE_ITEM) != 0) index += 2;
                  var moveID = model.ReadMultiByteValue(index, 2);
                  if (moveID == id) yield return index;
               }
            } else if (parentArrayName == HardcodeTablesModel.PokemonNameTable) {
               var index = start + PokemonFormat_PokemonStart;
               var pokemonID = model.ReadMultiByteValue(index, 2);
               if (pokemonID == id) yield return index;
            } else if (parentArrayName == HardcodeTablesModel.ItemsTableName && (StructType & INCLUDE_ITEM) != 0) {
               var index = start + PokemonFormat_ItemStart;
               var itemID = model.ReadMultiByteValue(index, 2);
               if (itemID == id) yield return index;
            }
         }
      }

      #region BaseRun

      public override int Length => ElementLength * ElementCount;

      public override string FormatString => SharedFormatString;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => this.CreateSegmentDataFormat(data, index);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new TrainerPokemonTeamRun(model, Start, showFullIVByteRange, newPointerSources);

      #endregion

      #region ITableRun

      public int ElementCount { get; }

      public int ElementLength { get; }

      public IReadOnlyList<string> ElementNames { get; } = new List<string>();

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public ITableRun Append(ModelDelta token, int length) {
         var totalLength = ElementLength * (ElementCount + length);
         var workingRun = this;
         if (totalLength > workingRun.Length) workingRun = model.RelocateForExpansion(token, workingRun, totalLength);

         // delete old elements
         for (int i = -1; i >= length; i--) {
            var start = workingRun.Start + Length + i * ElementLength;
            for (int j = 0; j < ElementLength; j++) token.ChangeData(model, start + j, 0xFF);
         }

         // add new elements
         for (int i = 0; i < length; i++) {
            var start = workingRun.Start + Length + i * ElementLength;
            for (int j = 0; j < ElementLength; j++) {
               if (ElementCount > 0) {
                  var previousMemberValue = model[start + j - ElementLength];
                  token.ChangeData(model, start + j, previousMemberValue);
               } else {
                  token.ChangeData(model, start + j, 0x00);
               }
            }
         }

         // update parent
         var parent = workingRun.PointerSources[0] - TrainerFormat_PointerOffset;
         model.WriteMultiByteValue(parent + TrainerFormat_PokemonCountOffset, 4, token, ElementCount + length);
         return new TrainerPokemonTeamRun(model, workingRun.Start, showFullIVByteRange, workingRun.PointerSources);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) => new TrainerPokemonTeamRun(model, start, showFullIVByteRange, pointerSources);

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         ITableRunExtensions.Clear(this, model, changeToken, start, length);
      }

      #endregion

      #region IStreamRun

      // example serialized pokemon:
      //
      // 50 Butterfree (31)@"Silk Powder"
      // - "Stun Spore"
      // - "Super Sonic"
      // - "Aerial Ace"
      // - "Silver Wind"

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) => DeserializeRun(content, token, false, false, out changedOffsets);
      public TrainerPokemonTeamRun DeserializeRun(string content, ModelDelta token, bool setDefaultMoves, bool setDefaultItems, out IReadOnlyList<int> changedOffsets) {
         var changedAddresses = new HashSet<int>();
         var lines = content.Split(Environment.NewLine).Select(line => line.Trim()).ToArray();

         // step 1: parse it into some data containers
         var data = new TeamData(model, lines, showFullIVByteRange);
         if (setDefaultMoves) data.SetDefaultMoves(this);
         if (setDefaultItems) data.SetDefaultItems();

         // step 2: figure out what I need based on the data
         var elementLength = data.MovesIncluded ? 16 : 8;
         var totalLength = elementLength * data.Pokemon.Count;
         var workingRun = this;
         if (totalLength > workingRun.Length) workingRun = model.RelocateForExpansion(token, workingRun, totalLength);

         // step 3: write the run data
         WriteData(token, workingRun.Start, data, changedAddresses);

         // step 4: write the parent data
         var structType = (data.ItemsIncluded ? INCLUDE_ITEM : 0) ^ (data.MovesIncluded ? INCLUDE_MOVES : 0);
         UpdateParents(token, structType, data.Pokemon.Count, workingRun.PointerSources);

         changedOffsets = new List<int>(changedAddresses);
         return new TrainerPokemonTeamRun(model, workingRun.Start, showFullIVByteRange, workingRun.PointerSources);
      }

      private void WriteData(ModelDelta token, int runStart, TeamData data, HashSet<int> changedAddresses) {
         var elementLength = data.MovesIncluded ? 16 : 8;
         for (int i = 0; i < data.Pokemon.Count; i++) {
            int start = runStart + elementLength * i;
            if (model.WriteMultiByteValue(start + 0, 2, token, data.IVs[i])) changedAddresses.Add(start);
            if (model.WriteMultiByteValue(start + 2, 2, token, data.Levels[i])) changedAddresses.Add(start + 2);
            if (model.WriteMultiByteValue(start + 4, 2, token, data.Pokemon[i])) changedAddresses.Add(start + 4);
            start += 6;
            if (data.ItemsIncluded) {
               if (model.WriteMultiByteValue(start, 2, token, data.Items[i])) changedAddresses.Add(start + 2);
               start += 2;
            }
            if (data.MovesIncluded) {
               for (int j = 0; j < 4; j++) {
                  if (model.WriteMultiByteValue(start + j * 2, 2, token, data.Moves[i * 4 + j])) changedAddresses.Add(start + j * 2);
               }
               start += 8;
            }

            // if there's no item, add 2 more bytes to get to the next multiple of 4.
            if (!data.ItemsIncluded) {
               if (model.WriteMultiByteValue(start, 2, token, 0)) changedAddresses.Add(start);
            }
         }

         // free space from the original run that's not needed by the new run
         for (int i = elementLength * data.Pokemon.Count; i < Length; i++) {
            token.ChangeData(model, runStart + i, 0xFF); // intentionally don't notify, these elements don't exist anymore.
         }
      }

      private void UpdateParents(ModelDelta token, int structType, int pokemonCount, IReadOnlyList<int> pointerSources) {
         foreach (var source in pointerSources) {
            if (!(model.GetNextRun(source) is ITableRun)) continue;
            var parent = source - TrainerFormat_PointerOffset;
            if (model[parent + TrainerFormat_StructTypeOffset] != structType) token.ChangeData(model, parent + TrainerFormat_StructTypeOffset, (byte)structType);
            if (model[parent + TrainerFormat_PokemonCountOffset] != pokemonCount) model.WriteMultiByteValue(parent + TrainerFormat_PokemonCountOffset, 4, token, pokemonCount);
         }
      }

      public string SerializeRun() {
         var cache = ModelCacheScope.GetCache(model);
         var buffer = new StringBuilder();
         for (int i = 0; i < ElementCount; i++) {
            var start = Start + ElementLength * i;
            var ivSpread = model.ReadMultiByteValue(start + 0, 2);
            if (!showFullIVByteRange) ivSpread = (int)Math.Round(ivSpread * IV_Cap / 255.0);
            var level = model.ReadMultiByteValue(start + 2, 2);
            var pokeID = model.ReadMultiByteValue(start + 4, 2);
            var pokemonNames = cache.GetOptions(HardcodeTablesModel.PokemonNameTable);
            var pokemon = pokemonNames.Count > pokeID ? pokemonNames[pokeID] : pokeID.ToString();
            start += 6;

            var item = string.Empty;
            if ((StructType & INCLUDE_ITEM) != 0) {
               var itemID = model.ReadMultiByteValue(start, 2);
               var itemNames = cache.GetOptions(HardcodeTablesModel.ItemsTableName);
               item = itemNames.Count > itemID ? itemNames[itemID] : itemID.ToString();
               item = "@" + item;
               start += 2;
            }

            buffer.AppendLine($"{level} {pokemon} (IVs={ivSpread}){item}");
            if ((StructType & INCLUDE_MOVES) != 0) {
               var moveNames = cache.GetOptions(HardcodeTablesModel.MoveNamesTable);
               for (int j = 0; j < 4; j++) {
                  var moveID = model.ReadMultiByteValue(start + j * 2, 2);
                  var move = moveNames.Count > moveID ? moveNames[moveID] : moveID.ToString();
                  buffer.AppendLine($"- {move}");
               }
            } else {
               buffer.AppendLine();
            }
            if (i + 1 < ElementCount) buffer.AppendLine();
         }
         return buffer.ToString();
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var result = new List<AutocompleteItem>();
         if (line.Trim().StartsWith("-")) result.AddRange(GetAutocompleteMoveOptions(line, caretCharacterIndex));
         else result.AddRange(GetAutocompletePokemonOptions(line, caretCharacterIndex));
         return result;
      }

      private IEnumerable<AutocompleteItem> GetAutocompletePokemonOptions(string line, int caretIndex) {
         var result = new List<AutocompleteItem>();
         var end = line.Substring(caretIndex).Trim();
         var start = line.Substring(0, caretIndex).Trim();
         var spaceIndex = start.IndexOf(" ");
         var parenIndex = start.IndexOf("(");
         var endParenIndex = start.IndexOf(")");
         var atIndex = start.IndexOf("@");

         // level
         if (spaceIndex == -1) return result;

         // pokemon
         if (parenIndex == -1 && atIndex == -1) {
            var level = start.Substring(0, spaceIndex);
            var pokemon = start.Substring(spaceIndex + 1);
            return ArrayRunEnumSegment.GetOptions(model, HardcodeTablesModel.PokemonNameTable)
               .Where(option => option.MatchesPartial(pokemon, onlyCheckLettersAndDigits: true))
               .Where(option => option.Contains("-") || !pokemon.Contains("-"))
               .Where(option => option.Contains("'") || !pokemon.Contains("'"))
               .Where(option => option.Contains(".") || !pokemon.Contains("."))
               .Select(option => new AutocompleteItem(option, $"{level} {option} {end}"));
         }

         // IVs
         if (parenIndex >= 0 && endParenIndex == -1) return result;

         // Item
         if (atIndex == -1) return result;
         var item = start.Substring(atIndex + 1);
         start = start.Substring(0, atIndex);
         var options = ArrayRunEnumSegment.GetOptions(model, HardcodeTablesModel.ItemsTableName).ToList();
         return options
            .Where(option => option.MatchesPartial(item, onlyCheckLettersAndDigits: true))
            .Where(option => option.Contains("-") || !item.Contains("-"))
            .Where(option => option.Contains("'") || !item.Contains("'"))
            .Select(option => new AutocompleteItem(option, $"{start.Trim()} @{option}"));
      }

      private IEnumerable<AutocompleteItem> GetAutocompleteMoveOptions(string line, int caretIndex) {
         var namePart = line.Split(new[] { '-' }, 2)[1].Trim();
         if (namePart == string.Empty) return Enumerable.Empty<AutocompleteItem>();
         return ArrayRunEnumSegment.GetOptions(model, HardcodeTablesModel.MoveNamesTable)
            .Where(option => option.MatchesPartial(namePart, onlyCheckLettersAndDigits: true))
            .Where(option => option.Contains("-") || !namePart.Contains("-"))
            .Select(option => new AutocompleteItem(option, $"- {option}"));
      }

      public IReadOnlyList<IPixelViewModel> Visualizations {
         get {
            var list = new List<IPixelViewModel>();
            var monOffset = ElementContent.Take(2).Sum(seg => seg.Length);
            var monSeg = ElementContent[2];
            if (!(model.GetTable(HardcodeTablesModel.FrontSpritesTable) is ITableRun sprites)) return list;
            for (int i = 0; i < ElementCount; i++) {
               var index = model.ReadMultiByteValue(Start + ElementLength * i + monOffset, monSeg.Length);
               var spriteAddress = model.ReadPointer(sprites.Start + sprites.ElementLength * index);
               if (!(model.GetNextRun(spriteAddress) is ISpriteRun sprite)) return new List<IPixelViewModel>();
               var pixels = SpriteDecorator.BuildSprite(model, sprite, useTransparency: true);
               if (pixels == null) return new List<IPixelViewModel>();
               list.Add(pixels);
            }
            return list;
         }
      }

      public bool DependsOn(string anchorName) =>
         anchorName == HardcodeTablesModel.ItemsTableName ||
         anchorName == HardcodeTablesModel.MoveNamesTable ||
         anchorName == HardcodeTablesModel.PokemonNameTable;

      private class TeamData {
         private readonly List<int> levels = new List<int>();
         private readonly List<int> pokemons = new List<int>();
         private readonly List<int> ivs = new List<int>();
         private readonly List<int> items = new List<int>();
         private readonly List<int> moves = new List<int>();
         private readonly IDataModel model;
         private readonly bool showFullIVByteRange = false;

         public bool ItemsIncluded { get; private set; }
         public bool MovesIncluded { get; private set; }

         public IReadOnlyList<int> Levels => levels;
         public IReadOnlyList<int> Pokemon => pokemons;
         public IReadOnlyList<int> IVs => ivs;
         public IReadOnlyList<int> Items => items;
         public IReadOnlyList<int> Moves => moves;

         public TeamData(IDataModel model, string[] lines, bool showFullIVByteRange) {
            this.model = model;
            this.showFullIVByteRange = showFullIVByteRange;
            var currentPokemonMoveCount = 0;
            var moveNames = model.GetOptions(HardcodeTablesModel.MoveNamesTable);
            var itemNames = model.GetOptions(HardcodeTablesModel.ItemsTableName);
            var pokemonNames = model.GetOptions(HardcodeTablesModel.PokemonNameTable);

            foreach (var line in lines) {
               if (line.Trim() is "") continue;
               if (line.Trim().StartsWith("-")) {
                  if (pokemons.Count == 0) continue;
                  if (currentPokemonMoveCount > 3) continue;
                  MovesIncluded = true;
                  var move = line.Trim().Substring(1).Trim(' ', '"');
                  var moveIndex = moveNames.IndexOfPartial(move);
                  if (moveIndex < 0) moveIndex = 0;
                  moves[(pokemons.Count - 1) * 4 + currentPokemonMoveCount] = moveIndex;
                  currentPokemonMoveCount++;
               } else {
                  if (pokemons.Count == 6) continue;

                  var levelTokenized = line.Split(new[] { ' ' }, 2);
                  if (levelTokenized.Length != 2) continue;
                  if (!int.TryParse(levelTokenized[0], out int level)) continue;
                  levels.Add(level);

                  var itemTokenized = levelTokenized[1].Split(new[] { '@' }, 2);
                  AddItem(itemNames, items, itemTokenized);

                  var ivTokenized = itemTokenized[0].Split(new[] { '(' }, 2);
                  AddIV(ivs, ivTokenized);

                  var pokemon = ivTokenized[0].Trim();
                  var pokemonIndex = pokemonNames.IndexOfPartial(pokemon);
                  if (pokemonIndex < 0) pokemonIndex = 0;
                  pokemons.Add(pokemonIndex);
                  moves.AddRange(new[] { 0, 0, 0, 0 });
                  currentPokemonMoveCount = 0;
               }
            }

            if (pokemons.Count == 0) {
               pokemons.Add(0);
               ivs.Add(0);
               levels.Add(0);
               moves.AddRange(new[] { 0, 0, 0, 0 });
            }
         }

         public TeamData(IDataModel model, int start, int structType, int count) {
            this.model = model;
            ItemsIncluded = (INCLUDE_ITEM & structType) != 0;
            MovesIncluded = (INCLUDE_MOVES & structType) != 0;
            for (int i = 0; i < count; i++) {
               ivs.Add(model.ReadMultiByteValue(start + 0, 2));
               levels.Add(model.ReadMultiByteValue(start + 2, 2));
               pokemons.Add(model.ReadMultiByteValue(start + 4, 2));
               start += 6;
               if (ItemsIncluded) {
                  items.Add(model.ReadMultiByteValue(start, 2));
                  start += 2;
               }
               if (MovesIncluded) {
                  for (int j = 0; j < 4; j++) moves.Add(model.ReadMultiByteValue(start + j * 2, 2));
                  start += 8;
               } else {
                  moves.AddRange(new[] { 0, 0, 0, 0 });
               }
               if (!ItemsIncluded) start += 2;
            }
         }

         public void SetDefaultMoves(TrainerPokemonTeamRun parent) {
            MovesIncluded = true;
            moves.Clear();
            for (int i = 0; i < pokemons.Count; i++) {
               moves.AddRange(parent.GetDefaultMoves(pokemons[i], levels[i]));
            }
         }

         public void RemoveMoves() => MovesIncluded = false;

         public void SetDefaultItems() {
            ItemsIncluded = true;
            items.Clear();
            var pokemonTable = model.GetTable(HardcodeTablesModel.PokemonStatsTable);
            items.AddRange(pokemons.Select(p => pokemonTable?.ReadValue(model, p, "item1") ?? 0));
         }

         public void RemoveItems() => ItemsIncluded = false;

         private void AddIV(List<int> ivs, string[] ivTokenized) {
            if (ivTokenized.Length == 2) {
               ivTokenized[1] = ivTokenized[1].Replace(")", "").Trim();
               ivTokenized[1] = ivTokenized[1].Split('=').Last();
               if (int.TryParse(ivTokenized[1], out int fixedIV)) {
                  if (!showFullIVByteRange) fixedIV = (int)Math.Round(fixedIV * 255.0 / IV_Cap);
                  ivs.Add(fixedIV);
               } else {
                  ivs.Add(0);
               }
            } else {
               ivs.Add(0);
            }
         }

         private void AddItem(IReadOnlyList<string> itemNames, List<int> items, string[] itemTokenized) {
            if (itemTokenized.Length == 2) {
               ItemsIncluded = true;
               var itemIndex = itemNames.IndexOfPartial(itemTokenized[1]);
               if (itemIndex < 0) itemIndex = 0;
               items.Add(itemIndex);
            } else {
               items.Add(0);
            }
         }
      }

      #endregion

      public TrainerPokemonTeamRun UpdateFromParent(ModelDelta token, int parentSegmentChange, int pointerSource, HashSet<int> changedAddresses) {
         // we only care if the change was to the parent's structType or pokemonCount.
         var sourceTable = model.GetNextRun(pointerSource) as ITableRun;
         if (sourceTable == null) return this;
         if (sourceTable.ElementContent.Count <= parentSegmentChange) return this;
         if (parentSegmentChange != 0 && sourceTable.ElementContent[parentSegmentChange].Name != "pokemonCount") return this;

         var newStructType = model[pointerSource - TrainerFormat_PointerOffset];
         var newElementCount = model[pointerSource - TrainerFormat_PointerOffset + TrainerFormat_PokemonCountOffset];
         newElementCount = Math.Max(newElementCount, (byte)1);

         var newRun = this;
         if (newElementCount != ElementCount) newRun = (TrainerPokemonTeamRun)newRun.Append(token, newElementCount - ElementCount);
         if (newStructType != StructType) {
            var data = new TeamData(model, newRun.Start, StructType, newElementCount);
            if ((newStructType & INCLUDE_MOVES) != 0 && !data.MovesIncluded) {
               data.SetDefaultMoves(this);
               var initialStart = newRun.Start;
               newRun = model.RelocateForExpansion(token, newRun, newRun.Length * 2);
            } else if ((newStructType & INCLUDE_MOVES) == 0 && data.MovesIncluded) {
               data.RemoveMoves();
            }
            if ((newStructType & INCLUDE_ITEM) != 0 && !data.ItemsIncluded) {
               data.SetDefaultItems();
            } else if ((newStructType & INCLUDE_ITEM) == 0 && data.ItemsIncluded) {
               data.RemoveItems();
            }
            WriteData(token, newRun.Start, data, changedAddresses);
         }
         if (newElementCount != ElementCount || newStructType != StructType) {
            UpdateParents(token, newStructType, newElementCount, newRun.PointerSources);
         }

         return new TrainerPokemonTeamRun(model, newRun.Start, showFullIVByteRange, PointerSources, pointerSource);
      }

      /// <summary>
      /// Finds what 4 moves a pokemon would have by default, based on lvlmoves and the given level
      /// </summary>
      private IReadOnlyList<int> GetDefaultMoves(int pokemon, int currentLevel) {
         var results = new List<int>();
         var levelMovesAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.LevelMovesTableName);
         var lvlMoves = model.GetNextRun(levelMovesAddress) as ArrayRun;
         if (lvlMoves != null) {
            var movesStart = model.ReadPointer(lvlMoves.Start + lvlMoves.ElementLength * pokemon);
            if (model.GetNextRun(movesStart) is ITableRun run) {
               for (int i = 0; i < run.ElementCount; i += 1) {
                  int level = 0, move = 0;
                  if (run.ElementContent.Count == 1) {
                     var pair = model.ReadMultiByteValue(run.Start + i * run.ElementLength, run.ElementLength);
                     (level, move) = PLMRun.SplitToken(pair);
                  } else if (run.ElementContent.Count == 2) {
                     move = model.ReadMultiByteValue(run.Start + i * run.ElementLength, run.ElementContent[0].Length);
                     level = model.ReadMultiByteValue(run.Start + i * run.ElementLength + run.ElementContent[0].Length, run.ElementContent[1].Length);
                  } else {
                     level = int.MaxValue;
                  }

                  if (currentLevel >= level) results.Add(move);
                  else break;
               }
            }
         }
         while (results.Count > 4) results.RemoveAt(0); // restrict to the last 4 moves
         while (results.Count < 4) results.Add(0);      // pad with extra '0' moves if needed
         return results;
      }
   }
}
