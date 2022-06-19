using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ViewPortSaveTests : BaseViewModelTestClass {

      private readonly StubFileSystem fileSystem;
      private string name = string.Empty;

      public ViewPortSaveTests() {
         fileSystem = new StubFileSystem {
            RequestNewName = (previousName, description, extensions) => { name = $"file.txt"; return name; },
            TrySavePrompt = loadedFile => { name = loadedFile.Name; return true; },
         };
      }

      [Fact]
      public void SaveAsRequestsNewName() {
         var viewPort = new ViewPort();
         viewPort.SaveAs.Execute(fileSystem);

         Assert.Equal("file.txt", name);
      }

      [Fact]
      public void SaveRequestsNewNameIfFileIsNew() {
         var viewPort = new ViewPort();

         viewPort.Edit("01 23 45");
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("file.txt", name);
      }

      [Fact]
      public void SaveDoesNotRequestNewNameIfFileIsNotNew() {
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[0]));
         fileSystem.Save = loadedFile => { name = loadedFile.Name; return true; };

         viewPort.Edit("01 23 45");
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("input.txt", name);
      }

      [Fact]
      public void SaveDoesNotCallFileSystemTrySavePromptIfNoChanges() {
         var viewPort1 = new ViewPort();
         var viewPort2 = new ViewPort(new LoadedFile("input1.txt", new byte[10]));
         var viewPort3 = new ViewPort(new LoadedFile("input2.txt", new byte[10]));

         Assert.False(viewPort1.Save.CanExecute(fileSystem));
         viewPort1.Save.Execute(fileSystem);

         Assert.False(viewPort2.Save.CanExecute(fileSystem));
         viewPort2.Save.Execute(fileSystem);

         viewPort3.Edit("01 23 45 67 89");
         viewPort3.Undo.Execute();
         Assert.False(viewPort3.Save.CanExecute(fileSystem));
         viewPort3.Save.Execute(fileSystem);

         Assert.Equal(string.Empty, name); // none should have called TrySave
      }

      [Fact]
      public void EditedFilePromptsForSaveOnExit() {
         bool triedToSave = false;
         var viewPort = new ViewPort();
         viewPort.Edit("ab cd ef");
         fileSystem.TrySavePrompt = loadedFile => { triedToSave = true; return true; };

         viewPort.Close.Execute(fileSystem);

         Assert.True(triedToSave);
      }

      [Fact]
      public void NonEditedFileDoesNotPromptForSaveOnExit() {
         bool triedToSave = false;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[10]));
         fileSystem.TrySavePrompt = loadedFile => { triedToSave = true; return true; };

         Assert.False(viewPort.Save.CanExecute(fileSystem));
         viewPort.Close.Execute(fileSystem);

         Assert.False(triedToSave);
      }

      [Fact]
      public void FileRaisesCloseEventIfClosedWithoutEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;

         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileRaisesCloseEventIfSavedAfterEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySavePrompt = loadedFile => true;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileRaisesCloseEventIfNotSavedAfterEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySavePrompt = loadedFile => false;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileDoesNotRaiseCloseEventIfSaveCanceled() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySavePrompt = loadedFile => null;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(0, closed);
      }

      [Fact]
      public void CallingSaveMultipleTimesOnlySavesOnce() {
         int count = 0;
         var viewPort = new ViewPort();
         fileSystem.Save = loadedFile => { count++; return true; };

         viewPort.Edit("00 01 02");
         viewPort.Save.Execute(fileSystem);
         viewPort.Save.Execute(fileSystem);

         Assert.Equal(1, count);
      }

      [Fact]
      public void SaveCanExecuteChangesOnEdit() {
         int canExecuteChangedCount = 0;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[20]));
         viewPort.Save.CanExecuteChanged += (sender, e) => canExecuteChangedCount++;

         viewPort.Edit("aa bb cc");

         Assert.Equal(1, canExecuteChangedCount);
      }

      [Fact]
      public void SaveCanExecuteChangesOnUndo() {
         int canExecuteChangedCount = 0;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[20]));
         viewPort.Save.CanExecuteChanged += (sender, e) => canExecuteChangedCount++;

         viewPort.Edit("aa bb cc");
         viewPort.Undo.Execute();

         Assert.Equal(2, canExecuteChangedCount);
      }

      [Fact]
      public void ViewPortNameDoesNotContainPathOrExension() {
         var viewPort = new ViewPort(new LoadedFile("path/to/myfile.txt", new byte[10]));

         Assert.Equal("myfile", viewPort.Name);
      }

      [Fact]
      public void ViewPortNameEndsWithStarIfNeedsSave() {
         var viewPort = new ViewPort(new LoadedFile("path/to/myfile.txt", new byte[10]));
         int nameChangedCount = 0;
         viewPort.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(viewPort.Name)) nameChangedCount++; };

         viewPort.Edit("12 34 56");

         Assert.EndsWith("*", viewPort.Name);
         Assert.Equal(1, nameChangedCount);
      }

      [Fact]
      public void ViewPortHasDefaultNameBeforeFirstSave() {
         var viewPort = new ViewPort();

         Assert.NotEqual(string.Empty, viewPort.Name);
         Assert.NotNull(viewPort.Name);
      }

      [Fact]
      public void EditDefaultFileStillShowsStar() {
         var viewPort = new ViewPort();
         var name = viewPort.Name;

         viewPort.Edit("11 22 33");

         Assert.Equal($"{name}*", viewPort.Name);
      }

      [Fact]
      public void ViewPortTakesNewNameOnSave() {
         var fileSystem = new StubFileSystem { RequestNewName = (originalName, description, extensions) => "path/to/newfile.txt", Save = loadedFile => true };
         var viewPort = new ViewPort();
         int nameChangedCount = 0;
         viewPort.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(viewPort.Name)) nameChangedCount++; };

         viewPort.Edit("012345");
         Assert.Equal(1, nameChangedCount);
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("newfile", viewPort.Name);
         Assert.Equal(3, nameChangedCount);
      }

      [Fact]
      public void ViewPortRequestsDelayedReloadIfReloadFails() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[50]));
         var fileSystem = new StubFileSystem { LoadFile = fileName => throw new IOException() };

         var retryCount = 0;
         viewPort.RequestDelayedWork += (sender, e) => retryCount++;

         viewPort.ConsiderReload(fileSystem);

         Assert.Equal(1, retryCount);
      }

      [Fact]
      public void ViewPortAdjustsSelectionWhenLoadingAShorterFile() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[12])) {
            SelectionStart = new Point(3, 3)
         };
         Assert.Equal(4, viewPort.Width);
         Assert.Equal(4, viewPort.Height);
         Assert.Equal(new Point(0, 3), viewPort.SelectionStart);

         var fileSystem = new StubFileSystem { LoadFile = filename => new LoadedFile("file.txt", new byte[10]) };
         viewPort.ConsiderReload(fileSystem);

         Assert.Equal(new Point(2, 2), viewPort.SelectionStart);
      }

      [Fact]
      public void ViewPortNotifiesOnFileNameChange() {
         var properties = new List<string>();

         var fileSystem = new StubFileSystem {
            RequestNewName = (currentName, description, extensionOptions) => "file.txt",
            Save = file => true,
         };
         var viewPort = new ViewPort();
         viewPort.PropertyChanged += (sender, e) => properties.Add(e.PropertyName);

         viewPort.Edit("01 23 45 67");
         viewPort.SaveAs.Execute(fileSystem);

         Assert.Contains("FileName", properties);
         Assert.Equal("file.txt", viewPort.FileName);
      }

      [Fact]
      public void EditorUpdatesFileSystemWatchesWhenViewPortFileNameChanges() {
         var fileSystem = new StubFileSystem();
         int addCalls = 0, removeCalls = 0;
         fileSystem.AddListenerToFile = (fileName, action) => addCalls++;
         fileSystem.RemoveListenerForFile = (fileName, action) => removeCalls++;
         var editor = new EditorViewModel(fileSystem);
         var tab = new StubViewPort();

         editor.Add(tab);
         Assert.Equal(0, addCalls);
         Assert.Equal(0, removeCalls);

         tab.FileName = "file.txt";
         tab.PropertyChanged.Invoke(tab, new ExtendedPropertyChangedEventArgs<string>(null, nameof(tab.FileName)));
         Assert.Equal(1, addCalls);
         Assert.Equal(0, removeCalls);

         tab.FileName = "file2.txt";
         tab.PropertyChanged.Invoke(tab, new ExtendedPropertyChangedEventArgs<string>("file.txt", nameof(tab.FileName)));
         Assert.Equal(2, addCalls);
         Assert.Equal(1, removeCalls);
      }

      [Fact]
      public void CanSaveAndLoadNamesAndFormats() {
         SetFullModel(0xFF);
         var viewPort = ViewPort;
         StoredMetadata metadata = null;
         var fileSystem = new StubFileSystem { Save = file => true, SaveMetadata = (file, md) => { metadata = new StoredMetadata(md); return true; } };

         viewPort.Edit("^bob\"\" \"Hello\"");
         viewPort.Save.Execute(fileSystem);

         var model2 = new PokemonModel(Model.RawData, metadata);
         var viewPort2 = AutoSearchTests.NewViewPort("file.txt", model2);

         Assert.Equal("bob", ((Anchor)viewPort2[0, 0].Format).Name);
      }

      [Fact]
      public void FormattingChangesDoNotMakeFileDirty() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = AutoSearchTests.NewViewPort("file.txt", model);
         var fileSystem = new StubFileSystem();

         viewPort.Edit("^bob ");

         Assert.True(viewPort.Save.CanExecute(fileSystem));
         Assert.DoesNotContain("*", viewPort.Name);
      }

      [Fact]
      public void UndoRedoRestoresSaveStar() {
         SetFullModel(0xFF);
         var viewPort = ViewPort;
         int nameChangedCount = 0;
         viewPort.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(viewPort.Name)) nameChangedCount++; };
         var fileSystem = new StubFileSystem();

         viewPort.Edit("AA");       // notify 1 -> adding the star
         viewPort.Undo.Execute();   // notify 2 -> removing the star
         viewPort.Redo.Execute();   // notify 3 -> re-adding the star

         Assert.Contains("*", viewPort.Name);
         Assert.Equal(3, nameChangedCount);
      }

      [Fact]
      public void ViewPortWarnsIfLoadedMatchedWordValueDoesNotMatch() {
         // Arrange
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var change = new ModelDelta();
         var dispatcher = new ControlledDispatch();
         ArrayRun.TryParse(model, "[a:]8", 0x10, null, out var table);
         model.ObserveAnchorWritten(change, "table", table);
         change.AddMatchedWord(model, 0, "table", 4);
         model.ObserveRunWritten(change, new WordRun(0, "table", 4, 0, 1));

         fileSystem.MetadataFor = name => model.ExportMetadata(BaseViewModelTestClass.Singletons.MetadataInfo).Serialize();
         fileSystem.OpenFile = (name, extensions) => new LoadedFile(name, data);
         var editor = new EditorViewModel(fileSystem, dispatcher);

         // change the data so that the viewPort will notice something weird
         change.ChangeData(model, 0, 4);

         // Act
         editor.Open.Execute("text.gba");
         dispatcher.RunAllWorkloads();

         // Assert
         Assert.True(editor.ShowMessage);
      }

      [Fact]
      public void UpdateOldTableWithNewNameTest() {
         // setup data with a pointer from 0x60 to 0x10
         var data = new byte[0x200];
         data[0x63] = 0x08;

         // setup the metadata loaded from file
         var anchor1 = new StoredAnchor(0x00, "bob",   "[number::]4");
         var anchor2 = new StoredAnchor(0x20, "user1", "[number::bob]4");
         var anchor3 = new StoredAnchor(0x40, "user2", "[number::]bob");
         var metadataInfo = new StubMetadataInfo { VersionNumber = "0.3.0.0" };
         var metadata = new StoredMetadata(new[] { anchor1, anchor2, anchor3 }, null, null, null, null, null, metadataInfo, default, default, default);

         // setup the current reference, loaded from singletons
         var gameReferenceTables = new GameReferenceTables(new[] { new ReferenceTable("tom", 0, 0x60, "[number::]4") });
         var singletons = new Singletons(
            new StubMetadataInfo { VersionNumber = "0.4.0.0" },
            new Dictionary<string, GameReferenceTables> { { new string((char)0, 4) + "0", gameReferenceTables }
         });

         // create a model, which should notice and resolve the conflict
         var model = new PokemonModel(data, metadata, singletons);

         // 'bob' updated to 'tom'
         Assert.Equal("tom", model.GetAnchorFromAddress(-1, 0x00));
         Assert.Equal("tom", ((ArrayRunEnumSegment)((ITableRun)model.GetNextRun(0x20)).ElementContent[0]).EnumName);
         Assert.Equal("tom", ((ArrayRun)model.GetNextRun(0x40)).LengthFromAnchor);
      }

      [Fact]
      public void UpdateOldTableWithNewName_UpdatesBitArrays() {
         // setup data with a pointer from 0x60 to 0x00 and some strings from 0x00 to 0x10
         var data = new byte[0x200];
         data[0x60] = 0x10;
         data[0x63] = 0x08;
         int i = 0;
         foreach (byte b in PCSString.Convert("aaa")) data[i++] = b;
         foreach (byte b in PCSString.Convert("bbb")) data[i++] = b;
         foreach (byte b in PCSString.Convert("ccc")) data[i++] = b;
         foreach (byte b in PCSString.Convert("ddd")) data[i++] = b;

         // setup the metadata loaded from file
         var anchor1 = new StoredAnchor(0x00, "names", "[name\"\"4]4");
         var anchor2 = new StoredAnchor(0x10, "bob", "[number::]names");
         var anchor3 = new StoredAnchor(0x20, "user1", "[number|b[]bob]4"); // should be 4 bytes long
         var metadataInfo = new StubMetadataInfo { VersionNumber = "0.3.0.0" };
         var metadata = new StoredMetadata(new[] { anchor1, anchor2, anchor3 }, null, null, null, null, null, metadataInfo, default, default, default);

         // setup the current reference, loaded from singletons
         var gameReferenceTables = new GameReferenceTables(new[] { new ReferenceTable("tom", 0, 0x60, "[number::]names") });
         var singletons = new Singletons(
            new StubMetadataInfo { VersionNumber = "0.4.0.0" },
            new Dictionary<string, GameReferenceTables> { { new string((char)0, 4) + "0", gameReferenceTables }
         });

         // create a model, which should notice and resolve the conflict
         var model = new PokemonModel(data, metadata, singletons);

         // 'bob' updated to 'tom'
         Assert.Equal("tom", ((ArrayRunBitArraySegment)((ITableRun)model.GetNextRun(0x20)).ElementContent[0]).SourceArrayName);
      }

      [Fact]
      public void UpdateOldTableWithNewName_MatchedLengthTableWithLengthModifier_UpdatesLengthCorrectly() {
         var data = new byte[0x200];
         data[0x33] = 0x08; // pointer from 0x30 to 0x00

         // metadata, loaded from the file
         var metadata = new StoredMetadata(
            new[] {
               new StoredAnchor(0x00, "bob", "[data:]4"),
               new StoredAnchor(0x10, "sam", "[data:]bob+2")
            },
            default, default, default, default, default,
            new StubMetadataInfo { VersionNumber = "0.3.0.0" },
            default,
            default,
            default
            );

         // setup the current reference, loaded from singletons
         var gameReferenceTables = new GameReferenceTables(new[] { new ReferenceTable("tom", 0, 0x30, "[data:]4") });
         var singletons = new Singletons(
            new StubMetadataInfo { VersionNumber = "0.4.0.0" },
            new Dictionary<string, GameReferenceTables> { { new string((char)0, 4) + "0", gameReferenceTables } }
            );

         // create a model, which should notice and resolve the conflict
         var model = new PokemonModel(data, metadata, singletons);

         // 'bob' updated to 'tom' in sam's length
         Assert.Equal("[data:]tom+2", model.GetNextRun(0x10).FormatString);
      }

      [Fact]
      public void UpdateOldTableWithNewName_UpdatesTilemapTilesetHints() {
         var data = new byte[0x200];

         // header for compressed tilemap
         data[0] = 0x10;
         data[1] = 2; // 2 bytes = 1 tile. 0000 = use tile 0, palette 0. Next 3 bytes represent the compressed data.

         // header for the compressed tileset
         data[8] = 0x10;
         data[9] = 0x20; // 0x20 bytes = 1 tile. Next 0x24 bytes represent the compressed data.

         (data[0x33], data[0x32], data[0x31], data[0x30]) = (0x08, 0x00, 0x00, 0x00); // pointer to tilemap
         (data[0x37], data[0x36], data[0x35], data[0x34]) = (0x08, 0x00, 0x00, 0x08); // pointer to tileset

         // metadata, loaded from the file
         var metadata = new StoredMetadata(
            new[] {
               new StoredAnchor(0x00, "bob", "`lzm4x1x1|tileset1`"),
               new StoredAnchor(0x08, "tileset1", "`lzt4`")
            },
            default, default, default, default, default,
            new StubMetadataInfo { VersionNumber = "0.3.0.0" },
            default,
            default,
            default
            );

         // setup the current reference, loaded from singletons
         var gameReferenceTables = new GameReferenceTables(new[] { new ReferenceTable("tileset2", 0, 0x34, "`lzt4`") });
         var singletons = new Singletons(
            new StubMetadataInfo { VersionNumber = "0.4.0.0" },
            new Dictionary<string, GameReferenceTables> { { new string((char)0, 4) + "0", gameReferenceTables } }
            );

         // create a model, which should notice and resolve the conflict
         var model = new PokemonModel(data, metadata, singletons);

         // assert that 'tileset1' was replaced with 'tileset2'
         Assert.Equal("`lzm4x1x1|tileset2`", model.GetNextRun(0x00).FormatString);
      }

      [Fact]
      public void Model_OffsetPointerMetadata_ContainsOffsetPointers() {
         var storedOffsetPointer = new StoredOffsetPointer(0x100, Pointer.NULL);
         var singletons = BaseViewModelTestClass.Singletons;
         var metadata = new StoredMetadata(default, default, default, new[] { storedOffsetPointer }, default, default, singletons.MetadataInfo, default, default, default);

         var model = new PokemonModel(new byte[0x200], metadata, singletons);

         // assert that the offset pointer was created
         var run = (OffsetPointerRun)model.GetNextRun(0x100);
         Assert.Equal(Pointer.NULL, run.Offset);

         // assert that the destination knows about it
         var anchor = model.GetNextRun(0);
         Assert.Equal(0, anchor.Start);
         Assert.Equal(0x100, anchor.PointerSources.Single());
      }

      [Fact]
      public void CustomBufferSpace_MoveData_LessSpaceIsWasted() {
         Model.ExpandData(new ModelDelta(), 0x400);
         SetFullModel(0xFF);
         Model[2] = 0x10;
         Model[0x100] = 0x10;
         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);
         metadata = new StoredMetadata(metadata.NamedAnchors,
            metadata.UnmappedPointers,
            metadata.MatchedWords,
            metadata.OffsetPointers,
            metadata.Lists,
            metadata.UnmappedConstants,
            Singletons.MetadataInfo,
            metadata.FreeSpaceSearch,
            0x10,
            metadata.NextExportID);

         Model.LoadMetadata(metadata);
         ViewPort.Refresh();
         ViewPort.Edit("^table[a:]1 2 +");

         var table = Model.GetTable("table");
         Assert.InRange(table.Start, 0x100, 0x180);
      }

      [Fact]
      public void StaticVariable_Serialize_Serialized() {
         ViewPort.Edit("@somevariable=0x12345678 ");

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);

         Assert.Single(metadata.UnmappedConstants);
         Assert.Equal("somevariable", metadata.UnmappedConstants[0].Name);
         Assert.Equal(0x12345678, metadata.UnmappedConstants[0].Value);
      }

      [Fact]
      public void UnmappedConstantInMetadata_Deserialize_UnmappedConstantInModel() {
         Model.Load(new byte[0x200], new StoredMetadata(unmappedConstants: new List<StoredUnmappedConstant> { new StoredUnmappedConstant("somevar", 0x12345678) }));

         Model.TryGetUnmappedConstant("somevar", out var value);

         Assert.Equal(0x12345678, value);
      }

      [Fact]
      public void MetadataChangeThenUndo_ChangeTableLengthFromAnchor_CanSave() {
         var fs = new StubFileSystem { Save = arg => true };
         ViewPort.Edit("^table[a:]4 ");
         ViewPort.Save.Execute(fs);
         ViewPort.Refresh();
         ViewPort.AnchorText = "^table[a:]3 ";
         ViewPort.Undo.Execute();
         ViewPort.Refresh();

         bool notifyCanExecuteChange = false;
         ViewPort.Save.CanExecuteChanged += (sender, e) => notifyCanExecuteChange = true;
         ViewPort.AnchorText = "^table[a:]3 ";

         Assert.True(notifyCanExecuteChange);
         Assert.True(ViewPort.Save.CanExecute(fs));
      }

      [Fact]
      public void SavedMetadata_MetadataChange_ToolbarCanSave() {
         var fs = new StubFileSystem { Save = arg => true };
         ViewPort.Edit("^table[a:]4 ");
         ViewPort.Save.Execute(fs);
         ViewPort.Refresh();
         var editor = new EditorViewModel(fs, Singletons.WorkDispatcher);
         editor.Add(ViewPort);

         bool notifyCanExecuteChange = false;
         editor.Save.CanExecuteChanged += (sender, e) => notifyCanExecuteChange = true;
         ViewPort.AnchorText = "^table[a:]3 ";

         Assert.True(notifyCanExecuteChange);
         Assert.True(editor.Save.CanExecute(fs));
      }

      [Fact]
      public void MetadataChange_CheckChange_IsMetadataOnlyChange() {
         var view = new StubView(ViewPort);

         ViewPort.Edit("^test ");

         Assert.True(ViewPort.IsMetadataOnlyChange);
         Assert.Contains(nameof(ViewPort.IsMetadataOnlyChange), view.PropertyNotifications);
      }

      [Fact]
      public void GotoShortcut_WriteSameShortcutTwice_MetadataContainsOneCopy() {
         var shortcuts = new[] { new StoredGotoShortcut("name", "image", "destination") };
         var metadata = new StoredMetadata(default, default, default, default, default, default, shortcuts, Singletons.MetadataInfo, default);

         Model.LoadMetadata(metadata);
         Model.LoadMetadata(metadata);

         Assert.Single(Model.ExportMetadata(Singletons.MetadataInfo).GotoShortcuts);
      }

      [Fact]
      public void MetadataWithShowRawIVByteForTrainer_CreateTrainerRun_ShowRawIVByteForTrainer() {
         var metadata = new StoredMetadata(default, default, default, default, default, default, default, Singletons.MetadataInfo, new StoredMetadataFields {
            ShowRawIVByteForTrainer = true
         });
         var model = new PokemonModel(new byte[0x200], metadata);

         var newRun = (IStreamRun)model.FormatRunFactory.GetStrategy("`tpt`").WriteNewRun(model, Token, Pointer.NULL, 0, "test", null);
         model[0] = 100;

         Assert.Contains("IVs=100", newRun.SerializeRun());
      }

      [Fact]
      public void DefaultMetadata_DoNotShowIVByteForTrainer() {
         var metadata = new StoredMetadata(new string[0]);
         Assert.False(metadata.ShowRawIVByteForTrainer);
      }

      [Fact]
      public void DoNotShowIVByteForTrainerContentInText_Parse_MetadataContainsFlag() {
         var metadata = new StoredMetadata(new[] { "ShowRawIVByteForTrainer = True" });
         Assert.True(metadata.ShowRawIVByteForTrainer);
      }

      [Fact]
      public void StoredListWithCustomHash_ExportMetadata_HashSaved() {
         Model.SetList(Token, "list", new[] { "a", "b", "c" }, "0");

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);

         var list = metadata.Lists[0];
         Assert.Equal("list", list.Name);
         Assert.Equal("0", list.Hash);
         Assert.False(list.HashMatches);
      }

      [Fact]
      public void TableGroup_ExportMetadata_ExportHash() {
         var items = new[] { "a", "b", "c" };
         Model.AppendTableGroup(Token, "group", items, null);

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);
         var lines = metadata.Serialize();

         var line = lines.Single(line => line.StartsWith("DefaultHash"));
         var expectedHash = StoredList.GenerateHash(items);
         Assert.Equal($"DefaultHash = '''{expectedHash:X8}'''", line);
      }

      [Fact]
      public void LoadShortcut_SameTableAlreadyInShortcut_NoLoad() {
         var shortcut1 = new StoredGotoShortcut("name1", "image1", "destination/1");
         Model.LoadMetadata(new StoredMetadata(gotoShortcuts: new[] { shortcut1 }));

         var shortcut2 = new StoredGotoShortcut("name2", "image2", "destination/2");
         Model.LoadMetadata(new StoredMetadata(gotoShortcuts: new[] { shortcut2 }));

         Assert.Single(Model.GotoShortcuts);
      }
   }
}
