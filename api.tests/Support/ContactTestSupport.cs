using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Api.Contact;
using Portfolio.Api.Options;

namespace Portfolio.Api.Tests;

internal static class ContactTestSupport
{
    public const string Key = "0123456789abcdef";

    public static ContactPipeline Pipeline(
        string? key,
        TimeProvider time,
        IContactSubmitter submitter,
        ILogger<ContactPipeline>? logger = null
    )
    {
        var tokens = new FormTokenService(
            Microsoft.Extensions.Options.Options.Create(new ContactApiOptions { FormTokenKey = key }),
            time
        );
        return new ContactPipeline(tokens, submitter, logger ?? NullLogger<ContactPipeline>.Instance);
    }

    public static DefaultHttpContext Json(string json, string? contentType = "application/json")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return Bytes(bytes, contentType, bytes.Length);
    }

    public static DefaultHttpContext Bytes(byte[] bytes, string? contentType, long? contentLength)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.ContentLength = contentLength;
        context.Request.Body = new MemoryStream(bytes);
        return context;
    }

    public static string ValidJson(string token) =>
        "{\"name\":\"  Seth  \",\"email\":\" seth@example.com \",\"message\":\"  Hello from the form.  \","
        + "\"website\":\"\",\"formToken\":\""
        + token
        + "\",\"turnstileToken\":\"  ts-1  \",\"unused\":\"ignored\"}";
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class RecordingSubmitter : IContactSubmitter
{
    public int Calls { get; private set; }

    public ValidatedContact? Received { get; private set; }

    public Task<ContactSubmitResult> SubmitAsync(
        ValidatedContact contact,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        Received = contact;
        return Task.FromResult(ContactSubmitResult.NotImplemented());
    }
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose() { }
    }
}

internal sealed class ThrowOnReadStream : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException("body was read");

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("body was read");
}
