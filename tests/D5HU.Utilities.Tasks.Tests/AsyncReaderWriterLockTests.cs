namespace D5HU.Utilities;

[TestFixture(TestOf = typeof(AsyncReaderWriterLock))]
internal class AsyncReaderWriterLockTests
{
    #region Tests: ReaderLockAsync method

    [Test]
    public void ReaderLockAsync_ThrowsOperationCanceledExceptionWhenAlreadyCancelled()
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        void ActFunction()
        {
            ValueTask<IDisposable> task = @lock.ReaderLockAsync(cts.Token);
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_CompletesSynchronouslyWhenNoActiveWriter(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();

        // Act
        ValueTask<IDisposable> task = @lock.ReaderLockAsync(cancellationToken);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(await task, Is.Not.Null);
        }
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_CompletesMultipleSynchronouslyWhenNoActiveWriter(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();

        // Act
        ValueTask<IDisposable> first = @lock.ReaderLockAsync(cancellationToken);
        ValueTask<IDisposable> second = @lock.ReaderLockAsync(cancellationToken);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.IsCompletedSuccessfully, Is.True);
            Assert.That(second.IsCompletedSuccessfully, Is.True);
        }
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_WaitsUntilWriterReleases(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable writerHandle = await @lock.WriterLockAsync(cancellationToken);

        // Act
        ValueTask<IDisposable> readerTask = @lock.ReaderLockAsync(cancellationToken);

        // Assert
        Assert.That(readerTask.IsCompleted, Is.False);
        writerHandle.Dispose();
        IDisposable readerHandle = await readerTask;
        Assert.That(readerHandle, Is.Not.Null);
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_IsNotReleasedBeforeWaitingWriter(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable firstReaderHandle = await @lock.ReaderLockAsync(cancellationToken);
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);

        // Act
        ValueTask<IDisposable> secondReaderTask = @lock.ReaderLockAsync(cancellationToken);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(writerTask.IsCompleted, Is.False);
            Assert.That(secondReaderTask.IsCompleted, Is.False);
        }

        firstReaderHandle.Dispose();
        await writerTask;
        Assert.That(secondReaderTask.IsCompleted, Is.False);
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_CancelsTaskWhenTokenCancelledWhileWaiting(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable writerHandle = await @lock.WriterLockAsync(cancellationToken);
        using CancellationTokenSource cts = new();

        // Act
        ValueTask<IDisposable> readerTask = @lock.ReaderLockAsync(cts.Token);
        cts.Cancel();

        // Assert
        Assert.That(async () => await readerTask, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_ReleasesAllWaitingReadersAfterWriterReleases(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable writerHandle = await @lock.WriterLockAsync(cancellationToken);
        ValueTask<IDisposable> firstReaderTask = @lock.ReaderLockAsync(cancellationToken);
        ValueTask<IDisposable> secondReaderTask = @lock.ReaderLockAsync(cancellationToken);

        // Act
        writerHandle.Dispose();

        // Assert
        IDisposable firstReaderHandle = await firstReaderTask;
        IDisposable secondReaderHandle = await secondReaderTask;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstReaderHandle, Is.Not.Null);
            Assert.That(secondReaderHandle, Is.Not.Null);
        }
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_ReleasesWaitingReadersWhenLastReaderReleasesAfterWriterCancellation(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable firstReaderHandle = await @lock.ReaderLockAsync(cancellationToken);
        using CancellationTokenSource writerCts = new();
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(writerCts.Token);
        ValueTask<IDisposable> secondReaderTask = @lock.ReaderLockAsync(cancellationToken);
        ValueTask<IDisposable> thirdReaderTask = @lock.ReaderLockAsync(cancellationToken);

        // Act
        writerCts.Cancel();
        firstReaderHandle.Dispose();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await writerTask, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(await secondReaderTask, Is.Not.Null);
            Assert.That(await thirdReaderTask, Is.Not.Null);
        }
    }

