using System;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class CartPlayback : IDisposable
    {
        private readonly Action _stopAction;
        private bool _hasCompleted;
        private bool _stopRequested;

        internal CartPlayback(Action stopAction)
        {
            _stopAction = stopAction ?? throw new ArgumentNullException(nameof(stopAction));
        }

        public bool IsActive => !_hasCompleted;

        public event EventHandler? Completed;

        public void Stop()
        {
            if (_hasCompleted || _stopRequested)
            {
                return;
            }

            _stopRequested = true;
            _stopAction();
        }

        internal void NotifyCompleted()
        {
            if (_hasCompleted)
            {
                return;
            }

            _hasCompleted = true;
            Completed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
