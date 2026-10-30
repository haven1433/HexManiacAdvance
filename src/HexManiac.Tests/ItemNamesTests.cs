using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>One item whose name cannot be read (the placeholder item 0 of some hacks) must not remove the names of every other item.</summary>
   public class ItemNamesTests : BaseViewModelTestClass {
      [SkippableFact]
      public void RealCube_BadFirstNamePointer_KeepsOtherNames() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var data = File.ReadAllBytes(path);
         data[0x6DF4F4] = 0x78; data[0x6DF4F5] = 0x02; data[0x6DF4F6] = 0x00; data[0x6DF4F7] = 0xF8; // item 0's name pointer: <F8000278>
         var model = new HardcodeTablesModel(Singletons, data, new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var port = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         port.Goto.Execute("data.items.stats/12");
         var options = port.Tools.TableTool.CurrentElementSelector.AllOptions;
         Assert.Equal("\"Quick Ball\"", options[12].Text);
         Assert.Equal("0", options[0].Text);
      }
   }
}
