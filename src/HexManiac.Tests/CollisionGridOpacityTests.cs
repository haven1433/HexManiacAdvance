using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The View menu and the slider in the map editor change one value, the opacity of the movement-permission grid.
   /// That value is static (shared by every map), so these tests stay in one class: xunit runs the tests of a class one at a time.
   /// </summary>
   public class CollisionGridOpacityTests : IDisposable {
      public CollisionGridOpacityTests() => BlockMapViewModel.CollisionGridOpacity = BlockMapViewModel.DefaultCollisionGridOpacity;

      public void Dispose() => BlockMapViewModel.CollisionGridOpacity = BlockMapViewModel.DefaultCollisionGridOpacity;

      private static EditorViewModel CreateEditor(params string[] settings) {
         var fileSystem = new StubFileSystem { MetadataFor = name => settings.Length == 0 ? null : settings };
         return new EditorViewModel(fileSystem);
      }

      [Fact]
      public void DefaultOpacity_DrawsTheGridAsDarkAsItWasBeforeTheSettingExisted() {
         Assert.Equal(40, BlockMapViewModel.DefaultCollisionGridOpacity);
         Assert.Equal(12, BlockMapViewModel.CollisionHighlightStrength);
         Assert.Equal(40, CreateEditor().CollisionGridOpacity);
      }

      [Fact]
      public void Opacity_IsInPercent_AndTheStrengthIsTheDarkenAmountOutOf31() {
         BlockMapViewModel.CollisionGridOpacity = 0;
         Assert.Equal(0, BlockMapViewModel.CollisionHighlightStrength);
         BlockMapViewModel.CollisionGridOpacity = 10;
         Assert.Equal(3, BlockMapViewModel.CollisionHighlightStrength);
         BlockMapViewModel.CollisionGridOpacity = 50;
         Assert.Equal(16, BlockMapViewModel.CollisionHighlightStrength);
         BlockMapViewModel.CollisionGridOpacity = 100;
         Assert.Equal(31, BlockMapViewModel.CollisionHighlightStrength);
      }

      [Fact]
      public void MenuChoices_IncludeTheDefault_AndAreInOrder() {
         var choices = EditorViewModel.CollisionGridOpacityChoices;
         Assert.Contains(BlockMapViewModel.DefaultCollisionGridOpacity, choices);
         Assert.Equal(choices.OrderBy(percent => percent), choices);
         Assert.Equal(choices.Count, choices.Distinct().Count());
         Assert.All(choices, percent => Assert.InRange(percent, 1, 100));
      }

      [Fact]
      public void SettingTheOpacity_ChangesTheSharedValue_AndNotifies() {
         var editor = CreateEditor();
         var notifications = new List<string>();
         editor.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         editor.CollisionGridOpacity = 75;

         Assert.Equal(75, editor.CollisionGridOpacity);
         Assert.Equal(75, BlockMapViewModel.CollisionGridOpacity);
         Assert.Equal(1, notifications.Count(name => name == nameof(EditorViewModel.CollisionGridOpacity)));
      }

      [Fact]
      public void SettingTheSameOpacity_DoesNotNotify() {
         var editor = CreateEditor();
         var notifications = new List<string>();
         editor.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         editor.CollisionGridOpacity = editor.CollisionGridOpacity;

         Assert.DoesNotContain(nameof(EditorViewModel.CollisionGridOpacity), notifications);
      }

      [Fact]
      public void Opacity_IsLimitedToZeroThroughOneHundred() {
         var editor = CreateEditor();
         editor.CollisionGridOpacity = 250;
         Assert.Equal(100, editor.CollisionGridOpacity);
         editor.CollisionGridOpacity = -5;
         Assert.Equal(0, editor.CollisionGridOpacity);
      }

      [Fact]
      public void Opacity_IsSavedWithTheApplicationSettings() {
         string[] saved = null;
         var fileSystem = new StubFileSystem { SaveMetadata = (name, lines) => { saved = lines; return true; } };
         var editor = new EditorViewModel(fileSystem);
         editor.CollisionGridOpacity = 25;

         editor.WriteAppLevelMetadata();

         Assert.Contains("CollisionGridOpacity = 25", saved);
         // saved next to the other View options
         Assert.True(Array.IndexOf(saved, "CollisionGridOpacity = 25") > Array.IndexOf(saved, "[GeneralSettings]"));
      }

      [Fact]
      public void Opacity_IsLoadedFromTheApplicationSettings() {
         var editor = CreateEditor("[GeneralSettings]", "AnimateScroll = True", "CollisionGridOpacity = 75");

         Assert.Equal(75, editor.CollisionGridOpacity);
         Assert.Equal(75, BlockMapViewModel.CollisionGridOpacity);
      }

      [Fact]
      public void SavedOpacity_SurvivesARestart() {
         string[] saved = null;
         var firstRun = new EditorViewModel(new StubFileSystem { SaveMetadata = (name, lines) => { saved = lines; return true; } });
         firstRun.CollisionGridOpacity = 10;
         firstRun.WriteAppLevelMetadata();

         BlockMapViewModel.CollisionGridOpacity = BlockMapViewModel.DefaultCollisionGridOpacity; // the next run starts with the default
         var secondRun = CreateEditor(saved);

         Assert.Equal(10, secondRun.CollisionGridOpacity);
      }

      [Fact]
      public void UnreadableOpacitySetting_KeepsTheDefault() {
         Assert.Equal(40, CreateEditor("[GeneralSettings]", "CollisionGridOpacity = banana").CollisionGridOpacity);
         Assert.Equal(40, CreateEditor("[GeneralSettings]", "CollisionGridOpacity = ").CollisionGridOpacity);
         Assert.Equal(40, CreateEditor("[GeneralSettings]", "AnimateScroll = False").CollisionGridOpacity);
      }

      [Fact]
      public void OutOfRangeOpacitySetting_IsLimited() {
         Assert.Equal(100, CreateEditor("[GeneralSettings]", "CollisionGridOpacity = 900").CollisionGridOpacity);
         BlockMapViewModel.CollisionGridOpacity = BlockMapViewModel.DefaultCollisionGridOpacity;
         Assert.Equal(0, CreateEditor("[GeneralSettings]", "CollisionGridOpacity = -3").CollisionGridOpacity);
      }
   }
}
