# Shaman.Async

Async-related utilities.

## ForEachThrottledAsync
Calls an async method for each item in a collection, specifying a concurrency level.

The code runs as a coroutine, with no multithreading (thus making it easier to reason about the async code).

```csharp
using Shaman;

await list.ForEachThrottledAsync(async x =>
{
    // process x asynchronously
}, 10); 

```

## SingleTaskContext
```csharp
using Shaman.Runtime;

class Example
{
    SingleTaskContext ctx = new SingleTaskContext();


    public async Task ExecuteAsync()
    {
        // Calling StartNew will cancel the token returned by the previous invocation of StartNew,
        // ensuring only a single execution of `ExecuteAsync` is running at any given time (the most recent one).
        CancellationToken t = ctx.StartNew();

        // await ProcessSomethingAsync1(t);
        
        t.ThrowIfCancellationRequested();

        // await ProcessSomethingAsync2(t);  
    }
}

```