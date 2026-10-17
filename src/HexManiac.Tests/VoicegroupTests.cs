using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Sound;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The file dialog filter of the Sound tab, and the voicegroups of the songs in it:
   /// listing them, changing the one a song uses, giving a song a voicegroup of its own and copying instruments into it.
   /// </summary>
   public class VoicegroupTests : BaseViewModelTestClass {
      // a small ROM: free space everywhere (0xFF), with a song table, a sample, two voicegroups and three songs put in by hand
      private const int TableAddress = 0x100;
      private const int SampleAddress = 0x200;
      private const int GroupA = 0x400; // 4 instruments: square 1, sample, square 2, noise. Used by songs 0 and 1.
      private const int GroupB = 0x500; // 2 instruments: sample, square 1. Named 'sound.voicegroups.beta'. Used by song 2.
      private const int ToneSize = 12;

      private const string SongSource = @"
	.include ""MPlayDef.s""
	.equ	t_grp, voicegroup000
t_1:
	.byte	TEMPO , 120*1/2
	.byte	VOICE , 0
	.byte	VOL , 127
	.byte	N24 , Cn3 , v127
	.byte	W24
	.byte	VOICE , 1
	.byte	N24 , En3 , v127
	.byte	W24
	.byte	FINE
	.align	2
t:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	t_grp
	.word	t_1
	.end
";

      private readonly int[] headers = new int[3];
      private readonly List<string> tabErrors = new List<string>();

      public VoicegroupTests() : base(0x8000) {
         SetFullModel(0xFF);

         var sine = new sbyte[400];
         for (int i = 0; i < sine.Length; i++) sine[i] = (sbyte)(Math.Sin(i * 2 * Math.PI * 220 / 13379) * 100);
         var sample = GbaSample.Build(sine, 13379, compressed: false, loopStart: 0);
         for (int i = 0; i < sample.Length; i++) Model[SampleAddress + i] = sample[i];

         WriteSquare(GroupA + 0 * ToneSize, 1, 2);
         WriteSample(GroupA + 1 * ToneSize);
         WriteSquare(GroupA + 2 * ToneSize, 2, 1);
         WriteSquare(GroupA + 3 * ToneSize, 4, 0); // noise
         WriteSample(GroupB + 0 * ToneSize);
         WriteSquare(GroupB + 1 * ToneSize, 1, 0);
         Model.ObserveAnchorWritten(Token, "sound.voicegroups.beta", new NoInfoRun(GroupB));

         headers[0] = PlaceSong(0x600, GroupA);
         headers[1] = PlaceSong(0x700, GroupA);
         headers[2] = PlaceSong(0x800, GroupB);
         CreateSongTable(TableAddress, headers);
      }

      #region Setup

      private void WriteSquare(int address, int type, int duty) {
         Model[address] = (byte)type;
         Model[address + 1] = 60;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WriteMultiByteValue(address + 4, 4, Token, duty);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0x000F0000); // attack 0, decay 0, sustain 15, release 0
      }

      private void WriteSample(int address) {
         Model[address] = 0;
         Model[address + 1] = 60;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WritePointer(Token, address + 4, SampleAddress);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0x00FF00FF); // attack 255, decay 0, sustain 255, release 0
      }

      /// <summary>Assembles a song at the address, using the voicegroup, and returns the address of its header.</summary>
      private int PlaceSong(int address, int voicegroup) {
         var song = SongAssembler.Assemble(SongSource, 0x08000000 + address, new Dictionary<string, int> { ["voicegroup000"] = 0x08000000 + voicegroup });
         Assert.True(song.Success, song.Error);
         for (int i = 0; i < song.Bytes.Length; i++) Model[address + i] = song.Bytes[i];
         var header = address + song.HeaderOffset;
         Model.ObserveRunWritten(Token, new PointerRun(header + 4)); // like a loaded ROM: the editor knows the header's pointer to its voicegroup
         return header;
      }

      private void CreateSongTable(int address, params int[] songHeaders) {
         for (int i = 0; i < songHeaders.Length; i++) {
            Model.WritePointer(Token, address + i * 8, songHeaders[i]);
            Model.WriteMultiByteValue(address + i * 8 + 4, 2, Token, 0);
            Model.WriteMultiByteValue(address + i * 8 + 6, 2, Token, 0);
         }
         var error = ArrayRun.TryParse(Model, "[pointer<> musicplayer: unknown:]" + songHeaders.Length, address, SortedSpan<int>.None, out var table);
         Assert.False(error.HasError, error.ErrorMessage);
         Model.ObserveAnchorWritten(Token, SoundTab.SongTable, table);
      }

      private SoundTab CreateTab() {
         var tab = new SoundTab(FileSystem, ViewPort);
         tab.OnError += (sender, e) => tabErrors.Add(e);
         return tab;
      }

      private int SongCount => Model.GetTable(SoundTab.SongTable).ElementCount;

      private int HeaderVoicegroup(int song) => Model.ReadPointer(headers[song] + 4);

      private byte[] Bytes(int address, int length) => Enumerable.Range(0, length).Select(i => Model[address + i]).ToArray();

      private static VoicegroupItem Group(SoundTab tab, int address) => tab.Voicegroups.First(group => group.Address == address);

      #endregion

      #region The file dialog filter

      [Fact]
      public void Filter_ListsTheExtensionsFirst_SeparatedBySemicolons() {
         var filter = FileDialogFilter.Create("Song assembly", "s", "asm", "inc");
         Assert.Equal("Song assembly|*.s;*.asm;*.inc|All Files|*.*", filter);
      }

      [Fact]
      public void Filter_NormalizesAndDropsDuplicates() {
         var filter = FileDialogFilter.Create("Songs", ".s", "*.asm", "S", "", " inc ", null);
         Assert.Equal("Songs|*.s;*.asm;*.inc|All Files|*.*", filter);
      }

      [Fact]
      public void Filter_WithoutExtensions_IsAllFiles() {
         Assert.Equal("All Files|*.*", FileDialogFilter.Create("Anything"));
         Assert.Equal("All Files|*.*", FileDialogFilter.Create("Anything", "", " "));
      }

      [Fact]
      public void Filter_WithoutDescription_IsEmpty() {
         Assert.Equal(string.Empty, FileDialogFilter.Create(null, "s"));
      }

      [Fact]
      public void Filter_DescriptionCannotBreakTheFilter() {
         Assert.Equal("a/b|*.s|All Files|*.*", FileDialogFilter.Create("a|b", "s"));
      }

      [Fact]
      public void InsertSong_AsksForAssemblyFilesFirst() {
         // the dialog starts with the first entry of its filter: it has to be the .s files, with All Files after them
         Assert.Equal("s", SoundTab.SongFileExtensions[0]);
         var filter = FileDialogFilter.Create(SoundTab.SongFileDescription, SoundTab.SongFileExtensions);
         Assert.StartsWith(SoundTab.SongFileDescription + "|*.s;", filter);
         Assert.EndsWith("|All Files|*.*", filter);
         Assert.DoesNotContain(",", filter);
      }

      #endregion

      #region Instruments

      [Fact]
      public void ToneData_RecognizesTheInstrumentTypes() {
         const int pointer = 0x08000200;
         Assert.Equal(ToneKind.DirectSound, ToneData.Classify(Model, 0x00, pointer, 0));
         Assert.Equal(ToneKind.DirectSound, ToneData.Classify(Model, 0x08, pointer, 0)); // fixed pitch
         Assert.Equal(ToneKind.Invalid, ToneData.Classify(Model, 0x00, 0, 0)); // a sample needs a pointer
         Assert.Equal(ToneKind.Square1, ToneData.Classify(Model, 0x01, 2, 0));
         Assert.Equal(ToneKind.Square2, ToneData.Classify(Model, 0x02, 1, 0));
         Assert.Equal(ToneKind.Invalid, ToneData.Classify(Model, 0x01, pointer, 0)); // a duty cycle isn't a pointer
         Assert.Equal(ToneKind.ProgrammableWave, ToneData.Classify(Model, 0x03, pointer, 0));
         Assert.Equal(ToneKind.Noise, ToneData.Classify(Model, 0x04, 0, 0));
         Assert.Equal(ToneKind.Keysplit, ToneData.Classify(Model, 0x40, pointer, pointer));
         Assert.Equal(ToneKind.DrumKit, ToneData.Classify(Model, 0x80, pointer, 0));
         Assert.Equal(ToneKind.Invalid, ToneData.Classify(Model, 0xFF, 0xFFFFFFF, 0));
         Assert.Equal(ToneKind.Invalid, ToneData.Classify(Model, 0x05, 0, 0));
      }

      [Fact]
      public void ToneData_ReadsTheFieldsOfAnInstrument() {
         var sample = ToneData.Read(Model, GroupA + ToneSize);
         Assert.Equal(ToneKind.DirectSound, sample.Kind);
         Assert.Equal(60, sample.Key);
         Assert.Equal(SampleAddress, sample.Word1Address);
         Assert.Equal(255, sample.Attack);
         Assert.Equal(255, sample.Sustain);
         Assert.Contains("Sample", sample.Describe(Model));
         var square = ToneData.Read(Model, GroupA);
         Assert.Equal(ToneKind.Square1, square.Kind);
         Assert.Equal(15, square.Sustain);
         Assert.Contains("Square wave 1", square.Describe(Model));
         Assert.Null(ToneData.Read(Model, Model.Count - 4)); // doesn't fit in the ROM
      }

      [Fact]
      public void Catalog_CountsTheInstrumentsUntilTheDataStopsLookingLikeOne() {
         Assert.Equal(4, VoicegroupCatalog.CountEntries(Model, GroupA));
         Assert.Equal(2, VoicegroupCatalog.CountEntries(Model, GroupB));
         Assert.Equal(0, VoicegroupCatalog.CountEntries(Model, 0x3000)); // free space
         Assert.Equal(0, VoicegroupCatalog.CountEntries(Model, -1));
      }

      [Fact]
      public void Catalog_RecognizesVoicegroupAnchorNames() {
         Assert.True(VoicegroupCatalog.IsVoicegroupAnchor("sound.voicegroups.route110", out var name));
         Assert.Equal("route110", name);
         Assert.True(VoicegroupCatalog.IsVoicegroupAnchor("data.sound.voicegroup.mus_foo", out name));
         Assert.Equal("mus_foo", name);
         Assert.False(VoicegroupCatalog.IsVoicegroupAnchor("sound.tracks", out _));
         Assert.False(VoicegroupCatalog.IsVoicegroupAnchor("sound.voicegroups", out _));
         Assert.False(VoicegroupCatalog.IsVoicegroupAnchor(null, out _));
      }

      [Fact]
      public void Catalog_Scan_FindsTheVoicegroupsOfTheSongs() {
         var found = VoicegroupCatalog.Scan(Model, new[] { (GroupA, "one"), (GroupA, "two"), (GroupB, "three") });
         Assert.Equal(2, found.Count);
         Assert.Equal(GroupB, found[0].Address); // named ones come first
         Assert.Equal("beta", found[0].Name);
         Assert.Equal(2, found[0].EntryCount);
         Assert.Equal(GroupA, found[1].Address);
         Assert.Null(found[1].Name);
         Assert.Equal(4, found[1].EntryCount);
         Assert.Equal("one,two", string.Join(",", found[1].UsedBy));
      }

      [Fact]
      public void Catalog_Scan_ListsNamedVoicegroupsNoSongUses() {
         var found = VoicegroupCatalog.Scan(Model, new (int, string)[0]);
         Assert.Equal(1, found.Count);
         Assert.Equal("beta", found[0].Name);
         Assert.Equal(0, found[0].UsedBy.Count);
      }

      [Fact]
      public void Catalog_Inspect_ReportsSomethingThatIsNoVoicegroupAsCustom() {
         Assert.False(VoicegroupCatalog.Inspect(Model, GroupA).IsCustom);
         var custom = VoicegroupCatalog.Inspect(Model, 0x3000);
         Assert.True(custom.IsCustom);
         Assert.Equal(0, custom.EntryCount);
      }

      #endregion

      #region Editing voicegroups

      [Fact]
      public void Editor_UniqueAnchorName_IsCleanedAndNotTaken() {
         Assert.Equal("sound.voicegroups.mus_my_song", VoicegroupEditor.UniqueAnchorName(Model, "MUS My-Song"));
         Assert.Equal("sound.voicegroups.song_9lives", VoicegroupEditor.UniqueAnchorName(Model, "9lives"));
         Assert.Equal("sound.voicegroups.song", VoicegroupEditor.UniqueAnchorName(Model, ""));
         Assert.Equal("sound.voicegroups.beta_2", VoicegroupEditor.UniqueAnchorName(Model, "beta"));
      }

      [Fact]
      public void Editor_CreateCopy_Makes128NamedInstrumentsStartingWithTheSource() {
         var source = VoicegroupCatalog.Inspect(Model, GroupA);
         Assert.True(VoicegroupEditor.TryCreateCopy(Model, Token, source, "MUS_NEW", out var address, out var anchor, out var error), error);

         Assert.Equal("sound.voicegroups.mus_new", anchor);
         Assert.Equal(address, Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor));
         Assert.Equal(0, address % 4);
         var table = Model.GetNextRun(address) as ITableRun;
         Assert.NotNull(table);
         Assert.Equal(address, table.Start);
         Assert.Equal(128, table.ElementCount);
         Assert.Equal(ToneSize, table.ElementLength);

         Assert.Equal(Bytes(GroupA, 4 * ToneSize), Bytes(address, 4 * ToneSize));
         var filler = VoicegroupCatalog.FillerTone();
         for (int slot = 4; slot < 128; slot++) Assert.Equal(filler, Bytes(address + slot * ToneSize, ToneSize));
         Assert.Equal(128, VoicegroupCatalog.CountEntries(Model, address));
      }

      [Fact]
      public void Editor_CreateCopy_WithoutASource_IsAllFiller() {
         Assert.True(VoicegroupEditor.TryCreateCopy(Model, Token, null, "x", out var address, out _, out var error), error);
         var filler = VoicegroupCatalog.FillerTone();
         for (int slot = 0; slot < 128; slot++) Assert.Equal(filler, Bytes(address + slot * ToneSize, ToneSize));
      }

      [Fact]
      public void Editor_Allocate_GrowsTheRomWhenNothingIsFree() {
         for (int i = 0; i < Model.Count; i++) Model[i] = 0; // no free space (0xFF) anywhere
         var length = Model.Count;
         Assert.True(VoicegroupEditor.TryAllocate(Model, Token, 1536, out var address, out var error), error);
         Assert.Equal(length, address);
         Assert.True(Model.Count >= address + 1536);
      }

      [Fact]
      public void Editor_Allocate_FailsClearlyWhenTheRomCannotGrow() {
         for (int i = 0; i < Model.Count; i++) Model[i] = 0;
         var length = Model.Count;
         Assert.False(VoicegroupEditor.TryAllocate(Model, Token, 1536, out _, out var error, maxRomLength: length + 100));
         Assert.Contains("no free space", error);
         Assert.Equal(length, Model.Count); // nothing changed
      }

      [Fact]
      public void Editor_AssignToSong_RewritesThePointerAndMovesItsBookkeeping() {
         VoicegroupEditor.AssignToSong(Model, Token, headers[2], GroupA);
         Assert.Equal(GroupA, HeaderVoicegroup(2));
         Assert.Equal(0x08, Model[headers[2] + 7]);
         var run = Model.GetNextRun(headers[2] + 4);
         Assert.Equal(headers[2] + 4, run.Start);
         Assert.True(run is PointerRun);
         // the voicegroup the song left no longer lists it as a user, and the new one does
         Assert.False(Model.GetNextRun(GroupB).PointerSources?.Contains(headers[2] + 4) ?? false);
         Assert.True(Model.GetNextRun(GroupA).PointerSources.Contains(headers[2] + 4));
      }

      [Fact]
      public void Editor_CopyTone_CopiesAllTwelveBytes_AndTheSampleStaysShared() {
         Assert.True(VoicegroupEditor.TryCopyTone(Model, Token, GroupA, 3, GroupB, out var error), error);
         Assert.Equal(Bytes(GroupB, ToneSize), Bytes(GroupA + 3 * ToneSize, ToneSize));
         Assert.Equal(Model.ReadPointer(GroupB + 4), Model.ReadPointer(GroupA + 3 * ToneSize + 4));
      }

      [Fact]
      public void Editor_CopyTone_RejectsSlotsAndAddressesOutsideTheRom() {
         Assert.False(VoicegroupEditor.TryCopyTone(Model, Token, GroupA, 128, GroupB, out var error));
         Assert.Contains("out of range", error);
         Assert.False(VoicegroupEditor.TryCopyTone(Model, Token, GroupA, -1, GroupB, out _));
         Assert.False(VoicegroupEditor.TryCopyTone(Model, Token, GroupA, 0, Model.Count - 4, out _));
         Assert.False(VoicegroupEditor.TryCopyTone(Model, Token, Model.Count - 4, 0, GroupB, out _));
      }

      #endregion

      #region Hearing an instrument

      private static double Rms(byte[] wav) {
         double sum = 0;
         int count = 0;
         for (int i = 44; i + 1 < wav.Length; i += 2) {
            double sample = (short)(wav[i] | (wav[i + 1] << 8));
            sum += sample * sample;
            count++;
         }
         return count == 0 ? 0 : Math.Sqrt(sum / count);
      }

      [Fact]
      public void Renderer_PreviewNote_PlaysSquaresAndSamples() {
         var renderer = new M4aRenderer(Model);
         var square = renderer.RenderPreviewNote(GroupA);
         var sample = renderer.RenderPreviewNote(GroupA + ToneSize);
         Assert.True(square.Length > 44 && Rms(square) > 100, $"square wave rms {Rms(square)}");
         Assert.True(sample.Length > 44 && Rms(sample) > 100, $"sample rms {Rms(sample)}");
         Assert.Equal((byte)'R', square[0]);
         Assert.Equal((byte)'W', square[8]);
      }

      [Fact]
      public void Renderer_PreviewNote_OfNothingIsSilent() {
         var wav = new M4aRenderer(Model).RenderPreviewNote(0x3000); // free space
         Assert.True(Rms(wav) < 10, $"rms {Rms(wav)}");
      }

      #endregion

      #region The Sound tab

      [Fact]
      public void Tab_ListsTheVoicegroupsOfTheRom() {
         var tab = CreateTab();
         Assert.True(tab.HasSongs);
         Assert.Equal(3, tab.Songs.Count);
         Assert.Equal(2, tab.Voicegroups.Count);
         Assert.Equal("beta", tab.Voicegroups[0].Title); // named first
         Assert.Equal("Voicegroup 000400", tab.Voicegroups[1].Title);
         Assert.Equal(4, tab.Voicegroups[1].EntryCount);
         Assert.Equal("0,1", string.Join(",", tab.Voicegroups[1].UsedBy));
         Assert.Contains("used by 0 +1", tab.Voicegroups[1].Label);
         Assert.Contains("4 instruments", tab.Voicegroups[1].Label);
         Assert.Contains("Square wave 1", tab.Voicegroups[1].Description);
         Assert.Empty(tabErrors);
      }

      [Fact]
      public void Tab_ShowsTheVoicegroupOfTheSelectedSong() {
         var tab = CreateTab();
         Assert.Null(tab.SongVoicegroup);
         Assert.False(tab.CanChangeVoicegroup);
         tab.SelectedSong = tab.Songs[2];
         Assert.Equal(GroupB, tab.SongVoicegroup.Address);
         Assert.True(tab.CanChangeVoicegroup);
         Assert.Equal(128, tab.SongInstruments.Count);
         tab.SelectedSong = tab.Songs[0];
         Assert.Equal(GroupA, tab.SongVoicegroup.Address);
         Assert.Contains("1 other song uses this voicegroup too", tab.VoicegroupNote);
         Assert.Equal(GroupA, tab.NewVoicegroupSource.Address);
         Assert.Equal(GroupA, tab.InstrumentSourceGroup.Address);
      }

      [Fact]
      public void Tab_ChoosingAnotherVoicegroup_RewritesTheHeader_AndUndoRestoresIt() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2];

         tab.SongVoicegroup = Group(tab, GroupA);
         Assert.Equal(GroupA, HeaderVoicegroup(2));
         Assert.Equal(GroupA, tab.SelectedSong.Header.Voicegroup); // the music engine reads the new one right away
         Assert.Equal(GroupA, tab.SongVoicegroup.Address);
         Assert.Equal("0,1,2", string.Join(",", Group(tab, GroupA).UsedBy));

         tab.Undo.Execute(null);
         Assert.Equal(GroupB, HeaderVoicegroup(2));
         Assert.Equal(GroupB, tab.SongVoicegroup.Address);

         tab.Redo.Execute(null);
         Assert.Equal(GroupA, HeaderVoicegroup(2));
         Assert.Equal(GroupA, tab.SongVoicegroup.Address);
         Assert.Empty(tabErrors);
      }

      [Fact]
      public void Tab_ChangingTheVoicegroupChangesTheSound() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2];
         var before = new M4aRenderer(Model).RenderWav(tab.SelectedSong.Header);
         tab.SongVoicegroup = Group(tab, GroupA);
         var after = new M4aRenderer(Model).RenderWav(tab.SelectedSong.Header);
         // voice 0 is a sample in group B and a square wave in group A
         Assert.False(before.SequenceEqual(after));
      }

      [Fact]
      public void Tab_AVoicegroupNothingUsesAnymore_StaysInTheList() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         tab.SongVoicegroup = Group(tab, GroupB);
         tab.SelectedSong = tab.Songs[1];
         tab.SongVoicegroup = Group(tab, GroupB);
         // GroupA has no name and no user now: it gets a name, so it stays in the list and the song can go back to it
         Assert.Equal(0, Group(tab, GroupA).UsedBy.Count);
         Assert.Equal("sound.voicegroups.song_1_original", Model.GetAnchorFromAddress(-1, GroupA));
         tab.Refresh.Execute(null);
         Assert.Equal("song_1_original", Group(tab, GroupA).Title);
         tab.SelectedSong = tab.Songs[1];
         tab.SongVoicegroup = Group(tab, GroupA);
         Assert.Equal(GroupA, HeaderVoicegroup(1));
      }

      [Fact]
      public void Tab_NewVoicegroup_GivesTheSongACopyOfItsOwn_AndLeavesTheOtherSongsAlone() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0]; // shares GroupA with song 1
         var count = Model.Count;

         tab.NewVoicegroup.Execute(null);

         var created = HeaderVoicegroup(0);
         Assert.NotEqual(GroupA, created);
         Assert.Equal(created, tab.SongVoicegroup.Address);
         Assert.Equal(GroupA, HeaderVoicegroup(1)); // the other song keeps the shared voicegroup
         Assert.Equal(128, tab.SongVoicegroup.EntryCount);
         Assert.Equal("sound.voicegroups.song_0", Model.GetAnchorFromAddress(-1, created));
         Assert.Equal(Bytes(GroupA, 4 * ToneSize), Bytes(created, 4 * ToneSize));
         Assert.Equal(3, tab.Voicegroups.Count);
         Assert.Equal(1, tab.SongVoicegroup.UsedBy.Count);
         Assert.Contains("Only this song", tab.VoicegroupNote);
         Assert.Empty(tabErrors);

         tab.Undo.Execute(null);
         Assert.Equal(GroupA, HeaderVoicegroup(0));
         Assert.Equal(GroupA, tab.SongVoicegroup.Address);
         Assert.True(string.IsNullOrEmpty(Model.GetAnchorFromAddress(-1, created)));
         for (int i = 0; i < 128 * ToneSize; i++) Assert.Equal(0xFF, Model[created + i]);
         Assert.Equal(2, tab.Voicegroups.Count);
         Assert.Equal(count, Model.Count);
      }

      [Fact]
      public void Tab_NewVoicegroup_CanBeACopyOfAnyVoicegroup() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         tab.NewVoicegroupSource = Group(tab, GroupB);
         tab.NewVoicegroup.Execute(null);
         var created = HeaderVoicegroup(0);
         Assert.Equal(Bytes(GroupB, 2 * ToneSize), Bytes(created, 2 * ToneSize));
         Assert.Equal(VoicegroupCatalog.FillerTone(), Bytes(created + 2 * ToneSize, ToneSize));
      }

      [Fact]
      public void Tab_UseInstrument_OfASharedVoicegroup_GivesTheSongItsOwnCopyFirst() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         var noiseBefore = Bytes(GroupA + 3 * ToneSize, ToneSize);
         tab.SelectedSlot = tab.SongInstruments[3];
         tab.InstrumentSourceGroup = Group(tab, GroupB);
         tab.SelectedInstrument = tab.InstrumentChoices.First(choice => choice.Slot == 0);
         Assert.Equal(ToneKind.DirectSound, tab.SelectedInstrument.Tone.Kind);

         tab.UseInstrument.Execute(null);

         var created = HeaderVoicegroup(0);
         Assert.NotEqual(GroupA, created);
         Assert.Equal(Bytes(GroupB, ToneSize), Bytes(created + 3 * ToneSize, ToneSize));
         Assert.Equal(Bytes(GroupA, 3 * ToneSize), Bytes(created, 3 * ToneSize)); // the rest is the copy
         Assert.Equal(noiseBefore, Bytes(GroupA + 3 * ToneSize, ToneSize)); // the shared voicegroup is untouched
         Assert.Equal(GroupA, HeaderVoicegroup(1));
         Assert.Contains("shared", tab.Status);
         Assert.Equal(3, tab.SelectedSlot.Slot);
         Assert.Empty(tabErrors);

         // the new voicegroup and the instrument are one undo step
         tab.Undo.Execute(null);
         Assert.Equal(GroupA, HeaderVoicegroup(0));
         Assert.Equal(0xFF, Model[created + 3 * ToneSize]);
      }

      [Fact]
      public void Tab_UseInstrument_OfTheSongsOwnVoicegroup_ChangesItInPlace() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         tab.NewVoicegroup.Execute(null);
         var own = HeaderVoicegroup(0);
         var count = Model.Count;

         tab.SelectedSlot = tab.SongInstruments[2];
         tab.InstrumentSourceGroup = Group(tab, GroupB);
         tab.SelectedInstrument = tab.InstrumentChoices.First(choice => choice.Slot == 0);
         tab.UseInstrument.Execute(null);

         Assert.Equal(own, HeaderVoicegroup(0)); // no further copy
         Assert.Equal(count, Model.Count);
         Assert.Equal(Bytes(GroupB, ToneSize), Bytes(own + 2 * ToneSize, ToneSize));
         Assert.Equal(tab.SelectedInstrument.Description, tab.SongInstruments[2].Description); // the slot list shows the new instrument
         Assert.Equal(2, tab.SelectedSlot.Slot);

         tab.Undo.Execute(null);
         Assert.Equal(own, HeaderVoicegroup(0));
         Assert.Equal(Bytes(GroupA + 2 * ToneSize, ToneSize), Bytes(own + 2 * ToneSize, ToneSize));
      }

      [Fact]
      public void Tab_UseInstrument_PastTheEndOfAVoicegroup_GivesItRoomForAllSlots() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2]; // GroupB has 2 instruments and only this song uses it
         tab.SelectedSlot = tab.SongInstruments[9];
         tab.InstrumentSourceGroup = Group(tab, GroupA);
         tab.SelectedInstrument = tab.InstrumentChoices.First(choice => choice.Slot == 3);

         tab.UseInstrument.Execute(null);

         var created = HeaderVoicegroup(2);
         Assert.NotEqual(GroupB, created);
         Assert.Equal(Bytes(GroupA + 3 * ToneSize, ToneSize), Bytes(created + 9 * ToneSize, ToneSize));
         Assert.Equal(Bytes(GroupB, 2 * ToneSize), Bytes(created, 2 * ToneSize));
         Assert.Equal(128, tab.SongVoicegroup.EntryCount);
         Assert.Empty(tabErrors);
      }

      [Fact]
      public void Tab_PickerListsEachDistinctInstrumentOnce() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         tab.NewVoicegroup.Execute(null); // 4 instruments, then 124 of the same filler
         tab.InstrumentSourceGroup = tab.SongVoicegroup;
         // square wave, sample, second square wave, noise: slot 0 and the 124 unused slots are the same instrument
         Assert.Equal(4, tab.InstrumentChoices.Count);
         Assert.Contains("also #4", tab.InstrumentChoices.First().Label);
      }

      [Fact]
      public void Tab_PickerStaysOnTheSameInstrumentAfterAnEditInThatVoicegroup() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         tab.NewVoicegroup.Execute(null);
         tab.InstrumentSourceGroup = tab.SongVoicegroup;
         tab.SelectedInstrument = tab.InstrumentChoices.First(choice => choice.Slot == 1); // the sample
         var key = tab.SelectedInstrument.BytesKey;
         tab.SelectedSlot = tab.SongInstruments[10];

         tab.UseInstrument.Execute(null);

         Assert.Equal(key, tab.SelectedInstrument.BytesKey);
      }

      [Fact]
      public void Tab_APreviewPlaysTheInstrument() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[0];
         byte[] played = null;
         tab.RequestPlayWav += (sender, wav) => played = wav;
         tab.SelectedSlot = tab.SongInstruments[1];
         tab.PreviewSlot.Execute(null);
         Assert.NotNull(played);
         Assert.True(Rms(played) > 100);

         played = null;
         tab.PreviewKey = 200; // limited to a real note
         Assert.Equal(127, tab.PreviewKey);
         tab.PreviewKey = 60;
         tab.InstrumentSourceGroup = Group(tab, GroupB);
         tab.SelectedInstrument = tab.InstrumentChoices.First();
         tab.PreviewInstrument.Execute(null);
         Assert.NotNull(played);
      }

      [Fact]
      public void Tab_AnUnknownVoicegroupIsShownAsCustom_AndReplacedOnTheFirstChange() {
         Model.WritePointer(Token, headers[2] + 4, 0x3000); // the header points at free space
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2];

         Assert.True(tab.SongVoicegroup.IsCustom);
         Assert.Equal("Custom (003000)", tab.SongVoicegroup.Title);
         Assert.Contains("doesn't look like a voicegroup", tab.VoicegroupNote);
         Assert.Equal(128, tab.SongInstruments.Count);

         tab.InstrumentSourceGroup = Group(tab, GroupA);
         tab.SelectedSlot = tab.SongInstruments[0];
         tab.SelectedInstrument = tab.InstrumentChoices.First(choice => choice.Slot == 1);
         tab.UseInstrument.Execute(null);

         var created = HeaderVoicegroup(2);
         Assert.NotEqual(0x3000, created);
         Assert.False(tab.SongVoicegroup.IsCustom);
         Assert.Equal(Bytes(GroupA + ToneSize, ToneSize), Bytes(created, ToneSize));
      }

      [Fact]
      public void Tab_WithoutASongTable_HasNothingToChange() {
         var model = new PokemonModel(new byte[0x200], singletons: Singletons);
         var viewPort = new ViewPort("empty.gba", model, InstantDispatch.Instance, Singletons);
         var tab = new SoundTab(FileSystem, viewPort);
         Assert.False(tab.HasSongs);
         Assert.Equal(0, tab.Voicegroups.Count);
         Assert.Null(tab.SongVoicegroup);
         Assert.False(tab.NewVoicegroup.CanExecute(null));
         Assert.False(tab.UseInstrument.CanExecute(null));
         Assert.False(tab.GotoVoicegroup.CanExecute(null));
         tab.NewVoicegroup.Execute(null); // nothing happens
         Assert.Equal(0, tab.Voicegroups.Count);
      }

      #endregion

      #region Inserting a song

      private const string InsertSource = @"
	.include ""MPlayDef.s""
	.equ	ins_grp, voicegroup000
	.section .rodata
	.global	inserted
	.align	2
