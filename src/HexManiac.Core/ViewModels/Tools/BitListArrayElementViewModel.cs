using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class BitElement : ViewModelCore, IEquatable<BitElement> {
      public string bitLabel;
      public string BitLabel { get => bitLabel; set => Set(ref bitLabel, value); }

      public bool isChecked, visible = true;
      public bool IsChecked { get => isChecked; set => Set(ref isChecked, value); }
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public bool Equals(BitElement other) => bitLabel == other.bitLabel && isChecked == other.isChecked;
   }

   public class BitListArrayElementViewModel : ViewModelCore, IReadOnlyList<BitElement>, IArrayElementViewModel {
      public readonly ChangeHistory<ModelDelta> history;
      public readonly IDataModel model;
      public readonly List<BitElement> children = new List<BitElement>();
      public readonly ArrayRunElementSegment segment;
      public readonly ArrayRun rotatedBitArray;

      public int start;
      public string Name { get; }

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; set; }
      public int ZIndex => 0;

      #region Mass Copy/Paste for rotated bit arrays

      public bool CanMassCopy => rotatedBitArray != null;
      public void MassCopy(IFileSystem fileSystem) {
         var content = new StringBuilder();
         foreach (var child in children) content.Append(child.IsChecked ? "1" : "0");
         fileSystem.CopyText = content.ToString();
      }

      public bool CanMassPaste => rotatedBitArray != null;

      #endregion

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public EventHandler dataChanged, dataSelected;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public event EventHandler DataSelected { add => dataSelected += value;remove => dataSelected -= value; }

      #region IReadOnlyList<BitElement> Implementation

      public int Count => children.Count;
      public BitElement this[int index] => children[index];
      public IEnumerator<BitElement> GetEnumerator() => children.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();

      #endregion

      public bool deferChanges;
   }
}
