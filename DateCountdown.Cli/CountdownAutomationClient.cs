using DateCountdown.Core;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace DateCountdown.Cli;

internal sealed class CountdownAutomationClient
{
    private const int ConnectAttemptTimeoutMilliseconds = 250;
    private const int HostConnectAttempts = 12;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public async Task<CountdownAutomationResponse?> TrySendAsync(
        CountdownAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < HostConnectAttempts; attempt++)
        {
            try
            {
                using NamedPipeClientStream pipe = new(
                    ".",
                    CountdownAutomationProtocol.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await pipe
                    .ConnectAsync(ConnectAttemptTimeoutMilliseconds, cancellationToken)
                    .ConfigureAwait(false);

                using CancellationTokenSource requestTimeoutSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeoutSource.CancelAfter(RequestTimeout);
                CancellationToken requestToken = requestTimeoutSource.Token;
                await CountdownAutomationProtocol
                    .WriteRequestAsync(pipe, request, requestToken)
                    .ConfigureAwait(false);
                return await CountdownAutomationProtocol
                    .ReadResponseAsync(pipe, requestToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new CountdownAutomationResponse(false, "HostUnavailable");
            }

            if (!IsHostPresent())
            {
                return null;
            }
        }

        return new CountdownAutomationResponse(false, "HostUnavailable");
    }

    public static bool IsHostPresent()
    {
        if (!EventWaitHandle.TryOpenExisting(
            CountdownAutomationProtocol.HostPresenceEventName,
            out EventWaitHandle? presenceEvent))
        {
            return false;
        }

        using (presenceEvent)
        {
            return presenceEvent.WaitOne(0);
        }
    }
}
