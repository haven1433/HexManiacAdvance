using System;
using System.Collections.Generic;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IModel {
      /// <summary>
      /// If dataIndex is in the middle of a run, returns that run.
      /// If dataIndex is between runs, returns the next available run.
      /// If dataIndex is before the first run, return the first run.
      /// If dataIndex is after the last run, return null;
      /// </summary>
      IFormattedRun GetNextRun(int dataIndex);

      void ObserveRunWritten(IFormattedRun run);
   }

   public class PointerModel : IModel {
      private readonly List<IFormattedRun> runs;

      public PointerModel(byte[] data) {

      }

      public IFormattedRun GetNextRun(int dataIndex) {
         throw new NotImplementedException();
      }

      public void ObserveRunWritten(IFormattedRun run) { }
   }

   public class BasicModel : IModel {
      public static IModel Instance { get; } = new BasicModel();
      public IFormattedRun GetNextRun(int dataIndex) => null;
      public void ObserveRunWritten(IFormattedRun run) { }
   }
}
