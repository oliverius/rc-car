namespace RcCar.Core.Models;

public static class GearExtensions
{
    public static byte Speed(this Gear gear) => gear switch
    {
        Gear.First => 40,
        Gear.Second => 70,
        Gear.Third => 100,
        _ => throw new ArgumentOutOfRangeException(nameof(gear))
    };
    public static Gear Next(this Gear gear) => gear switch
    {
        Gear.First => Gear.Second,
        Gear.Second => Gear.Third,
        Gear.Third => Gear.First,
        _ => throw new ArgumentOutOfRangeException(nameof(gear))
    };
}
