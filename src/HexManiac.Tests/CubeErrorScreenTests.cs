using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class CubeErrorScreenTests : BaseViewModelTestClass {
      [SkippableFact]
      public void RealCube_TheScreenWrapperIsFound_AndDisabledOnce() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), "set HMA_INTRO_CUBE");
         var data = File.ReadAllBytes(path);
         var wrapper = CubeErrorScreen.Find(data);
         Console.WriteLine($"wrapper at {wrapper:X}");
         Assert.True(wrapper > 0);
         Assert.Equal(0xB40E, data[wrapper] | (data[wrapper + 1] << 8)); // push {r1,r2,r3}
         Assert.False(CubeErrorScreen.IsDisabled(data, wrapper));

         var model = new HardcodeTablesModel(Singletons, data, null);
         model.InitializationWorkload.Wait();
         Assert.True(CubeErrorScreen.Disable(model, new ModelDelta()));
         Assert.True(CubeErrorScreen.IsDisabled(model.RawData, wrapper));
         Assert.Equal(wrapper, CubeErrorScreen.Find(model.RawData)); // still found, so it is known to be disabled already
         Assert.False(CubeErrorScreen.Disable(model.RawData));
         // only those two bytes changed
         var original = File.ReadAllBytes(path);
         for (int i = 0; i < original.Length; i++) if (i != wrapper && i != wrapper + 1) Assert.True(original[i] == model.RawData[i], $"byte {i:X} changed");
         File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path), "CUBE_noscreen.gba"), model.RawData);
      }

      [Fact]
      public void ARomWithoutTheCode_IsLeftAlone() {
         Assert.Equal(-1, CubeErrorScreen.Find(new byte[0x10000]));
         Assert.False(CubeErrorScreen.Disable(new byte[0x10000]));
      }
   }
}
