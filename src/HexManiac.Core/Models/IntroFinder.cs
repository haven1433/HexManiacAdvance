using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Finds the intro's settings by reading the game's code. Nothing here knows an address: every place is found from what the code does, so it keeps working when the code moved (expanded ROMs, patched games).
   /// The shapes it looks for were checked against Emerald, the CUBE hack (built with a newer compiler) and FireRed 1.0.
   /// </summary>
   internal static class IntroFinder {
      private const int MaxSpecies = 2000;

      #region Helpers

      private static IntroConstant Const(byte[] data, int use, int register) {
         var constant = IntroThumb.FindConstant(data, use, register);
         if (constant is IntroLiteral literal) literal.Loads = Math.Max(1, IntroThumb.FindLiteralLoads(data, literal.Address).Count);
         return constant;
      }

      private static bool IsPointer(byte[] data, int address) {
         var value = IntroThumb.Word(data, address);
         return value >= IntroThumb.RomBase + 0x200 && value < IntroThumb.RomBase + data.Length;
      }

      private static int Target(byte[] data, int address) => IntroThumb.Word(data, address) - IntroThumb.RomBase;

      private static bool Contains(byte[] data, int start, int length, params int[] halves) {
         foreach (var half in halves) {
            var found = false;
            for (int p = start; p < start + length && !found; p += 2) found = IntroThumb.Half(data, p) == half;
            if (!found) return false;
         }
         return true;
      }

      private static int FirstCall(byte[] data, int start, int length, out int target) {
         target = -1;
         for (int p = start; p < start + length; p += 2) if (IntroThumb.TryBranchLink(data, p, out target)) return p;
         return -1;
      }

      /// <summary>PlayBGM(song): a small function that calls the sound engine and compares with 0xFFFF ("no music").</summary>
      private static bool IsPlayBgm(byte[] data, int function) {
         if (function < 0x200 || function + 0x40 > data.Length || (function & 1) != 0) return false;
         if (IntroThumb.Half(data, function) != 0xB500) return false;
         if (FirstCall(data, function + 2, 0x20, out _) < 0) return false;
         for (int p = function + 0x10; p < function + 0x30; p += 4) {
            var word = IntroThumb.Word(data, p);
            if (word == 0xFFFF || word == unchecked((int)0xFFFF0001)) return true;
         }
         return false;
      }

      /// <summary>PlayCry(species, mode): a function that sets up the volume (85) and pitch values of the sound engine.</summary>
      private static bool IsPlayCry(byte[] data, int function) {
         if (function < 0x200 || function + 0x40 > data.Length || (function & 1) != 0) return false;
         if ((IntroThumb.Half(data, function) & 0xFF00) != 0xB500) return false;
         return Contains(data, function, 0x40, 0x2255, 0x2278) && (Contains(data, function, 0x40, 0x230A) || Contains(data, function, 0x40, 0x330A));
      }

      private static IEnumerable<int> CallsTo(byte[] data, int target, int from, int to) {
         from = Math.Max(0x200, from) & ~1;
         to = Math.Min(data.Length - 4, to);
         for (int p = from; p < to; p += 2) {
            if (IntroThumb.TryBranchLink(data, p, out var t) && t == target) yield return p;
         }
      }

      private static IntroSetting Setting(string id, string title, string description, IntroValueKind kind, params IntroConstant[] constants) {
         return new IntroSetting(id, title, description, kind, constants.Where(constant => constant != null));
      }

      #endregion

      #region Music (shared by Emerald and FireRed)

      /// <summary>
      /// The professor's music is started right after the function that creates the professor's Pokémon objects returns to its caller, with a call to PlayBGM.
      /// The title music is the one call of the sound engine's 'start song' function, from the title screen function, whose number is a song (not a sound effect).
      /// </summary>
      private static void FindMusic(IntroManager manager, byte[] data, int speechFunction, string professor, List<string> problems,
         out IntroSetting professorMusic, out IntroSetting titleMusic, out List<int> callers, out int titleCall) {
         professorMusic = null;
         titleMusic = null;
         titleCall = -1;
         callers = CallsTo(data, speechFunction, speechFunction - 0x40000, speechFunction + 0x40000).ToList();
         int m4a = -1;
         foreach (var caller in callers) {
            for (int q = caller + 4; q < caller + 0x80; q += 2) {
               if (!IntroThumb.TryBranchLink(data, q, out var target) || !IsPlayBgm(data, target)) continue;
               var constant = Const(data, q, 0);
               if (constant == null || constant.Read(data) < 0x100 || constant.Read(data) > 0xFFFF) continue;
               professorMusic = Setting("professorMusic", $"Music during {professor}'s speech",
                  $"The song that plays while {professor} talks, from his first words until the player has a name.", IntroValueKind.Song, constant);
               FirstCall(data, target + 2, 0x20, out m4a);
               break;
            }
            if (professorMusic != null) break;
         }
         if (professorMusic == null) { problems.Add($"The music of {professor}'s speech was not found."); return; }
         if (m4a < 0) { problems.Add("The sound engine call was not found, so the title music can't be found."); return; }

         var candidates = new List<(int call, IntroConstant constant)>();
         for (int p = 0x200; p < data.Length - 4; p += 2) {
            if (!IntroThumb.TryBranchLink(data, p, out var target) || target != m4a) continue;
            var constant = Const(data, p, 0);
            if (constant == null) continue;
            var value = constant.Read(data);
            if (value < 0x100 || value > 0x7FFF) continue;
            if (!AfterReadsMainState(data, p) && !PrologueReadsMainState(data, p)) continue;
            candidates.Add((p, constant));
         }
         if (candidates.Count == 1) {
            titleCall = candidates[0].call;
            titleMusic = Setting("titleMusic", "Title screen music", "The song that plays on the title screen, the screen with the game's logo that waits for the player to press Start.", IntroValueKind.Song, candidates[0].constant);
         } else {
            problems.Add(candidates.Count == 0 ? "The title screen music was not found." : "More than one place looked like the title screen music, so none was used.");
         }
      }

      // gMain.state is at offset 0x438 of gMain: movs rX, #135 ; lsls rX, rX, #3
      private static bool HasStateOffset(byte[] data, int start, int length) {
         bool movs = false;
         for (int p = start; p < start + length; p += 2) {
            var half = IntroThumb.Half(data, p);
            if ((half & 0xF8FF) == 0x2087) movs = true;
            else if (movs && (half & 0xFFC0) == 0x00C0) return true;
         }
         return false;
      }

      private static bool AfterReadsMainState(byte[] data, int call) {
         if (!HasStateOffset(data, call + 4, 28)) return false;
         for (int p = call + 4; p < call + 32; p += 2) if ((IntroThumb.Half(data, p) & 0xF8FF) == 0x2005) return true;
         return false;
      }

      private static bool PrologueReadsMainState(byte[] data, int call) {
         var start = IntroThumb.FunctionStart(data, call);
         return start >= 0 && HasStateOffset(data, start, 32);
      }

      #endregion

      #region Emerald

      public static bool FindEmerald(IntroManager manager, byte[] data, List<string> problems) {
         // the new game speech creates Birch's object with AddNewGameBirchObject(136, 60, 1): three 'movs' in any order, then the call
         var anchors = new List<int>();
         for (int p = 0x200; p < data.Length - 8; p += 2) {
            var a = IntroThumb.Half(data, p);
            if (a != 0x2088 && a != 0x213C && a != 0x2201) continue;
            var b = IntroThumb.Half(data, p + 2);
            var c = IntroThumb.Half(data, p + 4);
            if (a == b || b == c || a == c) continue;
            if ((b != 0x2088 && b != 0x213C && b != 0x2201) || (c != 0x2088 && c != 0x213C && c != 0x2201)) continue;
            if (IntroThumb.TryBranchLink(data, p + 6, out _)) anchors.Add(p + 6);
         }
         if (anchors.Count != 1) {
            problems.Add(anchors.Count == 0 ? "Professor Birch's speech code was not found." : "Professor Birch's speech code was found more than once, so none was used.");
            return false;
         }
         var anchor = anchors[0];
         IntroThumb.TryBranchLink(data, anchor, out var addBirch);
         var function = IntroThumb.FunctionStart(data, anchor);
         if (function < 0) { problems.Add("The start of Professor Birch's speech function was not found."); return false; }

         var species = FindEmeraldSpecies(manager, data, anchor, function, problems);
         FindMusic(manager, data, function, "Birch", problems, out var professorMusic, out var titleMusic, out _, out _);
         var picture = FindBirchPicture(data, addBirch, problems);
         manager.Set(IntroGame.Emerald, species, professorMusic, titleMusic, null, picture);
         return true;
      }

      private static IntroSetting FindEmeraldSpecies(IntroManager manager, byte[] data, int anchor, int function, List<string> problems) {
         IntroConstant first = null, second = null;

         // site A: the picture of the Pokémon next to Birch, made by the first call after Birch's object
         var call = FirstCall(data, anchor + 4, 0x300, out var helper);
         if (call >= 0) {
            if (Contains(data, call - 12, 12, 0x2064, 0x214B)) {
               // Emerald: CreateMonPicSprite_Affine is a small helper that loads the species from its own literal pool
               for (int p = helper; p < helper + 0x20 && first == null; p += 2) {
                  var literal = IntroThumb.LiteralAddress(data, p);
                  if (literal < 0) continue;
                  var value = IntroThumb.Word(data, literal);
                  if (value < 1 || value > MaxSpecies) continue;
                  first = new IntroLiteral(literal, p) { Loads = Math.Max(1, IntroThumb.FindLiteralLoads(data, literal).Count) };
               }
            } else {
               // a newer compiler put the helper inside the function: the species is the first argument
               var constant = Const(data, call, 0);
               if (constant != null && constant.Read(data) >= 1 && constant.Read(data) <= MaxSpecies) first = constant;
            }
         }

         // site B: the Poké Ball that releases the Pokémon. It stores the species for the call (str rX, [sp, #16]) after the position 112, 58.
         var matches = new List<IntroConstant>();
         var lo = Math.Max(0x200, function - 0x3000);
         var hi = Math.Min(data.Length - 4, function + 0x3000);
         for (int p = lo; p < hi; p += 2) {
            var half = IntroThumb.Half(data, p);
            if ((half & 0xF8FF) != 0x9004) continue;
            var next = FirstCall(data, p + 2, 28, out _);
            if (next < 0) continue;
            var window = Math.Max(0, next - 0x20);
            if (!Contains(data, window, next - window, 0x2270)) continue;
            if (!Contains(data, window, next - window, 0x233A) && !Contains(data, window, next - window, 0x331A)) continue;
            var constant = Const(data, p, (half >> 8) & 7);
            if (constant != null && constant.Read(data) >= 1 && constant.Read(data) <= MaxSpecies) matches.Add(constant);
         }
         if (first != null) second = matches.FirstOrDefault(m => m.Read(data) == first.Read(data) && m.Address != first.Address) ?? matches.FirstOrDefault(m => m.Address != first.Address);
         else second = matches.FirstOrDefault();

         if (first == null) problems.Add("The picture of Birch's Pokémon was not found.");
         if (second == null) problems.Add("The Poké Ball of Birch's Pokémon was not found.");
         if (first == null && second == null) return null;
         return Setting("species", "Pokémon in Birch's speech",
            "The Pokémon that Professor Birch shows in the speech: its picture appears next to Birch and its cry plays when it is released from the Poké Ball.",
            IntroValueKind.Species, first, second);
      }

      /// <summary>Birch's sprite template (tags 0xFFFF and 0x1006) is next to the data that AddNewGameBirchObject loads a pointer to. It points at a frame (pixels, 0x800 bytes) and a palette structure.</summary>
      private static IntroPicture FindBirchPicture(byte[] data, int addBirch, List<string> problems) {
         for (int p = addBirch; p < addBirch + 0x60; p += 2) {
            var literal = IntroThumb.LiteralAddress(data, p);
            if (literal < 0 || !IsPointer(data, literal)) continue;
            var baseAddress = Target(data, literal);
            for (int offset = -0x80; offset <= 0x180; offset += 4) {
               var template = baseAddress + offset;
               if (template < 0x200 || template + 0x20 > data.Length || IntroThumb.Word(data, template) != 0x1006FFFF) continue;
               var frame = Target(data, template + 0xC);
               if (!IsPointer(data, template + 0xC) || frame < 0 || frame + 8 > data.Length) continue;
               if (!IsPointer(data, frame) || IntroThumb.Word(data, frame + 4) != BirchPicture.FrameSize) continue;
               // the palette structure {pointer, 0x1006} is just before the template
               for (int palette = template - 4; palette >= template - 0x40; palette -= 4) {
                  if (IntroThumb.Word(data, palette + 4) == 0x1006 && IsPointer(data, palette)) return new BirchPicture(frame, palette);
               }
            }
         }
         problems.Add("Professor Birch's picture was not found.");
         return null;
      }

      #endregion

      #region FireRed

      public static bool FindFireRed(IntroManager manager, byte[] data, List<string> problems) {
         var model = manager.Model;
         var front = model.GetTable(HardcodeTablesModel.FrontSpritesTable);
         var palettes = model.GetTable(HardcodeTablesModel.PokePalettesTable);
         if (front == null || palettes == null) { problems.Add("The Pokémon picture tables were not found."); return false; }

         // the speech picks its Pokémon by pointing at its entry of the picture table and of the palette table, in two literals next to each other
         var count = Math.Min(front.ElementCount, palettes.ElementCount);
         var literals = new List<int>();
         for (int a = 0x200; a < data.Length - 8; a += 4) {
            var value = IntroThumb.Word(data, a) - IntroThumb.RomBase - front.Start;
            if (value < front.ElementLength || value % front.ElementLength != 0) continue;
            var entry = value / front.ElementLength;
            if (entry >= count) continue;
            if (IntroThumb.Word(data, a + 4) - IntroThumb.RomBase - palettes.Start != entry * palettes.ElementLength) continue;
            if (IntroThumb.FindLiteralLoads(data, a).Count == 0) continue;
            literals.Add(a);
         }
         if (literals.Count != 1) {
            problems.Add(literals.Count == 0 ? "Professor Oak's speech code was not found." : "Professor Oak's speech code was found more than once, so none was used.");
            return false;
         }
         var pictureLiteral = literals[0];
         var speciesNumber = (IntroThumb.Word(data, pictureLiteral) - IntroThumb.RomBase - front.Start) / front.ElementLength;
         var loads = IntroThumb.FindLiteralLoads(data, pictureLiteral);
         var function = IntroThumb.FunctionStart(data, loads[0]);
         if (function < 0) { problems.Add("The start of Professor Oak's speech function was not found."); return false; }

         var constants = new List<IntroConstant> {
            new IntroTablePointer(pictureLiteral, front.Start, front.ElementLength, count) { },
            new IntroTablePointer(pictureLiteral + 4, palettes.Start, palettes.ElementLength, count),
         };
         // the number is also given directly to the code that makes the Pokémon's sprite (movs r2, #n) and to the one that picks its sprite template (movs r0, #n; movs r1, #0; bl)
         if (speciesNumber <= 255) {
            for (int p = function; p < function + 0x100; p += 2) {
               var half = IntroThumb.Half(data, p);
               if (half == (0x2200 | speciesNumber)) constants.Add(new IntroImmediate(p, 2, -1, IntroImmediate.SecondNone));
               else if (half == (0x2000 | speciesNumber) && IntroThumb.Half(data, p + 2) == 0x2100 && IntroThumb.TryBranchLink(data, p + 4, out _)) constants.Add(new IntroImmediate(p, 0, -1, IntroImmediate.SecondNone));
            }
            // and the cry: PlayCry(n, 0)
            for (int p = Math.Max(0x200, function - 0x2000); p < Math.Min(data.Length - 4, function + 0x1000); p += 2) {
               if (IntroThumb.Half(data, p) != (0x2000 | speciesNumber) || IntroThumb.Half(data, p + 2) != 0x2100) continue;
               if (IntroThumb.TryBranchLink(data, p + 4, out var target) && IsPlayCry(data, target)) constants.Add(new IntroImmediate(p, 0, -1, IntroImmediate.SecondNone));
            }
         }
         var species = Setting("species", "Pokémon in Oak's speech",
            "The Pokémon that Professor Oak shows in the speech: its picture appears next to Oak and its cry plays. Only the first 255 Pokémon work here: the game's code can't store a bigger number.",
            IntroValueKind.Species, constants.GroupBy(c => c.Address).Select(g => g.First()).ToArray());

         FindMusic(manager, data, function, "Oak", problems, out var professorMusic, out var titleMusic, out var callers, out var titleCall);
         var titleCry = FindTitleCry(data, titleCall, problems);
         var picture = FindOakPicture(data, callers, problems);
         manager.Set(IntroGame.FireRed, species, professorMusic, titleMusic, titleCry, picture);
         return true;
      }

      private static IntroSetting FindTitleCry(byte[] data, int titleCall, List<string> problems) {
         if (titleCall < 0) return null;
         var found = new List<IntroConstant>();
         for (int p = Math.Max(0x200, titleCall - 0x1000); p < Math.Min(data.Length - 4, titleCall + 0x1000); p += 2) {
            if (!IntroThumb.TryBranchLink(data, p, out var target) || !IsPlayCry(data, target)) continue;
            var constant = Const(data, p, 0);
            if (constant is IntroImmediate immediate && immediate.Read(data) >= 1 && immediate.Read(data) <= 255) found.Add(constant);
         }
         if (found.Count != 1) {
            problems.Add(found.Count == 0 ? "The title screen cry was not found." : "More than one place looked like the title screen cry, so none was used.");
            return null;
         }
         return Setting("titleCry", "Title screen cry", "The cry of the Pokémon that is heard when the title screen appears. Only the first 255 Pokémon work here.", IntroValueKind.Species, found[0]);
      }

      /// <summary>The speech shows trainer picture 3. LoadTrainerPic is called with (3, 0) near the speech's music. Its case for 3 loads the palette and then the compressed pixels from two literals.</summary>
      private static IntroPicture FindOakPicture(byte[] data, List<int> callers, List<string> problems) {
         foreach (var caller in callers) {
            for (int q = caller + 4; q < caller + 0x80; q += 2) {
               if (IntroThumb.Half(data, q - 4) != 0x2003 || IntroThumb.Half(data, q - 2) != 0x2100 || !IntroThumb.TryBranchLink(data, q, out var loader)) continue;
               var picture = ReadOakCase(data, loader);
               if (picture != null) return picture;
            }
         }
         problems.Add("Professor Oak's picture was not found.");
         return null;
      }

      private static IntroPicture ReadOakCase(byte[] data, int loader) {
         for (int p = loader; p < loader + 0x80; p += 2) {
            if (IntroThumb.Half(data, p) != 0x2A03) continue;
            var branch = IntroThumb.Half(data, p + 2);
            if ((branch & 0xFF00) != 0xD000) continue;                       // beq
            var block = p + 2 + 4 + (sbyte)(branch & 0xFF) * 2;
            var literals = new List<int>();
            for (int q = block; q < block + 0x10 && literals.Count < 2; q += 2) {
               var literal = IntroThumb.LiteralAddress(data, q);
               if (literal >= 0 && IsPointer(data, literal)) literals.Add(literal);
            }
            if (literals.Count < 2) continue;
            foreach (var (palette, tiles) in new[] { (literals[0], literals[1]), (literals[1], literals[0]) }) {
               var pixels = LZRun.Decompress(data, Target(data, tiles));
               if (pixels != null && pixels.Length == OakPicture.TilesWide * OakPicture.TilesHigh * 64 && Target(data, palette) + 64 <= data.Length) return new OakPicture(palette, tiles);
            }
         }
         return null;
      }

      #endregion
   }
}
