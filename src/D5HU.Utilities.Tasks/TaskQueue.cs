using System.Diagnostics.CodeAnalysis;

namespace D5HU.Utilities;

/// <summary>
/// Specialized queue for managing pending tasks that can be completed in order.
/// </summary>
public sealed class TaskQueue<T> : IDisposable
{
    #region Nested definitions

    /// <summary>
    /// Helper class to combine a task completion source with cancellation token within the queue.
    /// </summary>
    private sealed class Entry : IDisposable
    {
        /// <summary>
        /// The task completion source for this entry.
        /// </summary>
        private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Associated cancellation token.
        /// </summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Registration disposable of the associated cancellation token.
        /// </summary>
        private readonly CancellationTokenRegistration _registration;

        /// <summary>
        /// Task of the entry.
        /// </summary>
        public Task<T> Task => _tcs.Task;

        /// <summary>
        /// Initializes the entry and registers cancellation on the given token.
        /// </summary>
        public Entry(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _registration = cancellationToken.Register(static state => ((Entry) state!).Cancel(), this);
        }

        /// <summary>
        /// Tries to set the result for the task.
        /// </summary>
        public bool TrySetResult(T result)
        {
            return _tcs.TrySetResult(result);
        }

        /// <summary>
        /// Sets the exception for the task.
        /// </summary>
        public void SetException(Exception exception)
        {
            _tcs.TrySetException(exception);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _registration.Dispose();
        }

        /// <summary>
        /// Helper to cancel the task completion source.
        /// </summary>
        private void Cancel()
        {
            _tcs.TrySetCanceled(_cancellationToken);
        }
    }

    #endregion

    #region Fields

    /// <summary>
    /// Result factory for completed tasks
    /// </summary>
    private readonly Func<T> _resultFactory;

    /// <summary>
    /// Lock used when accessing the queue.
    /// </summary>
    private readonly Lock _queueLock = new();

    /// <summary>
    /// Queue of pending task completion sources.
    /// </summary>
    private Queue<Entry>? _queue = new();

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the task queue with a factory for creating results.
    /// </summary>
    public TaskQueue(Func<T> resultFactory)
    {
        ArgumentNullException.ThrowIfNull(resultFactory);

        _resultFactory = resultFactory;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new pending task and enqueues it.
    /// </summary>
    public Task<T> Enqueue(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        Entry entry = new(cancellationToken);
        Enqueue(entry);
        return entry.Task;
    }

    /// <summary>
    /// Completes the next pending task in the queue and returns whether a task was completed.
    /// </summary>
    /// <returns>
    /// Returns <see langword="true"/> if a pending task was completed or <see langword="false"/>
    /// if there was no pending task to complete.
    /// </returns>
    public bool CompleteNext()
    {
        while (TryDequeue(out Entry? entry))
        {
            entry.Dispose();

            if (!entry.Task.IsCompleted && entry.TrySetResult(_resultFactory()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether there are any pending tasks in the queue.
    /// </summary>
    /// <remarks>The pending tasks might be completed or failed.</remarks>
    public bool HasPendingTasks()
    {
        lock (_queueLock)
        {
            return _queue is not null && _queue.Count > 0;
        }
    }

    /// <summary>
    /// Completes all pending tasks in the queue.
    /// </summary>
    public int CompleteAll()
    {
        int released = 0;

        while (TryDequeue(out Entry? entry))
        {
            entry.Dispose();

            if (!entry.Task.IsCompleted && entry.TrySetResult(_resultFactory()))
            {
                released++;
            }
        }

        return released;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Queue<Entry>? queue;
        lock (_queueLock)
        {
            if (_queue is null)
            {
                return;
            }

            queue = _queue;
            _queue = null;
        }

        while (queue.TryDequeue(out Entry? entry))
        {
            if (!entry.Task.IsCompleted)
            {
                entry.SetException(new ObjectDisposedException(GetType().Name));
            }

            entry.Dispose();
        }
    }

    /// <summary>
    /// Helper to dequeue an entry from the queue in a thread-safe manner.
    /// </summary>
    private bool TryDequeue([MaybeNullWhen(false)] out Entry result)
    {
        lock (_queueLock)
        {
            ObjectDisposedException.ThrowIf(_queue is null, this);
            return _queue.TryDequeue(out result);
        }
    }

    /// <summary>
    /// Helper to enqueue an entry to the queue in a thread-safe manner.
    /// </summary>
    private void Enqueue(Entry result)
    {
        lock (_queueLock)
        {
            ObjectDisposedException.ThrowIf(_queue is null, this);
            _queue.Enqueue(result);
        }
    }

    #endregion
}
