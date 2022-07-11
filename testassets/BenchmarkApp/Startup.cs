// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Crank.EventSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;

namespace BenchmarkApp;

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

        if (string.IsNullOrWhiteSpace(clusterUrls))
        {
            throw new ArgumentException("--clusterUrls is required");
        }

        if (!int.TryParse(_configuration["handlerCount"], out var handlerCount))
        {
            handlerCount = 1;
        }

        Console.WriteLine($"Using {handlerCount} handlers");
        services.AddSingleton<IForwarderHttpClientFactory>(new MultipleHandlerHttpClientFactory(handlerCount));

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

        services.AddReverseProxy().LoadFromConfig(proxyConfig);
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
    }
}
