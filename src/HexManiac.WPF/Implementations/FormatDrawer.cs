using HavenSoft.HexManiac.Core;
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
      static FormatDrawer() {
         consolas.TryGetGlyphTypeface(out typeface);
         var consolas2 = new Typeface(new FontFamily("Consolas"), FontStyles.Italic, FontWeights.Light, FontStretches.Normal);
         consolas2.TryGetGlyphTypeface(out italicTypeface);
      }

      private readonly int fontSize = 16;
      private readonly byte[] searchBytes;

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

      public FormatDrawer(DrawingContext drawingContext, IViewPort viewModel, int width, int height, double cellWidth, double cellHeight, int fontSize, byte[] searchBytes) {
         (context, viewPort, modelWidth, modelHeight, cellSize) = (drawingContext, viewModel, width, height, new Size(cellWidth, cellHeight));
         rectangleGeometry = new RectangleGeometry(new Rect(new Point(0, 0), cellSize));
         this.fontSize = fontSize;
         this.searchBytes = searchBytes;
         var testText = CreateText("00", fontSize, null);
         CellTextOffset = new Point((cellWidth - testText.Width) / 2, (cellHeight - testText.Height) / 2);
      }

      static readonly bool LightWeightUI = false;

      /// <summary>
      /// Rendering individual cells is too slow!
      /// For formats that follow the standard method of drawing (truncate and center, one color/size/style),
      /// we can render every cell of that format in a single pass.
      /// </summary>
      public void RenderRow() {
         var collector = new GlyphCollector(cellSize);

         collector.Initialize<None>(typeface, fontSize);            // actually None
         collector.Initialize<Undefined>(italicTypeface, fontSize); // actually None -> FF
         collector.Initialize<UnderEdit>(typeface, fontSize);       // actually None -> 00 and 'unused' Ints
         collector.Initialize<ErrorPCS>(typeface, fontSize);        // actually error pointer
         collector.Initialize<PCS>(typeface, fontSize);
         collector.Initialize<EscapedPCS>(typeface, fontSize);
         collector.Initialize<Ascii>(typeface, fontSize);
         collector.Initialize<Braille>(typeface, fontSize);
         collector.Initialize<Pointer>(typeface, fontSize);
         collector.Initialize<IDataFormat>(typeface, fontSize);     // actually offset pointers
         collector.Initialize<PlmItem>(typeface, fontSize * .75);
         collector.Initialize<EggSection>(typeface, fontSize * .75);
         collector.Initialize<EggItem>(typeface, fontSize * .75);
         collector.Initialize<IntegerEnum>(typeface, fontSize * .75);
         collector.Initialize<IntegerHex>(typeface, fontSize);
         collector.Initialize<Integer>(typeface, fontSize);
         collector.Initialize<BitArray>(typeface, fontSize);
         collector.Initialize<MatchedWord>(typeface, fontSize * .75);
         collector.Initialize<EndStream>(typeface, fontSize);
         collector.Initialize<LzMagicIdentifier>(typeface, fontSize);
         collector.Initialize<LzGroupHeader>(typeface, fontSize);
         collector.Initialize<LzUncompressed>(typeface, fontSize);
         collector.Initialize<LzCompressed>(typeface, fontSize);
         collector.Initialize<UncompressedPaletteColor>(typeface, fontSize * .75);
         collector.Initialize<Core.ViewModels.DataFormats.Tuple>(typeface, fontSize * .75);

         for (int x = 0; x < modelWidth; x++) {
            var cell = viewPort[x, position.Y];
            var format = cell.Format;
            while (format is IDataFormatDecorator decorator) format = decorator.OriginalFormat;

            if (format is PCS pcs) {
               collector.Collect<PCS>(format, x, 1, pcs.ThisCharacter);
            } else if (format is EscapedPCS escapedPCS) {
               collector.Collect<EscapedPCS>(format, x, 1, escapedPCS.ThisValue.ToString("X2"));
            } else if (format is Pointer pointer) {
               if (pointer.HasError) collector.Collect<ErrorPCS>(format, x, 4, pointer.DestinationAsText);
               else if (pointer.OffsetValue != 0) collector.Collect<IDataFormat>(format, x, 4, pointer.DestinationAsText);
               else collector.Collect<Pointer>(format, x, 4, pointer.DestinationAsText);
            } else if (format is PlmItem plm) {
               collector.Collect<PlmItem>(format, x, 2, plm.ToString());
            } else if (format is EggItem eggItem) {
               collector.Collect<EggItem>(format, x, 2, eggItem.ItemName);
            } else if (format is EggSection eggSection) {
               collector.Collect<EggSection>(format, x, 2, eggSection.SectionName);
            } else if (format is IntegerEnum intEnum) {
               collector.Collect<IntegerEnum>(format, x, intEnum.Length, intEnum.DisplayValue);
            } else if (format is IntegerHex integerHex) {
               if (integerHex.IsUnused) {
                  collector.Collect<UnderEdit>(format, x, integerHex.Length, integerHex.ToString());
               } else {
                  collector.Collect<IntegerHex>(format, x, integerHex.Length, integerHex.ToString());
               }
            } else if (format is Integer integer) {
               if (integer.IsUnused) {
                  collector.Collect<UnderEdit>(format, x, integer.Length, integer.Value.ToString());
               } else {
                  collector.Collect<Integer>(format, x, integer.Length, integer.Value.ToString());
               }
            } else if (format is Ascii asc) {
               collector.Collect<Ascii>(format, x, 1, asc.ThisCharacter.ToString());
            } else if (format is Braille braille) {
               collector.Collect<Braille>(format, x, 1, braille.ThisCharacter.ToString());
            } else if (format is None none) {
               if (!LightWeightUI) {
                  if (none.IsSearchResult) collector.Collect<None>(format, x, 1, cell.Value.ToHexString());
                  else if (cell.Value == 0x00) collector.Collect<UnderEdit>(format, x, 1, "00");
                  else if (cell.Value == 0xFF) collector.Collect<Undefined>(format, x, 1, "FF");
                  else if (searchBytes == null || searchBytes.Length == 0) collector.Collect<None>(format, x, 1, cell.Value.ToHexString());
                  else collector.Collect<UnderEdit>(format, x, 1, cell.Value.ToHexString());
               } else if (cell.Value == 0xB5 && x % 2 == 1) {
                  collector.Collect<Undefined>(format, x - 1, 2, "thumb");
               }
            } else if (format is BitArray array) {
               collector.Collect<BitArray>(format, x, array.Length, array.DisplayValue);
            } else if (format is MatchedWord word) {
               collector.Collect<MatchedWord>(format, x, 4, word.Name);
            } else if (format is EndStream endStream) {
               var converter = new ConvertCellToText(viewPort.Model, 0);
               converter.Visit(endStream, cell.Value);
               collector.Collect<EndStream>(format, x, endStream.Length, converter.Result);
            } else if (format is LzMagicIdentifier lzMagic) {
               collector.Collect<LzMagicIdentifier>(format, x, 1, "lz");
            } else if (format is LzGroupHeader lzGroup) {
               collector.Collect<LzGroupHeader>(format, x, 1, cell.Value.ToHexString());
            } else if (format is LzUncompressed lzUncompressed) {
               collector.Collect<LzUncompressed>(format, x, 1, cell.Value.ToHexString());
            } else if (format is LzCompressed lzCompressed) {
               collector.Collect<LzGroupHeader>(format, x, 2, $"{lzCompressed.RunLength}:{lzCompressed.RunOffset}");
            } else if (format is UncompressedPaletteColor color) {
               collector.Collect<UncompressedPaletteColor>(format, x, 2, color.ToString());
            } else if (format is Core.ViewModels.DataFormats.Tuple tuple) {
               collector.Collect<Core.ViewModels.DataFormats.Tuple>(format, x, tuple.Length, tuple.ToString());
            }
         }

         context.PushTransform(new TranslateTransform(0, position.Y * cellSize.Height));

         collector.Render<PCS>(context, Brush(nameof(Theme.Text1)));
         collector.Render<EscapedPCS>(context, Brush(nameof(Theme.Text1)));
         collector.Render<Pointer>(context, Brush(nameof(Theme.Accent)));
         collector.Render<IDataFormat>(context, Brush(nameof(Theme.Data2)));
         collector.Render<PlmItem>(context, Brush(nameof(Theme.Stream2)));
         collector.Render<EggItem>(context, Brush(nameof(Theme.Stream2)));
         collector.Render<EggSection>(context, Brush(nameof(Theme.Stream1)));
         collector.Render<IntegerEnum>(context, Brush(nameof(Theme.Data2)));
         collector.Render<IntegerHex>(context, Brush(nameof(Theme.Data2)));
         collector.Render<Integer>(context, Brush(nameof(Theme.Data1)));
         collector.Render<Ascii>(context, Brush(nameof(Theme.Text2)));
         collector.Render<Braille>(context, Brush(nameof(Theme.Text2)));
         collector.Render<None>(context, Brush(nameof(Theme.Primary)));
         collector.Render<UnderEdit>(context, Brush(nameof(Theme.Secondary)));
         collector.Render<Undefined>(context, Brush(nameof(Theme.Secondary)));
         collector.Render<ErrorPCS>(context, Brush(nameof(Theme.Error)));
         collector.Render<BitArray>(context, Brush(nameof(Theme.Data1)));
         collector.Render<MatchedWord>(context, Brush(nameof(Theme.Data1)));
         collector.Render<EndStream>(context, Brush(nameof(Theme.Stream1)));
         collector.Render<LzMagicIdentifier>(context, Brush(nameof(Theme.Text2)));
         collector.Render<LzGroupHeader>(context, Brush(nameof(Theme.Data1)));
         collector.Render<LzUncompressed>(context, Brush(nameof(Theme.Data2)));
         collector.Render<LzCompressed>(context, Brush(nameof(Theme.Stream2)));
         collector.Render<UncompressedPaletteColor>(context, Brush(nameof(Theme.Data2)));
         collector.Render<Core.ViewModels.DataFormats.Tuple>(context, Brush(nameof(Theme.Data2)));

         context.Pop();
      }

      public void Visit(Undefined dataFormat, byte data) {
         // intentionally draw nothing
      }

      public void Visit(None dataFormat, byte data) {
         if (data == 0xB5 && !LightWeightUI) Underline(nameof(Theme.Secondary), false, false);
      }

      public static double CalculateTextOffset(string text, int fontSize, double cellWidth, UnderEdit edit) {
         var defaultText = CreateText("00", fontSize, null);
         var baseTextOffset = (cellWidth - defaultText.Width) / 2;

         var actualText = CreateText(text, fontSize, null);
         var widthOverflow = actualText.Width - cellWidth * (edit?.EditWidth ?? 1);
         var spaceWidth = (text.Length - text.Trim().Length) * defaultText.Width / 2;

         if (widthOverflow > 0) {
            return -widthOverflow + actualText.Width + spaceWidth;
         } else {
            return baseTextOffset + actualText.Width + spaceWidth;
         }
      }

      public void Visit(UnderEdit dataFormat, byte data) {
         var content = dataFormat.CurrentText;

         var text = CreateText(content, fontSize, Brush(nameof(Theme.Primary)));

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
         if (dataFormat.OffsetValue != 0) brush = nameof(Theme.Data2);
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

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) => decorator.OriginalFormat.Visit(this, data);

      public void Visit(PCS pcs, byte data) { }

      public void Visit(EscapedPCS pcs, byte data) {
         // intentionally draw nothing: this is taken care of by Visit PCS
      }

      public void Visit(ErrorPCS pcs, byte data) {
         Draw(data.ToString("X2"), nameof(Theme.Error), fontSize, 1, 0);
      }

      public void Visit(Ascii ascii, byte data) { }

      public void Visit(Braille braille, byte data) { }

      public void Visit(Integer integer, byte data) { }

      public void Visit(IntegerEnum integerEnum, byte data) { }

      public void Visit(IntegerHex integerHex, byte data) { }

      public void Visit(EggSection section, byte data) { }

      public void Visit(EggItem item, byte data) { }

      public void Visit(PlmItem item, byte data) { }

      public void Visit(BitArray array, byte data) { }

      public void Visit(MatchedWord word, byte data) { }

      public void Visit(EndStream endStream, byte data) { }

      public void Visit(LzMagicIdentifier lz, byte data) { }

      public void Visit(LzGroupHeader lz, byte data) { }

      public void Visit(LzUncompressed lz, byte data) { }

      public void Visit(LzCompressed lz, byte data) { }

      public void Visit(UncompressedPaletteColor color, byte data) {
         var brush = (Brush)new PaletteColorConverter().Convert(color.Color, null, null, null);
         Underline(brush, color.Position == 0, color.Position == 1);
      }

      public void Visit(Core.ViewModels.DataFormats.Tuple tuple, byte data) { }

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

      private void Underline(string brush, bool isStart, bool isEnd) => Underline(Brush(brush), isStart, isEnd);

      private void Underline(Brush brush, bool isStart, bool isEnd) {
         var startPoint = position.X * cellSize.Width + (isStart ? cellSize.Width / 4 : 0);
         var endPoint = (position.X + 1) * cellSize.Width - (isEnd ? cellSize.Width / 4 : 0);
         double y = (position.Y + 1) * cellSize.Height - 1;
         context.DrawLine(new Pen(brush, 1), new Point(startPoint, y), new Point(endPoint, y));
      }

      private static FormattedText CreateText(string text, double size, SolidColorBrush color, bool italics = false) {
         var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            consolas,
            size,
            color,
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

      public void Collect<T>(IDataFormat format, int cellStart, int cellWidth, string text) where T : IDataFormat {
         if (text == null || text.Length == 0) return;

         if (format is IDataFormatInstance instance && instance.Position != 0) {
            if (cellStart > 0) return;
            cellStart -= instance.Position;
         }

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
         if (widths.Count == 0) return;
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
