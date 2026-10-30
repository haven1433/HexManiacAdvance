using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.IO;
using System.Linq;
using Xunit;
namespace HavenSoft.HexManiac.Tests {
   /// <summary>Clicking a field of the overworld sprite (a struct reached through a pointer) keeps the tool as it is, so the field can be typed into.</summary>
   public class OverworldFieldFocusTests : BaseViewModelTestClass {
      [SkippableFact]
      public void ClickingANestedField_KeepsTheTool_AndTheFieldIsEditable() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         Skip.If(string.IsNullOrEmpty(path), "x");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML"))));
         model.InitializationWorkload.Wait();
         var vp = new ViewPort("x.gba", model, InstantDispatch.Instance, Singletons);
         vp.Goto.Execute("graphics.overworld.sprites/243");
         var tool = vp.Tools.TableTool;
         var table = (ITableRun)model.GetTable("graphics.overworld.sprites");
         string Describe() => $"name={tool.CurrentElementName} groups={tool.Groups.Count} members={tool.Groups.Sum(g => g.Members.Count)} sel={vp.SelectionStart}";
         Console.WriteLine("start: " + Describe());
         var fields = tool.Groups.SelectMany(g => g.Members).OfType<FieldArrayElementViewModel>().ToList();
         var width = fields.First(f => f.Name == "width");
         var before = model.ReadMultiByteValue(width.Start, width.Length);
         width.Focus();
         Console.WriteLine("after focus: " + Describe());
         Assert.Equal("graphics.overworld.sprites/243", tool.CurrentElementName);
         fields = tool.Groups.SelectMany(g => g.Members).OfType<FieldArrayElementViewModel>().ToList();
         width = fields.First(f => f.Name == "width");
         width.Content = "24";
         Assert.Equal(24, model.ReadMultiByteValue(width.Start, width.Length)); Assert.Equal("", width.ErrorText); Console.WriteLine($"width {before} -> {model.ReadMultiByteValue(width.Start, width.Length)} error='{width.ErrorText}' | {Describe()}");
         var pal = tool.Groups.SelectMany(g => g.Members).OfType<FieldArrayElementViewModel>().First(f => f.Name == "paletteid");
         pal.Content = "0x1100";
         Assert.Equal(0x1100, model.ReadMultiByteValue(pal.Start, pal.Length));
         Assert.Equal("graphics.overworld.sprites/243", tool.CurrentElementName);
         Console.WriteLine($"paletteid content now {pal.Content} model {model.ReadMultiByteValue(pal.Start, pal.Length):X} error='{pal.ErrorText}' | {Describe()}");
         // typing digit by digit, the way the text box does, with the tool rebuilding in between
         FieldArrayElementViewModel Find(string name) => tool.Groups.SelectMany(g => g.Members).OfType<FieldArrayElementViewModel>().First(f => f.Name == name);
         foreach (var text in new[] { "1", "16", "160" }) {
            Find("height").Focus();
            Find("height").Content = text;
            tool.DataForCurrentRunChanged();
            Console.WriteLine($"typed {text}: field shows {Find("height").Content}, model {model.ReadMultiByteValue(Find("height").Start, 2)}");
         }
         Assert.Equal("160", Find("height").Content);
         Assert.Equal(160, model.ReadMultiByteValue(Find("height").Start, 2));
      }
   }
}
