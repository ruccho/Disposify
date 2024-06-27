#nullable enable

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;

namespace Disposify
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct Disposable : IDisposable, IEquatable<Disposable>
    {
        private readonly DisposableCore core;
        private readonly int version;

        public bool Equals(Disposable other)
        {
            return core.Equals(other.core) && version == other.version;
        }

        public override bool Equals(object? obj)
        {
            return obj is Disposable other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(core, version);
        }

        public static bool operator ==(Disposable left, Disposable right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Disposable left, Disposable right)
        {
            return !left.Equals(right);
        }

        public static Disposable Create<T, TDelegate>(T? target, TDelegate @delegate, Action<T, TDelegate> unregister)
            where T : class where TDelegate : Delegate
        {
            return new Disposable(target, @delegate, As<Action<T, TDelegate>, Action<object?, object>>(unregister));
        }

        private static unsafe TTo As<TFrom, TTo>(TFrom from) where TFrom : class where TTo : class
        {
            return ((delegate*<TFrom, TTo>)(delegate*<object, object>)(&AsRaw))(from);
        }

        private static object AsRaw(object target)
        {
            return target;
        }

        private Disposable(object? target, object @delegate, Action<object?, object> unregister)
        {
            var core = DisposableCore.Get(target, @delegate, unregister);
            this.core = core;
            version = core.Version;
        }

        public void Dispose()
        {
            core.Dispose(version);
        }

        private class DisposableCore
        {
            private static readonly ConcurrentStack<DisposableCore> Pool = new();
            private object @delegate;

            private object? target;
            private Action<object?, object> unregister;

            private int version;

            public DisposableCore(object? target, object @delegate, Action<object?, object> unregister)
            {
                this.target = target;
                this.@delegate = @delegate;
                this.unregister = unregister;
            }

            public int Version => version;

            public static DisposableCore Get(object? target, object @delegate, Action<object?, object> unregister)
            {
                if (Pool.TryPop(out var result))
                {
                    result.target = target;
                    result.@delegate = @delegate;
                    result.unregister = unregister;
                }
                else
                {
                    result = new DisposableCore(target, @delegate, unregister);
                }

                return result;
            }

            public void Dispose(int version)
            {
                if (Interlocked.CompareExchange(ref this.version, unchecked(version + 1), version) != version) return;
                unregister(target, @delegate);

                if (version == -2) return; // truly disposed

                Pool.Push(this);
            }
        }
    }
}