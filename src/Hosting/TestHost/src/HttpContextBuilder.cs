// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.TestHost;

internal sealed class HttpContextBuilder : IHttpBodyControlFeature, IHttpResetFeature
{
    private readonly ApplicationWrapper _application;
    private readonly bool _preserveExecutionContext;
    private readonly HttpContext _httpContext;

    private readonly TaskCompletionSource<HttpContext> _responseTcs = new TaskCompletionSource<HttpContext>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ResponseBodyReaderStream _responseReaderStream;
    private readonly ResponseBodyPipeWriter _responsePipeWriter;
    private readonly ResponseFeature _responseFeature;
    private readonly RequestLifetimeFeature _requestLifetimeFeature;
    private readonly ResponseTrailersFeature _responseTrailersFeature = new ResponseTrailersFeature();
    private bool _pipelineFinished;
    private bool _returningResponse;
    private object? _testContext;
    private readonly Pipe _requestPipe;
    private readonly Pipe _responsePipe;
    private readonly ObjectPool<Pipe>? _pipePool;

    // Set once the response pipe's writer completes (see CompleteResponseAsync/Abort) and once the response
    // pipe's reader completes (see ResponsePipeReaderComplete). Only once both sides are done is it safe to
    // Reset() and return the response Pipe to the pool; Pipe.Reset() throws otherwise.
    private int _responseWriterSideDone;
    private int _responseReaderSideDone;

    // Coordinates exclusive access between two code paths that can race on the SAME underlying Pipe object once
    // pooling is enabled: (1) RunRequestAsync's normal completion path, which completes/returns _requestPipe to
    // the pool once the response has been sent, and (2) CancelRequestBody (invoked from ClientInitiatedAbort or
    // Abort), which may fire asynchronously and arbitrarily late - e.g. via the client disposing its response
    // stream, well after this request has already finished. Once a Pipe is returned to the pool it may
    // immediately be Reset() and rented out to a completely different, unrelated HttpContextBuilder; if
    // CancelRequestBody won a race and called CancelPendingRead()/CancelPendingFlush() on that reused Pipe after
    // the fact, it would corrupt the new owner's in-flight read/write state (observed as a spurious "Flush was
    // canceled" exception on someone else's Writer.CompleteAsync(), or a lost-wakeup deadlock on someone else's
    // ReadAsync()). A simple bool flag is NOT sufficient here: checking the flag and then acting on the Pipe are
    // two separate steps, so two threads can both observe "not yet claimed" before either one commits - a
    // genuine TOCTOU race, reproduced under sustained concurrent load. Using Interlocked.CompareExchange to
    // atomically claim exclusive ownership of the Pipe (RequestPipeUnclaimed -> RequestPipeClaimedForReturn or
    // RequestPipeClaimedForCancel) closes this window completely: only one of the two paths can ever touch the
    // Pipe once either has claimed it.
    private const int RequestPipeUnclaimed = 0;
    private const int RequestPipeClaimedForReturn = 1;
    private const int RequestPipeClaimedForCancel = 2;
    private int _requestPipeClaim;

    private Action<HttpContext>? _responseReadCompleteCallback;
    private Func<PipeWriter, Task>? _sendRequestStream;

    internal HttpContextBuilder(ApplicationWrapper application, bool allowSynchronousIO, bool preserveExecutionContext)
        : this(application, allowSynchronousIO, preserveExecutionContext, pipePool: null)
    {
    }

    internal HttpContextBuilder(ApplicationWrapper application, bool allowSynchronousIO, bool preserveExecutionContext, ObjectPool<Pipe>? pipePool)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        AllowSynchronousIO = allowSynchronousIO;
        _preserveExecutionContext = preserveExecutionContext;
        _pipePool = pipePool;
        _httpContext = new DefaultHttpContext();
        _responseFeature = new ResponseFeature(Abort);
        _requestLifetimeFeature = new RequestLifetimeFeature(Abort);

        var request = _httpContext.Request;
        request.Protocol = HttpProtocol.Http11;
        request.Method = HttpMethods.Get;

        _requestPipe = RentPipe();

        _responsePipe = RentPipe();
        _responseReaderStream = new ResponseBodyReaderStream(_responsePipe, ClientInitiatedAbort, ResponseBodyReadComplete, ResponsePipeReaderComplete);
        _responsePipeWriter = new ResponseBodyPipeWriter(_responsePipe, ReturnResponseMessageAsync);
        _responseFeature.Body = new ResponseBodyWriterStream(_responsePipeWriter, () => AllowSynchronousIO);
        _responseFeature.BodyWriter = _responsePipeWriter;

