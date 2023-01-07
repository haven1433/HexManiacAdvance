using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;


namespace HavenSoft.HexManiac.Integration {
   public class IntegrationTests {
      public static Singletons singletons { get; } = new Singletons();
      private static readonly string fireredName = "sampleFiles/Pokemon FireRed.gba";
      private static readonly Lazy<ViewPort> lazyFireRed;

      public StubFileSystem FileSystem { get; } = new();
      public List<string> Errors { get; } = new();
      public List<string> Messages { get; } = new();

      static IntegrationTests() {
         lazyFireRed = new Lazy<ViewPort>(() => {
            var model = new HardcodeTablesModel(singletons, File.ReadAllBytes(fireredName), new StoredMetadata(new string[0]));
            return new ViewPort(fireredName, model, InstantDispatch.Instance, singletons);
         });
      }

      protected ViewPort LoadFireRed() {
         Skip.IfNot(File.Exists(fireredName));
         var model = new HardcodeTablesModel(singletons, File.ReadAllBytes(fireredName), new StoredMetadata(new string[0]));
         var vm = new ViewPort(fireredName, model, InstantDispatch.Instance, singletons, new(), FileSystem);
         vm.OnError += (sender, e) => Errors.Add(e);
         vm.OnMessage += (sender, e) => Messages.Add(e);
         return vm;
      }

      protected ViewPort LoadReadOnlyFireRed() {
         Skip.IfNot(File.Exists(fireredName));
         return lazyFireRed.Value;
      }

   }
}
