using RcCar.Core.Models;

namespace RcCar.Core.Abstractions;

public interface IAdapterInspector
{
    Task<AdapterInformation> InspectAsync(CancellationToken cancellationToken);
}