        _httpContext.Features.Set<IHttpBodyControlFeature>(this);
        _httpContext.Features.Set<IHttpResponseFeature>(_responseFeature);
        _httpContext.Features.Set<IHttpResponseBodyFeature>(_responseFeature);
        _httpContext.Features.Set<IHttpRequestLifetimeFeature>(_requestLifetimeFeature);
        _httpContext.Features.Set<IHttpResponseTrailersFeature>(_responseTrailersFeature);
        _httpContext.Features.Set<IHttpUpgradeFeature>(new UpgradeFeature());
    }

    public bool AllowSynchronousIO { get; set; }

    private Pipe RentPipe() => _pipePool?.Get() ?? new Pipe();

    // Only call this once a Pipe's reader and writer have both completed; Pipe.Reset() throws otherwise.
    private void TryReturnPipe(Pipe pipe) => _pipePool?.Return(pipe);

    // Invoked when the response body's reader (the client's view of the response, e.g. the
    // ResponseBodyReaderStream exposed via StreamContent) has been completed/disposed. Combined with the writer
    // completing in CompleteResponseAsync or Abort, this tells us both ends of the response Pipe are done and it
    // is safe to Reset() and pool it for reuse by a future request.
    private void ResponsePipeReaderComplete()
    {
        if (Interlocked.Exchange(ref _responseReaderSideDone, 1) == 0)
        {
            TryReturnResponsePipe();
        }
    }

    private void ResponsePipeWriterComplete()
    {
        if (Interlocked.Exchange(ref _responseWriterSideDone, 1) == 0)
        {
            TryReturnResponsePipe();
        }
    }

    private void TryReturnResponsePipe()
    {
        if (Volatile.Read(ref _responseWriterSideDone) == 1 && Volatile.Read(ref _responseReaderSideDone) == 1)
        {
            TryReturnPipe(_responsePipe);
        }
    }

    internal void Configure(Action<HttpContext, PipeReader> configureContext)
    {
        ArgumentNullException.ThrowIfNull(configureContext);

        configureContext(_httpContext, _requestPipe.Reader);
    }

    internal void SendRequestStream(Func<PipeWriter, Task> sendRequestStream)
    {
        ArgumentNullException.ThrowIfNull(sendRequestStream);

        _sendRequestStream = sendRequestStream;
    }

    internal void RegisterResponseReadCompleteCallback(Action<HttpContext> responseReadCompleteCallback)
    {
        _responseReadCompleteCallback = responseReadCompleteCallback;
    }

    /// <summary>
    /// Start processing the request.
    /// </summary>
    /// <returns></returns>
    internal Task<HttpContext> SendAsync(CancellationToken cancellationToken)
    {
        var registration = cancellationToken.Register(ClientInitiatedAbort);

        // Everything inside this function happens in the SERVER's execution context (unless PreserveExecutionContext is true)
        async Task RunRequestAsync()
        {
            // HTTP/2 specific features must be added after the request has been configured.
            if (HttpProtocol.IsHttp2(_httpContext.Request.Protocol) ||
                HttpProtocol.IsHttp3(_httpContext.Request.Protocol))
            {
                _httpContext.Features.Set<IHttpResetFeature>(this);
            }

            // This will configure IHttpContextAccessor so it needs to happen INSIDE this function,
            // since we are now inside the Server's execution context. If it happens outside this cont
            // it will be lost when we abandon the execution context.
            _testContext = _application.CreateContext(_httpContext.Features);
            try
            {
                if (_sendRequestStream != null)
                {
                    // Read content into a pipe in a background task.
                    // A background task allows duplex streaming scenarios.
                    var requestTask = _sendRequestStream(_requestPipe.Writer);
                    // Observe synchronous exceptions immediately.
                    if (requestTask.IsCompleted)
                    {
                        await requestTask;
                    }
                }
                else
                {
                    // There's no request content (e.g. a GET request), so nothing will ever complete
                    // _requestPipe.Writer. Complete it immediately so the reader side observes an
                    // empty, completed body rather than leaving the pipe in a permanently
                    // not-quite-finished state (which corrupts Pipe.Reset()-pooled instances since
                    // PipeOperationState is not cleared by Reset()).
                    await _requestPipe.Writer.CompleteAsync();
                }

                await _application.ProcessRequestAsync(_testContext);

                // Determine whether request body was complete when the delegate exited.
                // This could throw an error if there was a pending server read. Needs to
                // happen before completing the response so the response returns the error.
                var requestBodyInProgress = RequestBodyReadInProgress();
                if (requestBodyInProgress)
                {
                    // If request is still in progress then abort it.
                    CancelRequestBody();
                }

                // Matches Kestrel server: response is completed before request is drained
                await CompleteResponseAsync();

                if (!requestBodyInProgress)
                {
                    // Atomically claim exclusive ownership of the request Pipe before touching it further. If
                    // CancelRequestBody (fired concurrently by ClientInitiatedAbort/Abort) wins this race
                    // instead, skip completing/returning the pipe entirely - it's now that code's
                    // responsibility, and touching it here too could corrupt whatever CancelRequestBody is
                    // doing. See _requestPipeClaim's doc comment for the full race this closes.
                    if (Interlocked.CompareExchange(ref _requestPipeClaim, RequestPipeClaimedForReturn, RequestPipeUnclaimed) == RequestPipeUnclaimed)
                    {
                        // Writer was already completed in send request callback.
                        await _requestPipe.Reader.CompleteAsync();

                        TryReturnPipe(_requestPipe);
                    }

                    // Don't wait for request to drain. It could block indefinitely. In a real server
                    // we would wait for a timeout and then kill the socket.
                    // Potential future improvement: add logging that the request timed out
                }

                _application.DisposeContext(_testContext, exception: null);
            }
            catch (Exception ex)
            {
                Abort(ex);
                _application.DisposeContext(_testContext, ex);
            }
            finally
            {
                registration.Dispose();
            }
        }

        // Async offload, don't let the test code block the caller.
        if (_preserveExecutionContext)
        {
            _ = Task.Factory.StartNew(RunRequestAsync, default, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }
        else
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                _ = RunRequestAsync();
            }, null);
        }

        return _responseTcs.Task;
    }

    // Triggered by request CancellationToken canceling or response stream Disposal.
    internal void ClientInitiatedAbort()
    {
        if (!_pipelineFinished)
        {
            // We don't want to trigger the token for already completed responses.
            _requestLifetimeFeature.Cancel();
        }

        // Writes will still succeed, the app will only get an error if they check the CT.
        _responseReaderStream.Abort(new IOException("The client aborted the request."));

        // Cancel any pending request async activity when the client aborts a duplex
        // streaming scenario by disposing the HttpResponseMessage.
        CancelRequestBody();
    }

    private void ResponseBodyReadComplete()
    {
        _responseReadCompleteCallback?.Invoke(_httpContext);
    }

    private bool RequestBodyReadInProgress()
    {
        try
        {
            return !_requestPipe.Reader.TryRead(out var result) || !result.IsCompleted;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An error occurred when completing the request. Request delegate may have finished while there is a pending read of the request body.", ex);
        }
    }

    internal async Task CompleteResponseAsync()
    {
        _pipelineFinished = true;
        await ReturnResponseMessageAsync();
        _responsePipeWriter.Complete();
        ResponsePipeWriterComplete();
        await _responseFeature.FireOnResponseCompletedAsync();
    }

    internal async Task ReturnResponseMessageAsync()
    {
        // Check if the response is already returning because the TrySetResult below could happen a bit late
        // (as it happens on a different thread) by which point the CompleteResponseAsync could run and calls this
        // method again.
        if (!_returningResponse)
        {
            _returningResponse = true;

            try
            {
                await _responseFeature.FireOnSendingHeadersAsync();
            }
            catch (Exception ex)
            {
                Abort(ex);
                return;
            }

            // Copy the feature collection so we're not multi-threading on the same collection.
            var newFeatures = new FeatureCollection();
            foreach (var pair in _httpContext.Features)
            {
                newFeatures[pair.Key] = pair.Value;
            }
            var serverResponseFeature = _httpContext.Features.GetRequiredFeature<IHttpResponseFeature>();
            // The client gets a deep copy of this so they can interact with the body stream independently of the server.
            var clientResponseFeature = new HttpResponseFeature()
            {
                StatusCode = serverResponseFeature.StatusCode,
                ReasonPhrase = serverResponseFeature.ReasonPhrase,
                Headers = serverResponseFeature.Headers,
                Body = _responseReaderStream
            };
            newFeatures.Set<IHttpResponseFeature>(clientResponseFeature);
            newFeatures.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(_responseReaderStream));
            _responseTcs.TrySetResult(new DefaultHttpContext(newFeatures));
        }
    }

    internal void Abort(Exception exception)
    {
        _responsePipeWriter.Abort(exception);
        ResponsePipeWriterComplete();
        _responseReaderStream.Abort(exception);
        _requestLifetimeFeature.Cancel();
        _responseTcs.TrySetException(exception);
        CancelRequestBody();
    }

    private void CancelRequestBody()
    {
        // Atomically claim exclusive ownership of the request Pipe before touching it. If RunRequestAsync's
        // normal completion path wins this race instead (or already has), the pipe may already have been
        // returned to the pool and rented out to a completely different, unrelated HttpContextBuilder -
        // calling CancelPendingRead/CancelPendingFlush on it here would corrupt that unrelated request. See
        // _requestPipeClaim's doc comment for the full race this closes.
        if (Interlocked.CompareExchange(ref _requestPipeClaim, RequestPipeClaimedForCancel, RequestPipeUnclaimed) != RequestPipeUnclaimed)
        {
            return;
        }

        _requestPipe.Writer.CancelPendingFlush();
        _requestPipe.Reader.CancelPendingRead();
    }

    void IHttpResetFeature.Reset(int errorCode)
    {
        Abort(new HttpResetTestException(errorCode));
    }
}
