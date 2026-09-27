using RcCar.Core.Models;

namespace RcCar.Core.Abstractions;

public interface ICarService
{
    Task<IReadOnlyList<CarCandidate>> DiscoverAsync(CancellationToken cancellationToken);
    Task RunTestAsync(CarIdentity target, DiagnosticTest test, CancellationToken cancellationToken);
    Task DriveAsync(CarIdentity target, IControlInput input, CancellationToken cancellationToken);
}
