namespace RcCar.Core.Models;

public readonly record struct DriveState
{
    public ThrottleDirection Throttle { get; }
    public SteeringDirection Steering { get; }
    public byte Speed { get; }
    public bool LightsOn { get; }

    public DriveState(ThrottleDirection throttle, SteeringDirection steering, byte speed, bool lightsOn = false)
    {
        if (!Enum.IsDefined(throttle))
        {
            throw new ArgumentOutOfRangeException(nameof(throttle));
        }

        if (!Enum.IsDefined(steering))
        {
            throw new ArgumentOutOfRangeException(nameof(steering));
        }

        if (speed > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        Throttle = throttle;
        Steering = steering;
        Speed = throttle == ThrottleDirection.Neutral ? (byte)0 : speed;
        LightsOn = lightsOn;
    }

    public static DriveState Neutral => default;

    public override string ToString() => $"{Throttle} / {Steering}, speed {Speed}, lights {(LightsOn ? "on" : "off")}";
}
