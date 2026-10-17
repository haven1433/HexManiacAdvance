namespace HavenSoft.HexManiac.Core.ViewModels {
   public static partial class ShortcutFallbackIcons {
      // The pixel art. One text row per pixel row: "." is transparent, every other character is a colour of that icon's legend ("c=RRGGBB", 8 bits per channel,
      // reduced to the 5 bits per channel of the GBA when the icon is first used). Drawn by hand for this program (tools: draw_icons.py); none of it comes from a game.

      private const string GenericLegend = "k=24242c a=b8b8c8 A=8888a0 w=e8e8f0 l=f8f8f8 r=e04040 d=4868c8 y=e8c040";
      private static readonly string[] GenericRows = {
         "...kkkkkkkkkkkkkkk......",
         "..kwaaaaaaaaaaaaaAk.....",
         "..kwaaaaaaaaaaaaaAk.....",
         "..kwaaaaaaaaaaaaaAkkk...",
         "..kwaaaaaaaaaaaaaaaaAk..",
         "..kwaarrrrrrrrrrrraaAk..",
         "..kwaarrrrrrrrrrrraaAk..",
         "..kwaallllllllllllaaAk..",
         "..kwaallllllllllllaaAk..",
         "..kwaallddddddddllaaAk..",
         "..kwaallllllllllllaaAk..",
         "..kwaallddddddllllaaAk..",
         "..kwaallllllllllllaaAk..",
         "..kwaallllllllllllaaAk..",
         "..kwaaaaaaaaaaaaaaaaAk..",
         "..kwaaaaaaaaaaaaaaaaAk..",
         "..kwaaaaaaaaaaaaaaaaAk..",
         "..kwaaaaaaaaaaaaaaaaAk..",
         "..kAAAaaaaaaaaaaaaAAAk..",
         "...kkkwaaaaaaaaaaAkkk...",
         ".....kwyayayayayaAk.....",
         ".....kwyayayayayaAk.....",
         ".....kAyAyAyAyAyAAk.....",
         "......kkkkkkkkkkkk......",
      };

      private const string PokemonLegend = "k=2c2438 c=fcf4d4 s=d8c8a0 g=58b858 G=307838 w=ffffff";
      private static readonly string[] PokemonRows = {
         "........................",
         "...........kk...........",
         ".........kkcskk.........",
         "........kcccccsk........",
         ".......kwwcccccsk.......",
         "......kwwccgccccsk......",
         ".....kcwccgggcccsk......",
         ".....kccccgggccccsk.....",
         "....kccccccgcccccsk.....",
         "....kcccccccccgggcsk....",
         "....kccccccccggggcsk....",
         "...kcccccccccggggcsk....",
         "...kccccgggccggggcsk....",
         "...kcccgggggccccccsk....",
         "...kcccgggggccccccsk....",
         "...kcccgggggccccccsk....",
         "...kscccgggccgggccsk....",
         "....kccccccccgggcssk....",
         "....kscccccccgggcsk.....",
         ".....kscccccccgcssk.....",
         "......ksccccccsssk......",
         ".......kssssssskk.......",
         "........kkkkkkk.........",
         "........................",
      };

      private const string TrainersLegend = "k=2c1c20 r=e03838 R=a82020 w=f8f8f8 f=f8c8a0 F=d89870 h=703818 s=c06848";
      private static readonly string[] TrainersRows = {
         "........................",
         "........................",
         "........kkkkkkkk........",
         "......kkrrrrrrrrkk......",
         ".....krrrrrrrrrrrrk.....",
         ".....krrrwwrrrrrrrk.....",
         "....krrrrwwrrrrrrrRk....",
         "....kRRRRRRRRRRRRRRk....",
         "....kRRRRRRRRRRRRRRRk...",
         "....khfffffffffffFhk....",
         ".....kfffffffffffFk.....",
         ".....kffkffffffkffFk....",
         ".....kffkffffffkffFk....",
         ".....kffffffffffffFk....",
         "......kffffssffffFk.....",
         ".......kFFffffFFFk......",
         "....kkkkkkkFFFFkkkkkk...",
         "...krrrrrkwwwwwkrrrrrk..",
         "..krrrrrrkwwwwwkrrrrrrk.",
         "..krrrrrrrkwwwkrrrrrrRk.",
         "..krrrrrrrrkkkrrrrrrrRk.",
         "..krrrrrrrrrrrrrrrrrrRk.",
         "..kkkkkkkkkkkkkkkkkkkkk.",
         "........................",
      };

      private const string MovesLegend = "k=3a2008 y=f8d838 o=e08818 O=b05808 w=fff8b0 h=fffce0";
      private static readonly string[] MovesRows = {
         "...............kk.......",
         "..............kwok......",
         ".............khyok......",
         "............khyok.......",
         "...........khyyok.......",
         "..........kwyyok........",
         ".........kwyyyok........",
         "........kwyyyok.........",
         "........kwyyyok.........",
         ".......kwyyyokkkkkk.....",
         "......kwyyyyyOyyyyok....",
         ".....kwyyyyyyOOyyok.....",
         "....kooooooyyyyyok......",
         ".....kkkkkkwyyyok.......",
         ".........kwyyyok........",
         ".........kwyyok.........",
         ".........kwyok..........",
         "........kwOok...........",
         "........kwyOk...........",
         "........kwok............",
         ".......kwok.............",
         ".......kok..............",
         "........k...............",
         "........................",
      };

      private const string ItemsLegend = "k=24203c l=d8ecf8 e=a8c0e0 p=a068e0 P=7040b8 w=ffffff n=c89050 N=a07030";
      private static readonly string[] ItemsRows = {
         "........knnnnnnk........",
         "........knNnnnnk........",
         "........knnnnNnk........",
         ".......klnnnnnnek.......",
         ".......klllllllek.......",
         "........klllllek........",
         "........klwlllek........",
         "........klwlllek........",
         "........klwlllek........",
         ".......klllllllekk......",
         "......kllllllllllek.....",
         ".....klllllllllllek.....",
         "....klllllllllllllek....",
         "....kppppppppppppppPk...",
         "....kppwpppppppppppPk...",
         "....kpwppppppppppppPk...",
         "....kwwppppppppppppPk...",
         "....kppppppppppppppPk...",
         "....kPppppppppppppPk....",
         ".....kPpppppppppppPk....",
         "......kPpppppppppPk.....",
         ".......kPPpppppPPk......",
         "........kkPPPPPkk.......",
         "..........kkkkk.........",
      };

      private const string MapsLegend = "k=2c2018 e=f4e4b0 f=d8c488 g=60b860 b=58a0e8 n=a06830 r=e83030 w=ffffff";
      private static readonly string[] MapsRows = {
         "........................",
         "........................",
         ".kkkkkkkkkkkkkkkkkkkkkk.",
         "kggggggggggbbbbbbbbbbbek",
         "kgggggggggebbbbbbbbbbbek",
         "kgggggggggebbbbbbbbbbbek",
         "kgggggggggeebbbbbbbbbbek",
         "kgggnggggeeebbbbbbbbbbek",
         "kggggngggeeebbbbeebbbbek",
         "kgggggnggeeeebbfeeeeebek",
         "kggggggnggeeeeefeeeeeeek",
         "kgggggggngeeeeefeeeeeeek",
         "kggggggggngeeeeferrreeek",
         "kgggggggggngeeefrrrrreek",
         "kgggggggggggneefrrwrreek",
         "kggggggggggeeenfrrrrreek",
         "kgggggggggeeeeefnrrreeek",
         "kggggggggeeeeeefeeeeeeek",
         "kgggggggfeeeeeefeeeeeeek",
         "kggggggefeeeeeefeeeeeeek",
         "keeeeeeeeeeeeeeeeeeeeeek",
         ".kkkkkkkkkkkkkkkkkkkkkk.",
         "........................",
         "........................",
      };

      private const string SoundLegend = "k=2c1c24 w=ece8d8 W=ffffff s=b0b4cc y=f0c030 Y=b88818 L=ffe888 r=e04030 R=a02020 h=ff9880 z=303040";
      private static readonly string[] SoundRows = {
         ".................kLYk...",
         "................kLyyYk..",
         "...............kLyyyyYk.",
         "..............kWyyyyyYk.",
         ".............kWwwyyyYk..",
         "............kWwwwwyYk...",
         "...........kWwzzwwsk....",
         "..........kWwwwwwsk.....",
         ".........kWwzzwwsk......",
         "........kWwwwwwsk.......",
         "......kkWwzzwwsk........",
         ".....kLwwwwwwsk.........",
         "....kLyywwwwsk..........",
         "...khyyyywwsk...........",
         "..khhryyyysk............",
         ".krhrrryyyYk............",
         ".krrrrrryyRk............",
         ".krrrrrrrrRk............",
         ".krrrrrrrrRk............",
         ".kRrrrrrrRRk............",
         "..kRrrrrRRk.............",
         "...kRRRRRk..............",
         "....kkkkk...............",
         "........................",
      };

      private const string OverworldSpritesLegend = "k=1c1c24 g=38a058 G=207040 w=e8f8e8 h=583018 f=f8c8a0 F=d89870 s=c06848 b=3060d8 B=2040a0 d=605048";
      private static readonly string[] OverworldSpritesRows = {
         "................",
         "....kkkkkkkk....",
         "...kggggggggk...",
         "..kgggwwgggggk..",
         "..kggggggggggk..",
         "..kGGGGGGGGGGk..",
         ".kkhhhhhhhhhhkk.",
         ".khfffffffffffhk",
         ".kffkffffffkfffk",
         ".kffkffffffkffFk",
         "..kffffssffffFk.",
         "...kFFffffFFk...",
         "..kkkkkkkkkkkk..",
         ".kbbbkbbbbkbbbk.",
         "kbbbbkbbbbkbbbbk",
         "kfbbbkbbbbkbbbfk",
         "kffbbkBBBBkbbffk",
         ".kkbbbbbbbbbbkk.",
         "..kbbbbbbbbbbk..",
         "..kBBBBBBBBBBk..",
         "..kkdddkkdddkk..",
         "..kdddddkdddddk.",
         "..kdddkk.kdddk..",
         "..kkkk...kkkk...",
      };

      private const string AnimatedTilesLegend = "m=88d0a8 G=3c8c40 L=60b058 p=f06868 q=f8a8a0 y=f8f088";
      private static readonly string[] AnimatedTilesRows = {
         "mmmmmmmmmmmmmmmm",
         "mmmmmmmmmmmmmmmm",
         "mmmmmmmmGGGGGLmm",
         "mmmmmmmGGLqpqGGm",
         "mmmmmmmGGGpypGGm",
         "mmmmmmmGLGqpqGGm",
         "mmmmmmmGGGGLGGGm",
         "mmGqpqmLGGGGGGLm",
         "mGGpypGGGGLGGGmm",
         "mGGqpqLGGGGGGLGm",
         "mGLGGGGGGqpqGGGm",
         "mGGGGLGGGpypLGGm",
         "mLGGGGGGLqpqGGGm",
         "mGGGLGGGGGGLGGGm",
         "mmGGGGGLGGGGGGLm",
         "mmmmmmmGGGLGGGmm",
      };

      private const string CharacterCustomizationLegend = "k=1c1418 h=382830 H=584848 d=8a5a3c D=6a3e28 m=a86848 w=ffffff b=3878e8 B=2050b0";
      private static readonly string[] CharacterCustomizationRows = {
         "........................",
         "........................",
         "........kkkkkkkk........",
         "......kkhhhhhhhhkk......",
         ".....khhhHhhhhhhhhk.....",
         "....khhhhhhhhhhhhhhk....",
         "....khhhhhhhhhhhhhhk....",
         "....khhdddddddddddhk....",
         "....khddddddddddddhk....",
         ".....kddddddddddddk.....",
         ".....kddwddddddwddk.....",
         ".....kddkddddddkddk.....",
         ".....kddddddddddddk.....",
         "......kddddmmddddDk.....",
         ".......kDDddddDDDk......",
         "....kkkkkkDDDDkkkkkk....",
         "...kbbbbbkddddkbbbbbk...",
         "..kbbbbbbkddddkbbbbbbk..",
         "..kbbbbbbbkddkbbbbbbBk..",
         "..kbbbbbbbbkkbbbbbbbBk..",
         "..kbbbbbbbbbbbbbbbbbBk..",
         "..kkkkkkkkkkkkkkkkkkkk..",
         "........................",
         "........................",
      };

      // Professor Birch's bag (the bag of the starter-choosing screen of Emerald), redrawn small (cut down from the game's picture to 11 colours): an outline, five browns, the cream paper, the grey-blue lining.
      // 38x22: wider than the other pictures, like the bag. The Intro Manager button shows the real bag of the loaded ROM when it can find it (BirchBag), and this one otherwise (FireRed has no such bag).
      private const string IntroManagerLegend = "k=2e2428 d=5b403f D=4f585f b=84534a B=966f53 m=b18f61 h=bc9b5c H=d5a65b t=deac5a T=bdad95 s=dbd2cc";
      private static readonly string[] IntroManagerRows = {
         "...........sht........................",
         ".........stttttht.........dkkd........",
         ".....dddBtttttttttht..dddddddddddb....",
         "....dddhtttttttttthHhmbddddddkk...Bd..",
         "...ddbHtttttttttHHHHthhhmbdk.......B..",
         "...kBtmhtttttttthHHttHhhhhhh........d.",
         "...bttthmmHtttthHHtttHhhhhhhm.......b.",
         "..tHHHtHTsmmmHHHHttttHhhhhhhBd.....mb.",
         ".hhttthssTsssTmmtttttHhhhhhBbd.....bbd",
         "hBmhHtHTsTTsssssTmhtHHhhhhBbbd....bbbb",
         "mBBBmhHHhssTsssssssTmhhhhBbbbd..Bbbbd.",
         ".dbbbBmhhmTsTsssssssmBBhbbbdbbbbbbBbd.",
         ".kkdbbbbBhhmsTssssTBhhmbdbbdbbbbbbbd..",
         ".dbdkddbbbBmmTTssTbmhhBddbbdbbbbbbd...",
         "..dbdkkkdbbbBBmTTmhmmBbddbdbbbdddD....",
         "....dkkbkkddbbbBhhhhBbkbddkdDkDD......",
         ".....kdbbdkDdbbbbmhmbdkbdkDDDT........",
         ".....kkkdbDDDDDbbbbbbkdbkDD...........",
         "......kkkkkDDDDDdbbdkdddDD............",
         "........kkkDDDDDDkkkdbdDD.............",
         "...........BDDDkddbbbdDD..............",
         "...............DddddkDT...............",
      };

      private static (string legend, string[] rows) ArtFor(ShortcutKind kind) {
         switch (kind) {
            case ShortcutKind.Generic: return (GenericLegend, GenericRows);
            case ShortcutKind.Pokemon: return (PokemonLegend, PokemonRows);
            case ShortcutKind.Trainers: return (TrainersLegend, TrainersRows);
            case ShortcutKind.Moves: return (MovesLegend, MovesRows);
            case ShortcutKind.Items: return (ItemsLegend, ItemsRows);
            case ShortcutKind.Maps: return (MapsLegend, MapsRows);
            case ShortcutKind.Sound: return (SoundLegend, SoundRows);
            case ShortcutKind.OverworldSprites: return (OverworldSpritesLegend, OverworldSpritesRows);
            case ShortcutKind.AnimatedTiles: return (AnimatedTilesLegend, AnimatedTilesRows);
            case ShortcutKind.CharacterCustomization: return (CharacterCustomizationLegend, CharacterCustomizationRows);
            case ShortcutKind.IntroManager: return (IntroManagerLegend, IntroManagerRows);
            default: return (GenericLegend, GenericRows);
         }
      }
   }
}
