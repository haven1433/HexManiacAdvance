using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class MapTab : UserControl {
      private MapEditorViewModel ViewModel => (MapEditorViewModel)DataContext;
      private new ToolTip ToolTip => (ToolTip)base.ToolTip;

      public event EventHandler FocusElement;

      public MapTab() {
         InitializeComponent();
         DataContextChanged += UpdateDataContext;
         base.ToolTip = new ToolTip();
         ToolTipService.SetIsEnabled(this, false);
      }

      protected override void OnVisualParentChanged(DependencyObject oldParent) {
         base.OnVisualParentChanged(oldParent);
         Focus();
      }

      private void UpdateDataContext(object sender, DependencyPropertyChangedEventArgs e) {
         var oldContext = e.OldValue as MapEditorViewModel;
         if (oldContext != null) {
            oldContext.PropertyChanged -= HandleContextPropertyChanged;
            oldContext.AutoscrollBlocks -= AutoscrollBlocks;
            oldContext.AutoscrollTiles -= AutoscrollTiles;
         }
         var newContext = e.NewValue as MapEditorViewModel;
         if (newContext != null) {
            newContext.PropertyChanged += HandleContextPropertyChanged;
            newContext.AutoscrollBlocks += AutoscrollBlocks;
            newContext.AutoscrollTiles += AutoscrollTiles;
         }
      }

      private void HandleContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         // TODO any custom property logic here
      }

      private void AutoscrollBlocks(object sender, EventArgs e) {
         var scrollRange = BlockViewer.ExtentHeight - BlockViewer.ViewportHeight;
         var blockHeight = (ViewModel.Blocks.PixelHeight / 16.0) * 8 - 16;
         var scrollPercent = ((ViewModel.DrawBlockIndex - 8) / blockHeight).LimitToRange(0, 1);
         BlockViewer.ScrollToVerticalOffset(scrollRange * scrollPercent);
      }

      private void AutoscrollTiles(object sender, EventArgs e) {
         var scrollRange = TileViewer.ExtentHeight - TileViewer.ViewportHeight;
         var tileHeight = ViewModel.PrimaryMap.BlockEditor.TileRender.PixelHeight * 3 - 24;
         var scrollPercent = (ViewModel.PrimaryMap.BlockEditor.TileSelectionY / (double)tileHeight).LimitToRange(0, 1);
         TileViewer.ScrollToVerticalOffset(scrollRange * scrollPercent);
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         var partial = ActualWidth / 2 - (int)(ActualWidth / 2);
         var transform = (TranslateTransform)MapView.RenderTransform;
         transform.X = -partial;
      }

      private void UpdateTooltipContent(object content) {
         if (content == null) return;
         if (content is not object[] tooltip) return;
         ToolTipService.SetIsEnabled(this, false);
         ToolTip.IsOpen = false;
         InvalidateVisual();
         base.ToolTip = new HexContentToolTip { DataContext = content }; // have to make a new one to prevent a glitch of text changing as the old one fades to closed.
         if (tooltip.Length > 0) {
            ToolTipService.SetIsEnabled(this, true);
            ToolTip.IsOpen = true;
            InvalidateVisual();
         }
      }

      private void OnEnterTutorial(object sender, MapTutorialViewModel tutorial) {
         var index = (Tutorial)(tutorial.Index - 1);
         if (index == Tutorial.LeftClickBlock_SelectBlock) FocusElement.Raise(BlockViewer);
         if (index == Tutorial.BlockButton_EditBlocks) FocusElement.Raise(EditBlockButton);
         if (index == Tutorial.ToolbarButton_GotoWildData) FocusElement.Raise(WildButton);
         if (index == Tutorial.ToolbarButton_EditMapHeader) FocusElement.Raise(MapHeaderButton);
         if (index == Tutorial.ClickBlock_DrawTile) FocusElement.Raise(BlockEditor);
         if (index == Tutorial.BackButton_GoBack) FocusElement.Raise(BackButtonWidget);
         if (index == Tutorial.ToolbarUndo_Undo) FocusElement.Raise(UndoButtonWidget);
         if (index == Tutorial.ToolbarButton_EditBorderBlock) FocusElement.Raise(EditBorderButton);
         if (index == Tutorial.ToolbarTemplate_ConfigureObject) FocusElement.Raise(ShowTemplateSettingsButton);
         if (index == Tutorial.ToolbarTemplate_CreateObject) FocusElement.Raise(ObjectEventTemplate);
         if (index == Tutorial.ToolbarTemplate_CreateEvent) FocusElement.Raise(OtherEventTemplates);
         if (index == Tutorial.EventButtons_CycleEvent) FocusElement.Raise(EventCategorySelector);
      }

      #region Map Interaction

      private MouseButton withinMapInteraction = MouseButton.XButton1; // track which button is being used. Set to XButton1 when not in use.

      private static readonly object NoTooltip = new object[0];
      private void ButtonDown(object sender, MouseButtonEventArgs e) {
         if (e.ChangedButton == MouseButton.XButton1 && ViewModel.Back.CanExecute(null)) {
            ViewModel.Back.Execute();
            return;
         }
         if (e.ChangedButton == MouseButton.XButton2 && ViewModel.Forward.CanExecute(null)) {
            ViewModel.Forward.Execute();
            return;
         }
         if (withinMapInteraction != MouseButton.XButton1) return;
         UpdateTooltipContent(NoTooltip);
         Focus();
         e.Handled = true;
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         element.CaptureMouse();
         if (e.LeftButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Left;
            var interactionStart = PrimaryInteractionStart.Click;
            if (e.ClickCount == 2) interactionStart = PrimaryInteractionStart.DoubleClick;
            if (Keyboard.Modifiers == ModifierKeys.Shift) interactionStart = PrimaryInteractionStart.ShiftClick;
            if (Keyboard.Modifiers == ModifierKeys.Control) interactionStart = PrimaryInteractionStart.ControlClick;
            if (e.ClickCount == 2 && Keyboard.Modifiers == ModifierKeys.Control) interactionStart = PrimaryInteractionStart.ControlClick | PrimaryInteractionStart.DoubleClick;
            vm.PrimaryDown(p.X, p.Y, interactionStart);
         } else if (e.MiddleButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Middle;
            vm.DragDown(p.X, p.Y);
         } else if (e.RightButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Right;
            var result = vm.SelectDown(p.X, p.Y);
            if (result == SelectionInteractionResult.ShowMenu) {
               withinMapInteraction = MouseButton.XButton1;
               ShowMenu(element, e);
            }
         }
      }

      // this has to be an event rather than a command, otherwise the +/- interactions will happen even if we're in a textbox in the event panel (such as typing pokemon moves for a trainer)
      private void HandleKeyDown(object sender, KeyEventArgs e) {
         if (Keyboard.FocusedElement is TextBoxBase || Keyboard.FocusedElement is TextBoxLookAlike) return;
         e.Handled = true;
         if (e.Key == Key.Add) ViewModel.ZoomCommand.Execute(ZoomDirection.Enlarge);
         else if (e.Key == Key.Subtract) ViewModel.ZoomCommand.Execute(ZoomDirection.Shrink);
         else if (e.Key == Key.Space) {
            ViewModel.ShowBeneath = true;
         } else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control) {
            ViewModel.HideEvents |= !waitingForControlUp;
            waitingForControlUp = true;
            e.Handled = false;
         } else e.Handled = false;
      }

      bool waitingForControlUp; // sentinel to watch for ctrl keyup, since e.IsRepeat doesn't work for Ctrl after a Ctrl+Z
      private void HandleKeyUp(object sender, KeyEventArgs e) {
         if (Keyboard.FocusedElement is TextBoxBase || Keyboard.FocusedElement is TextBoxLookAlike) return;
         if (e.Key == Key.Space) {
            ViewModel.ShowBeneath = false;
            e.Handled = true;
         }
         if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) {
            waitingForControlUp = false;
            ViewModel.HideEvents = false;
         }
      }
      protected override void OnLostFocus(RoutedEventArgs e) {
         base.OnLostFocus(e);
         var viewModel = ViewModel;
         if (viewModel != null) viewModel.HideEvents = false;
         waitingForControlUp = false;
      }

      protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
         base.OnLostKeyboardFocus(e);
         var viewModel = ViewModel;
         if (viewModel != null) viewModel.HideEvents = false;
         waitingForControlUp = false;
      }

      private void ButtonMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         if (vm == null) return;
         var p = GetCoordinates(element, e);
         e.Handled = true;
         if (withinMapInteraction == MouseButton.XButton1) {
            UpdateTooltipContent(vm.Hover(p.X, p.Y));
            return;
         }
         if (withinMapInteraction == MouseButton.Left) {
            vm.PrimaryMove(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Middle) {
            vm.DragMove(p.X, p.Y, true);
         } else if (withinMapInteraction == MouseButton.Right) {
            vm.SelectMove(p.X, p.Y);
         }
      }

      private void ButtonUp(object sender, MouseButtonEventArgs e) {
         e.Handled = true;
         var element = (FrameworkElement)sender;
         var previousInteraction = withinMapInteraction;
         withinMapInteraction = MouseButton.XButton1;
         element.ReleaseMouseCapture();
         if (previousInteraction == MouseButton.XButton1) return;
         if (e.ChangedButton != previousInteraction) return;
         var vm = ViewModel;
         if (vm == null) return;
         var p = GetCoordinates(element, e);
         if (previousInteraction == MouseButton.Left) {
            vm.PrimaryUp(p.X, p.Y);
         } else if (previousInteraction == MouseButton.Middle) {
            vm.DragUp(p.X, p.Y);
         } else if (previousInteraction == MouseButton.Right) {
            vm.SelectUp(p.X, p.Y);
         }
      }

      private void ButtonLeave(object sender, MouseEventArgs e) {
         UpdateTooltipContent(NoTooltip);
         if (ViewModel == null) return;
         ViewModel.ShowHighlightCursor = false;
      }

      private void BackgroundDown(object sender, MouseButtonEventArgs e) {
         if (ViewModel == null) return;
         if (e.ChangedButton == MouseButton.XButton1 && ViewModel.Back.CanExecute(null)) {
            ViewModel.Back.Execute();
            return;
         }
         if (e.ChangedButton == MouseButton.XButton2 && ViewModel.Forward.CanExecute(null)) {
            ViewModel.Forward.Execute();
            return;
         }
         if (withinMapInteraction != MouseButton.XButton1) return;
         if (e.ChangedButton == MouseButton.Right) return; // right-click is not a valid drag operation, because that's confusing.
         UpdateTooltipContent(NoTooltip);
         var element = (FrameworkElement)sender;
         if (!element.IsMouseDirectlyOver) return;
         e.Handled = true;
         var vm = ViewModel;
         vm.ClearSelection();
         var p = GetCoordinates(element, e);
         element.CaptureMouse();
         withinMapInteraction = MouseButton.Middle;
         vm.DragDown(p.X, p.Y);
      }

      private void BackgroundMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         var vm = ViewModel;
         if (vm == null) return;
         var p = GetCoordinates(element, e);
         e.Handled = true;
         if (withinMapInteraction == MouseButton.XButton1) return;
         vm.DragMove(p.X, p.Y, false);
      }

      private void BackgroundUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         var previousInteraction = withinMapInteraction;
         withinMapInteraction = MouseButton.XButton1;
         element.ReleaseMouseCapture();
         if (previousInteraction == MouseButton.XButton1) return;
         if (e.ChangedButton != previousInteraction) return;
         var vm = ViewModel;
         if (vm == null) return;
         e.Handled = true;
         var p = GetCoordinates(element, e);
         vm.DragUp(p.X, p.Y);
      }

      private void Wheel(object sender, MouseWheelEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         vm.Zoom(p.X, p.Y, e.Delta > 0);
         e.Handled = true;
      }

      private void EatMouseWheel(object sender, MouseWheelEventArgs e) {
         // we have this so that mouse-wheel over certain elements won't get taken by the Wheel method above.
         e.Handled = true;
      }

      private void BlockBagMouseDown(object sender, MouseButtonEventArgs e) {
         if (Keyboard.Modifiers != ModifierKeys.Control) return;
         ViewModel.ClearBlockBag();
      }

      private void BlocksDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (element.IsMouseCaptured) return;
         element.CaptureMouse();
         var mainModel = ViewModel;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         if (Keyboard.Modifiers == ModifierKeys.Control) {
            mainModel.ToggleBlockInBag((int)p.X, (int)p.Y);
         }
         mainModel.SelectBlock((int)p.X, (int)p.Y);
         e.Handled = true;
      }

      private void BlocksMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         if (Keyboard.Modifiers == ModifierKeys.Control) return;
         var mainModel = ViewModel;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.DragBlock((int)p.X, (int)p.Y);
         e.Handled = true;
      }

      private void BlocksUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         element.ReleaseMouseCapture();
         var mainModel = ViewModel;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.ReleaseBlock((int)p.X, (int)p.Y);
         e.Handled = true;
      }

      private void TilesDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel.PrimaryMap.BlockEditor;
         var p = e.GetPosition(element);
         vm.TileSelectionY = (int)(p.Y * vm.TileRender.SpriteScale);
         vm.TileSelectionX = (int)(p.X * vm.TileRender.SpriteScale);
      }

      private Point GetCoordinates(FrameworkElement element, MouseEventArgs e) {
         var p = e.GetPosition(element);
         return new(p.X - element.ActualWidth / 2, p.Y - element.ActualHeight / 2);
      }

      private void ShowMenu(FrameworkElement element, MouseEventArgs e) {
         element.ContextMenu.DataContext = ViewModel;
         element.ContextMenu.IsEnabled = true;
         element.ContextMenu.IsOpen = true;
      }

      private void DisableMenuOnClose(object sender, ContextMenuEventArgs e) {
         var menu = (ContextMenu)sender;
         menu.IsEnabled = false;
      }

      #endregion

      #region Border Interaction

      private void BorderDown(object sender, MouseButtonEventArgs e) => BorderMove(sender, e);

      private void BorderSelect(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.CaptureMouse();
         var p = e.GetPosition(element);
         ViewModel.ReadBorderBlock(p.X, p.Y);
      }

      private void BorderMove(object sender, MouseEventArgs e) {
         if (e.LeftButton == MouseButtonState.Pressed) {
            var element = (FrameworkElement)sender;
            var p = e.GetPosition(element);
            ViewModel.DrawBorder(p.X, p.Y);
         }
      }

      private void BorderUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         ViewModel.CompleteBorderDraw();
      }

      #endregion

      #region Shifter Interaction

      private bool withinShiftInteraction;

      private void ShifterDown(object sender, MouseButtonEventArgs e) {
         if (withinShiftInteraction) return;
         var element = (FrameworkElement)sender;
         var p = GetCoordinates(MapButtons, e);
         element.CaptureMouse();
         withinShiftInteraction = true;
         ViewModel.ShiftDown(p.X, p.Y);
         e.Handled = true;
      }

      private void ShifterMove(object sender, MouseEventArgs e) {
         if (!withinShiftInteraction) return;
         var p = GetCoordinates(MapButtons, e);
         ViewModel.ShiftMove(p.X, p.Y);
         e.Handled = true;
      }

      private void ShifterUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         if (!withinShiftInteraction) return;
         var p = GetCoordinates(MapButtons, e);
         ViewModel.ShiftUp(p.X, p.Y);
         withinShiftInteraction = false;
         e.Handled = true;
      }

      #endregion

      #region Event Template Interaction

      private void EventTemplateDown(object sender, MouseEventArgs e) {
         var target = (EventCreationType)((FrameworkElement)sender).Tag;
         if (target == EventCreationType.Fly && !ViewModel.PrimaryMap.CanCreateFlyEvent) return;
         withinMapInteraction = MouseButton.Left;
         MapView.CaptureMouse();
         ViewModel.StartEventCreationInteraction(target);
         e.Handled = true;
      }

      #endregion

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         NativeProcess.Start(e.Uri.AbsoluteUri);
         e.Handled = true;
      }
   }
}
