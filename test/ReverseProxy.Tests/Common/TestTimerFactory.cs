// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Common.Tests
{
    internal class TestTimerFactory : ITimerFactory, IDisposable
    {
        public readonly List<TimerStub> Timers = new();

        public int Count => Timers.Count;

        public void FireTimer(int idx)
        {
            Timers[idx].Fire();
        }

        public void FireAll()
        {
            var timers = Timers.Count;
            for (var i = 0; i < timers; i++)
            {
                FireTimer(i);
            }
        }

        public void VerifyTimer(int idx, long dueTime)
        {
            Assert.Equal(dueTime, Timers[idx].DueTime);
        }

        public void AssertTimerDisposed(int idx)
        {
            Assert.True(Timers[idx].IsDisposed);
        }

        public IDisposable CreateTimer(TimerCallback callback, object state, long dueTime, long period)
        {
            Assert.Equal(Timeout.Infinite, period);
            var timer = new TimerStub(callback, state, dueTime, period);
            Timers.Add(timer);
            return timer;
        }

        public void Dispose()
        {}

        public sealed class TimerStub : IDisposable
        {
            public TimerStub(TimerCallback callback, object state, long dueTime, long period)
            {
                Callback = callback;
                State = state;
                DueTime = dueTime;
                Period = period;
            }

            public long DueTime { get; private set; }

            public long Period { get; private set; }

            public TimerCallback Callback { get; private set; }

            public object State { get; private set; }

            public bool IsDisposed { get; private set; }

            public void Fire()
            {
                if (!IsDisposed)
                {
                    Callback(State);
                }
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
