using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   /// <summary>
   /// Marker for the runs that the map editor's trainer panel and the trainer table tool can edit as a team.
   /// </summary>
   public interface ITrainerTeamRun : IStreamRun, ITableRun {
      IEnumerable<int> Search(string parentArrayName, int id);
      /// <summary>
      /// Like IStreamRun.DeserializeRun, but can also fill in each Pokémon's moves (the last 4 level-up moves at its level)
      /// and/or reset the held items to the species default.
      /// </summary>
      ITrainerTeamRun DeserializeRun(string content, ModelDelta token, bool setDefaultMoves, bool setDefaultItems, out IReadOnlyList<int> changedOffsets);
   }

   /// <summary>
   /// Format Specifier: `tpte`
   /// A trainer's party in pokeemerald-expansion (struct TrainerMon, 36 bytes per Pokémon):
   ///    0 nickname(pointer)  4 evs(pointer)  8 ivs::  12 move1: move2: move3: move4:  20 species:  22 item:  24 ability:  26 level.  27 ball.
   ///    28 friendship.  29 nature:5 gender:2 shiny:1  30 teraType:5 gmax:1 dynamax:1 pad:1  31 dynamaxLevel:4 pad:4  32 tags::
   /// The number of Pokémon comes from the 'pokemonCount' field of the trainer that points here.
   /// The text form is the same as HexManiac's vanilla team editor (level, species, IVs, item, moves), plus an optional
   /// '*' line per Pokémon for the expansion-only fields. Fields that aren't mentioned in the text keep their old values.
   /// </summary>
   public class ExpansionTrainerTeamRun : BaseRun, ITrainerTeamRun, IUpdateFromParentRun {
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "tpte" + AsciiRun.StreamDelimeter;
      public const int ElementSize = 36;
      public const int MaxTeamSize = 6;
      private const int Offset_Nickname = 0, Offset_Evs = 4, Offset_Ivs = 8, Offset_Moves = 12, Offset_Species = 20, Offset_Item = 22, Offset_Ability = 24,
         Offset_Level = 26, Offset_Ball = 27, Offset_Friendship = 28, Offset_NatureGenderShiny = 29, Offset_TeraDynamax = 30, Offset_DynamaxLevel = 31, Offset_Tags = 32;
      private const int Ball_Default = 28, Ball_Random = 29, Gender_Default = 3, DynamaxLevel_Default = 10;
      public const string SpeciesTable = "data.pokemon.stats";
      public const string MovesTable = "data.pokemon.moves.stats";
      public const string NatureList = "natures";
      public const string TypesTable = "data.pokemon.type.names";

      private readonly IDataModel model;
      private readonly int primarySource; // the pointer in the trainer table that points at this team, or -1

      public override int Length => ElementLength * ElementCount;
      public override string FormatString => SharedFormatString;
      public int ElementCount { get; }
      public int ElementLength => ElementSize;
      public IReadOnlyList<string> ElementNames { get; } = new List<string>();
      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }
      public bool CanAppend => ElementCount < MaxTeamSize;
      string IUpdateFromParentRun.RepointContentShortName => "Team";

      public ExpansionTrainerTeamRun(IDataModel model, int start, SortedSpan<int> sources) : base(start, sources) {
         this.model = model;
         primarySource = -1;
         ElementCount = 1;
         foreach (var source in sources ?? SortedSpan<int>.None) {
            if (model.GetNextRun(source) is not ITableRun table || table.Start > source) continue;
            primarySource = source;
            var count = ReadParentCount(model, table, source);
            if (count >= 0) ElementCount = count;
            break;
         }
         ElementCount = ElementCount.LimitToRange(1, MaxTeamSize);
         ElementContent = CreateSegments(model);
      }

      private static IReadOnlyList<ArrayRunElementSegment> CreateSegments(IDataModel model) => new List<ArrayRunElementSegment> {
         new ArrayRunElementSegment("nickname", ElementContentType.Pointer, 4),
         new ArrayRunElementSegment("evs", ElementContentType.Pointer, 4),
         new ArrayRunHexSegment("ivs", 4),
         new ArrayRunEnumSegment("move1", 2, MovesTable),
         new ArrayRunEnumSegment("move2", 2, MovesTable),
         new ArrayRunEnumSegment("move3", 2, MovesTable),
         new ArrayRunEnumSegment("move4", 2, MovesTable),
         new ArrayRunEnumSegment("species", 2, SpeciesTable),
         new ArrayRunEnumSegment("item", 2, HardcodeTablesModel.ItemsTableName),
         new ArrayRunEnumSegment("ability", 2, HardcodeTablesModel.AbilityNamesTable),
         new ArrayRunElementSegment("level", ElementContentType.Integer, 1),
         new ArrayRunElementSegment("ball", ElementContentType.Integer, 1),
         new ArrayRunElementSegment("friendship", ElementContentType.Integer, 1),
         new ArrayRunElementSegment("natureGenderShiny", ElementContentType.Integer, 1),
         new ArrayRunElementSegment("teraDynamax", ElementContentType.Integer, 1),
         new ArrayRunElementSegment("dynamaxLevel", ElementContentType.Integer, 1),
         new ArrayRunHexSegment("tags", 4),
      };

      #region parent (trainer) access

      /// <summary>
      /// Given a pointer within the trainer table, find the address of the trainer's party-size field.
      /// </summary>
      public static int FindParentCountAddress(IDataModel model, ITableRun table, int pointerSource) {
         var offsets = table.ConvertByteOffsetToArrayOffset(pointerSource);
         if (offsets.ElementIndex < 0) return -1;
         var elementStart = table.Start + table.ElementLength * offsets.ElementIndex;
         int fieldOffset = 0;
         foreach (var segment in table.ElementContent) {
            if (segment.Name.ToLower() is "pokemoncount" or "partysize" or "pokemonCount") return elementStart + fieldOffset;
            fieldOffset += segment.Length;
         }
         return -1;
      }

      private static int ReadParentCount(IDataModel model, ITableRun table, int pointerSource) {
         var address = FindParentCountAddress(model, table, pointerSource);
         if (address < 0) return -1;
         return model[address];
      }

      private void WriteParentCount(ModelDelta token, IReadOnlyList<int> sources, int count) {
         foreach (var source in sources) {
            if (model.GetNextRun(source) is not ITableRun table || table.Start > source) continue;
            var address = FindParentCountAddress(model, table, source);
            if (address < 0) continue;
            if (model[address] != count) token.ChangeData(model, address, (byte)count);
         }
      }

      #endregion

      #region BaseRun / ITableRun

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => this.CreateSegmentDataFormat(data, index);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new ExpansionTrainerTeamRun(model, Start, newPointerSources);

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) => new ExpansionTrainerTeamRun(model, start, pointerSources);

      public ITableRun Append(ModelDelta token, int length) {
         var newCount = (ElementCount + length).LimitToRange(1, MaxTeamSize);
         length = newCount - ElementCount;
         var workingRun = this;
         if (length > 0) workingRun = model.RelocateForExpansion(token, workingRun, ElementLength * newCount);
         for (int i = 0; i < length; i++) {
            var start = workingRun.Start + (ElementCount + i) * ElementLength;
            var template = workingRun.Start + (ElementCount + i - 1) * ElementLength;
            for (int j = 0; j < ElementLength; j++) token.ChangeData(model, start + j, model[template + j]);
         }
         for (int i = newCount * ElementLength; i < ElementCount * ElementLength; i++) token.ChangeData(model, workingRun.Start + i, 0xFF);
         WriteParentCount(token, workingRun.PointerSources, newCount);
         return new ExpansionTrainerTeamRun(model, workingRun.Start, workingRun.PointerSources);
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, int depth) => ITableRunExtensions.AppendTo(this, model, builder, start, length, depth);

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) => ITableRunExtensions.Clear(this, model, changeToken, start, length);

      IUpdateFromParentRun IUpdateFromParentRun.UpdateFromParent(ModelDelta token, int parentSegmentChange, int pointerSource) {
         if (model.GetNextRun(pointerSource) is not ITableRun table) return this;
         if (!parentSegmentChange.InRange(0, table.ElementContent.Count)) return this;
         var changed = table.ElementContent[parentSegmentChange].Name.ToLower();
         if (changed is not ("pokemoncount" or "partysize")) return this;
         var newCount = ReadParentCount(model, table, pointerSource);
         if (newCount < 1 || newCount > MaxTeamSize || newCount == ElementCount) return this;
         return (IUpdateFromParentRun)Append(token, newCount - ElementCount);
      }

      public IEnumerable<int> Search(string parentArrayName, int id) {
         for (int i = 0; i < ElementCount; i++) {
            var start = Start + i * ElementLength;
            if (parentArrayName == SpeciesTable) {
               if (model.ReadMultiByteValue(start + Offset_Species, 2) == id) yield return start + Offset_Species;
            } else if (parentArrayName == MovesTable) {
               for (int j = 0; j < 4; j++) if (model.ReadMultiByteValue(start + Offset_Moves + j * 2, 2) == id) yield return start + Offset_Moves + j * 2;
            } else if (parentArrayName == HardcodeTablesModel.ItemsTableName) {
               if (model.ReadMultiByteValue(start + Offset_Item, 2) == id) yield return start + Offset_Item;
            } else if (parentArrayName == HardcodeTablesModel.AbilityNamesTable) {
               if (model.ReadMultiByteValue(start + Offset_Ability, 2) == id) yield return start + Offset_Ability;
            }
         }
      }

      #endregion

      #region IStreamRun

      private IReadOnlyList<string> Names(string table) => ModelCacheScope.GetCache(model).GetOptions(table);
      private static string NameOf(IReadOnlyList<string> names, int id) => names != null && id >= 0 && id < names.Count && !string.IsNullOrEmpty(names[id]) ? names[id] : id.ToString();
      /// <summary>
      /// Options with spaces are stored quoted ("Great Ball"), so compare with and without quotes, exact first, then by prefix, then partial.
      /// </summary>
      private static int FindOption(IReadOnlyList<string> names, string text) {
         if (names == null || names.Count == 0) return 0;
         text = text.Trim().Trim('"');
         if (text.Length == 0) return 0;
         var quoted = $"\"{text}\"";
         for (int i = 0; i < names.Count; i++) if (names[i] == text || names[i] == quoted) return i;
         for (int i = 0; i < names.Count; i++) if (string.Equals(names[i].Trim('"'), text, StringComparison.OrdinalIgnoreCase)) return i;
         for (int i = 0; i < names.Count; i++) if (names[i].Trim('"').StartsWith(text, StringComparison.OrdinalIgnoreCase)) return i;
         return Math.Max(0, names.IndexOfPartial(text));
      }

      /// <summary>
      /// Like FindOption, but a number is used as-is, and when the name list isn't available (the table's anchor is missing or broken)
      /// the old value is kept: FindOption would answer 0 for every name, which wipes species / moves / items.
      /// </summary>
      private static int FindOptionOrKeep(IReadOnlyList<string> names, string text, int oldValue) {
         var trimmed = text.Trim().Trim('"');
         if (int.TryParse(trimmed, out var number) && number >= 0) return number;
         if (names == null || names.Count == 0) return oldValue;
         return FindOption(names, text);
      }

      private static int ReadValue(byte[] bytes, int offset, int width) {
         int value = 0;
         for (int i = 0; i < width; i++) value |= bytes[offset + i] << (8 * i);
         return value;
      }

      private static string Quote(string name) => name.Contains(' ') && !name.StartsWith("\"") ? $"\"{name}\"" : name;

      private static int[] UnpackIVs(int packed) => Enumerable.Range(0, 6).Select(i => (packed >> (5 * i)) & 31).ToArray();
      private static int PackIVs(IReadOnlyList<int> ivs) { int packed = 0; for (int i = 0; i < 6; i++) packed |= (ivs[i].LimitToRange(0, 31)) << (5 * i); return packed; }

      public string SerializeRun() {
         var species = Names(SpeciesTable);
         var moves = Names(MovesTable);
         var items = Names(HardcodeTablesModel.ItemsTableName);
         var abilities = Names(HardcodeTablesModel.AbilityNamesTable);
         var natures = Names(NatureList);
         var types = Names(TypesTable);
         var builder = new StringBuilder();
         for (int i = 0; i < ElementCount; i++) {
            var start = Start + i * ElementLength;
            var level = model[start + Offset_Level];
            var speciesID = model.ReadMultiByteValue(start + Offset_Species, 2);
            var itemID = model.ReadMultiByteValue(start + Offset_Item, 2);
            var ivs = UnpackIVs(model.ReadMultiByteValue(start + Offset_Ivs, 4));
            var ivText = ivs.All(iv => iv == ivs[0]) ? ivs[0].ToString() : "/".Join(ivs.Select(iv => iv.ToString()));
            builder.Append($"{level} {Quote(NameOf(species, speciesID))} (IVs={ivText})");
            if (itemID != 0) builder.Append($" @{Quote(NameOf(items, itemID))}");
            builder.AppendLine();
            for (int j = 0; j < 4; j++) {
               var moveID = model.ReadMultiByteValue(start + Offset_Moves + j * 2, 2);
               if (moveID != 0) builder.AppendLine($"- {NameOf(moves, moveID)}");
            }
            var extras = new List<string>();
            var ability = model.ReadMultiByteValue(start + Offset_Ability, 2);
            if (ability != 0) extras.Add($"ability={Quote(NameOf(abilities, ability))}");
            var ngs = model[start + Offset_NatureGenderShiny];
            var nature = ngs & 31; var gender = (ngs >> 5) & 3; var shiny = (ngs >> 7) & 1;
            if (nature != 0) extras.Add($"nature={NameOf(natures, nature)}");
            if (gender != Gender_Default) extras.Add("gender=" + (gender == 1 ? "male" : gender == 2 ? "female" : gender.ToString()));
            if (shiny != 0) extras.Add("shiny=yes");
            var ball = model[start + Offset_Ball];
            if (ball != Ball_Default) extras.Add("ball=" + (ball == Ball_Random ? "random" : Quote(NameOf(items, ball))));
            var friendship = model[start + Offset_Friendship];
            if (friendship != 0) extras.Add($"friendship={friendship}");
            var td = model[start + Offset_TeraDynamax];
            var tera = td & 31; var gmax = (td >> 5) & 1; var dmax = (td >> 6) & 1;
            if (tera != 0) extras.Add($"tera={NameOf(types, tera)}");
            var dmaxLevel = model[start + Offset_DynamaxLevel] & 15;
            if (gmax != 0) extras.Add("gmax=yes");
            if (dmax != 0) extras.Add("dynamax=yes");
            else if (gmax != 0 || dmaxLevel != DynamaxLevel_Default) extras.Add("dynamax=no"); // gmax= and dmaxlevel= alone mean 'dynamax=yes' (like trainerproc)
            if (dmaxLevel != DynamaxLevel_Default) extras.Add($"dmaxlevel={dmaxLevel}");
            var tags = model.ReadMultiByteValue(start + Offset_Tags, 4);
            if (tags != 0) extras.Add($"tags={tags:X}");
            var evPointer = model.ReadPointer(start + Offset_Evs);
            if (evPointer >= 0 && evPointer + 6 <= model.Count) extras.Add("evs=" + "/".Join(6.Range().Select(k => model[evPointer + k].ToString())));
            var nickPointer = model.ReadPointer(start + Offset_Nickname);
            if (nickPointer >= 0 && nickPointer < model.Count) {
               var nick = model.TextConverter.Convert(model, nickPointer, 12).Trim('"');
               extras.Add($"nickname=\"{nick}\"");
            }
            if (extras.Count > 0) builder.AppendLine("* " + " ".Join(extras));
            if (i + 1 < ElementCount) builder.AppendLine();
         }
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets, out IReadOnlyList<int> movedChildren) {
         movedChildren = new List<int>();
         return DeserializeRun(content, token, false, false, out changedOffsets);
      }

      public ITrainerTeamRun DeserializeRun(string content, ModelDelta token, bool setDefaultMoves, bool setDefaultItems, out IReadOnlyList<int> changedOffsets) {
         var changed = new List<int>();
         content = NormalizeShowdown(content);
         var lines = content.Split('\n').Select(line => line.Trim('\r').Trim()).ToList();
         var species = Names(SpeciesTable);
         var moves = Names(MovesTable);
         var items = Names(HardcodeTablesModel.ItemsTableName);
         var abilities = Names(HardcodeTablesModel.AbilityNamesTable);
         var natures = Names(NatureList);
         var types = Names(TypesTable);

         // step 1: parse into per-pokemon records.
         // The bytes start as the existing record (or a copy of the last one), so the nickname and EV pointers are kept,
         // but every field of the '*' line starts at its default: the text always shows all of them that differ from the default,
         // so a key that is not in the text (any more) means the default. Otherwise deleting '* tera=Fire' would keep the Tera type,
         // and a new Pokemon would silently inherit the Tera type / Dynamax of the Pokemon it was copied from.
         var records = new List<byte[]>();
         var evsWritten = new List<bool>();
         byte[] current = null;
         int moveIndex = 0;
         foreach (var line in lines) {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("-")) {
               if (current == null || moveIndex >= 4) continue;
               var moveName = line.Substring(1).Trim().Trim('"');
               var moveID = moveName is "" or "-" or "none" ? 0 : FindOptionOrKeep(moves, moveName, ReadValue(current, Offset_Moves + moveIndex * 2, 2));
               WriteValue(current, Offset_Moves + moveIndex * 2, 2, moveID);
               moveIndex++;
            } else if (line.StartsWith("*")) {
               if (current == null) continue;
               if (ApplyExtras(current, line.Substring(1), items, abilities, natures, types, token)) evsWritten[evsWritten.Count - 1] = true;
            } else {
               if (records.Count >= MaxTeamSize) continue;
               var tokens = line.Split(' ', 2);
               if (tokens.Length < 2 || !int.TryParse(tokens[0], out var level)) continue;
               current = TemplateFor(records.Count);
               ResetExtras(current);
               records.Add(current);
               evsWritten.Add(false);
               moveIndex = 0;
               if (moves != null && moves.Count > 0) for (int j = 0; j < 4; j++) WriteValue(current, Offset_Moves + j * 2, 2, 0);
               current[Offset_Level] = (byte)level.LimitToRange(0, 255);
               var rest = tokens[1];
               var itemSplit = rest.Split('@', 2);
               var itemID = 0;
               if (itemSplit.Length == 2) itemID = FindOptionOrKeep(items, itemSplit[1], ReadValue(current, Offset_Item, 2));
               WriteValue(current, Offset_Item, 2, itemID);
               var ivSplit = itemSplit[0].Split('(', 2);
               if (ivSplit.Length == 2) {
                  var ivText = ivSplit[1].Replace(")", "").Split('=').Last().Trim();
                  var parts = ivText.Split('/');
                  var ivs = new int[6];
                  if (parts.Length == 1 && int.TryParse(parts[0], out var all)) for (int k = 0; k < 6; k++) ivs[k] = all;
                  else for (int k = 0; k < 6 && k < parts.Length; k++) int.TryParse(parts[k], out ivs[k]);
                  WriteValue(current, Offset_Ivs, 4, PackIVs(ivs));
               }
               var speciesName = ivSplit[0].Trim().Trim('"');
               WriteValue(current, Offset_Species, 2, FindOptionOrKeep(species, speciesName, ReadValue(current, Offset_Species, 2)));
            }
         }
         for (int i = 0; i < records.Count; i++) {
            if (!evsWritten[i]) WriteValue(records[i], Offset_Evs, 4, 0); // no 'evs=' in the text: no EVs
         }
         if (records.Count == 0) records.Add(TemplateFor(0));
         if (setDefaultMoves || setDefaultItems) {
            foreach (var record in records) {
               var speciesID = record[Offset_Species] | (record[Offset_Species + 1] << 8);
               if (setDefaultMoves) {
                  var defaults = GetDefaultMoves(speciesID, record[Offset_Level]);
                  for (int j = 0; j < 4; j++) WriteValue(record, Offset_Moves + j * 2, 2, defaults[j]);
               }
               if (setDefaultItems) {
                  var pokemonTable = model.GetTable(SpeciesTable);
                  WriteValue(record, Offset_Item, 2, pokemonTable?.ReadValue(model, speciesID, "item1") ?? 0);
               }
            }
         }

         // step 2: make room
         var workingRun = this;
         var newLength = records.Count * ElementLength;
         if (newLength > Length) workingRun = model.RelocateForExpansion(token, workingRun, newLength);

         // step 3: write
         for (int i = 0; i < records.Count; i++) {
            var start = workingRun.Start + i * ElementLength;
            for (int j = 0; j < ElementLength; j++) {
               if (model[start + j] == records[i][j]) continue;
               token.ChangeData(model, start + j, records[i][j]);
               changed.Add(start + j);
            }
         }
         for (int i = newLength; i < Length; i++) token.ChangeData(model, workingRun.Start + i, 0xFF);

         // step 4: parent
         WriteParentCount(token, workingRun.PointerSources, records.Count);

         changedOffsets = changed;
         return new ExpansionTrainerTeamRun(model, workingRun.Start, workingRun.PointerSources);
      }

      #region Showdown / .party paste support

      private static readonly string[] statOrder = { "hp", "atk", "def", "spe", "spa", "spd" }; // the game's stat order: HP, Attack, Defense, Speed, Sp.Atk, Sp.Def

      /// <summary>
      /// Accepts Pokémon Showdown sets and pokeemerald-expansion .party sets (the format trainerproc reads) and converts them into this run's native text.
      /// Native lines pass through unchanged, so the two formats can be mixed. Example input:
      ///   Geodude @ Oran Berry
      ///   Level: 12
      ///   Ability: Rock Head
      ///   EVs: 252 Atk / 4 Def
      ///   IVs: 31 HP / 0 Atk
      ///   Adamant Nature
      ///   - Tackle
      /// </summary>
      public static string NormalizeShowdown(string content) {
         var output = new StringBuilder();
         string header = null;         // native header text (level species (IVs) @item), or null while a Showdown block is being collected
         string species = null, item = null, ivs = null;
         int level = 100;
         bool showdownBlock = false;
         var moves = new List<string>();
         var extras = new List<string>();

         void Flush() {
            if (header == null && species == null) return;
            if (showdownBlock) {
               header = $"{level} {Quote(species)}";
               if (ivs != null) header += $" (IVs={ivs})";
               if (!string.IsNullOrEmpty(item)) header += $" @{Quote(item)}";
            } else if (level >= 0 && header != null) {
               // a 'Level:' line under a native header replaces the level in the header
               var parts = header.Split(' ', 2);
               if (parts.Length == 2 && int.TryParse(parts[0], out _)) header = $"{level} {parts[1]}";
            }
            output.AppendLine(header);
            foreach (var move in moves) output.AppendLine("- " + move);
            if (extras.Count > 0) output.AppendLine("* " + " ".Join(extras));
            header = species = item = ivs = null;
            level = -1;
            showdownBlock = false;
            moves.Clear();
            extras.Clear();
         }

         foreach (var raw in content.Split('\n')) {
            var line = raw.Trim('\r').Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("-")) {
               if (header == null && species == null) output.AppendLine(line); else moves.Add(line.Substring(1).Trim());
               continue;
            }
            if (line.StartsWith("*")) {
               if (header == null && species == null) output.AppendLine(line); else extras.Add(line.Substring(1).Trim());
               continue;
            }
            if (char.IsDigit(line[0])) {
               // native header: level species ...
               Flush();
               header = line;
               level = -1;
               continue;
            }
            var colon = line.IndexOf(':');
            var inBlock = header != null || species != null;
            if (colon > 0) {
               var scratchLevel = level; string scratchIvs = ivs; var scratchExtras = inBlock ? extras : new List<string>();
               if (TryShowdownKey(line.Substring(0, colon).Trim(), line.Substring(colon + 1).Trim(), ref scratchLevel, ref scratchIvs, scratchExtras)) {
                  if (inBlock) { level = scratchLevel; ivs = scratchIvs; }
                  continue; // a key line outside of any block has nothing to apply to
               }
            }
            if (line.EndsWith(" Nature", StringComparison.OrdinalIgnoreCase)) {
               if (inBlock) extras.Add("nature=" + line.Substring(0, line.Length - " Nature".Length).Trim());
               continue;
            }
            // anything else starts a Showdown block: [Nickname (]Species[)] [(M|F)] [@ Item]
            Flush();
            showdownBlock = true;
            level = 100;
            var text = line;
            var at = text.IndexOf('@');
            if (at >= 0) { item = text.Substring(at + 1).Trim(); text = text.Substring(0, at).Trim(); }
            while (text.EndsWith(")")) {
               var open = text.LastIndexOf('(');
               if (open < 0) break;
               var inner = text.Substring(open + 1, text.Length - open - 2).Trim();
               text = text.Substring(0, open).Trim();
               if (inner.Equals("M", StringComparison.OrdinalIgnoreCase)) extras.Add("gender=male");
               else if (inner.Equals("F", StringComparison.OrdinalIgnoreCase)) extras.Add("gender=female");
               else if (inner.Length > 0) text = inner; // 'Nickname (Species)': use the species, drop the nickname
            }
            species = text.Trim();
         }
         Flush();
         return output.ToString();
      }

      private static bool TryShowdownKey(string key, string value, ref int level, ref string ivs, List<string> extras) {
         switch (key.ToLowerInvariant()) {
            case "level": if (int.TryParse(value, out var lv)) level = lv; return true;
            case "ability": extras.Add("ability=" + value); return true;
            case "evs": extras.Add("evs=" + ParseStatList(value, 0)); return true;
            case "ivs": ivs = ParseStatList(value, 31); return true;
            case "shiny": extras.Add("shiny=" + (IsYes(value) ? "yes" : "no")); return true;
            case "ball": extras.Add("ball=" + value); return true;
            case "happiness": case "friendship": extras.Add("friendship=" + value); return true;
            case "gender": extras.Add("gender=" + value); return true;
            case "nature": extras.Add("nature=" + value); return true;
            case "tera type": case "tera": extras.Add("tera=" + value); return true;
            case "dynamax level": extras.Add("dmaxlevel=" + value); return true;
            case "gigantamax": extras.Add("gmax=" + (IsYes(value) ? "yes" : "no")); return true;
            case "dynamax": extras.Add("dynamax=" + (IsYes(value) ? "yes" : "no")); return true;
            case "nickname": return true; // nicknames are edited in the table itself
            default: return false;
         }
      }

      /// <summary>
      /// '252 Atk / 4 Def / 252 Spe' -> 'hp/atk/def/spe/spa/spd' values in the game's order; unspecified stats get the default.
      /// </summary>
      private static string ParseStatList(string text, int defaultValue) {
         var values = Enumerable.Repeat(defaultValue, 6).ToArray();
         foreach (var part in text.Split('/')) {
            var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || !int.TryParse(tokens[0], out var amount)) continue;
            var stat = tokens[1].ToLowerInvariant() switch {
               "hp" => "hp", "atk" or "attack" => "atk", "def" or "defense" => "def", "spe" or "speed" => "spe",
               "spa" or "spatk" or "sp.atk" or "sp.att" or "spattack" => "spa", "spd" or "spdef" or "sp.def" or "spdefense" => "spd", _ => null,
            };
            var index = Array.IndexOf(statOrder, stat);
            if (index >= 0) values[index] = amount;
         }
         return "/".Join(values.Select(v => v.ToString()));
      }

      #endregion

      /// <summary>
      /// The last 4 level-up moves the species knows at the given level, read from the species' levelUpMoves list ([move: level:] records).
      /// </summary>
      private IReadOnlyList<int> GetDefaultMoves(int speciesID, int level) {
         var results = new List<int>();
         if (model.GetTable(SpeciesTable) is ITableRun species && speciesID < species.ElementCount) {
            int offset = 0, listOffset = -1;
            foreach (var segment in species.ElementContent) {
               if (segment.Name == "levelUpMoves") { listOffset = offset; break; }
               offset += segment.Length;
            }
            if (listOffset >= 0) {
               var listStart = model.ReadPointer(species.Start + species.ElementLength * speciesID + listOffset);
               if (model.GetNextRun(listStart) is ITableRun list && list.Start == listStart && list.ElementContent.Count >= 2) {
                  var moveWidth = list.ElementContent[0].Length;
                  var levelWidth = list.ElementContent[1].Length;
                  for (int i = 0; i < list.ElementCount; i++) {
                     var move = model.ReadMultiByteValue(list.Start + i * list.ElementLength, moveWidth);
                     var moveLevel = model.ReadMultiByteValue(list.Start + i * list.ElementLength + moveWidth, levelWidth);
                     if (move == 0 && moveLevel == 0) break;
                     if (moveLevel > level) continue;
                     results.Remove(move);
                     results.Add(move);
                  }
               }
            }
         }
         while (results.Count > 4) results.RemoveAt(0);
         while (results.Count < 4) results.Add(0);
         return results;
      }

      private byte[] TemplateFor(int index) {
         var bytes = new byte[ElementLength];
         if (index < ElementCount) {
            Array.Copy(model.RawData, Start + index * ElementLength, bytes, 0, ElementLength);
         } else if (ElementCount > 0) {
            Array.Copy(model.RawData, Start + (ElementCount - 1) * ElementLength, bytes, 0, ElementLength);
            WriteValue(bytes, Offset_Nickname, 4, 0); WriteValue(bytes, Offset_Evs, 4, 0); // pointers aren't shared with the copied pokemon
         } else {
            bytes[Offset_Ball] = Ball_Default;
            bytes[Offset_NatureGenderShiny] = Gender_Default << 5;
            bytes[Offset_DynamaxLevel] = DynamaxLevel_Default;
         }
         return bytes;
      }

      private static void WriteValue(byte[] bytes, int offset, int width, int value) {
         for (int i = 0; i < width; i++) bytes[offset + i] = (byte)(value >> (8 * i));
      }

      /// <summary>
      /// Sets every field that the '*' line can change to its default (the value that SerializeRun doesn't write).
      /// The pointers (nickname, EVs) and the padding bits are kept.
      /// </summary>
      private static void ResetExtras(byte[] record) {
         WriteValue(record, Offset_Ability, 2, 0);
         record[Offset_Ball] = Ball_Default;
         record[Offset_Friendship] = 0;
         record[Offset_NatureGenderShiny] = Gender_Default << 5;
         record[Offset_TeraDynamax] = (byte)(record[Offset_TeraDynamax] & 0x80);
         record[Offset_DynamaxLevel] = (byte)((record[Offset_DynamaxLevel] & 0xF0) | DynamaxLevel_Default);
         WriteValue(record, Offset_Tags, 4, 0);
      }

      /// <summary>
      /// Applies one '*' line. Returns true if the line had an 'evs=' key.
      /// Like the expansion's trainerproc ('Gigantamax: Yes' / 'Dynamax Level: n' set shouldUseDynamax), gmax=yes and dmaxlevel=n
      /// also mean dynamax=yes, unless the line says dynamax=no.
      /// </summary>
      private bool ApplyExtras(byte[] record, string text, IReadOnlyList<string> items, IReadOnlyList<string> abilities, IReadOnlyList<string> natures, IReadOnlyList<string> types, ModelDelta token) {
         bool hasEvs = false, impliesDynamax = false;
         bool? dynamax = null;
         foreach (var pair in ParseKeyValues(text)) {
            var v = pair.Value.Trim().Trim('"');
            switch (CanonicalKey(pair.Key)) {
               case "ability": WriteValue(record, Offset_Ability, 2, FindOption(abilities, v)); break;
               case "nature": {
                     var n = FindOption(natures, v);
                     record[Offset_NatureGenderShiny] = (byte)((record[Offset_NatureGenderShiny] & ~31) | (n & 31));
                     break;
                  }
               case "gender": {
                     var g = ParseGender(v);
                     if (g < 0) break;
                     record[Offset_NatureGenderShiny] = (byte)((record[Offset_NatureGenderShiny] & ~(3 << 5)) | ((g & 3) << 5));
                     break;
                  }
               case "shiny": {
                     var s = ParseBool(v);
                     if (s < 0) break;
                     record[Offset_NatureGenderShiny] = (byte)((record[Offset_NatureGenderShiny] & 0x7F) | (s << 7));
                     break;
                  }
               case "ball": {
                     var b = v.ToLower() is "default" or "" ? Ball_Default : v.ToLower() == "random" ? Ball_Random : int.TryParse(v, out var bv) ? bv : FindOption(items, v);
                     record[Offset_Ball] = (byte)b.LimitToRange(0, 255);
                     break;
                  }
               case "friendship": if (int.TryParse(v, out var f)) record[Offset_Friendship] = (byte)f.LimitToRange(0, 255); break;
               case "tera": {
                     var t = ParseType(types, v);
                     if (t < 0) break; // unknown type: the text editor marks it, nothing is written
                     record[Offset_TeraDynamax] = (byte)((record[Offset_TeraDynamax] & ~31) | (t & 31));
                     break;
                  }
               case "gmax": {
                     var g = ParseBool(v);
                     if (g < 0) break;
                     record[Offset_TeraDynamax] = (byte)((record[Offset_TeraDynamax] & ~(1 << 5)) | (g << 5));
                     if (g == 1) impliesDynamax = true;
                     break;
                  }
               case "dynamax": {
                     var d = ParseBool(v);
                     if (d >= 0) dynamax = d == 1;
                     break;
                  }
               case "dmaxlevel": {
                     if (!int.TryParse(v, out var dl) || !dl.InRange(0, 11)) break;
                     record[Offset_DynamaxLevel] = (byte)((record[Offset_DynamaxLevel] & 0xF0) | dl);
                     impliesDynamax = true;
                     break;
                  }
               case "tags": if (int.TryParse(v, System.Globalization.NumberStyles.HexNumber, null, out var tags)) WriteValue(record, Offset_Tags, 4, tags); break;
               case "evs": {
                     hasEvs = true;
                     var parts = v.Split('/');
                     var evs = new int[6];
                     for (int k = 0; k < 6 && k < parts.Length; k++) int.TryParse(parts[k], out evs[k]);
                     var pointer = (int)(record[Offset_Evs] | (record[Offset_Evs + 1] << 8) | (record[Offset_Evs + 2] << 16) | (record[Offset_Evs + 3] << 24)) - 0x08000000;
                     if (evs.All(e => e == 0) && (pointer < 0 || pointer >= model.Count)) break;
                     if (pointer < 0 || pointer + 6 > model.Count) {
                        pointer = model.FindFreeSpace(0x100, 8);
                        if (pointer < 0) break;
                        WriteValue(record, Offset_Evs, 4, pointer + 0x08000000);
                     }
                     for (int k = 0; k < 6; k++) if (model[pointer + k] != evs[k]) token.ChangeData(model, pointer + k, (byte)evs[k].LimitToRange(0, 255));
                     break;
                  }
               case "nickname": break; // the nickname pointer is kept as-is; edit the text itself in the table tool
            }
         }
         var useDynamax = dynamax ?? (impliesDynamax ? true : (bool?)null);
         if (useDynamax.HasValue) record[Offset_TeraDynamax] = (byte)((record[Offset_TeraDynamax] & ~(1 << 6)) | ((useDynamax.Value ? 1 : 0) << 6));
         return hasEvs;
      }

      private static bool IsYes(string v) => ParseBool(v) == 1;

      /// <summary>1 for yes, 0 for no, -1 if it's neither.</summary>
      internal static int ParseBool(string v) => v.Trim().Trim('"').ToLowerInvariant() switch {
         "yes" or "true" or "1" or "y" or "on" => 1,
         "no" or "false" or "0" or "n" or "off" => 0,
         _ => -1,
      };

      /// <summary>1 male, 2 female, 3 random, -1 if unknown.</summary>
      internal static int ParseGender(string v) => v.Trim().Trim('"').ToLowerInvariant() switch {
         "male" or "m" or "1" => 1,
         "female" or "f" or "2" => 2,
         "random" or "any" or "3" or "default" => 3,
         "0" => 0,
         _ => -1,
      };

      /// <summary>
      /// A type by name ('Fire', 'fire', 'TYPE_FIRE', '"Fire"'), by the beginning of its name ('Fir'), or by number.
      /// 'none' / 'no' / '-' clear it. Returns -1 when nothing matches (a partial match anywhere in a name would pick a random type).
      /// </summary>
      internal static int ParseType(IReadOnlyList<string> types, string text) {
         text = text.Trim().Trim('"').Trim();
         if (text.StartsWith("TYPE_", StringComparison.OrdinalIgnoreCase)) text = text.Substring(5);
         text = text.Replace('_', ' ');
         if (int.TryParse(text, out var number)) return number.InRange(0, 32) ? number : -1;
         if (text.ToLowerInvariant() is "" or "none" or "no" or "-" or "0") return 0;
         if (types == null) return -1;
         for (int i = 0; i < types.Count; i++) if (string.Equals(types[i]?.Trim('"'), text, StringComparison.OrdinalIgnoreCase)) return i < 32 ? i : -1;
         if (text.Length >= 2) {
            for (int i = 0; i < types.Count && i < 32; i++) if (types[i] != null && types[i].Trim('"').StartsWith(text, StringComparison.OrdinalIgnoreCase)) return i;
         }
         return -1;
      }

      /// <summary>
      /// The key of a '*' line field, in its canonical spelling, or null if it isn't one.
      /// Spaces, '_' and '-' and capital letters don't matter, and the Showdown / .party names are accepted too ('Tera Type', 'Gigantamax', 'Dynamax Level').
      /// </summary>
      internal static string CanonicalKey(string key) {
         var k = new string((key ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
         return k switch {
            "ability" => "ability",
            "nature" => "nature",
            "gender" => "gender",
            "shiny" or "isshiny" => "shiny",
            "ball" or "pokeball" => "ball",
            "friendship" or "happiness" => "friendship",
            "tera" or "teratype" => "tera",
            "gmax" or "gigantamax" or "gigantamaxfactor" => "gmax",
            "dynamax" or "dmax" or "shouldusedynamax" or "shoulddynamax" => "dynamax",
            "dmaxlevel" or "dynamaxlevel" or "dmaxlvl" => "dmaxlevel",
            "tags" => "tags",
            "evs" or "ev" => "evs",
            "nickname" or "nick" => "nickname",
            _ => null,
         };
      }

      internal record ExtraField(string Key, string Value, int Start, int Length);

      /// <summary>
      /// key=value key="value with spaces" key=value with spaces (words without '=' continue the previous value).
      /// 'key = value' and 'key: value' work too, and so do the two-word keys 'Tera Type' and 'Dynamax Level'.
      /// A word on its own at the start of the line, or after a value that doesn't take more words, is 'word=yes' ('* dynamax gmax').
      /// Start/Length cover the whole 'key=value' text (for the error marks of the text editor).
      /// </summary>
      internal static IReadOnlyList<ExtraField> ParseKeyValues(string text) {
         var tokens = new List<(string word, int start, int end, bool separator)>();
         int i = 0;
         while (i < text.Length) {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',' || text[i] == ';')) i++;
            if (i >= text.Length) break;
            var start = i;
            string word;
            if (text[i] == '"') {
               var close = text.IndexOf('"', i + 1);
               if (close == -1) close = text.Length;
               word = text.Substring(start, Math.Min(close + 1, text.Length) - start);
               i = Math.Min(close + 1, text.Length);
            } else {
               while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '=' && text[i] != ':' && text[i] != ',' && text[i] != ';') i++;
               word = text.Substring(start, i - start);
            }
            var end = i;
            var j = i;
            while (j < text.Length && text[j] == ' ') j++;
            var separator = j < text.Length && (text[j] == '=' || text[j] == ':');
            if (separator) i = j + 1;
            if (word.Length == 0 && !separator) { i++; continue; }
            tokens.Add((word, start, separator ? i : end, separator));
         }

         var pairs = new List<ExtraField>();
         var continued = new List<bool>(); // for each pair: whether bare words were added to its value
         for (int t = 0; t < tokens.Count; t++) {
            var (word, start, end, separator) = tokens[t];
            if (separator) {
               var key = word;
               // two-word keys: 'Tera Type:' / 'Dynamax Level ='
               if (t > 0 && !tokens[t - 1].separator && CanonicalKey(key) == null && CanonicalKey(tokens[t - 1].word + key) != null && pairs.Count > 0) {
                  var previous = tokens[t - 1];
                  var last = pairs[pairs.Count - 1];
                  if (last.Key == previous.word && last.Start == previous.start) {
                     pairs.RemoveAt(pairs.Count - 1); continued.RemoveAt(continued.Count - 1);
                  } else if (last.Value.EndsWith(" " + previous.word)) {
                     pairs[pairs.Count - 1] = last with { Value = last.Value.Substring(0, last.Value.Length - previous.word.Length - 1), Length = previous.start - last.Start };
                  }
                  key = previous.word + " " + key;
                  start = previous.start;
               }
               var value = string.Empty;
               if (t + 1 < tokens.Count && !tokens[t + 1].separator) {
                  t++;
                  value = tokens[t].word;
                  end = tokens[t].end;
               }
               pairs.Add(new(key, value, start, end - start));
               continued.Add(false);
            } else if (pairs.Count > 0 && TakesMoreWords(pairs[pairs.Count - 1])) {
               var last = pairs[pairs.Count - 1];
               pairs[pairs.Count - 1] = last with { Value = last.Value + " " + word, Length = end - last.Start };
               continued[continued.Count - 1] = true;
            } else {
               pairs.Add(new(word, "yes", start, end - start));
               continued.Add(false);
            }
         }
         return pairs;
      }

      /// <summary>Only names can have spaces without quotes: 'ability=Rock Head', 'ball=Great Ball', 'nature=...'. A number, yes/no, a type or EVs can't.</summary>
      private static bool TakesMoreWords(ExtraField pair) {
         if (pair.Value.StartsWith("\"")) return false;
         return CanonicalKey(pair.Key) is "ability" or "ball" or "nature" or "nickname" or null;
      }

      /// <summary>
      /// Problems in a '*' line: unknown keys, and values that can't be used (they would be ignored). Positions are relative to the text after the '*'.
      /// </summary>
      internal static IEnumerable<ExtraField> FindExtraErrors(string text, IReadOnlyList<string> types) {
         foreach (var pair in ParseKeyValues(text)) {
            var v = pair.Value.Trim().Trim('"');
            var ok = CanonicalKey(pair.Key) switch {
               null => false,
               "tera" => ParseType(types, v) >= 0,
               "shiny" or "gmax" or "dynamax" => ParseBool(v) >= 0,
               "gender" => ParseGender(v) >= 0,
               "dmaxlevel" => int.TryParse(v, out var dl) && dl.InRange(0, 11),
               "friendship" => int.TryParse(v, out _),
               "tags" => int.TryParse(v, System.Globalization.NumberStyles.HexNumber, null, out _),
               _ => true,
            };
            if (!ok) yield return pair;
         }
      }

      public bool DependsOn(string anchorName) => anchorName == SpeciesTable || anchorName == MovesTable || anchorName == HardcodeTablesModel.ItemsTableName || anchorName == HardcodeTablesModel.AbilityNamesTable;

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var result = new List<AutocompleteItem>();
         var trimmed = line.Trim();
         if (trimmed.StartsWith("*")) return result;
         if (trimmed.StartsWith("-")) {
            var namePart = line.Split(new[] { '-' }, 2)[1].Trim();
            if (namePart == string.Empty) return result;
            return Names(MovesTable)
               .Where(option => option.MatchesPartial(namePart, onlyCheckLettersAndDigits: true))
               .Select(option => new AutocompleteItem(option, $"- {option}")).ToList();
         }
         caretCharacterIndex = caretCharacterIndex.LimitToRange(0, line.Length);
         var end = line.Substring(caretCharacterIndex).Trim();
         var start = line.Substring(0, caretCharacterIndex).Trim();
         var spaceIndex = start.IndexOf(' ');
         if (spaceIndex == -1) return result;
         var parenIndex = start.IndexOf('(');
         var atIndex = start.IndexOf('@');
         if (parenIndex == -1 && atIndex == -1) {
            var level = start.Substring(0, spaceIndex);
            var pokemon = start.Substring(spaceIndex + 1).Trim('"');
            var ops = Names(SpeciesTable).Where(option => option.MatchesPartial(pokemon, onlyCheckLettersAndDigits: true));
            ops = ScriptParser.SortOptions(ops, pokemon, op => op);
            return ops.Select(option => new AutocompleteItem(option, $"{level} {Quote(option)} {end}".Trim())).ToList();
         }
         if (atIndex == -1) return result;
         var item = start.Substring(atIndex + 1).Trim('"');
         start = start.Substring(0, atIndex);
         var itemOps = Names(HardcodeTablesModel.ItemsTableName).Where(option => option.MatchesPartial(item, onlyCheckLettersAndDigits: true));
         itemOps = ScriptParser.SortOptions(itemOps, item, op => op);
         return itemOps.Select(option => new AutocompleteItem(option, $"{start.Trim()} @{Quote(option)}")).ToList();
      }

      public IReadOnlyList<IPixelViewModel> Visualizations {
         get {
            var list = new List<IPixelViewModel>();
            if (model.GetTable(SpeciesTable) is not ITableRun speciesTable) return list;
            int picOffset = -1, offset = 0;
            foreach (var segment in speciesTable.ElementContent) {
               if (segment.Name == "frontPic") { picOffset = offset; break; }
               offset += segment.Length;
            }
            if (picOffset == -1) return list;
            for (int i = 0; i < ElementCount; i++) {
               var index = model.ReadMultiByteValue(Start + ElementLength * i + Offset_Species, 2);
               if (index >= speciesTable.ElementCount) index = 0;
               var spriteAddress = model.ReadPointer(speciesTable.Start + speciesTable.ElementLength * index + picOffset);
               if (model.GetNextRun(spriteAddress) is not Sprites.ISpriteRun sprite || sprite.Start != spriteAddress) return new List<IPixelViewModel>();
               var pixels = SpriteDecorator.BuildSprite(model, sprite, useTransparency: true);
               if (pixels == null) return new List<IPixelViewModel>();
               // only show the first frame of 2-frame front sprites
               if (pixels.PixelHeight == pixels.PixelWidth * 2) pixels = ReadonlyPixelViewModel.Crop(pixels, 0, 0, pixels.PixelWidth, pixels.PixelWidth);
               list.Add(pixels);
            }
            return list;
         }
      }

      public ITextPreProcessor PreFormatter => new ExpansionTrainerTextFormatter(model);

      /// <summary>
      /// The error marks for a trainer team text box that isn't tied to one run (the map editor's trainer panel):
      /// the expansion's when the trainer table's teams are 'tpte' runs, otherwise vanilla HMA's (level / move legality).
      /// </summary>
      public static ITextPreProcessor TeamFormatterFor(IDataModel model) {
         var trainers = model.GetTable(HardcodeTablesModel.TrainerTableName);
         if (trainers != null && trainers.ElementContent.Any(segment => segment is ArrayRunPointerSegment pointer && pointer.InnerFormat == SharedFormatString)) {
            return new ExpansionTrainerTextFormatter(model);
         }
         return new TrainerTextFormatter(model);
      }

      #endregion
   }

   /// <summary>
   /// The expansion team text has no level-up / move legality checks (the expansion's learnsets live inside the species struct).
   /// It marks what in a '*' line can't be used (unknown keys like 'tera type=', unknown types, 'dynamax=maybe'), because those are ignored when the team is written.
   /// </summary>
   public record ExpansionTrainerTextFormatter(IDataModel Model) : ITextPreProcessor {
      public TextFormatting[] Format(string content) => new TextFormatting[0];
      public IEnumerable<TextSegment> FindErrors(string content) {
         var errors = new List<TextSegment>();
         if (string.IsNullOrEmpty(content)) return errors;
         IReadOnlyList<string> types = null;
         var lines = content.SplitLines();
         for (int i = 0; i < lines.Length; i++) {
            var star = lines[i].IndexOf('*');
            if (star < 0 || lines[i].Substring(0, star).Trim().Length > 0) continue;
            types ??= ModelCacheScope.GetCache(Model).GetOptions(ExpansionTrainerTeamRun.TypesTable);
            foreach (var error in ExpansionTrainerTeamRun.FindExtraErrors(lines[i].Substring(star + 1), types)) {
               errors.Add(new(i, star + 1 + error.Start, Math.Max(1, error.Length), SegmentType.Error));
            }
         }
         return errors;
      }
   }
}
