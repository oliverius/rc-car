namespace RcCar.Core.Models;

public readonly record struct DriveState
{
    public ThrottleDirection Throttle { get; }
    public SteeringDirection Steering { get; }
    public byte Speed { get; }

    public DriveState(ThrottleDirection throttle, SteeringDirection steering, byte speed)
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
    }

    public static DriveState Neutral => default;

    public override string ToString() => $"{Throttle} / {Steering}, speed {Speed}";
}
