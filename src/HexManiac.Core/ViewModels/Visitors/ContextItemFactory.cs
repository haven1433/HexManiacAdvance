using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   public interface IContextItem {
      string Text { get; }
      object Parameter { get; }
      string ShortcutText { get; }
   }

   public class ContextItem : IContextItem {
      public string Text { get; }
      public object Parameter { get; }
      public string ShortcutText { get; set; }
   }

   public class DisabledContextItem : IContextItem {
      public string Text { get; }
      public object Parameter { get; }
      public string ShortcutText { get; set; }
   }

   /// <summary>
   /// Displays a set of contex items inline
   /// </summary>
   public class CompositeContextItem : List<IContextItem>, IContextItem {
      public string Text => throw new NotImplementedException();
      public object Parameter => throw new NotImplementedException();
      public string ShortcutText => throw new NotImplementedException();
   }

   /// <summary>
   /// Displays a single context item that has a submenu
   /// </summary>
   public class ContextItemGroup : List<IContextItem>, IContextItem {
      public string Text { get; }
      public object Parameter => throw new NotImplementedException();
      public string ShortcutText => throw new NotImplementedException();

      public ContextItemGroup(string label) => Text = label;
   }

   public class ContextItemFactory : IDataFormatVisitor {
      public List<IContextItem> Results { get; } = new List<IContextItem>();

      public void Visit(UnderEdit dataFormat, byte data) { }
   }
}
