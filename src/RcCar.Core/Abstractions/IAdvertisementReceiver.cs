using RcCar.Core.Models;

namespace RcCar.Core.Abstractions;

public interface IAdvertisementReceiver
{
    /// <summary>Receives until cancellation; unexpected watcher termination must fail the task.</summary>
    Task WatchAsync(Action<Advertisement> onReceived, CancellationToken cancellationToken);
}
