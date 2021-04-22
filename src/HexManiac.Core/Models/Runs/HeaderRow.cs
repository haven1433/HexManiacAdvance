using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class ColumnHeader {
      public string ColumnTitle { get; }
      public int ByteWidth { get; }
      public ColumnHeader(string title, int byteWidth = 1) => (ColumnTitle, ByteWidth) = (title, byteWidth);
   }

   /// <summary>
   /// Represents a horizontal row of labels.
   /// Each entry is meant to label a column.
   /// </summary>
   public class HeaderRow {
      public IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

      public HeaderRow(ArrayRun source, int byteStart, int length) {
         var headers = new List<ColumnHeader>();
         // we know which 'byte' to start at, but we want to know what 'index' to start at
         // basically, count off each element to figure out how big it is
         int currentByte = 0;
         int startIndex = 0;
         while (currentByte < byteStart) {
            currentByte += source.ElementContent[startIndex % source.ElementContent.Count].Length;
            startIndex++;
         }
         int initialPartialSegmentLength = currentByte - byteStart;
         if (initialPartialSegmentLength > 0) startIndex--;
         currentByte = 0;
         for (int i = 0; currentByte < length; i++) {
            var segment = source.ElementContent[(startIndex + i) % source.ElementContent.Count];
            if (segment.Length == 0) continue;
            if (initialPartialSegmentLength != 0) {
               headers.Add(new ColumnHeader(segment.Name, initialPartialSegmentLength));
               currentByte += initialPartialSegmentLength;
            } else {
               headers.Add(new ColumnHeader(segment.Name, segment.Length));
               currentByte += segment.Length;
            }
            initialPartialSegmentLength = 0;
         }
         ColumnHeaders = headers;
      }

      public HeaderRow(int start, int length) {
         while (start < 0) start += length;
         var hex = "0123456789ABCDEF";
         var headers = new ColumnHeader[length];
         for (int i = 0; i < length; i++) headers[i] = new ColumnHeader(hex[(start + i) % 0x10].ToString());
         ColumnHeaders = headers;
      }

      public static IReadOnlyList<HeaderRow> GetDefaultColumnHeaders(int columnCount, int startingDataIndex) {
         if (columnCount > 0x10 && columnCount % 0x10 != 0) return new List<HeaderRow>();
         if (columnCount < 0x10 && 0x10 % columnCount != 0) return new List<HeaderRow>();
         if (columnCount >= 0x10) return new[] { new HeaderRow(startingDataIndex, columnCount) };
         return (0x10 / columnCount).Range().Select(i => new HeaderRow(columnCount * i + startingDataIndex, columnCount)).ToList();
      }
   }
}
