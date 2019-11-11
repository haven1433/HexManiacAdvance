using HavenSoft.HexManiac.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class BasicLoadTests {
      static BasicLoadTests() {
         SampleFiles = Directory.EnumerateFiles("sampleFiles", "*.gba").Select(file => new object[] { file }).ToArray();
      }
      public static IEnumerable<object[]> SampleFiles { get; }

      [Theory]
      [MemberData(nameof(SampleFiles))]
      public void CanLoad(string file) {
         var data = File.ReadAllBytes(file);
         var model = new HardcodeTablesModel(data);
      }
   }
}
