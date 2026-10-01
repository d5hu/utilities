namespace D5HU.Utilities;

/// <summary>
/// Asynchronous reader-writer lock.
/// </summary>
public sealed class AsyncReaderWriterLock : IDisposable
{
    #region Nested definitions

    /// <summary>
    /// Disposable implementation for releasing a read or write acquisition.
    /// </summary>
    private sealed class Releaser(AsyncReaderWriterLock owner, bool isWriter) : IDisposable
    {
        /// <summary>
        /// Owner of the disposable.
        /// </summary>
        private AsyncReaderWriterLock? _owner = owner;

        /// <inheritdoc/>
        public void Dispose()
        {
            AsyncReaderWriterLock? owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null)
            {
                return;
            }

            if (isWriter)
            {
                owner.WriterRelease();
            }
            else
            {
                owner.ReaderRelease();
            }
        }
    }

    #endregion

    #region Fields

    /// <summary>
    /// Queue of waiting writers.
    /// </summary>
    private readonly TaskQueue<IDisposable> _waitingWriters;

    /// <summary>
    /// Queue of waiting readers.
    /// </summary>
    private readonly TaskQueue<IDisposable> _waitingReaders;

    /// <summary>
    /// Status of the lock:
    /// <list type="table">
    ///    <listheader>
    ///     <term>Value</term>
    ///   </listheader>
    /// 
    ///   <item>
    ///     <term><c>0</c></term>
    ///     <description>No one has acquired the lock.</description>
    ///   </item>
    ///   <item>
    ///     <term><c>-1</c></term>
    ///     <description>A writer has acquired the lock.</description>
    ///   </item>
    ///   <item>
    ///     <term><c>&gt;0</c></term>
    ///     <description>One or more readers have acquired the lock.</description>
    ///   </item>
    /// </list>
    /// </summary>
    private int _status;

    /// <summary>
    /// Lock for synchronizing access to fields.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// Indicates whether the lock has been disposed.
    /// </summary>
    private bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the reader/writer lock.
    /// </summary>
    public AsyncReaderWriterLock()
    {
        _waitingWriters = new(() => new Releaser(this, isWriter: true));
        _waitingReaders = new(() => new Releaser(this, isWriter: false));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new reader-writer lock with an active reader lock.
    /// </summary>
    public static (AsyncReaderWriterLock Lock, IDisposable Handle) CreateWithReaderLock()
    {
        AsyncReaderWriterLock instance = new()
        {
            _status = 1
        };
        return (instance, new Releaser(instance, isWriter: false));
    }

    /// <summary>
    /// Creates a new reader-writer lock with an active writer lock.
    /// </summary>
    public static (AsyncReaderWriterLock Lock, IDisposable Handle) CreateWithWriterLock()
    {
        AsyncReaderWriterLock instance = new()
        {
            _status = -1
        };
        return (instance, new Releaser(instance, isWriter: true));
    }

    /// <summary>
    /// Request read access to the lock.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request while waiting.</param>
    /// <returns>Promise for a disposable which must be disposed in order to release the lock.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    public ValueTask<IDisposable> ReaderLockAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Fast path when no write access is active or waiting.
            if (_status >= 0 && !_waitingWriters.HasPendingTasks())
            {
                ++_status;
                return ValueTask.FromResult<IDisposable>(new Releaser(this, isWriter: false));
            }

            // Otherwise enqueue the reader.
            return new(_waitingReaders.Enqueue(cancellationToken));
        }
    }

    /// <summary>
    /// Request write access to the lock.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request while waiting.</param>
    /// <returns>Promise for a disposable which must be disposed in order to release the lock.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    public ValueTask<IDisposable> WriterLockAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Fast path when no access is active and no other writer is already waiting.
            if (_status == 0 && !_waitingWriters.HasPendingTasks())
            {
                _status = -1;
                return ValueTask.FromResult<IDisposable>(new Releaser(this, isWriter: true));
            }

            // Otherwise enqueue the writer.
            return new(_waitingWriters.Enqueue(cancellationToken));
        }
    }

    /// <summary>
    /// Releases the lock and faults all waiting requests with an <see cref="ObjectDisposedException"/>.
    /// </summary>
    /// <remarks>Acquisitions that are already granted remain valid; disposing them is a no-op.</remarks>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _waitingWriters.Dispose();
            _waitingReaders.Dispose();
        }
    }

    /// <summary>
    /// Helper method to release a read acquisition.
    /// </summary>
    private void ReaderRelease()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // Decrement status
            --_status;

            // If no reader has acquired the lock, either release the next waiting writer or, if
            // none are waiting, release all waiting readers.
            if (_status == 0)
            {
                _status = _waitingWriters.CompleteNext() ? -1 : _waitingReaders.CompleteAll();
            }
        }
    }

    /// <summary>
    /// Helper method to release a write acquisition.
    /// </summary>
    private void WriterRelease()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // If other writers are waiting, the next writer in the queue is released.
            if (_waitingWriters.CompleteNext())
            {
                return;
            }

            // If readers are waiting, release them.
            _status = _waitingReaders.CompleteAll();
        }
    }

    #endregion
}
