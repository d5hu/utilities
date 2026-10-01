# D5HU.Utilities.Tasks

[![NuGet Package](https://img.shields.io/nuget/v/D5HU.Utilities.Tasks.svg)](https://www.nuget.org/packages/D5HU.Utilities.Tasks/)

Asynchronous coordination primitives for .NET.

## Installation
Install the package via NuGet:
```
dotnet add package D5HU.Utilities.Tasks
```

## Contents

### `AsyncReaderWriterLock`
An asynchronous reader/writer lock that allows multiple concurrent readers or a single exclusive
writer.

```csharp
using D5HU.Utilities;

AsyncReaderWriterLock @lock = new();

using (await @lock.ReaderLockAsync(cancellationToken))
{
    // Read access.
}

using (await @lock.WriterLockAsync(cancellationToken))
{
    // Exclusive write access.
}
```


### `TaskQueue<T>`
A specialized queue for managing pending tasks that can be completed in order, one at a time or all
at once.

```csharp
using D5HU.Utilities;

using TaskQueue<bool> queue = new(() => true);

Task<bool> pending = queue.Enqueue(cancellationToken);

// Elsewhere, complete the oldest pending task:
queue.CompleteNext();
```
