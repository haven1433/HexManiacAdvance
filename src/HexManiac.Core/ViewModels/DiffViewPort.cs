using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DiffViewPort : ViewModelCore, IViewPort {
      public const int MaxInnerTabWidth = 32;
      private readonly IList<IChildViewPort> left, right;
      private readonly int leftWidth, rightWidth;
      private readonly List<int> startOfNextSegment;

      private HexElement[,] cell;

      public int ChildCount => left.Count;

      public DiffViewPort(IEnumerable<IChildViewPort> leftChildren, IEnumerable<IChildViewPort> rightChildren) {
         left = leftChildren.ToList();
         right = rightChildren.ToList();
         Debug.Assert(left.Count == right.Count, "Diff views must have the same number of diff elements on each side!");

         // combine similar children
         for (int i = 0; i < left.Count - 1; i++) {
            if (CompositeChildViewPort.TryCombine(left[i], left[i + 1], out var leftResult) && CompositeChildViewPort.TryCombine(right[i], right[i + 1], out var rightResult)) {
               left[i] = leftResult;
               right[i] = rightResult;
               left.RemoveAt(i + 1);
               right.RemoveAt(i + 1);
               i -= 1;
            }
         }

         leftWidth = left.Count == 0 ? 16 : Math.Min(MaxInnerTabWidth, left.Max(child => child.Width));
         rightWidth = right.Count == 0 ? 16 : Math.Min(MaxInnerTabWidth, right.Max(child => child.Width));
         startOfNextSegment = new List<int>();
         startOfNextSegment.Add(0);
         for (int i = 0; i < Math.Min(left.Count, right.Count); i++) {
            startOfNextSegment.Add(startOfNextSegment[i] + 1 + Math.Max(left[i].Height, right[i].Height));
         }
      }

      public int LeftHeight(int index) => left[index].Height;
      public int RightHeight(int index) => right[index].Height;

      #region IViewPort

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || y < 0 || x >= cell.GetLength(0) || y >= cell.GetLength(1)) return new HexElement(default, default, DataFormats.Undefined.Instance);
            return cell[x, y];
         }
      }

      public string FileName => string.Empty;

      public string FullFileName => Name;

      public int Width { get => leftWidth + rightWidth + 1; set { } }

      private int height;
      public int Height { get => height; set => Set(ref height, value, Refresh); }

      public bool AutoAdjustDataWidth { get; set; }
      public bool StretchData { get; set; }
      public bool AllowMultipleElementsPerLine { get; set; }
      public bool UseCustomHeaders { get; set; }

      public int MinimumScroll => 0;

      private int scrollValue;
      public int ScrollValue { get => scrollValue; set => Set(ref scrollValue, value.LimitToRange(MinimumScroll, MaximumScroll), Refresh); }

      public int MaximumScroll => startOfNextSegment[startOfNextSegment.Count - 1] - 1;

      public ObservableCollection<string> Headers { get; } = new ObservableCollection<string>();

      public ObservableCollection<HeaderRow> ColumnHeaders => null;

      public int DataOffset => 0;

      private StubCommand scrollCommand;
      public ICommand Scroll => StubCommand<Direction>(ref scrollCommand, ExecuteScroll);
      private void ExecuteScroll(Direction direction) {
         if (direction == Direction.Up) ScrollValue -= 1;
         if (direction == Direction.Up) ScrollValue += 1;
         if (direction == Direction.PageUp) ScrollValue -= height;
         if (direction == Direction.PageDown) ScrollValue += height;
      }

      public double Progress => 0;

      public bool UpdateInProgress => false;

      public string SelectedAddress => string.Empty;

      public string SelectedBytes => string.Empty;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

      public IDataModel Model => null;

      public bool HasTools => false;

      public ChangeHistory<ModelDelta> ChangeHistory { get; }

      public IToolTrayViewModel Tools => null;

      public byte[] FindBytes { get; set; }

      public string Name => left[0].Parent.Name.Trim('*') + " -> " + right[0].Parent.Name.Trim('*');
      public bool IsMetadataOnlyChange => false;
      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs => null;
      public ICommand ExportBackup => null;
      public ICommand Undo => null;
      public ICommand Redo => null;
      public ICommand Copy => null;
      public ICommand DeepCopy => null;
      public ICommand Clear => null;
      public ICommand SelectAll => null;
      public ICommand Goto => null;
      public ICommand ResetAlignment => null;
      public ICommand Back => null;
      public ICommand Forward => null;
      private StubCommand close;
      public ICommand Close => StubCommand(ref close, () => Closed?.Invoke(this, EventArgs.Empty));
      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler PreviewScrollChanged;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<IDataModel> RequestCloseOtherViewports;
      public event EventHandler<Func<Task>> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      public event PropertyChangedEventHandler PropertyChanged;
      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public Task ConsiderReload(IFileSystem fileSystem) => Task.CompletedTask;

      public IChildViewPort CreateChildView(int startAddress, int endAddress) {
         throw new NotImplementedException();
      }

      public void ExpandSelection(int x, int y) {
         if (x < leftWidth) {
            RequestTabChange(this, left[0].Parent);
            var (childIndex, childLine) = ConvertLine(y);
            var child = left[childIndex];
            var targetAddress = child.DataOffset + child.Width * childLine;
            left[0].Parent.Goto.Execute(targetAddress + x);
         } else if (x > leftWidth + 1) {
            RequestTabChange(this, right[0].Parent);
            var (childIndex, childLine) = ConvertLine(y);
            var child = right[childIndex];
            var targetAddress = child.DataOffset + child.Width * childLine;
            right[0].Parent.Goto.Execute(targetAddress + x - leftWidth - 1);
         }
      }

      public IReadOnlyList<(int start, int end)> Find(string search, bool matchExactCase = false) => Array.Empty<(int, int)>();

      public bool CanFindFreeSpace => false;
      public void FindFreeSpace(IFileSystem fs) { }

      public void FindAllSources(int x, int y) { }

      public void FollowLink(int x, int y) { }

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point point) {
         var results = new List<IContextItem>();
         results.Add(new ContextItem("Copy Address", arg => {
            var fileSystem = (IFileSystem)arg;
            var (childIndex, childLine) = ConvertLine(point.Y);
            var address = left[childIndex].DataOffset + childLine * left[childIndex].Width + point.X;
            if (point.X > leftWidth) address = right[childIndex].DataOffset + childLine * right[childIndex].Width + point.X - leftWidth - 1;
            fileSystem.CopyText = address.ToAddress();
         }));
         return results;
      }

      public bool IsSelected(Point point) {
         var (childIndex, childLine) = ConvertLine(point.Y);
         if (childIndex >= left.Count) return false;
         if (point.X < leftWidth) return left[childIndex].IsSelected(new Point(point.X, childLine));
         return right[childIndex].IsSelected(new Point(point.X - leftWidth - 1, childLine));
      }

      public bool IsTable(Point point) => false;

      private void Refresh(int unused) => FillCells();
      public void Refresh() => FillCells();

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      public void ValidateMatchedWords() { }

      #endregion

      /// <returns>The first offset that was edited</returns>
      public static int ApplyIPSPatch(IDataModel model, byte[] patch, ModelDelta token) {
         // 5 byte header (PATCH) and 3 byte footer (EOF)
         // hunk type 1: offset (3 bytes), length (2 bytes), payload (length bytes). Write the payload at offset.
         // RLE hunk:    offset (3 bytes), 00 00, length (2 bytes), target (1 byte). Write the target, length times, at offset

         var start = 5;
         var firstOffset = -1;

         while (patch.Length - start >= 6) {
            var offset = (patch[start] << 16) + (patch[start + 1] << 8) + patch[start + 2];
            if (firstOffset < 0) firstOffset = offset;
            start += 3;
            var length = (patch[start] << 8) + patch[start + 1];
            start += 2;
            if (length > 0) {
               // normal
               model.ExpandData(token, offset + length - 1);
               while (length > 0) {
                  token.ChangeData(model, offset, patch[start]);
                  offset += 1;
                  start += 1;
                  length -= 1;
               }
            } else {
               length = (patch[start] << 8) + patch[start + 1];
               start += 2;
               model.ExpandData(token, offset + length - 1);
               // rle
               while (length > 0) {
                  token.ChangeData(model, offset, patch[start]);
                  offset += 1;
                  length -= 1;
               }
               start += 1;
            }
         }

         return firstOffset;
      }

      public enum PatchDirection { Fail, SourceToDestination, DestinationToSource }

      // return -1 if the header is wrong (UPS1)
      // return -2 if the source file CRC doesn't match
      // return -3 if the patch file CRC doesn't match
      // return -4 if the source file size doesn't match
      // return -5 if the result file CRC doesn't match
      // return -6 if the UPS content didn't finish the last chunk with exactly 12 bytes left
      // return -7 if trying to write past the end of the destination file
      // returns a positive integer, the address of the first change, if everything worked correctly
      public static int ApplyUPSPatch(IDataModel model, byte[] patch, ModelDelta token, bool ignoreChecksums, out PatchDirection direction) {
         // 4 byte header: "UPS1"
         // 12 byte footer: 3 CRC32 checksums. Source file, destination file, patch file (CRC of everything except the last 4 bytes)
         direction = PatchDirection.Fail;

         // check header
         var headerMatches = patch.Take(4).Select(b => (char)b).SequenceEqual("UPS1");
         if (!headerMatches) return -1;

         // check source CRC
         var currentCRC = CalcCRC32(model.RawData);
         var patchSourceFileCRC = patch.ReadMultiByteValue(patch.Length - 12, 4);
         var patchDestinationFileCRC = patch.ReadMultiByteValue(patch.Length - 8, 4);
         if (currentCRC == patchSourceFileCRC) direction = PatchDirection.SourceToDestination;
         if (currentCRC == patchDestinationFileCRC) direction = PatchDirection.DestinationToSource;
         if (direction == PatchDirection.Fail && !ignoreChecksums) return -2;
         if (direction == PatchDirection.Fail) direction = PatchDirection.SourceToDestination;

         // check patch CRC
         var patchWithoutCRC = new byte[patch.Length - 4];
         Array.Copy(patch, patchWithoutCRC, patchWithoutCRC.Length);
         var patchCRC = CalcCRC32(patchWithoutCRC);
         if (patchCRC != patch.ReadMultiByteValue(patch.Length - 4, 4)) return -3;

         // resize (bigger)
         int readIndex = 4, writeIndex = 0, firstEdit = int.MaxValue;
         int sourceSize = ReadVariableWidthInteger(patch, ref readIndex);
         int destinationSize = ReadVariableWidthInteger(patch, ref readIndex);
         int writeLength = destinationSize;
         if (direction == PatchDirection.DestinationToSource) (sourceSize, destinationSize) = (destinationSize, sourceSize);
         if (sourceSize != model.Count && !ignoreChecksums) return -4;
         model.ExpandData(token, destinationSize - 1);
         token.ChangeData(model, sourceSize, new byte[Math.Max(0, destinationSize - sourceSize)]);
         token.ChangeData(model, destinationSize, new byte[Math.Max(0, sourceSize - destinationSize)]);

         // run algorithm
         firstEdit = RunUPSPatchAlgorithm(model, patch, token, writeLength, destinationSize, ref readIndex);
         if (firstEdit < 0) return firstEdit;

         // resize (smaller)
         model.ContractData(token, destinationSize - 1);

         // check result CRC
         if (!ignoreChecksums) {
            var finalCRC = CalcCRC32(model.RawData);
            if (direction == PatchDirection.SourceToDestination && finalCRC != patchDestinationFileCRC) return -5;
            if (direction == PatchDirection.DestinationToSource && finalCRC != patchSourceFileCRC) return -5;
         }

         // check that the chunk ended cleanly
         if (direction == PatchDirection.SourceToDestination && readIndex != patch.Length - 12) return -6;

         return firstEdit;
      }

      private static int RunUPSPatchAlgorithm(IDataModel model, byte[] patch, ModelDelta token, int writeLength, int destinationSize, ref int readIndex) {
         int writeIndex = 0;
         int firstEdit = int.MaxValue;
         while (readIndex < patch.Length - 12 && writeIndex < destinationSize) {
            var skipSize = ReadVariableWidthInteger(patch, ref readIndex);
            writeIndex += skipSize;
            if (writeIndex > writeLength) return -7;
            if (firstEdit == int.MaxValue) firstEdit = skipSize;

            while (patch[readIndex] != 0 && writeIndex < destinationSize) {
               token.ChangeData(model, writeIndex, (byte)(patch[readIndex] ^ model[writeIndex]));
               readIndex += 1;
               writeIndex += 1;
               if (writeIndex > writeLength) return -7;
            }
            readIndex += 1;
            writeIndex += 1;
         }

         return firstEdit;
      }

      public static int CalcCRC32(byte[] array) => (int)Force.Crc32.Crc32Algorithm.Compute(array);

      public static int ReadVariableWidthInteger(byte[] data, ref int index) {
         int result = 0, shift = 0;
         while (true) {
            result |= (data[index] & 0x7F) << shift;
            if (shift != 0) result += 1 << shift;
            shift += 7;
            index += 1;
            if ((data[index - 1] & 0x80) != 0) {
               return result;
            }
         }
      }

      private (int childIndex, int childLine) ConvertLine(int parentLine) {
         var scrollLine = parentLine + scrollValue;
         if (scrollLine < 0) return (0, scrollLine);
         int index = startOfNextSegment.BinarySearch(scrollLine);
         if (index >= 0) return (index, 0);
         index = ~index - 1;
         return (index, scrollLine - startOfNextSegment[index]);
      }

      private void FillCells() {
         Headers.Clear();
         if (Width < 0 || Height < 0) return;
         cell = new HexElement[Width, Height];
         var defaultCell = new HexElement(default, default, Undefined.Instance);

         var (childIndex, childLine) = ConvertLine(0);
         for (int y = 0; y < Height; y++) {
            var childIsValid = childIndex < left.Count;
            for (int x = 0; x < leftWidth; x++) {
               cell[x, y] = childIsValid ? left[childIndex][x, childLine] : defaultCell;
            }
            cell[leftWidth, y] = defaultCell;
            for (int x = 0; x < rightWidth; x++) {
               cell[leftWidth + 1 + x, y] = childIsValid ? right[childIndex][x, childLine] : defaultCell;
            }

            var hasHeader = childIsValid && left[childIndex].Headers.Count > childLine;
            Headers.Add(hasHeader ? left[childIndex].Headers[childLine] : string.Empty);
            childLine += 1;
            if (childIsValid && y + scrollValue == startOfNextSegment[childIndex + 1] - 1) {
               (childIndex, childLine) = (childIndex + 1, 0);
            }
         }
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }
   }
}
