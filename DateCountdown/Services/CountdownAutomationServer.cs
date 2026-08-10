using DateCountdown.Core;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace DateCountdown.Services;

internal sealed class CountdownAutomationServer : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Func<CountdownAutomationRequest, Task<CountdownAutomationResponse>> _requestHandler;
    private readonly EventWaitHandle _presenceEvent;
    private NamedPipeServerStream? _activePipe;
    private Task? _runTask;
    private bool _isDisposed;
    private bool _isStarted;

    public CountdownAutomationServer(
        DispatcherQueue dispatcherQueue,
        Func<CountdownAutomationRequest, Task<CountdownAutomationResponse>> requestHandler)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
        _presenceEvent = new EventWaitHandle(
            initialState: true,
            EventResetMode.ManualReset,
            CountdownAutomationProtocol.HostPresenceEventName);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _runTask = RunAsync(_cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _presenceEvent.Reset();
        _cancellationTokenSource.Cancel();
        _activePipe?.Dispose();
        _presenceEvent.Dispose();

        Task? runTask = _runTask;
        if (runTask is null || runTask.IsCompleted)
        {
            _cancellationTokenSource.Dispose();
        }
        else
        {
            _ = runTask.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                _cancellationTokenSource,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using NamedPipeServerStream pipe = new(
                    CountdownAutomationProtocol.PipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                _activePipe = pipe;
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using CancellationTokenSource requestTimeoutSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeoutSource.CancelAfter(RequestTimeout);
                CancellationToken requestToken = requestTimeoutSource.Token;
                CountdownAutomationRequest request = await CountdownAutomationProtocol
                    .ReadRequestAsync(pipe, requestToken)
                    .ConfigureAwait(false);
                CountdownAutomationResponse response = await DispatchAsync(request)
                    .WaitAsync(requestToken)
                    .ConfigureAwait(false);
                await CountdownAutomationProtocol
                    .WriteResponseAsync(pipe, response, requestToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Countdown automation request failed: {ex.GetType().Name}");
            }
            finally
            {
                _activePipe = null;
            }
        }
    }

    private Task<CountdownAutomationResponse> DispatchAsync(CountdownAutomationRequest request)
    {
        TaskCompletionSource<CountdownAutomationResponse> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                CountdownAutomationResponse response = await _requestHandler(request);
                completion.TrySetResult(response);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Countdown automation handler failed: {ex.GetType().Name}");
                completion.TrySetResult(new CountdownAutomationResponse(false, "InternalError"));
            }
        });

        if (!enqueued)
        {
            completion.TrySetResult(new CountdownAutomationResponse(false, "HostUnavailable"));
        }

        return completion.Task;
    }
}
