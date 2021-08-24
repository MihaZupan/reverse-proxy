// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health
{
    /// <summary>
    /// Periodically invokes specified actions on registered entities.
    /// </summary>
    /// <remarks>
    /// It creates a separate <see cref="Timer"/> for each registration which is considered
    /// reasonably efficient because .NET already maintains a process-wide managed timer queue.
    /// There are 2 scheduling modes supported: run once and infinite run. In "run once" mode,
    /// an entity gets unscheduled after the respective timer fired for the first time whereas
    /// in "infinite run" entities get repeatedly rescheduled until either they are explicitly removed
    /// or the <see cref="EntityActionScheduler{T}"/> instance is disposed.
    /// </remarks>
    internal sealed class EntityActionScheduler<T> : IDisposable where T : notnull
    {
        private readonly ConcurrentDictionary<T, SchedulerEntry> _entries = new();
        private readonly WeakReference<EntityActionScheduler<T>> _weakThisRef;
        private readonly Func<T, Task> _action;
        private readonly bool _runOnce;
        private readonly ITimerFactory _timerFactory;

        private const int NotStarted = 0;
        private const int Started = 1;
        private const int Disposed = 2;
        private int _status;

        public EntityActionScheduler(Func<T, Task> action, bool autoStart, bool runOnce, ITimerFactory timerFactory)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _runOnce = runOnce;
            _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
            _status = autoStart ? Started : NotStarted;
            _weakThisRef = new WeakReference<EntityActionScheduler<T>>(this);
        }

        public void Dispose()
        {
            Volatile.Write(ref _status, Disposed);

            foreach(var entry in _entries.Values)
            {
                entry.Dispose();
            }
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _status, Started, NotStarted) != NotStarted)
            {
                return;
            }

            foreach (var entry in _entries.Values)
            {
                entry.EnsureStarted();
            }
        }

        public void ScheduleEntity(T entity, TimeSpan period)
        {
            // Ensure the Timer has a weak reference to this scheduler; otherwise,
            // EntityActionScheduler can be rooted by the Timer implementation.
            var entry = new SchedulerEntry(_weakThisRef, entity, (long)period.TotalMilliseconds);

            if (_entries.TryAdd(entity, entry))
            {
                // Scheduler could have been started while we were adding the new entry.
                // Start timer here to ensure it's not forgotten.
                if (Volatile.Read(ref _status) == Started)
                {
                    entry.EnsureStarted();
                }
            }
            else
            {
                entry.Dispose();
            }
        }

        public void ChangePeriod(T entity, TimeSpan newPeriod)
        {
            Debug.Assert(!_runOnce, "Calling ChangePeriod on a RunOnce scheduler may cause the callback to fire twice");

            if (_entries.TryGetValue(entity, out var entry))
            {
                entry.ChangePeriod((long)newPeriod.TotalMilliseconds);
            }
            else
            {
                ScheduleEntity(entity, newPeriod);
            }
        }

        public void UnscheduleEntity(T entity)
        {
            if (_entries.TryRemove(entity, out var entry))
            {
                entry.Dispose();
            }
        }

        public bool IsScheduled(T entity)
        {
            return _entries.ContainsKey(entity);
        }

        private sealed class SchedulerEntry : IDisposable
        {
            // Timer.Change is racy as the callback could already be scheduled while we are starting the timer again.
            // This could result in the callback executing multiple times concurrently.
            // To avoid this, always create a new timer and pass a version as state.
            private static readonly TimerCallback _timerCallback = static async state =>
            {
                var (entry, version) = ((SchedulerEntry, int))state!;

                lock (entry)
                {
                    if (entry._version != version)
                    {
                        return;
                    }

                    entry._runningCallback = true;
                }

                if (!entry._scheduler.TryGetTarget(out var scheduler))
                {
                    return;
                }

                var pair = new KeyValuePair<T, SchedulerEntry>(entry._entity, entry);

                if (scheduler._runOnce && scheduler._entries.TryRemove(pair))
                {
                    entry.Dispose();
                }

                try
                {
                    await scheduler._action(entry._entity);

                    if (!scheduler._runOnce && scheduler._entries.Contains(pair))
                    {
                        // This entry has not been unscheduled - set the timer again
                        lock (entry)
                        {
                            entry._runningCallback = false;
                            entry.SetTimer();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // We are running on the ThreadPool, don't propagate excetions
                    Debug.Fail(ex.ToString()); // TODO: Log
                    if (scheduler._entries.TryRemove(pair))
                    {
                        entry.Dispose();
                    }
                    return;
                }
            };

            private readonly WeakReference<EntityActionScheduler<T>> _scheduler;
            private readonly T _entity;
            private IDisposable? _timer;
            private long _period;
            private int _version;
            private bool _runningCallback;
            private bool _disposed;

            public SchedulerEntry(WeakReference<EntityActionScheduler<T>> scheduler, T entity, long period)
            {
                _scheduler = scheduler;
                _entity = entity;
                _period = period;
            }

            public void ChangePeriod(long newPeriod)
            {
                lock (this)
                {
                    _period = newPeriod;
                    if (_timer is not null)
                    {
                        SetTimer();
                    }
                }
            }

            public void EnsureStarted()
            {
                lock (this)
                {
                    if (_timer is null)
                    {
                        SetTimer();
                    }
                }
            }

            private void SetTimer()
            {
                Debug.Assert(Monitor.IsEntered(this));

                if (_disposed || _runningCallback || !_scheduler.TryGetTarget(out var scheduler))
                {
                    return;
                }

                _timer?.Dispose();
                _version++;

                // Don't capture the current ExecutionContext and its AsyncLocals onto the timer causing them to live forever
                var restoreFlow = false;
                try
                {
                    if (!ExecutionContext.IsFlowSuppressed())
                    {
                        ExecutionContext.SuppressFlow();
                        restoreFlow = true;
                    }

                    _timer = scheduler._timerFactory.CreateTimer(_timerCallback, (this, _version), _period, Timeout.Infinite);
                }
                finally
                {
                    if (restoreFlow)
                    {
                        ExecutionContext.RestoreFlow();
                    }
                }
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (!_disposed)
                    {
                        _disposed = true;
                        _timer?.Dispose();
                    }
                }
            }
        }
    }
}
