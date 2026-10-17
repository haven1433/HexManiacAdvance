using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The Music and Cave dropdowns of the map editor's header panel.</summary>
   public class MapHeaderTests : BaseViewModelTestClass {
      private const int HeaderStart = 0x100;

      // pokeemerald-expansion: no 'cave' byte, 'cave' is one bit of the flags (flags is the 11th byte)
      private const string ExpansionHeader = "[music:songnames layoutID: regionSectionID. weather. mapType. floorNumber. nightMusic:songnames flags.|t|allowBiking.|allowEscaping.|allowRunning.|showMapName.|writeSpecialVarIsEffect.|cave.|unused: battleType.]2";
      private const int ExpansionFlagsOffset = 10;
      private const int AllowBiking = 0x01, AllowRunning = 0x04, Cave = 0x20;

      // vanilla: 'cave' is a byte (the 6th)
      private const string VanillaHeader = "[music:songnames layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.]2";
      private const int VanillaCaveOffset = 5;

      /// <summary>A song list like the expansion's: it is as long as the biggest song number, and almost every entry is empty.</summary>
      private static List<string> SparseSongs() {
         var songs = Enumerable.Range(0, 0x10000).Select(i => (string)null).ToList();
         songs[0] = "MUS_DUMMY";
         songs[5] = "MUS_FIVE";
         songs[9] = "MUS_NINE";
         songs[0xFFFF] = "MUS_LAST";
         return songs;
      }

      private void CreateHeaders(string format, bool withSongs = true) {
         if (withSongs) Model.SetList(new ModelDelta(), "songnames", SparseSongs(), null, null);
         ViewPort.Edit("@" + HeaderStart.ToString("X3") + " ^header" + format + " ");
      }

      private MapHeaderViewModel HeaderOf(int element = 0) {
         var table = new ModelTable(Model, (ITableRun)Model.GetNextRun(HeaderStart), () => Token);
         return new MapHeaderViewModel(table[element], new Format(Model), () => Token);
      }

      private int ElementLength => ((ITableRun)Model.GetNextRun(HeaderStart)).ElementLength;

      #region Music

      [Fact]
      public void Music_SparseSongList_OffersOnlyTheNamedSongs() {
         CreateHeaders(ExpansionHeader);
         var header = HeaderOf();

         Assert.True(header.HasMusicOptions);
         Assert.Equal(new[] { "MUS_DUMMY", "MUS_FIVE", "MUS_NINE", "MUS_LAST" }, header.MusicFilter.AllOptions.Select(option => option.Text));
         Assert.Equal(new[] { 0, 5, 9, 0xFFFF }, header.MusicFilter.AllOptions.Select(option => option.Index));
      }

      [Fact]
      public void Music_ShowsTheNameOfTheCurrentSong() {
         CreateHeaders(ExpansionHeader);
         Model.WriteMultiByteValue(HeaderStart, 2, Token, 5);

         var header = HeaderOf();

         Assert.Equal("MUS_FIVE", header.MusicFilter.DisplayText);
         Assert.Equal(5, header.Music);
      }

      [Fact]
      public void Music_HeadersOfOneRomShareTheirOptions() {
         CreateHeaders(ExpansionHeader);

         var first = HeaderOf(0);
         var second = HeaderOf(1);

         Assert.Same(first.MusicFilter.AllOptions[1], second.MusicFilter.AllOptions[1]);
      }

      [Fact]
      public void Music_PickingASong_WritesItToTheRom() {
         CreateHeaders(ExpansionHeader);
         var header = HeaderOf();

         header.MusicFilter.SelectedIndex = 2; // MUS_NINE

         Assert.Equal(9, header.Music);
         Assert.Equal(9, Model[HeaderStart]);
         Assert.Equal("MUS_NINE", header.MusicFilter.DisplayText);
      }

      [Fact]
      public void Music_ChangedOutsideTheDropdown_UpdatesTheDropdown() {
         CreateHeaders(ExpansionHeader);
         var header = HeaderOf();

         header.Music = 0xFFFF;

         Assert.Equal("MUS_LAST", header.MusicFilter.DisplayText);
      }

      [Fact]
      public void Music_SongWithoutAName_IsShownAsSongNumber() {
         CreateHeaders(ExpansionHeader);
         var first = HeaderOf(0);
         var second = HeaderOf(1);

         first.Music = 7;
         first.UpdateFromModel();

         Assert.Equal("song_7", first.MusicFilter.DisplayText);
         Assert.Equal(new[] { 0, 5, 7, 9, 0xFFFF }, first.MusicFilter.AllOptions.Select(option => option.Index)); // in numeric order
         Assert.Equal(4, second.MusicFilter.AllOptions.Count);                                                   // the other headers don't see it
      }

      [Fact]
      public void Music_NoSongList_HidesTheDropdown() {
         CreateHeaders(ExpansionHeader, withSongs: false);

         var header = HeaderOf();
         header.Music = 3;

         Assert.False(header.HasMusicOptions);
         Assert.Equal(3, header.Music);
      }

      #endregion

      #region Cave

      [Fact]
      public void Cave_Expansion_IsABitOfTheFlags() {
         CreateHeaders(ExpansionHeader);
         Model[HeaderStart + ExpansionFlagsOffset] = AllowBiking | AllowRunning | Cave;

         var header = HeaderOf();

         Assert.Equal(1, header.Cave);
         Assert.Equal(2, header.CaveOptions.Count);
         Assert.Equal("Normal", header.CaveOptions[0]);
         Assert.Equal("Flash Usable", header.CaveOptions[1]);
      }

      [Fact]
      public void Cave_Expansion_Off_ReadsNormal() {
         CreateHeaders(ExpansionHeader);
         Model[HeaderStart + ExpansionFlagsOffset] = AllowBiking;

         Assert.Equal(0, HeaderOf().Cave);
      }

      [Fact]
      public void Cave_Expansion_WritingChangesOnlyTheCaveBit() {
         CreateHeaders(ExpansionHeader);
         Model[HeaderStart + ExpansionFlagsOffset] = AllowBiking | AllowRunning | Cave;
         var header = HeaderOf();
         var notifications = new List<string>();
         header.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

         header.Cave = 0;
         Assert.Equal(AllowBiking | AllowRunning, Model[HeaderStart + ExpansionFlagsOffset]);
         Assert.Contains(nameof(MapHeaderViewModel.Cave), notifications);

         header.Cave = 1;
         Assert.Equal(AllowBiking | AllowRunning | Cave, Model[HeaderStart + ExpansionFlagsOffset]);
      }

      [Fact]
      public void Cave_Expansion_ValuesThatAreNotAChoice_AreIgnored() {
         CreateHeaders(ExpansionHeader);
         Model[HeaderStart + ExpansionFlagsOffset] = Cave;
         var header = HeaderOf();

         header.Cave = -1; // what a dropdown reports when its selection is cleared
         header.Cave = 2;

         Assert.Equal(Cave, Model[HeaderStart + ExpansionFlagsOffset]);
         Assert.Equal(1, header.Cave);
      }

      [Fact]
      public void Cave_Vanilla_IsAByte() {
         CreateHeaders(VanillaHeader);
         Model[HeaderStart + VanillaCaveOffset] = 2;

         var header = HeaderOf();

         Assert.Equal(2, header.Cave);
         Assert.Equal(3, header.CaveOptions.Count);

         header.Cave = 1;
         Assert.Equal(1, Model[HeaderStart + VanillaCaveOffset]);

         header.Cave = -1;
         Assert.Equal(1, Model[HeaderStart + VanillaCaveOffset]);
      }

      [Fact]
      public void Cave_WritesToTheElementOfTheHeader_NotTheFirstOne() {
         CreateHeaders(VanillaHeader);
         var header = HeaderOf(1);

         header.Cave = 2;

         Assert.Equal(0, Model[HeaderStart + VanillaCaveOffset]);
         Assert.Equal(2, Model[HeaderStart + ElementLength + VanillaCaveOffset]);
      }

      [Fact]
      public void Cave_NoSuchField_ReadsMinusOneAndIgnoresWrites() {
         CreateHeaders("[music:songnames layoutID: regionSectionID. weather. mapType. battleType.]2");
         var header = HeaderOf();

         header.Cave = 1;

         Assert.Equal(-1, header.Cave);
      }

      #endregion
   }
}
