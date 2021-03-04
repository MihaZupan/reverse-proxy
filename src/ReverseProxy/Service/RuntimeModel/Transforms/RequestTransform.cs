// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// The base class for request transforms.
    /// </summary>
    public abstract class RequestTransform
    {
        /// <summary>
        /// Transforms any of the available fields before building the outgoing request.
        /// </summary>
        public abstract ValueTask ApplyAsync(RequestTransformContext context);

        /// <summary>
        /// Removes and returns the current header value by first checking the HttpRequestMessage,
        /// then the HttpContent, and falling back to the HttpContext only if
        /// <see cref="RequestTransformContext.HeadersCopied"/> is not set.
        /// This ordering allows multiple transforms to mutate the same header.
        /// </summary>
        /// <param name="headerName">The name of the header to take.</param>
        /// <returns>The requested header value, or StringValues.Empty if none.</returns>
        public static StringValues TakeHeader(RequestTransformContext context, string headerName)
        {
            var existingValues = StringValues.Empty;
            if (context.ProxyRequest.Headers.TryGetValues(headerName, out var values))
            {
                context.ProxyRequest.Headers.Remove(headerName);
                existingValues = (string[])values;
            }
            else if (context.ProxyRequest.Content?.Headers.TryGetValues(headerName, out values) ?? false)
            {
                context.ProxyRequest.Content.Headers.Remove(headerName);
                existingValues = (string[])values;
            }
            else if (!context.HeadersCopied)
            {
                existingValues = context.HttpContext.Request.Headers[headerName];
            }

            return existingValues;
        }

        internal static void RemoveHeader(RequestTransformContext context, string headerName)
        {
            if (!TryRemove(context.ProxyRequest.Headers, headerName))
            {
                var content = context.ProxyRequest.Content;
                if (content is not null)
                {
                    TryRemove(content.Headers, headerName);
                }
            }

            static bool TryRemove(HttpHeaders headers, string headerName)
            {
                var rawHeaders = HttpTransformer.UnsafeGetRawHeaders(headers);
                if (rawHeaders is not null)
                {
                    foreach (var entry in rawHeaders)
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(entry.Key.Name, headerName))
                        {
                            var removed = rawHeaders.Remove(entry.Key);
                            Debug.Assert(removed);
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Adds the given header to the HttpRequestMessage or HttpContent where applicable.
        /// </summary>
        public static void AddHeader(RequestTransformContext context, string headerName, StringValues values)
        {
            RequestUtilities.AddHeader(context.ProxyRequest, headerName, values);
        }
    }
}
