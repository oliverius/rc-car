using System.Collections.Concurrent;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using RcCar.Core.Protocol;
using RcCar.Core.Services;
using Xunit;

namespace RcCar.Core.Tests;

internal sealed class ForwardControlInput : IControlInput
{
    public DriveState Read() => new(ThrottleDirection.Forward, SteeringDirection.Centre, 40);
}
