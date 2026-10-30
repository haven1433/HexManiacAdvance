using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The buttons that edit the front and back picture of the boy and the girl ask the window for an image editor through the tab itself (the window ignores the data tab while another tab is shown).</summary>
   public class CharacterPictureEditButtonTests : BaseViewModelTestClass {
      [SkippableFact]
      public void RealCube_EditFrontAndBack_AskTheWindowForTheImageEditor() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(tomlPath)));
         model.InitializationWorkload.Wait();
         var port = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
         var tab = new CharacterCustomizationTab(port);
         var requested = new List<ITabContent>();
         tab.RequestTabChange += (sender, e) => { requested.Add(e.NewTab); e.RequestAccepted = true; };
         foreach (var panel in tab.Panels) {
            requested.Clear();
            panel.EditFront.Execute(null);
            Assert.Single(requested);
            Assert.IsType<ImageEditorViewModel>(requested[0]);
            requested.Clear();
            panel.EditBack.Execute(null);
            Assert.Single(requested);
         }
      }
   }
}
