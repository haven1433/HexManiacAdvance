using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Sound;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The Sound tab says in its description whether the ROM has the All Instruments patch (the voicegroup with the 128 General MIDI instruments).
   /// The ROM here is small: a song table with two songs, a few voicegroups, and an All Instruments voicegroup of the kind the test asks for.
   /// </summary>
   public class AllInstrumentsPatchTests : BaseViewModelTestClass {
      private const int TableAddress = 0x100;
      private const int SampleAddress = 0x200;
      private const int KeyTable = 0x300;
      private const int SubGroup = 0x340;
      private const int AllInstruments = 0x1000; // up to 128 instruments (0x600 bytes)
      private const int Other = 0x1800;          // 2 instruments, named 'sound.voicegroups.other'
      private const int Ordinary = 0x1900;       // 128 instruments like the ones in the games: squares, a sample, a few key splits and a drum kit
      private const int ToneSize = 12;
      private const string SongSource = @"
	.include ""MPlayDef.s""
	.equ	t_grp, voicegroup000
t_1:
	.byte	VOICE , 0
	.byte	VOL , 127
	.byte	N24 , Cn3 , v127
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

      private readonly int[] headers = new int[2];

      public AllInstrumentsPatchTests() : base(0x10000) { SetFullModel(0xFF); }

      #region Setup

      private void WriteSquare(int address, int type = 1, int duty = 2) {
         Model[address] = (byte)type;
         Model[address + 1] = 60;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WriteMultiByteValue(address + 4, 4, Token, duty);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0x000F0000);
      }

      private void WriteSample(int address) {
         Model[address] = 0;
         Model[address + 1] = 60;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WritePointer(Token, address + 4, SampleAddress);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0x00FF00FF);
      }

      private void WriteKeySplit(int address) {
         Model[address] = 0x40;
         Model[address + 1] = 0;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WritePointer(Token, address + 4, SubGroup);
         Model.WritePointer(Token, address + 8, KeyTable);
      }

      private void WriteDrumKit(int address) {
         Model[address] = 0x80;
         Model[address + 1] = 0;
         Model[address + 2] = 0;
         Model[address + 3] = 0;
         Model.WritePointer(Token, address + 4, SubGroup);
         Model.WriteMultiByteValue(address + 8, 4, Token, 0);
      }

      /// <summary>The layout of the patch: 128 slots, the first ones samples, almost all of the rest key splits, and two drum kits at the end.</summary>
      private void WriteAllInstruments(int address, int slots = 128) {
         for (int i = 0; i < slots; i++) {
            if (i >= 126) WriteDrumKit(address + i * ToneSize);
            else if (i < 17) WriteSample(address + i * ToneSize);
            else WriteKeySplit(address + i * ToneSize);
         }
      }

      /// <summary>128 instruments like the voicegroups of the games have: mostly square waves, one sample, 3 key splits and a drum kit.</summary>
      private void WriteOrdinary(int address) {
         for (int i = 0; i < 128; i++) {
            if (i == 0) WriteSample(address + i * ToneSize);
            else if (i == 10 || i == 20 || i == 30) WriteKeySplit(address + i * ToneSize);
            else if (i == 127) WriteDrumKit(address + i * ToneSize);
            else WriteSquare(address + i * ToneSize, 1 + i % 2, i % 4);
         }
      }

      private int PlaceSong(int address, int voicegroup) {
         var song = SongAssembler.Assemble(SongSource, 0x08000000 + address, new Dictionary<string, int> { ["voicegroup000"] = 0x08000000 + voicegroup });
         Assert.True(song.Success, song.Error);
         for (int i = 0; i < song.Bytes.Length; i++) Model[address + i] = song.Bytes[i];
         var header = address + song.HeaderOffset;
         Model.ObserveRunWritten(Token, new PointerRun(header + 4));
         return header;
      }

      /// <summary>A song table with two songs. The first song uses <paramref name="firstSongVoicegroup"/>, the second the voicegroup called 'other'.</summary>
      private void BuildSongs(int firstSongVoicegroup) {
         var sample = GbaSample.Build(new sbyte[400], 13379, compressed: false, loopStart: 0);
         for (int i = 0; i < sample.Length; i++) Model[SampleAddress + i] = sample[i];
         WriteSample(Other);
         WriteSquare(Other + ToneSize);
         Model.ObserveAnchorWritten(Token, "sound.voicegroups.other", new NoInfoRun(Other));
         WriteSquare(SubGroup);

         headers[0] = PlaceSong(0x2000, firstSongVoicegroup);
         headers[1] = PlaceSong(0x2100, Other);
         for (int i = 0; i < headers.Length; i++) {
            Model.WritePointer(Token, TableAddress + i * 8, headers[i]);
            Model.WriteMultiByteValue(TableAddress + i * 8 + 4, 2, Token, 0);
            Model.WriteMultiByteValue(TableAddress + i * 8 + 6, 2, Token, 0);
         }
         var error = ArrayRun.TryParse(Model, "[pointer<> musicplayer: unknown:]" + headers.Length, TableAddress, SortedSpan<int>.None, out var table);
         Assert.False(error.HasError, error.ErrorMessage);
         Model.ObserveAnchorWritten(Token, SoundTab.SongTable, table);
      }

      private void NameAllInstruments(string anchor = SoundTab.AllInstrumentsAnchor) => Model.ObserveAnchorWritten(Token, anchor, new NoInfoRun(AllInstruments));

      private SoundTab CreateTab() => new SoundTab(FileSystem, ViewPort);

      private const string AppliedNamed = "All Instruments patch: applied (128 instruments available, voicegroup all_instruments) - MIDI and .s songs made for it will sound right.";

      #endregion

      #region The ROM has the patch

      [Fact]
      public void NamedVoicegroup_IsApplied_WithItsInstrumentCount() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other);
         NameAllInstruments();

         var status = AllInstrumentsPatch.Inspect(Model);

         Assert.Equal(AllInstrumentsState.Applied, status.State);
         Assert.True(status.IsApplied);
         Assert.Equal(AllInstruments, status.Address);
         Assert.Equal(SoundTab.AllInstrumentsAnchor, status.AnchorName);
         Assert.Equal(128, status.InstrumentCount);
         Assert.Equal(109, status.KeySplitCount);
         Assert.Equal(2, status.DrumKitCount);
      }

      [Fact]
      public void NamedVoicegroup_TheTabSaysSo() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other);
         NameAllInstruments();

         var tab = CreateTab();

         Assert.Equal(AppliedNamed, tab.AllInstrumentsNote);
         Assert.True(tab.HasAllInstruments);
         Assert.Contains("109 of its instruments are key splits", tab.AllInstrumentsDetails);
         Assert.Contains("sound.voicegroups.all_instruments", tab.AllInstrumentsDetails);
      }

      [Fact]
      public void NamedVoicegroup_NobodyUsesIt_StillApplied() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other); // neither song uses it
         NameAllInstruments();

         Assert.DoesNotContain(CreateTab().Voicegroups, group => group.Address == AllInstruments && group.Info.UsedBy.Count > 0);
         Assert.Equal(AppliedNamed, CreateTab().AllInstrumentsNote);
      }

      [Fact]
      public void SmallVoicegroup_CountsItsInstruments() {
         WriteSquare(AllInstruments + 0 * ToneSize);
         WriteSample(AllInstruments + 1 * ToneSize);
         WriteSquare(AllInstruments + 2 * ToneSize, 2, 1);
         WriteSquare(AllInstruments + 3 * ToneSize, 4, 0);
         BuildSongs(Other);
         NameAllInstruments();

         var tab = CreateTab();

         Assert.StartsWith("All Instruments patch: applied (4 instruments available, voicegroup all_instruments)", tab.AllInstrumentsNote);
      }

      [Fact]
      public void SingleInstrument_SaysInstrument() {
         WriteSquare(AllInstruments);
         BuildSongs(Other);
         NameAllInstruments();

         Assert.StartsWith("All Instruments patch: applied (1 instrument available, voicegroup all_instruments)", CreateTab().AllInstrumentsNote);
      }

      [Theory]
      [InlineData("data.sound.voicegroup.all_instruments")]
      [InlineData("sound.voicegroups.ALL_INSTRUMENTS")]
      [InlineData("sound.voicegroup.All_Instruments")]
      public void OtherSpellingsOfTheName_AreFound(string anchor) {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other);
         NameAllInstruments(anchor);

         var status = AllInstrumentsPatch.Inspect(Model);

         Assert.True(status.IsApplied);
         Assert.Equal(anchor, status.AnchorName);
         Assert.Equal(AppliedNamed, status.Summary);
      }

      [Fact]
      public void UnnamedVoicegroupUsedBySongs_IsRecognizedByItsLayout() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(AllInstruments); // the ROM does not name it, but a song uses it

         var tab = CreateTab();

         Assert.True(tab.HasAllInstruments);
         Assert.Equal("All Instruments patch: applied (128 instruments available, voicegroup at 001000) - MIDI and .s songs made for it will sound right.", tab.AllInstrumentsNote);
         Assert.Contains("does not give a name", tab.AllInstrumentsDetails);
      }

      #endregion

      #region The ROM does not have the patch

      [Fact]
      public void OnlyOrdinaryVoicegroups_NotApplied() {
         WriteOrdinary(Ordinary);
         BuildSongs(Ordinary);
         Model.ObserveAnchorWritten(Token, "sound.voicegroups.route101", new NoInfoRun(Ordinary));

         var status = AllInstrumentsPatch.Inspect(Model);
         var tab = CreateTab();

         Assert.Equal(AllInstrumentsState.NotApplied, status.State);
         Assert.False(tab.HasAllInstruments);
         Assert.StartsWith("All Instruments patch: not applied (standard voicegroups only)", tab.AllInstrumentsNote);
         Assert.Contains("voicegroup you choose below", tab.AllInstrumentsNote);
         Assert.Contains("sound.voicegroups.all_instruments", tab.AllInstrumentsDetails);
      }

      [Fact]
      public void NoNamedVoicegroupsAtAll_NotApplied() {
         WriteOrdinary(Ordinary);
         BuildSongs(Ordinary);

         var tab = CreateTab();

         Assert.False(tab.HasAllInstruments);
         Assert.StartsWith("All Instruments patch: not applied (standard voicegroups only)", tab.AllInstrumentsNote);
      }

      [Theory]
      [InlineData("sound.voicegroups.all_instruments_drums")]
      [InlineData("sound.voicegroups.all_instruments_drums_alt")]
      [InlineData("sound.voicegroups.all_instruments_split_000")]
      public void ThePatchsOtherVoicegroups_DoNotCountAsThePatch(string anchor) {
         // the drum kits and the split groups of the patch are named like it, but they are not the General MIDI voicegroup: 128 samples, no key splits
         for (int i = 0; i < 128; i++) WriteSample(AllInstruments + i * ToneSize);
         BuildSongs(Other);
         NameAllInstruments(anchor);

         var status = AllInstrumentsPatch.Inspect(Model);

         Assert.Equal(AllInstrumentsState.NotApplied, status.State);
         Assert.False(CreateTab().HasAllInstruments);
      }

      [Fact]
      public void NamedButNotInstruments_IsNotUsable() {
         BuildSongs(Other);
         for (int i = 0; i < 128 * ToneSize; i++) Model[AllInstruments + i] = 0xAB; // not instruments
         NameAllInstruments();

         var status = AllInstrumentsPatch.Inspect(Model);
         var tab = CreateTab();

         Assert.Equal(AllInstrumentsState.Unreadable, status.State);
         Assert.False(status.IsApplied);
         Assert.False(tab.HasAllInstruments);
         Assert.Equal("All Instruments patch: not usable - the voicegroup all_instruments at 001000 does not look like a list of instruments, so it is not used.", tab.AllInstrumentsNote);
      }

      [Fact]
      public void ModelWithoutVoicegroups_NotApplied() {
         var status = AllInstrumentsPatch.Inspect(Model);

         Assert.Equal(AllInstrumentsState.NotApplied, status.State);
         Assert.Equal(-1, status.Address);
      }

      #endregion

      #region Following the ROM

      [Fact]
      public void NamingTheVoicegroup_AndReloading_ChangesTheNote() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other);
         var tab = CreateTab();
         Assert.False(tab.HasAllInstruments);
         var changed = new List<string>();
         tab.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

         NameAllInstruments();
         tab.Refresh.Execute(null);

         Assert.True(tab.HasAllInstruments);
         Assert.Equal(AppliedNamed, tab.AllInstrumentsNote);
         Assert.Contains(nameof(SoundTab.AllInstrumentsNote), changed);
         Assert.Contains(nameof(SoundTab.AllInstrumentsDetails), changed);
         Assert.Contains(nameof(SoundTab.HasAllInstruments), changed);
      }

      [Fact]
      public void UndoOfTheNaming_ChangesTheNoteBack() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other);
         ViewPort.ChangeHistory.ChangeCompleted();
         var tab = CreateTab();
         Assert.False(tab.HasAllInstruments);
         ViewPort.Goto.Execute(AllInstruments.ToString("X6"));
         ViewPort.Edit($"^{SoundTab.AllInstrumentsAnchor}[type.|h key. length. pan_sweep.|h wave_or_group::|h attack. decay. sustain. release.]128 ");
         tab.Refresh.Execute(null);
         Assert.True(tab.HasAllInstruments);
         ViewPort.ChangeHistory.ChangeCompleted();

         tab.Undo.Execute(null);

         Assert.False(tab.HasAllInstruments);
         Assert.StartsWith("All Instruments patch: not applied", tab.AllInstrumentsNote);
      }

      [Fact]
      public void TheDeclaredTable_AsTheCubeMetadataHasIt_IsApplied() {
         WriteAllInstruments(AllInstruments);
         BuildSongs(Other);
         ViewPort.Goto.Execute(AllInstruments.ToString("X6"));
         ViewPort.Edit($"^{SoundTab.AllInstrumentsAnchor}[type.|h key. length. pan_sweep.|h wave_or_group::|h attack. decay. sustain. release.]128 ");

         var tab = CreateTab();

         Assert.Equal(AppliedNamed, tab.AllInstrumentsNote);
      }

      #endregion
   }
}
