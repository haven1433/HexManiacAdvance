using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PatchTests :BaseViewModelTestClass {

      [Fact]
      public void EmptyPatch_TryApply_NoMetadataChange() {
         var sourceData = new byte[0x200].Fluent(array => array[1] = 1);
         var destinationData = new byte[0x200].Fluent(array => array[2] = 2);
         var patch = Patcher.BuildUpsPatch(sourceData, destinationData);
         var patchFile = new LoadedFile("patch.ups", patch);
         FileSystem.ShowOptions = (_, _, _, _) => 1;

         ViewPort.TryImport(patchFile, FileSystem);

         Assert.True(ViewPort.ChangeHistory.IsSaved);
         Assert.False(ViewPort.ChangeHistory.HasDataChange);
      }

      [Theory]
      [InlineData(0x1000000)]
      public void WriteVariableWidthInteger_ReadVariableWidthInteger_SameValue(int targetValue) {
         var content = Patcher.WriteVariableWidthInteger(targetValue).ToArray();

         int index = 0;
         var result = Patcher.ReadVariableWidthInteger(content, ref index);

         Assert.Equal(content.Length, index);
         Assert.Equal(result, targetValue);
      }
   }
}
