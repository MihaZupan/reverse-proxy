// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    public interface ITelemetryConsumerFactory<TConsumer>
    {
        // Called once per request
        TConsumer Create();
    }

    internal abstract class EventListenerService<TService, TTelemetryConsumer, TMetricsConsumer> : EventListener, IHostedService
    {
        // We need a way to signal to OnEventSourceCreated that the EventListenerService constructor finished
        // OnEventSourceCreated may be called before we even reach the derived ctor (as it's exposed from the base ctor)
        // Because of that, we can't assign the MRE as part of the ctor, we have to do it as part of the _initializedMre field initializer
        // But since the ctor itself may throw here, we need a way to observe the same MRE instance from outside the ctor
        // We pull the MRE from a ThreadStatic that a ctor wrapper (Create) can observe
        [ThreadStatic]
        private static ManualResetEventSlim _threadStaticInitializedMre;

        public static TEventListener Create<TEventListener>(IServiceProvider serviceProvider)
            where TEventListener : EventListenerService<TService, TTelemetryConsumer, TMetricsConsumer>
        {
            _threadStaticInitializedMre = new();
            try
            {
                return ActivatorUtilities.CreateInstance<TEventListener>(serviceProvider);
            }
            finally
            {
                _threadStaticInitializedMre.Set();
                _threadStaticInitializedMre = null;
            }
        }

        protected abstract string EventSourceName { get; }

        protected readonly ILogger<TService> Logger;
        protected readonly TMetricsConsumer[] MetricsConsumers;

        private readonly IHttpContextAccessor _httpContextAccessor;

        private EventSource _eventSource;
        private readonly object _syncObject = new();
        private readonly ManualResetEventSlim _initializedMre = _threadStaticInitializedMre;

        private readonly int _telemetryConsumerCount;
        private readonly TTelemetryConsumer[] _telemetryConsumerSingletons;
        private readonly ITelemetryConsumerFactory<TTelemetryConsumer>[] _telemetryConsumerFactories;
        private readonly AsyncLocal<TTelemetryConsumer[]> _currentScopeTelemetryConsumers;

        public EventListenerService(
            ILogger<TService> logger,
            IHttpContextAccessor httpContextAccessor,
            IEnumerable<TTelemetryConsumer> consumerSingletons,
            IEnumerable<ITelemetryConsumerFactory<TTelemetryConsumer>> consumerFactories,
            IEnumerable<TMetricsConsumer> metricsConsumers)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            // ToDo null checks

            _telemetryConsumerSingletons = consumerSingletons.ToArray();
            _telemetryConsumerFactories = consumerFactories.ToArray();
            _telemetryConsumerCount = _telemetryConsumerSingletons.Length + _telemetryConsumerFactories.Length;

            if (_telemetryConsumerSingletons.Length == 0)
            {
                _telemetryConsumerSingletons = null;
            }

            if (_telemetryConsumerFactories.Length == 0)
            {
                _telemetryConsumerFactories = null;
            }
            else
            {
                _currentScopeTelemetryConsumers = new();
            }

            MetricsConsumers = metricsConsumers.ToArray();

            lock (_syncObject)
            {
                if (_eventSource is not null)
                {
                    EnableEventSource();
                }

                Debug.Assert(_initializedMre is not null);
                _initializedMre = null;
            }
        }

        public TTelemetryConsumer[] GetTelemetryConsumers()
        {
            var scope = _currentScopeTelemetryConsumers;
            if (scope is null)
            {
                return _telemetryConsumerSingletons;
            }

            var consumers = scope.Value;
            if (consumers is not null)
            {
                return consumers;
            }

            if (_httpContextAccessor.HttpContext is null)
            {
                return null;
            }

            consumers = new TTelemetryConsumer[_telemetryConsumerCount];
            var count = 0;

            var singletons = _telemetryConsumerSingletons;
            if (singletons is not null)
            {
                foreach (var singleton in singletons)
                {
                    consumers[count++] = singleton;
                }
            }

            foreach (var factory in _telemetryConsumerFactories)
            {
                consumers[count++] = factory.Create();
            }

            scope.Value = consumers;

            return consumers;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                lock (_syncObject)
                {
                    _eventSource = eventSource;

                    if (_initializedMre is null)
                    {
                        // Ctor already finished - enable the EventSource here
                        EnableEventSource();
                    }
                }

                // Ensure that the constructor finishes before exiting this method (so that the first events aren't dropped)
                // It's possible that we are executing as a part of the base ctor - only block if we're running on a different thread
                var mre = _initializedMre;
                if (mre is not null && !ReferenceEquals(mre, _threadStaticInitializedMre))
                {
                    mre.Wait();
                }
            }
        }

        private void EnableEventSource()
        {
            var enableEvents = _telemetryConsumerCount != 0;
            var enableMetrics = MetricsConsumers.Length != 0;

            if (!enableEvents && !enableMetrics)
            {
                return;
            }

            var eventLevel = enableEvents ? EventLevel.LogAlways : EventLevel.Critical;
            var arguments = enableMetrics ? new Dictionary<string, string> { { "EventCounterIntervalSec", MetricsOptions.Interval.TotalSeconds.ToString() } } : null;

            EnableEvents(_eventSource, eventLevel, EventKeywords.None, arguments);
            _eventSource = null;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
