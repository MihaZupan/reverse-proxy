// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Forwarder.Tests
{
    public class StreamCopyHttpContentTests
    {
        private static StreamCopyHttpContent CreateContent(Stream source = null, bool autoFlushHttpClientOutgoingStream = false, IClock clock = null, CancellationToken contentCancellation = default, CancellationToken requestCancellation = default)
        {
            source ??= new MemoryStream();
            clock ??= new Clock();

            if (!contentCancellation.CanBeCanceled)
            {
                contentCancellation = new CancellationTokenSource().Token;
            }
            if (!requestCancellation.CanBeCanceled)
            {
                requestCancellation = new CancellationTokenSource().Token;
            }

            return new StreamCopyHttpContent(source, autoFlushHttpClientOutgoingStream, clock, contentCancellation, requestCancellation);
        }

        [Fact]
        public async Task CopyToAsync_InvokesStreamCopier()
        {
            const int SourceSize = (128 * 1024) - 3;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            var sut = CreateContent(source);

            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            await sut.CopyToWithCancellationAsync(destination);

            Assert.True(sut.Started);
            Assert.True(sut.ConsumptionTask.IsCompleted);
            Assert.Equal(sourceBytes, destination.ToArray());
        }

        [Theory]
        [InlineData(false)] // we expect to always flush at least once to trigger sending request headers
        [InlineData(true)]
        public async Task CopyToAsync_AutoFlushing(bool autoFlush)
        {
            // Must be same as StreamCopier constant.
            const int DefaultBufferSize = 65536;
            const int SourceSize = (128 * 1024) - 3;

            var expectedFlushes = 0;
            if (autoFlush)
            {
                // How many buffers is needed to send the source rounded up.
                expectedFlushes = (SourceSize - 1) / DefaultBufferSize + 1;
            }
            // Explicit flush after headers are sent.
            expectedFlushes++;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();
            var flushCountingDestination = new FlushCountingStream(destination);

            var sut = CreateContent(source, autoFlushHttpClientOutgoingStream: autoFlush);

            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            await sut.CopyToWithCancellationAsync(flushCountingDestination);

            Assert.True(sut.Started);
            Assert.True(sut.ConsumptionTask.IsCompleted);
            Assert.Equal(sourceBytes, destination.ToArray());
            Assert.Equal(expectedFlushes, flushCountingDestination.NumFlushes);
        }

        [Fact]
        public async Task CopyToAsync_AsyncSequencing()
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = new Mock<Stream>();
            source.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>())).Returns(() => new ValueTask<int>(tcs.Task));
            var destination = new MemoryStream();

            var sut = CreateContent(source.Object);

            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            var task = sut.CopyToWithCancellationAsync(destination);

            Assert.True(sut.Started); // This should happen synchronously
            Assert.False(sut.ConsumptionTask.IsCompleted); // This cannot happen until the tcs releases it

            tcs.TrySetResult(0);
            await task;
            Assert.True(sut.ConsumptionTask.IsCompleted);
        }

        [Fact]
        public Task ReadAsStreamAsync_Throws()
        {
            var sut = CreateContent();

            Func<Task> func = () => sut.ReadAsStreamAsync();

            return Assert.ThrowsAsync<NotImplementedException>(func);
        }

        [Fact]
        public void AllowDuplex_ReturnsTrue()
        {
            var sut = CreateContent();

            // This is an internal property that HttpClient and friends use internally and which must be true
            // to support duplex channels.This test helps detect regressions or changes in undocumented behavior
            // in .NET Core, and it passes as of .NET Core 3.1.
            var allowDuplexProperty = typeof(HttpContent).GetProperty("AllowDuplex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(allowDuplexProperty);
            var allowDuplex = (bool)allowDuplexProperty.GetValue(sut);
            Assert.True(allowDuplex);
        }

        [Fact]
        public async Task SerializeToStreamAsync_RespectsContentCancellation()
        {
            var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);

            var source = new ReadDelegatingStream(new MemoryStream(), async (buffer, cancellation) =>
            {
                Assert.False(cancellation.IsCancellationRequested);
                await tcs.Task;
                Assert.True(cancellation.IsCancellationRequested);
                return 0;
            });

            var contentCts = new CancellationTokenSource();

            var sut = CreateContent(source, contentCancellation: contentCts.Token);

            var copyToTask = sut.CopyToWithCancellationAsync(new MemoryStream());
            contentCts.Cancel();
            tcs.SetResult(0);

            await copyToTask;
        }

        [Fact]
        public async Task SerializeToStreamAsync_CanBeCanceledExternally()
        {
            var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);

            var source = new ReadDelegatingStream(new MemoryStream(), async (buffer, cancellation) =>
            {
                Assert.False(cancellation.IsCancellationRequested);
                await tcs.Task;
                Assert.True(cancellation.IsCancellationRequested);
                return 0;
            });

            var sut = CreateContent(source);

            // NET 5.0+ uses the CancellationToken overload of SerializeToStreamAsync.
            // On 3.1, we workaround this by exposing a Cancel method on the content.
#if NET
            var cts = new CancellationTokenSource();
            var copyToTask = sut.CopyToAsync(new MemoryStream(), cts.Token);
            cts.Cancel();
#else
            var copyToTask = sut.CopyToAsync(new MemoryStream());
            sut.Cancel();
#endif

            tcs.SetResult(0);

            await copyToTask;
        }

#if NET
        [Fact]
        public async Task SerializeToStreamAsync_IgnoresContentCancellationForHttp11()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var source = new ReadDelegatingStream(new MemoryStream(), async (buffer, cancellation) =>
            {
                Assert.False(cancellation.IsCancellationRequested);
                await tcs.Task;
                // Note the cancellation is not canceled after contentCts has been canceled
                Assert.False(cancellation.IsCancellationRequested);
                return 0;
            });

            // HttpForwarder will always pass (HttpContext.RequestAborted + Request Timeout) for request cancellation
            // and HttpContext.RequestAborted for content cancellation.
            // On HTTP/1.1, the request cancellation will be passed down to SerializeToStreamAsync as-is.
            // As an optimization, StreamCopyHttpContent will ignore contentCancellation in this case.

            var contentCts = new CancellationTokenSource();
            var requestCts = new CancellationTokenSource();

            var sut = CreateContent(source, contentCancellation: contentCts.Token, requestCancellation: requestCts.Token);

            var copyToTask = sut.CopyToAsync(new MemoryStream(), requestCts.Token);
            contentCts.Cancel();
            tcs.SetResult();

            await copyToTask;
        }
#endif

        private class FlushCountingStream : DelegatingStream
        {
            public FlushCountingStream(Stream stream)
                : base(stream)
            { }

            public int NumFlushes { get; private set; }

            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                await base.FlushAsync(cancellationToken);
                NumFlushes++;
            }
        }

        private sealed class ReadDelegatingStream : DelegatingStream
        {
            private readonly Func<Memory<byte>, CancellationToken, ValueTask<int>> _readAsync;

            public ReadDelegatingStream(Stream stream, Func<Memory<byte>, CancellationToken, ValueTask<int>> readAsync)
                : base(stream)
            {
                _readAsync = readAsync;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _readAsync(buffer, cancellationToken);
            }
        }
    }
}
