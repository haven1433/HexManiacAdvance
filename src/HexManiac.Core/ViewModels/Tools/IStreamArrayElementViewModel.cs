using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IStreamArrayElementViewModel : IArrayElementViewModel {
      event EventHandler<(int originalStart, int newStart)> DataMoved;
   }
}
