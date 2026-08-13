using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DeltaZulu.Platform.Data.Proton;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeltaZulu.Platform.Tests.Analytics.Proton;

[TestClass]
public sealed class ProtonHttpExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_Success_DisposesResponse()
    {
        var content = new TrackingContent("ok");
        using var executor = CreateExecutor(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        await executor.ExecuteAsync("CREATE STREAM x");

        Assert.IsTrue(content.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonSuccess_ReadsTruncatesAndDisposesResponse()
    {
        var body = new string('x', 1005);
        var content = new TrackingContent(body);
        using var executor = CreateExecutor(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => executor.ExecuteAsync("BAD SQL"));

        Assert.Contains("Proton SQL execution failed (400):", ex.Message);
        Assert.Contains(new string('x', 1000) + "…(truncated)", ex.Message);
        Assert.IsTrue(content.WasSerialized);
        Assert.IsTrue(content.Disposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransportException_IsRethrown()
    {
        var expected = new HttpRequestException("connection refused");
        var logger = new RecordingLogger<ProtonHttpExecutor>();
        using var executor = CreateExecutor(new StubHandler(_ => throw expected), logger: logger);

        var actual = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => executor.ExecuteAsync("SELECT 1"));

        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, logger.ErrorCount);
        Assert.AreSame(expected, logger.LastException);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancellation_IsNotWrapped()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var executor = CreateExecutor(new StubHandler(_ => throw new OperationCanceledException(cts.Token)));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync("SELECT 1", cts.Token));
    }

    [TestMethod]
    public async Task ExecuteAsync_AppliesBasicAuthHeaderWhenUsernameConfigured()
    {
        AuthenticationHeaderValue? auth = null;
        using var executor = CreateExecutor(
            new StubHandler(request => {
                auth = request.Headers.Authorization;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            username: "user",
            password: "secret");

        await executor.ExecuteAsync("SELECT 1");

        Assert.IsNotNull(auth);
        Assert.AreEqual("Basic", auth.Scheme);
        Assert.AreEqual(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret")),
            auth.Parameter);
    }

    [TestMethod]
    public async Task ExecuteAsync_AppliesTimeoutOption()
    {
        using var executor = CreateExecutor(
            new StubHandler(async (_, ct) => {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            timeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => executor.ExecuteAsync("SELECT 1"));
    }

    private static ProtonHttpExecutor CreateExecutor(
        HttpMessageHandler handler,
        string? username = null,
        string? password = null,
        TimeSpan? timeout = null,
        ILogger<ProtonHttpExecutor>? logger = null)
    {
        var options = new ProtonHttpClientOptions {
            BaseUrl = "http://proton.example",
            Username = username,
            Password = password,
            ExecutionTimeout = timeout ?? TimeSpan.FromSeconds(30)
        };

        return new ProtonHttpExecutor(options, logger ?? NullLogger<ProtonHttpExecutor>.Instance, handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        {
            _sendAsync = (request, _) => Task.FromResult(send(request));
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _sendAsync(request, cancellationToken);
    }

    private sealed class TrackingContent : HttpContent
    {
        private readonly byte[] _bytes;

        public TrackingContent(string body)
        {
            _bytes = Encoding.UTF8.GetBytes(body);
        }

        public bool WasSerialized { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasSerialized = true;
            return stream.WriteAsync(_bytes, 0, _bytes.Length);
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            WasSerialized = true;
            return stream.WriteAsync(_bytes, cancellationToken).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _bytes.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public int ErrorCount { get; private set; }
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Error)
            {
                return;
            }

            ErrorCount++;
            LastException = exception;
        }
    }
}
