// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TelemetryConsumptionExtensions
    {
#if NET5_0
        /// <summary>
        /// Registers all telemetry listeners (Proxy, Kestrel, Http, NameResolution, NetSecurity and Sockets).
        /// </summary>
#else
        /// <summary>
        /// Registers all telemetry listeners (Proxy and Kestrel).
        /// </summary>
#endif
        public static IServiceCollection AddTelemetryListeners(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddHostedService(provider => ProxyEventListenerService.Create<ProxyEventListenerService>(provider));
            services.AddHostedService(provider => KestrelEventListenerService.Create<KestrelEventListenerService>(provider));
#if NET5_0
            services.AddHostedService(provider => HttpEventListenerService.Create<HttpEventListenerService>(provider));
            services.AddHostedService(provider => NameResolutionEventListenerService.Create<NameResolutionEventListenerService>(provider));
            services.AddHostedService(provider => NetSecurityEventListenerService.Create<NetSecurityEventListenerService>(provider));
            services.AddHostedService(provider => SocketsEventListenerService.Create<SocketsEventListenerService>(provider));
#endif
            return services;
        }

        public static IServiceCollection AddTelemetryConsumer(this IServiceCollection services, object consumer)
        {
            if (consumer is IProxyTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IProxyTelemetryConsumer), consumer));
            }

            if (consumer is IKestrelTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IKestrelTelemetryConsumer), consumer));
            }

#if NET5_0
            if (consumer is IHttpTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IHttpTelemetryConsumer), consumer));
            }

            if (consumer is INameResolutionTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(INameResolutionTelemetryConsumer), consumer));
            }

            if (consumer is INetSecurityTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(INetSecurityTelemetryConsumer), consumer));
            }

            if (consumer is ISocketsTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(ISocketsTelemetryConsumer), consumer));
            }
#endif

            return services;
        }
    }
}
