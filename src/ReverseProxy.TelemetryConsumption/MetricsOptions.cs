// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    internal static class MetricsOptions
    {
        // TODO: Should this be publicly configurable? It's currently only visible to tests to reduce execution time
        public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        public static EventLevel Level { get; set; } = EventLevel.LogAlways;
    }
}
