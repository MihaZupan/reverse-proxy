using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .ConfigureHttpClient((ctx, handler) => handler.ConnectTimeout = TimeSpan.FromMilliseconds(50))
    .AddTransforms(ctx =>
    {
        ctx.AddResponseTransform(ctx =>
        {
            ctx.SuppressResponseBody = true;

            ctx.HttpContext.Response.WriteAsync("Error");

            return default;
        });
    });

var app = builder.Build();

app.MapReverseProxy();

app.Run();
