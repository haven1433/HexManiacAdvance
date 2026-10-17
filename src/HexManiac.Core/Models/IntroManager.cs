using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public enum IntroGame { Unknown, Emerald, FireRed }

   public enum IntroValueKind { Species, Song }

   /// <summary>
   /// One thing of the intro that the Intro Manager changes with a single choice: the Pokémon of the speech, a song... It is a number that the game's code uses at one or more places
   /// (the picture of the Pokémon and the cry, for example): all places always hold the same number.
   /// </summary>
   public class IntroSetting {
      public string Id { get; }
      public string Title { get; }
      public string Description { get; }
      public IntroValueKind Kind { get; }
      public IReadOnlyList<IntroConstant> Constants { get; }

      public IntroSetting(string id, string title, string description, IntroValueKind kind, IEnumerable<IntroConstant> constants) {
         (Id, Title, Description, Kind) = (id, title, description, kind);
         Constants = constants.ToList();
      }

      /// <summary>True if a number stored in a literal is also loaded by other code: editing it would change something that is not part of the intro.</summary>
      public bool IsShared => Constants.OfType<IntroLiteral>().Any(literal => literal.Loads > 1);

      /// <summary>The numbers in the ROM now. Normally they are all the same.</summary>
      public IReadOnlyList<int> ReadAll(byte[] data) => Constants.Select(constant => constant.Read(data)).ToList();

      /// <summary>The number the ROM uses now (the first place's number), or -1 if the places don't agree on one that makes sense.</summary>
      public int Read(byte[] data, out bool consistent) {
         var values = ReadAll(data);
         consistent = values.All(value => value == values[0]);
         return values.Count == 0 ? -1 : values[0];
      }

      public int MaxValue => Constants.Count == 0 ? 0 : Constants.Min(constant => constant.MaxValue);

      public bool CanHold(int value, out string reason) {
         foreach (var constant in Constants) {
            if (constant.CanHold(value)) continue;
            reason = $"The game's code can only store {constant.Limit} at this place.";
            return false;
         }
         reason = null;
         return true;
      }

      /// <summary>Writes the number at every place. The caller closes the undo step.</summary>
      public void Write(IDataModel model, ModelDelta token, int value) {
         if (!CanHold(value, out var reason)) throw new InvalidOperationException(reason);
         foreach (var constant in Constants) constant.Write(model, token, value);
      }

      /// <summary>Where the number is stored, for the 'Show in the main tab' button and the details line.</summary>
      public IEnumerable<int> Addresses => Constants.Select(constant => constant.Address);
   }

   /// <summary>
   /// The intro of the game: what happens before the player has a name. It can be Professor Birch with a Lotad (Emerald and the games built on it, like CUBE) or Professor Oak with a Nidoran (FireRed).
   /// All the settings are found by reading the game's code (which function calls which, how a number is built before a call), not by fixed addresses:
   /// they are found again wherever the code is, in the original games and in games that were changed or expanded.
   /// </summary>
   public class IntroManager {
      public IDataModel Model { get; }
      public IntroGame Game { get; private set; }

      public string ProfessorName => Game == IntroGame.FireRed ? "Professor Oak" : "Professor Birch";
      public string ProfessorShortName => Game == IntroGame.FireRed ? "Oak" : "Birch";

      /// <summary>The Pokémon of the speech: its picture appears in the speech and its cry plays. Null if its code was not found.</summary>
      public IntroSetting Species { get; private set; }
      public IntroSetting ProfessorMusic { get; private set; }
      public IntroSetting TitleMusic { get; private set; }
      /// <summary>The Pokémon whose cry plays on the title screen (FireRed only).</summary>
      public IntroSetting TitleCry { get; private set; }
      public IntroPicture Picture { get; private set; }

      /// <summary>What could not be found and why (one sentence each).</summary>
      public IReadOnlyList<string> Problems { get; private set; } = Array.Empty<string>();

      public bool Found => Species != null || ProfessorMusic != null || TitleMusic != null || TitleCry != null || Picture != null;

      public IEnumerable<IntroSetting> Settings => new[] { Species, ProfessorMusic, TitleMusic, TitleCry }.Where(setting => setting != null);

      public const string UnsupportedMessage = "The Intro Manager works on Pokémon Emerald and FireRed (and games made from them, like CUBE). It could not recognize this ROM's new game intro, so there is nothing to edit here.";

      private IntroManager(IDataModel model) => Model = model;

      /// <summary>A quick check from the game code in the header: Emerald, FireRed or LeafGreen. Whether the intro code can really be found is only known after <see cref="Find"/>.</summary>
      public static bool IsSupported(IDataModel model) {
         if (model == null || model.Count < 0x400) return false;
         var code = model.GetGameCode();
         return code.StartsWith("BPE") || code.StartsWith("BPR") || code.StartsWith("BPG");
      }

      /// <summary>Reads the ROM's code and finds everything. Never throws: whatever is not found is listed in <see cref="Problems"/>.</summary>
      public static IntroManager Find(IDataModel model) {
         var manager = new IntroManager(model);
         var problems = new List<string>();
         try {
            var data = model.RawData;
            if (!IntroFinder.FindEmerald(manager, data, problems)) {
               var emeraldProblems = problems.ToList();
               problems.Clear();
               if (!IntroFinder.FindFireRed(manager, data, problems)) {
                  problems.Clear();
                  problems.AddRange(emeraldProblems);
                  problems.Add("The FireRed intro code was not found either.");
               }
            }
         } catch (Exception e) {
            problems.Add("Reading the code failed: " + e.Message);
         }
         manager.Problems = problems;
         return manager;
      }

      internal void Set(IntroGame game, IntroSetting species, IntroSetting professorMusic, IntroSetting titleMusic, IntroSetting titleCry, IntroPicture picture) {
         (Game, Species, ProfessorMusic, TitleMusic, TitleCry, Picture) = (game, species, professorMusic, titleMusic, titleCry, picture);
      }
   }
}
