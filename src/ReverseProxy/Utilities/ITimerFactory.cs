// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal interface ITimerFactory
    {
        IDisposable CreateTimer(TimerCallback callback, object state, long dueTime, long period);
    }
}
