using System;
using System.Collections;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools;

public class LogTool : ViewModelCore, IToolViewModel {
   /// <summary>
   /// Only the most recent messages are kept.
   /// An unbounded log grows for the whole session, and rebuilding the full log text on every refresh gets slower and slower.
   /// </summary>
   public const int MaxMessages = 1000;

   private int renderedVersion = -1;

   public string Name => "Logs";

   public void DataForCurrentRunChanged() {
      var version = LogMessages.Version;
      if (version == renderedVersion) return; // nothing new to show
      renderedVersion = version;
      AllLogText = string.Join(Environment.NewLine, LogMessages.Snapshot());
      NotifyPropertyChanged(nameof(AllLogText));
   }

   public LogMessageCollection LogMessages { get; } = new(MaxMessages);

   public string AllLogText { get; private set; } = string.Empty;
}

/// <summary>
/// A thread-safe, size-limited list of log messages.
/// Messages can be logged from background work (such as model loading) while the UI reads them.
/// </summary>
public class LogMessageCollection : IEnumerable<string> {
   private readonly Queue<string> messages = new();
   private readonly int capacity;
   private int version;

   public LogMessageCollection(int capacity) => this.capacity = Math.Max(1, capacity);

   public int Count { get { lock (messages) return messages.Count; } }

   /// <summary>
   /// Changes every time a message is added.
   /// </summary>
   public int Version { get { lock (messages) return version; } }

   public void Add(string message) {
      lock (messages) {
         messages.Enqueue(message ?? string.Empty);
         while (messages.Count > capacity) messages.Dequeue();
         version++;
      }
   }

   public void AddRange(IEnumerable<string> newMessages) {
      if (newMessages == null) return;
      foreach (var message in newMessages) Add(message);
   }

   public IReadOnlyList<string> Snapshot() {
      lock (messages) return messages.ToArray();
   }

   public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)Snapshot()).GetEnumerator();

   IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
