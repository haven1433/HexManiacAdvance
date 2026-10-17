using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public enum OverworldAnimCommandKind { Frame, Loop, Jump, End }

   /// <summary>
   /// One 4-byte animation command of an overworld sprite (pokeemerald's union AnimCmd).
   /// A frame is { imageValue:16, duration:6, hFlip:1, vFlip:1 }; loop is { -3, count }, jump is { -2, target }, end is { -1 }.
   /// </summary>
   public class OverworldAnimCommand {
      public OverworldAnimCommandKind Kind { get; }
      /// <summary>The picture a frame shows (its number in the sprite's list of pictures, or in the player's sheet).</summary>
      public int Frame { get; }
      /// <summary>How many game ticks a frame is shown (0 to 63).</summary>
      public int Duration { get; }
      public bool HFlip { get; }
      public bool VFlip { get; }
      /// <summary>The count of a loop, or the command number a jump goes to.</summary>
      public int Argument { get; }

      public OverworldAnimCommand(OverworldAnimCommandKind kind, int frame = 0, int duration = 0, bool hFlip = false, bool vFlip = false, int argument = 0)
         => (Kind, Frame, Duration, HFlip, VFlip, Argument) = (kind, frame, duration, hFlip, vFlip, argument);

      public static OverworldAnimCommand Decode(int word) {
         var type = (short)(word & 0xFFFF);
         switch (type) {
            case -1: return new OverworldAnimCommand(OverworldAnimCommandKind.End);
            case -2: return new OverworldAnimCommand(OverworldAnimCommandKind.Jump, argument: (word >> 16) & 0xFF);
            case -3: return new OverworldAnimCommand(OverworldAnimCommandKind.Loop, argument: (word >> 16) & 0xFF);
            default: return new OverworldAnimCommand(OverworldAnimCommandKind.Frame, type, (word >> 16) & 0x3F, (word & (1 << 22)) != 0, (word & (1 << 23)) != 0);
         }
      }

      public int Encode() {
         switch (Kind) {
            case OverworldAnimCommandKind.End: return 0xFFFF;
            case OverworldAnimCommandKind.Jump: return 0xFFFE | ((Argument & 0xFF) << 16);
            case OverworldAnimCommandKind.Loop: return 0xFFFD | ((Argument & 0xFF) << 16);
            default: return (Frame & 0xFFFF) | ((Duration & 0x3F) << 16) | (HFlip ? 1 << 22 : 0) | (VFlip ? 1 << 23 : 0);
         }
      }

      public override string ToString() => Kind switch {
         OverworldAnimCommandKind.End => "end",
         OverworldAnimCommandKind.Jump => $"jump to {Argument}",
         OverworldAnimCommandKind.Loop => $"loop {Argument}",
         _ => $"picture {Frame} for {Duration} ticks{(HFlip ? ", flipped" : string.Empty)}{(VFlip ? ", upside down" : string.Empty)}",
      };
   }

   public class OverworldAnimSequence {
      public int Index { get; }
      /// <summary>Where the first command is stored.</summary>
      public int Address { get; }
      public string Name { get; }
      public IReadOnlyList<OverworldAnimCommand> Commands { get; }
      public OverworldAnimSequence(int index, int address, string name, IReadOnlyList<OverworldAnimCommand> commands) => (Index, Address, Name, Commands) = (index, address, name, commands);

      /// <summary>The pictures this sequence shows, in order.</summary>
      public IEnumerable<int> Frames => Commands.Where(c => c.Kind == OverworldAnimCommandKind.Frame).Select(c => c.Frame);
   }

   /// <summary>
   /// The animation table of an overworld sprite (the info's anims pointer): a list of pointers, one per animation, to sequences of commands.
   /// Read and written straight from the ROM data so that it works whatever the metadata says about those bytes. Only the numbers inside a command are
   /// ever changed (the picture, the duration, the flips, a loop count or a jump target): the sequences keep their length, so nothing else needs to move.
   /// </summary>
   public static class OverworldAnimations {
      public const int MaxSequences = 64;
      private const int MaxCommands = 128;

      private static readonly string[] Directions = { "south", "north", "west", "east" };
      private static readonly string[] StandardKinds = { "stand facing", "walk", "walk fast", "walk faster", "walk fastest" };

      /// <summary>
      /// The name the game's own code gives animation number <paramref name="index"/> of the player's walking sheet (ANIM_STD_FACE_SOUTH ... ANIM_RUN_SOUTH ... ANIM_SPIN_EAST).
      /// The first 20 animations are the same in every sprite that walks; the others differ per sprite, so they are only named for the player's sheet (<paramref name="playerSheet"/>).
      /// </summary>
      public static string Name(int index, bool playerSheet) {
         if (index < 0) return $"animation {index}";
         if (index < 20) return $"{StandardKinds[index / 4]} {Directions[index % 4]}";
         if (playerSheet && index < 24) return $"run {Directions[index - 20]}";
         if (playerSheet && index < 28) return $"spin {Directions[index - 24]}";
         return $"animation {index}";
      }

      /// <summary>
      /// The sequences of the table at <paramref name="table"/>. Reads until an entry stops being a pointer into the data, the table reaches <paramref name="limit"/> bytes
      /// (pass the room up to the next anchor, or 0 for no limit), or <see cref="MaxSequences"/>. Returns an empty list if the address is not a table of sequences.
      /// </summary>
      public static IReadOnlyList<OverworldAnimSequence> Read(IDataModel model, int table, int limit = 0, bool playerSheet = false) {
         var result = new List<OverworldAnimSequence>();
         if (table < 0 || table + 4 > model.Count) return result;
         var entries = MaxSequences;
         if (limit > 0) entries = Math.Min(entries, limit / 4);
         for (int i = 0; i < entries; i++) {
            if (table + 4 * i + 4 > model.Count) break;
            var address = model.ReadPointer(table + 4 * i);
            if (address < 0 || address + 4 > model.Count) break;
            var commands = new List<OverworldAnimCommand>();
            for (int c = 0; c < MaxCommands && address + 4 * c + 4 <= model.Count; c++) {
               var command = OverworldAnimCommand.Decode(model.ReadMultiByteValue(address + 4 * c, 4));
               commands.Add(command);
               if (command.Kind == OverworldAnimCommandKind.Jump || command.Kind == OverworldAnimCommandKind.End) break;
            }
            result.Add(new OverworldAnimSequence(i, address, Name(i, playerSheet), commands));
         }
         return result;
      }

      /// <summary>The biggest picture number any of the sequences shows, or -1 if none show one.</summary>
      public static int BiggestFrame(IEnumerable<OverworldAnimSequence> sequences) {
         var biggest = -1;
         foreach (var sequence in sequences) foreach (var frame in sequence.Frames) biggest = Math.Max(biggest, frame);
         return biggest;
      }

      /// <summary>Which animations show picture <paramref name="frame"/>, and for how long ("run south (5 ticks)").</summary>
      public static IReadOnlyList<string> DescribeFrameUse(IEnumerable<OverworldAnimSequence> sequences, int frame) {
         var result = new List<string>();
         foreach (var sequence in sequences) {
            var shown = sequence.Commands.Where(c => c.Kind == OverworldAnimCommandKind.Frame && c.Frame == frame).ToList();
            if (shown.Count == 0) continue;
            result.Add($"{sequence.Name} ({string.Join("/", shown.Select(c => c.Duration).Distinct())} ticks)");
         }
         return result;
      }

      /// <summary>
      /// Changes command <paramref name="commandIndex"/> of a sequence in place. Returns false (and writes nothing) if the command is not in the sequence,
      /// the kind would change, or a number does not fit (picture 0 to 32766, duration 0 to 63, argument 0 to 255).
      /// </summary>
      public static bool TrySetCommand(IDataModel model, ModelDelta token, OverworldAnimSequence sequence, int commandIndex, OverworldAnimCommand replacement) {
         if (sequence == null || replacement == null || commandIndex < 0 || commandIndex >= sequence.Commands.Count) return false;
         var old = sequence.Commands[commandIndex];
         if (old.Kind != replacement.Kind) return false;
         if (replacement.Kind == OverworldAnimCommandKind.Frame && (replacement.Frame < 0 || replacement.Frame > 0x7FFE || replacement.Duration < 0 || replacement.Duration > 63)) return false;
         if (replacement.Kind != OverworldAnimCommandKind.Frame && replacement.Kind != OverworldAnimCommandKind.End && (replacement.Argument < 0 || replacement.Argument > 255)) return false;
         var address = sequence.Address + 4 * commandIndex;
         if (address + 4 > model.Count) return false;
         var word = replacement.Encode();
         // keep the high byte (unused by the game) of the old command as it was
         word |= model.ReadMultiByteValue(address, 4) & unchecked((int)0xFF000000);
         for (int i = 0; i < 4; i++) {
            var value = (byte)(word >> (8 * i));
            if (model[address + i] != value) token.ChangeData(model, address + i, value);
         }
         return true;
      }
   }
}
