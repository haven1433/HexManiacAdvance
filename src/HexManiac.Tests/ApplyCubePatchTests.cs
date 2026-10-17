using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Utilities > Misc > "Apply CUBE": it patches the ROM that is open in the current tab (like "Apply HUBOL DPE CFRU"), is grayed out unless that ROM is the
   /// unmodified Emerald, and leaves the saving to the user. Run against a tiny stand-in for Emerald (0x200 bytes) and for CUBE (0x300 bytes),
   /// with a file system that only exists in memory.
   /// </summary>
   public class ApplyCubePatchTests {
      private const int VanillaLength = 0x200, CubeLength = 0x300, CodeAddress = 0xAC;
      private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), "cube-apply-test");

      private class TestResources : ICubeResources {
         public byte[] Patch { get; set; }
         public string[] Toml { get; set; }
         public string Location => "test-resources";
         public byte[] ReadPatch() => Patch;
         public string[] ReadToml() => Toml;
         public byte[] ReadVersions() => null;
      }

      /// <summary>Runs everything at once, but lets a test change the world just before the patch is built in the background.</summary>
      private class HookedDispatcher : IWorkDispatcher {
         public Action BeforeBackgroundWork { get; set; }
         /// <summary>When set, work for the UI thread waits in <see cref="Queued"/> until the test runs it.</summary>
         public bool QueueUiWork { get; set; }
         public List<Action> Queued { get; } = new List<Action>();
         public Task WaitForRenderingAsync() => Task.CompletedTask;
         public void BlockOnUIWork(Action action) => action();
         public Task DispatchWork(Action action) {
            if (QueueUiWork) Queued.Add(action); else action?.Invoke();
            return Task.CompletedTask;
         }
         public Task RunBackgroundWork(Action action) {
            var hook = BeforeBackgroundWork;
            BeforeBackgroundWork = null; // only the first piece of background work (the patch itself), not the model that loads afterwards
            hook?.Invoke();
            action?.Invoke();
            return Task.CompletedTask;
         }
         public IDelayWorkTimer CreateDelayTimer() => new ImmediateWorkTimer();
      }

      private readonly StubFileSystem fs = new StubFileSystem();
      private readonly List<string> savedRoms = new List<string>();
      private readonly Dictionary<string, byte[]> romContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
      private readonly Dictionary<string, string[]> savedToml = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
      private readonly HookedDispatcher dispatcher = new HookedDispatcher();

      private readonly byte[] vanilla = new byte[VanillaLength];
      private readonly byte[] cube = new byte[CubeLength];
      private readonly VanillaRomSpec spec;
      private readonly TestResources resources;
      private readonly EditorViewModel editor;
      private readonly ApplyCubePatch item;

      public static readonly string[] CubeToml = {
         "[General]",
         "ApplicationVersion = '''0.6.1'''",
         "",
         "[[NamedAnchors]]",
         "Name = '''cube.test.anchor'''",
         "Address = 0x000100",
         "Format = '''[a: b:]2'''",
      };

      private static string VanillaPath => Path.Combine(TestDirectory, "Emerald.gba");

      public ApplyCubePatchTests() {
         for (int i = 0; i < vanilla.Length; i++) vanilla[i] = (byte)(i * 7 + i / 13);
         Encoding.ASCII.GetBytes("BPEE").CopyTo(vanilla, CodeAddress);
         vanilla[0xBC] = 0;

         // CUBE stand-in: the same header, then new data, a block copied from the vanilla image, and a block repeated from itself.
         Array.Copy(vanilla, cube, 0x100);
         var anchorData = new byte[] { 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE };
         anchorData.CopyTo(cube, 0x100);
         Array.Copy(vanilla, 0x100, cube, 0x108, 0xF8);
         Array.Copy(cube, 0, cube, 0x200, 0x100);
         var patch = new BpsPatchTests.BpsBuilder(vanilla, cube)
            .SourceRead(0x100)
            .TargetRead(anchorData)
            .SourceCopy(0xF8, 0x100)
            .TargetCopy(0x100, 0)
            .Build();
         var gz = new MemoryStream();
         using (var zip = new GZipStream(gz, CompressionMode.Compress, leaveOpen: true)) zip.Write(patch, 0, patch.Length);

         resources = new TestResources { Patch = gz.ToArray(), Toml = CubeToml };
         spec = new VanillaRomSpec { Name = "Test Emerald", Length = VanillaLength, GameCode = "BPEE", Sha1 = EmeraldRomCheck.ComputeSha1(vanilla), Crc32 = (uint)Patcher.CalcCRC32(vanilla) };

         fs.Exists = name => romContents.ContainsKey(name) || savedToml.ContainsKey(name);
         fs.Save = file => {
            savedRoms.Add(file.Name);
            romContents[file.Name] = file.Contents;
            return true;
         };
         fs.SaveMetadata = (name, lines) => {
            savedToml[Path.ChangeExtension(name, ".toml")] = lines;
            return true;
         };
         fs.MetadataFor = name => savedToml.TryGetValue(Path.ChangeExtension(name, ".toml"), out var lines) ? lines : null;
         fs.LoadFile = name => romContents.TryGetValue(name, out var data) ? new LoadedFile(name, data) : null;

         editor = new EditorViewModel(fs, dispatcher);
         item = new ApplyCubePatch(editor, resources, spec);
      }

      private ViewPort AddTab(string path, byte[] data) {
         var model = new PokemonModel((byte[])data.Clone(), singletons: BaseViewModelTestClass.Singletons);
         var viewPort = new ViewPort(path, model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         editor.Add(viewPort);
         return viewPort;
      }

      private static byte[] WithByte(byte[] data, int index, byte value) {
         var copy = (byte[])data.Clone();
         copy[index] = value;
         return copy;
      }

      private static byte[] WithGameCode(byte[] data, string code) {
         var copy = (byte[])data.Clone();
         Encoding.ASCII.GetBytes(code).CopyTo(copy, CodeAddress);
         return copy;
      }

      private ViewPort SelectedViewPort => Assert.IsType<ViewPort>(editor.SelectedTab);

      // ---- registration -------------------------------------------------------------------------------------------

      [Fact]
      public void UtilitiesMisc_ListsApplyCube_ThatNeedsAnOpenTabLikeHubol() {
         var command = editor.QuickEditsMisc.OfType<ApplyCubePatch>().Single();

         Assert.Equal("Apply CUBE", command.Name);
         Assert.False(command is IStandaloneQuickEdit);
         Assert.False(string.IsNullOrWhiteSpace(command.Description));
         Assert.Contains("Save", command.Description);
         Assert.Single(editor.QuickEditsMisc.Where(edit => edit is ApplyCFRUPatch)); // the built-in HUBOL item it copies is still there
      }

      [Fact]
      public void RunQuickEdit_WithNoTabOpen_DoesNothing() {
         var command = editor.QuickEditsMisc.OfType<ApplyCubePatch>().Single();

         editor.RunQuickEdit(command);

         Assert.Equal(0, editor.Count);
      }

      // ---- when it is available ---------------------------------------------------------------------------------------------

      [Fact]
      public void NoTab_CanRun_IsFalse() {
         Assert.False(item.CanRun(null));
      }

      [Fact]
      public void VanillaTab_CanRun_IsTrue() {
         Assert.True(item.CanRun(AddTab(VanillaPath, vanilla)));
      }

      [Fact]
      public void VanillaTab_CanRunWithoutAKnownCrc_IsTrueAndOtherFilesStillFalse() {
         var withoutCrc = new ApplyCubePatch(editor, resources, new VanillaRomSpec { Name = "Test Emerald", Length = VanillaLength, GameCode = "BPEE", Sha1 = spec.Sha1 });

         Assert.True(withoutCrc.CanRun(AddTab(VanillaPath, vanilla)));
         Assert.False(withoutCrc.CanRun(AddTab(VanillaPath, WithByte(vanilla, 0x150, 0x99))));
      }

      [Fact]
      public void CubeTab_CanRun_IsFalse() {
         Assert.False(item.CanRun(AddTab(VanillaPath, cube)));
      }

      [Fact]
      public void TabsThatAreNotTheOriginalEmerald_CanRun_IsFalse() {
         var expanded = new byte[VanillaLength + 0x300];
         Array.Copy(vanilla, expanded, vanilla.Length);
         var headered = new byte[VanillaLength + 0x200];
         Array.Copy(vanilla, 0, headered, 0x200, vanilla.Length);
         Encoding.ASCII.GetBytes("BPEE").CopyTo(headered, CodeAddress);

         Assert.False(item.CanRun(AddTab(VanillaPath, WithGameCode(vanilla, "BPRE"))), "FireRed");
         Assert.False(item.CanRun(AddTab(VanillaPath, WithGameCode(vanilla, "AXVE"))), "Ruby");
         Assert.False(item.CanRun(AddTab(VanillaPath, WithGameCode(vanilla, "BPED"))), "another language of Emerald");
         Assert.False(item.CanRun(AddTab(VanillaPath, WithByte(vanilla, 0x1F0, 0x99))), "edited Emerald");
         Assert.False(item.CanRun(AddTab(VanillaPath, expanded)), "expanded Emerald");
         Assert.False(item.CanRun(AddTab(VanillaPath, headered)), "Emerald with a copier header");
         Assert.False(item.CanRun(AddTab(VanillaPath, new byte[0x40])), "a tiny file");
         Assert.False(item.CanRun(AddTab(VanillaPath, new byte[0])), "an empty file");
      }

      [Fact]
      public void VanillaTabIsEditedAndUndone_CanRun_FollowsTheContent() {
         var tab = AddTab(VanillaPath, vanilla);
         Assert.True(item.CanRun(tab));

         tab.Edit("FF");
         Assert.False(item.CanRun(tab));

         tab.Undo.Execute();
         Assert.True(item.CanRun(tab));
      }

      [Fact]
      public void VanillaTabIsEdited_TheMenuItemIsAskedAgain() {
         var command = editor.QuickEditsMisc.OfType<ApplyCubePatch>().Single();
         var tab = AddTab(VanillaPath, vanilla);
         var asked = 0;
         command.CanRunChanged += (sender, e) => asked++;

         tab.Edit("FF"); // the tab becomes "Emerald*"
         var askedAfterEdit = asked;
         tab.Undo.Execute(); // and "Emerald" again

         Assert.True(askedAfterEdit > 0);
         Assert.True(asked > askedAfterEdit);
      }

      [Fact]
      public void RealEmeraldSpec_HasAKnownCrc32_ThatIsCheapToCheck() {
         Assert.Equal(0x1F1C08FBu, VanillaRomSpec.EmeraldUsV10.Crc32);
      }

      // ---- the normal flow ------------------------------------------------------------------------------------------------

      [Fact]
      public void VanillaTab_Run_ReplacesTheTabWithCubeAndItsMetadata() {
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         Assert.True(result.IsWarning, result.ErrorMessage);
         Assert.Contains("Applied CUBE to Emerald.gba", result.ErrorMessage);
         Assert.Equal(1, editor.Count);
         var cubeTab = SelectedViewPort;
         Assert.NotSame(tab, cubeTab);
         Assert.Equal(Path.GetFullPath(VanillaPath), cubeTab.FullFileName);
         cubeTab.Model.InitializationWorkload.Wait();
         Assert.Equal(cube, cubeTab.Model.RawData);
         Assert.Equal(0x100, cubeTab.Model.GetAddressFromAnchor(new ModelDelta(), -1, "cube.test.anchor"));
      }

      [Fact]
      public void VanillaTab_Run_MarksTheTabChangedButOffersNoUndo() {
         var tab = AddTab(VanillaPath, vanilla);

         item.Run(tab).Wait();

         var cubeTab = SelectedViewPort;
         Assert.Equal("Emerald*", cubeTab.Name);
         Assert.False(cubeTab.ChangeHistory.IsSaved);
         Assert.True(cubeTab.ChangeHistory.HasDataChange);
         Assert.True(cubeTab.Save.CanExecute(fs));
         Assert.False(cubeTab.Undo.CanExecute(null));
      }

      [Fact]
      public void VanillaTab_Run_OffersSaveOnlyOnceTheNewRomHasLoaded() {
         var tab = AddTab(VanillaPath, vanilla);
         dispatcher.QueueUiWork = true; // the UI thread is busy: what is dispatched to it waits

         item.Run(tab).Wait();
         var cubeTab = SelectedViewPort;

         Assert.True(cubeTab.ChangeHistory.IsSaved); // saving now would write the metadata of a model that is still being built
         Assert.False(cubeTab.Save.CanExecute(fs));

         dispatcher.QueueUiWork = false;
         foreach (var work in dispatcher.Queued.ToList()) work?.Invoke();

         Assert.False(cubeTab.ChangeHistory.IsSaved);
         Assert.True(cubeTab.Save.CanExecute(fs));
         Assert.Equal("Emerald*", cubeTab.Name);
      }

      [Fact]
      public void VanillaTab_Run_WritesNothingUntilTheUserSaves() {
         var tab = AddTab(VanillaPath, vanilla);

         item.Run(tab).Wait();

         Assert.Empty(savedRoms);
         Assert.Empty(savedToml);
         Assert.Equal(vanilla, tab.Model.RawData); // the model of the old tab is untouched too
      }

      [Fact]
      public void VanillaTab_RunThenSave_WritesCubeAndItsTomlOverTheSameFile() {
         var tab = AddTab(VanillaPath, vanilla);
         item.Run(tab).Wait();
         var cubeTab = SelectedViewPort;
         cubeTab.Model.InitializationWorkload.Wait();

         editor.Save.Execute(null);

         Assert.Equal(new[] { VanillaPath }, savedRoms);
         Assert.Equal(cube, romContents[VanillaPath]);
         var tomlPath = Path.ChangeExtension(VanillaPath, ".toml");
         Assert.True(savedToml.ContainsKey(tomlPath));
         Assert.Contains("cube.test.anchor", string.Join(Environment.NewLine, savedToml[tomlPath]));
         Assert.True(cubeTab.ChangeHistory.IsSaved);
         Assert.Equal("Emerald", cubeTab.Name);
      }

      [Fact]
      public void VanillaTab_RunThenEdit_UndoStopsAtTheCubeTab() {
         var tab = AddTab(VanillaPath, vanilla);
         item.Run(tab).Wait();
         var cubeTab = SelectedViewPort;

         cubeTab.Edit("FF");
         Assert.True(cubeTab.Undo.CanExecute(null));
         cubeTab.Undo.Execute();

         Assert.False(cubeTab.Undo.CanExecute(null));
         Assert.False(cubeTab.ChangeHistory.IsSaved); // the file on disk still is the original ROM
         Assert.Equal(cube, cubeTab.Model.RawData);
      }

      [Fact]
      public void VanillaTabBetweenOthers_Run_KeepsThePlaceInTheTabRow() {
         var left = AddTab(Path.Combine(TestDirectory, "Left.gba"), new byte[0x40]);
         var tab = AddTab(VanillaPath, vanilla);
         var right = AddTab(Path.Combine(TestDirectory, "Right.gba"), new byte[0x40]);
         editor.SelectedIndex = 1;

         item.Run(tab).Wait();

         Assert.Equal(3, editor.Count);
         Assert.Same(left, editor[0]);
         Assert.Same(right, editor[2]);
         Assert.Equal("Emerald*", editor[1].Name);
         Assert.Equal(1, editor.SelectedIndex);
      }

      [Fact]
      public void OtherTabsOfTheSameRom_Run_AreClosedWithIt() {
         var tab = AddTab(VanillaPath, vanilla);
         var second = new ViewPort(VanillaPath, tab.Model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         editor.Add(second);
         Assert.Equal(2, editor.Count);

         item.Run(tab).Wait();

         Assert.Equal(1, editor.Count);
         Assert.Equal("Emerald*", editor[0].Name);
      }

      [Fact]
      public void AfterRun_TheCubeTabIsNotCompatibleAnymore() {
         var tab = AddTab(VanillaPath, vanilla);

         item.Run(tab).Wait();

         Assert.False(item.CanRun(SelectedViewPort));
      }

      [Fact]
      public void CubeTab_Run_SaysThereIsNothingToApply() {
         var tab = AddTab(VanillaPath, vanilla);
         item.Run(tab).Wait();

         var result = item.Run(SelectedViewPort).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("already is CUBE", result.ErrorMessage);
         Assert.Equal(1, editor.Count);
      }

      [Fact]
      public void TabWithUnsavedNames_Run_RefusesInsteadOfThrowingThemAway() {
         var tab = AddTab(VanillaPath, vanilla);
         tab.Edit("^my.anchor ");
         Assert.False(tab.ChangeHistory.IsSaved);
         Assert.False(tab.ChangeHistory.HasDataChange);
         Assert.True(item.CanRun(tab)); // the bytes are still the original Emerald

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("not saved yet", result.ErrorMessage);
         Assert.Same(tab, editor[0]);
         Assert.Equal(1, editor.Count);
      }

      // ---- the tab changes while CUBE is being built -----------------------------------------------------------------------

      [Fact]
      public void TabIsEditedWhileBuilding_Run_AppliesNothing() {
         var tab = AddTab(VanillaPath, vanilla);
         dispatcher.BeforeBackgroundWork = () => tab.Edit("FF");

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("changed while CUBE was being built", result.ErrorMessage);
         Assert.Same(tab, editor[0]);
         Assert.Equal(1, editor.Count);
      }

      [Fact]
      public void TabIsClosedWhileBuilding_Run_AppliesNothing() {
         var tab = AddTab(VanillaPath, vanilla);
         dispatcher.BeforeBackgroundWork = () => tab.Close.Execute(fs);

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("closed while CUBE was being built", result.ErrorMessage);
         Assert.Equal(0, editor.Count);
      }

      // ---- inputs that are not a vanilla Emerald (the menu item is grayed out, but the menu can be a moment behind) ---------

      private ErrorInfo RunOnTabWith(byte[] contents) {
         var tab = AddTab(VanillaPath, contents);
         var result = item.Run(tab).Result;
         Assert.Same(tab, editor[0]); // nothing was replaced
         Assert.Equal(1, editor.Count);
         Assert.Empty(savedRoms);
         return result;
      }

      [Fact]
      public void OtherGame_Run_SaysWhichGameItIs() {
         var result = RunOnTabWith(WithGameCode(vanilla, "BPRE"));

         Assert.False(result.IsWarning);
         Assert.Contains("FireRed", result.ErrorMessage);
      }

      [Fact]
      public void OtherLanguageOfEmerald_Run_SaysSo() {
         var result = RunOnTabWith(WithGameCode(vanilla, "BPED"));

         Assert.Contains("BPED", result.ErrorMessage);
         Assert.Contains("language", result.ErrorMessage);
      }

      [Fact]
      public void ModifiedEmerald_Run_SaysItIsNotTheOriginal() {
         var result = RunOnTabWith(WithByte(vanilla, 0x1F0, 0x99));

         Assert.False(result.IsWarning);
         Assert.Contains("modified", result.ErrorMessage);
      }

      [Fact]
      public void ExpandedEmerald_Run_SaysItIsTheWrongSize() {
         var expanded = new byte[VanillaLength + 0x300];
         Array.Copy(vanilla, expanded, vanilla.Length);

         var result = RunOnTabWith(expanded);

         Assert.Contains("expanded", result.ErrorMessage);
      }

      [Fact]
      public void RomWithCopierHeader_Run_ExplainsTheHeader() {
         var headered = new byte[VanillaLength + 0x200];
         Array.Copy(vanilla, 0, headered, 0x200, vanilla.Length);
         Encoding.ASCII.GetBytes("BPEE").CopyTo(headered, CodeAddress); // the game code is where a headerless ROM has it, the length is off

         var result = RunOnTabWith(headered);

         Assert.Contains("header", result.ErrorMessage);
      }

      [Fact]
      public void FileThatIsNotARom_Run_SaysSo() {
         var result = RunOnTabWith(new byte[0x40]);

         Assert.Contains("too small", result.ErrorMessage);
      }

      [Fact]
      public void ChangedCube_Run_SaysItIsProbablyCube() {
         var result = RunOnTabWith(WithByte(cube, 0x250, 0x77));

         Assert.Contains("probably already CUBE", result.ErrorMessage);
      }

      [Fact]
      public void NoTab_Run_ExplainsWhatToDo() {
         var result = item.Run(null).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("open in the current tab", result.ErrorMessage);
         Assert.Equal(0, editor.Count);
      }

      // ---- the patch files ------------------------------------------------------------------------------------------------

      [Fact]
      public void PatchFilesMissing_Run_ExplainsWhatIsMissingAndWhere() {
         resources.Patch = null;
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains(ShippedCubeResources.PatchFileName, result.ErrorMessage);
         Assert.Contains("test-resources", result.ErrorMessage);
         Assert.Same(tab, editor[0]);
      }

      [Fact]
      public void TomlMissing_Run_ExplainsWhatIsMissing() {
         resources.Toml = null;
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         Assert.Contains(ShippedCubeResources.TomlFileName, result.ErrorMessage);
         Assert.Same(tab, editor[0]);
      }

      [Fact]
      public void DamagedPatchFile_Run_ReportsItAndChangesNothing() {
         var damaged = BpsPatch.DecompressIfGzip(resources.Patch);
         damaged[damaged.Length - 20] ^= 0x10;
         resources.Patch = damaged;
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("damaged", result.ErrorMessage);
         Assert.Same(tab, editor[0]);
         Assert.Equal(1, editor.Count);
      }

      [Fact]
      public void PatchForAnotherRom_Run_ReportsItAndChangesNothing() {
         // a patch whose source is not what the "vanilla" spec describes: same length, other content, so the ROM check passes but the BPS source checksum does not
         var otherSource = (byte[])vanilla.Clone();
         otherSource[0x1F0] ^= 0xFF;
         resources.Patch = new BpsPatchTests.BpsBuilder(otherSource, cube).SourceRead(0x100).TargetRead(new byte[CubeLength - 0x100]).Build();
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("Could not build CUBE", result.ErrorMessage);
         Assert.Same(tab, editor[0]);
      }

      [Fact]
      public void PatchWhoseResultHasTheWrongChecksum_Run_ChangesNothing() {
         // the patch ran, but what it made is not what its author recorded: the tab must keep the original ROM
         resources.Patch = new BpsPatchTests.BpsBuilder(vanilla, cube).SourceRead(0x100).TargetRead(new byte[CubeLength - 0x100]).Build(targetCrc: 0x12345678);
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         Assert.False(result.IsWarning);
         Assert.Contains("Could not build CUBE", result.ErrorMessage);
         Assert.Same(tab, editor[0]);
         Assert.Equal(vanilla, tab.Model.RawData);
         Assert.Empty(savedRoms);
      }

      [Fact]
      public void BrokenToml_Run_NeverLosesTheTab() {
         resources.Toml = new[] { "[[NamedAnchors]]", "Name = '''broken'''", "Address = 0x5", "Format = '''[a: b:'''", "[[[[" };
         var tab = AddTab(VanillaPath, vanilla);

         var result = item.Run(tab).Result;

         // however the metadata reader reacts to the garbage (an error message, or a tab without anchors), there still is exactly one tab for the file
         Assert.Equal(1, editor.Count);
         Assert.True(result.HasError);
      }

      [Fact]
      public void RunTwiceInARow_SecondRunIsRefusedWhileTheFirstIsRunning() {
         var tab = AddTab(VanillaPath, vanilla);
         Task<ErrorInfo> second = null;
         dispatcher.BeforeBackgroundWork = () => second = item.Run(tab);

         var first = item.Run(tab).Result;

         Assert.True(first.IsWarning, first.ErrorMessage);
         Assert.Contains("already being applied", second.Result.ErrorMessage);
         Assert.Equal(1, editor.Count);
      }
   }

   /// <summary>The 'unsaved' tag of the change history that a rebuilt tab (Apply CUBE) uses.</summary>
   public class ChangeHistoryUnsavedTagTests {
      private readonly PokemonModel model = new PokemonModel(new byte[0x20], singletons: BaseViewModelTestClass.Singletons);
      private readonly ChangeHistory<ModelDelta> history;

      public ChangeHistoryUnsavedTagTests() {
         history = new ChangeHistory<ModelDelta>(change => change.Revert(model));
      }

      [Fact]
      public void FreshHistory_IsSaved() {
         Assert.True(history.IsSaved);
         Assert.False(history.HasDataChange);
      }

      [Fact]
      public void TagAsUnsaved_MakesTheHistoryUnsavedWithADataChangeButNothingToUndo() {
         history.TagAsUnsaved();

         Assert.False(history.IsSaved);
         Assert.True(history.HasDataChange);
         Assert.False(history.Undo.CanExecute(null));
      }

      [Fact]
      public void TagAsUnsaved_NotifiesListeners() {
         var notified = new List<string>();
         history.PropertyChanged += (sender, e) => notified.Add(e.PropertyName);

         history.TagAsUnsaved();

         Assert.Contains(nameof(history.IsSaved), notified);
         Assert.Contains(nameof(history.HasDataChange), notified);
      }

      [Fact]
      public void TagAsSaved_AfterTagAsUnsaved_IsSavedAgain() {
         history.TagAsUnsaved();

         history.TagAsSaved();

         Assert.True(history.IsSaved);
         Assert.False(history.HasDataChange);
      }

      [Fact]
      public void EditThenUndo_AfterTagAsUnsaved_StaysUnsaved() {
         history.TagAsUnsaved();
         history.CurrentChange.ChangeData(model, 2, 0x55);
         history.ChangeCompleted();
         Assert.True(history.Undo.CanExecute(null));

         history.Undo.Execute(null);

         Assert.Equal(0, model[2]);
         Assert.False(history.Undo.CanExecute(null));
         Assert.False(history.IsSaved); // the content on disk is still not what is in the tab
      }

      [Fact]
      public void TagAsUnsavedTwice_IsTheSameAsOnce() {
         history.TagAsUnsaved();
         history.TagAsUnsaved();

         Assert.False(history.IsSaved);
         history.TagAsSaved();
         Assert.True(history.IsSaved);
      }
   }

   public class EmeraldRomCheckTests {
      private static readonly VanillaRomSpec Spec = new VanillaRomSpec { Name = "Test Emerald", Length = 0x400, GameCode = "BPEE", Sha1 = null };

      private static byte[] Rom(int length, string code) {
         var rom = new byte[length];
         for (int i = 0; i < rom.Length; i++) rom[i] = (byte)(i * 3);
         Encoding.ASCII.GetBytes(code).CopyTo(rom, 0xAC);
         return rom;
      }

      private static VanillaRomSpec SpecFor(byte[] rom) => new VanillaRomSpec { Name = "Test Emerald", Length = rom.Length, GameCode = "BPEE", Sha1 = EmeraldRomCheck.ComputeSha1(rom) };

      [Fact]
      public void Sha1_MatchesTheKnownValueOfAnEmptyAndASimpleInput() {
         Assert.Equal("da39a3ee5e6b4b0d3255bfef95601890afd80709", EmeraldRomCheck.ComputeSha1(new byte[0]));
         Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", EmeraldRomCheck.ComputeSha1(Encoding.ASCII.GetBytes("abc")));
      }

      [Fact]
      public void EmeraldUsV10_SpecMatchesTheRealRom() {
         var realSpec = VanillaRomSpec.EmeraldUsV10;

         Assert.Equal(0x1000000, realSpec.Length);
         Assert.Equal("BPEE", realSpec.GameCode);
         Assert.Equal("f3ae088181bf583e55daf962a92bb46f4f1d07b7", realSpec.Sha1);
      }

      [Fact]
      public void ExactRom_IsVanilla() {
         var rom = Rom(0x400, "BPEE");

         Assert.True(EmeraldRomCheck.Check(rom, SpecFor(rom)).IsVanilla);
      }

      [Fact]
      public void OneChangedByte_IsModified() {
         var rom = Rom(0x400, "BPEE");
         var romSpec = SpecFor(rom);
         rom[0x3FF] ^= 1;

         Assert.Equal(RomCheckKind.Modified, EmeraldRomCheck.Check(rom, romSpec).Kind);
      }

      [Fact]
      public void TooSmall_IsNotARom() {
         Assert.Equal(RomCheckKind.NotARom, EmeraldRomCheck.Check(new byte[10], Spec).Kind);
         Assert.Equal(RomCheckKind.NotARom, EmeraldRomCheck.Check(null, Spec).Kind);
      }

      [Theory]
      [InlineData("BPRE", "FireRed")]
      [InlineData("BPGE", "LeafGreen")]
      [InlineData("AXVE", "Ruby")]
      [InlineData("AXPE", "Sapphire")]
      [InlineData("BPEJ", "language or region")]
      [InlineData("ZZZZ", "ZZZZ")]
      public void OtherGameCodes_AreNamed(string code, string expectedText) {
         var result = EmeraldRomCheck.Check(Rom(0x400, code), Spec);

         Assert.Equal(RomCheckKind.WrongGame, result.Kind);
         Assert.Contains(expectedText, result.Message);
      }

      [Fact]
      public void ExactCubeRom_IsAlreadyPatched() {
         var cubeRom = Rom(0x800, "BPEE");
         var info = new BpsInfo { SourceSize = 0x400, TargetSize = 0x800, TargetCrc32 = (uint)Patcher.CalcCRC32(cubeRom) };

         Assert.Equal(RomCheckKind.AlreadyPatched, EmeraldRomCheck.Check(cubeRom, Spec, info).Kind);
      }

      [Fact]
      public void RomWithCubesLengthButOtherContent_IsWrongSizeAndMentionsCube() {
         var cubeRom = Rom(0x800, "BPEE");
         var info = new BpsInfo { SourceSize = 0x400, TargetSize = 0x800, TargetCrc32 = (uint)Patcher.CalcCRC32(cubeRom) };
         cubeRom[0x700] ^= 1;

         var result = EmeraldRomCheck.Check(cubeRom, Spec, info);

         Assert.Equal(RomCheckKind.WrongSize, result.Kind);
         Assert.Contains("CUBE", result.Message);
      }
   }
}
