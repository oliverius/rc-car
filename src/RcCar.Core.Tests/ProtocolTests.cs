using RcCar.Core.Models;
using RcCar.Core.Protocol;
using Xunit;

namespace RcCar.Core.Tests;

public sealed class ProtocolTests
{
    private readonly CarProtocol protocol = new(new(0x61, 0x62, 0x63));
    private static readonly CarIdentity Vehicle = new(0xF5, 0x71, 0xCD);

    [Fact]
    public void PairingRequestMatchesCapturedRequest() =>
        Assert.Equal("0406000000616263000000000000000000008B", Convert.ToHexString(protocol.PairingRequest()));

    [Theory]
    [InlineData(ThrottleDirection.Neutral, SteeringDirection.Centre, "0407F571CD61626300000064000001000000A6")]
    [InlineData(ThrottleDirection.Forward, SteeringDirection.Centre, "0407F571CD61626301140164000001000000B2")]
    [InlineData(ThrottleDirection.Reverse, SteeringDirection.Centre, "0407F571CD61626302140064000001000000B0")]
    [InlineData(ThrottleDirection.Neutral, SteeringDirection.Left, "0407F571CD61626304000064000001000000A2")]
    [InlineData(ThrottleDirection.Neutral, SteeringDirection.Right, "0407F571CD61626308000064000001000000AE")]
    [InlineData(ThrottleDirection.Forward, SteeringDirection.Left, "0407F571CD61626305140164000001000000B6")]
    [InlineData(ThrottleDirection.Reverse, SteeringDirection.Right, "0407F571CD6162630A140064000001000000B8")]
    public void ControlMatchesIndependentVectors(ThrottleDirection throttle, SteeringDirection steering, string expected) =>
        Assert.Equal(expected, Convert.ToHexString(protocol.Control(Vehicle, new(throttle, steering, 20), 1)));

    [Theory]
    [InlineData(ThrottleDirection.Neutral, "0407F571CD61626300000464000001000000A2")]
    [InlineData(ThrottleDirection.Forward, "0407F571CD61626301140564000001000000B6")]
    public void ControlEncodesTestedLightFlagWithoutChangingOtherFields(
        ThrottleDirection throttle,
        string expected) =>
        Assert.Equal(
            expected,
            Convert.ToHexString(protocol.Control(Vehicle, new(throttle, SteeringDirection.Centre, 20, lightsOn: true), 1)));

    [Fact]
    public void EveryGearAndCounterHasValidChecksumAndNeutralHasNoThrottle()
    {
        foreach (var gear in Enum.GetValues<Gear>())
        {
            foreach (var throttle in Enum.GetValues<ThrottleDirection>())
            {
                foreach (var steering in Enum.GetValues<SteeringDirection>())
                {
                    for (var counter = 1; counter <= 250; counter++)
                    {
                        var packet = protocol.Control(Vehicle, new(throttle, steering, gear.Speed()), (byte)counter);
                        Assert.Equal(0xE9, packet.Aggregate(0, (checksum, item) => checksum ^ item));
                        Assert.Equal(throttle == ThrottleDirection.Neutral ? 0 : gear.Speed(), packet[9]);
                    }
                }
            }
        }
    }

    [Fact]
    public void LearnsAnArbitraryIdentityRatherThanTheOriginalCar()
    {
        var reply = new Advertisement(0, Convert.FromHexString("040A11223361626300000000000000000000FF"), 1, -50);
        Assert.True(protocol.TryReadPairingReply(reply, out var identity));
        Assert.Equal(new CarIdentity(0x11, 0x22, 0x33), identity);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, protocol.Control(identity, DriveState.Neutral, 1)[2..5]);
    }

    [Fact]
    public void RejectsWrongClientCompanyOpcodeAndLengthButDoesNotInventReplyChecksum()
    {
        var data = Convert.FromHexString("040AF571CD61626300000000000000000000FF");
        Assert.True(protocol.TryReadPairingReply(new(0, data, 0, 0), out _));
        Assert.False(protocol.TryReadPairingReply(new(1, data, 0, 0), out _));
        for (var length = 0; length < 19; length++)
        {
            Assert.False(protocol.TryReadPairingReply(new(0, data[..length], 0, 0), out _));
        }

        Assert.False(protocol.TryReadPairingReply(new(0, data.Concat(new byte[] { 0 }).ToArray(), 0, 0), out _));
        foreach (var index in new[] { 0, 1, 5, 6, 7 })
        {
            var changed = (byte[])data.Clone();
            changed[index] ^= 1;
            Assert.False(protocol.TryReadPairingReply(new(0, changed, 0, 0), out _));
        }

        Assert.False(protocol.TryReadPairingReply(new(0, protocol.PairingRequest(), 0, 0), out _));
    }

    [Fact]
    public void RejectsInvalidDomainValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DriveState((ThrottleDirection)99, SteeringDirection.Centre, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DriveState(ThrottleDirection.Forward, SteeringDirection.Centre, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => protocol.Control(Vehicle, DriveState.Neutral, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => protocol.Control(Vehicle, DriveState.Neutral, 251));
    }
}
