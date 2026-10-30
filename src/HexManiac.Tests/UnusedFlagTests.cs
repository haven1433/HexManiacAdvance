using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class UnusedFlagTests : BaseViewModelTestClass {
      private static List<string> Names(int count, params (int index, string name)[] named) {
         var list = Enumerable.Range(0, count).Select(i => (string)null).ToList();
         foreach (var (index, name) in named) list[index] = name;
         return list;
      }


      [Fact]
      public void NamedFlags_CountAsUsed_UnusedAndCustomDoNot() {
         Model.SetList(new ModelDelta(), Flags.FlagListName, Names(0x40, (0x21, "FLAG_SYS_POKEDEX_GET"), (0x22, "FLAG_UNUSED_0x022"), (0x30, "FLAG_CUSTOM_000")), null, null);

         var reserved = Flags.GetReservedFlags(Model);

         Assert.Contains(0x21, reserved);
         Assert.DoesNotContain(0x22, reserved);
         Assert.DoesNotContain(0x30, reserved);
         Assert.DoesNotContain(0x23, reserved); // unnamed
      }

      [Fact]
      public void NewFlags_ComeFromTheCustomPool_WhenTheRomHasOne() {
         Model.SetList(new ModelDelta(), Flags.FlagListName, Names(0x40, (0x21, "FLAG_SYS_POKEDEX_GET"), (0x30, "FLAG_CUSTOM_000"), (0x31, "FLAG_CUSTOM_001")), null, null);
         var used = new HashSet<int>();

         var first = Flags.NextFreeFlag(Model, used);
         used.Add(first);
         var second = Flags.NextFreeFlag(Model, used);

         Assert.Equal(0x30, first);
         Assert.Equal(0x31, second);
      }

      [Fact]
      public void NewFlags_SkipNamedFlags_WhenThereIsNoCustomPool() {
         Model.SetList(new ModelDelta(), Flags.FlagListName, Names(0x40, (0x21, "FLAG_SYS_POKEDEX_GET"), (0x22, "FLAG_BADGE01_GET"), (0x23, "FLAG_UNUSED_0x023")), null, null);
         Assert.Equal(0x23, Flags.NextFreeFlag(Model, new HashSet<int>()));
      }

      [Fact]
      public void NewVariables_ComeFromTheCustomPool() {
         var names = Enumerable.Range(0, 0x4110).Select(i => (string)null).ToList();
         names[0x4034] = "VAR_SOMETHING_IMPORTANT";
         names[0x4100] = "VAR_CUSTOM_000";
         Model.SetList(new ModelDelta(), Flags.VariableListName, names, null, null);
         Assert.Equal(0x4100, Flags.NextFreeVariable(Model, new HashSet<int>()));
         Assert.Contains(0x4034, Flags.GetReservedVariables(Model));
      }
   }
}
