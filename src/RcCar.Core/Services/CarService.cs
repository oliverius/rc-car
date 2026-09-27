using System.Collections.Concurrent;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using RcCar.Core.Protocol;

namespace RcCar.Core.Services;

/// <summary>Owns one radio workflow at a time; every motion session freshly pairs and ends in neutral.</summary>
public sealed class CarService : ICarService
{
    private readonly IAdvertisementTransmitter transmitter;
    private readonly IAdvertisementReceiver receiver;
    private readonly ISessionEvents events;
    private readonly CarProtocol protocol;
    private readonly CarServiceOptions options;
    private readonly SemaphoreSlim operation = new(1, 1);
    private byte counter;

    public CarService(
        IAdvertisementTransmitter transmitter,
        IAdvertisementReceiver receiver,
        CarProtocol protocol,
        ISessionEvents events,
        CarServiceOptions? options = null)
    {
        this.transmitter = transmitter;
        this.receiver = receiver;
        this.protocol = protocol;
        this.events = events;
        this.options = options ?? new CarServiceOptions();
        this.options.Validate();
    }

    public async Task<IReadOnlyList<CarCandidate>> DiscoverAsync(CancellationToken cancellationToken)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidates = await PairingWindowAsync(null, cancellationToken).ConfigureAwait(false);
            events.Write(SessionEventKind.Information, $"Discovery completed: {candidates.Count} identity/address candidates. No movement sent.");
            return candidates;
        }
        finally
        {
            operation.Release();
        }
    }

    public Task RunTestAsync(CarIdentity target, DiagnosticTest test, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(test))
        {
            throw new ArgumentOutOfRangeException(nameof(test));
        }

        return RunMotionAsync(target, async token =>
        {
            events.Write(SessionEventKind.Information, $"Starting {test} diagnostic; throttle speed 20, no physical success assumed.");
            if (test == DiagnosticTest.Steering)
            {
                await SendAsync(target, new(ThrottleDirection.Neutral, SteeringDirection.Left, 0), options.RefreshPeriod, token).ConfigureAwait(false);
                for (var i = 0; i < 2; i++)
                {
                    await SendAsync(target, DriveState.Neutral, options.RefreshPeriod, token).ConfigureAwait(false);
                }

                await SendAsync(target, new(ThrottleDirection.Neutral, SteeringDirection.Right, 0), options.RefreshPeriod, token).ConfigureAwait(false);
            }
            else
            {
                var direction = test == DiagnosticTest.Forward ? ThrottleDirection.Forward : ThrottleDirection.Reverse;
                await SendAsync(target, new(direction, SteeringDirection.Centre, 20), options.PulseDuration, token).ConfigureAwait(false);
            }
        }, cancellationToken);
    }

    public Task DriveAsync(CarIdentity target, IControlInput input, CancellationToken cancellationToken) => RunMotionAsync(target, async token =>
    {
        events.Write(SessionEventKind.DrivingReady, "Keyboard driving ready. Release all arrows before driving. Space changes gear.");
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var state = input.Read();
            using var change = CancellationTokenSource.CreateLinkedTokenSource(token);
            var advertising = SendAsync(target, state, options.RefreshPeriod, change.Token);
            try
            {
                while (!advertising.IsCompleted)
                {
                    await Task.WhenAny(advertising, Task.Delay(options.InputPollPeriod, token)).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (input.Read() != state)
                    {
                        change.Cancel();
                        break;
                    }
                }
            }
            finally
            {
                // Always observe and stop the current publisher before another state or cleanup.
                change.Cancel();
                try
                {
                    await advertising.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested && change.IsCancellationRequested)
                {
                }
            }
        }
    }, cancellationToken);

    private async Task RunMotionAsync(CarIdentity target, Func<CancellationToken, Task> action, CancellationToken token)
    {
        await EnterAsync(token).ConfigureAwait(false);
        var paired = false;
        Exception? failure = null;
        try
        {
            events.Write(SessionEventKind.Information, $"Fresh pairing required for {target}. Turn the car OFF, then ON at PUBLISHER READY.");
            var candidates = await PairingWindowAsync(target, token).ConfigureAwait(false);
            if (!candidates.Any(candidate => candidate.Identity == target))
            {
                throw new InvalidOperationException($"No fresh reply from {target}; no control packets sent.");
            }

            paired = true;
            counter = 0;
            await SendAsync(target, DriveState.Neutral, options.RefreshPeriod, token).ConfigureAwait(false);
            await action(token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                if (paired)
                {
                    // The session token may already be cancelled. Cleanup must
                    // still transmit neutral before releasing workflow ownership.
                    events.Write(SessionEventKind.Information, "Sending final neutral; keep the car's power switch accessible.");
                    for (var i = 0; i < options.CleanupRepetitions; i++)
                    {
                        await SendAsync(target, DriveState.Neutral, options.RefreshPeriod, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                events.Write(SessionEventKind.Error, "Neutral cleanup failed. Turn the car OFF. " + exception.Message);
                failure = failure is null ? exception : new AggregateException(failure, exception);
            }
            finally
            {
                operation.Release();
            }
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        events.Write(SessionEventKind.Information, "Transmission sequence completed; physical response must be recorded separately.");
    }

    private async Task<IReadOnlyList<CarCandidate>> PairingWindowAsync(CarIdentity? target, CancellationToken token)
    {
        var candidates = new ConcurrentDictionary<(CarIdentity, ulong), CarCandidate>();
        var matched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
        // WinRT invokes receive callbacks from another thread. Accept replies
        // only after publishing starts, and stop accepting before teardown.
        var accepting = 0;
        void OnReceived(Advertisement advertisement)
        {
            if (Volatile.Read(ref accepting) == 0 || !protocol.TryReadPairingReply(advertisement, out var identity))
            {
                return;
            }

            var candidate = new CarCandidate(identity, advertisement.Address, advertisement.Rssi);
            candidates[(identity, advertisement.Address)] = candidate;
            events.Write(SessionEventKind.PairMatched, $"PAIR MATCH {candidate.DisplayName}; payload={CarProtocol.Hex(advertisement.Payload)}");
            if (target == identity)
            {
                matched.TrySetResult();
            }
        }

        Task watching = receiver.WatchAsync(OnReceived, window.Token);
        Task advertising = Task.CompletedTask;
        try
        {
            // Catch synchronous/startup receiver failures before publishing anything.
            if (watching.IsCompleted)
            {
                await RequireActiveWatcherAsync(watching).ConfigureAwait(false);
            }

            var payload = protocol.PairingRequest();
            events.Write(SessionEventKind.Transmission, $"TX PAIR company=0000 payload={CarProtocol.Hex(payload)}");
            var duration = target is null ? options.DiscoveryWindow : options.PairingWindow;
            advertising = transmitter.AdvertiseAsync(CarProtocol.CompanyId, payload, duration, () =>
            {
                Volatile.Write(ref accepting, 1);
                events.Write(SessionEventKind.PublisherReady, "PUBLISHER READY — turn the car ON now.");
            }, window.Token);
            // Discovery collects replies for the whole window. A motion session
            // can finish pairing as soon as the selected identity replies.
            await Task.WhenAny(advertising, watching, matched.Task).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (watching.IsCompleted)
            {
                await RequireActiveWatcherAsync(watching).ConfigureAwait(false);
            }
        }
        finally
        {
            Volatile.Write(ref accepting, 0);
            window.Cancel();
            // Observe both tasks, including stop failures. Cancellation is expected here only.
            var errors = new List<Exception>();
            foreach (var task in new[] { advertising, watching })
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (window.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("Pairing radio operation failed.", errors);
            }
        }

        token.ThrowIfCancellationRequested();
        return candidates.Values.OrderByDescending(candidate => candidate.Rssi).ToArray();
    }

    private static async Task RequireActiveWatcherAsync(Task watching)
    {
        await watching.ConfigureAwait(false);
        throw new InvalidOperationException("Advertisement watcher stopped unexpectedly.");
    }

    private async Task EnterAsync(CancellationToken token)
    {
        if (!await operation.WaitAsync(0, token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A radio workflow is already active. Stop it before starting another.");
        }
    }

    private Task SendAsync(CarIdentity target, DriveState state, TimeSpan duration, CancellationToken token)
    {
        counter = counter >= 250 ? (byte)1 : (byte)(counter + 1);
        var payload = protocol.Control(target, state, counter);
        events.Write(SessionEventKind.Transmission, $"TX {target}: {state}; counter={counter}; payload={CarProtocol.Hex(payload)}");
        return transmitter.AdvertiseAsync(CarProtocol.CompanyId, payload, duration, null, token);
    }
}
