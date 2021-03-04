// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends simple request header values.
    /// </summary>
    public class RequestHeaderValueTransform : RequestTransform
    {
        public RequestHeaderValueTransform(string headerName, string value, bool append)
        {
            HeaderName = headerName ?? throw new ArgumentNullException(nameof(headerName));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Append = append;
        }

        internal string HeaderName { get; }

        internal string Value { get; }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Append)
            {
                var existingValues = TakeHeader(context, HeaderName);
                var values = StringValues.Concat(existingValues, Value);
                AddHeader(context, HeaderName, values);
            }
            else
            {
                RemoveHeader(context, HeaderName);

                if (!string.IsNullOrEmpty(Value))
                {
                    AddHeader(context, HeaderName, Value);
                }
            }

            return default;
        }
    }
}
