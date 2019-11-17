using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TrainerPokemonTeamRun : BaseRun, IStreamRun, ITableRun {
      public const int TrainerFormat_StructTypeOffset = 0;
      public const int TrainerFormat_PokemonCountOffset = 32;
      public const int TrainerFormat_PointerOffset = 36;
      public const int TrainerFormat_Width = 40;
      public const byte INCLUDE_MOVES = 1;
      public const byte INCLUDE_ITEM = 2;

      public const int PokemonFormat_FixedIVStart = 0;
      public const int PokemonFormat_LevelStart = 2;
      public const int PokemonFormat_PokemonStart = 4;
      public const int PokemonFormat_MoveStart = 6;

      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "tpt" + AsciiRun.StreamDelimeter;

      private readonly IDataModel model;

      public byte StructType { get; }

      #region Constructors

      public TrainerPokemonTeamRun(IDataModel model, int start, IReadOnlyList<int> sources) : base(start, sources) {
         this.model = model;

         // trainer format (abbreviated):
         //     0           1       2         3        4-15     16     18      20     22       24          28       32         36          40 total
         // [structType. class. introMusic. sprite. name\"\"12 item1: item2: item3: item4: doubleBattle:: ai:: pokemonCount:: pokemon<>]
         StructType = 0;
         ElementCount = 1;
         foreach (var source in sources) {
            if (!(model.GetNextRun(source) is ITableRun)) continue;
            StructType = model[source - TrainerFormat_PointerOffset];
            ElementCount = model[source - TrainerFormat_PointerOffset + TrainerFormat_PokemonCountOffset];
            break;
         }

         var segments = Initialize();
         ElementContent = segments;
         ElementLength = ElementContent.Sum(segment => segment.Length);
      }

      private TrainerPokemonTeamRun(IDataModel model, int start, IReadOnlyList<int> sources, int primarySource) : base(start, sources) {
         this.model = model;
         StructType = model[primarySource - TrainerFormat_PointerOffset];
         ElementCount = model[primarySource - TrainerFormat_PointerOffset + TrainerFormat_PokemonCountOffset];
         var segments = Initialize();
         ElementContent = segments;
         ElementLength = ElementContent.Sum(segment => segment.Length);
      }

      private IReadOnlyList<ArrayRunElementSegment> Initialize() {
         var segments = new List<ArrayRunElementSegment>();
         segments.Add(new ArrayRunElementSegment("ivSpread", ElementContentType.Integer, 2));
         segments.Add(new ArrayRunElementSegment("level", ElementContentType.Integer, 2));
         segments.Add(new ArrayRunEnumSegment("mon", 2, EggMoveRun.PokemonNameTable));
         if ((StructType & INCLUDE_ITEM) != 0) {
            segments.Add(new ArrayRunEnumSegment("item", 2, HardcodeTablesModel.ItemsTableName));
         }
         if ((StructType & INCLUDE_MOVES) != 0) {
            segments.Add(new ArrayRunEnumSegment("move1", 2, EggMoveRun.MoveNamesTable));
            segments.Add(new ArrayRunEnumSegment("move2", 2, EggMoveRun.MoveNamesTable));
            segments.Add(new ArrayRunEnumSegment("move3", 2, EggMoveRun.MoveNamesTable));
            segments.Add(new ArrayRunEnumSegment("move4", 2, EggMoveRun.MoveNamesTable));
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
            if (parentArrayName == EggMoveRun.MoveNamesTable && (StructType & INCLUDE_MOVES) != 0) {
               for (int j = 0; j < 4; j++) {
                  var index = start + PokemonFormat_MoveStart + j * 2;
                  var moveID = model.ReadMultiByteValue(index, 2);
                  if (moveID == id) yield return index;
               }
            } else if (parentArrayName == EggMoveRun.PokemonNameTable) {
               var index = start + PokemonFormat_PokemonStart;
               var pokemonID = model.ReadMultiByteValue(index, 2);
               if (pokemonID == id) yield return index;
            } else if (parentArrayName == HardcodeTablesModel.ItemsTableName && (StructType & INCLUDE_ITEM) != 0) {
               var index = start + ElementLength - 2;
               var itemID = model.ReadMultiByteValue(index, 2);
               if (itemID == id) yield return index;
            }
         }
      }

      #region BaseRun

      public override int Length => ElementLength * ElementCount;

      public override string FormatString => SharedFormatString;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => this.CreateSegmentDataFormat(data, index);

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new TrainerPokemonTeamRun(model, Start, newPointerSources);

      #endregion

      #region ITableRun

      public int ElementCount { get; }

      public int ElementLength { get; }

      public IReadOnlyList<string> ElementNames { get; } = new List<string>();

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public ITableRun Append(ModelDelta token, int length) {
         var totalLength = ElementLength * (ElementCount + length);
         var workingRun = this;
         if (totalLength > workingRun.Length) workingRun = (TrainerPokemonTeamRun)model.RelocateForExpansion(token, workingRun, totalLength);

         // delete old elements
         for (int i = -1; i >= length; i--) {
            var start = workingRun.Start + workingRun.Length + i * ElementLength;
            for (int j = 0; j < ElementLength; j++) token.ChangeData(model, start + j, 0xFF);
         }

         // add new elements
         for (int i = 0; i < length; i++) {
            var start = workingRun.Start + workingRun.Length + i * ElementLength;
            for (int j = 0; j < ElementLength; j++) token.ChangeData(model, start + j, 0x00);
         }

         // update parent
         var parent = workingRun.PointerSources[0] - TrainerFormat_PointerOffset;
         model.WriteMultiByteValue(parent + TrainerFormat_PokemonCountOffset, 4, token, ElementCount + length);
         return new TrainerPokemonTeamRun(model, workingRun.Start, workingRun.PointerSources);
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

      public IStreamRun DeserializeRun(string content, ModelDelta token) {
         var lines = content.Split(Environment.NewLine).Select(line => line.Trim()).ToArray();

         // step 1: parse it into some data containers
         var data = new TeamData(ModelCacheScope.GetCache(model), lines);

         // step 2: figure out what I need based on the data
         var elementLength = data.MovesIncluded ? 16 : 8;
         var totalLength = elementLength * data.Pokemon.Count;
         var workingRun = this;
         if (totalLength > workingRun.Length) workingRun = (TrainerPokemonTeamRun)model.RelocateForExpansion(token, workingRun, totalLength);

         // step 3: write the run data
         WriteData(token, workingRun.Start, data);

         // step 4: write the parent data
         var structType = (data.ItemsIncluded ? INCLUDE_ITEM : 0) ^ (data.MovesIncluded ? INCLUDE_MOVES : 0);
         UpdateParents(token, structType, data.Pokemon.Count, workingRun.PointerSources);

         return new TrainerPokemonTeamRun(model, workingRun.Start, workingRun.PointerSources);
      }

      private void WriteData(ModelDelta token, int runStart, TeamData data) {
         var elementLength = data.MovesIncluded ? 16 : 8;
         for (int i = 0; i < data.Pokemon.Count; i++) {
            int start = runStart + elementLength * i;
            model.WriteMultiByteValue(start + 0, 2, token, data.IVs[i]);
            model.WriteMultiByteValue(start + 2, 2, token, data.Levels[i]);
            model.WriteMultiByteValue(start + 4, 2, token, data.Pokemon[i]);
            start += 6;
            if (data.ItemsIncluded) {
               model.WriteMultiByteValue(start, 2, token, data.Items[i]);
               start += 2;
            }
            if (data.MovesIncluded) {
               for (int j = 0; j < 4; j++) model.WriteMultiByteValue(start + j * 2, 2, token, data.Moves[i * 4 + j]);
               start += 8;
            }

            // if there's no item, add 2 more bytes to get to the next multiple of 4.
            if (!data.ItemsIncluded) {
               model.WriteMultiByteValue(start, 2, token, 0);
               start += 2;
            }
         }

         // free space from the original run that's not needed by the new run
         for (int i = elementLength * data.Pokemon.Count; i < Length; i++) {
            token.ChangeData(model, runStart + i, 0xFF);
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
            var ivSpread = model.ReadMultiByteValue(start + 0, 2) * 31 / 255;
            var level = model.ReadMultiByteValue(start + 2, 2);
            var pokeID = model.ReadMultiByteValue(start + 4, 2);
            var pokemonNames = cache.GetOptions(EggMoveRun.PokemonNameTable);
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

            buffer.AppendLine($"{level} {pokemon} ({ivSpread}){item}");
            if ((StructType & INCLUDE_MOVES) != 0) {
               var moveNames = cache.GetOptions(EggMoveRun.MoveNamesTable);
               for (int j = 0; j < 4; j++) {
                  var moveID = model.ReadMultiByteValue(start + j * 2, 2);
                  var move = moveNames.Count > moveID ? moveNames[moveID] : moveID.ToString();
                  buffer.AppendLine($"- {move}");
               }
            }
            if (i + 1 < ElementCount) buffer.AppendLine();
         }
         return buffer.ToString();
      }

      public bool DependsOn(string anchorName) => anchorName == HardcodeTablesModel.ItemsTableName || anchorName == EggMoveRun.MoveNamesTable || anchorName == EggMoveRun.PokemonNameTable;

      private class TeamData {
         private readonly List<int> levels = new List<int>();
         private readonly List<int> pokemons = new List<int>();
         private readonly List<int> ivs = new List<int>();
         private readonly List<int> items = new List<int>();
         private readonly List<int> moves = new List<int>();

         public bool ItemsIncluded { get; private set; }
         public bool MovesIncluded { get; private set; }

         public IReadOnlyList<int> Levels => levels;
         public IReadOnlyList<int> Pokemon => pokemons;
         public IReadOnlyList<int> IVs => ivs;
         public IReadOnlyList<int> Items => items;
         public IReadOnlyList<int> Moves => moves;

         public TeamData(ModelCacheScope cache, string[] lines) {
            var currentPokemonMoveCount = 0;
            var moveNames = cache.GetOptions(EggMoveRun.MoveNamesTable);
            var itemNames = cache.GetOptions(HardcodeTablesModel.ItemsTableName);
            var pokemonNames = cache.GetOptions(EggMoveRun.PokemonNameTable);

            foreach (var line in lines) {
               if (line.Trim() is "") continue;
               if (line.StartsWith("-")) {
                  if (pokemons.Count == 0) continue;
                  if (currentPokemonMoveCount > 3) continue;
                  MovesIncluded = true;
                  var move = line.Substring(1).Trim();
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
         }

         public TeamData(IDataModel model, int start, int structType, int count) {
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
            items.AddRange(Enumerable.Repeat(0, pokemons.Count));
         }

         public void RemoveItems() => ItemsIncluded = false;

         private void AddIV(List<int> ivs, string[] ivTokenized) {
            if (ivTokenized.Length == 2) {
               ivTokenized[1] = ivTokenized[1].Replace(")", "").Trim();
               if (int.TryParse(ivTokenized[1], out int fixedIV)) {
                  ivs.Add(fixedIV * 255 / 31);
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

      public TrainerPokemonTeamRun UpdateFromParent(ModelDelta token, int parentSegmentChange, int pointerSource) {
         // we only care if the change was to the parent's structType or pokemonCount.
         if (parentSegmentChange != 0 && parentSegmentChange != 11) return this;

         var newStructType = model[pointerSource - TrainerFormat_PointerOffset];
         var newElementCount = model[pointerSource - TrainerFormat_PointerOffset + TrainerFormat_PokemonCountOffset];

         var newRun = this;
         if (newElementCount != ElementCount) newRun = (TrainerPokemonTeamRun)newRun.Append(token, newElementCount - ElementCount);
         if (newStructType != StructType) {
            var data = new TeamData(model, newRun.Start, StructType, newElementCount);
            if ((newStructType & INCLUDE_MOVES) != 0 && !data.MovesIncluded) {
               data.SetDefaultMoves(this);
               newRun = (TrainerPokemonTeamRun)model.RelocateForExpansion(token, newRun, newRun.Length * 2);
            } else if ((newStructType & INCLUDE_MOVES) == 0 && data.MovesIncluded) {
               data.RemoveMoves();
            }
            if ((newStructType & INCLUDE_ITEM) != 0 && !data.ItemsIncluded) {
               data.SetDefaultItems();
            } else if ((newStructType & INCLUDE_ITEM) == 0 && data.ItemsIncluded) {
               data.RemoveItems();
            }
            WriteData(token, newRun.Start, data);
         }
         if (newElementCount != ElementCount || newStructType != StructType) {
            UpdateParents(token, newStructType, newElementCount, newRun.PointerSources);
         }

         return new TrainerPokemonTeamRun(model, newRun.Start, PointerSources, pointerSource);
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
            if (model.GetNextRun(movesStart) is PLMRun run) {
               for (int i = 0; i < run.Length; i += 2) {
                  var pair = model.ReadMultiByteValue(run.Start + i, 2);
                  var (level, move) = PLMRun.SplitToken(pair);
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
