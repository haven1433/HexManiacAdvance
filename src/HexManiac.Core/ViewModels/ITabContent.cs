using HavenSoft.HexManiac.Core.Models;
using System;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// Each command expects an IFileSystem as its Command Parameter.
   /// </summary>
   public interface ITabContent : INotifyPropertyChanged {
      string Name { get; }
      bool IsMetadataOnlyChange { get; }

      event EventHandler<string> OnError;
      event EventHandler<string> OnMessage;
      event EventHandler ClearMessage;
      event EventHandler Closed;
      event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      event EventHandler<Action> RequestDelayedWork;
      event EventHandler RequestMenuClose;
      event EventHandler<Direction> RequestDiff;
      event EventHandler<CanDiffEventArgs> RequestCanDiff;
      event EventHandler<CanPatchEventArgs> RequestCanCreatePatch;
      event EventHandler<CanPatchEventArgs> RequestCreatePatch;
      event EventHandler RequestRefreshGotoShortcuts;

      bool SpartanMode { get; set; }
      bool CanIpsPatchRight { get; }
      bool CanUpsPatchRight { get; }
      void IpsPatchRight();
      void UpsPatchRight();

      bool CanDuplicate { get; }
   }

   public record TabChangeRequestedEventArgs(ITabContent NewTab) {
      public bool RequestAccepted { get; set; }
   }

   public interface IRaiseMessageTab : ITabContent {
      void RaiseMessage(string message);
   }

   public interface IRaiseErrorTab : ITabContent {
      void RaiseError(string message);
   }

   public class CanDiffEventArgs : EventArgs {
      public Direction Direction { get; }
      public bool Result { get; set; }
      public CanDiffEventArgs(Direction d) => Direction = d;
   }

   public class CanPatchEventArgs : EventArgs {
      public Direction Direction { get; }
      public PatchType PatchType { get; }
      public bool Result { get; set; }
      public CanPatchEventArgs(Direction d, PatchType p) => (Direction, PatchType) = (d, p);
   }
}
