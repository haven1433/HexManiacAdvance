using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections;
using System.Diagnostics;
using System.Dynamic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PythonTool : ViewModelCore {
      private Lazy<ScriptEngine> engine;
      private Lazy<ScriptScope> scope;
      private readonly EditorViewModel editor;
      private readonly TextEditorViewModel content;

      private string resultText;
      public string Text { get => content.Content; set => content.Content = value; }
      public string ResultText { get => resultText; set => Set(ref resultText, value); }
      public TextEditorViewModel PythonEditor => content;

      public PythonTool(EditorViewModel editor) {
         this.editor = editor;
         content = SetupPythonEditor();
         engine = new(() => {
            var engine = IronPython.Hosting.Python.CreateEngine();
            var paths = engine.GetSearchPaths();
            paths.Add(Environment.CurrentDirectory);
            engine.SetSearchPaths(paths);
            return engine;
         });

         scope = new(() => {
            var scope = engine.Value.CreateScope();
            scope.SetVariable("editor", editor);
            scope.SetVariable("table", new TableGetter(editor));
            scope.SetVariable("print", (Action<string>)Printer);
            try {
               engine.Value.Execute(@"
import clr
clr.AddReference('HexManiac.Core')
import HavenSoft.HexManiac.Core
clr.ImportExtensions(HavenSoft.HexManiac.Core.Models)
",
                  scope);
               engine.Value.Execute(editor.Singletons.PythonUtility, scope);
            } catch (Exception ex) {
               Debug.Fail(ex.Message);
            }
            return scope;
         });
         Text = @"print('''
   Put python code here.
   'editor' is the EditorViewModel.
   Tables from the current tab
     can be accessed by name.
   For example, try printing:
      data.pokemon.names[1].name
   Or try changing a table using a loop:

   for mon in data.pokemon.stats:
     mon.hp = 100
''')";
      }

      public void AddVariable(string name, object value) => scope.Value.SetVariable(name, value);

      public void Printer(string text) {
         editor.FileSystem.ShowCustomMessageBox(text, false);
      }

      public static TextEditorViewModel SetupPythonEditor() {
         var editor = new TextEditorViewModel() {
            Keywords = {
               "for", "while", "in",
               "if", "elif", "else",
               "def", "return", "continue", "break", "yield",
               "class", "lambda",
               "import", "from",
               "try", "except",
               "with", "pass", "as", "is", "not",
               "len", "print", "zip", "range", "open", "round",
            },
            Constants = {
               "True",
               "False",
               "None",
               "str",
            },
            LineCommentHeader = "#",
            MultiLineCommentHeader = "'''",
            MultiLineCommentFooter = "'''",
            PreFormatter = new PythonTextFormatter(),
         };
         return editor;
      }
   }

   public class PythonTextFormatter : ITextPreProcessor {
      public TextFormatting[] Format(string content) {
         var result = new TextFormatting[content.Length];
         bool inSingleQuoteText = false;
         bool inDoubleQuoteText = false;
         var escaped = false;
         for (int i = 0; i < content.Length; i++) {
            var wasInQutoes = inSingleQuoteText || inDoubleQuoteText;
            if (!escaped && content[i] == '\'' && !inDoubleQuoteText) inSingleQuoteText = !inSingleQuoteText;
            if (!escaped && content[i] == '"' && !inSingleQuoteText) inDoubleQuoteText = !inDoubleQuoteText;
            if (wasInQutoes || inSingleQuoteText || inDoubleQuoteText) result[i] = TextFormatting.Text;
            escaped = content[i] == '\\' && !escaped;
         }
         return result;
      }
   }

   public record TableGetter(EditorViewModel Editor) {
      
   }
}
