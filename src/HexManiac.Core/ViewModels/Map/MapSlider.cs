using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public abstract class MapSlider : ViewModelCore {
      public const int SliderSize = 20;
      protected readonly int id;

      public abstract string Tooltip { get; }

      private MapSliderIcons icon;
      public MapSliderIcons Icon { get => icon; set => SetEnum(ref icon, value); }

      private bool anchorLeftEdge, anchorTopEdge;
      private int anchorX, anchorY;
      public bool AnchorLeftEdge { get => anchorLeftEdge; set => Set(ref anchorLeftEdge, value); } // if false, we anchor the right edge instead
      public bool AnchorTopEdge { get => anchorTopEdge; set => Set(ref anchorTopEdge, value); }    // if false, we anchor the bottom edge instead
      public int AnchorPositionX { get => anchorX; set => Set(ref anchorX, value); }
      public int AnchorPositionY { get => anchorY; set => Set(ref anchorY, value); }

      public MapSlider(int id, MapSliderIcons icon, int left = int.MinValue, int top = int.MinValue, int right = int.MinValue, int bottom = int.MinValue) {
         AnchorPositionX = left;
         AnchorLeftEdge = AnchorPositionX != int.MinValue;
         if (!AnchorLeftEdge) AnchorPositionX = -right;
         AnchorPositionY = top;
         AnchorTopEdge = AnchorPositionY != int.MinValue;
         if (!AnchorTopEdge) AnchorPositionY = -bottom;
         Icon = icon;
         this.id = id;
      }

      public abstract void Move(int x, int y);

   }

   public enum MapSliderIcons {
      LeftRight, UpDown, ExtendLeft, ExtendRight, ExtendUp, ExtendDown,
   }

   public class ConnectionSlider : MapSlider {
      private readonly MapTutorialsViewModel tutorials;
      private Action notify;
      private ConnectionModel connection;

      public override string Tooltip => "Drag to adjust the connection between the maps.";

      public ConnectionSlider(ConnectionModel connection, (int group, int num) sourceMapInfo, Action notify, int id, MapSliderIcons icon, MapTutorialsViewModel tutorials, int left = int.MinValue, int top = int.MinValue, int right = int.MinValue, int bottom = int.MinValue) : base(id, icon, left, top, right, bottom) {
         (this.notify, this.connection) = (notify, connection);
         this.tutorials = tutorials;
         var group = connection.MapGroup;
         var index = connection.MapNum;
      }

      public override void Move(int x, int y) {
         var inverse = connection.GetInverse();
         if (Icon == MapSliderIcons.LeftRight) {
            connection.Offset += x;
            if (inverse != null) inverse.Offset = -connection.Offset;
            if (x != 0) {
               notify();
               tutorials.Complete(Tutorial.DragButtons_AdjustConnection);
            }
         } else if (Icon == MapSliderIcons.UpDown) {
            connection.Offset += y;
            if (inverse != null) inverse.Offset = -connection.Offset;
            if (y != 0) {
               notify();
               tutorials.Complete(Tutorial.DragButtons_AdjustConnection);
            }
         } else {
            throw new NotImplementedException();
         }
      }
   }
}
