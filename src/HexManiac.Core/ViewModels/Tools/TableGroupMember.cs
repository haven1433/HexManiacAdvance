using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// One entry of a [[TableGroup]]: which table, and which of its fields the group shows (in which order).
   ///
   /// "table"              the whole table (or the first partition of it, if the format contains '|' splitters).
   /// "table|2"            partition 2 of the table (partitions are separated by '|' in the table's format).
   /// "table|name,class"   only the listed fields, in the listed order, under a single header.
   ///                      Each item is a field name (case insensitive) or a partition number (all the fields of that partition).
   ///                      Fields that do not exist are skipped, so a group still works with an older/newer version of the table.
   /// </summary>
   public record TableGroupMember(string TableName, int Partition, IReadOnlyList<string> Fields) {
      public const char FieldSeparator = ',';

      public static TableGroupMember Parse(string text) {
         var parts = text.Split(ArrayRunSplitterSegment.Separator);
         if (parts.Length != 2) return new(text, 0, Array.Empty<string>());
         var tableName = parts[0];
         var selector = parts[1].Trim();
         if (int.TryParse(selector, out var partition)) return new(tableName, partition, Array.Empty<string>());
         var fields = selector.Split(FieldSeparator).Select(field => field.Trim()).Where(field => field.Length > 0).ToList();
         return new(tableName, 0, fields);
      }

      public bool HasFieldList => Fields.Count > 0;

      public bool ShowsField(string name) => Fields.Any(field => field.Equals(name, StringComparison.OrdinalIgnoreCase));
   }
}
