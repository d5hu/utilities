namespace D5HU.Utilities;

[TestFixture(TestOf = typeof(TaskQueue<>))]
internal class TaskQueueTests
{
    #region Tests: Construcors

    [Test]
    public void Constructor_ThrowsArgumentNullExceptionWhenFactoryIsNull()
    {
        // Act
        static void ActFunction()
        {
            _ = new TaskQueue<int>(null!);
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<ArgumentNullException>()
            .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("resultFactory"));
    }

    #endregion

    #region Tests: Enqueue method

    [Test]
    public void Enqueue_ReturnsCanceledTaskWhenAlreadyCancelled()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Task<int> task = queue.Enqueue(cts.Token);

        // Assert
        Assert.That(task.IsCanceled, Is.True);
    }

    [Test]
    public void Enqueue_TaskRemainsPendingWhenNoCompletion()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();

        // Act
        Task<int> task = queue.Enqueue(CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(queue.HasPendingTasks(), Is.True);
        }
    }

    [Test]
    public void Enqueue_CancelsTaskWhenTokenCancelledAfterEnqueue()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();
        using CancellationTokenSource cts = new();

        // Act
        Task<int> task = queue.Enqueue(cts.Token);
        cts.Cancel();

        // Assert
        Assert.That(task.IsCanceled, Is.True);
    }

    [Test]
    public void Enqueue_ThrowsObjectDisposedExceptionWhenDisposed()
    {
        // Arrange
        TaskQueue<int> queue = CreateQueue();
        queue.Dispose();

        // Act
        void ActFunction()
        {
            _ = queue.Enqueue(CancellationToken.None);
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<ObjectDisposedException>());
    }

    #endregion

    #region Tests: CompleteNext method

    [Test]
    public void CompleteNext_ReturnsFalseWhenNoPendingTasks()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();

        // Act
        bool result = queue.CompleteNext();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test, CancelAfter(1000)]
    public async Task CompleteNext_CompletesTaskWithFactoryResult(CancellationToken cancellationToken)
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue(99);
        Task<int> task = queue.Enqueue(cancellationToken);

        // Act
        bool result = queue.CompleteNext();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(await task, Is.EqualTo(99));
        }
    }

    [Test, CancelAfter(1000)]
    public async Task CompleteNext_CompletesInFifoOrder(CancellationToken cancellationToken)
    {
        // Arrange
        int index = 0;
        using TaskQueue<int> queue = CreateQueue(() => Interlocked.Increment(ref index));
        Task<int> first = queue.Enqueue(cancellationToken);
        Task<int> second = queue.Enqueue(cancellationToken);

        // Act
        queue.CompleteNext();
        queue.CompleteNext();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await first, Is.EqualTo(1));
            Assert.That(await second, Is.EqualTo(2));
        }
    }

    [Test]
    public void CompleteNext_SkipsAlreadyCancelledEntriesAndCompletesNextPending()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();
        using CancellationTokenSource cts = new();

        Task<int> cancelledTask = queue.Enqueue(cts.Token);
        Task<int> pendingTask = queue.Enqueue(CancellationToken.None);

        cts.Cancel();

        // Act
        bool result = queue.CompleteNext();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cancelledTask.IsCanceled, Is.True);
            Assert.That(result, Is.True);
            Assert.That(pendingTask.IsCompletedSuccessfully, Is.True);
        }
    }

    [Test]
    public void CompleteNext_ThrowsObjectDisposedExceptionWhenDisposed()
    {
        // Arrange
        TaskQueue<int> queue = CreateQueue();
        queue.Dispose();

        // Act
        void ActFunction()
        {
            queue.CompleteNext();
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<ObjectDisposedException>());
    }

    #endregion

    #region Tests: HasPendingTasks method

    [Test]
    public void HasPendingTasks_ReturnsFalseWhenQueueIsEmpty()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();

        // Act
        bool result = queue.HasPendingTasks();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasPendingTasks_ReturnsFalseAfterAllEntriesCompleted()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();
        queue.Enqueue(CancellationToken.None);
        queue.CompleteNext();

        // Act
        bool result = queue.HasPendingTasks();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasPendingTasks_ReturnsFalseWhenDisposed()
    {
        // Arrange
        TaskQueue<int> queue = CreateQueue();
        _ = queue.Enqueue(CancellationToken.None);
        queue.Dispose();

        // Act
        bool result = queue.HasPendingTasks();

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region Tests: CompleteAll method

    [Test]
    public void CompleteAll_ReturnsZeroWhenNoPendingTasks()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();

        // Act
        int released = queue.CompleteAll();

        // Assert
        Assert.That(released, Is.Zero);
    }

    [Test, CancelAfter(1000)]
    public async Task CompleteAll_CompletesAllPendingAndReturnsCount(CancellationToken cancellationToken)
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue(7);
        Task<int> first = queue.Enqueue(cancellationToken);
        Task<int> second = queue.Enqueue(cancellationToken);
        Task<int> third = queue.Enqueue(cancellationToken);

        // Act
        int released = queue.CompleteAll();

        // Assert
        int[] results = await Task.WhenAll(first, second, third);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(released, Is.EqualTo(3));
            Assert.That(results, Is.All.EqualTo(7));
        }
    }

    [Test]
    public void CompleteAll_OnlyCountsCompletedOnes()
    {
        // Arrange
        using TaskQueue<int> queue = CreateQueue();
        using CancellationTokenSource cts = new();

        Task<int> cancelledTask = queue.Enqueue(cts.Token);
        Task<int> pendingTask = queue.Enqueue(CancellationToken.None);
        cts.Cancel();

        // Act
        int released = queue.CompleteAll();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cancelledTask.IsCanceled, Is.True);
            Assert.That(pendingTask.IsCompletedSuccessfully, Is.True);
            Assert.That(released, Is.EqualTo(1));
        }
    }

    [Test]
    public void CompleteAll_ThrowsObjectDisposedExceptionWhenDisposed()
    {
        // Arrange
        TaskQueue<int> queue = CreateQueue();
        queue.Dispose();

        // Act
        void ActFunction()
        {
            queue.CompleteAll();
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<ObjectDisposedException>());
    }

    #endregion

    #region Tests: Dispose method

    [Test]
    public void Dispose_FaultsPendingTasksWithObjectDisposedException()
    {
        // Arrange
        TaskQueue<int> queue = CreateQueue();
        Task<int> task = queue.Enqueue(CancellationToken.None);

        // Act
        queue.Dispose();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception?.InnerException, Is.InstanceOf<ObjectDisposedException>());
        }
    }

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        TaskQueue<int> queue = CreateQueue();

        // Act
        void ActFunction()
        {
            queue.Dispose();
            queue.Dispose();
        }

        // Assert
        Assert.That(ActFunction, Throws.Nothing);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a new instance with a static result factory.
    /// </summary>
    private static TaskQueue<int> CreateQueue(int result = 42)
    {
        return new(() => result);
    }

    /// <summary>
    /// Creates a new instance with a custom result factory.
    /// </summary>
    private static TaskQueue<int> CreateQueue(Func<int> resultFactory)
    {
        return new(resultFactory);
    }

    #endregion
}
