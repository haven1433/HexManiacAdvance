using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The expansion-only fields of a trainer's Pokemon ('* tera=Fire', '* dynamax=yes gmax=yes dmaxlevel=10') in the trainer team text.
   /// struct TrainerMon: byte 30 = teraType:5 gigantamaxFactor:1 shouldUseDynamax:1, byte 31 = dynamaxLevel:4.
   /// Runs against the real CUBE ROM when HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML point at it. Skipped otherwise.
   /// </summary>
   public class ExpansionTrainerGimmickTests {
      private const int Roxanne = 265, Calvin = 318, TypeFire = 11, TypeWater = 12;
      private const int TeraDynamax = 30, DynamaxLevel = 31, Size = ExpansionTrainerTeamRun.ElementSize;

      private readonly IDataModel model;
      private readonly ModelDelta token = new();

      public ExpansionTrainerGimmickTests() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(toml)));
         model.InitializationWorkload.Wait();
      }

      private ExpansionTrainerTeamRun Team(int trainer) => (ExpansionTrainerTeamRun)model.GetNextRun(model.GetTableModel(HardcodeTablesModel.TrainerTableName)[trainer].GetAddress("pokemon"));

      private ExpansionTrainerTeamRun Write(int trainer, string text) {
         var run = Team(trainer);
         var newRun = (ExpansionTrainerTeamRun)run.DeserializeRun(text, token, out _, out _);
         model.ObserveRunWritten(token, newRun);
         return Team(trainer);
      }

      /// <summary>The team text with an extra line added at the end of Pokemon #index.</summary>
      private static string AddLine(string text, int index, string line) {
         var blocks = text.Replace("\r", "").TrimEnd('\n').Split("\n\n").ToList();
         blocks[index] = blocks[index] + "\n" + line;
         return string.Join("\n\n", blocks) + "\n";
      }

      private byte Byte(ExpansionTrainerTeamRun run, int mon, int offset) => model[run.Start + mon * Size + offset];

      [SkippableTheory]
      [InlineData("* tera=Fire")]
      [InlineData("* tera=fire")]
      [InlineData("* tera=FIRE")]
      [InlineData("* tera=\"Fire\"")]
      [InlineData("Tera Type: Fire")]
      [InlineData("* tera = Fire")]
      [InlineData("* tera: Fire")]
      [InlineData("* Tera Type: Fire")]
      [InlineData("* teraType=Fire")]
      [InlineData("* tera_type=Fire")]
      [InlineData("* tera=TYPE_FIRE")]
      [InlineData("* tera=Fir")]
      [InlineData("* tera=11")]
      public void Tera_IsWrittenAndReadBack(string line) {
         var before = Team(Roxanne);
         Assert.Equal(0, Byte(before, 0, TeraDynamax) & 31);

         var run = Write(Roxanne, AddLine(before.SerializeRun(), 0, line));

         Assert.Equal(TypeFire, Byte(run, 0, TeraDynamax) & 31);
         Assert.Equal(0, Byte(run, 1, TeraDynamax)); // the second Geodude has no '*' line
         var text = run.SerializeRun();
         Assert.Contains("* tera=Fire", text);
         Assert.Single(text.Split('\n'), l => l.Contains("tera="));
      }

      [SkippableFact]
      public void Dynamax_IsWrittenAndReadBack() {
         var run = Write(Roxanne, AddLine(Team(Roxanne).SerializeRun(), 2, "* dynamax=yes gmax=yes dmaxlevel=10"));

         Assert.Equal(0x60, Byte(run, 2, TeraDynamax) & 0x60);
         Assert.Equal(10, Byte(run, 2, DynamaxLevel) & 15);
         Assert.Contains("* gmax=yes dynamax=yes", run.SerializeRun());

         run = Write(Roxanne, AddLine(Team(Roxanne).SerializeRun().Replace("* gmax=yes dynamax=yes", ""), 2, "* dynamax=yes dmaxlevel=3"));
         Assert.Equal(3, Byte(run, 2, DynamaxLevel) & 15);
      }

      [SkippableTheory]
      [InlineData("* gmax=yes", 0x60, 10)]
      [InlineData("* gigantamax=yes", 0x60, 10)]
      [InlineData("Gigantamax: Yes", 0x60, 10)]
      [InlineData("* dmaxlevel=5", 0x40, 5)]
      [InlineData("Dynamax Level: 5", 0x40, 5)]
      [InlineData("* Dynamax Level=5", 0x40, 5)]
      [InlineData("* dynamax", 0x40, 10)]
      [InlineData("* dynamax gmax", 0x60, 10)]
      [InlineData("* dynamax=true, gmax=true", 0x60, 10)]
      [InlineData("* gmax=yes dynamax=no", 0x20, 10)]
      [InlineData("* dynamax=no dmaxlevel=4", 0x00, 4)]
      public void GigantamaxOrDynamaxLevel_AlsoTurnsOnDynamax_LikeTrainerproc(string line, int bits, int level) {
         var run = Write(Roxanne, AddLine(Team(Roxanne).SerializeRun(), 0, line));

         Assert.Equal(bits, Byte(run, 0, TeraDynamax) & 0x60);
         Assert.Equal(level, Byte(run, 0, DynamaxLevel) & 15);
         var bytes = model.RawData.Skip(run.Start).Take(run.Length).ToArray();
         run = Write(Roxanne, run.SerializeRun()); // the text it shows writes the same bytes
         Assert.Equal(bytes, model.RawData.Skip(run.Start).Take(run.Length).ToArray());
      }

      [SkippableTheory]
      [InlineData("* tera=Fyre")]
      [InlineData("* tera=x")]
      public void UnknownTeraType_IsNotWritten_AndIsMarked(string line) {
         var text = AddLine(Team(Roxanne).SerializeRun(), 0, line);

         var run = Write(Roxanne, text);

         Assert.Equal(0, Byte(run, 0, TeraDynamax) & 31);
         var errors = run.PreFormatter.FindErrors(text.Replace("\n", Environment.NewLine)).ToList();
         var error = Assert.Single(errors);
         Assert.Equal(SegmentType.Error, error.Type);
         Assert.Equal(text.Replace("\r", "").Split('\n').ToList().FindIndex(l => l == line), error.Line);
      }

      [SkippableFact]
      public void UnknownKey_IsMarked() {
         var text = AddLine(Team(Roxanne).SerializeRun(), 0, "* terra=Fire dynamax=yes");

         var errors = Team(Roxanne).PreFormatter.FindErrors(text.Replace("\n", Environment.NewLine)).ToList();

         var error = Assert.Single(errors);
         Assert.Equal(2, error.Start);
         Assert.Equal("terra=Fire".Length, error.Length);
      }

      [SkippableFact]
      public void MapEditor_UsesTheExpansionErrorMarks() {
         Assert.IsType<ExpansionTrainerTextFormatter>(ExpansionTrainerTeamRun.TeamFormatterFor(model));
         var (objectEvent, _) = FindRouteTrainer(Calvin);
         Assert.IsType<ExpansionTrainerTextFormatter>(objectEvent.TrainerContent.PreFormatter);
      }

      [SkippableFact]
      public void SerializedText_RoundTripsWithoutChanges() {
         var text = AddLine(AddLine(Team(Roxanne).SerializeRun(), 0, "* tera=Water"), 2, "* dynamax=yes gmax=yes dmaxlevel=7");
         var run = Write(Roxanne, text);
         var bytes = model.RawData.Skip(run.Start).Take(run.Length).ToArray();
         var serialized = run.SerializeRun();

         run = Write(Roxanne, serialized);

         Assert.Equal(bytes, model.RawData.Skip(run.Start).Take(run.Length).ToArray());
         Assert.Equal(serialized, run.SerializeRun());
         Assert.Equal(TypeWater, Byte(run, 0, TeraDynamax) & 31);
      }

      [SkippableFact]
      public void RemovingTheLine_ClearsTheGimmick() {
         var original = Team(Roxanne).SerializeRun();
         Write(Roxanne, AddLine(AddLine(original, 0, "* tera=Fire"), 2, "* dynamax=yes gmax=yes"));

         var run = Write(Roxanne, original);

         Assert.Equal(0, Byte(run, 0, TeraDynamax));
         Assert.Equal(0, Byte(run, 2, TeraDynamax));
         Assert.Equal(original, run.SerializeRun());
      }

      [SkippableFact]
      public void NewPokemon_DoesNotCopyTheGimmickOfThePokemonAboveIt() {
         var text = Team(Calvin).SerializeRun(); // one Poochyena
         text = AddLine(text, 0, "* tera=Fire dynamax=yes gmax=yes");

         var run = Write(Calvin, text + "\n7 Zigzagoon (IVs=0)\n");

         Assert.Equal(2, run.ElementCount);
         Assert.Equal(0x60 | TypeFire, Byte(run, 0, TeraDynamax));
         Assert.Equal(0, Byte(run, 1, TeraDynamax));
         Assert.Single(run.SerializeRun().Split('\n'), l => l.Contains("tera="));
      }

      [SkippableFact]
      public void TypingCharacterByCharacter_EndsWithTheTypedValue() {
         var start = Team(Roxanne).SerializeRun();
         var full = AddLine(start, 0, "* tera=Fire");
         var at = full.IndexOf("* tera=Fire");
         for (int i = 1; i <= "* tera=Fire".Length; i++) Write(Roxanne, full.Substring(0, at + i) + full.Substring(at + "* tera=Fire".Length));

         var run = Team(Roxanne);
         Assert.Equal(TypeFire, Byte(run, 0, TeraDynamax) & 31);
         Assert.Equal(0, Byte(run, 1, TeraDynamax) & 31);
      }

      [SkippableFact]
      public void MapEventTeamEditor_CommitsTheTera() {
         var (objectEvent, trainer) = FindRouteTrainer(Calvin);
         Assert.Equal(Calvin, trainer);

         var editor = objectEvent.TrainerContent;
         editor.Content = AddLine(editor.Content, 0, "* tera=Fire dynamax=yes");

         var run = Team(Calvin);
         Assert.Equal(0x40 | TypeFire, Byte(run, 0, TeraDynamax));
         Assert.Contains("* tera=Fire dynamax=yes", run.SerializeRun());
      }

      [SkippableFact]
      public void TableToolTeamEditor_TypingTheLine_CommitsAndKeepsTheText() {
         var viewPort = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         var trainerStart = model.GetTableModel(HardcodeTablesModel.TrainerTableName)[Roxanne].Start;
         viewPort.Goto.Execute(trainerStart);
         var team = viewPort.Tools.TableTool.Children.OfType<TrainerPokemonTeamElementViewModel>().Single();

         var full = AddLine(team.Content, 0, "* tera=Fire");
         var at = full.IndexOf("* tera=Fire");
         for (int i = 1; i <= "* tera=Fire".Length; i++) team.Content = full.Substring(0, at + i) + full.Substring(at + "* tera=Fire".Length);

         Assert.Equal(full, team.Content);
         Assert.Equal(TypeFire, Byte(Team(Roxanne), 0, TeraDynamax) & 31);

         viewPort.Goto.Execute(model.GetTableModel(HardcodeTablesModel.TrainerTableName)[Calvin].Start);
         viewPort.Goto.Execute(trainerStart);
         team = viewPort.Tools.TableTool.Children.OfType<TrainerPokemonTeamElementViewModel>().Single();
         Assert.Contains("* tera=Fire", team.Content);
      }

      private const string CharizardPaste = "50 Charizard (IVs=31)\n- Flamethrower\n- Air Slash\n- Roost\n- Solar Beam\n* tera=Fire nature=Timid";

      [SkippableTheory]
      [InlineData(117, false)]
      [InlineData(117, true)]
      [InlineData(Roxanne, false)]
      [InlineData(Calvin, true)]
      public void TableToolTeamEditor_PasteOverTeam_KeepsMovesAndTrainer(int trainer, bool crlf) {
         var viewPort = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var trainerStart = trainers[trainer].Start;
         var length = trainers.Run.ElementLength;
         viewPort.Goto.Execute(trainerStart);
         var childCount = viewPort.Tools.TableTool.Children.Count;
         var team = viewPort.Tools.TableTool.Children.OfType<TrainerPokemonTeamElementViewModel>().Single();
         var before = model.RawData.Skip(trainerStart).Take(length).ToArray();

         var paste = crlf ? CharizardPaste.Replace("\n", "\r\n") : CharizardPaste;
         team.Content = paste;
         team.Content = paste.Replace("tera=Fire", "tera=Water");

         var run = Team(trainer);
         var text = run.SerializeRun();
         Assert.True(Enumerable.Range(0, 4).All(i => model.ReadMultiByteValue(run.Start + 12 + 2 * i, 2) != 0), "moves lost: " + text);
         Assert.Contains("- Flamethrower", text);
         Assert.Contains("- \"Solar Beam\"", text);
         Assert.Equal(TypeWater, Byte(run, 0, TeraDynamax) & 31);
         var after = model.RawData.Skip(trainerStart).Take(length).ToArray();
         var teamField = FieldOffset(trainers.Run, "pokemon"); var countField = FieldOffset(trainers.Run, "pokemonCount");
         var diffs = Enumerable.Range(0, length).Where(i => before[i] != after[i] && !(i >= teamField && i < teamField + 4) && i != countField).ToList();
         Assert.True(diffs.Count == 0, "trainer bytes changed at " + string.Join(",", diffs));
         Assert.Equal(1, after[countField]);
         Assert.Equal(childCount, viewPort.Tools.TableTool.Children.Count);
      }

      /// <summary>
      /// A CUBE TOML saved by an older CUBHMA triggers HMA's "metadata upgrade" on load. CUBE has the vanilla Emerald game code, and the upgrade used to
      /// lay vanilla Emerald's tables and table groups over it: the trainer panel lost its layout (only aiFlags / team left) and the species / moves anchors
      /// were gone, so a pasted team lost its moves.
      /// </summary>
      [SkippableFact]
      public void OlderCubeToml_DoesNotGetTheVanillaUpgrade_AndAPastedTeamKeepsItsMoves() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlLines = File.ReadAllLines(Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML"))
            .Select(line => line.StartsWith("ApplicationVersion = ") ? "ApplicationVersion = '''0.1'''" : line).ToArray();
         var metadata = new StoredMetadata(tomlLines);
         Assert.True(StoredMetadata.NeedVersionUpdate(metadata.Version, BaseViewModelTestClass.Singletons.MetadataInfo.VersionNumber ?? "1"));
         Assert.True(metadata.DescribesExpansionLayout);
         var old = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path), metadata);
         old.InitializationWorkload.Wait();

         var trainerGroup = old.GetTableGroups(HardcodeTablesModel.TrainerTableName).First(group => group.GroupName == "Trainer");
         Assert.Contains("data.trainers.stats|name,class,sprite", trainerGroup.Tables[0]);
         Assert.True(old.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, ExpansionTrainerTeamRun.MovesTable) >= 0);
         Assert.True(old.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, ExpansionTrainerTeamRun.SpeciesTable) >= 0);

         var trainers = old.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var run = (ExpansionTrainerTeamRun)old.GetNextRun(trainers[117].GetAddress("pokemon"));
         var newRun = (ExpansionTrainerTeamRun)run.DeserializeRun(CharizardPaste, new ModelDelta(), out _, out _);
         var text = newRun.SerializeRun();
         Assert.StartsWith("50 Charizard (IVs=31)", text);
         Assert.Contains("- \"Solar Beam\"", text);
      }

      private static int FieldOffset(ITableRun table, string name) {
         int offset = 0;
         foreach (var segment in table.ElementContent) { if (segment.Name == name) return offset; offset += segment.Length; }
         return -1;
      }

      private (ObjectEventViewModel, int) FindRouteTrainer(int trainer) {
         var banks = model.GetTableModel("data.maps.banks");
         foreach (var bank in banks) {
            foreach (var map in (IEnumerable<ModelArrayElement>)bank.GetSubTable("maps") ?? Enumerable.Empty<ModelArrayElement>()) {
               var events = map.GetSubTable("map")?[0]?.GetSubTable("events")?[0];
               var objects = events?.GetSubTable("objects");
               if (objects == null) continue;
               foreach (var obj in objects) {
                  var script = obj.GetAddress("script");
                  if (script < 0 || script + 5 > model.Count || model[script] != 0x5C) continue;
                  if (model.ReadMultiByteValue(script + 3, 2) != trainer) continue;
                  var element = new ModelTable(model, objects.Run, () => token)[obj.ArrayIndex];
                  var vm = new ObjectEventViewModel(null, address => { }, element, null, new List<IPixelViewModel>(), null, null);
                  return (vm, trainer);
               }
            }
         }
         throw new InvalidOperationException("trainer not found on any map");
      }
   }
}
