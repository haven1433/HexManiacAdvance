using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BorderEditor : ViewModelCore {
      public readonly ChangeHistory<ModelDelta> changeHistory;
      public readonly IDataModel model;
      public readonly BlockMapViewModel blockmap;
      public readonly MapTutorialsViewModel tutorials;

      public event EventHandler BorderChanged;

      public bool showBorder;
      public bool ShowBorderPanel { get => showBorder; set => Set(ref showBorder, value); }

      public void ToggleBorderEditor() {
         ShowBorderPanel = !ShowBorderPanel;
         tutorials.Complete(Tutorial.ToolbarButton_EditBorderBlock);
      }

      public CanvasPixelViewModel borderRender;

      #region Border Width/Height

      public bool HasBorderDimensions { get; set; }

      public int width = -1, height = -1;

      #endregion

      public BorderEditor(BlockMapViewModel blockmap, MapTutorialsViewModel tutorials) {
         this.blockmap = blockmap;
         this.tutorials = tutorials;
         this.model = blockmap.ViewPort.Model;
         this.changeHistory = blockmap.ViewPort.ChangeHistory;
         var layout = blockmap.GetLayout();
         if (layout == null) return;
         HasBorderDimensions = layout.HasField("borderwidth") && layout.HasField("borderheight");
      }
   }
}
