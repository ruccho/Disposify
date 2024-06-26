# Disposify

> [!WARNING]
> Currently very experimental.

Disposify is a source generator to subscribe / unsubscribe C# events with using-IDisposable pattern.

```csharp
using System;
using Disposify;

var c = new C();

using (c.Disposify().SomeEvent(v => ++v)) // subscribe with lambda
{
    c.Invoke(100);

} // unsubscribe on Dispose()

public class C
{
    public event Func<int, int>? SomeEvent;
    public int Invoke(int a) => SomeEvent.Invoke(a);
}
```

It is also useful to convert callbacks into `await` pattern.

```csharp
using System;
using System.Threading.Tasks;
using Disposify;

var c = new C();
var tcs = new TaskCompletionSource<int>();

using (c.Disposify().SomethingCompleted(result => tcs.TrySetResult(result)))
{
    var result = await tcs.Task;
}

public class C
{
    public event Action<int>? SomethingCompleted;
}
```

## Installation

### NuGet

WIP

### Unity

Unity 2022.3.12f1 and later is supported.

Add a git dependency to package manager:

```
https://github.com/ruccho/Disposify.git?path=/Disposify.Unity/Packages/com.ruccho.disposify
```

## Features

 - Disposifying events declared in non-generic classes

### Not supported yet

 - Disposifying events declared in generic types and value types


## Performance

 - Instances of `IDisposable` is automatically pooled after the disposal.
 - `Disposify()` seems to return `dynamic` but they are replaced with specialized overloads by source generator. There are no boxing allocations.

## Usages

### Disposifying static events

Use `((T)null).Disposify()`.

```csharp
using System;
using Disposify;

using (((C)null).Disposify().SomeEvent(v => ++v))
{
    C.Invoke(100);
}

public static class C
{
    public static event Func<int, int>? SomeEvent;
    public static int Invoke(int a) => SomeEvent.Invoke(a);
}
```