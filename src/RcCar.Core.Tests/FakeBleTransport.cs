using System.Collections.Concurrent;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using RcCar.Core.Protocol;
using RcCar.Core.Services;
using Xunit;

namespace RcCar.Core.Tests;

internal sealed class FakeBleTransport : IAdvertisementReceiver, IAdvertisementTransmitter
{
    public readonly ConcurrentQueue<byte[]> Packets = new();
    public readonly TaskCompletionSource Moving = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource Watching = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource watcherFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Action<Advertisement>? receive;

    public List<CarIdentity> Replies { get; } = new()
    {
        new CarIdentity(0x11, 0x22, 0x33)
    };

    public bool FailMovement;
    public bool FailCleanup;
    public bool FailWatcher;
    public bool BlockMovement;
    public int ActivePublishers;
    public int MaximumPublishers;
    public async Task WatchAsync(Action<Advertisement> onReceived, CancellationToken token)
    {
        receive = onReceived;
        Watching.TrySetResult();
        await watcherFailure.Task.WaitAsync(token);
        throw new InvalidOperationException("Watcher failed");
    }

    public async Task AdvertiseAsync(ushort companyId, byte[] payload, TimeSpan duration, Action? started, CancellationToken token)
    {
        MaximumPublishers = Math.Max(MaximumPublishers, Interlocked.Increment(ref ActivePublishers));
        try
        {
            token.ThrowIfCancellationRequested();
            Packets.Enqueue((byte[])payload.Clone());
            started?.Invoke();
            if (payload[1] == 6)
            {
                foreach (var identity in Replies)
                {
                    var reply = new byte[19];
                    reply[0] = 4;
                    reply[1] = 10;
                    identity.CopyTo(reply.AsSpan(2));
                    payload.AsSpan(5, 3).CopyTo(reply.AsSpan(5));
                    receive?.Invoke(new(0, reply, identity.First, -40));
                }

                if (FailWatcher)
                {
                    watcherFailure.TrySetResult();
                    await Task.Delay(100, token);
                }
            }
            else if (payload[8] != 0)
            {
                Moving.TrySetResult();
                if (FailMovement)
                {
                    throw new InvalidOperationException("Movement publisher failed");
                }

                if (BlockMovement)
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
            }
            else if (FailCleanup && Moving.Task.IsCompleted)
            {
                throw new InvalidOperationException("Neutral publisher failed");
            }

            await Task.Delay(duration, token);
        }
        finally
        {
            Interlocked.Decrement(ref ActivePublishers);
        }
    }
}
