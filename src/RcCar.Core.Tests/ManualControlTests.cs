using RcCar.Core.Models;
using RcCar.Core.Services;
using Xunit;

namespace RcCar.Core.Tests;

public sealed class ManualControlTests
{

    private static readonly HeldControls Forward = new(true, false, false, false, false);
    [Fact]
    public void ReleaseAndFocusLossSendNeutralAndHeldKeysCannotResume()
    {
        var controls = new ManualControlInput();
        controls.Update(true, Forward);
        Assert.Equal(DriveState.Neutral, controls.Read());
        controls.Update(true, default);
        controls.Update(true, Forward);
        Assert.Equal(ThrottleDirection.Forward, controls.Read().Throttle);
        controls.Update(true, default);
        Assert.Equal(DriveState.Neutral, controls.Read());
        controls.Update(true, Forward);
        controls.Update(false, Forward);
        controls.Update(true, Forward);
        Assert.Equal(DriveState.Neutral, controls.Read());
    }

    [Fact]
    public void StaleHeartbeatNeutralizesAndRequiresRelease()
    {
        var clock = new ManualTimeProvider();
        var controls = new ManualControlInput(clock);
        controls.Update(true, default);
        controls.Update(true, Forward);
        clock.Ticks = 251;
        Assert.Equal(DriveState.Neutral, controls.Read());
        controls.Update(true, Forward);
        Assert.Equal(DriveState.Neutral, controls.Read());
    }

    [Fact]
    public void SpaceCyclesOncePerPressDuringDrivingAndWraps()
    {
        var controls = new ManualControlInput();
        controls.Update(true, default);
        controls.Update(true, Forward);
        Assert.Equal(40, controls.Read().Speed);
        controls.Update(true, Forward with
        {
            ChangeGear = true
        });
        controls.Update(true, Forward with
        {
            ChangeGear = true
        });
        Assert.Equal(70, controls.Read().Speed);
        controls.Update(true, Forward);
        controls.Update(true, Forward with
        {
            ChangeGear = true
        });
        Assert.Equal(100, controls.Read().Speed);
        controls.Update(true, Forward);
        controls.Update(true, Forward with
        {
            ChangeGear = true
        });
        Assert.Equal(40, controls.Read().Speed);
        Assert.Equal(ThrottleDirection.Forward, controls.Read().Throttle);
    }

    [Fact]
    public void OppositeKeysCancelAndShiftingWhileIdleDoesNotMove()
    {
        var controls = new ManualControlInput();
        controls.Update(true, default);
        controls.Update(true, new(true, true, true, true, false));
        Assert.Equal(DriveState.Neutral, controls.Read());
        controls.Update(true, new(false, false, false, false, true));
        Assert.Equal(Gear.Second, controls.SelectedGear);
        Assert.Equal(DriveState.Neutral, controls.Read());
    }

    [Fact]
    public void EnterTogglesLightsOncePerPressAndResetTurnsThemOff()
    {
        var controls = new ManualControlInput();
        controls.Update(true, default);
        controls.Update(true, new(false, false, false, false, false, true));
        Assert.True(controls.Read().LightsOn);

        controls.Update(true, new(false, false, false, false, false, true));
        Assert.True(controls.Read().LightsOn);

        controls.Update(true, default);
        controls.Update(true, new(false, false, false, false, false, true));
        Assert.False(controls.Read().LightsOn);

        controls.Update(true, default);
        controls.Update(true, new(false, false, false, false, false, true));
        controls.Update(false, default);
        Assert.Equal(DriveState.Neutral, controls.Read());
    }
}
