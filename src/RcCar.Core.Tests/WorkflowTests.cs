using System.Collections.Concurrent;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using RcCar.Core.Protocol;
using RcCar.Core.Services;
using Xunit;

namespace RcCar.Core.Tests;

public sealed class WorkflowTests
{
    private static readonly CarIdentity Vehicle = new(0x11, 0x22, 0x33);

    private static CarService Service(FakeBleTransport radio) => new(
        radio, radio,
        new CarProtocol(new(0x61, 0x62, 0x63)),
        new DiscardingSessionEvents(),
        new CarServiceOptions
        {
            DiscoveryWindow = TimeSpan.FromMilliseconds(30),
            PairingWindow = TimeSpan.FromMilliseconds(40),
            RefreshPeriod = TimeSpan.FromMilliseconds(2),
            PulseDuration = TimeSpan.FromMilliseconds(2),
            InputPollPeriod = TimeSpan.FromMilliseconds(1),
            CleanupRepetitions = 3
        });
    [Fact]
    public async Task DiscoveryCollectsMultipleCarsWithoutControlPackets()
    {
        var radio = new FakeBleTransport();
        radio.Replies.Add(new(0x44, 0x55, 0x66));
        var found = await Service(radio).DiscoverAsync(default);
        Assert.Equal(2, found.Count);
        Assert.All(radio.Packets, payload => Assert.Equal(6, payload[1]));
    }

    [Fact]
    public async Task UnmatchedTargetNeverSendsControl()
    {
        var radio = new FakeBleTransport();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(radio).RunTestAsync(new(9, 9, 9), DiagnosticTest.Forward, default));
        Assert.All(radio.Packets, payload => Assert.Equal(6, payload[1]));
    }

    [Theory]
    [InlineData(DiagnosticTest.Forward, 1)]
    [InlineData(DiagnosticTest.Reverse, 2)]
    public async Task TestUsesLearnedIdentityAndOnePulseFollowedByNeutral(DiagnosticTest test, int direction)
    {
        var radio = new FakeBleTransport();
        await Service(radio).RunTestAsync(Vehicle, test, default);
        var controls = radio.Packets.Where(payload => payload[1] == 7).ToArray();
        Assert.Equal(5, controls.Length); // Initial neutral, pulse, three cleanup packets.
        Assert.Equal(direction, controls[1][8]);
        Assert.Equal(20, controls[1][9]);
        Assert.All(controls, payload => Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, payload[2..5]));
        Assert.All(controls.Skip(2), payload => Assert.Equal(0, payload[8]));
        Assert.Equal(1, radio.MaximumPublishers);
    }

    [Fact]
    public async Task SteeringHasTwoNeutralStatesBetweenDirections()
    {
        var radio = new FakeBleTransport();
        await Service(radio).RunTestAsync(Vehicle, DiagnosticTest.Steering, default);
        var controls = radio.Packets.Where(payload => payload[1] == 7).ToArray();
        Assert.Equal(new byte[] { 0, 4, 0, 0, 8, 0, 0, 0 }, controls.Select(payload => payload[8]).ToArray());
        Assert.All(controls, payload => Assert.Equal(0, payload[9]));
    }

    [Fact]
    public async Task CancelDuringDrivingStopsPublisherThenSendsNeutralDespiteCancelledToken()
    {
        var radio = new FakeBleTransport
        {
            BlockMovement = true
        };
        using var cancel = new CancellationTokenSource();
        var driving = Service(radio).DriveAsync(Vehicle, new ForwardControlInput(), cancel.Token);
        await radio.Moving.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => driving);
        Assert.All(radio.Packets.TakeLast(3), payload =>
        {
            Assert.Equal(7, payload[1]);
            Assert.Equal(0, payload[8]);
        });
        Assert.Equal(0, radio.ActivePublishers);
        Assert.Equal(1, radio.MaximumPublishers);
    }

    [Fact]
    public async Task MovementFailureStillAttemptsNeutral()
    {
        var radio = new FakeBleTransport
        {
            FailMovement = true
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(radio).RunTestAsync(Vehicle, DiagnosticTest.Forward, default));
        Assert.All(radio.Packets.TakeLast(3), payload => Assert.Equal(0, payload[8]));
    }

    [Fact]
    public async Task CleanupFailureIsReportedRatherThanSuccess()
    {
        var radio = new FakeBleTransport
        {
            FailCleanup = true
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(radio).RunTestAsync(Vehicle, DiagnosticTest.Forward, default));
    }

    [Fact]
    public async Task WatcherFailurePreventsMovement()
    {
        var radio = new FakeBleTransport
        {
            FailWatcher = true
        };
        radio.Replies.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => Service(radio).RunTestAsync(Vehicle, DiagnosticTest.Forward, default));
        Assert.All(radio.Packets, payload => Assert.Equal(6, payload[1]));
    }

    [Fact]
    public async Task ConcurrentWorkflowIsRejectedAndCancellationReleasesOwnership()
    {
        var radio = new FakeBleTransport
        {
            BlockMovement = true
        };
        var service = Service(radio);
        using var cancel = new CancellationTokenSource();
        var driving = service.DriveAsync(Vehicle, new ForwardControlInput(), cancel.Token);
        await radio.Moving.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DiscoverAsync(default));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => driving);
        Assert.Single(await service.DiscoverAsync(default));
    }
}
