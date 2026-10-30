using System.Threading;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// A number that goes up whenever the data or the format of any model is changed (a byte written, a run added or removed, a name or a list changed, an undo, a reload).
   /// <para>
   /// A tab that remembers the number it saw when it last looked at the model knows, when it is selected again, whether it has to look again:
   /// the same number means nothing in the model has changed in between, so what the tab built last time is still right.
   /// That is what makes jumping between a lot of open tabs cheap: only the tabs that really are behind pay for a refresh.
   /// </para>
   /// <para>
   /// The number is shared by all models (an edit in one file makes the tabs of another file refresh once, which is harmless).
   /// It only ever goes up, so a missed refresh is impossible: the worst a stray increment can do is cause one refresh that was not needed.
   /// </para>
   /// </summary>
   public static class ModelEditStamp {
      private static long current, runs;

      /// <summary>
      /// A counter for the runs (the formats) of the models only: it moves when a run is added, removed or replaced, and when a change is undone or data is loaded, but not when data bytes change.
      /// Whatever depends on the formats of a ROM, but not on its data (which tables use a table, for example), can be kept until this number moves.
      /// </summary>
      public static long RunsCurrent => Interlocked.Read(ref runs) + 1;

      public static void BumpRuns() {
         Interlocked.Increment(ref runs);
         Bump();
      }

      /// <summary>The number as it is right now. Starts above zero, so a tab can use 0 or -1 for 'never looked'.</summary>
      public static long Current => Interlocked.Read(ref current) + 1;

      /// <summary>Something in a model changed.</summary>
      public static void Bump() => Interlocked.Increment(ref current);
   }
}
