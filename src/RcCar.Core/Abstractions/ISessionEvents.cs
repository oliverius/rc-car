using RcCar.Core.Models;

namespace RcCar.Core.Abstractions;

public interface ISessionEvents
{
    void Write(SessionEventKind kind, string message);
}
