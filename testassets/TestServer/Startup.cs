// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SampleServer
{
    /// <summary>
    /// ASP .NET Core pipeline initialization.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            // Disabling https redirection behind the proxy. Forwarders are not currently set up so we can't tell if the external connection used https.
            // Nor do we know the correct port to redirect to.
            // app.UseHttpsRedirection();

            app.UseWebSockets();

            app.Map("/websocket", app => app.Use(next => context => DoWebSocketsAsync(context)));
            app.Map("/get", app => app.Use(next => context => DoGetRequestAsync(context)));
        }

        private static readonly byte[] _helloWorld = Encoding.UTF8.GetBytes("Hello world!");
        private static readonly byte[] _receiveBuffer = new byte[8192];

        private static async Task DoGetRequestAsync(HttpContext context)
        {
            await context.Response.Body.WriteAsync(_helloWorld);
        }

        private static async Task DoWebSocketsAsync(HttpContext context)
        {
            try
            {
                using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
                {
                    await webSocket.SendAsync(_helloWorld, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

                    while (!(await webSocket.ReceiveAsync(_receiveBuffer, CancellationToken.None)).EndOfMessage) { }

                    await Task.Delay(TimeSpan.FromMinutes(10), context.RequestAborted);
                }
            }
            catch when (context.RequestAborted.IsCancellationRequested) { }
        }
    }
}
