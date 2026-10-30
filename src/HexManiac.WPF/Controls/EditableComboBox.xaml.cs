using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Windows;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class EditableComboBox {
      public EditableComboBox() => InitializeComponent();

      private void KeyDownToViewModel(object sender, KeyEventArgs e) {
         var element = (FrameworkElement)sender;
         var viewModel = (IndexComboBoxViewModel)element.DataContext;
         if (e.Key == Key.Enter) viewModel.CompleteFilterInteraction();
      }
   }
}
