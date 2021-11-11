// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder
{
    /// <summary>
    /// A stream copier that captures errors.
    /// </summary>
    internal static class StreamCopier
    {
        // Based on performance investigations, see https://github.com/microsoft/reverse-proxy/pull/330#issuecomment-758851852.
        private const int DefaultBufferSize = 65536;

        public static ValueTask<(StreamCopyResult, Exception?)> CopyAsync(bool isRequest, Stream input, Stream output, IClock clock, ActivityCancellationTokenSource activityToken, CancellationToken cancellation)
        {
            Debug.Assert(input is not null);
            Debug.Assert(output is not null);
            Debug.Assert(clock is not null);
            Debug.Assert(activityToken is not null);

            var telemetry = ForwarderTelemetry.Log.IsEnabled(EventLevel.Informational, EventKeywords.All)
                ? new StreamCopierTelemetry(isRequest, clock)
                : null;

            return CopyAsync(input, output, telemetry, activityToken, cancellation);
        }

        private static async ValueTask<(StreamCopyResult, Exception?)> CopyAsync(Stream input, Stream output, StreamCopierTelemetry? telemetry, ActivityCancellationTokenSource activityToken, CancellationToken cancellation)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            var read = 0;
            try
            {
                while (true)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return (StreamCopyResult.Canceled, new OperationCanceledException(cancellation));
                    }

                    try
                    {
#if NET6_0_OR_GREATER
                        if (buffer is null)
                        {
                            await input.ReadAsync(Memory<byte>.Empty, cancellation);

                            buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                        }
#endif

                        read = await input.ReadAsync(buffer.AsMemory(), cancellation);

                        // End of the source stream.
                        if (read == 0)
                        {
                            return (StreamCopyResult.Success, null);
                        }

                        // Success, reset the activity monitor.
                        activityToken.ResetTimeout();
                    }
                    finally
                    {
                        telemetry?.AfterRead(read);
                    }

                    try
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellation);

#if NET6_0_OR_GREATER
                        // If the whole read buffer was filled, there is a very high chance the next read will complete synchronously
                        // Assume there is more data available and don't return the buffer yet
                        if (read != buffer.Length)
                        {
                            var bufferToReturn = buffer;
                            buffer = null;
                            ArrayPool<byte>.Shared.Return(bufferToReturn);
                        }
#endif

                        read = 0;

                        // Success, reset the activity monitor.
                        activityToken.ResetTimeout();
                    }
                    finally
                    {
                        telemetry?.AfterWrite();
                    }
                }
            }
            catch (OperationCanceledException oex)
            {
                return (StreamCopyResult.Canceled, oex);
            }
            catch (Exception ex)
            {
                return (read == 0 ? StreamCopyResult.InputError : StreamCopyResult.OutputError, ex);
            }
            finally
            {
                if (buffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                telemetry?.Stop();
            }
        }

        private sealed class StreamCopierTelemetry
        {
            private static readonly TimeSpan _timeBetweenTransferringEvents = TimeSpan.FromSeconds(1);

            private readonly bool _isRequest;
            private readonly IClock _clock;
            private long _contentLength;
            private long _iops;
            private TimeSpan _readTime;
            private TimeSpan _writeTime;
            private TimeSpan _firstReadTime;
            private TimeSpan _lastTime;
            private TimeSpan _nextTransferringEvent;

            public StreamCopierTelemetry(bool isRequest, IClock clock)
            {
                _isRequest = isRequest;
                _clock = clock ?? throw new ArgumentNullException(nameof(clock));
                _firstReadTime = new TimeSpan(-1);

                ForwarderTelemetry.Log.ForwarderStage(isRequest ? ForwarderStage.RequestContentTransferStart : ForwarderStage.ResponseContentTransferStart);

                _lastTime = clock.GetStopwatchTime();
                _nextTransferringEvent = _lastTime + _timeBetweenTransferringEvents;
            }

            public void AfterRead(int read)
            {
                _contentLength += read;
                _iops++;

                var readStop = _clock.GetStopwatchTime();
                var currentReadTime = readStop - _lastTime;
                _lastTime = readStop;
                _readTime += currentReadTime;
                if (_firstReadTime.Ticks < 0)
                {
                    _firstReadTime = currentReadTime;
                }
            }

            public void AfterWrite()
            {
                var writeStop = _clock.GetStopwatchTime();
                _writeTime += writeStop - _lastTime;
                _lastTime = writeStop;

                if (writeStop >= _nextTransferringEvent)
                {
                    ForwarderTelemetry.Log.ContentTransferring(
                        _isRequest,
                        _contentLength,
                        _iops,
                        _readTime.Ticks,
                        _writeTime.Ticks);

                    // Avoid attributing the time taken by logging ContentTransferring to the next read call
                    _lastTime = _clock.GetStopwatchTime();
                    _nextTransferringEvent = _lastTime + _timeBetweenTransferringEvents;
                }
            }

            public void Stop()
            {
                ForwarderTelemetry.Log.ContentTransferred(
                    _isRequest,
                    _contentLength,
                    _iops,
                    _readTime.Ticks,
                    _writeTime.Ticks,
                    Math.Max(0, _firstReadTime.Ticks));
            }
        }
    }
}
