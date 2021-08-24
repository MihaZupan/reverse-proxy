// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal sealed class TimerFactory : ITimerFactory
    {
        public IDisposable CreateTimer(TimerCallback callback, object state, long dueTime, long period)
        {
            return new Timer(callback, state, dueTime, period);
        }
    }
}
