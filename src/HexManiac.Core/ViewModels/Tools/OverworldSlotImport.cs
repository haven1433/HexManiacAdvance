using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// One overworld sprite slot as the table tool shows it: a row of the graphics-info table (graphics.overworld.sprites and friends)
   /// whose 'sprites' pointer leads to a frame list (<see cref="OverworldSpriteListRun"/>), and whose 'paletteid' names a row of the overworld palette table.
   /// Slots can share data: the 636 spare slots of CUBE all point at the same frames and the same palette as the Pikachu until the user gives them their own.
   /// </summary>
   public class OverworldSlot {
      public IDataModel Model { get; set; }
      /// <summary>The address of the 'sprites' pointer inside the graphics-info row.</summary>
      public int SourceAddress { get; set; }
      public ITableRun InfoTable { get; set; }
      public int InfoIndex { get; set; }
      public ArrayRunPointerSegment ListSegment { get; set; }
      public string PaletteIdField { get; set; }
      public int PaletteIdAddress { get; set; }
      public int PaletteId { get; set; }
      public OverworldSpriteListRun List { get; set; }

      /// <summary>The overworld palette table, or null when this slot's palette is not stored in it (then a new palette can't be added).</summary>
      public ITableRun PaletteTable { get; set; }
      public string PaletteKeyField { get; set; }

      /// <summary>The row's reflection palette tag (0x11FF means none: the game makes the reflection from the sprite's own colors), or -1 if the table has no such field.</summary>
      public int ReflectionTag { get; set; } = -1;
      public const int NoPaletteTag = 0x11FF;

      /// <summary>How many other graphics-info rows point at the same frame list.</summary>
      public int OtherRowsOnList { get; set; }
      /// <summary>True when some frame of the list is also pointed at from somewhere outside the list.</summary>
      public bool FramesUsedElsewhere { get; set; }
      /// <summary>How many other graphics-info rows use the same palette id.</summary>
      public int OtherRowsOnPalette { get; set; }

      public bool SharesPictures => OtherRowsOnList > 0 || FramesUsedElsewhere;
      public bool SharesPalette => OtherRowsOnPalette > 0;
      public bool CanCreatePalette => PaletteTable != null;
      /// <summary>Is there anything another sprite could lose by importing into this slot the usual way?</summary>
      public bool NeedsChoice => SharesPictures || (SharesPalette && CanCreatePalette);
   }

   public class OverworldSlotImportResult {
      public bool Success { get; set; }
      public string Error { get; set; }
      public int ListAddress { get; set; } = -1;
      public int PaletteAddress { get; set; } = -1;
      public int PaletteId { get; set; }
      public bool CopiedPictures { get; set; }
      public bool CreatedPalette { get; set; }
   }

   /// <summary>
   /// Copy-on-write and "create a new palette" for importing a picture into an overworld sprite slot.
   /// Everything is done with the change token that is given, so one undo takes it all back.
   /// </summary>
   public static class OverworldSlotImport {
      /// <summary>New palette tags start here: 0x1200 and 0x1201 are the weather sprites' palettes, and the vanilla object event tags are all below.</summary>
      public const int FirstNewTag = 0x1210;
      public const int LastNewTag = 0x7FFF;
      public const int ListEntryLength = 8;

      /// <summary>
      /// Describe the slot whose 'sprites' pointer is at the given address. Returns null if the pointer is not part of an overworld graphics-info row.
      /// </summary>
      public static OverworldSlot Find(IDataModel model, int sourceAddress) {
         if (model == null || sourceAddress < 0 || sourceAddress + 4 > model.Count) return null;
         if (model.GetNextRun(sourceAddress) is not ITableRun info || info.Start > sourceAddress) return null;
         var offset = info.ConvertByteOffsetToArrayOffset(sourceAddress);
         if (offset.ElementIndex < 0 || offset.ElementIndex >= info.ElementCount || offset.SegmentOffset != 0) return null;
         if (info.ElementContent[offset.SegmentIndex] is not ArrayRunPointerSegment listSegment || !listSegment.InnerFormat.StartsWith("`osl")) return null;
         var idSegment = info.ElementContent.FirstOrDefault(seg => seg.Name == "paletteid" && seg.Length == 2);
         if (idSegment == null) return null;

         var destination = model.ReadPointer(sourceAddress);
         if (destination < 0 || destination >= model.Count) return null;
         if (model.GetNextRun(destination) is not OverworldSpriteListRun list || list.Start != destination) return null;

         var elementStart = info.Start + info.ElementLength * offset.ElementIndex;
         var idAddress = elementStart + info.ElementContent.Until(seg => seg == idSegment).Sum(seg => seg.Length);
         var slot = new OverworldSlot {
            Model = model,
            SourceAddress = sourceAddress,
            InfoTable = info,
            InfoIndex = offset.ElementIndex,
            ListSegment = listSegment,
            PaletteIdField = idSegment.Name,
            PaletteIdAddress = idAddress,
            PaletteId = model.ReadMultiByteValue(idAddress, 2),
            List = list,
         };

         // which palette table does the id point into? Only the main overworld palette table can grow.
         ParseHint(listSegment.InnerFormat, out var tableNames, out var keyField);
         if (tableNames.Contains(HardcodeTablesModel.OverworldPalettes)) {
            var paletteTable = model.GetTable(HardcodeTablesModel.OverworldPalettes);
            var key = paletteTable?.ElementContent.FirstOrDefault(seg => seg.Name == keyField && seg.Length == 2);
            if (paletteTable != null && key != null && paletteTable.ElementContent.Any(seg => seg is ArrayRunPointerSegment)) {
               // (a field effect that finds its palette in a table of its own is not in this table: its palette can't be swapped for a new row of it)
               var keyOffset = paletteTable.ElementContent.Until(seg => seg == key).Sum(seg => seg.Length);
               for (int i = 0; i < paletteTable.ElementCount; i++) {
                  if (model.ReadMultiByteValue(paletteTable.Start + i * paletteTable.ElementLength + keyOffset, 2) != slot.PaletteId) continue;
                  slot.PaletteTable = paletteTable;
                  slot.PaletteKeyField = keyField;
                  break;
               }
            }
         }
         foreach (var field in new[] { "reflectionPaletteTag", "secondid" }) {
            if (TryReadRowValue(model, sourceAddress, field, out var reflection)) { slot.ReflectionTag = reflection; break; }
         }

         Measure(model, slot);
         return slot;
      }

      private static void ParseHint(string innerFormat, out string[] tableNames, out string keyField) {
         // (a plain `osl` has no hint: the frames then look for their palette in the overworld palette table, by 'id')
         tableNames = new[] { HardcodeTablesModel.OverworldPalettes };
         keyField = "id";
         var bar = innerFormat.IndexOf('|');
         if (bar < 0) return;
         var hint = innerFormat.Substring(bar + 1).TrimEnd('`');
         var colon = hint.IndexOf(':');
         tableNames = (colon < 0 ? hint : hint.Substring(0, colon)).Split(',');
         if (colon >= 0) {
            var key = hint.Substring(colon + 1).Split('=')[0];
            if (!string.IsNullOrEmpty(key)) keyField = key;
         }
      }

      private static void Measure(IDataModel model, OverworldSlot slot) {
         var list = slot.List;
         slot.OtherRowsOnList = Math.Max(0, (list.PointerSources?.Count ?? 0) - 1);

         for (int i = 0; i < list.ElementCount; i++) {
            var frame = model.ReadPointer(list.Start + i * ListEntryLength);
            if (frame < 0 || frame >= model.Count) continue;
            var run = model.GetNextRun(frame);
            if (run.Start != frame || run.PointerSources == null) continue;
            if (run.PointerSources.Any(source => source < list.Start || source >= list.Start + list.Length)) { slot.FramesUsedElsewhere = true; break; }
         }

         var count = 0;
         foreach (var otherList in model.All<OverworldSpriteListRun>()) {
            if (otherList.PointerSources == null) continue;
            foreach (var source in otherList.PointerSources) {
               if (source == slot.SourceAddress) continue;
               if (TryReadRowValue(model, source, "paletteid", out var id) && id == slot.PaletteId) count++;
            }
         }
         slot.OtherRowsOnPalette = count;
      }

      private static bool TryReadRowValue(IDataModel model, int pointerAddress, string fieldName, out int value) {
         value = 0;
         if (model.GetNextRun(pointerAddress) is not ITableRun table || table.Start > pointerAddress) return false;
         var offset = table.ConvertByteOffsetToArrayOffset(pointerAddress);
         if (offset.ElementIndex < 0 || offset.ElementIndex >= table.ElementCount) return false;
         var segment = table.ElementContent.FirstOrDefault(seg => seg.Name == fieldName && seg.Length == 2);
         if (segment == null) return false;
         var address = table.Start + table.ElementLength * offset.ElementIndex + table.ElementContent.Until(seg => seg == segment).Sum(seg => seg.Length);
         value = model.ReadMultiByteValue(address, 2);
         return true;
      }

      /// <summary>
      /// The first palette tag from <see cref="FirstNewTag"/> up that no palette-table row and no graphics-info row (as palette or reflection palette) uses.
      /// Returns -1 if every tag is taken.
      /// </summary>
      public static int FindFreeTag(OverworldSlot slot) {
         var model = slot.Model;
         var used = new HashSet<int>();
         if (slot.PaletteTable != null) {
            var table = slot.PaletteTable;
            var key = table.ElementContent.First(seg => seg.Name == slot.PaletteKeyField && seg.Length == 2);
            var keyOffset = table.ElementContent.Until(seg => seg == key).Sum(seg => seg.Length);
            for (int i = 0; i < table.ElementCount; i++) used.Add(model.ReadMultiByteValue(table.Start + i * table.ElementLength + keyOffset, 2));
         }
         foreach (var list in model.All<OverworldSpriteListRun>()) {
            if (list.PointerSources == null) continue;
            foreach (var source in list.PointerSources) {
               foreach (var field in new[] { "paletteid", "reflectionPaletteTag", "secondid" }) {
                  if (TryReadRowValue(model, source, field, out var tag)) used.Add(tag);
               }
            }
         }
         used.Add(slot.PaletteId);
         for (int tag = FirstNewTag; tag <= LastNewTag; tag++) {
            if (!used.Contains(tag)) return tag;
         }
         return -1;
      }

      /// <summary>
      /// Make this slot's picture data its own (if it is shared) and optionally give it a brand-new palette.
      /// Nothing that other slots use is written to.
      /// </summary>
      public static OverworldSlotImportResult Prepare(OverworldSlot slot, ModelDelta token, bool createNewPalette) {
         var model = slot.Model;
         var result = new OverworldSlotImportResult { PaletteId = slot.PaletteId };
         if (createNewPalette && !slot.CanCreatePalette) { result.Error = "This sprite's palette is not stored in the overworld palette table, so a new palette can't be added."; return result; }
         var newTag = -1;
         if (createNewPalette) {
            newTag = FindFreeTag(slot);
            if (newTag < 0) { result.Error = "There is no free palette tag left."; return result; }
         }

         // 1. the pictures: a slot that shares them gets copies, and only its own pointer is moved to the copies
         var listAddress = slot.List.Start;
         if (slot.SharesPictures) {
            listAddress = CopyList(slot, token);
            if (listAddress < 0) { result.Error = "There is no free space in the ROM for a copy of the pictures."; return result; }
            result.CopiedPictures = true;
         }
         result.ListAddress = listAddress;

         // 2. the palette: a new row in the palette table, filled with a copy of the current colors
         if (createNewPalette) {
            var paletteAddress = AddPalette(slot, token, newTag, out var error);
            if (paletteAddress < 0) { result.Error = error; return result; }
            var info = new ModelArrayElement(model, slot.InfoTable.Start, slot.InfoIndex, () => token, slot.InfoTable);
            info.SetValue(slot.PaletteIdField, newTag); // (also tells the frames which palette they use)
            result.CreatedPalette = true;
            result.PaletteAddress = paletteAddress;
            result.PaletteId = newTag;
         }

         result.Success = true;
         return result;
      }

      private static int Allocate(IDataModel model, ModelDelta token, int length) {
         var address = model.FindFreeSpace(model.FreeSpaceStart, length);
         if (address < 0) {
            address = (model.Count + 3) / 4 * 4;
            if (address + length + 0x10 > 0x2000000) return -1;
            model.ExpandData(token, address + length + 0x10);
         }
         return address;
      }

      /// <summary>
      /// Copies the frame list and every frame to new space (the frames behind the list entries become separate pictures, one per entry) and points the slot at the copy.
      /// </summary>
      private static int CopyList(OverworldSlot slot, ModelDelta token) {
         var model = slot.Model;
         var list = slot.List;
         var count = list.ElementCount;
         var frameLength = new int[count];
         var frameAddress = new int[count];
         var total = list.Length + 8; // (the gap after the list keeps it from growing into the first frame)
         for (int i = 0; i < count; i++) {
            var entry = list.Start + i * ListEntryLength;
            var destination = model.ReadPointer(entry);
            if (destination < 0 || destination >= model.Count) continue;
            var length = model.GetNextRun(destination) is ISpriteRun sprite && sprite.Start == destination ? sprite.Length : model.ReadMultiByteValue(entry + 4, 2);
            if (length <= 0 || destination + length > model.Count) continue;
            frameLength[i] = length;
            total += (length + 3) / 4 * 4;
         }

         var start = Allocate(model, token, total);
         if (start < 0) return -1;
         for (int i = 0; i < total; i++) token.ChangeData(model, start + i, 0xFF);
         var next = start + list.Length + 8;
         for (int i = 0; i < count; i++) {
            var entry = list.Start + i * ListEntryLength;
            var newEntry = start + i * ListEntryLength;
            for (int b = 0; b < ListEntryLength; b++) token.ChangeData(model, newEntry + b, model[entry + b]);
            if (frameLength[i] == 0) continue;
            var destination = model.ReadPointer(entry);
            for (int b = 0; b < frameLength[i]; b++) token.ChangeData(model, next + b, model[destination + b]);
            model.WritePointer(token, newEntry, next);
            frameAddress[i] = next;
            next += (frameLength[i] + 3) / 4 * 4;
         }

         // the slot's pointer moves to the copy: the format of the copy (list and frames) is made from the segment, like when the user types a pointer
         model.UpdateArrayPointer(token, slot.ListSegment, slot.InfoTable.ElementContent, slot.InfoIndex, slot.SourceAddress, start, true);
         if (model.GetNextRun(start) is not OverworldSpriteListRun) return start;
         for (int i = 0; i < count; i++) {
            if (frameAddress[i] == 0) continue;
            if (model.GetNextRun(frameAddress[i]) is ISpriteRun frame && frame.Start == frameAddress[i]) continue;
            // (should not happen: the list registers its frames) make sure the picture is known as a sprite anyway
            if (model.GetNextRun(slot.SourceAddress) != null && model.GetNextRun(start) is OverworldSpriteListRun newList) {
               model.ObserveRunWritten(token, new SpriteRun(model, frameAddress[i], newList.SpriteFormat, SortedSpan.One(start + i * ListEntryLength)));
            }
         }
         return start;
      }

      private static bool IsEmptyRow(IDataModel model, ITableRun table, int index) {
         var row = table.Start + index * table.ElementLength;
         return model.ReadMultiByteValue(row, 4) == 0; // (no palette pointer: this is the end marker; its id is 0 or 0x11FF depending on the game)
      }

      /// <summary>
      /// Adds a row to the palette table (in front of the end marker), writes a copy of the slot's current palette to new space and points the row at it.
      /// </summary>
      private static int AddPalette(OverworldSlot slot, ModelDelta token, int tag, out string error) {
         error = null;
         var model = slot.Model;
         var table = slot.PaletteTable;
         var oldCount = table.ElementCount;
         var endMarkerInside = oldCount > 0 && IsEmptyRow(model, table, oldCount - 1);

         // the colors to start from
         var palettePointer = FindPaletteData(slot);
         var colors = new byte[32];
         if (palettePointer >= 0 && palettePointer + 32 <= model.Count) for (int i = 0; i < 32; i++) colors[i] = model[palettePointer + i];

         var paletteAddress = Allocate(model, token, 32);
         if (paletteAddress < 0) { error = "There is no free space in the ROM for a new palette."; return -1; }
         for (int i = 0; i < 32; i++) token.ChangeData(model, paletteAddress + i, colors[i]);

         table = model.RelocateForExpansion(token, table, table.Length + table.ElementLength);
         table = table.Append(token, 1);
         model.ObserveRunWritten(token, table);
         var newIndex = endMarkerInside ? table.ElementCount - 2 : table.ElementCount - 1;
         var row = new ModelArrayElement(model, table.Start, newIndex, () => token, table);
         var palField = table.ElementContent.First(seg => seg is ArrayRunPointerSegment);
         row.SetAddress(palField.Name, paletteAddress);
         model.WriteMultiByteValue(table.Start + newIndex * table.ElementLength + table.ElementContent.Until(seg => seg.Name == slot.PaletteKeyField).Sum(seg => seg.Length), 2, token, tag);
         slot.PaletteTable = table;
         return paletteAddress;
      }

      private static int FindPaletteData(OverworldSlot slot) {
         var model = slot.Model;
         var table = slot.PaletteTable;
         var key = table.ElementContent.First(seg => seg.Name == slot.PaletteKeyField && seg.Length == 2);
         var keyOffset = table.ElementContent.Until(seg => seg == key).Sum(seg => seg.Length);
         var palField = table.ElementContent.First(seg => seg is ArrayRunPointerSegment);
         var palOffset = table.ElementContent.Until(seg => seg == palField).Sum(seg => seg.Length);
         for (int i = 0; i < table.ElementCount; i++) {
            var row = table.Start + i * table.ElementLength;
            if (model.ReadMultiByteValue(row + keyOffset, 2) != slot.PaletteId) continue;
            return model.ReadPointer(row + palOffset);
         }
         return -1;
      }
   }
}
