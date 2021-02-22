// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace BenchmarkApp
{
    public class Program
    {
        public static void Main(string[] args) =>
            CreateWebHostBuilder(args).Build().Run();

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var hostBuilder = WebHost.CreateDefaultBuilder(args)
                .ConfigureKestrel((context, kestrelOptions) =>
                {
                    kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(context.HostingEnvironment.ContentRootPath, "testCert.pfx"), "testPassword");
                    });
                })
                .UseStartup<Startup>();

#if NET
            hostBuilder.UseSockets(options =>
            {
                options.WaitForDataBeforeAllocatingBuffer = false;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    options.UnsafePreferInlineScheduling = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS") == "1";
                    Console.WriteLine($"{nameof(options.UnsafePreferInlineScheduling)}: {options.UnsafePreferInlineScheduling}");
                }
            });
#endif

            return hostBuilder;
        }
    }
}