inserted_1:
	.byte	VOICE , 1
	.byte	VOL , 100
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	FINE
	.align	2
inserted:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	ins_grp
	.word	inserted_1
	.end
";

      [Fact]
      public void Tab_InsertSong_CanGiveTheSongAVoicegroupOfItsOwn() {
         var tab = CreateTab();
         string[] extensions = null;
         FileSystem.OpenFile = (description, options) => {
            extensions = options;
            return new LoadedFile("inserted.s", Encoding.UTF8.GetBytes(InsertSource));
         };
         tab.SelectedSong = tab.Songs[1];
         tab.SelectedVoicegroup = Group(tab, GroupA); // the voicegroup to start from
         tab.InsertWithNewVoicegroup = true;

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal("s", extensions[0]);
         tab.SelectedSong = tab.Songs[1];
         var voicegroup = tab.SelectedSong.Header.Voicegroup;
         Assert.NotEqual(GroupA, voicegroup);
         Assert.NotEqual(GroupB, voicegroup);
         Assert.Equal(128, tab.SongVoicegroup.EntryCount);
         Assert.Equal(Bytes(GroupA, 4 * ToneSize), Bytes(voicegroup, 4 * ToneSize));
         Assert.Equal(GroupA, HeaderVoicegroup(0)); // the other songs are untouched
         Assert.Equal(GroupB, HeaderVoicegroup(2));
         Assert.StartsWith("sound.voicegroups.", Model.GetAnchorFromAddress(-1, voicegroup));

         // the song, its voicegroup and the table entry are one undo step
         tab.Undo.Execute(null);
         Assert.Equal(headers[1], Model.ReadPointer(TableAddress + 8));
         Assert.True(string.IsNullOrEmpty(Model.GetAnchorFromAddress(-1, voicegroup)));
         Assert.Equal(0xFF, Model[voicegroup]);
      }

      [Fact]
      public void Tab_InsertSong_WithoutTheOption_UsesTheChosenVoicegroupAsIs() {
         var tab = CreateTab();
         FileSystem.OpenFile = (description, options) => new LoadedFile("inserted.s", Encoding.UTF8.GetBytes(InsertSource));
         tab.SelectedSong = tab.Songs[1];
         tab.SelectedVoicegroup = Group(tab, GroupB);

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         tab.SelectedSong = tab.Songs[1];
         Assert.Equal(GroupB, tab.SelectedSong.Header.Voicegroup);
         Assert.Equal(2, tab.Voicegroups.Count);
      }

      #endregion

      #region Add new song

      private const string TwoVoiceSource = @"
	.include ""MPlayDef.s""
	.equ	two_grp, voicegroup000
	.section .rodata
	.global	two_voices
	.align	2
