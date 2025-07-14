using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class EditorViewModel : ViewModelCore, IEnumerable<ITabContent>, INotifyCollectionChanged {
      public const string ApplicationName = "HexManiacAdvance";

      public readonly IFileSystem fileSystem;
      public readonly IWorkDispatcher workDispatcher;
      public readonly bool allowLoadingMetadata;
      public readonly List<ITabContent> tabs;

      public (IViewPort tab, int start, int end)[] recentFindResults = new (IViewPort, int start, int end)[0];
      public int currentFindResultIndex;

      public bool showDevMenu;
      public bool ShowDeveloperMenu { get => showDevMenu; set => Set(ref showDevMenu, value); }

      public bool RecentFileMenuEnabled => RecentFileViewModels.Any();

      public MapTutorialsViewModel MapTutorials { get; } = new();

      #region Overlay Text

      public string overlayText;
      public string OverlayText {
         get => overlayText;
         set => Set(ref overlayText, value);
      }

      public bool showOverlayText;
      public bool ShowOverlayText {
         get => showOverlayText;
         set => Set(ref showOverlayText, value);
      }

      #endregion

      public bool findControlVisible;

      public bool hexConverterVisible;

      public string hexText;
      public string HexText {
         get => hexText;
         set {
            if (!TryUpdate(ref hexText, value)) return;
            var result = DoMath(hexText, text => text.TryParseHex(out int number) ? number : null);
            Set(ref decText, result.ToString(), nameof(DecText));
         }
      }

      public string decText;
      public string DecText {
         get => decText;
         set {
            if (!TryUpdate(ref decText, value)) return;
            var result = DoMath(decText, text => int.TryParse(text, out int number) ? number : null);
            Set(ref hexText, result.ToString("X1"), nameof(HexText));
         }
      }

      public int DoMath(string text, Func<string, int?> parse) {
         var operators = text.Length.Range().Where(i => text[i] == '+' || text[i] == '-').ToList();
         operators.Add(text.Length);
         var left = parse(text.Substring(0, operators[0]));
         if (left == null) return 0;
         for (int i = 1; i < operators.Count; i++) {
            var op = text[operators[i - 1]];
            var length = operators[i] - operators[i - 1] - 1;
            var right = parse(text.Substring(operators[i - 1] + 1, length));
            if (right == null) return 0;
            left = op switch {
               '-' => (int)left - (int)right,
               _ => (int)left + (int)right,
            };
         }
         return (int)left;
      }

      public byte[] searchBytes = null;
      public byte[] SearchBytes {
         get => searchBytes;
         set {
            searchBytes = value;
            NotifyPropertyChanged();
         }
      }

      public bool matchExactCase;
      public bool MatchExactCase { get => matchExactCase; set => Set(ref matchExactCase, value); }

      public bool searchAllFiles = true;
      public bool SearchAllFiles { get => searchAllFiles; set => Set(ref searchAllFiles, value); }

      public bool logAppStartupProgress;
      public bool LogAppStartupProgress {
         get => logAppStartupProgress;
         set => Set(ref logAppStartupProgress, value);
      }

      public int zoomLevel = 16;
      public int ZoomLevel {
         get => zoomLevel;
         set => TryUpdate(ref zoomLevel, value);
      }

      public bool insertAutoActive = true;

      public bool showError;

      public string errorMessage;

      public bool showMessage;

      public bool useTableEntryHeaders = true;

      public bool allowSingleTableMode = true;
      public bool AllowSingleTableMode {
         get => allowSingleTableMode;
         set {
            Set(ref allowSingleTableMode, value, arg => {
               foreach (var tab in tabs) {
                  if (tab is IEditableViewPort viewModel) viewModel.AllowSingleTableMode = allowSingleTableMode;
               }
            });
         }
      }

      public bool spartanMode = false;

      public bool focusOnGotoShortcuts = true;
      public bool FocusOnGotoShortcuts {
         get => focusOnGotoShortcuts;
         set => Set(ref focusOnGotoShortcuts, value);
      }

      public bool IsNewVersionAvailable { get; set; }
      public DateTime LastUpdateCheck { get; set; }

      public bool showMatrix = true;
      public bool ShowMatrix { get => showMatrix; set => Set(ref showMatrix, value); }

      public bool animateScroll = true;
      public bool AnimateScroll { get => animateScroll; set => Set(ref animateScroll, value); }

      public bool autoAdjustDataWidth = true;
      public bool AutoAdjustDataWidth { get => autoAdjustDataWidth; set => Set(ref autoAdjustDataWidth, value); }

      public bool stretchData = true;
      public bool StretchData { get => stretchData; set => Set(ref stretchData, value); }

      public bool allowMultipleElementsPerLine = false;
      public bool AllowMultipleElementsPerLine { get => allowMultipleElementsPerLine; set => Set(ref allowMultipleElementsPerLine, value); }

      public bool tutorialsAcknowledged, newVersionAcknowledged;
      public bool TutorialsAcknowledged { get => tutorialsAcknowledged; set => Set(ref tutorialsAcknowledged, value); }
      public bool NewVersionAcknowledged { get => newVersionAcknowledged; set => Set(ref newVersionAcknowledged, value); }

      public bool base10Length;
      public bool Base10Length { get => base10Length; set => Set(ref base10Length, value); }

      public Theme Theme { get; }

      public IFileSystem FileSystem => fileSystem;

      public string infoMessage;

      public int maxDiffSegCount = 1000;
      public int MaximumDiffSegments { get => maxDiffSegCount; set => Set(ref maxDiffSegCount, value.LimitToRange(1, 10000)); }
      public bool HideDiffPointerChanges { get; set; }

      public int maxSearchResults = 400;
      public int MaximumSearchResults { get => maxSearchResults; set => Set(ref maxSearchResults, value.LimitToRange(1, 10000)); }

      public IReadOnlyList<IQuickEditItem> QuickEditsPokedex { get; }

      public IReadOnlyList<IQuickEditItem> QuickEditsExpansion { get; }

      public IReadOnlyList<IQuickEditItem> QuickEditsMisc { get; }

      public bool showAutomationPanel;

      public PythonTool PythonTool { get; }

      public Singletons Singletons { get; }

      public event EventHandler<Action> RequestDelayedWork;

      public event EventHandler MoveFocusToFind;
      public event EventHandler MoveFocusToHexConverter;
      public event EventHandler MoveFocusToPrimaryContent;

      #region Collection Properties

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ITabContent this[int index] => tabs[index];

      public int Count => tabs.Count;

      public int selectedIndex;

      public ObservableCollection<string> RecentFiles { get; }
      public ObservableCollection<RecentFileViewModel> RecentFileViewModels { get; } = new ObservableCollection<RecentFileViewModel>();

      #endregion

      #region CalculateHashes

      public static string GetSHA1(IEditableViewPort viewPort) {
         var sha = SHA1.Create();
         var result = string.Concat(sha.ComputeHash(viewPort.Model.RawData).Select(b => b.ToString("X2")));
         return result;
      }

      public static string GetCRC32(IEditableViewPort viewPort) {
         var crc = Force.Crc32.Crc32Algorithm.Compute(viewPort.Model.RawData);
         return crc.ToString("X8");
      }

      #endregion

      public IEnumerator<ITabContent> GetEnumerator() => tabs.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

      public static bool TryParseBytes(string text, out byte[] results) {
         results = null;
         var parsed = new List<byte>();
         foreach (var token in text.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)) {
            if (token.Length != 2) return false;
            if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var result)) return false;
            parsed.Add(result);
         }
         results = parsed.ToArray();
         return true;
      }

      public void ForwardDelayedWork(object sender, Action e) => RequestDelayedWork?.Invoke(this, e);

      public ObservableCollection<string> ParseRecentFiles(string recentFilesLine) {
         var list = new ObservableCollection<string>();
         var content = recentFilesLine.Split('[')[1];
         content = content.Split(']')[0];

         foreach (var segment in content.Split(',')) {
            var element = segment.Trim(' ', '"', '\'');
            if (!fileSystem.Exists(element)) continue;
            list.Add(element);
         }

         return list;
      }

      public string SerializeRecentFiles() {
         var content = string.Join(", ", RecentFiles.Select(file => $"\"{file}\""));
         return $"RecentFiles = [{content}]";
      }

      public bool withinNotListeningScope = false;
   }

   /// <summary>
   /// Gives all QuickEditItem's an extra check at the end to make sure that they didn't leave the model in a bad state.
   /// </summary>
   public class EditItemWrapper : QuickEditItemDecorator {
      public EditItemWrapper(IQuickEditItem core) => InnerQuickEditItem = core;
   }

   public class RecentFileViewModel : ViewModelCore {
      public string ShortName { get; }
      public string LongName { get; }
   }
}