    [Test, CancelAfter(1000)]
    public async Task ReaderLockAsync_DoesNotThrowOrDoubleRelease(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable firstHandle = await @lock.ReaderLockAsync(cancellationToken);
        IDisposable secondHandle = await @lock.ReaderLockAsync(cancellationToken);

        // Act
        void ActFunction()
        {
            firstHandle.Dispose();
            firstHandle.Dispose();
        }

        // Assert
        Assert.That(ActFunction, Throws.Nothing);
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);
        Assert.That(writerTask.IsCompleted, Is.False);
        secondHandle.Dispose();
        Assert.That(await writerTask, Is.Not.Null);
    }

    #endregion

    #region Tests: WriterLockAsync method

    [Test]
    public void WriterLockAsync_ThrowsOperationCanceledExceptionWhenAlreadyCancelled()
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        void ActFunction()
        {
            ValueTask<IDisposable> task = @lock.WriterLockAsync(cts.Token);
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test, CancelAfter(1000)]
    public async Task WriterLockAsync_CompletesSynchronouslyWhenNoActiveReaderOrWriter(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();

        // Act
        ValueTask<IDisposable> task = @lock.WriterLockAsync(cancellationToken);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(await task, Is.Not.Null);
        }
    }

    [Test, CancelAfter(1000)]
    public async Task WriterLockAsync_WaitsUntilReleasedByPreviousWriter(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable firstWriterHandle = await @lock.WriterLockAsync(cancellationToken);

        // Act
        ValueTask<IDisposable> secondWriterTask = @lock.WriterLockAsync(cancellationToken);

        // Assert
        Assert.That(secondWriterTask.IsCompleted, Is.False);
        firstWriterHandle.Dispose();
        Assert.That(await secondWriterTask, Is.Not.Null);
    }

    [Test, CancelAfter(1000)]
    public async Task WriterLockAsync_WaitsUntilReleasedByAllPreviousReaders(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable firstReaderHandle = await @lock.ReaderLockAsync(cancellationToken);
        IDisposable secondReaderHandle = await @lock.ReaderLockAsync(cancellationToken);

        // Act
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);

        // Assert
        Assert.That(writerTask.IsCompleted, Is.False);

        firstReaderHandle.Dispose();
        Assert.That(writerTask.IsCompleted, Is.False);

        secondReaderHandle.Dispose();
        Assert.That(await writerTask, Is.Not.Null);
    }

    [Test, CancelAfter(1000)]
    public async Task WriterLockAsync_ReleasesQueuedWritersOneAtATime(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable firstWriterHandle = await @lock.WriterLockAsync(cancellationToken);
        ValueTask<IDisposable> secondWriterTask = @lock.WriterLockAsync(cancellationToken);
        ValueTask<IDisposable> thirdWriterTask = @lock.WriterLockAsync(cancellationToken);

        // Act
        firstWriterHandle.Dispose();
        IDisposable secondWriterHandle = await secondWriterTask;

        // Assert
        Assert.That(thirdWriterTask.IsCompleted, Is.False);

        secondWriterHandle.Dispose();
        IDisposable thirdWriterHandle = await thirdWriterTask;
        Assert.That(thirdWriterHandle, Is.Not.Null);
    }

    [Test, CancelAfter(1000)]
    public async Task WriterLockAsync_CancelsTaskWhenTokenCancelledWhileWaiting(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable writerHandle = await @lock.WriterLockAsync(cancellationToken);
        using CancellationTokenSource cts = new();

        // Act
        ValueTask<IDisposable> waitingWriterTask = @lock.WriterLockAsync(cts.Token);
        cts.Cancel();

        // Assert
        Assert.That(async () => await waitingWriterTask, Throws.InstanceOf<OperationCanceledException>());

        writerHandle.Dispose();
    }

    [Test, CancelAfter(1000)]
    public async Task WriterLockAsync_DoesNotThrowOrDoubleRelease(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable writerHandle = await @lock.WriterLockAsync(cancellationToken);
        ValueTask<IDisposable> secondWriterTask = @lock.WriterLockAsync(cancellationToken);
        ValueTask<IDisposable> thirdWriterTask = @lock.WriterLockAsync(cancellationToken);

        // Act
        void ActFunction()
        {
            writerHandle.Dispose();
            writerHandle.Dispose();
        }

        // Assert
        Assert.That(ActFunction, Throws.Nothing);
        IDisposable secondWriterHandle = await secondWriterTask;
        Assert.That(thirdWriterTask.IsCompleted, Is.False);
        secondWriterHandle.Dispose();
        Assert.That(await thirdWriterTask, Is.Not.Null);
    }

    #endregion

    #region Tests: CreateWithReaderLock method

    [Test]
    public void CreateWithReaderLock_ReturnsLockAndDisposable()
    {
        // Arrange & Act
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithReaderLock();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(@lock, Is.Not.Null);
            Assert.That(handle, Is.Not.Null);
        }
    }

    [Test, CancelAfter(1_000)]
    public async Task CreateWithReaderLock_AllowsAdditionalReaders(CancellationToken cancellationToken)
    {
        // Arrange
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithReaderLock();

        // Act
        ValueTask<IDisposable> readerTask = @lock.ReaderLockAsync(cancellationToken);

        // Assert
        Assert.That(readerTask.IsCompletedSuccessfully, Is.True);
        handle.Dispose();
        (await readerTask).Dispose();
    }

    [Test, CancelAfter(1_000)]
    public async Task CreateWithReaderLock_BlocksWritersUntilDisposed(CancellationToken cancellationToken)
    {
        // Arrange
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithReaderLock();

        // Act
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);

        // Assert
        Assert.That(writerTask.IsCompleted, Is.False);
        handle.Dispose();
        Assert.That(await writerTask, Is.Not.Null);
    }

    [Test, CancelAfter(1_000)]
    public async Task CreateWithReaderLock_DoesNotDoubleReleaseWhenDisposedTwice(CancellationToken cancellationToken)
    {
        // Arrange
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithReaderLock();
        IDisposable secondReader = await @lock.ReaderLockAsync(cancellationToken);

        // Act
        handle.Dispose();
        handle.Dispose();

        // Assert
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);
        Assert.That(writerTask.IsCompleted, Is.False);
        secondReader.Dispose();
        Assert.That(await writerTask, Is.Not.Null);
    }

    #endregion

    #region Tests: CreateWithWriterLock method

    [Test]
    public void CreateWithWriterLock_ReturnsLockAndDisposable()
    {
        // Arrange & Act
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithWriterLock();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(@lock, Is.Not.Null);
            Assert.That(handle, Is.Not.Null);
        }
    }

    [Test, CancelAfter(1_000)]
    public async Task CreateWithWriterLock_BlocksReadersUntilDisposed(CancellationToken cancellationToken)
    {
        // Arrange
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithWriterLock();

        // Act
        ValueTask<IDisposable> readerTask = @lock.ReaderLockAsync(cancellationToken);

        // Assert
        Assert.That(readerTask.IsCompleted, Is.False);
        handle.Dispose();
        Assert.That(await readerTask, Is.Not.Null);
    }

    [Test, CancelAfter(1_000)]
    public async Task CreateWithWriterLock_BlocksWritersUntilDisposed(CancellationToken cancellationToken)
    {
        // Arrange
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithWriterLock();

        // Act
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);

        // Assert
        Assert.That(writerTask.IsCompleted, Is.False);
        handle.Dispose();
        Assert.That(await writerTask, Is.Not.Null);
    }

    [Test, CancelAfter(1_000)]
    public async Task CreateWithWriterLock_AllowsAcquisitionAfterDisposeWithoutWaiters(CancellationToken cancellationToken)
    {
        // Arrange
        (AsyncReaderWriterLock @lock, IDisposable handle) = AsyncReaderWriterLock.CreateWithWriterLock();

        // Act
        handle.Dispose();
        handle.Dispose();

        // Assert
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);
        Assert.That(writerTask.IsCompletedSuccessfully, Is.True);
        Assert.That(await writerTask, Is.Not.Null);
    }

    #endregion

    #region Tests: Dispose method

    [Test]
    public void Dispose_DoesNotThrowWhenCalledMultipleTimes()
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();

        // Act
        void ActFunction()
        {
            @lock.Dispose();
            @lock.Dispose();
        }

        // Assert
        Assert.That(ActFunction, Throws.Nothing);
    }

    [Test, CancelAfter(1_000)]
    public async Task Dispose_FaultsWaitingReadersAndWritersWithObjectDisposedException(CancellationToken cancellationToken)
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        IDisposable writerHandle = await @lock.WriterLockAsync(cancellationToken);
        ValueTask<IDisposable> readerTask = @lock.ReaderLockAsync(cancellationToken);
        ValueTask<IDisposable> writerTask = @lock.WriterLockAsync(cancellationToken);

        // Act
        @lock.Dispose();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await readerTask, Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(async () => await writerTask, Throws.InstanceOf<ObjectDisposedException>());
            Assert.That(writerHandle.Dispose, Throws.Nothing);
        }
    }

    [Test]
    public void Dispose_ReaderLockAsyncThrowsObjectDisposedExceptionWhenDisposed()
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        @lock.Dispose();

        // Act
        void ActFunction()
        {
            ValueTask<IDisposable> handle = @lock.ReaderLockAsync();
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Dispose_WriterLockAsyncThrowsObjectDisposedExceptionWhenDisposed()
    {
        // Arrange
        AsyncReaderWriterLock @lock = new();
        @lock.Dispose();

        // Act
        void ActFunction()
        {
            _ = @lock.WriterLockAsync();
        }

        // Assert
        Assert.That(ActFunction, Throws.InstanceOf<ObjectDisposedException>());
    }

    #endregion
}
