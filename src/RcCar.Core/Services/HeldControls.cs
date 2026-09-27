namespace RcCar.Core.Services;

/// <summary>Physical button states sampled together before input and gear rules are applied.</summary>
public readonly record struct HeldControls(
    bool Forward,
    bool Reverse,
    bool Left,
    bool Right,
    bool ChangeGear,
    bool ToggleLights = false)
{
    public bool AnyArrow => Forward || Reverse || Left || Right;
}
