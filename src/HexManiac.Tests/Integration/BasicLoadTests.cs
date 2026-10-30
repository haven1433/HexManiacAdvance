using HavenSoft.HexManiac.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class BasicLoadTests {
      private static readonly Singletons singletons = new Singletons();
      static BasicLoadTests() {
         if (Directory.Exists("sampleFiles")) {
            SampleFiles = Directory.EnumerateFiles("sampleFiles", "*.gba").Select(file => new object[] { file }).ToArray();
         } else {
            SampleFiles = Enumerable.Empty<object[]>();
         }
      }
      public static IEnumerable<object[]> SampleFiles { get; }

      [Theory]
      [MemberData(nameof(SampleFiles))]
      public void CanLoad(string file) {
         var data = File.ReadAllBytes(file);
         new HardcodeTablesModel(singletons, data);
      }
   }
}
