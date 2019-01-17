using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using HavenSoft.Gen3Hex.WPF.Controls;
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

      public static void ClearVisualCaches() {
         noneVisualCache.Clear();
      }

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

         var content = dataFormat.CurrentText;
         if (content.Length > 12) content = "…" + content.Substring(content.Length - 11);

         var text = new FormattedText(
            content,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            brush,
            1.0);

         context.DrawText(text, CellTextOffset);
      }

      public void Visit(Pointer dataFormat, byte data) {
         var brush = Solarized.Brushes.Blue;
         if (dataFormat.Destination == Pointer.NULL) brush = Solarized.Brushes.Red;
         int startPoint = dataFormat.Position == 0 ? 5 : 0;
         int endPoint = (int)HexContent.CellWidth - (dataFormat.Position == 3 ? 5 : 0);
         double y = (int)HexContent.CellHeight - 1.5;
         context.DrawLine(new Pen(brush, 1), new Point(startPoint, y), new Point(endPoint, y));

         if (dataFormat.Position != 1) return;

         var typeface = new Typeface("Consolas");
         var destination = dataFormat.DestinationName;
         if (string.IsNullOrEmpty(destination)) destination = dataFormat.Destination.ToString("X6");
         if (destination.Length > 11) destination = destination.Substring(0, 10) + "…";
         destination = $"<{destination}>";
         var xOffset = 21 - destination.Length * 4.2; // centering
         var text = new FormattedText(
            destination,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            brush,
            1.0);

         context.DrawText(text, new Point(CellTextOffset.X + xOffset, CellTextOffset.Y));
      }

      private static readonly Geometry Triangle = Geometry.Parse("M0,5 L3,0 6,5");
      public void Visit(Anchor anchor, byte data) {
         anchor.OriginalFormat.Visit(this, data);
         context.DrawGeometry(null, new Pen(Solarized.Brushes.Blue, 2), Triangle);
      }

      public void Visit(PCS pcs, byte data) {
         var typeface = new Typeface("Consolas");
         var text = new FormattedText(
            pcs.ThisCharacter,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Solarized.Brushes.Violet,
            1.0);

         var xOffset = 1 - pcs.ThisCharacter.Length;
         context.DrawText(text, new Point(CellTextOffset.X + xOffset, CellTextOffset.Y));
      }

      public void Visit(EscapedPCS pcs, byte data) {
         // intentionally draw nothing: this is taken care of by Visit PCS
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
