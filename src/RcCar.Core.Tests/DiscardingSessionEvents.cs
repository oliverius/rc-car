using System.Collections.Concurrent;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using RcCar.Core.Protocol;
using RcCar.Core.Services;
using Xunit;

namespace RcCar.Core.Tests;

internal sealed class DiscardingSessionEvents : ISessionEvents
{
    public void Write(SessionEventKind kind, string message)
    {
    }
}
