using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class TextEditorViewModel : ViewModelCore {
      public ObservableCollection<string> Keywords { get; } = new();

      public TextEditorViewModel() {
         foreach (var word in new[] { "for", "do", "while", "in", "print" }) {
            Keywords.Add(word);
         }
      }

      private string content = string.Empty;
      public string Content {
         get => content;
         set {
            if (content == value) return;
            content = value;
            UpdateLayers();
            NotifyPropertyChanged();
         }
      }

      public string AccentContent { get; private set; } = string.Empty;
      public string PlainContent { get; private set; } = string.Empty;
      public string ConstantContent { get; private set; } = string.Empty;
      public string CommentContent { get; private set; } = string.Empty;

      private void UpdateLayers() {
         var basic = new MutableString(Content);
         var accent = new MutableString(basic.Length);
         var constants = new MutableString(basic.Length);
         var comments = new MutableString(basic.Length);
         accent.CopyNewlines(basic);
         constants.CopyNewlines(basic);
         comments.CopyNewlines(basic);

         // comments
         while (true) {
            var index = basic.IndexOf("//");
            if (index == -1) break;
            var endIndex = basic.IndexOfCharacter(index, '\n', '\r');
            if (index == -1) break;
            comments.Replace(index, basic, index, endIndex - index);
            basic.Clear(index, endIndex - index);
         }

         // accent
         foreach (var keyword in Keywords) {
            while (true) {
               var index = basic.IndexOfKeyword(keyword);
               if (index == -1) break;
               accent.Replace(index, keyword);
               basic.Clear(index, keyword.Length);
            }
         }

         // constants
         foreach (var keyword in new[] { "true", "false" }) {
            while (true) {
               var index = basic.IndexOfKeyword(keyword);
               if (index == -1) break;
               constants.Replace(index, keyword);
               basic.Clear(index, keyword.Length);
            }
         }
         int start = 0;
         while (true) {
            var (index, length) = basic.IndexOfNumber(start);
            if (index == -1) break;
            start = index;
            constants.Replace(index, basic, index, length);
            basic.Clear(index, length);
         }

         PlainContent = basic.ToString();
         AccentContent = accent.ToString();
         ConstantContent = constants.ToString();
         CommentContent = comments.ToString();
         NotifyPropertyChanged(nameof(PlainContent));
         NotifyPropertyChanged(nameof(AccentContent));
         NotifyPropertyChanged(nameof(ConstantContent));
         NotifyPropertyChanged(nameof(CommentContent));
      }
   }

   public class MutableString {
      private readonly char[] content;

      public int Length => content.Length;

      public MutableString(string initialContent) => content = initialContent.ToCharArray();
      public MutableString(int length) => content = new char[length];

      public override string ToString() => new(content);

      public int IndexOfKeyword(string keyword) {
         if (keyword.Length == 0) return -1;
         for (int i = 0; i < content.Length - keyword.Length + 1; i++) {
            if (i > 0 && char.IsLetterOrDigit(content[i - 1])) continue; // not valid keyword start
            if (i < content.Length - keyword.Length && char.IsLetterOrDigit(content[i + keyword.Length])) continue; // not valid keyword end
            var matchLength = 0;
            for (int j = 0; j < keyword.Length && keyword[j] == content[i + j]; j++) matchLength = j + 1;
            if (matchLength == keyword.Length) return i;
         }
         return -1;
      }

      public int IndexOf(string text) {
         if (text.Length == 0) return -1;
         for (int i = 0; i < content.Length - text.Length + 1; i++) {
            var matchLength = 0;
            for (int j = 0; j < text.Length && content[i + j] == text[j]; j++) matchLength = j + 1;
            if (matchLength == text.Length) return i;
         }
         return -1;
      }

      public int IndexOfCharacter(int start, params char[] options) {
         for (int i = start; i < content.Length; i++) {
            if (options.Contains(content[i])) return i;
         }
         return content.Length;
      }

      public (int, int) IndexOfNumber(int start) {
         for (int i = start; i < content.Length; i++) {
            if (i > 0 && char.IsLetterOrDigit(content[i - 1])) continue;
            if (!char.IsDigit(content[i])) continue;
            int length = 1;
            while (i + length < content.Length && char.IsDigit(content[i + length])) length++;
            if (i + length < content.Length && char.IsLetter(content[i + length])) continue;
            return (i, length);
         }
         return (-1, -1);
      }

      public void Replace(int index, string keyword) {
         for (int i = 0; i < keyword.Length; i++) content[index + i] = keyword[i];
      }

      public void Replace(int index, MutableString source, int start, int length) {
         for (int i = 0; i < length; i++) content[index + i] = source.content[start + i];
      }

      public void Clear(int index, int length) {
         for (int i = 0; i < length; i++) content[index + i] = ' ';
      }

      public void CopyNewlines(MutableString other) {
         for (int i = 0; i < content.Length; i++) {
            if (other.content[i] == '\n' || other.content[i] == '\r') {
               content[i] = other.content[i];
            }
         }
      }
   }
}
