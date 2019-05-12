using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   public interface IContextItem {
      string Text { get; }
      ICommand Command { get; }
      object Parameter { get; }
      string ShortcutText { get; }
   }

   public class ContextItem : IContextItem {
      public string Text { get; }
      public ICommand Command { get; }
      public object Parameter { get; }
      public string ShortcutText { get; set; }
      public ContextItem(string text, Action<object> action, object parameter = null) {
         Text = text;
         Command = action == null ? null : new StubCommand { CanExecute = CanAlwaysExecute, Execute = action };
         Parameter = parameter;
      }
   }

   public class CompositeContextItem : List<IContextItem>, IContextItem {
      public string Text => throw new NotImplementedException();
      public ICommand Command => throw new NotImplementedException();
      public object Parameter => throw new NotImplementedException();
      public string ShortcutText => throw new NotImplementedException();

      public CompositeContextItem(Action<object> command, params string[] text) {
         foreach (var item in text) {
            Add(new ContextItem(item, command, item));
         }
      }
   }

   public class ContextItemFactory : IDataFormatVisitor {
      public ViewPort ViewPort { get; }
      public List<IContextItem> Results { get; } = new List<IContextItem>();

      public ContextItemFactory(ViewPort parent) => ViewPort = parent;

      public void Visit(Undefined dataFormat, byte data) => Visit((None)null, data);

      public void Visit(None dataFormat, byte data) {
         Results.Add(new ContextItem("Display as Text", ViewPort.IsText.Execute));
      }

      public void Visit(UnderEdit dataFormat, byte data) { }

      public void Visit(Pointer pointer, byte data) {
         var point = ViewPort.SelectionStart;
         Results.Add(new ContextItem("Follow Pointer", arg => ViewPort.FollowLink(point.X, point.Y)) { ShortcutText = "Ctrl+Click" });

         var arrayRun = ViewPort.Model.GetNextRun(ViewPort.Tools.TableTool.Address) as ArrayRun;
         if (arrayRun != null) Results.AddRange(GetTableChildren(arrayRun));
         else Results.AddRange(GetFormattedChildren());
      }

      public void Visit(Anchor anchor, byte data) {
         if (!string.IsNullOrEmpty(anchor.Name)) {
            Results.Add(new ContextItem(anchor.Name, null));
         };

         if (anchor.Sources.Count == 0) {
            Results.Add(new ContextItem("(Nothing points to this.)", null));
         }

         if (anchor.Sources.Count > 1) {
            Results.Add(new ContextItem("Show All Sources in new tab", p => {
               ViewPort.FindAllSources(ViewPort.SelectionStart.X, ViewPort.SelectionStart.Y);
            }));
         }

         var destinations = anchor.Sources.Select(source => source.ToString("X6")).ToArray();
         if (anchor.Sources.Count < 5) {
            foreach (var destination in destinations) {
               Results.Add(new ContextItem(destination, ViewPort.Goto.Execute, destination));
            }
         } else {
            Results.Add(new CompositeContextItem(ViewPort.Goto.Execute, destinations));
         }

         anchor.OriginalFormat.Visit(this, data);
      }

      public void Visit(PCS pcs, byte data) {
         var p = ViewPort.SelectionStart;
         Results.Add(new ContextItem("Open In Text Tool", arg => ViewPort.FollowLink(p.X, p.Y)) { ShortcutText = "Ctrl+Click" });
         Results.Add(new ContextItem("Copy Selection", ViewPort.Copy.Execute) { ShortcutText = "Ctrl+C" });

         var arrayRun = ViewPort.Model.GetNextRun(ViewPort.Tools.TableTool.Address) as ArrayRun;
         if (arrayRun != null) Results.AddRange(GetTableChildren(arrayRun));
         else Results.AddRange(GetFormattedChildren());
      }

      public void Visit(EscapedPCS pcs, byte data) => Visit((PCS)null, data);

      public void Visit(ErrorPCS pcs, byte data) => Visit((PCS)null, data);

      public void Visit(Ascii ascii, byte data) { }

      public void Visit(Integer integer, byte data) {
         var arrayRun = (ArrayRun)ViewPort.Model.GetNextRun(ViewPort.Tools.TableTool.Address);
         Results.AddRange(GetTableChildren(arrayRun));
      }

      public void Visit(IntegerEnum integer, byte data) {
         var arrayRun = (ArrayRun)ViewPort.Model.GetNextRun(ViewPort.Tools.TableTool.Address);
         Results.AddRange(GetTableChildren(arrayRun));
      }

      private IEnumerable<IContextItem> GetTableChildren(ArrayRun array) {
         if (ViewPort.Tools.TableTool.Append.CanExecute(null)) {
            yield return new ContextItem("Extend Table", ViewPort.Tools.TableTool.Append.Execute);
         }

         yield return new ContextItem("Open in Table Tool", arg => ViewPort.Tools.SelectedIndex = 1);
         foreach (var item in GetFormattedChildren()) yield return item;
      }

      private IEnumerable<IContextItem> GetFormattedChildren() {
         yield return new ContextItem("Clear Format", arg => ViewPort.ClearFormat());
      }
   }
}
