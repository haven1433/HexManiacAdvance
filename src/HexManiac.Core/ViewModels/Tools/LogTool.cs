using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools;

public class LogTool : ViewModelCore, IToolViewModel {
   public string Name => "Logs";
   public void DataForCurrentRunChanged() {
      AllLogText = string.Join(Environment.NewLine, LogMessages);
      NotifyPropertyChanged(nameof(AllLogText));
   }
   public List<string> LogMessages { get; } = new();
   public string AllLogText { get; private set; }
}
