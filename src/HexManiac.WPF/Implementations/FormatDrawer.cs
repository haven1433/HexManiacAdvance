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

      private static readonly Typeface consolas = new Typeface("Consolas");
      private static readonly GlyphTypeface typeface, italicTypeface;
      private static string[] byteText;
      static FormatDrawer() {
         consolas.TryGetGlyphTypeface(out typeface);
         var consolas2 = new Typeface(new FontFamily("Consolas"), FontStyles.Italic, FontWeights.Light, FontStretches.Normal);
         consolas2.TryGetGlyphTypeface(out italicTypeface);
         byteText = Enumerable.Range(0, 0x100).Select(i => i.ToString("X2")).ToArray();
      }

      private readonly int fontSize = 16;

      private readonly Point CellTextOffset;

      private readonly int modelWidth, modelHeight;
      private readonly Size cellSize;

      private readonly DrawingContext context;
      private readonly IViewPort viewPort;
      private readonly Geometry rectangleGeometry;

      public bool MouseIsOverCurrentFormat { get; set; }

      private Core.Models.Point position;
      public HavenSoft.HexManiac.Core.Models.Point Position {
         get => position;
         set {
            position = value;
            if (position.X == 0) RenderRow();
         }
      }

      public FormatDrawer(DrawingContext drawingContext, IViewPort viewModel, int width, int height, double cellWidth, double cellHeight, int fontSize) {
         (context, viewPort, modelWidth, modelHeight, cellSize) = (drawingContext, viewModel, width, height, new Size(cellWidth, cellHeight));
         rectangleGeometry = new RectangleGeometry(new Rect(new Point(0, 0), cellSize));
         this.fontSize = fontSize;
         var testText = CreateText("00", fontSize, null);
         CellTextOffset = new Point((cellWidth - testText.Width) / 2, (cellHeight - testText.Height) / 2);
      }

      /// <summary>
      /// Rendering individual cells is too slow!
      /// For formats that follow the standard method of drawing (truncate and center, one color/size/style),
      /// we can render every cell of that format in a single pass.
      /// </summary>
      public void RenderRow() {
         var collector = new GlyphCollector(cellSize);

         collector.Initialize<None>(typeface, fontSize);            // actually None
         collector.Initialize<Undefined>(italicTypeface, fontSize); // actually None -> FF
         collector.Initialize<UnderEdit>(typeface, fontSize);       // actually None -> 00
         collector.Initialize<ErrorPCS>(typeface, fontSize);        // actually error pointer
         collector.Initialize<PCS>(typeface, fontSize);
         collector.Initialize<Ascii>(typeface, fontSize);
         collector.Initialize<Pointer>(typeface, fontSize);
         collector.Initialize<PlmItem>(typeface, fontSize * .75);
         collector.Initialize<EggSection>(typeface, fontSize * .75);
         collector.Initialize<EggItem>(typeface, fontSize * .75);
         collector.Initialize<IntegerEnum>(typeface, fontSize * .75);
         collector.Initialize<Integer>(typeface, fontSize);
         collector.Initialize<BitArray>(typeface, fontSize);

         for (int x = 0; x < modelWidth; x++) {
            var cell = viewPort[x, position.Y];
            var format = cell.Format;
            if (format is Anchor anchor) format = anchor.OriginalFormat; // anchor's have other formats nested inside that we may care about

            if (format is PCS pcs) {
               collector.Collect<PCS>(x, 1, pcs.ThisCharacter);
            } else if (format is Pointer pointer && pointer.Position == 0) {
               if (pointer.Destination < 0) collector.Collect<ErrorPCS>(x, 4, pointer.DestinationAsText);
               else collector.Collect<Pointer>(x, 4, pointer.DestinationAsText);
            } else if (format is PlmItem plm && plm.Position == 0) {
               collector.Collect<PlmItem>(x, 2, plm.ToString());
            } else if (format is EggItem eggItem && eggItem.Position == 0) {
               collector.Collect<EggItem>(x, 2, eggItem.ItemName);
            } else if (format is EggSection eggSection && eggSection.Position == 0) {
               collector.Collect<EggSection>(x, 2, eggSection.SectionName);
            } else if (format is IntegerEnum intEnum && intEnum.Position == 0) {
               collector.Collect<IntegerEnum>(x, intEnum.Length, intEnum.Value);
            } else if (format is Integer integer && integer.Position == 0) {
               collector.Collect<Integer>(x, integer.Length, integer.Value.ToString());
            } else if (format is Ascii asc) {
               collector.Collect<Ascii>(x, 1, asc.ThisCharacter.ToString());
            } else if (format is None none) {
               if (cell.Value == 0x00) collector.Collect<UnderEdit>(x, 1, "00");
               else if (cell.Value == 0xFF) collector.Collect<Undefined>(x, 1, "FF");
               else collector.Collect<None>(x, 1, byteText[cell.Value]);
            } else if (format is BitArray array) {
               collector.Collect<BitArray>(x, 1, byteText[cell.Value]);
            }
         }

         context.PushTransform(new TranslateTransform(0, position.Y * cellSize.Height));

         collector.Render<PCS>(context, Brush(nameof(Theme.Text1)));
         collector.Render<Pointer>(context, Brush(nameof(Theme.Accent)));
         collector.Render<PlmItem>(context, Brush(nameof(Theme.Stream2)));
         collector.Render<EggItem>(context, Brush(nameof(Theme.Stream2)));
         collector.Render<EggSection>(context, Brush(nameof(Theme.Stream1)));
         collector.Render<IntegerEnum>(context, Brush(nameof(Theme.Data2)));
         collector.Render<Integer>(context, Brush(nameof(Theme.Data1)));
         collector.Render<Ascii>(context, Brush(nameof(Theme.Text2)));
         collector.Render<None>(context, Brush(nameof(Theme.Primary)));
         collector.Render<UnderEdit>(context, Brush(nameof(Theme.Secondary)));
         collector.Render<Undefined>(context, Brush(nameof(Theme.Secondary)));
         collector.Render<ErrorPCS>(context, Brush(nameof(Theme.Error)));
         collector.Render<BitArray>(context, Brush(nameof(Theme.Data1)));

         context.Pop();
      }

      public void Visit(Undefined dataFormat, byte data) {
         // intentionally draw nothing
      }

      public void Visit(None dataFormat, byte data) { }

      public void Visit(UnderEdit dataFormat, byte data) {
         var content = dataFormat.CurrentText;

         var text = CreateText(content, fontSize, nameof(Theme.Primary));

         var offset = CellTextOffset;
         var widthOverflow = text.Width - cellSize.Width * dataFormat.EditWidth;
         context.PushTransform(new TranslateTransform(position.X * cellSize.Width, position.Y * cellSize.Height));

         if (widthOverflow > 0) {
            // make it right aligned
            offset.X -= widthOverflow;
            context.PushClip(new RectangleGeometry(new Rect(new Size(cellSize.Width * dataFormat.EditWidth, cellSize.Height))));
            context.DrawText(text, new Point(-widthOverflow, CellTextOffset.Y));
            context.Pop();
         } else {
            context.DrawText(text, CellTextOffset);
         }

         context.Pop();
      }

      public void Visit(Pointer dataFormat, byte data) {
         var brush = nameof(Theme.Accent);
         if (dataFormat.Destination < 0) brush = nameof(Theme.Error);
         Underline(brush, dataFormat.Position == 0, dataFormat.Position == 3);
      }

      private static readonly Geometry Triangle = Geometry.Parse("M0,5 L3,0 6,5");
      public void Visit(Anchor anchor, byte data) {
         anchor.OriginalFormat.Visit(this, data);
         var pen = new Pen(Brush(nameof(Theme.Accent)), 1);
         if (MouseIsOverCurrentFormat) pen.Thickness = 2;
         context.PushTransform(new TranslateTransform(position.X * cellSize.Width, position.Y * cellSize.Height));
         context.PushTransform(new ScaleTransform(fontSize / 16.0, fontSize / 16.0));
         context.DrawGeometry(null, pen, Triangle);
         context.Pop();
         context.Pop();
      }

      public void Visit(PCS pcs, byte data) { }

      public void Visit(EscapedPCS pcs, byte data) {
         // intentionally draw nothing: this is taken care of by Visit PCS
      }

      public void Visit(ErrorPCS pcs, byte data) {
         Draw(data.ToString("X2"), nameof(Theme.Error), fontSize, 1, 0);
      }

      public void Visit(Ascii ascii, byte data) { }

      public void Visit(Integer integer, byte data) { }

      public void Visit(IntegerEnum integerEnum, byte data) { }

      public void Visit(EggSection section, byte data) { }

      public void Visit(EggItem item, byte data) { }

      public void Visit(PlmItem item, byte data) { }

      public void Visit(BitArray array, byte data) { }

      /// <summary>
      /// This function is full of dragons. You probably don't want to touch it.
      /// </summary>
      private void Draw(string text, string brush, double size, int cells, int position, string appendEnd = "", bool italics = false) {
         var needsClip = position > Position.X || Position.X - position > modelWidth - cells;
         if (!needsClip && position != 0) return;
         
         var textTypeface = italics ? italicTypeface : typeface;
         var run = CreateGlyphRun(textTypeface, size, cells, position, text, appendEnd);

         context.PushTransform(new TranslateTransform(this.position.X * cellSize.Width, this.position.Y * cellSize.Height));
         if (needsClip) context.PushClip(rectangleGeometry);
         context.DrawGlyphRun(Brush(brush), run);
         if (needsClip) context.Pop();
         context.Pop();
      }

      private GlyphRun CreateGlyphRun(GlyphTypeface typeface, double size, int cells, int position, string text, string appendEnd = "") {
         appendEnd = "…" + appendEnd;

         var glyphIndexes = new List<ushort>(text.Length);
         var advanceWidths = new List<double>(text.Length);
         double totalWidth = 0;
         for (int i = 0; i < text.Length; i++) {
            if (!typeface.CharacterToGlyphMap.TryGetValue(text[i], out ushort glyphIndex)) {
               text = text.Substring(0, i) + text.Substring(i + 1);
               i--;
               continue;
            }
            glyphIndexes.Add(glyphIndex);
            double width = typeface.AdvanceWidths[glyphIndex] * size;
            advanceWidths.Add(width);
            totalWidth += width;
            if (totalWidth <= cellSize.Width * cells) continue;
            // too wide: replace the end with the appendEnd
            for (int j = 0; j < appendEnd.Length + 1; j++) {
               glyphIndexes.RemoveAt(glyphIndexes.Count - 1);
               advanceWidths.RemoveAt(advanceWidths.Count - 1);
            }
            totalWidth -= width * (appendEnd.Length + 1);
            text = text.Substring(0, advanceWidths.Count) + appendEnd;
            i -= appendEnd.Length + 1;
         }

         var xOffset = (cellSize.Width * cells - totalWidth) / 2;
         xOffset -= (position * cellSize.Width); // centering
         var yOffset = (cellSize.Height - typeface.Height * size) / 2 + typeface.Baseline * size;
         var origin = new Point(xOffset, yOffset);

         return new GlyphRun(typeface, 0, false, size, 1.0f, glyphIndexes, origin,
            advanceWidths, null, text.ToCharArray(), null, null, null, null);
      }

      private void Underline(string brush, bool isStart, bool isEnd) {
         var startPoint = position.X * cellSize.Width + (isStart ? cellSize.Width / 4 : 0);
         var endPoint = (position.X + 1) * cellSize.Width - (isEnd ? cellSize.Width / 4 : 0);
         double y = (position.Y + 1) * cellSize.Height - 1.5;
         context.DrawLine(new Pen(Brush(brush), 1), new Point(startPoint, y), new Point(endPoint, y));
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

      private FormattedText CreateText(string text, double size, string color, bool italics = false) {
         var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            consolas,
            size,
            Brush(color),
            1.0);
         if (italics) {
            formatted.SetFontStyle(FontStyles.Italic);
            formatted.SetFontWeight(FontWeights.Light);
         }
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

   public class GlyphCollector {
      private readonly Size cellSize;
      private readonly Dictionary<Type, double> sizes = new Dictionary<Type, double>();
      private readonly Dictionary<Type, double> initialHorizontalOffsets = new Dictionary<Type, double>();
      private readonly Dictionary<Type, GlyphTypeface> typefaces = new Dictionary<Type, GlyphTypeface>();
      private readonly Dictionary<Type, List<double>> widths = new Dictionary<Type, List<double>>();     // the spacing between each character
      private readonly Dictionary<Type, int> nextCell = new Dictionary<Type, int>();                     // which cell would be next to fill for this run
      private readonly Dictionary<Type, List<ushort>> glyphs = new Dictionary<Type, List<ushort>>();
      private readonly Dictionary<Type, List<char>> texts = new Dictionary<Type, List<char>>();

      public GlyphCollector(Size cellSize) => this.cellSize = cellSize;

      public void Initialize<T>(GlyphTypeface typeface, double size) where T : IDataFormat {
         typefaces[typeof(T)] = typeface;
         sizes[typeof(T)] = size;
         glyphs[typeof(T)] = new List<ushort>();
         widths[typeof(T)] = new List<double>();
         texts[typeof(T)] = new List<char>();
      }

      public void Collect<T>(int cellStart, int cellWidth, string text) where T : IDataFormat {
         if (text == null || text.Length == 0) return;
         string appendEnd = "…";
         if (text.EndsWith(">")) appendEnd += ">";
         double availableSectionWidth = cellSize.Width * cellWidth;

         var typeface = typefaces[typeof(T)];
         var size = sizes[typeof(T)];
         var glyphs = this.glyphs[typeof(T)];
         var widths = this.widths[typeof(T)];

         // add to the glyphs and widths
         var sectionWidth = 0.0;
         for (int i = 0; i < text.Length; i++) {
            if (!typeface.CharacterToGlyphMap.TryGetValue(text[i], out ushort glyphIndex)) {
               text = text.Substring(0, i) + text.Substring(i + 1);
               i--;
               continue;
            }
            glyphs.Add(glyphIndex);
            double width = typeface.AdvanceWidths[glyphIndex] * size;
            widths.Add(width);
            sectionWidth += width;
            if (sectionWidth <= availableSectionWidth) continue;
            // too wide: replace the end with the appendEnd
            for (int j = 0; j < appendEnd.Length + 1; j++) {
               glyphs.RemoveAt(glyphs.Count - 1);
               widths.RemoveAt(widths.Count - 1);
            }
            sectionWidth -= width * (appendEnd.Length + 1);
            i -= appendEnd.Length + 1;
            text = text.Substring(0, i + 1) + appendEnd;
         }

         texts[typeof(T)].AddRange(text);

         var sectionPadding = (cellWidth * cellSize.Width - sectionWidth) / 2;
         double sectionStart = cellStart * cellSize.Width + sectionPadding;
         widths[widths.Count - 1] += sectionPadding;
         if (!initialHorizontalOffsets.ContainsKey(typeof(T))) {
            initialHorizontalOffsets[typeof(T)] = sectionStart;
         } else {
            widths[widths.Count - 1 - text.Length] += sectionStart - nextCell[typeof(T)] * cellSize.Width;
         }
         nextCell[typeof(T)] = cellStart + cellWidth;
      }

      public void Render<T>(DrawingContext context, Brush brush) where T : IDataFormat {
         if (!initialHorizontalOffsets.ContainsKey(typeof(T))) return;
         var typeface = typefaces[typeof(T)];
         var size = sizes[typeof(T)];
         var glyph = glyphs[typeof(T)];
         var initialVerticalOffset = (cellSize.Height - typeface.Height * size) / 2 + typeface.Baseline * size;
         var origin = new Point(initialHorizontalOffsets[typeof(T)], initialVerticalOffset);
         var width = widths[typeof(T)];
         var text = texts[typeof(T)];
         var run = new GlyphRun(typeface, 0, false, size, 1.0f, glyph, origin, width, null, text, null, null, null, null);
         context.DrawGlyphRun(brush, run);
      }
   }
}
