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

            state = enabled && armed ? new DriveState(throttle, steering, gear.Speed()) : DriveState.Neutral;
            lastUpdate = time.GetTimestamp();
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            armed = false;
            spaceHeld = true;
            state = DriveState.Neutral;
        }
    }

    public DriveState Read()
    {
        lock (gate)
        {
            // Treat missing UI samples as an expired input lease, not a request
            // to keep driving with the last keys we saw.
            if (time.GetElapsedTime(lastUpdate) > TimeSpan.FromMilliseconds(250))
            {
                Reset();
            }

            return state;
        }
    }
}
