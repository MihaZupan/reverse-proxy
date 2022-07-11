// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace BenchmarkApp;

public sealed class MultipleHandlerHttpClientFactory : IForwarderHttpClientFactory
{
    private readonly ForwarderHttpClientFactory[] _clientFactories;

    public MultipleHandlerHttpClientFactory(int handlerCount)
    {
        _clientFactories = Enumerable.Repeat(0, handlerCount).Select(_ => new ForwarderHttpClientFactory()).ToArray();
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        return new HttpMessageInvoker(new MultipleInvokersHandler(_clientFactories.Select(f => f.CreateClient(context))));
    }

    private sealed class MultipleInvokersHandler : HttpMessageHandler
    {
        private readonly HttpMessageInvoker[] _invokers;
        private uint _index = 0;

        public MultipleInvokersHandler(IEnumerable<HttpMessageInvoker> invokers)
        {
            _invokers = invokers.ToArray();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _index);
            var invoker = _invokers[(int)(index % _invokers.Length)];
            return invoker.SendAsync(request, cancellationToken);
        }
    }
}
