using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Implementations {
   public class FormatDrawer : IDataFormatVisitor {
      private readonly int fontSize = 16;

      private readonly Point CellTextOffset;

      private static int noneVisualCacheFontSize;
      private static readonly List<FormattedText> noneVisualCache = new List<FormattedText>();

      private readonly int modelWidth, modelHeight;
      private readonly Size cellSize;

      private readonly DrawingContext context;
      private readonly Geometry rectangleGeometry;

      public bool MouseIsOverCurrentFormat { get; set; }

      public HavenSoft.HexManiac.Core.Models.Point Position { get; set; }

      public FormatDrawer(DrawingContext drawingContext, int width, int height, double cellWidth, double cellHeight, int fontSize) {
         (context, modelWidth, modelHeight, cellSize) = (drawingContext, width, height, new Size(cellWidth, cellHeight));
         rectangleGeometry = new RectangleGeometry(new Rect(new Point(0, 0), cellSize));
         this.fontSize = fontSize;
         var testText = CreateText("00", fontSize, null);
         CellTextOffset = new Point((cellWidth - testText.Width) / 2, (cellHeight - testText.Height) / 2);
      }

      public static void ClearVisualCaches() {
         noneVisualCache.Clear();
      }

      public void Visit(Undefined dataFormat, byte data) {
         // intentionally draw nothing
      }

      public void Visit(None dataFormat, byte data) {
         //VerifyNoneVisualCache();
         //context.DrawText(noneVisualCache[data], CellTextOffset);
         var brush = data == 0x00 || data == 0xFF ? nameof(Theme.Secondary) : nameof(Theme.Primary);
         Draw(data.ToString("X2"), brush, fontSize, 1, 0, italics: data == 0xFF);
      }

      public void Visit(UnderEdit dataFormat, byte data) {
         var content = dataFormat.CurrentText;

         var text = CreateText(content, fontSize, nameof(Theme.Primary));

         var offset = CellTextOffset;
         var widthOverflow = text.Width - cellSize.Width * dataFormat.EditWidth;
         if (widthOverflow > 0) {
            // make it right aligned
            offset.X -= widthOverflow;
            context.PushClip(new RectangleGeometry(new Rect(new Size(cellSize.Width * dataFormat.EditWidth, cellSize.Height))));
            context.DrawText(text, new Point(-widthOverflow, CellTextOffset.Y));
            context.Pop();
         } else {
            context.DrawText(text, CellTextOffset);
         }
      }

      public void Visit(Pointer dataFormat, byte data) {
         var brush = nameof(Theme.Accent);
         if (dataFormat.Destination < 0) brush = nameof(Theme.Error);
         Underline(brush, dataFormat.Position == 0, dataFormat.Position == 3);
         var destination = dataFormat.DestinationAsText;

         Draw(destination, brush, fontSize, 4, dataFormat.Position, ">");
      }

      private static readonly Geometry Triangle = Geometry.Parse("M0,5 L3,0 6,5");
      public void Visit(Anchor anchor, byte data) {
         anchor.OriginalFormat.Visit(this, data);
         var pen = new Pen(Brush(nameof(Theme.Accent)), 1);
         if (MouseIsOverCurrentFormat) pen.Thickness = 2;
         context.DrawGeometry(null, pen, Triangle);
      }

      public void Visit(PCS pcs, byte data) {
         Draw(pcs.ThisCharacter, nameof(Theme.Text1), fontSize, 1, 0);
      }

      public void Visit(EscapedPCS pcs, byte data) {
         // intentionally draw nothing: this is taken care of by Visit PCS
      }

      public void Visit(ErrorPCS pcs, byte data) {
         Draw(data.ToString("X2"), nameof(Theme.Error), fontSize, 1, 0);
      }

      public void Visit(Ascii ascii, byte data) {
         Draw(ascii.ThisCharacter.ToString(), nameof(Theme.Text2), fontSize, 1, 0);
      }

      public void Visit(Integer integer, byte data) {
         Draw(integer.Value.ToString(), nameof(Theme.Data1), fontSize, integer.Length, integer.Position);
      }

      public void Visit(IntegerEnum integerEnum, byte data) {
         Draw(integerEnum.Value, nameof(Theme.Data2), fontSize * 3 / 4, integerEnum.Length, integerEnum.Position);
      }

      public void Visit(EggSection section, byte data) {
         Draw(section.SectionName, nameof(Theme.Stream1), fontSize * 3 / 4, 2, section.Position);
      }

      public void Visit(EggItem item, byte data) {
         Draw(item.ItemName, nameof(Theme.Stream2), fontSize * 3 / 4, 2, item.Position);
      }

      public void Visit(PlmItem item, byte data) {
         Draw(item.ToString(), nameof(Theme.Stream2), fontSize * 3 / 4, 2, item.Position);
      }

      private void Draw(string content, string brush, double size, int cells, int position, string appendEnd = "", bool italics = false) {
         var needsClip = position > Position.X || Position.X - position > modelWidth - cells;

         if (!needsClip && position != 0) return;
         var text = TruncateText(content, size, brush, cells, appendEnd, italics);
         var offset = GetCenteredOffset(position, cells, text);

         if (needsClip) context.PushClip(rectangleGeometry);
         context.DrawText(text, offset);
         if (needsClip) context.Pop();
      }

      private void Underline(string brush, bool isStart, bool isEnd) {
         int startPoint = isStart ? 5 : 0;
         int endPoint = (int)cellSize.Width - (isEnd ? 5 : 0);
         double y = (int)cellSize.Height - 1.5;
         context.DrawLine(new Pen(Brush(brush), 1), new Point(startPoint, y), new Point(endPoint, y));
      }

      private void VerifyNoneVisualCache() {
         if (noneVisualCache.Count != 0 && fontSize == noneVisualCacheFontSize) return;

         noneVisualCacheFontSize = fontSize;
         noneVisualCache.Clear();
         var bytesAsHex = Enumerable.Range(0, 0x100).Select(i => i.ToString("X2"));

         var text = bytesAsHex.Select(hex => {
            var brush = Brush(nameof(Theme.Primary));
            var typeface = new Typeface("Consolas");
            if (hex == "00" || hex == "FF") brush = Brush(nameof(Theme.Secondary));
            if (hex == "FF") {
               typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Italic, FontWeights.Light, FontStretches.Normal);
            }
            return new FormattedText(
               hex,
               CultureInfo.CurrentCulture,
               FlowDirection.LeftToRight,
               typeface,
               fontSize,
               brush,
               1.0);
         });

         noneVisualCache.AddRange(text);
      }

      private FormattedText TruncateText(string destination, double fontSize, string brush, int widthInCells, string postText = "", bool italics = false) {
         var text = CreateText(destination, fontSize, brush, italics);
         if (text.Width > cellSize.Width * widthInCells) {
            var unitWidth = text.Width / destination.Length;
            var desiredLength = destination.Length;
            while (unitWidth * desiredLength > cellSize.Width * widthInCells) desiredLength--;
            destination = destination.Substring(0, desiredLength - 1 - postText.Length) + "…" + postText;
            text = CreateText(destination, fontSize, brush, italics);
         }
         return text;
      }

      private Point GetCenteredOffset(int position, int cellWidth, FormattedText text) {
         var xOffset = (cellSize.Width * cellWidth - text.Width) / 2;
         xOffset -= (position * cellSize.Width); // centering
         var yOffset = (cellSize.Height - text.Height) / 2;
         return new Point(xOffset, yOffset);
      }

      private static readonly Typeface consolas = new Typeface("Consolas");
      private FormattedText CreateText(string text, double size, string color, bool italics = false) {
         var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            consolas,
            size,
            Brush(color),
            1.0);
         if (italics) formatted.SetFontStyle(FontStyles.Italic);
         return formatted;
      }

      readonly Dictionary<string, SolidColorBrush> cachedBrushes = new Dictionary<string, SolidColorBrush>();
      private SolidColorBrush Brush(string name) {
         if (name == null) return null;
         if (cachedBrushes.TryGetValue(name, out var brush)) return brush;
         cachedBrushes[name] = (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
         return cachedBrushes[name];
      }
   }
}
