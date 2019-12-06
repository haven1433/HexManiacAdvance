using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;
using System.Globalization;
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
      public const string WildTableName = "wild";
      public const string ItemsTableName = "items";
      public const string DexInfoTableName = "dexinfo";
      public const string TrainerTableName = "trainerdata";
      public const string EggMovesTableName = "eggmoves";
      public const string EvolutionTableName = "evolutions";
      public const string LevelMovesTableName = "lvlmoves";
      public const string MultichoiceTableName = "multichoice";
      public const string DecorationsTableName = "decorations";
      public const string RegionalDexTableName = "regionaldex";
      public const string NationalDexTableName = "nationaldex";
      public const string MoveDescriptionsName = "movedescriptions";
      public const string ConversionDexTableName = "hoennToNational";

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
            DecodeDexArrays();
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
            case Ruby: case Sapphire: source = 0x00FA58; break;
         }
         AddTable(source, EggMoveRun.PokemonNameTable, "[name\"\"11]");

         // movenames
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x000148; break;
            case Ruby: case Sapphire: source = 0x02E18C; break;
         }
         AddTable(source, EggMoveRun.MoveNamesTable, "[name\"\"13]");

         // abilitynames
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001C0; break;
            case Ruby: case Sapphire: source = 0x09FE64; break;
         }
         AddTable(source, "abilitynames", "[name\"\"13]");

         // trainerclassnames
         switch (gameCode) {
            case FireRed: case LeafGreen: source = AllSourcesToSameDestination(source)[1] + 0x9C; break;
            case Emerald: source = 0x0183B4; break;
            case Ruby: case Sapphire: source = 0x1217BC; break;
         }
         AddTable(source, "trainerclassnames", "[name\"\"13]");

         // types
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x0309C8; break;
            case Emerald: source = 0x0166F4; break;
            case Ruby: case Sapphire: source = 0x02E3A8; break;
         }
         AddTable(source, "types", "^[name\"\"7]");

         // abilitydescriptions
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001C4; break;
            case Ruby: case Sapphire: source = 0x09FE68; break;
         }
         AddTable(source, "abilitydescriptions", $"[description<{PCSRun.SharedFormatString}>]abilitynames");

         // movedescriptions
         switch (gameCode) {
            case Ruby: case Sapphire: source = 0x0A0494; break;
            case FireRed: source = 0x0E5440; break;
            case LeafGreen: source = 0x0E5418; break;
            case Emerald: source = 0x1C3EFC; break;
         }
         AddTable(source, MoveDescriptionsName, $"[description<{PCSRun.SharedFormatString}>]{EggMoveRun.MoveNamesTable}-1");

         // multichoice
         switch (gameCode) {
            case Ruby: case Sapphire: source = 0x0B50A0; break;
            case FireRed: source = 0x09CB58; break;
            case LeafGreen: source = 0x09CB2C; break;
            case Emerald: source = 0x0E1FB8; break;
         }
         AddTable(source, MultichoiceTableName, $"[options<[text<\"\"> unused::]/count> count::]");
      }

      private void DecodeDataArrays() {
         int source = 0;

         // pokestats
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001BC; break;
            case Ruby: case Sapphire: source = 0x010B64; break;
         }
         var format = $"[hp. attack. def. speed. spatk. spdef. type1.types type2.types catchRate. baseExp. evs: item1:{ItemsTableName} item2:{ItemsTableName} genderratio. steps2hatch. basehappiness. growthrate. egg1. egg2. ability1.abilitynames ability2.abilitynames runrate. unknown. padding:]{EggMoveRun.PokemonNameTable}";
         AddTable(source, "pokestats", format);

         // evolutions
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x42F6C; break;
            case Ruby: case Sapphire: source = 0x3F534; break;
            case Emerald: source = 0x6D140; break;
         }
         AddTable(source, EvolutionTableName, $"[[method: arg: species:{EggMoveRun.PokemonNameTable} unused:]5]{EggMoveRun.PokemonNameTable}");

         // items
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001C8; break;
            case Ruby: case Sapphire: source = 0x0A98F0; break;
         }
         format = $"[name\"\"14 index: price: holdeffect. param. description<{PCSRun.SharedFormatString}> keyitemvalue. bagkeyitem. pocket. type. fieldeffect<> battleusage:: battleeffect<> battleextra::]";
         AddTable(source, ItemsTableName, format);

         // movedata
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x0001CC; break;
            case Ruby: case Sapphire: source = 0x00CA54; break;
         }
         format = $"[effect. power. type.types accuracy. pp. effectAccuracy. target. priority. more::]{EggMoveRun.MoveNamesTable}";
         AddTable(source, "movedata", format);

         // lvlmoves
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x03EA7C; break;
            case Emerald: source = 0x06930C; break;
            case Ruby: case Sapphire: source = 0x03B7BC; break;
         }
         AddTable(source, LevelMovesTableName, $"[movesFromLevel<{PLMRun.SharedFormatString}>]{EggMoveRun.PokemonNameTable}");

         // tutormoves / tutorcompatibility
         if (gameCode != Ruby && gameCode != Sapphire) {
            switch (gameCode) {
               case FireRed: source = 0x120BE4; break;
               case LeafGreen: source = 0x120BBC; break;
               case Emerald: source = 0x1B236C; break;
            }
            format = $"[move:{EggMoveRun.MoveNamesTable}]" + (gameCode == Emerald ? 30 : 15);
            AddTable(source, MoveTutors, format);
            source += gameCode == Emerald ? 0x24 : 0x4C;
            format = $"[moves|b[]{MoveTutors}]{EggMoveRun.PokemonNameTable}";
            AddTable(source, TutorCompatibility, format);
         }

         // tmmoves
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x125A60; break;
            case Emerald: source = 0x1B6D10; break;
            case Ruby: case Sapphire: source = 0x06F038; break;
         }
         source = GetNextRun(source).Start;
         AddTable(source, TmMoves, $"[move:{EggMoveRun.MoveNamesTable}]58");

         // tmcompatibility
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x043C68; break;
            case Emerald: source = 0x06E048; break;
            case Ruby: case Sapphire: source = 0x0403B0; break;
         }
         AddTable(source, TmCompatibility, $"[moves|b[]{TmMoves}]{EggMoveRun.PokemonNameTable}");

         // hmmoves
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x0441DC; break;
            case Emerald: source = 0x06E828; break;
            case Ruby: case Sapphire: source = 0x040A24; break;
         }
         AddTable(source, HmMoves, $"[move:{EggMoveRun.MoveNamesTable}]8");

         // itemimages
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x098970; break;
            case Emerald: source = 0x1B0034; break;
            case Ruby: case Sapphire: source = -1; break;
         }
         source = GetNextRun(source).Start;
         AddTable(source, "itemimages", $"[image<> palette<>]{ItemsTableName}");

         // trainer teams
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x00FC00; break;
            case Emerald: source = 0x03587C; break;
            case Ruby: case Sapphire: source = 0x00D890; break;
         }
         AddTable(source, TrainerTableName, $"[structType.4 class.trainerclassnames introMusic. sprite. name\"\"12 item1:{ItemsTableName} item2:{ItemsTableName} item3:{ItemsTableName} item4:{ItemsTableName} doubleBattle:: ai:: pokemonCount:: pokemon<`tpt`>]");

         // decorations
         switch (gameCode) {
            case FireRed: case LeafGreen: case Emerald: source = 0x00014C; break;
            case Ruby: case Sapphire: source = 0x0B3AC8; break;
         }
         AddTable(source, DecorationsTableName, $"[id. name\"\"16 permission. shape. category. price:: description<\"\"> graphics<>]");

         // wild pokemon
         source = Find("0348048009E00000FFFF0000");
         string table(int length) => $"<[rate:: list<[low. high. species:pokenames]{length}>]1>";
         AddTable(source, WildTableName, $"[bank. map. unused: grass{table(12)} surf{table(5)} tree{table(5)} fish{table(10)}]");
      }

      private void DecodeDexArrays() {
         int source = 0;

         // regional dex
         switch (gameCode) {
            case Ruby: case Sapphire: source = 0x03F7F0; break;
            case FireRed: case LeafGreen: source = 0x0431F0; break;
            case Emerald: source = 0x06D3FC; break;
         }
         AddTable(source, RegionalDexTableName, "[index:]pokenames-1");

         // national dex
         switch (gameCode) {
            case Ruby: case Sapphire: source = 0x03F83C; break;
            case FireRed: case LeafGreen: source = 0x04323C; break;
            case Emerald: source = 0x06D448; break;
         }
         AddTable(source, NationalDexTableName, "[index:]pokenames-1");

         // hoenn-to-national conversion
         // hoenn[treecko]  =   1, national[treecko]  = 252, HoeennToNationalDex[ 1]= 252
         // hoenn[bulbasaur]= 203, national[bulbasaur]=   1, HoennToNationalDex[203]=   1
         // -> this table's values can be determined automatically based on the first two
         switch (gameCode) {
            case Ruby: case Sapphire: source = 0x03F888; break;
            case FireRed: case LeafGreen: source = 0x043288; break;
            case Emerald: source = 0x06D494; break;
         }
         AddTable(source, ConversionDexTableName, "[index:]pokenames-1");

         // dex info
         switch (gameCode) {
            case Ruby: case Sapphire: source = 0x08F508; break;
            case FireRed: source = 0x088E34; break;
            case LeafGreen: source = 0x088E30; break;
            case Emerald: source = 0x0BFA20; break;
         }
         // height is in decimeters
         // weight is in hectograms
         var format = "[species\"\"12 height: weight: description1<\"\"> description2<\"\"> unused: pokemonScale: pokemonOffset: trainerScale: trainerOffset: unused:]";
         if (gameCode == Emerald) format = "[species\"\"12 height: weight: description<\"\"> unused: pokemonScale: pokemonOffset: trainerScale: trainerOffset: usused:]";
         AddTable(source, DexInfoTableName, format);
      }

      private void DecodeStreams() {
         int source = 0;

         // eggmoves
         switch (gameCode) {
            case FireRed: case LeafGreen: source = 0x045C50; break;
            case Emerald: source = 0x0703F0; break;
            case Ruby: case Sapphire: source = 0x041B44; break;
         }
         ObserveAnchorWritten(noChangeDelta, EggMovesTableName, new EggMoveRun(this, ReadPointer(source)));
      }

      private IReadOnlyList<int> AllSourcesToSameDestination(int source) {
         var destination = ReadPointer(source);
         var run = GetNextRun(destination);
         return run.PointerSources;
      }

      private int Find(string bytesText) {
         var bytes = new List<byte>();
         while (bytesText.Length > 0) {
            bytes.Add(byte.Parse(bytesText.Substring(0, 2), NumberStyles.HexNumber));
            bytesText = bytesText.Substring(2);
         }

         for (int i = 0; i < RawData.Length; i++) {
            if (RawData[i] != bytes[0]) continue;
            if (Enumerable.Range(1, bytes.Count - 1).All(
               j => i + j < RawData.Length && RawData[i + j] == bytes[j]
            )) {
               return i + bytes.Count;
            }
         }

         return -1;
      }

      private void AddTable(int source, string name, string format) {
         if (source < 0 || source > RawData.Length) return;
         var destination = ReadPointer(source);
         if (destination < 0 || destination > RawData.Length) return;

         var interruptingRun = GetNextRun(destination);
         if (interruptingRun.Start < destination && interruptingRun is ArrayRun array) {
            var elementLength = array.ElementLength;
            var elementCount = (destination - array.Start) / array.ElementLength;
            array = array.Append(noChangeDelta, elementCount - array.ElementCount);
            ObserveAnchorWritten(noChangeDelta, GetAnchorFromAddress(-1, array.Start), array);
         }

         var errorInfo = ArrayRun.TryParse(this, name, format, destination, null, out var arrayRun);
         if (!errorInfo.HasError) {
            ObserveAnchorWritten(noChangeDelta, name, arrayRun);
         }
      }
   }
}