two_voices_1:
	.byte	VOICE , 1
	.byte	VOL , 100
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	VOICE , 3
	.byte	N24 , En3 , v100
	.byte	W24
	.byte	FINE
	.align	2
two_voices:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	two_grp
	.word	two_voices_1
	.end
";

      [Fact]
      public void Tab_AddNewSong_AppendsAnEmptySongAndSelectsIt() {
         var tab = CreateTab();
         var before = SongCount;
         var group = HeaderVoicegroup(0);

         Assert.True(tab.AddNewSong.CanExecute(null));
         tab.AddNewSong.Execute(null);

         Assert.Empty(tabErrors);
         Assert.Equal(before + 1, SongCount);
         Assert.Equal(before + 1, tab.Songs.Count);
         Assert.Equal(before, tab.SelectedSong.Index);
         Assert.Equal(0, tab.SelectedSong.Header.TrackCount); // plays nothing
         Assert.Equal(group, HeaderVoicegroup(0)); // nothing else changed
         Assert.Equal(headers[2], Model.ReadPointer(TableAddress + 16));

         // one user action is one undo step
         tab.Undo.Execute(null);
         Assert.Equal(before, SongCount);
      }

      [Fact]
      public void Tab_InsertSong_IntoANewSong_GivesItAVoicegroupWithOnlyItsInstruments() {
         var tab = CreateTab();
         tab.AddNewSong.Execute(null);
         var index = tab.SelectedSong.Index;
         FileSystem.OpenFile = (description, options) => new LoadedFile("two voices.s", Encoding.UTF8.GetBytes(TwoVoiceSource));
         tab.SelectedVoicegroup = Group(tab, GroupA);

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         tab.SelectedSong = tab.Songs[index];
         var voicegroup = tab.SelectedSong.Header.Voicegroup;
         Assert.NotEqual(GroupA, voicegroup);
         Assert.NotEqual(GroupB, voicegroup);
         Assert.Equal(128, tab.SongVoicegroup.EntryCount);
         var filler = VoicegroupCatalog.FillerTone();
         for (int slot = 0; slot < 128; slot++) {
            var expected = slot == 1 || slot == 3 ? Bytes(GroupA + slot * ToneSize, ToneSize) : filler;
            Assert.Equal(expected, Bytes(voicegroup + slot * ToneSize, ToneSize));
         }
         Assert.Equal(GroupA, HeaderVoicegroup(0)); // the shared voicegroups and the other songs are untouched
         Assert.Equal(GroupA, HeaderVoicegroup(1));
         Assert.Equal(GroupB, HeaderVoicegroup(2));
         Assert.StartsWith("sound.voicegroups.", Model.GetAnchorFromAddress(-1, voicegroup));

         // song and voicegroup are one undo step
         tab.Undo.Execute(null);
         Assert.True(string.IsNullOrEmpty(Model.GetAnchorFromAddress(-1, voicegroup)));
      }

      [Fact]
      public void Tab_InsertSong_IntoANewSong_WithTheOptionOff_UsesTheChosenVoicegroup() {
         var tab = CreateTab();
         tab.AddNewSong.Execute(null);
         var index = tab.SelectedSong.Index;
         tab.NewSongsGetOwnVoicegroup = false;
         FileSystem.OpenFile = (description, options) => new LoadedFile("two voices.s", Encoding.UTF8.GetBytes(TwoVoiceSource));
         tab.SelectedVoicegroup = Group(tab, GroupA);

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         tab.SelectedSong = tab.Songs[index];
         Assert.Equal(GroupA, tab.SelectedSong.Header.Voicegroup);
      }

      #endregion

      #region Songs from the game (voices the song selects, and where they are copied from)

      // mid2agb writes the first copy of a phrase that is repeated in the middle of the track and ends it with PEND: the engine ignores a PEND outside of a pattern and plays on
      private const string PendSource = @"
	.include ""MPlayDef.s""
	.equ	pend_grp, voicegroup000
	.section .rodata
	.global	pend_song
	.align	2
