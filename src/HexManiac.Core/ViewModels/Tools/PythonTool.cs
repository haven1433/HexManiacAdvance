using HavenSoft.HexManiac.Core.Models;
using Microsoft.Scripting.Hosting;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PythonTool : ViewModelCore {
      private readonly ScriptEngine engine;
      private readonly ScriptScope scope;
      private readonly EditorViewModel editor;

      private string text, resultText;
      public string Text { get => text; set => Set(ref text, value); }
      public string ResultText { get=> resultText; set => Set(ref resultText, value); }

      public PythonTool(EditorViewModel editor) {
         this.editor = editor;
         engine = IronPython.Hosting.Python.CreateEngine();
         scope = engine.CreateScope();
         scope.SetVariable("editor", editor);
         scope.SetVariable("table", new TableGetter(editor));
         scope.SetVariable("print", (Action<string>)Printer);
         Text = @"print('''
   Put python code here.
   Use 'editor' to access the EditorViewModel.
   Use 'table' to access tables from the current tab.
   For example, try printing:
      table['data.pokemon.names'][1]['name']
''')";
      }

      public void RunPython() {
         try {
            var result = engine.Execute(text, scope);
            ResultText = result?.ToString() ?? "null";
         } catch (Exception ex) {
            ResultText = ex.Message;
         }

         editor.SelectedTab?.Refresh();
      }

      public void Printer(string text) {
         editor.FileSystem.ShowCustomMessageBox(text, false);
      }
   }

   public record TableGetter(EditorViewModel Editor) {
      public ModelTable this[string name] {
         get {
            if (Editor.SelectedTab is IViewPort viewPort && viewPort.Model is IDataModel model) {
               return new ModelTable(model, model.GetAddressFromAnchor(new(), -1, name), viewPort.ChangeHistory.CurrentChange);
            }
            return null;
         }
      }
   }
}
