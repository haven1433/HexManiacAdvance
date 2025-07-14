using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools;

public class BatchObservableCollection<T> : IList<T>, INotifyCollectionChanged {
   private readonly List<T> content = new();
   private int batchCount = 0;

   public IDisposable CreateBatch() {
      batchCount += 1;
      return new StubDisposable { Dispose = () => {
         batchCount -= 1;
         if (batchCount == 0) RaiseReset();
      } };
   }

   /// <summary>
   /// Creates a batch change operation that doesn't notify upon completion.
   /// Any batches wrapped within this batch won't notify.
   /// </summary>
   public IDisposable CreateSilentBatch() {
      batchCount += 1;
      return new StubDisposable { Dispose = () => batchCount -= 1 };
   }

   private void RaiseMemberChanged(int index, T newValue, T oldValue) =>
      CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Replace, newValue, oldValue, index));

   private void RaiseMemberInsert(int index, T item) => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Add, item, index));

   private void RaiseMemberAdd(T item) => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Add, item));

   private void RaiseMemberRemove(T item) => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, item));

   private void RaiseMemberRemove(int index, T item) => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, item, index));

   private void RaiseReset() => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));

   #region IList

   public T this[int index] {
      get => content[index];
      set {
         var old = content[index];
         content[index] = value;
         if (batchCount == 0) {
            RaiseMemberChanged(index, value, old);
         }
      }
   }

   public int Count => content.Count;

   public bool IsReadOnly => false;

   public void Add(T item) {
      content.Add(item);
      if (batchCount == 0) RaiseMemberAdd(item);
   }

   public void Clear() {
      content.Clear();
      if (batchCount == 0) RaiseReset();
   }

   public bool Contains(T item) => content.Contains(item);

   public void CopyTo(T[] array, int arrayIndex) => content.CopyTo(array, arrayIndex);

   public IEnumerator<T> GetEnumerator() => content.GetEnumerator();

   public int IndexOf(T item) => content.IndexOf(item);

   public void Insert(int index, T item) {
      content.Insert(index, item);
      if (batchCount == 0) RaiseMemberInsert(index, item);
   }

   public bool Remove(T item) {
      var result = content.Remove(item);
      if (batchCount == 0 && result) RaiseMemberRemove(item);
      return result;
   }

   public void RemoveAt(int index) {
      var item = content[index];
      content.RemoveAt(index);
      if (batchCount == 0) RaiseMemberRemove(index, item);
   }

   IEnumerator IEnumerable.GetEnumerator() => content.GetEnumerator();

   #endregion

   #region INotifyCollectionChanged

   public event NotifyCollectionChangedEventHandler? CollectionChanged;

   #endregion
}
