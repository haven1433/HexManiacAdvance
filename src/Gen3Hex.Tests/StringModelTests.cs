using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using System;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class StringModelTests {
      [Fact]
      public void CanRecognizeString() {
         var buffer = new byte[0x100];
         var model = new PointerModel(buffer);

         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         model.ObserveRunWritten(new PCSRun(0x10, data.Length));

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(data.Length, run.Length);
      }

      [Fact]
      public void CanWriteString() {
         var buffer = new byte[0x100];
         var model = new PointerModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.Edit("\"Hello World!\"");

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(13, run.Length);
      }
   }
}
