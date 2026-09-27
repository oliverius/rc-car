using RcCar.Core.Models;

namespace RcCar.Core.Abstractions;

public interface IAdvertisementTransmitter
{
    /// <summary>Holds one advertisement from Started, then confirms stop before returning.</summary>
    Task AdvertiseAsync(ushort companyId, byte[] payload, TimeSpan duration, Action? onStarted, CancellationToken cancellationToken);
}
