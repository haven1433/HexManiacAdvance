using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PythonTests : BaseViewModelTestClass {
      private readonly PythonTool tool;
      private List<string> Prints { get; } = new();
      private string Execute(string code) {
         tool.Text = code;
         tool.RunPython();
         return tool.ResultText;
      }

      public PythonTests() {
         FileSystem.ShowCustomMessageBox = (text, _, _) => {
            Prints.Add(text);
            return true;
         };
         var editor = New.EditorViewModel();
         tool = new PythonTool(editor);
         editor.Add(ViewPort);
      }

      [Fact]
      public void SubtableWithLengthFromParent_EditFieldWithPython_SubtableLengthUpdated() {
         SetFullModel(0xFF);
         //              2         <child>     (1,2)        (3,4)
         ViewPort.Edit("02 00 00 00 <100> @100 01 00 02 00 03 00 04 00 ");
         ViewPort.Edit("@000 ^parent[length:: child<[x: y:]/length>]1 ");

         Execute("table['parent'][0].length = 3");

         var table = (ITableRun)Model.GetNextRun(0x100);
         Assert.Equal(3, table.ElementCount);
      }
   }
}
