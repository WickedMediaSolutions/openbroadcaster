using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Services
{
    public sealed class QueueService
    {
        private readonly List<QueueItem> _items = new();
        private readonly List<QueueItem> _history = new();
        private readonly object _sync = new();
        private readonly ILogger<QueueService> _logger;
        private int _historyLimit;

        public QueueService(ILogger<QueueService>? logger = null, int historyLimit = 5)
        {
            _logger = logger ?? AppLogger.CreateLogger<QueueService>();
            _historyLimit = NormalizeHistoryLimit(historyLimit);
        }

        public event EventHandler? QueueChanged;
        public event EventHandler? HistoryChanged;

        public IReadOnlyList<QueueItem> Snapshot()
        {
            lock (_sync)
            {
                return _items.ToArray();
            }
        }

        public IReadOnlyList<QueueItem> HistorySnapshot()
        {
            lock (_sync)
            {
                return _history.ToArray();
            }
        }

        public void Enqueue(QueueItem queueItem)
        {
            if (queueItem == null)
            {
                throw new ArgumentNullException(nameof(queueItem));
            }

            var changed = false;
            lock (_sync)
            {
                _items.Add(queueItem);
                _logger.LogInformation("Enqueued track {Title} by {Artist} (Source={Source})", queueItem.Track.Title, queueItem.Track.Artist, queueItem.Source);
                changed = true;
            }

            if (changed)
            {
                OnQueueChanged();
            }
        }

        public void EnqueueFront(QueueItem queueItem)
        {
            InsertAt(0, queueItem);
        }

        public bool InsertAt(int index, QueueItem queueItem)
        {
            if (queueItem == null)
            {
                throw new ArgumentNullException(nameof(queueItem));
            }

            var changed = false;
            lock (_sync)
            {
                if (index < 0)
                {
                    index = 0;
                }

                if (index > _items.Count)
                {
                    index = _items.Count;
                }

                _items.Insert(index, queueItem);
                _logger.LogInformation("Inserted queue item {Title} at position {Index}", queueItem.Track.Title, index);
                changed = true;
            }

            if (changed)
            {
                OnQueueChanged();
            }

            return changed;
        }

        public bool RemoveAt(int index)
        {
            QueueItem? removed = null;
            lock (_sync)
            {
                if (index < 0 || index >= _items.Count)
                {
                    _logger.LogWarning("Invalid removal index {Index}", index);
                    return false;
                }

                removed = _items[index];
                _items.RemoveAt(index);
                _logger.LogInformation("Removed queue item {Title} at position {Index}", removed.Track.Title, index);
            }

            if (removed != null)
            {
                OnQueueChanged();
            }

            return removed != null;
        }

        public void Clear()
        {
            var changed = false;
            lock (_sync)
            {
                if (_items.Count > 0)
                {
                    _items.Clear();
                    changed = true;
                }
            }

            if (changed)
            {
                OnQueueChanged();
            }
        }

        public void Shuffle()
        {
            var changed = false;
            lock (_sync)
            {
                if (_items.Count <= 1)
                {
                    return;
                }

                // Fisher-Yates shuffle
                var rng = new Random();
                var n = _items.Count;
                while (n > 1)
                {
                    n--;
                    var k = rng.Next(n + 1);
                    (_items[k], _items[n]) = (_items[n], _items[k]);
                }
                changed = true;
                _logger.LogInformation("Shuffled queue ({Count} items)", _items.Count);
            }

            if (changed)
            {
                OnQueueChanged();
            }
        }

        public QueueItem? Dequeue()
        {
            QueueItem? item = null;
            var queueChanged = false;
            var historyChanged = false;
            lock (_sync)
            {
                if (_items.Count == 0)
                {
                    _logger.LogWarning("Dequeue requested but queue is empty");
                    return null;
                }

                item = _items[0];
                _items.RemoveAt(0);
                _logger.LogInformation("Dequeued track {Title} by {Artist}", item.Track.Title, item.Track.Artist);
                queueChanged = true;
                historyChanged = AddToHistoryLocked(item);
            }

            if (queueChanged)
            {
                OnQueueChanged();
            }

            if (historyChanged)
            {
                OnHistoryChanged();
            }

            return item;
        }

        public QueueItem? Peek()
        {
            lock (_sync)
            {
                return _items.Count > 0 ? _items[0] : null;
            }
        }

        public QueueItem? PeekAt(int index)
        {
            lock (_sync)
            {
                if (index < 0 || index >= _items.Count)
                {
                    return null;
                }

                return _items[index];
            }
        }

        public bool Reorder(int fromIndex, int toIndex)
        {
            var changed = false;
            lock (_sync)
            {
                if (fromIndex < 0 || fromIndex >= _items.Count)
                {
                    _logger.LogWarning("Invalid reorder source index {Index}", fromIndex);
                    return false;
                }

                if (toIndex < 0 || toIndex >= _items.Count)
                {
                    _logger.LogWarning("Invalid reorder destination index {Index}", toIndex);
                    return false;
                }

                if (fromIndex == toIndex)
                {
                    return true;
                }

                var item = _items[fromIndex];
                _items.RemoveAt(fromIndex);
                _items.Insert(toIndex, item);
                _logger.LogInformation("Moved queue item {Title} from {From} to {To}", item.Track.Title, fromIndex, toIndex);
                changed = true;
            }

            if (changed)
            {
                OnQueueChanged();
            }

            return changed;
        }

        public void UpdateHistoryLimit(int limit)
        {
            var normalized = NormalizeHistoryLimit(limit);
            var historyChanged = false;
            lock (_sync)
            {
                _historyLimit = normalized;
                historyChanged = TrimHistoryLocked();
            }

            if (historyChanged)
            {
                OnHistoryChanged();
            }
        }

        private bool AddToHistoryLocked(QueueItem item)
        {
            if (item == null)
            {
                return false;
            }

            _history.Insert(0, item);
            TrimHistoryLocked();

            return true;
        }

        private bool TrimHistoryLocked()
        {
            var limit = _historyLimit;
            if (limit <= 0)
            {
                limit = 1;
            }

            var trimmed = false;
            while (_history.Count > limit)
            {
                _history.RemoveAt(_history.Count - 1);
                trimmed = true;
            }

            return trimmed;
        }

        private static int NormalizeHistoryLimit(int requested)
        {
            if (requested <= 0)
            {
                return 1;
            }

            return Math.Min(requested, 1000);
        }

        private void OnQueueChanged()
        {
            QueueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnHistoryChanged()
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
