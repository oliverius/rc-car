namespace RcCar.Core.Services;

public sealed record CarServiceOptions
{
    public TimeSpan DiscoveryWindow { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan PairingWindow { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan RefreshPeriod { get; init; } = TimeSpan.FromMilliseconds(600);
    public TimeSpan InputPollPeriod { get; init; } = TimeSpan.FromMilliseconds(20);
    public TimeSpan PulseDuration { get; init; } = TimeSpan.FromMilliseconds(300);
    public int CleanupRepetitions { get; init; } = 5;

    internal void Validate()
    {
        if (DiscoveryWindow <= TimeSpan.Zero ||
            PairingWindow <= TimeSpan.Zero ||
            RefreshPeriod <= TimeSpan.Zero ||
            InputPollPeriod <= TimeSpan.Zero ||
            PulseDuration <= TimeSpan.Zero ||
            CleanupRepetitions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CarServiceOptions), "Durations and cleanup repetitions must be positive.");
        }
    }
}
