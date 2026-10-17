using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.CubeUpdate;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.Core.ViewModels;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The Utilities menu asks every quick edit whether it can run each time the user changes tabs,
   /// so a quick edit that is only for one exact file must be able to say "no" without reading the whole file.
   /// </summary>
   public class QuickEditCanRunTests : BaseViewModelTestClass {
      [Fact]
      public void ApplyCFRUPatch_SomethingThatIsNotAViewPort_CanNotRun() {
         Assert.False(new ApplyCFRUPatch().CanRun(null));
      }

      [Fact]
      public void ApplyCFRUPatch_ASmallFile_CanNotRun() {
         Assert.False(new ApplyCFRUPatch().CanRun(ViewPort));
      }

      [Fact]
      public void ApplyCFRUPatch_FireRedCodeButNotTheVanillaSize_CanNotRun() {
         SetGameCode("BPRE0");
         Assert.False(new ApplyCFRUPatch().CanRun(ViewPort));
      }

      // "Apply CUBE" is asked the same question each time the tab changes, and is grayed out the same way

      private static ApplyCubePatch NewApplyCube() => new ApplyCubePatch(new EditorViewModel(new StubFileSystem()));

      [Fact]
      public void ApplyCubePatch_SomethingThatIsNotAViewPort_CanNotRun() {
         Assert.False(NewApplyCube().CanRun(null));
      }

      [Fact]
      public void ApplyCubePatch_ASmallFile_CanNotRun() {
         Assert.False(NewApplyCube().CanRun(ViewPort));
      }

      [Fact]
      public void ApplyCubePatch_EmeraldCodeButNotTheVanillaSize_CanNotRun() {
         SetGameCode("BPEE0");
         Assert.False(NewApplyCube().CanRun(ViewPort));
      }

      [Fact]
      public void ApplyCubePatch_VanillaSizeButAnotherGame_CanNotRun() {
         var firered = new byte[VanillaRomSpec.EmeraldUsV10.Length];
         System.Text.Encoding.ASCII.GetBytes("BPRE").CopyTo(firered, 0xAC);
         var tab = new ViewPort("FireRed.gba", new PokemonModel(firered, singletons: Singletons), InstantDispatch.Instance, Singletons);
         Assert.False(NewApplyCube().CanRun(tab));
      }

      [Fact]
      public void ApplyCubePatch_VanillaSizeAndEmeraldCodeButOtherContent_CanNotRun() {
         var almostEmerald = new byte[VanillaRomSpec.EmeraldUsV10.Length];
         System.Text.Encoding.ASCII.GetBytes("BPEE").CopyTo(almostEmerald, 0xAC);
         var tab = new ViewPort("Hack.gba", new PokemonModel(almostEmerald, singletons: Singletons), InstantDispatch.Instance, Singletons);
         Assert.False(NewApplyCube().CanRun(tab));
      }

      private static UpdateCube NewUpdateCube() => new UpdateCube(new EditorViewModel(new StubFileSystem()), new StubFileSystem());

      private ViewPort CubeLikeTab(int length = 0x2000000) {
         var data = new byte[length];
         System.Text.Encoding.ASCII.GetBytes(CubeOptimization.CubeTitle).CopyTo(data, CubeOptimization.TitleAddress);
         System.Text.Encoding.ASCII.GetBytes("BPEE").CopyTo(data, CubeOptimization.GameCodeAddress);
         System.Text.Encoding.ASCII.GetBytes("RHHEXP").CopyTo(data, 0x100);
         return new ViewPort("Hack.gba", new PokemonModel(data, singletons: Singletons), InstantDispatch.Instance, Singletons);
      }

      [Fact]
      public void UpdateCube_ACubeRom_CanRun() => Assert.True(NewUpdateCube().CanRun(CubeLikeTab()));

      [Fact]
      public void UpdateCube_NothingOpen_CanNotRun() => Assert.False(NewUpdateCube().CanRun(null));

      [Fact]
      public void UpdateCube_ACubeOfTheWrongSize_CanNotRun() => Assert.False(NewUpdateCube().CanRun(CubeLikeTab(0x1000000)));

      [Fact]
      public void UpdateCube_AnEmeraldRomThatIsNotCube_CanNotRun() {
         var emerald = new byte[0x2000000];
         System.Text.Encoding.ASCII.GetBytes("BPEE").CopyTo(emerald, CubeOptimization.GameCodeAddress);
         var tab = new ViewPort("Hack.gba", new PokemonModel(emerald, singletons: Singletons), InstantDispatch.Instance, Singletons);
         Assert.False(NewUpdateCube().CanRun(tab));
      }

      private class NoResources : ICubeResources {
         public byte[] ReadPatch() => null;
         public string[] ReadToml() => null;
         public byte[] ReadVersions() => null;
         public string Location => "nowhere";
      }

      [Fact]
      public void UpdateCube_WithoutTheShippedFiles_SaysWhatIsMissing() {
         var editor = new EditorViewModel(new StubFileSystem());
         var item = new UpdateCube(editor, new StubFileSystem(), new NoResources());
         var result = item.Run(CubeLikeTab()).GetAwaiter().GetResult();
         Assert.True(result.HasError);
         Assert.Contains(ShippedCubeResources.VersionsFileName, result.ErrorMessage);
      }
   }
}