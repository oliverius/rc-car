using RcCar.Core.Models;
using RcCar.Core.Services;
using Xunit;

namespace RcCar.Core.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    public long Ticks;
    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => Ticks;
}
