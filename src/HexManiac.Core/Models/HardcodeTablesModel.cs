using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using System.Linq;
using static HavenSoft.HexManiac.Core.Models.AutoSearchModel;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// An alternative to the AutoSearchModel.
   /// Instead of using a 'smart' search algorithm to find all the data,
   /// follow hard-coded expected pointers to the known data.
   /// This should still be somewhat robust: the data may move, but the pointers to the data are more likely to be stable.
   ///
   /// Lengths of some tables are still calculated dynamically based on best-fit, so operations like adding pokemon from a separate tool should still be picked up correctly.
   /// </summary>
   public class HardcodeTablesModel : PokemonModel {
      private readonly string gameCode;
      private readonly ModelDelta noChangeDelta = new NoDataChangeDeltaModel();

      /// <summary>
      /// The first 0x100 bytes of the GBA rom is always the header.
      /// The next 0x100 bytes contains some tables and some startup code, but nothing interesting to point to.
      /// Choosing 0x200 might prevent us from seeing an actual anchor, but it will also remove a bunch
      ///      of false positives and keep us from getting conflicts with the RomName (see DecodeHeader).
      /// </summary>
      public override int EarliestAllowedAnchor => 0x200;

      public HardcodeTablesModel(byte[] data, StoredMetadata metadata = null) : base(data, metadata) {
         if (metadata != null && !metadata.IsEmpty) return;

         gameCode = string.Concat(Enumerable.Range(0xAC, 4).Select(i => ((char)data[i]).ToString()));

         // in vanilla emerald, this pointer isn't four-byte aligned
         // it's at the very front of the ROM, so if there's no metadata we can be pretty sure that the pointer is still there
         if (gameCode == Emerald && data[0x1C3] == 0x08) ObserveRunWritten(noChangeDelta, new PointerRun(0x1C0));

         var gamesToDecode = new[] { Ruby, Sapphire, Emerald, FireRed, LeafGreen };
         if (gamesToDecode.Contains(gameCode)) {
            DecodeHeader();
            DecodeNameArrays();
            DecodeDataArrays();
            DecodeStreams();
         }

         ResolveConflicts();
      }

      private void DecodeHeader() {
         ObserveAnchorWritten(noChangeDelta, "GameTitle", new AsciiRun(0xA0, 12));
         ObserveAnchorWritten(noChangeDelta, "GameCode", new AsciiRun(0xAC, 4));
         ObserveAnchorWritten(noChangeDelta, "MakerCode", new AsciiRun(0xB0, 2));

         if (gameCode != Ruby && gameCode != Sapphire) {
            ObserveAnchorWritten(noChangeDelta, "RomName", new AsciiRun(0x108, 0x20));
         }
      }

      private void DecodeNameArrays() {
         int source = 0;

         // pokenames
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x000144; break;
            case Ruby: case Sapphire:                   source = 0x00FA58; break;
         }
         AddTable(source, EggMoveRun.PokemonNameTable, "[name\"\"11]");

         // movenames
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x000148; break;
            case Ruby: case Sapphire:                   source = 0x02E180; break;
         }
         AddTable(source, EggMoveRun.MoveNamesTable, "[name\"\"13]");

         // abilitynames
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001C0; break;
            case Ruby: case Sapphire:                   source = 0x09FE64; break;
         }
         AddTable(source, "abilitynames", "[name\"\"13]");

         // trainerclassnames
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = AllSourcesToSameDestination(source)[1] + 0x9C; break;
            case Emerald:                               source = 0x0183B4; break;
            case Ruby: case Sapphire:                   source = 0x1217BC; break;
         }
         AddTable(source, "trainerclassnames", "[name\"\"13]");

         // types
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x0309C8; break;
            case Emerald:                               source = 0x0166F4; break;
            case Ruby: case Sapphire:                   source = 0x02E3A8; break;
         }
         AddTable(source, "types", "^[name\"\"7]");

         // abilitydescriptions
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001C4; break;
            case Ruby: case Sapphire:                   source = 0x09FE68; break;
         }
         AddTable(source, "abilitydescriptions", $"[description<{PCSRun.SharedFormatString}>]abilitynames");

      }

      private void DecodeDataArrays() {
         int source = 0;

         // pokestats
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001BC; break;
            case Ruby: case Sapphire:                   source = 0x010B64; break;
         }
         var format = $"[hp. attack. def. speed. spatk. spdef. type1.types type2.types catchRate. baseExp. evs: item1:items item2:items genderratio. steps2hatch. basehappiness. growthrate. egg1. egg2. ability1.abilitynames ability2.abilitynames runrate. unknown. padding:]{EggMoveRun.PokemonNameTable}";
         AddTable(source, "pokestats", format);

         // items
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001C8; break;
            case Ruby: case Sapphire:                   source = 0x0A98F0; break;
         }
         format = $"[name\"\"14 index: price: holdeffect: description<{PCSRun.SharedFormatString}> keyitemvalue. bagkeyitem. pocket. type. fieldeffect<> battleusage:: battleeffect<> battleextra::]";
         AddTable(source, "items", format);

         // movedata
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001CC; break;
            case Ruby: case Sapphire:                   source = 0x00CA54; break;
         }
         format = $"[effect. power. type.types accuracy. pp. effectAccuracy. target. priority. more::]{EggMoveRun.MoveNamesTable}";
         AddTable(source, "movedata", format);

         // lvlmoves
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x03EA7C; break;
            case Emerald:                               source = 0x06930C; break;
            case Ruby: case Sapphire:                   source = 0x03B7BC; break;
         }
         AddTable(source, "lvlmoves", $"[moves<{PLMRun.SharedFormatString}>]{EggMoveRun.PokemonNameTable}");

         // tutormoves / tutorcompatibility
         if (gameCode != Ruby && gameCode != Sapphire) {
            switch (gameCode) {
               case FireRed:                            source = 0x120BE4; break;
               case LeafGreen:                          source = 0x120BBC; break;
               case Emerald:                            source = 0x1B236C; break;
            }
            format = $"[move:{EggMoveRun.MoveNamesTable}]" + (gameCode == Emerald ? 30 : 15);
            AddTable(source, MoveTutors, format);
            source = GetNextRun(AllSourcesToSameDestination(source).Last() + 4).Start;
            format = $"[pokemon|b[]{MoveTutors}]{EggMoveRun.PokemonNameTable}";
            AddTable(source, TutorCompatibility, format);
         }

         // tmmoves
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x125A60; break;
            case Emerald:                               source = 0x1B6D10; break;
            case Ruby: case Sapphire:                   source = 0x06F038; break;
         }
         source = GetNextRun(source).Start;
         AddTable(source, TmMoves, $"[move:{EggMoveRun.MoveNamesTable}]58");

         // tmcompatibility
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x043C68; break;
            case Emerald:                               source = 0x06E048; break;
            case Ruby: case Sapphire:                   source = 0x0403B0; break;
         }
         AddTable(source, TmCompatibility, $"[pokemon|b[]{TmMoves}]{EggMoveRun.PokemonNameTable}");

         // hmmoves
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x0441DC; break;
            case Emerald:                               source = 0x06E828; break;
            case Ruby: case Sapphire:                   source = 0x040A24; break;
         }
         AddTable(source, HmMoves, $"[move:{EggMoveRun.MoveNamesTable}]8");

         // itemimages
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x098970; break;
            case Emerald:                               source = 0x1B0034; break;
            case Ruby: case Sapphire:                   source = -1;       break;
         }
         source = GetNextRun(source).Start;
         AddTable(source, "itemimages", "[image<> palette<>]items");
      }

      private void DecodeStreams() {
         int source = 0;

         // eggmoves
         switch (gameCode) {
            case FireRed: case LeafGreen:               source = 0x045C50; break;
            case Emerald:                               source = 0x0703F0; break;
            case Ruby: case Sapphire:                   source = 0x041B44; break;
         }

         ObserveAnchorWritten(noChangeDelta, "eggmoves", new EggMoveRun(this, ReadPointer(source)));
      }

      private IReadOnlyList<int> AllSourcesToSameDestination(int source) {
         var destination = ReadPointer(source);
         var run = GetNextRun(destination);
         return run.PointerSources;
      }

      private void AddTable(int source, string name, string format) {
         if (source < 0 || source > RawData.Length) return;
         var destination = ReadPointer(source);
         if (destination < 0 || destination > RawData.Length) return;
         var errorInfo = ArrayRun.TryParse(this, format, destination, null, out var arrayRun);
         if (!errorInfo.HasError) {
            ObserveAnchorWritten(noChangeDelta, name, arrayRun);
         }
      }
   }
}