pend_song_1:
	.byte	VOICE , 1
	.byte	VOL , 100
pend_song_1_001:
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	PEND
	.byte	VOICE , 3
	.byte	N24 , En3 , v100
	.byte	W24
	.byte	FINE
	.align	2
pend_song:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	pend_grp
	.word	pend_song_1
	.end
";

      private const string PatternSource = @"
	.include ""MPlayDef.s""
	.equ	pat_grp, voicegroup000
	.section .rodata
	.global	pat_song
	.align	2
pat_song_1:
	.byte	VOICE , 1
	.byte	PATT
	 .word	pat_song_1_001
	.byte	VOICE , 3
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	FINE
pat_song_1_001:
	.byte	VOICE , 2
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	PEND
	.byte	VOICE , 7
	.byte	N24 , Cn3 , v100
	.byte	W24
	.byte	FINE
	.align	2
pat_song:
	.byte	1
	.byte	0
	.byte	0
	.byte	0
	.word	pat_grp
	.word	pat_song_1
	.end
";

      private SortedSet<int> VoicesOf(string source) {
         var song = SongAssembler.Assemble(source, 0x08004000, new Dictionary<string, int> { ["voicegroup000"] = 0x08000000 + GroupA });
         Assert.True(song.Success, song.Error);
         for (int i = 0; i < song.Bytes.Length; i++) Model[0x4000 + i] = song.Bytes[i];
         Assert.True(SongHeader.TryRead(Model, 0x4000 + song.HeaderOffset, out var header));
         return new SongDisassembler(Model).FindVoices(header);
      }

      [Fact]
      public void FindVoices_GoesOnAfterAPendThatIsNotInAPattern() {
         Assert.Equal(new[] { 1, 3 }, VoicesOf(PendSource));
      }

      [Fact]
      public void FindVoices_StopsAtThePendOfAPattern_ButFindsTheVoicesInsideIt() {
         Assert.Equal(new[] { 1, 2, 3 }, VoicesOf(PatternSource));
      }

      [Fact]
      public void Tab_InsertSong_IntoANewSong_KeepsTheVoicesThatComeAfterAPhraseEndedByPend() {
         var tab = CreateTab();
         tab.AddNewSong.Execute(null);
         var index = tab.SelectedSong.Index;
         FileSystem.OpenFile = (description, options) => new LoadedFile("pend.s", Encoding.UTF8.GetBytes(PendSource));
         tab.SelectedVoicegroup = Group(tab, GroupA);

         tab.InsertSong.Execute(null);

         Assert.Empty(tabErrors);
         tab.SelectedSong = tab.Songs[index];
         var voicegroup = tab.SelectedSong.Header.Voicegroup;
         Assert.Equal(Bytes(GroupA + 1 * ToneSize, ToneSize), Bytes(voicegroup + 1 * ToneSize, ToneSize));
         Assert.Equal(Bytes(GroupA + 3 * ToneSize, ToneSize), Bytes(voicegroup + 3 * ToneSize, ToneSize)); // used to stay the blank square wave
         Assert.NotEqual(VoicegroupCatalog.FillerTone(), Bytes(voicegroup + 3 * ToneSize, ToneSize));
      }

      [Fact]
      public void BuildForVoices_CopiesAVoicePastTheEndOfTheVoicegroup_WhenAnInstrumentIsThereAsTheEngineReadsIt() {
         // GroupB has two instruments; the engine would read slot 8 just the same, so the copy has to carry it
         for (int i = 0; i < ToneSize; i++) Model[GroupB + 8 * ToneSize + i] = Model[GroupA + 1 * ToneSize + i];
         var missing = new List<int>();

         var bytes = VoicegroupEditor.BuildForVoices(Model, GroupB, 2, new[] { 0, 8, 9 }, missing);

         Assert.Equal(Bytes(GroupB + 8 * ToneSize, ToneSize), bytes.Skip(8 * ToneSize).Take(ToneSize).ToArray());
         Assert.Equal(new[] { 9 }, missing); // nothing there
         Assert.Equal(VoicegroupCatalog.FillerTone(), bytes.Skip(9 * ToneSize).Take(ToneSize).ToArray());
      }

      [Fact]
      public void Export_NamesTheVoicegroup_AndTheNameIsFoundAgain() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2]; // uses sound.voicegroups.beta
         Assert.Contains("@ voicegroup beta", tab.SongText);
         Assert.Equal("beta", SoundTab.FindVoicegroupHint(tab.SongText));
         Assert.Null(SoundTab.FindVoicegroupHint("\t.equ\tx_grp, 0x08000500\t@ voicegroup\r\n"));
         Assert.Null(SoundTab.FindVoicegroupHint("\t.equ\tx_pri, 1\t@ voicegroup beta\r\n"));
         Assert.Null(SoundTab.FindVoicegroupHint(null));
      }

      [Fact]
      public void Tab_InsertSong_IntoANewSong_FindsTheVoicegroupByName_WhenTheAddressOfAnExportedSongIsFromAnotherBuild() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2];
         var text = tab.SongText.Replace("0x08000500", "0x08003000"); // where the voicegroup was in the ROM the file was saved from
         Assert.Contains("0x08003000", text);
         tab.AddNewSong.Execute(null);
         var index = tab.SelectedSong.Index;
         FileSystem.OpenFile = (description, options) => new LoadedFile("old.s", Encoding.UTF8.GetBytes(text));
         tab.SelectedVoicegroup = Group(tab, GroupA);

         tab.InsertSong.Execute(null);

         tab.SelectedSong = tab.Songs[index];
         var voicegroup = tab.SelectedSong.Header.Voicegroup;
         Assert.NotEqual(GroupA, voicegroup);
         Assert.NotEqual(GroupB, voicegroup);
         Assert.Equal(Bytes(GroupB, ToneSize), Bytes(voicegroup, ToneSize)); // slot 0 comes from beta, not from the voicegroup picked in the tab
         Assert.Equal(Bytes(GroupB + ToneSize, ToneSize), Bytes(voicegroup + ToneSize, ToneSize));
         Assert.Contains(tabErrors, e => e.Contains("beta")); // and the user is told
      }

      [Fact]
      public void Tab_InsertSong_IntoANewSong_WarnsWhenTheAddressOfTheFileIsNoVoicegroup_AndNothingElseNamesOne() {
         var tab = CreateTab();
         tab.SelectedSong = tab.Songs[2];
         var text = tab.SongText.Replace("0x08000500", "0x08003000").Replace("@ voicegroup beta", "@ voicegroup");
         tab.AddNewSong.Execute(null);
         var index = tab.SelectedSong.Index;
         FileSystem.OpenFile = (description, options) => new LoadedFile("old.s", Encoding.UTF8.GetBytes(text));
         tab.SelectedVoicegroup = Group(tab, GroupA);

         tab.InsertSong.Execute(null);

         tab.SelectedSong = tab.Songs[index];
         var voicegroup = tab.SelectedSong.Header.Voicegroup;
         Assert.Equal(Bytes(GroupA, ToneSize), Bytes(voicegroup, ToneSize)); // the voicegroup chosen in the tab
         Assert.Contains(tabErrors, e => e.Contains("not a voicegroup of this ROM"));
      }

      #endregion
   }
}
