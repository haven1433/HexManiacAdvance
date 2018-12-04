using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HavenSoft.Gen3Hex.WPF.Implementations {
   public class FormatDrawer : IDataFormatVisitor {
      public static int FontSize = 16;

      public static readonly Point CellTextOffset = new Point(6, 1);

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

      public void Visit(UnderEdit dataFormat, byte data) {
         var brush = Solarized.Theme.Primary;
         var typeface = new Typeface("Consolas");

         var text = new FormattedText(
            dataFormat.CurrentText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            brush,
            1.0);

         context.DrawText(text, CellTextOffset);
      }

      public void Visit(Pointer dataFormat, byte data) {
         // TODO
      }

      private void VerifyNoneVisualCache() {
         if (noneVisualCache.Count != 0) return;

         var bytesAsHex = Enumerable.Range(0, 0x100).Select(i => i.ToString("X2"));

         var text = bytesAsHex.Select(hex => {
            var brush = Solarized.Theme.Emphasis;
            var typeface = new Typeface("Consolas");
            if (hex == "00" || hex == "FF") {
               brush = Solarized.Theme.Secondary;
               typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Italic, FontWeights.Light, FontStretches.Normal);
            }
            return new FormattedText(
               hex,
               CultureInfo.CurrentCulture,
               FlowDirection.LeftToRight,
               typeface,
               FontSize,
               brush,
               1.0);
         });

         noneVisualCache.AddRange(text);
      }
   }
}
