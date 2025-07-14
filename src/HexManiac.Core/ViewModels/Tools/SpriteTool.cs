using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum ImageExportMode { None, Horizontal, Vertical }

   public class SpriteTool : ViewModelCore, IToolViewModel, IPixelViewModel {
      public const int MaxSpriteWidth = 500 - 17; // From UI: Panel Width - Scroll Bar Width
      public readonly ChangeHistory<ModelDelta> history;
      public readonly IDataModel model;

      public double spriteScale;
      public int spritePages = 1, palPages = 1, spritePage = 0, palPage = 0;
      public int[,] pixels;
      public short[] palette;
      public PaletteFormat paletteFormat;

      public string Name => "Image";

      public double SpriteScale { get => spriteScale; set => TryUpdate(ref spriteScale, value); }

      #region Sprite Properties

      public bool showNoSpriteAnchorMessage = true;
      public bool ShowNoSpriteAnchorMessage { get => showNoSpriteAnchorMessage; set => Set(ref showNoSpriteAnchorMessage, value); }

      public bool showSpriteProperties = false;
      public bool ShowSpriteProperties { get => showSpriteProperties; set => Set(ref showSpriteProperties, value); }

      public string spriteWidthHeight;

      public bool spriteIs256Color;

      public bool spriteIsTilemap;
      public string spritePaletteHint = string.Empty;

      #endregion

      #region Palette Properties

      public bool showNoPaletteAnchorMessage = true;
      public bool ShowNoPaletteAnchorMessage { get => showNoPaletteAnchorMessage; set => Set(ref showNoPaletteAnchorMessage, value); }

      public bool ShowPaletteProperties => !showNoPaletteAnchorMessage;

      public bool paletteIs256Color;

      public string palettePages;

      #endregion

      #region Export Many

      public void ExecuteExportMany(IFileSystem fs) {
         var choice = fs.ShowOptions("Export Multi-Page Image", "How would you like to arrange the pages?",
            null,
            new VisualOption { Index = 0, Option = "Horizontal", ShortDescription = "Left-Right", Description = "Stack the pages from left to right." },
            new VisualOption { Index = 1, Option = "Vertical", ShortDescription = "Up-Down", Description = "Stack the pages from top to bottom." });

         ExecuteExportMany(fs, (ImageExportMode)(choice + 1));
      }

      public void ExecuteExportMany(IFileSystem fs, ImageExportMode choice) {
         int[,] manyPixels;
         if (choice == ImageExportMode.Horizontal) {
            manyPixels = new int[PixelWidth * spritePages, PixelHeight];
         } else if (choice == ImageExportMode.Vertical) {
            manyPixels = new int[PixelWidth, PixelHeight * spritePages];
         } else {
            return;
         }

         var spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
         var paletteRun = model.GetNextRun(paletteAddress) as IPaletteRun;
         var renderPalette = paletteRun.AllColors(model);

         for (int i = 0; i < spritePages; i++) {
            var (xPageOffset, yPageOffset) = choice == ImageExportMode.Horizontal ? (i * PixelWidth, 0) : (0, i * PixelHeight);
            var pagePixels = spriteRun.GetPixels(model, i, -1);
            int palOffset = (i % paletteRun.Pages) * 16;
            for (int x = 0; x < PixelWidth; x++) {
               for (int y = 0; y < PixelHeight; y++) {
                  var pixel = pagePixels[x, y] + palOffset;
                  pixel = Math.Max(0, pixel - (paletteFormat.InitialBlankPages << 4));
                  manyPixels[xPageOffset + x, yPageOffset + y] = pixel;
               }
            }
         }

         if (renderPalette.Count > 256) {
            var rendered = Render(manyPixels, renderPalette, 0, 0);
            fs.SaveImage(rendered, manyPixels.GetLength(0));
         } else {
            fs.SaveImage(manyPixels, renderPalette);
         }
      }

      #endregion

      public int spriteAddress;

      public int paletteAddress;

      public static readonly char[] allowableCharacters = "0123456789ABCDEF<>NUL".ToCharArray();
      public static readonly char[] toLower = "NUL".ToCharArray();
      public static string SanitizeAddressText(string address) {
         // allow for +/- on a specific digit of the address. Useful when searching for graphics.
         while (address.Contains("+")) {
            var index = address.IndexOf("+");
            if (index < 1) break;
            if (!int.TryParse(address.Substring(0, index), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed)) break;
            parsed += 1;
            address = parsed.ToString("X" + index) + address.Substring(index + 1);
         }
         while (address.Contains("-")) {
            var index = address.IndexOf("-");
            if (index < 1) break;
            if (!int.TryParse(address.Substring(0, index), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed)) break;
            parsed -= 1;
            if (parsed < 0) parsed = 0;
            address = parsed.ToString("X" + index) + address.Substring(index + 1);
         }

         var characters = address.ToUpper().Where(c => c.IsAny(allowableCharacters)).ToArray();
         for (int i = 0; i < characters.Length; i++) {
            foreach (char c in toLower) {
               if (characters[i] == c) characters[i] += (char)('a' - 'A');
            }
         }
         return new string(characters);
      }
      public string spriteAddressText, paletteAddressText;

      public bool HasMultipleSpritePages => spritePages > 1;
      public bool HasMultiplePalettePages => palPages > 1;

      public int PixelWidth { get; set; }
      public int PixelHeight { get; set; }
      public short Transparent => -1;
      public int PaletteWidth { get; set; }
      public int PaletteHeight { get; set; }

      public short[] PixelData { get; set; }

      public PaletteCollection Colors { get; }

      public bool IsReadOnly => true;

      public static short[] Render(int[,] pixels, IReadOnlyList<short> palette, int initialBlankPages, int palettePage) {
         if (pixels == null) return new short[0];
         if (palette == null || palette.Count == 0) palette = TileViewModel.CreateDefaultPalette(16);
         var data = new short[pixels.Length];
         var width = pixels.GetLength(0);
         var initialPageOffset = initialBlankPages << 4;
         var palettePageOffset = (palettePage << 4) + initialPageOffset;
         for (int i = 0; i < data.Length; i++) {
            var pixel = pixels[i % width, i / width];
            while (pixel < palettePageOffset) pixel += palettePageOffset;
            var pixelIntoPalette = Math.Max(0, pixel - initialPageOffset);
            data[i] = palette[pixelIntoPalette % palette.Count];
         }
         return data;
      }

      public static IReadOnlyList<short> CreatePaletteWithUniqueTransparentColor(IReadOnlyList<short> palette) {
         var copy = palette.ToList();
         var otherColors = palette.Skip(1).ToList();
         while (otherColors.Contains(copy[0])) copy[0] = (short)((copy[0] + 1) % 0x8000);
         return copy;
      }

      public SpriteFormat ReadDefaultSpriteFormat() {
         if (spriteWidthHeight == null || !spriteWidthHeight.Contains("x")) spriteWidthHeight = "4x4";
         var parts = spriteWidthHeight.Split('x');
         if (!int.TryParse(parts[0], out int width)) width = 4;
         if (parts.Length < 2 || !int.TryParse(parts[1], out int height)) height = 4;
         return new SpriteFormat(4, width, height, null);
      }

      public IReadOnlyList<short> GetRenderPalette(ISpriteRun sprite) {
         if (sprite == null) return palette;
         if (sprite.SpriteFormat.BitsPerPixel == 1) {
            return new short[] { 0, 0b0_11111_11111_11111 };
         }
         if (sprite.SpriteFormat.BitsPerPixel == 2) {
            return TileViewModel.CreateDefaultPalette(4);
         }
         if (sprite.SpriteFormat.BitsPerPixel == 8) {
            if (model.GetNextRun(paletteAddress) is IPaletteRun paletteRun) return paletteRun.AllColors(model);
         }
         if (!(sprite is LzTilemapRun tmRun)) return palette;
         tmRun.FindMatchingTileset(model);
         if (model.GetNextRun(paletteAddress) is IPaletteRun palRun) return palRun.AllColors(model);
         return palette;
      }

      public static int FindMatchingPalette(IDataModel model, ISpriteRun spriteRun, int defaultAddress) {
         var palettes = spriteRun.FindRelatedPalettes(model);
         if (palettes.Select(run => run.Start).Contains(defaultAddress)) return defaultAddress;
         if (palettes.Count > 0) return palettes[0].Start;
         return defaultAddress;
      }

      public bool RunPropertiesChanged(ISpriteRun run) {
         if (run == null) return false;
         if (run.SpriteFormat.TileWidth * 8 != PixelWidth) return true;
         if (run.SpriteFormat.TileHeight * 8 != PixelHeight) return true;
         if (run.Pages != spritePages) return true;
         return false;
      }

      public static (short[] image, int width) Unindex(int[,] pixels, IReadOnlyList<short> palette) {
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         var image = new short[width * height];
         for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) image[y * width + x] = palette[pixels[x, y]];
         return (image, width);
      }

      public short[][] SplitHorizontally(short[] image, int width, int count) {
         var images = new short[count][];
         var newWidth = width / count;
         for (var i = 0; i < count; i++) images[i] = new short[image.Length / count];
         for (int i = 0; i < image.Length; i++) {
            var j = (i / newWidth) % count;
            var y = i / width;
            var x = i % newWidth;
            images[j][newWidth * y + x] = image[i];
         }
         return images;
      }

      public static (int index, int page) GetTarget(IReadOnlyList<ISpriteRun> sprites, int target) {
         var (totalCount, initialTarget) = (0, target);
         for (int i = 0; i < sprites.Count; i++) {
            if (sprites[i].Pages > target) return (i, target);
            var pages = sprites[i].Pages;
            target -= pages;
            totalCount += pages;
         }
         throw new IndexOutOfRangeException($"Looking for page {initialTarget}, but there were only {totalCount} available pages among {sprites.Count} sprites!");
      }

      public static IReadOnlyList<short>[] DiscoverPalettes(short[][] tiles, int bitness, int paletteCount, IReadOnlyList<short>[] existingPalettes) {
         if (bitness == 1) return new[] { TileViewModel.CreateDefaultPalette(2) };
         if (bitness == 2) return new[] { TileViewModel.CreateDefaultPalette(4) };
         var targetColors = Math.Min((int)Math.Pow(2, bitness), existingPalettes.Sum(pal => pal.Count));

         // special case: we don't need to run palette discovery if the existing palette works
         bool allTilesFitExistingPalettes = true;
         foreach (var tile in tiles) {
            if (existingPalettes?.Any(pal => tile.All(pal.Contains)) ?? false) continue;
            allTilesFitExistingPalettes = false;
            break;
         }
         if (allTilesFitExistingPalettes && paletteCount == existingPalettes.Length) return existingPalettes;

         int palettePageCount = paletteCount;
         if (bitness == 8) paletteCount = 1;
         var palettes = new WeightedPalette[paletteCount];
         for (int i = 0; i < paletteCount; i++) palettes[i] = WeightedPalette.Reduce(tiles[i], targetColors);
         for (int i = paletteCount; i < tiles.Length; i++) {
            var newPalette = WeightedPalette.Reduce(tiles[i], targetColors);
            var (a, b) = WeightedPalette.CheapestMerge(palettes, newPalette);
            if (b < paletteCount) {
               palettes[a] = palettes[a].Merge(palettes[b], out var _);
               palettes[b] = newPalette;
            } else {
               palettes[a] = palettes[a].Merge(newPalette, out var _);
            }
         }

         // if this is an 8-bit image using 16-color palettes, split the resulting colors into arbitrary palettes.
         if (bitness == 8 && palettePageCount > 1) {
            var result = new List<IReadOnlyList<short>>();
            for (int i = 0; i < palettePageCount; i++) {
               result.Add(palettes[0].Palette.Skip(16 * i).Take(16).ToList());
            }
            return result.ToArray();
         }

         return palettes.Select(p => p.Palette).ToArray();
      }

      public static int[,] Index(short[] tile, IReadOnlyList<short>[] palettes, IReadOnlyList<int> usablePalPages, int bitness, int initialPageIndex, int palettePage, bool palettePerSprite = false) {
         var cheapestIndex = palettePage;
         if (bitness == 8 && usablePalPages.Count == palettes.Length) {
            palettes = new[] { palettes.SelectMany(pal => pal).ToList() };
            usablePalPages = new[] { 0 };
         } else if (bitness < 4) {
            // no palette: build a default palette
            palettes = new[] { TileViewModel.CreateDefaultPalette(bitness * 2) };
         }
         var cheapest = (usablePalPages != null && usablePalPages.Contains(palettePage) && palettes != null && palettes.Length > palettePage) ? WeightedPalette.CostToUse(tile, palettes[palettePage]) : double.PositiveInfinity;
         for (int i = 0; i < palettes.Length; i++) {
            if (cheapest == 0) break;
            if (usablePalPages != null && !usablePalPages.Contains(i)) continue;
            var cost = WeightedPalette.CostToUse(tile, palettes[i]);
            if (cost >= cheapest) continue;
            cheapest = cost;
            cheapestIndex = i;
         }
         var index = WeightedPalette.Index(tile, palettes[cheapestIndex]);
         for (int x = 0; x < index.GetLength(0); x++) {
            for (int y = 0; y < index.GetLength(1); y++) {
               // special case: 256-color sprites are still allowed to use the transparent color
               if (bitness == 8 && index[x, y] == 0) continue;
               if (palettePerSprite) continue; // special case: don't add the palette page offset if this is a bank of sprite-palette pairs
               index[x, y] += (initialPageIndex + cheapestIndex) << 4;
            }
         }
         return index;
      }

      public static short[][] Tilize(short[] image, int width) {
         var height = image.Length / width;
         if (width % 8 != 0 || height % 8 != 0) throw new NotSupportedException("You can only tilize an image if width/height are multiples of 8!");
         int tileWidth = width / 8, tileHeight = height / 8;
         var result = new short[tileWidth * tileHeight][];
         for (int y = 0; y < tileHeight; y++) {
            for (int x = 0; x < tileWidth; x++) {
               var tileIndex = y * tileWidth + x;
               result[tileIndex] = new short[64];
               for(int yy = 0; yy < 8; yy++) {
                  for(int xx = 0; xx < 8; xx++) {
                     var yIndex = y * 8 + yy;
                     var xIndex = x * 8 + xx;
                     var index = yIndex * width + xIndex;
                     result[tileIndex][yy * 8 + xx] = image[index];
                  }
               }
            }
         }
         return result;
      }

      public static int[,] Detilize(int[][,] tiles, int tileWidth) {
         Debug.Assert(tiles.Length % tileWidth == 0);
         int tileHeight = tiles.Length / tileWidth;
         var result = new int[tileWidth * 8, tiles.Length / tileWidth * 8];

         for (int y = 0; y < tileHeight; y++) {
            var yStart = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tile = tiles[y * tileWidth + x];
               var xStart = x * 8;
               for (int yy = 0; yy < 8; yy++) {
                  for (int xx = 0; xx < 8; xx++) {
                     result[xStart + xx, yStart + yy] = tile[xx, yy];
                  }
               }
            }
         }

         return result;
      }

      /// <summary>
      /// Given a set of indexed pixels and a new image,
      /// Edit a palette to be equal to the colors needed to map
      /// the indexed pixels into the new image.
      /// Note that if the image doesn't fit the indexed pixels, this method will return the pixels that caused the failure.
      /// This method will return (-1, -1) if some other issue is encountered, and return (width, height) if everything worked.
      /// </summary>
      public static Point TryReorderPalettesFromMatchingSprite(IReadOnlyList<short>[] palettes, short[] image, int[,] pixels) {
         if (palettes == null || palettes.Length != 1) return new Point(-1, -1);
         if (palettes[0].Count > 16) return new Point(-1, -1);
         var newPalette = new short[16];
         for (int i = 0; i < newPalette.Length; i++) newPalette[i] = -1;

         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
               var currentColor = image[y * width + x];
               var currentIndex = pixels[x, y] % newPalette.Length;
               if (newPalette[currentIndex] == -1) newPalette[currentIndex] = currentColor;
               if (newPalette[currentIndex] != currentColor) return new Point(x, y);
            }
         }

         // if there were any unused indexes, set thier color to black
         for (int i = 0; i < newPalette.Length; i++) {
            if (newPalette[i] == -1) newPalette[i] = 0;
         }

         palettes[0] = newPalette;
         return new Point(width, height);
      }
   }

   public enum ImportType {
      Unknown = -1,
      Smart = 0,
      Greedy = 1,
      Cautious = 2,
   }

   public class FlagViewModel : ViewModelCore {
      public string name;
      public string Name { get => name; set => Set(ref name, value); }
      public bool isSet;
      public bool IsSet { get => isSet; set => Set(ref isSet, value); }

      public FlagViewModel(string name) => (Name, IsSet) = (name, true);
   }

   public class EnumViewModel : ViewModelCore {
      public ObservableCollection<string> Options { get; } = new();

      public int choice;
      public int Choice { get => choice; set => Set(ref choice, value); }

      public EnumViewModel(params string[] options) {
         foreach (var option in options) Options.Add(option);
      }
   }

   public class WeightedPalette {
      public int TargetLength { get; }
      public IReadOnlyList<int> Weight { get; }
      public IReadOnlyList<short> Palette { get; }

      public WeightedPalette(IReadOnlyList<short> palette, int targetLength) : this(palette, new int[palette.Count], targetLength) { }

      public WeightedPalette(IReadOnlyList<short> palette, IReadOnlyList<int> weight, int targetLength) => (Palette, Weight, TargetLength) = (palette, weight, targetLength);

      public static WeightedPalette Weigh(int[,] sprite, IReadOnlyList<short> palette, int targetLength) {
         var weight = new int[targetLength];
         foreach (var num in sprite) weight[num % targetLength]++;
         return new WeightedPalette(palette, weight, targetLength);
      }

      /// <summary>
      /// Build a set of weighted palettes from a sprite that uses a set fo palettes.
      /// </summary>
      public static IReadOnlyList<WeightedPalette> Weigh(int[,] sprite, IReadOnlyList<short>[] palettes, int bitness, int pageOffset) {
         var targetLength = (int)Math.Pow(2, bitness);
         var weights = new int[palettes.Length][];
         for (int i = 0; i < palettes.Length; i++) weights[i] = new int[targetLength];

         for (int x = 0; x < sprite.GetLength(0); x++) {
            for (int y = 0; y < sprite.GetLength(1); y++) {
               var index = sprite[x, y] - (pageOffset << 4);
               var paletteIndex = (index >> 4) % palettes.Length;
               if (index < 0) continue; // we don't care about weighing colors from a palette that's out-of-range
               index -= paletteIndex << 4;
               if (index > weights[paletteIndex].Length) continue; // we don't care about weighing colors from a palette that's out-of-range
               weights[paletteIndex][index]++;
            }
         }

         var results = new List<WeightedPalette>();
         for (int i = 0; i < palettes.Length; i++) {
            results.Add(new WeightedPalette(palettes[i], weights[i], targetLength));
         }
         return results;
      }

      /// <summary>
      /// Given two sets of n palettes, merge them into a single set of n palettes
      /// </summary>
      public static IReadOnlyList<WeightedPalette> MergeLists(IReadOnlyList<WeightedPalette> set1, IReadOnlyList<WeightedPalette> set2) {
         Debug.Assert(set1.Count == set2.Count);
         var result = set1.ToList();
         foreach (var palette in set2) {
            var bestMerge = result[0].Merge(palette, out var bestCost);
            var bestIndex = 0;
            for (int i = 1; i < result.Count; i++) {
               if (bestCost == 0) break;
               var currentMerge = result[i].Merge(palette, out var currentCost);
               if (currentCost >= bestCost) continue;
               bestCost = currentCost;
               bestMerge = currentMerge;
               bestIndex = i;
            }
            result[bestIndex] = bestMerge;
         }
         return result;
      }

      public static ISpriteRun Update(IDataModel model, ModelDelta token, ISpriteRun spriteRun, IReadOnlyList<short>[] originalPalettes, IReadOnlyList<short>[] newPalettes, int initialPaletteOffset) {
         for (int i = 0; i < spriteRun.Pages; i++) {
            spriteRun = Update(model, token, spriteRun, originalPalettes, newPalettes, initialPaletteOffset, i);
         }
         return spriteRun;
      }

      /// <summary>
      /// Update a sprite to use a new palette
      /// </summary>
      public static ISpriteRun Update(IDataModel model, ModelDelta token, ISpriteRun spriteRun, IReadOnlyList<short>[] originalPalettes, IReadOnlyList<short>[] newPalettes, int initialPaletteOffset, int page) {
         var pixels = spriteRun.GetPixels(model, page, -1);

         if (spriteRun is LzTilemapRun tileMap) {
            var image = SpriteTool.Render(pixels, originalPalettes.SelectMany(s => s).ToList(), initialPaletteOffset, page);
            var tiles = SpriteTool.Tilize(image, spriteRun.SpriteFormat.TileWidth * 8);
            var indexedTiles = new int[tiles.Length][,];
            for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = SpriteTool.Index(tiles[i], newPalettes, null, spriteRun.SpriteFormat.BitsPerPixel, initialPaletteOffset, 0);
            pixels = SpriteTool.Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);
         } else {
            var indexMapping = CreatePixelMapping(originalPalettes, newPalettes, initialPaletteOffset);

            for (int x = 0; x < pixels.GetLength(0); x++) {
               for (int y = 0; y < pixels.GetLength(1); y++) {
                  var pixel = pixels[x, y];
                  while (pixel < (initialPaletteOffset << 4)) pixel += initialPaletteOffset << 4; // handle tiles mapped to no palette. Map them to the first palette.
                  pixels[x, y] = pixel >= indexMapping.Count ? pixel : indexMapping[pixel];  // don't remap any pixel that's using an unknown palette
               }
            }
         }

         spriteRun = spriteRun.SetPixels(model, token, page, pixels);
         return spriteRun;
      }

      public static IDictionary<int, int> CreatePixelMapping(IReadOnlyList<short>[] originalPalettes, IReadOnlyList<short>[] newPalettes, int initialPaletteOffset) {
         var result = new Dictionary<int, int>();
         for (int i = 0; i < originalPalettes.Length; i++) {
            var originalPaletteGroup = (i + initialPaletteOffset) << 4;
            var originalPalette = originalPalettes[i];
            var newPaletteIndex = TargetPalette(newPalettes, originalPalette);
            var newPalette = newPalettes[newPaletteIndex];
            var newPaletteGroup = (newPaletteIndex + initialPaletteOffset) << 4;
            for (int j = 0; j < originalPalette.Count; j++) {
               var colorIndex = BestMatch(originalPalette[j], newPalette);
               result[originalPaletteGroup + j] = newPaletteGroup + colorIndex;
            }
         }
         return result;
      }

      public static int TargetPalette(IReadOnlyList<short>[] options, IReadOnlyList<short> oldPalette) {
         var bestDistance = double.PositiveInfinity;
         var bestIndex = -1;
         for (int i = 0; i < options.Length; i++) {
            var currentDistance = 0.0;
            for (int j = 0; j < oldPalette.Count; j++) {
               var index = BestMatch(oldPalette[j], options[i]);
               currentDistance += Distance(options[i][index], oldPalette[j]);
            }
            if (bestDistance <= currentDistance) continue;
            bestDistance = currentDistance;
            bestIndex = i;
         }
         return bestIndex;
      }

      /// <summary>
      /// Creates a WeightedPalette from a non-indexed tile
      /// </summary>
      public static WeightedPalette Reduce(short[] rawColors, int targetColors) {
         var allColors = new Dictionary<short, ColorMass>();
         for (int i = 0; i < rawColors.Length; i++) {
            var color = rawColors[i];
            if (!allColors.ContainsKey(color)) allColors[color] = new ColorMass(color, 0);
            allColors[color] = new ColorMass(color, allColors[color].Mass + 1);
         }
         var palette = allColors.Values.ToList();
         return Reduce(palette, targetColors, out var _);
      }

      /// <summary>
      /// Reduces a number of colors to a target length or shorter, and reports how much force was needed to do it.
      /// </summary>
      public static WeightedPalette Reduce(IList<ColorMass> palette, int targetColors, out double cost) {
         cost = 0;
         if (palette.Count <= targetColors) return new WeightedPalette(palette.Select(p => p.ResultColor).ToList(), palette.Select(p => p.Mass).ToList(), targetColors);
         while (palette.Count > targetColors) {
            var bestPair = CheapestMerge(palette, out var bestCost);
            var combine1 = palette[bestPair.Item1];
            var combine2 = palette[bestPair.Item2];
            palette.RemoveAt(bestPair.Item2);
            palette[bestPair.Item1] = combine1 + combine2;
            cost += bestCost;
         }
         var resultPalette = palette.Select(p => p.ResultColor).ToList();
         var resultMasses = palette.Select(p => p.Mass).ToList();
         return new WeightedPalette(resultPalette, resultMasses, targetColors);
      }

      public static (int a, int b) CheapestMerge(IList<ColorMass> palette, out double cost) {
         var bestPair = (0, 1);
         var bestCost = double.PositiveInfinity;
         for (int i = 0; i < palette.Count - 1; i++) {
            for (int j = i + 1; j < palette.Count; j++) {
               var currentCost = palette[i] * palette[j];
               if (currentCost >= bestCost) continue;
               bestCost = currentCost;
               bestPair = (i, j);
            }
         }
         cost = bestCost;
         return bestPair;
      }

      public static (int a, int b) CheapestMerge(IReadOnlyList<WeightedPalette> palettes, WeightedPalette newPalette) {
         if (palettes.Count == 1) return (0, 1);

         var bestPair = (0, 0);
         double bestCost = double.PositiveInfinity;

         for (int i = 0; i < palettes.Count; i++) {
            for (int j = i + 1; j < palettes.Count; j++) {
               if (bestCost == 0) break;
               palettes[i].Merge(palettes[j], out var currentCost);
               if (currentCost >= bestCost) continue;
               bestCost = currentCost;
               bestPair = (i, j);
            }
            if (bestCost == 0) break;
            palettes[i].Merge(newPalette, out var newCost);
            if (newCost >= bestCost) continue;
            bestCost = newCost;
            bestPair = (i, palettes.Count);
         }

         return bestPair;
      }

      public static int[,] Index(short[] rawColors, IReadOnlyList<short> palette) {
         Debug.Assert(rawColors.Length == 64, "Expected to index a tile, but was not handed 64 pixels!");
         var result = new int[8, 8];
         for (int i = 0; i < rawColors.Length; i++) {
            int x = i % 8, y = i / 8;
            result[x, y] = BestMatch(rawColors[i], palette);
         }
         return result;
      }

      public static double CostToUse(short[] rawColors, IReadOnlyList<short> palette) {
         var allColors = new Dictionary<short, ColorMass>();
         for (int i = 0; i < rawColors.Length; i++) {
            var color = rawColors[i];
            if (!allColors.ContainsKey(color)) allColors[color] = new ColorMass(color, 0);
            allColors[color] = new ColorMass(color, allColors[color].Mass + 1);
         }

         double total = 0;
         foreach (var key in allColors.Keys) {
            var bestDistance = double.PositiveInfinity;
            for (int i = 0; i < palette.Count; i++) {
               var currentDistance = Distance(key, palette[i]);
               if (currentDistance < bestDistance) bestDistance = currentDistance;
               if (bestDistance == 0) break;
            }
            total += bestDistance * allColors[key].Mass;
         }

         return total;
      }

      public static int BestMatch(short input, IReadOnlyList<short> palette) {
         // TODO use best-match caching to reduce the number of distance calculations
         var bestIndex = 0;
         var bestDistance = Distance(input, palette[0]);
         for (int i = 1; i < palette.Count; i++) {
            if (bestDistance == 0) return bestIndex;
            var currentDistance = Distance(input, palette[i]);
            if (bestDistance <= currentDistance) continue;
            bestIndex = i;
            bestDistance = currentDistance;
         }
         return bestIndex;
      }

      public static double Distance(short a, short b) {
         var channel1A = a >> 10;
         var channel2A = (a >> 5) & 0x1F;
         var channel3A = a & 0x1F;

         var channel1B = b >> 10;
         var channel2B = (b >> 5) & 0x1F;
         var channel3B = b & 0x1F;

         var diff1 = channel1A - channel1B;
         var diff2 = channel2A - channel2B;
         var diff3 = channel3A - channel3B;

         return Math.Sqrt(diff1 * diff1 + diff2 * diff2 + diff3 * diff3);
      }

      public WeightedPalette Merge(WeightedPalette other, out double cost) {
         var masses = new List<ColorMass>();
         for (int i = 0; i < Palette.Count; i++) masses.Add(new ColorMass(Palette[i], Weight[i]));
         for (int i = 0; i < other.Palette.Count; i++) {
            var perfectMatchFound = false;
            for (int j = 0; j < masses.Count; j++) {
               if (masses[j].ResultColor != other.Palette[i]) continue;
               perfectMatchFound = true;
               masses[j] = new ColorMass(other.Palette[i], other.Weight[i] + masses[j].Mass);
               break;
            }
            if (!perfectMatchFound) {
               masses.Add(new ColorMass(other.Palette[i], other.Weight[i]));
            }
         }
         return Reduce(masses, TargetLength, out cost);
      }
   }

   public class ColorMass {
      public double R { get; set; }
      public double G { get; set; }
      public double B { get; set; }
      public int Mass { get; set; }

      public readonly Dictionary<short, int> originalColors = new Dictionary<short, int>();
      public IReadOnlyDictionary<short, int> OriginalColors => originalColors;

      public short ResultColor => CombineRGB((int)R, (int)G, (int)B);

      public ColorMass() { }
      public ColorMass(short color, int count) {
         originalColors[color] = count;
         (R, G, B) = SplitRGB(color);
         Mass = count;
      }

      /// <summary>
      /// Returns a new color mass that accounts for the positions/masses of the original two.
      /// </summary>
      public static ColorMass operator +(ColorMass a, ColorMass b) {
         if (a.Mass == 0) return b;
         if (b.Mass == 0) return a;
         var mass = a.Mass + b.Mass;
         var result = new ColorMass {
            Mass = mass,
            R = (a.R * a.Mass + b.R * b.Mass) / mass,
            G = (a.G * a.Mass + b.G * b.Mass) / mass,
            B = (a.B * a.Mass + b.B * b.Mass) / mass,
         };
         foreach (var key in a.originalColors.Keys) result.originalColors[key] = a.originalColors[key];
         foreach (var key in b.originalColors.Keys) {
            if (result.originalColors.ContainsKey(key)) result.originalColors[key] += b.originalColors[key];
            else result.originalColors[key] = b.originalColors[key];
         }
         return result;
      }

      /// <summary>
      /// Returns an inverse gravity factor, accounting for the distance/mass between the two color masses.
      /// Smaller numbers mean that it's a smaller change to merge the two. Larger numbers mean merging them should be avoided.
      /// </summary>
      public static double operator *(ColorMass a, ColorMass b) {
         if (a.Mass == 0 || b.Mass == 0) return 0;
         var dR = a.R - b.R;
         var dG = a.G - b.G;
         var dB = a.B - b.B;
         var distanceSquared = dR * dR + dG * dG + dB * dB;

         if (distanceSquared == 0) return 0; // no distance: merging is free

         // as the masses get farther apart, the force needed to merge them grows
         // as the masses get heavier, the force needed to merge them grows
         var force = a.Mass * b.Mass * distanceSquared;
         return force;
      }

      public static (int, int, int) SplitRGB(short color) => (color >> 10, (color >> 5) & 0x1F, color & 0x1F);

      public static short CombineRGB(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);

      public override string ToString() => $"{Mass}, {R}:{G}:{B}";
   }
}
