using HavenSoft.ViewModel.DataFormats;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HavenSoft.Gen3Hex.View {
   public class FormatDrawer : IDataFormatVisitor {
      public static int FontSize = 16;

      public static readonly Point CellTextOffset = new Point(4, 3);

      private static readonly List<FormattedText> noneVisualCache = new List<FormattedText>();

      private readonly DrawingContext context;

      public FormatDrawer(DrawingContext drawingContext) => context = drawingContext;

      public void Visit(Undefined dataFormat, byte data) {
         // intentionally draw nothing
      }

      public void Visit(None dataFormat, byte data) {
         VerifyNoneVisualCache();
         context.DrawText(noneVisualCache[data], CellTextOffset);
      }

      private void VerifyNoneVisualCache() {
         if (noneVisualCache.Count != 0) return;

         var bytesAsHex = Enumerable.Range(0, 0x100).Select(i => i.ToString("X2"));

         var text = bytesAsHex.Select(hex => new FormattedText(
            hex,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            FontSize,
            Brushes.Black,
            1.0));

         noneVisualCache.AddRange(text);
      }
   }
}
