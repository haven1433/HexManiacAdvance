using HavenSoft.HexManiac.Core.Models;
using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>The two sections of the secondary overworld panel.</summary>
   public enum OverworldSecondarySection { FieldEffects, Reflections }

   /// <summary>
   /// The secondary overworld data (field effects: grass, ripples, shadows, dust, with their palettes; and reflection palettes) in the empty right side of the overworld sprites view:
   /// a third column of the table tool, shown next to the overworld sprite table with a switch between its two sections. Each section is the complete editor, created when it is first shown.
   /// A game with only one of the two tables shows just that section.
   /// </summary>
   public class OverworldSecondaryElementViewModel : ViewModelCore, IArrayElementViewModel {
      /// <summary>The room the panel needs: a column for the sprite table, one for its fields and a third one for this panel.</summary>
      public const double WantedToolPanelWidth = 1000;

      private readonly ViewPort viewPort;
      private readonly IDataModel model;

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      /// <summary>True if the game has something to show here: field effects, reflection palettes, or both.</summary>
      public static bool IsSupported(IDataModel model) => FieldEffects.IsSupported(model) || ReflectionPaletteTab.IsSupported(model);

      public bool HasFieldEffects { get; }
      public bool HasReflections { get; }

      /// <summary>Both sections exist, so there is something to switch between.</summary>
      public bool HasBoth => HasFieldEffects && HasReflections;

      private OverworldSecondarySection section;
      public OverworldSecondarySection Section => section;
      public bool ShowFieldEffects => section == OverworldSecondarySection.FieldEffects;
      public bool ShowReflections => section == OverworldSecondarySection.Reflections;

      public ICommand SelectFieldEffects { get; }
      public ICommand SelectReflections { get; }

      private FieldEffectElementViewModel fieldEffects;
      /// <summary>The Field Effects editor. Null until that section is shown (and in a game without a field effects table).</summary>
      public FieldEffectElementViewModel FieldEffectsEditor {
         get {
            if (!HasFieldEffects || !ShowFieldEffects) return null;
            return fieldEffects ??= new FieldEffectElementViewModel(viewPort, 0, embedded: true);
         }
      }

      private ReflectionPaletteTab reflections;
      /// <summary>The Reflection Palettes editor. Null until that section is shown (and in a game without reflection palettes).</summary>
      public ReflectionPaletteTab ReflectionsEditor {
         get {
            if (!HasReflections || !ShowReflections) return null;
            if (reflections == null) {
               reflections = new ReflectionPaletteTab(viewPort);
               reflections.OnError += (sender, e) => viewPort.RaiseError(e);
               reflections.OnMessage += (sender, e) => viewPort.RaiseMessage(e);
            }
            return reflections;
         }
      }

      public OverworldSecondaryElementViewModel(ViewPort viewPort) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         HasFieldEffects = FieldEffects.IsSupported(model);
         HasReflections = ReflectionPaletteTab.IsSupported(model);
         section = Normalize(viewPort.SecondarySection);
         SelectFieldEffects = new StubCommand { CanExecute = arg => HasFieldEffects, Execute = arg => Show(OverworldSecondarySection.FieldEffects) };
         SelectReflections = new StubCommand { CanExecute = arg => HasReflections, Execute = arg => Show(OverworldSecondarySection.Reflections) };
      }

      /// <summary>The section to show: the wanted one if the game has it, otherwise the other.</summary>
      private OverworldSecondarySection Normalize(OverworldSecondarySection wanted) {
         if (wanted == OverworldSecondarySection.FieldEffects && !HasFieldEffects) return OverworldSecondarySection.Reflections;
         if (wanted == OverworldSecondarySection.Reflections && !HasReflections) return OverworldSecondarySection.FieldEffects;
         return wanted;
      }

      public void Show(OverworldSecondarySection wanted) {
         wanted = Normalize(wanted);
         viewPort.SecondarySection = wanted; // the next sprite keeps the section
         if (wanted == section) return;
         section = wanted;
         Refresh();
         NotifyPropertyChanged(nameof(Section));
         NotifyPropertyChanged(nameof(ShowFieldEffects));
         NotifyPropertyChanged(nameof(ShowReflections));
         NotifyPropertyChanged(nameof(FieldEffectsEditor));
         NotifyPropertyChanged(nameof(ReflectionsEditor));
      }

      /// <summary>Reads again whatever was changed in the ROM since the shown section last looked (an undo, an edit in the table tool...).</summary>
      private void Refresh() {
         if (ShowFieldEffects) fieldEffects?.UpdateIfEdited();
         if (ShowReflections) reflections?.ReloadIfEdited();
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not OverworldSecondaryElementViewModel next) return false;
         if (next.model != model) return false;
         Visible = other.Visible;
         Refresh();
         return true;
      }
   }
}
