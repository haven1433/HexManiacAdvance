using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>How a ROM can say where a new game starts.</summary>
   public enum NewGameSpawnKind {
      /// <summary>Nothing was found: the card can't change anything.</summary>
      None,
      /// <summary>CUBE: a small table that the game reads (<see cref="CubeNewGameSpawnTable"/>). It also says whether the truck shakes.</summary>
      CubeTable,
      /// <summary>Emerald and the games made from it: the game's own start code is changed (<see cref="EmeraldSpawnPatch"/>).</summary>
      CodePatch,
   }

   /// <summary>The one place the Intro Manager asks about the new game spawn: the CUBE table if the ROM has it, otherwise the code of Emerald.</summary>
   public static class NewGameSpawn {
      public static NewGameSpawnKind Kind(IDataModel model) {
         if (CubeNewGameSpawnTable.IsAvailable(model)) return NewGameSpawnKind.CubeTable;
         return EmeraldSpawnPatch.Find(model) != null ? NewGameSpawnKind.CodePatch : NewGameSpawnKind.None;
      }

      public static CubeNewGameSpawn Read(IDataModel model) {
         return Kind(model) switch {
            NewGameSpawnKind.CubeTable => CubeNewGameSpawnTable.Read(model),
            NewGameSpawnKind.CodePatch => EmeraldSpawnPatch.Read(model),
            _ => null,
         };
      }

      /// <summary>Writes the spawn (the caller closes the undo step). Returns false, changing nothing, if the ROM has no way to hold it.</summary>
      public static bool Write(IDataModel model, ModelDelta token, CubeNewGameSpawn spawn) {
         return Kind(model) switch {
            NewGameSpawnKind.CubeTable => CubeNewGameSpawnTable.Write(model, token, spawn),
            NewGameSpawnKind.CodePatch => EmeraldSpawnPatch.Write(model, token, spawn),
            _ => false,
         };
      }

      /// <summary>Where the data of the spawn is, for the 'Show in the main tab' button, or -1.</summary>
      public static int Address(IDataModel model) {
         return Kind(model) switch {
            NewGameSpawnKind.CubeTable => CubeNewGameSpawnTable.Find(model),
            NewGameSpawnKind.CodePatch => EmeraldSpawnPatch.Find(model)?.Warp ?? -1,
            _ => -1,
         };
      }

      /// <summary>False if the spawn is shown but this ROM's code can't be rewritten (an Emerald hack whose start code was changed more than the position).</summary>
      public static bool CanWrite(IDataModel model) => Kind(model) switch {
         NewGameSpawnKind.CubeTable => true,
         NewGameSpawnKind.CodePatch => EmeraldSpawnPatch.CanWrite(model),
         _ => false,
      };

      /// <summary>FireRed and LeafGreen: no truck, the game starts in the player's room.</summary>
      public static bool HasTruck(IDataModel model) => !(Kind(model) == NewGameSpawnKind.CodePatch && EmeraldSpawnPatch.IsSimple(model));

      /// <summary>What 'Back to the original' writes: the truck of Emerald and CUBE, the room of FireRed and LeafGreen (4.1 at 6,6).</summary>
      public static CubeNewGameSpawn Original(IDataModel model) => HasTruck(model) ? CubeNewGameSpawn.Original : new CubeNewGameSpawn(4, 1, 6, 6, true);

      /// <summary>The tile that the game uses for X and Y of -1, -1: the middle of the map. Null if the map's size can't be read.</summary>
      public static (int x, int y)? CenterOf(IDataModel model, int group, int map) {
         try {
            var banks = Map.AllMapsModel.Create(model);
            var layout = banks[group]?[map]?.Layout;
            if (layout == null || layout.Width <= 0 || layout.Height <= 0) return null;
            return (layout.Width / 2, layout.Height / 2);
         } catch (Exception) {
            return null;
         }
      }
   }

   /// <summary>
   /// Where a new game starts in Emerald and the games made from it, without any table: the game sends the player to the truck with a call to SetWarpDestination (WarpToTruck), starts the
   /// truck scene (the field callback of CB2_NewGame) and runs a script that resets the map flags (NewGameInitData). A start somewhere else changes these three things, found by their code:
   /// <list type="bullet">
   /// <item>the map, X and Y of the call (X and Y are one byte each there, so a tile from 0 to 127);</item>
   /// <item>the field callback, from the truck scene to the normal fade in;</item>
   /// <item>the script, which gets a new first part that runs the old script and then sets what the truck scene and the walk into the house set (Mom's moving in scene is next to play).</item>
   /// </list>
   /// The new script and the old values are kept together in one block of free space, so that choosing the truck again puts the game back as it was.
   /// Nothing here knows an address: the code is found by its shape, and a game whose start scripts are not the original ones (other flags) is refused.
   /// </summary>
   public static class EmeraldSpawnPatch {
      /// <summary>Where the pieces are in the code.</summary>
      public class Layout {
         /// <summary>The first of the 6 instructions of WarpToTruck that make the arguments.</summary>
         public int Warp;
         /// <summary>The literal of NewGameInitData that holds the pointer of the script that resets the map flags.</summary>
         public int ScriptLiteral;
         /// <summary>The literal of CB2_NewGame that holds the field callback.</summary>
         public int CallbackLiteral;
         /// <summary>The normal fade in callback (FieldCB_WarpExitFadeFromBlack), as a thumb address.</summary>
         public int FadeCallback;
         /// <summary>How many bytes the arguments of the call take (12 in the original code of Emerald, a little more or less elsewhere).</summary>
         public int WarpLength = 12;
         /// <summary>FireRed and LeafGreen: the game only walks into the player's room, there is no truck scene or script to change, so only the call is written.</summary>
         public bool Simple;
         /// <summary>False if the place was found and can be read, but the code around it is not the one that this class knows how to rewrite.</summary>
         public bool CanWrite = true;
      }

      public const int MaxTile = 127;
      public const string BlockMagic = "CUBHMASP";
      private const int BlockCallback = 8, BlockScript = 12, BlockWarp = 16, BlockBody = 28;

      // the original start scripts of the male and female player (InsideOfTruck_EventScript_SetIntroFlagsMale / Female): the flags that the new script has to agree with
      private static readonly byte[] OriginalMale = Hex("9f0100169240010029f70229fa0229100329df02293103168c4001003f0009ff03000a006b02");
      private static readonly byte[] OriginalFemale = Hex("9f0200169240020029f60229f90229110329e00229320316824001003f0009ff0c000a006b02");

      // the new first part of the script: [call old] checkplayergender; compare VAR_RESULT, MALE; goto_if_eq male; (female) ...; goto common; male: ...; common: ...; return
      private static readonly byte[] ScriptTemplate = Hex("04de152708a0210d8000000601f31727089f020029f60229110329e00229320316824001003f0009ff0c000a00050f1827089f010029f70229100329df02293103168c4001003f0009ff03000a0029fa0229f90229f00216924003002a640303");
      private const int ScriptCallAt = 1, ScriptMaleAt = 13, ScriptCommonAt = 46, ScriptMaleOffset = 50, ScriptCommonOffset = 78;

      private sealed class Cache { public Layout Layout; public bool Searched; }
      private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IDataModel, Cache> cache = new();

      private static byte[] Hex(string text) {
         var result = new byte[text.Length / 2];
         for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
         return result;
      }

      /// <summary>The places of the pieces, or null if this ROM's start code is not the one of Emerald. The answer stays the same when the ROM is edited here (the places don't move).</summary>
      public static Layout Find(IDataModel model) {
         if (model == null || model.Count < 0x1000) return null;
         var entry = cache.GetValue(model, key => new Cache());
         if (!entry.Searched) {
            entry.Searched = true;
            try { entry.Layout = Search(model.RawData); } catch (Exception) { entry.Layout = null; }
         }
         return entry.Layout;
      }

      private static int Half(byte[] data, int address) => IntroThumb.Half(data, address);

      private static Layout Search(byte[] data) => SearchOriginal(data) ?? SearchGeneric(data);

      private static Layout SearchOriginal(byte[] data) {
         int warp, scriptLiteral = -1, initData = -1;
         var legacy = FindWarpToTruck(data, out var warpFunction);
         if (legacy >= 0) {
            warp = legacy;
            if (!HasOriginalScripts(data)) return null;

            // NewGameInitData calls WarpToTruck, and then runs the script of the map flags: ldr r0, [pc] ; bl RunScriptImmediately
            for (int p = 0x200; p < data.Length - 12; p += 2) {
               if (!IntroThumb.TryBranchLink(data, p, out var target) || target != warpFunction) continue;
               if ((Half(data, p + 4) & 0xF800) != 0x4800 || !IntroThumb.TryBranchLink(data, p + 6, out _)) continue;
               var literal = IntroThumb.LiteralAddress(data, p + 4);
               if (literal < 0 || !IsPointer(data, literal)) continue;
               if (scriptLiteral != -1) return null;       // more than one: not the original code
               scriptLiteral = literal;
               initData = IntroThumb.FunctionStart(data, p);
            }
         } else {
            // a game built by a newer compiler has WarpToTruck inside of NewGameInitData: the arguments, bl SetWarpDestination ; bl WarpIntoMap ; ldr r0, [pc] ; bl RunScriptImmediately
            warp = -1;
            if (!HasOriginalScripts(data)) return null;
            for (int p = 0x200; p < data.Length - 0x20; p += 2) {
               if (!IsAnyArguments(data, p)) continue;
               if (!IntroThumb.TryBranchLink(data, p + 12, out _) || !IntroThumb.TryBranchLink(data, p + 16, out _)) continue;
               if ((Half(data, p + 20) & 0xF800) != 0x4800 || !IntroThumb.TryBranchLink(data, p + 22, out _)) continue;
               var literal = IntroThumb.LiteralAddress(data, p + 20);
               if (literal < 0 || !IsPointer(data, literal)) continue;
               if (warp != -1) return null;
               warp = p;
               scriptLiteral = literal;
               initData = IntroThumb.FunctionStart(data, p);
            }
         }
         if (warp < 0 || scriptLiteral < 0 || initData < 0) return null;

         // CB2_NewGame calls NewGameInitData, and a little later sets the field callback: ldr r1, [gFieldCallback] ; ldr r0, [callback] ; str r0, [r1]
         int callbackLiteral = -1;
         for (int p = 0x200; p < data.Length - 0x40; p += 2) {
            if (!IntroThumb.TryBranchLink(data, p, out var target) || (target != initData && target != initData - 2 && target != initData - 4)) continue; // a newer compiler may put an instruction before the push
            for (int q = p + 4; q < p + 0x30; q += 2) {
               if ((Half(data, q) & 0xF800) != 0x4800 || (Half(data, q + 2) & 0xF800) != 0x4800) continue;
               // str rB, [rA] where rA holds the address of gFieldCallback and rB the callback (the registers depend on the compiler)
               if (Half(data, q + 4) != (0x6000 | (((Half(data, q) >> 8) & 7) << 3) | ((Half(data, q + 2) >> 8) & 7))) continue;
               var literal = IntroThumb.LiteralAddress(data, q + 2);
               if (literal < 0 || (IntroThumb.Word(data, literal) & 1) == 0) continue;
               if (callbackLiteral != -1) return null;
               callbackLiteral = literal;
               break;
            }
         }
         if (callbackLiteral < 0) return null;

         var fade = FindFadeCallback(data);
         if (fade < 0) return null;
         return new Layout { Warp = warp, ScriptLiteral = scriptLiteral, CallbackLiteral = callbackLiteral, FadeCallback = fade };
      }

      #region A start that was changed by another tool, and FireRed and LeafGreen

      /// <summary>Reads what the instructions that make the arguments of SetWarpDestination(group, map, warpId, x, y) end up with: movs, negs, adds #0, mov and str [sp]. False for any other instruction or if something stays unknown.</summary>
      private static bool TryDecodeArguments(byte[] data, int start, int length, out int group, out int map, out int x, out int y) {
         group = map = x = y = -1;
         if (length < 4 || (length & 1) != 0 || start < 0 || start + length > data.Length) return false;
         var regs = new int?[8];
         int? stack = null;
         for (int a = start; a < start + length; a += 2) {
            var h = Half(data, a);
            if (h == 0x46C0) continue;                                                                      // nop
            else if ((h & 0xF800) == 0x2000) regs[(h >> 8) & 7] = h & 0xFF;                                   // movs rd, #imm
            else if ((h & 0xFFC0) == 0x4240) regs[h & 7] = regs[(h >> 3) & 7] is int v ? -v : null;       // negs rd, rm
            else if ((h & 0xFE00) == 0x1C00 && ((h >> 6) & 7) == 0) regs[h & 7] = regs[(h >> 3) & 7];     // adds rd, rn, #0
            else if ((h & 0xFFC0) == 0x0000) regs[h & 7] = regs[(h >> 3) & 7];                            // movs rd, rm
            else if ((h & 0xF8FF) == 0x9000) stack = regs[(h >> 8) & 7];                                  // str rt, [sp]
            else return false;
         }
         if (regs[0] == null || regs[1] == null || regs[3] == null || stack == null) return false;
         (group, map, x, y) = (regs[0].Value, regs[1].Value, regs[3].Value, stack.Value);
         return group is >= 0 and <= 255 && map is >= 0 and <= 255;
      }

      /// <summary>The first instruction of the arguments that end right before the call at 'end': as few as it takes to know group, map, X and Y. -1 if the instructions before the call are not such arguments.</summary>
      private static int FindArgumentsStart(byte[] data, int end) {
         for (int length = 4; length <= 20; length += 2) {
            var h = Half(data, end - length);
            if ((h & 0xF800) != 0x2000 && (h & 0xFFC0) != 0x4240 && (h & 0xFE00) != 0x1C00 && (h & 0xFFC0) != 0x0000 && (h & 0xF8FF) != 0x9000) return -1;
            if (TryDecodeArguments(data, end - length, length, out _, out _, out _, out _)) return end - length;
         }
         return -1;
      }

      private static bool IsFireRedOrLeafGreen(byte[] data) => data.Length > 0xB0 && data[0xAC] == (byte)'B' && data[0xAD] == (byte)'P' && (data[0xAE] == (byte)'R' || data[0xAE] == (byte)'G');

      /// <summary>
      /// The call of SetWarpDestination made with other instructions than the original (a tool or a hack changed the start), and the one of FireRed and LeafGreen.
      /// Either inside of NewGameInitData ("args; bl; bl; ldr r0, [script]; bl RunScriptImmediately") or in a function that it calls (push {lr} ; sub sp, #4 ; args ; bl ; bl ; add sp, #4 ; pop {r0} ; bx r0).
      /// </summary>
      private static Layout SearchGeneric(byte[] data) {
         int region = -1, length = 0, scriptLiteral = -1, initData = -1, found = 0;
         void Candidate(int start, int end, int literal, int caller) {
            found++;
            region = start; length = end - start; scriptLiteral = literal; initData = IntroThumb.FunctionStart(data, caller);
         }
         for (int p = 0x200; p < data.Length - 0x30; p += 2) {
            // inline
            if (IntroThumb.TryBranchLink(data, p, out _) && IntroThumb.TryBranchLink(data, p + 4, out _) && (Half(data, p + 8) & 0xF800) == 0x4800 && IntroThumb.TryBranchLink(data, p + 10, out _)) {
               var literal = IntroThumb.LiteralAddress(data, p + 8);
               var start = FindArgumentsStart(data, p);
               if (start >= 0 && literal >= 0 && IsPointer(data, literal)) Candidate(start, p, literal, p);
            }
            // a function of its own
            if (Half(data, p) == 0xB500 && Half(data, p + 2) == 0xB081) {
               for (int callAt = p + 6; callAt <= p + 26; callAt += 2) {
                  if (!IntroThumb.TryBranchLink(data, callAt, out _) || !IntroThumb.TryBranchLink(data, callAt + 4, out _)) continue;
                  if (Half(data, callAt + 8) != 0xB001 || Half(data, callAt + 10) != 0xBC01 || Half(data, callAt + 12) != 0x4700) break;
                  var start = p + 4;
                  if (!TryDecodeArguments(data, start, callAt - start, out _, out _, out _, out _)) break;
                  for (int q = 0x200; q < data.Length - 12; q += 2) {
                     if (!IntroThumb.TryBranchLink(data, q, out var target) || target != p) continue;
                     if ((Half(data, q + 4) & 0xF800) != 0x4800 || !IntroThumb.TryBranchLink(data, q + 6, out _)) continue;
                     var literal = IntroThumb.LiteralAddress(data, q + 4);
                     if (literal < 0 || !IsPointer(data, literal)) continue;
                     Candidate(start, callAt, literal, q);
                  }
                  break;
               }
            }
         }
         if (found != 1 || region < 0) return null;
         var layout = new Layout { Warp = region, WarpLength = length, ScriptLiteral = scriptLiteral };
         if (IsFireRedOrLeafGreen(data)) { layout.Simple = true; layout.CanWrite = length >= 12; return layout; }
         // Emerald-like: the rest is looked for as well; without it the start can be read but not changed
         layout.CanWrite = false;
         if (length != 12 || !HasOriginalScripts(data) || initData < 0) return layout;
         if (!FindCallback(data, initData, out var callbackLiteral)) return layout;
         var fade = FindFadeCallback(data);
         if (fade < 0) return layout;
         layout.CallbackLiteral = callbackLiteral;
         layout.FadeCallback = fade;
         layout.CanWrite = true;
         return layout;
      }

      private static bool FindCallback(byte[] data, int initData, out int callbackLiteral) {
         callbackLiteral = -1;
         for (int p = 0x200; p < data.Length - 0x40; p += 2) {
            if (!IntroThumb.TryBranchLink(data, p, out var target) || (target != initData && target != initData - 2 && target != initData - 4)) continue;
            for (int q = p + 4; q < p + 0x30; q += 2) {
               if ((Half(data, q) & 0xF800) != 0x4800 || (Half(data, q + 2) & 0xF800) != 0x4800) continue;
               if (Half(data, q + 4) != (0x6000 | (((Half(data, q) >> 8) & 7) << 3) | ((Half(data, q + 2) >> 8) & 7))) continue;
               var literal = IntroThumb.LiteralAddress(data, q + 2);
               if (literal < 0 || (IntroThumb.Word(data, literal) & 1) == 0) continue;
               if (callbackLiteral != -1) return false;
               callbackLiteral = literal;
               break;
            }
         }
         return callbackLiteral >= 0;
      }

      #endregion

      private static bool IsPointer(byte[] data, int address) {
         var value = IntroThumb.Word(data, address);
         return value >= IntroThumb.RomBase + 0x200 && value < IntroThumb.RomBase + data.Length;
      }

      /// <summary>
      /// WarpToTruck: push {lr} ; sub sp, #4 ; (6 instructions: the arguments) ; bl SetWarpDestination ; bl WarpIntoMap ; add sp, #4 ; pop {r0} ; bx r0.
      /// The arguments are either the original ones (-1 for X and Y) or the ones that this class writes for a start at a tile.
      /// </summary>
      private static int FindWarpToTruck(byte[] data, out int function) {
         int found = -1;
         function = -1;
         for (int p = 0x200; p < data.Length - 0x20; p += 2) {
            if (Half(data, p) != 0xB500 || Half(data, p + 2) != 0xB081) continue;
            if (Half(data, p + 0x18) != 0xB001 || Half(data, p + 0x1A) != 0xBC01 || Half(data, p + 0x1C) != 0x4700) continue;
            if (!IntroThumb.TryBranchLink(data, p + 0x10, out _) || !IntroThumb.TryBranchLink(data, p + 0x14, out _)) continue;
            if (!IsOriginalArguments(data, p + 4) && !IsTileArguments(data, p + 4)) continue;
            if (found != -1) return -1;
            found = p + 4;
            function = p;
         }
         return found;
      }

      // movs r3, #1 ; negs r3, r3 ; str r3, [sp] ; movs r0, #group ; movs r1, #map ; adds r2, r3, #0
      private static bool IsOriginalArguments(byte[] data, int a) =>
         Half(data, a) == 0x2301 && Half(data, a + 2) == 0x425B && Half(data, a + 4) == 0x9300 && (Half(data, a + 6) & 0xFF00) == 0x2000 && (Half(data, a + 8) & 0xFF00) == 0x2100 && Half(data, a + 10) == 0x1C1A;

      // the same arguments as a newer compiler writes them: movs r2, #1 ; negs r2, r2 ; movs r3, r2 ; then (in any order) movs r1, #map ; movs r0, #group ; str r2, [sp]
      private static bool IsModernOriginalArguments(byte[] data, int a) {
         if (Half(data, a) != 0x2201 || Half(data, a + 2) != 0x4252 || Half(data, a + 4) != 0x0013) return false;
         int groups = 0, maps = 0, stores = 0;
         for (int i = 6; i < 12; i += 2) {
            var half = Half(data, a + i);
            if ((half & 0xFF00) == 0x2000) groups++;
            else if ((half & 0xFF00) == 0x2100) maps++;
            else if (half == 0x9200) stores++;
         }
         return groups == 1 && maps == 1 && stores == 1;
      }

      private static bool IsAnyArguments(byte[] data, int a) => IsOriginalArguments(data, a) || IsModernOriginalArguments(data, a) || IsTileArguments(data, a);

      // movs r2, #255 ; movs r3, #x ; movs r0, #y ; str r0, [sp] ; movs r0, #group ; movs r1, #map
      private static bool IsTileArguments(byte[] data, int a) =>
         Half(data, a) == 0x22FF && (Half(data, a + 2) & 0xFF00) == 0x2300 && (Half(data, a + 4) & 0xFF00) == 0x2000 && Half(data, a + 6) == 0x9000 && (Half(data, a + 8) & 0xFF00) == 0x2000 && (Half(data, a + 10) & 0xFF00) == 0x2100;

      /// <summary>FieldCB_WarpExitFadeFromBlack: push {lr} ; bl OnTrainerHillEReaderChallengeFloor ; cmp r0, #0 ; bne ; bl ; bl FadeInFromBlack ; bl SetUpWarpExitTask ; bl LockPlayerFieldControls ; pop {r0} ; bx r0.</summary>
      private static int FindFadeCallback(byte[] data) {
         int found = -1;
         for (int p = 0x200; p < data.Length - 0x20; p += 2) {
            if (Half(data, p) != 0xB500 || Half(data, p + 6) != 0x2800 || Half(data, p + 8) != 0xD101) continue;
            if (Half(data, p + 0x1A) != 0xBC01 || Half(data, p + 0x1C) != 0x4700) continue;
            if (!IntroThumb.TryBranchLink(data, p + 2, out _) || !IntroThumb.TryBranchLink(data, p + 10, out _) || !IntroThumb.TryBranchLink(data, p + 14, out _)
               || !IntroThumb.TryBranchLink(data, p + 18, out _) || !IntroThumb.TryBranchLink(data, p + 22, out _)) continue;
            if (found != -1) return -1;
            found = p;
         }
         return found < 0 ? FindFadeCallbackOfNewerCompilers(data) : found + IntroThumb.RomBase + 1;
      }

      /// <summary>The same function made by a newer compiler: push {lr} ; (sub sp, #4) ; bl ; cmp r0, #0 ; b.. ; ... at least 5 calls ... pop {r0} ; bx r0. Only if there is exactly one.</summary>
      private static int FindFadeCallbackOfNewerCompilers(byte[] data) {
         int found = -1;
         for (int p = 0x200; p < data.Length - 0x60; p += 2) {
            if (Half(data, p) != 0xB500) continue;
            var o = Half(data, p + 2) == 0xB081 ? 4 : 2;
            if (!IntroThumb.TryBranchLink(data, p + o, out _) || Half(data, p + o + 4) != 0x2800 || (Half(data, p + o + 6) & 0xF000) != 0xD000) continue;
            int end = -1;
            for (int q = p + o + 8; q < p + 0x50; q += 2) if (Half(data, q) == 0xBC01 && Half(data, q + 2) == 0x4700) { end = q; break; }
            if (end < 0) continue;
            int calls = 0;
            for (int q = p; q < end; q += 2) if (IntroThumb.TryBranchLink(data, q, out _)) calls++;
            if (calls < 5) continue;
            if (found != -1) return -1;
            found = p;
         }
         return found < 0 ? -1 : found + IntroThumb.RomBase + 1;
      }

      private static bool HasOriginalScripts(byte[] data) => IndexOf(data, OriginalMale) >= 0 && IndexOf(data, OriginalFemale) >= 0;

      private static int IndexOf(byte[] data, byte[] pattern) {
         var first = pattern[0];
         for (int i = 0x200; i < data.Length - pattern.Length; i++) {
            if (data[i] != first) continue;
            int j = 1;
            while (j < pattern.Length && data[i + j] == pattern[j]) j++;
            if (j == pattern.Length) return i;
         }
         return -1;
      }

      #region The block with the old values and the new script

      /// <summary>The block that this class made (found by the script literal), or -1.</summary>
      private static int FindBlock(byte[] data, Layout layout) {
         if (!IsPointer(data, layout.ScriptLiteral)) return -1;
         var script = IntroThumb.Word(data, layout.ScriptLiteral) - IntroThumb.RomBase;
         var block = script - BlockBody;
         if (block < 0 || block + BlockBody + ScriptTemplate.Length > data.Length) return -1;
         for (int i = 0; i < BlockMagic.Length; i++) if (data[block + i] != BlockMagic[i]) return -1;
         return block;
      }

      public static bool IsPatched(IDataModel model) {
         var layout = Find(model);
         return layout != null && FindBlock(model.RawData, layout) != -1;
      }

      #endregion

      /// <summary>The spawn the code has now, or null if the start was not found. The truck shakes in every game that has the truck scene (it is not a setting here).</summary>
      public static CubeNewGameSpawn Read(IDataModel model) {
         var layout = Find(model);
         if (layout == null) return null;
         if (!TryDecodeArguments(model.RawData, layout.Warp, layout.WarpLength, out var group, out var map, out var x, out var y)) return null;
         return new CubeNewGameSpawn(group, map, x, y, true);
      }

      /// <summary>False if the start was found and is shown, but this class can't rewrite the code around it.</summary>
      public static bool CanWrite(IDataModel model) => Find(model)?.CanWrite ?? false;

      /// <summary>True for FireRed and LeafGreen, which have no truck: the game starts in the player's room.</summary>
      public static bool IsSimple(IDataModel model) => Find(model)?.Simple ?? false;

      /// <summary>Why a spawn can't be written, or null.</summary>
      public static string Refuse(CubeNewGameSpawn spawn, bool simple = false) {
         if (spawn.MapGroup is < 0 or > 255 || spawn.MapNum is < 0 or > 255) return "The map group and number must fit in a byte.";
         if (spawn.IsTruck && !simple) return null;
         var centre = spawn.X == -1 && spawn.Y == -1;
         var max = MaxTile; // the arguments are signed bytes in the game
         if (!centre && (spawn.X < 0 || spawn.X > max || spawn.Y < 0 || spawn.Y > max)) return $"In this game, X and Y can be from 0 to {max} (or -1 and -1 for the middle of the map), because the game's code stores each in one byte.";
         return null;
      }

      /// <summary>Writes the spawn (the caller closes the undo step). Returns false, changing nothing, if the code is not the one of Emerald.</summary>
      public static bool Write(IDataModel model, ModelDelta token, CubeNewGameSpawn spawn) {
         var layout = Find(model);
         if (layout == null) return false;
         if (!layout.CanWrite) return false;
         var refusal = Refuse(spawn, layout.Simple);
         if (refusal != null) throw new ArgumentOutOfRangeException(nameof(spawn), refusal);
         if (layout.Simple) {
            // FireRed and LeafGreen: only the call changes
            WriteArguments(model, token, layout, spawn.MapGroup, spawn.MapNum, spawn.X, spawn.Y);
            return true;
         }
         var data = model.RawData;
         var block = FindBlock(data, layout);

         if (spawn.IsTruck) {
            // the truck: everything goes back as it was
            if (block != -1) Restore(model, token, layout, block);
            else WriteArguments(model, token, layout, spawn.MapGroup, spawn.MapNum, -1, -1);
            return true;
         }

         if (block == -1) block = MakeBlock(model, token, layout);
         WriteArguments(model, token, layout, spawn.MapGroup, spawn.MapNum, spawn.X, spawn.Y);
         // the normal fade in instead of the truck scene (a pointer in code is not a pointer of the editor's bookkeeping)
         model.WriteValue(token, layout.CallbackLiteral, layout.FadeCallback);
         // the new script, which runs the old one first
         var scriptAddress = block + BlockBody;
         if (IntroThumb.Word(model.RawData, layout.ScriptLiteral) - IntroThumb.RomBase != scriptAddress) IntroEdit.Repoint(model, token, layout.ScriptLiteral, scriptAddress);
         return true;
      }

      private static void WriteArguments(IDataModel model, ModelDelta token, Layout layout, int group, int map, int x, int y) {
         var a = layout.Warp;
         int[] halves;
         if (x == -1 && y == -1 && layout.WarpLength == 12 && IsModernOriginalArguments(model.RawData, a)) {
            // already the original arguments of the newer compiler: only the map changes, where it is
            for (int i = 6; i < 12; i += 2) {
               var half = Half(model.RawData, a + i);
               if ((half & 0xFF00) == 0x2000 && model.RawData[a + i] != group) token.ChangeData(model, a + i, (byte)group);
               if ((half & 0xFF00) == 0x2100 && model.RawData[a + i] != map) token.ChangeData(model, a + i, (byte)map);
            }
            return;
         }
         if (x == -1 && y == -1) halves = new[] { 0x2301, 0x425B, 0x9300, 0x2000 | group, 0x2100 | map, 0x1C1A };
         else halves = new[] { 0x22FF, 0x2300 | x, 0x2000 | y, 0x9000, 0x2000 | group, 0x2100 | map };
         // an original call that takes more room than the new one is filled up with nops (mov r8, r8)
         var all = new int[Math.Max(halves.Length, layout.WarpLength / 2)];
         for (int i = 0; i < all.Length; i++) all[i] = i < halves.Length ? halves[i] : 0x46C0;
         for (int i = 0; i < all.Length; i++) {
            var at = a + i * 2;
            var bytes = new[] { (byte)all[i], (byte)(all[i] >> 8) };
            if (model.RawData[at] != bytes[0] || model.RawData[at + 1] != bytes[1]) token.ChangeData(model, at, bytes);
         }
      }

      /// <summary>Makes the block in free space: the magic, the old callback, the old script pointer, the old 12 bytes of arguments, and the new script.</summary>
      private static int MakeBlock(IDataModel model, ModelDelta token, Layout layout) {
         var data = model.RawData;
         var oldScript = IntroThumb.Word(data, layout.ScriptLiteral);
         var length = BlockBody + ScriptTemplate.Length;
         length = (length + 3) & ~3;
         var block = IntroEdit.AllocateData(model, token, length);
         var bytes = new byte[length];
         for (int i = 0; i < bytes.Length; i++) bytes[i] = 0xFF;
         for (int i = 0; i < BlockMagic.Length; i++) bytes[i] = (byte)BlockMagic[i];
         PutWord(bytes, BlockCallback, IntroThumb.Word(data, layout.CallbackLiteral));
         PutWord(bytes, BlockScript, oldScript);
         Array.Copy(data, layout.Warp, bytes, BlockWarp, 12);
         var script = (byte[])ScriptTemplate.Clone();
         var start = IntroThumb.RomBase + block + BlockBody;
         PutWord(script, ScriptCallAt, oldScript);
         PutWord(script, ScriptMaleAt, start + ScriptMaleOffset);
         PutWord(script, ScriptCommonAt, start + ScriptCommonOffset);
         Array.Copy(script, 0, bytes, BlockBody, script.Length);
         token.ChangeData(model, block, bytes);
         return block;
      }

      private static void Restore(IDataModel model, ModelDelta token, Layout layout, int block) {
         var data = model.RawData;
         var oldCallback = IntroThumb.Word(data, block + BlockCallback);
         var oldScript = IntroThumb.Word(data, block + BlockScript) - IntroThumb.RomBase;
         var warp = new byte[12];
         Array.Copy(data, block + BlockWarp, warp, 0, 12);
         token.ChangeData(model, layout.Warp, warp);
         model.WriteValue(token, layout.CallbackLiteral, oldCallback);
         IntroEdit.Repoint(model, token, layout.ScriptLiteral, oldScript);
         // the block is free space again
         var free = new byte[BlockBody + ScriptTemplate.Length];
         for (int i = 0; i < free.Length; i++) free[i] = 0xFF;
         token.ChangeData(model, block, free);
      }

      private static void PutWord(byte[] bytes, int at, int value) {
         for (int i = 0; i < 4; i++) bytes[at + i] = (byte)(value >> (8 * i));
      }
   }
}
