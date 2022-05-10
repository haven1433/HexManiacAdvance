using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PatchTests :BaseViewModelTestClass {

      [Fact]
      public void EmptyPatch_TryApply_NoMetadataChange() {
         var sourceData = new byte[0x200].Fluent(array => array[1] = 1);
         var destinationData = new byte[0x200].Fluent(array => array[2] = 2);
         var patch = BuildUpsPatch(sourceData, destinationData);
         var patchFile = new LoadedFile("patch.ups", patch);
         FileSystem.ShowOptions = (_, _, _, _) => 1;

         ViewPort.TryImport(patchFile, FileSystem);

         Assert.True(ViewPort.ChangeHistory.IsSaved);
         Assert.False(ViewPort.ChangeHistory.HasDataChange);
      }

      private static byte[] BuildUpsPatch(byte[] source, byte[] destination) {
         var patch = new List<byte>();
         patch.AddRange("55 50 53 31".ToByteArray());
         patch.AddRange(DiffViewPort.WriteVariableWidthInteger(source.Length));
         patch.AddRange(DiffViewPort.WriteVariableWidthInteger(destination.Length));
         // TODO
         patch.AddRange(BitConverter.GetBytes(DiffViewPort.CalcCRC32(source)));
         patch.AddRange(BitConverter.GetBytes(DiffViewPort.CalcCRC32(destination)));
         patch.AddRange(BitConverter.GetBytes(DiffViewPort.CalcCRC32(patch.ToArray())));

         return patch.ToArray();
      }
   }
}
