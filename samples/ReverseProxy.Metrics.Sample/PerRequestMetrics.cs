using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Sample
{
    public sealed class MyCustomHandler : DelegatingHandler
    {
        public MyCustomHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            PerRequestMetrics.Current.MessageHandler = this;

            return base.SendAsync(request, cancellationToken);
        }
    }

    public class PerRequestMetrics
    {
        private static readonly AsyncLocal<PerRequestMetrics> _local = new AsyncLocal<PerRequestMetrics>();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        // Ensure we are only fetched via the factory
        private PerRequestMetrics() { }

        /// <summary>
        /// Factory to instantiate or restore the metrics from AsyncLocal storage
        /// </summary>
        public static PerRequestMetrics Current => _local.Value ??= new PerRequestMetrics();

        // Time the request was started via the pipeline
        public DateTime StartTime { get; set; }

        /// <summary>
        /// <see cref="HttpMessageHandler"/> that processed the request to the backend.
        /// </summary>
        [JsonIgnore]
        public MyCustomHandler MessageHandler { get; set; }

        // Offset Tics for each part of the proxy operation
        public float RouteInvokeOffset { get; set; }
        public float ProxyStartOffset { get; set; }
        public float HttpRequestStartOffset { get; set; }
        public float HttpConnectionEstablishedOffset { get; set; }
        public float HttpRequestLeftQueueOffset { get; set; }

        public float HttpRequestHeadersStartOffset { get; set; }
        public float HttpRequestHeadersStopOffset { get; set; }
        public float HttpRequestContentStartOffset { get; set; }
        public float HttpRequestContentStopOffset { get; set; }

        public float HttpResponseHeadersStartOffset { get; set; }
        public float HttpResponseHeadersStopOffset { get; set; }
        public float HttpResponseContentStopOffset { get; set; }

        public float HttpRequestStopOffset { get; set; }
        public float ProxyStopOffset { get; set; }

        //Info about the request
        public ForwarderError Error { get; set; }
        public long RequestBodyLength { get; set; }
        public long ResponseBodyLength { get; set; }
        public long RequestContentIops { get; set; }
        public long ResponseContentIops { get; set; }
        public string DestinationId { get; set; }
        public string ClusterId { get; set; }
        public string RouteId { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, _jsonOptions);
        }

        public float CalcOffset(DateTime timestamp)
        {
            return (float)(timestamp - StartTime).TotalMilliseconds;
        }
    }
}

