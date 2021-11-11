// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Crank.EventSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Utilities;

namespace BenchmarkApp
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var clusterUrls = _configuration["clusterUrls"];

            clusterUrls = "http://localhost:10000";

            if (string.IsNullOrWhiteSpace(clusterUrls))
            {
                throw new ArgumentException("--clusterUrls is required");
            }

            var configDictionary = new Dictionary<string, string>
            {
                { "Routes:route:ClusterId", "cluster" },
                { "Routes:route:Match:Path", "/{**catchall}" },
                { "Clusters:cluster:HttpClient:DangerousAcceptAnyServerCertificate", "true" },
            };

            var clusterCount = 0;
            foreach (var clusterUrl in clusterUrls.Split(';'))
            {
                configDictionary.Add($"Clusters:cluster:Destinations:destination{clusterCount++}:Address", clusterUrl);
            }

            var proxyConfig = new ConfigurationBuilder().AddInMemoryCollection(configDictionary).Build();

            services.AddReverseProxy()
                .LoadFromConfig(proxyConfig)
                //.ConfigureHttpClient((context, handler) =>
                //{
                //    handler.ConnectCallback = async (context, cancellation) =>
                //    {
                //        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                //        try
                //        {
                //            await socket.ConnectAsync(context.DnsEndPoint, cancellation);
                //            return new InterceptStream(new NetworkStream(socket, ownsSocket: true));
                //        }
                //        catch
                //        {
                //            socket.Dispose();
                //            throw;
                //        }
                //    };
                //})
                ;
        }

        public void Configure(IApplicationBuilder app)
        {
            BenchmarksEventSource.MeasureAspNetVersion();
            BenchmarksEventSource.MeasureNetCoreAppVersion();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy(builder =>
                {
                    // Skip SessionAffinity, LoadBalancing and PassiveHealthChecks
                });
            });

            //Task.Run(async () =>
            //{
            //    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            //    while (await timer.WaitForNextTickAsync())
            //    {
            //        var privateKB = Process.GetCurrentProcess().PrivateMemorySize64 / 1024;
            //        var managedKB = GC.GetTotalMemory(false) / 1024;
            //        Console.WriteLine($"{privateKB} KB private, {managedKB} KB managed");
            //    }
            //});

            //Task.Run(() =>
            //{
            //    while (true)
            //    {
            //        Console.ReadLine();

            //        GC.Collect();
            //        GC.WaitForPendingFinalizers();
            //        GC.Collect();
            //    }
            //});
        }
    }

    public sealed class InterceptStream : DelegatingStream
    {
        public InterceptStream(Stream innerStream) : base(innerStream) { }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Read start with {buffer.Length} byte buffer");
            var read = await base.ReadAsync(buffer, cancellationToken);
            Console.WriteLine($"Read stop with {read} bytes read");
            return read;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            //Console.WriteLine($"Write start with {buffer.Length} byte buffer");
            await base.WriteAsync(buffer, cancellationToken);
            //Console.WriteLine($"Write stop");
        }
    }
}
