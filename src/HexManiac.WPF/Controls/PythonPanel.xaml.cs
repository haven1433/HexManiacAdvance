using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Windows;
using Microsoft.Scripting.Hosting;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class PythonPanel : UserControl {
      private readonly ScriptEngine engine;
      private readonly ScriptScope scope;

      public PythonPanel() {
         InitializeComponent();
         engine = IronPython.Hosting.Python.CreateEngine();
         scope = engine.CreateScope();
         scope.SetVariable("editor", DataContext);
         DataContextChanged += (sender, e) => {
            scope.SetVariable("editor", DataContext);
            scope.SetVariable("table", new TableGetter(DataContext as EditorViewModel));
            scope.SetVariable("print", (Action<string>)Printer);
         };
         PythonTextBox.Text = @"print('''
   Put python code here.
   Use 'editor' to access the EditorViewModel.
   Use 'table' to access tables from the current tab.
   For example, try printing:
      table['data.pokemon.names'][1]['name']
''')";
      }

      private void PythonTextKeyDown(object sender, KeyEventArgs e) {
         if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) {
            e.Handled = true;
            RunPython(default, default);
         }
      }

      private void RunPython(object sender, RoutedEventArgs e) {
         var text = PythonTextBox.Text;
         try {
            var result = engine.Execute(text, scope);
            ResultText.Text = result?.ToString() ?? "null";
         } catch (Exception ex) {
            ResultText.Text = ex.Message;
         }

         if (DataContext is EditorViewModel editor) {
            editor.SelectedTab?.Refresh();
         }
      }

      private void Printer(string text) {
         var window = (MainWindow)Application.Current.MainWindow;
         window.FileSystem.ShowCustomMessageBox(text, false);
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
