using RcCar.Core.Abstractions;
using RcCar.Core.Models;

namespace RcCar.Core.Services;

/// <summary>Input lease and gear state, independent of keyboard APIs or a window.</summary>
public sealed class ManualControlInput(TimeProvider? timeProvider = null) : IControlInput
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly object gate = new();
    private bool armed;
    private bool spaceHeld = true;
    private bool enterHeld = true;
    private bool lightsOn;
    private DriveState state;
    private long lastUpdate;
    private Gear gear = Gear.First;

    public Gear SelectedGear
    {
        get
        {
            lock (gate)
            {
                return gear;
            }
        }
    }

    public void Update(bool enabled, HeldControls controls)
    {
        lock (gate)
        {
            // After focus loss or reset, held arrows cannot restart movement.
            // The driver must first release them while input is enabled.
            if (!enabled)
            {
                armed = false;
            }
            else if (!controls.AnyArrow)
            {
                armed = true;
            }

            if (enabled && controls.ChangeGear && !spaceHeld)
            {
                gear = gear.Next();
            }

            spaceHeld = controls.ChangeGear;
            if (enabled && controls.ToggleLights && !enterHeld)
            {
                lightsOn = !lightsOn;
            }

            enterHeld = controls.ToggleLights;
            // Opposing buttons cancel each other, just like releasing both.
            var throttle = (controls.Forward, controls.Reverse) switch
            {
                (true, false) => ThrottleDirection.Forward,
                (false, true) => ThrottleDirection.Reverse,
                _ => ThrottleDirection.Neutral
            };
            var steering = (controls.Left, controls.Right) switch
            {
                (true, false) => SteeringDirection.Left,
                (false, true) => SteeringDirection.Right,
                _ => SteeringDirection.Centre
            };

            state = enabled
                ? new DriveState(
                    armed ? throttle : ThrottleDirection.Neutral,
                    armed ? steering : SteeringDirection.Centre,
                    gear.Speed(),
                    lightsOn)
                : DriveState.Neutral;
            lastUpdate = time.GetTimestamp();
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            armed = false;
            spaceHeld = true;
            enterHeld = true;
            lightsOn = false;
            state = DriveState.Neutral;
        }
    }

    public DriveState Read()
    {
        lock (gate)
        {
            // Expire stale movement input, but preserve the latched light state:
            // a delayed UI sample must not turn the lights off. Throttle and
            // steering are neutralized below, and held keys must be released.
            if (time.GetElapsedTime(lastUpdate) > TimeSpan.FromMilliseconds(250))
            {
                armed = false;
                spaceHeld = true;
                enterHeld = true;
                state = new DriveState(
                    ThrottleDirection.Neutral,
                    SteeringDirection.Centre,
                    0,
                    lightsOn);
            }

            return state;
        }
    }
}
