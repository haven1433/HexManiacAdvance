using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>A pokemon element holds a normal and a female sprite set. Each sprite offers its own normal + shiny palettes: two, not four.</summary>
   public class SpriteVariantPaletteTests : BaseViewModelTestClass {
      private const int Table = 0x100;
      public SpriteVariantPaletteTests() : base(0x400) { }

      private void Build() {
         // front 0x200, palette 0x220, shiny 0x240, front female 0x260, palette female 0x280, shiny female 0x2A0
         ViewPort.Edit($"@{Table:X2} <200> <220> <240> <260> <280> <2A0> ");
         PokemonModel.ApplyAnchor(Model, ViewPort.CurrentChange, Table,
            "^pokes[frontPic<`ucs4x1x1`> palette<`ucp4`> shinyPalette<`ucp4`> frontPicFemale<`ucs4x1x1`> paletteFemale<`ucp4`> shinyPaletteFemale<`ucp4`>]1", allowAnchorOverwrite: true);
      }

      [Fact]
      public void NormalSprite_OffersNormalAndShinyPalettes() {
         Build();
         var sprite = (ISpriteRun)Model.GetNextRun(0x200);
         var palettes = sprite.FindRelatedPalettes(Model);
         Assert.Equal(new[] { 0x220, 0x240 }, palettes.Select(p => p.Start).ToArray());
      }

      [Fact]
      public void FemaleSprite_OffersFemalePalettes() {
         Build();
         var sprite = (ISpriteRun)Model.GetNextRun(0x260);
         var palettes = sprite.FindRelatedPalettes(Model);
         Assert.Equal(new[] { 0x280, 0x2A0 }, palettes.Select(p => p.Start).ToArray());
      }
   }
}
