using RcCar.Core.Models;

namespace RcCar.Core.Abstractions;

public interface IControlInput
{
    DriveState Read();
}
