using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
         Command = new StubCommand { CanExecute = action == null ? sender => false : CanAlwaysExecute, Execute = action };
         Parameter = parameter;
      }
   }

   public class DisabledContextItem : IContextItem {
      public string Text { get; }
      public ICommand Command { get; }
      public object Parameter { get; }
      public string ShortcutText { get; set; }
      public DisabledContextItem(string text, object parameter = null) {
         Text = text;
         Command = new StubCommand { CanExecute = arg => false, Execute = arg => { } };
         Parameter = parameter;
      }
   }

   /// <summary>
   /// Displays a set of contex items inline
   /// </summary>
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

   /// <summary>
   /// Displays a single context item that has a submenu
   /// </summary>
   public class ContextItemGroup : List<IContextItem>, IContextItem {
      public string Text { get; }
      public ICommand Command => throw new NotImplementedException();
      public object Parameter => throw new NotImplementedException();
      public string ShortcutText => throw new NotImplementedException();

      public ContextItemGroup(string label) => Text = label;
   }

   public class ContextItemFactory : IDataFormatVisitor {
      public ViewPort ViewPort { get; }
      public List<IContextItem> Results { get; } = new List<IContextItem>();

      public ContextItemFactory(ViewPort parent) => ViewPort = parent;

      public void Visit(Undefined dataFormat, byte data) => Visit((None)null, data);

      public void Visit(None dataFormat, byte data) {

         Results.Add(new ContextItemGroup("Display As...") {
            new ContextItem("Text", ViewPort.Shortcuts.DisplayAsText.Execute) { ShortcutText = "Ctrl+D, T" },
            new ContextItem("Sprite", ViewPort.Shortcuts.DisplayAsSprite.Execute) { ShortcutText = "Ctrl+D, S" },
            new ContextItem("Color Palette", ViewPort.Shortcuts.DisplayAsColorPalette.Execute) { ShortcutText = "Ctrl+D, C" },
            new ContextItem("Event Script", ViewPort.Shortcuts.DisplayAsEventScript.Execute) { ShortcutText = "Ctrl+D, E" },
         });

         if (data == 0xFF && ViewPort.SelectionEnd == ViewPort.SelectionStart && ViewPort[ViewPort.SelectionStart].Format is None) Results.Add(new ContextItemGroup("Create New...") {
            new ContextItem("Event Script", arg => {
               var newScriptStart = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
               // write the 'end' command
               ViewPort.CurrentChange.ChangeData(ViewPort.Model, newScriptStart, 2);
               ViewPort.Model.ObserveAnchorWritten(ViewPort.CurrentChange, $"scripts.new.xse_{newScriptStart:X6}", new XSERun(newScriptStart));
               var name = ViewPort.Model.GetAnchorFromAddress(-1, newScriptStart);
               ViewPort.Refresh();
               ViewPort.UpdateToolsFromSelection(newScriptStart);
               ViewPort.AnchorTextSelectionStart = 1;
               ViewPort.AnchorTextSelectionLength = name.Length;
            }),
            // new ContextItem("Compressed Sprite", arg => { /* TODO what dimensions? */ }),
            // new ContextItem("Compressed Palette", arg => { /* TODO how many pages? */ }),
         });
      }

      public void Visit(UnderEdit dataFormat, byte data) { }

      public void Visit(Pointer pointer, byte data) {
         var point = ViewPort.SelectionStart;

         var group = new ContextItemGroup("Pointer Operations");

         group.Add(new ContextItem("Follow Pointer", arg => ViewPort.FollowLink(point.X, point.Y)) { ShortcutText = "Ctrl+Click" });

         var pointerAddress = ViewPort.ConvertViewPointToAddress(point);
         var pointerDestination = ViewPort.Model.ReadPointer(pointerAddress);
         if (pointerDestination == Pointer.NULL) {
            group.Add(new ContextItem("Create New Data", arg => {
               ViewPort.RepointToNewCopy(pointerAddress);
               ViewPort.Refresh();
            }));
         } else {
            var destination = ViewPort.Model.GetNextRun(pointerDestination);
            if (!(destination is NoInfoRun) && destination.PointerSources.Count > 1) {
               group.Add(new ContextItem("Repoint to New Copy", arg => {
                  ViewPort.RepointToNewCopy(pointerAddress);
                  ViewPort.Refresh();
               }));
            }
            group.Add(new ContextItem("Open in New Tab", arg => ViewPort.OpenInNewTab(pointerDestination)));
         }

         Results.Add(group);

         if (ViewPort.Model.GetNextRun(pointerAddress) is ITableRun arrayRun && arrayRun.Start <= pointerAddress) {
            Results.AddRange(GetTableChildren());
         } else {
            Results.AddRange(GetFormattedChildren());
         }
      }

      public void Visit(Anchor anchor, byte data) {
         var name = string.IsNullOrEmpty(anchor.Name) ? "Anchor " + ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart).ToString("X6") : anchor.Name;

         var group = new ContextItemGroup(name);

         var selectionStartAddress = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
         if (ViewPort.Model.GetNextRun(selectionStartAddress) is ITableRun tableRun && tableRun.Start == selectionStartAddress) {
            if (tableRun.Start != ViewPort.DataOffset || tableRun.ElementLength != ViewPort.PreferredWidth) {
               group.Add(new ContextItem("Align Here", ViewPort.Goto.Execute, tableRun.Start));
            } else {
               group.Add(new DisabledContextItem("Align Here"));
            }
         }

         group.AddRange(GetAnchorSourceItems(ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart)));

         Results.Add(group);

         anchor.OriginalFormat.Visit(this, data);

         if (anchor.OriginalFormat is None) {
            Results.AddRange(GetFormattedChildren());
         }
      }

      public IEnumerable<ContextItem> GetAnchorSourceItems(int address) {
         var sources = ViewPort.Model.GetNextRun(address).PointerSources;
         if (sources == null || sources.Count == 0) {
            yield return new ContextItem("(Nothing points to this.)", null);
            yield break;
         }

         if (sources.Count > 1) {
            yield return new ContextItem("Show All Sources in new tab", p => {
               ViewPort.FindAllSources(address);
            });
         }

         var destinations = sources.Select(SystemExtensions.ToAddress).ToArray();
         foreach (var destination in destinations) {
            yield return new ContextItem(destination, ViewPort.Goto.Execute, destination);
         }
      }

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) => decorator.OriginalFormat.Visit(this, data);

      public void Visit(PCS pcs, byte data) {
         var point = ViewPort.SelectionStart;
         Results.Add(new ContextItem("Open In Text Tool", arg => ViewPort.FollowLink(point.X, point.Y)) { ShortcutText = "Ctrl+Click" });

         var address = ViewPort.ConvertViewPointToAddress(point);
         if (ViewPort.Model.GetNextRun(address) is ITableRun arrayRun && arrayRun.Start <= address) {
            Results.AddRange(GetTableChildren());
         } else {
            Results.AddRange(GetFormattedChildren());
         }
      }

      public void Visit(EscapedPCS pcs, byte data) => Visit((PCS)null, data);

      public void Visit(ErrorPCS pcs, byte data) => Visit((PCS)null, data);

      public void Visit(Ascii ascii, byte data) => Results.AddRange(GetFormattedChildren());

      public void Visit(Braille braille, byte data) => Results.AddRange(GetFormattedChildren());

      public void Visit(Integer integer, byte data) {
         if (ViewPort.Model.GetNextRun(ViewPort.Tools.TableTool.Address) is ITableRun) {
            Results.AddRange(GetTableChildren());
         } else {
            Results.AddRange(GetFormattedChildren());
         }
      }

      public void Visit(IntegerEnum integer, byte data) => Results.AddRange(GetTableChildren());

      public void Visit(IntegerHex integerHex, byte data) => Results.AddRange(GetTableChildren());

      public void Visit(EggSection section, byte data) => Visit((EggItem)null, data);

      public void Visit(EggItem item, byte data) {
         var point = ViewPort.SelectionStart;
         Results.Add(new ContextItem("Open In Text Tool", arg => ViewPort.FollowLink(point.X, point.Y)) { ShortcutText = "Ctrl+Click" });
         Results.AddRange(GetFormattedChildren());
      }

      public void Visit(PlmItem item, byte data) {
         var point = ViewPort.SelectionStart;
         Results.Add(new ContextItem("Open In Text Tool", arg => ViewPort.FollowLink(point.X, point.Y)) { ShortcutText = "Ctrl+Click" });
         Results.AddRange(GetFormattedChildren());
      }

      public void Visit(BitArray array, byte data) => Results.AddRange(GetTableChildren());

      public void Visit(MatchedWord word, byte data) => Results.AddRange(GetFormattedChildren());

      public void Visit(EndStream endStream, byte data) => Results.AddRange(GetFormattedChildren());

      public void Visit(UncompressedPaletteColor color, byte data) {
         var selectionStart = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
         if (ViewPort.Model.GetNextRun(selectionStart) is IPaletteRun palRun) {
            AddPalPaletteExportOption(palRun);
         }
         Results.AddRange(GetFormattedChildren());
      }

      private void AddPalPaletteExportOption(IPaletteRun palRun) {
         Results.Add(new ContextItem("Copy Palette as .pal", arg => {
            var fileSystem = (IFileSystem)arg;
            var colors = new List<short>();
            colors.AddRange(palRun.Pages.Range().SelectMany(i => palRun.GetPalette(ViewPort.Model, i)));
            var copyText = new StringBuilder();
            copyText.AppendLine("JASC-PAL");
            copyText.AppendLine("0100");
            copyText.AppendLine(colors.Count.ToString());
            foreach (var color in colors) {
               var (r, g, b) = UncompressedPaletteColor.ToRGB(color);
               copyText.AppendLine($"{r << 3} {g << 3} {b << 3}");
            }
            fileSystem.CopyText = copyText.ToString();
         }));
      }

      public void Visit(DataFormats.Tuple tuple, byte data) => Results.AddRange(GetFormattedChildren());

      private IEnumerable<IContextItem> GetTableChildren() {
         if (ViewPort.Tools.TableTool.Append.CanExecute(null)) {
            yield return new ContextItem("Extend Table", ViewPort.Tools.TableTool.Append.Execute);
         }

         yield return new ContextItem("Open in Table Tool", arg => ViewPort.Tools.SelectedIndex = 1);
         foreach (var item in GetFormattedChildren()) yield return item;
      }

      private IEnumerable<IContextItem> GetFormattedChildren() {
         yield return new ContextItem("Clear Format", arg => ViewPort.ClearAnchor());
      }

      public void Visit(LzMagicIdentifier lz, byte data) => VisitLzFormat();

      public void Visit(LzGroupHeader lz, byte data) => VisitLzFormat();

      public void Visit(LzCompressed lz, byte data) => VisitLzFormat();

      public void Visit(LzUncompressed lz, byte data) => VisitLzFormat();

      private void VisitLzFormat() {
         var selectionStart = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
         if (ViewPort.Model.GetNextRun(selectionStart) is IPaletteRun palRun) {
            AddPalPaletteExportOption(palRun);
         }
         Results.AddRange(GetFormattedChildren());
      }
   }
}
