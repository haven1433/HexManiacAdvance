using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public struct SpriteFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedByteLength { get; }
      public string PaletteHint { get; }
      public SpriteFormat(int bitsPerPixel, int width, int height, string paletteHint) {
         (BitsPerPixel, TileWidth, TileHeight) = (bitsPerPixel, width, height);
         PaletteHint = paletteHint;
         ExpectedByteLength = 8 * BitsPerPixel * TileWidth * TileHeight;
      }
   }

   public struct PaletteFormat {
      public int Bits { get; }
      public int InitialBlankPages { get; }
      public int Pages { get; }
      public int ExpectedByteLengthPerPage => (int)Math.Pow(2, Bits + 1);

      public PaletteFormat(int bits, int pages, int initialBlankPages = 0) => (Bits, Pages, InitialBlankPages) = (bits, pages, initialBlankPages);
   }

   public struct TilesetFormat {
      public int BitsPerPixel { get; }
      public int Tiles { get; }
      public string PaletteHint { get; }
      public TilesetFormat(int bitsPerPixel, string paletteHint) => (BitsPerPixel, Tiles, PaletteHint) = (bitsPerPixel, -1, paletteHint);
      public TilesetFormat(int bitsPerPixel, int tiles, string paletteHint) : this(bitsPerPixel, paletteHint) => Tiles = tiles;
   }

   public struct TilemapFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedUncompressedLength => TileWidth * TileHeight * 2; // TODO handle BitsPerPixel
      public string MatchingTileset { get; }
      public string TilesetTableMember { get; }
      public TilemapFormat(int bits, int width, int height, string tileset, string tilesetTableMember = null) {
         BitsPerPixel = bits;
         TileWidth = width;
         TileHeight = height;
         MatchingTileset = tileset ?? string.Empty;
         TilesetTableMember = tilesetTableMember;
      }
   }

   public interface IPagedRun : IAppendToBuilderRun {
      int Pages { get; }
   }

   public interface ISpriteRun : IPagedRun {
      SpriteFormat SpriteFormat { get; }
      int[,] GetPixels(IDataModel model, int page);
      ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels);
      ISpriteRun Duplicate(SpriteFormat newFormat);
   }

   public interface IPaletteRun : IPagedRun {
      PaletteFormat PaletteFormat { get; }
      IReadOnlyList<short> GetPalette(IDataModel model, int page);
      IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors);
      IPaletteRun Duplicate(PaletteFormat newFormat);
   }

   public static class IPaletteRunExtensions {
      public static IReadOnlyList<short> AllColors(this IPaletteRun run, IDataModel model) {
         if (run.PaletteFormat.Bits == 8) return run.GetPalette(model, 0);
         var collection = new List<short>();

         for (int i = 0; i < run.Pages; i++) collection.AddRange(run.GetPalette(model, i));

         return collection;
      }
   }
}
