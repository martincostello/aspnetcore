// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace Microsoft.AspNetCore.TestHost;

/// <summary>
/// The client's view of the response body.
/// </summary>
internal sealed class ResponseBodyReaderStream : Stream
{
    private bool _readerComplete;
    private bool _aborted;
    private Exception? _abortException;
    private int _disposed;

    private readonly object _abortLock = new object();
    private readonly Action _abortRequest;
    private readonly Action _readComplete;
    private readonly Action? _pipeReaderComplete;
    private readonly Pipe _pipe;

    internal ResponseBodyReaderStream(Pipe pipe, Action abortRequest, Action readComplete)
        : this(pipe, abortRequest, readComplete, pipeReaderComplete: null)
    {
    }

    internal ResponseBodyReaderStream(Pipe pipe, Action abortRequest, Action readComplete, Action? pipeReaderComplete)
    {
        _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
        _abortRequest = abortRequest ?? throw new ArgumentNullException(nameof(abortRequest));
        _readComplete = readComplete;
        _pipeReaderComplete = pipeReaderComplete;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    #region NotSupported

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Flush()
    {
        // No-op
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Write with count 0 will still trigger OnFirstWrite
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

    #endregion NotSupported

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        CheckAborted();

        if (_readerComplete)
        {
            return 0;
        }

        using var registration = cancellationToken.Register(Cancel);
        var result = await _pipe.Reader.ReadAsync(cancellationToken);

        if (result.IsCanceled)
        {
            // Advance (examine nothing, consume nothing) so the Pipe's internal
            // read-operation-state doesn't remain permanently marked as in-progress; Reset()
            // does not clear that state, so leaving it dirty here corrupts a pooled Pipe for
            // whichever future request reuses it. This can race against a concurrent Dispose()
            // completing the reader out from under this pending read (e.g. the client disposing
            // the response stream while a read is in flight) - if so, AdvanceTo throws
            // InvalidOperationException, which is safe to ignore: the reader is already
            // completed, so there's no read-operation-state left to clean up.
            try
            {
                _pipe.Reader.AdvanceTo(result.Buffer.Start);
            }
            catch (InvalidOperationException)
            {
            }
            throw new OperationCanceledException();
        }

        if (result.Buffer.IsEmpty && result.IsCompleted)
        {
            // Always advance, even for an empty/EOF read. Skipping this leaves the Pipe's
            // internal read-operation-state flagged as still-in-progress ("tentative"), which
            // Pipe.Reset() does not clear, permanently corrupting a pooled Pipe instance for
            // whichever future request reuses it. See the comment above for why this is
            // guarded against a reader that may already have been completed concurrently.
            try
            {
                _pipe.Reader.AdvanceTo(result.Buffer.End);
            }
            catch (InvalidOperationException)
            {
            }
            _readComplete();
            _readerComplete = true;
            return 0;
        }

        var readableBuffer = result.Buffer;
        var actual = Math.Min(readableBuffer.Length, buffer.Length);
        readableBuffer = readableBuffer.Slice(0, actual);
        readableBuffer.CopyTo(buffer.Span);
        _pipe.Reader.AdvanceTo(readableBuffer.End);
        return (int)actual;
    }

    internal void Cancel()
    {
        Abort(new OperationCanceledException());
    }

    internal void Abort(Exception innerException)
    {
        Debug.Assert(innerException != null);

        lock (_abortLock)
        {
            _abortException = innerException;
            _aborted = true;
        }

        _pipe.Reader.CancelPendingRead();
    }

    private void CheckAborted()
    {
        lock (_abortLock)
        {
            if (_aborted)
            {
                throw new IOException(string.Empty, _abortException);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Dispose() can be called more than once for the same stream instance (this is a
        // documented, supported pattern for IDisposable). Once the underlying Pipe has been
        // returned to a pool it may already be rented out and in active use by a subsequent,
        // unrelated request, so guard against re-entering the completion logic (and touching
        // the Pipe again) on any call after the first.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            _abortRequest();
        }

        _pipe.Reader.Complete();
        _pipeReaderComplete?.Invoke();

        base.Dispose(disposing);
    }
}
