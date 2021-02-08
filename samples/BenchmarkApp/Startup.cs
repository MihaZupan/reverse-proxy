// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Crank.EventSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Telemetry.Consumption;

namespace BenchmarkApp
{
    public class Startup
    {
        public const bool RegisterTelemetryAsScoped = true;
        public const bool ProxyTelemetryOnly = false;
        public const bool MetricsOnly = true;

        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var clusterUrls = _configuration["clusterUrls"];

            if (string.IsNullOrWhiteSpace(clusterUrls))
            {
                throw new ArgumentException("--clusterUrls is required");
            }

            var configDictionary = new Dictionary<string, string>
            {
                { "Routes:0:RouteId", "route" },
                { "Routes:0:ClusterId", "cluster" },
                { "Routes:0:Match:Path", "/{**catchall}" },
                { "Clusters:cluster:HttpClient:DangerousAcceptAnyServerCertificate", "true" },
            };

            var clusterCount = 0;
            foreach (var clusterUrl in clusterUrls.Split(';'))
            {
                configDictionary.Add($"Clusters:cluster:Destinations:destination{clusterCount++}:Address", clusterUrl);
            }

            var proxyConfig = new ConfigurationBuilder().AddInMemoryCollection(configDictionary).Build();

            var consumer = new MetricsConsumer();
            services.AddSingleton<IProxyMetricsConsumer>(consumer);
#if !NETCOREAPP3_1
            services.AddSingleton<IKestrelMetricsConsumer>(consumer);
            services.AddSingleton<IHttpMetricsConsumer>(consumer);
            services.AddSingleton<ISocketsMetricsConsumer>(consumer);
            services.AddSingleton<INetSecurityMetricsConsumer>(consumer);
            services.AddSingleton<INameResolutionMetricsConsumer>(consumer);
#endif

            if (RegisterTelemetryAsScoped)
            {
                services.AddScoped<TelemetryConsumer>();
                services.AddScoped<IProxyTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                if (!ProxyTelemetryOnly)
                {
                    services.AddScoped<IKestrelTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
#if !NETCOREAPP3_1
                    services.AddScoped<IHttpTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<ISocketsTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<INetSecurityTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<INameResolutionTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
#endif
                }
            }
            else
            {
                var telemetry = new TelemetryConsumer();
                services.AddSingleton<IProxyTelemetryConsumer>(telemetry);
                if (!ProxyTelemetryOnly)
                {
                    services.AddSingleton<IKestrelTelemetryConsumer>(telemetry);
#if !NETCOREAPP3_1
                    services.AddSingleton<IHttpTelemetryConsumer>(telemetry);
                    services.AddSingleton<ISocketsTelemetryConsumer>(telemetry);
                    services.AddSingleton<INetSecurityTelemetryConsumer>(telemetry);
                    services.AddSingleton<INameResolutionTelemetryConsumer>(telemetry);
#endif
                }
            }

            if (ProxyTelemetryOnly)
            {
                services.AddProxyTelemetryListener();
            }
            else
            {
                services.AddTelemetryListeners();
            }

            services.AddReverseProxy().LoadFromConfig(proxyConfig);
        }

        public void Configure(IApplicationBuilder app)
        {
            BenchmarksEventSource.MeasureAspNetVersion();
            BenchmarksEventSource.MeasureNetCoreAppVersion();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
            });
        }
    }

    public sealed class MetricsConsumer :
            IProxyMetricsConsumer
#if !NETCOREAPP3_1
            ,
            IKestrelMetricsConsumer,
            IHttpMetricsConsumer,
            INameResolutionMetricsConsumer,
            INetSecurityMetricsConsumer,
            ISocketsMetricsConsumer
#endif
    {
        public void OnProxyMetrics(ProxyMetrics oldMetrics, ProxyMetrics newMetrics) { }
#if !NETCOREAPP3_1
        public void OnKestrelMetrics(KestrelMetrics oldMetrics, KestrelMetrics newMetrics) { }
        public void OnSocketsMetrics(SocketsMetrics oldMetrics, SocketsMetrics newMetrics) { }
        public void OnNetSecurityMetrics(NetSecurityMetrics oldMetrics, NetSecurityMetrics newMetrics) { }
        public void OnNameResolutionMetrics(NameResolutionMetrics oldMetrics, NameResolutionMetrics newMetrics) { }
        public void OnHttpMetrics(HttpMetrics oldMetrics, HttpMetrics newMetrics) { }
#endif
    }

    public sealed class TelemetryConsumer :
            IProxyTelemetryConsumer,
            IKestrelTelemetryConsumer
#if !NETCOREAPP3_1
            ,
            IHttpTelemetryConsumer,
            INameResolutionTelemetryConsumer,
            INetSecurityTelemetryConsumer,
            ISocketsTelemetryConsumer
#endif
    {
        public void OnProxyStart(DateTime timestamp, string destinationPrefix) { }
        public void OnProxyStop(DateTime timestamp, int statusCode) { }
        public void OnProxyFailed(DateTime timestamp, ProxyError error) { }
        public void OnProxyStage(DateTime timestamp, ProxyStage stage) { }
        public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) { }
        public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) { }
        public void OnProxyInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId) { }
#if !NETCOREAPP3_1
        public void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy) { }
        public void OnRequestStop(DateTime timestamp) { }
        public void OnRequestFailed(DateTime timestamp) { }
        public void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor) { }
        public void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor) { }
        public void OnRequestHeadersStart(DateTime timestamp) { }
        public void OnRequestHeadersStop(DateTime timestamp) { }
        public void OnRequestContentStart(DateTime timestamp) { }
        public void OnRequestContentStop(DateTime timestamp, long contentLength) { }
        public void OnResponseHeadersStart(DateTime timestamp) { }
        public void OnResponseHeadersStop(DateTime timestamp) { }
        public void OnResolutionStart(DateTime timestamp, string hostNameOrAddress) { }
        public void OnResolutionStop(DateTime timestamp) { }
        public void OnResolutionFailed(DateTime timestamp) { }
        public void OnHandshakeStart(DateTime timestamp, bool isServer, string targetHost) { }
        public void OnHandshakeStop(DateTime timestamp, SslProtocols protocol) { }
        public void OnHandshakeFailed(DateTime timestamp, bool isServer, TimeSpan elapsed, string exceptionMessage) { }
        public void OnConnectStart(DateTime timestamp, string address) { }
        public void OnConnectStop(DateTime timestamp) { }
        public void OnConnectFailed(DateTime timestamp, SocketError error, string exceptionMessage) { }
        public void OnRequestStart(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) { }
        public void OnRequestStop(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) { }
#else
        public void OnRequestStart(DateTime timestamp, string connectionId, string requestId) { }
        public void OnRequestStop(DateTime timestamp, string connectionId, string requestId) { }
#endif
    }
}
