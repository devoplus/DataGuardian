using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Devoplus.DataGuardian.Internal;

/// <summary>
/// A write-only response wrapper that buffers the body in memory only when it is worth analyzing.
/// </summary>
/// <remarks>
/// On the first write it asks <c>shouldBuffer</c> (typically "is this an analyzable content type?").
/// <list type="bullet">
/// <item>If not, it streams straight to the original body (no buffering) — this keeps large downloads
/// and <c>text/event-stream</c> responses working and bounds memory.</item>
/// <item>If yes, it buffers up to <c>cap</c> bytes for the middleware to inspect. If the body grows past
/// the cap, the buffered bytes are flushed to the original stream and it switches to passthrough.</item>
/// </list>
/// When <see cref="DidPassthrough"/> is true the body has already been sent and must not be rewritten.
/// </remarks>
internal sealed class ResponseCaptureStream : Stream
{
    private readonly Stream _inner;
    private readonly long _cap;
    private readonly Func<bool> _shouldBuffer;
    private MemoryStream? _buffer = new();
    private bool _decided;
    private bool _buffering;

    public ResponseCaptureStream(Stream inner, long cap, Func<bool> shouldBuffer)
    {
        _inner = inner;
        _cap = cap > 0 ? cap : 0;
        _shouldBuffer = shouldBuffer;
    }

    /// <summary>True once any bytes have been written directly to the original stream.</summary>
    public bool DidPassthrough { get; private set; }

    /// <summary>True if buffering started but the body exceeded the cap.</summary>
    public bool Overflowed { get; private set; }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public byte[] GetBufferedBytes() => _buffer?.ToArray() ?? Array.Empty<byte>();

    private void EnsureDecision()
    {
        if (_decided) return;
        _decided = true;
        _buffering = _shouldBuffer();
        if (!_buffering) DidPassthrough = true;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureDecision();
        if (!_buffering)
        {
            _inner.Write(buffer, offset, count);
            return;
        }
        if (_buffer!.Length + count > _cap)
        {
            SpillToInner();
            _inner.Write(buffer, offset, count);
            return;
        }
        _buffer!.Write(buffer, offset, count);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => await WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureDecision();
        if (!_buffering)
        {
            await _inner.WriteAsync(buffer, cancellationToken);
            return;
        }
        if (_buffer!.Length + buffer.Length > _cap)
        {
            await SpillToInnerAsync(cancellationToken);
            await _inner.WriteAsync(buffer, cancellationToken);
            return;
        }
        _buffer!.Write(buffer.Span);
    }

    private void SpillToInner()
    {
        Overflowed = true;
        _buffering = false;
        DidPassthrough = true;
        _buffer!.Position = 0;
        _buffer.CopyTo(_inner);
        _buffer.Dispose();
        _buffer = null;
    }

    private async Task SpillToInnerAsync(CancellationToken ct)
    {
        Overflowed = true;
        _buffering = false;
        DidPassthrough = true;
        _buffer!.Position = 0;
        await _buffer.CopyToAsync(_inner, ct);
        _buffer.Dispose();
        _buffer = null;
    }

    public override void Flush()
    {
        if (_decided && !_buffering) _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
        => (_decided && !_buffering) ? _inner.FlushAsync(cancellationToken) : Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _buffer?.Dispose();
        base.Dispose(disposing);
    }
}
