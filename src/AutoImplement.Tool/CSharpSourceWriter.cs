using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HavenSoft.AutoImplement.Tool {
   public class CSharpSourceWriter {
      private readonly StringBuilder builder = new StringBuilder();

      private int indentLevel;

      public string Indentation { get; }

      /// <param name="numberOfSpacesToIndent">
      /// The number of spaces used for indenting in the source file.
      /// If this number is not positive, a single tab is used for indentation.
      /// </param>
      public CSharpSourceWriter(int numberOfSpacesToIndent = 0) {
         if (numberOfSpacesToIndent > 0) {
            Indentation = new string(' ', numberOfSpacesToIndent);
         } else {
            Indentation = "\t";
         }
      }

      public CSharpSourceWriter(string indentation) => Indentation = indentation;

      public void WriteUsings(params string[] usings) {
         foreach (var item in usings) {
            Write($"using {item};");
         }
         Write(string.Empty);
      }

      public void Write(string content) {
         var splitTokens = new[] { Environment.NewLine };
         var noSpecialOptions = StringSplitOptions.None;
         var currentIndentation = string.Concat(Enumerable.Repeat(Indentation, indentLevel));
         foreach (var line in content.TrimStart().Split(splitTokens, noSpecialOptions)) {
            builder.AppendLine(currentIndentation + line);
         }
      }

      public IDisposable Scope => new IndentationScope(this);

      public void AssignDefaultValuesToOutParameters(string owningNamespace, ParameterInfo[] parameters) {
         foreach (var p in parameters.Where(p => p.IsOut)) {
            Write($"{p.Name} = default({p.ParameterType.CreateCsName(owningNamespace)});");
         }
      }

      public override string ToString() => builder.ToString();

      private class IndentationScope : IDisposable {
         private readonly CSharpSourceWriter parent;

         public IndentationScope(CSharpSourceWriter writer) {
            parent = writer;
            parent.Write("{");
            parent.indentLevel += 1;
         }

         public void Dispose() {
            parent.indentLevel -= 1;
            parent.Write("}");
         }
      }
   }
}
