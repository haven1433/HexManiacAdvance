using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public enum TextFormatting { None, Keyword, Constant, Numeric, Comment, Text }
   public interface ITextPreProcessor {
      TextFormatting[] Format(string content);
   }

   public class TextEditorViewModel : ViewModelCore {
      public ObservableCollection<string> Keywords { get; } = new();
      public ObservableCollection<string> Constants { get; } = new();
      public ObservableCollection<TextSegment> ErrorLocations { get; } = new();

      private string commentHeader = string.Empty, multiLineCommentStart = string.Empty, multiLineCommentEnd = string.Empty;
      public string LineCommentHeader { get => commentHeader; set => Set(ref commentHeader, value); }
      public string MultiLineCommentHeader { get => multiLineCommentStart; set => Set(ref multiLineCommentStart, value); }
      public string MultiLineCommentFooter { get => multiLineCommentEnd; set => Set(ref multiLineCommentEnd, value); }

      public bool SyntaxHighlighting { get; }

      public ITextPreProcessor PreFormatter { get; set; }

      public event EventHandler RequestCaretMove;
      public event EventHandler RequestKeyboardFocus;

      public TextEditorViewModel() {
         Keywords.CollectionChanged += (sender, e) => UpdateLayers();
         Constants.CollectionChanged += (sender, e) => UpdateLayers();
      }

      public TextEditorViewModel(bool syntaxHighlighting) {
         Keywords.CollectionChanged += (sender, e) => UpdateLayers();
         Constants.CollectionChanged += (sender, e) => UpdateLayers();
         SyntaxHighlighting = syntaxHighlighting;
      }

      private bool contentChanging;
      private string content = string.Empty;
      public string Content {
         get => content;
         set {
            if (content == value) return;
            var oldContent = content;
            content = value;
            UpdateLayers();
            using (Scope(ref contentChanging, true, back => contentChanging = back)) {
               NotifyPropertyChanged(oldContent, nameof(Content));
            }
         }
      }

      private int caretIndex;
      public int CaretIndex {
         get => caretIndex;
         set {
            if (contentChanging) return;
            Set(ref caretIndex, value, old => RequestCaretMove.Raise(this));
         }
      }

      public string AccentContent { get; private set; } = string.Empty;
      public string PlainContent { get; private set; } = string.Empty;
      public string ConstantContent { get; private set; } = string.Empty;
      public string NumericContent { get; private set; } = string.Empty;
      public string CommentContent { get; private set; } = string.Empty;
      public string TextContent { get; private set; } = string.Empty;

      public void PushCaretUpdate(int index) {
         CaretIndex = index;
         RequestCaretMove.Raise(this);
      }

      public void FocusKeyboard() => RequestKeyboardFocus.Raise(this);

      private void UpdateLayers() {
         if (content.Length == 0) {
            if (PlainContent.Length != 0) {
               PlainContent = AccentContent =
               ConstantContent = NumericContent =
               CommentContent = TextContent = string.Empty;
               NotifyPropertiesChanged(
                  nameof(PlainContent), nameof(AccentContent),
                  nameof(ConstantContent), nameof(NumericContent),
                  nameof(CommentContent), nameof(TextContent));
            }
            return;
         }
         var basic = new MutableString(Content);
         var accent = new MutableString(basic.Length);
         var constants = new MutableString(basic.Length);
         var numeric = new MutableString(basic.Length);
         var comments = new MutableString(basic.Length);
         var text = new MutableString(basic.Length);
         accent.CopyWhitespace(basic);
         constants.CopyWhitespace(basic);
         numeric.CopyWhitespace(basic);
         comments.CopyWhitespace(basic);
         text.CopyWhitespace(basic);

         if (PreFormatter != null) {
            var pre = PreFormatter.Format(Content);
            for (int i = 0; i < pre.Length; i++) {
               if (pre[i] == TextFormatting.None) continue;
               if (pre[i] == TextFormatting.Keyword) accent.Replace(i, basic, i, 1);
               if (pre[i] == TextFormatting.Constant) constants.Replace(i, basic, i, 1);
               if (pre[i] == TextFormatting.Numeric) numeric.Replace(i, basic, i, 1);
               if (pre[i] == TextFormatting.Comment) comments.Replace(i, basic, i, 1);
               if (pre[i] == TextFormatting.Text) text.Replace(i, basic, i, 1);
               basic.Clear(i, 1);
            }
         }

         // comments
         while (!string.IsNullOrEmpty(LineCommentHeader)) {
            var index = basic.IndexOf(LineCommentHeader);
            if (index == -1) break;
            var endIndex = basic.IndexOfCharacter(index, '\n', '\r');
            if (index == -1) break;
            comments.Replace(index, basic, index, endIndex - index);
            basic.Clear(index, endIndex - index);
         }
         while (!string.IsNullOrEmpty(multiLineCommentStart) && !string.IsNullOrEmpty(multiLineCommentEnd)) {
            var index = basic.IndexOf(multiLineCommentStart);
            if (index == -1) break;
            var endIndex = basic.IndexOf(multiLineCommentEnd, index + multiLineCommentStart.Length);
            if (endIndex == -1) endIndex = basic.Length;
            else endIndex += multiLineCommentEnd.Length;
            comments.Replace(index, basic, index, endIndex - index);
            basic.Clear(index, endIndex - index);
         }

         var check = basic.GetPossibleKeywordStartPoints().ToList();
         for (int i = 0; i < check.Count; i++) {
            bool match = false;

            // keywords
            for (int j = 0; j < Keywords.Count; j++) {
               if (check[i].length != Keywords[j].Length) continue;
               if (basic.Match(Keywords[j], check[i].start)) {
                  accent.Replace(check[i].start, Keywords[j]);
                  basic.Clear(check[i].start, Keywords[j].Length);
                  match = true;
                  break;
               }
            }
            if (match) continue;

            // constants
            for (int j = 0; j < Constants.Count; j++) {
               if (check[i].length != Constants[j].Length) continue;
               if (basic.Match(Constants[j], check[i].start)) {
                  constants.Replace(check[i].start, Constants[j]);
                  basic.Clear(check[i].start, Constants[j].Length);
                  break;
               }
            }
         }

         // numeric
         if (SyntaxHighlighting) {
            int start = 0;
            while (true) {
               var (index, length) = basic.IndexOfNumber(start);
               if (index == -1) break;
               start = index;
               numeric.Replace(index, basic, index, length);
               basic.Clear(index, length);
            }
         }

         PlainContent = basic.ToString();
         AccentContent = accent.ToString();
         ConstantContent = constants.ToString();
         NumericContent = numeric.ToString();
         CommentContent = comments.ToString();
         TextContent = text.ToString();
         NotifyPropertiesChanged(
            nameof(PlainContent),
            nameof(AccentContent),
            nameof(ConstantContent),
            nameof(NumericContent),
            nameof(CommentContent),
            nameof(TextContent));
      }
   }

   public enum SegmentType { None, Warning, Error }
   public record TextSegment(int Line, int Start, int Length, SegmentType Type) : INotifyPropertyChanged {
      public event PropertyChangedEventHandler? PropertyChanged;
   }

   public class MutableString {
      private readonly char[] content;

      public int Length => content.Length;

      public MutableString(string initialContent) => content = initialContent.ToCharArray();
      public MutableString(int length) => content = new char[length];

      public override string ToString() => new(content);

      public int IndexOfKeyword(string keyword, int start) {
         if (keyword.Length == 0) return -1;
         for (int i = start; i < content.Length - keyword.Length + 1; i++) {
            if (i > 0 && char.IsLetterOrDigit(content[i - 1])) continue; // not valid keyword start
            if (i < content.Length - keyword.Length && char.IsLetterOrDigit(content[i + keyword.Length])) continue; // not valid keyword end
            var matchLength = 0;
            for (int j = 0; j < keyword.Length && keyword[j] == content[i + j]; j++) matchLength = j + 1;
            if (matchLength == keyword.Length) return i;
         }
         return -1;
      }

      public IEnumerable<(int start, int length)> GetPossibleKeywordStartPoints() {
         for (int i = 0; i < content.Length - 1; i++) {
            if (i > 0 && char.IsLetterOrDigit(content[i - 1])) continue; // not valid keyword start
            if (content[i] == '"') {
               // look for matching
               var closeIndex = i;
               for (int j = i + 1; j < content.Length; j++) {
                  if (content[j] != '"') continue;
                  closeIndex = j;
                  break;
               }
               if (closeIndex == i) break;
               yield return (i, closeIndex - i + 1);
               i = closeIndex + 1;
            }
            if (i < content.Length && !char.IsLetter(content[i])) continue;
            int length = 1;                                                                                               
            while (i + length < content.Length && (char.IsLetterOrDigit(content[i + length]) || content[i + length].IsAny(".'-~_\\".ToCharArray()))) length++;
            yield return (i, length);
            i += length;
         }
      }

      public bool Match(string keyword, int start) {
         if (start < 0 || start + keyword.Length > content.Length) return false;
         for (int i = 0; i < keyword.Length; i++) {
            if (content[start + i] != keyword[i]) return false;
         }
         return true;
      }

      public int IndexOf(string text, int start = 0) {
         if (text.Length == 0) return -1;
         for (int i = start; i < content.Length - text.Length + 1; i++) {
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

      private static readonly char[] hexLetters = "ABCDEF".ToCharArray();
      public (int, int) IndexOfNumber(int start) {
         for (int i = start; i < content.Length; i++) {
            if (i > 0 && char.IsLetterOrDigit(content[i - 1])) continue;
            if (i > 0 && content[i - 1] == '_') continue; // non-letter characters that are not considered token splitters
            if (!char.IsDigit(content[i]) && !content[i].IsAny(hexLetters)) continue;
            int length = 1;
            while (true) {
               if (i + length >= content.Length) break;
               if (length == 1 && content[i + length] == 'x') {
                  length++;
                  continue;
               }
               if (content[i+length].IsAny(hexLetters)) {
                  length++;
                  continue;
               }
               if (!char.IsDigit(content[i + length])) break;
               length++;
            }
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
         for (int i = 0; i < length; i++) {
            if (content[index + i].IsAny('\n', '\r', '\t')) continue;
            content[index + i] = ' ';
         }
      }

      public void CopyWhitespace(MutableString other) {
         for (int i = 0; i < content.Length; i++) {
            if (other.content[i].IsAny('\n', '\r', '\t')) {
               content[i] = other.content[i];
            }
         }
      }
   }
}
