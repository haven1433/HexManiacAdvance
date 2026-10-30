using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>How the game gets the colours of a reflection (the picture of a sprite in water, ice and on a high bridge).</summary>
   public enum ReflectionMode {
      /// <summary>The game loads palettes that are stored in the ROM (the unedited Emerald, FireRed, Ruby... do this).</summary>
      Stored,
      /// <summary>The game works the colours out from the sprite's own palette while it runs (pokeemerald-expansion 1.17 does this): the stored reflection palettes are not read.</summary>
      Computed,
   }

   public enum ReflectionPaletteKind {
      /// <summary>A palette of the overworld palette table that is used (or kept) as a reflection.</summary>
      Stored,
      /// <summary>Not stored anywhere: the game adds blue to the sprite's colours (water, puddles).</summary>
      Water,
      /// <summary>Not stored anywhere: the game tints the sprite's colours (ice).</summary>
      Ice,
      /// <summary>Not stored anywhere: one flat colour (the reflection under a high bridge).</summary>
      Bridge,
   }

   /// <summary>The colours the game makes by itself (pokeemerald-expansion, field_effect_helpers.c: ApplyPondFilter, ApplyIceFilter and the high bridge fill). The channels are the GBA's: 0 to 31.</summary>
   public static class ReflectionFilters {
      /// <summary>The flat colour the game fills the reflection palette with under a high bridge.</summary>
      public const ushort HighBridgeColor = 0x55C9;

      /// <summary>Water and puddles: colour 0 stays, every other colour gets 10 more blue.</summary>
      public static ushort[] Water(IReadOnlyList<ushort> palette) => Apply(palette, (r, g, b) => (r, g, Math.Min(31, b + 10)));

      /// <summary>Ice: colour 0 stays, every other colour loses 5 red and gets 3 more green and 16 more blue.</summary>
      public static ushort[] Ice(IReadOnlyList<ushort> palette) => Apply(palette, (r, g, b) => (Math.Max(0, r - 5), Math.Min(31, g + 3), Math.Min(31, b + 16)));

      /// <summary>Under a high bridge: all 16 colours are the same.</summary>
      public static ushort[] Bridge() => Enumerable.Repeat(HighBridgeColor, 16).ToArray();

      private static ushort[] Apply(IReadOnlyList<ushort> palette, Func<int, int, int, (int r, int g, int b)> filter) {
         var result = new ushort[palette.Count];
         for (int i = 0; i < result.Length; i++) {
            if (i == 0) { result[i] = palette[i]; continue; }
            var (r, g, b) = GbaColor.Unpack(palette[i]);
            var (nr, ng, nb) = filter(r, g, b);
            result[i] = GbaColor.Pack(nr, ng, nb);
         }
         return result;
      }

      /// <summary>The same filter for a palette in the order a picture draws it (see <see cref="GbaColor.ToDisplay"/>).</summary>
      public static short[] ToDisplay(IReadOnlyList<ushort> palette) => palette.Select(GbaColor.ToDisplay).ToArray();

      public static ushort[] FromDisplay(IReadOnlyList<short> palette) => palette.Select(GbaColor.FromDisplay).ToArray();
   }

   /// <summary>The names of the object palette tags (the numbers that identify the entries of the overworld palette table).</summary>
   public static class ObjectPaletteTags {
      /// <summary>The tag that means 'no palette' (OBJ_EVENT_PAL_TAG_NONE).</summary>
      public const int None = 0x11FF;

      /// <summary>A metadata list called like this has the tag as its index and the name (OBJ_EVENT_PAL_TAG_BRENDAN...) as its content.</summary>
      public const string ListName = "objectpalettetags";

      // the tags of the unedited Emerald: pokeemerald-expansion keeps these numbers, and the CUBE metadata names the rest with the list above
      private static readonly Dictionary<int, string> Emerald = new() {
         [0x1100] = "Brendan", [0x1101] = "Brendan reflection", [0x1102] = "Bridge reflection",
         [0x1103] = "NPC 1", [0x1104] = "NPC 2", [0x1105] = "NPC 3", [0x1106] = "NPC 4",
         [0x1107] = "NPC 1 reflection", [0x1108] = "NPC 2 reflection", [0x1109] = "NPC 3 reflection", [0x110A] = "NPC 4 reflection",
         [0x110B] = "Quinty Plump", [0x110C] = "Quinty Plump reflection", [0x110D] = "Truck", [0x110E] = "Vigoroth", [0x110F] = "Zigzagoon",
         [0x1110] = "May", [0x1111] = "May reflection", [0x1112] = "Moving box", [0x1113] = "Cable car", [0x1114] = "SS Tidal",
         [0x1115] = "Player underwater", [0x1116] = "Kyogre", [0x1117] = "Kyogre reflection", [0x1118] = "Groudon", [0x1119] = "Groudon reflection",
         [0x111A] = "Unused", [0x111B] = "Submarine shadow", [0x111C] = "Poochyena", [0x111D] = "Red Leaf", [0x111E] = "Deoxys",
         [0x111F] = "Birth Island stone", [0x1120] = "Ho-Oh", [0x1121] = "Lugia", [0x1122] = "Ruby/Sapphire Brendan", [0x1123] = "Ruby/Sapphire May",
      };

      /// <summary>The name of a tag, or null if nothing in the ROM's metadata (or the unedited Emerald) names it.</summary>
      public static string Name(IDataModel model, int tag) {
         if (model.TryGetList(ListName, out var list) && list != null) {
            if (tag >= 0 && tag < list.Count && !string.IsNullOrWhiteSpace(list[tag])) return Humanize(list[tag]);
            return null;
         }
         if (!IsEmerald(model)) return null;
         return Emerald.TryGetValue(tag, out var name) ? name : null;
      }

      /// <summary>True for a tag whose name says it is a reflection.</summary>
      public static bool IsReflectionName(string name) => name != null && name.EndsWith("reflection", StringComparison.OrdinalIgnoreCase);

      private static bool IsEmerald(IDataModel model) {
         var code = model.GetGameCode();
         return code.StartsWith("BPEE");
      }

      /// <summary>OBJ_EVENT_PAL_TAG_QUINTY_PLUMP_REFLECTION without its prefix becomes "Quinty Plump reflection".</summary>
      public static string Humanize(string constant) {
         constant = (constant ?? string.Empty).Trim();
         const string prefix = "OBJ_EVENT_PAL_TAG_";
         if (constant.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) constant = constant.Substring(prefix.Length);
         var words = constant.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
         for (int i = 0; i < words.Length; i++) {
            var word = words[i];
            if (word.Equals("reflection", StringComparison.OrdinalIgnoreCase)) words[i] = "reflection";
            else if (word.Equals("npc", StringComparison.OrdinalIgnoreCase) || word.Equals("rs", StringComparison.OrdinalIgnoreCase) || word.Equals("ss", StringComparison.OrdinalIgnoreCase)) words[i] = word.ToUpper();
            else words[i] = char.ToUpper(word[0]) + word.Substring(1).ToLower();
         }
         return string.Join(" ", words);
      }
   }

   /// <summary>One entry of the overworld sprite table (graphics.overworld.sprites): what decides which reflection palette it gets.</summary>
   public class ReflectionSprite {
      public int Index { get; }
      public string Name { get; }
      /// <summary>The tag of its own palette (paletteid).</summary>
      public int Tag { get; }
      /// <summary>The palette slot: 0 is the player, 2 to 5 the four NPC palettes, 10 the special palette.</summary>
      public int Slot { get; }
      /// <summary>The second tag of the graphics info (the palette for a high bridge in the unedited games, 'reflectionPaletteTag' in the expansion), or -1.</summary>
      public int SecondTag { get; }
      /// <summary>The address of its graphics info.</summary>
      public int InfoAddress { get; }

      /// <summary>The stored palette that gives its water and ice reflection (an entry of the palette table), or -1 if it has none of its own (it reuses its own colours).</summary>
      public int ReflectionTag { get; internal set; } = -1;
      /// <summary>The stored palette for standing on a high bridge, or -1.</summary>
      public int BridgeTag { get; internal set; } = -1;

      internal ReflectionSprite(int index, string name, int tag, int slot, int secondTag, int infoAddress) {
         (Index, Name, Tag, Slot, SecondTag, InfoAddress) = (index, name, tag, slot, secondTag, infoAddress);
      }
   }

   /// <summary>One thing in the list of reflection palettes: a stored palette, or one of the colour rules the game applies by itself.</summary>
   public class ReflectionPalette {
      /// <summary>Identifies the entry even when the data around it moves: "tag:1101" for a stored palette, "water" / "ice" / "bridge" for the others.</summary>
      public string Key { get; }
      public ReflectionPaletteKind Kind { get; }
      /// <summary>The tag in the overworld palette table, or -1 for something the game computes.</summary>
      public int Tag { get; }
      public string Name { get; }
      /// <summary>Which row of the overworld palette table it is (-1 for the computed ones).</summary>
      public int RowIndex { get; }
      /// <summary>The address of the pointer to the colours (the 'pal' cell of its row), or -1.</summary>
      public int PointerAddress { get; }
      /// <summary>The address of the 16 colours, or -1.</summary>
      public int Address { get; }

      public bool IsStored => Kind == ReflectionPaletteKind.Stored;

      internal ReflectionPalette(string key, ReflectionPaletteKind kind, int tag, string name, int rowIndex, int pointerAddress, int address) {
         (Key, Kind, Tag, Name, RowIndex, PointerAddress, Address) = (key, kind, tag, name, rowIndex, pointerAddress, address);
      }
   }

   /// <summary>
   /// Everything the 'Reflection Palettes' editor needs to know about a ROM, found through its metadata: the overworld palette table, the overworld sprite table, and (in the unedited games) the two tables
   /// that pair a sprite's palette with its reflection palette. Nothing is hard-coded to an address. Reading is done once per version of the data (see <see cref="Get"/>).
   /// </summary>
   public class ReflectionCatalog {
      private const string CacheKey = "reflection-catalog";
      /// <summary>The unedited games pair a palette with its reflection in two tables: for the player's palettes (slot 0) and for the 'special' palettes (slot 10).</summary>
      public const string PlayerPairsTable = "graphics.overworld.reflection.palettes.player";
      public const string SpecialPairsTable = "graphics.overworld.reflection.palettes.slot10";
      /// <summary>The expansion's list of which tag goes into which palette slot (one list per kind of map).</summary>
      public const string TagSetsTable = "graphics.overworld.reflection.tagsets";
      /// <summary>A metadata list with the single word 'filtered' says that the ROM's game computes its reflections. 'stored' says it loads the palettes.</summary>
      public const string ModeListName = "reflectionmode";
      public const int PlayerSlot = 0, FirstNpcSlot = 2, LastNpcSlot = 5, SpecialSlot = 10;

      private readonly IDataModel model;
      private readonly List<(int index, int tag, int pointerAddress, int address)> rows = new();
      private readonly Dictionary<int, int> rowOfTag = new();
      private readonly List<ReflectionSprite> sprites = new();
      private readonly List<ReflectionPalette> stored = new();
      private readonly List<ReflectionPalette> computed = new();

      public ReflectionMode Mode { get; private set; } = ReflectionMode.Stored;
      public IReadOnlyList<ReflectionSprite> Sprites => sprites;
      /// <summary>The stored palettes that are reflections, in the order of the overworld palette table.</summary>
      public IReadOnlyList<ReflectionPalette> Stored => stored;
      /// <summary>The colour rules of a game that computes its reflections (empty for a game that loads palettes).</summary>
      public IReadOnlyList<ReflectionPalette> Computed => computed;
      public int PaletteRowCount => rows.Count;
      public bool HasPairTables { get; private set; }

      private ReflectionCatalog(IDataModel model) => this.model = model;

      /// <summary>The catalog for the current data of the model (read again after the data changes).</summary>
      public static ReflectionCatalog Get(IDataModel model) => model.CurrentCacheScope.GetOrAdd(CacheKey, () => Build(model));

      /// <summary>
      /// Cheap test used by the menu: the ROM has an overworld palette table and an overworld sprite table, and either the tables that pair the palettes
      /// or at least one palette that is named as a reflection. (Nothing is read from the sprites.)
      /// </summary>
      public static bool IsSupported(IDataModel model) {
         if (model == null) return false;
         try {
            var palettes = model.GetTable(HardcodeTablesModel.OverworldPalettes);
            if (palettes == null || model.GetTable(HardcodeTablesModel.OverworldSprites) == null) return false;
            if (model.GetTable(PlayerPairsTable) != null || model.GetTable(SpecialPairsTable) != null) return true;
            var idOffset = OffsetOf(palettes, "id", 4);
            for (int i = 0; i < palettes.ElementCount; i++) {
               var tag = Read16(model, palettes.Start + i * palettes.ElementLength + idOffset);
               if (tag >= 0 && ObjectPaletteTags.IsReflectionName(ObjectPaletteTags.Name(model, tag))) return true;
            }
         } catch (Exception) {
            return false;
         }
         return false;
      }

      public static ReflectionCatalog Build(IDataModel model) {
         var catalog = new ReflectionCatalog(model);
         try {
            catalog.ReadPaletteTable();
            catalog.ReadSprites();
            catalog.Pair();
            catalog.FindPalettes();
         } catch (Exception) {
            // a table that can't be read leaves the catalog as far as it got: the tab says so
         }
         return catalog;
      }

      #region Reading the tables

      private static int OffsetOf(ITableRun table, string name, int fallback) {
         var offset = 0;
         foreach (var segment in table.ElementContent) {
            if (string.Equals(segment.Name, name, StringComparison.OrdinalIgnoreCase)) return offset;
            offset += segment.Length;
         }
         return fallback;
      }

      private static int Read16(IDataModel model, int address) => address < 0 || address + 2 > model.Count ? -1 : model.ReadMultiByteValue(address, 2);

      private void ReadPaletteTable() {
         var table = model.GetTable(HardcodeTablesModel.OverworldPalettes);
         if (table == null) return;
         var palOffset = OffsetOf(table, "pal", 0);
         var idOffset = OffsetOf(table, "id", 4);
         for (int i = 0; i < table.ElementCount; i++) {
            var start = table.Start + i * table.ElementLength;
            var tag = Read16(model, start + idOffset);
            if (tag < 0) continue;
            var pointerAddress = start + palOffset;
            var address = pointerAddress + 4 <= model.Count ? model.ReadPointer(pointerAddress) : -1;
            rows.Add((i, tag, pointerAddress, address));
            if (!rowOfTag.ContainsKey(tag)) rowOfTag[tag] = rows.Count - 1; // the game uses the first entry with the tag
         }
      }

      private void ReadSprites() {
         var table = model.GetTable(HardcodeTablesModel.OverworldSprites);
         if (table == null) return;
         var names = model.GetOptions(HardcodeTablesModel.OverworldSprites);
         // every graphics info has the same layout: learn it from the first one the metadata has a table for
         int tagOffset = 2, secondOffset = 4, infoOffset = 12;
         for (int i = 0; i < table.ElementCount; i++) {
            var pointer = model.ReadPointer(table.Start + i * table.ElementLength);
            if (pointer < 0 || pointer >= model.Count) continue;
            if (model.GetNextRun(pointer) is not ITableRun inner || inner.Start != pointer) continue;
            tagOffset = OffsetOf(inner, "paletteid", 2);
            secondOffset = OffsetOf(inner, "reflectionPaletteTag", OffsetOf(inner, "secondid", 4));
            infoOffset = OffsetOf(inner, "info", 12);
            break;
         }
         for (int i = 0; i < table.ElementCount; i++) {
            var pointer = model.ReadPointer(table.Start + i * table.ElementLength);
            if (pointer < 0 || pointer + infoOffset + 1 > model.Count) continue;
            var tag = Read16(model, pointer + tagOffset);
            var second = Read16(model, pointer + secondOffset);
            if (tag < 0) continue;
            var slot = model[pointer + infoOffset] & 0xF;
            var name = names != null && i < names.Count && !string.IsNullOrEmpty(names[i]) ? names[i] : EmeraldOverworldNames.Name(model, i, table.ElementCount) ?? $"Sprite {i}";
            sprites.Add(new ReflectionSprite(i, name, tag, slot, second == ObjectPaletteTags.None || second == 0xFFFF ? -1 : second, pointer));
         }
      }

      /// <summary>tag of a palette -> tag of its reflection, from one of the pairing tables of the unedited games.</summary>
      private Dictionary<int, int> ReadPairs(string tableName) {
         var result = new Dictionary<int, int>();
         var table = model.GetTable(tableName);
         if (table == null) return result;
         var tagOffset = OffsetOf(table, "normalPalette", 0);
         var pointerOffset = OffsetOf(table, "pointer", 4);
         for (int i = 0; i < table.ElementCount; i++) {
            var start = table.Start + i * table.ElementLength;
            var tag = Read16(model, start + tagOffset);
            var pointer = start + pointerOffset + 4 <= model.Count ? model.ReadPointer(start + pointerOffset) : -1;
            var reflection = Read16(model, pointer);
            if (tag >= 0 && reflection >= 0 && !result.ContainsKey(tag)) result[tag] = reflection;
         }
         return result;
      }

      /// <summary>The reflection tag of each NPC palette slot (2 to 5) from the expansion's tag sets, or an empty list.</summary>
      private Dictionary<int, int> ReadNpcTagSet() {
         var result = new Dictionary<int, int>();
         var table = model.GetTable(TagSetsTable);
         if (table == null || table.ElementCount == 0) return result;
         var setAddress = model.ReadPointer(table.Start);
         if (setAddress < 0 || setAddress >= model.Count) return result;
         var names = new[] { "npc1Reflection", "npc2Reflection", "npc3Reflection", "npc4Reflection" };
         var inner = model.GetNextRun(setAddress) as ITableRun;
         for (int k = 0; k < 4; k++) {
            var offset = inner != null && inner.Start == setAddress ? OffsetOf(inner, names[k], 2 * (6 + k)) : 2 * (6 + k);
            var tag = Read16(model, setAddress + offset);
            if (tag >= 0) result[FirstNpcSlot + k] = tag;
         }
         return result;
      }

      #endregion

      #region Pairing

      private bool ExistsInTable(int tag) => rowOfTag.ContainsKey(tag);

      private int PartnerByName(int tag) {
         var name = ObjectPaletteTags.Name(model, tag);
         if (name == null || ObjectPaletteTags.IsReflectionName(name)) return -1;
         foreach (var row in rows) {
            var other = ObjectPaletteTags.Name(model, row.tag);
            if (other != null && string.Equals(other, name + " reflection", StringComparison.OrdinalIgnoreCase)) return row.tag;
         }
         return -1;
      }

      private void Pair() {
         var player = ReadPairs(PlayerPairsTable);
         var special = ReadPairs(SpecialPairsTable);
         HasPairTables = player.Count + special.Count > 0;
         var npcSlots = ReadNpcTagSet();
         var listMode = model.TryGetList(ModeListName, out var modeList) && modeList != null && modeList.Count > 0 ? modeList[0] : null;
         if (listMode != null) Mode = listMode.Trim().Equals("filtered", StringComparison.OrdinalIgnoreCase) ? ReflectionMode.Computed : ReflectionMode.Stored;
         else Mode = !HasPairTables && model.GetTable(TagSetsTable) != null ? ReflectionMode.Computed : ReflectionMode.Stored;

         foreach (var sprite in sprites) {
            int reflection = -1;
            if (sprite.Slot == PlayerSlot && player.TryGetValue(sprite.Tag, out var p)) reflection = p;
            else if (sprite.Slot == SpecialSlot && special.TryGetValue(sprite.Tag, out var s)) reflection = s;
            else if (sprite.Slot >= FirstNpcSlot && sprite.Slot <= LastNpcSlot && npcSlots.TryGetValue(sprite.Slot, out var n)) reflection = n;
            else {
               reflection = PartnerByName(sprite.Tag);
               if (reflection < 0 && sprite.Slot >= FirstNpcSlot && sprite.Slot <= LastNpcSlot && sprite.Tag >= 0x1103 && sprite.Tag <= 0x1106) reflection = sprite.Tag + 4;
            }
            if (reflection == sprite.Tag || !ExistsInTable(reflection)) reflection = -1; // a reflection that reuses the sprite's own colours has nothing of its own
            sprite.ReflectionTag = reflection;
            // a game that computes its reflections doesn't read the second tag at all
            sprite.BridgeTag = Mode == ReflectionMode.Stored && sprite.SecondTag >= 0 && sprite.SecondTag != sprite.Tag && ExistsInTable(sprite.SecondTag) ? sprite.SecondTag : -1;
         }
      }

      private void FindPalettes() {
         var tags = new SortedSet<int>();
         foreach (var sprite in sprites) {
            if (sprite.ReflectionTag >= 0) tags.Add(sprite.ReflectionTag);
            if (sprite.BridgeTag >= 0) tags.Add(sprite.BridgeTag);
         }
         foreach (var row in rows) {
            if (ObjectPaletteTags.IsReflectionName(ObjectPaletteTags.Name(model, row.tag))) tags.Add(row.tag);
         }
         foreach (var row in rows) {
            if (!tags.Remove(row.tag)) continue; // the first row of a tag is the one the game finds
            if (rowOfTag[row.tag] != rows.IndexOf(row)) continue;
            var name = ObjectPaletteTags.Name(model, row.tag) ?? $"Reflection palette {row.tag:X4}";
            stored.Add(new ReflectionPalette($"tag:{row.tag:X4}", ReflectionPaletteKind.Stored, row.tag, name, row.index, row.pointerAddress, row.address));
         }
         if (Mode == ReflectionMode.Computed) {
            computed.Add(new ReflectionPalette("water", ReflectionPaletteKind.Water, -1, "Water and puddles", -1, -1, -1));
            computed.Add(new ReflectionPalette("ice", ReflectionPaletteKind.Ice, -1, "Ice", -1, -1, -1));
            computed.Add(new ReflectionPalette("bridge", ReflectionPaletteKind.Bridge, -1, "Under a high bridge", -1, -1, -1));
         }
      }

      #endregion

      #region Using the catalog

      /// <summary>The entry (stored or computed) with this key, or null.</summary>
      public ReflectionPalette Find(string key) => stored.Concat(computed).FirstOrDefault(entry => entry.Key == key);

      /// <summary>The sprites that a stored palette is the reflection of (for a game that computes its reflections: the sprites it would be the reflection of).</summary>
      public IReadOnlyList<ReflectionSprite> SpritesFor(ReflectionPalette palette) {
         if (palette == null || !palette.IsStored) return Array.Empty<ReflectionSprite>();
         return sprites.Where(sprite => sprite.ReflectionTag == palette.Tag || sprite.BridgeTag == palette.Tag).ToList();
      }

      /// <summary>
      /// One sprite for each palette that is drawn with a reflection (the player first), up to the limit: what the colour rules of a game that computes its reflections are shown on.
      /// </summary>
      public IReadOnlyList<ReflectionSprite> Examples(int limit = 24) {
         var seen = new HashSet<int>();
         var result = new List<ReflectionSprite>();
         foreach (var sprite in sprites.OrderBy(sprite => sprite.Slot == PlayerSlot ? 0 : 1).ThenBy(sprite => sprite.Index)) {
            if (sprite.Slot > SpecialSlot || (sprite.ReflectionTag < 0 && sprite.BridgeTag < 0 && !HasOwnPalette(sprite.Tag))) continue;
            if (!HasOwnPalette(sprite.Tag) || !seen.Add(sprite.Tag)) continue;
            result.Add(sprite);
            if (result.Count >= limit) break;
         }
         return result;
      }

      /// <summary>True if the overworld palette table has an entry with the tag.</summary>
      public bool HasOwnPalette(int tag) => rowOfTag.ContainsKey(tag);

      /// <summary>The address of the colours of the first entry with this tag (what the game finds), or -1.</summary>
      public int AddressOfTag(int tag) => rowOfTag.TryGetValue(tag, out var row) ? rows[row].address : -1;

      /// <summary>The address of the pointer to the colours of the first entry with this tag, or -1.</summary>
      public int PointerOfTag(int tag) => rowOfTag.TryGetValue(tag, out var row) ? rows[row].pointerAddress : -1;

      /// <summary>The 16 colours of the first palette-table entry with this tag, as a picture draws them. Null if it can't be read.</summary>
      public IReadOnlyList<short> ColorsOfTag(int tag) => ReadColors(model, AddressOfTag(tag));

      public IReadOnlyList<short> ColorsOf(ReflectionPalette palette) => palette == null || palette.Address < 0 ? null : ReadColors(model, palette.Address);

      /// <summary>16 colours at an address (a palette run if there is one, otherwise the raw bytes), as a picture draws them. Null if the address isn't readable.</summary>
      public static IReadOnlyList<short> ReadColors(IDataModel model, int address) {
         if (address < 0 || address + 32 > model.Count) return null;
         if (model.GetNextRun(address) is IPaletteRun run && run.Start == address && run.PaletteFormat.Bits == 4) {
            var colors = run.GetPalette(model, 0);
            if (colors != null && colors.Count == 16) return colors;
         }
         var raw = new short[16];
         for (int i = 0; i < 16; i++) raw[i] = GbaColor.ToDisplay((ushort)model.ReadMultiByteValue(address + 2 * i, 2));
         return raw;
      }

      #endregion
   }
}
