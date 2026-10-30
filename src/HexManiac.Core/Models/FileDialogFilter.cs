using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Builds the filter string used by the Windows open/save file dialogs.
   /// Kept in Core (instead of next to the dialog code) so the formatting rules can be tested.
   /// </summary>
   /// <remarks>
   /// The Win32 common dialogs want "description|pattern;pattern|description|pattern".
   /// The patterns of one entry are separated by a SEMICOLON. A comma is not a separator: "*.s,*.asm" is read as one pattern that
   /// matches no file at all, which leaves the dialog empty until the user picks "All Files".
   /// </remarks>
   public static class FileDialogFilter {
      public const string AllFiles = "All Files|*.*";

      /// <summary>
      /// Creates "description|*.a;*.b|All Files|*.*" for extensions a and b.
      /// The first entry is the one the dialog starts with, so the requested extensions always come first and "All Files" stays an option after them.
      /// Extensions may be written as 'txt', '.txt' or '*.txt'; empty and duplicate extensions are dropped.
      /// With no extensions the filter is only "All Files". With a null description there is no filter at all (an empty string).
      /// </summary>
      public static string Create(string description, params string[] extensionOptions) {
         if (description == null) return string.Empty;
         var patterns = new List<string>();
         foreach (var option in extensionOptions ?? Array.Empty<string>()) {
            var extension = Normalize(option);
            if (extension.Length == 0) continue;
            var pattern = "*." + extension;
            if (!patterns.Contains(pattern, StringComparer.OrdinalIgnoreCase)) patterns.Add(pattern);
         }
         if (patterns.Count == 0) return AllFiles;
         // '|' separates filter entries: it can't be part of the description
         var label = description.Replace('|', '/');
         return $"{label}|{string.Join(";", patterns)}|{AllFiles}";
      }

      private static string Normalize(string extension) {
         if (extension == null) return string.Empty;
         extension = extension.Trim();
         if (extension.StartsWith("*")) extension = extension.Substring(1);
         if (extension.StartsWith(".")) extension = extension.Substring(1);
         return extension.Trim();
      }
   }
}
